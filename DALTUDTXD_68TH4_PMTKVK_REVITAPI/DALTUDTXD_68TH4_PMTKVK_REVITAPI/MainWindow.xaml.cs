using System;
using System.Windows;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Services;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI
{
    public partial class MainWindow : Window
    {
        public SharedDataViewModel SharedSharedDataViewModel { get; private set; }

        public MainWindow(SharedDataViewModel SharedDataViewModel = null)
        {
            InitializeComponent();
            
            SharedSharedDataViewModel = SharedDataViewModel ?? new SharedDataViewModel(new MessageBoxDialogService());

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
            System.Windows.Input.ICommand cmd = null;

            switch (actionName)
            {
                // Lựa Chọn (DuLieuView / SharedDataViewModel)
                case "SelectComponent":
                    cmd = SharedSharedDataViewModel.SelectComponentCommand;
                    break;
                case "SweepSelectComponent":
                    cmd = SharedSharedDataViewModel.SweepSelectComponentCommand;
                    break;

                // Dữ Liệu (DuLieuView / SharedDataViewModel)
                case "SelectGroundFloor":
                    cmd = SharedSharedDataViewModel.SelectGroundFloorCommand;
                    break;
                case "UpdateResults":
                    cmd = SharedSharedDataViewModel.UpdateResultsCommand;
                    break;
                case "RunAnalysis":
                    cmd = SharedSharedDataViewModel.RunAnalysisCommand;
                    break;

                // Tính Toán (TinhToanView / TinhToanViewModel)
                case "Recalculate":
                    if (TinhToanTab.DataContext is TinhToanViewModel tinhToanVm)
                        cmd = tinhToanVm.RecalculateCommand;
                    break;

                // Xuất & Mô Hình (KetQuaView / KetQuaViewModel)
                case "CreateRevitModel":
                    if (KetQuaTab.DataContext is KetQuaViewModel ketQuaVm1)
                        cmd = ketQuaVm1.CreateRevitModelCommand;
                    break;
                case "CalculateAllFloors":
                    if (KetQuaTab.DataContext is KetQuaViewModel ketQuaVm2)
                        cmd = ketQuaVm2.CalculateAllFloorsCommand;
                    break;
                case "ExportExcel":
                    if (KetQuaTab.DataContext is KetQuaViewModel ketQuaVm3)
                        cmd = ketQuaVm3.ExportExcelCommand;
                    break;

                // Cài Đặt (ThongSoView / ThongSoViewModel)
                case "SaveSettings":
                    if (ThongSoTab.DataContext is ThongSoViewModel thongSoVm1)
                        cmd = thongSoVm1.SaveSettingsCommand;
                    break;
                case "ResetSettings":
                    if (ThongSoTab.DataContext is ThongSoViewModel thongSoVm2)
                        cmd = thongSoVm2.ResetSettingsCommand;
                    break;
            }

            if (cmd != null && cmd.CanExecute(null))
            {
                cmd.Execute(null);
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
        }
    }
}
