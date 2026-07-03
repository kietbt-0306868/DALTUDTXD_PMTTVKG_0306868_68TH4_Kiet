using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels
{
    public class RevitSelectionState : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private bool _isSelectingSlab = false;
        public bool IsSelectingSlab { get => _isSelectingSlab; set { _isSelectingSlab = value; OnPropertyChanged(); } }
        
        private bool _isSweepSelectingSlab = false;
        public bool IsSweepSelectingSlab { get => _isSweepSelectingSlab; set { _isSweepSelectingSlab = value; OnPropertyChanged(); } }

        public SlabSelectionEventArgs LatestSelectionContext { get; set; }
        
        public List<Tuple<string, double>> SelectedSlabInfos { get; set; } = new List<Tuple<string, double>>();
        
        private List<FloorElementModel> _floorElements;
        public List<FloorElementModel> FloorElements { get => _floorElements; set { _floorElements = value; OnPropertyChanged(); } }

        private string _selectedFloorDetailName;
        public string SelectedFloorDetailName { get => _selectedFloorDetailName; set { _selectedFloorDetailName = value; OnPropertyChanged(); } }

        private double _selectedSlabArea = 0.0;
        public double SelectedSlabArea { get => _selectedSlabArea; set { _selectedSlabArea = value; OnPropertyChanged(); } }

        private double _selectedSlabPerimeter = 0.0;
        public double SelectedSlabPerimeter { get => _selectedSlabPerimeter; set { _selectedSlabPerimeter = value; OnPropertyChanged(); } }

        public List<List<System.Windows.Point>> SlabPolygons2D { get; set; } = new List<List<System.Windows.Point>>();
    }
}
