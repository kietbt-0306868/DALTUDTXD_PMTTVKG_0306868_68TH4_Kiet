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
