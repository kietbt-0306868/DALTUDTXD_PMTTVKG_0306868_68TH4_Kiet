using System.Windows.Controls;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.Views
{
    public partial class Preview3DView : UserControl
    {
        public Preview3DView()
        {
            InitializeComponent();
            this.IsVisibleChanged += Preview3DView_IsVisibleChanged;
        }

        private void Preview3DView_IsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (this.IsVisible && Viewport != null)
            {
                // Force a layout update and zoom extents when becoming visible to fix blank rendering bugs
                Viewport.UpdateLayout();
                Viewport.ZoomExtents();
            }
        }
    }
}
