using System;
using System.Collections.Generic;
using System.Linq;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.Services
{
    /// <summary>
    /// Provides one deterministic scaffold-system decision for each construction
    /// storey. Actual bearing faces still control every foot elevation, but Revit
    /// Level (or host-top fallback) controls the catalogue zone so slab drops and
    /// beam soffit offsets cannot alternate systems inside one storey.
    /// </summary>
    internal static class ScaffoldSystemLayoutPolicy
    {
        internal const double DefaultElevationToleranceMm = 75.0;

        internal static ScaffoldSystemType Normalize(ScaffoldSystemType selected)
        {
            ScaffoldSystemType known = selected &
                (ScaffoldSystemType.Frame | ScaffoldSystemType.Wedge |
                 ScaffoldSystemType.Ringlock);
            return known == ScaffoldSystemType.None
                ? ScaffoldSystemType.Ringlock
                : known;
        }

        internal static IReadOnlyList<ScaffoldSystemType> OrderedSystems(
            ScaffoldSystemType selected)
        {
            selected = Normalize(selected);
            var result = new List<ScaffoldSystemType>(3);
            if ((selected & ScaffoldSystemType.Frame) != 0)
                result.Add(ScaffoldSystemType.Frame);
            if ((selected & ScaffoldSystemType.Wedge) != 0)
                result.Add(ScaffoldSystemType.Wedge);
            if ((selected & ScaffoldSystemType.Ringlock) != 0)
                result.Add(ScaffoldSystemType.Ringlock);
            return result;
        }

        internal static string FamilyToken(ScaffoldSystemType system)
        {
            switch (NormalizeSingle(system))
            {
                case ScaffoldSystemType.Frame:
                    return "FRAME";
                case ScaffoldSystemType.Wedge:
                    return "WEDGE";
                default:
                    return "RINGLOCK";
            }
        }

        internal static ScaffoldSystemType NormalizeSingle(ScaffoldSystemType system)
        {
            if ((system & ScaffoldSystemType.Frame) != 0)
                return ScaffoldSystemType.Frame;
            if ((system & ScaffoldSystemType.Wedge) != 0)
                return ScaffoldSystemType.Wedge;
            return ScaffoldSystemType.Ringlock;
        }

        internal static ScaffoldElevationZoning CreateElevationZoning(
            IEnumerable<double> constructionZoneElevationsMm,
            ScaffoldSystemType selected,
            double toleranceMm = DefaultElevationToleranceMm)
        {
            return new ScaffoldElevationZoning(constructionZoneElevationsMm, selected,
                toleranceMm);
        }
    }

    /// <summary>
    /// Immutable clustered elevation map.  Sorting and clustering happen before
    /// assignment, so input/component traversal order cannot change the result.
    /// </summary>
    internal sealed class ScaffoldElevationZoning
    {
        private readonly IReadOnlyList<double> _zoneElevationsMm;
        private readonly IReadOnlyList<ScaffoldSystemType> _systems;
        private readonly double _toleranceMm;

        internal ScaffoldElevationZoning(IEnumerable<double> constructionZoneElevationsMm,
            ScaffoldSystemType selected, double toleranceMm)
        {
            _systems = ScaffoldSystemLayoutPolicy.OrderedSystems(selected);
            _toleranceMm = double.IsFinite(toleranceMm)
                ? Math.Max(1.0, toleranceMm)
                : ScaffoldSystemLayoutPolicy.DefaultElevationToleranceMm;

            List<double> sorted = (constructionZoneElevationsMm ?? Enumerable.Empty<double>())
                .Where(double.IsFinite)
                .OrderBy(value => value)
                .ToList();
            var clustered = new List<double>();
            int cursor = 0;
            while (cursor < sorted.Count)
            {
                double sum = sorted[cursor];
                int count = 1;
                double last = sorted[cursor];
                cursor++;
                while (cursor < sorted.Count && sorted[cursor] - last <= _toleranceMm)
                {
                    last = sorted[cursor];
                    sum += last;
                    count++;
                    cursor++;
                }
                clustered.Add(sum / count);
            }
            _zoneElevationsMm = clustered;
        }

        internal int ZoneCount => _zoneElevationsMm.Count;

        internal int ZoneIndexFor(double constructionZoneElevationMm)
        {
            if (_zoneElevationsMm.Count == 0 || !double.IsFinite(constructionZoneElevationMm))
                return 0;

            int bestIndex = 0;
            double bestDistance = Math.Abs(_zoneElevationsMm[0] - constructionZoneElevationMm);
            for (int index = 1; index < _zoneElevationsMm.Count; index++)
            {
                double distance = Math.Abs(_zoneElevationsMm[index] - constructionZoneElevationMm);
                if (distance >= bestDistance - 1e-9) continue;
                bestIndex = index;
                bestDistance = distance;
            }
            return bestIndex;
        }

        internal ScaffoldSystemType SystemFor(double constructionZoneElevationMm)
        {
            int zoneIndex = ZoneIndexFor(constructionZoneElevationMm);
            return _systems[zoneIndex % _systems.Count];
        }
    }
}
