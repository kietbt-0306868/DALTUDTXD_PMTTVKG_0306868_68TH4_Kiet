using System;
using System.Windows;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Services;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI
{
    public partial class MainWindow : Window
    {
        private string _pendingStartupAction;
        private bool _startupActionExecuted;
        public SharedDataViewModel SharedSharedDataViewModel { get; private set; }

        public MainWindow(SharedDataViewModel SharedDataViewModel = null)
        {
            InitializeComponent();
            
            SharedSharedDataViewModel = SharedDataViewModel ?? new SharedDataViewModel(new MessageBoxDialogService());
            DataContext = SharedSharedDataViewModel;

            if (SharedSharedDataViewModel.RevitDoc == null)
            {
                SharedSharedDataViewModel.InitializeRevitContext(null, null, Navigate);
            }
            else
            {
                // Must still wire up the navigation action if initialized from Revit
                SharedSharedDataViewModel.InitializeRevitContext(SharedSharedDataViewModel.RevitDoc, SharedSharedDataViewModel.RevitUIDoc, Navigate);
            }
            
            DuLieuTab.DataContext = SharedSharedDataViewModel;
            TinhToanTab.DataContext = new TinhToanViewModel(SharedSharedDataViewModel);
            ThongSoTab.DataContext = new ThongSoViewModel(SharedSharedDataViewModel);
            KetQuaTab.DataContext = new KetQuaViewModel(SharedSharedDataViewModel);
            Preview3DTab.DataContext = new Preview3DViewModel(SharedSharedDataViewModel);
        }

        public void QueueStartupAction(string actionName)
        {
            _pendingStartupAction = actionName;
            _startupActionExecuted = false;
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            if (_startupActionExecuted || string.IsNullOrWhiteSpace(_pendingStartupAction)) return;
            _startupActionExecuted = true;
            string action = _pendingStartupAction;
            _pendingStartupAction = null;
            // ContentRendered already runs while IExternalCommand.Execute is on the stack.
            // Do not leave a Dispatcher callback behind: if WPF initialization fails and
            // the external command returns, such a callback would later open a Transaction
            // outside Revit API context and trigger Revit's documented soft-crash path.
            ExecuteStartupAction(action);
        }

        public void Navigate(string tabName)
        {
            DuLieuTab.Visibility = Visibility.Collapsed;
            TinhToanTab.Visibility = Visibility.Collapsed;
            ThongSoTab.Visibility = Visibility.Collapsed;
            KetQuaTab.Visibility = Visibility.Collapsed;
            Preview3DTab.Visibility = Visibility.Collapsed;

            switch (tabName)
            {
                case "DuLieuView": DuLieuTab.Visibility = Visibility.Visible; break;
                case "TinhToanView":
                case "Report": TinhToanTab.Visibility = Visibility.Visible; break;
                case "ThongSoView":
                case "Settings": ThongSoTab.Visibility = Visibility.Visible; break;
                case "KetQuaView": KetQuaTab.Visibility = Visibility.Visible; break;
                case "Preview3DView": Preview3DTab.Visibility = Visibility.Visible; break;
            }
        }

        /// <summary>
        /// Auto-execute a specific action triggered by a Revit ribbon button.
        /// Maps action name strings to the corresponding ICommand on the active ViewModel.
        /// </summary>
        public void ExecuteStartupAction(string actionName)
        {
            try
            {
                if (actionName == "PreviewFast")
                {
                    SharedSharedDataViewModel.RequestFastPreview3D();
                    return;
                }

                System.Windows.Input.ICommand cmd = null;
                object commandParameter = null;
                switch (actionName)
                {
                    case "SelectComponent": cmd = SharedSharedDataViewModel.SelectComponentCommand; break;
                    case "SweepSelectComponent": cmd = SharedSharedDataViewModel.SweepSelectComponentCommand; break;
                    case "SelectGroundFloor": cmd = SharedSharedDataViewModel.SelectGroundFloorCommand; break;
                    case "UpdateResults": cmd = SharedSharedDataViewModel.UpdateResultsCommand; break;
                    case "RunAnalysis": cmd = SharedSharedDataViewModel.RunAnalysisCommand; break;
                    case "JoinByConstructionSequence": cmd = SharedSharedDataViewModel.JoinByConstructionSequenceCommand; break;
                    case "NameFormworkComponents": cmd = SharedSharedDataViewModel.NameFormworkComponentsCommand; break;
                    case "LoadFormworkSharedParameters": cmd = SharedSharedDataViewModel.LoadFormworkSharedParametersCommand; break;
                    case "InitializeFormworkCalculation": cmd = SharedSharedDataViewModel.InitializeFormworkCalculationCommand; break;
                    case "CreateFormworkSchedules": cmd = SharedSharedDataViewModel.CreateFormworkSchedulesCommand; break;
                    case "ExtractFormworkAreas": cmd = SharedSharedDataViewModel.ExtractFormworkAreasCommand; break;
                    case "RunFullTakeoffWorkflow": cmd = SharedSharedDataViewModel.RunFullTakeoffWorkflowCommand; break;
                    case "Recalculate":
                        if (TinhToanTab.DataContext is TinhToanViewModel calculation) cmd = calculation.RecalculateCommand;
                        break;
                    case "AutoDesign":
                        if (TinhToanTab.DataContext is TinhToanViewModel autoDesign) cmd = autoDesign.AutoFixPropsCommand;
                        break;
                    case "CreateRevitModel":
                        if (KetQuaTab.DataContext is KetQuaViewModel model) cmd = model.CreateRevitModelCommand;
                        break;
                    case "CalculateAllFloors":
                        if (KetQuaTab.DataContext is KetQuaViewModel takeoff) cmd = takeoff.CalculateAllFloorsCommand;
                        break;
                    case "ExportExcel":
                        if (KetQuaTab.DataContext is KetQuaViewModel excel) cmd = excel.ExportExcelCommand;
                        break;
                    case "SaveSettings":
                        if (ThongSoTab.DataContext is ThongSoViewModel save) cmd = save.SaveSettingsCommand;
                        break;
                    case "ResetSettings":
                        if (ThongSoTab.DataContext is ThongSoViewModel reset) cmd = reset.ResetSettingsCommand;
                        break;
                    case "SelectScaffoldSystems":
                        if (ThongSoTab.DataContext is ThongSoViewModel scaffold)
                        {
                            cmd = scaffold.SelectScaffoldSystemsCommand;
                            commandParameter = this;
                        }
                        break;
                }

                if (cmd == null)
                    throw new InvalidOperationException($"Không tìm thấy lệnh giao diện '{actionName}'.");
                if (!cmd.CanExecute(commandParameter))
                {
                    MessageBox.Show("Lệnh chưa thể chạy ở trạng thái hiện tại. Hãy kiểm tra cấu kiện đã chọn và dữ liệu đầu vào.",
                        "Lệnh chưa sẵn sàng", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                cmd.Execute(commandParameter);
            }
            catch (Exception exception)
            {
                MessageBox.Show($"Không thể thực hiện lệnh '{actionName}'.\n\n{exception.Message}",
                    "Lỗi lệnh Ribbon", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            
            if (TinhToanTab.DataContext is IDisposable tinhToanVm)
            {
                tinhToanVm.Dispose();
            }
            if (ThongSoTab.DataContext is IDisposable thongSoVm)
            {
                thongSoVm.Dispose();
            }
            if (KetQuaTab.DataContext is IDisposable ketQuaVm)
            {
                ketQuaVm.Dispose();
            }
            if (Preview3DTab.DataContext is IDisposable preview3DVm)
            {
                preview3DVm.Dispose();
            }
        }
    }
}
