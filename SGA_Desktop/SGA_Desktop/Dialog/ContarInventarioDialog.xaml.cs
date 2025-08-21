using System.Windows;
using SGA_Desktop.Models;
using SGA_Desktop.ViewModels;

namespace SGA_Desktop.Dialog
{
    /// <summary>
    /// Lógica de interacción para ContarInventarioDialog.xaml
    /// </summary>
    public partial class ContarInventarioDialog : Window
    {
        public ContarInventarioDialog()
        {
            InitializeComponent();
        }

        public ContarInventarioDialog(InventarioCabeceraDto inventario) : this()
        {
            if (DataContext is ContarInventarioDialogViewModel viewModel)
            {
                viewModel.Inventario = inventario;
            }
        }
    }
} 