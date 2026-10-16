using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Services;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Views;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels
{
    public class ThongSoViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly SharedDataViewModel _SharedDataViewModel;
        private readonly IDialogService _dialogService;
        private string _revitVersionStatus;
        private bool _disposed;

        public event PropertyChangedEventHandler PropertyChanged;

        public ThongSoViewModel(SharedDataViewModel SharedDataViewModel, IDialogService dialogService = null)
        {
            _SharedDataViewModel = SharedDataViewModel ?? throw new ArgumentNullException(nameof(SharedDataViewModel));
            _dialogService = dialogService ?? new MessageBoxDialogService();

            _SharedDataViewModel.PropertyChanged += OnDuLieuPropertyChanged;
            _SharedDataViewModel.SlabSelectionCompleted += OnSlabSelectionCompleted;

            SaveSettingsCommand = new RelayCommand(o => ExecuteSaveSettings());
            ResetSettingsCommand = new RelayCommand(o => ExecuteResetSettings());
            SelectScaffoldSystemsCommand = new RelayCommand(ExecuteSelectScaffoldSystems);
            ChangeTabCommand = new RelayCommand(ExecuteChangeTab);
            
            RevitVersionStatus = "Revit API - Cấu hình hệ số thiết kế";

            if (_SharedDataViewModel.LatestSelectionContext != null)
            {
                OnSlabSelectionCompleted(this, _SharedDataViewModel.LatestSelectionContext);
            }
        }

        private void OnDuLieuPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e.PropertyName);
        }

        private void OnSlabSelectionCompleted(object sender, SlabSelectionEventArgs e)
        {
            OnPropertyChanged(nameof(SlabThickness));
            RevitVersionStatus = $"Đồng bộ từ Revit: {e.FloorCount} sàn - Dày: {e.SlabThickness:F0} mm - Cập nhật lúc {DateTime.Now:HH:mm:ss}";
        }

        public double SlabThickness
        {
            get => _SharedDataViewModel.SlabThickness;
            set { _SharedDataViewModel.SlabThickness = value; OnPropertyChanged(); }
        }

        public double WoodFormworkWeight
        {
            get => _SharedDataViewModel.WoodFormworkWeight;
            set { _SharedDataViewModel.WoodFormworkWeight = value; }
        }

        public double ConcreteWeight
        {
            get => _SharedDataViewModel.ConcreteWeight;
            set { _SharedDataViewModel.ConcreteWeight = value; }
        }

        public double SteelWeight
        {
            get => _SharedDataViewModel.SteelWeight;
            set { _SharedDataViewModel.SteelWeight = value; }
        }

        public double ConstructionLiveLoad
        {
            get => _SharedDataViewModel.ConstructionLiveLoad;
            set { _SharedDataViewModel.ConstructionLiveLoad = value; }
        }

        public double PouringImpactLoad
        {
            get => _SharedDataViewModel.PouringImpactLoad;
            set { _SharedDataViewModel.PouringImpactLoad = value; }
        }

        public double VibrationLoad
        {
            get => _SharedDataViewModel.VibrationLoad;
            set { _SharedDataViewModel.VibrationLoad = value; }
        }

        public double DeadLoadSafetyFactor
        {
            get => _SharedDataViewModel.DeadLoadSafetyFactor;
            set { _SharedDataViewModel.DeadLoadSafetyFactor = value; }
        }

        public double LiveLoadSafetyFactor
        {
            get => _SharedDataViewModel.LiveLoadSafetyFactor;
            set { _SharedDataViewModel.LiveLoadSafetyFactor = value; }
        }

        public double MoistureFactor
        {
            get => _SharedDataViewModel.MoistureFactor;
            set { _SharedDataViewModel.MoistureFactor = value; }
        }

        public double TemperatureFactor
        {
            get => _SharedDataViewModel.TemperatureFactor;
            set { _SharedDataViewModel.TemperatureFactor = value; }
        }

        public double WoodStrength
        {
            get => _SharedDataViewModel.WoodStrength;
            set { _SharedDataViewModel.WoodStrength = value; OnPropertyChanged(); }
        }

        public double ElasticModulus
        {
            get => _SharedDataViewModel.ElasticModulus;
            set { _SharedDataViewModel.ElasticModulus = value; OnPropertyChanged(); }
        }

        public string SlabDeflectionLimit
        {
            get => _SharedDataViewModel.SlabDeflectionLimit;
            set { _SharedDataViewModel.SlabDeflectionLimit = value; OnPropertyChanged(); }
        }

        public string JoistDeflectionLimit
        {
            get => _SharedDataViewModel.JoistDeflectionLimit;
            set { _SharedDataViewModel.JoistDeflectionLimit = value; OnPropertyChanged(); }
        }

        // Geometric properties for UI bindings
        public double VanMatThickness
        {
            get => _SharedDataViewModel.VanMatThickness;
            set { _SharedDataViewModel.VanMatThickness = value; OnPropertyChanged(); }
        }

        public double XaGoPhuWidth
        {
            get => _SharedDataViewModel.XaGoPhuWidth;
            set { _SharedDataViewModel.XaGoPhuWidth = value; OnPropertyChanged(); }
        }

        public double XaGoPhuHeight
        {
            get => _SharedDataViewModel.XaGoPhuHeight;
            set { _SharedDataViewModel.XaGoPhuHeight = value; OnPropertyChanged(); }
        }

        public double XaGoChinhWidth
        {
            get => _SharedDataViewModel.XaGoChinhWidth;
            set { _SharedDataViewModel.XaGoChinhWidth = value; OnPropertyChanged(); }
        }

        public double XaGoChinhHeight
        {
            get => _SharedDataViewModel.XaGoChinhHeight;
            set { _SharedDataViewModel.XaGoChinhHeight = value; OnPropertyChanged(); }
        }

        public double CayChongDiameter
        {
            get => _SharedDataViewModel.CayChongDiameter;
            set { _SharedDataViewModel.CayChongDiameter = value; OnPropertyChanged(); }
        }

        public double CayChongThickness
        {
            get => _SharedDataViewModel.CayChongThickness;
            set { _SharedDataViewModel.CayChongThickness = value; OnPropertyChanged(); }
        }

        public double CayChongHeight
        {
            get => _SharedDataViewModel.CayChongHeight;
            set { _SharedDataViewModel.CayChongHeight = value; OnPropertyChanged(); }
        }

        public int SoNhipVanMat
        {
            get => _SharedDataViewModel.SoNhipVanMat;
            set { _SharedDataViewModel.SoNhipVanMat = value; OnPropertyChanged(); }
        }

        public int SoNhipXaGoPhu
        {
            get => _SharedDataViewModel.SoNhipXaGoPhu;
            set { _SharedDataViewModel.SoNhipXaGoPhu = value; OnPropertyChanged(); }
        }

        public int SoNhipXaGoChinh
        {
            get => _SharedDataViewModel.SoNhipXaGoChinh;
            set { _SharedDataViewModel.SoNhipXaGoChinh = value; OnPropertyChanged(); }
        }

        public int SelectedTieuChuan
        {
            get => _SharedDataViewModel.SelectedTieuChuan;
            set { _SharedDataViewModel.SelectedTieuChuan = value; OnPropertyChanged(); }
        }

        public double Joist1Spacing
        {
            get => _SharedDataViewModel.Joist1Spacing;
            set { _SharedDataViewModel.Joist1Spacing = value; OnPropertyChanged(); }
        }

        public double Joist2Spacing
        {
            get => _SharedDataViewModel.Joist2Spacing;
            set { _SharedDataViewModel.Joist2Spacing = value; OnPropertyChanged(); }
        }

        public double SupportSpacing
        {
            get => _SharedDataViewModel.SupportSpacing;
            set { _SharedDataViewModel.SupportSpacing = value; OnPropertyChanged(); }
        }

        public ScaffoldSystemType SelectedScaffoldSystems => _SharedDataViewModel.SelectedScaffoldSystems;
        public bool UseFrameScaffold
        {
            get => _SharedDataViewModel.UseFrameScaffold;
            set => _SharedDataViewModel.UseFrameScaffold = value;
        }
        public bool UseWedgeScaffold
        {
            get => _SharedDataViewModel.UseWedgeScaffold;
            set => _SharedDataViewModel.UseWedgeScaffold = value;
        }
        public bool UseRinglockScaffold
        {
            get => _SharedDataViewModel.UseRinglockScaffold;
            set => _SharedDataViewModel.UseRinglockScaffold = value;
        }
        public string SelectedScaffoldSystemsSummary => _SharedDataViewModel.SelectedScaffoldSystemsSummary;

        public string RevitVersionStatus
        {
            get => _revitVersionStatus;
            set { _revitVersionStatus = value; OnPropertyChanged(); }
        }

        public ICommand SaveSettingsCommand { get; }
        public ICommand ResetSettingsCommand { get; }
        public ICommand SelectScaffoldSystemsCommand { get; }
        public ICommand ChangeTabCommand { get; }

        private void ExecuteSaveSettings()
        {
            if (!ScaffoldSystemPreferenceStore.TrySave(_SharedDataViewModel.SelectedScaffoldSystems, out string error))
            {
                _dialogService.ShowWarning("Các thông số đã được áp dụng cho phiên làm việc hiện tại, nhưng không thể lưu lựa chọn hệ giàn giáo cho lần mở sau.\n\n" + error,
                    "Cấu hình thiết kế");
                return;
            }
            _dialogService.ShowInfo("Đã áp dụng cấu hình thiết kế.\nHệ giàn giáo: " +
                                    _SharedDataViewModel.SelectedScaffoldSystemsSummary +
                                    "\nLựa chọn này được lưu cho lần mở phần mềm sau.", "Cấu hình thiết kế");
            RevitVersionStatus = "Đã áp dụng cấu hình thiết kế lúc " + DateTime.Now.ToString("HH:mm:ss");
        }

        private void ExecuteResetSettings()
        {
            ResetToDefaults();
            bool saved = ScaffoldSystemPreferenceStore.TrySave(_SharedDataViewModel.SelectedScaffoldSystems,
                out string error);
            string persistenceNote = saved
                ? "\nHệ giàn giáo mặc định: Ringlock."
                : "\nKhông thể lưu lựa chọn mặc định cho lần mở sau: " + error;
            _dialogService.ShowInfo("Đã khôi phục toàn bộ thông số thiết kế về giá trị mặc định của TCVN 10307:2014." +
                                    persistenceNote, "Cấu hình thiết kế");
            RevitVersionStatus = "Đã đặt lại cấu hình mặc định lúc " + DateTime.Now.ToString("HH:mm:ss");
        }

        private void ExecuteSelectScaffoldSystems(object parameter)
        {
            var window = new ScaffoldSystemSelectionWindow(_SharedDataViewModel.SelectedScaffoldSystems);
            if (parameter is Window owner && owner.IsVisible) window.Owner = owner;
            if (window.ShowDialog() != true) return;

            _SharedDataViewModel.SelectedScaffoldSystems = window.SelectedSystems;
            if (!ScaffoldSystemPreferenceStore.TrySave(window.SelectedSystems, out string error))
            {
                _dialogService.ShowWarning("Đã áp dụng lựa chọn cho phiên làm việc hiện tại nhưng không thể lưu cho lần mở sau.\n\n" + error,
                    "Chọn hệ giàn giáo");
            }
            RevitVersionStatus = "Hệ giàn giáo: " + _SharedDataViewModel.SelectedScaffoldSystemsSummary;
        }

        private void ResetToDefaults()
        {
            _SharedDataViewModel.LoadDefaults();
        }

        private void ExecuteChangeTab(object parameter)
        {
            _SharedDataViewModel.ExecuteChangeTab(parameter);
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
                    if (_SharedDataViewModel != null)
                    {
                        _SharedDataViewModel.PropertyChanged -= OnDuLieuPropertyChanged;
                        _SharedDataViewModel.SlabSelectionCompleted -= OnSlabSelectionCompleted;
                    }
                }
                _disposed = true;
            }
        }
    }
}
