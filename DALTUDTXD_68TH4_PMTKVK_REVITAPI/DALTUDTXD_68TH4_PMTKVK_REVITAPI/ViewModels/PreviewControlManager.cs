using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels
{
    public class PreviewControlManager : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private FrameworkElement _sharedPreviewControl;
        public FrameworkElement SharedPreviewControl { get => _sharedPreviewControl; set { _sharedPreviewControl = value; OnPropertyChanged(); } }

        private FrameworkElement _duLieuPreviewControl;
        public FrameworkElement DuLieuPreviewControl { get => _duLieuPreviewControl; set { _duLieuPreviewControl = value; OnPropertyChanged(); } }

        private FrameworkElement _tinhToanPreviewControl;
        public FrameworkElement TinhToanPreviewControl { get => _tinhToanPreviewControl; set { _tinhToanPreviewControl = value; OnPropertyChanged(); } }

        private FrameworkElement _ketQuaPreviewControl;
        public FrameworkElement KetQuaPreviewControl { get => _ketQuaPreviewControl; set { _ketQuaPreviewControl = value; OnPropertyChanged(); } }

        public void DisposePreviewControl()
        {
            DuLieuPreviewControl = null;
            TinhToanPreviewControl = null;
            KetQuaPreviewControl = null;
            
            if (SharedPreviewControl is IDisposable disposable)
            {
                disposable.Dispose();
            }
            SharedPreviewControl = null;
        }
    }
}
