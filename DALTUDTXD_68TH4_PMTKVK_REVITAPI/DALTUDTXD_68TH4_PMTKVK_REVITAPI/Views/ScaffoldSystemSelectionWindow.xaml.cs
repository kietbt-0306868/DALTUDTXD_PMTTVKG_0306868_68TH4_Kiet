using System.Windows;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.Views
{
    public partial class ScaffoldSystemSelectionWindow : Window
    {
        public ScaffoldSystemType SelectedSystems { get; private set; }

        public ScaffoldSystemSelectionWindow(ScaffoldSystemType selectedSystems)
        {
            InitializeComponent();
            selectedSystems = selectedSystems.Normalize();
            FrameCheckBox.IsChecked = (selectedSystems & ScaffoldSystemType.Frame) != 0;
            WedgeCheckBox.IsChecked = (selectedSystems & ScaffoldSystemType.Wedge) != 0;
            RinglockCheckBox.IsChecked = (selectedSystems & ScaffoldSystemType.Ringlock) != 0;
            SelectedSystems = selectedSystems;
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            ScaffoldSystemType selected = ScaffoldSystemType.None;
            if (FrameCheckBox.IsChecked == true) selected |= ScaffoldSystemType.Frame;
            if (WedgeCheckBox.IsChecked == true) selected |= ScaffoldSystemType.Wedge;
            if (RinglockCheckBox.IsChecked == true) selected |= ScaffoldSystemType.Ringlock;

            if (selected == ScaffoldSystemType.None)
            {
                MessageBox.Show(this, "Hãy chọn ít nhất một loại giàn giáo.",
                    "Chưa chọn hệ giàn giáo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SelectedSystems = selected;
            DialogResult = true;
        }
    }
}
