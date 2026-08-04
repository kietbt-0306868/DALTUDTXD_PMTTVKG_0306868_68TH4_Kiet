using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels
{
    public class FloorSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            if (elem.Category != null)
            {
                var cat = elem.Category.BuiltInCategory;
                
                // Only allow OST_Floors
                if (cat == BuiltInCategory.OST_Floors)
                {
                    // Exclude if it's a Foundation Slab
                    if (elem is Floor floor)
                    {
                        var doc = floor.Document;
                        if (doc.GetElement(floor.GetTypeId()) as FloorType is FloorType floorType && floorType.IsFoundationSlab)
                        {
                            return false;
                        }
                    }
                    return true;
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
