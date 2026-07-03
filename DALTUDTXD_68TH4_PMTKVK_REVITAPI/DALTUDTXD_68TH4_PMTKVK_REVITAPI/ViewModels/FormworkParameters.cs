using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels
{
    public class FormworkParameters : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private double _woodStrength = 15.0;
        public double WoodStrength { get => _woodStrength; set { _woodStrength = value; OnPropertyChanged(); } }

        private double _plywoodBendingStrength = 10.0;
        public double PlywoodBendingStrength { get => _plywoodBendingStrength; set { _plywoodBendingStrength = value; OnPropertyChanged(); } }

        private double _woodShearStrength = 2.0;
        public double WoodShearStrength { get => _woodShearStrength; set { _woodShearStrength = value; OnPropertyChanged(); } }

        private double _elasticModulus = 9000.0;
        public double ElasticModulus { get => _elasticModulus; set { _elasticModulus = value; OnPropertyChanged(); } }

        private string _slabDeflectionLimit = "1/250";
        public string SlabDeflectionLimit { get => _slabDeflectionLimit; set { _slabDeflectionLimit = value; OnPropertyChanged(); } }

        private string _joistDeflectionLimit = "1/250";
        public string JoistDeflectionLimit { get => _joistDeflectionLimit; set { _joistDeflectionLimit = value; OnPropertyChanged(); } }

        private double _woodFormworkWeight = 0.30;
        public double WoodFormworkWeight { get => _woodFormworkWeight; set { _woodFormworkWeight = value; OnPropertyChanged(); } }

        private double _concreteWeight = 25.00;
        public double ConcreteWeight { get => _concreteWeight; set { _concreteWeight = value; OnPropertyChanged(); } }

        private double _constructionLiveLoad = 2.50;
        public double ConstructionLiveLoad { get => _constructionLiveLoad; set { _constructionLiveLoad = value; OnPropertyChanged(); } }

        private double _dynamicImpactFactor = 1.30;
        public double DynamicImpactFactor { get => _dynamicImpactFactor; set { _dynamicImpactFactor = value; OnPropertyChanged(); } }

        private double _deadLoadSafetyFactor = 1.20;
        public double DeadLoadSafetyFactor { get => _deadLoadSafetyFactor; set { _deadLoadSafetyFactor = value; OnPropertyChanged(); } }

        private double _liveLoadSafetyFactor = 1.30;
        public double LiveLoadSafetyFactor { get => _liveLoadSafetyFactor; set { _liveLoadSafetyFactor = value; OnPropertyChanged(); } }

        private double _moistureFactor = 0.85;
        public double MoistureFactor { get => _moistureFactor; set { _moistureFactor = value; OnPropertyChanged(); } }

        private double _temperatureFactor = 1.00;
        public double TemperatureFactor { get => _temperatureFactor; set { _temperatureFactor = value; OnPropertyChanged(); } }

        private double _joist1Spacing = 400.0;
        public double Joist1Spacing { get => _joist1Spacing; set { _joist1Spacing = value; OnPropertyChanged(); } }

        private double _joist2Spacing = 1000.0;
        public double Joist2Spacing { get => _joist2Spacing; set { _joist2Spacing = value; OnPropertyChanged(); } }

        private double _supportSpacing = 1000.0;
        public double SupportSpacing { get => _supportSpacing; set { _supportSpacing = value; OnPropertyChanged(); } }

        private double _slabThickness = 150.0;
        public double SlabThickness { get => _slabThickness; set { _slabThickness = value; OnPropertyChanged(); } }

        private double _vanMatThickness = 18.0;
        public double VanMatThickness { get => _vanMatThickness; set { _vanMatThickness = value; OnPropertyChanged(); } }

        private double _xaGoPhuWidth = 50.0;
        public double XaGoPhuWidth { get => _xaGoPhuWidth; set { _xaGoPhuWidth = value; OnPropertyChanged(); } }

        private double _xaGoPhuHeight = 100.0;
        public double XaGoPhuHeight { get => _xaGoPhuHeight; set { _xaGoPhuHeight = value; OnPropertyChanged(); } }

        private double _xaGoChinhWidth = 100.0;
        public double XaGoChinhWidth { get => _xaGoChinhWidth; set { _xaGoChinhWidth = value; OnPropertyChanged(); } }

        private double _xaGoChinhHeight = 100.0;
        public double XaGoChinhHeight { get => _xaGoChinhHeight; set { _xaGoChinhHeight = value; OnPropertyChanged(); } }

        private double _cayChongDiameter = 48.0;
        public double CayChongDiameter { get => _cayChongDiameter; set { _cayChongDiameter = value; OnPropertyChanged(); } }

        private double _cayChongThickness = 2.0;
        public double CayChongThickness { get => _cayChongThickness; set { _cayChongThickness = value; OnPropertyChanged(); } }

        private double _cayChongHeight = 3600.0;
        public double CayChongHeight { get => _cayChongHeight; set { _cayChongHeight = value; OnPropertyChanged(); } }

        private int _soNhipVanMat = 2;
        public int SoNhipVanMat { get => _soNhipVanMat; set { _soNhipVanMat = value; OnPropertyChanged(); } }

        private int _soNhipXaGoPhu = 3;
        public int SoNhipXaGoPhu { get => _soNhipXaGoPhu; set { _soNhipXaGoPhu = value; OnPropertyChanged(); } }

        private int _soNhipXaGoChinh = 3;
        public int SoNhipXaGoChinh { get => _soNhipXaGoChinh; set { _soNhipXaGoChinh = value; OnPropertyChanged(); } }

        private int _selectedTieuChuan = 0;
        public int SelectedTieuChuan { get => _selectedTieuChuan; set { _selectedTieuChuan = value; OnPropertyChanged(); } }
    }
}
