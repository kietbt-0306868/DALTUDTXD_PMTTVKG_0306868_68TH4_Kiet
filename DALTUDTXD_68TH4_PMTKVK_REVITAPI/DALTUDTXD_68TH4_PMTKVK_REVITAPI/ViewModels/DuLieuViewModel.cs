using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels
{
    public class DuLieuViewModel : INotifyPropertyChanged, IDataErrorInfo
    {
        private string _revitVersionStatus;
        private List<KetQuaHienThi> _checkResults;
        public Action<string> NavigateAction { get; private set; }
        private readonly Action _anSoCuaSo;
        private readonly IDialogService _dialogService;

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler OnKetQuaSelectionChanged;

        public Document RevitDoc { get; private set; }
        public UIDocument RevitUIDoc { get; private set; }

        // Dữ liệu dùng chung
        private double _woodStrength = 15.0;
        public double WoodStrength { get => _woodStrength; set { _woodStrength = value; OnPropertyChanged(); } }

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

        private bool _isSelectingSlab = false;
        public bool IsSelectingSlab { get => _isSelectingSlab; set { _isSelectingSlab = value; OnPropertyChanged(); } }

        private bool _isSelectingSlabFromKetQua = false;
        public bool IsSelectingSlabFromKetQua { get => _isSelectingSlabFromKetQua; set { _isSelectingSlabFromKetQua = value; OnPropertyChanged(); } }

        private bool _isSweepSelectingSlab = false;
        public bool IsSweepSelectingSlab { get => _isSweepSelectingSlab; set { _isSweepSelectingSlab = value; OnPropertyChanged(); } }

        private bool _isSweepSelectingSlabFromKetQua = false;
        public bool IsSweepSelectingSlabFromKetQua { get => _isSweepSelectingSlabFromKetQua; set { _isSweepSelectingSlabFromKetQua = value; OnPropertyChanged(); } }

        private bool _isSelectingSlabFromTinhToan = false;
        public bool IsSelectingSlabFromTinhToan { get => _isSelectingSlabFromTinhToan; set { _isSelectingSlabFromTinhToan = value; OnPropertyChanged(); } }

        private bool _isSweepSelectingSlabFromTinhToan = false;
        public bool IsSweepSelectingSlabFromTinhToan { get => _isSweepSelectingSlabFromTinhToan; set { _isSweepSelectingSlabFromTinhToan = value; OnPropertyChanged(); } }

        public List<string> SelectedSlabFromKetQuaUniqueIds { get; set; } = new List<string>();
        public List<string> SelectedSlabFromTinhToanUniqueIds { get; set; } = new List<string>();
        public string StartupTab { get; set; }

        public object KetQuaFloorElements { get; set; }
        public double KetQuaTotalProjectArea { get; set; }
        public double KetQuaTotalProjectPerimeter { get; set; }
        public bool KetQuaIsProjectCalculated { get; set; }
        public object KetQuaProjectMaterialQuantities { get; set; }

        public List<ElementId> KetQuaLastHighlightedFloorIds { get; set; } = new List<ElementId>();
        public ElementId KetQuaLastHighlightedViewId { get; set; }
        public ElementId KetQuaLastViewTemplateId { get; set; }

        private bool _isCalculated = false;
        public bool IsCalculated { get => _isCalculated; set { _isCalculated = value; OnPropertyChanged(); } }

        private double _selectedSlabArea = 0.0;
        public double SelectedSlabArea { get => _selectedSlabArea; set { _selectedSlabArea = value; OnPropertyChanged(); } }

        private double _selectedSlabPerimeter = 0.0;
        public double SelectedSlabPerimeter { get => _selectedSlabPerimeter; set { _selectedSlabPerimeter = value; OnPropertyChanged(); } }

        private System.Windows.FrameworkElement _sharedPreviewControl;
        public System.Windows.FrameworkElement SharedPreviewControl { get => _sharedPreviewControl; set { _sharedPreviewControl = value; OnPropertyChanged(); } }

        private System.Windows.FrameworkElement _tinhToanPreviewControl;
        public System.Windows.FrameworkElement TinhToanPreviewControl { get => _tinhToanPreviewControl; set { _tinhToanPreviewControl = value; OnPropertyChanged(); } }

        private System.Windows.FrameworkElement _ketQuaPreviewControl;
        public System.Windows.FrameworkElement KetQuaPreviewControl { get => _ketQuaPreviewControl; set { _ketQuaPreviewControl = value; OnPropertyChanged(); } }

        public DuLieuViewModel(IDialogService dialogService, Action anSoCuaSo = null)
        {
            _dialogService = dialogService ?? new MessageBoxDialogService();
            _anSoCuaSo = anSoCuaSo;

            UpdateResultsCommand = new RelayCommand(o => ExecuteRunAnalysis());
            RunAnalysisCommand = new RelayCommand(o => ExecuteRunAnalysis());
            ChangeTabCommand = new RelayCommand(ExecuteChangeTab);
            SelectSlabCommand = new RelayCommand(o => ExecuteSelectSlab());
            SweepSelectSlabCommand = new RelayCommand(o => ExecuteSweepSelectSlab());

            ExecuteUpdateResults(false);
        }

        public void ExecuteChangeTab(object parameter)
        {
            string tabName = parameter?.ToString();

            if ((tabName == "TinhToanView" || tabName == "Report" || tabName == "KetQuaView") && SharedPreviewControl == null && RevitDoc != null)
            {
                InitializePreviewControl(RevitDoc);
            }

            // Chuyển quyền sở hữu PreviewControl cho tab đang active để tránh lỗi WPF
            if (tabName == "TinhToanView" || tabName == "Report")
            {
                KetQuaPreviewControl = null;
                TinhToanPreviewControl = SharedPreviewControl;
            }
            else if (tabName == "KetQuaView")
            {
                TinhToanPreviewControl = null;
                KetQuaPreviewControl = SharedPreviewControl;
            }
            else
            {
                TinhToanPreviewControl = null;
                KetQuaPreviewControl = null;
            }

            NavigateAction?.Invoke(tabName);
        }

        public void InitializeRevitContext(Document doc, UIDocument uiDoc, Action<string> navigateAction)
        {
            RevitDoc = doc;
            RevitUIDoc = uiDoc;
            NavigateAction = navigateAction;

            if (RevitDoc != null)
            {
                RevitVersionStatus = $"Revit API {RevitDoc.Application.VersionNumber} - Dự án: {RevitDoc.Title}";
            }
            else
            {
                RevitVersionStatus = "Revit API - Chế độ thiết kế (Standalone/Offline)";
            }
        }

        public void InitializePreviewControl(Document doc)
        {
            if (SharedPreviewControl == null && doc != null)
            {
                try
                {
                    ViewPlan viewPlan = null;
                    if (KetQuaLastHighlightedViewId != null)
                    {
                        viewPlan = doc.GetElement(KetQuaLastHighlightedViewId) as ViewPlan;
                    }

                    if (viewPlan == null)
                    {
                        viewPlan = new FilteredElementCollector(doc)
                            .OfClass(typeof(ViewPlan))
                            .Cast<ViewPlan>()
                            .FirstOrDefault(v => !v.IsTemplate && (v.ViewType == ViewType.FloorPlan || v.ViewType == ViewType.EngineeringPlan));
                    }

                    if (viewPlan != null)
                    {
                        SharedPreviewControl = new PreviewControl(doc, viewPlan.Id);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Lỗi khởi tạo Shared Preview: {ex.Message}");
                }
            }
        }

        public void DisposePreviewControl()
        {
            if (SharedPreviewControl is IDisposable disposable)
            {
                disposable.Dispose();
            }
            SharedPreviewControl = null;
            KetQuaPreviewControl = null;
            TinhToanPreviewControl = null;
        }

        public string RevitVersionStatus
        {
            get => _revitVersionStatus;
            set { _revitVersionStatus = value; OnPropertyChanged(); }
        }

        public List<KetQuaHienThi> CheckResults
        {
            get => _checkResults;
            set { _checkResults = value; OnPropertyChanged(); }
        }

        public ICommand UpdateResultsCommand { get; }
        public ICommand RunAnalysisCommand { get; }
        public ICommand ChangeTabCommand { get; }
        public ICommand SelectSlabCommand { get; }
        public ICommand SweepSelectSlabCommand { get; }

        private double ParseFraction(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return 0;
            input = input.Trim();
            var parts = input.Split('/');
            if (parts.Length == 2
                && double.TryParse(parts[0], out double num)
                && double.TryParse(parts[1], out double den)
                && den != 0)
            {
                return num / den;
            }
            if (double.TryParse(input, out double direct)) return direct;
            return 0;
        }

        private void ExecuteUpdateResults(bool showWarning = false)
        {
            // Kiểm tra lỗi trước khi tính toán
            if (!string.IsNullOrEmpty(Error))
            {
                if (showWarning)
                {
                    _dialogService.ShowWarning("Vui lòng kiểm tra lại các thông số đầu vào không hợp lệ.", "Cảnh báo");
                }
                return;
            }

            double strength = WoodStrength > 0 ? WoodStrength : 15.0;
            double modulus = ElasticModulus > 0 ? ElasticModulus : 9000.0;
            double joist1Sp = Joist1Spacing > 0 ? Joist1Spacing : 400.0;
            double joist2Sp = Joist2Spacing > 0 ? Joist2Spacing : 1000.0;
            double suppSp = SupportSpacing > 0 ? SupportSpacing : 1000.0;
            double concreteWt = ConcreteWeight > 0 ? ConcreteWeight : 25.0;

            var results = new List<KetQuaHienThi>();

            // Khởi tạo các mô hình cấu kiện
            var vanMat = new Models.CauKienModel("Ván mặt", Models.LoaiTietDien.VanMat, "Gỗ dán phủ phim", 1000, VanMatThickness, VanMatThickness);
            var xaGoPhu = new Models.CauKienModel("Xà gồ phụ", Models.LoaiTietDien.XaGo, "Gỗ xẻ", XaGoPhuWidth, XaGoPhuHeight);
            var xaGoChinh = new Models.CauKienModel("Xà gồ chính", Models.LoaiTietDien.XaGo, "Gỗ xẻ", XaGoChinhWidth, XaGoChinhHeight);
            var cayChong = new Models.CauKienModel("Cây chống", Models.LoaiTietDien.CayChong, "Thép ống", CayChongDiameter, CayChongDiameter, CayChongThickness);

            // Tải trọng tính toán
            double q_bt = concreteWt * (SlabThickness / 1000.0) * DeadLoadSafetyFactor; // kN/m2
            double q_vk = WoodFormworkWeight * DeadLoadSafetyFactor; // kN/m2
            double q_ll = ConstructionLiveLoad * LiveLoadSafetyFactor; // kN/m2
            double q_tt = q_bt + q_vk + q_ll; // kN/m2
            double q_tc = concreteWt * (SlabThickness / 1000.0) + WoodFormworkWeight + ConstructionLiveLoad; // kN/m2

            // 1. Kiểm tra cường độ ván khuôn (Dải cắt b=1m)
            // q (N/mm) = q (kN/m2) * b (m)
            double q1_tt = q_tt * 1.0;
            double M1 = (q1_tt * Math.Pow(joist1Sp, 2)) / 10.0; // Sơ đồ dầm liên tục nhiều nhịp
            double plyStress = M1 / vanMat.MomenKhangUon_W; // N/mm2 = MPa

            var kqVanMatStress = new Models.KetQuaTinhToan("Ván mặt", "Kiểm tra cường độ uốn", Models.LoaiKiemTra.CuongDoUon, "σ = M/W ≤ Ru", plyStress, strength, "MPa");

            results.Add(new KetQuaHienThi
            {
                NoiDungKiemTra = "Kiểm tra cường độ ván khuôn (σ ≤ Ru)",
                GiaTriThucTe = $"{kqVanMatStress.GiaTriThucTe:F2} MPa",
                GiaTriChoPhep = $"{kqVanMatStress.GiaTriChoPhep:F2} MPa",
                KetLuan = kqVanMatStress.KetLuan,
                MauNenTrangThai = kqVanMatStress.IsThoaman ? "#D1FAE5" : "#FEE2E2",
                MauChuTrangThai = kqVanMatStress.IsThoaman ? "#065F46" : "#991B1B"
            });

            // 2. Kiểm tra độ võng ván khuôn
            double q1_tc = q_tc * 1.0; // N/mm
            double plyDef = (5.0 * q1_tc * Math.Pow(joist1Sp, 4)) / (384.0 * modulus * vanMat.MomenQuanTinh_I); // Mặc định tính dầm đơn giản cho an toàn hoặc thay hệ số
            double limitSlabRatio = ParseFraction(SlabDeflectionLimit);
            double plyDefLimit = limitSlabRatio > 0 ? joist1Sp * limitSlabRatio : 2.0;

            var kqVanMatDef = new Models.KetQuaTinhToan("Ván mặt", "Kiểm tra độ võng", Models.LoaiKiemTra.DoVong, "f = 5qL^4/384EI ≤ [f]", plyDef, plyDefLimit, "mm");

            results.Add(new KetQuaHienThi
            {
                NoiDungKiemTra = "Kiểm tra độ võng ván khuôn (f_tt ≤ [f])",
                GiaTriThucTe = $"{kqVanMatDef.GiaTriThucTe:F2} mm",
                GiaTriChoPhep = $"{kqVanMatDef.GiaTriChoPhep:F2} mm",
                KetLuan = kqVanMatDef.KetLuan,
                MauNenTrangThai = kqVanMatDef.IsThoaman ? "#D1FAE5" : "#FEE2E2",
                MauChuTrangThai = kqVanMatDef.IsThoaman ? "#065F46" : "#991B1B"
            });

            // 3. Kiểm tra cường độ xà gồ phụ
            double q2_tt = q_tt * (joist1Sp / 1000.0); // N/mm
            double M2 = (q2_tt * Math.Pow(joist2Sp, 2)) / 10.0;
            double j1Stress = M2 / xaGoPhu.MomenKhangUon_W;
            double j1Limit = strength * MoistureFactor; // Có thể nhân thêm hệ số
            var kqXaGoPhuStress = new Models.KetQuaTinhToan("Xà gồ phụ", "Kiểm tra cường độ uốn", Models.LoaiKiemTra.CuongDoUon, "σ = M/W ≤ Ru", j1Stress, j1Limit, "MPa");

            results.Add(new KetQuaHienThi
            {
                NoiDungKiemTra = "Kiểm tra cường độ xà gồ phụ (Thanh phụ)",
                GiaTriThucTe = $"{kqXaGoPhuStress.GiaTriThucTe:F2} MPa",
                GiaTriChoPhep = $"{kqXaGoPhuStress.GiaTriChoPhep:F2} MPa",
                KetLuan = kqXaGoPhuStress.KetLuan,
                MauNenTrangThai = kqXaGoPhuStress.IsThoaman ? "#D1FAE5" : "#FEE2E2",
                MauChuTrangThai = kqXaGoPhuStress.IsThoaman ? "#065F46" : "#991B1B"
            });

            // 4. Kiểm tra độ võng xà gồ chính
            double q3_tc = q_tc * (joist2Sp / 1000.0); // N/mm
            double j2Def = (5.0 * q3_tc * Math.Pow(suppSp, 4)) / (384.0 * modulus * xaGoChinh.MomenQuanTinh_I);
            double limitJoistRatio = ParseFraction(JoistDeflectionLimit);
            double j2DefLimit = limitJoistRatio > 0 ? suppSp * limitJoistRatio : 4.0;

            var kqXaGoChinhDef = new Models.KetQuaTinhToan("Xà gồ chính", "Kiểm tra độ võng", Models.LoaiKiemTra.DoVong, "f = 5qL^4/384EI ≤ [f]", j2Def, j2DefLimit, "mm");

            results.Add(new KetQuaHienThi
            {
                NoiDungKiemTra = "Kiểm tra độ võng xà gồ chính (Thanh chính)",
                GiaTriThucTe = $"{kqXaGoChinhDef.GiaTriThucTe:F2} mm",
                GiaTriChoPhep = $"{kqXaGoChinhDef.GiaTriChoPhep:F2} mm",
                KetLuan = kqXaGoChinhDef.KetLuan,
                MauNenTrangThai = kqXaGoChinhDef.IsThoaman ? "#D1FAE5" : "#FEE2E2",
                MauChuTrangThai = kqXaGoChinhDef.IsThoaman ? "#065F46" : "#991B1B"
            });

            // 5. Kiểm tra sức chịu tải cây chống
            double N_tt = q_tt * (joist2Sp / 1000.0) * (suppSp / 1000.0); // kN
            double shoreLimit = cayChong.DienTichMatCat_A * 210.0 / 1000.0; // Giả sử R = 210 MPa cho thép, chuyển sang kN

            // Giới hạn 25kN là giới hạn nhà sản xuất cho giáo
            shoreLimit = Math.Min(shoreLimit, 25.0);

            var kqCayChong = new Models.KetQuaTinhToan("Cây chống", "Kiểm tra sức chịu tải", Models.LoaiKiemTra.ChiuLucDoc, "N ≤ [P]", N_tt, shoreLimit, "kN");

            results.Add(new KetQuaHienThi
            {
                NoiDungKiemTra = "Kiểm tra sức chịu tải cây chống đứng (P_tt ≤ P_cp)",
                GiaTriThucTe = $"{kqCayChong.GiaTriThucTe:F2} kN",
                GiaTriChoPhep = $"{kqCayChong.GiaTriChoPhep:F2} kN",
                KetLuan = kqCayChong.KetLuan,
                MauNenTrangThai = kqCayChong.IsThoaman ? "#D1FAE5" : "#FEE2E2",
                MauChuTrangThai = kqCayChong.IsThoaman ? "#065F46" : "#991B1B"
            });

            CheckResults = results;
        }

        private void ExecuteRunAnalysis()
        {
            ExecuteUpdateResults(true);
            IsCalculated = true;
            RevitVersionStatus = "Đã hoàn thành phân tích kết cấu lúc " + DateTime.Now.ToString("HH:mm:ss");
        }


        private void ExecuteSelectSlab()
        {
            if (RevitUIDoc == null || RevitDoc == null)
            {
                _dialogService.ShowWarning("Chức năng này chỉ khả dụng khi chạy trực tiếp trong Revit.", "Thông báo");
                return;
            }

            IsSelectingSlab = true;
            StartupTab = "DuLieuView";
            _anSoCuaSo?.Invoke();
        }

        private void ExecuteSweepSelectSlab()
        {
            if (RevitUIDoc == null || RevitDoc == null)
            {
                _dialogService.ShowWarning("Chức năng này chỉ khả dụng khi chạy trực tiếp trong Revit.", "Thông báo");
                return;
            }

            IsSweepSelectingSlab = true;
            StartupTab = "DuLieuView";
            _anSoCuaSo?.Invoke();
        }

        public void ExecuteSelectSlabFromKetQua()
        {
            if (RevitUIDoc == null || RevitDoc == null)
            {
                _dialogService.ShowWarning("Chức năng này chỉ khả dụng khi chạy trực tiếp trong Revit.", "Thông báo");
                return;
            }

            IsSelectingSlabFromKetQua = true;
            StartupTab = "KetQuaView";
            _anSoCuaSo?.Invoke();
        }

        public void ExecuteSweepSelectSlabFromKetQua()
        {
            if (RevitUIDoc == null || RevitDoc == null)
            {
                _dialogService.ShowWarning("Chức năng này chỉ khả dụng khi chạy trực tiếp trong Revit.", "Thông báo");
                return;
            }

            IsSweepSelectingSlabFromKetQua = true;
            StartupTab = "KetQuaView";
            _anSoCuaSo?.Invoke();
        }

        public void ExecuteSelectSlabFromTinhToan()
        {
            if (RevitUIDoc == null || RevitDoc == null)
            {
                _dialogService.ShowWarning("Chức năng này chỉ khả dụng khi chạy trực tiếp trong Revit.", "Thông báo");
                return;
            }

            IsSelectingSlabFromTinhToan = true;
            StartupTab = "TinhToanView";
            _anSoCuaSo?.Invoke();
        }

        public void ExecuteSweepSelectSlabFromTinhToan()
        {
            if (RevitUIDoc == null || RevitDoc == null)
            {
                _dialogService.ShowWarning("Chức năng này chỉ khả dụng khi chạy trực tiếp trong Revit.", "Thông báo");
                return;
            }

            IsSweepSelectingSlabFromTinhToan = true;
            StartupTab = "TinhToanView";
            _anSoCuaSo?.Invoke();
        }

        public void ProcessSelectedSlab(System.Collections.Generic.IList<Reference> references)
        {
            if (references != null && references.Count > 0)
            {
                double totalArea = 0;
                double totalPerimeter = 0;
                double firstThicknessMm = 0;
                string firstFloorName = "";

                SelectedSlabFromKetQuaUniqueIds.Clear();

                foreach (var refer in references)
                {
                    Element element = RevitDoc.GetElement(refer.ElementId);
                    if (element is Floor floor)
                    {
                        SelectedSlabFromKetQuaUniqueIds.Add(floor.UniqueId);

                        if (firstThicknessMm == 0)
                        {
                            firstFloorName = floor.Name;
                            double thicknessFeet = 0;
                            Parameter thicknessParam = floor.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM);
                            if (thicknessParam != null)
                            {
                                thicknessFeet = thicknessParam.AsDouble();
                            }
                            else
                            {
                                FloorType floorType = floor.Document.GetElement(floor.GetTypeId()) as FloorType;
                                if (floorType != null)
                                {
                                    Parameter typeThicknessParam = floorType.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM);
                                    if (typeThicknessParam != null)
                                        thicknessFeet = typeThicknessParam.AsDouble();
                                }
                            }
                            firstThicknessMm = thicknessFeet * 304.8;
                        }

                        Parameter areaParam = floor.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                        if (areaParam != null)
                        {
                            double areaSqFt = areaParam.AsDouble();
                            totalArea += areaSqFt * 0.092903; // convert sq ft to m2
                        }

                        Parameter perimeterParam = floor.get_Parameter(BuiltInParameter.HOST_PERIMETER_COMPUTED);
                        if (perimeterParam != null)
                        {
                            double perimeterFt = perimeterParam.AsDouble();
                            totalPerimeter += perimeterFt * 0.3048; // convert ft to m
                        }
                    }
                }

                if (SelectedSlabFromKetQuaUniqueIds.Count > 0)
                {
                    SlabThickness = firstThicknessMm;
                    SelectedSlabArea = totalArea > 0 ? totalArea : 100.0;
                    SelectedSlabPerimeter = totalPerimeter > 0 ? totalPerimeter : 40.0;

                    AutoDesignFormwork();

                    RevitVersionStatus = $"Đã chọn {SelectedSlabFromKetQuaUniqueIds.Count} sàn | Chiều dày tham chiếu: {firstThicknessMm:F0} mm";
                    _dialogService.ShowInfo(
                                            $"Đã chọn thành công {SelectedSlabFromKetQuaUniqueIds.Count} sàn từ Revit!\n" +
                                            $"- Sàn mẫu: {firstFloorName}\n" +
                                            $"- Chiều dày: {firstThicknessMm:F0} mm\n" +
                                            $"- Tổng diện tích: {SelectedSlabArea:F2} m²\n\n" +
                                            $"Hệ thống đã TỰ ĐỘNG TÍNH TOÁN và đưa ra phương án bố trí ván khuôn dựa trên chiều dày sàn này.",
                                            "Revit API Integration");
                }
            }
        }

        private void AutoDesignFormwork()
        {
            double strength = WoodStrength > 0 ? WoodStrength : 15.0;
            double modulus = ElasticModulus > 0 ? ElasticModulus : 9000.0;
            double concreteWt = ConcreteWeight > 0 ? ConcreteWeight : 25.0;

            var vanMat = new Models.CauKienModel("Ván mặt", Models.LoaiTietDien.VanMat, "Gỗ dán phủ phim", 1000, VanMatThickness, VanMatThickness);
            var xaGoPhu = new Models.CauKienModel("Xà gồ phụ", Models.LoaiTietDien.XaGo, "Gỗ xẻ", XaGoPhuWidth, XaGoPhuHeight);
            var xaGoChinh = new Models.CauKienModel("Xà gồ chính", Models.LoaiTietDien.XaGo, "Gỗ xẻ", XaGoChinhWidth, XaGoChinhHeight);
            var cayChong = new Models.CauKienModel("Cây chống", Models.LoaiTietDien.CayChong, "Thép ống", CayChongDiameter, CayChongDiameter, CayChongThickness);

            double q_bt = concreteWt * (SlabThickness / 1000.0) * DeadLoadSafetyFactor;
            double q_vk = WoodFormworkWeight * DeadLoadSafetyFactor;
            double q_ll = ConstructionLiveLoad * LiveLoadSafetyFactor;
            double q_tt = q_bt + q_vk + q_ll;
            double q_tc = concreteWt * (SlabThickness / 1000.0) + WoodFormworkWeight + ConstructionLiveLoad;

            double limitSlabRatio = ParseFraction(SlabDeflectionLimit);
            double limitJoistRatio = ParseFraction(JoistDeflectionLimit);

            // 1. Khoảng cách xà gồ phụ (L1)
            double q1_tt = q_tt * 1.0;
            double q1_tc = q_tc * 1.0;
            double l1_stress = Math.Sqrt(10.0 * strength * vanMat.MomenKhangUon_W / q1_tt);
            double l1_def = limitSlabRatio > 0
                ? Math.Pow((384.0 * modulus * vanMat.MomenQuanTinh_I * limitSlabRatio) / (5.0 * q1_tc), 1.0 / 3.0)
                : Math.Pow((384.0 * modulus * vanMat.MomenQuanTinh_I * 2.0) / (5.0 * q1_tc), 1.0 / 4.0);

            double L1 = Math.Min(l1_stress, l1_def);
            L1 = Math.Floor(L1 / 50.0) * 50.0;
            Joist1Spacing = Math.Max(100.0, Math.Min(L1, 600.0));

            // 2. Khoảng cách xà gồ chính (L2)
            double q2_tt = q_tt * (Joist1Spacing / 1000.0);
            double j1Limit = strength * MoistureFactor;
            double l2_stress = Math.Sqrt(10.0 * j1Limit * xaGoPhu.MomenKhangUon_W / q2_tt);

            double L2 = l2_stress;
            L2 = Math.Floor(L2 / 50.0) * 50.0;
            Joist2Spacing = Math.Max(300.0, Math.Min(L2, 1200.0));

            // 3. Khoảng cách cây chống (L3)
            double q3_tc = q_tc * (Joist2Spacing / 1000.0);
            double l3_def = limitJoistRatio > 0
                ? Math.Pow((384.0 * modulus * xaGoChinh.MomenQuanTinh_I * limitJoistRatio) / (5.0 * q3_tc), 1.0 / 3.0)
                : Math.Pow((384.0 * modulus * xaGoChinh.MomenQuanTinh_I * 4.0) / (5.0 * q3_tc), 1.0 / 4.0);

            double shoreLimit = (cayChong.DienTichMatCat_A * 210.0 / 1000.0);
            shoreLimit = Math.Min(shoreLimit, 25.0);
            double l3_shore = (shoreLimit * 1000000.0) / (q_tt * Joist2Spacing);

            double L3 = Math.Min(l3_def, l3_shore);
            L3 = Math.Floor(L3 / 50.0) * 50.0;
            SupportSpacing = Math.Max(300.0, Math.Min(L3, 1500.0));

            ExecuteUpdateResults(false);
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

            // Tự động cập nhật kết quả khi các thông số thay đổi
            if (name == nameof(WoodStrength) || name == nameof(ElasticModulus) ||
                name == nameof(SlabDeflectionLimit) || name == nameof(JoistDeflectionLimit) ||
                name == nameof(WoodFormworkWeight) || name == nameof(ConcreteWeight) ||
                name == nameof(ConstructionLiveLoad) || name == nameof(DeadLoadSafetyFactor) ||
                name == nameof(LiveLoadSafetyFactor) || name == nameof(MoistureFactor) ||
                name == nameof(TemperatureFactor) || name == nameof(Joist1Spacing) ||
                name == nameof(Joist2Spacing) || name == nameof(SupportSpacing) ||
                name == nameof(SlabThickness) || name == nameof(VanMatThickness) ||
                name == nameof(XaGoPhuWidth) || name == nameof(XaGoPhuHeight) ||
                name == nameof(XaGoChinhWidth) || name == nameof(XaGoChinhHeight) ||
                name == nameof(CayChongDiameter) || name == nameof(CayChongThickness))
            {
                ExecuteUpdateResults(false);
            }
        }

        #region IDataErrorInfo Implementation

        public string Error => null;

        public string this[string columnName]
        {
            get
            {
                string error = null;
                switch (columnName)
                {
                    case nameof(WoodStrength):
                    case nameof(ElasticModulus):
                    case nameof(WoodFormworkWeight):
                    case nameof(ConcreteWeight):
                    case nameof(ConstructionLiveLoad):
                    case nameof(DeadLoadSafetyFactor):
                    case nameof(LiveLoadSafetyFactor):
                    case nameof(MoistureFactor):
                    case nameof(TemperatureFactor):
                    case nameof(Joist1Spacing):
                    case nameof(Joist2Spacing):
                    case nameof(SupportSpacing):
                    case nameof(VanMatThickness):
                    case nameof(XaGoPhuWidth):
                    case nameof(XaGoPhuHeight):
                    case nameof(XaGoChinhWidth):
                    case nameof(XaGoChinhHeight):
                    case nameof(CayChongDiameter):
                    case nameof(CayChongThickness):
                        var valueObj = GetType().GetProperty(columnName)?.GetValue(this);
                        if (valueObj is double val && val <= 0)
                        {
                            error = "Giá trị phải là một số thực dương.";
                        }
                        break;
                    case nameof(SlabDeflectionLimit):
                    case nameof(JoistDeflectionLimit):
                        string valLimit = (string)GetType().GetProperty(columnName)?.GetValue(this);
                        if (ParseFraction(valLimit) <= 0)
                        {
                            error = "Độ võng giới hạn không hợp lệ (ví dụ hợp lệ: 1/250 hoặc 0.004).";
                        }
                        break;
                }
                return error;
            }
        }

        #endregion
    }
}