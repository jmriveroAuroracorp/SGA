using SGA_Desktop.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SGA_Desktop.Views
{
	/// <summary>
	/// Lógica de interacción para ConsultaStockView.xaml
	/// </summary>
	public partial class ConsultaStockView : Page
	{
		public ConsultaStockView()
		{
			InitializeComponent();
			DataContext = new ConsultaStockViewModel(new Services.StockService());
			//DataContext = new ConsultaStockViewModel(new ApiService());

		}

		private void TextBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
			{
				var viewModel = DataContext as ConsultaStockViewModel;
				if (viewModel != null)
				{
					// Ejecutar el comando de búsqueda según el modo activo
					if (viewModel.IsArticleMode)
					{
						viewModel.BuscarPorArticuloCommand.Execute(null);
					}
					else
					{
						viewModel.BuscarPorUbicacionCommand.Execute(null);
					}
				}
			}
		}
	}

}
