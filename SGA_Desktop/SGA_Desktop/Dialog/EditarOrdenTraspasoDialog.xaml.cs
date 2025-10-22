using System.Windows;
using SGA_Desktop.ViewModels;

namespace SGA_Desktop.Dialog
{
    /// <summary>
    /// Lógica de interacción para EditarOrdenTraspasoDialog.xaml
    /// </summary>
    public partial class EditarOrdenTraspasoDialog : Window
    {
        public EditarOrdenTraspasoDialog()
        {
            InitializeComponent();
        }

        public EditarOrdenTraspasoDialog(EditarOrdenTraspasoDialogViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}























