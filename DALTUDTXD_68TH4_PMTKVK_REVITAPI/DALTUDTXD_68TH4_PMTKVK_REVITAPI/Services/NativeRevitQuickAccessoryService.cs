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
        private const int SlabPropSolidBudget = 12000;
        private const int MaximumGridStationsPerAxis = 80;
        private readonly SharedDataViewModel _data;
        private readonly IReadOnlyDictionary<string, FloorElementModel> _models;
        private readonly HashSet<(long X, long Y, long Z)> _propLocations =
            new HashSet<(long X, long Y, long Z)>();
        private List<PlanarFace> _slabTopFaces = new List<PlanarFace>();
        private ConstructionElevationResolver _constructionElevations;

        internal sealed class BuildResult
        {
            public List<Solid> Solids { get; } = new List<Solid>();
            public Dictionary<AccessoryVisualKind, List<Solid>> SolidsByKind { get; } =
                Enum.GetValues(typeof(AccessoryVisualKind)).Cast<AccessoryVisualKind>()
                    .ToDictionary(kind => kind, _ => new List<Solid>());
            public NativeRevitAccessoryFamilyService.PlacementResult Counts { get; } =
                new NativeRevitAccessoryFamilyService.PlacementResult();
            public List<string> Warnings { get; } = new List<string>();
            public bool IsDisplayCapped { get; internal set; }
            public int SkippedSolidCount { get; internal set; }
            public int ReducedSlabCount { get; internal set; }
            public int UnsupportedShoringPointCount { get; internal set; }
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
            _slabTopFaces = CollectSlabTopFaces(items);
            Document document = items.Select(item => item.Element?.Document)
                .FirstOrDefault(item => item != null);
            try
            {
                _constructionElevations = document == null
                    ? null
                    : new ConstructionElevationResolver(document);
            }
            catch (Exception exception)
            {
                // Failing closed is intentional: a nominal Level elevation is not a
                // safe substitute for a real bearing surface under a proposed foot.
                _constructionElevations = null;
                result.Warnings.Add($"Không thể đọc cao độ mặt kê thực tế: {exception.Message}");
            }
            Dictionary<long, CalculationParameters> parametersByElement = items
                .GroupBy(item => item.Element.Id.Value)
                .ToDictionary(group => group.Key,
                    group => EffectiveParameters(group.First().Element, group.First().Type));

            // Reserve a fair share of the lightweight geometry budget for every slab.
            // Slabs are handled first so props on upper levels cannot be starved by
            // thousands of column-yoke/tie solids created earlier in selection order.
            int slabCount = items.Count(item => item.Type == StructuralComponentType.Slab &&
                parametersByElement.TryGetValue(item.Element.Id.Value, out CalculationParameters slabParameters) &&
                !slabParameters.IsGroundLevel);
            int slabPropLimit = slabCount == 0
                ? 0
                : Math.Max(1, Math.Min(1200, SlabPropSolidBudget / slabCount));
            items = items
                .OrderBy(item => item.Type == StructuralComponentType.Slab ? 0 :
                    item.Type == StructuralComponentType.Column ? 1 :
                    item.Type == StructuralComponentType.Beam ? 2 : 3)
                .ThenBy(item => item.Element.get_BoundingBox(null)?.Min.Z ?? 0)
                .ToList();

            for (int index = 0; index < items.Count; index++)
            {
                ThrowIfCancelled(isCancellationRequested);
                (Element element, StructuralComponentType type) = items[index];
                try
                {
                    CalculationParameters parameters = parametersByElement[element.Id.Value];
                    switch (type)
                    {
                        case StructuralComponentType.Slab:
                            if (!parameters.IsGroundLevel)
                                AddSlabProps(element, parameters, result, slabPropLimit,
                                    isCancellationRequested);
                            else result.Counts.ShoringSuppressedComponentCount++;
                            break;
                        case StructuralComponentType.Beam:
                            if (!parameters.IsGroundLevel)
                                AddBeamProps(element, parameters, result);
                            else result.Counts.ShoringSuppressedComponentCount++;
                            if (!result.IsDisplayCapped)
                                AddBeamClamps(element, parameters, result);
                            if (!result.IsDisplayCapped && parameters.BeamPlacementModeIndex == 2)
                            {
                                AddBeamSurfaceTies(element, parameters, result);
                                if (!result.IsDisplayCapped)
                                    AddBeamSideBraces(element, parameters, result);
                            }
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

            if (result.UnsupportedShoringPointCount > 0)
            {
                result.Counts.UnsupportedShoringPointCount = result.UnsupportedShoringPointCount;
                result.Warnings.Add(
                    $"Đã bỏ qua {result.UnsupportedShoringPointCount} vị trí chống/giáo không có " +
                    "mặt sàn hoặc móng đỡ thực tế bên dưới.");
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
            int maximumProps, Func<bool> isCancellationRequested)
        {
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null) return;
            List<PlanarFace> planarFaces = GetPlanarFaces(element).ToList();
            List<PlanarFace> topFaces = planarFaces
                .Where(face => face.FaceNormal.Z > 0.70).ToList();
            List<PlanarFace> bottomFaces = planarFaces
                .Where(face => face.FaceNormal.Z < -0.70).ToList();
            if (topFaces.Count == 0 && bottomFaces.Count == 0) return;

            double spacing = Mm(ClampFinite(parameters.SupportSpacing, 300, 3000, 1000));
            double width = Mm(ClampFinite(parameters.CayChongDiameter, 25, 120, 48));
            double edgeInset = Math.Max(width / 2, Mm(50));
            double slabThickness = Mm(ClampFinite(parameters.SlabThickness, 40, 2000,
                Math.Max(40, InternalToMm(box.Max.Z - box.Min.Z))));
            double supportStack = Mm(
                ClampFinite(parameters.VanMatThickness, 6, 60, 18) +
                ClampFinite(parameters.XaGoPhuHeight, 20, 500, 100) +
                ClampFinite(parameters.XaGoChinhHeight, 20, 500, 100));
            ComponentIntersectionInfo intersection =
                _data.GetComponentIntersectionInfo(element.UniqueId);

            // Keep the grid symmetric about the centre of this individual floor plate.
            // If the quick-preview LOD budget is smaller than the calculated grid, enlarge
            // only the display spacing (calculation/takeoff spacing remains unchanged).
            List<double> xStations = CenteredGrid(box.Min.X, box.Max.X, spacing, edgeInset).ToList();
            List<double> yStations = CenteredGrid(box.Min.Y, box.Max.Y, spacing, edgeInset).ToList();
            int candidateCount = xStations.Count * yStations.Count;
            if (maximumProps > 0 && candidateCount > maximumProps)
            {
                double lodFactor = Math.Sqrt((double)candidateCount / maximumProps);
                double displaySpacing = spacing * Math.Max(1.0, lodFactor);
                xStations = CenteredGrid(box.Min.X, box.Max.X, displaySpacing, edgeInset).ToList();
                yStations = CenteredGrid(box.Min.Y, box.Max.Y, displaySpacing, edgeInset).ToList();
                result.ReducedSlabCount++;
            }

            int probe = 0;
            int added = 0;
            foreach (double x in xStations)
            foreach (double y in yStations)
            {
                if ((probe++ & 63) == 0) ThrowIfCancelled(isCancellationRequested);
                if (result.IsDisplayCapped) return;
                if (maximumProps > 0 && added >= maximumProps) return;
                if (!TryFindSlabUnderside(topFaces, bottomFaces, x, y, box, slabThickness,
                        out XYZ slabUnderside)) continue;
                if (IsInsidePlanCutout(intersection?.PlanCutouts, x, y)) continue;

                // The prop contacts the underside of the primary waler, not the Revit
                // Level and not merely the underside of a nominal 18 mm plywood sheet.
                double contactTopZ = slabUnderside.Z - supportStack;
                if (!TryResolveShoringInterval(element, x, y, contactTopZ,
                        out double baseZ, out double height))
                {
                    result.UnsupportedShoringPointCount++;
                    continue;
                }

                XYZ basePoint = new XYZ(x, y, baseZ);
                if (!RegisterProp(basePoint)) continue;
                if (!TryAddBox(result, AccessoryVisualKind.Support,
                        basePoint.X - width / 2, basePoint.Y - width / 2,
                        basePoint.Z, width, width, height)) continue;
                result.Counts.PropCount++;
                added++;
            }
        }

        private void AddBeamProps(Element element, CalculationParameters parameters, BuildResult result)
        {
            if (!(element.Location is LocationCurve location) || location.Curve == null) return;
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null) return;

            Curve curve = location.Curve;
            double spacing = Mm(ClampFinite(parameters.BeamPropSpacing, 300, 3000, 1000));
            double width = Mm(ClampFinite(parameters.CayChongDiameter, 25, 120, 48));
            double supportStack = Mm(
                ClampFinite(parameters.BeamBottomPlywoodThickness, 6, 60, 18) +
                ClampFinite(parameters.BeamSecondaryJoistDepth, 20, 500, 50) +
                ClampFinite(parameters.BeamPrimaryWalerDepth, 20, 500, 100));
            int stations = Math.Max(2, (int)Math.Ceiling(curve.Length / spacing) + 1);
            const int rows = 2;
            double rowOffset = Mm(ClampFinite(
                parameters.BeamScaffoldBayWidth, 300, 3000, 600)) / 2;
            double lengthMm = InternalToMm(curve.Length);
            ComponentIntersectionInfo intersection =
                _data.GetComponentIntersectionInfo(element.UniqueId);

            for (int station = 0; station < stations; station++)
            {
                if (result.IsDisplayCapped) return;
                double ratio = stations == 1 ? 0.5 : (double)station / (stations - 1);
                double stationMm = ratio * lengthMm;
                if (IsInsideLengthGap(intersection?.BeamColumnGaps, stationMm)) continue;
                XYZ point = curve.Evaluate(ratio, true);
                XYZ tangent = HorizontalDirection(curve.ComputeDerivatives(ratio, true).BasisX);
                XYZ cross = new XYZ(-tangent.Y, tangent.X, 0);
                for (int row = 0; row < rows; row++)
                {
                    double offset = row == 0 ? -rowOffset : rowOffset;
                    double beamBottomZ = BeamBottomAtStation(curve, box, point);
                    XYZ planPoint = new XYZ(point.X, point.Y, 0) + cross * offset;
                    double contactTopZ = beamBottomZ - supportStack;
                    if (!TryResolveShoringInterval(element, planPoint.X, planPoint.Y,
                            contactTopZ, out double baseZ, out double height))
                    {
                        result.UnsupportedShoringPointCount++;
                        continue;
                    }

                    XYZ basePoint = new XYZ(planPoint.X, planPoint.Y, baseZ);
                    if (!RegisterProp(basePoint)) continue;
                    if (!TryAddBox(result, AccessoryVisualKind.Support,
                            basePoint.X - width / 2, basePoint.Y - width / 2,
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
            double walerWidth = Mm(ClampFinite(parameters.BeamPrimaryWalerWidth, 25, 300, 50));
            double walerDepth = Mm(ClampFinite(parameters.BeamPrimaryWalerDepth, 40, 400, 100));
            double tieSize = Mm(ClampFinite(parameters.BeamTieRodDiameter, 8, 40, 16));
            double walerCenter = beamWidth / 2 + Mm(25) + walerDepth / 2;
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
                double beamTopZ = BeamTopAtStation(curve, box, point);
                double actualBeamDepth = Math.Max(Mm(100), beamTopZ - beamBottomZ);
                double walerHeight = actualBeamDepth + Mm(80);
                int before = result.Solids.Count;

                foreach (double sign in new[] { -1.0, 1.0 })
                {
                    XYZ center = new XYZ(point.X, point.Y,
                        beamBottomZ - Mm(80) + walerHeight / 2) + yAxis * (sign * walerCenter);
                    TryAddOrientedBox(result, AccessoryVisualKind.Clamp, center, xAxis, yAxis,
                        walerWidth, walerDepth, walerHeight);
                }
                foreach (double zRatio in new[] { 0.30, 0.78 })
                {
                    XYZ center = new XYZ(point.X, point.Y, beamBottomZ + actualBeamDepth * zRatio);
                    TryAddOrientedBox(result, AccessoryVisualKind.Clamp, center, xAxis, yAxis,
                        tieSize, rodLength, tieSize);
                }

                if (result.Solids.Count > before) result.Counts.BeamClampCount++;
            }
        }

        private void AddBeamSurfaceTies(Element element, CalculationParameters parameters,
            BuildResult result)
        {
            if (!(element.Location is LocationCurve location) || location.Curve == null) return;
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null) return;

            Curve curve = location.Curve;
            double lengthMm = InternalToMm(curve.Length);
            double beamWidth = Mm(ClampFinite(parameters.BeamWidth, 100, 5000, 200));
            double sidePlywood = Mm(ClampFinite(parameters.BeamSidePlywoodThickness, 6, 50, 18));
            double tieSize = Mm(ClampFinite(parameters.BeamEdgeSurfaceTieRodDiameter, 6, 40, 12));
            double anchorLength = Mm(ClampFinite(parameters.BeamEdgeSurfaceTieRodAnchorLength,
                500, 5000, 1200));
            ComponentIntersectionInfo intersection = _data.GetComponentIntersectionInfo(element.UniqueId);

            foreach (double stationMm in FormworkCalculationService.BuildBeamClampStations(
                         lengthMm, Math.Max(300, parameters.BeamEdgeSurfaceTieRodSpacing)))
            {
                if (result.IsDisplayCapped) return;
                double ratio = lengthMm <= 1e-6 ? 0.5 : Math.Max(0, Math.Min(1, stationMm / lengthMm));
                XYZ station = curve.Evaluate(ratio, true);
                XYZ axis = HorizontalDirection(curve.ComputeDerivatives(ratio, true).BasisX);
                XYZ cross = new XYZ(-axis.Y, axis.X, 0);
                double beamTopZ = BeamTopAtStation(curve, box, station);
                if (!TryResolveBeamInteriorSide(station, cross, beamWidth, beamTopZ,
                        anchorLength, intersection, out double interiorSign)) continue;

                XYZ exterior = new XYZ(station.X, station.Y, beamTopZ + tieSize / 2 + Mm(8)) -
                               cross * (interiorSign * (beamWidth / 2 + sidePlywood + Mm(40)));
                XYZ anchor = new XYZ(station.X, station.Y, exterior.Z) +
                             cross * (interiorSign * (beamWidth / 2 + anchorLength));
                XYZ center = (exterior + anchor) * 0.5;
                int before = result.Solids.Count;
                TryAddOrientedBox(result, AccessoryVisualKind.TieRod, center, axis, cross,
                    tieSize, (anchor - exterior).GetLength(), tieSize);
                TryAddOrientedBox(result, AccessoryVisualKind.TieRod, anchor, axis, cross,
                    Mm(120), Mm(120), Mm(10));
                TryAddOrientedBox(result, AccessoryVisualKind.TieRod, exterior, axis, cross,
                    Mm(90), Mm(90), Mm(10));
                if (result.Solids.Count > before) result.Counts.BeamSurfaceTieRodCount++;
            }
        }

        private void AddBeamSideBraces(Element element, CalculationParameters parameters,
            BuildResult result)
        {
            if (_constructionElevations == null ||
                !(element.Location is LocationCurve location) || location.Curve == null) return;
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null) return;

            Curve curve = location.Curve;
            double lengthMm = InternalToMm(curve.Length);
            double beamWidth = Mm(ClampFinite(parameters.BeamWidth, 100, 5000, 200));
            double sidePlywood = Mm(ClampFinite(parameters.BeamSidePlywoodThickness, 6, 50, 18));
            double braceSpacing = ClampFinite(parameters.BeamSideBraceSpacing,
                300, 3000, 1000);
            double targetAngle = ClampFinite(parameters.BeamSideBraceAngle, 35, 70, 55);
            double braceSizeMm = ClampFinite(parameters.BeamSideBraceDiameter, 25, 120, 48);
            double braceSize = Mm(braceSizeMm);
            int braceCount = Math.Max(1, Math.Min(4, parameters.BeamSideBraceCountPerStation));
            ComponentIntersectionInfo intersection =
                _data.GetComponentIntersectionInfo(element.UniqueId);
            int added = 0;

            foreach (double stationMm in FormworkCalculationService.BuildBeamClampStations(
                         lengthMm, braceSpacing))
            {
                if (result.IsDisplayCapped) return;
                if (IsInsideLengthGap(intersection?.BeamColumnGaps, stationMm)) continue;
                double ratio = lengthMm <= 1e-6 ? 0.5 :
                    Math.Max(0, Math.Min(1, stationMm / lengthMm));
                XYZ station = curve.Evaluate(ratio, true);
                XYZ axis = HorizontalDirection(curve.ComputeDerivatives(ratio, true).BasisX);
                XYZ cross = new XYZ(-axis.Y, axis.X, 0).Normalize();
                double beamTopZ = BeamTopAtStation(curve, box, station);
                if (!TryResolveBeamExteriorSide(station, cross, beamWidth, beamTopZ,
                        out double exteriorSign)) continue;

                double beamBottomZ = BeamBottomAtStation(curve, box, station);
                double attachZ = beamBottomZ +
                                 Math.Max(Mm(100), beamTopZ - beamBottomZ) * 0.70;
                for (int braceIndex = 0; braceIndex < braceCount; braceIndex++)
                {
                    double longitudinalOffsetMm =
                        (braceIndex - (braceCount - 1) / 2.0) * (braceSizeMm + 35);
                    XYZ tangentOffset = axis * Mm(longitudinalOffsetMm);
                    XYZ attach = new XYZ(station.X, station.Y, attachZ) +
                                 cross * (exteriorSign *
                                     (beamWidth / 2 + sidePlywood + Mm(35))) + tangentOffset;
                    XYZ bestFoot = null;
                    double bestAngleError = double.MaxValue;

                    // The foot must land on real slab/foundation/beam geometry.  Do not
                    // fall back to a Level elevation: that produces floating or outward
                    // braces in stepped floors and at openings.
                    for (double runMm = 2400; runMm >= 250; runMm -= 100)
                    {
                        XYZ candidate = new XYZ(station.X, station.Y, attachZ) +
                                        cross * (exteriorSign *
                                            (beamWidth / 2 + Mm(runMm))) + tangentOffset;
                        if (!_constructionElevations.TryFindHighestBearingBelow(
                                candidate.X, candidate.Y, attachZ - Mm(50), element.Id,
                                out double bearingZ)) continue;
                        XYZ foot = new XYZ(candidate.X, candidate.Y, bearingZ + Mm(30));
                        XYZ vector = attach - foot;
                        double horizontal = Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y);
                        double vertical = Math.Abs(vector.Z);
                        if (horizontal < Mm(200) || vertical < Mm(300) ||
                            vector.GetLength() < Mm(700)) continue;
                        double actualAngle = Math.Atan2(vertical, horizontal) * 180.0 / Math.PI;
                        double angleError = Math.Abs(actualAngle - targetAngle);
                        if (angleError >= bestAngleError) continue;
                        bestAngleError = angleError;
                        bestFoot = foot;
                    }

                    if (bestFoot == null) continue;
                    if (!TryAddBoxAlongAxis(result, AccessoryVisualKind.PushPullProp,
                            bestFoot, attach, braceSize, braceSize)) continue;
                    result.Counts.BeamSideBraceCount++;
                    added++;
                }
            }

            if (added == 0)
                result.Warnings.Add($"Dầm biên {element.Id}: không tìm thấy mặt tựa thực " +
                                    "cho chân chống xiên; không dựng chống ra khoảng không.");
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
            double groupHalfHeight = (pairs - 1) * pairPitch / 2 + barHeight / 2;
            double endClearance = Mm(20);
            double minimumGroupCenterZ = box.Min.Z + groupHalfHeight + endClearance;
            double maximumGroupCenterZ = box.Max.Z - groupHalfHeight - endClearance;
            if (maximumGroupCenterZ < minimumGroupCenterZ) return;
            XYZ center = new XYZ((box.Min.X + box.Max.X) / 2,
                (box.Min.Y + box.Max.Y) / 2, 0);
            (XYZ xAxis, XYZ yAxis) = ColumnAxes(element);
            parameters.ColumnHeight = InternalToMm(box.Max.Z - box.Min.Z);
            var usedGroupElevations = new HashSet<long>();

            foreach (double elevationMm in FormworkCalculationService.BuildColumnYokeElevations(parameters))
            {
                if (result.IsDisplayCapped) return;
                double groupCenterZ = Math.Max(minimumGroupCenterZ,
                    Math.Min(maximumGroupCenterZ, box.Min.Z + Mm(elevationMm)));
                long groupKey = (long)Math.Round(InternalToMm(groupCenterZ) / 2.0);
                if (!usedGroupElevations.Add(groupKey)) continue;
                int before = result.Solids.Count;
                for (int pair = 0; pair < pairs; pair++)
                {
                    double pairOffset = (pair - (pairs - 1) / 2.0) * pairPitch;
                    double z = groupCenterZ + pairOffset;
                    XYZ origin = new XYZ(center.X, center.Y, z);

                    TryAddOrientedBox(result, AccessoryVisualKind.Clamp,
                        origin - yAxis * (clearDepth / 2 + barWidth / 2), xAxis, yAxis,
                        clearWidth + 2 * barWidth, barWidth, barHeight);
                    TryAddOrientedBox(result, AccessoryVisualKind.Clamp,
                        origin + yAxis * (clearDepth / 2 + barWidth / 2), xAxis, yAxis,
                        clearWidth + 2 * barWidth, barWidth, barHeight);
                    TryAddOrientedBox(result, AccessoryVisualKind.Clamp,
                        origin - xAxis * (clearWidth / 2 + barWidth / 2), xAxis, yAxis,
                        barWidth, clearDepth, barHeight);
                    TryAddOrientedBox(result, AccessoryVisualKind.Clamp,
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
            double endClearance = tieSize / 2 + Mm(20);
            double minimumZ = box.Min.Z + endClearance;
            double maximumZ = box.Max.Z - endClearance;
            if (maximumZ < minimumZ) return;
            var usedElevations = new HashSet<long>();

            foreach (double elevationMm in FormworkCalculationService.BuildColumnYokeElevations(parameters))
            {
                if (result.IsDisplayCapped) return;
                double z = Math.Max(minimumZ,
                    Math.Min(maximumZ, box.Min.Z + Mm(elevationMm)));
                long elevationKey = (long)Math.Round(InternalToMm(z) / 2.0);
                if (!usedElevations.Add(elevationKey)) continue;
                for (int index = 0; index < ties; index++)
                {
                    double ratio = ties == 1 ? 0.5 : (double)index / (ties - 1);
                    double offsetY = -faceDepth / 2 + ratio * faceDepth;
                    double offsetX = -faceWidth / 2 + ratio * faceWidth;

                    int before = result.Solids.Count;
                    TryAddOrientedBox(result, AccessoryVisualKind.TieRod,
                        new XYZ(center.X, center.Y, z) + yAxis * offsetY,
                        xAxis, yAxis, xLength, tieSize, tieSize);
                    if (result.Solids.Count > before) result.Counts.ColumnTieRodCount++;

                    before = result.Solids.Count;
                    TryAddOrientedBox(result, AccessoryVisualKind.TieRod,
                        new XYZ(center.X, center.Y, z) + xAxis * offsetX,
                        xAxis, yAxis, tieSize, yLength, tieSize);
                    if (result.Solids.Count > before) result.Counts.ColumnTieRodCount++;
                }
            }
        }

        private bool TryResolveShoringInterval(Element targetElement, double x, double y,
            double contactTopZ, out double baseZ, out double height)
        {
            baseZ = 0;
            height = 0;
            if (_constructionElevations == null || !IsFinite(contactTopZ)) return false;
            if (!_constructionElevations.TryFindHighestBearingBelow(x, y, contactTopZ,
                    targetElement?.Id, out baseZ, 300.0)) return false;

            height = contactTopZ - baseZ;
            // Keep a small lower bound so geometry does not degenerate at local drops.
            // There is deliberately no nominal/global height fallback: exact contact is
            // what prevents a support from floating or piercing the formwork above it.
            return IsFinite(height) && height >= Mm(300);
        }

        private static bool IsInsidePlanCutout(
            IEnumerable<FormworkCutoutRectangle> cutouts, double x, double y)
        {
            double xMm = InternalToMm(x);
            double yMm = InternalToMm(y);
            const double toleranceMm = 10.0;
            return (cutouts ?? Enumerable.Empty<FormworkCutoutRectangle>()).Any(cutout =>
                xMm >= cutout.MinXmm - toleranceMm && xMm <= cutout.MaxXmm + toleranceMm &&
                yMm >= cutout.MinYmm - toleranceMm && yMm <= cutout.MaxYmm + toleranceMm);
        }

        private static bool IsInsideLengthGap(
            IEnumerable<FormworkLengthInterval> gaps, double stationMm)
        {
            const double toleranceMm = 10.0;
            return (gaps ?? Enumerable.Empty<FormworkLengthInterval>()).Any(gap =>
                stationMm >= gap.StartMm - toleranceMm &&
                stationMm <= gap.EndMm + toleranceMm);
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

        private static List<PlanarFace> CollectSlabTopFaces(
            IEnumerable<(Element Element, StructuralComponentType Type)> components)
        {
            Document document = components.Select(item => item.Element?.Document).FirstOrDefault(item => item != null);
            IEnumerable<Element> slabs = document == null
                ? components.Where(item => item.Type == StructuralComponentType.Slab)
                    .Select(item => item.Element)
                : new FilteredElementCollector(document)
                    .OfCategory(BuiltInCategory.OST_Floors)
                    .WhereElementIsNotElementType().ToElements();
            return slabs.Where(item => item != null).SelectMany(GetPlanarFaces)
                .Where(face => face.FaceNormal.Z > 0.70).ToList();
        }

        private bool TryResolveBeamInteriorSide(XYZ station, XYZ cross, double beamWidth,
            double beamTopZ, double anchorLength, ComponentIntersectionInfo intersection,
            out double interiorSign)
        {
            double probeOffset = beamWidth / 2 + Mm(250);
            bool plusHasSlab = IsPointOnSlabTop(station + cross * probeOffset, beamTopZ);
            bool minusHasSlab = IsPointOnSlabTop(station - cross * probeOffset, beamTopZ);
            if (plusHasSlab != minusHasSlab)
                interiorSign = plusHasSlab ? 1 : -1;
            else if (!plusHasSlab && !minusHasSlab && intersection?.BeamInteriorSideSign != 0)
                interiorSign = intersection.BeamInteriorSideSign;
            else
            {
                interiorSign = 0;
                return false;
            }

            XYZ anchor = station + cross * (interiorSign * (beamWidth / 2 + anchorLength));
            return IsPointOnSlabTop(anchor, beamTopZ);
        }

        private bool TryResolveBeamExteriorSide(XYZ station, XYZ cross, double beamWidth,
            double beamTopZ, out double exteriorSign)
        {
            double probeOffset = beamWidth / 2 + Mm(250);
            bool plusHasSlab = IsPointOnSlabTop(station + cross * probeOffset, beamTopZ);
            bool minusHasSlab = IsPointOnSlabTop(station - cross * probeOffset, beamTopZ);
            if (plusHasSlab == minusHasSlab)
            {
                exteriorSign = 0;
                return false;
            }
            exteriorSign = plusHasSlab ? -1 : 1;
            return true;
        }

        private bool IsPointOnSlabTop(XYZ point, double beamTopZ)
        {
            foreach (PlanarFace face in _slabTopFaces)
            {
                try
                {
                    IntersectionResult projection = face.Project(point);
                    if (projection == null || !face.IsInside(projection.UVPoint)) continue;
                    if (Math.Abs(projection.XYZPoint.Z - beamTopZ) <= Mm(300)) return true;
                }
                catch (Autodesk.Revit.Exceptions.ApplicationException)
                {
                    // Ignore malformed/imported faces and continue with another slab.
                }
            }
            return false;
        }

        private static double BeamBottomAtStation(Curve curve, BoundingBoxXYZ box, XYZ stationPoint)
        {
            XYZ start = curve.GetEndPoint(0);
            XYZ end = curve.GetEndPoint(1);
            double referenceMinimumZ = Math.Min(start.Z, end.Z);
            return stationPoint.Z + (box.Min.Z - referenceMinimumZ);
        }

        private static double BeamTopAtStation(Curve curve, BoundingBoxXYZ box, XYZ stationPoint)
        {
            XYZ start = curve.GetEndPoint(0);
            XYZ end = curve.GetEndPoint(1);
            double referenceMaximumZ = Math.Max(start.Z, end.Z);
            return stationPoint.Z + (box.Max.Z - referenceMaximumZ);
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

        private static bool TryFindSlabUnderside(IEnumerable<PlanarFace> topFaces,
            IEnumerable<PlanarFace> bottomFaces, double x, double y, BoundingBoxXYZ box,
            double slabThickness, out XYZ point)
        {
            // A Revit Join often cuts/segments the slab underside at beam intersections.
            // The top floor face still represents the floor plate and its openings, so use
            // it as the plan-validity source and derive the underside from actual thickness.
            foreach (PlanarFace face in topFaces)
            {
                try
                {
                    IntersectionResult projection = face.Project(new XYZ(x, y, box.Max.Z));
                    if (projection == null || !face.IsInside(projection.UVPoint)) continue;
                    double verticalThickness = slabThickness /
                        Math.Max(0.70, Math.Abs(face.FaceNormal.Z));
                    XYZ topPoint = projection.XYZPoint;
                    point = new XYZ(topPoint.X, topPoint.Y, topPoint.Z - verticalThickness);
                    return true;
                }
                catch (Autodesk.Revit.Exceptions.ApplicationException)
                {
                    // Try another face, then fall back to the real underside geometry.
                }
            }

            return TryFindBearingPoint(bottomFaces, x, y, box.Min.Z, out point);
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

        private static IEnumerable<double> CenteredGrid(double min, double max,
            double spacing, double inset)
        {
            double start = min + inset;
            double end = max - inset;
            if (end <= start || !IsFinite(spacing) || spacing <= 1e-8)
            {
                yield return (min + max) / 2;
                yield break;
            }

            double center = (start + end) / 2;
            yield return center;
            int maximumOffset = Math.Min(MaximumGridStationsPerAxis / 2,
                (int)Math.Ceiling((end - start) / (2 * spacing)) + 1);
            for (int offset = 1; offset <= maximumOffset; offset++)
            {
                double lower = center - offset * spacing;
                double upper = center + offset * spacing;
                bool emitted = false;
                if (lower >= start - 1e-8)
                {
                    yield return lower;
                    emitted = true;
                }
                if (upper <= end + 1e-8)
                {
                    yield return upper;
                    emitted = true;
                }
                if (!emitted) yield break;
            }
        }

        private static bool TryAddBox(BuildResult result, AccessoryVisualKind kind,
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
                Solid solid = CreateBox(x, y, z, dx, dy, dz);
                result.Solids.Add(solid);
                result.SolidsByKind[kind].Add(solid);
                return true;
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                result.SkippedSolidCount++;
                return false;
            }
        }

        private static bool TryAddOrientedBox(BuildResult result, AccessoryVisualKind kind, XYZ center,
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
                Solid solid = SolidUtils.CreateTransformed(local, transform);
                result.Solids.Add(solid);
                result.SolidsByKind[kind].Add(solid);
                return true;
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                result.SkippedSolidCount++;
                return false;
            }
        }

        private static bool TryAddBoxAlongAxis(BuildResult result, AccessoryVisualKind kind,
            XYZ start, XYZ end, double width, double depth)
        {
            XYZ vector = end - start;
            double length = vector.GetLength();
            if (!CanCreateBox(width, depth, length)) return false;
            if (result.Solids.Count >= MaximumSolidCount)
            {
                result.IsDisplayCapped = true;
                result.SkippedSolidCount++;
                return false;
            }
            try
            {
                XYZ zAxis = vector.Normalize();
                XYZ xAxis = XYZ.BasisZ.CrossProduct(zAxis);
                if (xAxis.GetLength() <= 1e-8)
                    xAxis = XYZ.BasisX;
                else
                    xAxis = xAxis.Normalize();
                XYZ yAxis = zAxis.CrossProduct(xAxis).Normalize();
                Solid local = CreateBox(-width / 2, -depth / 2, 0,
                    width, depth, length);
                Transform transform = Transform.Identity;
                transform.Origin = start;
                transform.BasisX = xAxis;
                transform.BasisY = yAxis;
                transform.BasisZ = zAxis;
                Solid solid = SolidUtils.CreateTransformed(local, transform);
                result.Solids.Add(solid);
                result.SolidsByKind[kind].Add(solid);
                return true;
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                result.SkippedSolidCount++;
                return false;
            }
            catch (ArgumentException)
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
