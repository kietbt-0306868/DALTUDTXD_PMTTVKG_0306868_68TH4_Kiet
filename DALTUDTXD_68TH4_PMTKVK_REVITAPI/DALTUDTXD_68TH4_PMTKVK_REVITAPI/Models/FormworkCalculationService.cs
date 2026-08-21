using System;
using System.Collections.Generic;
using System.Linq;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models
{
    public class CalculationParameters
    {
        public CalculationParameters Clone() => (CalculationParameters)MemberwiseClone();

        public double WoodStrength { get; set; } = 15.0;
        public double PlywoodBendingStrength { get; set; } = 10.0;
        public double WoodShearStrength { get; set; } = 2.0;
        public double ElasticModulus { get; set; } = 9000.0;
        public double ConcreteWeight { get; set; } = 24.0;
        public double SteelWeight { get; set; } = 1.0;
        public double WoodFormworkWeight { get; set; } = 0.3;
        public double ConstructionLiveLoad { get; set; } = 2.5;
        public double PouringImpactLoad { get; set; } = 2.0;
        public double VibrationLoad { get; set; } = 2.0;
        public double DeadLoadSafetyFactor { get; set; } = 1.2;
        public double LiveLoadSafetyFactor { get; set; } = 1.3;
        public double DynamicImpactFactor { get; set; } = 1.3;
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
        public double CayChongHeight { get; set; } = 3600.0;
        public double SupportEffectiveLengthFactor { get; set; } = 1.0;
        public double SupportSteelYieldStrength { get; set; } = 210.0;
        public double SupportRatedCapacity { get; set; } = 25.0;
        public double SupportSlendernessLimit { get; set; } = 200.0;
        public double SupportBasePlateWidth { get; set; } = 150.0;
        public double SupportBasePlateDepth { get; set; } = 150.0;
        public double SupportBaseBearingAllowable { get; set; } = 10.0;
        public double SupportSolePlateWidth { get; set; } = 500.0;
        public double SupportSolePlateDepth { get; set; } = 500.0;
        public double SupportGroundBearingAllowable { get; set; } = 0.15;
        public double SupportHeadPlateWidth { get; set; } = 150.0;
        public double SupportHeadPlateDepth { get; set; } = 150.0;
        public double SupportHeadBearingAllowable { get; set; } = 8.0;
        public double SupportJackExtension { get; set; } = 300.0;
        public double SupportJackExtensionLimit { get; set; } = 600.0;
        
        public int SoNhipVanMat { get; set; } = 2;
        public int SoNhipXaGoPhu { get; set; } = 3;
        public int SoNhipXaGoChinh { get; set; } = 3;

        public double Joist1Spacing { get; set; }
        public double Joist2Spacing { get; set; }
        public double SupportSpacing { get; set; }

        public string SelectedFloorDetailName { get; set; }
        public bool IsFloorSelected { get; set; } = false;
        public bool IsGroundLevel { get; set; } = false;

        public StructuralComponentType ComponentType { get; set; } = StructuralComponentType.Slab;

        // Intersection-aware quantities. These describe exposed formwork geometry,
        // never the concrete dimensions imported from Revit.
        public bool HasIntersectionContext { get; set; }
        public double NetPlywoodAreaM2 { get; set; }
        public double NetBottomFormworkAreaM2 { get; set; }
        public double NetSideFormworkAreaM2 { get; set; }
        public double NetTopFormworkAreaM2 { get; set; }
        public double NetInternalOpeningSideAreaM2 { get; set; }
        public double NetExposedPerimeterM { get; set; }
        public double NetFormworkLengthMm { get; set; }
        public string[] IntersectingElementUniqueIds { get; set; } = new string[0];

        // Beam Formwork Parameters
        public double BeamWidth { get; set; } = 200.0;
        public double BeamLength { get; set; } = 4000.0;
        public double BeamDepth { get; set; } = 400.0;
        public double BeamSidePlywoodThickness { get; set; } = 18.0;
        public double BeamBottomPlywoodThickness { get; set; } = 18.0;
        public double BeamSideYokeSpacing { get; set; } = 400.0;
        public double BeamBottomYokeSpacing { get; set; } = 200.0;
        public double BeamPropSpacing { get; set; } = 1200.0;
        public double BeamSecondaryJoistWidth { get; set; } = 50.0;
        public double BeamSecondaryJoistDepth { get; set; } = 50.0;
        public double BeamPrimaryWalerWidth { get; set; } = 50.0;
        public double BeamPrimaryWalerDepth { get; set; } = 100.0;
        public double BeamPrimaryWalerSpacing { get; set; } = 1200.0;
        public double BeamScaffoldBayWidth { get; set; } = 1200.0;
        public double BeamTieRodDiameter { get; set; } = 16.0;
        public double BeamTieRodSpacing { get; set; } = 1200.0;
        public double BeamTieRodAllowableCapacity { get; set; } = 20.0;
        // Fixed rods laid across the slab surface are a perimeter-beam accessory.
        public double BeamEdgeSurfaceTieRodDiameter { get; set; } = 12.0;
        public double BeamEdgeSurfaceTieRodSpacing { get; set; } = 1000.0;
        public double BeamEdgeSurfaceTieRodAnchorLength { get; set; } = 1200.0;
        public double BeamEdgeSurfaceTieRodAllowableCapacity { get; set; } = 12.0;
        public double BeamSideBraceSpacing { get; set; } = 1200.0;
        public double BeamSideBraceAngle { get; set; } = 55.0;
        public double BeamSideBraceDiameter { get; set; } = 48.0;
        public double BeamSideBraceThickness { get; set; } = 2.0;
        public int BeamSideBraceCountPerStation { get; set; } = 1;
        public double BeamSideBraceAllowableCapacity { get; set; } = 15.0;
        // 0: tự động/kiểm tra cả hai; 1: dầm giữa (ty giằng); 2: dầm biên (chống xiên).
        public int BeamPlacementModeIndex { get; set; } = 0;
        public double BeamStrippingStrengthPercent { get; set; } = 70.0;
        public double BeamVibratorOverlap { get; set; } = 75.0;
        public bool IsBeamSelected { get; set; } = false;

        // Column Formwork Parameters
        public double ColumnWidth { get; set; } = 200.0;
        public double ColumnDepth { get; set; } = 200.0;
        public double ColumnHeight { get; set; } = 3600.0;
        public double ColumnPlywoodThickness { get; set; } = 18.0;
        public double ColumnStiffenerWidth { get; set; } = 40.0;
        public double ColumnStiffenerDepth { get; set; } = 40.0;
        public double ColumnStiffenerSpacing { get; set; } = 200.0;
        public double ColumnYokeSpacing { get; set; } = 400.0;
        public bool ColumnUseZonedYokeSpacing { get; set; } = true;
        public double ColumnFirstYokeOffset { get; set; } = 150.0;
        public double ColumnYokeBottomSpacing { get; set; } = 300.0;
        public double ColumnYokeUpperSpacing { get; set; } = 500.0;
        public double ColumnYokeTransitionHeight { get; set; } = 1000.0;
        public double ColumnLastYokeOffset { get; set; } = 150.0;
        public double ColumnYokeWidth { get; set; } = 40.0;
        public double ColumnYokeHeight { get; set; } = 80.0;
        public double ColumnYokeThickness { get; set; } = 2.0;
        public int ColumnYokePairCount { get; set; } = 2;
        public int ColumnYokeMaterialIndex { get; set; } = 1; // 0: gỗ đặc, 1: thép hộp
        public double ColumnTieRodDiameter { get; set; } = 12.0;
        public int ColumnTieRodCountPerFace { get; set; } = 2;
        public double ColumnTieRodAllowableCapacity { get; set; } = 12.0;
        public int ColumnBraceCount { get; set; } = 4;
        public int ColumnSupportedBraceDirectionCount { get; set; } = 4;
        public double ColumnBraceAngle { get; set; } = 55.0;
        public double ColumnBraceAllowableCapacity { get; set; } = 15.0;
        // 0: open site; 1: keep feet inside the overall structural footprint;
        // 2: strict middle-plot mode, keep feet on the supporting floor footprint.
        public int ColumnBracePlacementModeIndex { get; set; } = 2;
        public double ColumnNeighbourTieThreshold { get; set; } = 5000.0;
        public bool ColumnIsBoundaryOrCorner { get; set; } = false;
        public double ColumnCableAllowableCapacity { get; set; } = 10.0;
        public double ColumnTurnbuckleAllowableCapacity { get; set; } = 10.0;
        public double ColumnAnchorAllowableCapacity { get; set; } = 12.0;
        public double ColumnAxisOffset { get; set; } = 1000.0;
        public double ColumnPlumbTargetTolerance { get; set; } = 3.0;
        public double ColumnFormworkTopExtension { get; set; } = 100.0;
        public double ColumnHoseBottomClearance { get; set; } = 1500.0;
        public double ColumnPourLiftHeight { get; set; } = 2500.0;
        public double ColumnBeamStopOffset { get; set; } = 50.0;
        public double ColumnStripTimeHours { get; set; } = 36.0;
        public double ColumnCuringDays { get; set; } = 2.0;
        public bool ColumnUseWorkPlatform { get; set; } = true;
        public double ColumnWorkPlatformWidth { get; set; } = 900.0;
        public double ColumnGuardrailHeight { get; set; } = 1100.0;
        public double ConcretePourRate { get; set; } = 2.0; // m/h
        public double ConcreteTemperature { get; set; } = 25.0; // °C
        public bool IsColumnSelected { get; set; } = false;
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
        // General / Slab
        public ComponentResult Plywood { get; set; } = new ComponentResult();
        public ComponentResult Joist1 { get; set; } = new ComponentResult();
        public ComponentResult Joist2 { get; set; } = new ComponentResult();
        public ComponentResult Support { get; set; } = new ComponentResult();
        public ComponentResult SupportSlenderness { get; set; } = new ComponentResult();
        public ComponentResult SupportEulerBuckling { get; set; } = new ComponentResult();
        public ComponentResult SupportBaseBearing { get; set; } = new ComponentResult();
        public ComponentResult SupportSoleBearing { get; set; } = new ComponentResult();
        public ComponentResult SupportHeadBearing { get; set; } = new ComponentResult();
        public ComponentResult SupportJack { get; set; } = new ComponentResult();
        // Beam specific
        public ComponentResult BeamSidePlywood { get; set; } = new ComponentResult();
        public ComponentResult BeamSideYoke { get; set; } = new ComponentResult();
        public ComponentResult BeamBottomPlywood { get; set; } = new ComponentResult();
        public ComponentResult BeamBottomYoke { get; set; } = new ComponentResult();
        public ComponentResult BeamSecondaryJoist { get; set; } = new ComponentResult();
        public ComponentResult BeamPrimaryWaler { get; set; } = new ComponentResult();
        public ComponentResult BeamTieRod { get; set; } = new ComponentResult();
        public ComponentResult BeamEdgeSurfaceTieRod { get; set; } = new ComponentResult();
        public ComponentResult BeamSideBrace { get; set; } = new ComponentResult();
        public ComponentResult BeamStripping { get; set; } = new ComponentResult();
        public ComponentResult BeamVibrationProcedure { get; set; } = new ComponentResult();
        public double BeamDesignBottomLoad { get; set; }
        public double BeamServiceBottomLoad { get; set; }
        public double BeamSideResultantPerMetre { get; set; }
        public double BeamSupportReaction { get; set; }
        public int BeamBottomPanelStiffenerCount { get; set; }
        public int BeamSidePanelStiffenerCount { get; set; }
        public int BeamClampStationCount { get; set; }
        public string BeamClampLayoutText { get; set; }
        public string BeamLoadPathText { get; set; }
        
        // Column specific
        public ComponentResult ColumnPlywood { get; set; } = new ComponentResult();
        public ComponentResult ColumnStiffener { get; set; } = new ComponentResult();
        public ComponentResult ColumnYoke { get; set; } = new ComponentResult();
        public ComponentResult ColumnTieRod { get; set; } = new ComponentResult();
        public ComponentResult ColumnBrace { get; set; } = new ComponentResult();
        public ComponentResult ColumnBraceBaseBearing { get; set; } = new ComponentResult();
        public ComponentResult ColumnCable { get; set; } = new ComponentResult();
        public ComponentResult ColumnTurnbuckle { get; set; } = new ComponentResult();
        public ComponentResult ColumnAnchor { get; set; } = new ComponentResult();
        public ComponentResult ColumnPlumb { get; set; } = new ComponentResult();
        public ComponentResult ColumnPouring { get; set; } = new ComponentResult();
        public double ColumnDesignPressure { get; set; }
        public double ColumnHydrostaticPressure { get; set; }
        public double ColumnPressureCap { get; set; }
        public double ColumnCriticalStiffenerSpan { get; set; }
        public double ColumnCriticalStiffenerPressure { get; set; }
        public double ColumnCriticalYokeTributaryHeight { get; set; }
        public double ColumnCriticalYokePressure { get; set; }
        public int ColumnWidthFaceStiffenerCount { get; set; }
        public int ColumnDepthFaceStiffenerCount { get; set; }
        public int ColumnYokeLevelCount { get; set; }
        public string ColumnYokeLayoutText { get; set; }
        
        public double SupportPhi { get; set; }
        public bool VerticalSupportApplicable { get; set; } = true;
        public List<KetQuaHienThi> ReportItems { get; set; } = new List<KetQuaHienThi>();

        public bool IsSystemOk { get; set; } = true;

        public IEnumerable<KetQuaTinhToan> GetAllChecks()
        {
            ComponentResult[] components =
            {
                Plywood, Joist1, Joist2,
                BeamSidePlywood, BeamSideYoke, BeamBottomPlywood,
                BeamSecondaryJoist, BeamPrimaryWaler, BeamTieRod, BeamEdgeSurfaceTieRod, BeamSideBrace,
                BeamStripping, BeamVibrationProcedure,
                ColumnPlywood, ColumnStiffener, ColumnYoke, ColumnTieRod, ColumnBrace, ColumnBraceBaseBearing,
                ColumnCable, ColumnTurnbuckle, ColumnAnchor, ColumnPlumb, ColumnPouring
            };
            foreach (ComponentResult component in components)
            {
                if (component == null) continue;
                if (component.StressResult != null) yield return component.StressResult;
                if (component.ShearResult != null) yield return component.ShearResult;
                if (component.DeflectionResult != null) yield return component.DeflectionResult;
            }
            if (VerticalSupportApplicable)
            {
                ComponentResult[] supportComponents =
                {
                    Support, SupportSlenderness, SupportEulerBuckling,
                    SupportBaseBearing, SupportSoleBearing, SupportHeadBearing, SupportJack
                };
                foreach (ComponentResult component in supportComponents)
                {
                    if (component.StressResult != null) yield return component.StressResult;
                    if (component.ShearResult != null) yield return component.ShearResult;
                    if (component.DeflectionResult != null) yield return component.DeflectionResult;
                }
            }
        }
    }

    public class AutoDesignResult
    {
        public double L1 { get; set; }
        public double L2 { get; set; }
        public double L3 { get; set; }
    }

    public class ComplianceDesignResult
    {
        public bool IsCompliant { get; set; }
        public int Iterations { get; set; }
        public CalculationParameters Parameters { get; set; }
        public List<string> Adjustments { get; } = new List<string>();
        public List<string> UnresolvedChecks { get; } = new List<string>();
        public string Summary => IsCompliant
            ? $"Đã thiết kế đạt yêu cầu sau {Iterations} vòng lặp; cập nhật {Adjustments.Count} thông số."
            : $"Bộ phụ kiện chưa vượt qua {UnresolvedChecks.Count} phép kiểm tra an toàn.";
    }

    public static class FormworkCalculationService
    {
        // The nested film-faced-plywood family shown in the tutorials arrays its
        // 50x50 stiffeners with: if(B < 300 mm, 2, B / 300 mm + 1).
        // Ceiling is used for non-modular widths so the realised bay never exceeds
        // the requested maximum spacing.
        public const double MaximumPanelStiffenerSpacingMm = 300.0;
        public const double TutorialBeamClampEndOffsetMm = 500.0;
        public const double MinimumColumnYokeSpacingMm = 300.0;
        public const double MinimumColumnUpperYokeSpacingMm = 400.0;
        public const double MaximumColumnYokeSpacingMm = 800.0;

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

        private static void CheckVerticalSupport(
            CalculationResult res, CalculationParameters p, CauKienModel support, double designReaction,
            bool includeGroundBearing, string supportName)
        {
            double height = Math.Max(100.0, p.CayChongHeight);
            double effectiveLengthFactor = Math.Max(0.5, p.SupportEffectiveLengthFactor);
            double effectiveLength = effectiveLengthFactor * height;
            double radius = Math.Max(0.01, support.BanKinhQuanTinh_r);
            double slenderness = effectiveLength / radius;
            double slendernessLimit = Math.Max(20.0, p.SupportSlendernessLimit);
            double phi = CalculateBucklingCoefficient(slenderness);
            res.SupportPhi = phi;

            double steelStrength = Math.Max(1.0, p.SupportSteelYieldStrength);
            double sectionCapacity = phi * support.DienTichMatCat_A * steelStrength / 1000.0;
            double ratedCapacity = Math.Max(0.1, p.SupportRatedCapacity);
            double eulerCapacity = Math.PI * Math.PI * 200000.0 * support.MomenQuanTinh_I /
                                   (effectiveLength * effectiveLength) / 1000.0;
            double eulerDesignCapacity = Math.Max(0.1, eulerCapacity / 1.5);
            double axialCapacity = Math.Max(0.1, Math.Min(ratedCapacity, Math.Min(sectionCapacity, eulerDesignCapacity)));

            res.Support.StressResult = new KetQuaTinhToan(supportName, "Nén dọc trục", LoaiKiemTra.ChiuLucDoc,
                "N_Ed=q_d·A_trib ≤ min(P_rated; φ·A·f_y; N_cr/γ_b)", designReaction, axialCapacity, "kN");
            res.SupportSlenderness.StressResult = new KetQuaTinhToan(supportName, "Độ mảnh", LoaiKiemTra.Khac,
                "λ=K·L/r ≤ [λ]", slenderness, slendernessLimit, "-");
            res.SupportEulerBuckling.StressResult = new KetQuaTinhToan(supportName, "Ổn định Euler", LoaiKiemTra.ChiuLucDoc,
                "N_Ed ≤ N_cr/γ_b; N_cr=π²EI/(K·L)²; γ_b=1.5", designReaction, eulerDesignCapacity, "kN");

            double baseArea = Math.Max(100.0, p.SupportBasePlateWidth * p.SupportBasePlateDepth);
            double baseStress = designReaction * 1000.0 / baseArea;
            res.SupportBaseBearing.StressResult = new KetQuaTinhToan("Bản đế cây chống", "Ép mặt cục bộ", LoaiKiemTra.Khac,
                "σ_b=N_Ed/(b_p·l_p) ≤ [σ_b]", baseStress, Math.Max(0.01, p.SupportBaseBearingAllowable), "MPa");

            double headArea = Math.Max(100.0, p.SupportHeadPlateWidth * p.SupportHeadPlateDepth);
            double headStress = designReaction * 1000.0 / headArea;
            res.SupportHeadBearing.StressResult = new KetQuaTinhToan("Đầu U/bản đầu", "Ép mặt dưới xà gồ chính", LoaiKiemTra.Khac,
                "σ_h=N_Ed/(b_h·l_h) ≤ [σ_h]", headStress, Math.Max(0.01, p.SupportHeadBearingAllowable), "MPa");

            res.SupportJack.StressResult = new KetQuaTinhToan("Kích điều chỉnh", "Độ vươn kích", LoaiKiemTra.Khac,
                "e_jack ≤ [e_jack]", Math.Max(0.0, p.SupportJackExtension),
                Math.Max(1.0, p.SupportJackExtensionLimit), "mm");

            if (includeGroundBearing)
            {
                double soleArea = Math.Max(100.0, p.SupportSolePlateWidth * p.SupportSolePlateDepth);
                double groundPressure = designReaction * 1000.0 / soleArea;
                res.SupportSoleBearing.StressResult = new KetQuaTinhToan("Tấm kê/nền", "Sức chịu tải nền", LoaiKiemTra.Khac,
                    "q_g=N_Ed/(b_s·l_s) ≤ q_allow", groundPressure,
                    Math.Max(0.001, p.SupportGroundBearingAllowable), "MPa");
            }
        }

        private static bool IsVerticalSupportSystemOk(CalculationResult res)
        {
            return res.Support.IsOk && res.SupportSlenderness.IsOk && res.SupportEulerBuckling.IsOk &&
                   res.SupportBaseBearing.IsOk && res.SupportSoleBearing.IsOk &&
                   res.SupportHeadBearing.IsOk && res.SupportJack.IsOk;
        }

        private static void AddVerticalSupportReportItems(
            CalculationResult res, string prefix, bool isSelected, bool includeGroundBearing)
        {
            res.ReportItems.Add(BuildReportItem(prefix, "Chống đứng: nén dọc trục", res.Support.StressResult, isSelected));
            res.ReportItems.Add(BuildReportItem(prefix, "Chống đứng: độ mảnh", res.SupportSlenderness.StressResult, isSelected));
            res.ReportItems.Add(BuildReportItem(prefix, "Chống đứng: ổn định Euler", res.SupportEulerBuckling.StressResult, isSelected));
            res.ReportItems.Add(BuildReportItem(prefix, "Bản đế: ép mặt cục bộ", res.SupportBaseBearing.StressResult, isSelected));
            res.ReportItems.Add(BuildReportItem(prefix, "Đầu U/bản đầu: ép mặt", res.SupportHeadBearing.StressResult, isSelected));
            res.ReportItems.Add(BuildReportItem(prefix, "Kích điều chỉnh: độ vươn", res.SupportJack.StressResult, isSelected));
            if (includeGroundBearing && res.SupportSoleBearing.StressResult != null)
                res.ReportItems.Add(BuildReportItem(prefix, "Tấm kê/nền: sức chịu tải", res.SupportSoleBearing.StressResult, isSelected));
        }

        public static CalculationResult CheckSafety(CalculationParameters p)
        {
            var res = new CalculationResult();
            res.VerticalSupportApplicable = !p.IsGroundLevel;
            
            // Khởi tạo mô hình
            var vanMat = new CauKienModel("Ván mặt", LoaiTietDien.VanMat, "Gỗ dán", 1000, p.VanMatThickness, p.VanMatThickness);
            var xaGoPhu = new CauKienModel("Xà gồ phụ", LoaiTietDien.XaGo, "Gỗ", p.XaGoPhuWidth, p.XaGoPhuHeight);
            var xaGoChinh = new CauKienModel("Xà gồ chính", LoaiTietDien.XaGo, "Gỗ", p.XaGoChinhWidth, p.XaGoChinhHeight);
            var cayChong = new CauKienModel("Cây chống", LoaiTietDien.CayChong, "Thép", p.CayChongDiameter, p.CayChongDiameter, p.CayChongThickness);

            // Tải trọng
            double q_bt = (p.ConcreteWeight + p.SteelWeight) * (p.SlabThickness / 1000.0) * p.DeadLoadSafetyFactor; 
            double q_vk = p.WoodFormworkWeight * p.DeadLoadSafetyFactor; 
            double q_ll = (p.ConstructionLiveLoad + Math.Max(p.PouringImpactLoad, p.VibrationLoad)) * p.LiveLoadSafetyFactor; 
            double q_tt = q_bt + q_vk + q_ll; 
            double q_tc = (p.ConcreteWeight + p.SteelWeight) * (p.SlabThickness / 1000.0) + p.WoodFormworkWeight + (p.ConstructionLiveLoad + Math.Max(p.PouringImpactLoad, p.VibrationLoad));

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
            
            CheckVerticalSupport(res, p, cayChong, N_tt, p.IsGroundLevel, "Cây chống đứng sàn");

            // Xây dựng List báo cáo cho SharedDataViewModel
            if (!p.IsGroundLevel)
            {
                res.ReportItems.Add(BuildReportItem(prefix, "Kiểm tra cường độ ván khuôn (σ ≤ Ru)", res.Plywood.StressResult, p.IsFloorSelected));
                res.ReportItems.Add(BuildReportItem(prefix, "Kiểm tra độ võng ván khuôn (f_tt ≤ [f])", res.Plywood.DeflectionResult, p.IsFloorSelected));
                res.ReportItems.Add(BuildReportItem(prefix, "Kiểm tra cường độ xà gồ phụ (Thanh phụ)", res.Joist1.StressResult, p.IsFloorSelected));
                res.ReportItems.Add(BuildReportItem(prefix, "Kiểm tra cắt xà gồ phụ (τ ≤ [τ])", res.Joist1.ShearResult, p.IsFloorSelected));
                res.ReportItems.Add(BuildReportItem(prefix, "Kiểm tra độ võng xà gồ phụ", res.Joist1.DeflectionResult, p.IsFloorSelected));
                res.ReportItems.Add(BuildReportItem(prefix, "Kiểm tra cường độ xà gồ chính (Thanh chính)", res.Joist2.StressResult, p.IsFloorSelected));
                res.ReportItems.Add(BuildReportItem(prefix, "Kiểm tra cắt xà gồ chính (τ ≤ [τ])", res.Joist2.ShearResult, p.IsFloorSelected));
                res.ReportItems.Add(BuildReportItem(prefix, "Kiểm tra độ võng xà gồ chính", res.Joist2.DeflectionResult, p.IsFloorSelected));
                AddVerticalSupportReportItems(res, prefix, p.IsFloorSelected, false);
            }
            else
            {
                res.ReportItems.Add(new KetQuaHienThi
                {
                    NoiDungKiemTra = prefix + "Kiểm tra hệ cốp pha đáy & cây chống",
                    GiaTriThucTe = "-",
                    GiaTriChoPhep = "-",
                    KetLuan = "Không yêu cầu (Sàn bệt)",
                    MauNenTrangThai = "#E5E7EB",
                    MauChuTrangThai = "#374151"
                });
            }

            res.IsSystemOk = res.Plywood.IsOk && res.Joist1.IsOk && res.Joist2.IsOk &&
                             (p.IsGroundLevel || IsVerticalSupportSystemOk(res));
            return res;
        }

        public static CalculationResult CheckBeamSafety(CalculationParameters p)
        {
            var res = new CalculationResult();
            var sidePlywood = new CauKienModel("Ván thành dầm", LoaiTietDien.VanMat, "Gỗ dán phủ phim", 1000, p.BeamSidePlywoodThickness, p.BeamSidePlywoodThickness);
            var bottomPlywood = new CauKienModel("Ván đáy dầm", LoaiTietDien.VanMat, "Gỗ dán phủ phim", 1000, p.BeamBottomPlywoodThickness, p.BeamBottomPlywoodThickness);
            var sideYoke = new CauKienModel("Gông/nẹp thành dầm", LoaiTietDien.XaGo, "Gỗ", p.BeamPrimaryWalerWidth, p.BeamPrimaryWalerDepth);
            var secondaryJoist = new CauKienModel("Xà gồ phụ đáy dầm", LoaiTietDien.XaGo, "Gỗ", p.BeamSecondaryJoistWidth, p.BeamSecondaryJoistDepth);
            var primaryWaler = new CauKienModel("Xà gồ chính trên đầu U", LoaiTietDien.XaGo, "Gỗ", p.BeamPrimaryWalerWidth, p.BeamPrimaryWalerDepth);
            var prop = new CauKienModel("Cây chống", LoaiTietDien.CayChong, "Thép", p.CayChongDiameter, p.CayChongDiameter, p.CayChongThickness);

            // Chuỗi truyền tải trọng theo video: ván phủ phim → xà gồ phụ 50×50 @200
            // → xà gồ chính 50×100 → đầu U/Ringlock → bản đế/tấm kê.
            double q_bt_bottom = (p.ConcreteWeight + p.SteelWeight) * (p.BeamDepth / 1000.0) * p.DeadLoadSafetyFactor;
            double q_vk_bottom = p.WoodFormworkWeight * p.DeadLoadSafetyFactor;
            double q_ll_bottom = (p.ConstructionLiveLoad + Math.Max(p.PouringImpactLoad, p.VibrationLoad)) * p.LiveLoadSafetyFactor;
            double q_tt_bottom = q_bt_bottom + q_vk_bottom + q_ll_bottom;
            double q_tc_bottom = (p.ConcreteWeight + p.SteelWeight) * (p.BeamDepth / 1000.0) + p.WoodFormworkWeight + (p.ConstructionLiveLoad + Math.Max(p.PouringImpactLoad, p.VibrationLoad));
            res.BeamDesignBottomLoad = q_tt_bottom;
            res.BeamServiceBottomLoad = q_tc_bottom;

            double width_m = Math.Max(0.05, p.BeamWidth / 1000.0);
            List<double> bottomStiffeners = BuildPanelStiffenerOffsets(
                p.BeamWidth, p.BeamBottomYokeSpacing);
            List<double> sideStiffeners = BuildPanelStiffenerOffsets(
                p.BeamDepth, p.BeamSideYokeSpacing);
            double secondarySpacing = MaximumBay(bottomStiffeners);
            double sideSpacing = MaximumBay(sideStiffeners);
            res.BeamBottomPanelStiffenerCount = bottomStiffeners.Count;
            res.BeamSidePanelStiffenerCount = sideStiffeners.Count;

            double beamFormworkLength = p.HasIntersectionContext && p.NetFormworkLengthMm > 0
                ? p.NetFormworkLengthMm
                : p.BeamLength;
            List<double> clampStations = BuildBeamClampStations(beamFormworkLength, p.BeamTieRodSpacing);
            double realisedClampSpacing = MaximumBay(clampStations);
            res.BeamClampStationCount = clampStations.Count;
            res.BeamClampLayoutText = string.Join(" - ", clampStations.ConvertAll(x => x.ToString("F0")));
            double primarySpacing = Math.Max(100.0, p.BeamPrimaryWalerSpacing);
            double scaffoldBay = Math.Max(100.0, p.BeamScaffoldBayWidth);
            double mDivPlywood = GetMomentDivisor(p.SoNhipVanMat);
            double shearCoeffPlywood = GetShearCoefficient(p.SoNhipVanMat);
            double plyLimit = p.PlywoodBendingStrength * p.MoistureFactor * p.TemperatureFactor;

            // Ván đáy: dải rộng 1 m, nhịp giữa các xà gồ phụ.
            double M_bottom = q_tt_bottom * Math.Pow(secondarySpacing, 2) / mDivPlywood;
            double plyBottomStress = M_bottom / bottomPlywood.MomenKhangUon_W;
            res.BeamBottomPlywood.StressResult = new KetQuaTinhToan("Ván đáy dầm", "Ứng suất uốn", LoaiKiemTra.CuongDoUon, "σ=M/W; M=q_dL²/m ≤ R_u", plyBottomStress, plyLimit, "MPa");
            double V_bottom = shearCoeffPlywood * q_tt_bottom * secondarySpacing / 1000.0;
            double tauBottom = 1.5 * V_bottom * 1000.0 / bottomPlywood.DienTichMatCat_A;
            res.BeamBottomPlywood.ShearResult = new KetQuaTinhToan("Ván đáy dầm", "Ứng suất cắt", LoaiKiemTra.CuongDoCat, "τ=1,5V/(b·t) ≤ R_v", tauBottom, p.WoodShearStrength, "MPa");
            double plyBottomDef = 5.0 * q_tc_bottom * Math.Pow(secondarySpacing, 4) / (384.0 * p.ElasticModulus * bottomPlywood.MomenQuanTinh_I);
            double plyDefLimitBottom = p.SlabDeflectionLimitRatio > 0 ? secondarySpacing * p.SlabDeflectionLimitRatio : 2.0;
            res.BeamBottomPlywood.DeflectionResult = new KetQuaTinhToan("Ván đáy dầm", "Độ võng", LoaiKiemTra.DoVong, "f=5q_sL⁴/(384EI) ≤ L/400", plyBottomDef, plyDefLimitBottom, "mm");

            // Xà gồ phụ 50×50: nhận tải theo bề rộng ảnh hưởng bằng bước bố trí và gối lên xà gồ chính.
            double q_secondary_tt = q_tt_bottom * secondarySpacing / 1000.0;
            double q_secondary_tc = q_tc_bottom * secondarySpacing / 1000.0;
            double M_secondary = q_secondary_tt * Math.Pow(primarySpacing, 2) / GetMomentDivisor(p.SoNhipXaGoPhu);
            double woodLimit = p.WoodStrength * p.MoistureFactor * p.TemperatureFactor;
            res.BeamSecondaryJoist.StressResult = new KetQuaTinhToan("Xà gồ phụ đáy dầm", "Ứng suất uốn", LoaiKiemTra.CuongDoUon, "σ=M/W; q=q_d·s_phụ ≤ R_u", M_secondary / secondaryJoist.MomenKhangUon_W, woodLimit, "MPa");
            double V_secondary = GetShearCoefficient(p.SoNhipXaGoPhu) * q_secondary_tt * primarySpacing / 1000.0;
            res.BeamSecondaryJoist.ShearResult = new KetQuaTinhToan("Xà gồ phụ đáy dầm", "Ứng suất cắt", LoaiKiemTra.CuongDoCat, "τ=1,5V/(b·h) ≤ R_v", 1.5 * V_secondary * 1000.0 / secondaryJoist.DienTichMatCat_A, p.WoodShearStrength, "MPa");
            double fSecondary = 5.0 * q_secondary_tc * Math.Pow(primarySpacing, 4) / (384.0 * p.ElasticModulus * secondaryJoist.MomenQuanTinh_I);
            res.BeamSecondaryJoist.DeflectionResult = new KetQuaTinhToan("Xà gồ phụ đáy dầm", "Độ võng", LoaiKiemTra.DoVong, "f=5q_sL⁴/(384EI) ≤ L/400", fSecondary, primarySpacing * p.JoistDeflectionLimitRatio, "mm");

            // Xà gồ chính đặt ngang dưới dầm: tải dầm tập trung trong bề rộng dầm, không trải đều
            // trên toàn nhịp Ringlock. Dùng tải tập trung giữa nhịp là trường hợp bảo thủ.
            double P_primary_tt = q_tt_bottom * width_m * primarySpacing / 1000.0;
            double P_primary_tc = q_tc_bottom * width_m * primarySpacing / 1000.0;
            double M_primary = P_primary_tt * 1000.0 * scaffoldBay / 4.0;
            res.BeamPrimaryWaler.StressResult = new KetQuaTinhToan("Xà gồ chính trên đầu U", "Ứng suất uốn", LoaiKiemTra.CuongDoUon, "σ=M/W; M=P_dL/4; P_d=q_d·b_dầm·s_chính ≤ R_u", M_primary / primaryWaler.MomenKhangUon_W, woodLimit, "MPa");
            double V_primary = 0.5 * P_primary_tt;
            res.BeamPrimaryWaler.ShearResult = new KetQuaTinhToan("Xà gồ chính trên đầu U", "Ứng suất cắt", LoaiKiemTra.CuongDoCat, "τ=1,5V/(b·h) ≤ R_v", 1.5 * V_primary * 1000.0 / primaryWaler.DienTichMatCat_A, p.WoodShearStrength, "MPa");
            double fPrimary = P_primary_tc * 1000.0 * Math.Pow(scaffoldBay, 3) / (48.0 * p.ElasticModulus * primaryWaler.MomenQuanTinh_I);
            res.BeamPrimaryWaler.DeflectionResult = new KetQuaTinhToan("Xà gồ chính trên đầu U", "Độ võng", LoaiKiemTra.DoVong, "f=P_sL³/(48EI) ≤ L/400", fPrimary, scaffoldBay * p.JoistDeflectionLimitRatio, "mm");
            // Giữ tương thích với thẻ kết quả cũ: thẻ “đà đáy” hiển thị xà gồ chính.
            res.BeamBottomYoke = res.BeamPrimaryWaler;

            // Thành dầm: áp lực tam giác của bê tông tươi + thành phần đầm rung phân bố đều.
            double P_max_lateral = p.ConcreteWeight * (p.BeamDepth / 1000.0) * p.DeadLoadSafetyFactor;
            double P_vibrate = p.VibrationLoad * p.LiveLoadSafetyFactor;
            double q_tt_side = P_max_lateral + P_vibrate;
            double q_tc_side = (p.ConcreteWeight) * (p.BeamDepth / 1000.0) + p.VibrationLoad;
            double M_side = (q_tt_side * Math.Pow(sideSpacing, 2)) / mDivPlywood;
            double plySideStress = M_side / sidePlywood.MomenKhangUon_W;
            res.BeamSidePlywood.StressResult = new KetQuaTinhToan("Ván thành dầm", "Ứng suất uốn", LoaiKiemTra.CuongDoUon, "σ=M/W ≤ R_u", plySideStress, plyLimit, "MPa");
            double V_side = shearCoeffPlywood * q_tt_side * sideSpacing / 1000.0;
            res.BeamSidePlywood.ShearResult = new KetQuaTinhToan("Ván thành dầm", "Ứng suất cắt", LoaiKiemTra.CuongDoCat, "τ=1,5V/(b·t) ≤ R_v", 1.5 * V_side * 1000.0 / sidePlywood.DienTichMatCat_A, p.WoodShearStrength, "MPa");
            double plySideDef = (5.0 * q_tc_side * Math.Pow(sideSpacing, 4)) / (384.0 * p.ElasticModulus * sidePlywood.MomenQuanTinh_I);
            double plyDefLimitSide = p.SlabDeflectionLimitRatio > 0 ? sideSpacing * p.SlabDeflectionLimitRatio : 2.0;
            res.BeamSidePlywood.DeflectionResult = new KetQuaTinhToan("Ván thành dầm", "Độ võng", LoaiKiemTra.DoVong, "f=5q_sL⁴/(384EI) ≤ L/400", plySideDef, plyDefLimitSide, "mm");

            double q_yoke_side_tt = q_tt_side * (sideSpacing / 1000.0); // kN/m
            double q_yoke_side_tc = q_tc_side * (sideSpacing / 1000.0);
            double M_yoke_side = q_yoke_side_tt * Math.Pow(p.BeamDepth, 2) / 8.0;
            double sideYokeStress = M_yoke_side / sideYoke.MomenKhangUon_W;
            res.BeamSideYoke.StressResult = new KetQuaTinhToan("Gông/nẹp thành dầm", "Ứng suất uốn", LoaiKiemTra.CuongDoUon, "σ=M/W; M=qL²/8 ≤ R_u", sideYokeStress, woodLimit, "MPa");
            double V_yoke_side = 0.5 * q_yoke_side_tt * p.BeamDepth / 1000.0;
            res.BeamSideYoke.ShearResult = new KetQuaTinhToan("Gông/nẹp thành dầm", "Ứng suất cắt", LoaiKiemTra.CuongDoCat, "τ=1,5V/(b·h) ≤ R_v", 1.5 * V_yoke_side * 1000.0 / sideYoke.DienTichMatCat_A, p.WoodShearStrength, "MPa");
            double fYokeSide = 5.0 * q_yoke_side_tc * Math.Pow(p.BeamDepth, 4) / (384.0 * p.ElasticModulus * sideYoke.MomenQuanTinh_I);
            res.BeamSideYoke.DeflectionResult = new KetQuaTinhToan("Gông/nẹp thành dầm", "Độ võng", LoaiKiemTra.DoVong, "f=5q_sL⁴/(384EI) ≤ L/400", fYokeSide, p.BeamDepth * p.JoistDeflectionLimitRatio, "mm");

            // Tổng lực ngang trên một mét dài dầm; ty D16 và chống xiên là hai phương án giữ thành.
            double beamDepthM = Math.Max(0.05, p.BeamDepth / 1000.0);
            double horizontalResultant = 0.5 * p.ConcreteWeight * p.DeadLoadSafetyFactor * beamDepthM * beamDepthM +
                                         p.VibrationLoad * p.LiveLoadSafetyFactor * beamDepthM;
            res.BeamSideResultantPerMetre = horizontalResultant;
            double tieDemand = horizontalResultant * Math.Max(0.1, realisedClampSpacing / 1000.0);
            res.BeamTieRod.StressResult = new KetQuaTinhToan($"Ty giằng thành dầm D{p.BeamTieRodDiameter:F0}", "Khả năng chịu kéo", LoaiKiemTra.ChiuLucDoc, "T=H_d·s_ty ≤ [T]", tieDemand, Math.Max(0.1, p.BeamTieRodAllowableCapacity), "kN");
            if (p.BeamPlacementModeIndex == 2)
            {
                double surfaceTieDemand = horizontalResultant *
                    Math.Max(0.1, p.BeamEdgeSurfaceTieRodSpacing / 1000.0);
                res.BeamEdgeSurfaceTieRod.StressResult = new KetQuaTinhToan(
                    $"Ty chết trên mặt dầm biên D{p.BeamEdgeSurfaceTieRodDiameter:F0}",
                    "Khả năng chịu kéo", LoaiKiemTra.ChiuLucDoc,
                    "T=H_d·s_ty ≤ [T]", surfaceTieDemand,
                    Math.Max(0.1, p.BeamEdgeSurfaceTieRodAllowableCapacity), "kN");
                double requiredSurfaceAnchor = Math.Max(500.0,
                    40.0 * Math.Max(1.0, p.BeamEdgeSurfaceTieRodDiameter));
                res.BeamEdgeSurfaceTieRod.DeflectionResult = new KetQuaTinhToan(
                    $"Neo ty chết trên mặt dầm biên D{p.BeamEdgeSurfaceTieRodDiameter:F0}",
                    "Chiều dài neo", LoaiKiemTra.Khac,
                    "L_neo ≥ max(40d; 500 mm)", requiredSurfaceAnchor,
                    Math.Max(1.0, p.BeamEdgeSurfaceTieRodAnchorLength), "mm");
            }
            double braceAngleRad = Math.Max(30.0, Math.Min(75.0, p.BeamSideBraceAngle)) * Math.PI / 180.0;
            int braceCount = Math.Max(1, p.BeamSideBraceCountPerStation);
            double braceDemand = horizontalResultant * Math.Max(0.1, p.BeamSideBraceSpacing / 1000.0) /
                                 (braceCount * Math.Cos(braceAngleRad));
            string beamBraceName = $"Chống xiên thành dầm biên D{p.BeamSideBraceDiameter:F0}×{p.BeamSideBraceThickness:F1} ({braceCount} thanh/vị trí)";
            res.BeamSideBrace.StressResult = new KetQuaTinhToan(beamBraceName, "Lực dọc mỗi chống", LoaiKiemTra.ChiuLucDoc,
                "N=H_d·s_chống/(n·cosα) ≤ [N]", braceDemand, Math.Max(0.1, p.BeamSideBraceAllowableCapacity), "kN");

            // Phản lực tại một đầu U/Ringlock theo diện tích ảnh hưởng của một nút khung.
            double N_tt = 0.5 * P_primary_tt;
            res.BeamSupportReaction = N_tt;
            CheckVerticalSupport(res, p, prop, N_tt, p.IsGroundLevel, "Cây chống đứng dầm");

            // Kiểm tra điều kiện công nghệ được nêu trong video. Đây là điều kiện nghiệm thu,
            // không thay thế kết quả thí nghiệm cường độ bê tông tại công trường.
            double strippingRatio = 70.0 / Math.Max(1.0, p.BeamStrippingStrengthPercent);
            res.BeamStripping.StressResult = new KetQuaTinhToan("Tháo cốp pha dầm/sàn", "Ngưỡng cường độ bê tông", LoaiKiemTra.Khac, "70%/β_tháo ≤ 1,00 (chỉ tháo khi có kết quả nghiệm thu)", strippingRatio, 1.0, "-");
            double overlapDeviation = Math.Max(0.0, Math.Max(50.0 - p.BeamVibratorOverlap, p.BeamVibratorOverlap - 100.0));
            res.BeamVibrationProcedure.StressResult = new KetQuaTinhToan("Đầm dùi bê tông", "Độ chồng vùng đầm", LoaiKiemTra.Khac, "1+max(50-a; a-100; 0) ≤ 1", 1.0 + overlapDeviation, 1.0, "-");
            res.BeamLoadPathText = $"Ván phủ phim → {res.BeamBottomPanelStiffenerCount} xà gồ phụ {p.BeamSecondaryJoistWidth:F0}×{p.BeamSecondaryJoistDepth:F0} đáy @≤{secondarySpacing:F0} → xà gồ chính {p.BeamPrimaryWalerWidth:F0}×{p.BeamPrimaryWalerDepth:F0} @ {primarySpacing:F0} → đầu U/Ringlock {scaffoldBay:F0} → bản đế/tấm kê; thành dầm {res.BeamSidePanelStiffenerCount} nẹp @≤{sideSpacing:F0}; {res.BeamClampStationCount} vị trí gông tại [{res.BeamClampLayoutText}] mm";

            string prefix = !string.IsNullOrEmpty(p.SelectedFloorDetailName) ? $"[{p.SelectedFloorDetailName}] " : "";
            res.ReportItems.Add(BuildReportItem(prefix, "Ván đáy: Kiểm tra ứng suất", res.BeamBottomPlywood.StressResult, p.IsBeamSelected));
            res.ReportItems.Add(BuildReportItem(prefix, "Ván đáy: Kiểm tra cắt", res.BeamBottomPlywood.ShearResult, p.IsBeamSelected));
            res.ReportItems.Add(BuildReportItem(prefix, "Ván đáy: Kiểm tra độ võng", res.BeamBottomPlywood.DeflectionResult, p.IsBeamSelected));
            res.ReportItems.Add(BuildReportItem(prefix, "Xà gồ phụ: Kiểm tra uốn", res.BeamSecondaryJoist.StressResult, p.IsBeamSelected));
            res.ReportItems.Add(BuildReportItem(prefix, "Xà gồ phụ: Kiểm tra cắt", res.BeamSecondaryJoist.ShearResult, p.IsBeamSelected));
            res.ReportItems.Add(BuildReportItem(prefix, "Xà gồ phụ: Kiểm tra độ võng", res.BeamSecondaryJoist.DeflectionResult, p.IsBeamSelected));
            res.ReportItems.Add(BuildReportItem(prefix, "Xà gồ chính: Kiểm tra uốn", res.BeamPrimaryWaler.StressResult, p.IsBeamSelected));
            res.ReportItems.Add(BuildReportItem(prefix, "Xà gồ chính: Kiểm tra cắt", res.BeamPrimaryWaler.ShearResult, p.IsBeamSelected));
            res.ReportItems.Add(BuildReportItem(prefix, "Xà gồ chính: Kiểm tra độ võng", res.BeamPrimaryWaler.DeflectionResult, p.IsBeamSelected));
            res.ReportItems.Add(BuildReportItem(prefix, "Ván thành: Kiểm tra ứng suất", res.BeamSidePlywood.StressResult, p.IsBeamSelected));
            res.ReportItems.Add(BuildReportItem(prefix, "Ván thành: Kiểm tra cắt", res.BeamSidePlywood.ShearResult, p.IsBeamSelected));
            res.ReportItems.Add(BuildReportItem(prefix, "Ván thành: Kiểm tra độ võng", res.BeamSidePlywood.DeflectionResult, p.IsBeamSelected));
            res.ReportItems.Add(BuildReportItem(prefix, "Gông thành: Kiểm tra ứng suất", res.BeamSideYoke.StressResult, p.IsBeamSelected));
            res.ReportItems.Add(BuildReportItem(prefix, "Ty giằng D16: Kiểm tra kéo", res.BeamTieRod.StressResult, p.IsBeamSelected));
            if (p.BeamPlacementModeIndex == 2)
            {
                res.ReportItems.Add(BuildReportItem(prefix, "DẦM BIÊN · Ty chết trên mặt: kiểm tra kéo", res.BeamEdgeSurfaceTieRod.StressResult, p.IsBeamSelected));
                res.ReportItems.Add(BuildReportItem(prefix, "DẦM BIÊN · Ty chết trên mặt: kiểm tra chiều dài neo", res.BeamEdgeSurfaceTieRod.DeflectionResult, p.IsBeamSelected));
            }
            res.ReportItems.Add(BuildReportItem(prefix, "Chống xiên thành dầm biên", res.BeamSideBrace.StressResult, p.IsBeamSelected));
            AddVerticalSupportReportItems(res, prefix, p.IsBeamSelected, p.IsGroundLevel);

            bool sideRestraintOk = p.BeamPlacementModeIndex == 1 ? res.BeamTieRod.IsOk :
                                   p.BeamPlacementModeIndex == 2 ? res.BeamSideBrace.IsOk && res.BeamEdgeSurfaceTieRod.IsOk :
                                   res.BeamTieRod.IsOk && res.BeamSideBrace.IsOk;
            res.IsSystemOk = res.BeamBottomPlywood.IsOk && res.BeamSecondaryJoist.IsOk &&
                             res.BeamPrimaryWaler.IsOk && res.BeamSidePlywood.IsOk && res.BeamSideYoke.IsOk &&
                             sideRestraintOk && res.BeamStripping.IsOk && res.BeamVibrationProcedure.IsOk &&
                             IsVerticalSupportSystemOk(res);
            return res;
        }

        public static int CalculatePanelStiffenerCount(double panelWidthMm,
            double maximumSpacingMm = MaximumPanelStiffenerSpacingMm)
        {
            double width = IsFinitePositive(panelWidthMm) ? panelWidthMm : 1.0;
            double spacing = IsFinitePositive(maximumSpacingMm)
                ? Math.Min(MaximumPanelStiffenerSpacingMm, Math.Max(50.0, maximumSpacingMm))
                : MaximumPanelStiffenerSpacingMm;
            return Math.Max(2, (int)Math.Ceiling(width / spacing) + 1);
        }

        public static List<double> BuildPanelStiffenerOffsets(double panelWidthMm,
            double maximumSpacingMm = MaximumPanelStiffenerSpacingMm)
        {
            double width = IsFinitePositive(panelWidthMm) ? panelWidthMm : 1.0;
            int count = CalculatePanelStiffenerCount(width, maximumSpacingMm);
            var offsets = new List<double>(count);
            for (int index = 0; index < count; index++)
                offsets.Add(-width / 2.0 + width * index / (count - 1));
            return offsets;
        }

        public static List<double> BuildBeamClampStations(double beamLengthMm, double maximumSpacingMm)
        {
            double length = IsFinitePositive(beamLengthMm) ? beamLengthMm : 1.0;
            double spacing = IsFinitePositive(maximumSpacingMm) ? Math.Max(100.0, maximumSpacingMm) : 1200.0;

            // L1/L2 are 500 mm in the updated master-family workflow. Short beams
            // retain two distinct stations at their quarter points.
            double endOffset = Math.Min(TutorialBeamClampEndOffsetMm, length / 4.0);
            double first = endOffset;
            double last = length - endOffset;
            double usableLength = Math.Max(0.0, last - first);
            int bayCount = Math.Max(1, (int)Math.Ceiling(usableLength / spacing));
            var stations = new List<double>(bayCount + 1);
            for (int bay = 0; bay <= bayCount; bay++)
                stations.Add(first + usableLength * bay / bayCount);
            return stations;
        }

        private static bool IsFinitePositive(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value > 0.0;

        private static double MaximumBay(IReadOnlyList<double> positions)
        {
            if (positions == null || positions.Count < 2) return 1.0;
            double maximum = 0.0;
            for (int index = 1; index < positions.Count; index++)
                maximum = Math.Max(maximum, positions[index] - positions[index - 1]);
            return Math.Max(1.0, maximum);
        }

        public static List<double> BuildColumnYokeElevations(CalculationParameters p)
        {
            double height = Math.Max(200.0, p.ColumnHeight);
            var elevations = new List<double>();

            if (!p.ColumnUseZonedYokeSpacing)
            {
                double spacing = ClampColumnYokeSpacing(p.ColumnYokeSpacing);
                double first = Math.Min(height / 2.0, spacing / 2.0);
                for (double z = first; z < height; z += spacing) elevations.Add(z);
                double last = height - first;
                if (last > first && (elevations.Count == 0 || last - elevations[elevations.Count - 1] > 80.0)) elevations.Add(last);
                return elevations;
            }

            double firstOffset = Math.Min(height / 2.0, Math.Max(50.0, p.ColumnFirstYokeOffset));
            double lastOffset = Math.Min(height / 2.0, Math.Max(50.0, p.ColumnLastYokeOffset));
            double lastElevation = height - lastOffset;
            double transition = Math.Max(firstOffset, Math.Min(height, p.ColumnYokeTransitionHeight));
            double zCurrent = firstOffset;
            elevations.Add(zCurrent);
            while (true)
            {
                double spacing = zCurrent < transition
                    ? ClampColumnYokeSpacing(p.ColumnYokeBottomSpacing)
                    : ClampColumnUpperYokeSpacing(p.ColumnYokeUpperSpacing);
                double next = zCurrent + spacing;
                if (next >= lastElevation - 80.0) break;
                elevations.Add(next);
                zCurrent = next;
            }
            if (lastElevation - elevations[elevations.Count - 1] > 1.0) elevations.Add(lastElevation);

            // Preserve the requested top offset without leaving two almost coincident
            // yokes. Redistribute only the final bay when it is shorter than half of
            // the applicable nominal spacing.
            if (elevations.Count >= 3)
            {
                double penultimate = elevations[elevations.Count - 2];
                double finalSpacing = penultimate < transition
                    ? ClampColumnYokeSpacing(p.ColumnYokeBottomSpacing)
                    : ClampColumnUpperYokeSpacing(p.ColumnYokeUpperSpacing);
                if (lastElevation - penultimate < finalSpacing / 2.0)
                {
                    double redistributionStart = elevations[elevations.Count - 3];
                    elevations.RemoveRange(elevations.Count - 2, 2);
                    int bayCount = Math.Max(2, (int)Math.Ceiling((lastElevation - redistributionStart) / finalSpacing));
                    double redistributedSpacing = (lastElevation - redistributionStart) / bayCount;
                    for (int bay = 1; bay <= bayCount; bay++)
                        elevations.Add(redistributionStart + redistributedSpacing * bay);
                }
            }
            return elevations;
        }

        public static double ClampColumnYokeSpacing(double spacing)
        {
            if (double.IsNaN(spacing) || double.IsInfinity(spacing)) return 500.0;
            return Math.Max(MinimumColumnYokeSpacingMm,
                Math.Min(MaximumColumnYokeSpacingMm, spacing));
        }

        public static double ClampColumnUpperYokeSpacing(double spacing)
        {
            if (double.IsNaN(spacing) || double.IsInfinity(spacing)) return 500.0;
            return Math.Max(MinimumColumnUpperYokeSpacingMm,
                Math.Min(MaximumColumnYokeSpacingMm, spacing));
        }

        public static void NormalizeColumnYokeLayout(CalculationParameters p)
        {
            if (p == null) return;
            p.ColumnYokeSpacing = ClampColumnYokeSpacing(p.ColumnYokeSpacing);
            p.ColumnYokeBottomSpacing = ClampColumnYokeSpacing(p.ColumnYokeBottomSpacing);
            p.ColumnYokeUpperSpacing = ClampColumnUpperYokeSpacing(p.ColumnYokeUpperSpacing);
        }

        private static double ColumnPressureCap(CalculationParameters p)
        {
            return 7.2 + 785.0 * Math.Max(0.0, p.ConcretePourRate) / Math.Max(1.0, p.ConcreteTemperature + 17.8);
        }

        private static double ColumnServicePressureAt(CalculationParameters p, double elevationMm)
        {
            double depthM = Math.Max(0.0, p.ColumnHeight - elevationMm) / 1000.0;
            return Math.Min(p.ConcreteWeight * depthM, ColumnPressureCap(p)) + p.VibrationLoad;
        }

        private static double ColumnDesignPressureAt(CalculationParameters p, double elevationMm)
        {
            double depthM = Math.Max(0.0, p.ColumnHeight - elevationMm) / 1000.0;
            return Math.Min(p.ConcreteWeight * depthM, ColumnPressureCap(p)) * p.DeadLoadSafetyFactor +
                   p.VibrationLoad * p.LiveLoadSafetyFactor;
        }

        public static CalculationResult CheckColumnSafety(CalculationParameters p)
        {
            NormalizeColumnYokeLayout(p);
            var res = new CalculationResult();
            double height = Math.Max(200.0, p.ColumnHeight);
            double plywoodThickness = Math.Max(1.0, p.ColumnPlywoodThickness);
            double stiffenerWidth = Math.Max(1.0, p.ColumnStiffenerWidth);
            double stiffenerDepth = Math.Max(1.0, p.ColumnStiffenerDepth);
            List<double> widthFaceStiffeners = BuildPanelStiffenerOffsets(
                p.ColumnWidth, p.ColumnStiffenerSpacing);
            List<double> depthFaceStiffeners = BuildPanelStiffenerOffsets(
                p.ColumnDepth, p.ColumnStiffenerSpacing);
            double stiffenerSpacing = Math.Max(MaximumBay(widthFaceStiffeners), MaximumBay(depthFaceStiffeners));
            res.ColumnWidthFaceStiffenerCount = widthFaceStiffeners.Count;
            res.ColumnDepthFaceStiffenerCount = depthFaceStiffeners.Count;
            double yokeWidth = Math.Max(10.0, p.ColumnYokeWidth);
            double yokeHeight = Math.Max(10.0, p.ColumnYokeHeight);
            double yokeThickness = Math.Min(Math.Max(1.0, p.ColumnYokeThickness), Math.Min(yokeWidth, yokeHeight) / 2.0 - 0.1);
            int yokePairCount = Math.Max(1, p.ColumnYokePairCount);

            var columnPlywood = new CauKienModel("Ván mặt cột", LoaiTietDien.VanMat, "Gỗ dán phủ phim", 1000, plywoodThickness, plywoodThickness);
            var columnStiffener = new CauKienModel("Xà gồ đứng", LoaiTietDien.XaGo, "Gỗ", stiffenerWidth, stiffenerDepth);

            bool steelYoke = p.ColumnYokeMaterialIndex == 1;
            double yokeInnerWidth = Math.Max(0.1, yokeWidth - 2.0 * yokeThickness);
            double yokeInnerHeight = Math.Max(0.1, yokeHeight - 2.0 * yokeThickness);
            double yokeArea = steelYoke
                ? yokeWidth * yokeHeight - yokeInnerWidth * yokeInnerHeight
                : yokeWidth * yokeHeight;
            double yokeI = steelYoke
                ? (yokeWidth * Math.Pow(yokeHeight, 3) - yokeInnerWidth * Math.Pow(yokeInnerHeight, 3)) / 12.0
                : yokeWidth * Math.Pow(yokeHeight, 3) / 12.0;
            double yokeW = yokeI / (yokeHeight / 2.0);
            double yokeElasticModulus = steelYoke ? 200000.0 : p.ElasticModulus;
            double yokeBendingLimit = steelYoke ? 210.0 : p.WoodStrength * p.MoistureFactor * p.TemperatureFactor;
            double yokeShearLimit = steelYoke ? 120.0 : p.WoodShearStrength * p.MoistureFactor * p.TemperatureFactor;
            string yokeName = steelYoke ? "Gông thép hộp đôi" : "Xà gồ 50x100 đôi";

            res.ColumnHydrostaticPressure = p.ConcreteWeight * height / 1000.0;
            res.ColumnPressureCap = ColumnPressureCap(p);
            res.ColumnDesignPressure = ColumnDesignPressureAt(p, 0.0);
            double serviceBottomPressure = ColumnServicePressureAt(p, 0.0);

            // Ván mặt vượt nhịp ngang giữa các xà gồ đứng, chịu áp lực lớn nhất tại chân cột.
            double mDivPlywood = GetMomentDivisor(p.SoNhipVanMat);
            double plywoodMoment = res.ColumnDesignPressure * Math.Pow(stiffenerSpacing, 2) / mDivPlywood;
            double plyLimit = p.PlywoodBendingStrength * p.MoistureFactor * p.TemperatureFactor;
            res.ColumnPlywood.StressResult = new KetQuaTinhToan("Ván mặt cột", "Ứng suất uốn", LoaiKiemTra.CuongDoUon,
                "p_d(z=0)=min(γH;p_cap)γG+q_vγQ; σ=p_d·a²/(nW) ≤ Ru", plywoodMoment / columnPlywood.MomenKhangUon_W, plyLimit, "MPa");
            double plywoodDef = 5.0 * serviceBottomPressure * Math.Pow(stiffenerSpacing, 4) /
                                 (384.0 * p.ElasticModulus * columnPlywood.MomenQuanTinh_I);
            double plyDefLimit = p.SlabDeflectionLimitRatio > 0 ? stiffenerSpacing * p.SlabDeflectionLimitRatio : 2.0;
            res.ColumnPlywood.DeflectionResult = new KetQuaTinhToan("Ván mặt cột", "Độ võng", LoaiKiemTra.DoVong,
                "f=5p_s·a⁴/(384EI) ≤ [f]", plywoodDef, plyDefLimit, "mm");

            List<double> yokes = BuildColumnYokeElevations(p);
            res.ColumnYokeLevelCount = yokes.Count;
            res.ColumnYokeLayoutText = string.Join(" - ", yokes.ConvertAll(z => z.ToString("F0")));

            // Xà gồ đứng: kiểm tra từng nhịp theo áp lực thực tại cao độ nhịp.
            double criticalStiffenerMoment = 0, criticalStiffenerDeflection = 0, criticalStiffenerShear = 0;
            double criticalStiffenerSpan = 0, criticalStiffenerPressure = 0, criticalStiffenerDeflectionLimit = 1;
            void CheckStiffenerSpan(double lower, double upper, bool cantilever)
            {
                double span = Math.Max(1.0, upper - lower);
                double pDesign = ColumnDesignPressureAt(p, lower);
                double pService = ColumnServicePressureAt(p, lower);
                double qDesign = pDesign * stiffenerSpacing / 1000.0;
                double qService = pService * stiffenerSpacing / 1000.0;
                double moment = qDesign * span * span / (cantilever ? 2.0 : 10.0);
                double deflection = qService * Math.Pow(span, 4) /
                                    ((cantilever ? 8.0 : 128.0) * p.ElasticModulus * columnStiffener.MomenQuanTinh_I);
                double shear = (cantilever ? 1.0 : 0.6) * qDesign * span;
                if (moment > criticalStiffenerMoment)
                {
                    criticalStiffenerMoment = moment;
                    criticalStiffenerSpan = span;
                    criticalStiffenerPressure = pDesign;
                }
                if (deflection > criticalStiffenerDeflection)
                {
                    criticalStiffenerDeflection = deflection;
                    criticalStiffenerDeflectionLimit = p.JoistDeflectionLimitRatio > 0 ? span * p.JoistDeflectionLimitRatio : 2.0;
                }
                criticalStiffenerShear = Math.Max(criticalStiffenerShear, shear);
            }
            if (yokes.Count > 0)
            {
                CheckStiffenerSpan(0, yokes[0], true);
                for (int i = 0; i < yokes.Count - 1; i++) CheckStiffenerSpan(yokes[i], yokes[i + 1], false);
                CheckStiffenerSpan(yokes[yokes.Count - 1], height, true);
            }
            else CheckStiffenerSpan(0, height, false);

            res.ColumnCriticalStiffenerSpan = criticalStiffenerSpan;
            res.ColumnCriticalStiffenerPressure = criticalStiffenerPressure;
            double stiffenerLimit = p.WoodStrength * p.MoistureFactor * p.TemperatureFactor;
            double stiffenerShearLimit = p.WoodShearStrength * p.MoistureFactor * p.TemperatureFactor;
            res.ColumnStiffener.StressResult = new KetQuaTinhToan("Xà gồ đứng", "Ứng suất uốn", LoaiKiemTra.CuongDoUon,
                "max[σ_i=p_d(z_i)·a·L_i²/(kW)] ≤ Ru", criticalStiffenerMoment / columnStiffener.MomenKhangUon_W, stiffenerLimit, "MPa");
            res.ColumnStiffener.DeflectionResult = new KetQuaTinhToan("Xà gồ đứng", "Độ võng", LoaiKiemTra.DoVong,
                "max[f_i=p_s(z_i)·a·L_i⁴/(kEI)] ≤ [f_i]", criticalStiffenerDeflection, criticalStiffenerDeflectionLimit, "mm");
            res.ColumnStiffener.ShearResult = new KetQuaTinhToan("Xà gồ đứng", "Ứng suất cắt", LoaiKiemTra.CuongDoCat,
                "τ=1,5V/(b·h) ≤ [τ]", 1.5 * criticalStiffenerShear / (stiffenerWidth * stiffenerDepth), stiffenerShearLimit, "MPa");

            // Gông/xà gồ ngang và ty giằng: tải phân bố theo dải ảnh hưởng của từng cao độ gông.
            double yokeSpan = Math.Max(p.ColumnWidth, p.ColumnDepth);
            double maxYokeMoment = 0, maxYokeDeflection = 0, maxYokeShear = 0, maxTieForce = 0;
            double criticalYokePressure = 0, criticalTributaryHeight = 0, yokeDeflectionLimit = Math.Max(1.0, yokeSpan / 400.0);
            int tieCount = Math.Max(1, p.ColumnTieRodCountPerFace);
            double tieTributaryWidth = yokeSpan / tieCount / 1000.0;
            for (int i = 0; i < yokes.Count; i++)
            {
                double below = i == 0 ? yokes[i] : (yokes[i] - yokes[i - 1]) / 2.0;
                double above = i == yokes.Count - 1 ? height - yokes[i] : (yokes[i + 1] - yokes[i]) / 2.0;
                double tributaryHeight = below + above;
                double pDesign = ColumnDesignPressureAt(p, yokes[i]);
                double pService = ColumnServicePressureAt(p, yokes[i]);
                double qDesign = pDesign * tributaryHeight / 1000.0 / yokePairCount;
                double qService = pService * tributaryHeight / 1000.0 / yokePairCount;
                double moment = qDesign * yokeSpan * yokeSpan / 8.0;
                if (moment > maxYokeMoment)
                {
                    maxYokeMoment = moment;
                    criticalYokePressure = pDesign;
                    criticalTributaryHeight = tributaryHeight;
                }
                maxYokeShear = Math.Max(maxYokeShear, 0.5 * qDesign * yokeSpan);
                maxYokeDeflection = Math.Max(maxYokeDeflection, 5.0 * qService * Math.Pow(yokeSpan, 4) /
                    (384.0 * yokeElasticModulus * yokeI));
                maxTieForce = Math.Max(maxTieForce, pDesign * tributaryHeight / 1000.0 * tieTributaryWidth);
            }
            res.ColumnCriticalYokePressure = criticalYokePressure;
            res.ColumnCriticalYokeTributaryHeight = criticalTributaryHeight;
            res.ColumnYoke.StressResult = new KetQuaTinhToan(yokeName, "Ứng suất uốn", LoaiKiemTra.CuongDoUon,
                "max[σ_i=p_d(z_i)·b_i·L²/(8nW)] ≤ [σ]", maxYokeMoment / yokeW, yokeBendingLimit, "MPa");
            res.ColumnYoke.ShearResult = new KetQuaTinhToan(yokeName, "Ứng suất cắt", LoaiKiemTra.CuongDoCat,
                "τ=1,5V/A ≤ [τ]", 1.5 * maxYokeShear / yokeArea, yokeShearLimit, "MPa");
            res.ColumnYoke.DeflectionResult = new KetQuaTinhToan(yokeName, "Độ võng", LoaiKiemTra.DoVong,
                "max[f_i=5q_iL⁴/(384EI)] ≤ [f]", maxYokeDeflection, yokeDeflectionLimit, "mm");

            double tieCapacity = Math.Max(0.1, p.ColumnTieRodAllowableCapacity);
            res.ColumnTieRod.StressResult = new KetQuaTinhToan($"Ty giằng D{p.ColumnTieRodDiameter:F0} + bát chuồn", "Lực kéo", LoaiKiemTra.ChiuLucDoc,
                "T_max=max[p_d(z_i)·b_i·b_phân_tải] ≤ [T]", maxTieForce, tieCapacity, "kN");

            // Chống xiên và cáp neo kiểm soát ổn định tổng thể/độ thẳng đứng.
            double averagePressure = 0.5 * (res.ColumnDesignPressure + ColumnDesignPressureAt(p, height));
            double faceThrust = averagePressure * (Math.Max(p.ColumnWidth, p.ColumnDepth) / 1000.0) * (height / 1000.0);
            double horizontalStabilityLoad = Math.Max(2.5, 0.02 * faceThrust);
            // Distribute the selected restraint assemblies over the directions that
            // geometry has proved usable. Increasing ColumnBraceCount therefore has
            // a real calculation effect: edge/corner columns can receive doubled or
            // tripled inward props instead of being locked to one prop per direction.
            int supportedBraceDirections = Math.Max(0, Math.Min(4, p.ColumnSupportedBraceDirectionCount));
            int totalRestraints = Math.Max(1, p.ColumnBraceCount);
            int activeBraces = p.ColumnIsBoundaryOrCorner
                ? Math.Max(1, totalRestraints / Math.Max(1, supportedBraceDirections))
                : Math.Max(1, (int)Math.Ceiling(totalRestraints / 2.0));
            double angleRad = Math.Max(10.0, Math.Min(80.0, p.ColumnBraceAngle)) * Math.PI / 180.0;
            double braceForce = horizontalStabilityLoad / (activeBraces * Math.Cos(angleRad));
            double braceCapacity = Math.Max(0.1, p.ColumnBraceAllowableCapacity);
            string braceName = p.ColumnBracePlacementModeIndex > 0
                ? "Chống xiên/cáp neo vào trong ranh"
                : p.ColumnIsBoundaryOrCorner ? "Chống xiên + cáp neo" : "Chống xiên điều chỉnh";
            res.ColumnBrace.StressResult = new KetQuaTinhToan(braceName, "Lực dọc ổn định", LoaiKiemTra.ChiuLucDoc,
                "N=H_d/(n·cosα) ≤ [N]", braceForce, braceCapacity, "kN");
            double braceVerticalReaction = braceForce * Math.Sin(angleRad);
            double braceBaseArea = Math.Max(100.0, p.SupportBasePlateWidth * p.SupportBasePlateDepth);
            res.ColumnBraceBaseBearing.StressResult = new KetQuaTinhToan("Đế chống xiên", "Ép mặt cục bộ", LoaiKiemTra.Khac,
                "σ_b=(N·sinα)/(b_p·l_p) ≤ [σ_b]", braceVerticalReaction * 1000.0 / braceBaseArea,
                Math.Max(0.01, p.SupportBaseBearingAllowable), "MPa");
            res.ColumnCable.StressResult = new KetQuaTinhToan("Cáp giằng", "Lực kéo", LoaiKiemTra.ChiuLucDoc,
                "T_cáp=N_neo ≤ [T_cáp]", braceForce, Math.Max(0.1, p.ColumnCableAllowableCapacity), "kN");
            res.ColumnTurnbuckle.StressResult = new KetQuaTinhToan("Tăng đơ", "Lực kéo", LoaiKiemTra.ChiuLucDoc,
                "T_tăng_đơ=N_neo ≤ [T_tăng_đơ]", braceForce, Math.Max(0.1, p.ColumnTurnbuckleAllowableCapacity), "kN");
            res.ColumnAnchor.StressResult = new KetQuaTinhToan("Neo chờ/bulldog anchor", "Lực nhổ", LoaiKiemTra.ChiuLucDoc,
                "N_neo ≤ [N_neo]", braceForce, Math.Max(0.1, p.ColumnAnchorAllowableCapacity), "kN");

            double plumbLimit = Math.Min(10.0, Math.Max(1.0, height / 1000.0));
            res.ColumnPlumb.StressResult = new KetQuaTinhToan("Kiểm tra độ thẳng đứng", "Sai lệch mục tiêu", LoaiKiemTra.Khac,
                "Δ_mục_tiêu ≤ min(H/1000;10 mm)", Math.Max(0.0, p.ColumnPlumbTargetTolerance), plumbLimit, "mm");
            res.ColumnPouring.StressResult = new KetQuaTinhToan("Đợt đổ bê tông", "Chiều cao mỗi đợt", LoaiKiemTra.Khac,
                "h_đợt ≤ 2500 mm", Math.Max(0.0, p.ColumnPourLiftHeight), 2500.0, "mm");
            res.ColumnPouring.ShearResult = new KetQuaTinhToan("Ống mềm đổ bê tông", "Khoảng cách tới chân cột", LoaiKiemTra.Khac,
                "h_rơi ≤ 1500 mm", Math.Max(0.0, p.ColumnHoseBottomClearance), 1500.0, "mm");

            string prefix = !string.IsNullOrEmpty(p.SelectedFloorDetailName) ? $"[{p.SelectedFloorDetailName}] " : "";
            res.ReportItems.Add(BuildReportItem(prefix, "Ván khuôn cột: Kiểm tra ứng suất", res.ColumnPlywood.StressResult, p.IsColumnSelected));
            res.ReportItems.Add(BuildReportItem(prefix, "Ván khuôn cột: Kiểm tra độ võng", res.ColumnPlywood.DeflectionResult, p.IsColumnSelected));
            res.ReportItems.Add(BuildReportItem(prefix, "Xà gồ đứng: Kiểm tra ứng suất theo từng nhịp", res.ColumnStiffener.StressResult, p.IsColumnSelected));
            res.ReportItems.Add(BuildReportItem(prefix, "Xà gồ đứng: Kiểm tra cắt", res.ColumnStiffener.ShearResult, p.IsColumnSelected));
            res.ReportItems.Add(BuildReportItem(prefix, "Xà gồ đứng: Kiểm tra độ võng theo từng nhịp", res.ColumnStiffener.DeflectionResult, p.IsColumnSelected));
            res.ReportItems.Add(BuildReportItem(prefix, yokeName + ": Kiểm tra ứng suất", res.ColumnYoke.StressResult, p.IsColumnSelected));
            res.ReportItems.Add(BuildReportItem(prefix, yokeName + ": Kiểm tra cắt", res.ColumnYoke.ShearResult, p.IsColumnSelected));
            res.ReportItems.Add(BuildReportItem(prefix, yokeName + ": Kiểm tra độ võng", res.ColumnYoke.DeflectionResult, p.IsColumnSelected));
            res.ReportItems.Add(BuildReportItem(prefix, "Ty giằng và bát chuồn: Kiểm tra lực kéo", res.ColumnTieRod.StressResult, p.IsColumnSelected));
            res.ReportItems.Add(BuildReportItem(prefix, "Chống xiên/cáp neo: Kiểm tra ổn định", res.ColumnBrace.StressResult, p.IsColumnSelected));
            res.ReportItems.Add(BuildReportItem(prefix, "Đế chống xiên: Kiểm tra ép mặt", res.ColumnBraceBaseBearing.StressResult, p.IsColumnSelected));
            res.ReportItems.Add(BuildReportItem(prefix, "Cáp giằng: Kiểm tra lực kéo", res.ColumnCable.StressResult, p.IsColumnSelected));
            res.ReportItems.Add(BuildReportItem(prefix, "Tăng đơ: Kiểm tra lực kéo", res.ColumnTurnbuckle.StressResult, p.IsColumnSelected));
            res.ReportItems.Add(BuildReportItem(prefix, "Neo chờ/bulldog anchor: Kiểm tra lực nhổ", res.ColumnAnchor.StressResult, p.IsColumnSelected));
            res.ReportItems.Add(BuildReportItem(prefix, "Nghiệm thu: Kiểm tra độ thẳng đứng", res.ColumnPlumb.StressResult, p.IsColumnSelected));
            res.ReportItems.Add(BuildReportItem(prefix, "Thi công: Chiều cao mỗi đợt đổ", res.ColumnPouring.StressResult, p.IsColumnSelected));
            res.ReportItems.Add(BuildReportItem(prefix, "Thi công: Chiều cao rơi qua ống mềm", res.ColumnPouring.ShearResult, p.IsColumnSelected));

            res.IsSystemOk = res.ColumnPlywood.IsOk && res.ColumnStiffener.IsOk && res.ColumnYoke.IsOk &&
                             res.ColumnTieRod.IsOk && res.ColumnBrace.IsOk && res.ColumnBraceBaseBearing.IsOk && res.ColumnCable.IsOk &&
                             res.ColumnTurnbuckle.IsOk && res.ColumnAnchor.IsOk && res.ColumnPlumb.IsOk && res.ColumnPouring.IsOk;
            return res;
        }

        public static KetQuaHienThi BuildReportItem(string prefix, string title, KetQuaTinhToan kq, bool isFloorSelected)
        {
            if (kq == null)
            {
                // Some component-specific calculation paths intentionally do not create
                // non-applicable checks. Report generation must never dereference such an
                // optional result and interrupt the Revit UI/debug session.
                System.Diagnostics.Debug.WriteLine($"Skipped non-applicable calculation report item: {prefix}{title}");
                return new KetQuaHienThi
                {
                    NoiDungKiemTra = prefix + title,
                    GiaTriThucTe = "-",
                    GiaTriChoPhep = "-",
                    KetLuan = "Không áp dụng",
                    MauNenTrangThai = "#E5E7EB",
                    MauChuTrangThai = "#475569"
                };
            }

            if (!isFloorSelected)
            {
                return new KetQuaHienThi
                {
                    NoiDungKiemTra = prefix + title,
                    GiaTriThucTe = $"{kq.GiaTriThucTe:F2} {kq.DonVi}",
                    GiaTriChoPhep = $"{kq.GiaTriChoPhep:F2} {kq.DonVi}",
                    KetLuan = "Null",
                    MauNenTrangThai = "#FEF08A",
                    MauChuTrangThai = "#854D0E"
                };
            }

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

        private static bool ReduceParameter(Func<double> getter, Action<double> setter, double minimum, double step,
            string name, List<string> adjustments)
        {
            double current = getter();
            if (current <= minimum + 0.01) return false;
            double next = Math.Max(minimum, Math.Floor((current - step) / step) * step);
            if (next >= current - 0.01) next = Math.Max(minimum, current - step);
            setter(next);
            adjustments.Add($"{name}: {current:F0} → {next:F0} mm");
            return true;
        }

        private static bool IncreaseParameter(Func<double> getter, Action<double> setter, double maximum, double step,
            string name, List<string> adjustments)
        {
            double current = getter();
            if (current >= maximum - 0.01) return false;
            double next = Math.Min(maximum, Math.Ceiling((current + step) / step) * step);
            setter(next);
            adjustments.Add($"{name}: {current:F0} → {next:F0} mm");
            return true;
        }

        private static bool UpgradeShore(CalculationParameters p, List<string> adjustments)
        {
            (double d, double t, double rated)[] catalogue =
            {
                (48, 2, 15), (48, 3.2, 25), (60, 3.2, 40),
                (76, 3.2, 60), (89, 4, 90), (114, 5, 140)
            };
            for (int i = 0; i < catalogue.Length; i++)
            {
                if (catalogue[i].d > p.CayChongDiameter + 0.01 ||
                    catalogue[i].d >= p.CayChongDiameter - 0.01 && catalogue[i].t > p.CayChongThickness + 0.01)
                {
                    double oldD = p.CayChongDiameter;
                    double oldT = p.CayChongThickness;
                    p.CayChongDiameter = catalogue[i].d;
                    p.CayChongThickness = catalogue[i].t;
                    p.SupportRatedCapacity = catalogue[i].rated;
                    adjustments.Add($"Cây chống: D{oldD:F0}×{oldT:F1} → D{p.CayChongDiameter:F0}×{p.CayChongThickness:F1} mm ([P]={p.SupportRatedCapacity:F0} kN)");
                    return true;
                }
            }
            return false;
        }

        private static bool UpgradeBeamSideBrace(CalculationParameters p, List<string> adjustments)
        {
            // Rated push-pull/brace profiles used as a discrete accessory catalogue.
            // Capacity changes only together with the selected physical cross-section.
            var catalogue = new[]
            {
                (d: 48.0, t: 2.0, rated: 15.0),
                (d: 48.0, t: 3.2, rated: 25.0),
                (d: 60.0, t: 3.2, rated: 40.0),
                (d: 76.0, t: 3.2, rated: 60.0),
                (d: 89.0, t: 4.0, rated: 90.0)
            };
            double currentArea = Math.PI / 4.0 *
                (p.BeamSideBraceDiameter * p.BeamSideBraceDiameter -
                 Math.Pow(Math.Max(0.0, p.BeamSideBraceDiameter - 2.0 * p.BeamSideBraceThickness), 2));
            for (int i = 0; i < catalogue.Length; i++)
            {
                double inner = Math.Max(0.0, catalogue[i].d - 2.0 * catalogue[i].t);
                double area = Math.PI / 4.0 * (catalogue[i].d * catalogue[i].d - inner * inner);
                if (area > currentArea + 0.1 || catalogue[i].rated > p.BeamSideBraceAllowableCapacity + 0.1)
                {
                    double oldD = p.BeamSideBraceDiameter;
                    double oldT = p.BeamSideBraceThickness;
                    p.BeamSideBraceDiameter = catalogue[i].d;
                    p.BeamSideBraceThickness = catalogue[i].t;
                    p.BeamSideBraceAllowableCapacity = catalogue[i].rated;
                    adjustments.Add($"Tiết diện chống xiên dầm: D{oldD:F0}×{oldT:F1} → D{catalogue[i].d:F0}×{catalogue[i].t:F1} mm ([N]={catalogue[i].rated:F0} kN/thanh)");
                    return true;
                }
            }
            if (p.BeamSideBraceCountPerStation < 4)
            {
                int old = p.BeamSideBraceCountPerStation;
                p.BeamSideBraceCountPerStation++;
                adjustments.Add($"Chống xiên song song tại mỗi vị trí: {old} → {p.BeamSideBraceCountPerStation} thanh");
                return true;
            }
            return ReduceParameter(() => p.BeamSideBraceSpacing, v => p.BeamSideBraceSpacing = v,
                100, 50, "Bước chống xiên thành", adjustments);
        }

        private static bool UpgradeBeamTieRod(CalculationParameters p, List<string> adjustments)
        {
            var catalogue = new[]
            {
                (diameter: 12.0, rated: 12.0),
                (diameter: 16.0, rated: 20.0),
                (diameter: 20.0, rated: 32.0),
                (diameter: 22.0, rated: 40.0),
                (diameter: 25.0, rated: 55.0)
            };
            foreach (var item in catalogue)
            {
                if (item.diameter > p.BeamTieRodDiameter + 0.1 || item.rated > p.BeamTieRodAllowableCapacity + 0.1)
                {
                    double old = p.BeamTieRodDiameter;
                    p.BeamTieRodDiameter = item.diameter;
                    p.BeamTieRodAllowableCapacity = item.rated;
                    adjustments.Add($"Tiết diện ty thành dầm: D{old:F0} → D{item.diameter:F0} mm ([T]={item.rated:F0} kN)");
                    return true;
                }
            }
            return ReduceParameter(() => p.BeamTieRodSpacing, v => p.BeamTieRodSpacing = v,
                100, 50, "Bước ty giằng thành", adjustments);
        }

        private static bool UpgradeColumnRestraintAccessories(
            CalculationParameters p, CalculationResult result, List<string> adjustments)
        {
            bool changed = false;
            if (!result.ColumnTieRod.IsOk)
            {
                var tieCatalogue = new[]
                {
                    (d: 12.0, rated: 12.0), (d: 16.0, rated: 20.0),
                    (d: 20.0, rated: 32.0), (d: 25.0, rated: 55.0)
                };
                foreach (var item in tieCatalogue)
                {
                    if (item.d > p.ColumnTieRodDiameter + 0.1 || item.rated > p.ColumnTieRodAllowableCapacity + 0.1)
                    {
                        double old = p.ColumnTieRodDiameter;
                        p.ColumnTieRodDiameter = item.d;
                        p.ColumnTieRodAllowableCapacity = item.rated;
                        adjustments.Add($"Tiết diện ty cột: D{old:F0} → D{item.d:F0} mm ([T]={item.rated:F0} kN)");
                        changed = true;
                        break;
                    }
                }
                if (!changed && p.ColumnTieRodCountPerFace < 12)
                {
                    int old = p.ColumnTieRodCountPerFace;
                    p.ColumnTieRodCountPerFace++;
                    adjustments.Add($"Số ty mỗi mặt cột: {old} → {p.ColumnTieRodCountPerFace}");
                    changed = true;
                }
            }

            double[] restraintCatalogue = { 15, 25, 40, 60, 90, 140 };
            bool UpgradeCapacity(Func<double> getter, Action<double> setter, string name)
            {
                double current = getter();
                double next = restraintCatalogue.FirstOrDefault(value => value > current + 0.1);
                if (next <= 0) return false;
                setter(next);
                adjustments.Add($"{name}: cấp {current:F0} → {next:F0} kN");
                return true;
            }
            bool restraintSystemFails = !result.ColumnBrace.IsOk ||
                                        !result.ColumnCable.IsOk ||
                                        !result.ColumnTurnbuckle.IsOk ||
                                        !result.ColumnAnchor.IsOk;
            if (restraintSystemFails)
            {
                int supportedDirections = Math.Max(1, Math.Min(4, p.ColumnSupportedBraceDirectionCount));
                int maximumAssemblies = Math.Max(16, supportedDirections * 8);
                if (p.ColumnBraceCount < maximumAssemblies)
                {
                    int old = p.ColumnBraceCount;
                    p.ColumnBraceCount++;
                    int assembliesPerDirection = Math.Max(1, p.ColumnBraceCount / supportedDirections);
                    adjustments.Add($"Số cụm chống/neo cột: {old} → {p.ColumnBraceCount} " +
                                    $"({assembliesPerDirection} cụm/hướng có điểm đỡ)");
                    changed = true;
                }
                else if (!result.ColumnBrace.IsOk)
                    changed |= UpgradeCapacity(() => p.ColumnBraceAllowableCapacity,
                        value => p.ColumnBraceAllowableCapacity = value, "Chống đẩy-kéo cột (nâng tiết diện)");
            }
            if (!result.ColumnCable.IsOk)
                changed |= UpgradeCapacity(() => p.ColumnCableAllowableCapacity,
                    value => p.ColumnCableAllowableCapacity = value, "Cáp giằng (nâng đường kính)");
            if (!result.ColumnTurnbuckle.IsOk)
                changed |= UpgradeCapacity(() => p.ColumnTurnbuckleAllowableCapacity,
                    value => p.ColumnTurnbuckleAllowableCapacity = value, "Tăng đơ (nâng cỡ ren)");
            if (!result.ColumnAnchor.IsOk)
                changed |= UpgradeCapacity(() => p.ColumnAnchorAllowableCapacity,
                    value => p.ColumnAnchorAllowableCapacity = value, "Neo chờ/bulldog (nâng tiết diện)");
            return changed;
        }

        private static bool ImproveVerticalSupport(CalculationParameters p, CalculationResult result,
            Func<double> spacingGetter, Action<double> spacingSetter, List<string> adjustments)
        {
            bool changed = false;
            if (!result.Support.IsOk)
                changed |= ReduceParameter(spacingGetter, spacingSetter, 100, 100, "Bước cây chống", adjustments);
            if (!result.SupportSlenderness.IsOk || !result.SupportEulerBuckling.IsOk)
                changed |= UpgradeShore(p, adjustments);
            if (!result.SupportBaseBearing.IsOk)
            {
                changed |= IncreaseParameter(() => p.SupportBasePlateWidth, v => p.SupportBasePlateWidth = v, 600, 50, "Bề rộng bản đế", adjustments);
                changed |= IncreaseParameter(() => p.SupportBasePlateDepth, v => p.SupportBasePlateDepth = v, 600, 50, "Chiều dài bản đế", adjustments);
            }
            if (!result.SupportHeadBearing.IsOk)
            {
                changed |= IncreaseParameter(() => p.SupportHeadPlateWidth, v => p.SupportHeadPlateWidth = v, 600, 50, "Bề rộng đầu U", adjustments);
                changed |= IncreaseParameter(() => p.SupportHeadPlateDepth, v => p.SupportHeadPlateDepth = v, 600, 50, "Chiều dài đầu U", adjustments);
            }
            if (!result.SupportSoleBearing.IsOk)
            {
                changed |= IncreaseParameter(() => p.SupportSolePlateWidth, v => p.SupportSolePlateWidth = v, 1600, 100, "Bề rộng tấm kê", adjustments);
                changed |= IncreaseParameter(() => p.SupportSolePlateDepth, v => p.SupportSolePlateDepth = v, 1600, 100, "Chiều dài tấm kê", adjustments);
            }
            if (!result.SupportJack.IsOk && p.SupportJackExtension > p.SupportJackExtensionLimit)
            {
                double old = p.SupportJackExtension;
                p.SupportJackExtension = p.SupportJackExtensionLimit;
                adjustments.Add($"Độ vươn kích: {old:F0} → {p.SupportJackExtension:F0} mm");
                changed = true;
            }
            return changed;
        }

        /// <summary>
        /// Thiết kế lặp có giới hạn. Chỉ thay đổi cấu hình hình học/bố trí trong danh mục thực tế;
        /// không giảm tải trọng, không tăng cường độ vật liệu hay sức chịu tải phụ kiện do người dùng khai báo.
        /// </summary>
        public static ComplianceDesignResult AutoDesignToCompliance(CalculationParameters p,
            bool checkSlab, bool checkFoundation, bool checkBeam, bool checkColumn)
        {
            var design = new ComplianceDesignResult { Parameters = p };
            const int maxIterations = 50;

            // Structural geometry is read-only design input from Revit. Keep an explicit
            // snapshot and restore it before every return; only formwork/accessory data
            // is permitted to change in this routine.
            double structuralSlabThickness = p.SlabThickness;
            double structuralBeamWidth = p.BeamWidth;
            double structuralBeamDepth = p.BeamDepth;
            double structuralBeamLength = p.BeamLength;
            double structuralColumnWidth = p.ColumnWidth;
            double structuralColumnDepth = p.ColumnDepth;
            double structuralColumnHeight = p.ColumnHeight;
            void RestoreStructuralGeometry()
            {
                p.SlabThickness = structuralSlabThickness;
                p.BeamWidth = structuralBeamWidth;
                p.BeamDepth = structuralBeamDepth;
                p.BeamLength = structuralBeamLength;
                p.ColumnWidth = structuralColumnWidth;
                p.ColumnDepth = structuralColumnDepth;
                p.ColumnHeight = structuralColumnHeight;
            }

            if (checkSlab || checkFoundation)
            {
                AutoDesignResult initial = AutoDesign(p);
                if (Math.Abs(p.Joist1Spacing - initial.L1) > 0.01) design.Adjustments.Add($"Bước xà gồ phụ: {p.Joist1Spacing:F0} → {initial.L1:F0} mm");
                if (Math.Abs(p.Joist2Spacing - initial.L2) > 0.01) design.Adjustments.Add($"Bước xà gồ chính: {p.Joist2Spacing:F0} → {initial.L2:F0} mm");
                if (Math.Abs(p.SupportSpacing - initial.L3) > 0.01) design.Adjustments.Add($"Bước cây chống: {p.SupportSpacing:F0} → {initial.L3:F0} mm");
                p.Joist1Spacing = initial.L1;
                p.Joist2Spacing = initial.L2;
                p.SupportSpacing = initial.L3;
            }

            // The column set may contain several storey heights. H/1000 becomes most
            // restrictive at the shortest segment, so use the attainable 1 mm survey/
            // turnbuckle target for the shared design. Per-section recalculation can
            // then never inherit a tolerance derived only from a taller representative.
            if (checkColumn && p.ColumnPlumbTargetTolerance > 1.0 + 0.001)
            {
                design.Adjustments.Add($"Dung sai chỉnh thẳng đứng cột: {p.ColumnPlumbTargetTolerance:F1} → 1.0 mm");
                p.ColumnPlumbTargetTolerance = 1.0;
            }

            CalculationResult slabResult = null;
            CalculationResult foundationResult = null;
            CalculationResult beamResult = null;
            CalculationResult columnResult = null;

            for (int iteration = 1; iteration <= maxIterations; iteration++)
            {
                design.Iterations = iteration;
                bool originalGround = p.IsGroundLevel;
                if (checkSlab) { p.IsGroundLevel = false; slabResult = CheckSafety(p); }
                if (checkFoundation) { p.IsGroundLevel = true; foundationResult = CheckSafety(p); }
                p.IsGroundLevel = originalGround;
                if (checkBeam) beamResult = CheckBeamSafety(p);
                if (checkColumn) columnResult = CheckColumnSafety(p);

                bool allOk = (!checkSlab || slabResult.IsSystemOk) &&
                             (!checkFoundation || foundationResult.IsSystemOk) &&
                             (!checkBeam || beamResult.IsSystemOk) &&
                             (!checkColumn || columnResult.IsSystemOk);
                if (allOk)
                {
                    RestoreStructuralGeometry();
                    design.IsCompliant = true;
                    return design;
                }

                bool changed = false;
                CalculationResult floorResult = checkSlab ? slabResult : foundationResult;
                if (floorResult != null && !floorResult.IsSystemOk)
                {
                    if (!floorResult.Plywood.IsOk)
                        changed |= ReduceParameter(() => p.Joist1Spacing, v => p.Joist1Spacing = v, 75, 50, "Bước xà gồ phụ", design.Adjustments) ||
                                   IncreaseParameter(() => p.VanMatThickness, v => p.VanMatThickness = v, 40, 3, "Chiều dày ván mặt", design.Adjustments);
                    if (!floorResult.Joist1.IsOk)
                        changed |= ReduceParameter(() => p.Joist2Spacing, v => p.Joist2Spacing = v, 100, 100, "Bước xà gồ chính", design.Adjustments) ||
                                   IncreaseParameter(() => p.XaGoPhuHeight, v => p.XaGoPhuHeight = v, 300, 25, "Chiều cao xà gồ phụ", design.Adjustments);
                    if (!floorResult.Joist2.IsOk)
                        changed |= ReduceParameter(() => p.SupportSpacing, v => p.SupportSpacing = v, 100, 100, "Bước cây chống", design.Adjustments) ||
                                   IncreaseParameter(() => p.XaGoChinhHeight, v => p.XaGoChinhHeight = v, 300, 25, "Chiều cao xà gồ chính", design.Adjustments);
                    changed |= ImproveVerticalSupport(p, floorResult, () => p.SupportSpacing, v => p.SupportSpacing = v, design.Adjustments);
                }

                if (beamResult != null && !beamResult.IsSystemOk)
                {
                    if (!beamResult.BeamBottomPlywood.IsOk)
                        changed |= ReduceParameter(() => p.BeamBottomYokeSpacing, v => p.BeamBottomYokeSpacing = v, 75, 50, "Bước xà gồ phụ đáy dầm", design.Adjustments) ||
                                   IncreaseParameter(() => p.BeamBottomPlywoodThickness, v => p.BeamBottomPlywoodThickness = v, 40, 3, "Chiều dày ván đáy dầm", design.Adjustments);
                    if (!beamResult.BeamSecondaryJoist.IsOk)
                        changed |= ReduceParameter(() => p.BeamPrimaryWalerSpacing, v => { p.BeamPrimaryWalerSpacing = v; p.BeamPropSpacing = Math.Min(p.BeamPropSpacing, v); }, 100, 100, "Bước xà gồ chính dầm", design.Adjustments) ||
                                   IncreaseParameter(() => p.BeamSecondaryJoistDepth, v => p.BeamSecondaryJoistDepth = v, 300, 25, "Chiều cao xà gồ phụ dầm", design.Adjustments);
                    if (!beamResult.BeamPrimaryWaler.IsOk)
                        changed |= ReduceParameter(() => p.BeamScaffoldBayWidth, v => p.BeamScaffoldBayWidth = v, 100, 100, "Nhịp ngang Ringlock", design.Adjustments) ||
                                   IncreaseParameter(() => p.BeamPrimaryWalerDepth, v => p.BeamPrimaryWalerDepth = v, 300, 25, "Chiều cao xà gồ chính dầm", design.Adjustments);
                    if (!beamResult.BeamSidePlywood.IsOk)
                        changed |= ReduceParameter(() => p.BeamSideYokeSpacing, v => p.BeamSideYokeSpacing = v, 75, 50, "Bước nẹp thành dầm", design.Adjustments) ||
                                   IncreaseParameter(() => p.BeamSidePlywoodThickness, v => p.BeamSidePlywoodThickness = v, 40, 3, "Chiều dày ván thành dầm", design.Adjustments);
                    if (!beamResult.BeamSideYoke.IsOk)
                        changed |= IncreaseParameter(() => p.BeamPrimaryWalerDepth, v => p.BeamPrimaryWalerDepth = v, 300, 25, "Chiều cao gông/nẹp thành", design.Adjustments);
                    if (!beamResult.BeamTieRod.IsOk && p.BeamPlacementModeIndex != 2)
                        changed |= UpgradeBeamTieRod(p, design.Adjustments);
                    if (p.BeamPlacementModeIndex == 2 && !beamResult.BeamEdgeSurfaceTieRod.IsOk)
                    {
                        if (beamResult.BeamEdgeSurfaceTieRod.StressResult != null &&
                            !beamResult.BeamEdgeSurfaceTieRod.StressResult.IsThoaman)
                            changed |= ReduceParameter(() => p.BeamEdgeSurfaceTieRodSpacing,
                                           v => p.BeamEdgeSurfaceTieRodSpacing = v, 300, 100,
                                           "Bước ty chết trên mặt dầm biên", design.Adjustments) ||
                                       IncreaseParameter(() => p.BeamEdgeSurfaceTieRodAllowableCapacity,
                                           v => p.BeamEdgeSurfaceTieRodAllowableCapacity = v, 40, 2,
                                           "Sức chịu kéo ty chết dầm biên", design.Adjustments);
                        if (beamResult.BeamEdgeSurfaceTieRod.DeflectionResult != null &&
                            !beamResult.BeamEdgeSurfaceTieRod.DeflectionResult.IsThoaman)
                            changed |= IncreaseParameter(() => p.BeamEdgeSurfaceTieRodAnchorLength,
                                v => p.BeamEdgeSurfaceTieRodAnchorLength = v, 2400, 100,
                                "Chiều dài neo ty chết dầm biên", design.Adjustments);
                    }
                    if (!beamResult.BeamSideBrace.IsOk && p.BeamPlacementModeIndex != 1)
                        changed |= UpgradeBeamSideBrace(p, design.Adjustments);
                    if (!beamResult.BeamStripping.IsOk && p.BeamStrippingStrengthPercent < 70)
                    {
                        design.Adjustments.Add($"Ngưỡng cường độ tháo cốp pha: {p.BeamStrippingStrengthPercent:F0} → 70%");
                        p.BeamStrippingStrengthPercent = 70;
                        changed = true;
                    }
                    if (!beamResult.BeamVibrationProcedure.IsOk)
                    {
                        double old = p.BeamVibratorOverlap;
                        p.BeamVibratorOverlap = Math.Max(50, Math.Min(100, old));
                        design.Adjustments.Add($"Chồng vùng đầm dùi: {old:F0} → {p.BeamVibratorOverlap:F0} mm");
                        changed = true;
                    }
                    changed |= ImproveVerticalSupport(p, beamResult, () => p.BeamPrimaryWalerSpacing,
                        v => { p.BeamPrimaryWalerSpacing = v; p.BeamPropSpacing = v; }, design.Adjustments);
                }

                if (columnResult != null && !columnResult.IsSystemOk)
                {
                    if (!columnResult.ColumnPlywood.IsOk)
                        changed |= ReduceParameter(() => p.ColumnStiffenerSpacing, v => p.ColumnStiffenerSpacing = v, 75, 25, "Bước nẹp đứng cột", design.Adjustments) ||
                                   IncreaseParameter(() => p.ColumnPlywoodThickness, v => p.ColumnPlywoodThickness = v, 40, 3, "Chiều dày ván cột", design.Adjustments);
                    if (!columnResult.ColumnStiffener.IsOk)
                    {
                        bool stiffenerChanged = false;
                        stiffenerChanged |= ReduceParameter(() => p.ColumnYokeBottomSpacing, v => p.ColumnYokeBottomSpacing = v, MinimumColumnYokeSpacingMm, 50, "Bước gông vùng chân", design.Adjustments);
                        stiffenerChanged |= ReduceParameter(() => p.ColumnYokeUpperSpacing, v => p.ColumnYokeUpperSpacing = v, MinimumColumnUpperYokeSpacingMm, 50, "Bước gông vùng trên", design.Adjustments);
                        if (!stiffenerChanged) stiffenerChanged |= IncreaseParameter(() => p.ColumnStiffenerDepth, v => p.ColumnStiffenerDepth = v, 300, 20, "Chiều cao nẹp đứng", design.Adjustments);
                        changed |= stiffenerChanged;
                    }
                    if (!columnResult.ColumnYoke.IsOk)
                    {
                        changed |= ReduceParameter(() => p.ColumnYokeBottomSpacing, v => p.ColumnYokeBottomSpacing = v, MinimumColumnYokeSpacingMm, 50, "Bước gông vùng chân", design.Adjustments);
                        changed |= ReduceParameter(() => p.ColumnYokeUpperSpacing, v => p.ColumnYokeUpperSpacing = v, MinimumColumnUpperYokeSpacingMm, 50, "Bước gông vùng trên", design.Adjustments);
                        if (p.ColumnYokeMaterialIndex == 0)
                            changed |= IncreaseParameter(() => p.ColumnYokeHeight, v => p.ColumnYokeHeight = v, 300, 20, "Chiều cao gông gỗ", design.Adjustments);
                        else
                            // For a rectangular hollow yoke, section depth governs I and W
                            // much more strongly than wall thickness. Upgrade the physical
                            // section in that order; thickness-only growth previously stalled
                            // on deep 300×2157/2211 mm perimeter columns.
                            changed |= IncreaseParameter(() => p.ColumnYokeHeight, v => p.ColumnYokeHeight = v, 300, 20, "Chiều cao gông thép hộp", design.Adjustments) ||
                                       IncreaseParameter(() => p.ColumnYokeWidth, v => p.ColumnYokeWidth = v, 150, 10, "Chiều rộng gông thép hộp", design.Adjustments) ||
                                       IncreaseParameter(() => p.ColumnYokeThickness, v => p.ColumnYokeThickness = v, 8, 0.5, "Chiều dày gông thép hộp", design.Adjustments);
                    }
                    changed |= UpgradeColumnRestraintAccessories(p, columnResult, design.Adjustments);
                    if (!columnResult.ColumnBraceBaseBearing.IsOk)
                    {
                        changed |= IncreaseParameter(() => p.SupportBasePlateWidth, v => p.SupportBasePlateWidth = v, 600, 50, "Bề rộng đế chống xiên", design.Adjustments);
                        changed |= IncreaseParameter(() => p.SupportBasePlateDepth, v => p.SupportBasePlateDepth = v, 600, 50, "Chiều dài đế chống xiên", design.Adjustments);
                    }
                    if (!columnResult.ColumnPlumb.IsOk)
                    {
                        double old = p.ColumnPlumbTargetTolerance;
                        p.ColumnPlumbTargetTolerance = 1.0;
                        design.Adjustments.Add($"Dung sai chỉnh cột: {old:F1} → {p.ColumnPlumbTargetTolerance:F1} mm");
                        changed |= Math.Abs(old - p.ColumnPlumbTargetTolerance) > 0.001;
                    }
                    if (!columnResult.ColumnPouring.StressResult.IsThoaman && p.ColumnPourLiftHeight > 2500)
                    {
                        design.Adjustments.Add($"Chiều cao đợt đổ: {p.ColumnPourLiftHeight:F0} → 2500 mm");
                        p.ColumnPourLiftHeight = 2500;
                        changed = true;
                    }
                    if (columnResult.ColumnPouring.ShearResult != null && !columnResult.ColumnPouring.ShearResult.IsThoaman && p.ColumnHoseBottomClearance > 1500)
                    {
                        design.Adjustments.Add($"Chiều cao rơi bê tông: {p.ColumnHoseBottomClearance:F0} → 1500 mm");
                        p.ColumnHoseBottomClearance = 1500;
                        changed = true;
                    }
                }

                if (!changed) break;
            }

            bool original = p.IsGroundLevel;
            var finalResults = new List<CalculationResult>();
            if (checkSlab) { p.IsGroundLevel = false; finalResults.Add(CheckSafety(p)); }
            if (checkFoundation) { p.IsGroundLevel = true; finalResults.Add(CheckSafety(p)); }
            p.IsGroundLevel = original;
            if (checkBeam) finalResults.Add(CheckBeamSafety(p));
            if (checkColumn) finalResults.Add(CheckColumnSafety(p));
            RestoreStructuralGeometry();
            design.IsCompliant = finalResults.Count > 0 && finalResults.TrueForAll(r => r.IsSystemOk);
            if (!design.IsCompliant)
            {
                foreach (CalculationResult result in finalResults)
                    foreach (KetQuaTinhToan check in result.GetAllChecks())
                        if (!check.IsThoaman)
                            design.UnresolvedChecks.Add($"{check.TenCauKien}: {check.NoiDungKiemTra} " +
                                $"({check.GiaTriThucTe:F2}/{check.GiaTriChoPhep:F2} {check.DonVi}, {check.PhanTramSuDung:F1}%)");
            }
            return design;
        }

        public static AutoDesignResult AutoDesign(CalculationParameters p)
        {
            var vanMat = new CauKienModel("Ván mặt", LoaiTietDien.VanMat, "Gỗ dán", 1000, p.VanMatThickness, p.VanMatThickness);
            var xaGoPhu = new CauKienModel("Xà gồ phụ", LoaiTietDien.XaGo, "Gỗ", p.XaGoPhuWidth, p.XaGoPhuHeight);
            var xaGoChinh = new CauKienModel("Xà gồ chính", LoaiTietDien.XaGo, "Gỗ", p.XaGoChinhWidth, p.XaGoChinhHeight);
            var cayChong = new CauKienModel("Cây chống", LoaiTietDien.CayChong, "Thép", p.CayChongDiameter, p.CayChongDiameter, p.CayChongThickness);

            double q_bt = (p.ConcreteWeight + p.SteelWeight) * (p.SlabThickness / 1000.0) * p.DeadLoadSafetyFactor; 
            double q_vk = p.WoodFormworkWeight * p.DeadLoadSafetyFactor; 
            double q_ll = (p.ConstructionLiveLoad + Math.Max(p.PouringImpactLoad, p.VibrationLoad)) * p.LiveLoadSafetyFactor; 
            double q_tt = q_bt + q_vk + q_ll; 
            double q_tc = (p.ConcreteWeight + p.SteelWeight) * (p.SlabThickness / 1000.0) + p.WoodFormworkWeight + (p.ConstructionLiveLoad + Math.Max(p.PouringImpactLoad, p.VibrationLoad));

            // 1. Khoảng cách xà gồ phụ (L1) — ván mặt trên dải rộng 1m
            double plyLimit = p.PlywoodBendingStrength * p.MoistureFactor * p.TemperatureFactor;
            double l1_stress = Math.Sqrt(GetMomentDivisor(p.SoNhipVanMat) * plyLimit * vanMat.MomenKhangUon_W / q_tt);
            double l1_def = p.SlabDeflectionLimitRatio > 0 
                ? Math.Pow((384.0 * p.ElasticModulus * vanMat.MomenQuanTinh_I * p.SlabDeflectionLimitRatio) / (5.0 * q_tc), 1.0/3.0)
                : Math.Pow((384.0 * p.ElasticModulus * vanMat.MomenQuanTinh_I * 2.0) / (5.0 * q_tc), 1.0/4.0);
            
            double L1 = Math.Min(l1_stress, l1_def);
            L1 = Math.Floor(L1 / 50.0) * 50.0;
            L1 = Math.Max(100.0, Math.Min(L1, 600.0));

            // 2. Khoảng cách xà gồ chính (L2)
            double q2_tt = q_tt * (L1 / 1000.0);
            double j1Limit = p.WoodStrength * p.MoistureFactor * p.TemperatureFactor;
            double l2_stress = Math.Sqrt(GetMomentDivisor(p.SoNhipXaGoPhu) * j1Limit * xaGoPhu.MomenKhangUon_W / q2_tt);
            
            double q2_tc = q_tc * (L1 / 1000.0);
            double l2_def = p.JoistDeflectionLimitRatio > 0
                ? Math.Pow((384.0 * p.ElasticModulus * xaGoPhu.MomenQuanTinh_I * p.JoistDeflectionLimitRatio) / (5.0 * q2_tc), 1.0/3.0)
                : Math.Pow((384.0 * p.ElasticModulus * xaGoPhu.MomenQuanTinh_I * 4.0) / (5.0 * q2_tc), 1.0/4.0);

            double j1ShearLimit = p.WoodShearStrength * p.MoistureFactor * p.TemperatureFactor;
            double shearCoeffJoist1 = GetShearCoefficient(p.SoNhipXaGoPhu);
            double l2_shear = (j1ShearLimit * xaGoPhu.DienTichMatCat_A) / (1.5 * shearCoeffJoist1 * q2_tt);

            double L2 = Math.Min(l2_stress, Math.Min(l2_def, l2_shear));
            L2 = Math.Floor(L2 / 50.0) * 50.0;
            L2 = Math.Max(300.0, Math.Min(L2, 1200.0));

            // 3. Khoảng cách cây chống (L3)
            double q3_tt = q_tt * (L2 / 1000.0);
            double j2Limit = p.WoodStrength * p.MoistureFactor * p.TemperatureFactor;
            double l3_stress = Math.Sqrt(GetMomentDivisor(p.SoNhipXaGoChinh) * j2Limit * xaGoChinh.MomenKhangUon_W / q3_tt);

            double q3_tc = q_tc * (L2 / 1000.0);
            double l3_def = p.JoistDeflectionLimitRatio > 0
                ? Math.Pow((384.0 * p.ElasticModulus * xaGoChinh.MomenQuanTinh_I * p.JoistDeflectionLimitRatio) / (5.0 * q3_tc), 1.0/3.0)
                : Math.Pow((384.0 * p.ElasticModulus * xaGoChinh.MomenQuanTinh_I * 4.0) / (5.0 * q3_tc), 1.0/4.0);
            
            double j2ShearLimit = p.WoodShearStrength * p.MoistureFactor * p.TemperatureFactor;
            double shearCoeffJoist2 = GetShearCoefficient(p.SoNhipXaGoChinh);
            double l3_shear = (j2ShearLimit * xaGoChinh.DienTichMatCat_A) / (1.5 * shearCoeffJoist2 * q3_tt);

            double r = cayChong.BanKinhQuanTinh_r;
            double lambda = r > 0 ? p.CayChongHeight / r : 0;
            double phi = CalculateBucklingCoefficient(lambda);
            double shoreLimit = phi * cayChong.DienTichMatCat_A * 210.0 / 1000.0; 
            shoreLimit = Math.Min(shoreLimit, 25.0); 

            double l3_shore = (shoreLimit * 1000000.0) / (q_tt * L2);

            double L3 = Math.Min(l3_stress, Math.Min(l3_def, Math.Min(l3_shear, l3_shore)));
            L3 = Math.Floor(L3 / 50.0) * 50.0;
            L3 = Math.Max(300.0, Math.Min(L3, 1500.0));

            return new AutoDesignResult { L1 = L1, L2 = L2, L3 = L3 };
        }

        public static double FixSupportSpacing(CalculationParameters p)
        {
            var xaGoChinh = new CauKienModel("Xà gồ chính", LoaiTietDien.XaGo, "Gỗ", p.XaGoChinhWidth, p.XaGoChinhHeight);
            var cayChong = new CauKienModel("Cây chống", LoaiTietDien.CayChong, "Thép", p.CayChongDiameter, p.CayChongDiameter, p.CayChongThickness);

            double q_bt = (p.ConcreteWeight + p.SteelWeight) * (p.SlabThickness / 1000.0) * p.DeadLoadSafetyFactor; 
            double q_vk = p.WoodFormworkWeight * p.DeadLoadSafetyFactor; 
            double q_ll = (p.ConstructionLiveLoad + Math.Max(p.PouringImpactLoad, p.VibrationLoad)) * p.LiveLoadSafetyFactor; 
            double q_tt = q_bt + q_vk + q_ll; 
            double q_tc = (p.ConcreteWeight + p.SteelWeight) * (p.SlabThickness / 1000.0) + p.WoodFormworkWeight + (p.ConstructionLiveLoad + Math.Max(p.PouringImpactLoad, p.VibrationLoad));

            // Tính L3 dựa trên L2 hiện tại (p.Joist2Spacing)
            double L2 = p.Joist2Spacing > 0 ? p.Joist2Spacing : 1000.0;

            double q3_tt = q_tt * (L2 / 1000.0);
            double j2Limit = p.WoodStrength * p.MoistureFactor * p.TemperatureFactor;
            double l3_stress = Math.Sqrt(GetMomentDivisor(p.SoNhipXaGoChinh) * j2Limit * xaGoChinh.MomenKhangUon_W / q3_tt);

            double q3_tc = q_tc * (L2 / 1000.0);
            double l3_def = p.JoistDeflectionLimitRatio > 0
                ? Math.Pow((384.0 * p.ElasticModulus * xaGoChinh.MomenQuanTinh_I * p.JoistDeflectionLimitRatio) / (5.0 * q3_tc), 1.0/3.0)
                : Math.Pow((384.0 * p.ElasticModulus * xaGoChinh.MomenQuanTinh_I * 4.0) / (5.0 * q3_tc), 1.0/4.0);
            
            double j2ShearLimit = p.WoodShearStrength * p.MoistureFactor * p.TemperatureFactor;
            double shearCoeffJoist2 = GetShearCoefficient(p.SoNhipXaGoChinh);
            double l3_shear = (j2ShearLimit * xaGoChinh.DienTichMatCat_A) / (1.5 * shearCoeffJoist2 * q3_tt);

            double r = cayChong.BanKinhQuanTinh_r;
            double lambda = r > 0 ? p.CayChongHeight / r : 0;
            double phi = CalculateBucklingCoefficient(lambda);
            double shoreLimit = phi * cayChong.DienTichMatCat_A * 210.0 / 1000.0; 
            shoreLimit = Math.Min(shoreLimit, 25.0); 

            double l3_shore = (shoreLimit * 1000000.0) / (q_tt * L2);

            double L3 = Math.Min(l3_stress, Math.Min(l3_def, Math.Min(l3_shear, l3_shore)));
            L3 = Math.Floor(L3 / 50.0) * 50.0;
            return Math.Max(300.0, Math.Min(L3, 1500.0));
        }
    }
}
