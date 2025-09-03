using System.Windows;
using SGA_Desktop.ViewModels;

namespace SGA_Desktop.Dialog
{
    public partial class TraspasoHistoricoDialog : Window
    {
        public TraspasoHistoricoDialog()
        {
            InitializeComponent();
        }

        public TraspasoHistoricoDialog(TraspasoHistoricoDialogViewModel viewModel) : this()
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