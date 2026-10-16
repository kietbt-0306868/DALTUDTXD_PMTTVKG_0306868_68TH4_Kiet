using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models
{
    public static class ComponentIntersectionService
    {
        private const double MmPerFoot = 304.8;
        private const double ContactToleranceFt = 75.0 / MmPerFoot;

        private sealed class Node
        {
            public FloorElementModel Model { get; set; }
            public Element Element { get; set; }
            public BoundingBoxXYZ Box { get; set; }
            public List<PlanarFace> TopFaces { get; set; } = new List<PlanarFace>();
        }

        public static Dictionary<string, ComponentIntersectionInfo> Analyze(
            Document document, IEnumerable<FloorElementModel> components)
        {
            var result = new Dictionary<string, ComponentIntersectionInfo>(StringComparer.OrdinalIgnoreCase);
            if (document == null) return result;

            List<Node> nodes = (components ?? Enumerable.Empty<FloorElementModel>())
                .Where(model => model != null && !string.IsNullOrWhiteSpace(model.UniqueId))
                .Select(model =>
                {
                    Element element = document.GetElement(model.UniqueId);
                    return new Node
                    {
                        Model = model,
                        Element = element,
                        Box = element?.get_BoundingBox(null),
                        TopFaces = model.ComponentType == StructuralComponentType.Slab
                            ? GetPlanarFaces(element).Where(face => face.FaceNormal.Z > 0.70).ToList()
                            : new List<PlanarFace>()
                    };
                })
                .Where(node => node.Element != null && node.Box != null)
                .GroupBy(node => node.Model.UniqueId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Last())
                .ToList();

            var neighbours = nodes.ToDictionary(node => node.Model.UniqueId,
                node => new List<Node>(), StringComparer.OrdinalIgnoreCase);
            for (int left = 0; left < nodes.Count; left++)
            {
                for (int right = left + 1; right < nodes.Count; right++)
                {
                    if (!BoxesTouch(nodes[left].Box, nodes[right].Box, ContactToleranceFt)) continue;
                    neighbours[nodes[left].Model.UniqueId].Add(nodes[right]);
                    neighbours[nodes[right].Model.UniqueId].Add(nodes[left]);
                }
            }

            foreach (Node node in nodes)
            {
                ComponentIntersectionInfo info = CreateGrossInfo(node.Model);
                foreach (Node neighbour in neighbours[node.Model.UniqueId])
                    info.IntersectingElementUniqueIds.Add(neighbour.Model.UniqueId);

                switch (node.Model.ComponentType)
                {
                    case StructuralComponentType.Column:
                        AnalyzeColumn(node, neighbours[node.Model.UniqueId], info);
                        break;
                    case StructuralComponentType.Beam:
                        AnalyzeBeam(node, neighbours[node.Model.UniqueId], info);
                        break;
                    case StructuralComponentType.Foundation:
                        AnalyzeFoundation(node, neighbours[node.Model.UniqueId], info);
                        break;
                    default:
                        AnalyzeSlab(node, neighbours[node.Model.UniqueId], info);
                        break;
                }
                result[node.Model.UniqueId] = info;
            }
            return result;
        }

        private static ComponentIntersectionInfo CreateGrossInfo(FloorElementModel model)
        {
            double widthM = Math.Max(0, model.Elevation) / 1000.0;
            double depthM = Math.Max(0, model.Thickness) / 1000.0;
            double lengthM = Math.Max(0, model.StoryHeight) / 1000.0;
            var info = new ComponentIntersectionInfo
            {
                ElementUniqueId = model.UniqueId,
                ComponentType = model.ComponentType,
                GrossPerimeterM = Math.Max(0, model.Perimeter),
                NetExposedPerimeterM = Math.Max(0, model.Perimeter),
                GrossLengthMm = Math.Max(0, model.StoryHeight),
                NetFormworkLengthMm = Math.Max(0, model.StoryHeight)
            };
            switch (model.ComponentType)
            {
                case StructuralComponentType.Beam:
                    info.GrossBottomAreaM2 = widthM * lengthM;
                    info.NetBottomAreaM2 = info.GrossBottomAreaM2;
                    info.GrossSideAreaM2 = 2 * depthM * lengthM;
                    info.NetSideAreaM2 = info.GrossSideAreaM2;
                    break;
                case StructuralComponentType.Column:
                    info.GrossSideAreaM2 = 2 * (widthM + depthM) * lengthM;
                    info.NetSideAreaM2 = info.GrossSideAreaM2;
                    break;
                case StructuralComponentType.Foundation:
                    info.GrossSideAreaM2 = Math.Max(0, model.Perimeter) * depthM;
                    info.NetSideAreaM2 = info.GrossSideAreaM2;
                    info.GrossTopAreaM2 = Math.Max(0, model.Area);
                    info.NetTopAreaM2 = info.GrossTopAreaM2;
                    break;
                default:
                    info.GrossBottomAreaM2 = Math.Max(0, model.Area);
                    info.NetBottomAreaM2 = info.GrossBottomAreaM2;
                    info.GrossSideAreaM2 = Math.Max(0, model.Perimeter) * depthM;
                    info.NetSideAreaM2 = info.GrossSideAreaM2;
                    break;
            }
            return info;
        }

        private static void AnalyzeBeam(Node beam, List<Node> neighbours, ComponentIntersectionInfo info)
        {
            if (!(beam.Element.Location is LocationCurve location) || location.Curve == null) return;
            XYZ start = location.Curve.GetEndPoint(0);
            XYZ end = location.Curve.GetEndPoint(1);
            XYZ axis = end - start;
            double curveLengthFt = axis.GetLength();
            if (curveLengthFt < 1e-9) return;
            axis = axis.Normalize();
            XYZ cross = new XYZ(-axis.Y, axis.X, 0).Normalize();
            double probeFt = (Math.Max(100.0, beam.Model.Elevation) / 2.0 + 250.0) / MmPerFoot;
            List<Node> supportingSlabs = neighbours
                .Where(item => item.Model.ComponentType == StructuralComponentType.Slab &&
                               Math.Abs(item.Box.Max.Z - beam.Box.Max.Z) * MmPerFoot <= 300.0)
                .ToList();
            if (supportingSlabs.Count > 0)
            {
                int plusOnly = 0;
                int minusOnly = 0;
                int bothSides = 0;
                foreach (double ratio in new[] { 0.10, 0.30, 0.50, 0.70, 0.90 })
                {
                    XYZ station = location.Curve.Evaluate(ratio, true);
                    XYZ plusProbe = station + cross * probeFt;
                    XYZ minusProbe = station - cross * probeFt;
                    bool plusHasSlab = supportingSlabs.Any(slab =>
                        IsPointOnSlabTop(slab, plusProbe, beam.Box.Max.Z));
                    bool minusHasSlab = supportingSlabs.Any(slab =>
                        IsPointOnSlabTop(slab, minusProbe, beam.Box.Max.Z));
                    if (plusHasSlab && minusHasSlab) bothSides++;
                    else if (plusHasSlab) plusOnly++;
                    else if (minusHasSlab) minusOnly++;
                }
                if (plusOnly + minusOnly + bothSides > 0)
                {
                    info.HasBeamSideClassification = true;
                    info.IsPerimeterBeam = plusOnly + minusOnly > 0;
                    info.BeamInteriorSideSign = info.IsPerimeterBeam
                        ? (plusOnly >= minusOnly ? 1 : -1)
                        : 0;
                }
            }
            double modelLengthMm = Math.Max(1, beam.Model.StoryHeight);
            var rawIntervals = new List<FormworkLengthInterval>();
            foreach (Node column in neighbours.Where(item => item.Model.ComponentType == StructuralComponentType.Column))
            {
                double min = double.MaxValue, max = double.MinValue;
                foreach (XYZ corner in BoxCorners(column.Box))
                {
                    double projection = (corner - start).DotProduct(axis) * MmPerFoot;
                    min = Math.Min(min, projection);
                    max = Math.Max(max, projection);
                }
                min = Math.Max(0, min);
                max = Math.Min(modelLengthMm, max);
                if (max - min <= 1.0) continue;
                rawIntervals.Add(new FormworkLengthInterval
                {
                    StartMm = min,
                    EndMm = max,
                    SourceElementUniqueId = column.Model.UniqueId
                });
            }
            List<FormworkLengthInterval> merged = MergeIntervals(rawIntervals);
            info.BeamColumnGaps.AddRange(merged);
            double excludedMm = merged.Sum(interval => interval.LengthMm);
            info.NetFormworkLengthMm = Math.Max(0, modelLengthMm - excludedMm);
            double effectiveM = info.NetFormworkLengthMm / 1000.0;
            info.NetBottomAreaM2 = Math.Max(0, beam.Model.Elevation) / 1000.0 * effectiveM;
            info.NetSideAreaM2 = 2 * Math.Max(0, beam.Model.Thickness) / 1000.0 * effectiveM;
        }

        private static void AnalyzeColumn(Node column, List<Node> neighbours, ComponentIntersectionInfo info)
        {
            GetLocalAxes(column.Element, out XYZ localX, out XYZ localY);
            XYZ center = (column.Box.Min + column.Box.Max) * 0.5;
            double halfX = ProjectedHalfExtent(column.Box, center, localX);
            double halfY = ProjectedHalfExtent(column.Box, center, localY);
            foreach (Node beam in neighbours.Where(item => item.Model.ComponentType == StructuralComponentType.Beam))
            {
                XYZ axis;
                XYZ beamStart;
                XYZ beamEnd;
                if (beam.Element.Location is LocationCurve curve && curve.Curve != null)
                {
                    beamStart = curve.Curve.GetEndPoint(0);
                    beamEnd = curve.Curve.GetEndPoint(1);
                    axis = (curve.Curve.GetEndPoint(1) - curve.Curve.GetEndPoint(0)).Normalize();
                }
                else
                {
                    beamStart = beam.Box.Min;
                    beamEnd = beam.Box.Max;
                    axis = XYZ.BasisX;
                }
                double verticalOverlapMm = Math.Max(0,
                    Math.Min(column.Box.Max.Z, beam.Box.Max.Z) - Math.Max(column.Box.Min.Z, beam.Box.Min.Z)) * MmPerFoot;
                double beamDepth = Math.Min(Math.Max(0, column.Model.StoryHeight),
                    Math.Min(Math.Max(0, beam.Model.Thickness), verticalOverlapMm));
                if (beamDepth <= 1.0) continue;
                if (Math.Abs(axis.DotProduct(localX)) >= Math.Abs(axis.DotProduct(localY)))
                {
                    MarkReachedFaces(beamStart, beamEnd, center, localX, halfX, beamDepth,
                        info.ColumnFaceTopReductionsMm, 0, 1);
                }
                else
                {
                    MarkReachedFaces(beamStart, beamEnd, center, localY, halfY, beamDepth,
                        info.ColumnFaceTopReductionsMm, 2, 3);
                }
            }
            double h = Math.Max(0, column.Model.StoryHeight);
            double width = Math.Max(0, column.Model.Elevation);
            double depth = Math.Max(0, column.Model.Thickness);
            double areaMm2 = depth * Math.Max(0, h - info.ColumnFaceTopReductionsMm[0]) +
                             depth * Math.Max(0, h - info.ColumnFaceTopReductionsMm[1]) +
                             width * Math.Max(0, h - info.ColumnFaceTopReductionsMm[2]) +
                             width * Math.Max(0, h - info.ColumnFaceTopReductionsMm[3]);
            info.NetSideAreaM2 = areaMm2 / 1_000_000.0;
        }

        private static void MarkReachedFaces(XYZ start, XYZ end, XYZ center, XYZ faceAxis,
            double halfExtentFt, double reductionMm, double[] reductions, int positiveIndex, int negativeIndex)
        {
            double a = (start - center).DotProduct(faceAxis);
            double b = (end - center).DotProduct(faceAxis);
            double min = Math.Min(a, b);
            double max = Math.Max(a, b);
            double tolerance = 75.0 / MmPerFoot;
            bool reachesPositive = max >= halfExtentFt - tolerance;
            bool reachesNegative = min <= -halfExtentFt + tolerance;

            // Revit beams commonly terminate at the column centre rather than at
            // its physical face. The sign of the other end identifies the single
            // contacted face in that representation.
            if (!reachesPositive && !reachesNegative)
            {
                if (Math.Abs(max) >= Math.Abs(min)) reachesPositive = max > 0;
                else reachesNegative = min < 0;
            }
            if (reachesPositive)
                reductions[positiveIndex] = Math.Max(reductions[positiveIndex], reductionMm);
            if (reachesNegative)
                reductions[negativeIndex] = Math.Max(reductions[negativeIndex], reductionMm);
        }

        private static bool IsPointOnSlabTop(Node slab, XYZ point, double expectedTopZ)
        {
            foreach (PlanarFace face in slab.TopFaces)
            {
                try
                {
                    IntersectionResult projection = face.Project(point);
                    if (projection == null || !face.IsInside(projection.UVPoint)) continue;
                    if (Math.Abs(projection.XYZPoint.Z - expectedTopZ) * MmPerFoot <= 300.0)
                        return true;
                }
                catch (Autodesk.Revit.Exceptions.ApplicationException)
                {
                    // Ignore malformed/imported faces and continue with another face.
                }
            }
            return false;
        }

        private static IEnumerable<PlanarFace> GetPlanarFaces(Element element)
        {
            if (element == null) return Enumerable.Empty<PlanarFace>();
            try
            {
                GeometryElement geometry = element.get_Geometry(new Options
                {
                    DetailLevel = ViewDetailLevel.Medium,
                    IncludeNonVisibleObjects = false,
                    ComputeReferences = false
                });
                return geometry == null
                    ? Enumerable.Empty<PlanarFace>()
                    : GetSolids(geometry, Transform.Identity)
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

        private static double ProjectedHalfExtent(BoundingBoxXYZ box, XYZ center, XYZ axis)
        {
            return BoxCorners(box).Max(corner => Math.Abs((corner - center).DotProduct(axis)));
        }

        private static void AnalyzeSlab(Node slab, List<Node> neighbours, ComponentIntersectionInfo info)
        {
            var allBottomCutouts = new List<FormworkCutoutRectangle>();
            var beamCutouts = new List<FormworkCutoutRectangle>();
            var columnCutouts = new List<FormworkCutoutRectangle>();
            foreach (Node neighbour in neighbours)
            {
                if (neighbour.Model.ComponentType != StructuralComponentType.Beam &&
                    neighbour.Model.ComponentType != StructuralComponentType.Column) continue;
                FormworkCutoutRectangle cutout = IntersectPlan(slab.Box, neighbour.Box,
                    neighbour.Model.ComponentType == StructuralComponentType.Beam
                        ? FormworkCutoutKind.BeamSupport
                        : FormworkCutoutKind.ColumnPenetration,
                    neighbour.Model.UniqueId);
                if (cutout == null) continue;
                info.PlanCutouts.Add(cutout);
                allBottomCutouts.Add(cutout);
                if (neighbour.Model.ComponentType == StructuralComponentType.Beam) beamCutouts.Add(cutout);
                else columnCutouts.Add(cutout);
            }
            double excludedBottom = UnionAreaM2(allBottomCutouts);
            info.NetBottomAreaM2 = Math.Max(0, info.GrossBottomAreaM2 - excludedBottom);
            double columnOpeningSide = columnCutouts.Sum(cutout =>
                2 * ((cutout.MaxXmm - cutout.MinXmm) + (cutout.MaxYmm - cutout.MinYmm)) / 1000.0 *
                Math.Max(0, slab.Model.Thickness) / 1000.0);
            info.InternalOpeningSideAreaM2 = columnOpeningSide;

            double coveredEdgeM = CoveredRectangularPerimeterM(slab.Box, beamCutouts);
            info.NetExposedPerimeterM = Math.Max(0, info.GrossPerimeterM - coveredEdgeM);
            info.NetSideAreaM2 = info.NetExposedPerimeterM * Math.Max(0, slab.Model.Thickness) / 1000.0;
        }

        private static void AnalyzeFoundation(Node foundation, List<Node> neighbours, ComponentIntersectionInfo info)
        {
            var cutouts = new List<FormworkCutoutRectangle>();
            foreach (Node column in neighbours.Where(item => item.Model.ComponentType == StructuralComponentType.Column))
            {
                FormworkCutoutRectangle cutout = IntersectPlan(foundation.Box, column.Box,
                    FormworkCutoutKind.FoundationColumnOpening, column.Model.UniqueId);
                if (cutout == null) continue;
                cutouts.Add(cutout);
                info.PlanCutouts.Add(cutout);
            }
            info.NetTopAreaM2 = Math.Max(0, info.GrossTopAreaM2 - UnionAreaM2(cutouts));
        }

        private static FormworkCutoutRectangle IntersectPlan(BoundingBoxXYZ host, BoundingBoxXYZ cutter,
            FormworkCutoutKind kind, string sourceId)
        {
            double minX = Math.Max(host.Min.X, cutter.Min.X);
            double maxX = Math.Min(host.Max.X, cutter.Max.X);
            double minY = Math.Max(host.Min.Y, cutter.Min.Y);
            double maxY = Math.Min(host.Max.Y, cutter.Max.Y);
            if (maxX - minX <= 1e-6 || maxY - minY <= 1e-6) return null;
            return new FormworkCutoutRectangle
            {
                MinXmm = minX * MmPerFoot,
                MaxXmm = maxX * MmPerFoot,
                MinYmm = minY * MmPerFoot,
                MaxYmm = maxY * MmPerFoot,
                Kind = kind,
                SourceElementUniqueId = sourceId
            };
        }

        private static double CoveredRectangularPerimeterM(BoundingBoxXYZ slab,
            List<FormworkCutoutRectangle> beams)
        {
            double minX = slab.Min.X * MmPerFoot, maxX = slab.Max.X * MmPerFoot;
            double minY = slab.Min.Y * MmPerFoot, maxY = slab.Max.Y * MmPerFoot;
            const double tolerance = 100.0;
            var edgeIntervals = new[] { new List<FormworkLengthInterval>(), new List<FormworkLengthInterval>(),
                new List<FormworkLengthInterval>(), new List<FormworkLengthInterval>() };
            foreach (FormworkCutoutRectangle beam in beams)
            {
                if (Math.Abs(beam.MinYmm - minY) <= tolerance)
                    edgeIntervals[0].Add(new FormworkLengthInterval { StartMm = beam.MinXmm, EndMm = beam.MaxXmm });
                if (Math.Abs(beam.MaxYmm - maxY) <= tolerance)
                    edgeIntervals[1].Add(new FormworkLengthInterval { StartMm = beam.MinXmm, EndMm = beam.MaxXmm });
                if (Math.Abs(beam.MinXmm - minX) <= tolerance)
                    edgeIntervals[2].Add(new FormworkLengthInterval { StartMm = beam.MinYmm, EndMm = beam.MaxYmm });
                if (Math.Abs(beam.MaxXmm - maxX) <= tolerance)
                    edgeIntervals[3].Add(new FormworkLengthInterval { StartMm = beam.MinYmm, EndMm = beam.MaxYmm });
            }
            return edgeIntervals.Sum(intervals => MergeIntervals(intervals).Sum(interval => interval.LengthMm)) / 1000.0;
        }

        public static List<FormworkLengthInterval> MergeIntervals(IEnumerable<FormworkLengthInterval> source)
        {
            var merged = new List<FormworkLengthInterval>();
            foreach (FormworkLengthInterval next in (source ?? Enumerable.Empty<FormworkLengthInterval>())
                         .Where(interval => interval != null && interval.EndMm > interval.StartMm)
                         .OrderBy(interval => interval.StartMm))
            {
                if (merged.Count == 0 || next.StartMm > merged[merged.Count - 1].EndMm + 0.1)
                    merged.Add(new FormworkLengthInterval
                    {
                        StartMm = next.StartMm,
                        EndMm = next.EndMm,
                        SourceElementUniqueId = next.SourceElementUniqueId
                    });
                else
                    merged[merged.Count - 1].EndMm = Math.Max(merged[merged.Count - 1].EndMm, next.EndMm);
            }
            return merged;
        }

        public static double UnionAreaM2(IEnumerable<FormworkCutoutRectangle> rectangles)
        {
            List<FormworkCutoutRectangle> rects = (rectangles ?? Enumerable.Empty<FormworkCutoutRectangle>())
                .Where(rect => rect != null && rect.MaxXmm > rect.MinXmm && rect.MaxYmm > rect.MinYmm).ToList();
            if (rects.Count == 0) return 0;
            List<double> xs = rects.SelectMany(rect => new[] { rect.MinXmm, rect.MaxXmm }).Distinct().OrderBy(x => x).ToList();
            double areaMm2 = 0;
            for (int i = 0; i < xs.Count - 1; i++)
            {
                double x0 = xs[i], x1 = xs[i + 1];
                var ys = rects.Where(rect => rect.MinXmm < x1 && rect.MaxXmm > x0)
                    .Select(rect => new FormworkLengthInterval { StartMm = rect.MinYmm, EndMm = rect.MaxYmm });
                areaMm2 += (x1 - x0) * MergeIntervals(ys).Sum(interval => interval.LengthMm);
            }
            return areaMm2 / 1_000_000.0;
        }

        private static bool BoxesTouch(BoundingBoxXYZ a, BoundingBoxXYZ b, double tolerance)
        {
            return a.Min.X <= b.Max.X + tolerance && a.Max.X >= b.Min.X - tolerance &&
                   a.Min.Y <= b.Max.Y + tolerance && a.Max.Y >= b.Min.Y - tolerance &&
                   a.Min.Z <= b.Max.Z + tolerance && a.Max.Z >= b.Min.Z - tolerance;
        }

        private static IEnumerable<XYZ> BoxCorners(BoundingBoxXYZ box)
        {
            foreach (double x in new[] { box.Min.X, box.Max.X })
                foreach (double y in new[] { box.Min.Y, box.Max.Y })
                    foreach (double z in new[] { box.Min.Z, box.Max.Z })
                        yield return new XYZ(x, y, z);
        }

        private static void GetLocalAxes(Element element, out XYZ xAxis, out XYZ yAxis)
        {
            xAxis = XYZ.BasisX;
            yAxis = XYZ.BasisY;
            if (!(element is FamilyInstance instance)) return;
            Transform transform = instance.GetTransform();
            XYZ x = new XYZ(transform.BasisX.X, transform.BasisX.Y, 0);
            XYZ y = new XYZ(transform.BasisY.X, transform.BasisY.Y, 0);
            if (x.GetLength() > 1e-8) xAxis = x.Normalize();
            if (y.GetLength() > 1e-8) yAxis = y.Normalize();
        }
    }
}
