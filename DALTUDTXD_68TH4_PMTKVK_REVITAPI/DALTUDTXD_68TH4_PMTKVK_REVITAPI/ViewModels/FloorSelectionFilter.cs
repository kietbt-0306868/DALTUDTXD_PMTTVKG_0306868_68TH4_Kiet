using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels
{
    public class FloorSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is Floor;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
