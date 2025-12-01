using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using SGA_Desktop.ViewModels;

namespace SGA_Desktop.Views
{
    /// <summary>
    /// Lógica de interacción para CalidadView.xaml
    /// </summary>
    public partial class CalidadView : Page
    {
        public CalidadView()
        {
            InitializeComponent();
            DataContext = new CalidadViewModel();
        }

        /// <summary>
        /// Maneja el evento KeyDown para los textboxes de búsqueda (Código y Lote)
        /// </summary>
        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Forzar actualización del binding antes de ejecutar el comando
                if (sender is TextBox textBox)
                {
                    var bindingExpression = BindingOperations.GetBindingExpression(textBox, TextBox.TextProperty);
                    bindingExpression?.UpdateSource();
                }

                // Ejecutar búsqueda de stock
                var viewModel = DataContext as CalidadViewModel;
                if (viewModel?.BuscarStockCommand?.CanExecute(null) == true)
                {
                    viewModel.BuscarStockCommand.Execute(null);
                }
            }
        }

        /// <summary>
        /// Maneja el evento KeyDown para el textbox de comentarios
        /// </summary>
        private void ComentarioTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                // Ejecutar comando de bloqueo (solo si no se mantiene Shift para nueva línea)
                var viewModel = DataContext as CalidadViewModel;
                if (viewModel?.BloquearStockCommand?.CanExecute(null) == true)
                {
                    viewModel.BloquearStockCommand.Execute(null);
                }
                e.Handled = true; // Prevenir que se agregue nueva línea
            }
        }

        /// <summary>
        /// Maneja el evento KeyDown para los textboxes de filtros (Código y Lote en pestaña Bloqueos)
        /// </summary>
        private void FiltroTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Forzar actualización del binding antes de ejecutar el comando
                if (sender is TextBox textBox)
                {
                    var bindingExpression = BindingOperations.GetBindingExpression(textBox, TextBox.TextProperty);
                    bindingExpression?.UpdateSource();
                }

                // Ejecutar filtrado de bloqueos
                var viewModel = DataContext as CalidadViewModel;
                if (viewModel?.FiltrarBloqueosCommand?.CanExecute(null) == true)
                {
                    viewModel.FiltrarBloqueosCommand.Execute(null);
                }
            }
        }

        /// <summary>
        /// Maneja el evento KeyDown para el textbox de comentarios de desbloqueo
        /// </summary>
        private void DesbloquearTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                // Ejecutar comando de desbloqueo (solo si no se mantiene Shift para nueva línea)
                var viewModel = DataContext as CalidadViewModel;
                if (viewModel?.DesbloquearStockCommand?.CanExecute(null) == true)
                {
                    viewModel.DesbloquearStockCommand.Execute(null);
                }
                e.Handled = true; // Prevenir que se agregue nueva línea
            }
        }
    }
}
