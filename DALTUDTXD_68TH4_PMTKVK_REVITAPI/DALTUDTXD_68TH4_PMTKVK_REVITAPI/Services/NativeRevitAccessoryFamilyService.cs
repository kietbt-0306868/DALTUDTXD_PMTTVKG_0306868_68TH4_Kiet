using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels;
using Point3D = System.Windows.Media.Media3D.Point3D;
using Vector3D = System.Windows.Media.Media3D.Vector3D;

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
        private static readonly object TemplatePathCacheLock = new object();
        private static readonly Dictionary<string, string> TemplatePathCache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Document _document;
        private readonly SharedDataViewModel _data;
        private readonly IReadOnlyDictionary<string, FloorElementModel> _models;
        private readonly System.Windows.Media.Media3D.Point3D _braceCoordinateOriginMm;
        private readonly Dictionary<string, FamilySymbol> _symbols =
            new Dictionary<string, FamilySymbol>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _requestedProjectSymbols =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _preexistingProjectSymbols =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _failedSymbols =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _requestedFamilyDefinitions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _warnings = new List<string>();
        // A location may be reached from both the slab and beam layout passes.  The
        // symbol must not participate in the key; otherwise two different height
        // types are placed concentrically and Revit reports duplicate instances.
        private readonly HashSet<(long Xmm, long Ymm, long Zmm)> _placedPropLocations =
            new HashSet<(long Xmm, long Ymm, long Zmm)>();
        private readonly HashSet<string> _placedScaffoldBays = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<long, List<SupportSpanPlan>> _supportSpansByElement =
            new Dictionary<long, List<SupportSpanPlan>>();
        private readonly Dictionary<long, List<ScaffoldBayPlan>> _scaffoldBaysByElement =
            new Dictionary<long, List<ScaffoldBayPlan>>();
        private readonly Dictionary<long, List<BeamSideBraceAxisPlan>> _beamSideBraceAxesByElement =
            new Dictionary<long, List<BeamSideBraceAxisPlan>>();
        private ScaffoldElevationZoning _scaffoldZoning;
        private int _unsupportedShoringPointCount;
        private readonly Dictionary<string, CableTurnbucklePlacement> _cableTurnbucklePlacements =
            new Dictionary<string, CableTurnbucklePlacement>(StringComparer.Ordinal);
        private Dictionary<long, List<ColumnBraceAssessment>> _assessmentsByColumn =
            new Dictionary<long, List<ColumnBraceAssessment>>();
        private List<Level> _levels;
        private List<PlanarFace> _slabTopFaces = new List<PlanarFace>();
        private ConstructionElevationResolver _braceBearingResolver;
        private bool _projectSymbolsIndexed;
        public IReadOnlyList<string> Warnings => _warnings;
        public int ReusedProjectFamilyCount { get; private set; }
        public int LoadedCachedFamilyCount { get; private set; }
        public int GeneratedFamilyCount { get; private set; }

        internal sealed class PlacementResult
        {
            public int PropCount { get; set; }
            public int ScaffoldBraceCount { get; set; }
            public int BeamClampCount { get; set; }
            public int BeamSurfaceTieRodCount { get; set; }
            public int BeamSideBraceCount { get; set; }
            public int ColumnYokeCount { get; set; }
            public int ColumnTieRodCount { get; set; }
            public int PushPullPropCount { get; set; }
            public int TurnbuckleCount { get; set; }
            public int ShoringSuppressedComponentCount { get; set; }
            public int UnsupportedShoringPointCount { get; set; }
            public int ColumnBraceSegmentCount { get; set; }
            public int ColumnBraceCoveredSegmentCount { get; set; }
            public int ColumnBraceUnrestrainedSegmentCount { get; set; }
            public int ColumnBraceUnderRestrainedSegmentCount { get; set; }
            public List<long> UnderRestrainedColumnIds { get; } = new List<long>();
            public int Total => PropCount + ScaffoldBraceCount + BeamClampCount + BeamSurfaceTieRodCount + BeamSideBraceCount + ColumnYokeCount + ColumnTieRodCount +
                                PushPullPropCount + TurnbuckleCount;
        }

        private sealed class CableTurnbucklePlacement
        {
            public XYZ Anchor { get; set; }
            public XYZ Attach { get; set; }
            public double LengthMm { get; set; }
        }

        private sealed class SupportSpanPlan
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double BaseZ { get; set; }
            public double ContactZ { get; set; }
            public double ConstructionZoneElevationMm { get; set; }
            public double HeightMm { get; set; }
            public double DiameterMm { get; set; }
            public ScaffoldSystemType SystemType { get; set; }
            public bool IsCoveredByBay { get; set; }
        }

        private sealed class ScaffoldBayPlan
        {
            public double X0 { get; set; }
            public double Y0 { get; set; }
            public double X1 { get; set; }
            public double Y1 { get; set; }
            public double BaseZ { get; set; }
            public double ClearHeightMm { get; set; }
            public double DiameterMm { get; set; }
            public double BayWidthMm { get; set; }
            public double ModuleHeightMm { get; set; }
            public int ModuleCount { get; set; }
            public ScaffoldSystemType SystemType { get; set; }
        }

        private sealed class BeamSideBraceAxisPlan
        {
            public XYZ Foot { get; set; }
            public XYZ Attach { get; set; }
            public double LengthMm { get; set; }
        }

        private readonly struct ScaffoldTierLayout
        {
            public ScaffoldTierLayout(int count, double moduleHeightMm)
            {
                Count = count;
                ModuleHeightMm = moduleHeightMm;
            }

            public int Count { get; }
            public double ModuleHeightMm { get; }
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
            Dictionary<long, Element> componentById = componentList
                .GroupBy(item => item.Element.Id.Value)
                .ToDictionary(group => group.Key, group => group.First().Element);
            Dictionary<long, CalculationParameters> parametersByElement = componentList
                .GroupBy(item => item.Element.Id.Value)
                .ToDictionary(group => group.Key,
                    group => EffectiveParameters(group.First().Element, group.First().Type));
            reportProgress?.Invoke(1, "Đang lập chỉ mục family phụ kiện đã có...");
            IndexExistingSymbols();
            reportProgress?.Invoke(3, "Đang xác định cao độ thi công và mặt tựa thực tế...");
            PrepareVerticalSupportPlans(componentList, parametersByElement, isCancellationRequested);
            _beamSideBraceAxesByElement.Clear();
            _braceBearingResolver = new ConstructionElevationResolver(_document);
            _cableTurnbucklePlacements.Clear();
            bool needsSlabTopFaces = componentList.Any(item =>
                item.Type == StructuralComponentType.Beam &&
                parametersByElement[item.Element.Id.Value].BeamPlacementModeIndex == 2);
            _slabTopFaces = needsSlabTopFaces ? CollectSlabTopFaces() : new List<PlanarFace>();
            for (int componentIndex = 0; componentIndex < componentList.Count; componentIndex++)
            {
                ThrowIfCancelled(isCancellationRequested);
                (Element element, StructuralComponentType type) = componentList[componentIndex];
                CalculationParameters parameters = parametersByElement[element.Id.Value];
                if (type == StructuralComponentType.Slab || type == StructuralComponentType.Beam)
                {
                    if (!parameters.IsGroundLevel)
                    {
                        if (_supportSpansByElement.TryGetValue(element.Id.Value,
                                out List<SupportSpanPlan> supportSpans))
                        {
                            foreach (SupportSpanPlan span in supportSpans
                                         .Where(item => !item.IsCoveredByBay && item.HeightMm > 0)
                                         .GroupBy(item => new
                                         {
                                             item.SystemType,
                                             item.DiameterMm,
                                             item.HeightMm
                                         })
                                         .Select(group => group.First()))
                            {
                                string key = ScaffoldStandardKey(span.SystemType,
                                    span.DiameterMm, span.HeightMm);
                                TryEnsureSymbol(key,
                                    family => BuildScaffoldStandardFamily(family,
                                        span.SystemType, span.DiameterMm, span.HeightMm));
                            }
                        }
                        if (_scaffoldBaysByElement.TryGetValue(element.Id.Value,
                                out List<ScaffoldBayPlan> scaffoldBays))
                        {
                            foreach (ScaffoldBayPlan bay in scaffoldBays
                                         .GroupBy(item => new
                                         {
                                             item.BayWidthMm,
                                             item.ClearHeightMm,
                                             item.ModuleHeightMm,
                                             item.ModuleCount,
                                             item.SystemType,
                                             item.DiameterMm
                                         })
                                         .Select(group => group.First()))
                            {
                                string braceKey = ScaffoldBraceKey(bay.SystemType,
                                    bay.BayWidthMm, bay.ClearHeightMm,
                                    bay.ModuleHeightMm, bay.ModuleCount, bay.DiameterMm);
                                TryEnsureSymbol(braceKey,
                                    family => BuildScaffoldTowerBayFamily(family,
                                        bay.SystemType, bay.BayWidthMm, bay.ClearHeightMm,
                                        bay.ModuleHeightMm, bay.ModuleCount, bay.DiameterMm));
                            }
                        }
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
                        if (parameters.BeamPlacementModeIndex == 2)
                        {
                            double surfaceDiameter = Round(parameters.BeamEdgeSurfaceTieRodDiameter > 0
                                ? parameters.BeamEdgeSurfaceTieRodDiameter : 12, 1);
                            double surfaceLength = Round(beamWidth +
                                Math.Max(6, parameters.BeamSidePlywoodThickness) + 40 +
                                Math.Max(500, parameters.BeamEdgeSurfaceTieRodAnchorLength), 10);
                            TryEnsureSymbol(TieRodKey(surfaceDiameter, surfaceLength),
                                family => BuildTieRodFamily(family, surfaceDiameter, surfaceLength), true);

                            NativeAccessoryFamilyDimensionPolicy.PushPullProfile braceProfile =
                                BeamSideBraceProfileFor(parameters);
                            List<BeamSideBraceAxisPlan> braceAxes = BuildBeamSideBraceAxisPlans(
                                element, parameters, _braceBearingResolver);
                            _beamSideBraceAxesByElement[element.Id.Value] = braceAxes;
                            foreach (BeamSideBraceAxisPlan plan in braceAxes
                                         .GroupBy(item => item.LengthMm)
                                         .Select(group => group.First()))
                            {
                                string braceKey = BeamSideBraceKey(plan.LengthMm, braceProfile);
                                TryEnsureSymbol(braceKey,
                                    family => BuildPushPullFamily(family, plan.LengthMm, braceProfile),
                                    false, true);
                            }
                        }
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
            reportProgress?.Invoke(78, "Đang kiểm tra mặt tựa và chuẩn bị family chống xiên...");
            RevalidateColumnBraceBearings(componentById);
            foreach ((Element element, StructuralComponentType type) in componentList
                         .Where(item => item.Type == StructuralComponentType.Column))
                parametersByElement[element.Id.Value] = EffectiveParameters(element, type);
            foreach (ColumnBraceAssessment assessment in _data.ColumnBraceAssessmentCache.Values)
            {
                componentById.TryGetValue(assessment.ColumnId, out Element column);
                BoundingBoxXYZ box = column?.get_BoundingBox(null);
                if (box == null) continue;
                parametersByElement.TryGetValue(assessment.ColumnId,
                    out CalculationParameters columnParameters);
                NativeAccessoryFamilyDimensionPolicy.PushPullProfile profile =
                    PushPullProfileFor(columnParameters);
                foreach ((BraceDirectionAssessment direction, double offsetMm, _) in
                         BraceAssemblyOffsets(assessment, columnParameters))
                {
                    if (!TryResolveOffsetBraceAxis(_braceBearingResolver, column, box, assessment,
                            direction, offsetMm, out XYZ foot, out XYZ attach)) continue;
                    double length = Round(UnitUtils.ConvertFromInternalUnits((attach - foot).GetLength(),
                        UnitTypeId.Millimeters), 50);
                    string key = PushPullKey(length, profile);
                    TryEnsureSymbol(key, family => BuildPushPullFamily(family, length, profile), false, true);
                }
            }

            List<ColumnBraceAssessment> cableAssessments = _data.ColumnBraceAssessmentCache.Values
                .Where(CanResolveOpenDirectionsAutomatically)
                .ToList();
            List<PlanarFace> bearingFaces = cableAssessments.Count > 0
                ? CollectTopBearingFaces()
                : new List<PlanarFace>();
            ThrowIfCancelled(isCancellationRequested);
            reportProgress?.Invoke(90, "Đang chuẩn bị family cáp và tăng đơ...");
            foreach (ColumnBraceAssessment assessment in cableAssessments)
            {
                componentById.TryGetValue(assessment.ColumnId, out Element column);
                if (column == null) continue;
                foreach (BraceDirectionAssessment direction in assessment.Directions.Where(item => !item.CanPlaceProp))
                {
                    if (!TryCreateInwardCablePlacement(column, assessment, direction, bearingFaces,
                            out CableTurnbucklePlacement placement)) continue;
                    string placementKey = CablePlacementKey(assessment, direction);
                    _cableTurnbucklePlacements[placementKey] = placement;
                    double roundedLength = Round(placement.LengthMm, 50);
                    TryEnsureSymbol(TurnbuckleKey(roundedLength),
                        family => BuildCableTurnbuckleFamily(family, roundedLength), false, true);
                }
            }
            reportProgress?.Invoke(100,
                $"Đã chuẩn bị {_requestedFamilyDefinitions.Count} loại family: dùng lại " +
                $"{ReusedProjectFamilyCount}, nạp cache {LoadedCachedFamilyCount}, tạo mới {GeneratedFamilyCount}.");
        }

        private void PrepareVerticalSupportPlans(
            IReadOnlyCollection<(Element Element, StructuralComponentType Type)> components,
            IReadOnlyDictionary<long, CalculationParameters> parametersByElement,
            Func<bool> isCancellationRequested)
        {
            _supportSpansByElement.Clear();
            _scaffoldBaysByElement.Clear();
            _unsupportedShoringPointCount = 0;
            var resolver = new ConstructionElevationResolver(_document);

            List<(Element Element, StructuralComponentType Type)> shoredComponents = components
                .Where(item => item.Type == StructuralComponentType.Slab ||
                               item.Type == StructuralComponentType.Beam)
                .Where(item => parametersByElement.TryGetValue(item.Element.Id.Value,
                    out CalculationParameters parameters) && !parameters.IsGroundLevel)
                .OrderBy(item => item.Element.Id.Value)
                .ToList();

            // Pass 1 resolves every real bearing elevation. System assignment is
            // deliberately postponed until the complete construction-storey set is
            // known, so selection/input order cannot alternate systems between
            // neighbouring slab and beam bays.
            foreach ((Element element, StructuralComponentType type) in shoredComponents)
            {
                ThrowIfCancelled(isCancellationRequested);
                CalculationParameters parameters = parametersByElement[element.Id.Value];

                List<SupportSpanPlan> spans = type == StructuralComponentType.Slab
                    ? BuildSlabSupportSpans(element, parameters, resolver)
                    : BuildBeamSupportSpans(element, parameters, resolver);
                _supportSpansByElement[element.Id.Value] = spans;
            }

            ScaffoldSystemType selectedSystems = ScaffoldSystemLayoutPolicy.Normalize(
                _data.SelectedScaffoldSystems);
            _scaffoldZoning = ScaffoldSystemLayoutPolicy.CreateElevationZoning(
                _supportSpansByElement.Values.SelectMany(items => items)
                    .Select(span => span.ConstructionZoneElevationMm), selectedSystems);
            foreach (SupportSpanPlan span in _supportSpansByElement.Values.SelectMany(items => items))
            {
                span.SystemType = _scaffoldZoning.SystemFor(
                    span.ConstructionZoneElevationMm);
                span.IsCoveredByBay = false;
            }

            // Pass 2 connects only nodes already assigned to the same storey zone.
            // No system decision is made inside the bay loops.
            foreach ((Element element, StructuralComponentType type) in shoredComponents)
            {
                ThrowIfCancelled(isCancellationRequested);
                CalculationParameters parameters = parametersByElement[element.Id.Value];
                List<SupportSpanPlan> spans = _supportSpansByElement[element.Id.Value];
                _scaffoldBaysByElement[element.Id.Value] = type == StructuralComponentType.Slab
                    ? BuildSlabScaffoldPlans(element, parameters, spans)
                    : BuildBeamScaffoldPlans(element, parameters, spans);
            }

            // A slab pass and a beam pass can propose the same physical scaffold node.
            // Mark endpoints globally only after every bay is known; otherwise a span
            // owned by another host would still create a concentric standalone prop.
            var globalBayEndpoints = new HashSet<(long X, long Y, long Z)>();
            foreach (ScaffoldBayPlan bay in _scaffoldBaysByElement.Values.SelectMany(items => items))
            {
                globalBayEndpoints.Add(SupportNodeKey(bay.X0, bay.Y0, bay.BaseZ));
                globalBayEndpoints.Add(SupportNodeKey(bay.X1, bay.Y1, bay.BaseZ));
            }
            foreach (SupportSpanPlan span in _supportSpansByElement.Values.SelectMany(items => items))
            {
                if (globalBayEndpoints.Contains(SupportNodeKey(span.X, span.Y, span.BaseZ)))
                    span.IsCoveredByBay = true;
                else if (span.HeightMm <= 0)
                    _unsupportedShoringPointCount++;
            }

            if (_unsupportedShoringPointCount > 0)
                _warnings.Add($"Đã bỏ qua {_unsupportedShoringPointCount} vị trí chống không có mặt tựa thực, " +
                              "không ghép được thành khoang mô-đun hoặc nằm ngoài phạm vi cây chống đơn 0,9–6,0 m.");
        }

        private List<SupportSpanPlan> BuildSlabSupportSpans(Element element,
            CalculationParameters parameters, ConstructionElevationResolver resolver)
        {
            var result = new List<SupportSpanPlan>();
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null) return result;
            List<PlanarFace> bottomFaces = GetPlanarFaces(element)
                .Where(face => face.FaceNormal.Z < -0.70).ToList();
            if (bottomFaces.Count == 0) return result;

            double spacing = Mm(Math.Max(300,
                parameters.SupportSpacing > 0 ? parameters.SupportSpacing : 1000));
            double stackMm = Math.Max(6, parameters.VanMatThickness) +
                             Math.Max(20, parameters.XaGoPhuHeight) +
                             Math.Max(20, parameters.XaGoChinhHeight);
            double diameterMm = Round(parameters.CayChongDiameter > 0
                ? parameters.CayChongDiameter : 48, 1);
            double constructionZoneElevationMm = ConstructionZoneElevationMm(element, box);
            foreach (double x in PairedGrid(box.Min.X, box.Max.X, spacing))
            foreach (double y in Grid(box.Min.Y, box.Max.Y, spacing))
            {
                if (!TryFaceElevationAt(bottomFaces, x, y, false, out double undersideZ)) continue;
                double contactZ = undersideZ - Mm(stackMm);
                if (!resolver.TryFindHighestBearingBelow(x, y, contactZ, element.Id,
                        out double baseZ))
                {
                    _unsupportedShoringPointCount++;
                    continue;
                }
                AddResolvedSupportSpan(result, x, y, baseZ, contactZ, diameterMm,
                    constructionZoneElevationMm);
            }
            return result;
        }

        private List<SupportSpanPlan> BuildBeamSupportSpans(Element element,
            CalculationParameters parameters, ConstructionElevationResolver resolver)
        {
            var result = new List<SupportSpanPlan>();
            if (!(element.Location is LocationCurve location) || location.Curve == null) return result;
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null) return result;
            Curve curve = location.Curve;
            List<PlanarFace> bottomFaces = GetPlanarFaces(element)
                .Where(face => face.FaceNormal.Z < -0.70).ToList();
            double spacing = Mm(Math.Max(300,
                parameters.BeamPropSpacing > 0 ? parameters.BeamPropSpacing : 1000));
            int stations = Math.Max(2, (int)Math.Ceiling(curve.Length / spacing) + 1);
            const int rows = 2;
            // BeamScaffoldBayWidth is the checked primary-waler span between the
            // two standards. BeamPropSpacing remains the longitudinal station pitch.
            double rowOffset = Mm(Math.Max(300, parameters.BeamScaffoldBayWidth > 0
                ? parameters.BeamScaffoldBayWidth : 600)) / 2.0;
            double stackMm = Math.Max(6, parameters.BeamBottomPlywoodThickness) +
                             Math.Max(20, parameters.BeamSecondaryJoistDepth) +
                             Math.Max(20, parameters.BeamPrimaryWalerDepth);
            double diameterMm = Round(parameters.CayChongDiameter > 0
                ? parameters.CayChongDiameter : 48, 1);
            double constructionZoneElevationMm = ConstructionZoneElevationMm(element, box);

            for (int index = 0; index < stations; index++)
            {
                double t = (double)index / (stations - 1);
                XYZ point = curve.Evaluate(t, true);
                XYZ tangent = HorizontalDirection(curve.ComputeDerivatives(t, true).BasisX);
                XYZ cross = new XYZ(-tangent.Y, tangent.X, 0).Normalize();
                for (int row = 0; row < rows; row++)
                {
                    double offset = rows == 1 ? 0 : (row == 0 ? -rowOffset : rowOffset);
                    double x = point.X + cross.X * offset;
                    double y = point.Y + cross.Y * offset;
                    double beamBottomZ;
                    if (!TryFaceElevationAt(bottomFaces, x, y, false, out beamBottomZ))
                        beamBottomZ = BeamBottomAtStation(curve, box, point);
                    double contactZ = beamBottomZ - Mm(stackMm);
                    if (!resolver.TryFindHighestBearingBelow(x, y, contactZ, element.Id,
                            out double baseZ))
                    {
                        _unsupportedShoringPointCount++;
                        continue;
                    }
                    AddResolvedSupportSpan(result, x, y, baseZ, contactZ, diameterMm,
                        constructionZoneElevationMm);
                }
            }
            return result;
        }

        private void AddResolvedSupportSpan(ICollection<SupportSpanPlan> result,
            double x, double y, double baseZ, double contactZ, double diameterMm,
            double constructionZoneElevationMm)
        {
            double actualHeightMm = UnitUtils.ConvertFromInternalUnits(
                contactZ - baseZ, UnitTypeId.Millimeters);
            // Modular bays remain valid outside the 0.9–6.0 m range of a standalone
            // telescopic prop. Keep a bay candidate from 0.6 m upward, and only store
            // a standalone-family height when that separate catalogue rule is met.
            if (!double.IsFinite(actualHeightMm) || actualHeightMm < 600.0)
            {
                _unsupportedShoringPointCount++;
                return;
            }
            NativeAccessoryFamilyDimensionPolicy.TryGetPropFamilyHeight(
                actualHeightMm, out double familyHeightMm);
            result.Add(new SupportSpanPlan
            {
                X = x,
                Y = y,
                BaseZ = baseZ,
                ContactZ = contactZ,
                ConstructionZoneElevationMm = constructionZoneElevationMm,
                HeightMm = familyHeightMm,
                DiameterMm = diameterMm
            });
        }

        private double ConstructionZoneElevationMm(Element element, BoundingBoxXYZ box)
        {
            try
            {
                ElementId levelId = element?.LevelId;
                if (levelId != null && levelId != ElementId.InvalidElementId &&
                    _document.GetElement(levelId) is Level level)
                {
                    return UnitUtils.ConvertFromInternalUnits(level.Elevation,
                        UnitTypeId.Millimeters);
                }
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                // Geometry elevation below remains a deterministic fallback for
                // unhosted/imported framing without a usable Revit Level.
            }

            double fallback = box == null ? 0.0 : box.Max.Z;
            return UnitUtils.ConvertFromInternalUnits(fallback, UnitTypeId.Millimeters);
        }

        private List<ScaffoldBayPlan> BuildSlabScaffoldPlans(Element element,
            CalculationParameters parameters, IReadOnlyCollection<SupportSpanPlan> spans)
        {
            var result = new List<ScaffoldBayPlan>();
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null || spans.Count == 0) return result;
            double spacing = Mm(Math.Max(300,
                parameters.SupportSpacing > 0 ? parameters.SupportSpacing : 1000));
            List<double> xs = PairedGrid(box.Min.X, box.Max.X, spacing).ToList();
            List<double> ys = Grid(box.Min.Y, box.Max.Y, spacing).ToList();
            Dictionary<(long X, long Y), SupportSpanPlan> lookup = spans
                .GroupBy(span => SupportPointKey(span.X, span.Y))
                .ToDictionary(group => group.Key, group => group.First());

            // Pair every row in one consistent direction.  The paired X grid always
            // has an even number of nodes, so adjacent towers never need coincident
            // outer standards merely to cover a leftover node.
            foreach (double y in ys)
            for (int index = 0; index + 1 < xs.Count; index += 2)
                TryAddScaffoldBay(result, lookup, xs[index], y, xs[index + 1], y,
                    parameters.SupportJackExtensionLimit);
            return result;
        }

        private List<ScaffoldBayPlan> BuildBeamScaffoldPlans(Element element,
            CalculationParameters parameters, IReadOnlyCollection<SupportSpanPlan> spans)
        {
            var result = new List<ScaffoldBayPlan>();
            if (!(element.Location is LocationCurve location) || location.Curve == null || spans.Count == 0)
                return result;
            Curve curve = location.Curve;
            double spacing = Mm(Math.Max(300,
                parameters.BeamPropSpacing > 0 ? parameters.BeamPropSpacing : 1000));
            int stations = Math.Max(2, (int)Math.Ceiling(curve.Length / spacing) + 1);
            double rowOffset = Mm(Math.Max(300, parameters.BeamScaffoldBayWidth > 0
                ? parameters.BeamScaffoldBayWidth : 600)) / 2.0;
            Dictionary<(long X, long Y), SupportSpanPlan> lookup = spans
                .GroupBy(span => SupportPointKey(span.X, span.Y))
                .ToDictionary(group => group.Key, group => group.First());
            // One transverse scaffold bay per longitudinal support station.  The
            // former implementation connected consecutive stations along the beam
            // and silently used BeamPropSpacing as the scaffold bay width.
            for (int index = 0; index < stations; index++)
            {
                double t = stations == 1 ? 0.5 : (double)index / (stations - 1);
                XYZ point = curve.Evaluate(t, true);
                XYZ tangent = HorizontalDirection(curve.ComputeDerivatives(t, true).BasisX);
                XYZ cross = new XYZ(-tangent.Y, tangent.X, 0).Normalize();
                TryAddScaffoldBay(result, lookup,
                    point.X - cross.X * rowOffset, point.Y - cross.Y * rowOffset,
                    point.X + cross.X * rowOffset, point.Y + cross.Y * rowOffset,
                    parameters.SupportJackExtensionLimit);
            }
            return result;
        }

        private static void TryAddScaffoldBay(ICollection<ScaffoldBayPlan> result,
            IReadOnlyDictionary<(long X, long Y), SupportSpanPlan> lookup,
            double x0, double y0, double x1, double y1, double maximumTopAdjustmentMm)
        {
            if (!lookup.TryGetValue(SupportPointKey(x0, y0), out SupportSpanPlan first) ||
                !lookup.TryGetValue(SupportPointKey(x1, y1), out SupportSpanPlan second)) return;
            if (first.SystemType != second.SystemType) return;

            // A point-family bay is horizontal.  If the real surfaces differ by more
            // than construction tolerance, omitting it is safer than drawing one leg
            // in mid-air or cutting through the soffit.
            if (Math.Abs(first.BaseZ - second.BaseZ) > Mm(5) ||
                Math.Abs(first.ContactZ - second.ContactZ) > Mm(5)) return;
            double baseZ = Math.Max(first.BaseZ, second.BaseZ);
            double contactZ = Math.Min(first.ContactZ, second.ContactZ);
            double clearHeightMm = UnitUtils.ConvertFromInternalUnits(contactZ - baseZ,
                UnitTypeId.Millimeters);
            if (!NativeAccessoryFamilyDimensionPolicy.TryGetScaffoldTierLayout(
                    first.SystemType, clearHeightMm, maximumTopAdjustmentMm,
                    out int moduleCount, out double moduleHeightMm)) return;
            var tiers = new ScaffoldTierLayout(moduleCount, moduleHeightMm);
            double bayWidthMm = NativeAccessoryFamilyDimensionPolicy.GetScaffoldBayFamilyWidth(
                UnitUtils.ConvertFromInternalUnits(
                    new XYZ(x1 - x0, y1 - y0, 0).GetLength(), UnitTypeId.Millimeters));
            if (bayWidthMm < 300) return;
            result.Add(new ScaffoldBayPlan
            {
                X0 = x0,
                Y0 = y0,
                X1 = x1,
                Y1 = y1,
                BaseZ = baseZ,
                ClearHeightMm = clearHeightMm,
                DiameterMm = first.DiameterMm,
                BayWidthMm = bayWidthMm,
                ModuleHeightMm = tiers.ModuleHeightMm,
                ModuleCount = tiers.Count,
                SystemType = first.SystemType
            });
            first.IsCoveredByBay = true;
            second.IsCoveredByBay = true;
        }

        private static bool TryFaceElevationAt(IEnumerable<PlanarFace> faces,
            double x, double y, bool selectHighest, out double elevation)
        {
            elevation = selectHighest ? double.NegativeInfinity : double.PositiveInfinity;
            bool found = false;
            foreach (PlanarFace face in faces)
            {
                try
                {
                    XYZ normal = face.FaceNormal;
                    if (Math.Abs(normal.Z) < 1e-8) continue;
                    XYZ origin = face.Origin;
                    double z = origin.Z -
                               (normal.X * (x - origin.X) + normal.Y * (y - origin.Y)) / normal.Z;
                    IntersectionResult projection = face.Project(new XYZ(x, y, z));
                    if (projection == null || projection.Distance > Mm(5) ||
                        !face.IsInside(projection.UVPoint)) continue;
                    if (!found || (selectHighest ? projection.XYZPoint.Z > elevation :
                            projection.XYZPoint.Z < elevation))
                        elevation = projection.XYZPoint.Z;
                    found = true;
                }
                catch (Autodesk.Revit.Exceptions.ApplicationException)
                {
                    // Invalid imported faces are ignored; no geometric fallback to a
                    // nominal Level is allowed for shoring.
                }
            }
            return found;
        }

        private static (long X, long Y) SupportPointKey(double x, double y)
        {
            long Q(double internalValue) => (long)Math.Round(UnitUtils.ConvertFromInternalUnits(
                internalValue, UnitTypeId.Millimeters));
            return (Q(x), Q(y));
        }

        private static (long X, long Y, long Z) SupportNodeKey(double x, double y, double z)
        {
            (long X, long Y) point = SupportPointKey(x, y);
            long zMm = (long)Math.Round(UnitUtils.ConvertFromInternalUnits(
                z, UnitTypeId.Millimeters));
            return (point.X, point.Y, zMm);
        }

        /// <summary>The caller owns the project transaction.</summary>
        public PlacementResult Place(IEnumerable<(Element Element, StructuralComponentType Type)> components,
            View3D previewView,
            IReadOnlyDictionary<AccessoryVisualKind, OverrideGraphicSettings> graphicsByKind,
            Action<int, string> reportProgress = null, Func<bool> isCancellationRequested = null)
        {
            _placedPropLocations.Clear();
            _placedScaffoldBays.Clear();
            _assessmentsByColumn = _data.ColumnBraceAssessmentCache.Values
                .GroupBy(item => item.ColumnId)
                .ToDictionary(group => group.Key, group => group.ToList());
            foreach (ColumnBraceAssessment assessment in _assessmentsByColumn.Values.SelectMany(items => items))
            foreach (BraceDirectionAssessment direction in assessment.Directions)
            {
                direction.WasRenderedAsProp = false;
                direction.WasResolvedByAlternativeRestraint = false;
            }
            var result = new PlacementResult();
            result.UnsupportedShoringPointCount = _unsupportedShoringPointCount;
            List<(Element Element, StructuralComponentType Type)> componentList = components.ToList();
            for (int componentIndex = 0; componentIndex < componentList.Count; componentIndex++)
            {
                ThrowIfCancelled(isCancellationRequested);
                (Element element, StructuralComponentType type) = componentList[componentIndex];
                CalculationParameters parameters = EffectiveParameters(element, type);
                if (type == StructuralComponentType.Slab)
                {
                    if (!parameters.IsGroundLevel)
                    {
                        result.PropCount += PlaceSlabProps(element, parameters, previewView,
                            Graphics(graphicsByKind, AccessoryVisualKind.Support));
                        result.ScaffoldBraceCount += PlaceSlabScaffoldBraces(element, parameters, previewView,
                            Graphics(graphicsByKind, AccessoryVisualKind.ScaffoldBrace));
                    }
                    else result.ShoringSuppressedComponentCount++;
                }
                else if (type == StructuralComponentType.Beam)
                {
                    if (!parameters.IsGroundLevel)
                    {
                        result.PropCount += PlaceBeamProps(element, parameters, previewView,
                            Graphics(graphicsByKind, AccessoryVisualKind.Support));
                        result.ScaffoldBraceCount += PlaceBeamScaffoldBraces(element, parameters, previewView,
                            Graphics(graphicsByKind, AccessoryVisualKind.ScaffoldBrace));
                    }
                    else result.ShoringSuppressedComponentCount++;
                    result.BeamClampCount += PlaceBeamClamps(element, parameters, previewView,
                        Graphics(graphicsByKind, AccessoryVisualKind.Clamp));
                    if (parameters.BeamPlacementModeIndex == 2)
                    {
                        result.BeamSurfaceTieRodCount += PlaceBeamSurfaceTies(element, parameters, previewView,
                            Graphics(graphicsByKind, AccessoryVisualKind.TieRod));
                        result.BeamSideBraceCount += PlaceBeamSideBraces(element, parameters, previewView,
                            Graphics(graphicsByKind, AccessoryVisualKind.PushPullProp));
                    }
                }
                else if (type == StructuralComponentType.Column)
                {
                    result.ColumnYokeCount += PlaceColumnYokes(element, parameters, previewView,
                        Graphics(graphicsByKind, AccessoryVisualKind.Clamp));
                    result.ColumnTieRodCount += PlaceColumnTieRods(element, parameters, previewView,
                        Graphics(graphicsByKind, AccessoryVisualKind.TieRod));
                    result.PushPullPropCount += PlaceColumnPushPullProps(element, parameters, previewView,
                        Graphics(graphicsByKind, AccessoryVisualKind.PushPullProp));
                    result.TurnbuckleCount += PlaceColumnTurnbuckles(element, previewView,
                        Graphics(graphicsByKind, AccessoryVisualKind.TieRod));
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
            EvaluateColumnBraceCoverage(result, componentList);
            return result;
        }

        private static OverrideGraphicSettings Graphics(
            IReadOnlyDictionary<AccessoryVisualKind, OverrideGraphicSettings> graphicsByKind,
            AccessoryVisualKind kind)
        {
            return graphicsByKind != null && graphicsByKind.TryGetValue(kind, out OverrideGraphicSettings graphics)
                ? graphics
                : new OverrideGraphicSettings();
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
            if (type == StructuralComponentType.Column)
                ApplyResolvedColumnBraceAssessment(element.Id.Value, parameters);
            return parameters;
        }

        private void ApplyResolvedColumnBraceAssessment(long columnId, CalculationParameters parameters)
        {
            List<ColumnBraceAssessment> assessments = _data.ColumnBraceAssessmentCache.Values
                .Where(item => item.ColumnId == columnId && item.Directions.Count > 0)
                .ToList();
            if (assessments.Count == 0) return;

            int supportedDirections = assessments.Min(item =>
                item.Directions.Count(direction => direction.CanPlaceProp));
            parameters.ColumnSupportedBraceDirectionCount = Math.Max(0,
                Math.Min(4, supportedDirections));
            parameters.ColumnIsBoundaryOrCorner = supportedDirections < 4 ||
                                                   assessments.Any(item => item.IsPerimeterForCalculation);
            parameters.ColumnBracePlacementModeIndex = parameters.ColumnIsBoundaryOrCorner ? 2 : 0;
        }

        private static NativeAccessoryFamilyDimensionPolicy.PushPullProfile PushPullProfileFor(
            CalculationParameters parameters)
        {
            return NativeAccessoryFamilyDimensionPolicy.GetPushPullProfile(
                parameters?.ColumnBraceAllowableCapacity ?? 15.0,
                parameters?.ColumnBraceDiameter ?? 0.0,
                parameters?.ColumnBraceThickness ?? 0.0);
        }

        private static NativeAccessoryFamilyDimensionPolicy.PushPullProfile BeamSideBraceProfileFor(
            CalculationParameters parameters)
        {
            return NativeAccessoryFamilyDimensionPolicy.GetPushPullProfile(
                parameters?.BeamSideBraceAllowableCapacity ?? 15.0,
                parameters?.BeamSideBraceDiameter ?? 48.0,
                parameters?.BeamSideBraceThickness ?? 2.0);
        }

        private int PlaceSlabProps(Element element, CalculationParameters parameters, View3D view,
            OverrideGraphicSettings graphics)
        {
            if (!_supportSpansByElement.TryGetValue(element.Id.Value,
                    out List<SupportSpanPlan> spans)) return 0;
            int count = 0;
            foreach (SupportSpanPlan span in spans.Where(item => !item.IsCoveredByBay && item.HeightMm > 0))
            {
                if (!_symbols.TryGetValue(ScaffoldStandardKey(span.SystemType,
                        span.DiameterMm, span.HeightMm),
                        out FamilySymbol symbol)) continue;
                Activate(symbol);
                if (!TryNewUniqueProp(symbol, new XYZ(span.X, span.Y, span.BaseZ),
                        out FamilyInstance instance))
                    continue;
                Mark(instance, view, graphics);
                count++;
            }
            return count;
        }

        private int PlaceColumnPushPullProps(Element element, CalculationParameters parameters,
            View3D view, OverrideGraphicSettings graphics)
        {
            if (!_assessmentsByColumn.TryGetValue(element.Id.Value,
                    out List<ColumnBraceAssessment> assessments)) return 0;
            if (assessments.Count == 0) return 0;
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null) return 0;
            NativeAccessoryFamilyDimensionPolicy.PushPullProfile profile =
                PushPullProfileFor(parameters);
            ConstructionElevationResolver bearingResolver = _braceBearingResolver ??
                new ConstructionElevationResolver(_document);
            int count = 0;
            foreach (ColumnBraceAssessment assessment in assessments)
            {
                foreach ((BraceDirectionAssessment direction, double tangentOffsetMm,
                             int assemblyIndex) in BraceAssemblyOffsets(assessment, parameters))
                {
                    if (!TryResolveOffsetBraceAxis(bearingResolver, element, box, assessment,
                            direction, tangentOffsetMm, out XYZ foot, out XYZ attach))
                    {
                        AddBraceWarning(assessment, direction,
                            $"bo qua chong song song thu {assemblyIndex + 1} vi ban de lech khong con mat tua");
                        continue;
                    }

                    XYZ vector = attach - foot;
                    if (!IsInclinedBraceAxis(foot, attach))
                    {
                        AddBraceWarning(assessment, direction,
                            "bo qua vi truc chong khong co do doc 3D hop le");
                        continue;
                    }
                    double lengthMm = Round(UnitUtils.ConvertFromInternalUnits(vector.GetLength(),
                        UnitTypeId.Millimeters), 50);
                    if (!_symbols.TryGetValue(PushPullKey(lengthMm, profile),
                            out FamilySymbol symbol))
                    {
                        AddBraceWarning(assessment, direction,
                            $"khong co family chong dai {lengthMm:F0} mm");
                        continue;
                    }
                    Activate(symbol);
                    Level level = NearestLevel(foot.Z);
                    if (level == null)
                    {
                        AddBraceWarning(assessment, direction,
                            "khong tim thay Level gan cao do chan chong");
                        continue;
                    }
                    // A curve-based Generic Model is oriented by its placement line. This
                    // avoids RotateElement, which Revit rejects for one-level point families.
                    FamilyInstance instance = _document.Create.NewFamilyInstance(
                        Line.CreateBound(foot, attach), symbol, level, StructuralType.Brace);
                    DisallowFrameJoins(instance);
                    if (!PlacedBracePreservesAxis(instance, foot, attach))
                    {
                        _document.Delete(instance.Id);
                        AddBraceWarning(assessment, direction,
                            "family khong giu duoc truc chong xien 3D; instance da bi loai");
                        continue;
                    }
                    Mark(instance, view, graphics);
                    direction.WasRenderedAsProp = true;
                    count++;
                }
            }
            return count;
        }

        private void EvaluateColumnBraceCoverage(PlacementResult result,
            IEnumerable<(Element Element, StructuralComponentType Type)> components)
        {
            var selectedColumnIds = new HashSet<long>(components
                .Where(item => item.Type == StructuralComponentType.Column)
                .Select(item => item.Element.Id.Value));
            List<ColumnBraceAssessment> assessments = _assessmentsByColumn.Values
                .SelectMany(items => items)
                .Where(item => selectedColumnIds.Contains(item.ColumnId))
                .ToList();
            var assessedColumnIds = new HashSet<long>(assessments.Select(item => item.ColumnId));
            List<long> missingAssessmentIds = selectedColumnIds
                .Where(columnId => !assessedColumnIds.Contains(columnId))
                .OrderBy(columnId => columnId)
                .ToList();

            // A selected column must never disappear silently from the coverage report.
            // If geometry/level segmentation could not create an assessment, count that
            // column as one unresolved segment and expose its ElementId to the user.
            result.ColumnBraceSegmentCount = assessments.Count + missingAssessmentIds.Count;
            result.ColumnBraceUnrestrainedSegmentCount += missingAssessmentIds.Count;
            result.ColumnBraceUnderRestrainedSegmentCount += missingAssessmentIds.Count;
            foreach (long columnId in missingAssessmentIds)
            {
                result.UnderRestrainedColumnIds.Add(columnId);
                string missingWarning = $"Cot {columnId}: khong tao duoc danh gia mat tua chong xien; " +
                                        "cot da duoc danh dau chua du hai phuong thay vi bi bo qua.";
                if (!_warnings.Contains(missingWarning)) _warnings.Add(missingWarning);
            }

            foreach (ColumnBraceAssessment assessment in assessments)
            {
                List<BraceDirectionAssessment> resolved = assessment.Directions
                    .Where(direction => direction.WasRenderedAsProp ||
                                        direction.WasResolvedByAlternativeRestraint)
                    .ToList();
                if (resolved.Count == 0)
                    result.ColumnBraceUnrestrainedSegmentCount++;

                bool hasIndependentPair = false;
                for (int first = 0; first < resolved.Count && !hasIndependentPair; first++)
                for (int second = first + 1; second < resolved.Count; second++)
                {
                    Vector3D firstDirection = resolved[first].LocalDirection;
                    Vector3D secondDirection = resolved[second].LocalDirection;
                    if (firstDirection.LengthSquared < 1e-8 || secondDirection.LengthSquared < 1e-8)
                        continue;
                    firstDirection.Normalize();
                    secondDirection.Normalize();
                    if (Math.Abs(Vector3D.DotProduct(firstDirection, secondDirection)) < 0.80)
                    {
                        hasIndependentPair = true;
                        break;
                    }
                }

                if (hasIndependentPair)
                {
                    result.ColumnBraceCoveredSegmentCount++;
                    continue;
                }

                result.ColumnBraceUnderRestrainedSegmentCount++;
                if (!result.UnderRestrainedColumnIds.Contains(assessment.ColumnId))
                    result.UnderRestrainedColumnIds.Add(assessment.ColumnId);
                string warning = $"Cot {assessment.ColumnId}, tang {assessment.LevelName}: " +
                                 "chua du 2 phuong giu on dinh doc lap; can bo sung chan chong co mat tua " +
                                 "hoac neo/cap da duoc ky su xac nhan.";
                if (!_warnings.Contains(warning)) _warnings.Add(warning);
            }
        }

        private static IEnumerable<(BraceDirectionAssessment Direction, double TangentOffsetMm,
            int AssemblyIndex)> BraceAssemblyOffsets(ColumnBraceAssessment assessment,
            CalculationParameters parameters)
        {
            List<BraceDirectionAssessment> validDirections = assessment?.Directions?
                .Where(item => item.CanPlaceProp).ToList() ?? new List<BraceDirectionAssessment>();
            if (validDirections.Count == 0) yield break;

            // ColumnBraceCount is the total designed assembly count, not a fixed
            // one-member-per-face switch.
            int requestedCount = Math.Min(validDirections.Count * 2,
                Math.Max(validDirections.Count,
                    Math.Max(1, parameters?.ColumnBraceCount ?? validDirections.Count)));
            int baseCount = requestedCount / validDirections.Count;
            int remainder = requestedCount % validDirections.Count;
            for (int directionIndex = 0; directionIndex < validDirections.Count; directionIndex++)
            {
                int countOnDirection = baseCount + (directionIndex < remainder ? 1 : 0);
                for (int assemblyIndex = 0; assemblyIndex < countOnDirection; assemblyIndex++)
                {
                    double faceLengthMm = validDirections[directionIndex].DirectionIndex < 2
                        ? assessment.ColumnDepthMm
                        : assessment.ColumnWidthMm;
                    double halfOffsetMm = countOnDirection <= 1
                        ? 0.0
                        : Math.Min(90.0, Math.Max(0.0, (faceLengthMm - 80.0) / 2.0));
                    double offsetMm = countOnDirection <= 1
                        ? 0.0
                        : -halfOffsetMm + 2.0 * halfOffsetMm * assemblyIndex /
                          (countOnDirection - 1);
                    yield return (validDirections[directionIndex], offsetMm, assemblyIndex);
                }
            }
        }

        private bool TryResolveOffsetBraceAxis(ConstructionElevationResolver resolver,
            Element column, BoundingBoxXYZ box, ColumnBraceAssessment assessment,
            BraceDirectionAssessment direction, double requestedTangentOffsetMm,
            out XYZ foot, out XYZ attach)
        {
            foot = ProjectPoint(direction.ProposedFootPoint);
            double attachZmm = assessment.SupportElevationMm + assessment.ColumnHeightMm * 0.85;
            attach = ColumnBraceAttachmentPoint(box, assessment, direction, attachZmm);

            var outward = new XYZ(direction.LocalDirection.X, direction.LocalDirection.Y, 0);
            if (outward.GetLength() < 1e-8) return false;
            outward = outward.Normalize();
            var tangent = new XYZ(-outward.Y, outward.X, 0);
            double appliedOffsetMm = requestedTangentOffsetMm;

            // The central member was already validated during definition preparation.
            // Every displaced member is independently sampled over a 150 mm base patch.
            // Tight edges progressively reduce the separation; a failed patch is skipped
            // rather than reverting to a duplicate member at the original point.
            int attempts = Math.Abs(appliedOffsetMm) < 0.01 ? 1 : 5;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                XYZ candidateFoot = foot + tangent * Mm(appliedOffsetMm);
                if (TryResolveBraceBasePatch(resolver, column, candidateFoot,
                        out double bearingZ))
                {
                    candidateFoot = new XYZ(candidateFoot.X, candidateFoot.Y, bearingZ + Mm(60));
                    foot = candidateFoot;
                    attach += tangent * Mm(appliedOffsetMm);
                    return true;
                }
                appliedOffsetMm *= 0.5;
            }
            return false;
        }

        private int PlaceColumnTurnbuckles(Element element, View3D view, OverrideGraphicSettings graphics)
        {
            if (!_assessmentsByColumn.TryGetValue(element.Id.Value,
                    out List<ColumnBraceAssessment> assessments)) return 0;
            if (assessments.Count == 0) return 0;
            Level level = NearestLevel(element.get_BoundingBox(null).Min.Z);
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
                    StructuralType.Brace);
                DisallowFrameJoins(instance);
                Mark(instance, view, graphics);
                direction.WasResolvedByAlternativeRestraint = true;
                count++;
            }
            return count;
        }

        private static void DisallowFrameJoins(FamilyInstance instance)
        {
            try
            {
                StructuralFramingUtils.DisallowJoinAtEnd(instance, 0);
                StructuralFramingUtils.DisallowJoinAtEnd(instance, 1);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                // If a user replaces this generated type with a non-framing family,
                // placement is still valid and there are simply no frame ends to join.
            }
        }

        private XYZ ColumnBraceAttachmentPoint(BoundingBoxXYZ box,
            ColumnBraceAssessment assessment, BraceDirectionAssessment direction,
            double? attachZmm = null)
        {
            double centerX = (box.Min.X + box.Max.X) / 2;
            double centerY = (box.Min.Y + box.Max.Y) / 2;
            var outward = new XYZ(direction.LocalDirection.X, direction.LocalDirection.Y, 0);
            if (outward.GetLength() < 1e-8) outward = XYZ.BasisX;
            else outward = outward.Normalize();

            // Directions 0/1 are normal to the local X faces and directions 2/3
            // to the local Y faces.  Connect to the outside of the column
            // formwork/yoke package instead of drawing every member through the
            // column centre.
            double halfSectionMm = direction.DirectionIndex < 2
                ? assessment.ColumnWidthMm / 2.0
                : assessment.ColumnDepthMm / 2.0;
            const double formworkAndHardwareClearanceMm = 55.0;
            double radialOffset = Mm(Math.Max(0, halfSectionMm) + formworkAndHardwareClearanceMm);
            double zMm = attachZmm ??
                         (assessment.SupportElevationMm + assessment.ColumnHeightMm * 0.85);
            return new XYZ(centerX + outward.X * radialOffset,
                centerY + outward.Y * radialOffset,
                Mm(_braceCoordinateOriginMm.Z + zMm));
        }

        private static bool IsInclinedBraceAxis(XYZ foot, XYZ attach)
        {
            XYZ vector = attach - foot;
            double horizontal = Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y);
            double vertical = attach.Z - foot.Z;
            return vertical >= Mm(300) && horizontal >= Mm(300) && vector.GetLength() >= Mm(700);
        }

        private static bool PlacedBracePreservesAxis(FamilyInstance instance, XYZ requestedFoot,
            XYZ requestedAttach)
        {
            if (!(instance?.Location is LocationCurve location) || location.Curve == null) return false;
            XYZ start = location.Curve.GetEndPoint(0);
            XYZ end = location.Curve.GetEndPoint(1);
            double requestedRise = Math.Abs(requestedAttach.Z - requestedFoot.Z);
            double actualRise = Math.Abs(end.Z - start.Z);
            double requestedLength = requestedFoot.DistanceTo(requestedAttach);
            double actualLength = start.DistanceTo(end);
            if (requestedRise < Mm(300) || actualRise < requestedRise * 0.90 ||
                actualLength < requestedLength * 0.90) return false;

            // LocationCurve alone did not expose the old 0..L family-origin bug.
            // When Revit already exposes the instance geometry box, also require
            // the visible solid to be centred on and rise with the requested axis.
            BoundingBoxXYZ box = instance.get_BoundingBox(null);
            if (box == null) return true;
            XYZ visibleCenter = (box.Min + box.Max) / 2.0;
            XYZ requestedCenter = (requestedFoot + requestedAttach) / 2.0;
            double centerTolerance = Math.Max(Mm(300), requestedLength * 0.12);
            if (visibleCenter.DistanceTo(requestedCenter) > centerTolerance ||
                box.Max.Z - box.Min.Z < requestedRise * 0.80) return false;

            XYZ axis = (requestedAttach - requestedFoot).Normalize();
            double minimumProjection = double.PositiveInfinity;
            double maximumProjection = double.NegativeInfinity;
            foreach (double x in new[] { box.Min.X, box.Max.X })
            foreach (double y in new[] { box.Min.Y, box.Max.Y })
            foreach (double z in new[] { box.Min.Z, box.Max.Z })
            {
                double projection = new XYZ(x, y, z).Subtract(requestedFoot).DotProduct(axis);
                minimumProjection = Math.Min(minimumProjection, projection);
                maximumProjection = Math.Max(maximumProjection, projection);
            }
            double endpointTolerance = Mm(300);
            return minimumProjection >= -endpointTolerance &&
                   maximumProjection <= requestedLength + endpointTolerance &&
                   minimumProjection <= endpointTolerance &&
                   maximumProjection >= requestedLength - endpointTolerance;
        }

        private void AddBraceWarning(ColumnBraceAssessment assessment,
            BraceDirectionAssessment direction, string detail)
        {
            string warning = $"Cot {assessment.ColumnId}, tang {assessment.LevelName}, " +
                             $"huong {direction.DirectionIndex + 1}: {detail}.";
            if (!_warnings.Contains(warning)) _warnings.Add(warning);
        }

        private void RevalidateColumnBraceBearings(
            IReadOnlyDictionary<long, Element> componentById)
        {
            _braceBearingResolver = new ConstructionElevationResolver(_document);
            foreach (ColumnBraceAssessment assessment in _data.ColumnBraceAssessmentCache.Values)
            {
                if (!componentById.TryGetValue(assessment.ColumnId, out Element column)) continue;
                bool changed = false;
                foreach (BraceDirectionAssessment direction in assessment.Directions.ToList())
                {
                    bool wasPlaceable = direction.CanPlaceProp;
                    if (!TryResolveAdaptiveBraceBasePatch(_braceBearingResolver, column, assessment,
                            direction, out Point3D resolvedLocalFoot, out double bearingZ))
                    {
                        if (wasPlaceable)
                        {
                            direction.SupportKind = BraceFootSupportKind.Unsupported;
                            changed = true;
                            AddBraceWarning(assessment, direction,
                                "khong tim thay mat tua Revit lien tuc duoi ban de sau khi da thu rut ngan chong; huong chong da bi loai");
                        }
                        continue;
                    }

                    resolvedLocalFoot.Z = UnitUtils.ConvertFromInternalUnits(bearingZ,
                                              UnitTypeId.Millimeters) - _braceCoordinateOriginMm.Z + 60.0;
                    direction.ProposedFootPoint = resolvedLocalFoot;
                    if (!wasPlaceable)
                    {
                        direction.SupportKind = assessment.DerivedType == ColumnBraceDerivedType.FoundationGround
                            ? BraceFootSupportKind.FoundationOrGround
                            : BraceFootSupportKind.Slab;
                        changed = true;
                    }
                }
                if (changed) ReclassifyAfterBearingValidation(assessment);
            }
        }

        /// <summary>
        /// Validates every direction against exact Revit faces and searches a shorter
        /// radial run before rejecting it.  The former one-way revalidation could only
        /// demote coarse candidates, so a false negative from a split/offset slab could
        /// never recover and an otherwise ordinary column received no braces.
        /// </summary>
        private bool TryResolveAdaptiveBraceBasePatch(ConstructionElevationResolver resolver,
            Element column, ColumnBraceAssessment assessment, BraceDirectionAssessment direction,
            out Point3D resolvedLocalFoot, out double bearingZ)
        {
            resolvedLocalFoot = direction.ProposedFootPoint;
            XYZ proposedProjectFoot = ProjectPoint(resolvedLocalFoot);
            if (TryResolveBraceBasePatch(resolver, column, proposedProjectFoot, out bearingZ))
                return true;

            var radial = new XYZ(direction.LocalDirection.X, direction.LocalDirection.Y, 0);
            if (radial.GetLength() < 1e-8)
            {
                bearingZ = double.NaN;
                return false;
            }
            radial = radial.Normalize();

            double preferredRunMm = direction.ResolvedRunMm > 0.1
                ? direction.ResolvedRunMm
                : Math.Max(600.0, assessment.BraceRunMm);
            double attachmentRiseMm = Math.Max(300.0, assessment.ColumnHeightMm * 0.85);
            double minimumRunMm = Math.Max(600.0,
                attachmentRiseMm / Math.Tan(75.0 * Math.PI / 180.0));
            for (double candidateRunMm = preferredRunMm - 100.0;
                 candidateRunMm >= minimumRunMm - 0.1;
                 candidateRunMm -= 100.0)
            {
                double inwardShiftMm = preferredRunMm - candidateRunMm;
                Point3D candidateLocal = direction.ProposedFootPoint -
                                         new Vector3D(radial.X, radial.Y, 0) * inwardShiftMm;
                XYZ candidateProject = ProjectPoint(candidateLocal);
                if (!TryResolveBraceBasePatch(resolver, column, candidateProject, out bearingZ))
                    continue;

                resolvedLocalFoot = candidateLocal;
                direction.ResolvedRunMm = candidateRunMm;
                return true;
            }

            bearingZ = double.NaN;
            return false;
        }

        private bool TryResolveBraceBasePatch(ConstructionElevationResolver resolver,
            Element column, BraceDirectionAssessment direction, out double bearingZ)
        {
            XYZ proposedFoot = ProjectPoint(direction.ProposedFootPoint);
            return TryResolveBraceBasePatch(resolver, column, proposedFoot, out bearingZ);
        }

        private static bool TryResolveBraceBasePatch(ConstructionElevationResolver resolver,
            Element column, XYZ proposedFoot, out double bearingZ)
        {
            bearingZ = double.NaN;
            double expectedBearingZ = proposedFoot.Z - Mm(60);
            double probeTopZ = expectedBearingZ + Mm(400);
            double patchOffset = Mm(75);
            var elevations = new List<double>();
            foreach ((double dx, double dy) in new[]
                     {
                         (0.0, 0.0),
                         (patchOffset, 0.0),
                         (-patchOffset, 0.0),
                         (0.0, patchOffset),
                         (0.0, -patchOffset)
                     })
            {
                if (!resolver.TryFindHighestBearingBelow(proposedFoot.X + dx,
                        proposedFoot.Y + dy, probeTopZ, column.Id, out double sampleZ, 50.0) ||
                    Math.Abs(sampleZ - expectedBearingZ) > Mm(300))
                    return false;
                elevations.Add(sampleZ);
            }

            if (elevations.Max() - elevations.Min() > Mm(40)) return false;
            bearingZ = elevations.Average();
            return true;
        }

        private static void ReclassifyAfterBearingValidation(ColumnBraceAssessment assessment)
        {
            if (assessment.DerivedType == ColumnBraceDerivedType.FoundationGround)
            {
                if (assessment.Directions.Any(item => item.CanPlaceProp)) return;
                assessment.DerivedType = ColumnBraceDerivedType.NoSupportWarning;
                assessment.RecommendedAction =
                    "No verified Revit foundation/ground bearing remains beneath the proposed feet.";
                return;
            }

            if (assessment.Directions.Any(item =>
                    item.SupportKind == BraceFootSupportKind.InternalOpening))
            {
                assessment.DerivedType = ColumnBraceDerivedType.InternalOpeningWarning;
                return;
            }

            List<BraceDirectionAssessment> open = assessment.Directions
                .Where(item => !item.CanPlaceProp).ToList();
            switch (open.Count)
            {
                case 0:
                    assessment.DerivedType = ColumnBraceDerivedType.Interior;
                    break;
                case 1:
                    assessment.DerivedType = ColumnBraceDerivedType.Edge;
                    assessment.RecommendedAction =
                        "One direction has no verified bearing; use three supported props and an inward tie/anchor.";
                    break;
                case 2:
                    double dot = Vector3D.DotProduct(open[0].LocalDirection,
                        open[1].LocalDirection);
                    assessment.DerivedType = dot < -0.9
                        ? ColumnBraceDerivedType.OppositeOpenWarning
                        : ColumnBraceDerivedType.Corner;
                    break;
                case 3:
                    assessment.DerivedType = ColumnBraceDerivedType.IsolatedWarning;
                    break;
                default:
                    assessment.DerivedType = ColumnBraceDerivedType.NoSupportWarning;
                    assessment.RecommendedAction =
                        "No verified Revit bearing surface remains beneath any proposed brace foot.";
                    break;
            }
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

        private List<PlanarFace> CollectSlabTopFaces() =>
            new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Floors)
                .WhereElementIsNotElementType()
                .ToElements()
                .SelectMany(GetPlanarFaces)
                .Where(face => face.FaceNormal.Z > 0.70)
                .ToList();

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
            if (!_supportSpansByElement.TryGetValue(element.Id.Value,
                    out List<SupportSpanPlan> spans)) return 0;
            int count = 0;
            foreach (SupportSpanPlan span in spans.Where(item => !item.IsCoveredByBay && item.HeightMm > 0))
            {
                if (!_symbols.TryGetValue(ScaffoldStandardKey(span.SystemType,
                        span.DiameterMm, span.HeightMm),
                        out FamilySymbol symbol)) continue;
                Activate(symbol);
                if (!TryNewUniqueProp(symbol, new XYZ(span.X, span.Y, span.BaseZ),
                        out FamilyInstance instance)) continue;
                Mark(instance, view, graphics);
                count++;
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
                XYZ origin = new XYZ(point.X, point.Y, BeamBottomAtStation(curve, box, point));
                FamilyInstance instance = NewInstance(symbol, origin);
                if (Math.Abs(rotation) > 1e-9)
                    ElementTransformUtils.RotateElement(_document, instance.Id,
                        Line.CreateBound(origin, origin + XYZ.BasisZ), rotation);
                Mark(instance, view, graphics);
                count++;
            }
            return count;
        }

        private int PlaceBeamSurfaceTies(Element element, CalculationParameters parameters, View3D view,
            OverrideGraphicSettings graphics)
        {
            if (!(element.Location is LocationCurve location) || location.Curve == null) return 0;
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null) return 0;
            if (_slabTopFaces.Count == 0) _slabTopFaces = CollectSlabTopFaces();

            Curve curve = location.Curve;
            double beamWidthMm = Round(parameters.BeamWidth > 0
                ? parameters.BeamWidth : Math.Min(BoxSizeMm(box, true), BoxSizeMm(box, false)), 10);
            double surfaceDiameterMm = Round(parameters.BeamEdgeSurfaceTieRodDiameter > 0
                ? parameters.BeamEdgeSurfaceTieRodDiameter : 12, 1);
            double anchorLengthMm = Math.Max(500, parameters.BeamEdgeSurfaceTieRodAnchorLength);
            double surfaceLengthMm = Round(beamWidthMm +
                Math.Max(6, parameters.BeamSidePlywoodThickness) + 40 + anchorLengthMm, 10);
            if (!_symbols.TryGetValue(TieRodKey(surfaceDiameterMm, surfaceLengthMm),
                    out FamilySymbol symbol)) return 0;
            Activate(symbol);

            double lengthMm = UnitUtils.ConvertFromInternalUnits(curve.Length, UnitTypeId.Millimeters);
            double beamWidth = Mm(beamWidthMm);
            double sidePlywood = Mm(Math.Max(6, parameters.BeamSidePlywoodThickness));
            double anchorLength = Mm(anchorLengthMm);
            ComponentIntersectionInfo intersection = _data.GetComponentIntersectionInfo(element.UniqueId);
            int count = 0;
            foreach (double stationMm in FormworkCalculationService.BuildBeamClampStations(lengthMm,
                         Math.Max(300, parameters.BeamEdgeSurfaceTieRodSpacing)))
            {
                double t = lengthMm <= 1e-6 ? 0.5 : Math.Max(0, Math.Min(1, stationMm / lengthMm));
                XYZ station = curve.Evaluate(t, true);
                XYZ axis = HorizontalDirection(curve.ComputeDerivatives(t, true).BasisX);
                XYZ cross = new XYZ(-axis.Y, axis.X, 0);
                double beamTopZ = BeamTopAtStation(curve, box, station);
                if (!TryResolveBeamInteriorSide(station, cross, beamWidth, beamTopZ,
                        anchorLength, intersection, out double interiorSign)) continue;

                XYZ origin = new XYZ(station.X, station.Y,
                                 beamTopZ + Mm(surfaceDiameterMm / 2 + 8)) -
                             cross * (interiorSign * (beamWidth / 2 + sidePlywood + Mm(40)));
                FamilyInstance instance = NewInstance(symbol, origin);
                double rotation = Math.Atan2(cross.Y * interiorSign, cross.X * interiorSign);
                if (Math.Abs(rotation) > 1e-9)
                    ElementTransformUtils.RotateElement(_document, instance.Id,
                        Line.CreateBound(origin, origin + XYZ.BasisZ), rotation);
                Mark(instance, view, graphics);
                count++;
            }
            if (count == 0)
                _warnings.Add($"Dầm biên {element.Id}: không tìm thấy mặt sàn phía trong đủ chiều dài neo ty chết; không đặt ty ra khoảng không.");
            return count;
        }

        private List<BeamSideBraceAxisPlan> BuildBeamSideBraceAxisPlans(Element element,
            CalculationParameters parameters, ConstructionElevationResolver resolver)
        {
            var result = new List<BeamSideBraceAxisPlan>();
            if (resolver == null || !(element.Location is LocationCurve location) ||
                location.Curve == null) return result;
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null) return result;
            if (_slabTopFaces.Count == 0) _slabTopFaces = CollectSlabTopFaces();

            Curve curve = location.Curve;
            double lengthMm = UnitUtils.ConvertFromInternalUnits(curve.Length,
                UnitTypeId.Millimeters);
            double beamWidth = Mm(Math.Max(100, parameters.BeamWidth > 0
                ? parameters.BeamWidth
                : Math.Min(BoxSizeMm(box, true), BoxSizeMm(box, false))));
            double sidePlywood = Mm(Math.Max(6, parameters.BeamSidePlywoodThickness));
            double braceSpacing = Math.Max(300, parameters.BeamSideBraceSpacing > 0
                ? parameters.BeamSideBraceSpacing : 1000);
            double targetAngle = Math.Max(35, Math.Min(70,
                parameters.BeamSideBraceAngle > 0 ? parameters.BeamSideBraceAngle : 55));
            int braceCount = Math.Max(1, parameters.BeamSideBraceCountPerStation);
            double braceDiameterMm = Math.Max(25, parameters.BeamSideBraceDiameter > 0
                ? parameters.BeamSideBraceDiameter : 48);
            ComponentIntersectionInfo intersection =
                _data.GetComponentIntersectionInfo(element.UniqueId);

            foreach (double stationMm in FormworkCalculationService.BuildBeamClampStations(
                         lengthMm, braceSpacing))
            {
                if (IsInsideBeamColumnGap(intersection?.BeamColumnGaps, stationMm)) continue;
                double t = lengthMm <= 1e-6 ? 0.5 :
                    Math.Max(0, Math.Min(1, stationMm / lengthMm));
                XYZ station = curve.Evaluate(t, true);
                XYZ axis = HorizontalDirection(curve.ComputeDerivatives(t, true).BasisX);
                XYZ cross = new XYZ(-axis.Y, axis.X, 0).Normalize();
                double beamTopZ = BeamTopAtStation(curve, box, station);
                if (!TryResolveBeamExteriorSide(station, cross, beamWidth, beamTopZ,
                        out double exteriorSign)) continue;

                double beamBottomZ = BeamBottomAtStation(curve, box, station);
                double attachZ = beamBottomZ + Math.Max(Mm(100), beamTopZ - beamBottomZ) * 0.70;
                for (int braceIndex = 0; braceIndex < braceCount; braceIndex++)
                {
                    double longitudinalOffsetMm =
                        (braceIndex - (braceCount - 1) / 2.0) * (braceDiameterMm + 35);
                    XYZ tangentOffset = axis * Mm(longitudinalOffsetMm);
                    XYZ attach = new XYZ(station.X, station.Y, attachZ) +
                                 cross * (exteriorSign *
                                     (beamWidth / 2 + sidePlywood + Mm(35))) + tangentOffset;

                    BeamSideBraceAxisPlan best = null;
                    double bestAngleError = double.MaxValue;
                    // Search only real bearing geometry. Selecting the candidate whose
                    // realised angle is closest to the checked angle avoids a guessed
                    // Level-based foot and prevents floating/outward members.
                    for (double runMm = 2400; runMm >= 250; runMm -= 100)
                    {
                        XYZ candidate = new XYZ(station.X, station.Y, attachZ) +
                                        cross * (exteriorSign *
                                            (beamWidth / 2 + Mm(runMm))) + tangentOffset;
                        if (!resolver.TryFindHighestBearingBelow(candidate.X, candidate.Y,
                                attachZ - Mm(50), element.Id, out double bearingZ)) continue;
                        XYZ foot = new XYZ(candidate.X, candidate.Y, bearingZ + Mm(30));
                        XYZ vector = attach - foot;
                        double horizontal = Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y);
                        double vertical = Math.Abs(vector.Z);
                        if (horizontal < Mm(200) || vertical < Mm(300) ||
                            !IsInclinedBraceAxis(foot, attach)) continue;
                        double actualAngle = Math.Atan2(vertical, horizontal) * 180.0 / Math.PI;
                        double angleError = Math.Abs(actualAngle - targetAngle);
                        if (angleError >= bestAngleError) continue;
                        double realisedLengthMm = UnitUtils.ConvertFromInternalUnits(
                            vector.GetLength(), UnitTypeId.Millimeters);
                        if (realisedLengthMm < 700) continue;
                        bestAngleError = angleError;
                        best = new BeamSideBraceAxisPlan
                        {
                            Foot = foot,
                            Attach = attach,
                            LengthMm = Round(realisedLengthMm, 50)
                        };
                    }
                    if (best != null) result.Add(best);
                }
            }
            return result;
        }

        private int PlaceBeamSideBraces(Element element, CalculationParameters parameters,
            View3D view, OverrideGraphicSettings graphics)
        {
            if (!_beamSideBraceAxesByElement.TryGetValue(element.Id.Value,
                    out List<BeamSideBraceAxisPlan> plans))
            {
                plans = BuildBeamSideBraceAxisPlans(element, parameters,
                    _braceBearingResolver ?? new ConstructionElevationResolver(_document));
            }
            NativeAccessoryFamilyDimensionPolicy.PushPullProfile profile =
                BeamSideBraceProfileFor(parameters);
            int count = 0;
            foreach (BeamSideBraceAxisPlan plan in plans)
            {
                if (!_symbols.TryGetValue(BeamSideBraceKey(plan.LengthMm, profile),
                        out FamilySymbol symbol)) continue;
                Level level = NearestLevel(plan.Foot.Z);
                if (level == null) continue;
                Activate(symbol);
                FamilyInstance instance = _document.Create.NewFamilyInstance(
                    Line.CreateBound(plan.Foot, plan.Attach), symbol, level,
                    StructuralType.Brace);
                DisallowFrameJoins(instance);
                if (!PlacedBracePreservesAxis(instance, plan.Foot, plan.Attach))
                {
                    _document.Delete(instance.Id);
                    continue;
                }
                Mark(instance, view, graphics);
                count++;
            }
            if (count == 0)
            {
                string warning = $"Dầm biên {element.Id}: không tìm thấy vị trí chân chống xiên " +
                                 "có mặt tựa thực; không dựng chống ra khoảng không.";
                if (!_warnings.Contains(warning)) _warnings.Add(warning);
            }
            return count;
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

        private static bool IsInsideBeamColumnGap(
            IEnumerable<FormworkLengthInterval> gaps, double stationMm)
        {
            return gaps != null && gaps.Any(gap => gap != null &&
                stationMm >= gap.StartMm - 1.0 && stationMm <= gap.EndMm + 1.0);
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
            return PlaceScaffoldPlans(element, view, graphics);
        }

        private int PlaceBeamScaffoldBraces(Element element, CalculationParameters parameters, View3D view,
            OverrideGraphicSettings graphics)
        {
            return PlaceScaffoldPlans(element, view, graphics);
        }

        private int PlaceScaffoldPlans(Element element, View3D view,
            OverrideGraphicSettings graphics)
        {
            if (!_scaffoldBaysByElement.TryGetValue(element.Id.Value,
                    out List<ScaffoldBayPlan> plans)) return 0;
            int placed = 0;
            foreach (ScaffoldBayPlan plan in plans
                         .OrderBy(item => item.BaseZ)
                         .ThenBy(item => Math.Min(item.X0, item.X1))
                         .ThenBy(item => Math.Min(item.Y0, item.Y1)))
            {
                if (!_symbols.TryGetValue(ScaffoldBraceKey(plan.SystemType,
                        plan.BayWidthMm, plan.ClearHeightMm, plan.ModuleHeightMm,
                        plan.ModuleCount, plan.DiameterMm), out FamilySymbol symbol)) continue;
                Activate(symbol);
                double rotation = Math.Atan2(plan.Y1 - plan.Y0, plan.X1 - plan.X0);
                (long X, long Y) first = SupportPointKey(plan.X0, plan.Y0);
                (long X, long Y) second = SupportPointKey(plan.X1, plan.Y1);
                if (first.X > second.X || first.X == second.X && first.Y > second.Y)
                    (first, second) = (second, first);
                string placementKey = string.Join("|", first.X, first.Y,
                    second.X, second.Y,
                    Math.Round(UnitUtils.ConvertFromInternalUnits(plan.BaseZ,
                        UnitTypeId.Millimeters)));
                // System is intentionally absent from the collision key. If two host
                // passes ever propose the same physical bay, the first stable plan wins
                // instead of overlaying two selected geometries at one location.
                if (!_placedScaffoldBays.Add(placementKey)) continue;

                XYZ bayVector = new XYZ(plan.X1 - plan.X0, plan.Y1 - plan.Y0, 0);
                double actualBayLength = bayVector.GetLength();
                XYZ bayDirection = actualBayLength > 1e-9
                    ? bayVector / actualBayLength
                    : XYZ.BasisX;
                double familyBayLength = Mm(plan.BayWidthMm);
                double centeringOffset = Math.Max(0, actualBayLength - familyBayLength) / 2;
                XYZ origin = new XYZ(plan.X0, plan.Y0, plan.BaseZ) +
                             bayDirection * centeringOffset;
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
            foreach (double elevationMm in BuildSafeColumnYokeElevations(parameters, barHeight, pairCount))
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
            double barHeight = Round(parameters.ColumnYokeHeight > 0 ? parameters.ColumnYokeHeight : 40, 5);
            int pairCount = Math.Max(1, parameters.ColumnYokePairCount);
            parameters.ColumnHeight = UnitUtils.ConvertFromInternalUnits(box.Max.Z - box.Min.Z,
                UnitTypeId.Millimeters);
            Level level = NearestLevel(box.Min.Z);
            if (level == null) return 0;

            int count = 0;
            foreach (double elevationMm in BuildSafeColumnYokeElevations(parameters, barHeight, pairCount))
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

        private static IReadOnlyList<double> BuildSafeColumnYokeElevations(
            CalculationParameters parameters, double barHeightMm, int pairCount)
        {
            double columnHeightMm = Math.Max(1, parameters.ColumnHeight);
            int pairs = Math.Max(1, Math.Min(4, pairCount));
            double pairPitchMm = Math.Max(20, barHeightMm) + 15;
            double groupHalfHeightMm = (pairs - 1) * pairPitchMm / 2 +
                                       Math.Max(20, barHeightMm) / 2;
            double minimumCenterMm = groupHalfHeightMm + 20;
            double maximumCenterMm = columnHeightMm - groupHalfHeightMm - 20;
            if (maximumCenterMm < minimumCenterMm)
                return new[] { columnHeightMm / 2 };
            return FormworkCalculationService.BuildColumnYokeElevations(parameters)
                .Select(value => Math.Max(minimumCenterMm, Math.Min(maximumCenterMm, value)))
                .Select(value => Math.Round(value, 3))
                .Distinct()
                .OrderBy(value => value)
                .ToList();
        }

        private void TryEnsureSymbol(string familyName, Action<Document> buildGeometry,
            bool lineBased = false, bool structuralBrace = false)
        {
            if (!_requestedFamilyDefinitions.Add(familyName)) return;
            if (_failedSymbols.Contains(familyName)) return;
            if (_symbols.ContainsKey(familyName))
            {
                if (_preexistingProjectSymbols.Contains(familyName) &&
                    _requestedProjectSymbols.Add(familyName)) ReusedProjectFamilyCount++;
                return;
            }
            try
            {
                EnsureSymbol(familyName, buildGeometry, lineBased, structuralBrace);
            }
            catch (Exception exception)
            {
                _failedSymbols.Add(familyName);
                string warning = $"{familyName}: {exception.Message}";
                if (!_warnings.Contains(warning)) _warnings.Add(warning);
                System.Diagnostics.Debug.WriteLine("Native accessory family warning: " + warning);
            }
        }

        private void EnsureSymbol(string familyName, Action<Document> buildGeometry,
            bool lineBased, bool structuralBrace)
        {
            FamilySymbol existing = FindSymbol(familyName);
            if (existing != null)
            {
                _symbols[familyName] = existing;
                if (_preexistingProjectSymbols.Contains(familyName) &&
                    _requestedProjectSymbols.Add(familyName)) ReusedProjectFamilyCount++;
                return;
            }

            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DALTUDTXD_RevitAPI", "GeneratedFamilies");
            Directory.CreateDirectory(folder);
            string familyPath = Path.Combine(folder, familyName + ".rfa");
            if (TryLoadCachedFamily(familyPath, familyName, out FamilySymbol cachedSymbol))
            {
                _symbols[familyName] = cachedSymbol;
                LoadedCachedFamilyCount++;
                return;
            }

            RemoveStaleCacheFile(familyPath);
            Family loadedFamily = CreateAndLoadFamily(familyPath, buildGeometry, lineBased, structuralBrace);
            GeneratedFamilyCount++;
            if (loadedFamily == null)
            {
                existing = FindSymbolAfterLoad(familyName);
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

        private void IndexExistingSymbols()
        {
            if (_projectSymbolsIndexed) return;
            foreach (FamilySymbol symbol in new FilteredElementCollector(_document)
                         .OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>())
            {
                string familyName = symbol.FamilyName;
                if (string.IsNullOrWhiteSpace(familyName) ||
                    !familyName.StartsWith(FamilyPrefix, StringComparison.OrdinalIgnoreCase) ||
                    _symbols.ContainsKey(familyName)) continue;
                _symbols[familyName] = symbol;
                _preexistingProjectSymbols.Add(familyName);
            }
            _projectSymbolsIndexed = true;
        }

        private bool TryLoadCachedFamily(string familyPath, string familyName, out FamilySymbol symbol)
        {
            symbol = null;
            if (!File.Exists(familyPath)) return false;
            try
            {
                _document.LoadFamily(familyPath, new ReloadFamilyOptions(), out Family loadedFamily);
                symbol = loadedFamily?.GetFamilySymbolIds()
                    .Select(id => _document.GetElement(id) as FamilySymbol)
                    .FirstOrDefault(item => item != null) ?? FindSymbolAfterLoad(familyName);
                return symbol != null;
            }
            catch (Exception exception)
            {
                // A stale or version-incompatible cache entry is harmless: rebuild it
                // from the template below and overwrite the generated RFA.
                System.Diagnostics.Debug.WriteLine(
                    $"Native accessory cache miss for {familyName}: {exception.Message}");
                return false;
            }
        }

        private FamilySymbol FindSymbol(string familyName)
        {
            if (_symbols.TryGetValue(familyName, out FamilySymbol indexed)) return indexed;
            IndexExistingSymbols();
            return _symbols.TryGetValue(familyName, out indexed) ? indexed : null;
        }

        private FamilySymbol FindSymbolAfterLoad(string familyName)
        {
            FamilySymbol symbol = new FilteredElementCollector(_document)
                .OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
                .FirstOrDefault(item => string.Equals(item.FamilyName, familyName,
                    StringComparison.OrdinalIgnoreCase));
            if (symbol != null) _symbols[familyName] = symbol;
            return symbol;
        }

        private Family CreateAndLoadFamily(string path, Action<Document> buildGeometry,
            bool lineBased, bool structuralBrace)
        {
            string template = structuralBrace
                ? FindStructuralBraceTemplate()
                : FindGenericModelTemplate(lineBased);
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
                    if (structuralBrace)
                        RemoveStructuralFramingTemplateBody(family);
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

        private static void RemoveStructuralFramingTemplateBody(Document family)
        {
            // Revit's "Structural Framing - Beams and Braces" template contains a
            // sample rectangular extrusion driven by the reference length. If it is
            // left in the generated family, it stretches along the complete placement
            // line and appears as a cyan hitbox around the intended telescoping tubes.
            // Delete only model forms; keep reference planes, the analytical/path
            // definition and family parameters required for curve-based placement.
            List<ElementId> templateFormIds = new FilteredElementCollector(family)
                .WhereElementIsNotElementType()
                .ToElements()
                .Where(element => element is GenericForm)
                .Select(element => element.Id)
                .ToList();
            if (templateFormIds.Count > 0)
                family.Delete(templateFormIds);
        }

        private string FindGenericModelTemplate(bool lineBased)
            => FindFamilyTemplate(lineBased
                ? "Metric Generic Model line based.rft"
                : "Metric Generic Model.rft");

        private string FindStructuralBraceTemplate() => FindFamilyTemplate(
            "Metric Structural Framing - Beams and Braces.rft");

        private string FindFamilyTemplate(string templateName)
        {
            string root = _document.Application.FamilyTemplatePath;
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                throw new FileNotFoundException($"Không tìm thấy thư mục template family Revit cho '{templateName}'.");

            string cacheKey = root.TrimEnd(Path.DirectorySeparatorChar,
                                  Path.AltDirectorySeparatorChar) + "|" + templateName;
            lock (TemplatePathCacheLock)
            {
                if (TemplatePathCache.TryGetValue(cacheKey, out string cached) &&
                    File.Exists(cached)) return cached;
            }

            string exact = Directory.EnumerateFiles(root, templateName,
                    SearchOption.AllDirectories)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(exact))
                throw new FileNotFoundException($"Không tìm thấy template '{templateName}' của Revit.");

            lock (TemplatePathCacheLock)
                TemplatePathCache[cacheKey] = exact;
            return exact;
        }

        private static void RemoveStaleCacheFile(string path)
        {
            if (!File.Exists(path)) return;
            try
            {
                // The file was already proven unloadable by TryLoadCachedFamily.
                // Removing it before SaveAs prevents Revit from creating another
                // numbered backup and keeps the generated-family cache compact.
                File.Delete(path);
            }
            catch (IOException exception)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Unable to replace stale native accessory cache '{path}': {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Unable to replace stale native accessory cache '{path}': {exception.Message}");
            }
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
            double firstMarkerZ = innerBottom + Mm(110);
            double lastMarkerZ = innerTop - Mm(80);
            int markerCount = lastMarkerZ > firstMarkerZ
                ? Math.Min(8, Math.Max(2, (int)Math.Ceiling((lastMarkerZ - firstMarkerZ) / Mm(450)) + 1))
                : 0;
            for (int marker = 0; marker < markerCount; marker++)
            {
                double ratio = markerCount == 1 ? 0 : (double)marker / (markerCount - 1);
                double z = firstMarkerZ + (lastMarkerZ - firstMarkerZ) * ratio;
                AddFreeForm(family, CylinderAlongAxis(new XYZ(-innerRadius - Mm(2), 0, z),
                    XYZ.BasisX, 2 * innerRadius + Mm(4), Mm(4.5)));
            }

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
            double usableLength = Math.Max(0, length);
            if (usableLength <= 1e-9) return;

            // Six representative rings communicate the threaded jack while avoiding
            // dozens of independent FreeFormElements in every generated height type.
            // This is preview LOD only; the calculated diameter/capacity is unchanged.
            int count = Math.Min(6, Math.Max(2, (int)Math.Ceiling(usableLength / Mm(55)) + 1));
            for (int index = 0; index < count; index++)
            {
                double ratio = count == 1 ? 0 : (double)index / (count - 1);
                double offset = Math.Max(0, usableLength - Mm(4)) * ratio;
                AddFreeForm(family, CylinderAlongAxis(new XYZ(0, 0, startZ + offset), axis,
                    Mm(4), coreRadius + Mm(1.8)));
            }
        }

        private static void BuildScaffoldStandardFamily(Document family,
            ScaffoldSystemType system, double diameterMm, double heightMm)
        {
            BuildPropFamily(family, diameterMm, heightMm);
            double height = Mm(Math.Max(900, heightMm));
            double standardRadius = Mm(Math.Max(48, diameterMm)) / 2;
            double pitch = Mm(system == ScaffoldSystemType.Frame ? 850 : 500);
            for (double z = pitch; z < height - Mm(180); z += pitch)
                AddScaffoldNodeHardware(family, system, 0, z, standardRadius);
        }

        private static void BuildScaffoldTowerBayFamily(Document family,
            ScaffoldSystemType system, double bayWidthMm, double clearHeightMm,
            double moduleHeightMm, int moduleCount, double diameterMm)
        {
            system = ScaffoldSystemLayoutPolicy.NormalizeSingle(system);
            double width = Mm(Math.Max(300, bayWidthMm));
            double clearHeight = Mm(Math.Max(600, clearHeightMm));
            double moduleHeight = Mm(Math.Max(500, moduleHeightMm));
            double standardRadius = Mm(Math.Max(48, diameterMm)) / 2;
            double plate = Mm(150), plateThickness = Mm(8);

            // The bay owns both standards. Generic telescopic-prop instances are not
            // placed at these nodes, eliminating the former prop + H-frame overlay.
            foreach (double x in new[] { 0.0, width })
            {
                AddFreeForm(family, Box(x - plate / 2, -plate / 2, 0,
                    plate, plate, plateThickness));
                AddFreeForm(family, CylinderAlongAxis(
                    new XYZ(x, 0, plateThickness), XYZ.BasisZ,
                    Math.Max(Mm(500), clearHeight - plateThickness), standardRadius));
                AddScaffoldHead(family, x, clearHeight, plate);
            }

            int safeModuleCount = Math.Max(1, moduleCount);
            for (int tier = 0; tier < safeModuleCount; tier++)
            {
                double z0 = moduleHeight * tier;
                double z1 = Math.Min(clearHeight, z0 + moduleHeight);
                if (z1 - z0 < Mm(300)) break;
                AddScaffoldTier(family, system, width, z0, z1,
                    standardRadius, tier);
            }

            // When the catalogue stack leaves an allowable top-jack residual, retain
            // a visible final ledger below the U-head without inventing another module.
            double stackTop = Math.Min(clearHeight, safeModuleCount * moduleHeight);
            if (clearHeight - stackTop > Mm(80))
            {
                double ledgerRadius = Mm(Math.Max(18, ScaffoldBraceDiameter(diameterMm) * 0.72));
                AddFreeForm(family, CylinderBetween(
                    new XYZ(0, 0, stackTop), new XYZ(width, 0, stackTop), ledgerRadius));
            }
        }

        private static void AddScaffoldTier(Document family, ScaffoldSystemType system,
            double width, double z0, double z1, double standardRadius, int tierIndex)
        {
            double inset = Math.Max(Mm(45), standardRadius + Mm(12));
            double braceRadius = Mm(ScaffoldBraceDiameter(
                UnitUtils.ConvertFromInternalUnits(2 * standardRadius,
                    UnitTypeId.Millimeters))) / 2;
            double ledgerRadius = Math.Max(Mm(10), braceRadius * 0.72);
            XYZ lowerLeft = new XYZ(inset, 0, z0 + inset);
            XYZ lowerRight = new XYZ(width - inset, 0, z0 + inset);
            XYZ upperLeft = new XYZ(inset, 0, z1 - inset);
            XYZ upperRight = new XYZ(width - inset, 0, z1 - inset);
            if (upperLeft.Z <= lowerLeft.Z + Mm(100)) return;

            AddFreeForm(family, CylinderBetween(lowerLeft, lowerRight, ledgerRadius));
            AddFreeForm(family, CylinderBetween(upperLeft, upperRight, ledgerRadius));

            switch (ScaffoldSystemLayoutPolicy.NormalizeSingle(system))
            {
                case ScaffoldSystemType.Frame:
                    // Fabricated H-frame with opposed cross braces and inner ladder.
                    AddFreeForm(family, CylinderBetween(lowerLeft, upperRight, braceRadius));
                    AddFreeForm(family, CylinderBetween(upperLeft, lowerRight, braceRadius));
                    if (width > Mm(600))
                    {
                        double innerOffset = Math.Min(Mm(300), Math.Max(Mm(170), width * 0.20));
                        double innerLeft = innerOffset;
                        double innerRight = width - innerOffset;
                        double innerBottom = z0 + (z1 - z0) * 0.35;
                        AddFreeForm(family, CylinderBetween(
                            new XYZ(innerLeft, 0, innerBottom),
                            new XYZ(innerLeft, 0, upperLeft.Z), braceRadius));
                        AddFreeForm(family, CylinderBetween(
                            new XYZ(innerRight, 0, innerBottom),
                            new XYZ(innerRight, 0, upperRight.Z), braceRadius));
                        AddFreeForm(family, CylinderBetween(
                            new XYZ(innerLeft, 0, z0 + (z1 - z0) * 0.68),
                            new XYZ(innerRight, 0, z0 + (z1 - z0) * 0.68),
                            ledgerRadius));
                    }
                    break;

                case ScaffoldSystemType.Wedge:
                    // Wedge-lock bay: X bracing plus a mid ledger and rectangular
                    // wedge/cup nodes, with no fabricated H-frame inner uprights.
                    AddFreeForm(family, CylinderBetween(lowerLeft, upperRight, braceRadius));
                    AddFreeForm(family, CylinderBetween(upperLeft, lowerRight, braceRadius));
                    double wedgeMidZ = (z0 + z1) / 2;
                    AddFreeForm(family, CylinderBetween(new XYZ(0, 0, wedgeMidZ),
                        new XYZ(width, 0, wedgeMidZ), ledgerRadius));
                    foreach (double x in new[] { 0.0, width })
                    foreach (double z in new[] { lowerLeft.Z, wedgeMidZ, upperLeft.Z })
                        AddScaffoldNodeHardware(family, system, x, z, standardRadius);
                    break;

                default:
                    // Ringlock bay: alternating single diagonal, three ledgers and
                    // unmistakable circular rosettes at every connection level.
                    AddFreeForm(family, tierIndex % 2 == 0
                        ? CylinderBetween(lowerLeft, upperRight, braceRadius)
                        : CylinderBetween(upperLeft, lowerRight, braceRadius));
                    double ringMidZ = (z0 + z1) / 2;
                    AddFreeForm(family, CylinderBetween(new XYZ(0, 0, ringMidZ),
                        new XYZ(width, 0, ringMidZ), ledgerRadius));
                    foreach (double x in new[] { 0.0, width })
                    foreach (double z in new[] { lowerLeft.Z, ringMidZ, upperLeft.Z })
                        AddScaffoldNodeHardware(family, system, x, z, standardRadius);
                    break;
            }
        }

        private static void AddScaffoldNodeHardware(Document family,
            ScaffoldSystemType system, double x, double z, double standardRadius)
        {
            switch (ScaffoldSystemLayoutPolicy.NormalizeSingle(system))
            {
                case ScaffoldSystemType.Frame:
                    AddFreeForm(family, CylinderAlongAxis(
                        new XYZ(x, -Mm(32), z), XYZ.BasisY, Mm(64),
                        standardRadius + Mm(5)));
                    break;
                case ScaffoldSystemType.Wedge:
                    double cup = standardRadius + Mm(17);
                    AddFreeForm(family, Box(x - cup, -cup, z - Mm(12),
                        2 * cup, 2 * cup, Mm(24)));
                    AddFreeForm(family, Box(x - Mm(8), -cup - Mm(25), z - Mm(30),
                        Mm(16), Mm(28), Mm(60)));
                    break;
                default:
                    AddFreeForm(family, CylinderAlongAxis(
                        new XYZ(x, 0, z - Mm(6)), XYZ.BasisZ, Mm(12),
                        standardRadius + Mm(34)));
                    AddFreeForm(family, CylinderAlongAxis(
                        new XYZ(x, -Mm(45), z), XYZ.BasisY, Mm(90), Mm(6)));
                    break;
            }
        }

        private static void AddScaffoldHead(Document family, double x,
            double height, double plate)
        {
            double thickness = Mm(10);
            AddFreeForm(family, Box(x - plate / 2, -plate / 2,
                height - thickness, plate, plate, thickness));
            double cheekHeight = Mm(85), cheekThickness = Mm(10);
            AddFreeForm(family, Box(x - plate / 2, -plate / 2, height,
                cheekThickness, plate, cheekHeight));
            AddFreeForm(family, Box(x + plate / 2 - cheekThickness,
                -plate / 2, height, cheekThickness, plate, cheekHeight));
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
            // The ordinary beam clamp ends at the real beam top.  Surface-fixed
            // tie rods for edge beams are generated by their own family/layout and
            // are the only members intentionally shown above that surface.
            double walerHeight = beamDepth + Mm(80);
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

        private static void BuildPushPullFamily(Document family, double lengthMm,
            NativeAccessoryFamilyDimensionPolicy.PushPullProfile profile)
        {
            double length = Mm(Math.Max(700, lengthMm));
            double plate = Mm(profile.PlateSizeMm);
            double plateThickness = Mm(Math.Max(8.0, profile.WallThicknessMm * 2.0));
            double outerRadius = Mm(profile.OuterDiameterMm / 2.0);
            double innerRadius = Mm(profile.InnerStageDiameterMm / 2.0);
            double endClearance = Mm(Math.Max(75.0, profile.OuterDiameterMm * 1.58));
            double overlap = Mm(Math.Max(160.0, profile.OuterDiameterMm * 3.65));
            double middle = length / 2.0;

            // Structural Framing families are inserted about the midpoint of their
            // location curve.  Model in a temporary 0..L coordinate system, then
            // translate every solid by -L/2 before it enters the family document.
            // Leaving these solids at 0..L shifts the entire brace by half its
            // length, which was the source of the green members projecting beyond
            // roofs and through neighbouring bays.
            AddCenteredBraceForm(family, Box(-plateThickness / 2, -plate / 2, -plate / 2,
                plateThickness, plate, plate), length);
            AddCenteredBraceForm(family, Box(length - plateThickness / 2, -plate / 2, -plate / 2,
                plateThickness, plate, plate), length);

            // Two telescoping tube stages overlap around the middle instead of one
            // solid rectangular bar. This preserves the adjustable construction
            // shown in the reference family while remaining lightweight in previews.
            double outerStart = endClearance;
            double outerEnd = Math.Min(length - endClearance, middle + overlap / 2);
            double innerStart = Math.Max(endClearance, middle - overlap / 2);
            double innerEnd = length - endClearance;
            AddCenteredBraceForm(family, CylinderAlongAxis(new XYZ(outerStart, 0, 0), XYZ.BasisX,
                Math.Max(Mm(150), outerEnd - outerStart), outerRadius), length);
            AddCenteredBraceForm(family, CylinderAlongAxis(new XYZ(innerStart, 0, 0), XYZ.BasisX,
                Math.Max(Mm(150), innerEnd - innerStart), innerRadius), length);

            // Central adjustment sleeve and collars make the adjustable zone
            // visible without generating costly thread helices on every brace.
            double sleeveLength = Math.Min(
                Mm(Math.Max(140.0, profile.OuterDiameterMm * 3.0)), length * 0.18);
            AddCenteredBraceForm(family, CylinderAlongAxis(new XYZ(middle - sleeveLength / 2, 0, 0),
                XYZ.BasisX, sleeveLength, Mm(profile.SleeveDiameterMm / 2.0)), length);
            double collarLength = Mm(Math.Max(12.0, profile.OuterDiameterMm * 0.30));
            foreach (double x in new[]
                     {
                         middle - sleeveLength / 2 - collarLength,
                         middle + sleeveLength / 2
                     })
                AddCenteredBraceForm(family, CylinderAlongAxis(new XYZ(x, 0, 0), XYZ.BasisX,
                    collarLength, Mm(profile.CollarDiameterMm / 2.0)), length);

            AddBraceEndHardware(family, 0, true, endClearance, plate, length, profile);
            AddBraceEndHardware(family, length, false, endClearance, plate, length, profile);
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
            AddCenteredBraceForm(family, CylinderAlongAxis(XYZ.Zero, XYZ.BasisX,
                Math.Max(Mm(80), bodyStart - Mm(70)), cableRadius), length);
            AddCenteredBraceForm(family, CylinderAlongAxis(new XYZ(bodyEnd + Mm(70), 0, 0), XYZ.BasisX,
                Math.Max(Mm(80), length - bodyEnd - Mm(70)), cableRadius), length);
            AddCenteredBraceForm(family, CylinderAlongAxis(new XYZ(bodyStart - Mm(90), 0, 0), XYZ.BasisX,
                Mm(150), threadedRadius), length);
            AddCenteredBraceForm(family, CylinderAlongAxis(new XYZ(bodyEnd - Mm(60), 0, 0), XYZ.BasisX,
                Mm(150), threadedRadius), length);

            AddCenteredBraceForm(family, CylinderAlongAxis(new XYZ(bodyStart, -frameOffset, 0), XYZ.BasisX,
                bodyLength, frameRadius), length);
            AddCenteredBraceForm(family, CylinderAlongAxis(new XYZ(bodyStart, frameOffset, 0), XYZ.BasisX,
                bodyLength, frameRadius), length);
            foreach (double x in new[] { bodyStart, bodyEnd - Mm(24) })
            {
                AddCenteredBraceForm(family, CylinderAlongAxis(new XYZ(x, -frameOffset, 0), XYZ.BasisY,
                    2 * frameOffset, Mm(18)), length);
                AddCenteredBraceForm(family, CylinderAlongAxis(new XYZ(x, 0, -frameOffset), XYZ.BasisZ,
                    2 * frameOffset, Mm(18)), length);
            }

            // Short collars indicate the adjustable threaded zones without an
            // expensive helix, keeping hundreds of preview instances responsive.
            double pitch = Mm(15);
            for (int index = 0; index < 5; index++)
            {
                double leftX = bodyStart - Mm(75) + index * pitch;
                double rightX = bodyEnd + Mm(10) + index * pitch;
                AddCenteredBraceForm(family, CylinderAlongAxis(new XYZ(leftX, 0, 0), XYZ.BasisX,
                    Mm(3), threadedRadius + Mm(1.5)), length);
                AddCenteredBraceForm(family, CylinderAlongAxis(new XYZ(rightX, 0, 0), XYZ.BasisX,
                    Mm(3), threadedRadius + Mm(1.5)), length);
            }

            AddEyeRing(family, Mm(42), Mm(33), Mm(7), length);
            AddEyeRing(family, length - Mm(42), Mm(33), Mm(7), length);
        }

        private static void AddEyeRing(Document family, double centerX, double radius,
            double tubeRadius, double totalLength)
        {
            const int segments = 12;
            for (int index = 0; index < segments; index++)
            {
                double a0 = 2 * Math.PI * index / segments;
                double a1 = 2 * Math.PI * (index + 1) / segments;
                XYZ start = new XYZ(centerX + radius * Math.Cos(a0), radius * Math.Sin(a0), 0);
                XYZ end = new XYZ(centerX + radius * Math.Cos(a1), radius * Math.Sin(a1), 0);
                AddCenteredBraceForm(family, CylinderBetween(start, end, tubeRadius), totalLength);
            }
        }

        private static void AddBraceEndHardware(Document family, double endpointX, bool startEnd,
            double endClearance, double plate, double totalLength,
            NativeAccessoryFamilyDimensionPolicy.PushPullProfile profile)
        {
            double direction = startEnd ? 1 : -1;
            double couplerLength = Mm(Math.Max(55.0, profile.OuterDiameterMm + 5.0));
            double couplerRadius = Mm(profile.CouplerDiameterMm / 2.0);
            double couplerStart = endpointX + direction * (endClearance - couplerLength);
            if (!startEnd) couplerStart -= couplerLength;
            AddCenteredBraceForm(family, CylinderAlongAxis(new XYZ(couplerStart, 0, 0), XYZ.BasisX,
                couplerLength, couplerRadius), totalLength);

            // Fork plates and a transverse pivot pin at each end reproduce the
            // editable connection hardware visible in the tutorial family.
            double lugLength = Mm(Math.Max(55.0, profile.OuterDiameterMm + 10.0));
            double lugThickness = Mm(Math.Max(8.0, profile.WallThicknessMm * 2.5));
            double lugHeight = Mm(Math.Max(45.0, profile.OuterDiameterMm));
            double lugX = startEnd ? endpointX : endpointX - lugLength;
            double lugOffset = Mm(Math.Max(22.0, profile.OuterDiameterMm * 0.50));
            AddCenteredBraceForm(family, Box(lugX, -lugOffset - lugThickness, -lugHeight / 2,
                lugLength, lugThickness, lugHeight), totalLength);
            AddCenteredBraceForm(family, Box(lugX, lugOffset, -lugHeight / 2,
                lugLength, lugThickness, lugHeight), totalLength);
            double pinLength = Math.Min(plate * 0.72, Mm(90));
            AddCenteredBraceForm(family, CylinderAlongAxis(new XYZ(endpointX, -pinLength / 2, 0),
                XYZ.BasisY, pinLength, Mm(Math.Max(7.0, profile.WallThicknessMm * 2.0))), totalLength);
        }

        private static void AddCenteredBraceForm(Document family, Solid solid, double totalLength)
        {
            Transform centerAtFamilyOrigin = Transform.CreateTranslation(
                -XYZ.BasisX * (totalLength / 2.0));
            AddFreeForm(family, SolidUtils.CreateTransformed(solid, centerAtFamilyOrigin));
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

        private static XYZ HorizontalDirection(XYZ direction)
        {
            var horizontal = new XYZ(direction.X, direction.Y, 0);
            return horizontal.GetLength() > 1e-8 ? horizontal.Normalize() : XYZ.BasisX;
        }

        private static double BeamTopAtStation(Curve curve, BoundingBoxXYZ box, XYZ stationPoint)
        {
            XYZ start = curve.GetEndPoint(0);
            XYZ end = curve.GetEndPoint(1);
            double referenceMaximumZ = Math.Max(start.Z, end.Z);
            return stationPoint.Z + (box.Max.Z - referenceMaximumZ);
        }

        private static double BeamBottomAtStation(Curve curve, BoundingBoxXYZ box, XYZ stationPoint)
        {
            XYZ start = curve.GetEndPoint(0);
            XYZ end = curve.GetEndPoint(1);
            double referenceMinimumZ = Math.Min(start.Z, end.Z);
            return stationPoint.Z + (box.Min.Z - referenceMinimumZ);
        }

        private static IEnumerable<double> Grid(double min, double max, double spacing)
        {
            double length = max - min;
            int count = Math.Max(1, (int)Math.Ceiling(length / spacing));
            for (int index = 0; index <= count; index++)
                yield return min + length * index / count;
        }

        private static IEnumerable<double> PairedGrid(double min, double max, double spacing)
        {
            double length = max - min;
            int segmentCount = Math.Max(1, (int)Math.Ceiling(length / spacing));
            // An odd segment count produces an even node count. Pairing nodes 0-1,
            // 2-3, ... then covers the complete plate without overlapping standards.
            if ((segmentCount & 1) == 0) segmentCount++;
            for (int index = 0; index <= segmentCount; index++)
                yield return min + length * index / segmentCount;
        }

        private FamilyInstance NewInstance(FamilySymbol symbol, XYZ origin) =>
            _document.Create.NewFamilyInstance(origin, symbol, StructuralType.NonStructural);

        private bool TryNewUniqueProp(FamilySymbol symbol, XYZ origin, out FamilyInstance instance)
        {
            long Q(double internalValue) => (long)Math.Round(UnitUtils.ConvertFromInternalUnits(
                internalValue, UnitTypeId.Millimeters));
            var key = (Q(origin.X), Q(origin.Y), Q(origin.Z));
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

        private Level NearestLevel(double elevation)
        {
            if (_levels == null)
                _levels = new FilteredElementCollector(_document).OfClass(typeof(Level)).Cast<Level>()
                    .ToList();
            return _levels.OrderBy(item => Math.Abs(item.Elevation - elevation)).FirstOrDefault();
        }

        private static void Mark(FamilyInstance instance, View3D view, OverrideGraphicSettings graphics)
        {
            Parameter comments = instance.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            if (comments != null && !comments.IsReadOnly) comments.Set(InstanceMarker);
            view.SetElementOverrides(instance.Id, graphics);
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
        private static double ScaffoldBraceDiameter(double propDiameterMm) =>
            Round(Math.Max(20, Math.Min(27, Math.Max(20, propDiameterMm) * 0.45)), 0.1);
        private static string N(double value) => value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', '_');
        private static string ProfileNumber(double value) => value.ToString("0.###",
            System.Globalization.CultureInfo.InvariantCulture).Replace('.', '_');
        private static string PropKey(double diameter, double height) =>
            $"{FamilyPrefix}CAY_CHONG_HOAN_CHINH_V3_D{N(diameter)}_H{N(height)}";
        private static string ScaffoldStandardKey(ScaffoldSystemType system,
            double diameter, double height) =>
            $"{FamilyPrefix}GIAN_GIAO_{ScaffoldSystemLayoutPolicy.FamilyToken(system)}_STANDARD_V1_" +
            $"D{N(diameter)}_H{N(height)}";

        private static string ScaffoldBraceKey(ScaffoldSystemType system,
            double bayWidth, double clearHeight, double moduleHeight,
            int moduleCount, double diameter) =>
            $"{FamilyPrefix}GIAN_GIAO_{ScaffoldSystemLayoutPolicy.FamilyToken(system)}_BAY_V1_" +
            $"W{N(bayWidth)}_CH{N(clearHeight)}_MH{N(moduleHeight)}_N{Math.Max(1, moduleCount)}_" +
            $"PD{N(diameter)}_BD{N(ScaffoldBraceDiameter(diameter))}";
        private static string BeamClampKey(double beamWidth, double beamDepth, double walerWidth,
            double walerDepth, double tieDiameter) =>
            $"{FamilyPrefix}GONG_TY_DAM_V2_B{N(beamWidth)}_H{N(beamDepth)}_W{N(walerWidth)}x{N(walerDepth)}_D{N(tieDiameter)}";
        private static string YokeKey(double width, double depth, double barWidth, double barHeight,
            int pairCount) =>
            $"{FamilyPrefix}GONG_COT_V3_{N(width)}x{N(depth)}_{N(barWidth)}x{N(barHeight)}_P{pairCount}";
        private static string TieRodKey(double diameter, double length) =>
            $"{FamilyPrefix}TY_BAT_CHUON_LINE_V1_D{N(diameter)}_L{N(length)}";
        // V8 also removes the rectangular sample extrusion inherited from Revit's
        // Structural Framing template. The version bump prevents cached/project V7
        // definitions from retaining that visible hitbox around the real prop.
        private static string PushPullKey(double length,
            NativeAccessoryFamilyDimensionPolicy.PushPullProfile profile) =>
            $"{FamilyPrefix}CHONG_XIEN_FRAME_V8_D{ProfileNumber(profile.OuterDiameterMm)}x{ProfileNumber(profile.WallThicknessMm)}_L{N(length)}";
        private static string BeamSideBraceKey(double length,
            NativeAccessoryFamilyDimensionPolicy.PushPullProfile profile) =>
            $"{FamilyPrefix}CHONG_XIEN_DAM_BIEN_FRAME_V1_D{ProfileNumber(profile.OuterDiameterMm)}x{ProfileNumber(profile.WallThicknessMm)}_L{N(length)}";
        private static string TurnbuckleKey(double length) =>
            // V4 is regenerated after removing the Structural Framing template body,
            // preventing the same inherited rectangular prism around cable hardware.
            $"{FamilyPrefix}CAP_TANG_DO_FRAME_V4_L{N(length)}";

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
