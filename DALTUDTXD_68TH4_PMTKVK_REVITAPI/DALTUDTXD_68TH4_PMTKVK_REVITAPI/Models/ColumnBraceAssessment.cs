using System.Collections.Generic;
using System.Windows.Media.Media3D;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models
{
    public enum BraceFootSupportKind
    {
        Slab,
        ExteriorOpen,
        InternalOpening,
        FoundationOrGround,
        Unsupported
    }

    public enum ColumnBraceDerivedType
    {
        Interior,
        Edge,
        Corner,
        OppositeOpenWarning,
        IsolatedWarning,
        InternalOpeningWarning,
        FoundationGround,
        NoSupportWarning
    }

    public sealed class BraceDirectionAssessment
    {
        public int DirectionIndex { get; set; }
        public Vector3D LocalDirection { get; set; }
        public Point3D ProposedFootPoint { get; set; }
        public double ResolvedRunMm { get; set; }
        public BraceFootSupportKind SupportKind { get; set; }
        public bool WasRenderedAsProp { get; set; }
        public bool WasResolvedByAlternativeRestraint { get; set; }
        public bool CanPlaceProp => SupportKind == BraceFootSupportKind.Slab ||
                                    SupportKind == BraceFootSupportKind.FoundationOrGround;
    }

    public sealed class ColumnBraceAssessment
    {
        public long ColumnId { get; set; }
        public long LevelId { get; set; }
        public string LevelName { get; set; }
        public double SupportElevationMm { get; set; }
        public double BraceRunMm { get; set; }
        public double ColumnWidthMm { get; set; }
        public double ColumnDepthMm { get; set; }
        public double ColumnHeightMm { get; set; }
        public List<BraceDirectionAssessment> Directions { get; } = new List<BraceDirectionAssessment>();
        public ColumnBraceDerivedType DerivedType { get; set; }
        public bool IsManuallyOverridden { get; set; }
        public ColumnBraceDerivedType ManualOverrideType { get; set; }
        public string RecommendedAction { get; set; }
        public string PreviousLevelAction { get; set; }

        public ColumnBraceDerivedType EffectiveType => IsManuallyOverridden ? ManualOverrideType : DerivedType;
        public bool HasUnsupportedSlabDirection => Directions.Exists(direction =>
            direction.SupportKind != BraceFootSupportKind.Slab);
        public bool IsPerimeterForCalculation => HasUnsupportedSlabDirection;
        public string CacheKey => BuildCacheKey(ColumnId, LevelId);
        public string DisplayLabel => LevelName + " — " + EffectiveType + " (" + CacheKey + ")";

        public static string BuildCacheKey(long columnId, long levelId) => columnId + ":" + levelId;
    }
}
