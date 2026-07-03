using System;
using System.Collections.Generic;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models
{
    public class CalculationParameters
    {
        public double WoodStrength { get; set; } = 15.0;
        public double PlywoodBendingStrength { get; set; } = 10.0;
        public double WoodShearStrength { get; set; } = 2.0;
        public double ElasticModulus { get; set; } = 9000.0;
        public double ConcreteWeight { get; set; } = 25.0;
        public double WoodFormworkWeight { get; set; } = 0.3;
        public double ConstructionLiveLoad { get; set; } = 2.5;
        public double DynamicImpactFactor { get; set; } = 1.3;
        public double DeadLoadSafetyFactor { get; set; } = 1.2;
        public double LiveLoadSafetyFactor { get; set; } = 1.3;
        public double MoistureFactor { get; set; } = 0.85;
        public double TemperatureFactor { get; set; } = 1.0;

        public static CalculationParameters Default => new CalculationParameters();

        public double SlabDeflectionLimitRatio { get; set; } = 1.0 / 400.0;
        public double JoistDeflectionLimitRatio { get; set; } = 1.0 / 400.0;

        public double SlabThickness { get; set; }
        public double VanMatThickness { get; set; }
        
        public double XaGoPhuWidth { get; set; }
        public double XaGoPhuHeight { get; set; }
        
        public double XaGoChinhWidth { get; set; }
        public double XaGoChinhHeight { get; set; }
        
        public double CayChongDiameter { get; set; }
        public double CayChongThickness { get; set; }
        public double CayChongHeight { get; set; }
        
        public int SoNhipVanMat { get; set; } = 2;
        public int SoNhipXaGoPhu { get; set; } = 3;
        public int SoNhipXaGoChinh { get; set; } = 3;

        public double Joist1Spacing { get; set; }
        public double Joist2Spacing { get; set; }
        public double SupportSpacing { get; set; }
        
        public string SelectedFloorDetailName { get; set; }
    }

    public class ComponentResult
    {
        public KetQuaTinhToan StressResult { get; set; }
        public KetQuaTinhToan DeflectionResult { get; set; }
        public KetQuaTinhToan ShearResult { get; set; }
        public bool IsOk => (StressResult == null || StressResult.IsThoaman) && 
                            (DeflectionResult == null || DeflectionResult.IsThoaman) &&
                            (ShearResult == null || ShearResult.IsThoaman);
    }

    public class CalculationResult
    {
        public ComponentResult Plywood { get; set; } = new ComponentResult();
        public ComponentResult Joist1 { get; set; } = new ComponentResult();
        public ComponentResult Joist2 { get; set; } = new ComponentResult();
        public ComponentResult Support { get; set; } = new ComponentResult();
        
        public double SupportPhi { get; set; }
        public List<KetQuaHienThi> ReportItems { get; set; } = new List<KetQuaHienThi>();

        public bool IsSystemOk => Plywood.IsOk && Joist1.IsOk && Joist2.IsOk && Support.IsOk;
    }

    public class AutoDesignResult
    {
        public double L1 { get; set; }
        public double L2 { get; set; }
        public double L3 { get; set; }
    }

    public static class FormworkCalculationService
    {
        public static double ParseFraction(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return 0;
            if (input.Contains("/"))
            {
                var parts = input.Split('/');
                if (parts.Length == 2 && double.TryParse(parts[0], out double num) && double.TryParse(parts[1], out double den) && den != 0)
                {
                    return num / den;
                }
            }
            if (double.TryParse(input, out double direct)) return direct;
            return 0;
        }

        private static double GetMomentDivisor(int soNhip)
        {
            if (soNhip == 1 || soNhip == 2) return 8.0;
            return 10.0;
        }

        // Hệ số lực cắt: V = coeff * q * L
        // Dầm đơn giản: V = 0.5·q·L, dầm liên tục (≥3 nhịp): V = 0.6·q·L
        private static double GetShearCoefficient(int soNhip)
        {
            if (soNhip <= 2) return 0.5;
            return 0.6;
        }

        private static double CalculateBucklingCoefficient(double lambda)
        {
            if (lambda <= 20) return 1.0;
            if (lambda <= 100) return 1.0 - 0.006 * (lambda - 20);
            return 5200.0 / (lambda * lambda); // Đảm bảo hàm liên tục tại lambda = 100 (phi = 0.52)
        }

        public static CalculationResult CheckSafety(CalculationParameters p)
        {
            var res = new CalculationResult();
            
            // Khởi tạo mô hình
            var vanMat = new CauKienModel("Ván mặt", LoaiTietDien.VanMat, "Gỗ dán", 1000, p.VanMatThickness, p.VanMatThickness);
            var xaGoPhu = new CauKienModel("Xà gồ phụ", LoaiTietDien.XaGo, "Gỗ", p.XaGoPhuWidth, p.XaGoPhuHeight);
            var xaGoChinh = new CauKienModel("Xà gồ chính", LoaiTietDien.XaGo, "Gỗ", p.XaGoChinhWidth, p.XaGoChinhHeight);
            var cayChong = new CauKienModel("Cây chống", LoaiTietDien.CayChong, "Thép", p.CayChongDiameter, p.CayChongDiameter, p.CayChongThickness);

            // Tải trọng
            double q_bt = p.ConcreteWeight * (p.SlabThickness / 1000.0) * p.DeadLoadSafetyFactor; 
            double q_vk = p.WoodFormworkWeight * p.DeadLoadSafetyFactor; 
            double q_ll = p.ConstructionLiveLoad * p.LiveLoadSafetyFactor * p.DynamicImpactFactor; 
            double q_tt = q_bt + q_vk + q_ll; 
            double q_tc = p.ConcreteWeight * (p.SlabThickness / 1000.0) + p.WoodFormworkWeight + (p.ConstructionLiveLoad * p.DynamicImpactFactor);

            string prefix = !string.IsNullOrEmpty(p.SelectedFloorDetailName) ? $"[{p.SelectedFloorDetailName} - Dày {p.SlabThickness:F0}mm] " : "";

            // 1. Ván mặt (tải phân bố trên dải rộng 1m, nên q_ván_mặt = q_tt)
            double mDivPlywood = GetMomentDivisor(p.SoNhipVanMat);
            double M1 = (q_tt * Math.Pow(p.Joist1Spacing, 2)) / mDivPlywood; 
            double plyStress = M1 / vanMat.MomenKhangUon_W; 
            double plyLimit = p.PlywoodBendingStrength * p.MoistureFactor * p.TemperatureFactor;
            res.Plywood.StressResult = new KetQuaTinhToan("Ván mặt", "Ứng suất", LoaiKiemTra.CuongDoUon, "σ = M/W ≤ Ru", plyStress, plyLimit, "MPa");
            
            double plyDef = (5.0 * q_tc * Math.Pow(p.Joist1Spacing, 4)) / (384.0 * p.ElasticModulus * vanMat.MomenQuanTinh_I); 
            double plyDefLimit = p.SlabDeflectionLimitRatio > 0 ? p.Joist1Spacing * p.SlabDeflectionLimitRatio : 2.0;
            res.Plywood.DeflectionResult = new KetQuaTinhToan("Ván mặt", "Độ võng", LoaiKiemTra.DoVong, "f = 5qL^4/384EI ≤ [f]", plyDef, plyDefLimit, "mm");

            // 2. Xà gồ phụ
            double mDivJoist1 = GetMomentDivisor(p.SoNhipXaGoPhu);
            double q2_tt = q_tt * (p.Joist1Spacing / 1000.0); 
            double M2 = (q2_tt * Math.Pow(p.Joist2Spacing, 2)) / mDivJoist1;
            double j1Stress = M2 / xaGoPhu.MomenKhangUon_W;
            double j1Limit = p.WoodStrength * p.MoistureFactor * p.TemperatureFactor; 
            res.Joist1.StressResult = new KetQuaTinhToan("Xà gồ phụ", "Ứng suất", LoaiKiemTra.CuongDoUon, "σ = M/W ≤ Ru", j1Stress, j1Limit, "MPa");

            double q2_tc = q_tc * (p.Joist1Spacing / 1000.0);
            double j1Def = (5.0 * q2_tc * Math.Pow(p.Joist2Spacing, 4)) / (384.0 * p.ElasticModulus * xaGoPhu.MomenQuanTinh_I);
            double j1DefLimit = p.JoistDeflectionLimitRatio > 0 ? p.Joist2Spacing * p.JoistDeflectionLimitRatio : 4.0;
            res.Joist1.DeflectionResult = new KetQuaTinhToan("Xà gồ phụ", "Độ võng", LoaiKiemTra.DoVong, "f = 5qL^4/384EI ≤ [f]", j1Def, j1DefLimit, "mm");

            // 2c. Kiểm tra cắt xà gồ phụ (TCVN 1072:1971)
            double shearCoeffJoist1 = GetShearCoefficient(p.SoNhipXaGoPhu);
            double V1 = shearCoeffJoist1 * q2_tt * (p.Joist2Spacing / 1000.0); // kN
            double j1Area = xaGoPhu.DienTichMatCat_A; // mm²
            double j1ShearStress = j1Area > 0 ? (1.5 * V1 * 1000.0) / j1Area : 0; // MPa (V convert kN→N)
            double j1ShearLimit = p.WoodShearStrength * p.MoistureFactor * p.TemperatureFactor;
            res.Joist1.ShearResult = new KetQuaTinhToan("Xà gồ phụ", "Cắt", LoaiKiemTra.CuongDoCat, "τ = 1.5V/A ≤ [τ]", j1ShearStress, j1ShearLimit, "MPa");

            // 3. Xà gồ chính
            double mDivJoist2 = GetMomentDivisor(p.SoNhipXaGoChinh);
            double q3_tt = q_tt * (p.Joist2Spacing / 1000.0);
            double M3 = (q3_tt * Math.Pow(p.SupportSpacing, 2)) / mDivJoist2;
            double j2Stress = M3 / xaGoChinh.MomenKhangUon_W;
            double j2Limit = p.WoodStrength * p.MoistureFactor * p.TemperatureFactor;
            res.Joist2.StressResult = new KetQuaTinhToan("Xà gồ chính", "Ứng suất", LoaiKiemTra.CuongDoUon, "σ = M/W ≤ Ru", j2Stress, j2Limit, "MPa");

            double q3_tc = q_tc * (p.Joist2Spacing / 1000.0); 
            double j2Def = (5.0 * q3_tc * Math.Pow(p.SupportSpacing, 4)) / (384.0 * p.ElasticModulus * xaGoChinh.MomenQuanTinh_I);
            double j2DefLimit = p.JoistDeflectionLimitRatio > 0 ? p.SupportSpacing * p.JoistDeflectionLimitRatio : 4.0;
            res.Joist2.DeflectionResult = new KetQuaTinhToan("Xà gồ chính", "Độ võng", LoaiKiemTra.DoVong, "f = 5qL^4/384EI ≤ [f]", j2Def, j2DefLimit, "mm");

            // 3c. Kiểm tra cắt xà gồ chính (TCVN 1072:1971)
            double shearCoeffJoist2 = GetShearCoefficient(p.SoNhipXaGoChinh);
            double V2 = shearCoeffJoist2 * q3_tt * (p.SupportSpacing / 1000.0); // kN
            double j2Area = xaGoChinh.DienTichMatCat_A; // mm²
            double j2ShearStress = j2Area > 0 ? (1.5 * V2 * 1000.0) / j2Area : 0; // MPa
            double j2ShearLimit = p.WoodShearStrength * p.MoistureFactor * p.TemperatureFactor;
            res.Joist2.ShearResult = new KetQuaTinhToan("Xà gồ chính", "Cắt", LoaiKiemTra.CuongDoCat, "τ = 1.5V/A ≤ [τ]", j2ShearStress, j2ShearLimit, "MPa");

            // 4. Cây chống
            double N_tt = q_tt * (p.Joist2Spacing / 1000.0) * (p.SupportSpacing / 1000.0); 
            
            double r = cayChong.BanKinhQuanTinh_r;
            double lambda = r > 0 ? p.CayChongHeight / r : 0;
            double phi = CalculateBucklingCoefficient(lambda);
            res.SupportPhi = phi;

            double shoreLimit = phi * cayChong.DienTichMatCat_A * 210.0 / 1000.0; 
            shoreLimit = Math.Min(shoreLimit, 25.0); 
            res.Support.StressResult = new KetQuaTinhToan("Cây chống", "Sức chịu tải", LoaiKiemTra.ChiuLucDoc, "N ≤ [P]", N_tt, shoreLimit, "kN");

            // Xây dựng List báo cáo cho SharedDataViewModel
            res.ReportItems.Add(BuildReportItem(prefix, "Kiểm tra cường độ ván khuôn (σ ≤ Ru)", res.Plywood.StressResult));
            res.ReportItems.Add(BuildReportItem(prefix, "Kiểm tra độ võng ván khuôn (f_tt ≤ [f])", res.Plywood.DeflectionResult));
            res.ReportItems.Add(BuildReportItem(prefix, "Kiểm tra cường độ xà gồ phụ (Thanh phụ)", res.Joist1.StressResult));
            res.ReportItems.Add(BuildReportItem(prefix, "Kiểm tra cắt xà gồ phụ (τ ≤ [τ])", res.Joist1.ShearResult));
            res.ReportItems.Add(BuildReportItem(prefix, "Kiểm tra độ võng xà gồ phụ", res.Joist1.DeflectionResult));
            res.ReportItems.Add(BuildReportItem(prefix, "Kiểm tra cường độ xà gồ chính (Thanh chính)", res.Joist2.StressResult));
            res.ReportItems.Add(BuildReportItem(prefix, "Kiểm tra cắt xà gồ chính (τ ≤ [τ])", res.Joist2.ShearResult));
            res.ReportItems.Add(BuildReportItem(prefix, "Kiểm tra độ võng xà gồ chính", res.Joist2.DeflectionResult));
            res.ReportItems.Add(BuildReportItem(prefix, "Kiểm tra sức chịu tải cây chống đứng (P_tt ≤ P_cp)", res.Support.StressResult));

            return res;
        }

        private static KetQuaHienThi BuildReportItem(string prefix, string title, KetQuaTinhToan kq)
        {
            return new KetQuaHienThi
            {
                NoiDungKiemTra = prefix + title,
                GiaTriThucTe = $"{kq.GiaTriThucTe:F2} {kq.DonVi}",
                GiaTriChoPhep = $"{kq.GiaTriChoPhep:F2} {kq.DonVi}",
                KetLuan = kq.KetLuan,
                MauNenTrangThai = kq.IsThoaman ? "#D1FAE5" : "#FEE2E2",
                MauChuTrangThai = kq.IsThoaman ? "#065F46" : "#991B1B"
            };
        }

        public static AutoDesignResult AutoDesign(CalculationParameters p)
        {
            var vanMat = new CauKienModel("Ván mặt", LoaiTietDien.VanMat, "Gỗ dán", 1000, p.VanMatThickness, p.VanMatThickness);
            var xaGoPhu = new CauKienModel("Xà gồ phụ", LoaiTietDien.XaGo, "Gỗ", p.XaGoPhuWidth, p.XaGoPhuHeight);
            var xaGoChinh = new CauKienModel("Xà gồ chính", LoaiTietDien.XaGo, "Gỗ", p.XaGoChinhWidth, p.XaGoChinhHeight);
            var cayChong = new CauKienModel("Cây chống", LoaiTietDien.CayChong, "Thép", p.CayChongDiameter, p.CayChongDiameter, p.CayChongThickness);

            double q_bt = p.ConcreteWeight * (p.SlabThickness / 1000.0) * p.DeadLoadSafetyFactor; 
            double q_vk = p.WoodFormworkWeight * p.DeadLoadSafetyFactor; 
            double q_ll = p.ConstructionLiveLoad * p.LiveLoadSafetyFactor; 
            double q_tt = q_bt + q_vk + q_ll; 
            double q_tc = p.ConcreteWeight * (p.SlabThickness / 1000.0) + p.WoodFormworkWeight + p.ConstructionLiveLoad;

            // 1. Khoảng cách xà gồ phụ (L1) — ván mặt trên dải rộng 1m
            double l1_stress = Math.Sqrt(GetMomentDivisor(p.SoNhipVanMat) * p.WoodStrength * vanMat.MomenKhangUon_W / q_tt);
            double l1_def = p.SlabDeflectionLimitRatio > 0 
                ? Math.Pow((384.0 * p.ElasticModulus * vanMat.MomenQuanTinh_I * p.SlabDeflectionLimitRatio) / (5.0 * q_tc), 1.0/3.0)
                : Math.Pow((384.0 * p.ElasticModulus * vanMat.MomenQuanTinh_I * 2.0) / (5.0 * q_tc), 1.0/4.0);
            
            double L1 = Math.Min(l1_stress, l1_def);
            L1 = Math.Floor(L1 / 50.0) * 50.0;
            L1 = Math.Max(100.0, Math.Min(L1, 600.0));

            // 2. Khoảng cách xà gồ chính (L2)
            double q2_tt = q_tt * (L1 / 1000.0);
            double j1Limit = p.WoodStrength * p.MoistureFactor;
            double l2_stress = Math.Sqrt(GetMomentDivisor(p.SoNhipXaGoPhu) * j1Limit * xaGoPhu.MomenKhangUon_W / q2_tt);
            
            double q2_tc = q_tc * (L1 / 1000.0);
            double l2_def = p.JoistDeflectionLimitRatio > 0
                ? Math.Pow((384.0 * p.ElasticModulus * xaGoPhu.MomenQuanTinh_I * p.JoistDeflectionLimitRatio) / (5.0 * q2_tc), 1.0/3.0)
                : Math.Pow((384.0 * p.ElasticModulus * xaGoPhu.MomenQuanTinh_I * 4.0) / (5.0 * q2_tc), 1.0/4.0);

            double L2 = Math.Min(l2_stress, l2_def);
            L2 = Math.Floor(L2 / 50.0) * 50.0;
            L2 = Math.Max(300.0, Math.Min(L2, 1200.0));

            // 3. Khoảng cách cây chống (L3)
            double q3_tt = q_tt * (L2 / 1000.0);
            double j2Limit = p.WoodStrength * p.MoistureFactor;
            double l3_stress = Math.Sqrt(GetMomentDivisor(p.SoNhipXaGoChinh) * j2Limit * xaGoChinh.MomenKhangUon_W / q3_tt);

            double q3_tc = q_tc * (L2 / 1000.0);
            double l3_def = p.JoistDeflectionLimitRatio > 0
                ? Math.Pow((384.0 * p.ElasticModulus * xaGoChinh.MomenQuanTinh_I * p.JoistDeflectionLimitRatio) / (5.0 * q3_tc), 1.0/3.0)
                : Math.Pow((384.0 * p.ElasticModulus * xaGoChinh.MomenQuanTinh_I * 4.0) / (5.0 * q3_tc), 1.0/4.0);
            
            double r = cayChong.BanKinhQuanTinh_r;
            double lambda = r > 0 ? p.CayChongHeight / r : 0;
            double phi = CalculateBucklingCoefficient(lambda);
            double shoreLimit = phi * cayChong.DienTichMatCat_A * 210.0 / 1000.0; 
            shoreLimit = Math.Min(shoreLimit, 25.0); 

            double l3_shore = (shoreLimit * 1000000.0) / (q_tt * L2);

            double L3 = Math.Min(l3_stress, Math.Min(l3_def, l3_shore));
            L3 = Math.Floor(L3 / 50.0) * 50.0;
            L3 = Math.Max(300.0, Math.Min(L3, 1500.0));

            return new AutoDesignResult { L1 = L1, L2 = L2, L3 = L3 };
        }

        public static double FixSupportSpacing(CalculationParameters p)
        {
            var xaGoChinh = new CauKienModel("Xà gồ chính", LoaiTietDien.XaGo, "Gỗ", p.XaGoChinhWidth, p.XaGoChinhHeight);
            var cayChong = new CauKienModel("Cây chống", LoaiTietDien.CayChong, "Thép", p.CayChongDiameter, p.CayChongDiameter, p.CayChongThickness);

            double q_bt = p.ConcreteWeight * (p.SlabThickness / 1000.0) * p.DeadLoadSafetyFactor; 
            double q_vk = p.WoodFormworkWeight * p.DeadLoadSafetyFactor; 
            double q_ll = p.ConstructionLiveLoad * p.LiveLoadSafetyFactor; 
            double q_tt = q_bt + q_vk + q_ll; 
            double q_tc = p.ConcreteWeight * (p.SlabThickness / 1000.0) + p.WoodFormworkWeight + p.ConstructionLiveLoad;

            // Tính L3 dựa trên L2 hiện tại (p.Joist2Spacing)
            double L2 = p.Joist2Spacing > 0 ? p.Joist2Spacing : 1000.0;

            double q3_tt = q_tt * (L2 / 1000.0);
            double j2Limit = p.WoodStrength * p.MoistureFactor;
            double l3_stress = Math.Sqrt(GetMomentDivisor(p.SoNhipXaGoChinh) * j2Limit * xaGoChinh.MomenKhangUon_W / q3_tt);

            double q3_tc = q_tc * (L2 / 1000.0);
            double l3_def = p.JoistDeflectionLimitRatio > 0
                ? Math.Pow((384.0 * p.ElasticModulus * xaGoChinh.MomenQuanTinh_I * p.JoistDeflectionLimitRatio) / (5.0 * q3_tc), 1.0/3.0)
                : Math.Pow((384.0 * p.ElasticModulus * xaGoChinh.MomenQuanTinh_I * 4.0) / (5.0 * q3_tc), 1.0/4.0);
            
            double r = cayChong.BanKinhQuanTinh_r;
            double lambda = r > 0 ? p.CayChongHeight / r : 0;
            double phi = CalculateBucklingCoefficient(lambda);
            double shoreLimit = phi * cayChong.DienTichMatCat_A * 210.0 / 1000.0; 
            shoreLimit = Math.Min(shoreLimit, 25.0); 

            double l3_shore = (shoreLimit * 1000000.0) / (q_tt * L2);

            double L3 = Math.Min(l3_stress, Math.Min(l3_def, l3_shore));
            L3 = Math.Floor(L3 / 50.0) * 50.0;
            return Math.Max(300.0, Math.Min(L3, 1500.0));
        }
    }
}
