using System.Windows;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.Services
{
    public interface IDialogService
    {
        void ShowInfo(string message, string title);
        void ShowWarning(string message, string title);
        void ShowError(string message, string title);
    }

    public class MessageBoxDialogService : IDialogService
    {
        public void ShowInfo(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ShowWarning(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public void ShowError(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
