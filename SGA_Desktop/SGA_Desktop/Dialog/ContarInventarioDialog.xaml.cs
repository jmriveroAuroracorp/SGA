using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;
using System;
using SGA_Desktop.Models;
using SGA_Desktop.ViewModels;

namespace SGA_Desktop.Dialog
{
    /// <summary>
    /// Lógica de interacción para ContarInventarioDialog.xaml
    /// </summary>
    public partial class ContarInventarioDialog : Window
    {
        public ContarInventarioDialog()
        {
            InitializeComponent();
        }

        public ContarInventarioDialog(InventarioCabeceraDto inventario) : this()
        {
            if (DataContext is ContarInventarioDialogViewModel viewModel)
            {
                viewModel.Inventario = inventario;
            }
        }

        /// <summary>
        /// Valida que solo se permitan números y punto decimal
        /// </summary>
        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // Solo permitir números y punto decimal
                if (e.Text == ".")
                {
                    // Si ya hay un punto decimal, no permitir otro
                    if (textBox.Text.Contains("."))
                    {
                        e.Handled = true;
                    }
                    return;
                }

                // Para cualquier otro carácter, verificar si es número
                if (!char.IsDigit(e.Text[0]))
                {
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Selecciona todo el texto cuando el TextBox obtiene el foco
        /// </summary>
        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // Usar Dispatcher para evitar conflictos con el binding
                textBox.Dispatcher.BeginInvoke(new Action(() =>
                {
                    textBox.SelectAll();
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }

        /// <summary>
        /// Maneja el evento MouseUp para seleccionar todo al hacer click
        /// </summary>
        private void TextBox_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox && textBox.SelectionLength == 0)
            {
                textBox.SelectAll();
            }
        }
    }
} 