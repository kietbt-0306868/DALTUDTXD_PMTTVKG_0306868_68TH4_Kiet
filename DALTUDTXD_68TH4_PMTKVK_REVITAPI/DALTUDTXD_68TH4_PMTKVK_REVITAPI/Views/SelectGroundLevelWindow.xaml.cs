using System.Collections.Generic;
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
                LevelComboBox.SelectedIndex = 0;
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
