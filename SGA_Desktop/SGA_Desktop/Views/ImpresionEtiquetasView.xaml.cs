using System.Windows.Controls;
using SGA_Desktop.Services;
using SGA_Desktop.ViewModels;

namespace SGA_Desktop.Views
{
	public partial class ImpresionEtiquetasView : Page
	{
		public ImpresionEtiquetasView()
		{
			InitializeComponent();
			DataContext = new ImpresionEtiquetasViewModel(
							  new StockService(),
							  new PrintQueueService(),
							  new LoginService());
		}

	}
}
