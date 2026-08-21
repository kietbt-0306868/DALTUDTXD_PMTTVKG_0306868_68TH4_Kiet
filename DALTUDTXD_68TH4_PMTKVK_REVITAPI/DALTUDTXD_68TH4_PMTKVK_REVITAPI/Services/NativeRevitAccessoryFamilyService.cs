using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.Services
{
    /// <summary>
    /// Creates reusable Generic Model families for preview accessories and places
    /// editable FamilyInstances. Family names encode their validated dimensions,
    /// so different per-element designs remain independently selectable/editable.
    /// </summary>
    internal sealed class NativeRevitAccessoryFamilyService
    {
        private const string FamilyPrefix = "VK_NATIVE_";
        internal const string InstanceMarker = "DALTUDTXD_NATIVE_PREVIEW_ACCESSORY";
        private readonly Document _document;
        private readonly SharedDataViewModel _data;
        private readonly IReadOnlyDictionary<string, FloorElementModel> _models;
        private readonly System.Windows.Media.Media3D.Point3D _braceCoordinateOriginMm;
        private readonly Dictionary<string, FamilySymbol> _symbols = new Dictionary<string, FamilySymbol>();
        private readonly List<string> _warnings = new List<string>();
        private readonly HashSet<(long SymbolId, long Xmm, long Ymm, long Zmm)> _placedPropLocations =
            new HashSet<(long SymbolId, long Xmm, long Ymm, long Zmm)>();
        private readonly HashSet<string> _placedScaffoldBays = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, CableTurnbucklePlacement> _cableTurnbucklePlacements =
            new Dictionary<string, CableTurnbucklePlacement>(StringComparer.Ordinal);
        public IReadOnlyList<string> Warnings => _warnings;

        internal sealed class PlacementResult
        {
            public int PropCount { get; set; }
            public int ScaffoldBraceCount { get; set; }
            public int BeamClampCount { get; set; }
            public int ColumnYokeCount { get; set; }
            public int ColumnTieRodCount { get; set; }
            public int PushPullPropCount { get; set; }
            public int TurnbuckleCount { get; set; }
            public int Total => PropCount + ScaffoldBraceCount + BeamClampCount + ColumnYokeCount + ColumnTieRodCount +
                                PushPullPropCount + TurnbuckleCount;
        }

        private sealed class CableTurnbucklePlacement
        {
            public XYZ Anchor { get; set; }
            public XYZ Attach { get; set; }
            public double LengthMm { get; set; }
        }

        public NativeRevitAccessoryFamilyService(Document document, SharedDataViewModel data,
            IEnumerable<FloorElementModel> models, System.Windows.Media.Media3D.Point3D braceCoordinateOriginMm)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _braceCoordinateOriginMm = braceCoordinateOriginMm;
            _models = (models ?? Enumerable.Empty<FloorElementModel>())
                .Where(model => model != null && !string.IsNullOrWhiteSpace(model.UniqueId))
                .GroupBy(model => model.UniqueId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Load/create all required RFA definitions. The project must not be modifiable.</summary>
        public void EnsureDefinitions(IEnumerable<(Element Element, StructuralComponentType Type)> components,
            Action<int, string> reportProgress = null, Func<bool> isCancellationRequested = null)
        {
            List<(Element Element, StructuralComponentType Type)> componentList = components.ToList();
            _cableTurnbucklePlacements.Clear();
            for (int componentIndex = 0; componentIndex < componentList.Count; componentIndex++)
            {
                ThrowIfCancelled(isCancellationRequested);
                (Element element, StructuralComponentType type) = componentList[componentIndex];
                CalculationParameters parameters = EffectiveParameters(element, type);
                if (type == StructuralComponentType.Slab || type == StructuralComponentType.Beam)
                {
                    double height = Round(parameters.CayChongHeight > 0 ? parameters.CayChongHeight : 3000, 50);
                    double diameter = Round(parameters.CayChongDiameter > 0 ? parameters.CayChongDiameter : 48, 1);
                    string key = PropKey(diameter, height);
                    TryEnsureSymbol(key, family => BuildPropFamily(family, diameter, height));
                    foreach (double bayWidth in SupportBayWidths(element, type, parameters))
                    {
                        double roundedBay = Round(bayWidth, 50);
                        double moduleHeight = ScaffoldBraceModuleHeight(height);
                        string braceKey = ScaffoldBraceKey(roundedBay, moduleHeight, diameter);
                        TryEnsureSymbol(braceKey,
                            family => BuildScaffoldXBraceFamily(family, roundedBay, moduleHeight, diameter));
                    }
                    if (type == StructuralComponentType.Beam)
                    {
                        BoundingBoxXYZ beamBox = element.get_BoundingBox(null);
                        double beamWidth = Round(parameters.BeamWidth > 0
                            ? parameters.BeamWidth : Math.Min(BoxSizeMm(beamBox, true), BoxSizeMm(beamBox, false)), 10);
                        double beamDepth = Round(parameters.BeamDepth > 0
                            ? parameters.BeamDepth : BoxHeightMm(beamBox), 10);
                        double walerWidth = Round(parameters.BeamPrimaryWalerWidth > 0
                            ? parameters.BeamPrimaryWalerWidth : 50, 5);
                        double walerDepth = Round(parameters.BeamPrimaryWalerDepth > 0
                            ? parameters.BeamPrimaryWalerDepth : 100, 5);
                        double tieDiameter = Round(parameters.BeamTieRodDiameter > 0
                            ? parameters.BeamTieRodDiameter : 16, 1);
                        string clampKey = BeamClampKey(beamWidth, beamDepth, walerWidth, walerDepth, tieDiameter);
                        TryEnsureSymbol(clampKey, family => BuildBeamClampFamily(family, beamWidth, beamDepth,
                            walerWidth, walerDepth, tieDiameter));
                    }
                }
                else if (type == StructuralComponentType.Column)
                {
                    BoundingBoxXYZ box = element.get_BoundingBox(null);
                    double width = Round(parameters.ColumnWidth > 0 ? parameters.ColumnWidth : BoxSizeMm(box, true), 10);
                    double depth = Round(parameters.ColumnDepth > 0 ? parameters.ColumnDepth : BoxSizeMm(box, false), 10);
                    double barWidth = Round(parameters.ColumnYokeWidth > 0 ? parameters.ColumnYokeWidth : 40, 5);
                    double barHeight = Round(parameters.ColumnYokeHeight > 0 ? parameters.ColumnYokeHeight : 40, 5);
                    int pairCount = Math.Max(1, parameters.ColumnYokePairCount);
                    int tieCount = Math.Max(1, parameters.ColumnTieRodCountPerFace);
                    double tieDiameter = Round(parameters.ColumnTieRodDiameter > 0
                        ? parameters.ColumnTieRodDiameter : 12, 1);
                    string key = YokeKey(width, depth, barWidth, barHeight, pairCount);
                    TryEnsureSymbol(key, family => BuildYokeFamily(family, width, depth, barWidth, barHeight,
                        pairCount));

                    double projection = 140;
                    double xLength = Round(width + 2 * 35 + 2 * projection, 10);
                    double yLength = Round(depth + 2 * 35 + 2 * projection, 10);
                    TryEnsureSymbol(TieRodKey(tieDiameter, xLength),
                        family => BuildTieRodFamily(family, tieDiameter, xLength), true);
                    TryEnsureSymbol(TieRodKey(tieDiameter, yLength),
                        family => BuildTieRodFamily(family, tieDiameter, yLength), true);
                }
                if (componentIndex % Math.Max(1, componentList.Count / 20) == 0 ||
                    componentIndex == componentList.Count - 1)
                {
                    int percent = componentList.Count == 0 ? 70 :
                        (int)Math.Round(70.0 * (componentIndex + 1) / componentList.Count);
                    reportProgress?.Invoke(percent,
                        $"Đang chuẩn bị family: {componentIndex + 1}/{componentList.Count} cấu kiện...");
                }
            }

            ThrowIfCancelled(isCancellationRequested);
            reportProgress?.Invoke(78, "Đang chuẩn bị family chống xiên...");
            foreach (ColumnBraceAssessment assessment in _data.ColumnBraceAssessmentCache.Values)
            foreach (BraceDirectionAssessment direction in assessment.Directions.Where(item => item.CanPlaceProp))
            {
                Element column = componentList.Select(item => item.Element)
                    .FirstOrDefault(item => item.Id.Value == assessment.ColumnId);
                BoundingBoxXYZ box = column?.get_BoundingBox(null);
                if (box == null) continue;
                XYZ foot = ProjectPoint(direction.ProposedFootPoint);
                XYZ attach = new XYZ((box.Min.X + box.Max.X) / 2, (box.Min.Y + box.Max.Y) / 2,
                    Mm(_braceCoordinateOriginMm.Z + assessment.SupportElevationMm +
                       assessment.ColumnHeightMm * 0.85));
                double length = Round(UnitUtils.ConvertFromInternalUnits((attach - foot).GetLength(),
                    UnitTypeId.Millimeters), 50);
                string key = PushPullKey(length);
                TryEnsureSymbol(key, family => BuildPushPullFamily(family, length), true);
            }

            List<PlanarFace> bearingFaces = CollectTopBearingFaces();
            ThrowIfCancelled(isCancellationRequested);
            reportProgress?.Invoke(90, "Đang chuẩn bị family cáp và tăng đơ...");
            foreach (ColumnBraceAssessment assessment in _data.ColumnBraceAssessmentCache.Values
                         .Where(CanResolveOpenDirectionsAutomatically))
            {
                Element column = componentList.Select(item => item.Element)
                    .FirstOrDefault(item => item.Id.Value == assessment.ColumnId);
                if (column == null) continue;
                foreach (BraceDirectionAssessment direction in assessment.Directions.Where(item => !item.CanPlaceProp))
                {
                    if (!TryCreateInwardCablePlacement(column, assessment, direction, bearingFaces,
                            out CableTurnbucklePlacement placement)) continue;
                    string placementKey = CablePlacementKey(assessment, direction);
                    _cableTurnbucklePlacements[placementKey] = placement;
                    double roundedLength = Round(placement.LengthMm, 50);
                    TryEnsureSymbol(TurnbuckleKey(roundedLength),
                        family => BuildCableTurnbuckleFamily(family, roundedLength), true);
                }
            }
            reportProgress?.Invoke(100, "Đã chuẩn bị family phụ kiện.");
        }

        /// <summary>The caller owns the project transaction.</summary>
        public PlacementResult Place(IEnumerable<(Element Element, StructuralComponentType Type)> components,
            View3D previewView, OverrideGraphicSettings graphics,
            Action<int, string> reportProgress = null, Func<bool> isCancellationRequested = null)
        {
            DeleteOwnedInstances();
            _placedPropLocations.Clear();
            _placedScaffoldBays.Clear();
            var result = new PlacementResult();
            List<(Element Element, StructuralComponentType Type)> componentList = components.ToList();
            for (int componentIndex = 0; componentIndex < componentList.Count; componentIndex++)
            {
                ThrowIfCancelled(isCancellationRequested);
                (Element element, StructuralComponentType type) = componentList[componentIndex];
                CalculationParameters parameters = EffectiveParameters(element, type);
                if (type == StructuralComponentType.Slab)
                {
                    result.PropCount += PlaceSlabProps(element, parameters, previewView, graphics);
                    result.ScaffoldBraceCount += PlaceSlabScaffoldBraces(element, parameters, previewView, graphics);
                }
                else if (type == StructuralComponentType.Beam)
                {
                    result.PropCount += PlaceBeamProps(element, parameters, previewView, graphics);
                    result.ScaffoldBraceCount += PlaceBeamScaffoldBraces(element, parameters, previewView, graphics);
                    result.BeamClampCount += PlaceBeamClamps(element, parameters, previewView, graphics);
                }
                else if (type == StructuralComponentType.Column)
                {
                    result.ColumnYokeCount += PlaceColumnYokes(element, parameters, previewView, graphics);
                    result.ColumnTieRodCount += PlaceColumnTieRods(element, parameters, previewView, graphics);
                    result.PushPullPropCount += PlaceColumnPushPullProps(element, previewView, graphics);
                    result.TurnbuckleCount += PlaceColumnTurnbuckles(element, previewView, graphics);
                }
                if (componentIndex % Math.Max(1, componentList.Count / 30) == 0 ||
                    componentIndex == componentList.Count - 1)
                {
                    int percent = componentList.Count == 0 ? 100 :
                        (int)Math.Round(100.0 * (componentIndex + 1) / componentList.Count);
                    reportProgress?.Invoke(percent,
                        $"Đang đặt phụ kiện: {componentIndex + 1}/{componentList.Count} cấu kiện...");
                }
            }
            return result;
        }

        private static void ThrowIfCancelled(Func<bool> isCancellationRequested)
        {
            if (isCancellationRequested?.Invoke() == true)
                throw new OperationCanceledException("Người dùng đã hủy dựng Family phụ kiện.");
        }

        private CalculationParameters EffectiveParameters(Element element, StructuralComponentType type)
        {
            _models.TryGetValue(element.UniqueId, out FloorElementModel model);
            CalculationParameters parameters = _data.GetEffectiveComponentParameters(model);
            parameters.ComponentType = type;
            if (model == null)
            {
                BoundingBoxXYZ box = element.get_BoundingBox(null);
                if (box != null)
                {
                    double dx = UnitUtils.ConvertFromInternalUnits(box.Max.X - box.Min.X, UnitTypeId.Millimeters);
                    double dy = UnitUtils.ConvertFromInternalUnits(box.Max.Y - box.Min.Y, UnitTypeId.Millimeters);
                    double dz = UnitUtils.ConvertFromInternalUnits(box.Max.Z - box.Min.Z, UnitTypeId.Millimeters);
                    if (type == StructuralComponentType.Column)
                    {
                        parameters.ColumnWidth = Math.Max(1, dx);
                        parameters.ColumnDepth = Math.Max(1, dy);
                        parameters.ColumnHeight = Math.Max(1, dz);
                    }
                    else if (type == StructuralComponentType.Beam)
                    {
                        parameters.BeamWidth = Math.Max(1, Math.Min(dx, dy));
                        parameters.BeamDepth = Math.Max(1, dz);
                        parameters.BeamLength = Math.Max(dx, dy);
                    }
                }
            }
            return parameters;
        }

        private int PlaceSlabProps(Element element, CalculationParameters parameters, View3D view,
            OverrideGraphicSettings graphics)
        {
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null) return 0;
            double heightMm = Round(parameters.CayChongHeight > 0 ? parameters.CayChongHeight : 3000, 50);
            double diameterMm = Round(parameters.CayChongDiameter > 0 ? parameters.CayChongDiameter : 48, 1);
            if (!_symbols.TryGetValue(PropKey(diameterMm, heightMm), out FamilySymbol symbol)) return 0;
            Activate(symbol);
            double spacing = Mm(Math.Max(300, parameters.SupportSpacing > 0 ? parameters.SupportSpacing : 1000));
            double baseZ = box.Min.Z - Mm(heightMm);
            List<PlanarFace> bottomFaces = GetPlanarFaces(element)
                .Where(face => face.FaceNormal.Z < -0.70).ToList();
            if (bottomFaces.Count == 0) return 0;

            int count = 0;
            foreach (double x in Grid(box.Min.X, box.Max.X, spacing))
            foreach (double y in Grid(box.Min.Y, box.Max.Y, spacing))
            {
                XYZ probe = new XYZ(x, y, box.Min.Z);
                if (!bottomFaces.Any(face => IsPointOnFace(face, probe))) continue;
                if (!TryNewUniqueProp(symbol, new XYZ(x, y, baseZ), out FamilyInstance instance))
                    continue;
                Mark(instance, view, graphics);
                count++;
            }
            return count;
        }

        private int PlaceColumnPushPullProps(Element element, View3D view, OverrideGraphicSettings graphics)
        {
            List<ColumnBraceAssessment> assessments = _data.ColumnBraceAssessmentCache.Values
                .Where(item => item.ColumnId == element.Id.Value).ToList();
            if (assessments.Count == 0) return 0;
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null) return 0;
            double centerX = (box.Min.X + box.Max.X) / 2;
            double centerY = (box.Min.Y + box.Max.Y) / 2;
            int count = 0;
            foreach (ColumnBraceAssessment assessment in assessments)
            foreach (BraceDirectionAssessment direction in assessment.Directions.Where(item => item.CanPlaceProp))
            {
                double attachZmm = assessment.SupportElevationMm + assessment.ColumnHeightMm * 0.85;
                XYZ foot = ProjectPoint(direction.ProposedFootPoint);
                XYZ attach = new XYZ(centerX, centerY, Mm(_braceCoordinateOriginMm.Z + attachZmm));
                XYZ vector = attach - foot;
                double lengthMm = Round(UnitUtils.ConvertFromInternalUnits(vector.GetLength(), UnitTypeId.Millimeters), 50);
                if (!_symbols.TryGetValue(PushPullKey(lengthMm), out FamilySymbol symbol)) continue;
                Activate(symbol);
                Level level = new FilteredElementCollector(_document).OfClass(typeof(Level)).Cast<Level>()
                    .OrderBy(item => Math.Abs(item.Elevation - foot.Z)).FirstOrDefault();
                if (level == null) continue;
                // A curve-based Generic Model is oriented by its placement line. This
                // avoids RotateElement, which Revit rejects for one-level point families.
                FamilyInstance instance = _document.Create.NewFamilyInstance(
                    Line.CreateBound(foot, attach), symbol, level, StructuralType.NonStructural);
                Mark(instance, view, graphics);
                count++;
            }
            return count;
        }

        private int PlaceColumnTurnbuckles(Element element, View3D view, OverrideGraphicSettings graphics)
        {
            List<ColumnBraceAssessment> assessments = _data.ColumnBraceAssessmentCache.Values
                .Where(item => item.ColumnId == element.Id.Value).ToList();
            if (assessments.Count == 0) return 0;
            Level level = new FilteredElementCollector(_document).OfClass(typeof(Level)).Cast<Level>()
                .OrderBy(item => Math.Abs(item.Elevation - element.get_BoundingBox(null).Min.Z)).FirstOrDefault();
            if (level == null) return 0;
            int count = 0;
            foreach (ColumnBraceAssessment assessment in assessments)
            foreach (BraceDirectionAssessment direction in assessment.Directions.Where(item => !item.CanPlaceProp))
            {
                if (!_cableTurnbucklePlacements.TryGetValue(CablePlacementKey(assessment, direction),
                        out CableTurnbucklePlacement placement)) continue;
                double roundedLength = Round(placement.LengthMm, 50);
                if (!_symbols.TryGetValue(TurnbuckleKey(roundedLength), out FamilySymbol symbol)) continue;
                Activate(symbol);
                FamilyInstance instance = _document.Create.NewFamilyInstance(
                    Line.CreateBound(placement.Anchor, placement.Attach), symbol, level,
                    StructuralType.NonStructural);
                Mark(instance, view, graphics);
                direction.WasResolvedByAlternativeRestraint = true;
                count++;
            }
            return count;
        }

        private List<PlanarFace> CollectTopBearingFaces()
        {
            var categories = new List<BuiltInCategory>
            {
                BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_StructuralFoundation
            };
            return new FilteredElementCollector(_document)
                .WherePasses(new ElementMulticategoryFilter(categories))
                .WhereElementIsNotElementType()
                .ToElements()
                .SelectMany(GetPlanarFaces)
                .Where(face => face.FaceNormal.Z > 0.70)
                .ToList();
        }

        private bool TryCreateInwardCablePlacement(Element column, ColumnBraceAssessment assessment,
            BraceDirectionAssessment direction, IReadOnlyCollection<PlanarFace> bearingFaces,
            out CableTurnbucklePlacement placement)
        {
            placement = null;
            BoundingBoxXYZ box = column.get_BoundingBox(null);
            if (box == null || bearingFaces == null || bearingFaces.Count == 0) return false;
            var open = new XYZ(direction.LocalDirection.X, direction.LocalDirection.Y, 0);
            if (open.GetLength() < 1e-9) return false;
            open = open.Normalize();
            XYZ center = new XYZ((box.Min.X + box.Max.X) / 2, (box.Min.Y + box.Max.Y) / 2, 0);
            double supportZ = Mm(_braceCoordinateOriginMm.Z + assessment.SupportElevationMm);
            double attachZ = Mm(_braceCoordinateOriginMm.Z + assessment.SupportElevationMm +
                                assessment.ColumnHeightMm * 0.82);
            double faceOffset = Math.Abs(open.X) * (box.Max.X - box.Min.X) / 2 +
                                Math.Abs(open.Y) * (box.Max.Y - box.Min.Y) / 2 + Mm(35);
            XYZ attach = new XYZ(center.X, center.Y, attachZ) + open * faceOffset;
            double preferredMm = Math.Min(1800, Math.Max(600, assessment.BraceRunMm * 0.55));
            for (double distanceMm = preferredMm; distanceMm >= 300; distanceMm -= 100)
            {
                XYZ anchorProbe = new XYZ(center.X, center.Y, supportZ) - open * Mm(distanceMm);
                if (!bearingFaces.Any(face => IsPointOnFace(face, anchorProbe))) continue;
                XYZ anchor = anchorProbe + XYZ.BasisZ * Mm(80);
                double lengthMm = UnitUtils.ConvertFromInternalUnits((attach - anchor).GetLength(),
                    UnitTypeId.Millimeters);
                if (lengthMm < 500) continue;
                placement = new CableTurnbucklePlacement
                {
                    Anchor = anchor,
                    Attach = attach,
                    LengthMm = lengthMm
                };
                return true;
            }
            return false;
        }

        private static bool CanResolveOpenDirectionsAutomatically(ColumnBraceAssessment assessment) =>
            assessment.IsManuallyOverridden ||
            assessment.DerivedType == ColumnBraceDerivedType.Edge ||
            assessment.DerivedType == ColumnBraceDerivedType.Corner ||
            assessment.DerivedType == ColumnBraceDerivedType.FoundationGround;

        private static string CablePlacementKey(ColumnBraceAssessment assessment,
            BraceDirectionAssessment direction) => $"{assessment.CacheKey}:{direction.DirectionIndex}";

        private int PlaceBeamProps(Element element, CalculationParameters parameters, View3D view,
            OverrideGraphicSettings graphics)
        {
            if (!(element.Location is LocationCurve location) || location.Curve == null) return 0;
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null) return 0;
            double heightMm = Round(parameters.CayChongHeight > 0 ? parameters.CayChongHeight : 3000, 50);
            double diameterMm = Round(parameters.CayChongDiameter > 0 ? parameters.CayChongDiameter : 48, 1);
            if (!_symbols.TryGetValue(PropKey(diameterMm, heightMm), out FamilySymbol symbol)) return 0;
            Activate(symbol);

            Curve curve = location.Curve;
            double spacing = Mm(Math.Max(300, parameters.BeamPropSpacing > 0 ? parameters.BeamPropSpacing : 1000));
            int stations = Math.Max(2, (int)Math.Ceiling(curve.Length / spacing) + 1);
            int rows = parameters.BeamWidth >= 500 ? 2 : 1;
            double rowOffset = rows == 2 ? Mm(parameters.BeamWidth * 0.25) : 0;
            double baseZ = box.Min.Z - Mm(heightMm);
            int count = 0;
            for (int index = 0; index < stations; index++)
            {
                double t = stations == 1 ? 0.5 : (double)index / (stations - 1);
                XYZ point = curve.Evaluate(t, true);
                XYZ tangent = curve.ComputeDerivatives(t, true).BasisX.Normalize();
                XYZ cross = new XYZ(-tangent.Y, tangent.X, 0).Normalize();
                for (int row = 0; row < rows; row++)
                {
                    double offset = rows == 1 ? 0 : (row == 0 ? -rowOffset : rowOffset);
                    XYZ origin = new XYZ(point.X + cross.X * offset, point.Y + cross.Y * offset, baseZ);
                    if (!TryNewUniqueProp(symbol, origin, out FamilyInstance instance))
                        continue;
                    Mark(instance, view, graphics);
                    count++;
                }
            }
            return count;
        }

        private int PlaceBeamClamps(Element element, CalculationParameters parameters, View3D view,
            OverrideGraphicSettings graphics)
        {
            if (!(element.Location is LocationCurve location) || location.Curve == null) return 0;
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null) return 0;
            Curve curve = location.Curve;
            double beamWidth = Round(parameters.BeamWidth > 0
                ? parameters.BeamWidth : Math.Min(BoxSizeMm(box, true), BoxSizeMm(box, false)), 10);
            double beamDepth = Round(parameters.BeamDepth > 0 ? parameters.BeamDepth : BoxHeightMm(box), 10);
            double walerWidth = Round(parameters.BeamPrimaryWalerWidth > 0
                ? parameters.BeamPrimaryWalerWidth : 50, 5);
            double walerDepth = Round(parameters.BeamPrimaryWalerDepth > 0
                ? parameters.BeamPrimaryWalerDepth : 100, 5);
            double tieDiameter = Round(parameters.BeamTieRodDiameter > 0 ? parameters.BeamTieRodDiameter : 16, 1);
            if (!_symbols.TryGetValue(BeamClampKey(beamWidth, beamDepth, walerWidth, walerDepth, tieDiameter),
                    out FamilySymbol symbol)) return 0;
            Activate(symbol);

            double lengthMm = UnitUtils.ConvertFromInternalUnits(curve.Length, UnitTypeId.Millimeters);
            List<double> stations = FormworkCalculationService.BuildBeamClampStations(lengthMm,
                Math.Max(75, parameters.BeamTieRodSpacing));
            int count = 0;
            foreach (double stationMm in stations)
            {
                double t = lengthMm <= 1e-6 ? 0.5 : Math.Max(0, Math.Min(1, stationMm / lengthMm));
                XYZ point = curve.Evaluate(t, true);
                XYZ tangent = curve.ComputeDerivatives(t, true).BasisX;
                double rotation = Math.Atan2(tangent.Y, tangent.X);
                XYZ origin = new XYZ(point.X, point.Y, box.Min.Z);
                FamilyInstance instance = NewInstance(symbol, origin);
                if (Math.Abs(rotation) > 1e-9)
                    ElementTransformUtils.RotateElement(_document, instance.Id,
                        Line.CreateBound(origin, origin + XYZ.BasisZ), rotation);
                Mark(instance, view, graphics);
                count++;
            }
            return count;
        }

        private IEnumerable<double> SupportBayWidths(Element element, StructuralComponentType type,
            CalculationParameters parameters)
        {
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null) yield break;
            if (type == StructuralComponentType.Slab)
            {
                double spacing = Mm(Math.Max(300, parameters.SupportSpacing > 0
                    ? parameters.SupportSpacing : 1000));
                List<double> xs = Grid(box.Min.X, box.Max.X, spacing).ToList();
                List<double> ys = Grid(box.Min.Y, box.Max.Y, spacing).ToList();
                if (xs.Count > 1) yield return UnitUtils.ConvertFromInternalUnits(xs[1] - xs[0], UnitTypeId.Millimeters);
                if (ys.Count > 1) yield return UnitUtils.ConvertFromInternalUnits(ys[1] - ys[0], UnitTypeId.Millimeters);
            }
            else if (type == StructuralComponentType.Beam && element.Location is LocationCurve location &&
                     location.Curve != null)
            {
                double spacing = Mm(Math.Max(300, parameters.BeamPropSpacing > 0
                    ? parameters.BeamPropSpacing : 1000));
                int stations = Math.Max(2, (int)Math.Ceiling(location.Curve.Length / spacing) + 1);
                yield return UnitUtils.ConvertFromInternalUnits(location.Curve.Length / (stations - 1),
                    UnitTypeId.Millimeters);
            }
        }

        private int PlaceSlabScaffoldBraces(Element element, CalculationParameters parameters, View3D view,
            OverrideGraphicSettings graphics)
        {
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null) return 0;
            double heightMm = Round(parameters.CayChongHeight > 0 ? parameters.CayChongHeight : 3000, 50);
            double diameterMm = Round(parameters.CayChongDiameter > 0 ? parameters.CayChongDiameter : 48, 1);
            double spacing = Mm(Math.Max(300, parameters.SupportSpacing > 0 ? parameters.SupportSpacing : 1000));
            double baseZ = box.Min.Z - Mm(heightMm);
            List<double> xs = Grid(box.Min.X, box.Max.X, spacing).ToList();
            List<double> ys = Grid(box.Min.Y, box.Max.Y, spacing).ToList();
            List<PlanarFace> bottomFaces = GetPlanarFaces(element).Where(face => face.FaceNormal.Z < -0.70).ToList();
            if (bottomFaces.Count == 0) return 0;
            int count = 0;

            // Brace the outside support planes. Every second bay is sufficient for a
            // readable/stable scaffold system and avoids the previous wall of loose rods.
            foreach (double y in new[] { ys.First(), ys.Last() }.Distinct())
            for (int index = 0; index + 1 < xs.Count; index += 2)
                count += PlaceScaffoldBay(xs[index], y, xs[index + 1], y, baseZ, heightMm, diameterMm,
                    bottomFaces, view, graphics);
            foreach (double x in new[] { xs.First(), xs.Last() }.Distinct())
            for (int index = 0; index + 1 < ys.Count; index += 2)
                count += PlaceScaffoldBay(x, ys[index], x, ys[index + 1], baseZ, heightMm, diameterMm,
                    bottomFaces, view, graphics);
            return count;
        }

        private int PlaceBeamScaffoldBraces(Element element, CalculationParameters parameters, View3D view,
            OverrideGraphicSettings graphics)
        {
            if (!(element.Location is LocationCurve location) || location.Curve == null) return 0;
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null) return 0;
            Curve curve = location.Curve;
            double heightMm = Round(parameters.CayChongHeight > 0 ? parameters.CayChongHeight : 3000, 50);
            double diameterMm = Round(parameters.CayChongDiameter > 0 ? parameters.CayChongDiameter : 48, 1);
            double spacing = Mm(Math.Max(300, parameters.BeamPropSpacing > 0 ? parameters.BeamPropSpacing : 1000));
            int stations = Math.Max(2, (int)Math.Ceiling(curve.Length / spacing) + 1);
            int rows = parameters.BeamWidth >= 500 ? 2 : 1;
            double rowOffset = rows == 2 ? Mm(parameters.BeamWidth * 0.25) : 0;
            double baseZ = box.Min.Z - Mm(heightMm);
            int count = 0;
            for (int index = 0; index + 1 < stations; index += 2)
            {
                double t0 = (double)index / (stations - 1);
                double t1 = (double)(index + 1) / (stations - 1);
                XYZ p0 = curve.Evaluate(t0, true);
                XYZ p1 = curve.Evaluate(t1, true);
                XYZ tangent = curve.ComputeDerivatives((t0 + t1) / 2, true).BasisX.Normalize();
                XYZ cross = new XYZ(-tangent.Y, tangent.X, 0).Normalize();
                for (int row = 0; row < rows; row++)
                {
                    double offset = rows == 1 ? 0 : (row == 0 ? -rowOffset : rowOffset);
                    count += PlaceScaffoldBay(p0.X + cross.X * offset, p0.Y + cross.Y * offset,
                        p1.X + cross.X * offset, p1.Y + cross.Y * offset, baseZ, heightMm, diameterMm,
                        null, view, graphics);
                }
            }
            return count;
        }

        private int PlaceScaffoldBay(double x0, double y0, double x1, double y1, double baseZ,
            double heightMm, double diameterMm, IReadOnlyCollection<PlanarFace> supportFaces, View3D view,
            OverrideGraphicSettings graphics)
        {
            if (supportFaces != null)
            {
                double topZ = baseZ + Mm(heightMm);
                if (!supportFaces.Any(face => IsPointOnFace(face, new XYZ(x0, y0, topZ))) ||
                    !supportFaces.Any(face => IsPointOnFace(face, new XYZ(x1, y1, topZ)))) return 0;
            }
            double bayMm = Round(UnitUtils.ConvertFromInternalUnits(
                new XYZ(x1 - x0, y1 - y0, 0).GetLength(), UnitTypeId.Millimeters), 50);
            double moduleHeightMm = ScaffoldBraceModuleHeight(heightMm);
            if (!_symbols.TryGetValue(ScaffoldBraceKey(bayMm, moduleHeightMm, diameterMm), out FamilySymbol symbol))
                return 0;
            Activate(symbol);
            double rotation = Math.Atan2(y1 - y0, x1 - x0);
            int moduleCount = Math.Max(1, (int)Math.Ceiling(heightMm / moduleHeightMm));
            int placed = 0;
            for (int tier = 0; tier < moduleCount; tier++)
            {
                double tierZ = baseZ + Mm(moduleHeightMm * tier);
                string placementKey = string.Join("|", symbol.Id.Value,
                    Math.Round(UnitUtils.ConvertFromInternalUnits(x0, UnitTypeId.Millimeters)),
                    Math.Round(UnitUtils.ConvertFromInternalUnits(y0, UnitTypeId.Millimeters)),
                    Math.Round(UnitUtils.ConvertFromInternalUnits(x1, UnitTypeId.Millimeters)),
                    Math.Round(UnitUtils.ConvertFromInternalUnits(y1, UnitTypeId.Millimeters)),
                    Math.Round(UnitUtils.ConvertFromInternalUnits(tierZ, UnitTypeId.Millimeters)));
                if (!_placedScaffoldBays.Add(placementKey)) continue;
                XYZ origin = new XYZ(x0, y0, tierZ);
                FamilyInstance instance = NewInstance(symbol, origin);
                if (Math.Abs(rotation) > 1e-9)
                    ElementTransformUtils.RotateElement(_document, instance.Id,
                        Line.CreateBound(origin, origin + XYZ.BasisZ), rotation);
                Mark(instance, view, graphics);
                placed++;
            }
            return placed;
        }

        private int PlaceColumnYokes(Element element, CalculationParameters parameters, View3D view,
            OverrideGraphicSettings graphics)
        {
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null) return 0;
            double widthMm = Round(parameters.ColumnWidth > 0 ? parameters.ColumnWidth : BoxSizeMm(box, true), 10);
            double depthMm = Round(parameters.ColumnDepth > 0 ? parameters.ColumnDepth : BoxSizeMm(box, false), 10);
            double barWidth = Round(parameters.ColumnYokeWidth > 0 ? parameters.ColumnYokeWidth : 40, 5);
            double barHeight = Round(parameters.ColumnYokeHeight > 0 ? parameters.ColumnYokeHeight : 40, 5);
            int pairCount = Math.Max(1, parameters.ColumnYokePairCount);
            if (!_symbols.TryGetValue(YokeKey(widthMm, depthMm, barWidth, barHeight, pairCount),
                    out FamilySymbol symbol)) return 0;
            Activate(symbol);

            XYZ center = new XYZ((box.Min.X + box.Max.X) / 2, (box.Min.Y + box.Max.Y) / 2, 0);
            double rotation = element.Location is LocationPoint locationPoint ? locationPoint.Rotation : 0;
            int count = 0;
            parameters.ColumnHeight = UnitUtils.ConvertFromInternalUnits(
                box.Max.Z - box.Min.Z, UnitTypeId.Millimeters);
            foreach (double elevationMm in FormworkCalculationService.BuildColumnYokeElevations(parameters))
            {
                double z = box.Min.Z + Mm(elevationMm);
                FamilyInstance instance = NewInstance(symbol, new XYZ(center.X, center.Y, z));
                if (Math.Abs(rotation) > 1e-9)
                    ElementTransformUtils.RotateElement(_document, instance.Id,
                        Line.CreateBound(new XYZ(center.X, center.Y, z - 1), new XYZ(center.X, center.Y, z + 1)), rotation);
                Mark(instance, view, graphics);
                count++;
            }
            return count;
        }

        private int PlaceColumnTieRods(Element element, CalculationParameters parameters, View3D view,
            OverrideGraphicSettings graphics)
        {
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null) return 0;

            double widthMm = Round(parameters.ColumnWidth > 0 ? parameters.ColumnWidth : BoxSizeMm(box, true), 10);
            double depthMm = Round(parameters.ColumnDepth > 0 ? parameters.ColumnDepth : BoxSizeMm(box, false), 10);
            double tieDiameter = Round(parameters.ColumnTieRodDiameter > 0
                ? parameters.ColumnTieRodDiameter : 12, 1);
            double projectionMm = 140;
            double clearanceMm = 35;
            double xLengthMm = Round(widthMm + 2 * clearanceMm + 2 * projectionMm, 10);
            double yLengthMm = Round(depthMm + 2 * clearanceMm + 2 * projectionMm, 10);
            if (!_symbols.TryGetValue(TieRodKey(tieDiameter, xLengthMm), out FamilySymbol xSymbol) ||
                !_symbols.TryGetValue(TieRodKey(tieDiameter, yLengthMm), out FamilySymbol ySymbol)) return 0;
            Activate(xSymbol);
            Activate(ySymbol);

            XYZ center = new XYZ((box.Min.X + box.Max.X) / 2, (box.Min.Y + box.Max.Y) / 2, 0);
            double rotation = element.Location is LocationPoint locationPoint ? locationPoint.Rotation : 0;
            XYZ localX = new XYZ(Math.Cos(rotation), Math.Sin(rotation), 0);
            XYZ localY = new XYZ(-Math.Sin(rotation), Math.Cos(rotation), 0);
            double halfWidth = Mm(widthMm / 2 + clearanceMm + projectionMm);
            double halfDepth = Mm(depthMm / 2 + clearanceMm + projectionMm);
            double faceWidth = Mm(widthMm + 2 * clearanceMm);
            double faceDepth = Mm(depthMm + 2 * clearanceMm);
            int ties = Math.Max(1, Math.Min(8, parameters.ColumnTieRodCountPerFace));
            parameters.ColumnHeight = UnitUtils.ConvertFromInternalUnits(box.Max.Z - box.Min.Z,
                UnitTypeId.Millimeters);
            Level level = new FilteredElementCollector(_document).OfClass(typeof(Level)).Cast<Level>()
                .OrderBy(item => Math.Abs(item.Elevation - box.Min.Z)).FirstOrDefault();
            if (level == null) return 0;

            int count = 0;
            foreach (double elevationMm in FormworkCalculationService.BuildColumnYokeElevations(parameters))
            {
                double z = box.Min.Z + Mm(elevationMm);
                for (int index = 0; index < ties; index++)
                {
                    double ratio = ties == 1 ? 0.5 : (double)index / (ties - 1);
                    double offsetY = -faceDepth / 2 + ratio * faceDepth;
                    double offsetX = -faceWidth / 2 + ratio * faceWidth;

                    XYZ xCenter = new XYZ(center.X, center.Y, z) + localY * offsetY;
                    FamilyInstance xInstance = _document.Create.NewFamilyInstance(
                        Line.CreateBound(xCenter - localX * halfWidth, xCenter + localX * halfWidth),
                        xSymbol, level, StructuralType.NonStructural);
                    Mark(xInstance, view, graphics);
                    count++;

                    XYZ yCenter = new XYZ(center.X, center.Y, z) + localX * offsetX;
                    FamilyInstance yInstance = _document.Create.NewFamilyInstance(
                        Line.CreateBound(yCenter - localY * halfDepth, yCenter + localY * halfDepth),
                        ySymbol, level, StructuralType.NonStructural);
                    Mark(yInstance, view, graphics);
                    count++;
                }
            }
            return count;
        }

        private void TryEnsureSymbol(string familyName, Action<Document> buildGeometry,
            bool lineBased = false)
        {
            if (_symbols.ContainsKey(familyName)) return;
            try
            {
                EnsureSymbol(familyName, buildGeometry, lineBased);
            }
            catch (Exception exception)
            {
                string warning = $"{familyName}: {exception.Message}";
                if (!_warnings.Contains(warning)) _warnings.Add(warning);
                System.Diagnostics.Debug.WriteLine("Native accessory family warning: " + warning);
            }
        }

        private void EnsureSymbol(string familyName, Action<Document> buildGeometry, bool lineBased)
        {
            FamilySymbol existing = FindSymbol(familyName);
            if (existing != null)
            {
                _symbols[familyName] = existing;
                return;
            }

            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DALTUDTXD_RevitAPI", "GeneratedFamilies");
            Directory.CreateDirectory(folder);
            string familyPath = Path.Combine(folder, familyName + ".rfa");
            Family loadedFamily = CreateAndLoadFamily(familyPath, buildGeometry, lineBased);
            if (loadedFamily == null)
            {
                existing = FindSymbol(familyName);
                if (existing != null)
                {
                    _symbols[familyName] = existing;
                    return;
                }
                throw new InvalidOperationException($"Revit không trả về family sau khi nạp {familyName}.");
            }
            FamilySymbol symbol = loadedFamily.GetFamilySymbolIds().Select(id => _document.GetElement(id) as FamilySymbol)
                .FirstOrDefault(item => item != null);
            if (symbol == null) throw new InvalidOperationException($"Family {familyName} không có FamilySymbol.");
            _symbols[familyName] = symbol;
        }

        private FamilySymbol FindSymbol(string familyName)
        {
            Family family = new FilteredElementCollector(_document).OfClass(typeof(Family)).Cast<Family>()
                .FirstOrDefault(item => string.Equals(item.Name, familyName, StringComparison.OrdinalIgnoreCase));
            if (family != null)
                return family.GetFamilySymbolIds().Select(id => _document.GetElement(id) as FamilySymbol)
                    .FirstOrDefault(item => item != null);
            return new FilteredElementCollector(_document).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
                .FirstOrDefault(symbol => string.Equals(symbol.FamilyName, familyName,
                    StringComparison.OrdinalIgnoreCase));
        }

        private Family CreateAndLoadFamily(string path, Action<Document> buildGeometry, bool lineBased)
        {
            string template = FindGenericModelTemplate(lineBased);
            Document family = _document.Application.NewFamilyDocument(template);
            try
            {
                using (var transaction = new Transaction(family, "Tạo hình học phụ kiện ván khuôn"))
                {
                    transaction.Start();
                    Parameter alwaysVertical = family.OwnerFamily?
                        .get_Parameter(BuiltInParameter.FAMILY_ALWAYS_VERTICAL);
                    if (alwaysVertical != null && !alwaysVertical.IsReadOnly)
                        alwaysVertical.Set(0);
                    buildGeometry(family);
                    transaction.Commit();
                }
                family.SaveAs(path, new SaveAsOptions
                {
                    OverwriteExistingFile = true,
                    MaximumBackups = 1
                });
                // Loading from the live FamilyDocument is more reliable than loading
                // the just-saved path and correctly handles an already present family.
                return family.LoadFamily(_document, new ReloadFamilyOptions());
            }
            finally
            {
                family.Close(false);
            }
        }

        private string FindGenericModelTemplate(bool lineBased)
        {
            string root = _document.Application.FamilyTemplatePath;
            if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
            {
                string templateName = lineBased
                    ? "Metric Generic Model line based.rft"
                    : "Metric Generic Model.rft";
                string exact = Directory.EnumerateFiles(root, templateName, SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(exact)) return exact;
            }
            throw new FileNotFoundException(lineBased
                ? "Không tìm thấy template 'Metric Generic Model line based.rft' của Revit."
                : "Không tìm thấy template 'Metric Generic Model.rft' của Revit.");
        }

        private static void BuildPropFamily(Document family, double diameterMm, double heightMm)
        {
            double height = Mm(Math.Max(900, heightMm));
            double plate = Mm(150), plateThickness = Mm(8);
            double outerRadius = Mm(Math.Max(48, diameterMm)) / 2;
            double innerRadius = outerRadius * 0.78;
            double screwRadius = Math.Max(Mm(9), innerRadius * 0.42);
            double lowerScrewHeight = Math.Min(Mm(300), height * 0.16);
            double uHeadHeight = Math.Min(Mm(145), height * 0.09);
            double tubeBottom = plateThickness + lowerScrewHeight * 0.72;
            double outerTop = height * 0.64;
            double innerBottom = height * 0.46;
            double innerTop = height - uHeadHeight;

            // Complete support assembly from the tutorial: base plate + threaded
            // jack, telescoping outer/inner tubes, adjustment collar and U-head.
            AddFreeForm(family, Box(-plate / 2, -plate / 2, 0, plate, plate, plateThickness));
            AddFreeForm(family, CylinderAlongAxis(new XYZ(0, 0, plateThickness), XYZ.BasisZ,
                lowerScrewHeight, screwRadius));
            AddThreadCollars(family, plateThickness + Mm(18), lowerScrewHeight - Mm(25),
                screwRadius, XYZ.BasisZ);

            AddFreeForm(family, CylinderAlongAxis(new XYZ(0, 0, tubeBottom), XYZ.BasisZ,
                Math.Max(Mm(300), outerTop - tubeBottom), outerRadius));
            AddFreeForm(family, CylinderAlongAxis(new XYZ(0, 0, innerBottom), XYZ.BasisZ,
                Math.Max(Mm(300), innerTop - innerBottom), innerRadius));

            double collarHeight = Mm(55);
            double collarZ = innerBottom - collarHeight * 0.45;
            AddFreeForm(family, CylinderAlongAxis(new XYZ(0, 0, collarZ), XYZ.BasisZ,
                collarHeight, outerRadius + Mm(10)));
            AddFreeForm(family, CylinderAlongAxis(new XYZ(0, 0, collarZ + collarHeight), XYZ.BasisZ,
                Mm(20), outerRadius + Mm(15)));
            AddFreeForm(family, CylinderAlongAxis(new XYZ(-Mm(95), 0, collarZ + collarHeight + Mm(3)),
                XYZ.BasisX, Mm(190), Mm(8)));

            // Repeated collars on the exposed inner tube indicate adjustment holes
            // and make the telescoping range visible without expensive perforations.
            double holePitch = Mm(100);
            for (double z = innerBottom + Mm(110); z < innerTop - Mm(80); z += holePitch)
                AddFreeForm(family, CylinderAlongAxis(new XYZ(-innerRadius - Mm(2), 0, z), XYZ.BasisX,
                    2 * innerRadius + Mm(4), Mm(4.5)));

            // Four-way fork head, matching the tutorial's finished prop family.
            // The opposed X and Y cheeks retain either orientation of the primary
            // waler, so rotating the placed prop is no longer required merely to
            // seat a perpendicular member.
            double headPlateZ = height - uHeadHeight;
            double headWidth = Mm(170), headDepth = Mm(170), headThickness = Mm(10);
            AddFreeForm(family, Box(-headWidth / 2, -headDepth / 2, headPlateZ,
                headWidth, headDepth, headThickness));
            double cheekThickness = Mm(10), cheekHeight = Math.Max(Mm(60), uHeadHeight - headThickness);
            AddFreeForm(family, Box(-headWidth / 2, -headDepth / 2, headPlateZ + headThickness,
                cheekThickness, headDepth, cheekHeight));
            AddFreeForm(family, Box(headWidth / 2 - cheekThickness, -headDepth / 2,
                headPlateZ + headThickness, cheekThickness, headDepth, cheekHeight));
            AddFreeForm(family, Box(-headWidth / 2, -headDepth / 2, headPlateZ + headThickness,
                headWidth, cheekThickness, cheekHeight));
            AddFreeForm(family, Box(-headWidth / 2, headDepth / 2 - cheekThickness,
                headPlateZ + headThickness, headWidth, cheekThickness, cheekHeight));
        }

        private static void AddThreadCollars(Document family, double startZ, double length,
            double coreRadius, XYZ axis)
        {
            double pitch = Mm(18);
            for (double offset = 0; offset < Math.Max(0, length); offset += pitch)
                AddFreeForm(family, CylinderAlongAxis(new XYZ(0, 0, startZ + offset), axis,
                    Mm(4), coreRadius + Mm(1.8)));
        }

        private static void BuildScaffoldXBraceFamily(Document family, double bayWidthMm,
            double bayHeightMm, double diameterMm)
        {
            double width = Mm(Math.Max(300, bayWidthMm));
            double height = Mm(Math.Max(600, bayHeightMm));
            // A diagonal brace is a separate light member in the tutorial; it must
            // not inherit the vertical prop diameter. For a normal D48-D60 support
            // this produces the usual approximately D21-D27 brace range.
            double braceDiameterMm = ScaffoldBraceDiameter(diameterMm);
            double radius = Mm(braceDiameterMm) / 2.0;
            double inset = Mm(45);
            XYZ lowerLeft = new XYZ(inset, 0, inset);
            XYZ lowerRight = new XYZ(width - inset, 0, inset);
            XYZ upperLeft = new XYZ(inset, 0, height - inset);
            XYZ upperRight = new XYZ(width - inset, 0, height - inset);

            // The tutorial builds a complete reusable bay from nested support and
            // bracing families. Props remain separate shared-node instances here to
            // prevent adjacent bays from creating coincident Revit instances; this
            // family supplies the equivalent constrained brace-and-ledger module.
            AddFreeForm(family, CylinderBetween(lowerLeft, upperRight, radius));
            AddFreeForm(family, CylinderBetween(upperLeft, lowerRight, radius));

            // Horizontal ledgers make the two support nodes act as one bay frame.
            // They were missing from the previous X-only representation.
            double ledgerRadius = Math.Max(Mm(12), radius * 0.72);
            AddFreeForm(family, CylinderBetween(lowerLeft, lowerRight, ledgerRadius));
            AddFreeForm(family, CylinderBetween(upperLeft, upperRight, ledgerRadius));

            // H-frame infill from the scaffold-family tutorial. The unique prop
            // instances placed at the two bay nodes act as the outer standards;
            // this family adds the two inner ladder uprights, side rungs and the
            // lower knee returns without duplicating those shared standards.
            if (width > Mm(600))
            {
                double innerOffset = Math.Min(Mm(320), Math.Max(Mm(180), width * 0.18));
                double leftOuterX = inset;
                double rightOuterX = width - inset;
                double leftInnerX = leftOuterX + innerOffset;
                double rightInnerX = rightOuterX - innerOffset;
                double topZ = height - inset;
                double kneeZ = Math.Min(height * 0.32, Mm(520));
                double innerBottomZ = Math.Min(topZ - Mm(250), kneeZ + Mm(190));
                double frameRadius = Math.Max(Mm(10), radius * 0.90);

                AddFreeForm(family, CylinderBetween(new XYZ(leftInnerX, 0, innerBottomZ),
                    new XYZ(leftInnerX, 0, topZ), frameRadius));
                AddFreeForm(family, CylinderBetween(new XYZ(rightInnerX, 0, innerBottomZ),
                    new XYZ(rightInnerX, 0, topZ), frameRadius));

                // Short inclined pieces approximate the fabricated curved returns
                // while remaining robust FreeForm solids in Revit family documents.
                AddFreeForm(family, CylinderBetween(new XYZ(leftOuterX, 0, kneeZ),
                    new XYZ(leftInnerX, 0, innerBottomZ), frameRadius));
                AddFreeForm(family, CylinderBetween(new XYZ(rightOuterX, 0, kneeZ),
                    new XYZ(rightInnerX, 0, innerBottomZ), frameRadius));

                double centralRailZ = Math.Max(innerBottomZ + Mm(180), topZ - Mm(260));
                AddFreeForm(family, CylinderBetween(new XYZ(leftInnerX, 0, centralRailZ),
                    new XYZ(rightInnerX, 0, centralRailZ), frameRadius));

                foreach (double rungZ in new[]
                         {
                             Math.Min(topZ - Mm(120), height * 0.78),
                             Math.Max(kneeZ + Mm(90), height * 0.52)
                         }.Distinct())
                {
                    AddFreeForm(family, CylinderBetween(new XYZ(leftOuterX, 0, rungZ),
                        new XYZ(leftInnerX, 0, rungZ), frameRadius));
                    AddFreeForm(family, CylinderBetween(new XYZ(rightInnerX, 0, rungZ),
                        new XYZ(rightOuterX, 0, rungZ), frameRadius));
                }
            }

            // End sleeves and transverse locking pins represent the four nested-family
            // connections shown in the assembly workflow while keeping this preview
            // family light enough for a project-wide model.
            foreach (XYZ endpoint in new[] { lowerLeft, lowerRight, upperLeft, upperRight })
            {
                AddFreeForm(family, CylinderAlongAxis(endpoint - XYZ.BasisY * Mm(32), XYZ.BasisY,
                    Mm(64), radius + Mm(6)));
                AddFreeForm(family, CylinderAlongAxis(endpoint - XYZ.BasisY * Mm(42), XYZ.BasisY,
                    Mm(84), Mm(7)));
            }
        }

        private static void BuildBeamClampFamily(Document family, double beamWidthMm, double beamDepthMm,
            double walerWidthMm, double walerDepthMm, double tieDiameterMm)
        {
            double beamWidth = Mm(Math.Max(100, beamWidthMm));
            double beamDepth = Mm(Math.Max(100, beamDepthMm));
            double walerWidth = Mm(Math.Max(25, walerWidthMm));
            double walerDepth = Mm(Math.Max(40, walerDepthMm));
            double rodRadius = Mm(Math.Max(8, tieDiameterMm)) / 2;
            double plywoodAllowance = Mm(25);
            double walerCenter = beamWidth / 2 + plywoodAllowance + walerDepth / 2;
            double walerHeight = beamDepth + Mm(260);
            double walerBottom = -Mm(80);

            // Paired vertical walers shown in the beam clamp tutorial.
            AddFreeForm(family, Box(-walerWidth / 2, -walerCenter - walerDepth / 2, walerBottom,
                walerWidth, walerDepth, walerHeight));
            AddFreeForm(family, Box(-walerWidth / 2, walerCenter - walerDepth / 2, walerBottom,
                walerWidth, walerDepth, walerHeight));

            double rodProjection = Mm(130);
            double rodLength = 2 * (walerCenter + walerDepth / 2 + rodProjection);
            foreach (double z in new[] { beamDepth * 0.30, beamDepth * 0.78 })
            {
                double rodStartY = -rodLength / 2;
                AddFreeForm(family, CylinderAlongAxis(new XYZ(0, rodStartY, z), XYZ.BasisY,
                    rodLength, rodRadius));

                foreach (double sign in new[] { -1.0, 1.0 })
                {
                    double outerFaceY = sign * (walerCenter + walerDepth / 2);
                    double plateY = sign < 0 ? outerFaceY - Mm(10) : outerFaceY;
                    AddFreeForm(family, Box(-Mm(55), plateY, z - Mm(55), Mm(110), Mm(10), Mm(110)));

                    // Wing nut and two wings are part of the same editable clamp
                    // assembly, matching the demonstrated gông-ty family.
                    double nutStartY = sign < 0 ? outerFaceY - Mm(30) : outerFaceY + Mm(8);
                    AddFreeForm(family, CylinderAlongAxis(new XYZ(0, nutStartY, z), XYZ.BasisY,
                        Mm(22), rodRadius + Mm(12)));
                    AddFreeForm(family, Box(-Mm(48), nutStartY, z - Mm(7), Mm(96), Mm(8), Mm(14)));
                    AddFreeForm(family, Box(-Mm(7), nutStartY, z - Mm(48), Mm(14), Mm(8), Mm(96)));
                }
            }
        }

        private static void BuildYokeFamily(Document family, double columnWidthMm, double columnDepthMm,
            double barWidthMm, double barHeightMm, int pairCount)
        {
            // Horizontal walers stay in their own reusable family. Threaded rods and
            // wing nuts are separate line-based instances, matching the demonstrated
            // Revit workflow and allowing each rod to be selected/edited independently.
            double clearance = Mm(35);
            double width = Mm(columnWidthMm) + 2 * clearance;
            double depth = Mm(columnDepthMm) + 2 * clearance;
            double barWidth = Mm(Math.Max(20, barWidthMm));
            double barHeight = Mm(Math.Max(20, barHeightMm));
            int pairs = Math.Max(1, Math.Min(4, pairCount));
            double pairPitch = barHeight + Mm(15);
            for (int pair = 0; pair < pairs; pair++)
            {
                double z = (pair - (pairs - 1) / 2.0) * pairPitch - barHeight / 2.0;
                AddFreeForm(family, Box(-width / 2 - barWidth, -depth / 2 - barWidth, z,
                    width + 2 * barWidth, barWidth, barHeight));
                AddFreeForm(family, Box(-width / 2 - barWidth, depth / 2, z,
                    width + 2 * barWidth, barWidth, barHeight));
                AddFreeForm(family, Box(-width / 2 - barWidth, -depth / 2, z,
                    barWidth, depth, barHeight));
                AddFreeForm(family, Box(width / 2, -depth / 2, z,
                    barWidth, depth, barHeight));
            }
        }

        private static void BuildTieRodFamily(Document family, double diameterMm, double lengthMm)
        {
            double length = Mm(Math.Max(250, lengthMm));
            double rodRadius = Mm(Math.Max(6, diameterMm)) / 2.0;
            AddFreeForm(family, CylinderAlongAxis(XYZ.Zero, XYZ.BasisX, length, rodRadius));

            // Show the threaded zones without tessellating a full helix along every
            // rod. Repeated collars near both adjustable ends read clearly in 3D and
            // keep large-project previews responsive.
            double threadPitch = Mm(Math.Max(8, diameterMm * 0.75));
            double threadLength = Math.Min(Mm(120), length * 0.22);
            for (double offset = threadPitch / 2; offset < threadLength; offset += threadPitch)
            {
                AddFreeForm(family, CylinderAlongAxis(new XYZ(offset, 0, 0), XYZ.BasisX,
                    Math.Min(Mm(2.5), threadPitch * 0.25), rodRadius + Mm(1.5)));
                AddFreeForm(family, CylinderAlongAxis(new XYZ(length - offset - Mm(2.5), 0, 0), XYZ.BasisX,
                    Math.Min(Mm(2.5), threadPitch * 0.25), rodRadius + Mm(1.5)));
            }

            AddWingNutAssembly(family, 0, true, rodRadius);
            AddWingNutAssembly(family, length, false, rodRadius);
        }

        private static Solid CylinderAlongAxis(XYZ start, XYZ axis, double length, double radius)
        {
            XYZ direction = axis.Normalize();
            XYZ u = Math.Abs(direction.DotProduct(XYZ.BasisZ)) < 0.9
                ? direction.CrossProduct(XYZ.BasisZ).Normalize()
                : direction.CrossProduct(XYZ.BasisX).Normalize();
            XYZ v = direction.CrossProduct(u).Normalize();
            XYZ p0 = start + u * radius;
            XYZ p1 = start - u * radius;
            var loop = new CurveLoop();
            loop.Append(Arc.Create(p0, p1, start + v * radius));
            loop.Append(Arc.Create(p1, p0, start - v * radius));
            return GeometryCreationUtilities.CreateExtrusionGeometry(
                new List<CurveLoop> { loop }, direction, Math.Max(Mm(10), length));
        }

        private static Solid CylinderBetween(XYZ start, XYZ end, double radius)
        {
            XYZ vector = end - start;
            return CylinderAlongAxis(start, vector.Normalize(), vector.GetLength(), radius);
        }

        private static void AddWingNutAssembly(Document family, double x, bool negativeEnd, double rodRadius)
        {
            double washerRadius = Mm(35);
            double washerThickness = Mm(8);
            double nutLength = Mm(18);
            double direction = negativeEnd ? -1 : 1;
            double washerStart = negativeEnd ? x - washerThickness : x;
            AddFreeForm(family, CylinderAlongAxis(new XYZ(washerStart, 0, 0), XYZ.BasisX,
                washerThickness, washerRadius));
            double nutStart = negativeEnd ? washerStart - nutLength : x + washerThickness;
            AddFreeForm(family, CylinderAlongAxis(new XYZ(nutStart, 0, 0), XYZ.BasisX,
                nutLength, Math.Max(rodRadius + Mm(5), Mm(14))));

            double wingBaseX = negativeEnd ? nutStart - Mm(7) : nutStart + nutLength;
            AddFreeForm(family, WingPlateAlongX(wingBaseX, direction, 1));
            AddFreeForm(family, WingPlateAlongX(wingBaseX, direction, -1));
        }

        private static Solid WingPlateAlongX(double x, double direction, int side)
        {
            double thickness = Mm(7);
            double near = Mm(10) * side;
            double far = Mm(52) * side;
            double halfHeight = Mm(18);
            var loop = new CurveLoop();
            XYZ p0 = new XYZ(x, near, -halfHeight / 2);
            XYZ p1 = new XYZ(x, far, -halfHeight);
            XYZ p2 = new XYZ(x, far, halfHeight);
            XYZ p3 = new XYZ(x, near, halfHeight / 2);
            loop.Append(Line.CreateBound(p0, p1));
            loop.Append(Line.CreateBound(p1, p2));
            loop.Append(Line.CreateBound(p2, p3));
            loop.Append(Line.CreateBound(p3, p0));
            XYZ extrusion = direction > 0 ? XYZ.BasisX : -XYZ.BasisX;
            if (direction < 0)
            {
                Transform shift = Transform.CreateTranslation(-XYZ.BasisX * thickness);
                loop = CurveLoop.CreateViaTransform(loop, shift);
            }
            return GeometryCreationUtilities.CreateExtrusionGeometry(
                new List<CurveLoop> { loop }, extrusion, thickness);
        }

        private static void BuildPushPullFamily(Document family, double lengthMm)
        {
            double length = Mm(Math.Max(700, lengthMm));
            double plate = Mm(120), plateThickness = Mm(8);
            double outerRadius = Mm(30), innerRadius = Mm(24);
            double endClearance = Mm(95), overlap = Mm(220);
            double middle = length / 2.0;

            // The tutorial constructs the brace as a two-point line-based family.
            // Keep both bearing/pivot plates centred on the two family endpoints so
            // NewFamilyInstance(Line, ...) places and tilts it without RotateElement.
            AddFreeForm(family, Box(-plateThickness / 2, -plate / 2, -plate / 2,
                plateThickness, plate, plate));
            AddFreeForm(family, Box(length - plateThickness / 2, -plate / 2, -plate / 2,
                plateThickness, plate, plate));

            // Two telescoping tube stages overlap around the middle instead of one
            // solid rectangular bar. This preserves the adjustable construction
            // shown in the reference family while remaining lightweight in previews.
            double outerStart = endClearance;
            double outerEnd = Math.Min(length - endClearance, middle + overlap / 2);
            double innerStart = Math.Max(endClearance, middle - overlap / 2);
            double innerEnd = length - endClearance;
            AddFreeForm(family, CylinderAlongAxis(new XYZ(outerStart, 0, 0), XYZ.BasisX,
                Math.Max(Mm(150), outerEnd - outerStart), outerRadius));
            AddFreeForm(family, CylinderAlongAxis(new XYZ(innerStart, 0, 0), XYZ.BasisX,
                Math.Max(Mm(150), innerEnd - innerStart), innerRadius));

            // Central adjustment sleeve and collars make the adjustable zone
            // visible without generating costly thread helices on every brace.
            double sleeveLength = Math.Min(Mm(180), length * 0.18);
            AddFreeForm(family, CylinderAlongAxis(new XYZ(middle - sleeveLength / 2, 0, 0),
                XYZ.BasisX, sleeveLength, Mm(36)));
            double collarLength = Mm(18);
            foreach (double x in new[]
                     {
                         middle - sleeveLength / 2 - collarLength,
                         middle + sleeveLength / 2
                     })
                AddFreeForm(family, CylinderAlongAxis(new XYZ(x, 0, 0), XYZ.BasisX,
                    collarLength, Mm(39)));

            AddBraceEndHardware(family, 0, true, endClearance, plate);
            AddBraceEndHardware(family, length, false, endClearance, plate);
        }

        private static void BuildCableTurnbuckleFamily(Document family, double totalLengthMm)
        {
            double length = Mm(Math.Max(700, totalLengthMm));
            double bodyLength = Math.Min(Mm(500), length * 0.46);
            double bodyStart = (length - bodyLength) / 2;
            double bodyEnd = bodyStart + bodyLength;
            double cableRadius = Mm(6);
            double threadedRadius = Mm(10.5);
            double frameRadius = Mm(15);
            double frameOffset = Mm(36);

            // The tutorial uses a length-controlled center body, two threaded end
            // rods and eye fittings. Keep the same load-path hierarchy; the slim
            // outer segments represent the cable between the turnbuckle and anchors.
            AddFreeForm(family, CylinderAlongAxis(XYZ.Zero, XYZ.BasisX,
                Math.Max(Mm(80), bodyStart - Mm(70)), cableRadius));
            AddFreeForm(family, CylinderAlongAxis(new XYZ(bodyEnd + Mm(70), 0, 0), XYZ.BasisX,
                Math.Max(Mm(80), length - bodyEnd - Mm(70)), cableRadius));
            AddFreeForm(family, CylinderAlongAxis(new XYZ(bodyStart - Mm(90), 0, 0), XYZ.BasisX,
                Mm(150), threadedRadius));
            AddFreeForm(family, CylinderAlongAxis(new XYZ(bodyEnd - Mm(60), 0, 0), XYZ.BasisX,
                Mm(150), threadedRadius));

            AddFreeForm(family, CylinderAlongAxis(new XYZ(bodyStart, -frameOffset, 0), XYZ.BasisX,
                bodyLength, frameRadius));
            AddFreeForm(family, CylinderAlongAxis(new XYZ(bodyStart, frameOffset, 0), XYZ.BasisX,
                bodyLength, frameRadius));
            foreach (double x in new[] { bodyStart, bodyEnd - Mm(24) })
            {
                AddFreeForm(family, CylinderAlongAxis(new XYZ(x, -frameOffset, 0), XYZ.BasisY,
                    2 * frameOffset, Mm(18)));
                AddFreeForm(family, CylinderAlongAxis(new XYZ(x, 0, -frameOffset), XYZ.BasisZ,
                    2 * frameOffset, Mm(18)));
            }

            // Short collars indicate the adjustable threaded zones without an
            // expensive helix, keeping hundreds of preview instances responsive.
            double pitch = Mm(15);
            for (int index = 0; index < 5; index++)
            {
                double leftX = bodyStart - Mm(75) + index * pitch;
                double rightX = bodyEnd + Mm(10) + index * pitch;
                AddFreeForm(family, CylinderAlongAxis(new XYZ(leftX, 0, 0), XYZ.BasisX,
                    Mm(3), threadedRadius + Mm(1.5)));
                AddFreeForm(family, CylinderAlongAxis(new XYZ(rightX, 0, 0), XYZ.BasisX,
                    Mm(3), threadedRadius + Mm(1.5)));
            }

            AddEyeRing(family, Mm(42), Mm(33), Mm(7));
            AddEyeRing(family, length - Mm(42), Mm(33), Mm(7));
        }

        private static void AddEyeRing(Document family, double centerX, double radius, double tubeRadius)
        {
            const int segments = 12;
            for (int index = 0; index < segments; index++)
            {
                double a0 = 2 * Math.PI * index / segments;
                double a1 = 2 * Math.PI * (index + 1) / segments;
                XYZ start = new XYZ(centerX + radius * Math.Cos(a0), radius * Math.Sin(a0), 0);
                XYZ end = new XYZ(centerX + radius * Math.Cos(a1), radius * Math.Sin(a1), 0);
                AddFreeForm(family, CylinderBetween(start, end, tubeRadius));
            }
        }

        private static void AddBraceEndHardware(Document family, double endpointX, bool startEnd,
            double endClearance, double plate)
        {
            double direction = startEnd ? 1 : -1;
            double couplerLength = Mm(65), couplerRadius = Mm(34);
            double couplerStart = endpointX + direction * (endClearance - couplerLength);
            if (!startEnd) couplerStart -= couplerLength;
            AddFreeForm(family, CylinderAlongAxis(new XYZ(couplerStart, 0, 0), XYZ.BasisX,
                couplerLength, couplerRadius));

            // Fork plates and a transverse pivot pin at each end reproduce the
            // editable connection hardware visible in the tutorial family.
            double lugLength = Mm(70), lugThickness = Mm(10), lugHeight = Mm(58);
            double lugX = startEnd ? endpointX : endpointX - lugLength;
            double lugOffset = Mm(30);
            AddFreeForm(family, Box(lugX, -lugOffset - lugThickness, -lugHeight / 2,
                lugLength, lugThickness, lugHeight));
            AddFreeForm(family, Box(lugX, lugOffset, -lugHeight / 2,
                lugLength, lugThickness, lugHeight));
            double pinLength = Math.Min(plate * 0.72, Mm(90));
            AddFreeForm(family, CylinderAlongAxis(new XYZ(endpointX, -pinLength / 2, 0),
                XYZ.BasisY, pinLength, Mm(9)));
        }

        private static void AddFreeForm(Document family, Solid solid) => FreeFormElement.Create(family, solid);

        private static Solid Box(double x, double y, double z, double dx, double dy, double dz)
        {
            var loop = new CurveLoop();
            XYZ p0 = new XYZ(x, y, z), p1 = new XYZ(x + dx, y, z);
            XYZ p2 = new XYZ(x + dx, y + dy, z), p3 = new XYZ(x, y + dy, z);
            loop.Append(Line.CreateBound(p0, p1)); loop.Append(Line.CreateBound(p1, p2));
            loop.Append(Line.CreateBound(p2, p3)); loop.Append(Line.CreateBound(p3, p0));
            return GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { loop }, XYZ.BasisZ, dz);
        }

        private static IEnumerable<PlanarFace> GetPlanarFaces(Element element)
        {
            GeometryElement geometry = element.get_Geometry(new Options
            {
                DetailLevel = ViewDetailLevel.Fine,
                IncludeNonVisibleObjects = false,
                ComputeReferences = false
            });
            if (geometry == null) return Enumerable.Empty<PlanarFace>();
            return Solids(geometry, Transform.Identity).SelectMany(solid => solid.Faces.Cast<Face>())
                .OfType<PlanarFace>().ToList();
        }

        private static IEnumerable<Solid> Solids(GeometryElement geometry, Transform transform)
        {
            foreach (GeometryObject item in geometry)
            {
                if (item is Solid solid && solid.Faces.Size > 0 && solid.Volume > 1e-9)
                    yield return transform.IsIdentity ? solid : SolidUtils.CreateTransformed(solid, transform);
                else if (item is GeometryInstance instance)
                    foreach (Solid child in Solids(instance.GetSymbolGeometry(), transform.Multiply(instance.Transform)))
                        yield return child;
            }
        }

        private static bool IsPointOnFace(PlanarFace face, XYZ point)
        {
            IntersectionResult projection = face.Project(point);
            return projection != null && projection.Distance < Mm(50) && face.IsInside(projection.UVPoint);
        }

        private static IEnumerable<double> Grid(double min, double max, double spacing)
        {
            double length = max - min;
            int count = Math.Max(1, (int)Math.Ceiling(length / spacing));
            for (int index = 0; index <= count; index++)
                yield return min + length * index / count;
        }

        private FamilyInstance NewInstance(FamilySymbol symbol, XYZ origin) =>
            _document.Create.NewFamilyInstance(origin, symbol, StructuralType.NonStructural);

        private bool TryNewUniqueProp(FamilySymbol symbol, XYZ origin, out FamilyInstance instance)
        {
            long Q(double internalValue) => (long)Math.Round(UnitUtils.ConvertFromInternalUnits(
                internalValue, UnitTypeId.Millimeters));
            var key = (symbol.Id.Value, Q(origin.X), Q(origin.Y), Q(origin.Z));
            if (!_placedPropLocations.Add(key))
            {
                instance = null;
                return false;
            }
            instance = NewInstance(symbol, origin);
            return true;
        }

        private static void Activate(FamilySymbol symbol)
        {
            if (!symbol.IsActive) symbol.Activate();
        }

        private static void Mark(FamilyInstance instance, View3D view, OverrideGraphicSettings graphics)
        {
            Parameter comments = instance.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            if (comments != null && !comments.IsReadOnly) comments.Set(InstanceMarker);
            view.SetElementOverrides(instance.Id, graphics);
        }

        private void DeleteOwnedInstances()
        {
            List<ElementId> ids = new FilteredElementCollector(_document).OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(IsPreviewFamilyInstance)
                .Select(instance => instance.Id).ToList();
            if (ids.Count > 0) _document.Delete(ids);
        }

        private static double BoxSizeMm(BoundingBoxXYZ box, bool x) => box == null
            ? 300
            : UnitUtils.ConvertFromInternalUnits(x ? box.Max.X - box.Min.X : box.Max.Y - box.Min.Y,
                UnitTypeId.Millimeters);
        private static double BoxHeightMm(BoundingBoxXYZ box) => box == null
            ? 500
            : UnitUtils.ConvertFromInternalUnits(box.Max.Z - box.Min.Z, UnitTypeId.Millimeters);
        private static double Mm(double value) => UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Millimeters);
        private static double Round(double value, double step) => Math.Max(step, Math.Round(value / step) * step);
        private static double ScaffoldBraceModuleHeight(double totalHeightMm)
        {
            int tiers = Math.Max(1, (int)Math.Ceiling(Math.Max(600, totalHeightMm) / 1500.0));
            return Round(Math.Max(600, totalHeightMm) / tiers, 50);
        }
        private static double ScaffoldBraceDiameter(double propDiameterMm) =>
            Round(Math.Max(20, Math.Min(27, Math.Max(20, propDiameterMm) * 0.45)), 0.1);
        private static string N(double value) => value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', '_');
        private static string PropKey(double diameter, double height) =>
            $"{FamilyPrefix}CAY_CHONG_HOAN_CHINH_V3_D{N(diameter)}_H{N(height)}";
        private static string ScaffoldBraceKey(double width, double height, double diameter) =>
            $"{FamilyPrefix}HE_GIAN_GIAO_H_GIANG_X_V4_W{N(width)}_H{N(height)}_PD{N(diameter)}_BD{N(ScaffoldBraceDiameter(diameter))}";
        private static string BeamClampKey(double beamWidth, double beamDepth, double walerWidth,
            double walerDepth, double tieDiameter) =>
            $"{FamilyPrefix}GONG_TY_DAM_V1_B{N(beamWidth)}_H{N(beamDepth)}_W{N(walerWidth)}x{N(walerDepth)}_D{N(tieDiameter)}";
        private static string YokeKey(double width, double depth, double barWidth, double barHeight,
            int pairCount) =>
            $"{FamilyPrefix}GONG_COT_V3_{N(width)}x{N(depth)}_{N(barWidth)}x{N(barHeight)}_P{pairCount}";
        private static string TieRodKey(double diameter, double length) =>
            $"{FamilyPrefix}TY_BAT_CHUON_LINE_V1_D{N(diameter)}_L{N(length)}";
        // V3 uses a curve-based family. Earlier point-based families could not be
        // tilted out of their host level and generated fatal rotation failures.
        private static string PushPullKey(double length) => $"{FamilyPrefix}CHONG_XIEN_LINE_V4_L{N(length)}";
        private static string TurnbuckleKey(double length) =>
            $"{FamilyPrefix}CAP_TANG_DO_LINE_V1_L{N(length)}";

        internal static bool IsOwnedPreviewInstance(FamilyInstance instance) =>
            IsPreviewFamilyInstance(instance) &&
            instance.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString() == InstanceMarker;

        internal static bool IsPreviewFamilyInstance(FamilyInstance instance) =>
            instance?.Symbol?.FamilyName?.StartsWith(FamilyPrefix, StringComparison.OrdinalIgnoreCase) == true;

        private XYZ ProjectPoint(System.Windows.Media.Media3D.Point3D localPointMm) => new XYZ(
            Mm(_braceCoordinateOriginMm.X + localPointMm.X),
            Mm(_braceCoordinateOriginMm.Y + localPointMm.Y),
            Mm(_braceCoordinateOriginMm.Z + localPointMm.Z));

        private sealed class ReloadFamilyOptions : IFamilyLoadOptions
        {
            public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
            { overwriteParameterValues = false; return true; }
            public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source,
                out bool overwriteParameterValues)
            { source = FamilySource.Family; overwriteParameterValues = false; return true; }
        }
    }
}
