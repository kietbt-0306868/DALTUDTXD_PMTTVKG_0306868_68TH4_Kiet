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
    public sealed class CalculationCheckDisplay
    {
        public string Group { get; set; }
        public string Component { get; set; }
        public string Check { get; set; }
        public string Formula { get; set; }
        public string Demand { get; set; }
        public string Allowable { get; set; }
        public double Utilization { get; set; }
        public bool IsOk { get; set; }
        public string Status { get; set; }
        public string StatusBackground { get; set; }
        public string StatusForeground { get; set; }
        public string GroupAccent { get; set; }

        public static CalculationCheckDisplay From(string group, KetQuaTinhToan result)
        {
            return new CalculationCheckDisplay
            {
                Group = group,
                Component = result.TenCauKien,
                Check = result.NoiDungKiemTra,
                Formula = result.CongThucKiemTra,
                Demand = $"{result.GiaTriThucTe:F2} {result.DonVi}",
                Allowable = $"{result.GiaTriChoPhep:F2} {result.DonVi}",
                Utilization = result.PhanTramSuDung,
                IsOk = result.IsThoaman,
                Status = result.KetLuan,
                StatusBackground = result.IsThoaman ? "#D1FAE5" : "#FEE2E2",
                StatusForeground = result.IsThoaman ? "#065F46" : "#991B1B",
                GroupAccent = group.StartsWith("CỘT BIÊN", StringComparison.Ordinal)
                    ? "#F59E0B"
                    : group.StartsWith("CỘT GIỮA", StringComparison.Ordinal)
                        ? "#2563EB"
                        : "#ABB2B9"
            };
        }
    }

    public class TinhToanViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly SharedDataViewModel _SharedDataViewModel;
        
        private string _isSystemSafeColor;
        private string _isSystemSafeBorderColor;
        private string _isSystemSafeIconColor;
        private string _isSystemSafeTextColor;
        private string _systemSafeTitle;
        private string _systemSafeDescription;
        private string _columnPressureVerification;
        private string _columnSpanVerification;
        private string _columnConstructionVerification;
        private string _beamLoadVerification;
        private string _beamLoadPathVerification;
        private string _beamConstructionVerification;

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
        private List<CalculationCheckDisplay> _detailedChecks = new List<CalculationCheckDisplay>();
        private string _detailedCheckSummary;
        private string _autoDesignStatus = "Sẵn sàng tự động thiết kế theo các giới hạn cho phép.";
        private string _autoDesignStatusColor = "#475569";
        private List<string> _autoDesignDiffItems = new List<string>();
        private bool _isApplyingAutoDesign;
        private bool _isRecalculating;
        private bool _disposed;

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
            SelectFloorCommand = new RelayCommand(o => _SharedDataViewModel.ExecuteSelectComponentFromTinhToan());
            SweepSelectFloorCommand = new RelayCommand(o => _SharedDataViewModel.ExecuteSweepSelectComponentFromTinhToan());
            ShowFloorDetailsCommand = new RelayCommand(ExecuteShowFloorDetails);
            AutoFixPropsCommand = new RelayCommand(o => ExecuteAutoFixProps(), o => !_isApplyingAutoDesign);

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
        public string ColumnPressureVerification { get => _columnPressureVerification; set { _columnPressureVerification = value; OnPropertyChanged(); } }
        public string ColumnSpanVerification { get => _columnSpanVerification; set { _columnSpanVerification = value; OnPropertyChanged(); } }
        public string ColumnConstructionVerification { get => _columnConstructionVerification; set { _columnConstructionVerification = value; OnPropertyChanged(); } }
        public string ColumnVerificationVisibility => CurrentComponentType == StructuralComponentType.Column ? "Visible" : "Collapsed";
        public string BeamLoadVerification { get => _beamLoadVerification; set { _beamLoadVerification = value; OnPropertyChanged(); } }
        public string BeamLoadPathVerification { get => _beamLoadPathVerification; set { _beamLoadPathVerification = value; OnPropertyChanged(); } }
        public string BeamConstructionVerification { get => _beamConstructionVerification; set { _beamConstructionVerification = value; OnPropertyChanged(); } }
        public string BeamVerificationVisibility => CurrentComponentType == StructuralComponentType.Beam ? "Visible" : "Collapsed";

        private string _selectedFloorInfo = "Phạm vi tính: Sàn · Móng/Nền · Dầm · Cột";
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
        public List<CalculationCheckDisplay> DetailedChecks { get => _detailedChecks; set { _detailedChecks = value; OnPropertyChanged(); } }
        public string DetailedCheckSummary { get => _detailedCheckSummary; set { _detailedCheckSummary = value; OnPropertyChanged(); } }
        public string AutoDesignStatus { get => _autoDesignStatus; set { _autoDesignStatus = value; OnPropertyChanged(); } }
        public string AutoDesignStatusColor { get => _autoDesignStatusColor; set { _autoDesignStatusColor = value; OnPropertyChanged(); } }
        public List<string> AutoDesignDiffItems { get => _autoDesignDiffItems; set { _autoDesignDiffItems = value ?? new List<string>(); OnPropertyChanged(); OnPropertyChanged(nameof(AutoDesignDiffVisibility)); } }
        public System.Windows.Visibility AutoDesignDiffVisibility => AutoDesignDiffItems.Count > 0
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;

        public ICommand RecalculateCommand { get; }
        public ICommand ChangeTabCommand { get; }
        public ICommand SelectFloorCommand { get; }
        public ICommand SweepSelectFloorCommand { get; }
        public ICommand ShowFloorDetailsCommand { get; }
        public ICommand AutoFixPropsCommand { get; }

        public bool IsFilterSlabChecked { get => _SharedDataViewModel.IsFilterSlabChecked; set { _SharedDataViewModel.IsFilterSlabChecked = value; OnPropertyChanged(); } }
        public bool IsFilterFoundationChecked { get => _SharedDataViewModel.IsFilterFoundationChecked; set { _SharedDataViewModel.IsFilterFoundationChecked = value; OnPropertyChanged(); } }
        public bool IsFilterBeamChecked { get => _SharedDataViewModel.IsFilterBeamChecked; set { _SharedDataViewModel.IsFilterBeamChecked = value; OnPropertyChanged(); } }
        public bool IsFilterColumnChecked { get => _SharedDataViewModel.IsFilterColumnChecked; set { _SharedDataViewModel.IsFilterColumnChecked = value; OnPropertyChanged(); } }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
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

        public StructuralComponentType CurrentComponentType => _SharedDataViewModel.CurrentComponentType;

        public string Card1Title => CurrentComponentType == StructuralComponentType.Beam ? "VÁN ĐÁY DẦM" : 
                                    (CurrentComponentType == StructuralComponentType.Column ? "VÁN MẶT CỘT" : "VÁN MẶT");

        public string Card2Title => CurrentComponentType == StructuralComponentType.Beam ? "XÀ GỒ PHỤ ĐÁY DẦM" : 
                                    (CurrentComponentType == StructuralComponentType.Column ? "NẸP ĐỨNG 40×40" : "XÀ GỒ LỚP 1 (THANH PHỤ)");

        public string Card3Title => CurrentComponentType == StructuralComponentType.Beam ? "XÀ GỒ CHÍNH TRÊN ĐẦU U" :
                                    (CurrentComponentType == StructuralComponentType.Column
                                        ? (_SharedDataViewModel.ColumnYokeMaterialIndex == 0 ? "XÀ GỒ NGANG ĐÔI (TIMBER WALER)" : "GÔNG THÉP HỘP ĐÔI (DOUBLE YOKE)")
                                        : "XÀ GỒ LỚP 2 (THANH CHÍNH)");

        public string Card4Title => CurrentComponentType == StructuralComponentType.Column ? "CHỐNG XIÊN / CÁP TĂNG ĐƠ" : "HỆ CÂY CHỐNG ĐỨNG";

        public string Card3Visibility => "Visible";
        
        public string Card4Visibility => CurrentComponentType == StructuralComponentType.Foundation ? "Collapsed" : "Visible";

        public string Card4LoadLabel => CurrentComponentType == StructuralComponentType.Column ? "Lực dọc chống/neo (N_tt)" : "Lực dọc trục lên đầu cột (P_tt)";
        public string Card4FactorLabel => CurrentComponentType == StructuralComponentType.Column ? "Bố trí: phân bổ theo hướng có điểm đỡ" : "Hệ số uốn dọc φ:";

        private void ExecuteRecalculate()
        {
            // Geometry/classification refresh raises PropertyChanged events while a
            // calculation is still running. Those events must not recursively start
            // another calculation (the previous behavior exhausted the process stack).
            if (_isRecalculating || _disposed)
                return;

            _isRecalculating = true;
            try
            {
                ExecuteRecalculateCore();
            }
            finally
            {
                _isRecalculating = false;
            }
        }

        private void ExecuteRecalculateCore()
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
                SupportEffectiveLengthFactor = _SharedDataViewModel.SupportEffectiveLengthFactor,
                SupportRatedCapacity = _SharedDataViewModel.SupportRatedCapacity,
                SupportSlendernessLimit = _SharedDataViewModel.SupportSlendernessLimit,
                SupportBasePlateWidth = _SharedDataViewModel.SupportBasePlateWidth,
                SupportBasePlateDepth = _SharedDataViewModel.SupportBasePlateDepth,
                SupportBaseBearingAllowable = _SharedDataViewModel.SupportBaseBearingAllowable,
                SupportSolePlateWidth = _SharedDataViewModel.SupportSolePlateWidth,
                SupportSolePlateDepth = _SharedDataViewModel.SupportSolePlateDepth,
                SupportGroundBearingAllowable = _SharedDataViewModel.SupportGroundBearingAllowable,
                SupportHeadPlateWidth = _SharedDataViewModel.SupportHeadPlateWidth,
                SupportHeadPlateDepth = _SharedDataViewModel.SupportHeadPlateDepth,
                SupportHeadBearingAllowable = _SharedDataViewModel.SupportHeadBearingAllowable,
                SupportJackExtension = _SharedDataViewModel.SupportJackExtension,
                SupportJackExtensionLimit = _SharedDataViewModel.SupportJackExtensionLimit,
                SoNhipVanMat = _SharedDataViewModel.SoNhipVanMat,
                SoNhipXaGoPhu = _SharedDataViewModel.SoNhipXaGoPhu,
                SoNhipXaGoChinh = _SharedDataViewModel.SoNhipXaGoChinh,
                Joist1Spacing = _SharedDataViewModel.Joist1Spacing > 0 ? _SharedDataViewModel.Joist1Spacing : 400.0,
                Joist2Spacing = _SharedDataViewModel.Joist2Spacing > 0 ? _SharedDataViewModel.Joist2Spacing : 1000.0,
                SupportSpacing = _SharedDataViewModel.SupportSpacing > 0 ? _SharedDataViewModel.SupportSpacing : 1000.0,
                IsGroundLevel = _SharedDataViewModel.IsGroundLevel,
                ComponentType = _SharedDataViewModel.CurrentComponentType,
                
                // Beam
                BeamWidth = _SharedDataViewModel.BeamWidth,
                BeamDepth = _SharedDataViewModel.BeamDepth,
                BeamLength = _SharedDataViewModel.BeamLength,
                BeamSidePlywoodThickness = _SharedDataViewModel.BeamSidePlywoodThickness,
                BeamBottomPlywoodThickness = _SharedDataViewModel.BeamBottomPlywoodThickness,
                BeamSideYokeSpacing = _SharedDataViewModel.BeamSideYokeSpacing,
                BeamBottomYokeSpacing = _SharedDataViewModel.BeamBottomYokeSpacing,
                BeamPropSpacing = _SharedDataViewModel.BeamPropSpacing,
                BeamSecondaryJoistWidth = _SharedDataViewModel.BeamSecondaryJoistWidth,
                BeamSecondaryJoistDepth = _SharedDataViewModel.BeamSecondaryJoistDepth,
                BeamPrimaryWalerWidth = _SharedDataViewModel.BeamPrimaryWalerWidth,
                BeamPrimaryWalerDepth = _SharedDataViewModel.BeamPrimaryWalerDepth,
                BeamPrimaryWalerSpacing = _SharedDataViewModel.BeamPrimaryWalerSpacing,
                BeamScaffoldBayWidth = _SharedDataViewModel.BeamScaffoldBayWidth,
                BeamTieRodDiameter = _SharedDataViewModel.BeamTieRodDiameter,
                BeamTieRodSpacing = _SharedDataViewModel.BeamTieRodSpacing,
                BeamTieRodAllowableCapacity = _SharedDataViewModel.BeamTieRodAllowableCapacity,
                BeamSideBraceSpacing = _SharedDataViewModel.BeamSideBraceSpacing,
                BeamSideBraceAngle = _SharedDataViewModel.BeamSideBraceAngle,
                BeamSideBraceDiameter = _SharedDataViewModel.BeamSideBraceDiameter,
                BeamSideBraceThickness = _SharedDataViewModel.BeamSideBraceThickness,
                BeamSideBraceCountPerStation = _SharedDataViewModel.BeamSideBraceCountPerStation,
                BeamSideBraceAllowableCapacity = _SharedDataViewModel.BeamSideBraceAllowableCapacity,
                BeamPlacementModeIndex = _SharedDataViewModel.BeamPlacementModeIndex,
                BeamStrippingStrengthPercent = _SharedDataViewModel.BeamStrippingStrengthPercent,
                BeamVibratorOverlap = _SharedDataViewModel.BeamVibratorOverlap,
                
                // Column
                ColumnWidth = _SharedDataViewModel.ColumnWidth,
                ColumnDepth = _SharedDataViewModel.ColumnDepth,
                ColumnHeight = _SharedDataViewModel.ColumnHeight,
                ColumnPlywoodThickness = _SharedDataViewModel.ColumnPlywoodThickness,
                ColumnStiffenerWidth = _SharedDataViewModel.ColumnStiffenerWidth,
                ColumnStiffenerDepth = _SharedDataViewModel.ColumnStiffenerDepth,
                ColumnStiffenerSpacing = _SharedDataViewModel.ColumnStiffenerSpacing,
                ColumnYokeSpacing = _SharedDataViewModel.ColumnYokeSpacing,
                ColumnUseZonedYokeSpacing = _SharedDataViewModel.ColumnUseZonedYokeSpacing,
                ColumnFirstYokeOffset = _SharedDataViewModel.ColumnFirstYokeOffset,
                ColumnYokeBottomSpacing = _SharedDataViewModel.ColumnYokeBottomSpacing,
                ColumnYokeUpperSpacing = _SharedDataViewModel.ColumnYokeUpperSpacing,
                ColumnYokeTransitionHeight = _SharedDataViewModel.ColumnYokeTransitionHeight,
                ColumnLastYokeOffset = _SharedDataViewModel.ColumnLastYokeOffset,
                ColumnYokeWidth = _SharedDataViewModel.ColumnYokeWidth,
                ColumnYokeHeight = _SharedDataViewModel.ColumnYokeHeight,
                ColumnYokeThickness = _SharedDataViewModel.ColumnYokeThickness,
                ColumnYokePairCount = _SharedDataViewModel.ColumnYokePairCount,
                ColumnYokeMaterialIndex = _SharedDataViewModel.ColumnYokeMaterialIndex,
                ColumnTieRodDiameter = _SharedDataViewModel.ColumnTieRodDiameter,
                ColumnTieRodCountPerFace = _SharedDataViewModel.ColumnTieRodCountPerFace,
                ColumnTieRodAllowableCapacity = _SharedDataViewModel.ColumnTieRodAllowableCapacity,
                ColumnBraceCount = _SharedDataViewModel.ColumnBraceCount,
                ColumnBraceAngle = _SharedDataViewModel.ColumnBraceAngle,
                ColumnBraceAllowableCapacity = _SharedDataViewModel.ColumnBraceAllowableCapacity,
                ColumnBracePlacementModeIndex = _SharedDataViewModel.ColumnBracePlacementModeIndex,
                ColumnNeighbourTieThreshold = _SharedDataViewModel.ColumnNeighbourTieThreshold,
                ColumnIsBoundaryOrCorner = _SharedDataViewModel.ColumnIsBoundaryOrCorner,
                ColumnCableAllowableCapacity = _SharedDataViewModel.ColumnCableAllowableCapacity,
                ColumnTurnbuckleAllowableCapacity = _SharedDataViewModel.ColumnTurnbuckleAllowableCapacity,
                ColumnAnchorAllowableCapacity = _SharedDataViewModel.ColumnAnchorAllowableCapacity,
                ColumnAxisOffset = _SharedDataViewModel.ColumnAxisOffset,
                ColumnPlumbTargetTolerance = _SharedDataViewModel.ColumnPlumbTargetTolerance,
                ColumnFormworkTopExtension = _SharedDataViewModel.ColumnFormworkTopExtension,
                ColumnHoseBottomClearance = _SharedDataViewModel.ColumnHoseBottomClearance,
                ColumnPourLiftHeight = _SharedDataViewModel.ColumnPourLiftHeight,
                ColumnBeamStopOffset = _SharedDataViewModel.ColumnBeamStopOffset,
                ColumnStripTimeHours = _SharedDataViewModel.ColumnStripTimeHours,
                ColumnCuringDays = _SharedDataViewModel.ColumnCuringDays,
                ColumnUseWorkPlatform = _SharedDataViewModel.ColumnUseWorkPlatform,
                ColumnWorkPlatformWidth = _SharedDataViewModel.ColumnWorkPlatformWidth,
                ColumnGuardrailHeight = _SharedDataViewModel.ColumnGuardrailHeight,
                ConcretePourRate = _SharedDataViewModel.ConcretePourRate,
                ConcreteTemperature = _SharedDataViewModel.ConcreteTemperature
            };

            // The four checkboxes define both the 2D display filter and the calculation scope.
            // Previously they affected only the preview, while this method fell back to the
            // current component (Slab by default), causing every detailed check to say "SÀN".
            bool checkSlab = IsFilterSlabChecked;
            bool checkFoundation = IsFilterFoundationChecked;
            bool checkBeam = IsFilterBeamChecked;
            bool checkColumn = IsFilterColumnChecked;
            if (!checkSlab && !checkFoundation && !checkBeam && !checkColumn)
            {
                // Keep the module usable when every filter is cleared: calculate only the
                // explicitly active component instead of silently treating it as a slab.
                checkSlab = CurrentComponentType == StructuralComponentType.Slab;
                checkFoundation = CurrentComponentType == StructuralComponentType.Foundation;
                checkBeam = CurrentComponentType == StructuralComponentType.Beam;
                checkColumn = CurrentComponentType == StructuralComponentType.Column;
            }

            p.IsFloorSelected = checkSlab || checkFoundation;
            p.IsBeamSelected = checkBeam;
            p.IsColumnSelected = checkColumn;

            var calculationSets = BuildScopedCalculationSets(p, checkSlab, checkFoundation, checkBeam, checkColumn);
            bool originalGround = p.IsGroundLevel;

            string preferredGroup = CurrentComponentType == StructuralComponentType.Column ? "CỘT" :
                CurrentComponentType == StructuralComponentType.Beam ? "DẦM" :
                CurrentComponentType == StructuralComponentType.Foundation ? "MÓNG/NỀN" : "SÀN";
            Models.CalculationResult res = calculationSets.FirstOrDefault(set =>
                set.Key.StartsWith(preferredGroup, StringComparison.Ordinal)).Value;
            if (res == null)
            {
                // Summary cards continue to represent the active component even if its
                // category is temporarily hidden from the combined detailed-check list.
                p.IsGroundLevel = CurrentComponentType == StructuralComponentType.Foundation;
                res = CurrentComponentType == StructuralComponentType.Beam
                    ? Models.FormworkCalculationService.CheckBeamSafety(p)
                    : CurrentComponentType == StructuralComponentType.Column
                        ? Models.FormworkCalculationService.CheckColumnSafety(p)
                        : Models.FormworkCalculationService.CheckSafety(p);
                p.IsGroundLevel = originalGround;
            }

            DetailedChecks = calculationSets
                .SelectMany(set => set.Value.GetAllChecks().Select(check => CalculationCheckDisplay.From(set.Key, check)))
                .ToList();
            int failedDetailedChecks = DetailedChecks.Count(check => !check.IsOk);
            string groupBreakdown = string.Join(" · ", DetailedChecks
                .GroupBy(check => check.Group)
                .Select(group => $"{group.Key}: {group.Count()}"));
            DetailedCheckSummary = $"{calculationSets.Count} nhóm ({groupBreakdown}) · {DetailedChecks.Count} phép kiểm tra · {failedDetailedChecks} mục không đạt · " +
                                   "hiển thị giá trị tính toán, giới hạn cho phép, mức sử dụng và công thức chi phối.";

            // 1. Card 1 (Ván mặt / Ván đáy / Ván mặt cột)
            var card1Result = CurrentComponentType == StructuralComponentType.Beam ? res.BeamBottomPlywood : (CurrentComponentType == StructuralComponentType.Column ? res.ColumnPlywood : res.Plywood);
            
            PlywoodStressActual = $"{card1Result.StressResult.GiaTriThucTe:F2}";
            PlywoodStressLimit = $"{card1Result.StressResult.GiaTriChoPhep:F2}";
            PlywoodStressRatio = card1Result.StressResult.PhanTramSuDung;
            PlywoodDeflectionActual = $"{card1Result.DeflectionResult.GiaTriThucTe:F2}";
            PlywoodDeflectionLimit = $"{card1Result.DeflectionResult.GiaTriChoPhep:F2}";
            PlywoodDeflectionRatio = card1Result.DeflectionResult.PhanTramSuDung;
            
            PlywoodStressColor = card1Result.StressResult.IsThoaman ? "#10B981" : "#EF4444";
            PlywoodStressTextColor = card1Result.StressResult.IsThoaman ? "#2C3E50" : "#EF4444";
            PlywoodDeflectionColor = card1Result.DeflectionResult.IsThoaman ? "#10B981" : "#EF4444";
            PlywoodDeflectionTextColor = card1Result.DeflectionResult.IsThoaman ? "#2C3E50" : "#EF4444";

            bool isCard1Ok = card1Result.IsOk;
            PlywoodStatusColor = isCard1Ok ? "#D1FAE5" : "#FEE2E2";
            PlywoodStatusText = isCard1Ok ? "ĐẠT" : "KHÔNG ĐẠT";
            PlywoodStatusTextColor = isCard1Ok ? "#065F46" : "#991B1B";

            // 2. Card 2 (Xà gồ phụ / Ván thành / Nẹp đứng cột)
            var card2Result = CurrentComponentType == StructuralComponentType.Beam ? res.BeamSecondaryJoist : (CurrentComponentType == StructuralComponentType.Column ? res.ColumnStiffener : res.Joist1);
            
            Joist1StressActual = $"{card2Result.StressResult.GiaTriThucTe:F2}";
            Joist1StressLimit = $"{card2Result.StressResult.GiaTriChoPhep:F2}";
            Joist1StressRatio = card2Result.StressResult.PhanTramSuDung;
            Joist1DeflectionActual = $"{card2Result.DeflectionResult.GiaTriThucTe:F2}";
            Joist1DeflectionLimit = $"{card2Result.DeflectionResult.GiaTriChoPhep:F2}";
            Joist1DeflectionRatio = card2Result.DeflectionResult.PhanTramSuDung;

            Joist1StressColor = card2Result.StressResult.IsThoaman ? "#10B981" : "#EF4444";
            Joist1StressTextColor = card2Result.StressResult.IsThoaman ? "#2C3E50" : "#EF4444";
            if (card2Result.ShearResult != null)
            {
                Joist1ShearActual = $"{card2Result.ShearResult.GiaTriThucTe:F2}";
                Joist1ShearLimit = $"{card2Result.ShearResult.GiaTriChoPhep:F2}";
                Joist1ShearRatio = card2Result.ShearResult.PhanTramSuDung;
                Joist1ShearColor = card2Result.ShearResult.IsThoaman ? "#10B981" : "#EF4444";
                Joist1ShearTextColor = card2Result.ShearResult.IsThoaman ? "#2C3E50" : "#EF4444";
            }
            Joist1DeflectionColor = card2Result.DeflectionResult.IsThoaman ? "#10B981" : "#EF4444";
            Joist1DeflectionTextColor = card2Result.DeflectionResult.IsThoaman ? "#2C3E50" : "#EF4444";

            bool isCard2Ok = card2Result.IsOk;
            Joist1StatusColor = isCard2Ok ? "#D1FAE5" : "#FEE2E2";
            Joist1StatusText = isCard2Ok ? "ĐẠT" : "KHÔNG ĐẠT";
            Joist1StatusTextColor = isCard2Ok ? "#065F46" : "#991B1B";

            // 3. Card 3 (Xà gồ chính / Gông đáy / Gông đôi cột)
            bool isCard3Ok = true;
            {
                var card3Result = CurrentComponentType == StructuralComponentType.Beam ? res.BeamBottomYoke :
                                  (CurrentComponentType == StructuralComponentType.Column ? res.ColumnYoke : res.Joist2);
                
                Joist2StressActual = $"{card3Result.StressResult.GiaTriThucTe:F2}";
                Joist2StressLimit = $"{card3Result.StressResult.GiaTriChoPhep:F2}";
                Joist2StressRatio = card3Result.StressResult.PhanTramSuDung;
                Joist2DeflectionActual = $"{card3Result.DeflectionResult.GiaTriThucTe:F2}";
                Joist2DeflectionLimit = $"{card3Result.DeflectionResult.GiaTriChoPhep:F2}";
                Joist2DeflectionRatio = card3Result.DeflectionResult.PhanTramSuDung;

                Joist2StressColor = card3Result.StressResult.IsThoaman ? "#10B981" : "#EF4444";
                Joist2StressTextColor = card3Result.StressResult.IsThoaman ? "#2C3E50" : "#EF4444";
                if (card3Result.ShearResult != null)
                {
                    Joist2ShearActual = $"{card3Result.ShearResult.GiaTriThucTe:F2}";
                    Joist2ShearLimit = $"{card3Result.ShearResult.GiaTriChoPhep:F2}";
                    Joist2ShearRatio = card3Result.ShearResult.PhanTramSuDung;
                    Joist2ShearColor = card3Result.ShearResult.IsThoaman ? "#10B981" : "#EF4444";
                    Joist2ShearTextColor = card3Result.ShearResult.IsThoaman ? "#2C3E50" : "#EF4444";
                }
                Joist2DeflectionColor = card3Result.DeflectionResult.IsThoaman ? "#10B981" : "#EF4444";
                Joist2DeflectionTextColor = card3Result.DeflectionResult.IsThoaman ? "#2C3E50" : "#EF4444";

                isCard3Ok = card3Result.IsOk;
                Joist2StatusColor = isCard3Ok ? "#D1FAE5" : "#FEE2E2";
                Joist2StatusText = isCard3Ok ? "ĐẠT" : "KHÔNG ĐẠT";
                Joist2StatusTextColor = isCard3Ok ? "#065F46" : "#991B1B";
            }

            // 4. Card 4 (Cây chống đứng / chống xiên cột)
            bool isCard4Ok = true;
            {
                var card4Result = CurrentComponentType == StructuralComponentType.Column ? res.ColumnBrace : res.Support;
                SupportLoadActual = $"{card4Result.StressResult.GiaTriThucTe:F2}";
                SupportLoadLimit = $"{card4Result.StressResult.GiaTriChoPhep:F2}";
                SupportLoadRatio = card4Result.StressResult.PhanTramSuDung;
                
                SupportLoadColor = card4Result.StressResult.IsThoaman ? "#10B981" : "#EF4444";
                SupportLoadTextColor = card4Result.StressResult.IsThoaman ? "#2C3E50" : "#EF4444";

                SupportSlendernessFactor = CurrentComponentType == StructuralComponentType.Column
                    ? _SharedDataViewModel.ColumnBracePlacementModeIndex > 0
                        ? $"{Math.Max(1, _SharedDataViewModel.ColumnBraceCount)} cụm chống/neo @ {_SharedDataViewModel.ColumnBraceAngle:F0}° (chỉ trên hướng có điểm đỡ)"
                        : $"{_SharedDataViewModel.ColumnBraceCount} chống @ {_SharedDataViewModel.ColumnBraceAngle:F0}°"
                    : res.SupportPhi.ToString("F2");
                
                bool detailedSupportOk = CurrentComponentType == StructuralComponentType.Column
                    ? res.ColumnTieRod.IsOk && res.ColumnBraceBaseBearing.IsOk && res.ColumnCable.IsOk &&
                      res.ColumnTurnbuckle.IsOk && res.ColumnAnchor.IsOk && res.ColumnPlumb.IsOk &&
                      res.ColumnPouring.IsOk && _SharedDataViewModel.SelectedColumnBraceAssessmentIsAutoResolvable
                    : res.SupportSlenderness.IsOk && res.SupportEulerBuckling.IsOk &&
                      res.SupportBaseBearing.IsOk && res.SupportSoleBearing.IsOk &&
                      res.SupportHeadBearing.IsOk && res.SupportJack.IsOk;
                isCard4Ok = card4Result.IsOk && detailedSupportOk;
                if (CurrentComponentType == StructuralComponentType.Foundation) isCard4Ok = true;
                SupportStatusColor = isCard4Ok ? "#D1FAE5" : "#FEE2E2";
                SupportStatusText = isCard4Ok ? "ĐẠT" : "KHÔNG ĐẠT";
                SupportStatusTextColor = isCard4Ok ? "#065F46" : "#991B1B";
            }

            bool isSystemOk = isCard1Ok && isCard2Ok && isCard3Ok && isCard4Ok &&
                              calculationSets.All(set => set.Value.IsSystemOk);
            string layoutSummary = "";
            if (CurrentComponentType == StructuralComponentType.Column)
            {
                int stiffeners = 2 * ((int)Math.Ceiling(p.ColumnWidth / Math.Max(100.0, p.ColumnStiffenerSpacing)) + 1) +
                                 2 * ((int)Math.Ceiling(p.ColumnDepth / Math.Max(100.0, p.ColumnStiffenerSpacing)) + 1);
                int yokeLevels = res.ColumnYokeLevelCount;
                int tieRods = yokeLevels * 2 * Math.Max(1, p.ColumnTieRodCountPerFace);
                layoutSummary = $" Áp lực thiết kế {res.ColumnDesignPressure:F2} kN/m²; bố trí khoảng {stiffeners} nẹp đứng, {yokeLevels} tầng gông đôi, {tieRods} ty D{p.ColumnTieRodDiameter:F0} và yêu cầu {Math.Max(2, p.ColumnBraceCount)} chống/neo.";
                SelectedFloorInfo = $"Cột {p.ColumnWidth:F0}×{p.ColumnDepth:F0}, H={p.ColumnHeight:F0} mm | Offset tim {p.ColumnAxisOffset:F0} mm";
                ColumnPressureVerification = $"ÁP LỰC: p_h=γH={res.ColumnHydrostaticPressure:F2}; p_cap=7,2+785R/(T+17,8)={res.ColumnPressureCap:F2}; p_d(0)=min(p_h,p_cap)γG+q_vγQ={res.ColumnDesignPressure:F2} kN/m².";
                ColumnSpanVerification = $"SƠ ĐỒ BIẾN BƯỚC: cao độ gông từ chân cột = [{res.ColumnYokeLayoutText}] mm. Nhịp xà gồ đứng nguy hiểm L={res.ColumnCriticalStiffenerSpan:F0} mm tại p_d={res.ColumnCriticalStiffenerPressure:F2} kN/m²; dải ảnh hưởng gông nguy hiểm b={res.ColumnCriticalYokeTributaryHeight:F0} mm tại p_d={res.ColumnCriticalYokePressure:F2} kN/m².";
                string bracePlacement = p.ColumnBracePlacementModeIndex == 0
                    ? "chế độ tính thủ công/công trường thoáng"
                    : "kiểm tra hình học từng hướng cho mỗi cặp (cột, tầng)";
                ColumnConstructionVerification = $"THI CÔNG: {_SharedDataViewModel.SelectedColumnBraceAssessmentSummary} Cáp/tăng đơ/neo cho phép = {p.ColumnCableAllowableCapacity:F1}/{p.ColumnTurnbuckleAllowableCapacity:F1}/{p.ColumnAnchorAllowableCapacity:F1} kN; {bracePlacement}; đầu cốp pha cao hơn mạch ngừng {p.ColumnFormworkTopExtension:F0} mm; ống đổ ≤{p.ColumnHoseBottomClearance:F0} mm; mỗi đợt ≤{p.ColumnPourLiftHeight:F0} mm; dừng dưới đáy dầm {p.ColumnBeamStopOffset:F0} mm; tháo sau {p.ColumnStripTimeHours:F0} h, dưỡng hộ {p.ColumnCuringDays:F0} ngày.";
            }
            else if (CurrentComponentType == StructuralComponentType.Beam)
            {
                string restraint = p.BeamPlacementModeIndex == 1
                    ? $"dầm giữa: ty giằng D{p.BeamTieRodDiameter:F0} @ {p.BeamTieRodSpacing:F0} mm"
                    : p.BeamPlacementModeIndex == 2
                        ? $"dầm biên: chống xiên @ {p.BeamSideBraceSpacing:F0} mm, α={p.BeamSideBraceAngle:F0}°"
                        : "tự động: kiểm tra cả ty giằng dầm giữa và chống xiên dầm biên";
                layoutSummary = $" Chuỗi truyền lực: {res.BeamLoadPathText}. Phản lực nút chống lớn nhất {res.BeamSupportReaction:F2} kN; {restraint}.";
                SelectedFloorInfo = $"Dầm {p.BeamWidth:F0}×{p.BeamDepth:F0}, L={p.BeamLength:F0} mm | {restraint}";
                BeamLoadVerification = $"TẢI ĐÁY: q_d=γG[(γ_c+γ_s)h+g_vk]+γQ[q_tc+max(q_đổ,q_đầm)]={res.BeamDesignBottomLoad:F2} kN/m²; q_s={res.BeamServiceBottomLoad:F2} kN/m².";
                BeamLoadPathVerification = $"TRUYỀN LỰC: {res.BeamLoadPathText}. Mỗi lớp đều kiểm tra uốn σ=M/W và cắt τ=1,5V/A; võng dùng 5qL⁴/(384EI) cho tải đều, PL³/(48EI) cho xà gồ chính chịu tải tập trung. Đầu U nhận N_Ed={res.BeamSupportReaction:F2} kN.";
                BeamConstructionVerification = $"THÀNH DẦM: H_d=½γGγ_ch²+γQq_đầmh={res.BeamSideResultantPerMetre:F2} kN/m; {restraint}. Trình tự: dựng Ringlock → xà gồ chính → xà gồ phụ → ván phủ phim → cốt thép dầm rồi cốt thép sàn → đổ từ xa đến gần. Chỉ tháo khi bê tông được nghiệm thu ≥{p.BeamStrippingStrengthPercent:F0}% cường độ; vùng đầm chồng {p.BeamVibratorOverlap:F0} mm.";
            }
            var scopeLabels = new List<string>();
            if (checkSlab) scopeLabels.Add("Sàn");
            if (checkFoundation) scopeLabels.Add("Móng/Nền");
            if (checkBeam) scopeLabels.Add("Dầm");
            if (checkColumn) scopeLabels.Add("Cột");
            int selectedElementCount = _SharedDataViewModel.SelectedSlabCount +
                                       _SharedDataViewModel.SelectedFoundationCount +
                                       _SharedDataViewModel.SelectedBeamCount +
                                       _SharedDataViewModel.SelectedColumnCount;
            string sourceDescription = selectedElementCount > 0
                ? _SharedDataViewModel.SelectedComponentComposition
                : "dùng thông số cấu kiện hiện hành";
            SelectedFloorInfo = $"Phạm vi tính: {string.Join(" · ", scopeLabels)} | {sourceDescription}";
            if (isSystemOk)
            {
                IsSystemSafeColor = "#D1FAE5";
                IsSystemSafeBorderColor = "#10B981";
                IsSystemSafeIconColor = "#10B981";
                IsSystemSafeTextColor = "#065F46";
                SystemSafeTitle = "HỆ THỐNG ĐẠT YÊU CẦU AN TOÀN KỸ THUẬT";
                
                string tieuChuanStr = _SharedDataViewModel.SelectedTieuChuan == 0 ? "TCVN 9356:2012" : 
                                     (_SharedDataViewModel.SelectedTieuChuan == 1 ? "EN 1995-1-1 (Eurocode 5)" : "AWC NDS-2018");
                SystemSafeDescription = $"Tất cả các cấu kiện ván khuôn và đà giáo đều đảm bảo khả năng chịu lực và độ võng nằm trong giới hạn cho phép theo {tieuChuanStr}.{layoutSummary}";
            }
            else
            {
                IsSystemSafeColor = "#FEE2E2";
                IsSystemSafeBorderColor = "#EF4444";
                IsSystemSafeIconColor = "#EF4444";
                IsSystemSafeTextColor = "#991B1B";
                SystemSafeTitle = "HỆ THỐNG KHÔNG ĐẠT YÊU CẦU AN TOÀN KỸ THUẬT";
                
                var failedItems = DetailedChecks.Where(check => !check.IsOk)
                    .GroupBy(check => check.Group)
                    .Select(group => $"{group.Key}: {string.Join(", ", group.Select(check => check.Component).Distinct())}")
                    .ToList();
                SystemSafeDescription = $"Các nhóm [{string.Join("; ", failedItems)}] KHÔNG ĐẠT do vượt giới hạn cho phép. Nhấn [Tự động thiết kế đạt chuẩn] để tăng tiết diện/số lượng phụ kiện hoặc giảm bước bố trí; kích thước kết cấu Revit được giữ nguyên.{layoutSummary}";
            }

            RevitVersionStatus = "Đã cập nhật kiểm tra kỹ thuật lúc " + DateTime.Now.ToString("HH:mm:ss");
        }

        private void OnSharedDataViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_SharedDataViewModel.RevitVersionStatus)) return;
            if (_isApplyingAutoDesign && e.PropertyName != nameof(_SharedDataViewModel.TinhToanPreviewControl)) return;
            
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
                e.PropertyName == nameof(SharedDataViewModel.CayChongHeight) ||
                e.PropertyName == nameof(SharedDataViewModel.SupportEffectiveLengthFactor) ||
                e.PropertyName == nameof(SharedDataViewModel.SupportRatedCapacity) ||
                e.PropertyName == nameof(SharedDataViewModel.SupportSlendernessLimit) ||
                e.PropertyName == nameof(SharedDataViewModel.SupportBasePlateWidth) ||
                e.PropertyName == nameof(SharedDataViewModel.SupportBasePlateDepth) ||
                e.PropertyName == nameof(SharedDataViewModel.SupportBaseBearingAllowable) ||
                e.PropertyName == nameof(SharedDataViewModel.SupportSolePlateWidth) ||
                e.PropertyName == nameof(SharedDataViewModel.SupportSolePlateDepth) ||
                e.PropertyName == nameof(SharedDataViewModel.SupportGroundBearingAllowable) ||
                e.PropertyName == nameof(SharedDataViewModel.SupportHeadPlateWidth) ||
                e.PropertyName == nameof(SharedDataViewModel.SupportHeadPlateDepth) ||
                e.PropertyName == nameof(SharedDataViewModel.SupportHeadBearingAllowable) ||
                e.PropertyName == nameof(SharedDataViewModel.SupportJackExtension) ||
                e.PropertyName == nameof(SharedDataViewModel.SupportJackExtensionLimit) ||
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
                e.PropertyName == nameof(SharedDataViewModel.JoistDeflectionLimit) ||
                e.PropertyName == nameof(SharedDataViewModel.IsFilterSlabChecked) ||
                e.PropertyName == nameof(SharedDataViewModel.IsFilterFoundationChecked) ||
                e.PropertyName == nameof(SharedDataViewModel.IsFilterBeamChecked) ||
                e.PropertyName == nameof(SharedDataViewModel.IsFilterColumnChecked) ||
                e.PropertyName == nameof(SharedDataViewModel.HasMixedComponentSelection) ||
                e.PropertyName == nameof(SharedDataViewModel.SelectedComponentComposition) ||
                e.PropertyName == nameof(SharedDataViewModel.CurrentComponentType) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamWidth) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamDepth) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamLength) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamSidePlywoodThickness) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamBottomPlywoodThickness) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamSideYokeSpacing) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamBottomYokeSpacing) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamPropSpacing) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamSecondaryJoistWidth) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamSecondaryJoistDepth) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamPrimaryWalerWidth) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamPrimaryWalerDepth) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamPrimaryWalerSpacing) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamScaffoldBayWidth) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamTieRodDiameter) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamTieRodSpacing) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamTieRodAllowableCapacity) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamSideBraceSpacing) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamSideBraceAngle) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamSideBraceDiameter) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamSideBraceThickness) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamSideBraceCountPerStation) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamSideBraceAllowableCapacity) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamPlacementModeIndex) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamStrippingStrengthPercent) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamVibratorOverlap) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnWidth) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnDepth) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnHeight) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnPlywoodThickness) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnStiffenerWidth) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnStiffenerDepth) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnStiffenerSpacing) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnYokeSpacing) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnUseZonedYokeSpacing) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnFirstYokeOffset) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnYokeBottomSpacing) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnYokeUpperSpacing) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnYokeTransitionHeight) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnLastYokeOffset) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnYokeWidth) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnYokeHeight) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnYokeThickness) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnYokePairCount) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnYokeMaterialIndex) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnTieRodDiameter) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnTieRodCountPerFace) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnTieRodAllowableCapacity) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnBraceCount) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnBraceAngle) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnBraceAllowableCapacity) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnBracePlacementModeIndex) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnNeighbourTieThreshold) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnIsBoundaryOrCorner) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnCableAllowableCapacity) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnTurnbuckleAllowableCapacity) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnAnchorAllowableCapacity) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnBraceOverrideIndex) ||
                e.PropertyName == nameof(SharedDataViewModel.SelectedColumnBraceAssessmentKey) ||
                e.PropertyName == nameof(SharedDataViewModel.SelectedColumnBraceAssessmentIsAutoResolvable) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnAxisOffset) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnPlumbTargetTolerance) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnFormworkTopExtension) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnHoseBottomClearance) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnPourLiftHeight) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnBeamStopOffset) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnStripTimeHours) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnCuringDays) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnUseWorkPlatform) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnWorkPlatformWidth) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnGuardrailHeight) ||
                e.PropertyName == nameof(SharedDataViewModel.ConcretePourRate) ||
                e.PropertyName == nameof(SharedDataViewModel.ConcreteTemperature))
            {
                if (e.PropertyName == nameof(SharedDataViewModel.CurrentComponentType))
                {
                    OnPropertyChanged(nameof(CurrentComponentType));
                    OnPropertyChanged(nameof(Card1Title));
                    OnPropertyChanged(nameof(Card2Title));
                    OnPropertyChanged(nameof(Card3Title));
                    OnPropertyChanged(nameof(Card4Title));
                    OnPropertyChanged(nameof(Card3Visibility));
                    OnPropertyChanged(nameof(Card4Visibility));
                    OnPropertyChanged(nameof(Card4LoadLabel));
                    OnPropertyChanged(nameof(Card4FactorLabel));
                    OnPropertyChanged(nameof(ColumnVerificationVisibility));
                    OnPropertyChanged(nameof(BeamVerificationVisibility));
                }
                else if (e.PropertyName == nameof(SharedDataViewModel.ColumnYokeMaterialIndex))
                {
                    OnPropertyChanged(nameof(Card3Title));
                }
                ExecuteRecalculate();
            }
            else if (e.PropertyName == nameof(_SharedDataViewModel.IsCalculated) && _SharedDataViewModel.IsCalculated)
            {
                ExecuteRecalculate();
            }
        }

        /// <summary>
        /// Xử lý sự kiện đồng bộ khi người dùng chọn sàn từ bất kỳ trang nào.
        /// Cập nhật danh sách sàn, thông tin hiển thị, và tính toán lại.
        /// </summary>
        private void OnSlabSelectionCompleted(object sender, SlabSelectionEventArgs e)
        {
            FloorElements = e.FloorElements;
            ExecuteRecalculate();
            RevitVersionStatus = $"Đồng bộ từ Revit: {e.FloorCount} sàn - Cập nhật lúc {DateTime.Now:HH:mm:ss}";
        }

        private List<FloorElementModel> GetScopedComponents(bool slab, bool foundation, bool beam, bool column)
        {
            return (_SharedDataViewModel.FloorElements ?? new List<FloorElementModel>())
                .Where(item => item != null &&
                    (slab && item.ComponentType == StructuralComponentType.Slab ||
                     foundation && item.ComponentType == StructuralComponentType.Foundation ||
                     beam && item.ComponentType == StructuralComponentType.Beam ||
                     column && item.ComponentType == StructuralComponentType.Column))
                .GroupBy(item => string.IsNullOrWhiteSpace(item.UniqueId) ? item.ElementId : item.UniqueId,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Last())
                .ToList();
        }

        private void ApplyColumnBraceAssessment(FloorElementModel component, CalculationParameters parameters)
        {
            if (component == null || parameters == null || component.ComponentType != StructuralComponentType.Column)
                return;

            if (_SharedDataViewModel.ColumnBraceAssessmentCache.Count == 0 && _SharedDataViewModel.RevitDoc != null)
                new ProjectFormworkPreviewBuilder(_SharedDataViewModel).RefreshColumnBraceAssessmentsOnly();

            long columnId;
            if (!long.TryParse(component.ElementId, out columnId)) return;
            List<ColumnBraceAssessment> segments = _SharedDataViewModel.ColumnBraceAssessmentCache.Values
                .Where(assessment => assessment != null && assessment.ColumnId == columnId && assessment.Directions.Count > 0)
                .ToList();
            if (segments.Count == 0) return;

            int supported = segments.Min(segment => segment.Directions.Count(direction => direction.CanPlaceProp));
            parameters.ColumnSupportedBraceDirectionCount = Math.Max(1, Math.Min(4, supported));
            parameters.ColumnIsBoundaryOrCorner = segments.Any(segment => segment.IsPerimeterForCalculation) || supported < 4;
            parameters.ColumnBracePlacementModeIndex = parameters.ColumnIsBoundaryOrCorner ? 2 : 0;
        }

        private static string DesignSignature(FloorElementModel component, CalculationParameters p)
        {
            switch (component.ComponentType)
            {
                case StructuralComponentType.Beam:
                    return $"B|{p.BeamWidth:F1}|{p.BeamDepth:F1}|{p.BeamLength:F1}|{p.NetFormworkLengthMm:F1}|{p.NetPlywoodAreaM2:F3}|{p.BeamBottomPlywoodThickness:F1}|{p.BeamSidePlywoodThickness:F1}|{p.BeamBottomYokeSpacing:F1}|{p.BeamSideYokeSpacing:F1}|{p.BeamPrimaryWalerSpacing:F1}|{p.BeamSecondaryJoistWidth:F1}|{p.BeamSecondaryJoistDepth:F1}|{p.BeamPrimaryWalerWidth:F1}|{p.BeamPrimaryWalerDepth:F1}|{p.BeamPropSpacing:F1}";
                case StructuralComponentType.Column:
                    return $"C|{p.ColumnWidth:F1}|{p.ColumnDepth:F1}|{p.ColumnHeight:F1}|{p.NetSideFormworkAreaM2:F3}|{p.ColumnIsBoundaryOrCorner}|{p.ColumnSupportedBraceDirectionCount}|{p.ColumnPlywoodThickness:F1}|{p.ColumnStiffenerWidth:F1}|{p.ColumnStiffenerDepth:F1}|{p.ColumnStiffenerSpacing:F1}|{p.ColumnYokeBottomSpacing:F1}|{p.ColumnYokeUpperSpacing:F1}|{p.ColumnYokeWidth:F1}|{p.ColumnYokeHeight:F1}|{p.ColumnYokeThickness:F1}|{p.ColumnTieRodDiameter:F1}|{p.ColumnTieRodCountPerFace}|{p.ColumnBraceCount}";
                default:
                    return $"F|{component.ComponentType}|{p.SlabThickness:F1}|{p.CayChongHeight:F1}|{p.NetBottomFormworkAreaM2:F3}|{p.NetSideFormworkAreaM2:F3}|{p.NetTopFormworkAreaM2:F3}|{p.VanMatThickness:F1}|{p.Joist1Spacing:F1}|{p.Joist2Spacing:F1}|{p.SupportSpacing:F1}|{p.XaGoPhuWidth:F1}|{p.XaGoPhuHeight:F1}|{p.XaGoChinhWidth:F1}|{p.XaGoChinhHeight:F1}";
            }
        }

        private List<KeyValuePair<string, CalculationResult>> BuildScopedCalculationSets(CalculationParameters template,
            bool checkSlab, bool checkFoundation, bool checkBeam, bool checkColumn)
        {
            List<FloorElementModel> components = GetScopedComponents(checkSlab, checkFoundation, checkBeam, checkColumn);
            var prepared = components.Select(component =>
            {
                CalculationParameters parameters = _SharedDataViewModel.GetEffectiveComponentParameters(component, template);
                parameters.IsFloorSelected = component.ComponentType == StructuralComponentType.Slab || component.ComponentType == StructuralComponentType.Foundation;
                parameters.IsBeamSelected = component.ComponentType == StructuralComponentType.Beam;
                parameters.IsColumnSelected = component.ComponentType == StructuralComponentType.Column;
                ApplyColumnBraceAssessment(component, parameters);
                return new { Component = component, Parameters = parameters, Signature = DesignSignature(component, parameters) };
            }).ToList();

            var sets = new List<KeyValuePair<string, CalculationResult>>();
            foreach (var group in prepared.GroupBy(item => item.Signature))
            {
                var first = group.First();
                FloorElementModel component = first.Component;
                CalculationParameters parameters = first.Parameters;
                string prefix;
                CalculationResult result;
                switch (component.ComponentType)
                {
                    case StructuralComponentType.Foundation:
                        prefix = $"MÓNG/NỀN · {parameters.SlabThickness:F0} mm";
                        parameters.IsGroundLevel = true;
                        result = FormworkCalculationService.CheckSafety(parameters);
                        break;
                    case StructuralComponentType.Beam:
                        prefix = $"DẦM · {parameters.BeamWidth:F0}×{parameters.BeamDepth:F0}×{parameters.BeamLength:F0} mm";
                        result = FormworkCalculationService.CheckBeamSafety(parameters);
                        break;
                    case StructuralComponentType.Column:
                        prefix = $"{(parameters.ColumnIsBoundaryOrCorner ? "CỘT BIÊN" : "CỘT GIỮA")} · {parameters.ColumnWidth:F0}×{parameters.ColumnDepth:F0} mm";
                        result = FormworkCalculationService.CheckColumnSafety(parameters);
                        break;
                    default:
                        prefix = $"SÀN · {parameters.SlabThickness:F0} mm";
                        parameters.IsGroundLevel = false;
                        result = FormworkCalculationService.CheckSafety(parameters);
                        break;
                }
                if (group.Count() > 1) prefix += $" · {group.Count()} cấu kiện";
                if (parameters.IntersectingElementUniqueIds?.Length > 0)
                    prefix += $" · {parameters.IntersectingElementUniqueIds.Length} giao · " +
                              $"ván ròng {parameters.NetPlywoodAreaM2:F2} m²";
                sets.Add(new KeyValuePair<string, CalculationResult>(prefix, result));
            }

            // Backward-compatible representatives keep the page useful before any
            // Revit selection has been made. Selected elements always take precedence.
            if (checkSlab && !components.Any(item => item.ComponentType == StructuralComponentType.Slab))
            {
                var p = template.Clone(); p.IsGroundLevel = false;
                sets.Add(new KeyValuePair<string, CalculationResult>("SÀN · mặc định", FormworkCalculationService.CheckSafety(p)));
            }
            if (checkFoundation && !components.Any(item => item.ComponentType == StructuralComponentType.Foundation))
            {
                var p = template.Clone(); p.IsGroundLevel = true;
                sets.Add(new KeyValuePair<string, CalculationResult>("MÓNG/NỀN · mặc định", FormworkCalculationService.CheckSafety(p)));
            }
            if (checkBeam && !components.Any(item => item.ComponentType == StructuralComponentType.Beam))
                sets.Add(new KeyValuePair<string, CalculationResult>("DẦM · mặc định", FormworkCalculationService.CheckBeamSafety(template.Clone())));
            if (checkColumn && !components.Any(item => item.ComponentType == StructuralComponentType.Column))
                sets.AddRange(BuildColumnCalculationSets(template));
            return sets;
        }

        private List<KeyValuePair<string, Models.CalculationResult>> BuildColumnCalculationSets(
            Models.CalculationParameters template)
        {
            if (_SharedDataViewModel.ColumnBraceAssessmentCache.Count == 0 &&
                _SharedDataViewModel.RevitDoc != null)
            {
                // Calculation cannot depend on the user having opened Preview3D first.
                // Refresh only floor footprints and column assessments; do not tessellate
                // or construct the retained-mode 3D scene here.
                new ProjectFormworkPreviewBuilder(_SharedDataViewModel)
                    .RefreshColumnBraceAssessmentsOnly();
            }

            var assessments = _SharedDataViewModel.ColumnBraceAssessmentCache.Values
                .Where(assessment => assessment != null && assessment.Directions.Count > 0)
                .ToList();

            if (assessments.Count == 0)
            {
                var fallback = template.Clone();
                fallback.ColumnSupportedBraceDirectionCount = fallback.ColumnIsBoundaryOrCorner
                    ? Math.Max(1, Math.Min(3, fallback.ColumnBraceCount))
                    : 4;
                string fallbackType = fallback.ColumnIsBoundaryOrCorner ? "CỘT BIÊN" : "CỘT GIỮA";
                string fallbackLabel = $"{fallbackType} · {fallback.ColumnWidth:F0}×{fallback.ColumnDepth:F0} mm";
                return new List<KeyValuePair<string, Models.CalculationResult>>
                {
                    new KeyValuePair<string, Models.CalculationResult>(fallbackLabel,
                        Models.FormworkCalculationService.CheckColumnSafety(fallback))
                };
            }

            // Classification is a result of the four local-direction foot checks at each
            // (ColumnId, LevelId). One foot without slab below makes the segment perimeter;
            // only four slab-supported feet qualify it as interior.
            return assessments
                .GroupBy(assessment => new
                {
                    IsPerimeter = assessment.IsPerimeterForCalculation,
                    Width = Math.Round(assessment.ColumnWidthMm > 0
                        ? assessment.ColumnWidthMm
                        : template.ColumnWidth),
                    Depth = Math.Round(assessment.ColumnDepthMm > 0
                        ? assessment.ColumnDepthMm
                        : template.ColumnDepth)
                })
                .OrderByDescending(group => group.Key.IsPerimeter)
                .ThenBy(group => group.Key.Width)
                .ThenBy(group => group.Key.Depth)
                .Select(group =>
                {
                    var parameters = template.Clone();
                    parameters.ColumnWidth = group.Key.Width;
                    parameters.ColumnDepth = group.Key.Depth;
                    parameters.ColumnHeight = group.Max(assessment => assessment.ColumnHeightMm > 0
                        ? assessment.ColumnHeightMm
                        : template.ColumnHeight);
                    parameters.ColumnIsBoundaryOrCorner = group.Key.IsPerimeter;
                    parameters.ColumnSupportedBraceDirectionCount = group.Min(assessment =>
                        assessment.Directions.Count(direction =>
                            direction.SupportKind == Models.BraceFootSupportKind.Slab));
                    parameters.ColumnBracePlacementModeIndex = group.Key.IsPerimeter ? 2 : 0;

                    string typeLabel = group.Key.IsPerimeter ? "CỘT BIÊN" : "CỘT GIỮA";
                    string levels = string.Join(", ", group.Select(assessment => assessment.LevelName)
                        .Where(level => !string.IsNullOrWhiteSpace(level))
                        .Distinct()
                        .Take(3));
                    string countAndLevels = string.IsNullOrWhiteSpace(levels)
                        ? $"{group.Count()} đoạn tầng"
                        : $"{group.Count()} đoạn tầng: {levels}";
                    parameters.SelectedFloorDetailName =
                        $"{typeLabel} {group.Key.Width:F0}×{group.Key.Depth:F0} mm · {countAndLevels}";
                    string groupLabel = $"{typeLabel} · {group.Key.Width:F0}×{group.Key.Depth:F0} mm";

                    return new KeyValuePair<string, Models.CalculationResult>(groupLabel,
                        Models.FormworkCalculationService.CheckColumnSafety(parameters));
                })
                .ToList();
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
                SupportEffectiveLengthFactor = _SharedDataViewModel.SupportEffectiveLengthFactor,
                SupportRatedCapacity = _SharedDataViewModel.SupportRatedCapacity,
                SupportSlendernessLimit = _SharedDataViewModel.SupportSlendernessLimit,
                SupportBasePlateWidth = _SharedDataViewModel.SupportBasePlateWidth,
                SupportBasePlateDepth = _SharedDataViewModel.SupportBasePlateDepth,
                SupportBaseBearingAllowable = _SharedDataViewModel.SupportBaseBearingAllowable,
                SupportSolePlateWidth = _SharedDataViewModel.SupportSolePlateWidth,
                SupportSolePlateDepth = _SharedDataViewModel.SupportSolePlateDepth,
                SupportGroundBearingAllowable = _SharedDataViewModel.SupportGroundBearingAllowable,
                SupportHeadPlateWidth = _SharedDataViewModel.SupportHeadPlateWidth,
                SupportHeadPlateDepth = _SharedDataViewModel.SupportHeadPlateDepth,
                SupportHeadBearingAllowable = _SharedDataViewModel.SupportHeadBearingAllowable,
                SupportJackExtension = _SharedDataViewModel.SupportJackExtension,
                SupportJackExtensionLimit = _SharedDataViewModel.SupportJackExtensionLimit,
                SoNhipVanMat = _SharedDataViewModel.SoNhipVanMat,
                SoNhipXaGoPhu = _SharedDataViewModel.SoNhipXaGoPhu,
                SoNhipXaGoChinh = _SharedDataViewModel.SoNhipXaGoChinh,
                Joist1Spacing = _SharedDataViewModel.Joist1Spacing > 0 ? _SharedDataViewModel.Joist1Spacing : 400.0,
                Joist2Spacing = _SharedDataViewModel.Joist2Spacing > 0 ? _SharedDataViewModel.Joist2Spacing : 1000.0,
                SupportSpacing = _SharedDataViewModel.SupportSpacing > 0 ? _SharedDataViewModel.SupportSpacing : 1000.0,
                BeamWidth = _SharedDataViewModel.BeamWidth,
                BeamDepth = _SharedDataViewModel.BeamDepth,
                BeamLength = _SharedDataViewModel.BeamLength,
                BeamSidePlywoodThickness = _SharedDataViewModel.BeamSidePlywoodThickness,
                BeamBottomPlywoodThickness = _SharedDataViewModel.BeamBottomPlywoodThickness,
                BeamSideYokeSpacing = _SharedDataViewModel.BeamSideYokeSpacing,
                BeamBottomYokeSpacing = _SharedDataViewModel.BeamBottomYokeSpacing,
                BeamPropSpacing = _SharedDataViewModel.BeamPropSpacing,
                BeamSecondaryJoistWidth = _SharedDataViewModel.BeamSecondaryJoistWidth,
                BeamSecondaryJoistDepth = _SharedDataViewModel.BeamSecondaryJoistDepth,
                BeamPrimaryWalerWidth = _SharedDataViewModel.BeamPrimaryWalerWidth,
                BeamPrimaryWalerDepth = _SharedDataViewModel.BeamPrimaryWalerDepth,
                BeamPrimaryWalerSpacing = _SharedDataViewModel.BeamPrimaryWalerSpacing,
                BeamScaffoldBayWidth = _SharedDataViewModel.BeamScaffoldBayWidth,
                BeamTieRodDiameter = _SharedDataViewModel.BeamTieRodDiameter,
                BeamTieRodSpacing = _SharedDataViewModel.BeamTieRodSpacing,
                BeamTieRodAllowableCapacity = _SharedDataViewModel.BeamTieRodAllowableCapacity,
                BeamSideBraceSpacing = _SharedDataViewModel.BeamSideBraceSpacing,
                BeamSideBraceAngle = _SharedDataViewModel.BeamSideBraceAngle,
                BeamSideBraceDiameter = _SharedDataViewModel.BeamSideBraceDiameter,
                BeamSideBraceThickness = _SharedDataViewModel.BeamSideBraceThickness,
                BeamSideBraceCountPerStation = _SharedDataViewModel.BeamSideBraceCountPerStation,
                BeamSideBraceAllowableCapacity = _SharedDataViewModel.BeamSideBraceAllowableCapacity,
                BeamPlacementModeIndex = _SharedDataViewModel.BeamPlacementModeIndex,
                BeamStrippingStrengthPercent = _SharedDataViewModel.BeamStrippingStrengthPercent,
                BeamVibratorOverlap = _SharedDataViewModel.BeamVibratorOverlap,
                ColumnWidth = _SharedDataViewModel.ColumnWidth,
                ColumnDepth = _SharedDataViewModel.ColumnDepth,
                ColumnHeight = _SharedDataViewModel.ColumnHeight,
                ColumnPlywoodThickness = _SharedDataViewModel.ColumnPlywoodThickness,
                ColumnStiffenerWidth = _SharedDataViewModel.ColumnStiffenerWidth,
                ColumnStiffenerDepth = _SharedDataViewModel.ColumnStiffenerDepth,
                ColumnStiffenerSpacing = _SharedDataViewModel.ColumnStiffenerSpacing,
                ColumnYokeSpacing = _SharedDataViewModel.ColumnYokeSpacing,
                ColumnUseZonedYokeSpacing = _SharedDataViewModel.ColumnUseZonedYokeSpacing,
                ColumnFirstYokeOffset = _SharedDataViewModel.ColumnFirstYokeOffset,
                ColumnYokeBottomSpacing = _SharedDataViewModel.ColumnYokeBottomSpacing,
                ColumnYokeUpperSpacing = _SharedDataViewModel.ColumnYokeUpperSpacing,
                ColumnYokeTransitionHeight = _SharedDataViewModel.ColumnYokeTransitionHeight,
                ColumnLastYokeOffset = _SharedDataViewModel.ColumnLastYokeOffset,
                ColumnYokeWidth = _SharedDataViewModel.ColumnYokeWidth,
                ColumnYokeHeight = _SharedDataViewModel.ColumnYokeHeight,
                ColumnYokeThickness = _SharedDataViewModel.ColumnYokeThickness,
                ColumnYokePairCount = _SharedDataViewModel.ColumnYokePairCount,
                ColumnYokeMaterialIndex = _SharedDataViewModel.ColumnYokeMaterialIndex,
                ColumnTieRodDiameter = _SharedDataViewModel.ColumnTieRodDiameter,
                ColumnTieRodCountPerFace = _SharedDataViewModel.ColumnTieRodCountPerFace,
                ColumnTieRodAllowableCapacity = _SharedDataViewModel.ColumnTieRodAllowableCapacity,
                ColumnBraceCount = _SharedDataViewModel.ColumnBraceCount,
                ColumnBraceAngle = _SharedDataViewModel.ColumnBraceAngle,
                ColumnBraceAllowableCapacity = _SharedDataViewModel.ColumnBraceAllowableCapacity,
                ColumnBracePlacementModeIndex = _SharedDataViewModel.ColumnBracePlacementModeIndex,
                ColumnNeighbourTieThreshold = _SharedDataViewModel.ColumnNeighbourTieThreshold,
                ColumnIsBoundaryOrCorner = _SharedDataViewModel.ColumnIsBoundaryOrCorner,
                ColumnCableAllowableCapacity = _SharedDataViewModel.ColumnCableAllowableCapacity,
                ColumnTurnbuckleAllowableCapacity = _SharedDataViewModel.ColumnTurnbuckleAllowableCapacity,
                ColumnAnchorAllowableCapacity = _SharedDataViewModel.ColumnAnchorAllowableCapacity,
                ColumnAxisOffset = _SharedDataViewModel.ColumnAxisOffset,
                ColumnPlumbTargetTolerance = _SharedDataViewModel.ColumnPlumbTargetTolerance,
                ColumnFormworkTopExtension = _SharedDataViewModel.ColumnFormworkTopExtension,
                ColumnHoseBottomClearance = _SharedDataViewModel.ColumnHoseBottomClearance,
                ColumnPourLiftHeight = _SharedDataViewModel.ColumnPourLiftHeight,
                ColumnBeamStopOffset = _SharedDataViewModel.ColumnBeamStopOffset,
                ColumnStripTimeHours = _SharedDataViewModel.ColumnStripTimeHours,
                ColumnCuringDays = _SharedDataViewModel.ColumnCuringDays,
                ColumnUseWorkPlatform = _SharedDataViewModel.ColumnUseWorkPlatform,
                ColumnWorkPlatformWidth = _SharedDataViewModel.ColumnWorkPlatformWidth,
                ColumnGuardrailHeight = _SharedDataViewModel.ColumnGuardrailHeight,
                ConcretePourRate = _SharedDataViewModel.ConcretePourRate,
                ConcreteTemperature = _SharedDataViewModel.ConcreteTemperature,
                IsGroundLevel = _SharedDataViewModel.IsGroundLevel,
                IsFloorSelected = true,
                IsBeamSelected = true,
                IsColumnSelected = true
            };

            // Auto-design must validate the same component groups shown in the calculation
            // module. The category checkboxes are the single source of truth for that scope.
            bool checkSlab = IsFilterSlabChecked;
            bool checkFoundation = IsFilterFoundationChecked;
            bool checkBeam = IsFilterBeamChecked;
            bool checkColumn = IsFilterColumnChecked;
            if (!checkSlab && !checkFoundation && !checkBeam && !checkColumn)
            {
                checkSlab = CurrentComponentType == StructuralComponentType.Slab;
                checkFoundation = CurrentComponentType == StructuralComponentType.Foundation;
                checkBeam = CurrentComponentType == StructuralComponentType.Beam;
                checkColumn = CurrentComponentType == StructuralComponentType.Column;
            }

            List<FloorElementModel> components = GetScopedComponents(checkSlab, checkFoundation, checkBeam, checkColumn);
            void AddRepresentative(StructuralComponentType type)
            {
                if (components.Any(item => item.ComponentType == type)) return;
                components.Add(new FloorElementModel
                {
                    UniqueId = $"DEFAULT|{type}",
                    ElementId = $"DEFAULT|{type}",
                    ComponentType = type,
                    FamilyTypeName = "Cấu kiện đại diện",
                    Level = "Mặc định",
                    Thickness = type == StructuralComponentType.Beam ? p.BeamDepth :
                        type == StructuralComponentType.Column ? p.ColumnDepth : p.SlabThickness,
                    Elevation = type == StructuralComponentType.Beam ? p.BeamWidth :
                        type == StructuralComponentType.Column ? p.ColumnWidth : 0,
                    StoryHeight = type == StructuralComponentType.Beam ? p.BeamLength :
                        type == StructuralComponentType.Column ? p.ColumnHeight : p.CayChongHeight
                });
            }
            if (checkSlab) AddRepresentative(StructuralComponentType.Slab);
            if (checkFoundation) AddRepresentative(StructuralComponentType.Foundation);
            if (checkBeam) AddRepresentative(StructuralComponentType.Beam);
            if (checkColumn) AddRepresentative(StructuralComponentType.Column);

            var componentDesigns = new List<ComponentFormworkDesign>();
            var allDiffs = new List<string>();
            var unresolved = new List<string>();
            _isApplyingAutoDesign = true;
            try
            {
                foreach (FloorElementModel component in components)
                {
                    CalculationParameters current = _SharedDataViewModel.GetEffectiveComponentParameters(component, p);
                    ApplyColumnBraceAssessment(component, current);
                    bool slabOnly = component.ComponentType == StructuralComponentType.Slab;
                    bool foundationOnly = component.ComponentType == StructuralComponentType.Foundation;
                    bool beamOnly = component.ComponentType == StructuralComponentType.Beam;
                    bool columnOnly = component.ComponentType == StructuralComponentType.Column;
                    ComplianceDesignResult result = FormworkCalculationService.AutoDesignToCompliance(
                        current, slabOnly, foundationOnly, beamOnly, columnOnly);

                    string identity = $"{ComponentTypeLabel(component.ComponentType)} {component.ElementId} · {component.FamilyTypeName}";
                    var stored = new ComponentFormworkDesign
                    {
                        ElementUniqueId = component.UniqueId,
                        ElementId = component.ElementId,
                        DisplayName = component.FamilyTypeName,
                        LevelName = component.Level,
                        ComponentType = component.ComponentType,
                        Parameters = current.Clone(),
                        IsCompliant = result.IsCompliant,
                        Iterations = result.Iterations,
                        Timestamp = DateTime.Now,
                        Adjustments = result.Adjustments.ToList(),
                        UnresolvedChecks = result.UnresolvedChecks.ToList()
                    };
                    componentDesigns.Add(stored);
                    if (result.Adjustments.Count == 0)
                        allDiffs.Add($"{identity}: giữ nguyên — đã đạt chuẩn.");
                    else
                        allDiffs.Add($"{identity}: " + string.Join("; ", result.Adjustments));
                    if (!result.IsCompliant)
                        unresolved.AddRange(result.UnresolvedChecks.Select(item => $"{identity}: {item}"));
                }

                if (unresolved.Count > 0)
                {
                    AutoDesignDiffItems = unresolved;
                    AutoDesignStatusColor = "#B91C1C";
                    AutoDesignStatus = $"Không hội tụ sau giới hạn 50 vòng lặp cho {unresolved.Count} phép kiểm tra; chưa lưu bất kỳ thay đổi nào.";
                    _dialogService.ShowWarning(AutoDesignStatus + "\n\nCần kỹ sư ghi đè thủ công cho:\n" +
                        string.Join("\n", unresolved.Take(20).Select(item => "• " + item)),
                        "Tự động thiết kế — cần ghi đè thủ công");
                    return;
                }

                CalculationParameters envelope = BuildAccessoryEnvelope(p, componentDesigns);
                _SharedDataViewModel.CommitComponentDesigns(componentDesigns, envelope, allDiffs);
            }
            finally
            {
                _isApplyingAutoDesign = false;
                CommandManager.InvalidateRequerySuggested();
            }

            ExecuteRecalculate();
            AutoDesignDiffItems = allDiffs;
            int changedCount = componentDesigns.Count(item => item.Adjustments.Count > 0);
            int maxIterations = componentDesigns.Max(item => item.Iterations);
            AutoDesignStatusColor = "#047857";
            AutoDesignStatus = $"Đã thiết kế đạt chuẩn {componentDesigns.Count} cấu kiện; {changedCount} cấu kiện được điều chỉnh, tối đa {maxIterations} vòng lặp. " +
                               "Thiết kế theo từng Element đã được lưu và đồng bộ sang tiên lượng; kích thước kết cấu Revit được giữ nguyên.";
            _dialogService.ShowInfo(AutoDesignStatus + "\n\n" +
                string.Join("\n", allDiffs.Take(12).Select(item => "• " + item)),
                "Tự động thiết kế hoàn tất");
        }

        private static string ComponentTypeLabel(StructuralComponentType type)
        {
            switch (type)
            {
                case StructuralComponentType.Foundation: return "Móng";
                case StructuralComponentType.Beam: return "Dầm";
                case StructuralComponentType.Column: return "Cột";
                default: return "Sàn";
            }
        }

        private static CalculationParameters BuildAccessoryEnvelope(CalculationParameters defaults,
            IEnumerable<ComponentFormworkDesign> designs)
        {
            CalculationParameters envelope = defaults.Clone();
            foreach (ComponentFormworkDesign design in designs)
            {
                CalculationParameters value = design.Parameters;
                envelope.VanMatThickness = Math.Max(envelope.VanMatThickness, value.VanMatThickness);
                envelope.XaGoPhuWidth = Math.Max(envelope.XaGoPhuWidth, value.XaGoPhuWidth);
                envelope.XaGoPhuHeight = Math.Max(envelope.XaGoPhuHeight, value.XaGoPhuHeight);
                envelope.XaGoChinhWidth = Math.Max(envelope.XaGoChinhWidth, value.XaGoChinhWidth);
                envelope.XaGoChinhHeight = Math.Max(envelope.XaGoChinhHeight, value.XaGoChinhHeight);
                envelope.Joist1Spacing = Math.Min(envelope.Joist1Spacing, value.Joist1Spacing);
                envelope.Joist2Spacing = Math.Min(envelope.Joist2Spacing, value.Joist2Spacing);
                envelope.SupportSpacing = Math.Min(envelope.SupportSpacing, value.SupportSpacing);
                envelope.CayChongDiameter = Math.Max(envelope.CayChongDiameter, value.CayChongDiameter);
                envelope.CayChongThickness = Math.Max(envelope.CayChongThickness, value.CayChongThickness);
                envelope.SupportRatedCapacity = Math.Max(envelope.SupportRatedCapacity, value.SupportRatedCapacity);
                envelope.SupportBasePlateWidth = Math.Max(envelope.SupportBasePlateWidth, value.SupportBasePlateWidth);
                envelope.SupportBasePlateDepth = Math.Max(envelope.SupportBasePlateDepth, value.SupportBasePlateDepth);
                envelope.SupportHeadPlateWidth = Math.Max(envelope.SupportHeadPlateWidth, value.SupportHeadPlateWidth);
                envelope.SupportHeadPlateDepth = Math.Max(envelope.SupportHeadPlateDepth, value.SupportHeadPlateDepth);
                envelope.SupportSolePlateWidth = Math.Max(envelope.SupportSolePlateWidth, value.SupportSolePlateWidth);
                envelope.SupportSolePlateDepth = Math.Max(envelope.SupportSolePlateDepth, value.SupportSolePlateDepth);

                if (design.ComponentType == StructuralComponentType.Beam)
                {
                    envelope.BeamBottomPlywoodThickness = Math.Max(envelope.BeamBottomPlywoodThickness, value.BeamBottomPlywoodThickness);
                    envelope.BeamSidePlywoodThickness = Math.Max(envelope.BeamSidePlywoodThickness, value.BeamSidePlywoodThickness);
                    envelope.BeamBottomYokeSpacing = Math.Min(envelope.BeamBottomYokeSpacing, value.BeamBottomYokeSpacing);
                    envelope.BeamSideYokeSpacing = Math.Min(envelope.BeamSideYokeSpacing, value.BeamSideYokeSpacing);
                    envelope.BeamPropSpacing = Math.Min(envelope.BeamPropSpacing, value.BeamPropSpacing);
                    envelope.BeamSecondaryJoistWidth = Math.Max(envelope.BeamSecondaryJoistWidth, value.BeamSecondaryJoistWidth);
                    envelope.BeamSecondaryJoistDepth = Math.Max(envelope.BeamSecondaryJoistDepth, value.BeamSecondaryJoistDepth);
                    envelope.BeamPrimaryWalerWidth = Math.Max(envelope.BeamPrimaryWalerWidth, value.BeamPrimaryWalerWidth);
                    envelope.BeamPrimaryWalerDepth = Math.Max(envelope.BeamPrimaryWalerDepth, value.BeamPrimaryWalerDepth);
                    envelope.BeamPrimaryWalerSpacing = Math.Min(envelope.BeamPrimaryWalerSpacing, value.BeamPrimaryWalerSpacing);
                    envelope.BeamScaffoldBayWidth = Math.Min(envelope.BeamScaffoldBayWidth, value.BeamScaffoldBayWidth);
                    envelope.BeamTieRodDiameter = Math.Max(envelope.BeamTieRodDiameter, value.BeamTieRodDiameter);
                    envelope.BeamTieRodSpacing = Math.Min(envelope.BeamTieRodSpacing, value.BeamTieRodSpacing);
                    envelope.BeamSideBraceCountPerStation = Math.Max(envelope.BeamSideBraceCountPerStation, value.BeamSideBraceCountPerStation);
                }
                if (design.ComponentType == StructuralComponentType.Column)
                {
                    envelope.ColumnPlywoodThickness = Math.Max(envelope.ColumnPlywoodThickness, value.ColumnPlywoodThickness);
                    envelope.ColumnStiffenerWidth = Math.Max(envelope.ColumnStiffenerWidth, value.ColumnStiffenerWidth);
                    envelope.ColumnStiffenerDepth = Math.Max(envelope.ColumnStiffenerDepth, value.ColumnStiffenerDepth);
                    envelope.ColumnStiffenerSpacing = Math.Min(envelope.ColumnStiffenerSpacing, value.ColumnStiffenerSpacing);
                    envelope.ColumnYokeBottomSpacing = Math.Min(envelope.ColumnYokeBottomSpacing, value.ColumnYokeBottomSpacing);
                    envelope.ColumnYokeUpperSpacing = Math.Min(envelope.ColumnYokeUpperSpacing, value.ColumnYokeUpperSpacing);
                    envelope.ColumnYokeWidth = Math.Max(envelope.ColumnYokeWidth, value.ColumnYokeWidth);
                    envelope.ColumnYokeHeight = Math.Max(envelope.ColumnYokeHeight, value.ColumnYokeHeight);
                    envelope.ColumnYokeThickness = Math.Max(envelope.ColumnYokeThickness, value.ColumnYokeThickness);
                    envelope.ColumnYokePairCount = Math.Max(envelope.ColumnYokePairCount, value.ColumnYokePairCount);
                    envelope.ColumnTieRodDiameter = Math.Max(envelope.ColumnTieRodDiameter, value.ColumnTieRodDiameter);
                    envelope.ColumnTieRodCountPerFace = Math.Max(envelope.ColumnTieRodCountPerFace, value.ColumnTieRodCountPerFace);
                    envelope.ColumnBraceCount = Math.Max(envelope.ColumnBraceCount, value.ColumnBraceCount);
                    envelope.ColumnBraceAllowableCapacity = Math.Max(envelope.ColumnBraceAllowableCapacity, value.ColumnBraceAllowableCapacity);
                    envelope.ColumnCableAllowableCapacity = Math.Max(envelope.ColumnCableAllowableCapacity, value.ColumnCableAllowableCapacity);
                    envelope.ColumnTurnbuckleAllowableCapacity = Math.Max(envelope.ColumnTurnbuckleAllowableCapacity, value.ColumnTurnbuckleAllowableCapacity);
                    envelope.ColumnAnchorAllowableCapacity = Math.Max(envelope.ColumnAnchorAllowableCapacity, value.ColumnAnchorAllowableCapacity);
                }
            }
            return envelope;
        }
    }
}
