using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.Views
{
    public partial class SelectGroundLevelWindow : Window
    {
        public LevelModel SelectedLevel { get; private set; }

        public SelectGroundLevelWindow(List<LevelModel> levels, LevelModel currentSelected)
        {
            InitializeComponent();
            LevelComboBox.ItemsSource = levels;
            if (currentSelected != null)
            {
                foreach (var lvl in levels)
                {
                    if (lvl.Name == currentSelected.Name)
                    {
                        LevelComboBox.SelectedItem = lvl;
                        break;
                    }
                }
            }
            else if (levels.Count > 0)
            {
                // Ground floor is normally the level nearest project elevation 0.
                // Defaulting to the lowest level accidentally selected pile-cap/
                // foundation levels and enabled shoring below the actual ground floor.
                LevelComboBox.SelectedItem = levels
                    .OrderBy(level => Math.Abs(level.Elevation))
                    .ThenByDescending(level => level.Elevation)
                    .First();
            }
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            if (LevelComboBox.SelectedItem is LevelModel lvl)
            {
                SelectedLevel = lvl;
                DialogResult = true;
            }
            else
            {
                MessageBox.Show("Vui lòng chọn một tầng.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
