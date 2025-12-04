using SGA_Desktop.Models;
using SGA_Desktop.Services;
using SGA_Desktop.ViewModels;
using System.Windows;

namespace SGA_Desktop.Dialog
{
    public partial class AgregarLineaInventarioDialog : Window
    {
        public AgregarLineaInventarioDialog(InventarioCabeceraDto inventario)
        {
            InitializeComponent();
            
            var inventarioService = new InventarioService();
            var stockService = new StockService();
            DataContext = new AgregarLineaInventarioDialogViewModel(inventario, inventarioService, stockService);
        }
    }
}

