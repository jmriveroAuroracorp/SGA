using System.Windows;
using SGA_Desktop.ViewModels;

namespace SGA_Desktop.Dialog
{
    public partial class AgregarLineasOrdenTraspasoDialog : Window
    {
        public AgregarLineasOrdenTraspasoDialog(string codigoAlmacenDestino, string nombreAlmacenDestino)
        {
            InitializeComponent();
            
            // Establecer el DataContext con la información del destino
            DataContext = new AgregarLineasOrdenTraspasoDialogViewModel(codigoAlmacenDestino, nombreAlmacenDestino);
            
            // Inicializar el ViewModel
            if (DataContext is AgregarLineasOrdenTraspasoDialogViewModel viewModel)
            {
                _ = viewModel.InitializeAsync();
            }
        }
    }
}
