using System.Windows;

namespace SGA_Desktop.Dialog
{
    public partial class CrearOrdenTraspasoDialog : Window
    {
        public CrearOrdenTraspasoDialog()
        {
            InitializeComponent();
        }

        private void Crear_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
} 