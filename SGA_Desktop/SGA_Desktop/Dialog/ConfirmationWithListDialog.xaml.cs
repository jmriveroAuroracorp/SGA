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
using System.Windows.Shapes;

namespace SGA_Desktop.Dialog
{
	/// <summary>
	/// Lógica de interacción para ConfirmationWithListDialog.xaml
	/// </summary>
	public partial class ConfirmationWithListDialog : Window
	{
		public ConfirmationWithListDialogViewModel VM { get; }

		//public ConfirmationWithListDialog(
		//	IEnumerable<LineaPaletDto> lineas,
		//	IEnumerable<UbicacionDetalladaDto> ubicaciones,
		//	IEnumerable<AlmacenDto> almacenes)
		//{
		//	InitializeComponent();
		//	VM = new ConfirmationWithListDialogViewModel(
		//		lineas,
		//		ubicaciones,
		//		almacenes
		//	);
		//	DataContext = VM;
		//}

		public ConfirmationWithListDialog(
		IEnumerable<LineaPaletDto> lineas,
		IEnumerable<AlmacenDto> almacenes,
		UbicacionesService ubicacionesService)
		{
			InitializeComponent();

			VM = new ConfirmationWithListDialogViewModel(lineas, almacenes, ubicacionesService);
			DataContext = VM;
		}



		public UbicacionDto? UbicacionSeleccionada => VM.UbicacionSeleccionada;

		private void YesButton_Click(object sender, RoutedEventArgs e)
		{
			var vm = DataContext as SGA_Desktop.ViewModels.ConfirmationWithListDialogViewModel;
			if (vm != null)
			{
				if (vm.Altura <= 0 || vm.Peso <= 0)
				{
					MessageBox.Show("La altura y el peso deben ser mayores que 0.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
					return;
				}
			}
			if (UbicacionSeleccionada == null)
			{
				MessageBox.Show("Por favor selecciona una ubicación.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			DialogResult = true;
			Close();
		}

		private void NoButton_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close();
		}
	}

}
