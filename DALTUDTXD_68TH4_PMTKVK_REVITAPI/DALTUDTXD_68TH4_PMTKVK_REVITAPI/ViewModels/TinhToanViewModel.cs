using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels
{
    public class TinhToanViewModel : INotifyPropertyChanged
    {
        private readonly DuLieuViewModel _duLieuViewModel;
        
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

        private string _joist2StressColor;
        private string _joist2StressTextColor;
        private string _joist2DeflectionColor;
        private string _joist2DeflectionTextColor;

        private string _supportLoadColor;
        private string _supportLoadTextColor;

        private string _revitVersionStatus;

        public event PropertyChangedEventHandler PropertyChanged;

        public TinhToanViewModel(DuLieuViewModel duLieuViewModel)
        {
            _duLieuViewModel = duLieuViewModel ?? throw new ArgumentNullException(nameof(duLieuViewModel));

            _duLieuViewModel.PropertyChanged += (s, e) => 
            {
                if (e.PropertyName == nameof(DuLieuViewModel.WoodStrength) ||
                    e.PropertyName == nameof(DuLieuViewModel.ElasticModulus) ||
                    e.PropertyName == nameof(DuLieuViewModel.SlabThickness) ||
                    e.PropertyName == nameof(DuLieuViewModel.VanMatThickness) ||
                    e.PropertyName == nameof(DuLieuViewModel.XaGoPhuWidth) ||
                    e.PropertyName == nameof(DuLieuViewModel.XaGoPhuHeight) ||
                    e.PropertyName == nameof(DuLieuViewModel.XaGoChinhWidth) ||
                    e.PropertyName == nameof(DuLieuViewModel.XaGoChinhHeight) ||
                    e.PropertyName == nameof(DuLieuViewModel.CayChongDiameter) ||
                    e.PropertyName == nameof(DuLieuViewModel.CayChongThickness) ||
                    e.PropertyName == nameof(DuLieuViewModel.Joist1Spacing) ||
                    e.PropertyName == nameof(DuLieuViewModel.Joist2Spacing) ||
                    e.PropertyName == nameof(DuLieuViewModel.SupportSpacing) ||
                    e.PropertyName == nameof(DuLieuViewModel.WoodFormworkWeight) ||
                    e.PropertyName == nameof(DuLieuViewModel.ConcreteWeight) ||
                    e.PropertyName == nameof(DuLieuViewModel.ConstructionLiveLoad) ||
                    e.PropertyName == nameof(DuLieuViewModel.DeadLoadSafetyFactor) ||
                    e.PropertyName == nameof(DuLieuViewModel.LiveLoadSafetyFactor) ||
                    e.PropertyName == nameof(DuLieuViewModel.MoistureFactor) ||
                    e.PropertyName == nameof(DuLieuViewModel.TemperatureFactor) ||
                    e.PropertyName == nameof(DuLieuViewModel.SlabDeflectionLimit) ||
                    e.PropertyName == nameof(DuLieuViewModel.JoistDeflectionLimit))
                {
                    ExecuteRecalculate();
                }
                else if (e.PropertyName == nameof(_duLieuViewModel.IsCalculated) && _duLieuViewModel.IsCalculated)
                {
                    ExecuteRecalculate();
                    _duLieuViewModel.IsCalculated = false; // Reset sau khi nhận
                }
            };

            _revitVersionStatus = "Revit API - Kiểm tra ứng suất và độ võng";

            RecalculateCommand = new RelayCommand(o => ExecuteRecalculate());
            ChangeTabCommand = new RelayCommand(ExecuteChangeTab);

            ExecuteRecalculate();
        }

        // Các thuộc tính
        public string IsSystemSafeColor { get => _isSystemSafeColor; set { _isSystemSafeColor = value; OnPropertyChanged(); } }
        public string IsSystemSafeBorderColor { get => _isSystemSafeBorderColor; set { _isSystemSafeBorderColor = value; OnPropertyChanged(); } }
        public string IsSystemSafeIconColor { get => _isSystemSafeIconColor; set { _isSystemSafeIconColor = value; OnPropertyChanged(); } }
        public string IsSystemSafeTextColor { get => _isSystemSafeTextColor; set { _isSystemSafeTextColor = value; OnPropertyChanged(); } }
        public string SystemSafeTitle { get => _systemSafeTitle; set { _systemSafeTitle = value; OnPropertyChanged(); } }
        public string SystemSafeDescription { get => _systemSafeDescription; set { _systemSafeDescription = value; OnPropertyChanged(); } }

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
        public string Joist1DeflectionColor { get => _joist1DeflectionColor; set { _joist1DeflectionColor = value; OnPropertyChanged(); } }
        public string Joist1DeflectionTextColor { get => _joist1DeflectionTextColor; set { _joist1DeflectionTextColor = value; OnPropertyChanged(); } }

        public string Joist2StressColor { get => _joist2StressColor; set { _joist2StressColor = value; OnPropertyChanged(); } }
        public string Joist2StressTextColor { get => _joist2StressTextColor; set { _joist2StressTextColor = value; OnPropertyChanged(); } }
        public string Joist2DeflectionColor { get => _joist2DeflectionColor; set { _joist2DeflectionColor = value; OnPropertyChanged(); } }
        public string Joist2DeflectionTextColor { get => _joist2DeflectionTextColor; set { _joist2DeflectionTextColor = value; OnPropertyChanged(); } }

        public string SupportLoadColor { get => _supportLoadColor; set { _supportLoadColor = value; OnPropertyChanged(); } }
        public string SupportLoadTextColor { get => _supportLoadTextColor; set { _supportLoadTextColor = value; OnPropertyChanged(); } }

        public string RevitVersionStatus { get => _revitVersionStatus; set { _revitVersionStatus = value; OnPropertyChanged(); } }

        public ICommand RecalculateCommand { get; }
        public ICommand ChangeTabCommand { get; }

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

        private void ExecuteRecalculate()
        {
            double strength = _duLieuViewModel.WoodStrength > 0 ? _duLieuViewModel.WoodStrength : 15.0;
            double modulus = _duLieuViewModel.ElasticModulus > 0 ? _duLieuViewModel.ElasticModulus : 9000.0;
            double joist1Sp = _duLieuViewModel.Joist1Spacing > 0 ? _duLieuViewModel.Joist1Spacing : 400.0;
            double joist2Sp = _duLieuViewModel.Joist2Spacing > 0 ? _duLieuViewModel.Joist2Spacing : 1000.0;
            double suppSp = _duLieuViewModel.SupportSpacing > 0 ? _duLieuViewModel.SupportSpacing : 1000.0;
            double concreteWt = _duLieuViewModel.ConcreteWeight > 0 ? _duLieuViewModel.ConcreteWeight : 25.0;
            double moistureFactor = _duLieuViewModel.MoistureFactor;

            // Khởi tạo các mô hình cấu kiện
            var vanMat = new Models.CauKienModel("Ván mặt", Models.LoaiTietDien.VanMat, "Gỗ dán phủ phim", 1000, _duLieuViewModel.VanMatThickness, _duLieuViewModel.VanMatThickness);
            var xaGoPhu = new Models.CauKienModel("Xà gồ phụ", Models.LoaiTietDien.XaGo, "Gỗ xẻ", _duLieuViewModel.XaGoPhuWidth, _duLieuViewModel.XaGoPhuHeight);
            var xaGoChinh = new Models.CauKienModel("Xà gồ chính", Models.LoaiTietDien.XaGo, "Gỗ xẻ", _duLieuViewModel.XaGoChinhWidth, _duLieuViewModel.XaGoChinhHeight);
            var cayChong = new Models.CauKienModel("Cây chống", Models.LoaiTietDien.CayChong, "Thép ống", _duLieuViewModel.CayChongDiameter, _duLieuViewModel.CayChongDiameter, _duLieuViewModel.CayChongThickness);

            // Tải trọng tính toán
            double q_bt = concreteWt * (_duLieuViewModel.SlabThickness / 1000.0) * _duLieuViewModel.DeadLoadSafetyFactor; 
            double q_vk = _duLieuViewModel.WoodFormworkWeight * _duLieuViewModel.DeadLoadSafetyFactor; 
            double q_ll = _duLieuViewModel.ConstructionLiveLoad * _duLieuViewModel.LiveLoadSafetyFactor; 
            double q_tt = q_bt + q_vk + q_ll; 
            double q_tc = concreteWt * (_duLieuViewModel.SlabThickness / 1000.0) + _duLieuViewModel.WoodFormworkWeight + _duLieuViewModel.ConstructionLiveLoad;

            // 1. Ván mặt
            double q1_tt = q_tt * 1.0; 
            double M1 = (q1_tt * Math.Pow(joist1Sp, 2)) / 10.0; 
            double plyStress = M1 / vanMat.MomenKhangUon_W; 
            var kqVanMatStress = new Models.KetQuaTinhToan("Ván mặt", "Ứng suất", Models.LoaiKiemTra.CuongDoUon, "-", plyStress, strength, "MPa");
            
            double q1_tc = q_tc * 1.0; 
            double plyDef = (5.0 * q1_tc * Math.Pow(joist1Sp, 4)) / (384.0 * modulus * vanMat.MomenQuanTinh_I); 
            double limitSlabRatio = ParseFraction(_duLieuViewModel.SlabDeflectionLimit);
            double plyDefLimit = limitSlabRatio > 0 ? joist1Sp * limitSlabRatio : 2.0;
            var kqVanMatDef = new Models.KetQuaTinhToan("Ván mặt", "Độ võng", Models.LoaiKiemTra.DoVong, "-", plyDef, plyDefLimit, "mm");

            PlywoodStressActual = $"{kqVanMatStress.GiaTriThucTe:F2}";
            PlywoodStressLimit = $"{kqVanMatStress.GiaTriChoPhep:F2}";
            PlywoodStressRatio = kqVanMatStress.PhanTramSuDung;
            PlywoodDeflectionActual = $"{kqVanMatDef.GiaTriThucTe:F2}";
            PlywoodDeflectionLimit = $"{kqVanMatDef.GiaTriChoPhep:F2}";
            PlywoodDeflectionRatio = kqVanMatDef.PhanTramSuDung;
            
            PlywoodStressColor = kqVanMatStress.IsThoaman ? "#10B981" : "#EF4444";
            PlywoodStressTextColor = kqVanMatStress.IsThoaman ? "#2C3E50" : "#EF4444";
            PlywoodDeflectionColor = kqVanMatDef.IsThoaman ? "#10B981" : "#EF4444";
            PlywoodDeflectionTextColor = kqVanMatDef.IsThoaman ? "#2C3E50" : "#EF4444";

            bool isPlywoodOk = kqVanMatStress.IsThoaman && kqVanMatDef.IsThoaman;
            PlywoodStatusColor = isPlywoodOk ? "#D1FAE5" : "#FEE2E2";
            PlywoodStatusText = isPlywoodOk ? "ĐẠT" : "KHÔNG ĐẠT";
            PlywoodStatusTextColor = isPlywoodOk ? "#065F46" : "#991B1B";

            // 2. Xà gồ phụ
            double q2_tt = q_tt * (joist1Sp / 1000.0); 
            double M2 = (q2_tt * Math.Pow(joist2Sp, 2)) / 10.0;
            double j1Stress = M2 / xaGoPhu.MomenKhangUon_W;
            double j1Limit = strength * moistureFactor; 
            var kqXaGoPhuStress = new Models.KetQuaTinhToan("Xà gồ phụ", "Ứng suất", Models.LoaiKiemTra.CuongDoUon, "-", j1Stress, j1Limit, "MPa");

            // Xà gồ phụ độ võng
            double q2_tc = q_tc * (joist1Sp / 1000.0);
            double j1Def = (5.0 * q2_tc * Math.Pow(joist2Sp, 4)) / (384.0 * modulus * xaGoPhu.MomenQuanTinh_I);
            double limitJoist1Ratio = ParseFraction(_duLieuViewModel.JoistDeflectionLimit);
            double j1DefLimit = limitJoist1Ratio > 0 ? joist2Sp * limitJoist1Ratio : 4.0;
            var kqXaGoPhuDef = new Models.KetQuaTinhToan("Xà gồ phụ", "Độ võng", Models.LoaiKiemTra.DoVong, "-", j1Def, j1DefLimit, "mm");

            Joist1StressActual = $"{kqXaGoPhuStress.GiaTriThucTe:F2}";
            Joist1StressLimit = $"{kqXaGoPhuStress.GiaTriChoPhep:F2}";
            Joist1StressRatio = kqXaGoPhuStress.PhanTramSuDung;
            Joist1DeflectionActual = $"{kqXaGoPhuDef.GiaTriThucTe:F2}";
            Joist1DeflectionLimit = $"{kqXaGoPhuDef.GiaTriChoPhep:F2}";
            Joist1DeflectionRatio = kqXaGoPhuDef.PhanTramSuDung;

            Joist1StressColor = kqXaGoPhuStress.IsThoaman ? "#10B981" : "#EF4444";
            Joist1StressTextColor = kqXaGoPhuStress.IsThoaman ? "#2C3E50" : "#EF4444";
            Joist1DeflectionColor = kqXaGoPhuDef.IsThoaman ? "#10B981" : "#EF4444";
            Joist1DeflectionTextColor = kqXaGoPhuDef.IsThoaman ? "#2C3E50" : "#EF4444";

            bool isJoist1Ok = kqXaGoPhuStress.IsThoaman && kqXaGoPhuDef.IsThoaman;
            Joist1StatusColor = isJoist1Ok ? "#D1FAE5" : "#FEE2E2";
            Joist1StatusText = isJoist1Ok ? "ĐẠT" : "KHÔNG ĐẠT";
            Joist1StatusTextColor = isJoist1Ok ? "#065F46" : "#991B1B";

            // 3. Xà gồ chính
            double q3_tt = q_tt * (joist2Sp / 1000.0);
            double M3 = (q3_tt * Math.Pow(suppSp, 2)) / 10.0;
            double j2Stress = M3 / xaGoChinh.MomenKhangUon_W;
            double j2Limit = strength * moistureFactor;
            var kqXaGoChinhStress = new Models.KetQuaTinhToan("Xà gồ chính", "Ứng suất", Models.LoaiKiemTra.CuongDoUon, "-", j2Stress, j2Limit, "MPa");

            double q3_tc = q_tc * (joist2Sp / 1000.0); 
            double j2Def = (5.0 * q3_tc * Math.Pow(suppSp, 4)) / (384.0 * modulus * xaGoChinh.MomenQuanTinh_I);
            double limitJoist2Ratio = ParseFraction(_duLieuViewModel.JoistDeflectionLimit);
            double j2DefLimit = limitJoist2Ratio > 0 ? suppSp * limitJoist2Ratio : 4.0;
            var kqXaGoChinhDef = new Models.KetQuaTinhToan("Xà gồ chính", "Độ võng", Models.LoaiKiemTra.DoVong, "-", j2Def, j2DefLimit, "mm");

            Joist2StressActual = $"{kqXaGoChinhStress.GiaTriThucTe:F2}";
            Joist2StressLimit = $"{kqXaGoChinhStress.GiaTriChoPhep:F2}";
            Joist2StressRatio = kqXaGoChinhStress.PhanTramSuDung;
            Joist2DeflectionActual = $"{kqXaGoChinhDef.GiaTriThucTe:F2}";
            Joist2DeflectionLimit = $"{kqXaGoChinhDef.GiaTriChoPhep:F2}";
            Joist2DeflectionRatio = kqXaGoChinhDef.PhanTramSuDung;

            Joist2StressColor = kqXaGoChinhStress.IsThoaman ? "#10B981" : "#EF4444";
            Joist2StressTextColor = kqXaGoChinhStress.IsThoaman ? "#2C3E50" : "#EF4444";
            Joist2DeflectionColor = kqXaGoChinhDef.IsThoaman ? "#10B981" : "#EF4444";
            Joist2DeflectionTextColor = kqXaGoChinhDef.IsThoaman ? "#2C3E50" : "#EF4444";

            bool isJoist2Ok = kqXaGoChinhStress.IsThoaman && kqXaGoChinhDef.IsThoaman;
            Joist2StatusColor = isJoist2Ok ? "#D1FAE5" : "#FEE2E2";
            Joist2StatusText = isJoist2Ok ? "ĐẠT" : "KHÔNG ĐẠT";
            Joist2StatusTextColor = isJoist2Ok ? "#065F46" : "#991B1B";

            // 4. Cây chống
            double N_tt = q_tt * (joist2Sp / 1000.0) * (suppSp / 1000.0); // kN
            double shoreLimit = cayChong.DienTichMatCat_A * 210.0 / 1000.0; 
            shoreLimit = Math.Min(shoreLimit, 25.0); 
            var kqCayChong = new Models.KetQuaTinhToan("Cây chống", "Sức chịu tải", Models.LoaiKiemTra.ChiuLucDoc, "-", N_tt, shoreLimit, "kN");

            SupportLoadActual = $"{kqCayChong.GiaTriThucTe:F2}";
            SupportLoadLimit = $"{kqCayChong.GiaTriChoPhep:F2}";
            SupportLoadRatio = kqCayChong.PhanTramSuDung;
            
            SupportLoadColor = kqCayChong.IsThoaman ? "#10B981" : "#EF4444";
            SupportLoadTextColor = kqCayChong.IsThoaman ? "#2C3E50" : "#EF4444";

            // Giả định hệ số uốn dọc thay đổi theo chiều cao tầng (mặc định lấy 0.85 cho minh họa)
            SupportSlendernessFactor = "0.85";
            
            bool isSupportOk = kqCayChong.IsThoaman;
            SupportStatusColor = isSupportOk ? "#D1FAE5" : "#FEE2E2";
            SupportStatusText = isSupportOk ? "ĐẠT" : "KHÔNG ĐẠT";
            SupportStatusTextColor = isSupportOk ? "#065F46" : "#991B1B";

            // 5. Kết luận hệ thống
            bool isSystemOk = isPlywoodOk && isJoist1Ok && isJoist2Ok && isSupportOk;
            if (isSystemOk)
            {
                IsSystemSafeColor = "#D1FAE5";
                IsSystemSafeBorderColor = "#10B981";
                IsSystemSafeIconColor = "#10B981";
                IsSystemSafeTextColor = "#065F46";
                SystemSafeTitle = "HỆ THỐNG ĐẠT YÊU CẦU AN TOÀN KỸ THUẬT";
                SystemSafeDescription = "Tất cả các cấu kiện ván khuôn và đà giáo đều đảm bảo khả năng chịu lực và độ võng nằm trong giới hạn cho phép theo TCVN 10307:2014.";
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

        private void ExecuteChangeTab(object parameter)
        {
            _duLieuViewModel.NavigateAction?.Invoke(parameter?.ToString());
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
