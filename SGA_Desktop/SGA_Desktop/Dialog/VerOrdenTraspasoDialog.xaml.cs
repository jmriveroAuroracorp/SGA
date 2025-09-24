using SGA_Desktop.Models;
using SGA_Desktop.ViewModels;
using System.Windows;

namespace SGA_Desktop.Dialog
{
    /// <summary>
    /// Lógica de interacción para VerOrdenTraspasoDialog.xaml
    /// </summary>
    public partial class VerOrdenTraspasoDialog : Window
    {
        public VerOrdenTraspasoDialog()
        {
            InitializeComponent();
        }

        public VerOrdenTraspasoDialog(OrdenTraspasoDto ordenTraspaso) : this()
        {
            if (DataContext is VerOrdenTraspasoDialogViewModel viewModel)
            {
                viewModel.EstablecerOrden(ordenTraspaso);
            }
        }
    }
}

