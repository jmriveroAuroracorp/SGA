using SGA_Desktop.Models;
using SGA_Desktop.ViewModels;
using System.Windows;

namespace SGA_Desktop.Dialog
{
    /// <summary>
    /// Lógica de interacción para InventarioDialog.xaml
    /// </summary>
    public partial class InventarioDialog : Window
    {
        public InventarioDialog()
        {
            InitializeComponent();
        }

        public InventarioDialog(StockDto stockSistema)
        {
            InitializeComponent();
            DataContext = new InventarioDialogViewModel(stockSistema);
        }

        public InventarioDialog(InventarioDto inventarioExistente)
        {
            InitializeComponent();
            DataContext = new InventarioDialogViewModel(inventarioExistente);
        }

        public decimal StockFisico => ((InventarioDialogViewModel)DataContext).StockFisico;
        public string Observaciones => ((InventarioDialogViewModel)DataContext).Observaciones;
    }
} 