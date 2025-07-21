using CommunityToolkit.Mvvm.ComponentModel;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using System.Collections.ObjectModel;
using System.Windows;
namespace SGA_Desktop.ViewModels
{
	public partial class ConfirmationWithListDialogViewModel : ObservableObject
	{
		private readonly UbicacionesService _ubicacionesService;

		[ObservableProperty]
		private AlmacenDto? almacenDestinoSeleccionado;

		public IEnumerable<LineaPaletDto> Lineas { get; }
		public IEnumerable<AlmacenDto> Almacenes { get; }

		[ObservableProperty]
		private List<UbicacionDto> ubicaciones = new();

		public UbicacionDto? UbicacionSeleccionada { get; set; }

		[ObservableProperty]
		private string? comentario;

		public ConfirmationWithListDialogViewModel(
			IEnumerable<LineaPaletDto> lineas,
			IEnumerable<AlmacenDto> almacenes,
			UbicacionesService ubicacionesService)
		{
			Lineas = lineas;
			Almacenes = almacenes.ToList();
			_ubicacionesService = ubicacionesService;

			AlmacenDestinoSeleccionado = Almacenes.FirstOrDefault();
		}

		partial void OnAlmacenDestinoSeleccionadoChanged(AlmacenDto? value)
		{
			if (value is not null)
			{
				_ = CargarUbicacionesParaAlmacen(value.CodigoAlmacen);
			}
		}

		private async Task CargarUbicacionesParaAlmacen(string codigoAlmacen)
		{
			try
			{
				var lista = await _ubicacionesService.ObtenerUbicacionesVaciasOEspAsync(
					SessionManager.EmpresaSeleccionada.Value, codigoAlmacen);

				if (lista == null || !lista.Any())
				{
					MessageBox.Show("El servicio devolvió vacío.");
				}
				else
				{
					MessageBox.Show($"El servicio devolvió {lista.Count} ubicaciones.");
				}

				Ubicaciones = lista
					.Select(u => new UbicacionDto
					{
						CodigoAlmacen = u.CodigoAlmacen,
						Ubicacion = u.Ubicacion
					}).ToList();
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Error al cargar ubicaciones: {ex.Message}");
				Ubicaciones = new List<UbicacionDto>();
			}
		}

	}
}
