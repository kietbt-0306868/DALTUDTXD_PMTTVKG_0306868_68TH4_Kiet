using System;
using System.Windows;
using System.Windows.Input;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;
        private bool _isExecuting;

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        // Execution re-entry is guarded in Execute. Do not expose the transient synchronous
        // execution flag as CanExecute=false: WPF can cache that state after a modal dialog
        // or a Revit context switch and leave an otherwise valid button greyed out.
        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);

        public void Execute(object parameter)
        {
            if (_isExecuting || !CanExecute(parameter))
                return;

            _isExecuting = true;
            CommandManager.InvalidateRequerySuggested();
            try
            {
                _execute(parameter);
            }
            catch (Exception ex)
            {
                // A WPF command is the common boundary for every interactive button
                // in the add-in. Keep ordinary command failures inside the add-in so
                // they cannot unwind into Revit's external-command dispatcher.
                MessageBox.Show(
                    $"Không thể hoàn tất thao tác.\n\n{ex.Message}",
                    "Lỗi thao tác",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _isExecuting = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }
}
