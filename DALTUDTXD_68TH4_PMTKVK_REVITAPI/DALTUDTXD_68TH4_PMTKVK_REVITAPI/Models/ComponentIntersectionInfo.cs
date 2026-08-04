using System;
using System.Collections.Generic;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models
{
    public enum FormworkCutoutKind
    {
        BeamSupport,
        ColumnPenetration,
        FoundationColumnOpening
    }

    public sealed class FormworkCutoutRectangle
    {
        public double MinXmm { get; set; }
        public double MaxXmm { get; set; }
        public double MinYmm { get; set; }
        public double MaxYmm { get; set; }
        public FormworkCutoutKind Kind { get; set; }
        public string SourceElementUniqueId { get; set; }
        public double AreaM2 => Math.Max(0, MaxXmm - MinXmm) * Math.Max(0, MaxYmm - MinYmm) / 1_000_000.0;
    }

    public sealed class FormworkLengthInterval
    {
        public double StartMm { get; set; }
        public double EndMm { get; set; }
        public string SourceElementUniqueId { get; set; }
        public double LengthMm => Math.Max(0, EndMm - StartMm);
    }

    public sealed class ComponentIntersectionInfo
    {
        public string ElementUniqueId { get; set; }
        public StructuralComponentType ComponentType { get; set; }
        public List<string> IntersectingElementUniqueIds { get; } = new List<string>();
        public List<FormworkCutoutRectangle> PlanCutouts { get; } = new List<FormworkCutoutRectangle>();
        public List<FormworkLengthInterval> BeamColumnGaps { get; } = new List<FormworkLengthInterval>();

        // +local X, -local X, +local Y, -local Y. Each value is the
        // vertical height occupied by a beam on that column face.
        public double[] ColumnFaceTopReductionsMm { get; } = new double[4];

        public double GrossBottomAreaM2 { get; set; }
        public double NetBottomAreaM2 { get; set; }
        public double GrossSideAreaM2 { get; set; }
        public double NetSideAreaM2 { get; set; }
        public double GrossTopAreaM2 { get; set; }
        public double NetTopAreaM2 { get; set; }
        public double InternalOpeningSideAreaM2 { get; set; }
        public double GrossPerimeterM { get; set; }
        public double NetExposedPerimeterM { get; set; }
        public double GrossLengthMm { get; set; }
        public double NetFormworkLengthMm { get; set; }

        public double NetPlywoodAreaM2 => Math.Max(0,
            NetBottomAreaM2 + NetSideAreaM2 + NetTopAreaM2 + InternalOpeningSideAreaM2);

        public string DebugSummary =>
            $"{IntersectingElementUniqueIds.Count} giao; ván ròng {NetPlywoodAreaM2:F3} m²; " +
            $"đáy {NetBottomAreaM2:F3}; thành {NetSideAreaM2:F3}; đỉnh {NetTopAreaM2:F3}";
    }
}
