using System;
using System.Collections.Generic;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models
{
    /// <summary>
    /// Scaffold systems supported by the automatic slab/beam shoring workflow.
    /// The flags representation permits one system or a user-approved set of systems.
    /// </summary>
    [Flags]
    public enum ScaffoldSystemType
    {
        None = 0,
        Frame = 1,
        Wedge = 2,
        Ringlock = 4
    }

    public static class ScaffoldSystemTypeExtensions
    {
        public const ScaffoldSystemType SupportedSystems =
            ScaffoldSystemType.Frame | ScaffoldSystemType.Wedge | ScaffoldSystemType.Ringlock;

        public static ScaffoldSystemType Normalize(this ScaffoldSystemType systems)
        {
            ScaffoldSystemType supported = systems & SupportedSystems;
            return supported == ScaffoldSystemType.None ? ScaffoldSystemType.Ringlock : supported;
        }

        public static string ToDisplayText(this ScaffoldSystemType systems)
        {
            systems = systems.Normalize();
            var labels = new List<string>();
            if ((systems & ScaffoldSystemType.Frame) != 0) labels.Add("Giàn giáo khung");
            if ((systems & ScaffoldSystemType.Wedge) != 0) labels.Add("Giàn giáo nêm");
            if ((systems & ScaffoldSystemType.Ringlock) != 0) labels.Add("Ringlock");
            return string.Join(" · ", labels);
        }
    }
}
