using SGA_Desktop.Models;
using SGA_Desktop.Services;
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

namespace SGA_Desktop.Dialog
{
	/// <summary>
	/// Lógica de interacción para UbicacionMasivoDialog.xaml
	/// </summary>
	public partial class UbicacionMasivoDialog : Window
	{
		public UbicacionMasivoDialog()
		{
			InitializeComponent();
		}

		/// <summary>
		/// Constructor para generación masiva, inyecta el ViewModel con el almacén.
		/// </summary>
		public UbicacionMasivoDialog(AlmacenDto almacen) : this()
		{
			InitializeComponent();

			var vm = new UbicacionMasivoDialogViewModel(
				almacen,
				new UbicacionesService(),
				new PaletService());

			DataContext = vm;
			vm.CloseAction = () => this.DialogResult = true;

			// Cuando la ventana ya esté cargada, arrancamos la inicialización
			this.Loaded += async (_, __) => await vm.InitializeAsync();
		}


	}
}
