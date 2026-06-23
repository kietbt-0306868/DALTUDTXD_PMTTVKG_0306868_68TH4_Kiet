using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels
{
    public class DuLieuViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

