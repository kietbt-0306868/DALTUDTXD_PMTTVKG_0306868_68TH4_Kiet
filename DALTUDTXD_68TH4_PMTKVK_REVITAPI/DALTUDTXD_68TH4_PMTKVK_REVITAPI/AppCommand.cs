using System;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.Attributes;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Services;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI
{
    [Transaction(TransactionMode.Manual)]
    public abstract class AppCommandBase : IExternalCommand
    {
        protected abstract string TargetTab { get; }


        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool EnableWindow(IntPtr hWnd, bool bEnable);

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

                // Create a single instance of SharedDataViewModel to share across the application
                var dialogService = new MessageBoxDialogService();
                
                Action closeWindowAction = null;
                var anSoCuaSoDelegate = new Action(() => 
                {
                    closeWindowAction?.Invoke();
                });
                
                var sharedViewModel = new SharedDataViewModel(dialogService, anSoCuaSoDelegate);
                sharedViewModel.InitializeRevitContext(doc, uiDoc, null);
                sharedViewModel.StartupTab = TargetTab;

                while (true)
                {
                    var dummyOwner = new System.Windows.Window()
                    {
                        Width = 0, Height = 0,
                        WindowStyle = System.Windows.WindowStyle.None,
                        ShowInTaskbar = false,
                        AllowsTransparency = true,
                        Background = System.Windows.Media.Brushes.Transparent,
                        Opacity = 0
                    };
                    dummyOwner.Show();

                    var mainWindow = new MainWindow(sharedViewModel);
                    mainWindow.Owner = dummyOwner;

                    IntPtr revitHandle = commandData.Application.MainWindowHandle;
                    // Bỏ khóa Revit để cho phép tương tác (Pan, Zoom, đổi Level)
                    // EnableWindow(revitHandle, false);

                    closeWindowAction = () => { mainWindow.DialogResult = true; };

                    if (!string.IsNullOrEmpty(sharedViewModel.StartupTab))
                    {
                        mainWindow.Navigate(sharedViewModel.StartupTab);
                        sharedViewModel.ExecuteChangeTab(sharedViewModel.StartupTab);
                        sharedViewModel.StartupTab = null;
                    }

                    try
                    {
                        mainWindow.ShowDialog();
                    }
                    finally
                    {
                        // EnableWindow(revitHandle, true);
                        dummyOwner.Close();
                        sharedViewModel.DisposePreviewControl();
                    }

                    if (sharedViewModel.IsSelectingSlab || sharedViewModel.IsSweepSelectingSlab)
                    {
                        HandleSelection(uiDoc, sharedViewModel);
                    }
                    else
                    {
                        break;
                    }
                }

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
                message = "Có lỗi xảy ra: " + ex.Message;
                return Result.Failed;
            }
        }

        private void HandleSelection(UIDocument uiDoc, SharedDataViewModel sharedViewModel)
        {
            if (sharedViewModel.IsSelectingSlab)
            {
                string title = "chọn sàn";
                System.Collections.Generic.IList<Reference> references = new System.Collections.Generic.List<Reference>();
                try
                {
                    references = uiDoc.Selection.PickObjects(
                        Autodesk.Revit.UI.Selection.ObjectType.Element,
                        new FloorSelectionFilter(),
                        "Quét vùng chuột hoặc giữ Ctrl để chọn nhiều sàn. NHẤN NÚT FINISH Ở GÓC TRÊN CÙNG BÊN TRÁI SAU KHI XONG!");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Lỗi {title}: {ex.Message}", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }

                if (references != null && references.Count > 0)
                {
                    sharedViewModel.ProcessSelectedSlab(references);
                }
                else
                {
                    sharedViewModel.RevitVersionStatus = $"Đã hủy {title}.";
                }
                
                sharedViewModel.IsSelectingSlab = false;
            }
            else if (sharedViewModel.IsSweepSelectingSlab)
            {
                string title = "quét chọn sàn";
                System.Collections.Generic.IList<Element> elements = new System.Collections.Generic.List<Element>();
                try
                {
                    elements = uiDoc.Selection.PickElementsByRectangle(
                        new FloorSelectionFilter(),
                        "Quét vùng chuột để chọn nhiều sàn.");
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
                    sharedViewModel.ProcessSelectedSlab(sweepRefs);
                }
                else
                {
                    sharedViewModel.RevitVersionStatus = $"Đã hủy {title}.";
                }
                
                sharedViewModel.IsSweepSelectingSlab = false;
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
        protected override string TargetTab => "Preview3DView";
    }
}
