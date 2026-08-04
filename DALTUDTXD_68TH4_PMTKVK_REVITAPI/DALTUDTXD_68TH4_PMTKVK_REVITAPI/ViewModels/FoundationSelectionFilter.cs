using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels
{
    public class FoundationSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            if (elem.Category != null)
            {
                var cat = elem.Category.BuiltInCategory;
                
                // Mat foundation (Pile caps) or Isolated/Wall footings
                if (cat == BuiltInCategory.OST_StructuralFoundation)
                {
                    return true;
                }

                // If it's a Floor but configured as a Foundation Slab
                if (cat == BuiltInCategory.OST_Floors && elem is Floor floor)
                {
                    var doc = floor.Document;
                    if (doc.GetElement(floor.GetTypeId()) as FloorType is FloorType floorType && floorType.IsFoundationSlab)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
