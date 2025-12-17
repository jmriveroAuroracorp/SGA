using System.Windows;

namespace SGA_Desktop.Dialog
{
    public partial class HistorialAjustesFiltrosDialog : Window
    {
        public HistorialAjustesFiltrosDialog()
        {
            InitializeComponent();
        }

        private void AplicarButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}

