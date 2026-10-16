using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Autodesk.Revit.DB;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Services;
using HelixToolkit.Wpf.SharpDX;
using HelixToolkit.SharpDX;
using Point3D = System.Windows.Media.Media3D.Point3D;
using Vector3D = System.Windows.Media.Media3D.Vector3D;
using MediaColor = System.Windows.Media.Color;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels
{
    /// <summary>
    /// Builds one project-coordinate preview containing every structural storey.
    /// Revit solids are tessellated for an accurate concrete model; formwork is
    /// generated around each element at its actual model elevation.
    /// </summary>
    internal sealed class ProjectFormworkPreviewBuilder
    {
        private sealed class FootprintTriangle
        {
            public Point3D A { get; set; }
            public Point3D B { get; set; }
            public Point3D C { get; set; }

            public bool Contains(double x, double y)
            {
                double denominator = (B.Y - C.Y) * (A.X - C.X) + (C.X - B.X) * (A.Y - C.Y);
                if (Math.Abs(denominator) < 1e-8) return false;
                double u = ((B.Y - C.Y) * (x - C.X) + (C.X - B.X) * (y - C.Y)) / denominator;
                double v = ((C.Y - A.Y) * (x - C.X) + (A.X - C.X) * (y - C.Y)) / denominator;
                double w = 1.0 - u - v;
                const double tolerance = -1e-7;
                return u >= tolerance && v >= tolerance && w >= tolerance;
            }
        }

        private sealed class SupportFootprint
        {
            public long SourceElementId { get; set; } = -1;
            public long LevelId { get; set; } = -1;
            public double MinX { get; set; }
            public double MaxX { get; set; }
            public double MinY { get; set; }
            public double MaxY { get; set; }
            public double TopZ { get; set; }
            public List<FootprintTriangle> TopTriangles { get; } = new List<FootprintTriangle>();
            public List<List<Point3D>> OuterLoops { get; } = new List<List<Point3D>>();

            public bool ContainsOverall(Point3D point, double tolerance = 0.0)
            {
                if (OuterLoops.Count > 0) return OuterLoops.Any(loop => PointInPolygon(point, loop));
                return point.X >= MinX - tolerance && point.X <= MaxX + tolerance &&
                       point.Y >= MinY - tolerance && point.Y <= MaxY + tolerance;
            }

            private static bool PointInPolygon(Point3D point, List<Point3D> polygon)
            {
                bool inside = false;
                for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
                {
                    Point3D a = polygon[i];
                    Point3D b = polygon[j];
                    if ((a.Y > point.Y) != (b.Y > point.Y) &&
                        point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                        inside = !inside;
                }
                return inside;
            }

            public bool Contains(Point3D point, double clearance)
            {
                bool ContainsExact(double x, double y)
                {
                    if (x < MinX || x > MaxX || y < MinY || y > MaxY) return false;
                    if (TopTriangles.Count > 0)
                        return TopTriangles.Any(triangle => triangle.Contains(x, y));
                    if (OuterLoops.Count > 0)
                        return OuterLoops.Any(loop => PointInPolygon(new Point3D(x, y, point.Z), loop));
                    // A bounding box is not proof of a bearing surface for irregular,
                    // stepped or opening-cut floors. Fail closed when face geometry is
                    // unavailable so no prop can be drawn into empty space.
                    return false;
                }

                if (!ContainsExact(point.X, point.Y)) return false;
                if (clearance <= 0) return true;
                return ContainsExact(point.X + clearance, point.Y) &&
                       ContainsExact(point.X - clearance, point.Y) &&
                       ContainsExact(point.X, point.Y + clearance) &&
                       ContainsExact(point.X, point.Y - clearance);
            }
        }

        private sealed class BraceCandidate
        {
            public Vector3D Direction { get; set; }
            public double FaceDistance { get; set; }
            public double Run { get; set; }
        }

        private sealed class ColumnLocationInfo
        {
            public long ElementId { get; set; }
            public long LevelId { get; set; }
            public Point3D Center { get; set; }
            public double BottomZ { get; set; }
            public double TopZ { get; set; }
        }

        private sealed class ColumnStoreySegment
        {
            public ElementId LevelId { get; set; }
            public double BottomZ { get; set; }
            public double TopZ { get; set; }
            public double Height => TopZ - BottomZ;
        }

        private const double MmPerFoot = 304.8;
        private readonly SharedDataViewModel _data;
        private readonly Document _document;
        private Point3D _origin;
        internal Point3D CoordinateOriginMm => _origin;
        private List<Level> _levels;
        private Options _geometryOptions;
        private bool _compactLod;
        private int _columnPlatformCount;
        private int _constrainedBraceCount;
        private int _interiorColumnCount;
        private int _perimeterColumnCount;
        private int _cornerColumnCount;
        private int _cableRestraintCount;
        private int _warningColumnCount;
        private int _openingColumnCount;
        private int _foundationColumnCount;
        private int _renderedUnsupportedPropCount;
        private Models.CalculationParameters _activeColumnDesign;
        private readonly List<SupportFootprint> _supportFootprints = new List<SupportFootprint>();
        private readonly List<SupportFootprint> _foundationFootprints = new List<SupportFootprint>();
        private readonly List<ColumnLocationInfo> _columnLocations = new List<ColumnLocationInfo>();
        private SupportFootprint _groundWorkingFootprint;

        private readonly SimpleMeshBuilder _floorConcrete = new SimpleMeshBuilder();
        private readonly SimpleMeshBuilder _beamConcrete = new SimpleMeshBuilder();
        private readonly SimpleMeshBuilder _columnConcrete = new SimpleMeshBuilder();
        private readonly SimpleMeshBuilder _foundationConcrete = new SimpleMeshBuilder();
        private readonly SimpleMeshBuilder _plywood = new SimpleMeshBuilder();
        private readonly SimpleMeshBuilder _timber = new SimpleMeshBuilder();
        private readonly SimpleMeshBuilder _steel = new SimpleMeshBuilder();
        private readonly SimpleMeshBuilder _bracing = new SimpleMeshBuilder();

        public ProjectFormworkPreviewBuilder(SharedDataViewModel data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _document = data.RevitDoc ?? throw new InvalidOperationException("Chưa có tài liệu Revit đang hoạt động.");
        }

        public void RefreshColumnBraceAssessmentsOnly()
        {
            var categories = new List<BuiltInCategory>
            {
                BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_StructuralFraming,
                BuiltInCategory.OST_StructuralColumns,
                BuiltInCategory.OST_Columns,
                BuiltInCategory.OST_StructuralFoundation
            };
            List<Element> elements = new FilteredElementCollector(_document)
                .WherePasses(new ElementMulticategoryFilter(categories))
                .WhereElementIsNotElementType()
                .ToElements()
                .Where(element => element?.Category != null && element.get_BoundingBox(null) != null)
                .ToList();
            if (elements.Count == 0) return;

            _data.BeginColumnBraceAssessmentRefresh();
            SetCoordinateOrigin(elements);
            _levels = new FilteredElementCollector(_document)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(level => level.Elevation)
                .ToList();
            BuildColumnLocationCache(elements);

            foreach (Element element in elements.Where(candidate =>
                         candidate.Category.BuiltInCategory == BuiltInCategory.OST_StructuralColumns ||
                         candidate.Category.BuiltInCategory == BuiltInCategory.OST_Columns))
            {
                BoundingBoxXYZ box = element.get_BoundingBox(null);
                if (box == null) continue;
                double zBottom = Z(box.Min.Z), zTop = Z(box.Max.Z);
                GetColumnLocalAxes(element, out Vector3D xAxis, out Vector3D yAxis);
                double width = Math.Max(100, GetFamilyDimension(element,
                    new[] { "b", "Width", "B", "Chiều rộng" }, (box.Max.X - box.Min.X) * MmPerFoot));
                double depth = Math.Max(100, GetFamilyDimension(element,
                    new[] { "h", "Depth", "H", "Chiều sâu" }, (box.Max.Y - box.Min.Y) * MmPerFoot));
                Point3D center = element.Location is LocationPoint locationPoint
                    ? new Point3D(X(locationPoint.Point.X), Y(locationPoint.Point.Y), (zBottom + zTop) / 2)
                    : new Point3D((X(box.Min.X) + X(box.Max.X)) / 2,
                        (Y(box.Min.Y) + Y(box.Max.Y)) / 2, (zBottom + zTop) / 2);

                Models.FloorElementModel component = (_data.FloorElements ?? new List<Models.FloorElementModel>())
                    .FirstOrDefault(model => model != null &&
                                             (string.Equals(model.UniqueId, element.UniqueId,
                                                  StringComparison.OrdinalIgnoreCase) ||
                                              model.ElementId == element.Id.Value.ToString()));
                Models.CalculationParameters columnDesign = component == null
                    ? null
                    : _data.GetComponentFormworkDesign(component.UniqueId)?.Parameters;
                double angle = Math.Max(30, Math.Min(75,
                    Positive(columnDesign?.ColumnBraceAngle ?? _data.ColumnBraceAngle, 55))) *
                               Math.PI / 180.0;

                foreach (ColumnStoreySegment segment in BuildColumnStoreySegments(element, zBottom, zTop))
                {
                    double attachZ = segment.BottomZ + segment.Height * 0.85;
                    double braceRun = Math.Max(600, Math.Min(3500,
                        (attachZ - segment.BottomZ) / Math.Tan(angle)));
                    _data.StoreColumnBraceAssessment(AssessColumnBracing(element, segment.LevelId, center,
                        segment.BottomZ, segment.Height, width, depth, xAxis, yAxis, braceRun));
                }
            }
        }

        public GroupModel3D Build(out string summary)
        {
            var categories = new List<BuiltInCategory>
            {
                BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_StructuralFraming,
                BuiltInCategory.OST_StructuralColumns,
                BuiltInCategory.OST_Columns,
                BuiltInCategory.OST_StructuralFoundation
            };

            List<Element> elements = new FilteredElementCollector(_document)
                .WherePasses(new ElementMulticategoryFilter(categories))
                .WhereElementIsNotElementType()
                .ToElements()
                .Where(e => e?.Category != null && e.get_BoundingBox(null) != null)
                .ToList();

            if (elements.Count == 0)
            {
                summary = "Không tìm thấy sàn, dầm, cột hoặc móng trong mô hình Revit.";
                return new GroupModel3D();
            }

            _data.BeginColumnBraceAssessmentRefresh();
            SetCoordinateOrigin(elements);
            _compactLod = elements.Count >= 180;
            _geometryOptions = new Options
            {
                // Medium is visually indistinguishable for structural members in an
                // overview, but avoids fine-detail family meshes that stall WPF 3D.
                DetailLevel = ViewDetailLevel.Medium,
                ComputeReferences = false,
                IncludeNonVisibleObjects = false
            };
            _levels = new FilteredElementCollector(_document)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(level => level.Elevation)
                .ToList();
            BuildColumnLocationCache(elements);

            int floors = 0, beams = 0, columns = 0, foundations = 0;
            foreach (Element element in elements)
            {
                BoundingBoxXYZ box = element.get_BoundingBox(null);
                if (box == null) continue;

                BuiltInCategory category = element.Category.BuiltInCategory;
                bool foundationSlab = IsFoundationSlab(element);

                if (category == BuiltInCategory.OST_Floors && !foundationSlab)
                {
                    AddElementGeometry(element, _floorConcrete);
                    AddFloorFormwork(element, box);
                    floors++;
                }
                else if (category == BuiltInCategory.OST_StructuralFraming)
                {
                    AddElementGeometry(element, _beamConcrete);
                    AddBeamFormwork(element, box);
                    beams++;
                }
                else if (category == BuiltInCategory.OST_StructuralColumns ||
                         category == BuiltInCategory.OST_Columns)
                {
                    AddElementGeometry(element, _columnConcrete);
                    AddColumnFormwork(element, box);
                    columns++;
                }
                else if (category == BuiltInCategory.OST_StructuralFoundation || foundationSlab)
                {
                    AddElementGeometry(element, _foundationConcrete);
                    AddFoundationFormwork(element, box);
                    foundations++;
                }
            }

            var group = new GroupModel3D();
            // Opaque materials stay on WPF's fast depth-buffered path. Transparent
            // concrete forced expensive per-frame sorting for the entire building.
            AddModel(group, _floorConcrete, MediaColor.FromRgb(185, 195, 205));
            AddModel(group, _beamConcrete, MediaColor.FromRgb(150, 165, 180));
            AddModel(group, _columnConcrete, MediaColor.FromRgb(130, 145, 160));
            AddModel(group, _foundationConcrete, MediaColor.FromRgb(115, 125, 140));
            AddModel(group, _plywood, MediaColor.FromRgb(222, 181, 112));
            AddModel(group, _timber, MediaColor.FromRgb(139, 94, 52));
            AddModel(group, _steel, MediaColor.FromRgb(55, 112, 172));
            AddModel(group, _bracing, MediaColor.FromRgb(235, 176, 24));

            int stories = elements
                .Select(GetElementLevelId)
                .Where(id => id != null && id != ElementId.InvalidElementId)
                .Select(id => id.Value)
                .Distinct()
                .Count();

            int triangles = group.Children
                .OfType<MeshGeometryModel3D>()
                .Select(model => model.Geometry as HelixToolkit.SharpDX.MeshGeometry3D)
                .Where(mesh => mesh != null)
                .Sum(mesh => mesh.TriangleIndices.Count / 3);
            string lod = _compactLod ? "LOD tối ưu" : "LOD chuẩn";
            string platformSummary = _data.ColumnUseWorkPlatform
                ? $" · {_columnPlatformCount}/{columns} sàn thao tác đại diện"
                : string.Empty;
            string boundarySummary = _data.ColumnBracePlacementModeIndex > 0 && _constrainedBraceCount > 0
                ? $" · {_constrainedBraceCount} hướng chân chống hở đã chặn (không dựng ra ngoài)"
                : string.Empty;
            string footSafetySummary = _renderedUnsupportedPropCount == 0
                ? " · kiểm tra chân chống: 0 chân kê vào khoảng không"
                : $" · {_renderedUnsupportedPropCount} vị trí chân chống không có mặt đỡ đã được loại";
            string restraintSummary = _data.ColumnBracePlacementModeIndex > 0
                ? $" · phân đoạn cột trong/biên/góc {_interiorColumnCount}/{_perimeterColumnCount}/{_cornerColumnCount} · {_cableRestraintCount} cáp-tăng đơ"
                : string.Empty;
            if (_openingColumnCount > 0 || _warningColumnCount > 0 || _foundationColumnCount > 0)
                restraintSummary += $" · lỗ mở/cảnh báo/cột móng {_openingColumnCount}/{_warningColumnCount}/{_foundationColumnCount}";
            summary = $"Toàn bộ dự án: {Math.Max(stories, 1)} tầng · {floors} sàn · {beams} dầm · {columns} cột · {foundations} móng · {triangles:N0} tam giác ({lod}){platformSummary}{boundarySummary}{restraintSummary}{footSafetySummary}";

            return group;
        }

        private void SetCoordinateOrigin(IEnumerable<Element> elements)
        {
            // A builder can be reused by the calculation and preview pipelines.  Never
            // let footprints from an earlier refresh participate in a new bearing check.
            _supportFootprints.Clear();
            _foundationFootprints.Clear();
            _groundWorkingFootprint = null;

            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            foreach (Element element in elements)
            {
                BoundingBoxXYZ box = element.get_BoundingBox(null);
                if (box == null) continue;
                minX = Math.Min(minX, box.Min.X * MmPerFoot);
                minY = Math.Min(minY, box.Min.Y * MmPerFoot);
                minZ = Math.Min(minZ, box.Min.Z * MmPerFoot);
            }
            _origin = new Point3D(minX, minY, minZ);

            // Keep every slab footprint separate. A single project-wide union would
            // misclassify columns in stepped/tiered buildings.
            foreach (Element element in elements.Where(e =>
                         e.Category.BuiltInCategory == BuiltInCategory.OST_Floors))
            {
                BoundingBoxXYZ box = element.get_BoundingBox(null);
                if (box == null) continue;
                _supportFootprints.Add(CreateSupportFootprint(element, box));
            }

            foreach (Element foundation in elements.Where(e =>
                         e.Category.BuiltInCategory == BuiltInCategory.OST_StructuralFoundation))
            {
                BoundingBoxXYZ box = foundation.get_BoundingBox(null);
                if (box != null) _foundationFootprints.Add(CreateSupportFootprint(foundation, box));
            }

            // Include grade beams at or below the lowest floor as foundation
            // support, without confusing upper-storey framing with ground support.
            double lowestFloorTop = _supportFootprints.Count > 0
                ? _supportFootprints.Min(footprint => footprint.TopZ)
                : double.PositiveInfinity;
            foreach (Element gradeBeam in elements.Where(e =>
                         e.Category.BuiltInCategory == BuiltInCategory.OST_StructuralFraming))
            {
                BoundingBoxXYZ box = gradeBeam.get_BoundingBox(null);
                if (box == null) continue;
                if (Z(box.Max.Z) <= lowestFloorTop + 500.0)
                    _foundationFootprints.Add(CreateSupportFootprint(gradeBeam, box));
            }

            _groundWorkingFootprint = BuildGroundWorkingFootprint();
        }

        private SupportFootprint BuildGroundWorkingFootprint()
        {
            var points = new List<Point3D>();
            foreach (SupportFootprint footprint in _foundationFootprints)
            {
                if (footprint.OuterLoops.Count > 0)
                {
                    foreach (List<Point3D> loop in footprint.OuterLoops)
                        points.AddRange(loop.Select(point => new Point3D(point.X, point.Y, footprint.TopZ)));
                }
                else
                {
                    points.Add(new Point3D(footprint.MinX, footprint.MinY, footprint.TopZ));
                    points.Add(new Point3D(footprint.MinX, footprint.MaxY, footprint.TopZ));
                    points.Add(new Point3D(footprint.MaxX, footprint.MinY, footprint.TopZ));
                    points.Add(new Point3D(footprint.MaxX, footprint.MaxY, footprint.TopZ));
                }
            }
            if (points.Count < 3) return null;

            List<Point3D> hull = BuildConvexHull(points);
            if (hull.Count < 3) return null;
            var result = new SupportFootprint
            {
                MinX = hull.Min(point => point.X),
                MaxX = hull.Max(point => point.X),
                MinY = hull.Min(point => point.Y),
                MaxY = hull.Max(point => point.Y),
                TopZ = _foundationFootprints.Max(footprint => footprint.TopZ)
            };
            result.OuterLoops.Add(hull);
            return result;
        }

        private static List<Point3D> BuildConvexHull(IEnumerable<Point3D> source)
        {
            List<Point3D> points = source
                .GroupBy(point => $"{Math.Round(point.X, 3)}:{Math.Round(point.Y, 3)}")
                .Select(group => group.First())
                .OrderBy(point => point.X)
                .ThenBy(point => point.Y)
                .ToList();
            if (points.Count <= 3) return points;

            double Cross(Point3D origin, Point3D a, Point3D b) =>
                (a.X - origin.X) * (b.Y - origin.Y) - (a.Y - origin.Y) * (b.X - origin.X);

            var lower = new List<Point3D>();
            foreach (Point3D point in points)
            {
                while (lower.Count >= 2 && Cross(lower[lower.Count - 2], lower[lower.Count - 1], point) <= 0)
                    lower.RemoveAt(lower.Count - 1);
                lower.Add(point);
            }
            var upper = new List<Point3D>();
            for (int index = points.Count - 1; index >= 0; index--)
            {
                Point3D point = points[index];
                while (upper.Count >= 2 && Cross(upper[upper.Count - 2], upper[upper.Count - 1], point) <= 0)
                    upper.RemoveAt(upper.Count - 1);
                upper.Add(point);
            }
            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            lower.AddRange(upper);
            return lower;
        }

        private void BuildColumnLocationCache(IEnumerable<Element> elements)
        {
            _columnLocations.Clear();
            foreach (Element column in elements.Where(e =>
                         e.Category.BuiltInCategory == BuiltInCategory.OST_StructuralColumns ||
                         e.Category.BuiltInCategory == BuiltInCategory.OST_Columns))
            {
                BoundingBoxXYZ box = column.get_BoundingBox(null);
                if (box == null) continue;
                Point3D center = column.Location is LocationPoint location
                    ? new Point3D(X(location.Point.X), Y(location.Point.Y), (Z(box.Min.Z) + Z(box.Max.Z)) / 2.0)
                    : new Point3D((X(box.Min.X) + X(box.Max.X)) / 2.0,
                        (Y(box.Min.Y) + Y(box.Max.Y)) / 2.0,
                        (Z(box.Min.Z) + Z(box.Max.Z)) / 2.0);
                foreach (ColumnStoreySegment segment in BuildColumnStoreySegments(column, Z(box.Min.Z), Z(box.Max.Z)))
                {
                    ElementId levelId = segment.LevelId;
                    _columnLocations.Add(new ColumnLocationInfo
                    {
                        ElementId = column.Id.Value,
                        LevelId = levelId != null && levelId != ElementId.InvalidElementId ? levelId.Value : -1,
                        Center = new Point3D(center.X, center.Y, (segment.BottomZ + segment.TopZ) / 2.0),
                        BottomZ = segment.BottomZ,
                        TopZ = segment.TopZ
                    });
                }
            }
        }

        private SupportFootprint CreateSupportFootprint(Element floor, BoundingBoxXYZ box)
        {
            var footprint = new SupportFootprint
            {
                SourceElementId = floor.Id.Value,
                LevelId = GetElementLevelId(floor) is ElementId supportLevelId &&
                          supportLevelId != ElementId.InvalidElementId ? supportLevelId.Value : -1,
                MinX = X(box.Min.X),
                MaxX = X(box.Max.X),
                MinY = Y(box.Min.Y),
                MaxY = Y(box.Max.Y),
                TopZ = Z(box.Max.Z)
            };

            try
            {
                var options = new Options
                {
                    DetailLevel = ViewDetailLevel.Coarse,
                    ComputeReferences = false,
                    IncludeNonVisibleObjects = false
                };
                GeometryElement geometry = floor.get_Geometry(options);
                if (geometry == null) return footprint;

                foreach (GeometryObject geometryObject in geometry)
                {
                    if (!(geometryObject is Solid solid) || solid.Volume <= 1e-9) continue;
                    foreach (Face face in solid.Faces)
                    {
                        Autodesk.Revit.DB.Mesh mesh;
                        try { mesh = face.Triangulate(); }
                        catch { continue; }
                        bool isTopFace = face is PlanarFace planarFace && planarFace.FaceNormal.Z > 0.8;
                        if (isTopFace && mesh.NumTriangles > 0)
                        {
                            MeshTriangle sample = mesh.get_Triangle(0);
                            double sampleZ = (ToPoint(sample.get_Vertex(0)).Z +
                                              ToPoint(sample.get_Vertex(1)).Z +
                                              ToPoint(sample.get_Vertex(2)).Z) / 3.0;
                            if (Math.Abs(sampleZ - footprint.TopZ) <= 80.0)
                            {
                                var loops = new List<List<Point3D>>();
                                foreach (EdgeArray edgeLoop in face.EdgeLoops)
                                {
                                    var polygon = new List<Point3D>();
                                    foreach (Edge edge in edgeLoop)
                                    {
                                        foreach (XYZ vertex in edge.Tessellate())
                                        {
                                            Point3D projected = ToPoint(vertex);
                                            if (polygon.Count == 0 || (projected - polygon[polygon.Count - 1]).Length > 1.0)
                                                polygon.Add(projected);
                                        }
                                    }
                                    if (polygon.Count >= 3) loops.Add(polygon);
                                }
                                List<Point3D> outer = loops
                                    .OrderByDescending(loop => Math.Abs(PolygonArea(loop)))
                                    .FirstOrDefault();
                                if (outer != null) footprint.OuterLoops.Add(outer);
                            }
                        }
                        for (int index = 0; index < mesh.NumTriangles; index++)
                        {
                            MeshTriangle triangle = mesh.get_Triangle(index);
                            Point3D a = ToPoint(triangle.get_Vertex(0));
                            Point3D b = ToPoint(triangle.get_Vertex(1));
                            Point3D c = ToPoint(triangle.get_Vertex(2));
                            Vector3D normal = Vector3D.CrossProduct(b - a, c - a);
                            if (normal.Length < 1e-8) continue;
                            normal.Normalize();
                            if (normal.Z <= 0.5) continue;
                            double averageZ = (a.Z + b.Z + c.Z) / 3.0;
                            if (Math.Abs(averageZ - footprint.TopZ) > 80.0) continue;
                            footprint.TopTriangles.Add(new FootprintTriangle { A = a, B = b, C = c });
                        }
                    }
                }
            }
            catch
            {
                // Keep the bounding box only for outer-outline diagnostics. Contains()
                // fails closed when no verified top-face triangles/loops are available.
            }
            return footprint;
        }

        private static double PolygonArea(IList<Point3D> polygon)
        {
            double area = 0.0;
            for (int index = 0; index < polygon.Count; index++)
            {
                Point3D current = polygon[index];
                Point3D next = polygon[(index + 1) % polygon.Count];
                area += current.X * next.Y - next.X * current.Y;
            }
            return area / 2.0;
        }

        private void AddElementGeometry(Element element, SimpleMeshBuilder target)
        {
            GeometryElement geometry = element.get_Geometry(_geometryOptions);
            if (geometry != null) AddGeometryObjects(geometry, target);
        }

        private void AddGeometryObjects(IEnumerable<GeometryObject> objects, SimpleMeshBuilder target)
        {
            foreach (GeometryObject geometryObject in objects)
            {
                if (geometryObject is Solid solid && solid.Volume > 1e-9)
                {
                    foreach (Face face in solid.Faces)
                    {
                        Autodesk.Revit.DB.Mesh mesh;
                        try { mesh = face.Triangulate(); }
                        catch { continue; }

                        for (int i = 0; i < mesh.NumTriangles; i++)
                        {
                            MeshTriangle triangle = mesh.get_Triangle(i);
                            target.AddTriangle(
                                ToPoint(triangle.get_Vertex(0)),
                                ToPoint(triangle.get_Vertex(1)),
                                ToPoint(triangle.get_Vertex(2)));
                        }
                    }
                }
                else if (geometryObject is GeometryInstance instance)
                {
                    GeometryElement instanceGeometry = instance.GetInstanceGeometry();
                    if (instanceGeometry != null) AddGeometryObjects(instanceGeometry, target);
                }
            }
        }

        private void AddFloorFormwork(Element element, BoundingBoxXYZ box)
        {
            Models.CalculationParameters design = _data.GetComponentFormworkDesign(element.UniqueId)?.Parameters;
            Models.ComponentIntersectionInfo intersections = _data.GetComponentIntersectionInfo(element.UniqueId);
            double minX = X(box.Min.X), maxX = X(box.Max.X);
            double minY = Y(box.Min.Y), maxY = Y(box.Max.Y);
            double slabBottom = Z(box.Min.Z);
            double width = Math.Max(50, maxX - minX);
            double depth = Math.Max(50, maxY - minY);
            double plywoodThickness = Positive(design?.VanMatThickness ?? _data.VanMatThickness, 18);
            double secondaryW = Positive(design?.XaGoPhuWidth ?? _data.XaGoPhuWidth, 50);
            double secondaryH = Positive(design?.XaGoPhuHeight ?? _data.XaGoPhuHeight, 100);
            double primaryW = Positive(design?.XaGoChinhWidth ?? _data.XaGoChinhWidth, 100);
            double primaryH = Positive(design?.XaGoChinhHeight ?? _data.XaGoChinhHeight, 100);
            double secondarySpacing = Positive(design?.Joist1Spacing ?? _data.Joist1Spacing, 400);
            double primarySpacing = Positive(design?.Joist2Spacing ?? _data.Joist2Spacing, 1000);
            double supportSpacing = Positive(design?.SupportSpacing ?? _data.SupportSpacing, 1000);
            List<Models.FormworkCutoutRectangle> localCutouts = GetLocalPlanCutouts(intersections?.PlanCutouts);
            SupportFootprint supportedSlab = _supportFootprints.FirstOrDefault(footprint =>
                footprint.SourceElementId == element.Id.Value);

            double plywoodBottom = slabBottom - plywoodThickness;
            AddPlanSurfaceWithCutouts(minX, maxX, minY, maxY,
                plywoodBottom + plywoodThickness / 2, plywoodThickness,
                intersections?.PlanCutouts);

            double secondaryBottom = plywoodBottom - secondaryH;
            foreach (double x in Positions(minX, maxX, secondarySpacing, _compactLod ? 14 : 22))
            {
                var gaps = localCutouts.Where(c => x >= c.MinXmm - secondaryW / 2 && x <= c.MaxXmm + secondaryW / 2)
                    .Select(c => new Models.FormworkLengthInterval { StartMm = c.MinYmm - minY, EndMm = c.MaxYmm - minY });
                foreach (Models.FormworkLengthInterval segment in ComplementIntervals(depth, gaps))
                    _timber.AddBox(new Point3D(x, minY + (segment.StartMm + segment.EndMm) / 2,
                            secondaryBottom + secondaryH / 2), secondaryW, segment.LengthMm, secondaryH);
            }

            double primaryBottom = secondaryBottom - primaryH;
            List<double> primaryYs = Positions(minY, maxY, primarySpacing, _compactLod ? 8 : 14);
            foreach (double y in primaryYs)
            {
                var gaps = localCutouts.Where(c => y >= c.MinYmm - primaryW / 2 && y <= c.MaxYmm + primaryW / 2)
                    .Select(c => new Models.FormworkLengthInterval { StartMm = c.MinXmm - minX, EndMm = c.MaxXmm - minX });
                foreach (Models.FormworkLengthInterval segment in ComplementIntervals(width, gaps))
                    _timber.AddBox(new Point3D(minX + (segment.StartMm + segment.EndMm) / 2, y,
                            primaryBottom + primaryH / 2), segment.LengthMm, primaryW, primaryH);
            }

            // Ground-floor and foundation-level members keep their formwork layers but
            // do not receive an elevated prop/scaffold system. The user-selected ground
            // floor is the common cutoff used by calculation, takeoff and native preview.
            if (!_data.ShouldPlaceElevatedShoring(element, Models.StructuralComponentType.Slab)) return;

            double supportTop = primaryBottom;

            double propDiameter = Positive(design?.CayChongDiameter ?? _data.CayChongDiameter, 48);
            foreach (double x in Positions(minX, maxX, supportSpacing, _compactLod ? 7 : 11))
            {
                foreach (double y in primaryYs)
                {
                    if (localCutouts.Any(c => x > c.MinXmm - 0.1 && x < c.MaxXmm + 0.1 &&
                                                  y > c.MinYmm - 0.1 && y < c.MaxYmm + 0.1)) continue;
                    Point3D station = new Point3D(x, y, supportTop);
                    if (supportedSlab != null && !supportedSlab.Contains(station, 50.0)) continue;
                    if (!TryFindSupportElevationAt(station, supportTop, 75.0, out double supportBottom))
                    {
                        _renderedUnsupportedPropCount++;
                        continue;
                    }

                    double postHeight = supportTop - supportBottom;
                    if (postHeight < 500.0)
                    {
                        _renderedUnsupportedPropCount++;
                        continue;
                    }
                    // Draw the complete bearing interval. The previous 100 mm offsets at
                    // both ends made the post visibly float even when the selected support
                    // elevation happened to be correct.
                    _steel.AddBox(new Point3D(x, y, supportBottom + postHeight / 2),
                        propDiameter, propDiameter, postHeight);
                }
            }
        }

        private void AddBeamFormwork(Element element, BoundingBoxXYZ box)
        {
            Models.CalculationParameters design = _data.GetComponentFormworkDesign(element.UniqueId)?.Parameters;
            Models.ComponentIntersectionInfo intersections = _data.GetComponentIntersectionInfo(element.UniqueId);
            Point3D start;
            Point3D end;
            if (element.Location is LocationCurve location && location.Curve != null)
            {
                start = ToPoint(location.Curve.GetEndPoint(0));
                end = ToPoint(location.Curve.GetEndPoint(1));
            }
            else
            {
                start = new Point3D(X(box.Min.X), (Y(box.Min.Y) + Y(box.Max.Y)) / 2, Z(box.Min.Z));
                end = new Point3D(X(box.Max.X), (Y(box.Min.Y) + Y(box.Max.Y)) / 2, Z(box.Min.Z));
            }

            Vector3D axis = end - start;
            axis.Z = 0;
            double length = axis.Length;
            if (length < 10) return;
            axis.Normalize();
            Vector3D cross = new Vector3D(-axis.Y, axis.X, 0);
            Vector3D up = new Vector3D(0, 0, 1);

            double beamWidth = GetFamilyDimension(element, new[] { "b", "Width", "B", "Chiều rộng" }, GetPerpendicularBoxWidth(box, cross));
            double beamDepth = GetFamilyDimension(element, new[] { "h", "Height", "Depth", "H", "Chiều cao" }, (box.Max.Z - box.Min.Z) * MmPerFoot);
            beamWidth = Math.Max(100, beamWidth);
            beamDepth = Math.Max(100, beamDepth);

            double bottomZ = Z(box.Min.Z);
            double centerZ = bottomZ + beamDepth / 2;
            Point3D center = new Point3D((start.X + end.X) / 2, (start.Y + end.Y) / 2, centerZ);
            double bottomPly = Positive(design?.BeamBottomPlywoodThickness ?? _data.BeamBottomPlywoodThickness, 18);
            double sidePly = Positive(design?.BeamSidePlywoodThickness ?? _data.BeamSidePlywoodThickness, 18);

            List<Models.FormworkLengthInterval> clearBeamSegments = ComplementIntervals(length,
                intersections?.BeamColumnGaps);
            foreach (Models.FormworkLengthInterval segment in clearBeamSegments)
            {
                double segmentLength = segment.LengthMm;
                if (segmentLength < 1) continue;
                Point3D segmentCenter = start + axis * ((segment.StartMm + segment.EndMm) / 2.0);
                segmentCenter.Z = centerZ;
                _plywood.AddOrientedBox(new Point3D(segmentCenter.X, segmentCenter.Y, bottomZ - bottomPly / 2),
                    axis, cross, up, segmentLength, beamWidth + 2 * sidePly, bottomPly);
                _plywood.AddOrientedBox(segmentCenter + cross * (beamWidth / 2 + sidePly / 2),
                    axis, cross, up, segmentLength, sidePly, beamDepth);
                _plywood.AddOrientedBox(segmentCenter - cross * (beamWidth / 2 + sidePly / 2),
                    axis, cross, up, segmentLength, sidePly, beamDepth);
            }

            double yokeSpacing = Positive(design?.BeamBottomYokeSpacing ?? _data.BeamBottomYokeSpacing, 500);
            double joistWidth = Positive(design?.BeamSecondaryJoistWidth ?? _data.BeamSecondaryJoistWidth, 50);
            double joistDepth = Positive(design?.BeamSecondaryJoistDepth ?? _data.BeamSecondaryJoistDepth, 100);
            foreach (double distance in Distances(length, yokeSpacing, _compactLod ? 10 : 18))
            {
                if (!IsDistanceInClearSegment(distance, clearBeamSegments)) continue;
                Point3D point = start + axis * distance;
                _timber.AddOrientedBox(new Point3D(point.X, point.Y, bottomZ - bottomPly - joistDepth / 2), axis, cross, up, joistWidth, beamWidth + 300, joistDepth);
            }

            // Fixed surface rods belong only to a perimeter beam. Classify each station
            // from the actual floor footprint: slab on exactly one beam side means edge;
            // slab on both sides means an interior beam and no fixed surface rod.
            double beamTop = Z(box.Max.Z);
            List<SupportFootprint> beamTopSlabs = _supportFootprints
                .Where(floor => Math.Abs(floor.TopZ - beamTop) <= 250.0)
                .ToList();
            double surfaceTieSpacing = Positive(design?.BeamEdgeSurfaceTieRodSpacing ?? _data.BeamEdgeSurfaceTieRodSpacing, 1000.0);
            double surfaceTieDiameter = Positive(design?.BeamEdgeSurfaceTieRodDiameter ?? _data.BeamEdgeSurfaceTieRodDiameter, 12.0);
            double requestedSurfaceAnchor = Positive(design?.BeamEdgeSurfaceTieRodAnchorLength ?? _data.BeamEdgeSurfaceTieRodAnchorLength, 1200.0);
            foreach (double distance in Distances(length, surfaceTieSpacing, _compactLod ? 8 : 16))
            {
                if (!IsDistanceInClearSegment(distance, clearBeamSegments)) continue;
                Point3D station = start + axis * distance;
                double probeOffset = beamWidth / 2.0 + 250.0;
                Point3D plusProbe = station + cross * probeOffset;
                Point3D minusProbe = station - cross * probeOffset;
                SupportFootprint plusSlab = beamTopSlabs.FirstOrDefault(floor => floor.Contains(plusProbe, 40.0));
                SupportFootprint minusSlab = beamTopSlabs.FirstOrDefault(floor => floor.Contains(minusProbe, 40.0));
                if ((plusSlab == null) == (minusSlab == null)) continue;

                double interiorSign = plusSlab != null ? 1.0 : -1.0;
                SupportFootprint supportingSlab = plusSlab ?? minusSlab;
                double anchorLength = requestedSurfaceAnchor;
                Point3D anchorPoint = station + cross * (interiorSign * (beamWidth / 2.0 + anchorLength));
                while (anchorLength >= 500.0 && !supportingSlab.Contains(anchorPoint, 30.0))
                {
                    anchorLength -= 100.0;
                    anchorPoint = station + cross * (interiorSign * (beamWidth / 2.0 + anchorLength));
                }
                if (anchorLength < 500.0) continue;

                double exteriorSign = -interiorSign;
                Point3D rodStart = station + cross * (exteriorSign * (beamWidth / 2.0 + sidePly + 40.0));
                rodStart.Z = beamTop + surfaceTieDiameter / 2.0 + 8.0;
                anchorPoint.Z = rodStart.Z;
                _steel.AddCylinder(rodStart, anchorPoint, surfaceTieDiameter, 8);
                _steel.AddOrientedBox(anchorPoint, axis, cross, up, 120.0, 120.0, 10.0);
                AddWingNut(rodStart, axis, cross, up, surfaceTieDiameter);
            }

            if (_data.ShouldPlaceElevatedShoring(element, Models.StructuralComponentType.Beam))
            {
                double propSpacing = Positive(design?.BeamPropSpacing ?? _data.BeamPropSpacing, 1000);
                double propDiameter = Positive(design?.CayChongDiameter ?? _data.CayChongDiameter, 48);
                double primaryWalerDepth = Positive(
                    design?.BeamPrimaryWalerDepth ?? _data.BeamPrimaryWalerDepth, 100);
                double scaffoldBayWidth = Positive(
                    design?.BeamScaffoldBayWidth ?? _data.BeamScaffoldBayWidth, 600);
                double rowOffset = scaffoldBayWidth / 2.0;
                double supportTop = bottomZ - bottomPly - joistDepth - primaryWalerDepth;
                foreach (double distance in Distances(length, propSpacing, _compactLod ? 8 : 14))
                {
                    if (!IsDistanceInClearSegment(distance, clearBeamSegments)) continue;
                    Point3D centerStation = start + axis * distance;
                    // BeamScaffoldBayWidth is the checked span of the primary waler
                    // between two scaffold standards. BeamPropSpacing remains the
                    // longitudinal pitch of those transverse two-row frames.
                    foreach (double rowSign in new[] { -1.0, 1.0 })
                    {
                        Point3D point = centerStation + cross * (rowSign * rowOffset);
                        Point3D station = new Point3D(point.X, point.Y, supportTop);
                        if (!TryFindSupportElevationAt(station, supportTop, 75.0,
                                out double supportBottom))
                        {
                            _renderedUnsupportedPropCount++;
                            continue;
                        }

                        double postHeight = supportTop - supportBottom;
                        if (postHeight < 500.0)
                        {
                            _renderedUnsupportedPropCount++;
                            continue;
                        }
                        _steel.AddBox(new Point3D(point.X, point.Y,
                                supportBottom + postHeight / 2),
                            propDiameter, propDiameter, postHeight);
                    }
                }
            }

            // Beam-edge side-form restraint. Use the validated accessory section/count and
            // accept only feet that remain on the supporting floor footprint. This prevents
            // automatic design from calculating a brace that is absent from the 3D layout or
            // projects beyond a stepped/middle-plot boundary.
            if ((design?.BeamPlacementModeIndex ?? _data.BeamPlacementModeIndex) == 2)
            {
                double braceSpacing = Positive(design?.BeamSideBraceSpacing ?? _data.BeamSideBraceSpacing, 1000);
                double braceAngle = Math.Max(35, Math.Min(70, Positive(design?.BeamSideBraceAngle ?? _data.BeamSideBraceAngle, 55))) * Math.PI / 180.0;
                double braceDiameter = Positive(design?.BeamSideBraceDiameter ?? _data.BeamSideBraceDiameter, 48);
                int braceCount = Math.Max(1, design?.BeamSideBraceCountPerStation ?? _data.BeamSideBraceCountPerStation);
                double attachZ = bottomZ + beamDepth * 0.70;

                foreach (double distance in Distances(length, braceSpacing, _compactLod ? 6 : 12))
                {
                    if (!IsDistanceInClearSegment(distance, clearBeamSegments)) continue;
                    Point3D station = start + axis * distance;
                    double edgeProbeOffset = beamWidth / 2.0 + 250.0;
                    Point3D plusProbe = station + cross * edgeProbeOffset;
                    Point3D minusProbe = station - cross * edgeProbeOffset;
                    plusProbe.Z = beamTop;
                    minusProbe.Z = beamTop;
                    bool plusHasSlab = beamTopSlabs.Any(floor =>
                        floor.Contains(plusProbe, 40.0));
                    bool minusHasSlab = beamTopSlabs.Any(floor =>
                        floor.Contains(minusProbe, 40.0));

                    // A true edge station has slab on exactly one side. Stations with
                    // slab on both sides are middle beams; stations with no detected
                    // slab are unresolved and must not receive a guessed exterior brace.
                    if (plusHasSlab == minusHasSlab) continue;
                    double exteriorSign = plusHasSlab ? -1.0 : 1.0;

                    if (!TryFindSupportElevationAt(new Point3D(station.X, station.Y, attachZ),
                            attachZ, 50.0, out double referenceBearing))
                        referenceBearing = bottomZ - 1200.0;
                    double desiredRun = Math.Max(500,
                        (attachZ - (referenceBearing + 30.0)) / Math.Tan(braceAngle));
                    double sign = exteriorSign;
                    {
                        double supportedRun = 0;
                        double supportedElevation = double.NaN;
                        for (double run = desiredRun; run >= 250; run -= 100)
                        {
                            Point3D candidate = new Point3D(station.X, station.Y, attachZ) +
                                                cross * (sign * (beamWidth / 2 + run));
                            if (TryFindSupportElevationAt(candidate, attachZ, 50.0,
                                    out double candidateElevation))
                            {
                                supportedRun = run;
                                supportedElevation = candidateElevation;
                                break;
                            }
                        }
                        if (supportedRun <= 0) continue;

                        Point3D attach = new Point3D(station.X, station.Y, attachZ) + cross * (sign * (beamWidth / 2 + sidePly + 35));
                        Point3D foot = new Point3D(station.X, station.Y, supportedElevation + 30.0) +
                                       cross * (sign * (beamWidth / 2 + supportedRun));
                        bool renderedBrace = false;
                        Point3D renderedPlatePoint = foot;
                        for (int braceIndex = 0; braceIndex < braceCount; braceIndex++)
                        {
                            double longitudinalOffset = (braceIndex - (braceCount - 1) / 2.0) * (braceDiameter + 35);
                            Vector3D offset = axis * longitudinalOffset;
                            Point3D actualFootPlan = foot + offset;
                            if (!TryFindSupportElevationAt(actualFootPlan, attachZ, 50.0,
                                    out double actualBearing)) continue;
                            Point3D actualFoot = new Point3D(actualFootPlan.X, actualFootPlan.Y,
                                actualBearing + 30.0);
                            _bracing.AddCylinder(actualFoot, attach + offset, braceDiameter, 8);
                            renderedPlatePoint = actualFoot;
                            renderedBrace = true;
                        }
                        if (renderedBrace)
                            _steel.AddOrientedBox(renderedPlatePoint, axis, cross, up, 180, 140, 12);
                    }
                }
            }
        }

        private bool IsVerifiedSupportPoint(Point3D point, double supportElevation, double clearance)
        {
            if (_supportFootprints.Any(floor =>
                    Math.Abs(floor.TopZ - supportElevation) <= 450.0 && floor.Contains(point, clearance)))
                return true;
            if (_foundationFootprints.Any(foundation =>
                    Math.Abs(foundation.TopZ - supportElevation) <= 800.0 && foundation.Contains(point, clearance)))
                return true;
            return _groundWorkingFootprint != null &&
                   Math.Abs(_groundWorkingFootprint.TopZ - supportElevation) <= 1000.0 &&
                   _groundWorkingFootprint.Contains(point, clearance);
        }

        private void AddColumnFormwork(Element element, BoundingBoxXYZ box)
        {
            Models.CalculationParameters design = _data.GetComponentFormworkDesign(element.UniqueId)?.Parameters;
            Models.ComponentIntersectionInfo intersections = _data.GetComponentIntersectionInfo(element.UniqueId);
            _activeColumnDesign = design;
            double zBottom = Z(box.Min.Z), zTop = Z(box.Max.Z);
            double height = zTop - zBottom;
            if (height < 100) return;

            GetColumnLocalAxes(element, out Vector3D xAxis, out Vector3D yAxis);
            Vector3D up = new Vector3D(0, 0, 1);
            double width = GetFamilyDimension(element, new[] { "b", "Width", "B", "Chiều rộng" }, (box.Max.X - box.Min.X) * MmPerFoot);
            double depth = GetFamilyDimension(element, new[] { "h", "Depth", "H", "Chiều sâu" }, (box.Max.Y - box.Min.Y) * MmPerFoot);
            width = Math.Max(100, width);
            depth = Math.Max(100, depth);

            Point3D center;
            if (element.Location is LocationPoint locationPoint)
                center = new Point3D(X(locationPoint.Point.X), Y(locationPoint.Point.Y), (zBottom + zTop) / 2);
            else
                center = new Point3D((X(box.Min.X) + X(box.Max.X)) / 2, (Y(box.Min.Y) + Y(box.Max.Y)) / 2, (zBottom + zTop) / 2);

            double ply = Positive(design?.ColumnPlywoodThickness ?? _data.ColumnPlywoodThickness, 18);
            double[] reductions = intersections?.ColumnFaceTopReductionsMm ?? new double[4];
            AddColumnFacePanel(center, yAxis, width + 2 * ply, depth / 2 + ply / 2,
                height, reductions[2], ply, xAxis, yAxis, up);
            AddColumnFacePanel(center, -yAxis, width + 2 * ply, depth / 2 + ply / 2,
                height, reductions[3], ply, xAxis, yAxis, up);
            AddColumnFacePanel(center, xAxis, depth, width / 2 + ply / 2,
                height, reductions[0], ply, yAxis, xAxis, up);
            AddColumnFacePanel(center, -xAxis, depth, width / 2 + ply / 2,
                height, reductions[1], ply, yAxis, xAxis, up);

            // Nẹp đứng 40x40: truyền tải từ ván mặt tới các tầng gông.
            double stiffenerW = Positive(design?.ColumnStiffenerWidth ?? _data.ColumnStiffenerWidth, 40);
            double stiffenerD = Positive(design?.ColumnStiffenerDepth ?? _data.ColumnStiffenerDepth, 40);
            double stiffenerSpacing = Positive(design?.ColumnStiffenerSpacing ?? _data.ColumnStiffenerSpacing, 200);
            double stiffenerZ = (zBottom + zTop) / 2;
            foreach (double offset in CenteredPositions(width, stiffenerSpacing, _compactLod ? 6 : 14))
            {
                Point3D faceCenter = center + xAxis * offset;
                double positiveHeight = Math.Max(0, height - reductions[2]);
                double negativeHeight = Math.Max(0, height - reductions[3]);
                if (positiveHeight > 1)
                    _timber.AddOrientedBox(new Point3D(faceCenter.X, faceCenter.Y, stiffenerZ - reductions[2] / 2) + yAxis * (depth / 2 + ply + stiffenerD / 2), xAxis, yAxis, up, stiffenerW, stiffenerD, positiveHeight);
                if (negativeHeight > 1)
                    _timber.AddOrientedBox(new Point3D(faceCenter.X, faceCenter.Y, stiffenerZ - reductions[3] / 2) - yAxis * (depth / 2 + ply + stiffenerD / 2), xAxis, yAxis, up, stiffenerW, stiffenerD, negativeHeight);
            }
            foreach (double offset in CenteredPositions(depth, stiffenerSpacing, _compactLod ? 6 : 14))
            {
                Point3D faceCenter = center + yAxis * offset;
                double positiveHeight = Math.Max(0, height - reductions[0]);
                double negativeHeight = Math.Max(0, height - reductions[1]);
                if (positiveHeight > 1)
                    _timber.AddOrientedBox(new Point3D(faceCenter.X, faceCenter.Y, stiffenerZ - reductions[0] / 2) + xAxis * (width / 2 + ply + stiffenerD / 2), xAxis, yAxis, up, stiffenerD, stiffenerW, positiveHeight);
                if (negativeHeight > 1)
                    _timber.AddOrientedBox(new Point3D(faceCenter.X, faceCenter.Y, stiffenerZ - reductions[1] / 2) - xAxis * (width / 2 + ply + stiffenerD / 2), xAxis, yAxis, up, stiffenerD, stiffenerW, negativeHeight);
            }

            // Gông thép hộp đôi 40x80/50x100 và ty ren + bát chuồn.
            double yokeW = Positive(design?.ColumnYokeWidth ?? _data.ColumnYokeWidth, 40);
            double yokeH = Positive(design?.ColumnYokeHeight ?? _data.ColumnYokeHeight, 80);
            int yokePairCount = Math.Max(1, design?.ColumnYokePairCount ?? _data.ColumnYokePairCount);
            double yokeStandOff = ply + stiffenerD + yokeW / 2;
            double pairPitch = yokeH + 15;
            double tieDiameter = Positive(design?.ColumnTieRodDiameter ?? _data.ColumnTieRodDiameter, 12);
            int tieCount = Math.Max(1, design?.ColumnTieRodCountPerFace ?? _data.ColumnTieRodCountPerFace);
            var yokeParameters = new Models.CalculationParameters
            {
                ColumnHeight = height,
                ColumnYokeSpacing = design?.ColumnYokeSpacing ?? _data.ColumnYokeSpacing,
                ColumnUseZonedYokeSpacing = design?.ColumnUseZonedYokeSpacing ?? _data.ColumnUseZonedYokeSpacing,
                ColumnFirstYokeOffset = design?.ColumnFirstYokeOffset ?? _data.ColumnFirstYokeOffset,
                ColumnYokeBottomSpacing = design?.ColumnYokeBottomSpacing ?? _data.ColumnYokeBottomSpacing,
                ColumnYokeUpperSpacing = design?.ColumnYokeUpperSpacing ?? _data.ColumnYokeUpperSpacing,
                ColumnYokeTransitionHeight = design?.ColumnYokeTransitionHeight ?? _data.ColumnYokeTransitionHeight,
                ColumnLastYokeOffset = design?.ColumnLastYokeOffset ?? _data.ColumnLastYokeOffset
            };
            List<double> yokeElevations = Models.FormworkCalculationService.BuildColumnYokeElevations(yokeParameters);
            SimpleMeshBuilder yokeBuilder = (design?.ColumnYokeMaterialIndex ?? _data.ColumnYokeMaterialIndex) == 0 ? _timber : _steel;
            foreach (double relativeZ in yokeElevations)
            {
                double z = zBottom + relativeZ;
                for (int pair = 0; pair < yokePairCount; pair++)
                {
                    double pairOffset = (pair - (yokePairCount - 1) / 2.0) * pairPitch;
                    Point3D at = new Point3D(center.X, center.Y, z + pairOffset);
                    yokeBuilder.AddOrientedBox(at + yAxis * (depth / 2 + yokeStandOff), xAxis, yAxis, up, width + 2 * (ply + stiffenerD + yokeW + 120), yokeW, yokeH);
                    yokeBuilder.AddOrientedBox(at - yAxis * (depth / 2 + yokeStandOff), xAxis, yAxis, up, width + 2 * (ply + stiffenerD + yokeW + 120), yokeW, yokeH);
                    yokeBuilder.AddOrientedBox(at + xAxis * (width / 2 + yokeStandOff), xAxis, yAxis, up, yokeW, depth + 2 * (ply + stiffenerD + yokeW + 120), yokeH);
                    yokeBuilder.AddOrientedBox(at - xAxis * (width / 2 + yokeStandOff), xAxis, yAxis, up, yokeW, depth + 2 * (ply + stiffenerD + yokeW + 120), yokeH);
                }

                double rodZ = z;
                double rodX = width / 2 + ply + stiffenerD + yokeW + 80;
                double rodY = depth / 2 + ply + stiffenerD + yokeW + 80;
                Point3D rodCenter = new Point3D(center.X, center.Y, rodZ);
                foreach (double xOffset in TieOffsets(rodX, tieCount))
                {
                    Point3D tieCenter = rodCenter + xAxis * xOffset;
                    _steel.AddCylinder(tieCenter - yAxis * rodY, tieCenter + yAxis * rodY, tieDiameter, 6);
                    AddWingNut(tieCenter + yAxis * rodY, xAxis, yAxis, up, tieDiameter);
                    AddWingNut(tieCenter - yAxis * rodY, xAxis, yAxis, up, tieDiameter);
                }
                foreach (double yOffset in TieOffsets(rodY, tieCount))
                {
                    Point3D tieCenter = rodCenter + yAxis * yOffset;
                    _steel.AddCylinder(tieCenter - xAxis * rodX, tieCenter + xAxis * rodX, tieDiameter, 6);
                    AddWingNut(tieCenter + xAxis * rodX, yAxis, xAxis, up, tieDiameter);
                    AddWingNut(tieCenter - xAxis * rodX, yAxis, xAxis, up, tieDiameter);
                }
            }

            double angle = Math.Max(30, Math.Min(75, Positive(design?.ColumnBraceAngle ?? _data.ColumnBraceAngle, 55))) * Math.PI / 180.0;
            var availableCandidates = new List<BraceCandidate>();
            foreach (ColumnStoreySegment segment in BuildColumnStoreySegments(element, zBottom, zTop))
            {
                // Direction-first assessment: each storey segment gets its own
                // proposed foot points and its own (ColumnId, LevelId) cache entry.
                double attachZ = segment.BottomZ + segment.Height * 0.85;
                double braceRun = Math.Max(600, Math.Min(3500, (attachZ - segment.BottomZ) / Math.Tan(angle)));
                Models.ColumnBraceAssessment assessment = AssessColumnBracing(
                    element, segment.LevelId, center, segment.BottomZ, segment.Height,
                    width, depth, xAxis, yAxis, braceRun);
                // First store applies any existing per-key manual override; the second
                // store below persists the generated neighbour/anchor recommendation.
                _data.StoreColumnBraceAssessment(assessment);
                RenderAssessedColumnRestraints(assessment, center, segment.BottomZ, attachZ, segment.Height,
                    width, depth, ply, stiffenerD, xAxis, yAxis, up);
                _data.StoreColumnBraceAssessment(assessment);

                if (availableCandidates.Count == 0)
                {
                    availableCandidates.AddRange(assessment.Directions
                        .Where(direction => direction.CanPlaceProp)
                        .Select(direction => new BraceCandidate
                        {
                            Direction = direction.LocalDirection,
                            FaceDistance = FaceDistanceAlong(direction.LocalDirection, width, depth, xAxis, yAxis),
                            Run = braceRun
                        }));
                }
            }

            // A full scaffold around every column quickly dominates WPF's retained-mode
            // scene. Keep representative platforms visible while bounding mesh growth.
            int platformLimit = _compactLod ? 2 : 8;
            if ((design?.ColumnUseWorkPlatform ?? _data.ColumnUseWorkPlatform) && availableCandidates.Count > 0 && _columnPlatformCount < platformLimit)
            {
                _columnPlatformCount++;
                double platformWidth = Positive(design?.ColumnWorkPlatformWidth ?? _data.ColumnWorkPlatformWidth, 900);
                double deckZ = zBottom + Math.Min(height * 0.62, Math.Max(1600, height - 900));
                double deckDepth = platformWidth;
                Vector3D platformNormal = availableCandidates.Count > 0 ? availableCandidates[0].Direction : yAxis;
                bool normalAlongX = Math.Abs(Vector3D.DotProduct(platformNormal, xAxis)) > 0.5;
                Vector3D platformLongAxis = normalAlongX ? yAxis : xAxis;
                double platformFaceDistance = normalAlongX ? width / 2 : depth / 2;
                double columnLongDimension = normalAlongX ? depth : width;
                double nearDistance = platformFaceDistance + ply + 250;
                double farDistance = nearDistance + deckDepth;
                double halfLong = columnLongDimension / 2 + platformWidth;
                Point3D deckCenter = new Point3D(center.X, center.Y, deckZ) + platformNormal * (nearDistance + deckDepth / 2);
                _timber.AddOrientedBox(deckCenter, platformLongAxis, platformNormal, up, 2 * halfLong, deckDepth, 45);

                double railHeight = Positive(design?.ColumnGuardrailHeight ?? _data.ColumnGuardrailHeight, 1100);
                foreach (double longOffset in new[] { -halfLong, halfLong })
                {
                    Point3D postBaseNear = new Point3D(center.X, center.Y, deckZ) + platformLongAxis * longOffset + platformNormal * nearDistance;
                    Point3D postBaseFar = new Point3D(center.X, center.Y, deckZ) + platformLongAxis * longOffset + platformNormal * farDistance;
                    _steel.AddCylinder(new Point3D(postBaseNear.X, postBaseNear.Y, zBottom), postBaseNear + up * railHeight, 42, 6);
                    _steel.AddCylinder(new Point3D(postBaseFar.X, postBaseFar.Y, zBottom), postBaseFar + up * railHeight, 42, 6);
                }
                foreach (double railZ in new[] { deckZ + railHeight * 0.5, deckZ + railHeight })
                {
                    Point3D railCenter = new Point3D(center.X, center.Y, railZ) + platformNormal * farDistance;
                    _steel.AddOrientedBox(railCenter, platformLongAxis, platformNormal, up, 2 * halfLong, 35, 35);
                    _steel.AddOrientedBox(new Point3D(center.X, center.Y, railZ) + platformLongAxis * halfLong + platformNormal * (nearDistance + deckDepth / 2), platformLongAxis, platformNormal, up, 35, deckDepth, 35);
                    _steel.AddOrientedBox(new Point3D(center.X, center.Y, railZ) - platformLongAxis * halfLong + platformNormal * (nearDistance + deckDepth / 2), platformLongAxis, platformNormal, up, 35, deckDepth, 35);
                }
            }
        }

        private List<ColumnStoreySegment> BuildColumnStoreySegments(Element element, double zBottom, double zTop)
        {
            var supportLevels = (_levels ?? new List<Level>())
                .Select(level => new { Level = level, Z = level.Elevation * MmPerFoot - _origin.Z })
                .Where(item => item.Z >= zBottom - 200.0 && item.Z < zTop - 300.0)
                .OrderBy(item => item.Z)
                .ToList();

            if (supportLevels.Count == 0)
            {
                return new List<ColumnStoreySegment>
                {
                    new ColumnStoreySegment
                    {
                        LevelId = GetElementLevelId(element),
                        BottomZ = zBottom,
                        TopZ = zTop
                    }
                };
            }

            var segments = new List<ColumnStoreySegment>();
            for (int index = 0; index < supportLevels.Count; index++)
            {
                double bottom = index == 0 ? zBottom : Math.Max(zBottom, supportLevels[index].Z);
                double top = index + 1 < supportLevels.Count ? Math.Min(zTop, supportLevels[index + 1].Z) : zTop;
                if (top - bottom < 500.0) continue;
                segments.Add(new ColumnStoreySegment
                {
                    LevelId = supportLevels[index].Level.Id,
                    BottomZ = bottom,
                    TopZ = top
                });
            }
            return segments.Count > 0 ? segments : new List<ColumnStoreySegment>
            {
                new ColumnStoreySegment { LevelId = GetElementLevelId(element), BottomZ = zBottom, TopZ = zTop }
            };
        }

        private Models.ColumnBraceAssessment AssessColumnBracing(
            Element element, ElementId assessmentLevelId, Point3D center, double zBottom, double height, double width, double depth,
            Vector3D xAxis, Vector3D yAxis, double braceRun)
        {
            ElementId levelId = assessmentLevelId ?? GetElementLevelId(element);
            long levelValue = levelId != null && levelId != ElementId.InvalidElementId ? levelId.Value : -1;
            string levelName = levelId != null && levelId != ElementId.InvalidElementId
                ? (_document.GetElement(levelId) as Level)?.Name ?? "Unknown level"
                : "Unknown level";
            var assessment = new Models.ColumnBraceAssessment
            {
                ColumnId = element.Id.Value,
                LevelId = levelValue,
                LevelName = levelName,
                SupportElevationMm = zBottom,
                BraceRunMm = braceRun,
                ColumnWidthMm = width,
                ColumnDepthMm = depth,
                ColumnHeightMm = height
            };

            List<SupportFootprint> floorsAtElevation = _supportFootprints
                .Where(floor => Math.Abs(floor.TopZ - zBottom) <= 450.0)
                .ToList();
            // Revit LevelId is metadata, not a physical bearing boundary.  Split slabs,
            // offset slabs and duplicated structural levels commonly share one actual
            // construction elevation while carrying different LevelIds.  Keep every
            // co-elevated slab and use a matching LevelId only as a tie-breaker.
            List<SupportFootprint> floorsAtSupport = floorsAtElevation
                .OrderBy(floor => levelValue >= 0 && floor.LevelId == levelValue ? 0 : 1)
                .ThenBy(floor => Math.Abs(floor.TopZ - zBottom))
                .ToList();
            Vector3D[] directions = { xAxis, -xAxis, yAxis, -yAxis };
            double attachmentRise = Math.Max(300.0, height * 0.85);
            double minimumRun = Math.Max(600.0,
                attachmentRise / Math.Tan(75.0 * Math.PI / 180.0));
            for (int index = 0; index < directions.Length; index++)
            {
                Vector3D direction = directions[index];
                double faceDistance = FaceDistanceAlong(direction, width, depth, xAxis, yAxis);
                Point3D foot = new Point3D(center.X, center.Y, zBottom + 60) + direction * (faceDistance + braceRun);
                SupportFootprint slabBearing = null;
                double resolvedRun = braceRun;
                // Do not reject a column merely because the single nominal-angle foot
                // lands just beyond an edge or inside an opening.  Search radially toward
                // the column down to the steepest permitted prop angle, always validating
                // the complete 200 mm base patch against real top-face geometry.
                for (double candidateRun = braceRun;
                     candidateRun >= minimumRun - 0.1;
                     candidateRun -= 100.0)
                {
                    Point3D candidate = new Point3D(center.X, center.Y, zBottom + 60) +
                                        direction * (faceDistance + candidateRun);
                    slabBearing = floorsAtSupport.FirstOrDefault(floor => floor.Contains(candidate, 100.0));
                    if (slabBearing == null) continue;
                    foot = candidate;
                    resolvedRun = candidateRun;
                    break;
                }
                bool onSlab = slabBearing != null;
                if (slabBearing != null)
                    foot.Z = slabBearing.TopZ + 60.0;
                bool insideOuterOutline = floorsAtSupport.Any(floor => floor.ContainsOverall(foot, 30.0));
                Models.BraceFootSupportKind supportKind = onSlab
                    ? Models.BraceFootSupportKind.Slab
                    : insideOuterOutline
                        ? Models.BraceFootSupportKind.InternalOpening
                        : Models.BraceFootSupportKind.ExteriorOpen;
                assessment.Directions.Add(new Models.BraceDirectionAssessment
                {
                    DirectionIndex = index,
                    LocalDirection = direction,
                    ProposedFootPoint = foot,
                    ResolvedRunMm = resolvedRun,
                    SupportKind = supportKind
                });
            }

            bool hasInternalOpening = assessment.Directions.Any(direction =>
                direction.SupportKind == Models.BraceFootSupportKind.InternalOpening);
            bool columnBaseOnFoundation = _foundationFootprints.Any(foundation =>
                Math.Abs(foundation.TopZ - zBottom) <= 800.0 && foundation.ContainsOverall(center, 250.0));

            if (columnBaseOnFoundation)
            {
                // A pile cap under the column centre does not prove that four remote prop
                // feet are supported. Validate every proposed foot against either the cap/
                // grade-beam geometry or the bounded compacted-ground working footprint.
                foreach (Models.BraceDirectionAssessment direction in assessment.Directions
                             .Where(direction => !direction.CanPlaceProp))
                {
                    SupportFootprint foundationBearing = _foundationFootprints
                        .Where(foundation => Math.Abs(foundation.TopZ - zBottom) <= 800.0 &&
                                             foundation.Contains(direction.ProposedFootPoint, 100.0))
                        .OrderBy(foundation => Math.Abs(foundation.TopZ - zBottom))
                        .FirstOrDefault();
                    bool onFoundationFoot = foundationBearing != null;
                    bool onBoundedGround = _groundWorkingFootprint != null &&
                                           _groundWorkingFootprint.Contains(direction.ProposedFootPoint, 100.0);
                    if (onFoundationFoot || onBoundedGround)
                    {
                        direction.SupportKind = Models.BraceFootSupportKind.FoundationOrGround;
                        Point3D supportedFoot = direction.ProposedFootPoint;
                        supportedFoot.Z = (foundationBearing ?? _groundWorkingFootprint).TopZ + 60.0;
                        direction.ProposedFootPoint = supportedFoot;
                    }
                }
                assessment.DerivedType = Models.ColumnBraceDerivedType.FoundationGround;
                int supportedGroundDirections = assessment.Directions.Count(direction => direction.CanPlaceProp);
                assessment.RecommendedAction = $"Use {supportedGroundDirections} verified ground props with sole plates; resolve remaining directions with same-level ties or inward anchors. No foot may cross the bounded ground working footprint.";
                return assessment;
            }

            int openCount = assessment.Directions.Count(direction => !direction.CanPlaceProp);

            if (hasInternalOpening)
            {
                assessment.DerivedType = Models.ColumnBraceDerivedType.InternalOpeningWarning;
                assessment.RecommendedAction = "Internal floor opening detected; do not auto-place props. Engineer a hanging or lower-storey support arrangement.";
                return assessment;
            }

            if (openCount == 0)
            {
                assessment.DerivedType = Models.ColumnBraceDerivedType.Interior;
                assessment.RecommendedAction = "Four symmetric push-pull props.";
            }
            else if (openCount == 1)
            {
                assessment.DerivedType = Models.ColumnBraceDerivedType.Edge;
                assessment.RecommendedAction = "Three slab-supported props plus a neighbour frame tie or a pre-installed perimeter-beam anchor.";
            }
            else if (openCount == 2)
            {
                List<Models.BraceDirectionAssessment> openDirections = assessment.Directions
                    .Where(direction => !direction.CanPlaceProp).ToList();
                double dot = Vector3D.DotProduct(openDirections[0].LocalDirection, openDirections[1].LocalDirection);
                if (dot < -0.9)
                {
                    assessment.DerivedType = Models.ColumnBraceDerivedType.OppositeOpenWarning;
                    assessment.RecommendedAction = "Two opposite open directions detected; engineer review required for a narrow strip/corridor condition.";
                }
                else
                {
                    assessment.DerivedType = Models.ColumnBraceDerivedType.Corner;
                    assessment.RecommendedAction = "Two slab-supported props plus triangulated ties to neighbours or two embedded anchors.";
                }
            }
            else if (openCount == 3)
            {
                assessment.DerivedType = Models.ColumnBraceDerivedType.IsolatedWarning;
                assessment.RecommendedAction = "Three open directions: no automatic bracing; engineer input is mandatory.";
            }
            else
            {
                assessment.DerivedType = Models.ColumnBraceDerivedType.NoSupportWarning;
                assessment.RecommendedAction = "No slab or foundation support found beneath the proposed feet; engineer input is mandatory.";
            }
            return assessment;
        }

        private void RenderAssessedColumnRestraints(
            Models.ColumnBraceAssessment assessment, Point3D center, double zBottom, double attachZ,
            double height, double width, double depth, double ply, double stiffenerDepth,
            Vector3D xAxis, Vector3D yAxis, Vector3D up)
        {
            // Manual overrides change the engineering label/report, never the geometric
            // permission to place a foot. Layout always follows the direction checks.
            Models.ColumnBraceDerivedType type = assessment.DerivedType;
            List<Models.BraceDirectionAssessment> valid = assessment.Directions
                .Where(direction => direction.CanPlaceProp).ToList();
            List<Models.BraceDirectionAssessment> open = assessment.Directions
                .Where(direction => !direction.CanPlaceProp).ToList();
            _constrainedBraceCount += open.Count;

            bool warningType = type == Models.ColumnBraceDerivedType.OppositeOpenWarning ||
                               type == Models.ColumnBraceDerivedType.IsolatedWarning ||
                               type == Models.ColumnBraceDerivedType.InternalOpeningWarning ||
                               type == Models.ColumnBraceDerivedType.NoSupportWarning;
            if (warningType && !assessment.IsManuallyOverridden)
            {
                if (type == Models.ColumnBraceDerivedType.InternalOpeningWarning) _openingColumnCount++;
                else _warningColumnCount++;
                // A warning on one face must not suppress props whose feet on the other
                // faces have already been proved.  Keep those physical restraints visible
                // and flag only the unresolved directions for engineering action.
                AddConfiguredPushPullProps(valid, center, zBottom, attachZ, width, depth, ply,
                    stiffenerDepth, xAxis, yAxis, up, false);
                assessment.RecommendedAction += valid.Count > 0
                    ? $" {valid.Count} verified support direction(s) were generated; unresolved directions still require an inward anchor/tie or engineer override."
                    : " No verified support direction was found; an engineer override is required.";
                return;
            }

            if (warningType && assessment.IsManuallyOverridden)
            {
                AddConfiguredPushPullProps(valid, center, zBottom, attachZ, width, depth, ply,
                    stiffenerDepth, xAxis, yAxis, up, false);
                foreach (Models.BraceDirectionAssessment direction in open)
                    ResolveOpenDirection(assessment, direction, center, zBottom, attachZ, height,
                        width, depth, ply, xAxis, yAxis, up, false);
                assessment.RecommendedAction += " Engineer override applied with geometry-verified feet and inward restraints only.";
                return;
            }

            if (type == Models.ColumnBraceDerivedType.Interior)
            {
                _interiorColumnCount++;
                AddConfiguredPushPullProps(valid, center, zBottom, attachZ, width, depth, ply,
                    stiffenerDepth, xAxis, yAxis, up, false);
                return;
            }

            if (type == Models.ColumnBraceDerivedType.FoundationGround)
            {
                _foundationColumnCount++;
                AddConfiguredPushPullProps(valid, center, zBottom, attachZ, width, depth, ply,
                    stiffenerDepth, xAxis, yAxis, up, true);
                foreach (Models.BraceDirectionAssessment direction in open)
                    ResolveOpenDirection(assessment, direction, center, zBottom, attachZ, height,
                        width, depth, ply, xAxis, yAxis, up, true);
                return;
            }

            if (type == Models.ColumnBraceDerivedType.Edge)
            {
                _perimeterColumnCount++;
                AddConfiguredPushPullProps(valid, center, zBottom, attachZ, width, depth, ply,
                    stiffenerDepth, xAxis, yAxis, up, false);

                Models.BraceDirectionAssessment openDirection = open.FirstOrDefault();
                ColumnLocationInfo neighbour = openDirection == null ? null :
                    FindNeighbour(assessment, center, openDirection.LocalDirection, true);
                if (neighbour != null)
                {
                    AddNeighbourFrameTie(center, neighbour.Center, zBottom, height, up);
                    if (openDirection != null) openDirection.WasResolvedByAlternativeRestraint = true;
                    assessment.RecommendedAction += " Nearby column found: horizontal triangulated frame tie generated.";
                }
                else if (openDirection != null)
                {
                    ResolveOpenDirection(assessment, openDirection, center, zBottom, attachZ, height,
                        width, depth, ply, xAxis, yAxis, up, false);
                }
                return;
            }

            if (type == Models.ColumnBraceDerivedType.Corner)
            {
                _cornerColumnCount++;
                AddConfiguredPushPullProps(valid, center, zBottom, attachZ, width, depth, ply,
                    stiffenerDepth, xAxis, yAxis, up, false);

                for (int index = 0; index < valid.Count; index++)
                {
                    Models.BraceDirectionAssessment validDirection = valid[index];
                    ColumnLocationInfo neighbour = FindNeighbour(assessment, center, validDirection.LocalDirection, false);
                    if (neighbour != null)
                    {
                        AddNeighbourDiagonalTie(center, neighbour.Center, zBottom, height, up);
                        if (open.Count > index) open[index].WasResolvedByAlternativeRestraint = true;
                    }
                    else
                    {
                        Vector3D openDirection = open.Count > index ? open[index].LocalDirection : -validDirection.LocalDirection;
                        Models.BraceDirectionAssessment unresolved = open.Count > index
                            ? open[index]
                            : new Models.BraceDirectionAssessment
                            {
                                LocalDirection = openDirection,
                                ProposedFootPoint = center + openDirection * assessment.BraceRunMm,
                                SupportKind = Models.BraceFootSupportKind.ExteriorOpen
                            };
                        ResolveOpenDirection(assessment, unresolved, center, zBottom, attachZ, height,
                            width, depth, ply, xAxis, yAxis, up, false);
                    }
                }
                return;
            }

            _warningColumnCount++;
            assessment.RecommendedAction += " No matching automatic layout branch was found; no restraint geometry was generated.";
        }

        private void AddConfiguredPushPullProps(
            IReadOnlyList<Models.BraceDirectionAssessment> validDirections,
            Point3D center, double zBottom, double attachZ, double width, double depth,
            double ply, double stiffenerDepth, Vector3D xAxis, Vector3D yAxis, Vector3D up,
            bool useGroundSolePlate)
        {
            if (validDirections == null || validDirections.Count == 0) return;

            // ColumnBraceCount is the total number of restraint assemblies, not a fixed
            // one-prop-per-face switch. Distribute the designed quantity only among the
            // directions whose proposed feet have verified bearing surfaces.
            int requestedCount = Math.Min(validDirections.Count * 2,
                Math.Max(validDirections.Count,
                    Math.Max(1, _activeColumnDesign?.ColumnBraceCount ?? _data.ColumnBraceCount)));
            int baseCount = requestedCount / validDirections.Count;
            int remainder = requestedCount % validDirections.Count;
            for (int directionIndex = 0; directionIndex < validDirections.Count; directionIndex++)
            {
                int countOnDirection = baseCount + (directionIndex < remainder ? 1 : 0);
                for (int propIndex = 0; propIndex < countOnDirection; propIndex++)
                {
                    // Keep parallel attachment plates within the usable column face.
                    double faceLength = validDirections[directionIndex].DirectionIndex < 2
                        ? depth
                        : width;
                    double halfOffset = countOnDirection <= 1
                        ? 0.0
                        : Math.Min(90.0, Math.Max(0.0, (faceLength - 80.0) / 2.0));
                    double tangentOffset = countOnDirection <= 1
                        ? 0.0
                        : -halfOffset + 2.0 * halfOffset * propIndex /
                          (countOnDirection - 1);
                    AddPushPullProp(center, zBottom, attachZ, width, depth, ply, stiffenerDepth,
                        xAxis, yAxis, up, validDirections[directionIndex], useGroundSolePlate,
                        tangentOffset);
                }
            }
        }

        private void AddPushPullProp(
            Point3D center, double zBottom, double attachZ, double width, double depth,
            double ply, double stiffenerDepth, Vector3D xAxis, Vector3D yAxis, Vector3D up,
            Models.BraceDirectionAssessment assessment, bool useGroundSolePlate,
            double tangentOffset = 0.0)
        {
            if (assessment == null || !assessment.CanPlaceProp)
            {
                _renderedUnsupportedPropCount++;
                return;
            }
            Vector3D direction = assessment.LocalDirection;
            direction.Normalize();
            Vector3D tangent = new Vector3D(-direction.Y, direction.X, 0);
            if (tangent.LengthSquared > 1e-8) tangent.Normalize();
            double faceDistance = FaceDistanceAlong(direction, width, depth, xAxis, yAxis);
            Point3D top = new Point3D(center.X, center.Y, attachZ) +
                          direction * (faceDistance + ply + stiffenerDepth + 40);
            Point3D foot = assessment.ProposedFootPoint;

            // A wider/doubled arrangement is accepted only when its actual displaced
            // foot remains on a verified slab/foundation/working-ground surface. Reduce
            // the separation progressively at tight edges; never draw a prop into air.
            double appliedOffset = tangentOffset;
            if (Math.Abs(appliedOffset) > 0.01)
            {
                bool foundSupportedOffset = false;
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    Point3D candidate = assessment.ProposedFootPoint + tangent * appliedOffset;
                    if (IsVerifiedSupportPoint(candidate, zBottom, 100.0))
                    {
                        foot = candidate;
                        foundSupportedOffset = true;
                        break;
                    }
                    appliedOffset *= 0.5;
                }
                if (!foundSupportedOffset)
                    appliedOffset = 0.0;
                top += tangent * appliedOffset;
            }
            // Keep the retained-mode preview on the same per-column accessory
            // section selected by the calculation/auto-design pipeline.  The old
            // fixed D42 shaft with a D60 sleeve made every load class look alike
            // and could disagree with both the native RFA and the takeoff.
            NativeAccessoryFamilyDimensionPolicy.PushPullProfile profile =
                NativeAccessoryFamilyDimensionPolicy.GetPushPullProfile(
                    _activeColumnDesign?.ColumnBraceAllowableCapacity ??
                    _data.ColumnBraceAllowableCapacity,
                    _activeColumnDesign?.ColumnBraceDiameter ?? _data.ColumnBraceDiameter,
                    _activeColumnDesign?.ColumnBraceThickness ?? _data.ColumnBraceThickness);
            double outerDiameter = profile.OuterDiameterMm;
            double wallThickness = profile.WallThicknessMm;
            double innerDiameter = profile.InnerStageDiameterMm;
            double sleeveDiameter = profile.SleeveDiameterMm;
            double collarDiameter = profile.CollarDiameterMm;
            Vector3D propAxis = top - foot;

            // Two overlapping telescoping stages plus the central adjustment
            // sleeve reproduce the physical profile without a costly hollow mesh.
            _steel.AddCylinder(foot, foot + propAxis * 0.57, outerDiameter, 8);
            _steel.AddCylinder(foot + propAxis * 0.43, top, innerDiameter, 8);
            _steel.AddCylinder(foot + propAxis * 0.44, foot + propAxis * 0.56,
                sleeveDiameter, 8);
            _steel.AddCylinder(foot + propAxis * 0.42, foot + propAxis * 0.44,
                collarDiameter, 8);
            _steel.AddCylinder(foot + propAxis * 0.56, foot + propAxis * 0.58,
                collarDiameter, 8);

            double basePlateWidth = Positive(
                _activeColumnDesign?.SupportBasePlateWidth ?? _data.SupportBasePlateWidth, 150);
            double basePlateDepth = Positive(
                _activeColumnDesign?.SupportBasePlateDepth ?? _data.SupportBasePlateDepth, 150);
            double basePlateThickness = Math.Max(8, wallThickness * 4);
            _steel.AddOrientedBox(foot, xAxis, yAxis, up,
                basePlateWidth, basePlateDepth, basePlateThickness);
            if (useGroundSolePlate)
                _timber.AddOrientedBox(new Point3D(foot.X, foot.Y, zBottom + 20), xAxis, yAxis, up, 500, 500, 40);
            assessment.WasRenderedAsProp = true;
        }

        private void ResolveOpenDirection(
            Models.ColumnBraceAssessment assessment, Models.BraceDirectionAssessment direction,
            Point3D center, double zBottom, double attachZ, double height,
            double width, double depth, double ply,
            Vector3D xAxis, Vector3D yAxis, Vector3D up, bool groundCase)
        {
            ColumnLocationInfo neighbour = FindNeighbour(assessment, center, direction.LocalDirection, false) ??
                                           FindNeighbour(assessment, center, direction.LocalDirection, true);
            if (neighbour != null)
            {
                AddNeighbourDiagonalTie(center, neighbour.Center, zBottom, height, up);
                direction.WasResolvedByAlternativeRestraint = true;
                return;
            }

            if (TryAddEmbeddedAnchorRestraint(center, zBottom, attachZ, width, depth, ply,
                    xAxis, yAxis, up, direction.LocalDirection, assessment.BraceRunMm, groundCase))
            {
                direction.WasResolvedByAlternativeRestraint = true;
                assessment.PreviousLevelAction = groundCase
                    ? "Prepare the shown inward ground/grade-beam anchor and sole-plate zone before erecting the column formwork."
                    : "Install the shown embedded rebar/bulldog anchor in the supporting perimeter beam before concrete placement.";
                return;
            }

            _warningColumnCount++;
            assessment.RecommendedAction += " No verified same-level neighbour or inward anchor point was found for one open direction; no prop was drawn in that direction.";
        }

        private ColumnLocationInfo FindNeighbour(
            Models.ColumnBraceAssessment assessment, Point3D center, Vector3D referenceDirection, bool perpendicular)
        {
            double threshold = Math.Max(1000.0,
                _activeColumnDesign?.ColumnNeighbourTieThreshold ?? _data.ColumnNeighbourTieThreshold);
            return _columnLocations
                .Where(column => column.ElementId != assessment.ColumnId && column.LevelId == assessment.LevelId)
                .Select(column =>
                {
                    Vector3D delta = column.Center - center;
                    double distance = Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
                    if (distance > 1e-6) delta.Normalize();
                    double alignment = Math.Abs(Vector3D.DotProduct(delta, referenceDirection));
                    return new { Column = column, Distance = distance, Alignment = alignment };
                })
                .Where(candidate => candidate.Distance <= threshold)
                .Where(candidate => perpendicular ? candidate.Alignment <= 0.35 : candidate.Alignment >= 0.75)
                .OrderBy(candidate => candidate.Distance)
                .Select(candidate => candidate.Column)
                .FirstOrDefault();
        }

        private void AddNeighbourFrameTie(Point3D start, Point3D end, double zBottom, double height, Vector3D up)
        {
            Point3D startLow = new Point3D(start.X, start.Y, zBottom + height * 0.40);
            Point3D endLow = new Point3D(end.X, end.Y, zBottom + height * 0.40);
            Point3D startHigh = new Point3D(start.X, start.Y, zBottom + height * 0.72);
            Point3D endHigh = new Point3D(end.X, end.Y, zBottom + height * 0.72);
            _bracing.AddCylinder(startLow, endLow, 42, 8);
            _bracing.AddCylinder(startHigh, endHigh, 42, 8);
            _steel.AddCylinder(startLow, endHigh, 12, 6);
            _steel.AddCylinder(startLow + (endHigh - startLow) * 0.43, startLow + (endHigh - startLow) * 0.57, 30, 8);
            _cableRestraintCount++;
        }

        private void AddNeighbourDiagonalTie(Point3D start, Point3D end, double zBottom, double height, Vector3D up)
        {
            Point3D cableStart = new Point3D(start.X, start.Y, zBottom + height * 0.82);
            Point3D cableEnd = new Point3D(end.X, end.Y, zBottom + height * 0.32);
            _steel.AddCylinder(cableStart, cableEnd, 12, 6);
            _steel.AddCylinder(cableStart + (cableEnd - cableStart) * 0.43,
                cableStart + (cableEnd - cableStart) * 0.57, 30, 8);
            _cableRestraintCount++;
        }

        private bool TryAddEmbeddedAnchorRestraint(
            Point3D center, double zBottom, double attachZ, double width, double depth, double ply,
            Vector3D xAxis, Vector3D yAxis, Vector3D up, Vector3D openDirection, double braceRun,
            bool includeGroundWorkingArea)
        {
            openDirection.Normalize();
            double faceDistance = FaceDistanceAlong(openDirection, width, depth, xAxis, yAxis);
            Point3D cableTop = new Point3D(center.X, center.Y, attachZ) + openDirection * (faceDistance + ply + 25);
            List<SupportFootprint> supportSurfaces = _supportFootprints
                .Where(floor => Math.Abs(floor.TopZ - zBottom) <= 450.0)
                .Concat(_foundationFootprints.Where(foundation =>
                    Math.Abs(foundation.TopZ - zBottom) <= 800.0))
                .ToList();

            Point3D anchor = new Point3D();
            bool found = false;
            double preferredDistance = Math.Min(1800.0, Math.Max(600.0, braceRun * 0.55));
            for (double distance = preferredDistance; distance >= 300.0; distance -= 100.0)
            {
                Point3D candidate = new Point3D(center.X, center.Y, zBottom + 80) - openDirection * distance;
                bool supported = supportSurfaces.Any(surface => surface.Contains(candidate, 100.0));
                if (!supported && includeGroundWorkingArea && _groundWorkingFootprint != null)
                    supported = _groundWorkingFootprint.Contains(candidate, 100.0);
                if (!supported) continue;
                anchor = candidate;
                found = true;
                break;
            }
            if (!found) return false;

            _steel.AddCylinder(anchor, cableTop, 12, 6);
            _steel.AddCylinder(anchor + (cableTop - anchor) * 0.43,
                anchor + (cableTop - anchor) * 0.57, 30, 8);
            _steel.AddOrientedBox(anchor, xAxis, yAxis, up, 180, 180, 16);
            _steel.AddCylinder(anchor - up * 40, anchor + up * 100, 22, 8);
            _cableRestraintCount++;
            return true;
        }

        private static double FaceDistanceAlong(
            Vector3D direction, double width, double depth, Vector3D xAxis, Vector3D yAxis)
        {
            direction.Normalize();
            return Math.Abs(Vector3D.DotProduct(direction, xAxis)) * width / 2.0 +
                   Math.Abs(Vector3D.DotProduct(direction, yAxis)) * depth / 2.0;
        }

        private static void GetColumnLocalAxes(Element element, out Vector3D xAxis, out Vector3D yAxis)
        {
            xAxis = new Vector3D(1, 0, 0);
            yAxis = new Vector3D(0, 1, 0);
            try
            {
                if (element is FamilyInstance instance)
                {
                    Autodesk.Revit.DB.Transform transform = instance.GetTransform();
                    Vector3D transformedX = new Vector3D(transform.BasisX.X, transform.BasisX.Y, 0);
                    Vector3D transformedY = new Vector3D(transform.BasisY.X, transform.BasisY.Y, 0);
                    if (transformedX.Length > 1e-6 && transformedY.Length > 1e-6)
                    {
                        transformedX.Normalize();
                        transformedY.Normalize();
                        xAxis = transformedX;
                        yAxis = transformedY;
                        return;
                    }
                }
            }
            catch
            {
                // Fall through to the LocationPoint rotation compatibility path.
            }

            double rotation = element.Location is LocationPoint point ? point.Rotation : 0;
            xAxis = new Vector3D(Math.Cos(rotation), Math.Sin(rotation), 0);
            yAxis = new Vector3D(-Math.Sin(rotation), Math.Cos(rotation), 0);
        }

        private void AddWingNut(Point3D center, Vector3D lengthAxis, Vector3D depthAxis, Vector3D up, double tieDiameter)
        {
            _steel.AddOrientedBox(center, lengthAxis, depthAxis, up, 70, tieDiameter, 12);
            _steel.AddOrientedBox(center, lengthAxis, depthAxis, up, tieDiameter, 70, 12);
        }

        private void AddFoundationFormwork(Element element, BoundingBoxXYZ box)
        {
            Models.CalculationParameters design = _data.GetComponentFormworkDesign(element.UniqueId)?.Parameters;
            Models.ComponentIntersectionInfo intersections = _data.GetComponentIntersectionInfo(element.UniqueId);
            double minX = X(box.Min.X), maxX = X(box.Max.X);
            double minY = Y(box.Min.Y), maxY = Y(box.Max.Y);
            double minZ = Z(box.Min.Z), maxZ = Z(box.Max.Z);
            double width = maxX - minX, depth = maxY - minY, height = maxZ - minZ;
            if (width < 100 || depth < 100 || height < 100) return;

            // Do not wrap deep slender piles; pile-cap/footing formwork remains visible.
            if (height > 3 * Math.Max(width, depth) && Math.Max(width, depth) < 1200) return;

            double ply = Positive(design?.VanMatThickness ?? _data.VanMatThickness, 18);
            Point3D center = new Point3D((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);
            _plywood.AddBox(new Point3D(center.X, minY - ply / 2, center.Z), width + 2 * ply, ply, height);
            _plywood.AddBox(new Point3D(center.X, maxY + ply / 2, center.Z), width + 2 * ply, ply, height);
            _plywood.AddBox(new Point3D(minX - ply / 2, center.Y, center.Z), ply, depth, height);
            _plywood.AddBox(new Point3D(maxX + ply / 2, center.Y, center.Z), ply, depth, height);
            AddPlanSurfaceWithCutouts(minX, maxX, minY, maxY, maxZ + ply / 2, ply,
                intersections?.PlanCutouts);

            double walerWidth = Positive(design?.XaGoPhuWidth ?? _data.XaGoPhuWidth, 60);
            double walerDepth = Positive(design?.XaGoPhuHeight ?? _data.XaGoPhuHeight, 60);
            double walerSpacing = Positive(design?.Joist1Spacing ?? _data.Joist1Spacing, 500);
            foreach (double z in Positions(minZ + walerDepth / 2, maxZ - walerDepth / 2,
                         walerSpacing, _compactLod ? 5 : 8))
            {
                _timber.AddBox(new Point3D(center.X, minY - ply - walerDepth / 2, z),
                    width + 2 * walerWidth, walerDepth, walerWidth);
                _timber.AddBox(new Point3D(center.X, maxY + ply + walerDepth / 2, z),
                    width + 2 * walerWidth, walerDepth, walerWidth);
                _timber.AddBox(new Point3D(minX - ply - walerDepth / 2, center.Y, z),
                    walerDepth, depth, walerWidth);
                _timber.AddBox(new Point3D(maxX + ply + walerDepth / 2, center.Y, z),
                    walerDepth, depth, walerWidth);
            }
        }

        private void AddColumnFacePanel(Point3D columnCenter, Vector3D outwardNormal,
            double faceLength, double standOff, double grossHeight, double topReduction,
            double thickness, Vector3D lengthAxis, Vector3D depthAxis, Vector3D up)
        {
            double clearHeight = Math.Max(0, grossHeight - Math.Max(0, topReduction));
            if (clearHeight < 1) return;
            double bottomZ = columnCenter.Z - grossHeight / 2.0;
            Point3D faceCenter = new Point3D(columnCenter.X, columnCenter.Y,
                bottomZ + clearHeight / 2.0) + outwardNormal * standOff;
            _plywood.AddOrientedBox(faceCenter, lengthAxis, depthAxis, up,
                faceLength, thickness, clearHeight);
        }

        private List<Models.FormworkCutoutRectangle> GetLocalPlanCutouts(
            IEnumerable<Models.FormworkCutoutRectangle> globalCutouts)
        {
            return (globalCutouts ?? Enumerable.Empty<Models.FormworkCutoutRectangle>())
                .Select(cutout => new Models.FormworkCutoutRectangle
                {
                    MinXmm = cutout.MinXmm - _origin.X,
                    MaxXmm = cutout.MaxXmm - _origin.X,
                    MinYmm = cutout.MinYmm - _origin.Y,
                    MaxYmm = cutout.MaxYmm - _origin.Y,
                    Kind = cutout.Kind,
                    SourceElementUniqueId = cutout.SourceElementUniqueId
                })
                .ToList();
        }

        private void AddPlanSurfaceWithCutouts(double minX, double maxX, double minY, double maxY,
            double centerZ, double thickness, IEnumerable<Models.FormworkCutoutRectangle> globalCutouts)
        {
            List<Models.FormworkCutoutRectangle> cutouts = (globalCutouts ??
                Enumerable.Empty<Models.FormworkCutoutRectangle>())
                .Select(cutout => new Models.FormworkCutoutRectangle
                {
                    MinXmm = Math.Max(minX, cutout.MinXmm - _origin.X),
                    MaxXmm = Math.Min(maxX, cutout.MaxXmm - _origin.X),
                    MinYmm = Math.Max(minY, cutout.MinYmm - _origin.Y),
                    MaxYmm = Math.Min(maxY, cutout.MaxYmm - _origin.Y),
                    Kind = cutout.Kind,
                    SourceElementUniqueId = cutout.SourceElementUniqueId
                })
                .Where(cutout => cutout.MaxXmm - cutout.MinXmm > 1 &&
                                 cutout.MaxYmm - cutout.MinYmm > 1)
                .ToList();
            if (cutouts.Count == 0)
            {
                _plywood.AddBox(new Point3D((minX + maxX) / 2, (minY + maxY) / 2, centerZ),
                    maxX - minX, maxY - minY, thickness);
                return;
            }

            List<double> xs = new[] { minX, maxX }.Concat(cutouts.SelectMany(cutout =>
                new[] { cutout.MinXmm, cutout.MaxXmm })).Distinct().OrderBy(value => value).ToList();
            List<double> ys = new[] { minY, maxY }.Concat(cutouts.SelectMany(cutout =>
                new[] { cutout.MinYmm, cutout.MaxYmm })).Distinct().OrderBy(value => value).ToList();
            for (int ix = 0; ix < xs.Count - 1; ix++)
            {
                for (int iy = 0; iy < ys.Count - 1; iy++)
                {
                    double x0 = xs[ix], x1 = xs[ix + 1], y0 = ys[iy], y1 = ys[iy + 1];
                    if (x1 - x0 < 1 || y1 - y0 < 1) continue;
                    double cx = (x0 + x1) / 2.0, cy = (y0 + y1) / 2.0;
                    if (cutouts.Any(cutout => cx > cutout.MinXmm - 0.1 && cx < cutout.MaxXmm + 0.1 &&
                                                     cy > cutout.MinYmm - 0.1 && cy < cutout.MaxYmm + 0.1))
                        continue;
                    _plywood.AddBox(new Point3D(cx, cy, centerZ), x1 - x0, y1 - y0, thickness);
                }
            }
        }

        private static List<Models.FormworkLengthInterval> ComplementIntervals(double length,
            IEnumerable<Models.FormworkLengthInterval> gaps)
        {
            List<Models.FormworkLengthInterval> clipped = Models.ComponentIntersectionService.MergeIntervals(
                (gaps ?? Enumerable.Empty<Models.FormworkLengthInterval>()).Select(gap =>
                    new Models.FormworkLengthInterval
                    {
                        StartMm = Math.Max(0, Math.Min(length, gap.StartMm)),
                        EndMm = Math.Max(0, Math.Min(length, gap.EndMm)),
                        SourceElementUniqueId = gap.SourceElementUniqueId
                    }));
            var clear = new List<Models.FormworkLengthInterval>();
            double cursor = 0;
            foreach (Models.FormworkLengthInterval gap in clipped)
            {
                if (gap.StartMm > cursor + 0.1)
                    clear.Add(new Models.FormworkLengthInterval { StartMm = cursor, EndMm = gap.StartMm });
                cursor = Math.Max(cursor, gap.EndMm);
            }
            if (cursor < length - 0.1)
                clear.Add(new Models.FormworkLengthInterval { StartMm = cursor, EndMm = length });
            return clear;
        }

        private static bool IsDistanceInClearSegment(double distance,
            IEnumerable<Models.FormworkLengthInterval> clearSegments)
        {
            return (clearSegments ?? Enumerable.Empty<Models.FormworkLengthInterval>()).Any(segment =>
                distance >= segment.StartMm - 0.1 && distance <= segment.EndMm + 0.1);
        }

        /// <summary>
        /// Resolves the actual bearing surface at one XY station. Revit level elevations
        /// are only datums; they are not proof that a slab or foundation exists under the
        /// prop. Choosing the highest verified top face below the accessory target also
        /// handles stepped floor plates and double-height openings correctly.
        /// </summary>
        private bool TryFindSupportElevationAt(Point3D station, double targetTop,
            double clearance, out double supportElevation)
        {
            const double minimumClearHeight = 300.0;
            SupportFootprint bearing = _supportFootprints
                .Concat(_foundationFootprints)
                .Where(footprint => footprint.TopZ <= targetTop - minimumClearHeight)
                .Where(footprint => footprint.Contains(
                    new Point3D(station.X, station.Y, footprint.TopZ), clearance))
                .OrderByDescending(footprint => footprint.TopZ)
                .FirstOrDefault();

            if (bearing == null)
            {
                supportElevation = double.NaN;
                return false;
            }

            supportElevation = bearing.TopZ;
            return true;
        }

        private static ElementId GetElementLevelId(Element element)
        {
            if (element.LevelId != null && element.LevelId != ElementId.InvalidElementId) return element.LevelId;
            Parameter levelParameter = element.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM)
                ?? element.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
            return levelParameter?.AsElementId();
        }

        private bool IsFoundationSlab(Element element)
        {
            return element is Floor floor
                && _document.GetElement(floor.GetTypeId()) is FloorType floorType
                && floorType.IsFoundationSlab;
        }

        private double GetFamilyDimension(Element element, IEnumerable<string> names, double fallback)
        {
            ElementType type = _document.GetElement(element.GetTypeId()) as ElementType;
            foreach (string name in names)
            {
                Parameter parameter = element.LookupParameter(name) ?? type?.LookupParameter(name);
                if (parameter != null && parameter.StorageType == StorageType.Double && parameter.AsDouble() > 0)
                    return parameter.AsDouble() * MmPerFoot;
            }
            return Positive(fallback, 300);
        }

        private double GetPerpendicularBoxWidth(BoundingBoxXYZ box, Vector3D cross)
        {
            var points = new[]
            {
                new Point3D(X(box.Min.X), Y(box.Min.Y), 0),
                new Point3D(X(box.Min.X), Y(box.Max.Y), 0),
                new Point3D(X(box.Max.X), Y(box.Min.Y), 0),
                new Point3D(X(box.Max.X), Y(box.Max.Y), 0)
            };
            IEnumerable<double> values = points.Select(p => p.X * cross.X + p.Y * cross.Y);
            return values.Max() - values.Min();
        }

        private static List<double> Positions(double min, double max, double requestedSpacing, int maximumCount)
        {
            var result = new List<double>();
            if (max <= min) return result;
            double spacing = Math.Max(100, requestedSpacing);
            int count = Math.Max(2, (int)Math.Ceiling((max - min) / spacing) + 1);
            count = Math.Min(count, maximumCount);
            if (count == 1) return new List<double> { (min + max) / 2 };
            for (int i = 0; i < count; i++) result.Add(min + (max - min) * i / (count - 1));
            return result;
        }

        private static IEnumerable<double> Distances(double length, double requestedSpacing, int maximumCount)
        {
            double spacing = Math.Max(100, requestedSpacing);
            int count = Math.Max(2, (int)Math.Ceiling(length / spacing) + 1);
            count = Math.Min(count, maximumCount);
            for (int i = 0; i < count; i++) yield return length * i / (count - 1);
        }

        private static IEnumerable<double> CenteredPositions(double length, double requestedSpacing, int maximumCount)
        {
            double usable = Math.Max(0, length - 80);
            int count = Math.Max(2, (int)Math.Ceiling(usable / Math.Max(100, requestedSpacing)) + 1);
            count = Math.Min(count, maximumCount);
            if (count <= 1)
            {
                yield return 0;
                yield break;
            }
            for (int i = 0; i < count; i++)
                yield return -usable / 2 + usable * i / (count - 1);
        }

        private static IEnumerable<double> TieOffsets(double outsideOffset, int count)
        {
            count = Math.Max(1, Math.Min(4, count));
            if (count == 1)
            {
                yield return 0;
                yield break;
            }
            for (int i = 0; i < count; i++)
                yield return -outsideOffset + 2.0 * outsideOffset * i / (count - 1);
        }

        private static double Positive(double value, double fallback) => value > 0 ? value : fallback;
        private double X(double feet) => feet * MmPerFoot - _origin.X;
        private double Y(double feet) => feet * MmPerFoot - _origin.Y;
        private double Z(double feet) => feet * MmPerFoot - _origin.Z;
        private Point3D ToPoint(XYZ point) => new Point3D(X(point.X), Y(point.Y), Z(point.Z));

        private static void AddModel(GroupModel3D group, SimpleMeshBuilder builder, MediaColor color)
        {
            if (builder.Mesh.Positions.Count == 0) return;
            var mesh = builder.ToMesh();
            // Freeze not needed for SharpDX geometry
            var material = new HelixToolkit.Wpf.SharpDX.DiffuseMaterial() { DiffuseColor = new HelixToolkit.Maths.Color4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f) };
            // Freeze not needed for SharpDX material
            var model = new MeshGeometryModel3D { Geometry = mesh, Material = material };
            // Freeze not needed for SharpDX model
            group.Children.Add(model);
        }
    }
}
