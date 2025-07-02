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
	/// Lógica de interacción para PaletizacionView.xaml
	/// </summary>
	public partial class PaletizacionView : Page
	{
		public PaletizacionView()
		{
			InitializeComponent();
			DataContext = new PaletizacionViewModel();
			//this.Loaded += async (_, __) =>
			//{
			//	if (DataContext is PaletizacionViewModel vm)
			//		await vm.LoadPaletsCommand.ExecuteAsync(null);
			//};
		}

		//private async void PaletizacionView_Loaded(object sender, RoutedEventArgs e)
		//{
		//	// Al cargar la página, pide al VM que traiga los datos
		//	if (DataContext is PaletizacionViewModel vm)
		//	{
		//		// Ejecuta el comando async (carga los primeros 100 pallets)
		//		await vm.LoadPaletsCommand.ExecuteAsync(null);
		//	}
		//}
	}
}
