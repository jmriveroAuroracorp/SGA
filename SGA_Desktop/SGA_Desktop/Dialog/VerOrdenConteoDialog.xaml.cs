using System.Windows;
using SGA_Desktop.Models;
using SGA_Desktop.ViewModels;
using SGA_Desktop.Services;

namespace SGA_Desktop.Dialog
{
    public partial class VerOrdenConteoDialog : Window
    {
        public VerOrdenConteoDialog(OrdenConteoDto orden)
        {
            InitializeComponent();
            var conteosService = new ConteosService();
            DataContext = new VerOrdenConteoDialogViewModel(orden, conteosService);
        }

        private void CerrarButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
