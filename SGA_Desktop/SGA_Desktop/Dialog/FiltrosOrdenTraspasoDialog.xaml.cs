using System.Windows;
using SGA_Desktop.ViewModels;

namespace SGA_Desktop.Dialog
{
    public partial class FiltrosOrdenTraspasoDialog : Window
    {
        public FiltrosOrdenTraspasoDialog()
        {
            InitializeComponent();
        }

        public FiltrosOrdenTraspasoDialog(FiltrosOrdenTraspasoDialogViewModel viewModel) : this()
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

