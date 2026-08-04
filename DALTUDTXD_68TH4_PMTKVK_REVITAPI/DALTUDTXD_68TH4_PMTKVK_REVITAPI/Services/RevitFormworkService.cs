using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.Services
{
    /// <summary>
    /// Creates formwork DirectShapes after cutting structural contact volumes
    /// out of the proposed formwork solids. The caller owns the transaction.
    /// </summary>
    public static class RevitFormworkService
    {
        public static DirectShape BuildSlabFormwork(Document document, IEnumerable<Solid> formwork,
            IEnumerable<Element> intersectingElements, string name = "Ván khuôn sàn") =>
            CreateIntersectionAwareShape(document, formwork, intersectingElements, name);

        public static DirectShape BuildBeamFormwork(Document document, IEnumerable<Solid> formwork,
            IEnumerable<Element> intersectingElements, string name = "Ván khuôn dầm") =>
            CreateIntersectionAwareShape(document, formwork, intersectingElements, name);

        public static DirectShape BuildVerticalFormwork(Document document, IEnumerable<Solid> formwork,
            IEnumerable<Element> intersectingElements, string name = "Ván khuôn cột") =>
            CreateIntersectionAwareShape(document, formwork, intersectingElements, name);

        public static DirectShape BuildFoundationFormwork(Document document, IEnumerable<Solid> formwork,
            IEnumerable<Element> intersectingElements, string name = "Ván khuôn móng") =>
            CreateIntersectionAwareShape(document, formwork, intersectingElements, name);

        public static DirectShape CreateIntersectionAwareShape(Document document,
            IEnumerable<Solid> proposedFormwork, IEnumerable<Element> intersectingElements, string name)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            List<Solid> cut = SubtractIntersectingSolids(proposedFormwork, intersectingElements);
            if (cut.Count == 0) return null;

            var shape = DirectShape.CreateElement(document,
                new ElementId((long)BuiltInCategory.OST_GenericModel));
            shape.Name = string.IsNullOrWhiteSpace(name) ? "Ván khuôn" : name;
            shape.SetShape(cut.Cast<GeometryObject>().ToList());
            return shape;
        }

        public static List<Solid> SubtractIntersectingSolids(IEnumerable<Solid> proposedFormwork,
            IEnumerable<Element> intersectingElements)
        {
            List<Solid> cutters = (intersectingElements ?? Enumerable.Empty<Element>())
                .SelectMany(GetElementSolids)
                .Where(IsUsable)
                .ToList();
            var output = new List<Solid>();
            foreach (Solid source in (proposedFormwork ?? Enumerable.Empty<Solid>()).Where(IsUsable))
            {
                var pieces = new List<Solid> { source };
                foreach (Solid cutter in cutters)
                {
                    var next = new List<Solid>();
                    foreach (Solid piece in pieces)
                    {
                        try
                        {
                            Solid difference = BooleanOperationsUtils.ExecuteBooleanOperation(
                                piece, cutter, BooleanOperationsType.Difference);
                            if (IsUsable(difference)) next.Add(difference);
                        }
                        catch (Autodesk.Revit.Exceptions.InvalidOperationException)
                        {
                            // Coincident or tangent faces can make Revit reject a Boolean.
                            next.Add(piece);
                        }
                    }
                    pieces = next;
                    if (pieces.Count == 0) break;
                }
                output.AddRange(pieces.Where(IsUsable));
            }
            return output;
        }

        private static IEnumerable<Solid> GetElementSolids(Element element)
        {
            if (element == null) yield break;
            GeometryElement geometry = element.get_Geometry(new Options
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = false,
                DetailLevel = ViewDetailLevel.Fine
            });
            if (geometry == null) yield break;
            foreach (Solid solid in GetSolids(geometry, Transform.Identity)) yield return solid;
        }

        private static IEnumerable<Solid> GetSolids(GeometryElement geometry, Transform transform)
        {
            foreach (GeometryObject item in geometry)
            {
                if (item is Solid solid && IsUsable(solid))
                    yield return SolidUtils.CreateTransformed(solid, transform);
                else if (item is GeometryInstance instance)
                {
                    Transform nested = transform.Multiply(instance.Transform);
                    foreach (Solid child in GetSolids(instance.GetSymbolGeometry(), nested)) yield return child;
                }
            }
        }

        private static bool IsUsable(Solid solid) => solid != null && solid.Faces.Size > 0 && solid.Volume > 1e-9;
    }
}
