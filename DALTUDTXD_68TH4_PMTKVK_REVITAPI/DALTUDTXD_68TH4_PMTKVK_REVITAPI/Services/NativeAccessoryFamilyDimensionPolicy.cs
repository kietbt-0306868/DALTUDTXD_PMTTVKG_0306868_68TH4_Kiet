using System;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.Services
{
    /// <summary>
    /// Reduces preview-only accessory dimensions to a small reusable catalogue.
    /// Structural dimensions and calculation parameters are deliberately untouched;
    /// only generated Revit family geometry is normalised here.
    /// </summary>
    internal static class NativeAccessoryFamilyDimensionPolicy
    {
        internal readonly struct PushPullProfile
        {
            internal PushPullProfile(double ratedCapacityKn, double outerDiameterMm,
                double wallThicknessMm, double innerStageDiameterMm)
            {
                RatedCapacityKn = ratedCapacityKn;
                OuterDiameterMm = outerDiameterMm;
                WallThicknessMm = wallThicknessMm;
                InnerStageDiameterMm = innerStageDiameterMm;
            }

            internal double RatedCapacityKn { get; }
            internal double OuterDiameterMm { get; }
            internal double WallThicknessMm { get; }
            internal double InnerStageDiameterMm { get; }
            internal double SleeveDiameterMm => OuterDiameterMm + 12.0;
            internal double CollarDiameterMm => OuterDiameterMm + 18.0;
            internal double CouplerDiameterMm => OuterDiameterMm + 8.0;
            internal double PlateSizeMm => Math.Min(220.0,
                Math.Max(100.0, OuterDiameterMm * 2.0));
        }

        // Rated push-pull props.  The 140 kN entry is deliberately an explicit
        // conservative D114x5 profile rather than silently reusing the D89 family.
        // Inner-stage diameters describe the visible telescoping member and keep the
        // common 15 kN family consistent with a D48 outer / approximately D42 inner.
        private static readonly PushPullProfile[] PushPullProfiles =
        {
            new PushPullProfile(15.0, 48.0, 2.0, 42.0),
            new PushPullProfile(25.0, 48.0, 3.2, 42.0),
            new PushPullProfile(40.0, 60.0, 3.2, 48.0),
            new PushPullProfile(60.0, 76.0, 3.2, 60.0),
            new PushPullProfile(90.0, 89.0, 4.0, 76.0),
            new PushPullProfile(140.0, 114.0, 5.0, 89.0)
        };

        internal const double MinimumPropHeightMm = 900.0;
        internal const double MaximumSinglePropHeightMm = 6000.0;
        internal const double PropHeightStepMm = 100.0;
        internal const double ScaffoldBayWidthStepMm = 100.0;

        // Module catalogues intentionally differ by system.  Sharing one anonymous
        // height list was one reason H-frame geometry appeared beside Ringlock/Wedge
        // geometry while still carrying the same family key.
        private static readonly double[] FrameModuleHeightsMm =
        {
            1700.0, 1530.0, 1200.0
        };

        private static readonly double[] WedgeModuleHeightsMm =
        {
            1800.0, 1500.0, 1200.0, 900.0
        };

        private static readonly double[] RinglockModuleHeightsMm =
        {
            2000.0, 1500.0, 1000.0, 500.0
        };

        internal static bool TryGetPropFamilyHeight(double actualHeightMm,
            out double familyHeightMm)
        {
            familyHeightMm = 0;
            if (!double.IsFinite(actualHeightMm) ||
                actualHeightMm < MinimumPropHeightMm ||
                actualHeightMm > MaximumSinglePropHeightMm)
                return false;

            familyHeightMm = FloorToStep(actualHeightMm, PropHeightStepMm);
            return familyHeightMm >= MinimumPropHeightMm;
        }

        internal static double GetScaffoldBayFamilyWidth(double actualWidthMm)
        {
            if (!double.IsFinite(actualWidthMm) || actualWidthMm < 300.0) return 0;
            return FloorToStep(actualWidthMm, ScaffoldBayWidthStepMm);
        }

        internal static bool TryGetScaffoldTierLayout(double clearHeightMm,
            double maximumTopAdjustmentMm, out int moduleCount, out double moduleHeightMm)
        {
            return TryGetScaffoldTierLayout(ScaffoldSystemType.Ringlock,
                clearHeightMm, maximumTopAdjustmentMm, out moduleCount,
                out moduleHeightMm);
        }

        internal static bool TryGetScaffoldTierLayout(ScaffoldSystemType system,
            double clearHeightMm, double maximumTopAdjustmentMm,
            out int moduleCount, out double moduleHeightMm)
        {
            moduleCount = 0;
            moduleHeightMm = 0;
            if (!double.IsFinite(clearHeightMm) || clearHeightMm < 600.0) return false;

            double adjustmentLimit = double.IsFinite(maximumTopAdjustmentMm)
                ? Math.Max(0, maximumTopAdjustmentMm)
                : 0;
            double bestResidual = double.PositiveInfinity;
            int bestCount = int.MaxValue;

            foreach (double candidateHeight in ScaffoldModuleHeights(system))
            {
                int candidateCount = (int)Math.Floor((clearHeightMm + 1e-6) / candidateHeight);
                if (candidateCount < 1) continue;
                double residual = clearHeightMm - candidateCount * candidateHeight;
                if (residual < -1e-6 || residual > adjustmentLimit + 1e-6) continue;

                if (residual < bestResidual - 1e-6 ||
                    Math.Abs(residual - bestResidual) <= 1e-6 && candidateCount < bestCount)
                {
                    bestResidual = residual;
                    bestCount = candidateCount;
                    moduleCount = candidateCount;
                    moduleHeightMm = candidateHeight;
                }
            }

            return moduleCount > 0;
        }

        private static double[] ScaffoldModuleHeights(ScaffoldSystemType system)
        {
            switch (ScaffoldSystemLayoutPolicy.NormalizeSingle(system))
            {
                case ScaffoldSystemType.Frame:
                    return FrameModuleHeightsMm;
                case ScaffoldSystemType.Wedge:
                    return WedgeModuleHeightsMm;
                default:
                    return RinglockModuleHeightsMm;
            }
        }

        internal static PushPullProfile GetPushPullProfile(double ratedCapacityKn,
            double explicitDiameterMm = 0, double explicitThicknessMm = 0)
        {
            double requestedCapacity = double.IsFinite(ratedCapacityKn)
                ? Math.Max(0.0, ratedCapacityKn)
                : 0.0;
            PushPullProfile catalogueProfile = PushPullProfiles[PushPullProfiles.Length - 1];
            foreach (PushPullProfile candidate in PushPullProfiles)
            {
                if (candidate.RatedCapacityKn + 1e-6 < requestedCapacity) continue;
                catalogueProfile = candidate;
                break;
            }

            // The supported accessory catalogue ends at 140 kN. Never attach an
            // arbitrary higher rating to that geometry: the check must stop at the
            // catalogue limit and require a manual/manufacturer-specific override.
            double effectiveCapacity = requestedCapacity <= 0
                ? catalogueProfile.RatedCapacityKn
                : Math.Min(requestedCapacity,
                    PushPullProfiles[PushPullProfiles.Length - 1].RatedCapacityKn);

            if (!double.IsFinite(explicitDiameterMm) || explicitDiameterMm <= 0)
                return new PushPullProfile(effectiveCapacity,
                    catalogueProfile.OuterDiameterMm,
                    catalogueProfile.WallThicknessMm,
                    catalogueProfile.InnerStageDiameterMm);

            double diameter = Math.Max(20.0, explicitDiameterMm);
            double thickness = double.IsFinite(explicitThicknessMm) && explicitThicknessMm > 0
                ? Math.Min(diameter * 0.20, explicitThicknessMm)
                : catalogueProfile.WallThicknessMm;

            // An explicit section may be larger than the minimum catalogue section,
            // but it may never be smaller in either outside diameter or wall
            // thickness. This repairs old saved states such as 40 kN + D48x2 before
            // they reach either the calculation or the generated RFA.
            if (diameter + 0.1 < catalogueProfile.OuterDiameterMm ||
                thickness + 0.05 < catalogueProfile.WallThicknessMm)
            {
                return new PushPullProfile(effectiveCapacity,
                    catalogueProfile.OuterDiameterMm,
                    catalogueProfile.WallThicknessMm,
                    catalogueProfile.InnerStageDiameterMm);
            }

            // Preserve the catalogue's known telescoping pairs whenever the selected
            // diameter matches one. A custom diameter retains at least 6 mm clearance
            // so the preview never displays two coincident solid tubes.
            PushPullProfile matchingDiameter = default;
            bool matched = false;
            foreach (PushPullProfile candidate in PushPullProfiles)
            {
                if (Math.Abs(candidate.OuterDiameterMm - diameter) > 0.1) continue;
                matchingDiameter = candidate;
                matched = true;
                break;
            }
            double innerDiameter = matched
                ? matchingDiameter.InnerStageDiameterMm
                : Math.Max(12.0, diameter - Math.Max(6.0, 2.0 * thickness));
            return new PushPullProfile(effectiveCapacity,
                diameter, thickness, innerDiameter);
        }

        internal static bool TryGetNextPushPullProfile(double ratedCapacityKn,
            double diameterMm, double thicknessMm, out PushPullProfile profile,
            double maximumRatedCapacityKn = double.PositiveInfinity)
        {
            profile = default;
            double currentCapacity = double.IsFinite(ratedCapacityKn)
                ? Math.Max(0.0, ratedCapacityKn)
                : 0.0;
            double currentDiameter = double.IsFinite(diameterMm)
                ? Math.Max(0.0, diameterMm)
                : 0.0;
            double currentThickness = double.IsFinite(thicknessMm)
                ? Math.Max(0.0, Math.Min(currentDiameter / 2.0, thicknessMm))
                : 0.0;
            double currentArea = Math.PI / 4.0 *
                                 (currentDiameter * currentDiameter -
                                  Math.Pow(Math.Max(0.0,
                                      currentDiameter - 2.0 * currentThickness), 2));

            foreach (PushPullProfile candidate in PushPullProfiles)
            {
                if (candidate.RatedCapacityKn <= currentCapacity + 0.1 ||
                    candidate.RatedCapacityKn > maximumRatedCapacityKn + 0.1)
                    continue;
                double candidateArea = Math.PI / 4.0 *
                                       (candidate.OuterDiameterMm * candidate.OuterDiameterMm -
                                        Math.Pow(candidate.OuterDiameterMm -
                                                 2.0 * candidate.WallThicknessMm, 2));
                // Never satisfy a capacity upgrade by silently reducing a custom or
                // previously selected physical tube section.
                if (candidateArea + 0.1 < currentArea) continue;
                profile = candidate;
                return true;
            }
            return false;
        }

        internal static double FloorToStep(double value, double step)
        {
            if (!double.IsFinite(value) || value <= 0 ||
                !double.IsFinite(step) || step <= 0) return 0;
            return Math.Floor((value + 1e-6) / step) * step;
        }
    }
}
