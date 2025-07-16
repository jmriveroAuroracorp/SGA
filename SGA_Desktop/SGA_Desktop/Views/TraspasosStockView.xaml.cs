using System.Windows.Controls;
using SGA_Desktop.Services;
using SGA_Desktop.ViewModels;

namespace SGA_Desktop.Views
{
    public partial class TraspasosStockView : Page
    {
        public TraspasosStockView()
        {
            InitializeComponent();
            DataContext = new TraspasosStockViewModel(new StockService(), new TraspasosService());
        }
    }
} 