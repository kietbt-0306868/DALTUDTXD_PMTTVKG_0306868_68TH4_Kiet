using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Linq;
using System.Windows;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Services;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels
{
    public class TinhToanViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly SharedDataViewModel _SharedDataViewModel;
        
        private string _isSystemSafeColor;
        private string _isSystemSafeBorderColor;
        private string _isSystemSafeIconColor;
        private string _isSystemSafeTextColor;
        private string _systemSafeTitle;
        private string _systemSafeDescription;

        private string _plywoodStatusColor;
        private string _plywoodStatusText;
        private string _plywoodStatusTextColor;
        private string _plywoodStressActual;
        private string _plywoodStressLimit;
        private double _plywoodStressRatio;
        private string _plywoodDeflectionActual;
        private string _plywoodDeflectionLimit;
        private double _plywoodDeflectionRatio;

        private string _joist1StatusColor;
        private string _joist1StatusText;
        private string _joist1StatusTextColor;
        private string _joist1StressActual;
        private string _joist1StressLimit;
        private double _joist1StressRatio;
        private string _joist1DeflectionActual;
        private string _joist1DeflectionLimit;
        private double _joist1DeflectionRatio;

        private string _joist2StatusColor;
        private string _joist2StatusText;
        private string _joist2StatusTextColor;
        private string _joist2StressActual;
        private string _joist2StressLimit;
        private double _joist2StressRatio;
        private string _joist2DeflectionActual;
        private string _joist2DeflectionLimit;
        private double _joist2DeflectionRatio;

        private string _supportStatusColor;
        private string _supportStatusText;
        private string _supportStatusTextColor;
        private string _supportLoadActual;
        private string _supportLoadLimit;
        private double _supportLoadRatio;
        private string _supportSlendernessFactor;

        private string _plywoodStressColor;
        private string _plywoodStressTextColor;
        private string _plywoodDeflectionColor;
        private string _plywoodDeflectionTextColor;

        private string _joist1StressColor;
        private string _joist1StressTextColor;
        private string _joist1DeflectionColor;
        private string _joist1DeflectionTextColor;
        private string _joist1ShearColor;
        private string _joist1ShearTextColor;

        private string _joist2StressColor;
        private string _joist2StressTextColor;
        private string _joist2DeflectionColor;
        private string _joist2DeflectionTextColor;
        private string _joist2ShearColor;
        private string _joist2ShearTextColor;

        private string _supportLoadColor;
        private string _supportLoadTextColor;

        private string _revitVersionStatus;
        private readonly IDialogService _dialogService;
        private List<FloorElementModel> _floorElements;

        public event PropertyChangedEventHandler PropertyChanged;

        public TinhToanViewModel(SharedDataViewModel SharedDataViewModel, IDialogService dialogService = null)
        {
            _SharedDataViewModel = SharedDataViewModel ?? throw new ArgumentNullException(nameof(SharedDataViewModel));
            _dialogService = dialogService ?? new MessageBoxDialogService();

            _SharedDataViewModel.PropertyChanged += OnSharedDataViewModelPropertyChanged;
            _SharedDataViewModel.SlabSelectionCompleted += OnSlabSelectionCompleted;

            _revitVersionStatus = "Revit API - Kiểm tra ứng suất và độ võng";

            RecalculateCommand = new RelayCommand(o => ExecuteRecalculate());
            ChangeTabCommand = new RelayCommand(ExecuteChangeTab);
            SelectFloorCommand = new RelayCommand(o => _SharedDataViewModel.ExecuteSelectSlabFromTinhToan());
            SweepSelectFloorCommand = new RelayCommand(o => _SharedDataViewModel.ExecuteSweepSelectSlabFromTinhToan());
            ShowFloorDetailsCommand = new RelayCommand(ExecuteShowFloorDetails);
            AutoFixPropsCommand = new RelayCommand(o => ExecuteAutoFixProps());

            ExecuteRecalculate();

            if (_SharedDataViewModel.LatestSelectionContext != null)
            {
                OnSlabSelectionCompleted(this, _SharedDataViewModel.LatestSelectionContext);
            }
        }

        // Các thuộc tính
        public string IsSystemSafeColor { get => _isSystemSafeColor; set { _isSystemSafeColor = value; OnPropertyChanged(); } }
        public string IsSystemSafeBorderColor { get => _isSystemSafeBorderColor; set { _isSystemSafeBorderColor = value; OnPropertyChanged(); } }
        public string IsSystemSafeIconColor { get => _isSystemSafeIconColor; set { _isSystemSafeIconColor = value; OnPropertyChanged(); } }
        public string IsSystemSafeTextColor { get => _isSystemSafeTextColor; set { _isSystemSafeTextColor = value; OnPropertyChanged(); } }
        public string SystemSafeTitle { get => _systemSafeTitle; set { _systemSafeTitle = value; OnPropertyChanged(); } }
        public string SystemSafeDescription { get => _systemSafeDescription; set { _systemSafeDescription = value; OnPropertyChanged(); } }

        private string _selectedFloorInfo = "Đang hiển thị kết quả cho: Toàn bộ / Sàn mặc định";
        public string SelectedFloorInfo { get => _selectedFloorInfo; set { _selectedFloorInfo = value; OnPropertyChanged(); } }

        // Ván mặt
        public string PlywoodStatusColor { get => _plywoodStatusColor; set { _plywoodStatusColor = value; OnPropertyChanged(); } }
        public string PlywoodStatusText { get => _plywoodStatusText; set { _plywoodStatusText = value; OnPropertyChanged(); } }
        public string PlywoodStatusTextColor { get => _plywoodStatusTextColor; set { _plywoodStatusTextColor = value; OnPropertyChanged(); } }
        public string PlywoodStressActual { get => _plywoodStressActual; set { _plywoodStressActual = value; OnPropertyChanged(); } }
        public string PlywoodStressLimit { get => _plywoodStressLimit; set { _plywoodStressLimit = value; OnPropertyChanged(); } }
        public double PlywoodStressRatio { get => _plywoodStressRatio; set { _plywoodStressRatio = value; OnPropertyChanged(); } }
        public string PlywoodDeflectionActual { get => _plywoodDeflectionActual; set { _plywoodDeflectionActual = value; OnPropertyChanged(); } }
        public string PlywoodDeflectionLimit { get => _plywoodDeflectionLimit; set { _plywoodDeflectionLimit = value; OnPropertyChanged(); } }
        public double PlywoodDeflectionRatio { get => _plywoodDeflectionRatio; set { _plywoodDeflectionRatio = value; OnPropertyChanged(); } }

        // Xà gồ phụ
        public string Joist1StatusColor { get => _joist1StatusColor; set { _joist1StatusColor = value; OnPropertyChanged(); } }
        public string Joist1StatusText { get => _joist1StatusText; set { _joist1StatusText = value; OnPropertyChanged(); } }
        public string Joist1StatusTextColor { get => _joist1StatusTextColor; set { _joist1StatusTextColor = value; OnPropertyChanged(); } }
        public string Joist1StressActual { get => _joist1StressActual; set { _joist1StressActual = value; OnPropertyChanged(); } }
        public string Joist1StressLimit { get => _joist1StressLimit; set { _joist1StressLimit = value; OnPropertyChanged(); } }
        public double Joist1StressRatio { get => _joist1StressRatio; set { _joist1StressRatio = value; OnPropertyChanged(); } }
        public string Joist1DeflectionActual { get => _joist1DeflectionActual; set { _joist1DeflectionActual = value; OnPropertyChanged(); } }
        public string Joist1DeflectionLimit { get => _joist1DeflectionLimit; set { _joist1DeflectionLimit = value; OnPropertyChanged(); } }
        public double Joist1DeflectionRatio { get => _joist1DeflectionRatio; set { _joist1DeflectionRatio = value; OnPropertyChanged(); } }

        private string _joist1ShearActual;
        private string _joist1ShearLimit;
        private double _joist1ShearRatio;
        public string Joist1ShearActual { get => _joist1ShearActual; set { _joist1ShearActual = value; OnPropertyChanged(); } }
        public string Joist1ShearLimit { get => _joist1ShearLimit; set { _joist1ShearLimit = value; OnPropertyChanged(); } }
        public double Joist1ShearRatio { get => _joist1ShearRatio; set { _joist1ShearRatio = value; OnPropertyChanged(); } }

        // Xà gồ chính
        public string Joist2StatusColor { get => _joist2StatusColor; set { _joist2StatusColor = value; OnPropertyChanged(); } }
        public string Joist2StatusText { get => _joist2StatusText; set { _joist2StatusText = value; OnPropertyChanged(); } }
        public string Joist2StatusTextColor { get => _joist2StatusTextColor; set { _joist2StatusTextColor = value; OnPropertyChanged(); } }
        public string Joist2StressActual { get => _joist2StressActual; set { _joist2StressActual = value; OnPropertyChanged(); } }
        public string Joist2StressLimit { get => _joist2StressLimit; set { _joist2StressLimit = value; OnPropertyChanged(); } }
        public double Joist2StressRatio { get => _joist2StressRatio; set { _joist2StressRatio = value; OnPropertyChanged(); } }
        public string Joist2DeflectionActual { get => _joist2DeflectionActual; set { _joist2DeflectionActual = value; OnPropertyChanged(); } }
        public string Joist2DeflectionLimit { get => _joist2DeflectionLimit; set { _joist2DeflectionLimit = value; OnPropertyChanged(); } }
        public double Joist2DeflectionRatio { get => _joist2DeflectionRatio; set { _joist2DeflectionRatio = value; OnPropertyChanged(); } }

        private string _joist2ShearActual;
        private string _joist2ShearLimit;
        private double _joist2ShearRatio;
        public string Joist2ShearActual { get => _joist2ShearActual; set { _joist2ShearActual = value; OnPropertyChanged(); } }
        public string Joist2ShearLimit { get => _joist2ShearLimit; set { _joist2ShearLimit = value; OnPropertyChanged(); } }
        public double Joist2ShearRatio { get => _joist2ShearRatio; set { _joist2ShearRatio = value; OnPropertyChanged(); } }

        // Cây chống
        public string SupportStatusColor { get => _supportStatusColor; set { _supportStatusColor = value; OnPropertyChanged(); } }
        public string SupportStatusText { get => _supportStatusText; set { _supportStatusText = value; OnPropertyChanged(); } }
        public string SupportStatusTextColor { get => _supportStatusTextColor; set { _supportStatusTextColor = value; OnPropertyChanged(); } }
        public string SupportLoadActual { get => _supportLoadActual; set { _supportLoadActual = value; OnPropertyChanged(); } }
        public string SupportLoadLimit { get => _supportLoadLimit; set { _supportLoadLimit = value; OnPropertyChanged(); } }
        public double SupportLoadRatio { get => _supportLoadRatio; set { _supportLoadRatio = value; OnPropertyChanged(); } }
        public string SupportSlendernessFactor { get => _supportSlendernessFactor; set { _supportSlendernessFactor = value; OnPropertyChanged(); } }

        // Màu sắc chi tiết (Progress Bar & Text)
        public string PlywoodStressColor { get => _plywoodStressColor; set { _plywoodStressColor = value; OnPropertyChanged(); } }
        public string PlywoodStressTextColor { get => _plywoodStressTextColor; set { _plywoodStressTextColor = value; OnPropertyChanged(); } }
        public string PlywoodDeflectionColor { get => _plywoodDeflectionColor; set { _plywoodDeflectionColor = value; OnPropertyChanged(); } }
        public string PlywoodDeflectionTextColor { get => _plywoodDeflectionTextColor; set { _plywoodDeflectionTextColor = value; OnPropertyChanged(); } }

        public string Joist1StressColor { get => _joist1StressColor; set { _joist1StressColor = value; OnPropertyChanged(); } }
        public string Joist1StressTextColor { get => _joist1StressTextColor; set { _joist1StressTextColor = value; OnPropertyChanged(); } }
        public string Joist1ShearColor { get => _joist1ShearColor; set { _joist1ShearColor = value; OnPropertyChanged(); } }
        public string Joist1ShearTextColor { get => _joist1ShearTextColor; set { _joist1ShearTextColor = value; OnPropertyChanged(); } }
        public string Joist1DeflectionColor { get => _joist1DeflectionColor; set { _joist1DeflectionColor = value; OnPropertyChanged(); } }
        public string Joist1DeflectionTextColor { get => _joist1DeflectionTextColor; set { _joist1DeflectionTextColor = value; OnPropertyChanged(); } }

        public string Joist2StressColor { get => _joist2StressColor; set { _joist2StressColor = value; OnPropertyChanged(); } }
        public string Joist2StressTextColor { get => _joist2StressTextColor; set { _joist2StressTextColor = value; OnPropertyChanged(); } }
        public string Joist2ShearColor { get => _joist2ShearColor; set { _joist2ShearColor = value; OnPropertyChanged(); } }
        public string Joist2ShearTextColor { get => _joist2ShearTextColor; set { _joist2ShearTextColor = value; OnPropertyChanged(); } }
        public string Joist2DeflectionColor { get => _joist2DeflectionColor; set { _joist2DeflectionColor = value; OnPropertyChanged(); } }
        public string Joist2DeflectionTextColor { get => _joist2DeflectionTextColor; set { _joist2DeflectionTextColor = value; OnPropertyChanged(); } }

        public string SupportLoadColor { get => _supportLoadColor; set { _supportLoadColor = value; OnPropertyChanged(); } }
        public string SupportLoadTextColor { get => _supportLoadTextColor; set { _supportLoadTextColor = value; OnPropertyChanged(); } }

        public string RevitVersionStatus { get => _revitVersionStatus; set { _revitVersionStatus = value; OnPropertyChanged(); } }
        
        public FrameworkElement ProjectPreview { get => _SharedDataViewModel.TinhToanPreviewControl; }

        public List<FloorElementModel> FloorElements { get => _floorElements; set { _floorElements = value; OnPropertyChanged(); } }

        public ICommand RecalculateCommand { get; }
        public ICommand ChangeTabCommand { get; }
        public ICommand SelectFloorCommand { get; }
        public ICommand SweepSelectFloorCommand { get; }
        public ICommand ShowFloorDetailsCommand { get; }
        public ICommand AutoFixPropsCommand { get; }

        public void Dispose()
        {
            _SharedDataViewModel.PropertyChanged -= OnSharedDataViewModelPropertyChanged;
            _SharedDataViewModel.SlabSelectionCompleted -= OnSlabSelectionCompleted;
        }




        private void ExecuteShowFloorDetails(object parameter)
        {
            if (parameter is FloorElementModel floorModel)
            {
                var floorModels = new List<FloorElementModel> { floorModel };
                _SharedDataViewModel.FloorElements = floorModels;
                _SharedDataViewModel.SelectedFloorDetailName = floorModel.FamilyTypeName;
                _SharedDataViewModel.SlabThickness = floorModel.Thickness;
                
                ExecuteRecalculate();
                SelectedFloorInfo = $"Sàn: {floorModel.FamilyTypeName} (Dày: {floorModel.Thickness} mm | DT: {floorModel.Area:F1} m²)";
                ExecuteShowMultipleFloorDetails(new List<string> { floorModel.UniqueId });
            }
        }

        private void ExecuteShowMultipleFloorDetails(List<string> uniqueIds)
        {
            if (uniqueIds == null || uniqueIds.Count == 0) return;
            
            var validIds = uniqueIds.Where(id => !string.IsNullOrEmpty(id)).ToList();
            if (validIds.Count == 0) return;

            _SharedDataViewModel.RenderPreviewGraphics(validIds, control => 
            {
                _SharedDataViewModel.TinhToanPreviewControl = control;
            });
        }

        private void ExecuteRecalculate()
        {
            var p = new Models.CalculationParameters
            {
                WoodStrength = _SharedDataViewModel.WoodStrength > 0 ? _SharedDataViewModel.WoodStrength : 15.0,
                PlywoodBendingStrength = _SharedDataViewModel.PlywoodBendingStrength > 0 ? _SharedDataViewModel.PlywoodBendingStrength : 10.0,
                WoodShearStrength = _SharedDataViewModel.WoodShearStrength > 0 ? _SharedDataViewModel.WoodShearStrength : 2.0,
                ElasticModulus = _SharedDataViewModel.ElasticModulus,
                ConcreteWeight = _SharedDataViewModel.ConcreteWeight > 0 ? _SharedDataViewModel.ConcreteWeight : 25.0,
                WoodFormworkWeight = _SharedDataViewModel.WoodFormworkWeight,
                ConstructionLiveLoad = _SharedDataViewModel.ConstructionLiveLoad,
                DynamicImpactFactor = _SharedDataViewModel.DynamicImpactFactor > 0 ? _SharedDataViewModel.DynamicImpactFactor : 1.3,
                DeadLoadSafetyFactor = _SharedDataViewModel.DeadLoadSafetyFactor,
                LiveLoadSafetyFactor = _SharedDataViewModel.LiveLoadSafetyFactor,
                MoistureFactor = _SharedDataViewModel.MoistureFactor,
                TemperatureFactor = _SharedDataViewModel.TemperatureFactor,
                SlabDeflectionLimitRatio = Models.FormworkCalculationService.ParseFraction(_SharedDataViewModel.SlabDeflectionLimit),
                JoistDeflectionLimitRatio = Models.FormworkCalculationService.ParseFraction(_SharedDataViewModel.JoistDeflectionLimit),
                SlabThickness = _SharedDataViewModel.SlabThickness,
                VanMatThickness = _SharedDataViewModel.VanMatThickness,
                XaGoPhuWidth = _SharedDataViewModel.XaGoPhuWidth,
                XaGoPhuHeight = _SharedDataViewModel.XaGoPhuHeight,
                XaGoChinhWidth = _SharedDataViewModel.XaGoChinhWidth,
                XaGoChinhHeight = _SharedDataViewModel.XaGoChinhHeight,
                CayChongDiameter = _SharedDataViewModel.CayChongDiameter,
                CayChongThickness = _SharedDataViewModel.CayChongThickness,
                CayChongHeight = _SharedDataViewModel.CayChongHeight,
                SoNhipVanMat = _SharedDataViewModel.SoNhipVanMat,
                SoNhipXaGoPhu = _SharedDataViewModel.SoNhipXaGoPhu,
                SoNhipXaGoChinh = _SharedDataViewModel.SoNhipXaGoChinh,
                Joist1Spacing = _SharedDataViewModel.Joist1Spacing > 0 ? _SharedDataViewModel.Joist1Spacing : 400.0,
                Joist2Spacing = _SharedDataViewModel.Joist2Spacing > 0 ? _SharedDataViewModel.Joist2Spacing : 1000.0,
                SupportSpacing = _SharedDataViewModel.SupportSpacing > 0 ? _SharedDataViewModel.SupportSpacing : 1000.0
            };

            var res = Models.FormworkCalculationService.CheckSafety(p);

            // 1. Ván mặt
            PlywoodStressActual = $"{res.Plywood.StressResult.GiaTriThucTe:F2}";
            PlywoodStressLimit = $"{res.Plywood.StressResult.GiaTriChoPhep:F2}";
            PlywoodStressRatio = res.Plywood.StressResult.PhanTramSuDung;
            PlywoodDeflectionActual = $"{res.Plywood.DeflectionResult.GiaTriThucTe:F2}";
            PlywoodDeflectionLimit = $"{res.Plywood.DeflectionResult.GiaTriChoPhep:F2}";
            PlywoodDeflectionRatio = res.Plywood.DeflectionResult.PhanTramSuDung;
            
            PlywoodStressColor = res.Plywood.StressResult.IsThoaman ? "#10B981" : "#EF4444";
            PlywoodStressTextColor = res.Plywood.StressResult.IsThoaman ? "#2C3E50" : "#EF4444";
            PlywoodDeflectionColor = res.Plywood.DeflectionResult.IsThoaman ? "#10B981" : "#EF4444";
            PlywoodDeflectionTextColor = res.Plywood.DeflectionResult.IsThoaman ? "#2C3E50" : "#EF4444";

            bool isPlywoodOk = res.Plywood.IsOk;
            PlywoodStatusColor = isPlywoodOk ? "#D1FAE5" : "#FEE2E2";
            PlywoodStatusText = isPlywoodOk ? "ĐẠT" : "KHÔNG ĐẠT";
            PlywoodStatusTextColor = isPlywoodOk ? "#065F46" : "#991B1B";

            // 2. Xà gồ phụ
            Joist1StressActual = $"{res.Joist1.StressResult.GiaTriThucTe:F2}";
            Joist1StressLimit = $"{res.Joist1.StressResult.GiaTriChoPhep:F2}";
            Joist1StressRatio = res.Joist1.StressResult.PhanTramSuDung;
            Joist1DeflectionActual = $"{res.Joist1.DeflectionResult.GiaTriThucTe:F2}";
            Joist1DeflectionLimit = $"{res.Joist1.DeflectionResult.GiaTriChoPhep:F2}";
            Joist1DeflectionRatio = res.Joist1.DeflectionResult.PhanTramSuDung;

            Joist1StressColor = res.Joist1.StressResult.IsThoaman ? "#10B981" : "#EF4444";
            Joist1StressTextColor = res.Joist1.StressResult.IsThoaman ? "#2C3E50" : "#EF4444";
            Joist1ShearActual = $"{res.Joist1.ShearResult.GiaTriThucTe:F2}";
            Joist1ShearLimit = $"{res.Joist1.ShearResult.GiaTriChoPhep:F2}";
            Joist1ShearRatio = res.Joist1.ShearResult.PhanTramSuDung;
            Joist1ShearColor = res.Joist1.ShearResult.IsThoaman ? "#10B981" : "#EF4444";
            Joist1ShearTextColor = res.Joist1.ShearResult.IsThoaman ? "#2C3E50" : "#EF4444";
            Joist1DeflectionColor = res.Joist1.DeflectionResult.IsThoaman ? "#10B981" : "#EF4444";
            Joist1DeflectionTextColor = res.Joist1.DeflectionResult.IsThoaman ? "#2C3E50" : "#EF4444";

            bool isJoist1Ok = res.Joist1.IsOk;
            Joist1StatusColor = isJoist1Ok ? "#D1FAE5" : "#FEE2E2";
            Joist1StatusText = isJoist1Ok ? "ĐẠT" : "KHÔNG ĐẠT";
            Joist1StatusTextColor = isJoist1Ok ? "#065F46" : "#991B1B";

            // 3. Xà gồ chính
            Joist2StressActual = $"{res.Joist2.StressResult.GiaTriThucTe:F2}";
            Joist2StressLimit = $"{res.Joist2.StressResult.GiaTriChoPhep:F2}";
            Joist2StressRatio = res.Joist2.StressResult.PhanTramSuDung;
            Joist2DeflectionActual = $"{res.Joist2.DeflectionResult.GiaTriThucTe:F2}";
            Joist2DeflectionLimit = $"{res.Joist2.DeflectionResult.GiaTriChoPhep:F2}";
            Joist2DeflectionRatio = res.Joist2.DeflectionResult.PhanTramSuDung;

            Joist2StressColor = res.Joist2.StressResult.IsThoaman ? "#10B981" : "#EF4444";
            Joist2StressTextColor = res.Joist2.StressResult.IsThoaman ? "#2C3E50" : "#EF4444";
            Joist2ShearActual = $"{res.Joist2.ShearResult.GiaTriThucTe:F2}";
            Joist2ShearLimit = $"{res.Joist2.ShearResult.GiaTriChoPhep:F2}";
            Joist2ShearRatio = res.Joist2.ShearResult.PhanTramSuDung;
            Joist2ShearColor = res.Joist2.ShearResult.IsThoaman ? "#10B981" : "#EF4444";
            Joist2ShearTextColor = res.Joist2.ShearResult.IsThoaman ? "#2C3E50" : "#EF4444";
            Joist2DeflectionColor = res.Joist2.DeflectionResult.IsThoaman ? "#10B981" : "#EF4444";
            Joist2DeflectionTextColor = res.Joist2.DeflectionResult.IsThoaman ? "#2C3E50" : "#EF4444";

            bool isJoist2Ok = res.Joist2.IsOk;
            Joist2StatusColor = isJoist2Ok ? "#D1FAE5" : "#FEE2E2";
            Joist2StatusText = isJoist2Ok ? "ĐẠT" : "KHÔNG ĐẠT";
            Joist2StatusTextColor = isJoist2Ok ? "#065F46" : "#991B1B";

            // 4. Cây chống
            SupportLoadActual = $"{res.Support.StressResult.GiaTriThucTe:F2}";
            SupportLoadLimit = $"{res.Support.StressResult.GiaTriChoPhep:F2}";
            SupportLoadRatio = res.Support.StressResult.PhanTramSuDung;
            
            SupportLoadColor = res.Support.StressResult.IsThoaman ? "#10B981" : "#EF4444";
            SupportLoadTextColor = res.Support.StressResult.IsThoaman ? "#2C3E50" : "#EF4444";

            SupportSlendernessFactor = res.SupportPhi.ToString("F2");
            
            bool isSupportOk = res.Support.IsOk;
            SupportStatusColor = isSupportOk ? "#D1FAE5" : "#FEE2E2";
            SupportStatusText = isSupportOk ? "ĐẠT" : "KHÔNG ĐẠT";
            SupportStatusTextColor = isSupportOk ? "#065F46" : "#991B1B";

            bool isSystemOk = res.IsSystemOk;
            if (isSystemOk)
            {
                IsSystemSafeColor = "#D1FAE5";
                IsSystemSafeBorderColor = "#10B981";
                IsSystemSafeIconColor = "#10B981";
                IsSystemSafeTextColor = "#065F46";
                SystemSafeTitle = "HỆ THỐNG ĐẠT YÊU CẦU AN TOÀN KỸ THUẬT";
                
                string tieuChuanStr = _SharedDataViewModel.SelectedTieuChuan == 0 ? "TCVN 9356:2012" : 
                                     (_SharedDataViewModel.SelectedTieuChuan == 1 ? "EN 1995-1-1 (Eurocode 5)" : "AWC NDS-2018");
                SystemSafeDescription = $"Tất cả các cấu kiện ván khuôn và đà giáo đều đảm bảo khả năng chịu lực và độ võng nằm trong giới hạn cho phép theo {tieuChuanStr}.";
            }
            else
            {
                IsSystemSafeColor = "#FEE2E2";
                IsSystemSafeBorderColor = "#EF4444";
                IsSystemSafeIconColor = "#EF4444";
                IsSystemSafeTextColor = "#991B1B";
                SystemSafeTitle = "HỆ THỐNG KHÔNG ĐẠT YÊU CẦU AN TOÀN KỸ THUẬT";
                
                var failedItems = new System.Collections.Generic.List<string>();
                if (!isPlywoodOk) failedItems.Add("Ván mặt");
                if (!isJoist1Ok) failedItems.Add("Xà gồ phụ");
                if (!isJoist2Ok) failedItems.Add("Xà gồ chính");
                if (!isSupportOk) failedItems.Add("Cây chống");
                
                SystemSafeDescription = $"Cấu kiện [{string.Join(", ", failedItems)}] KHÔNG ĐẠT do vượt quá giới hạn cho phép. Vui lòng giảm khoảng cách hoặc tăng tiết diện thanh ở thẻ [Thông số].";
            }

            RevitVersionStatus = "Đã cập nhật kiểm tra kỹ thuật lúc " + DateTime.Now.ToString("HH:mm:ss");
        }

        private void OnSharedDataViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_SharedDataViewModel.RevitVersionStatus)) return;
            
            if (e.PropertyName == nameof(_SharedDataViewModel.TinhToanPreviewControl))
            {
                OnPropertyChanged(nameof(ProjectPreview));
            }
            else if (e.PropertyName == nameof(SharedDataViewModel.WoodStrength) ||
                e.PropertyName == nameof(SharedDataViewModel.ElasticModulus) ||
                e.PropertyName == nameof(SharedDataViewModel.SlabThickness) ||
                e.PropertyName == nameof(SharedDataViewModel.VanMatThickness) ||
                e.PropertyName == nameof(SharedDataViewModel.XaGoPhuWidth) ||
                e.PropertyName == nameof(SharedDataViewModel.XaGoPhuHeight) ||
                e.PropertyName == nameof(SharedDataViewModel.XaGoChinhWidth) ||
                e.PropertyName == nameof(SharedDataViewModel.XaGoChinhHeight) ||
                e.PropertyName == nameof(SharedDataViewModel.CayChongDiameter) ||
                e.PropertyName == nameof(SharedDataViewModel.CayChongThickness) ||
                e.PropertyName == nameof(SharedDataViewModel.Joist1Spacing) ||
                e.PropertyName == nameof(SharedDataViewModel.Joist2Spacing) ||
                e.PropertyName == nameof(SharedDataViewModel.SupportSpacing) ||
                e.PropertyName == nameof(SharedDataViewModel.WoodFormworkWeight) ||
                e.PropertyName == nameof(SharedDataViewModel.ConcreteWeight) ||
                e.PropertyName == nameof(SharedDataViewModel.ConstructionLiveLoad) ||
                e.PropertyName == nameof(SharedDataViewModel.DeadLoadSafetyFactor) ||
                e.PropertyName == nameof(SharedDataViewModel.LiveLoadSafetyFactor) ||
                e.PropertyName == nameof(SharedDataViewModel.MoistureFactor) ||
                e.PropertyName == nameof(SharedDataViewModel.TemperatureFactor) ||
                e.PropertyName == nameof(SharedDataViewModel.SlabDeflectionLimit) ||
                e.PropertyName == nameof(SharedDataViewModel.JoistDeflectionLimit))
            {
                ExecuteRecalculate();
            }
            else if (e.PropertyName == nameof(_SharedDataViewModel.IsCalculated) && _SharedDataViewModel.IsCalculated)
            {
                ExecuteRecalculate();
                _SharedDataViewModel.IsCalculated = false; // Reset sau khi nhận
            }
        }

        /// <summary>
        /// Xử lý sự kiện đồng bộ khi người dùng chọn sàn từ bất kỳ trang nào.
        /// Cập nhật danh sách sàn, thông tin hiển thị, và tính toán lại.
        /// </summary>
        private void OnSlabSelectionCompleted(object sender, SlabSelectionEventArgs e)
        {
            FloorElements = e.FloorElements;
            SelectedFloorInfo = $"Đã chọn {e.FloorCount} sàn | Dày nhất: {e.MaxFloorName} ({e.SlabThickness:F0} mm) | DT: {e.SelectedSlabArea:F2} m²";
            ExecuteRecalculate();
            RevitVersionStatus = $"Đồng bộ từ Revit: {e.FloorCount} sàn - Cập nhật lúc {DateTime.Now:HH:mm:ss}";
        }

        private void ExecuteChangeTab(object parameter)
        {
            _SharedDataViewModel.ExecuteChangeTab(parameter);
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void ExecuteAutoFixProps()
        {
            var p = new Models.CalculationParameters
            {
                WoodStrength = _SharedDataViewModel.WoodStrength > 0 ? _SharedDataViewModel.WoodStrength : 15.0,
                PlywoodBendingStrength = _SharedDataViewModel.PlywoodBendingStrength > 0 ? _SharedDataViewModel.PlywoodBendingStrength : 10.0,
                WoodShearStrength = _SharedDataViewModel.WoodShearStrength > 0 ? _SharedDataViewModel.WoodShearStrength : 2.0,
                ElasticModulus = _SharedDataViewModel.ElasticModulus,
                ConcreteWeight = _SharedDataViewModel.ConcreteWeight > 0 ? _SharedDataViewModel.ConcreteWeight : 25.0,
                WoodFormworkWeight = _SharedDataViewModel.WoodFormworkWeight,
                ConstructionLiveLoad = _SharedDataViewModel.ConstructionLiveLoad,
                DynamicImpactFactor = _SharedDataViewModel.DynamicImpactFactor > 0 ? _SharedDataViewModel.DynamicImpactFactor : 1.3,
                DeadLoadSafetyFactor = _SharedDataViewModel.DeadLoadSafetyFactor,
                LiveLoadSafetyFactor = _SharedDataViewModel.LiveLoadSafetyFactor,
                MoistureFactor = _SharedDataViewModel.MoistureFactor,
                TemperatureFactor = _SharedDataViewModel.TemperatureFactor,
                SlabDeflectionLimitRatio = Models.FormworkCalculationService.ParseFraction(_SharedDataViewModel.SlabDeflectionLimit),
                JoistDeflectionLimitRatio = Models.FormworkCalculationService.ParseFraction(_SharedDataViewModel.JoistDeflectionLimit),
                SlabThickness = _SharedDataViewModel.SlabThickness,
                VanMatThickness = _SharedDataViewModel.VanMatThickness,
                XaGoPhuWidth = _SharedDataViewModel.XaGoPhuWidth,
                XaGoPhuHeight = _SharedDataViewModel.XaGoPhuHeight,
                XaGoChinhWidth = _SharedDataViewModel.XaGoChinhWidth,
                XaGoChinhHeight = _SharedDataViewModel.XaGoChinhHeight,
                CayChongDiameter = _SharedDataViewModel.CayChongDiameter,
                CayChongThickness = _SharedDataViewModel.CayChongThickness,
                CayChongHeight = _SharedDataViewModel.CayChongHeight,
                SoNhipVanMat = _SharedDataViewModel.SoNhipVanMat,
                SoNhipXaGoPhu = _SharedDataViewModel.SoNhipXaGoPhu,
                SoNhipXaGoChinh = _SharedDataViewModel.SoNhipXaGoChinh,
                Joist1Spacing = _SharedDataViewModel.Joist1Spacing > 0 ? _SharedDataViewModel.Joist1Spacing : 400.0,
                Joist2Spacing = _SharedDataViewModel.Joist2Spacing > 0 ? _SharedDataViewModel.Joist2Spacing : 1000.0,
                SupportSpacing = _SharedDataViewModel.SupportSpacing > 0 ? _SharedDataViewModel.SupportSpacing : 1000.0
            };

            double optimizedL3 = Models.FormworkCalculationService.FixSupportSpacing(p);
            _SharedDataViewModel.SupportSpacing = optimizedL3;
            // ExecuteRecalculate() is triggered automatically via PropertyChanged event
        }
    }
}
