using System.Windows;
using SGA_Desktop.ViewModels;

namespace SGA_Desktop.Dialog
{
    public partial class FiltrosOrdenesConteoDialog : Window
    {
        public FiltrosOrdenesConteoDialog()
        {
            InitializeComponent();
        }

        public FiltrosOrdenesConteoDialog(FiltrosOrdenesConteoDialogViewModel viewModel) : this()
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

