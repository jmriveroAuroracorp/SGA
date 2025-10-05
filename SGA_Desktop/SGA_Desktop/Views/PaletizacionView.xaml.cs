using SGA_Desktop.ViewModels;
using SGA_Desktop.Models;
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
		}

		private void PaletCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (sender is Grid grid && grid.Tag is PaletDto palet && DataContext is PaletizacionViewModel vm)
			{
				vm.PaletSeleccionado = palet;
			}
		}
	}
}
