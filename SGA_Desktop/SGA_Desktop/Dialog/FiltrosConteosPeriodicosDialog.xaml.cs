using System.Windows;
using SGA_Desktop.ViewModels;

namespace SGA_Desktop.Dialog
{
    public partial class FiltrosConteosPeriodicosDialog : Window
    {
        public FiltrosConteosPeriodicosDialog()
        {
            InitializeComponent();
        }

        public FiltrosConteosPeriodicosDialog(FiltrosConteosPeriodicosDialogViewModel viewModel) : this()
        {
            DataContext = viewModel;

            // Configurar el evento de cierre
            viewModel.RequestClose += (result) =>
            {
                DialogResult = result;
                Close();
            };
        }
    }
}
