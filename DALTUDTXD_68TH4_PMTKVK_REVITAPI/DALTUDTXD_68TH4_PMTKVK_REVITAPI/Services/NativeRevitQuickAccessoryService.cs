using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.Services
{
    /// <summary>
    /// Creates a lightweight, database-safe representation of calculated accessories
    /// for Quick Preview. The detailed preview still creates editable RFA instances;
    /// this path deliberately uses only deterministic box solids so a large project can
    /// show props, beam clamps and column yokes without loading/rotating thousands of
    /// FamilyInstances in the user's document.
    /// </summary>
    internal sealed class NativeRevitQuickAccessoryService
    {
        private const int MaximumSolidCount = 24000;
        private const int MaximumGridStationsPerAxis = 80;
        private readonly SharedDataViewModel _data;
        private readonly IReadOnlyDictionary<string, FloorElementModel> _models;
        private readonly HashSet<(long X, long Y, long Z)> _propLocations =
            new HashSet<(long X, long Y, long Z)>();

        internal sealed class BuildResult
        {
            public List<Solid> Solids { get; } = new List<Solid>();
            public NativeRevitAccessoryFamilyService.PlacementResult Counts { get; } =
                new NativeRevitAccessoryFamilyService.PlacementResult();
            public List<string> Warnings { get; } = new List<string>();
            public bool IsDisplayCapped { get; internal set; }
            public int SkippedSolidCount { get; internal set; }
        }

        public NativeRevitQuickAccessoryService(SharedDataViewModel data,
            IEnumerable<FloorElementModel> models)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _models = (models ?? Enumerable.Empty<FloorElementModel>())
                .Where(model => model != null && !string.IsNullOrWhiteSpace(model.UniqueId))
                .GroupBy(model => model.UniqueId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(),
                    StringComparer.OrdinalIgnoreCase);
        }

        public BuildResult Build(
            IEnumerable<(Element Element, StructuralComponentType Type)> components,
            Action<int, string> reportProgress = null,
            Func<bool> isCancellationRequested = null)
        {
            var result = new BuildResult();
            List<(Element Element, StructuralComponentType Type)> items = components?.ToList() ??
                new List<(Element Element, StructuralComponentType Type)>();
            _propLocations.Clear();

            for (int index = 0; index < items.Count; index++)
            {
                ThrowIfCancelled(isCancellationRequested);
                (Element element, StructuralComponentType type) = items[index];
                try
                {
                    CalculationParameters parameters = EffectiveParameters(element, type);
                    switch (type)
                    {
                        case StructuralComponentType.Slab:
                            AddSlabProps(element, parameters, result, isCancellationRequested);
                            break;
                        case StructuralComponentType.Beam:
                            AddBeamProps(element, parameters, result);
                            if (!result.IsDisplayCapped)
                                AddBeamClamps(element, parameters, result);
                            break;
                        case StructuralComponentType.Column:
                            AddColumnYokes(element, parameters, result);
                            if (!result.IsDisplayCapped)
                                AddColumnTieRods(element, parameters, result);
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    // One malformed/imported family must not roll back the complete
                    // project preview. Detailed mode remains available for inspection.
                    result.Warnings.Add($"{type} {element.Id}: {exception.Message}");
                }

                if (result.IsDisplayCapped) break;

                if (index % Math.Max(1, items.Count / 30) == 0 || index == items.Count - 1)
                {
                    int percent = items.Count == 0
                        ? 100
                        : (int)Math.Round(100.0 * (index + 1) / items.Count);
                    reportProgress?.Invoke(percent,
                        $"Đang dựng phụ kiện nhanh: {index + 1}/{items.Count} cấu kiện...");
                }
            }

            return result;
        }

        private CalculationParameters EffectiveParameters(Element element,
            StructuralComponentType type)
        {
            _models.TryGetValue(element.UniqueId, out FloorElementModel model);
            CalculationParameters parameters = _data.GetEffectiveComponentParameters(model);
            parameters.ComponentType = type;
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null) return parameters;

            double dx = InternalToMm(box.Max.X - box.Min.X);
            double dy = InternalToMm(box.Max.Y - box.Min.Y);
            double dz = InternalToMm(box.Max.Z - box.Min.Z);
            if (type == StructuralComponentType.Column)
            {
                if (parameters.ColumnWidth <= 0) parameters.ColumnWidth = Math.Max(1, dx);
                if (parameters.ColumnDepth <= 0) parameters.ColumnDepth = Math.Max(1, dy);
                parameters.ColumnHeight = Math.Max(1, dz);
            }
            else if (type == StructuralComponentType.Beam)
            {
                if (parameters.BeamWidth <= 0)
                    parameters.BeamWidth = Math.Max(1, Math.Min(dx, dy));
                if (parameters.BeamDepth <= 0) parameters.BeamDepth = Math.Max(1, dz);
                if (parameters.BeamLength <= 0) parameters.BeamLength = Math.Max(dx, dy);
            }
            return parameters;
        }

        private void AddSlabProps(Element element, CalculationParameters parameters, BuildResult result,
            Func<bool> isCancellationRequested)
        {
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null) return;
            List<PlanarFace> bottomFaces = GetPlanarFaces(element)
                .Where(face => face.FaceNormal.Z < -0.70).ToList();
            if (bottomFaces.Count == 0) return;

            double spacing = Mm(ClampFinite(parameters.SupportSpacing, 300, 3000, 1000));
            double height = Mm(ClampFinite(parameters.CayChongHeight, 600, 6000, 3000));
            double width = Mm(ClampFinite(parameters.CayChongDiameter, 25, 120, 48));
            double edgeInset = Math.Max(width / 2, Mm(50));

            int probe = 0;
            foreach (double x in GridWithInset(box.Min.X, box.Max.X, spacing, edgeInset))
            foreach (double y in GridWithInset(box.Min.Y, box.Max.Y, spacing, edgeInset))
            {
                if ((probe++ & 63) == 0) ThrowIfCancelled(isCancellationRequested);
                if (result.IsDisplayCapped) return;
                if (!TryFindBearingPoint(bottomFaces, x, y, box.Min.Z, out XYZ top)) continue;
                top -= XYZ.BasisZ * Mm(18);
                XYZ basePoint = top - XYZ.BasisZ * height;
                if (!RegisterProp(basePoint)) continue;
                if (!TryAddBox(result, basePoint.X - width / 2, basePoint.Y - width / 2,
                        basePoint.Z, width, width, height)) continue;
                result.Counts.PropCount++;
            }
        }

        private void AddBeamProps(Element element, CalculationParameters parameters, BuildResult result)
        {
            if (!(element.Location is LocationCurve location) || location.Curve == null) return;
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null) return;

            Curve curve = location.Curve;
            double spacing = Mm(ClampFinite(parameters.BeamPropSpacing, 300, 3000, 1000));
            double height = Mm(ClampFinite(parameters.CayChongHeight, 600, 6000, 3000));
            double width = Mm(ClampFinite(parameters.CayChongDiameter, 25, 120, 48));
            int stations = Math.Max(2, (int)Math.Ceiling(curve.Length / spacing) + 1);
            int rows = parameters.BeamWidth >= 500 ? 2 : 1;
            double rowOffset = rows == 2 ? Mm(parameters.BeamWidth * 0.25) : 0;

            for (int station = 0; station < stations; station++)
            {
                if (result.IsDisplayCapped) return;
                double ratio = stations == 1 ? 0.5 : (double)station / (stations - 1);
                XYZ point = curve.Evaluate(ratio, true);
                XYZ tangent = HorizontalDirection(curve.ComputeDerivatives(ratio, true).BasisX);
                XYZ cross = new XYZ(-tangent.Y, tangent.X, 0);
                for (int row = 0; row < rows; row++)
                {
                    double offset = rows == 1 ? 0 : (row == 0 ? -rowOffset : rowOffset);
                    double beamBottomZ = BeamBottomAtStation(curve, box, point);
                    XYZ top = new XYZ(point.X, point.Y, beamBottomZ - Mm(18)) + cross * offset;
                    XYZ basePoint = top - XYZ.BasisZ * height;
                    if (!RegisterProp(basePoint)) continue;
                    if (!TryAddBox(result, basePoint.X - width / 2, basePoint.Y - width / 2,
                            basePoint.Z, width, width, height)) continue;
                    result.Counts.PropCount++;
                }
            }
        }

        private void AddBeamClamps(Element element, CalculationParameters parameters,
            BuildResult result)
        {
            if (!(element.Location is LocationCurve location) || location.Curve == null) return;
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null) return;

            Curve curve = location.Curve;
            double lengthMm = InternalToMm(curve.Length);
            double beamWidth = Mm(ClampFinite(parameters.BeamWidth, 100, 5000, 200));
            double beamDepth = Mm(ClampFinite(parameters.BeamDepth, 100, 5000, 400));
            double walerWidth = Mm(ClampFinite(parameters.BeamPrimaryWalerWidth, 25, 300, 50));
            double walerDepth = Mm(ClampFinite(parameters.BeamPrimaryWalerDepth, 40, 400, 100));
            double tieSize = Mm(ClampFinite(parameters.BeamTieRodDiameter, 8, 40, 16));
            double walerCenter = beamWidth / 2 + Mm(25) + walerDepth / 2;
            double walerHeight = beamDepth + Mm(260);
            double rodLength = 2 * (walerCenter + walerDepth / 2 + Mm(130));

            foreach (double stationMm in FormworkCalculationService.BuildBeamClampStations(
                         lengthMm, Math.Max(100, parameters.BeamTieRodSpacing)))
            {
                if (result.IsDisplayCapped) return;
                double ratio = lengthMm <= 1e-6 ? 0.5 : Math.Max(0, Math.Min(1, stationMm / lengthMm));
                XYZ point = curve.Evaluate(ratio, true);
                XYZ xAxis = HorizontalDirection(curve.ComputeDerivatives(ratio, true).BasisX);
                XYZ yAxis = new XYZ(-xAxis.Y, xAxis.X, 0);
                double beamBottomZ = BeamBottomAtStation(curve, box, point);
                int before = result.Solids.Count;

                foreach (double sign in new[] { -1.0, 1.0 })
                {
                    XYZ center = new XYZ(point.X, point.Y,
                        beamBottomZ - Mm(80) + walerHeight / 2) + yAxis * (sign * walerCenter);
                    TryAddOrientedBox(result, center, xAxis, yAxis,
                        walerWidth, walerDepth, walerHeight);
                }
                foreach (double zRatio in new[] { 0.30, 0.78 })
                {
                    XYZ center = new XYZ(point.X, point.Y, beamBottomZ + beamDepth * zRatio);
                    TryAddOrientedBox(result, center, xAxis, yAxis, tieSize, rodLength, tieSize);
                }

                if (result.Solids.Count > before) result.Counts.BeamClampCount++;
            }
        }

        private void AddColumnYokes(Element element, CalculationParameters parameters,
            BuildResult result)
        {
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null) return;

            double width = Mm(ClampFinite(parameters.ColumnWidth, 100, 5000, 200));
            double depth = Mm(ClampFinite(parameters.ColumnDepth, 100, 5000, 200));
            double barWidth = Mm(ClampFinite(parameters.ColumnYokeWidth, 20, 300, 40));
            double barHeight = Mm(ClampFinite(parameters.ColumnYokeHeight, 20, 300, 80));
            double clearance = Mm(35);
            double clearWidth = width + 2 * clearance;
            double clearDepth = depth + 2 * clearance;
            int pairs = Math.Max(1, Math.Min(4, parameters.ColumnYokePairCount));
            double pairPitch = barHeight + Mm(15);
            XYZ center = new XYZ((box.Min.X + box.Max.X) / 2,
                (box.Min.Y + box.Max.Y) / 2, 0);
            (XYZ xAxis, XYZ yAxis) = ColumnAxes(element);
            parameters.ColumnHeight = InternalToMm(box.Max.Z - box.Min.Z);

            foreach (double elevationMm in FormworkCalculationService.BuildColumnYokeElevations(parameters))
            {
                if (result.IsDisplayCapped) return;
                int before = result.Solids.Count;
                for (int pair = 0; pair < pairs; pair++)
                {
                    double pairOffset = (pair - (pairs - 1) / 2.0) * pairPitch;
                    double z = box.Min.Z + Mm(elevationMm) + pairOffset;
                    XYZ origin = new XYZ(center.X, center.Y, z);

                    TryAddOrientedBox(result,
                        origin - yAxis * (clearDepth / 2 + barWidth / 2), xAxis, yAxis,
                        clearWidth + 2 * barWidth, barWidth, barHeight);
                    TryAddOrientedBox(result,
                        origin + yAxis * (clearDepth / 2 + barWidth / 2), xAxis, yAxis,
                        clearWidth + 2 * barWidth, barWidth, barHeight);
                    TryAddOrientedBox(result,
                        origin - xAxis * (clearWidth / 2 + barWidth / 2), xAxis, yAxis,
                        barWidth, clearDepth, barHeight);
                    TryAddOrientedBox(result,
                        origin + xAxis * (clearWidth / 2 + barWidth / 2), xAxis, yAxis,
                        barWidth, clearDepth, barHeight);
                }
                if (result.Solids.Count > before) result.Counts.ColumnYokeCount++;
            }
        }

        private void AddColumnTieRods(Element element, CalculationParameters parameters,
            BuildResult result)
        {
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null) return;

            double width = Mm(ClampFinite(parameters.ColumnWidth, 100, 5000, 200));
            double depth = Mm(ClampFinite(parameters.ColumnDepth, 100, 5000, 200));
            double tieSize = Mm(ClampFinite(parameters.ColumnTieRodDiameter, 6, 40, 12));
            double clearance = Mm(35);
            double projection = Mm(140);
            double xLength = width + 2 * clearance + 2 * projection;
            double yLength = depth + 2 * clearance + 2 * projection;
            double faceWidth = width + 2 * clearance;
            double faceDepth = depth + 2 * clearance;
            int ties = Math.Max(1, Math.Min(8, parameters.ColumnTieRodCountPerFace));
            XYZ center = new XYZ((box.Min.X + box.Max.X) / 2,
                (box.Min.Y + box.Max.Y) / 2, 0);
            (XYZ xAxis, XYZ yAxis) = ColumnAxes(element);
            parameters.ColumnHeight = InternalToMm(box.Max.Z - box.Min.Z);

            foreach (double elevationMm in FormworkCalculationService.BuildColumnYokeElevations(parameters))
            {
                if (result.IsDisplayCapped) return;
                double z = box.Min.Z + Mm(elevationMm);
                for (int index = 0; index < ties; index++)
                {
                    double ratio = ties == 1 ? 0.5 : (double)index / (ties - 1);
                    double offsetY = -faceDepth / 2 + ratio * faceDepth;
                    double offsetX = -faceWidth / 2 + ratio * faceWidth;

                    int before = result.Solids.Count;
                    TryAddOrientedBox(result,
                        new XYZ(center.X, center.Y, z) + yAxis * offsetY,
                        xAxis, yAxis, xLength, tieSize, tieSize);
                    if (result.Solids.Count > before) result.Counts.ColumnTieRodCount++;

                    before = result.Solids.Count;
                    TryAddOrientedBox(result,
                        new XYZ(center.X, center.Y, z) + xAxis * offsetX,
                        xAxis, yAxis, tieSize, yLength, tieSize);
                    if (result.Solids.Count > before) result.Counts.ColumnTieRodCount++;
                }
            }
        }

        private bool RegisterProp(XYZ basePoint)
        {
            long Quantize(double value) => (long)Math.Round(InternalToMm(value) / 10.0);
            return _propLocations.Add((Quantize(basePoint.X), Quantize(basePoint.Y), Quantize(basePoint.Z)));
        }

        private static (XYZ X, XYZ Y) ColumnAxes(Element element)
        {
            double rotation = element.Location is LocationPoint point ? point.Rotation : 0;
            XYZ xAxis = new XYZ(Math.Cos(rotation), Math.Sin(rotation), 0);
            return (xAxis, new XYZ(-xAxis.Y, xAxis.X, 0));
        }

        private static XYZ HorizontalDirection(XYZ direction)
        {
            var horizontal = new XYZ(direction.X, direction.Y, 0);
            return horizontal.GetLength() > 1e-8 ? horizontal.Normalize() : XYZ.BasisX;
        }

        private static double BeamBottomAtStation(Curve curve, BoundingBoxXYZ box, XYZ stationPoint)
        {
            XYZ start = curve.GetEndPoint(0);
            XYZ end = curve.GetEndPoint(1);
            double referenceMinimumZ = Math.Min(start.Z, end.Z);
            return stationPoint.Z + (box.Min.Z - referenceMinimumZ);
        }

        private static bool TryFindBearingPoint(IEnumerable<PlanarFace> faces,
            double x, double y, double fallbackZ, out XYZ point)
        {
            foreach (PlanarFace face in faces)
            {
                try
                {
                    IntersectionResult projection = face.Project(new XYZ(x, y, fallbackZ));
                    if (projection == null || !face.IsInside(projection.UVPoint)) continue;
                    point = projection.XYZPoint;
                    return true;
                }
                catch (Autodesk.Revit.Exceptions.ApplicationException)
                {
                    // A malformed imported face is ignored; no unsupported prop is drawn.
                }
            }
            point = null;
            return false;
        }

        private static IEnumerable<PlanarFace> GetPlanarFaces(Element element)
        {
            try
            {
                GeometryElement geometry = element.get_Geometry(new Options
                {
                    DetailLevel = ViewDetailLevel.Medium,
                    IncludeNonVisibleObjects = false,
                    ComputeReferences = false
                });
                if (geometry == null) return Enumerable.Empty<PlanarFace>();
                return GetSolids(geometry, Transform.Identity)
                    .SelectMany(solid => solid.Faces.Cast<Face>())
                    .OfType<PlanarFace>().ToList();
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                return Enumerable.Empty<PlanarFace>();
            }
        }

        private static IEnumerable<Solid> GetSolids(GeometryElement geometry, Transform transform)
        {
            foreach (GeometryObject item in geometry)
            {
                if (item is Solid solid && solid.Faces.Size > 0 && solid.Volume > 1e-9)
                    yield return transform.IsIdentity ? solid : SolidUtils.CreateTransformed(solid, transform);
                else if (item is GeometryInstance instance)
                    foreach (Solid child in GetSolids(instance.GetSymbolGeometry(),
                                 transform.Multiply(instance.Transform)))
                        yield return child;
            }
        }

        private static IEnumerable<double> Grid(double min, double max, double spacing)
        {
            double length = max - min;
            int count = Math.Max(1, Math.Min(MaximumGridStationsPerAxis - 1,
                (int)Math.Ceiling(length / spacing)));
            for (int index = 0; index <= count; index++)
                yield return min + length * index / count;
        }

        private static IEnumerable<double> GridWithInset(double min, double max,
            double spacing, double inset)
        {
            if (max - min <= 2 * inset)
            {
                yield return (min + max) / 2;
                yield break;
            }
            foreach (double coordinate in Grid(min + inset, max - inset, spacing))
                yield return coordinate;
        }

        private static bool TryAddBox(BuildResult result,
            double x, double y, double z, double dx, double dy, double dz)
        {
            if (!CanCreateBox(dx, dy, dz)) return false;
            if (result.Solids.Count >= MaximumSolidCount)
            {
                result.IsDisplayCapped = true;
                result.SkippedSolidCount++;
                return false;
            }
            try
            {
                result.Solids.Add(CreateBox(x, y, z, dx, dy, dz));
                return true;
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                result.SkippedSolidCount++;
                return false;
            }
        }

        private static bool TryAddOrientedBox(BuildResult result, XYZ center,
            XYZ xAxis, XYZ yAxis, double xSize, double ySize, double zSize)
        {
            if (!CanCreateBox(xSize, ySize, zSize)) return false;
            if (result.Solids.Count >= MaximumSolidCount)
            {
                result.IsDisplayCapped = true;
                result.SkippedSolidCount++;
                return false;
            }
            try
            {
                Solid local = CreateBox(-xSize / 2, -ySize / 2, -zSize / 2,
                    xSize, ySize, zSize);
                Transform transform = Transform.Identity;
                transform.Origin = center;
                transform.BasisX = xAxis;
                transform.BasisY = yAxis;
                transform.BasisZ = XYZ.BasisZ;
                result.Solids.Add(SolidUtils.CreateTransformed(local, transform));
                return true;
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                result.SkippedSolidCount++;
                return false;
            }
        }

        private static Solid CreateBox(double x, double y, double z,
            double dx, double dy, double dz)
        {
            var loop = new CurveLoop();
            XYZ p0 = new XYZ(x, y, z);
            XYZ p1 = new XYZ(x + dx, y, z);
            XYZ p2 = new XYZ(x + dx, y + dy, z);
            XYZ p3 = new XYZ(x, y + dy, z);
            loop.Append(Line.CreateBound(p0, p1));
            loop.Append(Line.CreateBound(p1, p2));
            loop.Append(Line.CreateBound(p2, p3));
            loop.Append(Line.CreateBound(p3, p0));
            return GeometryCreationUtilities.CreateExtrusionGeometry(
                new List<CurveLoop> { loop }, XYZ.BasisZ, dz);
        }

        private static bool CanCreateBox(double dx, double dy, double dz) =>
            IsFinite(dx) && IsFinite(dy) && IsFinite(dz) && dx > 1e-6 && dy > 1e-6 && dz > 1e-6;

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static double ClampFinite(double value, double minimum, double maximum,
            double fallback) => IsFinite(value) && value > 0
            ? Math.Max(minimum, Math.Min(maximum, value))
            : fallback;
        private static double Mm(double value) =>
            UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Millimeters);
        private static double InternalToMm(double value) =>
            UnitUtils.ConvertFromInternalUnits(value, UnitTypeId.Millimeters);

        private static void ThrowIfCancelled(Func<bool> isCancellationRequested)
        {
            if (isCancellationRequested?.Invoke() == true)
                throw new OperationCanceledException("Người dùng đã hủy dựng phụ kiện Preview 3D.");
        }
    }
}
