using System.Windows;
using SGA_Desktop.Models;
using SGA_Desktop.ViewModels;
using SGA_Desktop.Services;

namespace SGA_Desktop.Dialog
{
    public partial class HistorialRenovacionesDialog : Window
    {
        public HistorialRenovacionesDialog(ConteoPeriodicoDto conteoPeriodico)
        {
            InitializeComponent();
            var conteosService = new ConteosService();
            var loginService = new LoginService();
            DataContext = new HistorialRenovacionesDialogViewModel(conteoPeriodico, conteosService, loginService);
        }

        private void CerrarButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
