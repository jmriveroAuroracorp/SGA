using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;
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
    }
} 