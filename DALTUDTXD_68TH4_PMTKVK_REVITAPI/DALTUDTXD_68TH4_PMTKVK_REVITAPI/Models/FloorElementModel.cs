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

        public string DisplaySummary
        {
            get
            {
                string summary = string.IsNullOrWhiteSpace(ComponentSummary)
                    ? $"Diện tích: {Area:F2} m² · Chiều dày: {Thickness:F0} mm"
                    : ComponentSummary;
                return IntersectionInfo == null || IntersectionInfo.IntersectingElementUniqueIds.Count == 0
                    ? summary
                    : $"{summary} · {IntersectionInfo.IntersectingElementUniqueIds.Count} giao · " +
                      $"ván ròng {IntersectionInfo.NetPlywoodAreaM2:F2} m²";
            }
        }
    }
}
