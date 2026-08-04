using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels
{
    public class ColumnSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            if (elem.Category != null)
            {
                return elem.Category.BuiltInCategory == BuiltInCategory.OST_StructuralColumns;
            }
            return false;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
