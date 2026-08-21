namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models
{
    public class FloorElementModel
    {
        public string UniqueId { get; set; }
        public string ElementId { get; set; }
        public StructuralComponentType ComponentType { get; set; } = StructuralComponentType.Slab;
        public string FamilyTypeName { get; set; }
        public string Level { get; set; }
        public double Area { get; set; }
        public double Perimeter { get; set; }
        public double Thickness { get; set; }
        public double StoryHeight { get; set; }
        public double Elevation { get; set; }
        public string ComponentSummary { get; set; }
        public ComponentIntersectionInfo IntersectionInfo { get; set; }
        public string FormworkName { get; set; }
        public string ConstructionGroup { get; set; }
        public string ConstructionSequence { get; set; }
        public string WorkflowStatus { get; set; }

        public string ComponentTypeLabel
        {
            get
            {
                switch (ComponentType)
                {
                    case StructuralComponentType.Foundation: return "MÓNG";
                    case StructuralComponentType.Beam: return "DẦM";
                    case StructuralComponentType.Column: return "CỘT";
                    default: return "SÀN";
                }
            }
        }

        public string CalculationGeometrySummary
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(ComponentSummary)) return ComponentSummary;
                switch (ComponentType)
                {
                    case StructuralComponentType.Beam:
                        return $"Dầm {Elevation:F0}×{Thickness:F0} mm · L={StoryHeight:F0} mm";
                    case StructuralComponentType.Column:
                        return $"Cột {Elevation:F0}×{Thickness:F0} mm · H={StoryHeight:F0} mm";
                    case StructuralComponentType.Foundation:
                        return $"Móng · {Area:F2} m² · dày {Thickness:F0} mm";
                    default:
                        return $"Sàn · {Area:F2} m² · dày {Thickness:F0} mm";
                }
            }
        }

        public string DisplaySummary
        {
            get
            {
                string summary = string.IsNullOrWhiteSpace(ComponentSummary)
                    ? $"Diện tích: {Area:F2} m² · Chiều dày: {Thickness:F0} mm"
                    : ComponentSummary;
                return IntersectionInfo == null || IntersectionInfo.IntersectingElementUniqueIds.Count == 0
                    ? AppendWorkflow(summary)
                    : $"{summary} · {IntersectionInfo.IntersectingElementUniqueIds.Count} giao · " +
                      $"ván ròng {IntersectionInfo.NetPlywoodAreaM2:F2} m²" +
                      (string.IsNullOrWhiteSpace(FormworkName) ? string.Empty : $" · {FormworkName}");
            }
        }

        private string AppendWorkflow(string summary)
        {
            if (string.IsNullOrWhiteSpace(FormworkName)) return summary;
            return $"{summary} · {FormworkName}";
        }
    }
}
