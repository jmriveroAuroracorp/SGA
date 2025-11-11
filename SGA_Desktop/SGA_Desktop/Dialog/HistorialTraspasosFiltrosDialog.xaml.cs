using System.Windows;

namespace SGA_Desktop.Dialog
{
    public partial class HistorialTraspasosFiltrosDialog : Window
    {
        public HistorialTraspasosFiltrosDialog()
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

