using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace SGA_Desktop.Dialog
{
    /// <summary>
    /// Lógica de interacción para ReconteoLineasProblematicasDialog.xaml
    /// </summary>
    public partial class ReconteoLineasProblematicasDialog : Window
    {
        public ReconteoLineasProblematicasDialog()
        {
            InitializeComponent();
        }

        public ReconteoLineasProblematicasDialog(Models.InventarioCabeceraDto inventario)
        {
            InitializeComponent();
            
            if (DataContext is ViewModels.ReconteoLineasProblematicasDialogViewModel viewModel)
            {
                viewModel.Inventario = inventario;
            }
        }

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Permitir solo números, punto decimal y coma
            var regex = new Regex(@"^[0-9.,]+$");
            if (!regex.IsMatch(e.Text))
            {
                e.Handled = true;
                return;
            }

            var textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null)
            {
                var newText = textBox.Text.Insert(textBox.CaretIndex, e.Text);
                
                // Verificar que solo hay un separador decimal
                var separators = newText.Count(c => c == '.' || c == ',');
                if (separators > 1)
                {
                    e.Handled = true;
                    return;
                }

                // Verificar que no hay más de 4 decimales
                var parts = newText.Split('.', ',');
                if (parts.Length > 1 && parts[1].Length > 4)
                {
                    e.Handled = true;
                    return;
                }
            }
        }
    }
} 