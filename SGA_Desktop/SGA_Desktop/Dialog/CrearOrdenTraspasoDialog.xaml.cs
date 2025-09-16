using System.Windows;
using SGA_Desktop.ViewModels;

namespace SGA_Desktop.Dialog
{
    public partial class CrearOrdenTraspasoDialog : Window
    {
        public CrearOrdenTraspasoDialog()
        {
            InitializeComponent();
            
            // Crear y establecer el ViewModel
            var viewModel = new CrearOrdenTraspasoDialogViewModel();
            DataContext = viewModel;
            
            // Establecer la referencia del dialog en el ViewModel
            viewModel.DialogResult = this;
        }
    }
} 