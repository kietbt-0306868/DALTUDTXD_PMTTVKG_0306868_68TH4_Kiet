using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Services;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels
{
    /// <summary>
    /// Payload truyền qua sự kiện SlabSelectionCompleted, chứa toàn bộ dữ liệu
    /// sau khi người dùng chọn sàn từ Revit, để các trang đồng bộ cùng lúc.
    /// </summary>
    public class SlabSelectionEventArgs : EventArgs
    {
        public List<FloorElementModel> FloorElements { get; set; }
        public double SlabThickness { get; set; }
        public double SelectedSlabArea { get; set; }
        public double SelectedSlabPerimeter { get; set; }
        public double Joist1Spacing { get; set; }
        public double Joist2Spacing { get; set; }
        public double SupportSpacing { get; set; }
        public int FloorCount { get; set; }
        public int ComponentCount { get; set; }
        public int SlabCount { get; set; }
        public int FoundationCount { get; set; }
        public int BeamCount { get; set; }
        public int ColumnCount { get; set; }
        public string MaxFloorName { get; set; }
        public List<List<System.Windows.Point>> SlabPolygons { get; set; }
    }

    public class SharedDataViewModel : INotifyPropertyChanged, IDataErrorInfo
    {
        private sealed class PersistedValidatedDesign
        {
            public CalculationParameters Parameters { get; set; }
            public IReadOnlyList<string> Adjustments { get; set; }
            public Dictionary<string, ComponentFormworkDesign> ComponentDesigns { get; set; }
            public int Revision { get; set; }
            public DateTime Timestamp { get; set; }
        }

        private static readonly object ValidatedDesignSessionLock = new object();
        private static readonly Dictionary<string, PersistedValidatedDesign> ValidatedDesignSessionCache =
            new Dictionary<string, PersistedValidatedDesign>();

        private readonly Dictionary<string, ColumnBraceAssessment> _columnBraceAssessmentCache =
            new Dictionary<string, ColumnBraceAssessment>();
        private readonly Dictionary<string, ColumnBraceDerivedType> _columnBraceOverrides =
            new Dictionary<string, ColumnBraceDerivedType>();
        private Dictionary<string, ComponentIntersectionInfo> _componentIntersectionCache =
            new Dictionary<string, ComponentIntersectionInfo>(StringComparer.OrdinalIgnoreCase);
        private string _selectedColumnBraceAssessmentKey;
        private long _selectedColumnId = -1;
        private int _columnBraceOverrideIndex;

        public IReadOnlyDictionary<string, ColumnBraceAssessment> ColumnBraceAssessmentCache => _columnBraceAssessmentCache;
        public List<ColumnBraceAssessment> SelectedColumnBraceAssessments => _columnBraceAssessmentCache.Values
            .Where(assessment => assessment.ColumnId == _selectedColumnId)
            .OrderBy(assessment => assessment.SupportElevationMm)
            .ToList();

        public string SelectedColumnBraceAssessmentKey
        {
            get => _selectedColumnBraceAssessmentKey;
            set
            {
                _selectedColumnBraceAssessmentKey = value;
                if (!string.IsNullOrEmpty(value) && _columnBraceAssessmentCache.TryGetValue(value, out ColumnBraceAssessment assessment))
                    _columnBraceOverrideIndex = assessment.IsManuallyOverridden ? OverrideIndexFromType(assessment.ManualOverrideType) : 0;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ColumnBraceOverrideIndex));
                OnPropertyChanged(nameof(SelectedColumnBraceAssessmentSummary));
                OnPropertyChanged(nameof(SelectedColumnBraceAssessmentIsAutoResolvable));
            }
        }

        public int ColumnBraceOverrideIndex
        {
            get => _columnBraceOverrideIndex;
            set
            {
                _columnBraceOverrideIndex = value;
                if (!string.IsNullOrEmpty(SelectedColumnBraceAssessmentKey))
                {
                    if (value <= 0) _columnBraceOverrides.Remove(SelectedColumnBraceAssessmentKey);
                    else _columnBraceOverrides[SelectedColumnBraceAssessmentKey] = OverrideTypeFromIndex(value);
                    if (_columnBraceAssessmentCache.TryGetValue(SelectedColumnBraceAssessmentKey, out ColumnBraceAssessment assessment))
                    {
                        assessment.IsManuallyOverridden = value > 0;
                        if (value > 0) assessment.ManualOverrideType = OverrideTypeFromIndex(value);
                    }
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedColumnBraceAssessmentSummary));
                OnPropertyChanged(nameof(SelectedColumnBraceAssessmentIsAutoResolvable));
            }
        }

        public string SelectedColumnBraceAssessmentSummary
        {
            get
            {
                if (string.IsNullOrEmpty(SelectedColumnBraceAssessmentKey) ||
                    !_columnBraceAssessmentCache.TryGetValue(SelectedColumnBraceAssessmentKey, out ColumnBraceAssessment assessment))
                    return "Select a Revit column and refresh Preview3D to obtain its per-storey assessment.";
                string source = assessment.IsManuallyOverridden ? "manual override" : "derived from geometry";
                string priorLevel = string.IsNullOrWhiteSpace(assessment.PreviousLevelAction)
                    ? string.Empty
                    : " PRIOR-LEVEL ACTION: " + assessment.PreviousLevelAction;
                string directionChecks = string.Join(" | ", assessment.Directions.Select(direction =>
                    $"H{direction.DirectionIndex + 1} ({direction.ProposedFootPoint.X:F0},{direction.ProposedFootPoint.Y:F0})={direction.SupportKind}"));
                return $"{assessment.LevelName}: {assessment.EffectiveType} ({source}) — {assessment.RecommendedAction}{priorLevel} KIỂM TRA CHÂN: {directionChecks}";
            }
        }

        public bool SelectedColumnBraceAssessmentIsAutoResolvable
        {
            get
            {
                if (string.IsNullOrEmpty(SelectedColumnBraceAssessmentKey) ||
                    !_columnBraceAssessmentCache.TryGetValue(SelectedColumnBraceAssessmentKey, out ColumnBraceAssessment assessment))
                    return true;
                bool requiresEngineer = assessment.DerivedType == ColumnBraceDerivedType.OppositeOpenWarning ||
                                        assessment.DerivedType == ColumnBraceDerivedType.IsolatedWarning ||
                                        assessment.DerivedType == ColumnBraceDerivedType.InternalOpeningWarning ||
                                        assessment.DerivedType == ColumnBraceDerivedType.NoSupportWarning;
                if (requiresEngineer && !assessment.IsManuallyOverridden) return false;
                return assessment.Directions.Count > 0 && assessment.Directions.All(direction =>
                    direction.CanPlaceProp || direction.WasResolvedByAlternativeRestraint);
            }
        }

        public void StoreColumnBraceAssessment(ColumnBraceAssessment assessment)
        {
            if (assessment == null) return;
            if (_columnBraceOverrides.TryGetValue(assessment.CacheKey, out ColumnBraceDerivedType overrideType))
            {
                assessment.IsManuallyOverridden = true;
                assessment.ManualOverrideType = overrideType;
            }
            _columnBraceAssessmentCache[assessment.CacheKey] = assessment;
            if (assessment.ColumnId == _selectedColumnId)
                OnPropertyChanged(nameof(SelectedColumnBraceAssessments));
            if (assessment.CacheKey == SelectedColumnBraceAssessmentKey)
            {
                _columnBraceOverrideIndex = assessment.IsManuallyOverridden
                    ? OverrideIndexFromType(assessment.ManualOverrideType)
                    : 0;
                OnPropertyChanged(nameof(ColumnBraceOverrideIndex));
                OnPropertyChanged(nameof(SelectedColumnBraceAssessmentSummary));
                OnPropertyChanged(nameof(SelectedColumnBraceAssessmentIsAutoResolvable));
            }
        }

        public void BeginColumnBraceAssessmentRefresh()
        {
            _columnBraceAssessmentCache.Clear();
            OnPropertyChanged(nameof(ColumnBraceAssessmentCache));
            OnPropertyChanged(nameof(SelectedColumnBraceAssessments));
            OnPropertyChanged(nameof(SelectedColumnBraceAssessmentSummary));
            OnPropertyChanged(nameof(SelectedColumnBraceAssessmentIsAutoResolvable));
        }

        private static ColumnBraceDerivedType OverrideTypeFromIndex(int index)
        {
            switch (index)
            {
                case 1: return ColumnBraceDerivedType.Interior;
                case 2: return ColumnBraceDerivedType.Edge;
                case 3: return ColumnBraceDerivedType.Corner;
                case 4: return ColumnBraceDerivedType.InternalOpeningWarning;
                case 5: return ColumnBraceDerivedType.FoundationGround;
                default: return ColumnBraceDerivedType.IsolatedWarning;
            }
        }

        private static int OverrideIndexFromType(ColumnBraceDerivedType type)
        {
            switch (type)
            {
                case ColumnBraceDerivedType.Interior: return 1;
                case ColumnBraceDerivedType.Edge: return 2;
                case ColumnBraceDerivedType.Corner: return 3;
                case ColumnBraceDerivedType.InternalOpeningWarning: return 4;
                case ColumnBraceDerivedType.FoundationGround: return 5;
                default: return 6;
            }
        }

        private string _revitVersionStatus;
        private List<KetQuaHienThi> _checkResults;
        public Action<string> NavigateAction { get; private set; }
        private Action _closeWindowAction;

        /// <summary>
        /// False is the default, responsive preview (native structure + formwork).
        /// True is requested only by the explicit "Dựng Family chi tiết" action.
        /// </summary>
        public bool UseDetailed3DPreview { get; private set; }
        private readonly IDialogService _dialogService;
        private readonly FormworkTakeoffWorkflowService _takeoffWorkflowService =
            new FormworkTakeoffWorkflowService();
        private string _takeoffWorkflowStatus = "Sẵn sàng thực hiện quy trình bóc tách khối lượng ván khuôn.";

        public string TakeoffWorkflowStatus
        {
            get => _takeoffWorkflowStatus;
            private set { _takeoffWorkflowStatus = value; OnPropertyChanged(); }
        }

        // Versioned, validated formwork/accessory design shared by Calculation,
        // Schedule and Preview. Structural Revit dimensions are not committed here.
        private CalculationParameters _validatedDesignParameters;
        private IReadOnlyList<string> _validatedDesignAdjustments = new List<string>();
        private int _validatedDesignRevision;
        private int _scheduledDesignRevision;
        private DateTime? _validatedDesignTimestamp;
        private bool _isApplyingValidatedDesign;
        private readonly Dictionary<string, ComponentFormworkDesign> _componentFormworkDesigns =
            new Dictionary<string, ComponentFormworkDesign>(StringComparer.OrdinalIgnoreCase);

        public bool HasValidatedDesign => _validatedDesignParameters != null;
        public int SelectedComponentCount => FloorElements?.Count ?? 0;
        public bool IsScheduleSynchronized => HasValidatedDesign &&
                                              _scheduledDesignRevision == _validatedDesignRevision;
        public string CurrentWorkflowTab { get; private set; } = "DuLieuView";
        public string WorkflowStatusText
        {
            get
            {
                if (SelectedComponentCount == 0)
                    return "Bước 1/5: Chọn cấu kiện Revit; hệ thống sẽ nhóm theo tầng, loại và tên cấu kiện.";
                if (!HasValidatedDesign)
                    return $"Đã nạp {SelectedComponentCount} cấu kiện. Tiếp theo: khai báo thông số và tính toán/thiết kế đạt chuẩn.";
                if (!IsScheduleSynchronized)
                    return $"Thiết kế đạt chuẩn phiên bản {_validatedDesignRevision} đã được lưu. Mở Thống kê để đồng bộ tiên lượng.";
                return $"Quy trình đã đồng bộ: {SelectedComponentCount} cấu kiện · thiết kế phiên bản {_validatedDesignRevision} · sẵn sàng thống kê hoặc diễn họa 3D.";
            }
        }
        public bool IsApplyingValidatedDesign => _isApplyingValidatedDesign;
        public int ValidatedDesignRevision => _validatedDesignRevision;
        public int ScheduledDesignRevision => _scheduledDesignRevision;
        public DateTime? ValidatedDesignTimestamp => _validatedDesignTimestamp;
        public IReadOnlyList<string> ValidatedDesignAdjustments => _validatedDesignAdjustments;
        public CalculationParameters ValidatedDesignParameters =>
            _validatedDesignParameters == null ? null : CloneCalculationParameters(_validatedDesignParameters);
        public IReadOnlyDictionary<string, ComponentFormworkDesign> ComponentFormworkDesigns =>
            _componentFormworkDesigns.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.OrdinalIgnoreCase);

        public ComponentFormworkDesign GetComponentFormworkDesign(string elementUniqueId)
        {
            if (string.IsNullOrWhiteSpace(elementUniqueId)) return null;
            return _componentFormworkDesigns.TryGetValue(elementUniqueId, out ComponentFormworkDesign design)
                ? design.Clone()
                : null;
        }

        public CalculationParameters GetEffectiveComponentParameters(FloorElementModel component,
            CalculationParameters fallback = null)
        {
            CalculationParameters parameters = GetComponentFormworkDesign(component?.UniqueId)?.Parameters ??
                                               fallback?.Clone() ??
                                               _validatedDesignParameters?.Clone() ??
                                               new CalculationParameters();
            ApplyStructuralGeometry(parameters, component);
            ApplyIntersectionContext(parameters, component?.IntersectionInfo ?? GetComponentIntersectionInfo(component?.UniqueId));
            return parameters;
        }

        public IReadOnlyDictionary<string, ComponentIntersectionInfo> ComponentIntersectionCache =>
            _componentIntersectionCache;

        public ComponentIntersectionInfo GetComponentIntersectionInfo(string uniqueId)
        {
            if (string.IsNullOrWhiteSpace(uniqueId)) return null;
            _componentIntersectionCache.TryGetValue(uniqueId, out ComponentIntersectionInfo info);
            return info;
        }

        public void RefreshComponentIntersections()
        {
            _componentIntersectionCache = ComponentIntersectionService.Analyze(RevitDoc, FloorElements);
            foreach (FloorElementModel component in FloorElements ?? new List<FloorElementModel>())
                component.IntersectionInfo = GetComponentIntersectionInfo(component.UniqueId);
            OnPropertyChanged(nameof(ComponentIntersectionCache));
        }

        private static void ApplyIntersectionContext(CalculationParameters parameters, ComponentIntersectionInfo info)
        {
            if (parameters == null || info == null) return;
            parameters.HasIntersectionContext = true;
            parameters.NetPlywoodAreaM2 = info.NetPlywoodAreaM2;
            parameters.NetBottomFormworkAreaM2 = info.NetBottomAreaM2;
            parameters.NetSideFormworkAreaM2 = info.NetSideAreaM2;
            parameters.NetTopFormworkAreaM2 = info.NetTopAreaM2;
            parameters.NetInternalOpeningSideAreaM2 = info.InternalOpeningSideAreaM2;
            parameters.NetExposedPerimeterM = info.NetExposedPerimeterM;
            parameters.NetFormworkLengthMm = info.NetFormworkLengthMm;
            parameters.IntersectingElementUniqueIds = info.IntersectingElementUniqueIds.ToArray();
            if (info.ComponentType == StructuralComponentType.Beam && info.HasBeamSideClassification)
                parameters.BeamPlacementModeIndex = info.IsPerimeterBeam ? 2 : 1;
        }

        public static void ApplyStructuralGeometry(CalculationParameters parameters, FloorElementModel component)
        {
            if (parameters == null || component == null) return;
            parameters.ComponentType = component.ComponentType;
            parameters.SelectedFloorDetailName = component.FamilyTypeName;
            switch (component.ComponentType)
            {
                case StructuralComponentType.Beam:
                    parameters.BeamWidth = Math.Max(1, component.Elevation);
                    parameters.BeamDepth = Math.Max(1, component.Thickness);
                    parameters.BeamLength = Math.Max(1, component.StoryHeight);
                    parameters.CayChongHeight = Math.Max(1, parameters.CayChongHeight);
                    break;
                case StructuralComponentType.Column:
                    parameters.ColumnWidth = Math.Max(1, component.Elevation);
                    parameters.ColumnDepth = Math.Max(1, component.Thickness);
                    parameters.ColumnHeight = Math.Max(1, component.StoryHeight);
                    break;
                case StructuralComponentType.Foundation:
                    parameters.SlabThickness = Math.Max(1, component.Thickness);
                    parameters.IsGroundLevel = true;
                    break;
                default:
                    parameters.SlabThickness = Math.Max(1, component.Thickness);
                    parameters.IsGroundLevel = false;
                    if (component.StoryHeight > 0)
                        parameters.CayChongHeight = Math.Max(1, component.StoryHeight - component.Thickness -
                            parameters.VanMatThickness - parameters.XaGoPhuHeight - parameters.XaGoChinhHeight);
                    break;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Sự kiện phát ra sau khi hoàn tất chọn sàn từ Revit.
        /// Tất cả các ViewModel con đều subscribe để đồng bộ dữ liệu.
        /// </summary>
        public event EventHandler<SlabSelectionEventArgs> SlabSelectionCompleted;

        /// <summary>
        /// Sự kiện phát ra khi người dùng yêu cầu dựng mô hình 3D từ trang Kết quả.
        /// Preview3DViewModel subscribe để bắt đầu render pipeline.
        /// </summary>
        public event EventHandler Render3DRequested;

        /// <summary>
        /// Phát sự kiện yêu cầu dựng 3D, đồng thời chuyển sang tab Preview3DView.
        /// </summary>
        public void RequestRender3D()
        {
            UseDetailed3DPreview = true;
            OnPropertyChanged(nameof(UseDetailed3DPreview));
            SynchronizeScheduleFromValidatedDesign();
            ExecuteChangeTab("Preview3DView");
            Render3DRequested?.Invoke(this, EventArgs.Empty);
        }

        public void RequestFastPreview3D()
        {
            UseDetailed3DPreview = false;
            OnPropertyChanged(nameof(UseDetailed3DPreview));
            ExecuteChangeTab("Preview3DView");
            Render3DRequested?.Invoke(this, EventArgs.Empty);
        }

        public void CommitValidatedDesign(CalculationParameters parameters, IEnumerable<string> adjustments)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            _isApplyingValidatedDesign = true;
            try
            {
                ApplyAccessoryDesign(parameters);
                _validatedDesignParameters = CloneCalculationParameters(parameters);
                _validatedDesignAdjustments = (adjustments ?? Enumerable.Empty<string>()).ToList().AsReadOnly();
                _validatedDesignTimestamp = DateTime.Now;
                _validatedDesignRevision++;
                PersistValidatedDesignForCurrentDocument();
            }
            finally
            {
                _isApplyingValidatedDesign = false;
            }
            OnPropertyChanged(nameof(HasValidatedDesign));
            OnPropertyChanged(nameof(ValidatedDesignParameters));
            OnPropertyChanged(nameof(ValidatedDesignAdjustments));
            OnPropertyChanged(nameof(ValidatedDesignTimestamp));
            OnPropertyChanged(nameof(ValidatedDesignRevision));
            NotifyWorkflowStateChanged();
            IsCalculated = true;
        }

        public void CommitComponentDesigns(IEnumerable<ComponentFormworkDesign> designs,
            CalculationParameters accessoryEnvelope, IEnumerable<string> adjustments)
        {
            if (designs == null) throw new ArgumentNullException(nameof(designs));
            List<ComponentFormworkDesign> committed = designs.Where(design => design != null).Select(design => design.Clone()).ToList();
            if (committed.Count == 0) throw new InvalidOperationException("Không có cấu kiện nào để lưu thiết kế.");
            if (committed.Any(design => !design.IsCompliant || design.Parameters == null))
                throw new InvalidOperationException("Không được lưu một thiết kế cấu kiện chưa đạt chuẩn.");

            _isApplyingValidatedDesign = true;
            try
            {
                foreach (ComponentFormworkDesign design in committed)
                    _componentFormworkDesigns[design.ElementUniqueId] = design;

                _validatedDesignParameters = CloneCalculationParameters(accessoryEnvelope ?? committed[0].Parameters);
                ApplyAccessoryDesign(_validatedDesignParameters);
                _validatedDesignAdjustments = (adjustments ?? committed.SelectMany(item => item.Adjustments))
                    .ToList().AsReadOnly();
                _validatedDesignTimestamp = DateTime.Now;
                _validatedDesignRevision++;
                PersistValidatedDesignForCurrentDocument();
            }
            finally
            {
                _isApplyingValidatedDesign = false;
            }

            OnPropertyChanged(nameof(HasValidatedDesign));
            OnPropertyChanged(nameof(ComponentFormworkDesigns));
            OnPropertyChanged(nameof(ValidatedDesignParameters));
            OnPropertyChanged(nameof(ValidatedDesignAdjustments));
            OnPropertyChanged(nameof(ValidatedDesignTimestamp));
            OnPropertyChanged(nameof(ValidatedDesignRevision));
            NotifyWorkflowStateChanged();
            IsCalculated = true;
        }

        public bool SynchronizeScheduleFromValidatedDesign()
        {
            if (_validatedDesignParameters == null) return false;
            if (_scheduledDesignRevision == _validatedDesignRevision) return true;

            _scheduledDesignRevision = _validatedDesignRevision;
            OnPropertyChanged(nameof(ScheduledDesignRevision));
            NotifyWorkflowStateChanged();
            return true;
        }

        private static CalculationParameters CloneCalculationParameters(CalculationParameters source)
        {
            var clone = new CalculationParameters();
            foreach (var property in typeof(CalculationParameters).GetProperties()
                         .Where(property => property.CanRead && property.CanWrite))
            {
                property.SetValue(clone, property.GetValue(source, null), null);
            }
            return clone;
        }

        private static string GetDocumentDesignKey(Document document)
        {
            if (document == null) return null;
            string path = document.PathName;
            if (!string.IsNullOrWhiteSpace(path))
                return "PATH|" + path.Trim().ToUpperInvariant();
            return $"UNSAVED|{document.Title}|{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(document)}";
        }

        private void PersistValidatedDesignForCurrentDocument()
        {
            string key = GetDocumentDesignKey(RevitDoc);
            if (key == null || _validatedDesignParameters == null) return;

            lock (ValidatedDesignSessionLock)
            {
                ValidatedDesignSessionCache[key] = new PersistedValidatedDesign
                {
                    Parameters = CloneCalculationParameters(_validatedDesignParameters),
                    Adjustments = _validatedDesignAdjustments.ToList().AsReadOnly(),
                    ComponentDesigns = _componentFormworkDesigns.ToDictionary(
                        pair => pair.Key, pair => pair.Value.Clone(), StringComparer.OrdinalIgnoreCase),
                    Revision = _validatedDesignRevision,
                    Timestamp = _validatedDesignTimestamp ?? DateTime.Now
                };
            }
        }

        private void RestoreValidatedDesignForCurrentDocument()
        {
            if (_validatedDesignParameters != null) return;
            string key = GetDocumentDesignKey(RevitDoc);
            if (key == null) return;

            PersistedValidatedDesign persisted;
            lock (ValidatedDesignSessionLock)
            {
                if (!ValidatedDesignSessionCache.TryGetValue(key, out persisted)) return;
            }

            _isApplyingValidatedDesign = true;
            try
            {
                _validatedDesignParameters = CloneCalculationParameters(persisted.Parameters);
                _validatedDesignAdjustments = persisted.Adjustments.ToList().AsReadOnly();
                _validatedDesignRevision = persisted.Revision;
                _scheduledDesignRevision = 0;
                _validatedDesignTimestamp = persisted.Timestamp;
                _componentFormworkDesigns.Clear();
                foreach (KeyValuePair<string, ComponentFormworkDesign> pair in
                         persisted.ComponentDesigns ?? new Dictionary<string, ComponentFormworkDesign>())
                    _componentFormworkDesigns[pair.Key] = pair.Value.Clone();
                ApplyAccessoryDesign(_validatedDesignParameters);
                IsCalculated = true;
            }
            finally
            {
                _isApplyingValidatedDesign = false;
            }
            NotifyWorkflowStateChanged();
        }

        private void ApplyAccessoryDesign(CalculationParameters p)
        {
            VanMatThickness = p.VanMatThickness;
            XaGoPhuWidth = p.XaGoPhuWidth;
            XaGoPhuHeight = p.XaGoPhuHeight;
            XaGoChinhWidth = p.XaGoChinhWidth;
            XaGoChinhHeight = p.XaGoChinhHeight;
            Joist1Spacing = p.Joist1Spacing;
            Joist2Spacing = p.Joist2Spacing;
            SupportSpacing = p.SupportSpacing;
            CayChongDiameter = p.CayChongDiameter;
            CayChongThickness = p.CayChongThickness;
            CayChongHeight = p.CayChongHeight;
            SupportEffectiveLengthFactor = p.SupportEffectiveLengthFactor;
            SupportRatedCapacity = p.SupportRatedCapacity;
            SupportSlendernessLimit = p.SupportSlendernessLimit;
            SupportBasePlateWidth = p.SupportBasePlateWidth;
            SupportBasePlateDepth = p.SupportBasePlateDepth;
            SupportBaseBearingAllowable = p.SupportBaseBearingAllowable;
            SupportSolePlateWidth = p.SupportSolePlateWidth;
            SupportSolePlateDepth = p.SupportSolePlateDepth;
            SupportGroundBearingAllowable = p.SupportGroundBearingAllowable;
            SupportHeadPlateWidth = p.SupportHeadPlateWidth;
            SupportHeadPlateDepth = p.SupportHeadPlateDepth;
            SupportHeadBearingAllowable = p.SupportHeadBearingAllowable;
            SupportJackExtension = p.SupportJackExtension;
            SupportJackExtensionLimit = p.SupportJackExtensionLimit;
            SoNhipVanMat = p.SoNhipVanMat;
            SoNhipXaGoPhu = p.SoNhipXaGoPhu;
            SoNhipXaGoChinh = p.SoNhipXaGoChinh;

            BeamSidePlywoodThickness = p.BeamSidePlywoodThickness;
            BeamBottomPlywoodThickness = p.BeamBottomPlywoodThickness;
            BeamSideYokeSpacing = p.BeamSideYokeSpacing;
            BeamBottomYokeSpacing = p.BeamBottomYokeSpacing;
            BeamSecondaryJoistWidth = p.BeamSecondaryJoistWidth;
            BeamSecondaryJoistDepth = p.BeamSecondaryJoistDepth;
            BeamPrimaryWalerWidth = p.BeamPrimaryWalerWidth;
            BeamPrimaryWalerDepth = p.BeamPrimaryWalerDepth;
            BeamPrimaryWalerSpacing = p.BeamPrimaryWalerSpacing;
            BeamScaffoldBayWidth = p.BeamScaffoldBayWidth;
            BeamTieRodDiameter = p.BeamTieRodDiameter;
            BeamTieRodSpacing = p.BeamTieRodSpacing;
            BeamTieRodAllowableCapacity = p.BeamTieRodAllowableCapacity;
            BeamSideBraceSpacing = p.BeamSideBraceSpacing;
            BeamSideBraceAngle = p.BeamSideBraceAngle;
            BeamSideBraceDiameter = p.BeamSideBraceDiameter;
            BeamSideBraceThickness = p.BeamSideBraceThickness;
            BeamSideBraceCountPerStation = p.BeamSideBraceCountPerStation;
            BeamSideBraceAllowableCapacity = p.BeamSideBraceAllowableCapacity;
            BeamPlacementModeIndex = p.BeamPlacementModeIndex;
            BeamStrippingStrengthPercent = p.BeamStrippingStrengthPercent;
            BeamVibratorOverlap = p.BeamVibratorOverlap;

            ColumnPlywoodThickness = p.ColumnPlywoodThickness;
            ColumnStiffenerWidth = p.ColumnStiffenerWidth;
            ColumnStiffenerDepth = p.ColumnStiffenerDepth;
            ColumnStiffenerSpacing = p.ColumnStiffenerSpacing;
            ColumnYokeSpacing = p.ColumnYokeSpacing;
            ColumnUseZonedYokeSpacing = p.ColumnUseZonedYokeSpacing;
            ColumnFirstYokeOffset = p.ColumnFirstYokeOffset;
            ColumnYokeBottomSpacing = p.ColumnYokeBottomSpacing;
            ColumnYokeUpperSpacing = p.ColumnYokeUpperSpacing;
            ColumnYokeTransitionHeight = p.ColumnYokeTransitionHeight;
            ColumnLastYokeOffset = p.ColumnLastYokeOffset;
            ColumnYokeWidth = p.ColumnYokeWidth;
            ColumnYokeHeight = p.ColumnYokeHeight;
            ColumnYokeThickness = p.ColumnYokeThickness;
            ColumnYokePairCount = p.ColumnYokePairCount;
            ColumnYokeMaterialIndex = p.ColumnYokeMaterialIndex;
            ColumnTieRodDiameter = p.ColumnTieRodDiameter;
            ColumnTieRodCountPerFace = p.ColumnTieRodCountPerFace;
            ColumnTieRodAllowableCapacity = p.ColumnTieRodAllowableCapacity;
            ColumnBraceCount = p.ColumnBraceCount;
            ColumnBraceAngle = p.ColumnBraceAngle;
            ColumnBraceAllowableCapacity = p.ColumnBraceAllowableCapacity;
            ColumnBracePlacementModeIndex = p.ColumnBracePlacementModeIndex;
            ColumnNeighbourTieThreshold = p.ColumnNeighbourTieThreshold;
            ColumnIsBoundaryOrCorner = p.ColumnIsBoundaryOrCorner;
            ColumnCableAllowableCapacity = p.ColumnCableAllowableCapacity;
            ColumnTurnbuckleAllowableCapacity = p.ColumnTurnbuckleAllowableCapacity;
            ColumnAnchorAllowableCapacity = p.ColumnAnchorAllowableCapacity;
            ColumnAxisOffset = p.ColumnAxisOffset;
            ColumnPlumbTargetTolerance = p.ColumnPlumbTargetTolerance;
            ColumnFormworkTopExtension = p.ColumnFormworkTopExtension;
            ColumnHoseBottomClearance = p.ColumnHoseBottomClearance;
            ColumnPourLiftHeight = p.ColumnPourLiftHeight;
            ColumnBeamStopOffset = p.ColumnBeamStopOffset;
            ColumnStripTimeHours = p.ColumnStripTimeHours;
            ColumnCuringDays = p.ColumnCuringDays;
            ColumnUseWorkPlatform = p.ColumnUseWorkPlatform;
            ColumnWorkPlatformWidth = p.ColumnWorkPlatformWidth;
            ColumnGuardrailHeight = p.ColumnGuardrailHeight;
            ConcretePourRate = p.ConcretePourRate;
            ConcreteTemperature = p.ConcreteTemperature;
        }

        public Document RevitDoc { get; private set; }
        public UIDocument RevitUIDoc { get; private set; }

        private StructuralComponentType _currentComponentType = StructuralComponentType.Slab;
        public StructuralComponentType CurrentComponentType { get => _currentComponentType; set { _currentComponentType = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedComponentIndex)); } }

        public int SelectedSlabCount { get; private set; }
        public int SelectedFoundationCount { get; private set; }
        public int SelectedBeamCount { get; private set; }
        public int SelectedColumnCount { get; private set; }
        public bool HasMixedComponentSelection =>
            (SelectedSlabCount > 0 ? 1 : 0) + (SelectedFoundationCount > 0 ? 1 : 0) +
            (SelectedBeamCount > 0 ? 1 : 0) + (SelectedColumnCount > 0 ? 1 : 0) > 1;
        public string SelectedComponentComposition =>
            $"Sàn {SelectedSlabCount} · móng {SelectedFoundationCount} · dầm {SelectedBeamCount} · cột {SelectedColumnCount}";

        public void SetSelectedComponentComposition(int slabs, int foundations, int beams, int columns)
        {
            SelectedSlabCount = Math.Max(0, slabs);
            SelectedFoundationCount = Math.Max(0, foundations);
            SelectedBeamCount = Math.Max(0, beams);
            SelectedColumnCount = Math.Max(0, columns);
            OnPropertyChanged(nameof(SelectedSlabCount));
            OnPropertyChanged(nameof(SelectedFoundationCount));
            OnPropertyChanged(nameof(SelectedBeamCount));
            OnPropertyChanged(nameof(SelectedColumnCount));
            OnPropertyChanged(nameof(HasMixedComponentSelection));
            OnPropertyChanged(nameof(SelectedComponentComposition));
        }

        public int SelectedComponentIndex
        {
            get => (int)CurrentComponentType;
            set { CurrentComponentType = (StructuralComponentType)value; }
        }

        // Dữ liệu dùng chung
        private double _woodStrength = 15.0;
        public double WoodStrength { get => _woodStrength; set { _woodStrength = value; OnPropertyChanged(); } }

        private double _plywoodBendingStrength = 10.0;
        public double PlywoodBendingStrength { get => _plywoodBendingStrength; set { _plywoodBendingStrength = value; OnPropertyChanged(); } }

        private double _woodShearStrength = 2.0;
        public double WoodShearStrength { get => _woodShearStrength; set { _woodShearStrength = value; OnPropertyChanged(); } }

        private double _elasticModulus = 9000.0;
        public double ElasticModulus { get => _elasticModulus; set { _elasticModulus = value; OnPropertyChanged(); } }

        private string _slabDeflectionLimit = "1/250";
        public string SlabDeflectionLimit { get => _slabDeflectionLimit; set { _slabDeflectionLimit = value; OnPropertyChanged(); } }

        private string _joistDeflectionLimit = "1/250";
        public string JoistDeflectionLimit { get => _joistDeflectionLimit; set { _joistDeflectionLimit = value; OnPropertyChanged(); } }

        private double _woodFormworkWeight = 0.30;
        public double WoodFormworkWeight { get => _woodFormworkWeight; set { _woodFormworkWeight = value; OnPropertyChanged(); } }

        private double _concreteWeight = 24.00;
        public double ConcreteWeight { get => _concreteWeight; set { _concreteWeight = value; OnPropertyChanged(); } }

        private double _steelWeight = 1.00;
        public double SteelWeight { get => _steelWeight; set { _steelWeight = value; OnPropertyChanged(); } }

        private double _constructionLiveLoad = 2.50;
        public double ConstructionLiveLoad { get => _constructionLiveLoad; set { _constructionLiveLoad = value; OnPropertyChanged(); } }

        private double _pouringImpactLoad = 2.00;
        public double PouringImpactLoad { get => _pouringImpactLoad; set { _pouringImpactLoad = value; OnPropertyChanged(); } }

        private double _vibrationLoad = 2.00;
        public double VibrationLoad { get => _vibrationLoad; set { _vibrationLoad = value; OnPropertyChanged(); } }

        private double _dynamicImpactFactor = 1.30;
        public double DynamicImpactFactor { get => _dynamicImpactFactor; set { _dynamicImpactFactor = value; OnPropertyChanged(); } }

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

        private double _cayChongHeight = 3600.0;
        public double CayChongHeight { get => _cayChongHeight; set { _cayChongHeight = value; OnPropertyChanged(); } }

        private double _supportEffectiveLengthFactor = 1.0;
        public double SupportEffectiveLengthFactor { get => _supportEffectiveLengthFactor; set { _supportEffectiveLengthFactor = value; OnPropertyChanged(); } }
        private double _supportRatedCapacity = 25.0;
        public double SupportRatedCapacity { get => _supportRatedCapacity; set { _supportRatedCapacity = value; OnPropertyChanged(); } }
        private double _supportSlendernessLimit = 200.0;
        public double SupportSlendernessLimit { get => _supportSlendernessLimit; set { _supportSlendernessLimit = value; OnPropertyChanged(); } }
        private double _supportBasePlateWidth = 150.0;
        public double SupportBasePlateWidth { get => _supportBasePlateWidth; set { _supportBasePlateWidth = value; OnPropertyChanged(); } }
        private double _supportBasePlateDepth = 150.0;
        public double SupportBasePlateDepth { get => _supportBasePlateDepth; set { _supportBasePlateDepth = value; OnPropertyChanged(); } }
        private double _supportBaseBearingAllowable = 10.0;
        public double SupportBaseBearingAllowable { get => _supportBaseBearingAllowable; set { _supportBaseBearingAllowable = value; OnPropertyChanged(); } }
        private double _supportSolePlateWidth = 500.0;
        public double SupportSolePlateWidth { get => _supportSolePlateWidth; set { _supportSolePlateWidth = value; OnPropertyChanged(); } }
        private double _supportSolePlateDepth = 500.0;
        public double SupportSolePlateDepth { get => _supportSolePlateDepth; set { _supportSolePlateDepth = value; OnPropertyChanged(); } }
        private double _supportGroundBearingAllowable = 0.15;
        public double SupportGroundBearingAllowable { get => _supportGroundBearingAllowable; set { _supportGroundBearingAllowable = value; OnPropertyChanged(); } }
        private double _supportHeadPlateWidth = 150.0;
        public double SupportHeadPlateWidth { get => _supportHeadPlateWidth; set { _supportHeadPlateWidth = value; OnPropertyChanged(); } }
        private double _supportHeadPlateDepth = 150.0;
        public double SupportHeadPlateDepth { get => _supportHeadPlateDepth; set { _supportHeadPlateDepth = value; OnPropertyChanged(); } }
        private double _supportHeadBearingAllowable = 8.0;
        public double SupportHeadBearingAllowable { get => _supportHeadBearingAllowable; set { _supportHeadBearingAllowable = value; OnPropertyChanged(); } }
        private double _supportJackExtension = 300.0;
        public double SupportJackExtension { get => _supportJackExtension; set { _supportJackExtension = value; OnPropertyChanged(); } }
        private double _supportJackExtensionLimit = 600.0;
        public double SupportJackExtensionLimit { get => _supportJackExtensionLimit; set { _supportJackExtensionLimit = value; OnPropertyChanged(); } }

        // Beam Properties
        private double _beamWidth = 200.0;
        public double BeamWidth { get => _beamWidth; set { _beamWidth = value; OnPropertyChanged(); } }

        private double _beamDepth = 400.0;
        public double BeamDepth { get => _beamDepth; set { _beamDepth = value; OnPropertyChanged(); } }
        
        private double _beamLength = 4000.0;
        public double BeamLength { get => _beamLength; set { _beamLength = value; OnPropertyChanged(); } }

        private double _beamSidePlywoodThickness = 18.0;
        public double BeamSidePlywoodThickness { get => _beamSidePlywoodThickness; set { _beamSidePlywoodThickness = value; OnPropertyChanged(); } }

        private double _beamBottomPlywoodThickness = 18.0;
        public double BeamBottomPlywoodThickness { get => _beamBottomPlywoodThickness; set { _beamBottomPlywoodThickness = value; OnPropertyChanged(); } }

        private double _beamSideYokeSpacing = 400.0;
        public double BeamSideYokeSpacing { get => _beamSideYokeSpacing; set { _beamSideYokeSpacing = value; OnPropertyChanged(); } }

        private double _beamBottomYokeSpacing = 200.0;
        public double BeamBottomYokeSpacing { get => _beamBottomYokeSpacing; set { _beamBottomYokeSpacing = value; OnPropertyChanged(); } }

        private double _beamPropSpacing = 1200.0;
        public double BeamPropSpacing
        {
            get => _beamPropSpacing;
            set
            {
                _beamPropSpacing = value;
                _beamPrimaryWalerSpacing = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BeamPrimaryWalerSpacing));
            }
        }

        private double _beamSecondaryJoistWidth = 50.0;
        public double BeamSecondaryJoistWidth { get => _beamSecondaryJoistWidth; set { _beamSecondaryJoistWidth = value; OnPropertyChanged(); } }
        private double _beamSecondaryJoistDepth = 50.0;
        public double BeamSecondaryJoistDepth { get => _beamSecondaryJoistDepth; set { _beamSecondaryJoistDepth = value; OnPropertyChanged(); } }
        private double _beamPrimaryWalerWidth = 50.0;
        public double BeamPrimaryWalerWidth { get => _beamPrimaryWalerWidth; set { _beamPrimaryWalerWidth = value; OnPropertyChanged(); } }
        private double _beamPrimaryWalerDepth = 100.0;
        public double BeamPrimaryWalerDepth { get => _beamPrimaryWalerDepth; set { _beamPrimaryWalerDepth = value; OnPropertyChanged(); } }
        private double _beamPrimaryWalerSpacing = 1200.0;
        public double BeamPrimaryWalerSpacing
        {
            get => _beamPrimaryWalerSpacing;
            set
            {
                _beamPrimaryWalerSpacing = value;
                _beamPropSpacing = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BeamPropSpacing));
            }
        }
        private double _beamScaffoldBayWidth = 1200.0;
        public double BeamScaffoldBayWidth { get => _beamScaffoldBayWidth; set { _beamScaffoldBayWidth = value; OnPropertyChanged(); } }
        private double _beamTieRodDiameter = 16.0;
        public double BeamTieRodDiameter { get => _beamTieRodDiameter; set { _beamTieRodDiameter = value; OnPropertyChanged(); } }
        private double _beamTieRodSpacing = 1200.0;
        public double BeamTieRodSpacing { get => _beamTieRodSpacing; set { _beamTieRodSpacing = value; OnPropertyChanged(); } }
        private double _beamTieRodAllowableCapacity = 20.0;
        public double BeamTieRodAllowableCapacity { get => _beamTieRodAllowableCapacity; set { _beamTieRodAllowableCapacity = value; OnPropertyChanged(); } }
        private double _beamSideBraceSpacing = 1200.0;
        public double BeamSideBraceSpacing { get => _beamSideBraceSpacing; set { _beamSideBraceSpacing = value; OnPropertyChanged(); } }
        private double _beamSideBraceAngle = 55.0;
        public double BeamSideBraceAngle { get => _beamSideBraceAngle; set { _beamSideBraceAngle = value; OnPropertyChanged(); } }
        private double _beamSideBraceDiameter = 48.0;
        public double BeamSideBraceDiameter { get => _beamSideBraceDiameter; set { _beamSideBraceDiameter = value; OnPropertyChanged(); } }
        private double _beamSideBraceThickness = 2.0;
        public double BeamSideBraceThickness { get => _beamSideBraceThickness; set { _beamSideBraceThickness = value; OnPropertyChanged(); } }
        private int _beamSideBraceCountPerStation = 1;
        public int BeamSideBraceCountPerStation { get => _beamSideBraceCountPerStation; set { _beamSideBraceCountPerStation = value; OnPropertyChanged(); } }
        private double _beamSideBraceAllowableCapacity = 15.0;
        public double BeamSideBraceAllowableCapacity { get => _beamSideBraceAllowableCapacity; set { _beamSideBraceAllowableCapacity = value; OnPropertyChanged(); } }
        private int _beamPlacementModeIndex;
        public int BeamPlacementModeIndex { get => _beamPlacementModeIndex; set { _beamPlacementModeIndex = value; OnPropertyChanged(); } }
        private double _beamStrippingStrengthPercent = 70.0;
        public double BeamStrippingStrengthPercent { get => _beamStrippingStrengthPercent; set { _beamStrippingStrengthPercent = value; OnPropertyChanged(); } }
        private double _beamVibratorOverlap = 75.0;
        public double BeamVibratorOverlap { get => _beamVibratorOverlap; set { _beamVibratorOverlap = value; OnPropertyChanged(); } }

        // Column Properties
        private double _columnWidth = 400.0;
        public double ColumnWidth { get => _columnWidth; set { _columnWidth = value; OnPropertyChanged(); } }

        private double _columnDepth = 400.0;
        public double ColumnDepth { get => _columnDepth; set { _columnDepth = value; OnPropertyChanged(); } }

        private double _columnHeight = 3600.0;
        public double ColumnHeight { get => _columnHeight; set { _columnHeight = value; OnPropertyChanged(); } }

        private double _columnPlywoodThickness = 18.0;
        public double ColumnPlywoodThickness { get => _columnPlywoodThickness; set { _columnPlywoodThickness = value; OnPropertyChanged(); } }

        private double _columnStiffenerWidth = 40.0;
        public double ColumnStiffenerWidth { get => _columnStiffenerWidth; set { _columnStiffenerWidth = value; OnPropertyChanged(); } }

        private double _columnStiffenerDepth = 40.0;
        public double ColumnStiffenerDepth { get => _columnStiffenerDepth; set { _columnStiffenerDepth = value; OnPropertyChanged(); } }

        private double _columnStiffenerSpacing = 200.0;
        public double ColumnStiffenerSpacing { get => _columnStiffenerSpacing; set { _columnStiffenerSpacing = value; OnPropertyChanged(); } }

        private double _columnYokeSpacing = 400.0;
        public double ColumnYokeSpacing { get => _columnYokeSpacing; set { _columnYokeSpacing = FormworkCalculationService.ClampColumnYokeSpacing(value); OnPropertyChanged(); } }

        private bool _columnUseZonedYokeSpacing = true;
        public bool ColumnUseZonedYokeSpacing { get => _columnUseZonedYokeSpacing; set { _columnUseZonedYokeSpacing = value; OnPropertyChanged(); } }

        private double _columnFirstYokeOffset = 150.0;
        public double ColumnFirstYokeOffset { get => _columnFirstYokeOffset; set { _columnFirstYokeOffset = value; OnPropertyChanged(); } }

        private double _columnYokeBottomSpacing = 300.0;
        public double ColumnYokeBottomSpacing { get => _columnYokeBottomSpacing; set { _columnYokeBottomSpacing = FormworkCalculationService.ClampColumnYokeSpacing(value); OnPropertyChanged(); } }

        private double _columnYokeUpperSpacing = 500.0;
        public double ColumnYokeUpperSpacing { get => _columnYokeUpperSpacing; set { _columnYokeUpperSpacing = FormworkCalculationService.ClampColumnUpperYokeSpacing(value); OnPropertyChanged(); } }

        private double _columnYokeTransitionHeight = 1000.0;
        public double ColumnYokeTransitionHeight { get => _columnYokeTransitionHeight; set { _columnYokeTransitionHeight = value; OnPropertyChanged(); } }

        private double _columnLastYokeOffset = 150.0;
        public double ColumnLastYokeOffset { get => _columnLastYokeOffset; set { _columnLastYokeOffset = value; OnPropertyChanged(); } }

        private double _columnYokeWidth = 40.0;
        public double ColumnYokeWidth { get => _columnYokeWidth; set { _columnYokeWidth = value; OnPropertyChanged(); } }

        private double _columnYokeHeight = 80.0;
        public double ColumnYokeHeight { get => _columnYokeHeight; set { _columnYokeHeight = value; OnPropertyChanged(); } }

        private double _columnYokeThickness = 2.0;
        public double ColumnYokeThickness { get => _columnYokeThickness; set { _columnYokeThickness = value; OnPropertyChanged(); } }

        private int _columnYokePairCount = 2;
        public int ColumnYokePairCount { get => _columnYokePairCount; set { _columnYokePairCount = value; OnPropertyChanged(); } }

        private int _columnYokeMaterialIndex = 1;
        public int ColumnYokeMaterialIndex { get => _columnYokeMaterialIndex; set { _columnYokeMaterialIndex = value; OnPropertyChanged(); } }

        private double _columnTieRodDiameter = 12.0;
        public double ColumnTieRodDiameter { get => _columnTieRodDiameter; set { _columnTieRodDiameter = value; OnPropertyChanged(); } }

        private int _columnTieRodCountPerFace = 2;
        public int ColumnTieRodCountPerFace { get => _columnTieRodCountPerFace; set { _columnTieRodCountPerFace = value; OnPropertyChanged(); } }

        private double _columnTieRodAllowableCapacity = 12.0;
        public double ColumnTieRodAllowableCapacity { get => _columnTieRodAllowableCapacity; set { _columnTieRodAllowableCapacity = value; OnPropertyChanged(); } }

        private int _columnBraceCount = 4;
        public int ColumnBraceCount { get => _columnBraceCount; set { _columnBraceCount = value; OnPropertyChanged(); } }

        private double _columnBraceAngle = 55.0;
        public double ColumnBraceAngle
        {
            get => _columnBraceAngle;
            set
            {
                if (Math.Abs(_columnBraceAngle - value) < 0.001) return;
                _columnBraceAngle = value;
                BeginColumnBraceAssessmentRefresh();
                OnPropertyChanged();
            }
        }

        private double _columnBraceAllowableCapacity = 15.0;
        public double ColumnBraceAllowableCapacity { get => _columnBraceAllowableCapacity; set { _columnBraceAllowableCapacity = value; OnPropertyChanged(); } }

        private int _columnBracePlacementModeIndex = 2;
        public int ColumnBracePlacementModeIndex { get => _columnBracePlacementModeIndex; set { _columnBracePlacementModeIndex = value; OnPropertyChanged(); } }

        private double _columnNeighbourTieThreshold = 5000.0;
        public double ColumnNeighbourTieThreshold { get => _columnNeighbourTieThreshold; set { _columnNeighbourTieThreshold = value; OnPropertyChanged(); } }

        private bool _columnIsBoundaryOrCorner;
        public bool ColumnIsBoundaryOrCorner { get => _columnIsBoundaryOrCorner; set { _columnIsBoundaryOrCorner = value; OnPropertyChanged(); } }

        private double _columnCableAllowableCapacity = 10.0;
        public double ColumnCableAllowableCapacity { get => _columnCableAllowableCapacity; set { _columnCableAllowableCapacity = value; OnPropertyChanged(); } }

        private double _columnTurnbuckleAllowableCapacity = 10.0;
        public double ColumnTurnbuckleAllowableCapacity { get => _columnTurnbuckleAllowableCapacity; set { _columnTurnbuckleAllowableCapacity = value; OnPropertyChanged(); } }

        private double _columnAnchorAllowableCapacity = 12.0;
        public double ColumnAnchorAllowableCapacity { get => _columnAnchorAllowableCapacity; set { _columnAnchorAllowableCapacity = value; OnPropertyChanged(); } }

        private double _columnAxisOffset = 1000.0;
        public double ColumnAxisOffset { get => _columnAxisOffset; set { _columnAxisOffset = value; OnPropertyChanged(); } }

        private double _columnPlumbTargetTolerance = 3.0;
        public double ColumnPlumbTargetTolerance { get => _columnPlumbTargetTolerance; set { _columnPlumbTargetTolerance = value; OnPropertyChanged(); } }

        private double _columnFormworkTopExtension = 100.0;
        public double ColumnFormworkTopExtension { get => _columnFormworkTopExtension; set { _columnFormworkTopExtension = value; OnPropertyChanged(); } }

        private double _columnHoseBottomClearance = 1500.0;
        public double ColumnHoseBottomClearance { get => _columnHoseBottomClearance; set { _columnHoseBottomClearance = value; OnPropertyChanged(); } }

        private double _columnPourLiftHeight = 2500.0;
        public double ColumnPourLiftHeight { get => _columnPourLiftHeight; set { _columnPourLiftHeight = value; OnPropertyChanged(); } }

        private double _columnBeamStopOffset = 50.0;
        public double ColumnBeamStopOffset { get => _columnBeamStopOffset; set { _columnBeamStopOffset = value; OnPropertyChanged(); } }

        private double _columnStripTimeHours = 36.0;
        public double ColumnStripTimeHours { get => _columnStripTimeHours; set { _columnStripTimeHours = value; OnPropertyChanged(); } }

        private double _columnCuringDays = 2.0;
        public double ColumnCuringDays { get => _columnCuringDays; set { _columnCuringDays = value; OnPropertyChanged(); } }

        private bool _columnUseWorkPlatform = true;
        public bool ColumnUseWorkPlatform { get => _columnUseWorkPlatform; set { _columnUseWorkPlatform = value; OnPropertyChanged(); } }

        private double _columnWorkPlatformWidth = 900.0;
        public double ColumnWorkPlatformWidth { get => _columnWorkPlatformWidth; set { _columnWorkPlatformWidth = value; OnPropertyChanged(); } }

        private double _columnGuardrailHeight = 1100.0;
        public double ColumnGuardrailHeight { get => _columnGuardrailHeight; set { _columnGuardrailHeight = value; OnPropertyChanged(); } }

        private double _concretePourRate = 2.0;
        public double ConcretePourRate { get => _concretePourRate; set { _concretePourRate = value; OnPropertyChanged(); } }

        private double _concreteTemperature = 30.0;
        public double ConcreteTemperature { get => _concreteTemperature; set { _concreteTemperature = value; OnPropertyChanged(); } }

        private double _currentStoryHeight = 0.0;
        public double CurrentStoryHeight { get => _currentStoryHeight; set { _currentStoryHeight = value; OnPropertyChanged(); } }

        private int _soNhipVanMat = 2;
        public int SoNhipVanMat { get => _soNhipVanMat; set { _soNhipVanMat = value; OnPropertyChanged(); } }

        private int _soNhipXaGoPhu = 3;
        public int SoNhipXaGoPhu { get => _soNhipXaGoPhu; set { _soNhipXaGoPhu = value; OnPropertyChanged(); } }

        private int _soNhipXaGoChinh = 3;
        public int SoNhipXaGoChinh { get => _soNhipXaGoChinh; set { _soNhipXaGoChinh = value; OnPropertyChanged(); } }

        private int _selectedTieuChuan = 0;
        public int SelectedTieuChuan { get => _selectedTieuChuan; set { _selectedTieuChuan = value; OnPropertyChanged(); } }

        private bool _isSelectingSlab = false;
        public bool IsSelectingSlab { get => _isSelectingSlab; set { _isSelectingSlab = value; OnPropertyChanged(); } }
        
        private bool _isSweepSelectingSlab = false;
        public bool IsSweepSelectingSlab { get => _isSweepSelectingSlab; set { _isSweepSelectingSlab = value; OnPropertyChanged(); } }

        private bool _isSelectingBeam = false;
        public bool IsSelectingBeam { get => _isSelectingBeam; set { _isSelectingBeam = value; OnPropertyChanged(); } }
        
        private bool _isSweepSelectingBeam = false;
        public bool IsSweepSelectingBeam { get => _isSweepSelectingBeam; set { _isSweepSelectingBeam = value; OnPropertyChanged(); } }

        private bool _isSelectingColumn = false;
        public bool IsSelectingColumn { get => _isSelectingColumn; set { _isSelectingColumn = value; OnPropertyChanged(); } }
        
        private bool _isSweepSelectingColumn = false;
        public bool IsSweepSelectingColumn { get => _isSweepSelectingColumn; set { _isSweepSelectingColumn = value; OnPropertyChanged(); } }

        private bool _isSelectingFoundation = false;
        public bool IsSelectingFoundation { get => _isSelectingFoundation; set { _isSelectingFoundation = value; OnPropertyChanged(); } }

        private bool _isSweepSelectingFoundation = false;
        public bool IsSweepSelectingFoundation { get => _isSweepSelectingFoundation; set { _isSweepSelectingFoundation = value; OnPropertyChanged(); } }

        // Filter Checkboxes for multi-component selection
        private bool _isFilterBeamChecked = true;
        public bool IsFilterBeamChecked { get => _isFilterBeamChecked; set { _isFilterBeamChecked = value; OnPropertyChanged(); } }

        private bool _isFilterSlabChecked = true;
        public bool IsFilterSlabChecked { get => _isFilterSlabChecked; set { _isFilterSlabChecked = value; OnPropertyChanged(); } }

        private bool _isFilterColumnChecked = true;
        public bool IsFilterColumnChecked { get => _isFilterColumnChecked; set { _isFilterColumnChecked = value; OnPropertyChanged(); } }

        private bool _isFilterFoundationChecked = true;
        public bool IsFilterFoundationChecked { get => _isFilterFoundationChecked; set { _isFilterFoundationChecked = value; OnPropertyChanged(); } }

        private bool _isSelectingComponent = false;
        public bool IsSelectingComponent { get => _isSelectingComponent; set { _isSelectingComponent = value; OnPropertyChanged(); } }

        private bool _isSweepSelectingComponent = false;
        public bool IsSweepSelectingComponent { get => _isSweepSelectingComponent; set { _isSweepSelectingComponent = value; OnPropertyChanged(); } }

        private bool _isGroundLevel = false;
        public bool IsGroundLevel { get => _isGroundLevel; set { _isGroundLevel = value; OnPropertyChanged(); } }

        private LevelModel _designatedGroundLevel;
        public LevelModel DesignatedGroundLevel { get => _designatedGroundLevel; set { _designatedGroundLevel = value; OnPropertyChanged(); UpdateGroundLevelState(); } }

        private List<LevelModel> _availableLevels = new List<LevelModel>();
        public List<LevelModel> AvailableLevels { get => _availableLevels; set { _availableLevels = value; OnPropertyChanged(); } }

        private void UpdateGroundLevelState()
        {
            if (FloorElements != null && FloorElements.Count > 0 && DesignatedGroundLevel != null)
            {
                IsGroundLevel = FloorElements.All(f => f.Elevation <= DesignatedGroundLevel.Elevation + 0.01);
                IsCalculated = true; // Trigger recalculation
            }
        }

        /// <summary>
        /// Stores the latest selection payload so newly constructed ViewModels can instantly sync state.
        /// </summary>
        public SlabSelectionEventArgs LatestSelectionContext { get; private set; }
        public List<Tuple<string, double>> SelectedSlabInfos { get; set; } = new List<Tuple<string, double>>();
        
        private List<FloorElementModel> _floorElements;
        public List<FloorElementModel> FloorElements
        {
            get => _floorElements;
            set
            {
                _floorElements = value ?? new List<FloorElementModel>();
                RefreshComponentIntersections();
                OnPropertyChanged();
                NotifyWorkflowStateChanged();
            }
        }

        private string _selectedFloorDetailName;
        public string SelectedFloorDetailName { get => _selectedFloorDetailName; set { _selectedFloorDetailName = value; OnPropertyChanged(); } }
        
        public string StartupTab { get; set; }
        public string StartupAction { get; set; }

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

        private System.Windows.FrameworkElement _duLieuPreviewControl;
        public System.Windows.FrameworkElement DuLieuPreviewControl { get => _duLieuPreviewControl; set { _duLieuPreviewControl = value; OnPropertyChanged(); } }

        private System.Windows.FrameworkElement _tinhToanPreviewControl;
        public System.Windows.FrameworkElement TinhToanPreviewControl { get => _tinhToanPreviewControl; set { _tinhToanPreviewControl = value; OnPropertyChanged(); } }

        private System.Windows.FrameworkElement _ketQuaPreviewControl;
        public System.Windows.FrameworkElement KetQuaPreviewControl { get => _ketQuaPreviewControl; set { _ketQuaPreviewControl = value; OnPropertyChanged(); } }

        public SharedDataViewModel(IDialogService dialogService, Action anSoCuaSo = null)
        {
            _dialogService = dialogService ?? new MessageBoxDialogService();
            _closeWindowAction = anSoCuaSo;

            LoadDefaults();

            UpdateResultsCommand = new RelayCommand(o => ExecuteRunAnalysis());
            RunAnalysisCommand = new RelayCommand(o => ExecuteRunAnalysis());
            ChangeTabCommand = new RelayCommand(ExecuteChangeTab);
            SelectSlabCommand = new RelayCommand(o => ExecuteSelectSlab());
            SweepSelectSlabCommand = new RelayCommand(o => ExecuteSweepSelectSlab());
            SelectComponentCommand = new RelayCommand(o => ExecuteSelectComponent());
            SweepSelectComponentCommand = new RelayCommand(o => ExecuteSweepSelectComponent());
            ShowFloorDetailsCommand = new RelayCommand(ExecuteShowFloorDetails);
            SelectGroundFloorCommand = new RelayCommand(o => ExecuteSelectGroundFloor());
            JoinByConstructionSequenceCommand = new RelayCommand(o => ExecuteJoinByConstructionSequence());
            NameFormworkComponentsCommand = new RelayCommand(o => ExecuteNameFormworkComponents());
            LoadFormworkSharedParametersCommand = new RelayCommand(o => ExecuteLoadFormworkSharedParameters());
            InitializeFormworkCalculationCommand = new RelayCommand(o => ExecuteInitializeFormworkCalculation());
            CreateFormworkSchedulesCommand = new RelayCommand(o => ExecuteCreateFormworkSchedules());
            ExtractFormworkAreasCommand = new RelayCommand(o => ExecuteExtractFormworkAreas());
            RunFullTakeoffWorkflowCommand = new RelayCommand(o => ExecuteFullTakeoffWorkflow());
            OpenFastPreviewCommand = new RelayCommand(o => RequestFastPreview3D());

            ExecuteUpdateResults(false);
        }

        public void SetCloseWindowAction(Action closeWindowAction)
        {
            _closeWindowAction = closeWindowAction;
        }

        public void LoadDefaults()
        {
            var p = DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models.CalculationParameters.Default;
            WoodStrength = p.WoodStrength;
            ElasticModulus = p.ElasticModulus;
            ConcreteWeight = p.ConcreteWeight;
            SteelWeight = p.SteelWeight;
            WoodFormworkWeight = p.WoodFormworkWeight;
            ConstructionLiveLoad = p.ConstructionLiveLoad;
            PouringImpactLoad = p.PouringImpactLoad;
            VibrationLoad = p.VibrationLoad;
            DeadLoadSafetyFactor = p.DeadLoadSafetyFactor;
            LiveLoadSafetyFactor = p.LiveLoadSafetyFactor;
            MoistureFactor = p.MoistureFactor;
            
            // Deflection limit ratio conversion back to string (simplified for typical values)
            SlabDeflectionLimit = "1/400";
            JoistDeflectionLimit = "1/400";

            VanMatThickness = 18.0;
            XaGoPhuWidth = 50.0;
            XaGoPhuHeight = 100.0;
            XaGoChinhWidth = 100.0;
            XaGoChinhHeight = 100.0;
            CayChongDiameter = 48.0;
            CayChongThickness = 2.0;
            CayChongHeight = 3600.0;
            SupportEffectiveLengthFactor = p.SupportEffectiveLengthFactor;
            SupportRatedCapacity = p.SupportRatedCapacity;
            SupportSlendernessLimit = p.SupportSlendernessLimit;
            SupportBasePlateWidth = p.SupportBasePlateWidth;
            SupportBasePlateDepth = p.SupportBasePlateDepth;
            SupportBaseBearingAllowable = p.SupportBaseBearingAllowable;
            SupportSolePlateWidth = p.SupportSolePlateWidth;
            SupportSolePlateDepth = p.SupportSolePlateDepth;
            SupportGroundBearingAllowable = p.SupportGroundBearingAllowable;
            SupportHeadPlateWidth = p.SupportHeadPlateWidth;
            SupportHeadPlateDepth = p.SupportHeadPlateDepth;
            SupportHeadBearingAllowable = p.SupportHeadBearingAllowable;
            SupportJackExtension = p.SupportJackExtension;
            SupportJackExtensionLimit = p.SupportJackExtensionLimit;

            BeamBottomYokeSpacing = p.BeamBottomYokeSpacing;
            BeamPropSpacing = p.BeamPropSpacing;
            BeamSecondaryJoistWidth = p.BeamSecondaryJoistWidth;
            BeamSecondaryJoistDepth = p.BeamSecondaryJoistDepth;
            BeamPrimaryWalerWidth = p.BeamPrimaryWalerWidth;
            BeamPrimaryWalerDepth = p.BeamPrimaryWalerDepth;
            BeamPrimaryWalerSpacing = p.BeamPrimaryWalerSpacing;
            BeamScaffoldBayWidth = p.BeamScaffoldBayWidth;
            BeamTieRodDiameter = p.BeamTieRodDiameter;
            BeamTieRodSpacing = p.BeamTieRodSpacing;
            BeamTieRodAllowableCapacity = p.BeamTieRodAllowableCapacity;
            BeamSideBraceSpacing = p.BeamSideBraceSpacing;
            BeamSideBraceAngle = p.BeamSideBraceAngle;
            BeamSideBraceDiameter = p.BeamSideBraceDiameter;
            BeamSideBraceThickness = p.BeamSideBraceThickness;
            BeamSideBraceCountPerStation = p.BeamSideBraceCountPerStation;
            BeamSideBraceAllowableCapacity = p.BeamSideBraceAllowableCapacity;
            BeamPlacementModeIndex = p.BeamPlacementModeIndex;
            BeamStrippingStrengthPercent = p.BeamStrippingStrengthPercent;
            BeamVibratorOverlap = p.BeamVibratorOverlap;

            Joist1Spacing = 400.0;
            Joist2Spacing = 1000.0;
            SupportSpacing = 1000.0;
            
            SoNhipVanMat = 2;
            SoNhipXaGoPhu = 3;
            SoNhipXaGoChinh = 3;

            // LoadDefaults is also the explicit "reset settings" path. A deliberate reset
            // invalidates a prior compliance transaction; ordinary page navigation never
            // calls this method and therefore keeps the committed revision intact.
            if (_validatedDesignParameters != null)
            {
                string documentKey = GetDocumentDesignKey(RevitDoc);
                if (documentKey != null)
                {
                    lock (ValidatedDesignSessionLock)
                        ValidatedDesignSessionCache.Remove(documentKey);
                }
                _validatedDesignParameters = null;
                _validatedDesignAdjustments = new List<string>();
                _componentFormworkDesigns.Clear();
                _validatedDesignTimestamp = null;
                _validatedDesignRevision = 0;
                _scheduledDesignRevision = 0;
                IsCalculated = false;
                OnPropertyChanged(nameof(HasValidatedDesign));
                OnPropertyChanged(nameof(ValidatedDesignParameters));
                OnPropertyChanged(nameof(ValidatedDesignAdjustments));
                OnPropertyChanged(nameof(ValidatedDesignTimestamp));
                OnPropertyChanged(nameof(ValidatedDesignRevision));
                OnPropertyChanged(nameof(ScheduledDesignRevision));
                NotifyWorkflowStateChanged();
            }
        }

        public void ExecuteChangeTab(object parameter)
        {
            string tabName = parameter?.ToString();
            if (string.IsNullOrWhiteSpace(tabName)) return;

            // The quantity schedule consumes the latest committed design revision directly.
            // 3D visualization remains optional and is never a prerequisite for takeoff.
            if (tabName == "KetQuaView" && HasValidatedDesign)
                SynchronizeScheduleFromValidatedDesign();

            CurrentWorkflowTab = tabName;
            OnPropertyChanged(nameof(CurrentWorkflowTab));
            NotifyWorkflowStateChanged();
            
            if ((tabName == "DuLieuView" || tabName == "TinhToanView" || tabName == "Report" || tabName == "KetQuaView") && SharedPreviewControl == null && RevitDoc != null)
            {
                InitializePreviewControl(RevitDoc);
            }

            // Chuyển quyền sở hữu PreviewControl cho tab đang active để tránh lỗi WPF
            if (tabName == "TinhToanView" || tabName == "Report")
            {
                KetQuaPreviewControl = null;
                DuLieuPreviewControl = null;
                TinhToanPreviewControl = SharedPreviewControl;
            }
            else if (tabName == "KetQuaView")
            {
                TinhToanPreviewControl = null;
                DuLieuPreviewControl = null;
                KetQuaPreviewControl = SharedPreviewControl;
            }
            else if (tabName == "DuLieuView")
            {
                TinhToanPreviewControl = null;
                KetQuaPreviewControl = null;
                DuLieuPreviewControl = SharedPreviewControl;
            }
            else
            {
                TinhToanPreviewControl = null;
                KetQuaPreviewControl = null;
                DuLieuPreviewControl = null;
            }

            NavigateAction?.Invoke(tabName);
        }

        private void NotifyWorkflowStateChanged()
        {
            OnPropertyChanged(nameof(SelectedComponentCount));
            OnPropertyChanged(nameof(IsScheduleSynchronized));
            OnPropertyChanged(nameof(WorkflowStatusText));
        }

        public void InitializeRevitContext(Document doc, UIDocument uiDoc, Action<string> navigateAction)
        {
            RevitDoc = doc;
            RevitUIDoc = uiDoc;
            NavigateAction = navigateAction;
            RestoreValidatedDesignForCurrentDocument();

            if (RevitDoc != null)
            {
                RevitVersionStatus = $"Revit API {RevitDoc.Application.VersionNumber} - Dự án: {RevitDoc.Title}";
                
                var levels = new FilteredElementCollector(RevitDoc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .Select(l => new LevelModel { Name = l.Name, Elevation = l.Elevation * 304.8 })
                    .ToList();
                AvailableLevels = levels;
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
            // Detach the single native PreviewControl from every WPF ContentControl before
            // disposing it. Autodesk permits only one active PreviewControl, and disposing
            // a still-parented native host can terminate Revit instead of throwing .NET.
            System.Windows.FrameworkElement oldPreview = SharedPreviewControl;
            KetQuaPreviewControl = null;
            TinhToanPreviewControl = null;
            DuLieuPreviewControl = null;
            SharedPreviewControl = null;
            if (oldPreview is IDisposable disposable)
                disposable.Dispose();
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
        public ICommand SelectComponentCommand { get; }
        public ICommand SweepSelectComponentCommand { get; }
        public ICommand ShowFloorDetailsCommand { get; }
        public ICommand SelectGroundFloorCommand { get; }
        public ICommand JoinByConstructionSequenceCommand { get; }
        public ICommand NameFormworkComponentsCommand { get; }
        public ICommand LoadFormworkSharedParametersCommand { get; }
        public ICommand InitializeFormworkCalculationCommand { get; }
        public ICommand CreateFormworkSchedulesCommand { get; }
        public ICommand ExtractFormworkAreasCommand { get; }
        public ICommand RunFullTakeoffWorkflowCommand { get; }
        public ICommand OpenFastPreviewCommand { get; }

        private void ExecuteJoinByConstructionSequence()
        {
            ExecuteWorkflowStep("JOIN cấu kiện theo trình tự thi công",
                () => _takeoffWorkflowService.JoinByConstructionSequence(RevitDoc, FloorElements), true);
        }

        private void ExecuteNameFormworkComponents()
        {
            ExecuteWorkflowStep("Đặt tên cấu kiện",
                () => _takeoffWorkflowService.NameComponents(RevitDoc, FloorElements), true);
        }

        private void ExecuteLoadFormworkSharedParameters()
        {
            ExecuteWorkflowStep("Load Shared Parameters",
                () => _takeoffWorkflowService.EnsureSharedParameters(RevitDoc));
        }

        private void ExecuteInitializeFormworkCalculation()
        {
            try
            {
                EnsureTakeoffSelection();
                RefreshComponentIntersections();
                ExecuteUpdateResults(false);
                IsCalculated = true;
                TakeoffWorkflowStatus = "Đã khởi tạo dữ liệu tính toán ván khuôn từ hình học ròng của các cấu kiện.";
                RevitVersionStatus = TakeoffWorkflowStatus;
                ExecuteChangeTab("TinhToanView");
            }
            catch (Exception ex)
            {
                TakeoffWorkflowStatus = "Lỗi khởi tạo tính toán: " + ex.Message;
                _dialogService.ShowWarning(TakeoffWorkflowStatus, "Quy trình bóc tách ván khuôn");
            }
        }

        private void ExecuteCreateFormworkSchedules()
        {
            ExecuteWorkflowStep("Tạo lập bảng thống kê",
                () => _takeoffWorkflowService.CreateSchedules(RevitDoc));
        }

        private void ExecuteExtractFormworkAreas()
        {
            try
            {
                EnsureTakeoffSelection();
                RefreshComponentIntersections();
                FormworkWorkflowResult result = _takeoffWorkflowService.PublishAreas(RevitDoc, FloorElements,
                    GetComponentFormworkDesign, ValidatedDesignRevision);
                RefreshWorkflowModels();
                TakeoffWorkflowStatus = result.Summary;
                RevitVersionStatus = result.Summary;
                _dialogService.ShowInfo(result.Summary, "Trích xuất diện tích ván khuôn");
            }
            catch (Exception ex)
            {
                TakeoffWorkflowStatus = "Lỗi trích xuất diện tích: " + ex.Message;
                _dialogService.ShowWarning(TakeoffWorkflowStatus, "Quy trình bóc tách ván khuôn");
            }
        }

        private void ExecuteFullTakeoffWorkflow()
        {
            try
            {
                if (FloorElements == null || FloorElements.Count == 0)
                {
                    TakeoffWorkflowStatus = "Đang tự động quét toàn bộ cấu kiện kết cấu trong dự án Revit...";
                    LoadAllProjectComponentsForTakeoff();
                }
                EnsureTakeoffSelection();
                var summaries = new List<string>();
                summaries.Add(_takeoffWorkflowService.JoinByConstructionSequence(RevitDoc, FloorElements).Summary);
                summaries.Add(_takeoffWorkflowService.NameComponents(RevitDoc, FloorElements).Summary);
                summaries.Add(_takeoffWorkflowService.EnsureSharedParameters(RevitDoc).Summary);
                RefreshComponentIntersections();
                ExecuteUpdateResults(false);
                IsCalculated = true;
                summaries.Add("Đã khởi tạo phép tính từ tải trọng, hình học và diện tích ròng.");
                summaries.Add(_takeoffWorkflowService.CreateSchedules(RevitDoc).Summary);
                summaries.Add(_takeoffWorkflowService.PublishAreas(RevitDoc, FloorElements,
                    GetComponentFormworkDesign, ValidatedDesignRevision).Summary);
                RefreshWorkflowModels();
                TakeoffWorkflowStatus = "Đã hoàn thành 6 bước bóc tách khối lượng ván khuôn.";
                RevitVersionStatus = TakeoffWorkflowStatus;
                _dialogService.ShowInfo(TakeoffWorkflowStatus + "\n\n• " + string.Join("\n• ", summaries),
                    "Bóc tách khối lượng ván khuôn");
            }
            catch (Exception ex)
            {
                TakeoffWorkflowStatus = "Quy trình dừng tại bước lỗi: " + ex.Message;
                _dialogService.ShowWarning(TakeoffWorkflowStatus, "Bóc tách khối lượng ván khuôn");
            }
        }

        /// <summary>
        /// The full-project command is self-contained: when the user has not made a manual
        /// selection it collects every supported structural instance from the active model.
        /// Manual selection remains available for running the six steps on a limited scope.
        /// </summary>
        private void LoadAllProjectComponentsForTakeoff()
        {
            if (RevitDoc == null) throw new InvalidOperationException("Không có tài liệu Revit đang hoạt động.");

            var categories = new[]
            {
                BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_StructuralFraming,
                BuiltInCategory.OST_StructuralColumns,
                BuiltInCategory.OST_StructuralFoundation
            };
            List<Element> elements = new FilteredElementCollector(RevitDoc)
                .WherePasses(new ElementMulticategoryFilter(categories))
                .WhereElementIsNotElementType()
                .ToElements()
                .Where(element => element?.Category != null)
                .ToList();

            var slabs = new List<Reference>();
            var foundations = new List<Reference>();
            var beams = new List<Reference>();
            var columns = new List<Reference>();
            foreach (Element element in elements)
            {
                BuiltInCategory category = element.Category.BuiltInCategory;
                if (category == BuiltInCategory.OST_StructuralFoundation)
                {
                    foundations.Add(new Reference(element));
                }
                else if (category == BuiltInCategory.OST_Floors)
                {
                    bool foundationSlab = element is Floor floor &&
                                          RevitDoc.GetElement(floor.GetTypeId()) is FloorType floorType &&
                                          floorType.IsFoundationSlab;
                    (foundationSlab ? foundations : slabs).Add(new Reference(element));
                }
                else if (category == BuiltInCategory.OST_StructuralFraming)
                {
                    beams.Add(new Reference(element));
                }
                else if (category == BuiltInCategory.OST_StructuralColumns)
                {
                    columns.Add(new Reference(element));
                }
            }

            bool append = false;
            if (foundations.Count > 0)
            {
                ProcessSelectedSlab(foundations, showDialog: false, isFoundation: true, appendSelection: append);
                append = true;
            }
            if (slabs.Count > 0)
            {
                ProcessSelectedSlab(slabs, showDialog: false, isFoundation: false, appendSelection: append);
                append = true;
            }
            if (beams.Count > 0)
            {
                ProcessSelectedBeam(beams, append);
                append = true;
            }
            if (columns.Count > 0)
            {
                ProcessSelectedColumn(columns, append);
            }

            SetSelectedComponentComposition(slabs.Count, foundations.Count, beams.Count, columns.Count);
            if (FloorElements == null || FloorElements.Count == 0)
                throw new InvalidOperationException("Dự án không có Sàn, Dầm, Cột hoặc Móng phù hợp để bóc tách.");

            TakeoffWorkflowStatus = $"Đã tự động nạp {FloorElements.Count} cấu kiện: " +
                                    $"{slabs.Count} sàn · {foundations.Count} móng · " +
                                    $"{beams.Count} dầm · {columns.Count} cột.";
            RevitVersionStatus = TakeoffWorkflowStatus;
        }

        private void ExecuteWorkflowStep(string title, Func<FormworkWorkflowResult> action,
            bool refreshModels = false)
        {
            try
            {
                EnsureTakeoffSelection();
                FormworkWorkflowResult result = action();
                if (refreshModels) RefreshWorkflowModels();
                TakeoffWorkflowStatus = result.Summary;
                RevitVersionStatus = result.Summary;
                string warnings = result.Warnings.Count == 0
                    ? string.Empty
                    : "\n\nMột số trường hợp được bỏ qua:\n• " + string.Join("\n• ", result.Warnings);
                _dialogService.ShowInfo(result.Summary + warnings, title);
            }
            catch (Exception ex)
            {
                TakeoffWorkflowStatus = $"{title}: {ex.Message}";
                _dialogService.ShowWarning(TakeoffWorkflowStatus, "Quy trình bóc tách ván khuôn");
            }
        }

        private void EnsureTakeoffSelection()
        {
            if (RevitDoc == null) throw new InvalidOperationException("Chức năng này chỉ chạy trong Revit.");
            if (FloorElements == null || FloorElements.Count == 0)
                throw new InvalidOperationException("Hãy chọn Sàn, Dầm, Cột hoặc Móng trước khi chạy quy trình.");
        }

        private void RefreshWorkflowModels()
        {
            FloorElements = (FloorElements ?? new List<FloorElementModel>()).ToList();
            OnPropertyChanged(nameof(TakeoffWorkflowStatus));
        }

        private void ExecuteUpdateResults(bool showWarning = false)
        {
            if (!string.IsNullOrEmpty(Error))
            {
                if (showWarning)
                {
                    _dialogService.ShowWarning("Vui lòng kiểm tra lại các thông số đầu vào không hợp lệ.", "Cảnh báo");
                }
                return;
            }

            var p = new Models.CalculationParameters
            {
                WoodStrength = WoodStrength > 0 ? WoodStrength : 15.0,
                PlywoodBendingStrength = PlywoodBendingStrength > 0 ? PlywoodBendingStrength : 10.0,
                WoodShearStrength = WoodShearStrength > 0 ? WoodShearStrength : 2.0,
                ElasticModulus = ElasticModulus,
                ConcreteWeight = ConcreteWeight > 0 ? ConcreteWeight : 24.0,
                SteelWeight = SteelWeight > 0 ? SteelWeight : 1.0,
                WoodFormworkWeight = WoodFormworkWeight,
                ConstructionLiveLoad = ConstructionLiveLoad,
                PouringImpactLoad = PouringImpactLoad > 0 ? PouringImpactLoad : 2.0,
                VibrationLoad = VibrationLoad > 0 ? VibrationLoad : 2.0,
                DeadLoadSafetyFactor = DeadLoadSafetyFactor,
                LiveLoadSafetyFactor = LiveLoadSafetyFactor,
                MoistureFactor = MoistureFactor,
                TemperatureFactor = TemperatureFactor,
                SlabDeflectionLimitRatio = Models.FormworkCalculationService.ParseFraction(SlabDeflectionLimit),
                JoistDeflectionLimitRatio = Models.FormworkCalculationService.ParseFraction(JoistDeflectionLimit),
                SlabThickness = SlabThickness,
                VanMatThickness = VanMatThickness,
                XaGoPhuWidth = XaGoPhuWidth,
                XaGoPhuHeight = XaGoPhuHeight,
                XaGoChinhWidth = XaGoChinhWidth,
                XaGoChinhHeight = XaGoChinhHeight,
                CayChongDiameter = CayChongDiameter,
                CayChongThickness = CayChongThickness,
                CayChongHeight = CayChongHeight,
                SupportEffectiveLengthFactor = SupportEffectiveLengthFactor,
                SupportRatedCapacity = SupportRatedCapacity,
                SupportSlendernessLimit = SupportSlendernessLimit,
                SupportBasePlateWidth = SupportBasePlateWidth,
                SupportBasePlateDepth = SupportBasePlateDepth,
                SupportBaseBearingAllowable = SupportBaseBearingAllowable,
                SupportSolePlateWidth = SupportSolePlateWidth,
                SupportSolePlateDepth = SupportSolePlateDepth,
                SupportGroundBearingAllowable = SupportGroundBearingAllowable,
                SupportHeadPlateWidth = SupportHeadPlateWidth,
                SupportHeadPlateDepth = SupportHeadPlateDepth,
                SupportHeadBearingAllowable = SupportHeadBearingAllowable,
                SupportJackExtension = SupportJackExtension,
                SupportJackExtensionLimit = SupportJackExtensionLimit,
                SoNhipVanMat = SoNhipVanMat,
                SoNhipXaGoPhu = SoNhipXaGoPhu,
                SoNhipXaGoChinh = SoNhipXaGoChinh,
                Joist1Spacing = Joist1Spacing > 0 ? Joist1Spacing : 400.0,
                Joist2Spacing = Joist2Spacing > 0 ? Joist2Spacing : 1000.0,
                SupportSpacing = SupportSpacing > 0 ? SupportSpacing : 1000.0,
                
                ComponentType = CurrentComponentType,
                
                // Beam
                BeamWidth = BeamWidth,
                BeamDepth = BeamDepth,
                BeamLength = BeamLength,
                BeamSidePlywoodThickness = BeamSidePlywoodThickness,
                BeamBottomPlywoodThickness = BeamBottomPlywoodThickness,
                BeamSideYokeSpacing = BeamSideYokeSpacing,
                BeamBottomYokeSpacing = BeamBottomYokeSpacing,
                BeamPropSpacing = BeamPropSpacing,
                BeamSecondaryJoistWidth = BeamSecondaryJoistWidth,
                BeamSecondaryJoistDepth = BeamSecondaryJoistDepth,
                BeamPrimaryWalerWidth = BeamPrimaryWalerWidth,
                BeamPrimaryWalerDepth = BeamPrimaryWalerDepth,
                BeamPrimaryWalerSpacing = BeamPrimaryWalerSpacing,
                BeamScaffoldBayWidth = BeamScaffoldBayWidth,
                BeamTieRodDiameter = BeamTieRodDiameter,
                BeamTieRodSpacing = BeamTieRodSpacing,
                BeamTieRodAllowableCapacity = BeamTieRodAllowableCapacity,
                BeamSideBraceSpacing = BeamSideBraceSpacing,
                BeamSideBraceAngle = BeamSideBraceAngle,
                BeamSideBraceDiameter = BeamSideBraceDiameter,
                BeamSideBraceThickness = BeamSideBraceThickness,
                BeamSideBraceCountPerStation = BeamSideBraceCountPerStation,
                BeamSideBraceAllowableCapacity = BeamSideBraceAllowableCapacity,
                BeamPlacementModeIndex = BeamPlacementModeIndex,
                BeamStrippingStrengthPercent = BeamStrippingStrengthPercent,
                BeamVibratorOverlap = BeamVibratorOverlap,
                IsBeamSelected = true,
                
                // Column
                ColumnWidth = ColumnWidth,
                ColumnDepth = ColumnDepth,
                ColumnHeight = ColumnHeight,
                ColumnPlywoodThickness = ColumnPlywoodThickness,
                ColumnStiffenerWidth = ColumnStiffenerWidth,
                ColumnStiffenerDepth = ColumnStiffenerDepth,
                ColumnStiffenerSpacing = ColumnStiffenerSpacing,
                ColumnYokeSpacing = ColumnYokeSpacing,
                ColumnUseZonedYokeSpacing = ColumnUseZonedYokeSpacing,
                ColumnFirstYokeOffset = ColumnFirstYokeOffset,
                ColumnYokeBottomSpacing = ColumnYokeBottomSpacing,
                ColumnYokeUpperSpacing = ColumnYokeUpperSpacing,
                ColumnYokeTransitionHeight = ColumnYokeTransitionHeight,
                ColumnLastYokeOffset = ColumnLastYokeOffset,
                ColumnYokeWidth = ColumnYokeWidth,
                ColumnYokeHeight = ColumnYokeHeight,
                ColumnYokeThickness = ColumnYokeThickness,
                ColumnYokePairCount = ColumnYokePairCount,
                ColumnYokeMaterialIndex = ColumnYokeMaterialIndex,
                ColumnTieRodDiameter = ColumnTieRodDiameter,
                ColumnTieRodCountPerFace = ColumnTieRodCountPerFace,
                ColumnTieRodAllowableCapacity = ColumnTieRodAllowableCapacity,
                ColumnBraceCount = ColumnBraceCount,
                ColumnBraceAngle = ColumnBraceAngle,
                ColumnBraceAllowableCapacity = ColumnBraceAllowableCapacity,
                ColumnBracePlacementModeIndex = ColumnBracePlacementModeIndex,
                ColumnNeighbourTieThreshold = ColumnNeighbourTieThreshold,
                ColumnIsBoundaryOrCorner = ColumnIsBoundaryOrCorner,
                ColumnCableAllowableCapacity = ColumnCableAllowableCapacity,
                ColumnTurnbuckleAllowableCapacity = ColumnTurnbuckleAllowableCapacity,
                ColumnAnchorAllowableCapacity = ColumnAnchorAllowableCapacity,
                ColumnAxisOffset = ColumnAxisOffset,
                ColumnPlumbTargetTolerance = ColumnPlumbTargetTolerance,
                ColumnFormworkTopExtension = ColumnFormworkTopExtension,
                ColumnHoseBottomClearance = ColumnHoseBottomClearance,
                ColumnPourLiftHeight = ColumnPourLiftHeight,
                ColumnBeamStopOffset = ColumnBeamStopOffset,
                ColumnStripTimeHours = ColumnStripTimeHours,
                ColumnCuringDays = ColumnCuringDays,
                ColumnUseWorkPlatform = ColumnUseWorkPlatform,
                ColumnWorkPlatformWidth = ColumnWorkPlatformWidth,
                ColumnGuardrailHeight = ColumnGuardrailHeight,
                ConcretePourRate = ConcretePourRate,
                ConcreteTemperature = ConcreteTemperature,
                IsColumnSelected = true,
                
                SelectedFloorDetailName = SelectedFloorDetailName,
                IsFloorSelected = FloorElements != null && FloorElements.Count > 0,
                IsGroundLevel = this.IsGroundLevel
            };

            if (HasMixedComponentSelection)
            {
                var combined = new List<KetQuaHienThi>();
                void Append(string group, CalculationResult result)
                {
                    foreach (KetQuaHienThi item in result.ReportItems)
                    {
                        item.NoiDungKiemTra = $"[{group}] {item.NoiDungKiemTra}";
                        combined.Add(item);
                    }
                }

                bool originalGround = p.IsGroundLevel;
                if (SelectedSlabCount > 0)
                {
                    p.IsGroundLevel = false;
                    Append("SÀN", Models.FormworkCalculationService.CheckSafety(p));
                }
                if (SelectedFoundationCount > 0)
                {
                    p.IsGroundLevel = true;
                    Append("MÓNG/NỀN", Models.FormworkCalculationService.CheckSafety(p));
                }
                p.IsGroundLevel = originalGround;
                if (SelectedBeamCount > 0) Append("DẦM", Models.FormworkCalculationService.CheckBeamSafety(p));
                if (SelectedColumnCount > 0) Append("CỘT", Models.FormworkCalculationService.CheckColumnSafety(p));
                CheckResults = combined;
            }
            else
            {
                CalculationResult res = CurrentComponentType == StructuralComponentType.Beam
                    ? Models.FormworkCalculationService.CheckBeamSafety(p)
                    : CurrentComponentType == StructuralComponentType.Column
                        ? Models.FormworkCalculationService.CheckColumnSafety(p)
                        : Models.FormworkCalculationService.CheckSafety(p);
                CheckResults = res.ReportItems;
            }
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

            if (CurrentComponentType == StructuralComponentType.Beam) IsSelectingBeam = true;
            else if (CurrentComponentType == StructuralComponentType.Column) IsSelectingColumn = true;
            else IsSelectingSlab = true;

            StartupTab = "DuLieuView";
            _closeWindowAction?.Invoke();
        }

        private void ExecuteSweepSelectSlab()
        {
            if (RevitUIDoc == null || RevitDoc == null)
            {
                _dialogService.ShowWarning("Chức năng này chỉ khả dụng khi chạy trực tiếp trong Revit.", "Thông báo");
                return;
            }

            if (CurrentComponentType == StructuralComponentType.Beam) IsSweepSelectingBeam = true;
            else if (CurrentComponentType == StructuralComponentType.Column) IsSweepSelectingColumn = true;
            else IsSweepSelectingSlab = true;

            StartupTab = "DuLieuView";
            _closeWindowAction?.Invoke();
        }

        private void ExecuteSelectComponent()
        {
            if (RevitUIDoc == null || RevitDoc == null)
            {
                _dialogService.ShowWarning("Chức năng này chỉ khả dụng khi chạy trực tiếp trong Revit.", "Thông báo");
                return;
            }

            IsSelectingComponent = true;
            _closeWindowAction?.Invoke();
        }

        private void ExecuteSweepSelectComponent()
        {
            if (RevitUIDoc == null || RevitDoc == null)
            {
                _dialogService.ShowWarning("Chức năng này chỉ khả dụng khi chạy trực tiếp trong Revit.", "Thông báo");
                return;
            }

            IsSweepSelectingComponent = true;
            _closeWindowAction?.Invoke();
        }

        private void ExecuteSelectGroundFloor()
        {
            var window = new Views.SelectGroundLevelWindow(AvailableLevels, DesignatedGroundLevel);
            if (window.ShowDialog() == true)
            {
                DesignatedGroundLevel = window.SelectedLevel;
                RevitVersionStatus = $"Đã thiết lập tầng trệt: {DesignatedGroundLevel.Name}";
                
                if (FloorElements != null && FloorElements.Count > 0)
                {
                    IsGroundLevel = FloorElements.All(f => f.Elevation <= DesignatedGroundLevel.Elevation + 0.01);
                }
                else
                {
                    IsGroundLevel = false;
                }
                
                ExecuteUpdateResults(false);
                // Changing the designated ground level invalidates calculation/layout
                // data only. Native Revit geometry is generated exclusively through
                // RequestRender3D(), after an explicit user request.
            }
        }

        private void ExecuteShowFloorDetails(object parameter)
        {
            if (parameter is FloorElementModel floorModel)
            {
                if (floorModel.Thickness > 0)
                {
                    SlabThickness = floorModel.Thickness;
                    SelectedFloorDetailName = floorModel.FamilyTypeName;
                    ExecuteUpdateResults(false);
                }
            }
        }


        public void ExecuteSelectComponentFromKetQua()
        {
            if (RevitUIDoc == null || RevitDoc == null) return;
            IsSelectingComponent = true;
            StartupTab = "KetQuaView";
            _closeWindowAction?.Invoke();
        }

        public void ExecuteSweepSelectComponentFromKetQua()
        {
            if (RevitUIDoc == null || RevitDoc == null) return;
            IsSweepSelectingComponent = true;
            StartupTab = "KetQuaView";
            _closeWindowAction?.Invoke();
        }

        public void ExecuteSelectComponentFromTinhToan()
        {
            if (RevitUIDoc == null || RevitDoc == null) return;
            IsSelectingComponent = true;
            StartupTab = "TinhToanView";
            _closeWindowAction?.Invoke();
        }

        public void ExecuteSweepSelectComponentFromTinhToan()
        {
            if (RevitUIDoc == null || RevitDoc == null) return;
            IsSweepSelectingComponent = true;
            StartupTab = "TinhToanView";
            _closeWindowAction?.Invoke();
        }

        private void StoreSelectedComponentModels(IEnumerable<FloorElementModel> models, bool appendSelection)
        {
            List<FloorElementModel> incoming = (models ?? Enumerable.Empty<FloorElementModel>()).ToList();
            if (!appendSelection && !IsSweepSelectingComponent)
            {
                FloorElements = incoming;
                return;
            }

            var merged = (FloorElements ?? new List<FloorElementModel>())
                .Concat(incoming)
                .Where(item => item != null)
                .GroupBy(item => string.IsNullOrWhiteSpace(item.UniqueId) ? item.ElementId : item.UniqueId,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Last())
                .ToList();
            FloorElements = merged;
        }

        /// <summary>
        /// Publishes the complete structural selection. The legacy event name is retained so
        /// existing views remain binary compatible, but its payload is no longer slab-only.
        /// </summary>
        private void PublishComponentSelectionContext(string maxComponentName = null,
            List<List<System.Windows.Point>> slabPolygons = null)
        {
            List<FloorElementModel> selected = (FloorElements ?? new List<FloorElementModel>())
                .Where(item => item != null)
                .GroupBy(item => string.IsNullOrWhiteSpace(item.UniqueId) ? item.ElementId : item.UniqueId,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Last())
                .ToList();

            LatestSelectionContext = new SlabSelectionEventArgs
            {
                FloorElements = selected,
                SlabThickness = SlabThickness,
                SelectedSlabArea = selected.Where(item => item.ComponentType == StructuralComponentType.Slab)
                    .Sum(item => item.Area),
                SelectedSlabPerimeter = selected.Where(item => item.ComponentType == StructuralComponentType.Slab)
                    .Sum(item => item.Perimeter),
                Joist1Spacing = Joist1Spacing,
                Joist2Spacing = Joist2Spacing,
                SupportSpacing = SupportSpacing,
                FloorCount = selected.Count,
                ComponentCount = selected.Count,
                SlabCount = selected.Count(item => item.ComponentType == StructuralComponentType.Slab),
                FoundationCount = selected.Count(item => item.ComponentType == StructuralComponentType.Foundation),
                BeamCount = selected.Count(item => item.ComponentType == StructuralComponentType.Beam),
                ColumnCount = selected.Count(item => item.ComponentType == StructuralComponentType.Column),
                MaxFloorName = maxComponentName ?? SelectedFloorDetailName,
                SlabPolygons = slabPolygons ?? new List<List<System.Windows.Point>>()
            };
            SlabSelectionCompleted?.Invoke(this, LatestSelectionContext);
        }

        public void ProcessSelectedSlab(System.Collections.Generic.IList<Reference> references, bool showDialog = true,
            bool isFoundation = false, bool appendSelection = false)
        {
            if (references != null && references.Count > 0)
            {
                CurrentComponentType = isFoundation ? StructuralComponentType.Foundation : StructuralComponentType.Slab;
                if (!IsSelectingComponent && !IsSweepSelectingComponent)
                    SetSelectedComponentComposition(isFoundation ? 0 : references.Count,
                        isFoundation ? references.Count : 0, 0, 0);

                double totalArea = 0;
                double totalPerimeter = 0;
                double maxThicknessMm = 0;
                string maxFloorName = "";
                double minPropHeightMm = double.MaxValue;

                SelectedSlabInfos.Clear();
                
                var floorModels = new List<FloorElementModel>();
                var allPolygons = new List<List<System.Windows.Point>>();

                foreach (var refer in references)
                {
                    Element element = RevitDoc.GetElement(refer.ElementId);
                    if (element != null && element.Category != null && (element.Category.BuiltInCategory == BuiltInCategory.OST_Floors || element.Category.BuiltInCategory == BuiltInCategory.OST_StructuralFoundation))
                    {
                        double thicknessFeet = 0;
                        Parameter thicknessParam = element.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM) ?? element.LookupParameter("Thickness") ?? element.LookupParameter("Chiều dày");
                        if (thicknessParam != null)
                        {
                            thicknessFeet = thicknessParam.AsDouble();
                        }
                        else
                        {
                            ElementType elemType = element.Document.GetElement(element.GetTypeId()) as ElementType;
                            if (elemType != null)
                            {
                                Parameter typeThicknessParam = elemType.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM) ?? elemType.LookupParameter("Thickness") ?? elemType.LookupParameter("Chiều dày");
                                if (typeThicknessParam != null)
                                    thicknessFeet = typeThicknessParam.AsDouble();
                            }
                        }
                        
                        if (thicknessFeet <= 0) thicknessFeet = 150.0 / 304.8; // default

                        // Extract Geometry
                        Options opt = new Options();
                        opt.ComputeReferences = true;
                        GeometryElement geomElem = element.get_Geometry(opt);
                        if (geomElem != null)
                        {
                            foreach (GeometryObject geomObj in geomElem)
                            {
                                if (geomObj is Solid solid && solid.Faces.Size > 0)
                                {
                                    Face topFace = null;
                                    foreach (Face face in solid.Faces)
                                    {
                                        if (face is PlanarFace pf && pf.FaceNormal.IsAlmostEqualTo(XYZ.BasisZ))
                                        {
                                            topFace = face;
                                            break;
                                        }
                                    }

                                    if (topFace != null)
                                    {
                                        foreach (EdgeArray edgeArray in topFace.EdgeLoops)
                                        {
                                            var polygon = new List<System.Windows.Point>();
                                            foreach (Edge edge in edgeArray)
                                            {
                                                var pts = edge.Tessellate();
                                                for (int i = 0; i < pts.Count - 1; i++) // exclude last point to avoid dupes at corners
                                                {
                                                    polygon.Add(new System.Windows.Point(pts[i].X * 304.8, pts[i].Y * 304.8));
                                                }
                                            }
                                            allPolygons.Add(polygon);
                                        }
                                    }
                                }
                            }
                        }
                        
                        double thicknessMm = thicknessFeet * 304.8;
                        SelectedSlabInfos.Add(new Tuple<string, double>(element.Name, thicknessMm));

                        if (thicknessMm > maxThicknessMm)
                        {
                            maxThicknessMm = thicknessMm;
                            maxFloorName = element.Name;
                        }

                        double areaM2 = 0;
                        Parameter areaParam = element.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                        if (areaParam != null)
                        {
                            double areaSqFt = areaParam.AsDouble();
                            areaM2 = areaSqFt * 0.092903; // convert sq ft to m2
                            totalArea += areaM2; 
                        }

                        Parameter perimeterParam = element.get_Parameter(BuiltInParameter.HOST_PERIMETER_COMPUTED);
                        double perimeterM = 0;
                        if (perimeterParam != null)
                        {
                            double perimeterFt = perimeterParam.AsDouble();
                            perimeterM = perimeterFt * 0.3048;
                            totalPerimeter += perimeterM; // convert ft to m
                        }

                        BoundingBoxXYZ elementBounds = element.get_BoundingBox(null);
                        double planLengthMm = elementBounds == null
                            ? Math.Sqrt(Math.Max(0.01, areaM2)) * 1000.0
                            : Math.Max(elementBounds.Max.X - elementBounds.Min.X,
                                elementBounds.Max.Y - elementBounds.Min.Y) * 304.8;
                        double planWidthMm = elementBounds == null
                            ? Math.Sqrt(Math.Max(0.01, areaM2)) * 1000.0
                            : Math.Min(elementBounds.Max.X - elementBounds.Min.X,
                                elementBounds.Max.Y - elementBounds.Min.Y) * 304.8;
                        
                        string levelName = "Unknown Level";
                        double storyHeightMm = 3600.0;
                        Level level = RevitDoc.GetElement(element.LevelId) as Level;
                        if (level != null)
                        {
                            levelName = level.Name;
                            double currentElevation = level.Elevation;
                            var allLevels = new FilteredElementCollector(RevitDoc)
                                .OfClass(typeof(Level))
                                .Cast<Level>()
                                .OrderBy(l => l.Elevation)
                                .ToList();
                            Level levelBelow = allLevels.LastOrDefault(l => l.Elevation < currentElevation - 0.01);
                            if (levelBelow != null)
                            {
                                storyHeightMm = (currentElevation - levelBelow.Elevation) * 304.8;
                            }
                            else if (currentElevation > 0)
                            {
                                storyHeightMm = currentElevation * 304.8;
                            }

                            Level nextLevel = allLevels.FirstOrDefault(l => l.Elevation > currentElevation + 0.01);
                            if (nextLevel != null)
                            {
                                double floorToFloorHeightMm = (nextLevel.Elevation - currentElevation) * 304.8;
                                double joistSystemHeightMm = VanMatThickness + XaGoPhuHeight + XaGoChinhHeight;
                                double computedPropHeightMm = floorToFloorHeightMm - thicknessMm - joistSystemHeightMm;
                                if (computedPropHeightMm > 0)
                                {
                                    if (computedPropHeightMm < minPropHeightMm)
                                    {
                                        minPropHeightMm = computedPropHeightMm;
                                    }
                                }
                            }
                        }

                        var tempModel = new FloorElementModel
                        {
                            UniqueId = element.UniqueId,
                            ElementId = element.Id.ToString(),
                            ComponentType = isFoundation ? StructuralComponentType.Foundation : StructuralComponentType.Slab,
                            FamilyTypeName = element.Name,
                            Level = levelName,
                            Area = areaM2,
                            Perimeter = perimeterM,
                            Thickness = thicknessMm,
                            StoryHeight = isFoundation ? planLengthMm : storyHeightMm,
                            Elevation = isFoundation ? planWidthMm : 0.0,
                            ComponentSummary = isFoundation
                                ? $"Móng · {areaM2:F2} m² · dày {thicknessMm:F0} mm"
                                : $"Sàn · {areaM2:F2} m² · dày {thicknessMm:F0} mm"
                        };
                        floorModels.Add(tempModel);
                    }
                }

                if (floorModels.Count > 0)
                {
                    StoreSelectedComponentModels(floorModels, appendSelection);

                    // Normalize polygons to (0,0)
                    if (allPolygons.Count > 0)
                    {
                        double minX = double.MaxValue, minY = double.MaxValue;
                        foreach (var poly in allPolygons)
                        {
                            foreach (var pt in poly)
                            {
                                if (pt.X < minX) minX = pt.X;
                                if (pt.Y < minY) minY = pt.Y;
                            }
                        }
                        for (int i = 0; i < allPolygons.Count; i++)
                        {
                            for (int j = 0; j < allPolygons[i].Count; j++)
                            {
                                allPolygons[i][j] = new System.Windows.Point(allPolygons[i][j].X - minX, allPolygons[i][j].Y - minY);
                            }
                        }
                    }

                    // Chọn sàn dày nhất (nguy hiểm nhất) để hiện chi tiết mặc định
                    var maxFloor = floorModels.OrderByDescending(f => f.Thickness).FirstOrDefault();
                    if (maxFloor != null)
                    {
                        SelectedFloorDetailName = maxFloor.FamilyTypeName;
                        SlabThickness = maxFloor.Thickness;
                        CurrentStoryHeight = maxFloor.StoryHeight;
                    }
                    else
                    {
                        SlabThickness = maxThicknessMm;
                    }
                    SelectedSlabArea = totalArea > 0 ? totalArea : 100.0;
                    SelectedSlabPerimeter = totalPerimeter > 0 ? totalPerimeter : 40.0;

                    AutoDesignFormwork();

                    string propWarning = "";
                    if (minPropHeightMm != double.MaxValue && minPropHeightMm > 0)
                    {
                        if (minPropHeightMm < 1500.0)
                        {
                            CayChongHeight = 3600.0;
                            propWarning = " [Cảnh báo: Cây chống < 1.5m, dùng mặc định 3.6m]";
                        }
                        else if (minPropHeightMm > 6000.0)
                        {
                            CayChongHeight = 3600.0;
                            propWarning = " [Cảnh báo: Cây chống > 6m cần giằng, dùng mặc định 3.6m]";
                        }
                        else
                        {
                            CayChongHeight = minPropHeightMm;
                        }
                    }

                    RevitVersionStatus = $"Đã chọn {floorModels.Count} thành phần | Chiều dày nguy hiểm nhất: {maxThicknessMm:F0} mm{propWarning}";

                    if (showDialog)
                    {
                        _dialogService.ShowInfo(
                            $"Đã chọn thành công {floorModels.Count} đối tượng từ Revit!\n" +
                            $"- Đối tượng nguy hiểm nhất: {maxFloorName}\n" +
                            $"- Chiều dày: {maxThicknessMm:F0} mm\n" +
                            $"- Tổng diện tích: {SelectedSlabArea:F2} m²\n\n" +
                            $"- Tự động tính toán lại các khoảng cách xà gồ, cây chống tối ưu.",
                            "Revit API Integration");
                    }
                }
                
                // Highlight the selected slabs
                var selectedIds = floorModels.Select(f => new ElementId(long.Parse(f.ElementId))).ToList();
                UpdatePreviewGraphics(selectedIds);

                // Check if the floor is on Level 1 / Ground Level or is a Foundation
                if (isFoundation)
                {
                    IsGroundLevel = true;
                }
                else if (DesignatedGroundLevel != null)
                {
                    IsGroundLevel = floorModels.All(f => f.Elevation <= DesignatedGroundLevel.Elevation + 0.01);
                }
                else
                {
                    IsGroundLevel = false;
                }

                // Lưu trạng thái trước khi phát sự kiện
                // Phát toàn bộ phạm vi cấu kiện, không chỉ riêng các sàn vừa xử lý.
                PublishComponentSelectionContext(maxFloorName, allPolygons);

                // Đánh dấu IsCalculated để TinhToan tự động recalculate
                IsCalculated = true;
            }
        }

        private void UpdatePreviewGraphics(List<ElementId> selectedFloorIds)
        {
            if (RevitDoc == null || RevitUIDoc == null) return;
            
            try
            {
                View activeView = RevitUIDoc.ActiveView;
                if (activeView == null) return;

                using (Transaction t = new Transaction(RevitDoc, "Highlight Slabs"))
                {
                    t.Start();
                    
                    if (KetQuaLastHighlightedFloorIds != null && KetQuaLastHighlightedViewId != null)
                    {
                        View lastView = RevitDoc.GetElement(KetQuaLastHighlightedViewId) as View;
                        if (lastView != null)
                        {
                            foreach (var oldId in KetQuaLastHighlightedFloorIds)
                            {
                                lastView.SetElementOverrides(oldId, new OverrideGraphicSettings());
                            }
                        }
                    }
                    
                    OverrideGraphicSettings ogs = new OverrideGraphicSettings();
                    ogs.SetSurfaceTransparency(0);
                    ogs.SetHalftone(false);
                    
                    FillPatternElement solidFill = new FilteredElementCollector(RevitDoc)
                        .OfClass(typeof(FillPatternElement))
                        .Cast<FillPatternElement>()
                        .FirstOrDefault(a => a.GetFillPattern() != null && a.GetFillPattern().IsSolidFill);
                        
                    Autodesk.Revit.DB.Color redColor = new Autodesk.Revit.DB.Color(255, 128, 128);
                    Autodesk.Revit.DB.Color darkRedColor = new Autodesk.Revit.DB.Color(255, 0, 0);

                    if (solidFill != null)
                    {
                        ogs.SetSurfaceForegroundPatternId(solidFill.Id);
                        ogs.SetSurfaceForegroundPatternColor(redColor);
                        ogs.SetSurfaceBackgroundPatternId(solidFill.Id);
                        ogs.SetSurfaceBackgroundPatternColor(redColor);

                        ogs.SetCutForegroundPatternId(solidFill.Id);
                        ogs.SetCutForegroundPatternColor(redColor);
                        ogs.SetCutBackgroundPatternId(solidFill.Id);
                        ogs.SetCutBackgroundPatternColor(redColor);
                    }
                    
                    ogs.SetProjectionLineColor(darkRedColor);
                    ogs.SetCutLineColor(darkRedColor);
                    ogs.SetProjectionLineWeight(8);
                    ogs.SetCutLineWeight(8);
                    
                    KetQuaLastHighlightedFloorIds.Clear();
                    
                    foreach (var id in selectedFloorIds)
                    {
                        activeView.SetElementOverrides(id, ogs);
                        KetQuaLastHighlightedFloorIds.Add(id);
                    }
                    
                    KetQuaLastHighlightedViewId = activeView.Id;
                    
                    RevitDoc.Regenerate();
                    t.Commit();
                }
                
                // Cập nhật lại PreviewControl nếu cần
                if (SharedPreviewControl is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                SharedPreviewControl = new Autodesk.Revit.UI.PreviewControl(RevitDoc, activeView.Id);
                
                // Gán lại cho tab hiện tại đang active
                if (DuLieuPreviewControl != null) DuLieuPreviewControl = SharedPreviewControl;
                else if (TinhToanPreviewControl != null) TinhToanPreviewControl = SharedPreviewControl;
                else if (KetQuaPreviewControl != null) KetQuaPreviewControl = SharedPreviewControl;
                else DuLieuPreviewControl = SharedPreviewControl; // Default
            }
            catch { /* Ignore */ }
        }

        public void RenderPreviewGraphics(List<string> validUniqueIds, Action<System.Windows.FrameworkElement> setPreviewControlAction)
        {
            if (RevitDoc == null || validUniqueIds == null || validUniqueIds.Count == 0) return;

            try
            {
                Element firstFloorElem = RevitDoc.GetElement(validUniqueIds.First());
                if (firstFloorElem != null)
                {
                    ElementId levelId = firstFloorElem.LevelId;
                    
                    ViewPlan viewPlan = new FilteredElementCollector(RevitDoc)
                        .OfClass(typeof(ViewPlan))
                        .Cast<ViewPlan>()
                        .FirstOrDefault(v => v.GenLevel != null && v.GenLevel.Id == levelId && !v.IsTemplate && (v.ViewType == ViewType.FloorPlan || v.ViewType == ViewType.EngineeringPlan));

                    if (viewPlan == null)
                    {
                        viewPlan = new FilteredElementCollector(RevitDoc)
                            .OfClass(typeof(ViewPlan))
                            .Cast<ViewPlan>()
                            .FirstOrDefault(v => !v.IsTemplate && (v.ViewType == ViewType.FloorPlan || v.ViewType == ViewType.EngineeringPlan));
                    }

                    if (viewPlan != null)
                    {
                        using (Transaction t = new Transaction(RevitDoc, "Highlight Floors"))
                        {
                            t.Start();
                            
                            if (KetQuaLastHighlightedFloorIds != null && KetQuaLastHighlightedViewId != null)
                            {
                                View lastView = RevitDoc.GetElement(KetQuaLastHighlightedViewId) as View;
                                if (lastView != null)
                                {
                                    foreach (var oldId in KetQuaLastHighlightedFloorIds)
                                    {
                                        lastView.SetElementOverrides(oldId, new OverrideGraphicSettings());
                                    }
                                    if (KetQuaLastViewTemplateId != null)
                                    {
                                        lastView.ViewTemplateId = KetQuaLastViewTemplateId;
                                    }
                                }
                            }
                            
                            KetQuaLastViewTemplateId = viewPlan.ViewTemplateId;
                            viewPlan.ViewTemplateId = ElementId.InvalidElementId;

                            OverrideGraphicSettings ogs = new OverrideGraphicSettings();
                            ogs.SetSurfaceTransparency(0);
                            ogs.SetHalftone(false);
                            
                            FillPatternElement solidFill = new FilteredElementCollector(RevitDoc)
                                .OfClass(typeof(FillPatternElement))
                                .Cast<FillPatternElement>()
                                .FirstOrDefault(a => a.GetFillPattern() != null && a.GetFillPattern().IsSolidFill);
                                
                            Autodesk.Revit.DB.Color redColor = new Autodesk.Revit.DB.Color(255, 128, 128);
                            Autodesk.Revit.DB.Color darkRedColor = new Autodesk.Revit.DB.Color(255, 0, 0);

                            if (solidFill != null)
                            {
                                ogs.SetSurfaceForegroundPatternId(solidFill.Id);
                                ogs.SetSurfaceForegroundPatternColor(redColor);
                                ogs.SetSurfaceBackgroundPatternId(solidFill.Id);
                                ogs.SetSurfaceBackgroundPatternColor(redColor);

                                ogs.SetCutForegroundPatternId(solidFill.Id);
                                ogs.SetCutForegroundPatternColor(redColor);
                                ogs.SetCutBackgroundPatternId(solidFill.Id);
                                ogs.SetCutBackgroundPatternColor(redColor);
                            }
                            
                            ogs.SetProjectionLineColor(darkRedColor);
                            ogs.SetCutLineColor(darkRedColor);
                            ogs.SetProjectionLineWeight(8);
                            ogs.SetCutLineWeight(8);
                            
                            KetQuaLastHighlightedFloorIds.Clear();
                            
                            foreach (var id in validUniqueIds)
                            {
                                Element elem = RevitDoc.GetElement(id);
                                if (elem != null)
                                {
                                    viewPlan.SetElementOverrides(elem.Id, ogs);
                                    KetQuaLastHighlightedFloorIds.Add(elem.Id);
                                }
                            }
                            
                            KetQuaLastHighlightedViewId = viewPlan.Id;
                            
                            RevitDoc.Regenerate();
                            t.Commit();
                        }
                            
                        if (SharedPreviewControl is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                            
                        SharedPreviewControl = new PreviewControl(RevitDoc, viewPlan.Id);
                        setPreviewControlAction?.Invoke(SharedPreviewControl);
                    }
                    else
                    {
                        _dialogService.ShowWarning("Không tìm thấy mặt bằng 2D nào phù hợp để hiển thị.", "Thông báo");
                    }
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowWarning($"Lỗi khi tạo hình ảnh 2D: {ex.Message}", "Lỗi");
            }
        }


        private void AutoDesignFormwork()
        {
            var p = new Models.CalculationParameters
            {
                WoodStrength = WoodStrength > 0 ? WoodStrength : 15.0,
                PlywoodBendingStrength = PlywoodBendingStrength > 0 ? PlywoodBendingStrength : 10.0,
                WoodShearStrength = WoodShearStrength > 0 ? WoodShearStrength : 2.0,
                ElasticModulus = ElasticModulus,
                ConcreteWeight = ConcreteWeight > 0 ? ConcreteWeight : 25.0,
                WoodFormworkWeight = WoodFormworkWeight,
                ConstructionLiveLoad = ConstructionLiveLoad,
                DynamicImpactFactor = DynamicImpactFactor > 0 ? DynamicImpactFactor : 1.3,
                DeadLoadSafetyFactor = DeadLoadSafetyFactor,
                LiveLoadSafetyFactor = LiveLoadSafetyFactor,
                MoistureFactor = MoistureFactor,
                TemperatureFactor = TemperatureFactor,
                SlabDeflectionLimitRatio = Models.FormworkCalculationService.ParseFraction(SlabDeflectionLimit),
                JoistDeflectionLimitRatio = Models.FormworkCalculationService.ParseFraction(JoistDeflectionLimit),
                SlabThickness = SlabThickness,
                VanMatThickness = VanMatThickness,
                XaGoPhuWidth = XaGoPhuWidth,
                XaGoPhuHeight = XaGoPhuHeight,
                XaGoChinhWidth = XaGoChinhWidth,
                XaGoChinhHeight = XaGoChinhHeight,
                CayChongDiameter = CayChongDiameter,
                CayChongThickness = CayChongThickness,
                CayChongHeight = CayChongHeight,
                SoNhipVanMat = SoNhipVanMat,
                SoNhipXaGoPhu = SoNhipXaGoPhu,
                SoNhipXaGoChinh = SoNhipXaGoChinh
            };

            var res = Models.FormworkCalculationService.AutoDesign(p);
            Joist1Spacing = res.L1;
            Joist2Spacing = res.L2;
            SupportSpacing = res.L3;

            ExecuteUpdateResults(false);
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

            if (name == nameof(CurrentStoryHeight) || name == nameof(SlabThickness) || name == nameof(VanMatThickness) ||
                name == nameof(XaGoPhuHeight) || name == nameof(XaGoChinhHeight))
            {
                if (CurrentStoryHeight > 0)
                {
                    double calculatedHeight = CurrentStoryHeight - SlabThickness - XaGoPhuHeight - XaGoChinhHeight - VanMatThickness;
                    if (calculatedHeight > 0 && Math.Abs(_cayChongHeight - calculatedHeight) > 0.01)
                    {
                        CayChongHeight = calculatedHeight;
                    }
                }
            }

            // Tự động cập nhật kết quả khi các thông số thay đổi
            if (name == nameof(WoodStrength) || name == nameof(ElasticModulus) ||
                name == nameof(SlabDeflectionLimit) || name == nameof(JoistDeflectionLimit) ||
                name == nameof(WoodFormworkWeight) || name == nameof(ConcreteWeight) ||
                name == nameof(XaGoChinhWidth) || name == nameof(XaGoChinhHeight) ||
                name == nameof(CayChongDiameter) || name == nameof(CayChongThickness) || name == nameof(CayChongHeight) ||
                name == nameof(SoNhipVanMat) || name == nameof(SoNhipXaGoPhu) || name == nameof(SoNhipXaGoChinh) ||
                name == nameof(Joist1Spacing) || name == nameof(Joist2Spacing) || name == nameof(SupportSpacing) ||
                name == nameof(SlabThickness) || name == nameof(VanMatThickness) ||
                name == nameof(XaGoPhuWidth) || name == nameof(XaGoPhuHeight) ||
                name == nameof(XaGoChinhWidth) || name == nameof(XaGoChinhHeight) ||
                name == nameof(CayChongDiameter) || name == nameof(CayChongThickness))
            {
                ExecuteUpdateResults(false);
            }
        }

        public void ProcessSelectedBeam(System.Collections.Generic.IList<Reference> references, bool appendSelection = false)
        {
            if (references != null && references.Count > 0)
            {
                if (!IsSelectingComponent && !IsSweepSelectingComponent)
                    SetSelectedComponentComposition(0, 0, references.Count, 0);
                string maxFamilyName = "";
                double maxWidth = 0;
                double maxDepth = 0;
                double minPropHeightMm = double.MaxValue;
                var beamModels = new List<FloorElementModel>();

                foreach (var refer in references)
                {
                    Element element = RevitDoc.GetElement(refer.ElementId);
                    if (element is FamilyInstance fi)
                    {
                        double b = 200, h = 400; // default
                        Parameter bParam = fi.Symbol.LookupParameter("b") ?? fi.Symbol.LookupParameter("Width") ?? fi.Symbol.LookupParameter("B");
                        Parameter hParam = fi.Symbol.LookupParameter("h") ?? fi.Symbol.LookupParameter("Depth") ?? fi.Symbol.LookupParameter("H");
                        
                        if (bParam != null) b = bParam.AsDouble() * 304.8;
                        if (hParam != null) h = hParam.AsDouble() * 304.8;

                        string levelName = "Unknown Level";
                        
                        // Check level for prop height
                        Level level = RevitDoc.GetElement(fi.LevelId) as Level;
                        if (level != null)
                        {
                            levelName = level.Name;
                            double currentElevation = level.Elevation;
                            var allLevels = new FilteredElementCollector(RevitDoc)
                                .OfClass(typeof(Level))
                                .Cast<Level>()
                                .OrderBy(l => l.Elevation)
                                .ToList();
                            
                            Level levelBelow = allLevels.LastOrDefault(l => l.Elevation < currentElevation - 0.01);
                            if (levelBelow != null)
                            {
                                double floorToFloorHeightMm = (currentElevation - levelBelow.Elevation) * 304.8;
                                double joistSystemHeightMm = BeamBottomPlywoodThickness + XaGoChinhHeight; // approx
                                double computedPropHeightMm = floorToFloorHeightMm - h - joistSystemHeightMm;
                                if (computedPropHeightMm > 0 && computedPropHeightMm < minPropHeightMm)
                                {
                                    minPropHeightMm = computedPropHeightMm;
                                }
                            }
                        }

                        double lengthM = 0;
                        Parameter lengthParam = fi.get_Parameter(BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH);
                        if (lengthParam != null) lengthM = lengthParam.AsDouble() * 0.3048;

                        beamModels.Add(new FloorElementModel
                        {
                            UniqueId = fi.UniqueId,
                            ElementId = fi.Id.ToString(),
                            ComponentType = StructuralComponentType.Beam,
                            FamilyTypeName = fi.Name,
                            Level = levelName,
                            Area = lengthM * (b + 2 * h) / 1000.0,
                            Perimeter = lengthM,
                            Elevation = b,
                            Thickness = h,
                            StoryHeight = Math.Max(100, lengthM * 1000.0),
                            ComponentSummary = $"Dầm {b:F0}×{h:F0} mm · L={lengthM * 1000.0:F0} mm"
                        });

                        if (h > maxDepth || (h == maxDepth && b > maxWidth))
                        {
                            maxDepth = h;
                            maxWidth = b;
                            maxFamilyName = fi.Name;
                        }
                    }
                }

                if (beamModels.Count > 0)
                {
                    StoreSelectedComponentModels(beamModels, appendSelection);
                }

                if (maxWidth > 0 && maxDepth > 0)
                {
                    BeamWidth = maxWidth;
                    BeamDepth = maxDepth;
                    SelectedFloorDetailName = maxFamilyName;

                    if (minPropHeightMm != double.MaxValue && minPropHeightMm > 0)
                    {
                        if (minPropHeightMm < 1500.0 || minPropHeightMm > 6000.0) CayChongHeight = 3600.0;
                        else CayChongHeight = minPropHeightMm;
                    }

                    RevitVersionStatus = $"Đã chọn {references.Count} dầm | Tiết diện nguy hiểm nhất: {maxWidth:F0}x{maxDepth:F0} mm";
                    IsCalculated = true;
                    ExecuteUpdateResults(false);
                }
                PublishComponentSelectionContext(maxFamilyName);
            }
        }

        public void ProcessSelectedColumn(System.Collections.Generic.IList<Reference> references, bool appendSelection = false)
        {
            if (references != null && references.Count > 0)
            {
                if (!IsSelectingComponent && !IsSweepSelectingComponent)
                    SetSelectedComponentComposition(0, 0, 0, references.Count);
                string maxFamilyName = "";
                double maxWidth = 0;
                double maxDepth = 0;
                double maxHeight = 0;
                string firstAssessmentKey = null;
                
                var columnModels = new System.Collections.Generic.List<FloorElementModel>();

                foreach (var refer in references)
                {
                    Element element = RevitDoc.GetElement(refer.ElementId);
                    if (element is FamilyInstance fi)
                    {
                        if (firstAssessmentKey == null)
                        {
                            _selectedColumnId = fi.Id.Value;
                            firstAssessmentKey = ColumnBraceAssessment.BuildCacheKey(fi.Id.Value, fi.LevelId.Value);
                        }
                        double b = 400, h = 400, height = 3600;
                        Parameter bParam = fi.Symbol.LookupParameter("b") ?? fi.Symbol.LookupParameter("Width") ?? fi.Symbol.LookupParameter("B");
                        Parameter hParam = fi.Symbol.LookupParameter("h") ?? fi.Symbol.LookupParameter("Depth") ?? fi.Symbol.LookupParameter("H");
                        
                        if (bParam != null) b = bParam.AsDouble() * 304.8;
                        if (hParam != null) h = hParam.AsDouble() * 304.8;

                        BoundingBoxXYZ bbox = fi.get_BoundingBox(null);
                        if (bbox != null)
                        {
                            height = (bbox.Max.Z - bbox.Min.Z) * 304.8;
                        }

                        double area = b * h;
                        double maxArea = maxWidth * maxDepth;

                        if (area > maxArea || (area == maxArea && height > maxHeight))
                        {
                            maxWidth = b;
                            maxDepth = h;
                            maxHeight = height;
                            maxFamilyName = fi.Name;
                        }

                        string levelName = "Không xác định";
                        if (fi.LevelId != ElementId.InvalidElementId)
                        {
                            Level level = RevitDoc.GetElement(fi.LevelId) as Level;
                            if (level != null) levelName = level.Name;
                        }

                        columnModels.Add(new FloorElementModel
                        {
                            UniqueId = fi.UniqueId,
                            ElementId = fi.Id.ToString(),
                            ComponentType = StructuralComponentType.Column,
                            FamilyTypeName = fi.Name,
                            Level = levelName,
                            Area = (b * h) / 1000000.0,
                            Elevation = b,
                            Thickness = h,
                            StoryHeight = height,
                            ComponentSummary = $"Cột {b:F0}×{h:F0} mm · H={height:F0} mm"
                        });
                    }
                }

                if (columnModels.Count > 0)
                {
                    StoreSelectedComponentModels(columnModels, appendSelection);
                }

                if (maxWidth > 0 && maxDepth > 0)
                {
                    SelectedColumnBraceAssessmentKey = firstAssessmentKey;
                    ColumnWidth = maxWidth;
                    ColumnDepth = maxDepth;
                    ColumnHeight = maxHeight > 0 ? maxHeight : 3600;
                    SelectedFloorDetailName = maxFamilyName;

                    RevitVersionStatus = $"Đã chọn {references.Count} cột | Tiết diện lớn nhất: {maxWidth:F0}x{maxDepth:F0} mm, H = {maxHeight:F0} mm";
                    IsCalculated = true;
                    ExecuteUpdateResults(false);
                }
                PublishComponentSelectionContext(maxFamilyName);
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
                    case nameof(CayChongHeight):
                    case nameof(SupportEffectiveLengthFactor):
                    case nameof(SupportRatedCapacity):
                    case nameof(SupportSlendernessLimit):
                    case nameof(SupportBasePlateWidth):
                    case nameof(SupportBasePlateDepth):
                    case nameof(SupportBaseBearingAllowable):
                    case nameof(SupportSolePlateWidth):
                    case nameof(SupportSolePlateDepth):
                    case nameof(SupportGroundBearingAllowable):
                    case nameof(SupportHeadPlateWidth):
                    case nameof(SupportHeadPlateDepth):
                    case nameof(SupportHeadBearingAllowable):
                    case nameof(SupportJackExtensionLimit):
                        var valueObj = GetType().GetProperty(columnName)?.GetValue(this);
                        if (valueObj is double val && val <= 0)
                        {
                            error = "Giá trị phải là một số thực dương.";
                        }
                        break;
                    case nameof(SlabDeflectionLimit):
                    case nameof(JoistDeflectionLimit):
                        string valLimit = (string)GetType().GetProperty(columnName)?.GetValue(this);
                        if (Models.FormworkCalculationService.ParseFraction(valLimit) <= 0)
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
