using System.Windows;
using SGA_Desktop.ViewModels;

namespace SGA_Desktop.Dialog
{
    public partial class FiltrosInventarioDialog : Window
    {
        public FiltrosInventarioDialog()
        {
            InitializeComponent();
        }

        public FiltrosInventarioDialog(FiltrosInventarioDialogViewModel viewModel) : this()
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

