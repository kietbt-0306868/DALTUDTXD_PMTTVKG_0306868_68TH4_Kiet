using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Services;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels
{
    public class ThongSoViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly DuLieuViewModel _duLieuViewModel;
        private readonly IDialogService _dialogService;
        private string _revitVersionStatus;
        private bool _disposed;

        public event PropertyChangedEventHandler PropertyChanged;

        public ThongSoViewModel(DuLieuViewModel duLieuViewModel, IDialogService dialogService = null)
        {
            _duLieuViewModel = duLieuViewModel ?? throw new ArgumentNullException(nameof(duLieuViewModel));
            _dialogService = dialogService ?? new MessageBoxDialogService();

            _duLieuViewModel.PropertyChanged += OnDuLieuPropertyChanged;

            SaveSettingsCommand = new RelayCommand(o => ExecuteSaveSettings());
            ResetSettingsCommand = new RelayCommand(o => ExecuteResetSettings());
            ChangeTabCommand = new RelayCommand(ExecuteChangeTab);
            
            RevitVersionStatus = "Revit API - Cấu hình hệ số thiết kế";
        }

        private void OnDuLieuPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e.PropertyName);
        }

        public double WoodFormworkWeight
        {
            get => _duLieuViewModel.WoodFormworkWeight;
            set { _duLieuViewModel.WoodFormworkWeight = value; }
        }

        public double ConcreteWeight
        {
            get => _duLieuViewModel.ConcreteWeight;
            set { _duLieuViewModel.ConcreteWeight = value; }
        }

        public double ConstructionLiveLoad
        {
            get => _duLieuViewModel.ConstructionLiveLoad;
            set { _duLieuViewModel.ConstructionLiveLoad = value; }
        }

        public double DeadLoadSafetyFactor
        {
            get => _duLieuViewModel.DeadLoadSafetyFactor;
            set { _duLieuViewModel.DeadLoadSafetyFactor = value; }
        }

        public double LiveLoadSafetyFactor
        {
            get => _duLieuViewModel.LiveLoadSafetyFactor;
            set { _duLieuViewModel.LiveLoadSafetyFactor = value; }
        }

        public double MoistureFactor
        {
            get => _duLieuViewModel.MoistureFactor;
            set { _duLieuViewModel.MoistureFactor = value; }
        }

        public double TemperatureFactor
        {
            get => _duLieuViewModel.TemperatureFactor;
            set { _duLieuViewModel.TemperatureFactor = value; }
        }

        // Geometric properties for UI bindings
        public double VanMatThickness
        {
            get => _duLieuViewModel.VanMatThickness;
            set { _duLieuViewModel.VanMatThickness = value; OnPropertyChanged(); }
        }

        public double XaGoPhuWidth
        {
            get => _duLieuViewModel.XaGoPhuWidth;
            set { _duLieuViewModel.XaGoPhuWidth = value; OnPropertyChanged(); }
        }

        public double XaGoPhuHeight
        {
            get => _duLieuViewModel.XaGoPhuHeight;
            set { _duLieuViewModel.XaGoPhuHeight = value; OnPropertyChanged(); }
        }

        public double XaGoChinhWidth
        {
            get => _duLieuViewModel.XaGoChinhWidth;
            set { _duLieuViewModel.XaGoChinhWidth = value; OnPropertyChanged(); }
        }

        public double XaGoChinhHeight
        {
            get => _duLieuViewModel.XaGoChinhHeight;
            set { _duLieuViewModel.XaGoChinhHeight = value; OnPropertyChanged(); }
        }

        public double CayChongDiameter
        {
            get => _duLieuViewModel.CayChongDiameter;
            set { _duLieuViewModel.CayChongDiameter = value; OnPropertyChanged(); }
        }

        public double CayChongThickness
        {
            get => _duLieuViewModel.CayChongThickness;
            set { _duLieuViewModel.CayChongThickness = value; OnPropertyChanged(); }
        }

        public double Joist1Spacing
        {
            get => _duLieuViewModel.Joist1Spacing;
            set { _duLieuViewModel.Joist1Spacing = value; OnPropertyChanged(); }
        }

        public double Joist2Spacing
        {
            get => _duLieuViewModel.Joist2Spacing;
            set { _duLieuViewModel.Joist2Spacing = value; OnPropertyChanged(); }
        }

        public double SupportSpacing
        {
            get => _duLieuViewModel.SupportSpacing;
            set { _duLieuViewModel.SupportSpacing = value; OnPropertyChanged(); }
        }

        public string RevitVersionStatus
        {
            get => _revitVersionStatus;
            set { _revitVersionStatus = value; OnPropertyChanged(); }
        }

        public ICommand SaveSettingsCommand { get; }
        public ICommand ResetSettingsCommand { get; }
        public ICommand ChangeTabCommand { get; }

        private void ExecuteSaveSettings()
        {
            _dialogService.ShowInfo("Đã áp dụng các hệ số an toàn và tải trọng thiết kế thành công!\nCác thông số này sẽ được áp dụng trực tiếp vào mô hình phân tích.", "Cấu hình thiết kế");
            RevitVersionStatus = "Đã áp dụng cấu hình thiết kế lúc " + DateTime.Now.ToString("HH:mm:ss");
        }

        private void ExecuteResetSettings()
        {
            ResetToDefaults();
            _dialogService.ShowInfo("Đã khôi phục toàn bộ thông số thiết kế về giá trị mặc định của TCVN 10307:2014.", "Cấu hình thiết kế");
            RevitVersionStatus = "Đã đặt lại cấu hình mặc định lúc " + DateTime.Now.ToString("HH:mm:ss");
        }

        private void ResetToDefaults()
        {
            WoodFormworkWeight = 0.30;
            ConcreteWeight = 25.00;
            ConstructionLiveLoad = 2.50;
            DeadLoadSafetyFactor = 1.20;
            LiveLoadSafetyFactor = 1.30;
            MoistureFactor = 0.85;
            TemperatureFactor = 1.00;

            VanMatThickness = 18.0;
            XaGoPhuWidth = 50.0;
            XaGoPhuHeight = 100.0;
            XaGoChinhWidth = 100.0;
            XaGoChinhHeight = 100.0;
            CayChongDiameter = 48.0;
            CayChongThickness = 2.0;

            Joist1Spacing = 400.0;
            Joist2Spacing = 1000.0;
            SupportSpacing = 1000.0;
        }

        private void ExecuteChangeTab(object parameter)
        {
            _duLieuViewModel.NavigateAction?.Invoke(parameter?.ToString());
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_duLieuViewModel != null)
                    {
                        _duLieuViewModel.PropertyChanged -= OnDuLieuPropertyChanged;
                    }
                }
                _disposed = true;
            }
        }
    }
}
