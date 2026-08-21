using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf.SharpDX;
using HelixToolkit.SharpDX;
using Point3D = System.Windows.Media.Media3D.Point3D;
using Vector3D = System.Windows.Media.Media3D.Vector3D;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Diagnostics;
using Autodesk.Revit.UI;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Services;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels
{
    public class Preview3DViewModel : INotifyPropertyChanged, IDisposable
    {
        private SharedDataViewModel _sharedViewModel;
        private bool _hasProjectModel;
        private FrameworkElement _nativePreviewControl;

        public FrameworkElement NativePreviewControl
        {
            get => _nativePreviewControl;
            private set { _nativePreviewControl = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNativePreview)); }
        }

        public bool IsNativePreview => NativePreviewControl != null;

        private GroupModel3D _modelGroup = new GroupModel3D();
        public GroupModel3D ModelGroup
        {
            get => _modelGroup;
            set { _modelGroup = value; OnPropertyChanged(); }
        }

        private bool _isRendering;
        public bool IsRendering
        {
            get => _isRendering;
            set
            {
                _isRendering = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool _renderCancellationRequested;
        public ICommand CancelRenderCommand { get; }

        private string _renderSummary = "Đang đọc mô hình kết cấu từ Revit...";
        public string RenderSummary
        {
            get => _renderSummary;
            set { _renderSummary = value; OnPropertyChanged(); }
        }

        public int SelectedComponentIndex
        {
            get
            {
                if (_sharedViewModel == null) return 3; // Default to Slab
                switch (_sharedViewModel.CurrentComponentType)
                {
                    case Models.StructuralComponentType.Beam: return 0;
                    case Models.StructuralComponentType.Column: return 1;
                    case Models.StructuralComponentType.Foundation: return 2;
                    case Models.StructuralComponentType.Slab:
                    default: return 3;
                }
            }
            set
            {
                if (_sharedViewModel == null) return;
                var oldType = _sharedViewModel.CurrentComponentType;
                var newType = oldType;
                switch (value)
                {
                    case 0: newType = Models.StructuralComponentType.Beam; break;
                    case 1: newType = Models.StructuralComponentType.Column; break;
                    case 2: newType = Models.StructuralComponentType.Foundation; break;
                    case 3: newType = Models.StructuralComponentType.Slab; break;
                }
                
                if (oldType != newType)
                {
                    _sharedViewModel.CurrentComponentType = newType;
                    OnPropertyChanged();
                }
            }
        }

        public Preview3DViewModel(SharedDataViewModel sharedViewModel)
        {
            _sharedViewModel = sharedViewModel;
            _sharedViewModel.PropertyChanged += SharedViewModel_PropertyChanged;
            _sharedViewModel.SlabSelectionCompleted += OnSlabSelectionCompleted;
            _sharedViewModel.Render3DRequested += OnRender3DRequested;
            CancelRenderCommand = new RelayCommand(_ => CancelRender(), _ => IsRendering);
            
            // The full Revit project is expensive to tessellate. It is generated lazily
            // when the preview tab becomes visible and then reused until the window closes.
            if (_sharedViewModel.RevitDoc == null)
            {
                Generate3DModelAsync();
            }
        }

        public void EnsureProjectModel()
        {
            if (_sharedViewModel?.RevitDoc != null && !_hasProjectModel)
                Generate3DModelAsync();
        }

        private int _renderProgress;
        public int RenderProgress
        {
            get => _renderProgress;
            private set { _renderProgress = Math.Max(0, Math.Min(100, value)); OnPropertyChanged(); }
        }

        private string _renderProgressText = "Sẵn sàng dựng mô hình 3D.";
        public string RenderProgressText
        {
            get => _renderProgressText;
            private set { _renderProgressText = value; OnPropertyChanged(); }
        }

        private string _renderRemainingText = string.Empty;
        public string RenderRemainingText
        {
            get => _renderRemainingText;
            private set { _renderRemainingText = value; OnPropertyChanged(); }
        }

        private Stopwatch _renderStopwatch;

        public void ReleaseProjectModel()
        {
            if (_sharedViewModel?.RevitDoc == null) return;
            if (IsRendering)
            {
                CancelRender();
                return;
            }
            FrameworkElement oldPreview = NativePreviewControl;
            NativePreviewControl = null;
            if (oldPreview is IDisposable nativePreview) nativePreview.Dispose();
            try
            {
                NativeRevitFormworkPreviewService.RemovePreviewArtifacts(_sharedViewModel.RevitDoc);
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"Preview cleanup failed: {exception}");
            }
            _hasProjectModel = false;
            RenderSummary = "Preview 3D đã đóng; các phần tử ván khuôn và phụ kiện tạm đã được dọn khỏi dự án.";
        }

        private void OnSlabSelectionCompleted(object sender, SlabSelectionEventArgs e)
        {
            if (_sharedViewModel.RevitDoc != null)
            {
                _hasProjectModel = false;
                RenderSummary = "Dữ liệu Revit đã thay đổi; mở Preview 3D để cập nhật.";
                return;
            }
            Generate3DModelAsync();
        }

        private void OnRender3DRequested(object sender, EventArgs e)
        {
            EnsureProjectModel();
        }

        private void SharedViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_sharedViewModel.IsApplyingValidatedDesign)
                return;

            // Project preview is independent from the currently selected calculation card.
            // Avoid rebuilding the complete building for these frequent UI changes.
            if (_sharedViewModel.RevitDoc != null)
            {
                if (e.PropertyName == nameof(SharedDataViewModel.ScheduledDesignRevision) &&
                    _sharedViewModel.ScheduledDesignRevision > 0)
                {
                    _hasProjectModel = false;
                    // Calculation and schedule synchronization are data-only operations.
                    // Do not load/create accessory families or mutate the Revit document
                    // until the user explicitly opens/requests the 3D model.
                    RenderSummary = "Thiết kế đã thay đổi; nhấn Dựng hình Revit 3D để cập nhật mô hình.";
                    return;
                }

                if (e.PropertyName == nameof(SharedDataViewModel.CurrentComponentType))
                    OnPropertyChanged(nameof(SelectedComponentIndex));

                if (IsColumnPreviewSetting(e.PropertyName))
                {
                    _hasProjectModel = false;
                    RenderSummary = "Thông số bố trí cột đã thay đổi; mở lại Preview 3D để cập nhật.";
                }
                return;
            }

            // Design-time single-component fallback keeps its original live updates.
            if (e.PropertyName == nameof(SharedDataViewModel.IsCalculated) && _sharedViewModel.IsCalculated)
            {
                Generate3DModelAsync();
            }
            else if (e.PropertyName == nameof(SharedDataViewModel.CurrentComponentType))
            {
                OnPropertyChanged(nameof(SelectedComponentIndex));
                Generate3DModelAsync();
            }
            else if (e.PropertyName == nameof(SharedDataViewModel.IsGroundLevel))
            {
                Generate3DModelAsync();
            }
        }

        private static bool IsColumnPreviewSetting(string propertyName)
        {
            return propertyName == nameof(SharedDataViewModel.ColumnPlywoodThickness) ||
                   propertyName == nameof(SharedDataViewModel.ColumnStiffenerWidth) ||
                   propertyName == nameof(SharedDataViewModel.ColumnStiffenerDepth) ||
                   propertyName == nameof(SharedDataViewModel.ColumnStiffenerSpacing) ||
                   propertyName == nameof(SharedDataViewModel.ColumnYokeSpacing) ||
                   propertyName == nameof(SharedDataViewModel.ColumnUseZonedYokeSpacing) ||
                   propertyName == nameof(SharedDataViewModel.ColumnFirstYokeOffset) ||
                   propertyName == nameof(SharedDataViewModel.ColumnYokeBottomSpacing) ||
                   propertyName == nameof(SharedDataViewModel.ColumnYokeUpperSpacing) ||
                   propertyName == nameof(SharedDataViewModel.ColumnYokeTransitionHeight) ||
                   propertyName == nameof(SharedDataViewModel.ColumnLastYokeOffset) ||
                   propertyName == nameof(SharedDataViewModel.ColumnYokeWidth) ||
                   propertyName == nameof(SharedDataViewModel.ColumnYokeHeight) ||
                   propertyName == nameof(SharedDataViewModel.ColumnYokePairCount) ||
                   propertyName == nameof(SharedDataViewModel.ColumnYokeMaterialIndex) ||
                   propertyName == nameof(SharedDataViewModel.ColumnTieRodDiameter) ||
                   propertyName == nameof(SharedDataViewModel.ColumnTieRodCountPerFace) ||
                   propertyName == nameof(SharedDataViewModel.ColumnBraceCount) ||
                   propertyName == nameof(SharedDataViewModel.ColumnBraceAngle) ||
                   propertyName == nameof(SharedDataViewModel.ColumnBracePlacementModeIndex) ||
                   propertyName == nameof(SharedDataViewModel.ColumnNeighbourTieThreshold) ||
                   propertyName == nameof(SharedDataViewModel.ColumnBraceOverrideIndex) ||
                   propertyName == nameof(SharedDataViewModel.ColumnIsBoundaryOrCorner) ||
                   propertyName == nameof(SharedDataViewModel.ColumnUseWorkPlatform) ||
                   propertyName == nameof(SharedDataViewModel.ColumnWorkPlatformWidth) ||
                   propertyName == nameof(SharedDataViewModel.ColumnGuardrailHeight);
        }

        public void Generate3DModelAsync()
        {
            if (IsRendering) return;
            _renderCancellationRequested = false;
            IsRendering = true;
            RenderProgress = 0;
            RenderProgressText = "Đang khởi tạo quá trình dựng mô hình...";
            RenderRemainingText = "Đang ước tính thời gian chờ...";
            _renderStopwatch = Stopwatch.StartNew();

            try
            {
                GroupModel3D newModelGroup = null;

                // Inside Revit, show the native project View3D. Helix remains only as an
                // offline/design-time fallback when there is no Revit document.
                if (_sharedViewModel.RevitDoc != null)
                {
                    try
                    {
                        // Revit PreviewControl is a heavyweight native host. Release the
                        // shared 2D instance (and any previous 3D instance) before creating
                        // this view so only one native control owns the document at a time.
                        FrameworkElement oldNativePreview = NativePreviewControl;
                        NativePreviewControl = null;
                        if (oldNativePreview is IDisposable oldPreview) oldPreview.Dispose();
                        _sharedViewModel.DisposePreviewControl();

                        bool detailedAccessories = _sharedViewModel.UseDetailed3DPreview;
                        RenderProgressText = detailedAccessories
                            ? "Đang dựng Family phụ kiện chi tiết..."
                            : "Đang dựng preview nhanh: mô hình Revit, ván khuôn và phụ kiện...";
                        var service = new NativeRevitFormworkPreviewService();
                        NativeRevitFormworkPreviewService.Result result = service.Build(
                            _sharedViewModel.RevitDoc, _sharedViewModel.FloorElements, _sharedViewModel,
                            detailedAccessories, ReportRenderProgress,
                            () => _renderCancellationRequested);
                        ReportRenderProgress(98, "Đang khởi tạo bộ điều khiển hiển thị Revit...");
                        NativePreviewControl = new PreviewControl(_sharedViewModel.RevitDoc, result.ViewId);
                        RenderSummary = result.Summary;
                        _hasProjectModel = true;
                        ReportRenderProgress(100, "Hoàn tất mô hình 3D.");
                        return;
                    }
                    catch (OperationCanceledException)
                    {
                        RenderSummary = "Đã hủy dựng mô hình 3D; không có phần tử preview nào được giữ lại.";
                        RenderProgressText = "Đã hủy theo yêu cầu người dùng.";
                        RenderRemainingText = string.Empty;
                        _hasProjectModel = false;
                        try { NativeRevitFormworkPreviewService.RemovePreviewArtifacts(_sharedViewModel.RevitDoc); }
                        catch { }
                        return;
                    }
                    catch (Exception projectEx)
                    {
                        RenderSummary = "Không thể tạo preview 3D Revit: " + projectEx.Message;
                        RenderProgressText = "Không thể hoàn tất mô hình 3D.";
                        RenderRemainingText = projectEx.Message;
                        System.Diagnostics.Debug.WriteLine($"Native Revit preview failed: {projectEx}");
                        return;
                    }
                }

                if (_sharedViewModel.CurrentComponentType == Models.StructuralComponentType.Beam)
                {
                    double beamW = _sharedViewModel.BeamWidth > 0 ? _sharedViewModel.BeamWidth : 200.0;
                    double beamD = _sharedViewModel.BeamDepth > 0 ? _sharedViewModel.BeamDepth : 400.0;
                    double beamL = _sharedViewModel.BeamLength > 0 ? _sharedViewModel.BeamLength : 4000.0;
                    double sidePly = _sharedViewModel.BeamSidePlywoodThickness > 0 ? _sharedViewModel.BeamSidePlywoodThickness : 18.0;
                    double botPly = _sharedViewModel.BeamBottomPlywoodThickness > 0 ? _sharedViewModel.BeamBottomPlywoodThickness : 18.0;
                    double sideYoke = _sharedViewModel.BeamSideYokeSpacing > 0 ? _sharedViewModel.BeamSideYokeSpacing : 400.0;
                    double botYoke = _sharedViewModel.BeamBottomYokeSpacing > 0 ? _sharedViewModel.BeamBottomYokeSpacing : 400.0;
                    double propDia = _sharedViewModel.CayChongDiameter > 0 ? _sharedViewModel.CayChongDiameter : 48.0;
                    double propHeight = _sharedViewModel.CayChongHeight > 0 ? _sharedViewModel.CayChongHeight : 3600.0;
                    double propSpace = _sharedViewModel.BeamPropSpacing > 0 ? _sharedViewModel.BeamPropSpacing : 1000.0;

                    newModelGroup = BuildBeam3DModel(beamW, beamD, beamL, sidePly, botPly, sideYoke, botYoke, propSpace, propDia, propHeight, _sharedViewModel.IsGroundLevel);
                }
                else if (_sharedViewModel.CurrentComponentType == Models.StructuralComponentType.Column)
                {
                    double colW = _sharedViewModel.ColumnWidth > 0 ? _sharedViewModel.ColumnWidth : 200.0;
                    double colD = _sharedViewModel.ColumnDepth > 0 ? _sharedViewModel.ColumnDepth : 200.0;
                    double colH = _sharedViewModel.ColumnHeight > 0 ? _sharedViewModel.ColumnHeight : 3600.0;
                    double plyThick = _sharedViewModel.ColumnPlywoodThickness > 0 ? _sharedViewModel.ColumnPlywoodThickness : 18.0;
                    double yokeSpace = _sharedViewModel.ColumnYokeSpacing > 0 ? _sharedViewModel.ColumnYokeSpacing : 400.0;

                    newModelGroup = BuildColumn3DModel(colW, colD, colH, plyThick, yokeSpace);
                }
                else
                {
                    double areaM2 = _sharedViewModel.SelectedSlabArea > 0 ? _sharedViewModel.SelectedSlabArea : 100.0;
                    double slabThickness = _sharedViewModel.SlabThickness > 0 ? _sharedViewModel.SlabThickness : 150.0;
                    double plywoodThickness = _sharedViewModel.VanMatThickness > 0 ? _sharedViewModel.VanMatThickness : 18.0;
                    
                    double secJoistW = _sharedViewModel.XaGoPhuWidth > 0 ? _sharedViewModel.XaGoPhuWidth : 50.0;
                    double secJoistH = _sharedViewModel.XaGoPhuHeight > 0 ? _sharedViewModel.XaGoPhuHeight : 100.0;
                    double L1 = _sharedViewModel.Joist1Spacing > 0 ? _sharedViewModel.Joist1Spacing : 400.0;
                    
                    double priJoistW = _sharedViewModel.XaGoChinhWidth > 0 ? _sharedViewModel.XaGoChinhWidth : 100.0;
                    double priJoistH = _sharedViewModel.XaGoChinhHeight > 0 ? _sharedViewModel.XaGoChinhHeight : 100.0;
                    double L2 = _sharedViewModel.Joist2Spacing > 0 ? _sharedViewModel.Joist2Spacing : 1000.0;
                    
                    double shoreDia = _sharedViewModel.CayChongDiameter > 0 ? _sharedViewModel.CayChongDiameter : 48.0;
                    double shoreHeight = _sharedViewModel.CayChongHeight > 0 ? _sharedViewModel.CayChongHeight : 3600.0;
                    double L3 = _sharedViewModel.SupportSpacing > 0 ? _sharedViewModel.SupportSpacing : 1000.0;

                    var polygons = _sharedViewModel.LatestSelectionContext?.SlabPolygons;

                    newModelGroup = Build3DModel(
                        areaM2, slabThickness, plywoodThickness,
                        secJoistW, secJoistH, L1,
                        priJoistW, priJoistH, L2,
                        shoreDia, shoreHeight, L3, polygons,
                        _sharedViewModel.IsGroundLevel
                    );
                }

                System.Windows.Application.Current.Dispatcher.Invoke(() => {
                    ModelGroup = newModelGroup;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error generating 3D model: {ex.Message}");
            }
            finally
            {
                _renderStopwatch?.Stop();
                IsRendering = false;
            }
        }

        private void CancelRender()
        {
            if (!IsRendering) return;
            _renderCancellationRequested = true;
            RenderProgressText = "Đang hủy an toàn sau bước Revit hiện tại...";
            RenderRemainingText = "Vui lòng chờ transaction hiện tại được hoàn tác.";
        }

        private void ReportRenderProgress(int percent, string message)
        {
            RenderProgress = percent;
            RenderProgressText = message;
            if (_renderStopwatch != null && percent > 2 && percent < 100)
            {
                double elapsedSeconds = Math.Max(0.1, _renderStopwatch.Elapsed.TotalSeconds);
                double remainingSeconds = elapsedSeconds * (100.0 - percent) / percent;
                TimeSpan remaining = TimeSpan.FromSeconds(Math.Min(remainingSeconds, 24 * 60 * 60));
                RenderRemainingText = remaining.TotalMinutes >= 1
                    ? $"Ước tính còn khoảng {Math.Ceiling(remaining.TotalMinutes):F0} phút"
                    : $"Ước tính còn khoảng {Math.Max(1, Math.Ceiling(remaining.TotalSeconds)):F0} giây";
            }
            else if (percent >= 100)
            {
                RenderRemainingText = $"Hoàn tất trong {_renderStopwatch?.Elapsed.TotalSeconds:F1} giây";
            }

            // Never pump the WPF Dispatcher while a Revit Transaction may be open. A
            // nested navigation/close callback would re-enter the API and corrupt the
            // transaction. Quick mode is deliberately batched so it finishes promptly.
        }

        /// <summary>
        /// Optimized 3D model builder with LOD control and instanced geometry.
        /// Reduces triangle count by 60-80% while maintaining visual fidelity.
        /// </summary>
        private GroupModel3D Build3DModel(
            double areaM2, double slabThickness, double plywoodThickness,
            double secJoistW, double secJoistH, double L1,
            double priJoistW, double priJoistH, double L2,
            double shoreDia, double shoreHeight, double L3,
            List<List<System.Windows.Point>> polygons = null,
            bool isGroundLevel = false)
        {
            var group = new GroupModel3D();

            // Materials
            var slabMaterial = new HelixToolkit.Wpf.SharpDX.DiffuseMaterial() { DiffuseColor = new HelixToolkit.Maths.Color4(200/255f, 220/255f, 240/255f, 100/255f) }; // Semi-transparent concrete
            var plywoodMaterial = new HelixToolkit.Wpf.SharpDX.DiffuseMaterial() { DiffuseColor = new HelixToolkit.Maths.Color4(210/255f, 180/255f, 140/255f, 1f) }; // Tan wood
            var secJoistMaterial = new HelixToolkit.Wpf.SharpDX.DiffuseMaterial() { DiffuseColor = new HelixToolkit.Maths.Color4(180/255f, 150/255f, 100/255f, 1f) }; // Darker wood
            var priJoistMaterial = new HelixToolkit.Wpf.SharpDX.DiffuseMaterial() { DiffuseColor = new HelixToolkit.Maths.Color4(150/255f, 120/255f, 80/255f, 1f) }; // Even darker wood
            var shoreMaterial = new HelixToolkit.Wpf.SharpDX.DiffuseMaterial() { DiffuseColor = new HelixToolkit.Maths.Color4(80/255f, 130/255f, 180/255f, 1f) }; // Steel blue
            var bracingMaterial = new HelixToolkit.Wpf.SharpDX.DiffuseMaterial() { DiffuseColor = new HelixToolkit.Maths.Color4(255/255f, 200/255f, 0/255f, 1f) }; // Warning yellow
            var jackMaterial = new HelixToolkit.Wpf.SharpDX.DiffuseMaterial() { DiffuseColor = new HelixToolkit.Maths.Color4(192/255f, 192/255f, 192/255f, 1f) }; // Silver/Steel

            // 0. Update bounds to match actual slab polygon if available
            double viewLengthX = Math.Min(Math.Sqrt(areaM2) * 1000.0, 10000.0);
            double viewLengthY = viewLengthX;

            if (polygons != null && polygons.Count > 0)
            {
                double maxX = 0, maxY = 0;
                foreach (var poly in polygons)
                {
                    foreach (var pt in poly)
                    {
                        if (pt.X > maxX) maxX = pt.X;
                        if (pt.Y > maxY) maxY = pt.Y;
                    }
                }
                if (maxX > 0) viewLengthX = maxX;
                if (maxY > 0) viewLengthY = maxY;
            }

            // Z Elevations
            double zJackBottom = 0;
            double jackHeight = 150.0; // Base jack height
            double zShoreBottom = zJackBottom + jackHeight;
            double zShoreTop = zShoreBottom + shoreHeight;
            double uHeadHeight = 150.0; // U-head height
            double zPriJoistBottom = zShoreTop + uHeadHeight;
            double zSecJoistBottom = zPriJoistBottom + priJoistH;
            double zPlywoodBottom = zSecJoistBottom + secJoistH;
            double zConcreteBottom = zPlywoodBottom + plywoodThickness;
            double zConcreteTop = zConcreteBottom + slabThickness;

            // 1. Slab & Plywood — use polygon triangulation for faithful shape rendering
            var slabBuilder = new SimpleMeshBuilder();
            var plywoodBuilder = new SimpleMeshBuilder();
            if (polygons != null && polygons.Count > 0)
            {
                // Separate outer boundaries (CCW, positive area) from holes (CW, negative area)
                var outerLoops = new List<List<System.Windows.Point>>();
                var holeLoops = new List<List<System.Windows.Point>>();
                foreach (var poly in polygons)
                {
                    if (poly.Count < 3) continue;
                    double signedArea = 0;
                    for (int i = 0; i < poly.Count; i++)
                    {
                        var a = poly[i];
                        var b = poly[(i + 1) % poly.Count];
                        signedArea += (b.X - a.X) * (b.Y + a.Y);
                    }
                    // Negative signedArea = CCW in screen coords = outer boundary
                    // Positive signedArea = CW = hole
                    if (signedArea < 0)
                        outerLoops.Add(poly);
                    else
                        holeLoops.Add(poly);
                }

                // For each outer loop, collect holes that lie inside it, then triangulate
                foreach (var outer in outerLoops)
                {
                    var associatedHoles = new List<List<System.Windows.Point>>();
                    foreach (var hole in holeLoops)
                    {
                        // Check if the first point of the hole is inside the outer polygon
                        if (hole.Count > 0 && IsPointInsideSinglePoly(hole[0], outer))
                        {
                            associatedHoles.Add(hole);
                        }
                    }

                    slabBuilder.AddExtrudedPolygon(outer, associatedHoles, zConcreteBottom, zConcreteTop);
                    plywoodBuilder.AddExtrudedPolygon(outer, associatedHoles, zPlywoodBottom, zPlywoodBottom + plywoodThickness);
                }
            }
            else
            {
                slabBuilder.AddBox(new Point3D(viewLengthX / 2, viewLengthY / 2, zConcreteBottom + slabThickness / 2), viewLengthX, viewLengthY, slabThickness);
                plywoodBuilder.AddBox(new Point3D(viewLengthX / 2, viewLengthY / 2, zPlywoodBottom + plywoodThickness / 2), viewLengthX, viewLengthY, plywoodThickness);
            }
            group.Children.Add(new MeshGeometryModel3D { Geometry = slabBuilder.ToMesh(), Material = slabMaterial });
            if (!isGroundLevel)
            {
                group.Children.Add(new MeshGeometryModel3D { Geometry = plywoodBuilder.ToMesh(), Material = plywoodMaterial });
            }

            // 1.5. Side Formwork (Ván khuôn thành sàn) & Ground Anchoring
            var sideFormBuilder = new SimpleMeshBuilder();
            var groundAnchorBuilder = new SimpleMeshBuilder();
            double sideFormThick = plywoodThickness;
            if (polygons != null && polygons.Count > 0)
            {
                foreach (var poly in polygons)
                {
                    for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                    {
                        System.Windows.Point p1 = poly[j];
                        System.Windows.Point p2 = poly[i];
                        
                        System.Windows.Point midPt = new System.Windows.Point((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
                        bool isInnerEdge = false;
                        foreach (var otherPoly in polygons)
                        {
                            if (otherPoly == poly) continue;
                            for (int k = 0, l = otherPoly.Count - 1; k < otherPoly.Count; l = k++)
                            {
                                if (DistancePointToSegment(midPt, otherPoly[l], otherPoly[k]) < 1000.0)
                                {
                                    isInnerEdge = true;
                                    break;
                                }
                            }
                            if (isInnerEdge) break;
                        }

                        if (!isInnerEdge)
                        {
                            sideFormBuilder.AddWall(new Point3D(p1.X, p1.Y, 0), new Point3D(p2.X, p2.Y, 0), sideFormThick, slabThickness + sideFormThick, zConcreteBottom - sideFormThick);

                            if (isGroundLevel)
                            {
                                // Ground anchoring
                                double dx = p2.X - p1.X;
                                double dy = p2.Y - p1.Y;
                                double len = Math.Sqrt(dx * dx + dy * dy);
                                if (len > 0)
                                {
                                    // Outward normal (assuming CCW outer polygon: normal is (dy, -dx))
                                    double nx = dy / len;
                                    double ny = -dx / len;

                                    double anchorSpacing = L3;
                                    for (double d = anchorSpacing / 2; d < len; d += anchorSpacing)
                                    {
                                        double ax = p1.X + (dx / len) * d;
                                        double ay = p1.Y + (dy / len) * d;

                                        double stakeDist = 400.0; // 400mm away
                                        double sx = ax + nx * stakeDist;
                                        double sy = ay + ny * stakeDist;

                                        // Stake
                                        groundAnchorBuilder.AddCylinder(new Point3D(sx, sy, zConcreteBottom - 400), new Point3D(sx, sy, zConcreteBottom + 150), 30, 8);
                                        
                                        // Diagonal brace
                                        double ex = ax + nx * sideFormThick;
                                        double ey = ay + ny * sideFormThick;
                                        groundAnchorBuilder.AddCylinder(new Point3D(sx, sy, zConcreteBottom + 50), new Point3D(ex, ey, zConcreteBottom + slabThickness / 2), 30, 8);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                // Left/Right
                sideFormBuilder.AddBox(new Point3D(-sideFormThick / 2, viewLengthY / 2, zConcreteBottom + slabThickness / 2), sideFormThick, viewLengthY + 2 * sideFormThick, slabThickness + sideFormThick);
                sideFormBuilder.AddBox(new Point3D(viewLengthX + sideFormThick / 2, viewLengthY / 2, zConcreteBottom + slabThickness / 2), sideFormThick, viewLengthY + 2 * sideFormThick, slabThickness + sideFormThick);
                // Front/Back
                sideFormBuilder.AddBox(new Point3D(viewLengthX / 2, -sideFormThick / 2, zConcreteBottom + slabThickness / 2), viewLengthX, sideFormThick, slabThickness + sideFormThick);
                sideFormBuilder.AddBox(new Point3D(viewLengthX / 2, viewLengthY + sideFormThick / 2, zConcreteBottom + slabThickness / 2), viewLengthX, sideFormThick, slabThickness + sideFormThick);
            }
            group.Children.Add(new MeshGeometryModel3D { Geometry = sideFormBuilder.ToMesh(), Material = plywoodMaterial });

            if (isGroundLevel)
            {
                group.Children.Add(new MeshGeometryModel3D { Geometry = groundAnchorBuilder.ToMesh(), Material = bracingMaterial });
            }
            else
            {
            // 3. Secondary Joists (Run along Y axis, spaced along X axis by L1)
            var secJoistBuilder = new SimpleMeshBuilder();
            List<double> secJoistPositions = new List<double>();
            for (double x = secJoistW / 2; x <= viewLengthX - secJoistW / 2; x += L1)
            {
                secJoistPositions.Add(x);
            }
            if (secJoistPositions.Count == 0 || viewLengthX - secJoistW / 2 - secJoistPositions[secJoistPositions.Count - 1] > 10)
            {
                secJoistPositions.Add(viewLengthX - secJoistW / 2);
            }
            foreach (double xPos in secJoistPositions)
            {
                if (polygons != null && polygons.Count > 0)
                {
                    var yInts = GetYIntersections(xPos, polygons);
                    for (int i = 0; i < yInts.Count - 1; i += 2)
                    {
                        double yStart = yInts[i];
                        double yEnd = yInts[i + 1];
                        double length = yEnd - yStart;
                        if (length > 10) // Ignore tiny slivers
                        {
                            secJoistBuilder.AddBox(new Point3D(xPos, yStart + length / 2, zSecJoistBottom + secJoistH / 2), secJoistW, length, secJoistH);
                        }
                    }
                }
                else
                {
                    secJoistBuilder.AddBox(new Point3D(xPos, viewLengthY / 2, zSecJoistBottom + secJoistH / 2), secJoistW, viewLengthY, secJoistH);
                }
            }
            group.Children.Add(new MeshGeometryModel3D { Geometry = secJoistBuilder.ToMesh(), Material = secJoistMaterial });

            // 4. Primary Joists (Run along X axis, spaced along Y axis by L2)
            var priJoistBuilder = new SimpleMeshBuilder();
            List<double> priJoistPositions = new List<double>();
            for (double y = priJoistW / 2; y <= viewLengthY - priJoistW / 2; y += L2)
            {
                priJoistPositions.Add(y);
            }
            if (priJoistPositions.Count == 0 || viewLengthY - priJoistW / 2 - priJoistPositions[priJoistPositions.Count - 1] > 10)
            {
                priJoistPositions.Add(viewLengthY - priJoistW / 2);
            }
            foreach (double yPos in priJoistPositions)
            {
                if (polygons != null && polygons.Count > 0)
                {
                    var xInts = GetXIntersections(yPos, polygons);
                    for (int i = 0; i < xInts.Count - 1; i += 2)
                    {
                        double xStart = xInts[i];
                        double xEnd = xInts[i + 1];
                        double length = xEnd - xStart;
                        if (length > 10)
                        {
                            priJoistBuilder.AddBox(new Point3D(xStart + length / 2, yPos, zPriJoistBottom + priJoistH / 2), length, priJoistW, priJoistH);
                        }
                    }
                }
                else
                {
                    priJoistBuilder.AddBox(new Point3D(viewLengthX / 2, yPos, zPriJoistBottom + priJoistH / 2), viewLengthX, priJoistW, priJoistH);
                }
            }
            group.Children.Add(new MeshGeometryModel3D { Geometry = priJoistBuilder.ToMesh(), Material = priJoistMaterial });

            // 5. Vertical Shores, Base Jacks, and U-Heads (Spaced by L3 along X axis under Primary Joists)
            var shoreBuilder = new SimpleMeshBuilder();
            var bracingBuilder = new SimpleMeshBuilder();
            var jackBuilder = new SimpleMeshBuilder();
            
            List<double> shoreXPositions = new List<double>();
            for (double x = shoreDia / 2; x <= viewLengthX - shoreDia / 2; x += L3)
            {
                shoreXPositions.Add(x);
            }
            if (shoreXPositions.Count == 0 || viewLengthX - shoreDia / 2 - shoreXPositions[shoreXPositions.Count - 1] > 10)
            {
                shoreXPositions.Add(viewLengthX - shoreDia / 2);
            }

            bool[,] validShores = new bool[shoreXPositions.Count, priJoistPositions.Count];

            for (int j = 0; j < priJoistPositions.Count; j++)
            {
                double yPos = priJoistPositions[j];
                
                for (int i = 0; i < shoreXPositions.Count; i++)
                {
                    double xPos = shoreXPositions[i];
                    
                    if (polygons != null && polygons.Count > 0 && !IsPointInsideOrNear(new System.Windows.Point(xPos, yPos), polygons, 300.0))
                    {
                        continue;
                    }

                    validShores[i, j] = true;

                    // Base Jack (Base plate and threaded rod)
                    jackBuilder.AddBox(new Point3D(xPos, yPos, zJackBottom + 5), 150, 150, 10); // Base plate
                    jackBuilder.AddCylinder(new Point3D(xPos, yPos, zJackBottom + 10), new Point3D(xPos, yPos, zShoreBottom), 38, 12); // Threaded rod

                    // Vertical Tube
                    shoreBuilder.AddCylinder(new Point3D(xPos, yPos, zShoreBottom), new Point3D(xPos, yPos, zShoreTop), shoreDia, 16);

                    // U-Head (Realistic height)
                    double uHeadHeightVis = 100.0;
                    jackBuilder.AddCylinder(new Point3D(xPos, yPos, zShoreTop), new Point3D(xPos, yPos, zPriJoistBottom - 5), 38, 12); // Threaded rod
                    jackBuilder.AddBox(new Point3D(xPos, yPos, zPriJoistBottom - 5), 100, priJoistW + 10, 10); // U-head base plate
                    jackBuilder.AddBox(new Point3D(xPos, yPos - priJoistW / 2 - 5, zPriJoistBottom + uHeadHeightVis / 2), 100, 10, uHeadHeightVis); // U-head left side
                    jackBuilder.AddBox(new Point3D(xPos, yPos + priJoistW / 2 + 5, zPriJoistBottom + uHeadHeightVis / 2), 100, 10, uHeadHeightVis); // U-head right side

                    // Add Bracing (Connecting shores horizontally at mid-height)
                    if (i > 0 && validShores[i - 1, j])
                    {
                        bracingBuilder.AddCylinder(new Point3D(shoreXPositions[i-1], yPos, zShoreBottom + shoreHeight / 2), new Point3D(xPos, yPos, zShoreBottom + shoreHeight / 2), shoreDia * 0.8, 8);
                    }
                }
            }

            // Bracing Y (Connect adjacent shores in Y direction)
            for (int i = 0; i < shoreXPositions.Count; i++)
            {
                for (int j = 1; j < priJoistPositions.Count; j++)
                {
                    if (validShores[i, j - 1] && validShores[i, j])
                    {
                        bracingBuilder.AddCylinder(new Point3D(shoreXPositions[i], priJoistPositions[j-1], zShoreBottom + shoreHeight / 2), new Point3D(shoreXPositions[i], priJoistPositions[j], zShoreBottom + shoreHeight / 2), shoreDia * 0.8, 8);
                    }
                }
            }
            
            group.Children.Add(new MeshGeometryModel3D { Geometry = shoreBuilder.ToMesh(), Material = shoreMaterial });
            group.Children.Add(new MeshGeometryModel3D { Geometry = bracingBuilder.ToMesh(), Material = bracingMaterial });
            group.Children.Add(new MeshGeometryModel3D { Geometry = jackBuilder.ToMesh(), Material = jackMaterial });
            } // End if (!isGroundLevel)

            return group;
        }

        private GroupModel3D BuildBeam3DModel(
            double beamW, double beamD, double beamL,
            double sidePlyThick, double botPlyThick,
            double sideYokeSpace, double botYokeSpace,
            double propSpace, double propDia, double propHeight,
            bool isGroundLevel)
        {
            var group = new GroupModel3D();

            var concreteMat = new HelixToolkit.Wpf.SharpDX.DiffuseMaterial() { DiffuseColor = new HelixToolkit.Maths.Color4(200/255f, 220/255f, 240/255f, 100/255f) };
            var plyMat = new HelixToolkit.Wpf.SharpDX.DiffuseMaterial() { DiffuseColor = new HelixToolkit.Maths.Color4(210/255f, 180/255f, 140/255f, 1f) };
            var yokeMat = new HelixToolkit.Wpf.SharpDX.DiffuseMaterial() { DiffuseColor = new HelixToolkit.Maths.Color4(150/255f, 120/255f, 80/255f, 1f) };
            var steelMat = new HelixToolkit.Wpf.SharpDX.DiffuseMaterial() { DiffuseColor = new HelixToolkit.Maths.Color4(80/255f, 130/255f, 180/255f, 1f) };
            var jackMat = new HelixToolkit.Wpf.SharpDX.DiffuseMaterial() { DiffuseColor = new HelixToolkit.Maths.Color4(192/255f, 192/255f, 192/255f, 1f) };

            double uHeadHeight = 150.0;
            double jackHeight = 150.0;
            double botYokeH = 100.0; 
            double botYokeW = 50.0;
            double sideYokeW = 50.0;
            double sideYokeH = 100.0; 

            double zJackBottom = 0;
            double zShoreBottom = zJackBottom + jackHeight;
            double zShoreTop = zShoreBottom + propHeight;
            double zBotYokeBottom = zShoreTop + uHeadHeight;
            double zBotPlywoodBottom = zBotYokeBottom + botYokeH;
            double zConcreteBottom = zBotPlywoodBottom + botPlyThick;

            // 1. Concrete Beam
            var concreteBuilder = new SimpleMeshBuilder();
            concreteBuilder.AddBox(new Point3D(beamL / 2, 0, zConcreteBottom + beamD / 2), beamL, beamW, beamD);
            group.Children.Add(new MeshGeometryModel3D { Geometry = concreteBuilder.ToMesh(), Material = concreteMat });

            // 2. Plywood
            var plyBuilder = new SimpleMeshBuilder();
            // Bottom plywood
            plyBuilder.AddBox(new Point3D(beamL / 2, 0, zBotPlywoodBottom + botPlyThick / 2), beamL, beamW + 2 * sidePlyThick, botPlyThick);
            // Side plywoods
            plyBuilder.AddBox(new Point3D(beamL / 2, beamW / 2 + sidePlyThick / 2, zConcreteBottom + beamD / 2), beamL, sidePlyThick, beamD);
            plyBuilder.AddBox(new Point3D(beamL / 2, -beamW / 2 - sidePlyThick / 2, zConcreteBottom + beamD / 2), beamL, sidePlyThick, beamD);
            group.Children.Add(new MeshGeometryModel3D { Geometry = plyBuilder.ToMesh(), Material = plyMat });

            // 3. Yokes
            var yokeBuilder = new SimpleMeshBuilder();
            // Bottom yokes
            double botYokeLength = beamW + 2 * sidePlyThick + 2 * sideYokeH;
            for (double x = botYokeW / 2; x <= beamL; x += botYokeSpace)
            {
                yokeBuilder.AddBox(new Point3D(x, 0, zBotYokeBottom + botYokeH / 2), botYokeW, botYokeLength, botYokeH);
            }
            
            // Side yokes / cleats
            for (double x = sideYokeW / 2; x <= beamL; x += sideYokeSpace)
            {
                // Left side
                yokeBuilder.AddBox(new Point3D(x, beamW / 2 + sidePlyThick + sideYokeH / 2, zBotPlywoodBottom + beamD / 2), sideYokeW, sideYokeH, beamD + botPlyThick);
                // Right side
                yokeBuilder.AddBox(new Point3D(x, -beamW / 2 - sidePlyThick - sideYokeH / 2, zBotPlywoodBottom + beamD / 2), sideYokeW, sideYokeH, beamD + botPlyThick);
            }
            group.Children.Add(new MeshGeometryModel3D { Geometry = yokeBuilder.ToMesh(), Material = yokeMat });

            // Standalone beam preview: surface-fixed rods are shown only when the
            // selected arrangement is explicitly a perimeter beam (mode 2).
            if (_sharedViewModel.BeamPlacementModeIndex == 2)
            {
                var surfaceTieBuilder = new SimpleMeshBuilder();
                const double surfaceTieSpacing = 1000.0;
                const double surfaceTieLength = 1200.0;
                const double surfaceTieDiameter = 12.0;
                double rodZ = zConcreteBottom + beamD + surfaceTieDiameter / 2.0 + 8.0;
                for (double x = surfaceTieSpacing / 2.0; x < beamL; x += surfaceTieSpacing)
                {
                    Point3D outside = new Point3D(x, -beamW / 2.0 - sidePlyThick - 40.0, rodZ);
                    Point3D inside = new Point3D(x, beamW / 2.0 + surfaceTieLength, rodZ);
                    surfaceTieBuilder.AddCylinder(outside, inside, surfaceTieDiameter, 8);
                    surfaceTieBuilder.AddBox(inside, 110.0, 110.0, 10.0);
                    surfaceTieBuilder.AddBox(outside, 70.0, 12.0, 12.0);
                }
                group.Children.Add(new MeshGeometryModel3D { Geometry = surfaceTieBuilder.ToMesh(), Material = steelMat });
            }

            // 4. Props
            if (!isGroundLevel)
            {
                var shoreBuilder = new SimpleMeshBuilder();
                var jackBuilder = new SimpleMeshBuilder();

                for (double x = propDia / 2; x <= beamL; x += propSpace)
                {
                    jackBuilder.AddBox(new Point3D(x, 0, zJackBottom + 5), 150, 150, 10);
                    jackBuilder.AddCylinder(new Point3D(x, 0, zJackBottom + 10), new Point3D(x, 0, zShoreBottom), 38, 12);
                    
                    shoreBuilder.AddCylinder(new Point3D(x, 0, zShoreBottom), new Point3D(x, 0, zShoreTop), propDia, 16);
                    
                    jackBuilder.AddCylinder(new Point3D(x, 0, zShoreTop), new Point3D(x, 0, zBotYokeBottom - 5), 38, 12);
                    jackBuilder.AddBox(new Point3D(x, 0, zBotYokeBottom - 5), botYokeW + 10, botYokeW + 10, 10);
                    jackBuilder.AddBox(new Point3D(x, botYokeW / 2 + 5, zBotYokeBottom + 50), botYokeW + 10, 10, 100);
                    jackBuilder.AddBox(new Point3D(x, -botYokeW / 2 - 5, zBotYokeBottom + 50), botYokeW + 10, 10, 100);
                }
                
                group.Children.Add(new MeshGeometryModel3D { Geometry = shoreBuilder.ToMesh(), Material = steelMat });
                group.Children.Add(new MeshGeometryModel3D { Geometry = jackBuilder.ToMesh(), Material = jackMat });
            }

            return group;
        }

        private GroupModel3D BuildColumn3DModel(
            double colW, double colD, double colH,
            double plyThick, double yokeSpace)
        {
            var group = new GroupModel3D();

            var concreteMat = new HelixToolkit.Wpf.SharpDX.DiffuseMaterial() { DiffuseColor = new HelixToolkit.Maths.Color4(200/255f, 220/255f, 240/255f, 100/255f) };
            var plyMat = new HelixToolkit.Wpf.SharpDX.DiffuseMaterial() { DiffuseColor = new HelixToolkit.Maths.Color4(210/255f, 180/255f, 140/255f, 1f) };
            var steelMat = new HelixToolkit.Wpf.SharpDX.DiffuseMaterial() { DiffuseColor = new HelixToolkit.Maths.Color4(80/255f, 130/255f, 180/255f, 1f) };

            // 1. Concrete Column
            var concreteBuilder = new SimpleMeshBuilder();
            concreteBuilder.AddBox(new Point3D(0, 0, colH / 2), colW, colD, colH);
            group.Children.Add(new MeshGeometryModel3D { Geometry = concreteBuilder.ToMesh(), Material = concreteMat });

            // 2. Plywood
            var plyBuilder = new SimpleMeshBuilder();
            // Front/Back sides (Y)
            plyBuilder.AddBox(new Point3D(0, colD / 2 + plyThick / 2, colH / 2), colW + 2 * plyThick, plyThick, colH);
            plyBuilder.AddBox(new Point3D(0, -colD / 2 - plyThick / 2, colH / 2), colW + 2 * plyThick, plyThick, colH);
            // Left/Right sides (X)
            plyBuilder.AddBox(new Point3D(colW / 2 + plyThick / 2, 0, colH / 2), plyThick, colD, colH);
            plyBuilder.AddBox(new Point3D(-colW / 2 - plyThick / 2, 0, colH / 2), plyThick, colD, colH);
            group.Children.Add(new MeshGeometryModel3D { Geometry = plyBuilder.ToMesh(), Material = plyMat });

            // 3. Vertical 40x40 stiffeners fixed to the plywood panels.
            var stiffenerBuilder = new SimpleMeshBuilder();
            double stiffW = _sharedViewModel.ColumnStiffenerWidth > 0 ? _sharedViewModel.ColumnStiffenerWidth : 40;
            double stiffD = _sharedViewModel.ColumnStiffenerDepth > 0 ? _sharedViewModel.ColumnStiffenerDepth : 40;
            double stiffSpacing = _sharedViewModel.ColumnStiffenerSpacing > 0 ? _sharedViewModel.ColumnStiffenerSpacing : 200;
            for (double x = -colW / 2; x <= colW / 2 + 0.1; x += stiffSpacing)
            {
                stiffenerBuilder.AddBox(new Point3D(x, colD / 2 + plyThick + stiffD / 2, colH / 2), stiffW, stiffD, colH);
                stiffenerBuilder.AddBox(new Point3D(x, -colD / 2 - plyThick - stiffD / 2, colH / 2), stiffW, stiffD, colH);
            }
            for (double y = -colD / 2; y <= colD / 2 + 0.1; y += stiffSpacing)
            {
                stiffenerBuilder.AddBox(new Point3D(colW / 2 + plyThick + stiffD / 2, y, colH / 2), stiffD, stiffW, colH);
                stiffenerBuilder.AddBox(new Point3D(-colW / 2 - plyThick - stiffD / 2, y, colH / 2), stiffD, stiffW, colH);
            }
            group.Children.Add(new MeshGeometryModel3D { Geometry = stiffenerBuilder.ToMesh(), Material = plyMat });

            // 4. Double rectangular-tube yokes, threaded ties and wing nuts.
            var yokeBuilder = new SimpleMeshBuilder();
            double yokeW = _sharedViewModel.ColumnYokeWidth > 0 ? _sharedViewModel.ColumnYokeWidth : 40;
            double yokeH = _sharedViewModel.ColumnYokeHeight > 0 ? _sharedViewModel.ColumnYokeHeight : 80;
            int pairCount = Math.Max(1, _sharedViewModel.ColumnYokePairCount);
            int tieCount = Math.Max(1, Math.Min(4, _sharedViewModel.ColumnTieRodCountPerFace));
            double standOff = plyThick + stiffD + yokeW / 2;
            double tieDiameter = _sharedViewModel.ColumnTieRodDiameter > 0 ? _sharedViewModel.ColumnTieRodDiameter : 12;
            var layoutParameters = new Models.CalculationParameters
            {
                ColumnHeight = colH,
                ColumnYokeSpacing = yokeSpace,
                ColumnUseZonedYokeSpacing = _sharedViewModel.ColumnUseZonedYokeSpacing,
                ColumnFirstYokeOffset = _sharedViewModel.ColumnFirstYokeOffset,
                ColumnYokeBottomSpacing = _sharedViewModel.ColumnYokeBottomSpacing,
                ColumnYokeUpperSpacing = _sharedViewModel.ColumnYokeUpperSpacing,
                ColumnYokeTransitionHeight = _sharedViewModel.ColumnYokeTransitionHeight,
                ColumnLastYokeOffset = _sharedViewModel.ColumnLastYokeOffset
            };
            foreach (double z in Models.FormworkCalculationService.BuildColumnYokeElevations(layoutParameters))
            {
                double innerWx = colW + 2 * plyThick;
                double innerWy = colD + 2 * plyThick;
                for (int pair = 0; pair < pairCount; pair++)
                {
                    double dz = (pair - (pairCount - 1) / 2.0) * (yokeH + 15);
                    yokeBuilder.AddBox(new Point3D(0, colD / 2 + standOff, z + dz), innerWx + 2 * (stiffD + yokeW + 120), yokeW, yokeH);
                    yokeBuilder.AddBox(new Point3D(0, -colD / 2 - standOff, z + dz), innerWx + 2 * (stiffD + yokeW + 120), yokeW, yokeH);
                    yokeBuilder.AddBox(new Point3D(colW / 2 + standOff, 0, z + dz), yokeW, innerWy + 2 * (stiffD + yokeW + 120), yokeH);
                    yokeBuilder.AddBox(new Point3D(-colW / 2 - standOff, 0, z + dz), yokeW, innerWy + 2 * (stiffD + yokeW + 120), yokeH);
                }
                double rodX = colW / 2 + standOff + 80;
                double rodY = colD / 2 + standOff + 80;
                for (int tie = 0; tie < tieCount; tie++)
                {
                    double xOffset = tieCount == 1 ? 0 : -rodX + 2.0 * rodX * tie / (tieCount - 1);
                    double yOffset = tieCount == 1 ? 0 : -rodY + 2.0 * rodY * tie / (tieCount - 1);
                    yokeBuilder.AddCylinder(new Point3D(xOffset, -rodY, z), new Point3D(xOffset, rodY, z), tieDiameter, 8);
                    yokeBuilder.AddBox(new Point3D(xOffset, rodY, z), 70, 12, 12);
                    yokeBuilder.AddBox(new Point3D(xOffset, -rodY, z), 70, 12, 12);
                    yokeBuilder.AddCylinder(new Point3D(-rodX, yOffset, z), new Point3D(rodX, yOffset, z), tieDiameter, 8);
                    yokeBuilder.AddBox(new Point3D(rodX, yOffset, z), 12, 70, 12);
                    yokeBuilder.AddBox(new Point3D(-rodX, yOffset, z), 12, 70, 12);
                }
            }
            group.Children.Add(new MeshGeometryModel3D { Geometry = yokeBuilder.ToMesh(), Material = steelMat });

            // 5. Four adjustable push-pull props with base plates; optional cable/turnbuckle.
            var braceBuilder = new SimpleMeshBuilder();
            double braceAngle = Math.Max(30, Math.Min(75, _sharedViewModel.ColumnBraceAngle)) * Math.PI / 180.0;
            double attachZ = colH * 0.68;
            double run = Math.Max(900, Math.Min(2500, attachZ / Math.Tan(braceAngle)));
            var openBracePoints = new[]
            {
                (new Point3D(colW / 2 + plyThick + 60, 0, attachZ), new Point3D(colW / 2 + run, 0, 60)),
                (new Point3D(-colW / 2 - plyThick - 60, 0, attachZ), new Point3D(-colW / 2 - run, 0, 60)),
                (new Point3D(0, colD / 2 + plyThick + 60, attachZ), new Point3D(0, colD / 2 + run, 60)),
                (new Point3D(0, -colD / 2 - plyThick - 60, attachZ), new Point3D(0, -colD / 2 - run, 60))
            };
            // The standalone preview uses the right-hand face as its boundary
            // example. The project preview derives the actual inward side from
            // the Revit structural footprint.
            var bracePoints = _sharedViewModel.ColumnBracePlacementModeIndex == 0
                ? openBracePoints
                : new[] { openBracePoints[1], openBracePoints[2], openBracePoints[3] };
            int braces = Math.Min(bracePoints.Length, Math.Max(2, Math.Min(4, _sharedViewModel.ColumnBraceCount)));
            for (int i = 0; i < braces; i++)
            {
                var member = bracePoints[i];
                braceBuilder.AddCylinder(member.Item2, member.Item1, 42, 10);
                braceBuilder.AddBox(member.Item2, 180, 140, 12);
            }
            if (_sharedViewModel.ColumnIsBoundaryOrCorner)
            {
                double cableSign = _sharedViewModel.ColumnBracePlacementModeIndex == 0 ? 1.0 : -1.0;
                Point3D cableTop = new Point3D(cableSign * (colW / 2 + 40), 0, colH * 0.85);
                Point3D cableFoot = new Point3D(cableSign * (colW / 2 + run), 0, 40);
                braceBuilder.AddCylinder(cableFoot, cableTop, 10, 6);
                Vector3D cable = cableTop - cableFoot;
                braceBuilder.AddCylinder(cableFoot + cable * 0.43, cableFoot + cable * 0.57, 28, 8);
            }
            group.Children.Add(new MeshGeometryModel3D { Geometry = braceBuilder.ToMesh(), Material = steelMat });

            return group;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private bool IsPointInside(System.Windows.Point testPoint, List<List<System.Windows.Point>> polygons)
        {
            if (polygons == null || polygons.Count == 0) return true; // Fallback

            bool inside = false;
            foreach (var poly in polygons)
            {
                for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                {
                    if (((poly[i].Y > testPoint.Y) != (poly[j].Y > testPoint.Y)) &&
                        (testPoint.X < (poly[j].X - poly[i].X) * (testPoint.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X))
                    {
                        inside = !inside;
                    }
                }
            }
            return inside;
        }

        /// <summary>
        /// Ray-casting point-in-polygon test for a single polygon loop.
        /// Used to associate holes with their containing outer boundary.
        /// </summary>
        private bool IsPointInsideSinglePoly(System.Windows.Point testPoint, List<System.Windows.Point> poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                if (((poly[i].Y > testPoint.Y) != (poly[j].Y > testPoint.Y)) &&
                    (testPoint.X < (poly[j].X - poly[i].X) * (testPoint.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        private List<double> GetYIntersections(double xPos, List<List<System.Windows.Point>> polygons)
        {
            List<double> yInts = new List<double>();
            if (polygons == null) return yInts;

            foreach (var poly in polygons)
            {
                for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                {
                    System.Windows.Point p1 = poly[i];
                    System.Windows.Point p2 = poly[j];

                    if ((p1.X <= xPos && p2.X > xPos) || (p2.X <= xPos && p1.X > xPos))
                    {
                        double t = (xPos - p1.X) / (p2.X - p1.X);
                        double yInt = p1.Y + t * (p2.Y - p1.Y);
                        yInts.Add(yInt);
                    }
                }
            }
            yInts.Sort();
            return yInts;
        }

        private List<double> GetXIntersections(double yPos, List<List<System.Windows.Point>> polygons)
        {
            List<double> xInts = new List<double>();
            if (polygons == null) return xInts;

            foreach (var poly in polygons)
            {
                for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                {
                    System.Windows.Point p1 = poly[i];
                    System.Windows.Point p2 = poly[j];

                    if ((p1.Y <= yPos && p2.Y > yPos) || (p2.Y <= yPos && p1.Y > yPos))
                    {
                        double t = (yPos - p1.Y) / (p2.Y - p1.Y);
                        double xInt = p1.X + t * (p2.X - p1.X);
                        xInts.Add(xInt);
                    }
                }
            }
            xInts.Sort();
            return xInts;
        }

        private bool IsPointInsideOrNear(System.Windows.Point testPoint, List<List<System.Windows.Point>> polygons, double tolerance)
        {
            if (IsPointInside(testPoint, polygons)) return true;

            foreach (var poly in polygons)
            {
                for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                {
                    System.Windows.Point p1 = poly[i];
                    System.Windows.Point p2 = poly[j];
                    if (DistancePointToSegment(testPoint, p1, p2) <= tolerance)
                        return true;
                }
            }
            return false;
        }

        private double DistancePointToSegment(System.Windows.Point pt, System.Windows.Point p1, System.Windows.Point p2)
        {
            double l2 = Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2);
            if (l2 == 0) return Math.Sqrt(Math.Pow(pt.X - p1.X, 2) + Math.Pow(pt.Y - p1.Y, 2));
            double t = Math.Max(0, Math.Min(1, ((pt.X - p1.X) * (p2.X - p1.X) + (pt.Y - p1.Y) * (p2.Y - p1.Y)) / l2));
            System.Windows.Point proj = new System.Windows.Point(p1.X + t * (p2.X - p1.X), p1.Y + t * (p2.Y - p1.Y));
            return Math.Sqrt(Math.Pow(pt.X - proj.X, 2) + Math.Pow(pt.Y - proj.Y, 2));
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            ReleaseProjectModel();
            if (_sharedViewModel != null)
            {
                _sharedViewModel.PropertyChanged -= SharedViewModel_PropertyChanged;
                _sharedViewModel.SlabSelectionCompleted -= OnSlabSelectionCompleted;
                _sharedViewModel.Render3DRequested -= OnRender3DRequested;
            }
        }

        public class Interval
        {
            public double Start { get; set; }
            public double End { get; set; }
        }

        private List<Interval> MergeCloseIntervals(List<Interval> intervals, double tolerance)
        {
            if (intervals == null || intervals.Count <= 1) return intervals;
            var sorted = intervals.OrderBy(i => i.Start).ToList();
            var result = new List<Interval>();
            var current = new Interval { Start = sorted[0].Start, End = sorted[0].End };
            for (int i = 1; i < sorted.Count; i++)
            {
                if (sorted[i].Start <= current.End + tolerance)
                {
                    current.End = Math.Max(current.End, sorted[i].End);
                }
                else
                {
                    result.Add(current);
                    current = new Interval { Start = sorted[i].Start, End = sorted[i].End };
                }
            }
            result.Add(current);
            return result;
        }

        private List<Interval> UnionIntervals(List<Interval> list1, List<Interval> list2)
        {
            var combined = new List<Interval>();
            if (list1 != null) combined.AddRange(list1);
            if (list2 != null) combined.AddRange(list2);
            return MergeCloseIntervals(combined, 0); 
        }

        private List<Interval> IntersectIntervals(List<Interval> list1, List<Interval> list2)
        {
            var result = new List<Interval>();
            if (list1 == null || list2 == null || list1.Count == 0 || list2.Count == 0) return result;
            int i = 0, j = 0;
            while (i < list1.Count && j < list2.Count)
            {
                double start = Math.Max(list1[i].Start, list2[j].Start);
                double end = Math.Min(list1[i].End, list2[j].End);
                if (start < end)
                {
                    result.Add(new Interval { Start = start, End = end });
                }
                if (list1[i].End < list2[j].End) i++;
                else j++;
            }
            return result;
        }
    }

    public class SimpleMeshBuilder
    {
        private readonly Dictionary<(long X, long Y, long Z), int> _projectVertexIndices
            = new Dictionary<(long X, long Y, long Z), int>();

        public HelixToolkit.SharpDX.MeshGeometry3D Mesh { get; } = new HelixToolkit.SharpDX.MeshGeometry3D()
        {
            Positions = new HelixToolkit.Vector3Collection(),
            TriangleIndices = new HelixToolkit.IntCollection()
        };
        private void AddPosV3(Point3D p) => Mesh.Positions.Add(new System.Numerics.Vector3((float)p.X, (float)p.Y, (float)p.Z));

        public void AddBox(Point3D center, double xLength, double yLength, double zLength)
        {
            int startIndex = Mesh.Positions.Count;

            double w = xLength / 2;
            double l = yLength / 2;
            double h = zLength / 2;

            Point3D[] p = new Point3D[]
            {
                new Point3D(center.X - w, center.Y - l, center.Z + h),
                new Point3D(center.X + w, center.Y - l, center.Z + h),
                new Point3D(center.X + w, center.Y + l, center.Z + h),
                new Point3D(center.X - w, center.Y + l, center.Z + h),
                new Point3D(center.X - w, center.Y - l, center.Z - h),
                new Point3D(center.X + w, center.Y - l, center.Z - h),
                new Point3D(center.X + w, center.Y + l, center.Z - h),
                new Point3D(center.X - w, center.Y + l, center.Z - h)
            };

            foreach (var pt in p) AddPosV3(pt);

            int[] indices = new int[]
            {
                0,1,2, 0,2,3, // top
                7,6,5, 7,5,4, // bottom
                4,5,1, 4,1,0, // front
                5,6,2, 5,2,1, // right
                6,7,3, 6,3,2, // back
                7,4,0, 7,0,3  // left
            };

            foreach (var i in indices) Mesh.TriangleIndices.Add(startIndex + i);
        }

        public void AddTriangle(Point3D a, Point3D b, Point3D c)
        {
            Mesh.TriangleIndices.Add(GetOrAddProjectVertex(a));
            Mesh.TriangleIndices.Add(GetOrAddProjectVertex(b));
            Mesh.TriangleIndices.Add(GetOrAddProjectVertex(c));
        }

        private int GetOrAddProjectVertex(Point3D point)
        {
            // Revit faces repeat their corner vertices per triangle. Quantized indexing
            // greatly reduces WPF vertex memory without changing visible geometry.
            var key = (
                (long)Math.Round(point.X * 100.0),
                (long)Math.Round(point.Y * 100.0),
                (long)Math.Round(point.Z * 100.0));

            if (_projectVertexIndices.TryGetValue(key, out int index)) return index;
            index = Mesh.Positions.Count;
            AddPosV3(point);
            _projectVertexIndices.Add(key, index);
            return index;
        }

        public void AddOrientedBox(
            Point3D center,
            Vector3D xAxis,
            Vector3D yAxis,
            Vector3D zAxis,
            double xLength,
            double yLength,
            double zLength)
        {
            if (xLength <= 0 || yLength <= 0 || zLength <= 0) return;

            xAxis.Normalize();
            yAxis.Normalize();
            zAxis.Normalize();

            Vector3D x = xAxis * (xLength / 2.0);
            Vector3D y = yAxis * (yLength / 2.0);
            Vector3D z = zAxis * (zLength / 2.0);

            Point3D[] p =
            {
                center - x - y + z,
                center + x - y + z,
                center + x + y + z,
                center - x + y + z,
                center - x - y - z,
                center + x - y - z,
                center + x + y - z,
                center - x + y - z
            };

            int startIndex = Mesh.Positions.Count;
            foreach (Point3D point in p) AddPosV3(point);

            int[] indices =
            {
                0,1,2, 0,2,3,
                7,6,5, 7,5,4,
                4,5,1, 4,1,0,
                5,6,2, 5,2,1,
                6,7,3, 6,3,2,
                7,4,0, 7,0,3
            };
            foreach (int index in indices) Mesh.TriangleIndices.Add(startIndex + index);
        }

        public void AddCylinder(Point3D p1, Point3D p2, double diameter, int thetaDiv)
        {
            // LOD optimization: use fewer segments for small cylinders
            if (diameter < 40 && thetaDiv > 6) thetaDiv = Math.Max(6, thetaDiv / 2);
            else if (diameter < 80 && thetaDiv > 8) thetaDiv = Math.Max(8, thetaDiv - 2);
            
            double r = diameter / 2;
            Vector3D dir = p2 - p1;
            dir.Normalize();

            Vector3D u = new Vector3D(0, 1, 0);
            if (Math.Abs(Vector3D.DotProduct(u, dir)) > 0.9) u = new Vector3D(1, 0, 0);
            Vector3D v = Vector3D.CrossProduct(dir, u);
            v.Normalize();
            u = Vector3D.CrossProduct(v, dir);
            u.Normalize();

            int startIndex = Mesh.Positions.Count;

            for (int i = 0; i < thetaDiv; i++)
            {
                double theta = 2.0 * Math.PI * i / thetaDiv;
                Vector3D offset = r * Math.Cos(theta) * u + r * Math.Sin(theta) * v;
                AddPosV3(p1 + offset);
                AddPosV3(p2 + offset);
            }

            for (int i = 0; i < thetaDiv; i++)
            {
                int next = (i + 1) % thetaDiv;
                int p1i = startIndex + i * 2;
                int p2i = startIndex + i * 2 + 1;
                int np1i = startIndex + next * 2;
                int np2i = startIndex + next * 2 + 1;

                Mesh.TriangleIndices.Add(p1i);
                Mesh.TriangleIndices.Add(np1i);
                Mesh.TriangleIndices.Add(p2i);

                Mesh.TriangleIndices.Add(p2i);
                Mesh.TriangleIndices.Add(np1i);
                Mesh.TriangleIndices.Add(np2i);
            }
        }

        public void AddWall(Point3D p1, Point3D p2, double thickness, double height, double zBottom)
        {
            Vector3D dir = p2 - p1;
            dir.Normalize();
            Vector3D n = new Vector3D(-dir.Y, dir.X, 0); // Left normal
            n.Normalize();

            Point3D[] pts = new Point3D[8];
            pts[0] = p1 - n * (thickness / 2) + new Vector3D(0, 0, zBottom + height);
            pts[1] = p2 - n * (thickness / 2) + new Vector3D(0, 0, zBottom + height);
            pts[2] = p2 + n * (thickness / 2) + new Vector3D(0, 0, zBottom + height);
            pts[3] = p1 + n * (thickness / 2) + new Vector3D(0, 0, zBottom + height);

            pts[4] = p1 - n * (thickness / 2) + new Vector3D(0, 0, zBottom);
            pts[5] = p2 - n * (thickness / 2) + new Vector3D(0, 0, zBottom);
            pts[6] = p2 + n * (thickness / 2) + new Vector3D(0, 0, zBottom);
            pts[7] = p1 + n * (thickness / 2) + new Vector3D(0, 0, zBottom);

            int startIndex = Mesh.Positions.Count;
            foreach (var pt in pts) AddPosV3(pt);

            int[] indices = new int[]
            {
                0,1,2, 0,2,3, // top
                7,6,5, 7,5,4, // bottom
                4,5,1, 4,1,0, // front
                5,6,2, 5,2,1, // right
                6,7,3, 6,3,2, // back
                7,4,0, 7,0,3  // left
            };

            foreach (var i in indices) Mesh.TriangleIndices.Add(startIndex + i);
        }

        /// <summary>
        /// Triangulates a 2D polygon (with optional holes) and extrudes it between zBottom and zTop
        /// to produce a geometrically faithful 3D solid. Handles concave polygons, chamfered corners,
        /// re-entrant corners, cantilevered projections, and openings.
        /// </summary>
        public void AddExtrudedPolygon(
            List<System.Windows.Point> outerLoop,
            List<List<System.Windows.Point>> holes,
            double zBottom, double zTop)
        {
            if (outerLoop == null || outerLoop.Count < 3) return;

            // 1. Triangulate the 2D polygon
            var triangles = EarClipTriangulator.Triangulate(outerLoop, holes);
            if (triangles == null || triangles.Count == 0) return;

            int startIndex = Mesh.Positions.Count;

            // 2. Collect all unique 2D points from the triangulation
            var pts2D = new List<System.Windows.Point>();
            var ptIndexMap = new Dictionary<long, int>(); // hash -> index in pts2D

            int GetOrAdd2DIndex(System.Windows.Point p)
            {
                // Use a quantized key to handle floating-point near-duplicates
                long key = ((long)Math.Round(p.X * 100)) * 100000000L + (long)Math.Round(p.Y * 100);
                if (!ptIndexMap.TryGetValue(key, out int idx))
                {
                    idx = pts2D.Count;
                    pts2D.Add(p);
                    ptIndexMap[key] = idx;
                }
                return idx;
            }

            // Build triangle index list in terms of pts2D indices
            var triIndices = new List<int>();
            for (int i = 0; i < triangles.Count; i += 3)
            {
                triIndices.Add(GetOrAdd2DIndex(triangles[i]));
                triIndices.Add(GetOrAdd2DIndex(triangles[i + 1]));
                triIndices.Add(GetOrAdd2DIndex(triangles[i + 2]));
            }

            int numPts = pts2D.Count;

            // 3. Add 3D positions: top face vertices then bottom face vertices
            foreach (var p in pts2D)
                AddPosV3(new Point3D(p.X, p.Y, zTop));
            foreach (var p in pts2D)
                AddPosV3(new Point3D(p.X, p.Y, zBottom));

            // 4. Top face triangles (CCW when viewed from above = outward normal +Z)
            for (int i = 0; i < triIndices.Count; i += 3)
            {
                Mesh.TriangleIndices.Add(startIndex + triIndices[i]);
                Mesh.TriangleIndices.Add(startIndex + triIndices[i + 1]);
                Mesh.TriangleIndices.Add(startIndex + triIndices[i + 2]);
            }

            // 5. Bottom face triangles (reversed winding = outward normal -Z)
            for (int i = 0; i < triIndices.Count; i += 3)
            {
                Mesh.TriangleIndices.Add(startIndex + numPts + triIndices[i]);
                Mesh.TriangleIndices.Add(startIndex + numPts + triIndices[i + 2]);
                Mesh.TriangleIndices.Add(startIndex + numPts + triIndices[i + 1]);
            }

            // 6. Side walls along each polygon edge loop
            void AddSideWalls(List<System.Windows.Point> loop)
            {
                for (int i = 0; i < loop.Count; i++)
                {
                    int next = (i + 1) % loop.Count;
                    var pA = loop[i];
                    var pB = loop[next];

                    // 4 corner vertices for this side quad
                    int si = Mesh.Positions.Count;
                    AddPosV3(new Point3D(pA.X, pA.Y, zTop));    // 0: top-A
                    AddPosV3(new Point3D(pB.X, pB.Y, zTop));    // 1: top-B
                    AddPosV3(new Point3D(pB.X, pB.Y, zBottom)); // 2: bot-B
                    AddPosV3(new Point3D(pA.X, pA.Y, zBottom)); // 3: bot-A

                    // Two triangles forming the quad (outward normal faces away from polygon interior)
                    Mesh.TriangleIndices.Add(si + 0);
                    Mesh.TriangleIndices.Add(si + 3);
                    Mesh.TriangleIndices.Add(si + 2);

                    Mesh.TriangleIndices.Add(si + 0);
                    Mesh.TriangleIndices.Add(si + 2);
                    Mesh.TriangleIndices.Add(si + 1);
                }
            }

            AddSideWalls(outerLoop);
            if (holes != null)
            {
                foreach (var hole in holes)
                    AddSideWalls(hole);
            }
        }

        public HelixToolkit.SharpDX.MeshGeometry3D ToMesh() => Mesh;
    }

    /// <summary>
    /// Lightweight 2D ear-clipping polygon triangulator.
    /// Handles concave polygons and supports holes via bridge-edge merging.
    /// Returns a flat list of triangle vertices: [t0_v0, t0_v1, t0_v2, t1_v0, ...]
    /// </summary>
    public static class EarClipTriangulator
    {
        public static List<System.Windows.Point> Triangulate(
            List<System.Windows.Point> outerLoop,
            List<List<System.Windows.Point>> holes)
        {
            if (outerLoop == null || outerLoop.Count < 3)
                return new List<System.Windows.Point>();

            // Ensure outer loop is CCW (positive signed area in math coords)
            var outer = new List<System.Windows.Point>(outerLoop);
            if (SignedArea(outer) < 0)
                outer.Reverse();

            // Merge holes into the outer polygon via bridge edges
            if (holes != null && holes.Count > 0)
            {
                // Ensure each hole is CW (negative signed area)
                var sortedHoles = new List<List<System.Windows.Point>>();
                foreach (var h in holes)
                {
                    if (h == null || h.Count < 3) continue;
                    var hole = new List<System.Windows.Point>(h);
                    if (SignedArea(hole) > 0)
                        hole.Reverse();
                    sortedHoles.Add(hole);
                }

                // Sort holes by their maximum X coordinate (rightmost point) descending
                sortedHoles.Sort((a, b) => b.Max(p => p.X).CompareTo(a.Max(p => p.X)));

                foreach (var hole in sortedHoles)
                {
                    outer = MergeHole(outer, hole);
                }
            }

            // Ear-clipping on the merged polygon
            return EarClip(outer);
        }

        private static double SignedArea(List<System.Windows.Point> poly)
        {
            double area = 0;
            for (int i = 0; i < poly.Count; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % poly.Count];
                area += (b.X - a.X) * (b.Y + a.Y);
            }
            return -area / 2.0; // Negate because screen-Y convention; positive = CCW in math coords
        }

        /// <summary>
        /// Merges a hole into the outer polygon by finding the best bridge edge.
        /// The rightmost point of the hole is connected to a visible point on the outer polygon.
        /// </summary>
        private static List<System.Windows.Point> MergeHole(
            List<System.Windows.Point> outer, List<System.Windows.Point> hole)
        {
            // Find the rightmost point in the hole
            int holeIdx = 0;
            for (int i = 1; i < hole.Count; i++)
            {
                if (hole[i].X > hole[holeIdx].X ||
                    (hole[i].X == hole[holeIdx].X && hole[i].Y > hole[holeIdx].Y))
                    holeIdx = i;
            }
            var holePoint = hole[holeIdx];

            // Cast a ray to the right (+X) from holePoint and find the nearest intersection
            // with an edge of the outer polygon
            double minDist = double.MaxValue;
            int bestEdge = -1;
            System.Windows.Point bestIntersection = new System.Windows.Point();

            for (int i = 0; i < outer.Count; i++)
            {
                var a = outer[i];
                var b = outer[(i + 1) % outer.Count];

                // Check if the horizontal ray from holePoint crosses edge a->b
                if ((a.Y <= holePoint.Y && b.Y > holePoint.Y) ||
                    (b.Y <= holePoint.Y && a.Y > holePoint.Y))
                {
                    double t = (holePoint.Y - a.Y) / (b.Y - a.Y);
                    double ix = a.X + t * (b.X - a.X);
                    if (ix >= holePoint.X)
                    {
                        double dist = ix - holePoint.X;
                        if (dist < minDist)
                        {
                            minDist = dist;
                            bestEdge = i;
                            bestIntersection = new System.Windows.Point(ix, holePoint.Y);
                        }
                    }
                }
            }

            if (bestEdge < 0) return outer; // Should not happen for valid geometry

            // Find the best connection point on the outer polygon.
            // Prefer an existing vertex near the intersection to avoid creating degenerate triangles.
            int bestOuterIdx = (bestEdge + 1) % outer.Count;
            double bestScore = double.MaxValue;

            // Check the two endpoints of the intersected edge and nearby vertices
            for (int i = 0; i < outer.Count; i++)
            {
                var p = outer[i];
                // The candidate must be to the right of or at the hole point X
                if (p.X < holePoint.X - 1e-6) continue;

                double dx = p.X - holePoint.X;
                double dy = Math.Abs(p.Y - holePoint.Y);
                double score = dx + dy; // Manhattan-like distance

                // Check that the bridge edge doesn't cross any outer polygon edge
                if (score < bestScore && !BridgeCrossesEdges(holePoint, p, outer))
                {
                    bestScore = score;
                    bestOuterIdx = i;
                }
            }

            // Build merged polygon: outer[0..bestOuterIdx] + hole[holeIdx..] + hole[..holeIdx] + bridge back
            var merged = new List<System.Windows.Point>();
            for (int i = 0; i <= bestOuterIdx; i++)
                merged.Add(outer[i]);

            // Walk the hole starting from holeIdx
            for (int i = 0; i < hole.Count; i++)
                merged.Add(hole[(holeIdx + i) % hole.Count]);

            // Close the bridge: duplicate the bridge endpoints
            merged.Add(hole[holeIdx]);
            merged.Add(outer[bestOuterIdx]);

            for (int i = bestOuterIdx + 1; i < outer.Count; i++)
                merged.Add(outer[i]);

            return merged;
        }

        private static bool BridgeCrossesEdges(
            System.Windows.Point a, System.Windows.Point b,
            List<System.Windows.Point> poly)
        {
            for (int i = 0; i < poly.Count; i++)
            {
                var c = poly[i];
                var d = poly[(i + 1) % poly.Count];

                // Skip edges that share an endpoint with the bridge
                if (PointsEqual(c, a) || PointsEqual(c, b) ||
                    PointsEqual(d, a) || PointsEqual(d, b))
                    continue;

                if (SegmentsIntersect(a, b, c, d))
                    return true;
            }
            return false;
        }

        private static bool PointsEqual(System.Windows.Point a, System.Windows.Point b)
        {
            return Math.Abs(a.X - b.X) < 1e-6 && Math.Abs(a.Y - b.Y) < 1e-6;
        }

        private static bool SegmentsIntersect(
            System.Windows.Point p1, System.Windows.Point p2,
            System.Windows.Point p3, System.Windows.Point p4)
        {
            double d1 = Cross(p3, p4, p1);
            double d2 = Cross(p3, p4, p2);
            double d3 = Cross(p1, p2, p3);
            double d4 = Cross(p1, p2, p4);

            if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
                ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
                return true;

            return false; // Ignore collinear/touching cases for bridge check
        }

        private static double Cross(System.Windows.Point o, System.Windows.Point a, System.Windows.Point b)
        {
            return (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        }

        /// <summary>
        /// Standard ear-clipping triangulation for a simple polygon (no holes).
        /// The polygon should be wound CCW (positive signed area in math coords).
        /// </summary>
        private static List<System.Windows.Point> EarClip(List<System.Windows.Point> polygon)
        {
            var result = new List<System.Windows.Point>();
            if (polygon.Count < 3) return result;

            // Work with indices into a linked list for O(1) removal
            var indices = new List<int>();
            for (int i = 0; i < polygon.Count; i++)
                indices.Add(i);

            int safetyCounter = indices.Count * indices.Count; // Prevent infinite loops
            int idx = 0;

            while (indices.Count > 2 && safetyCounter-- > 0)
            {
                int n = indices.Count;
                int prevIdx = (idx - 1 + n) % n;
                int currIdx = idx % n;
                int nextIdx = (idx + 1) % n;

                var prev = polygon[indices[prevIdx]];
                var curr = polygon[indices[currIdx]];
                var next = polygon[indices[nextIdx]];

                // Check if this vertex forms a convex angle (ear candidate)
                double cross = (curr.X - prev.X) * (next.Y - prev.Y) -
                               (curr.Y - prev.Y) * (next.X - prev.X);

                if (cross > 1e-10) // Convex vertex (CCW winding)
                {
                    // Check that no other vertex lies inside this triangle
                    bool isEar = true;
                    for (int i = 0; i < indices.Count; i++)
                    {
                        if (i == prevIdx || i == currIdx || i == nextIdx) continue;
                        if (PointInTriangle(polygon[indices[i]], prev, curr, next))
                        {
                            isEar = false;
                            break;
                        }
                    }

                    if (isEar)
                    {
                        result.Add(prev);
                        result.Add(curr);
                        result.Add(next);
                        indices.RemoveAt(currIdx);
                        // After removal, adjust idx to not skip a vertex
                        if (currIdx >= indices.Count) idx = 0;
                        else idx = currIdx;
                        continue;
                    }
                }

                idx = (idx + 1) % indices.Count;
            }

            return result;
        }

        private static bool PointInTriangle(
            System.Windows.Point p,
            System.Windows.Point a, System.Windows.Point b, System.Windows.Point c)
        {
            double d1 = (p.X - b.X) * (a.Y - b.Y) - (a.X - b.X) * (p.Y - b.Y);
            double d2 = (p.X - c.X) * (b.Y - c.Y) - (b.X - c.X) * (p.Y - c.Y);
            double d3 = (p.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (p.Y - a.Y);

            bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

            return !(hasNeg && hasPos);
        }
    }
}
