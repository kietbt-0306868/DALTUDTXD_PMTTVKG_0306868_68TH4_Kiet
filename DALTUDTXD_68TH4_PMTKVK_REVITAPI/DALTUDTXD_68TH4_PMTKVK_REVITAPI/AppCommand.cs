using System;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.Attributes;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Services;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI
{
    [Transaction(TransactionMode.Manual)]
    public class AppCommand : IExternalCommand
    {
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

                // Create a single instance of DuLieuViewModel to share across the application
                var dialogService = new MessageBoxDialogService();
                
                Action closeWindowAction = null;
                var anSoCuaSoDelegate = new Action(() => 
                {
                    closeWindowAction?.Invoke();
                });
                
                var sharedViewModel = new DuLieuViewModel(dialogService, anSoCuaSoDelegate);
                sharedViewModel.InitializeRevitContext(doc, uiDoc, null);

                while (true)
                {
                    var mainWindow = new MainWindow(sharedViewModel);
                    System.Windows.Interop.WindowInteropHelper helper = new System.Windows.Interop.WindowInteropHelper(mainWindow);
                    helper.Owner = commandData.Application.MainWindowHandle;

                    closeWindowAction = () => { mainWindow.DialogResult = true; };

                    mainWindow.ShowDialog();

                    if (sharedViewModel.IsSelectingSlab)
                    {
                        try
                        {
                            var reference = uiDoc.Selection.PickObject(
                                Autodesk.Revit.UI.Selection.ObjectType.Element,
                                new FloorSelectionFilter(),
                                "Hãy chọn một sàn (Floor) từ mô hình Revit");

                            sharedViewModel.ProcessSelectedSlab(reference);
                        }
                        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                        {
                            sharedViewModel.RevitVersionStatus = "Đã hủy chọn sàn.";
                        }
                        catch (Exception ex)
                        {
                            System.Windows.MessageBox.Show("Lỗi chọn sàn: " + ex.Message, "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                        }

                        sharedViewModel.IsSelectingSlab = false;
                    }
                    else
                    {
                        break;
                    }
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = "Có lỗi xảy ra: " + ex.Message;
                return Result.Failed;
            }
        }
    }
}
