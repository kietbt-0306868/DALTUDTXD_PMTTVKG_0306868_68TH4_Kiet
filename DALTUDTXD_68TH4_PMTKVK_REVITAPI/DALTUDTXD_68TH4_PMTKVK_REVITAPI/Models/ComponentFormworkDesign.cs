using System;
using System.Collections.Generic;
using System.Linq;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models
{
    /// <summary>
    /// The validated accessory/formwork design for one Revit element. Structural
    /// dimensions in Parameters are an immutable calculation input; this object is
    /// never used to resize the source Revit element.
    /// </summary>
    public sealed class ComponentFormworkDesign
    {
        public string ElementUniqueId { get; set; }
        public string ElementId { get; set; }
        public string DisplayName { get; set; }
        public string LevelName { get; set; }
        public StructuralComponentType ComponentType { get; set; }
        public CalculationParameters Parameters { get; set; }
        public bool IsCompliant { get; set; }
        public int Iterations { get; set; }
        public DateTime Timestamp { get; set; }
        public List<string> Adjustments { get; set; } = new List<string>();
        public List<string> UnresolvedChecks { get; set; } = new List<string>();

        public ComponentFormworkDesign Clone()
        {
            return new ComponentFormworkDesign
            {
                ElementUniqueId = ElementUniqueId,
                ElementId = ElementId,
                DisplayName = DisplayName,
                LevelName = LevelName,
                ComponentType = ComponentType,
                Parameters = Parameters?.Clone(),
                IsCompliant = IsCompliant,
                Iterations = Iterations,
                Timestamp = Timestamp,
                Adjustments = Adjustments?.ToList() ?? new List<string>(),
                UnresolvedChecks = UnresolvedChecks?.ToList() ?? new List<string>()
            };
        }
    }
}
