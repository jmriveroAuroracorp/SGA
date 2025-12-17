using SGA_Desktop.Models;
using SGA_Desktop.Services;
using SGA_Desktop.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace SGA_Desktop.Dialog
{
	/// <summary>
	/// Lógica de interacción para EditarUbicacionesMasivoDialog.xaml
	/// </summary>
	public partial class EditarUbicacionesMasivoDialog : Window
	{
		public EditarUbicacionesMasivoDialog()
		{
			InitializeComponent();
		}

		/// <summary>
		/// Constructor para edición masiva de ubicaciones seleccionadas
		/// </summary>
		public EditarUbicacionesMasivoDialog(
			List<UbicacionDetalladaDto> ubicacionesSeleccionadas,
			UbicacionesService ubicService,
			PaletService paletService,
			short codigoEmpresa) : this()
		{
			var vm = new EditarUbicacionesMasivoDialogViewModel(
				ubicacionesSeleccionadas,
				ubicService,
				paletService,
				codigoEmpresa);

			DataContext = vm;
			vm.CloseAction = () => this.DialogResult = true;
		}
	}
}

