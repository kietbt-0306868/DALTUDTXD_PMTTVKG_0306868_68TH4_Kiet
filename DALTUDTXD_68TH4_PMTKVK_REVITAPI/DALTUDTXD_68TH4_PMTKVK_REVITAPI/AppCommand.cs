using System;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.Attributes;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Services;
using System.Runtime.CompilerServices;
using System.Windows.Interop;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI
{
    [Transaction(TransactionMode.Manual)]
    public abstract class AppCommandBase : IExternalCommand
    {
        private static readonly ConditionalWeakTable<Document, SharedDataViewModel> Sessions =
            new ConditionalWeakTable<Document, SharedDataViewModel>();

        protected abstract string TargetTab { get; }
        protected virtual string TargetAction => null;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uiDoc = commandData.Application.ActiveUIDocument;
                if (uiDoc == null)
                {
                    message = "Không tìm thấy tài liệu Revit đang hoạt động.";
                    return Result.Failed;
                }
                Document doc = uiDoc.Document;

                // Keep calculation/selection state for the lifetime of this Revit Document.
                // Previously every ribbon button created a fresh data model, so a command
                // could not see the selection/design produced by the previous command.
                SharedDataViewModel sharedViewModel = Sessions.GetValue(doc,
                    _ => new SharedDataViewModel(new MessageBoxDialogService()));
                Action closeWindowAction = null;
                sharedViewModel.SetCloseWindowAction(() => closeWindowAction?.Invoke());
                sharedViewModel.InitializeRevitContext(doc, uiDoc, null);
                sharedViewModel.StartupTab = TargetTab;
                sharedViewModel.StartupAction = TargetAction;

                while (true)
                {
                    var mainWindow = new MainWindow(sharedViewModel);
                    IntPtr revitHandle = commandData.Application.MainWindowHandle;
                    new WindowInteropHelper(mainWindow).Owner = revitHandle;

                    closeWindowAction = () => { mainWindow.DialogResult = true; };

                    if (!string.IsNullOrEmpty(sharedViewModel.StartupTab))
                    {
                        mainWindow.Navigate(sharedViewModel.StartupTab);
                        sharedViewModel.ExecuteChangeTab(sharedViewModel.StartupTab);
                        sharedViewModel.StartupTab = null;
                    }

                    // Queue the shortcut only after ContentRendered. Running it here used
                    // to execute against an unrendered WPF tree and was the root of several
                    // ribbon crashes and invisible long-running operations.
                    if (!string.IsNullOrEmpty(sharedViewModel.StartupAction))
                    {
                        mainWindow.QueueStartupAction(sharedViewModel.StartupAction);
                        sharedViewModel.StartupAction = null;
                    }

                    try
                    {
                        mainWindow.ShowDialog();
                    }
                    finally
                    {
                        sharedViewModel.DisposePreviewControl();
                    }

                    if (sharedViewModel.IsSelectingSlab || sharedViewModel.IsSweepSelectingSlab ||
                        sharedViewModel.IsSelectingBeam || sharedViewModel.IsSweepSelectingBeam ||
                        sharedViewModel.IsSelectingColumn || sharedViewModel.IsSweepSelectingColumn ||
                        sharedViewModel.IsSelectingComponent || sharedViewModel.IsSweepSelectingComponent)
                    {
                        HandleSelection(uiDoc, sharedViewModel);
                    }
                    else
                    {
                        break;
                    }
                }

                sharedViewModel.SetCloseWindowAction(null);

                // Dọn dẹp trạng thái Highlight trước khi đóng Add-in hoàn toàn
                if (sharedViewModel.KetQuaLastHighlightedFloorIds != null && sharedViewModel.KetQuaLastHighlightedFloorIds.Count > 0 && sharedViewModel.KetQuaLastHighlightedViewId != null)
                {
                    try
                    {
                        using (Transaction t = new Transaction(doc, "Restore Floor Highlight"))
                        {
                            t.Start();
                            View lastView = doc.GetElement(sharedViewModel.KetQuaLastHighlightedViewId) as View;
                            if (lastView != null)
                            {
                                foreach (var oldId in sharedViewModel.KetQuaLastHighlightedFloorIds)
                                {
                                    lastView.SetElementOverrides(oldId, new OverrideGraphicSettings());
                                }
                                if (sharedViewModel.KetQuaLastViewTemplateId != null)
                                {
                                    lastView.ViewTemplateId = sharedViewModel.KetQuaLastViewTemplateId;
                                }
                            }
                            t.Commit();
                        }
                    }
                    catch { /* Ignore */ }
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"Không thể thực hiện lệnh {GetType().Name}: {ex.Message}";
                return Result.Failed;
            }
        }

        private void HandleSelection(UIDocument uiDoc, SharedDataViewModel sharedViewModel)
        {
            Autodesk.Revit.UI.Selection.ISelectionFilter filter = new FloorSelectionFilter();
            string title = "chọn cấu kiện";
            bool isSweep = false;
            
            if (sharedViewModel.IsSelectingComponent || sharedViewModel.IsSweepSelectingComponent)
            {
                filter = new MultiComponentSelectionFilter(
                    sharedViewModel.IsFilterBeamChecked,
                    sharedViewModel.IsFilterSlabChecked,
                    sharedViewModel.IsFilterColumnChecked,
                    sharedViewModel.IsFilterFoundationChecked);
                title = "chọn cấu kiện";
                isSweep = sharedViewModel.IsSweepSelectingComponent;
            }
            else if (sharedViewModel.IsSelectingSlab || sharedViewModel.IsSweepSelectingSlab)
            {
                filter = new FloorSelectionFilter();
                title = "chọn sàn";
                isSweep = sharedViewModel.IsSweepSelectingSlab;
            }
            else if (sharedViewModel.IsSelectingFoundation || sharedViewModel.IsSweepSelectingFoundation)
            {
                filter = new FoundationSelectionFilter();
                title = "chọn móng";
                isSweep = sharedViewModel.IsSweepSelectingFoundation;
            }
            else if (sharedViewModel.IsSelectingBeam || sharedViewModel.IsSweepSelectingBeam)
            {
                filter = new BeamSelectionFilter();
                title = "chọn dầm";
                isSweep = sharedViewModel.IsSweepSelectingBeam;
            }
            else if (sharedViewModel.IsSelectingColumn || sharedViewModel.IsSweepSelectingColumn)
            {
                filter = new ColumnSelectionFilter();
                title = "chọn cột";
                isSweep = sharedViewModel.IsSweepSelectingColumn;
            }

            if (!isSweep)
            {
                System.Collections.Generic.IList<Reference> references = new System.Collections.Generic.List<Reference>();
                try
                {
                    references = uiDoc.Selection.PickObjects(
                        Autodesk.Revit.UI.Selection.ObjectType.Element,
                        filter,
                        $"Quét vùng chuột hoặc giữ Ctrl để chọn nhiều {title}. NHẤN NÚT FINISH Ở GÓC TRÊN CÙNG BÊN TRÁI SAU KHI XONG!");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Lỗi {title}: {ex.Message}", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }

                if (references != null && references.Count > 0)
                {
                    if (sharedViewModel.IsSelectingComponent)
                    {
                        var doc = uiDoc.Document;
                        var slabRefs = new System.Collections.Generic.List<Reference>();
                        var foundationRefs = new System.Collections.Generic.List<Reference>();
                        var beamRefs = new System.Collections.Generic.List<Reference>();
                        var columnRefs = new System.Collections.Generic.List<Reference>();

                        foreach (var r in references)
                        {
                            var elem = doc.GetElement(r);
                            if (elem == null || elem.Category == null) continue;
                            BuiltInCategory cat = elem.Category.BuiltInCategory;
                            
                            if (cat == BuiltInCategory.OST_StructuralFoundation) 
                                foundationRefs.Add(r);
                            else if (cat == BuiltInCategory.OST_Floors)
                            {
                                bool isFoundationSlab = false;
                                if (elem is Floor floor && doc.GetElement(floor.GetTypeId()) as FloorType is FloorType floorType && floorType.IsFoundationSlab)
                                {
                                    isFoundationSlab = true;
                                }

                                if (isFoundationSlab) foundationRefs.Add(r);
                                else slabRefs.Add(r);
                            }
                            else if (cat == BuiltInCategory.OST_StructuralFraming) beamRefs.Add(r);
                            else if (cat == BuiltInCategory.OST_StructuralColumns) columnRefs.Add(r);
                        }

                        bool appendCategory = false;
                        if (foundationRefs.Count > 0) { sharedViewModel.ProcessSelectedSlab(foundationRefs, showDialog: false, isFoundation: true, appendSelection: appendCategory); appendCategory = true; }
                        if (slabRefs.Count > 0) { sharedViewModel.ProcessSelectedSlab(slabRefs, showDialog: false, isFoundation: false, appendSelection: appendCategory); appendCategory = true; }
                        if (beamRefs.Count > 0) { sharedViewModel.ProcessSelectedBeam(beamRefs, appendCategory); appendCategory = true; }
                        if (columnRefs.Count > 0) sharedViewModel.ProcessSelectedColumn(columnRefs, appendCategory);
                        sharedViewModel.SetSelectedComponentComposition(
                            slabRefs.Count, foundationRefs.Count, beamRefs.Count, columnRefs.Count);

                        if (slabRefs.Count > 0 || foundationRefs.Count > 0 || beamRefs.Count > 0 || columnRefs.Count > 0)
                        {
                            var parts = new System.Collections.Generic.List<string>();
                            string details = "Đã quét chọn thành công các cấu kiện từ Revit:\n\n";

                            if (foundationRefs.Count > 0)
                            {
                                parts.Add($"{foundationRefs.Count} móng");
                                details += $"• Móng: {foundationRefs.Count} cấu kiện (Diện tích: {sharedViewModel.SelectedSlabArea:F2} m²)\n";
                            }
                            if (slabRefs.Count > 0)
                            {
                                parts.Add($"{slabRefs.Count} sàn");
                                details += $"• Sàn: {slabRefs.Count} cấu kiện (Diện tích: {sharedViewModel.SelectedSlabArea:F2} m²)\n";
                            }
                            if (beamRefs.Count > 0)
                            {
                                parts.Add($"{beamRefs.Count} dầm");
                                details += $"• Dầm: {beamRefs.Count} cấu kiện (Tiết diện: {sharedViewModel.BeamWidth:F0}x{sharedViewModel.BeamDepth:F0} mm)\n";
                            }
                            if (columnRefs.Count > 0)
                            {
                                parts.Add($"{columnRefs.Count} cột");
                                details += $"• Cột: {columnRefs.Count} cấu kiện (Tiết diện: {sharedViewModel.ColumnWidth:F0}x{sharedViewModel.ColumnDepth:F0} mm, H={sharedViewModel.ColumnHeight:F0} mm)\n";
                            }

                            details += "\n- Tự động đồng bộ và tính toán lại các thông số ván khuôn tối ưu.";
                            sharedViewModel.RevitVersionStatus = $"Đã chọn: {string.Join(", ", parts)}";
                            System.Windows.MessageBox.Show(details, "Revit API Integration", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                        }
                        
                        // Update active component type based on what was selected so the UI parameter tab aligns with the results
                        if (columnRefs.Count > 0)
                            sharedViewModel.CurrentComponentType = DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models.StructuralComponentType.Column;
                        else if (beamRefs.Count > 0)
                            sharedViewModel.CurrentComponentType = DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models.StructuralComponentType.Beam;
                        else if (foundationRefs.Count > 0)
                            sharedViewModel.CurrentComponentType = DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models.StructuralComponentType.Foundation;
                        else if (slabRefs.Count > 0)
                            sharedViewModel.CurrentComponentType = DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models.StructuralComponentType.Slab;
                    }
                    else if (sharedViewModel.IsSelectingBeam) sharedViewModel.ProcessSelectedBeam(references);
                    else if (sharedViewModel.IsSelectingColumn) sharedViewModel.ProcessSelectedColumn(references);
                    else if (sharedViewModel.IsSelectingFoundation) sharedViewModel.ProcessSelectedSlab(references, isFoundation: true);
                    else sharedViewModel.ProcessSelectedSlab(references, isFoundation: false);
                }
                else
                {
                    sharedViewModel.RevitVersionStatus = $"Đã hủy {title}.";
                }
                
                sharedViewModel.IsSelectingSlab = false;
                sharedViewModel.IsSelectingFoundation = false;
                sharedViewModel.IsSelectingBeam = false;
                sharedViewModel.IsSelectingColumn = false;
                sharedViewModel.IsSelectingComponent = false;
            }
            else
            {
                System.Collections.Generic.IList<Element> elements = new System.Collections.Generic.List<Element>();
                try
                {
                    elements = uiDoc.Selection.PickElementsByRectangle(
                        filter,
                        $"Quét vùng chuột để chọn nhiều {title}.");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Lỗi {title}: {ex.Message}", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }

                if (elements != null && elements.Count > 0)
                {
                    var sweepRefs = new System.Collections.Generic.List<Reference>();
                    foreach (var elem in elements) sweepRefs.Add(new Reference(elem));
                    
                    if (sharedViewModel.IsSweepSelectingComponent)
                    {
                        var doc = uiDoc.Document;
                        var slabRefs = new System.Collections.Generic.List<Reference>();
                        var foundationRefs = new System.Collections.Generic.List<Reference>();
                        var beamRefs = new System.Collections.Generic.List<Reference>();
                        var columnRefs = new System.Collections.Generic.List<Reference>();

                        foreach (var r in sweepRefs)
                        {
                            var elem = doc.GetElement(r);
                            if (elem == null || elem.Category == null) continue;
                            BuiltInCategory cat = elem.Category.BuiltInCategory;
                            
                            if (cat == BuiltInCategory.OST_StructuralFoundation) 
                                foundationRefs.Add(r);
                            else if (cat == BuiltInCategory.OST_Floors)
                            {
                                bool isFoundationSlab = false;
                                if (elem is Floor floor && doc.GetElement(floor.GetTypeId()) as FloorType is FloorType floorType && floorType.IsFoundationSlab)
                                {
                                    isFoundationSlab = true;
                                }

                                if (isFoundationSlab) foundationRefs.Add(r);
                                else slabRefs.Add(r);
                            }
                            else if (cat == BuiltInCategory.OST_StructuralFraming) beamRefs.Add(r);
                            else if (cat == BuiltInCategory.OST_StructuralColumns) columnRefs.Add(r);
                        }

                        bool appendCategory = false;
                        if (foundationRefs.Count > 0) { sharedViewModel.ProcessSelectedSlab(foundationRefs, showDialog: false, isFoundation: true, appendSelection: appendCategory); appendCategory = true; }
                        if (slabRefs.Count > 0) { sharedViewModel.ProcessSelectedSlab(slabRefs, showDialog: false, isFoundation: false, appendSelection: appendCategory); appendCategory = true; }
                        if (beamRefs.Count > 0) { sharedViewModel.ProcessSelectedBeam(beamRefs, appendCategory); appendCategory = true; }
                        if (columnRefs.Count > 0) sharedViewModel.ProcessSelectedColumn(columnRefs, appendCategory);
                        sharedViewModel.SetSelectedComponentComposition(
                            slabRefs.Count, foundationRefs.Count, beamRefs.Count, columnRefs.Count);

                        if (slabRefs.Count > 0 || foundationRefs.Count > 0 || beamRefs.Count > 0 || columnRefs.Count > 0)
                        {
                            var parts = new System.Collections.Generic.List<string>();
                            string details = "Đã quét chọn thành công các cấu kiện từ Revit:\n\n";

                            if (foundationRefs.Count > 0)
                            {
                                parts.Add($"{foundationRefs.Count} móng");
                                details += $"• Móng: {foundationRefs.Count} cấu kiện (Diện tích: {sharedViewModel.SelectedSlabArea:F2} m²)\n";
                            }
                            if (slabRefs.Count > 0)
                            {
                                parts.Add($"{slabRefs.Count} sàn");
                                details += $"• Sàn: {slabRefs.Count} cấu kiện (Diện tích: {sharedViewModel.SelectedSlabArea:F2} m²)\n";
                            }
                            if (beamRefs.Count > 0)
                            {
                                parts.Add($"{beamRefs.Count} dầm");
                                details += $"• Dầm: {beamRefs.Count} cấu kiện (Tiết diện: {sharedViewModel.BeamWidth:F0}x{sharedViewModel.BeamDepth:F0} mm)\n";
                            }
                            if (columnRefs.Count > 0)
                            {
                                parts.Add($"{columnRefs.Count} cột");
                                details += $"• Cột: {columnRefs.Count} cấu kiện (Tiết diện: {sharedViewModel.ColumnWidth:F0}x{sharedViewModel.ColumnDepth:F0} mm, H={sharedViewModel.ColumnHeight:F0} mm)\n";
                            }

                            details += "\n- Tự động đồng bộ và tính toán lại các thông số ván khuôn tối ưu.";
                            sharedViewModel.RevitVersionStatus = $"Đã chọn: {string.Join(", ", parts)}";
                            System.Windows.MessageBox.Show(details, "Revit API Integration", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                        }
                        
                        if (sharedViewModel.CurrentComponentType == DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models.StructuralComponentType.Slab && slabRefs.Count == 0 && foundationRefs.Count == 0 && (beamRefs.Count > 0 || columnRefs.Count > 0))
                             sharedViewModel.CurrentComponentType = columnRefs.Count > 0 ? DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models.StructuralComponentType.Column : DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models.StructuralComponentType.Beam;
                    }
                    else if (sharedViewModel.IsSweepSelectingBeam) sharedViewModel.ProcessSelectedBeam(sweepRefs);
                    else if (sharedViewModel.IsSweepSelectingColumn) sharedViewModel.ProcessSelectedColumn(sweepRefs);
                    else if (sharedViewModel.IsSweepSelectingFoundation) sharedViewModel.ProcessSelectedSlab(sweepRefs, isFoundation: true);
                    else sharedViewModel.ProcessSelectedSlab(sweepRefs, isFoundation: false);
                }
                else
                {
                    sharedViewModel.RevitVersionStatus = $"Đã hủy {title}.";
                }
                
                sharedViewModel.IsSweepSelectingSlab = false;
                sharedViewModel.IsSweepSelectingFoundation = false;
                sharedViewModel.IsSweepSelectingBeam = false;
                sharedViewModel.IsSweepSelectingColumn = false;
                sharedViewModel.IsSweepSelectingComponent = false;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class CmdDuLieu : AppCommandBase
    {
        protected override string TargetTab => "DuLieuView";
    }

    [Transaction(TransactionMode.Manual)]
    public class CmdThongSo : AppCommandBase
    {
        protected override string TargetTab => "ThongSoView";
    }

    [Transaction(TransactionMode.Manual)]
    public class CmdTinhToan : AppCommandBase
    {
        protected override string TargetTab => "TinhToanView";
    }

    [Transaction(TransactionMode.Manual)]
    public class CmdKetQua : AppCommandBase
    {
        protected override string TargetTab => "KetQuaView";
    }

    [Transaction(TransactionMode.Manual)]
    public class CmdPreview3D : AppCommandBase
    {
        // PreviewFast selects its rendering mode before navigating. Navigating here used
        // to make the preview tab visible first, which queued a second render with stale
        // mode/state while the WPF window was still being constructed.
        protected override string TargetTab => null;
        protected override string TargetAction => "PreviewFast";
    }

    // ── Lựa Chọn ──

    [Transaction(TransactionMode.Manual)]
    public class CmdSelectComponent : AppCommandBase
    {
        protected override string TargetTab => "DuLieuView";
        protected override string TargetAction => "SelectComponent";
    }

    [Transaction(TransactionMode.Manual)]
    public class CmdSweepSelectComponent : AppCommandBase
    {
        protected override string TargetTab => "DuLieuView";
        protected override string TargetAction => "SweepSelectComponent";
    }

    // ── Dữ Liệu ──

    [Transaction(TransactionMode.Manual)]
    public class CmdSelectGroundFloor : AppCommandBase
    {
        protected override string TargetTab => "DuLieuView";
        protected override string TargetAction => "SelectGroundFloor";
    }

    [Transaction(TransactionMode.Manual)]
    public class CmdUpdateResults : AppCommandBase
    {
        protected override string TargetTab => "DuLieuView";
        protected override string TargetAction => "UpdateResults";
    }

    [Transaction(TransactionMode.Manual)]
    public class CmdRunAnalysis : AppCommandBase
    {
        protected override string TargetTab => "DuLieuView";
        protected override string TargetAction => "RunAnalysis";
    }

    // ── Tính Toán ──

    [Transaction(TransactionMode.Manual)]
    public class CmdRecalculate : AppCommandBase
    {
        protected override string TargetTab => "TinhToanView";
        protected override string TargetAction => "Recalculate";
    }

    [Transaction(TransactionMode.Manual)]
    public class CmdAutoDesign : AppCommandBase
    {
        protected override string TargetTab => "TinhToanView";
        protected override string TargetAction => "AutoDesign";
    }

    // ── Quy trình bóc tách 6 bước ──

    [Transaction(TransactionMode.Manual)]
    public class CmdJoinByConstructionSequence : AppCommandBase
    {
        protected override string TargetTab => "DuLieuView";
        protected override string TargetAction => "JoinByConstructionSequence";
    }

    [Transaction(TransactionMode.Manual)]
    public class CmdNameFormworkComponents : AppCommandBase
    {
        protected override string TargetTab => "DuLieuView";
        protected override string TargetAction => "NameFormworkComponents";
    }

    [Transaction(TransactionMode.Manual)]
    public class CmdLoadFormworkSharedParameters : AppCommandBase
    {
        protected override string TargetTab => "DuLieuView";
        protected override string TargetAction => "LoadFormworkSharedParameters";
    }

    [Transaction(TransactionMode.Manual)]
    public class CmdInitializeFormworkCalculation : AppCommandBase
    {
        protected override string TargetTab => "DuLieuView";
        protected override string TargetAction => "InitializeFormworkCalculation";
    }

    [Transaction(TransactionMode.Manual)]
    public class CmdCreateFormworkSchedules : AppCommandBase
    {
        protected override string TargetTab => "DuLieuView";
        protected override string TargetAction => "CreateFormworkSchedules";
    }

    [Transaction(TransactionMode.Manual)]
    public class CmdExtractFormworkAreas : AppCommandBase
    {
        protected override string TargetTab => "DuLieuView";
        protected override string TargetAction => "ExtractFormworkAreas";
    }

    [Transaction(TransactionMode.Manual)]
    public class CmdRunFullTakeoffWorkflow : AppCommandBase
    {
        protected override string TargetTab => "DuLieuView";
        protected override string TargetAction => "RunFullTakeoffWorkflow";
    }

    // ── Xuất & Mô Hình ──

    [Transaction(TransactionMode.Manual)]
    public class CmdCreateRevitModel : AppCommandBase
    {
        protected override string TargetTab => "KetQuaView";
        protected override string TargetAction => "CreateRevitModel";
    }

    [Transaction(TransactionMode.Manual)]
    public class CmdCalculateAllFloors : AppCommandBase
    {
        protected override string TargetTab => "KetQuaView";
        protected override string TargetAction => "CalculateAllFloors";
    }

    [Transaction(TransactionMode.Manual)]
    public class CmdExportExcel : AppCommandBase
    {
        protected override string TargetTab => "KetQuaView";
        protected override string TargetAction => "ExportExcel";
    }

    // ── Cài Đặt ──

    [Transaction(TransactionMode.Manual)]
    public class CmdSaveSettings : AppCommandBase
    {
        protected override string TargetTab => "ThongSoView";
        protected override string TargetAction => "SaveSettings";
    }

    [Transaction(TransactionMode.Manual)]
    public class CmdResetSettings : AppCommandBase
    {
        protected override string TargetTab => "ThongSoView";
        protected override string TargetAction => "ResetSettings";
    }
}
