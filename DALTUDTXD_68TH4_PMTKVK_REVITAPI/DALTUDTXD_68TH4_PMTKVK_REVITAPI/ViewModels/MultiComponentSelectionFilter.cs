using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels
{
    public class MultiComponentSelectionFilter : ISelectionFilter
    {
        private readonly bool _allowBeam;
        private readonly bool _allowSlab;
        private readonly bool _allowColumn;
        private readonly bool _allowFoundation;

        public MultiComponentSelectionFilter(bool allowBeam, bool allowSlab, bool allowColumn, bool allowFoundation)
        {
            _allowBeam = allowBeam;
            _allowSlab = allowSlab;
            _allowColumn = allowColumn;
            _allowFoundation = allowFoundation;
        }

        public bool AllowElement(Element elem)
        {
            if (elem.Category != null)
            {
                var category = elem.Category.BuiltInCategory;

                if (category == BuiltInCategory.OST_Floors)
                {
                    bool isFoundationSlab = false;
                    if (elem is Floor floor)
                    {
                        var doc = floor.Document;
                        if (doc.GetElement(floor.GetTypeId()) as FloorType is FloorType floorType && floorType.IsFoundationSlab)
                        {
                            isFoundationSlab = true;
                        }
                    }

                    if (_allowFoundation && isFoundationSlab) return true;
                    if (_allowSlab && !isFoundationSlab) return true;
                }
                
                if (_allowFoundation && category == BuiltInCategory.OST_StructuralFoundation)
                {
                    return true;
                }
                if (_allowBeam && category == BuiltInCategory.OST_StructuralFraming)
                {
                    return true;
                }
                if (_allowColumn && category == BuiltInCategory.OST_StructuralColumns)
                {
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
