using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.Services
{
    /// <summary>
    /// Resolves the real construction bearing elevation below a proposed shoring
    /// point. Revit Level elevation is intentionally not used: stepped slabs,
    /// offsets, foundations and openings are evaluated from their actual top faces.
    /// </summary>
    internal sealed class ConstructionElevationResolver
    {
        private sealed class BearingSurface
        {
            public ElementId SourceId { get; set; }
            public PlanarFace Face { get; set; }
            public double MinX { get; set; }
            public double MaxX { get; set; }
            public double MinY { get; set; }
            public double MaxY { get; set; }
        }

        private readonly List<BearingSurface> _surfaces = new List<BearingSurface>();

        public ConstructionElevationResolver(Document document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            var categories = new List<BuiltInCategory>
            {
                BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_StructuralFoundation,
                // A column brace base can legitimately bear on a grade/perimeter beam.
                // The caller still constrains the expected elevation and validates a
                // complete base patch, so upper framing cannot be mistaken for ground.
                BuiltInCategory.OST_StructuralFraming
            };
            foreach (Element element in new FilteredElementCollector(document)
                         .WherePasses(new ElementMulticategoryFilter(categories))
                         .WhereElementIsNotElementType())
            {
                BoundingBoxXYZ box = element.get_BoundingBox(null);
                if (box == null) continue;
                foreach (PlanarFace face in GetPlanarFaces(element).Where(item => item.FaceNormal.Z > 0.70))
                {
                    _surfaces.Add(new BearingSurface
                    {
                        SourceId = element.Id,
                        Face = face,
                        MinX = box.Min.X,
                        MaxX = box.Max.X,
                        MinY = box.Min.Y,
                        MaxY = box.Max.Y
                    });
                }
            }
        }

        public bool TryFindHighestBearingBelow(double x, double y, double targetZ,
            ElementId excludedElementId, out double bearingZ, double minimumClearanceMm = 300.0)
        {
            bearingZ = double.NegativeInfinity;
            double clearance = Mm(Math.Max(50, minimumClearanceMm));
            double xyTolerance = Mm(20);
            foreach (BearingSurface surface in _surfaces)
            {
                if (excludedElementId != null && surface.SourceId == excludedElementId) continue;
                if (x < surface.MinX - xyTolerance || x > surface.MaxX + xyTolerance ||
                    y < surface.MinY - xyTolerance || y > surface.MaxY + xyTolerance) continue;
                try
                {
                    XYZ normal = surface.Face.FaceNormal;
                    if (Math.Abs(normal.Z) < 1e-8) continue;
                    XYZ origin = surface.Face.Origin;
                    double z = origin.Z -
                               (normal.X * (x - origin.X) + normal.Y * (y - origin.Y)) / normal.Z;
                    if (z >= targetZ - clearance || z <= bearingZ) continue;
                    XYZ pointOnPlane = new XYZ(x, y, z);
                    IntersectionResult projection = surface.Face.Project(pointOnPlane);
                    if (projection == null || projection.Distance > Mm(5) ||
                        !surface.Face.IsInside(projection.UVPoint)) continue;
                    bearingZ = projection.XYZPoint.Z;
                }
                catch (Autodesk.Revit.Exceptions.ApplicationException)
                {
                    // Ignore malformed/imported faces. Failing closed prevents a foot
                    // from being drawn into empty space.
                }
            }
            return !double.IsNegativeInfinity(bearingZ);
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
                    .OfType<PlanarFace>()
                    .ToList();
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

        private static double Mm(double value) =>
            UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Millimeters);
    }
}
