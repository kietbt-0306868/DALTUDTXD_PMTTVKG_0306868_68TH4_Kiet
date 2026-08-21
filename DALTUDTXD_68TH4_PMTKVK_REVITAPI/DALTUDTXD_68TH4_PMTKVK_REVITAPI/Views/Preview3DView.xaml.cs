using System.Windows.Controls;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels;

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
            // Rendering is started only by RequestFastPreview3D/RequestRender3D. Starting
            // again from IsVisibleChanged created a race between two native PreviewControl
            // instances and could terminate Revit before a managed exception was raised.
            if (!this.IsVisible && DataContext is Preview3DViewModel hiddenViewModel)
            {
                hiddenViewModel.ReleaseProjectModel();
            }
        }
    }
}
