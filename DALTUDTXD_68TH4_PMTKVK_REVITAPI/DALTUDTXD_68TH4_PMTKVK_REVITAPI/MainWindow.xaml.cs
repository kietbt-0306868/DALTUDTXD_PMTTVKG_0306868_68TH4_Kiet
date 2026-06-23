using System;
using System.Windows;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Services;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI
{
    public partial class MainWindow : Window
    {
        public DuLieuViewModel SharedDuLieuViewModel { get; private set; }

        public MainWindow(DuLieuViewModel duLieuViewModel = null)
        {
            InitializeComponent();
            
            SharedDuLieuViewModel = duLieuViewModel ?? new DuLieuViewModel(new MessageBoxDialogService());

            if (SharedDuLieuViewModel.RevitDoc == null)
            {
                SharedDuLieuViewModel.InitializeRevitContext(null, null, Navigate);
            }
            else
            {
                // Must still wire up the navigation action if initialized from Revit
                SharedDuLieuViewModel.InitializeRevitContext(SharedDuLieuViewModel.RevitDoc, SharedDuLieuViewModel.RevitUIDoc, Navigate);
            }
            
            DuLieuTab.DataContext = SharedDuLieuViewModel;
            TinhToanTab.DataContext = new TinhToanViewModel(SharedDuLieuViewModel);
            ThongSoTab.DataContext = new ThongSoViewModel(SharedDuLieuViewModel);
            KetQuaTab.DataContext = new KetQuaViewModel(SharedDuLieuViewModel);
        }

        public void Navigate(string tabName)
        {
            DuLieuTab.Visibility = Visibility.Collapsed;
            TinhToanTab.Visibility = Visibility.Collapsed;
            ThongSoTab.Visibility = Visibility.Collapsed;
            KetQuaTab.Visibility = Visibility.Collapsed;

            switch (tabName)
            {
                case "DuLieuView": DuLieuTab.Visibility = Visibility.Visible; break;
                case "TinhToanView":
                case "Report": TinhToanTab.Visibility = Visibility.Visible; break;
                case "ThongSoView":
                case "Settings": ThongSoTab.Visibility = Visibility.Visible; break;
                case "KetQuaView": KetQuaTab.Visibility = Visibility.Visible; break;
            }
        }
    }
}
