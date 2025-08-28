using System.Windows;
using SGA_Desktop.Models;
using SGA_Desktop.ViewModels;

namespace SGA_Desktop.Dialog
{
    /// <summary>
    /// Lógica de interacción para VerInventarioDialog.xaml
    /// </summary>
    public partial class VerInventarioDialog : Window
    {
        public VerInventarioDialog()
        {
            InitializeComponent();
        }

        public VerInventarioDialog(InventarioCabeceraDto inventario) : this()
        {
            if (DataContext is VerInventarioDialogViewModel viewModel)
            {
                viewModel.Inventario = inventario;
            }
        }
    }
} 