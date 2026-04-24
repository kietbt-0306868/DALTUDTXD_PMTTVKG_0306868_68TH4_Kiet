using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.Views.UserControls
{
    /// <summary>
    /// Interaction logic for PageThongKe.xaml
    /// </summary>
    public partial class PageThongKe : UserControl
    {
        public PageThongKe()
        {
            InitializeComponent();
        }

        private void Tab_Click(object sender, RoutedEventArgs e)
        {
            // Basic handler to update the visual state of the tab buttons.
            var btn = sender as Button;
            if (btn == null) return;

            var parent = btn.Parent as Panel;
            if (parent == null) return;

            foreach (var child in parent.Children.OfType<Button>())
            {
                if (child == btn)
                {
                    child.Background = new SolidColorBrush(Color.FromRgb(214, 234, 248)); // #D6EAF8
                    child.FontWeight = FontWeights.Bold;
                }
                else
                {
                    child.Background = Brushes.Transparent;
                    child.FontWeight = FontWeights.Normal;
                }
            }
        }
    }
}
