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

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.Views
{
    /// <summary>
    /// Interaction logic for KetQuaView.xaml
    /// </summary>
    public partial class KetQuaView : UserControl
    {
        public KetQuaView()
        {
            InitializeComponent();
        }
    }

    /// <summary>
    /// Class hỗ trợ binding dữ liệu tĩnh cho giao diện KetQuaView
    /// </summary>
    public class MaterialQuantityMock
    {
        public string MaterialName { get; set; }
        public string Specification { get; set; }
        public double Quantity { get; set; }
        public string Unit { get; set; }
    }
}
