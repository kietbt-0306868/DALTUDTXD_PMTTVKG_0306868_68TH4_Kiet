namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models
{
    public class LevelModel
    {
        public string Name { get; set; }
        public double Elevation { get; set; }
        public string DisplayName => $"{Name} ({Elevation:N0} mm)";

        public override string ToString()
        {
            return Name;
        }
    }
}
