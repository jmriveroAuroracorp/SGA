using System.Windows;
using SGA_Desktop.ViewModels;

namespace SGA_Desktop.Dialog
{
    public partial class FiltrosSupervisionDialog : Window
    {
        public FiltrosSupervisionDialog()
        {
            InitializeComponent();
        }

        public FiltrosSupervisionDialog(FiltrosSupervisionDialogViewModel viewModel) : this()
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
