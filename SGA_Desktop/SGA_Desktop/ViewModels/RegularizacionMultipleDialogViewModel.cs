using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Services;
using SGA_Desktop.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using SGA_Desktop.Helpers;
using System.Windows.Data;
using SGA_Desktop.Dialog;
using System.Collections.Generic;

namespace SGA_Desktop.ViewModels
{
	public partial class RegularizacionMultipleDialogViewModel : ObservableObject
	{
		private readonly TraspasosService _traspasosService;
		private readonly StockService _stockService;

		public ObservableCollection<ArticuloConStockDto> ArticulosConStock { get; } = new();
		public ObservableCollection<StockDisponibleDto> LineasPendientes { get; } = new();
		public ObservableCollection<AlmacenDto> AlmacenesDestino { get; } = new();

		// NUEVAS PROPIEDADES DESTINO COMÚN
		[ObservableProperty]
		private AlmacenDto destinoComunAlmacen;

		// Cambia la propiedad DestinoComunUbicacion a UbicacionDto
		[ObservableProperty]
		private UbicacionDto destinoComunUbicacion;

		// NUEVO: Ubicaciones destino común
		public ObservableCollection<UbicacionDto> UbicacionesDestinoComun { get; } = new();

		partial void OnDestinoComunAlmacenChanged(AlmacenDto value)
		{
			_ = CargarUbicacionesDestinoComunAsync();
		}

		private async Task CargarUbicacionesDestinoComunAsync()
		{
			UbicacionesDestinoComun.Clear();
			DestinoComunUbicacion = null;
			if (DestinoComunAlmacen == null) return;
			var lista = await new UbicacionesService().ObtenerUbicacionesAsync(
				DestinoComunAlmacen.CodigoAlmacen,
				SessionManager.EmpresaSeleccionada.Value
			);
			if (lista != null)
			{
				foreach (var u in lista)
					UbicacionesDestinoComun.Add(u);
			}
		}

		// Fecha de entrada a la ventana (inicio de la regularización múltiple)
		private readonly DateTime _fechaInicioDialogo = DateTime.Now;

		public RegularizacionMultipleDialogViewModel(TraspasosService traspasosService, StockService stockService)
		{
			_traspasosService = traspasosService;
			_stockService = stockService;

		}

		public async Task InitializeAsync()
		{
			var empresa = SessionManager.EmpresaSeleccionada!.Value;
			var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
			var permisos = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();

			if (!permisos.Any())
				permisos = await _stockService.ObtenerAlmacenesAsync(centro);

			var almacenes = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa, centro, permisos);

			AlmacenesDestino.Clear();
			foreach (var a in almacenes)
				AlmacenesDestino.Add(a);

			OnPropertyChanged(nameof(AlmacenesDestino));
		}

		public async Task CargarUbicacionesDestinoAsync(StockDisponibleDto linea)
		{
			linea.UbicacionesDestino.Clear();
			linea.UbicacionDestino = null;

			if (string.IsNullOrWhiteSpace(linea.AlmacenDestino)) return;

			var lista = await new UbicacionesService().ObtenerUbicacionesAsync(
				linea.AlmacenDestino,
				SessionManager.EmpresaSeleccionada.Value
			);

			if (lista != null)
			{
				foreach (var u in lista)
					linea.UbicacionesDestino.Add(u);

				CollectionViewSource.GetDefaultView(linea.UbicacionesDestino)?.Refresh();
			}
			else
			{
				new WarningDialog("Aviso", "No se recibieron ubicaciones (lista es null)").ShowDialog();
			}
		}

		[ObservableProperty]
		private string articuloBuscado;

		[ObservableProperty]
		private string articuloDescripcion;

		partial void OnArticuloBuscadoChanged(string value) => BuscarStockCommand.NotifyCanExecuteChanged();
		partial void OnArticuloDescripcionChanged(string value) => BuscarStockCommand.NotifyCanExecuteChanged();

		[RelayCommand(CanExecute = nameof(CanBuscarStock))]
		private async Task BuscarStockAsync()
		{
			try
			{
				var codigo = string.IsNullOrWhiteSpace(articuloBuscado) ? null : articuloBuscado;
				var descripcion = string.IsNullOrWhiteSpace(articuloDescripcion) ? null : articuloDescripcion;

				if (codigo == null && descripcion == null)
				{
					new WarningDialog("Aviso", "Introduce al menos código de artículo o descripción para buscar.").ShowDialog();
					return;
				}

				// Nuevo: usa el método que trae Reservado y Disponible
				var resultados = await _stockService.ObtenerStockDisponibleAsync(codigo, descripcion);

				// Filtra por almacenes permitidos
				var permisos = SessionManager.UsuarioActual?.codigosAlmacen?.ToList() ?? new();
				if (!permisos.Any())
				{
					string centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
					permisos = await _stockService.ObtenerAlmacenesAsync(centro) ?? new();
				}

				resultados = resultados
					.Where(x => x?.CodigoAlmacen != null && permisos.Contains(x.CodigoAlmacen))
					.ToList();

				var grupos = resultados
					.GroupBy(s => new { s.CodigoArticulo, s.DescripcionArticulo })
					.Select(g => new ArticuloConStockDto
					{
						CodigoArticulo = g.Key.CodigoArticulo,
						DescripcionArticulo = g.Key.DescripcionArticulo,
						Ubicaciones = new ObservableCollection<StockDisponibleDto>(g.ToList())
					})
					.ToList();

				ArticulosConStock.Clear();
				foreach (var art in grupos)
					ArticulosConStock.Add(art);
			}
			catch (Exception ex)
			{
				new WarningDialog("Error", $"Error al buscar stock: {ex.Message}").ShowDialog();
			}
		}

		private bool CanBuscarStock()
			=> !string.IsNullOrWhiteSpace(articuloBuscado) || !string.IsNullOrWhiteSpace(articuloDescripcion);

		[RelayCommand]
		private async Task AnhadirLineaAsync(StockDisponibleDto dto)
		{
			if (dto.CantidadAMoverDecimal is not decimal cantidad || cantidad <= 0)
			{
				new WarningDialog("Aviso", "Indica una cantidad mayor que 0").ShowDialog();
				return;
			}

			// Cambia la validación: no permitir más que el disponible real
			if (cantidad > dto.Disponible)
			{
				new WarningDialog("Aviso", $"La cantidad a mover ({cantidad}) es mayor que la disponible real ({dto.Disponible}).").ShowDialog();
				return;
			}

			dto.CantidadAMover = cantidad;

			bool yaExiste = LineasPendientes.Any(x =>
				x.CodigoArticulo == dto.CodigoArticulo &&
				x.Partida == dto.Partida &&
				x.Ubicacion == dto.Ubicacion &&
				x.CodigoAlmacen == dto.CodigoAlmacen);

			if (yaExiste)
			{
				new WarningDialog("Aviso", "Ya has añadido esta línea antes.").ShowDialog();
				return;
			}

			dto.AlmacenDestinoChanged += async (s, e) => {
				await CargarUbicacionesDestinoAsync(dto);
			};

			LineasPendientes.Add(dto);
		}

		[RelayCommand]
		private void EliminarLinea(StockDisponibleDto dto)
		{
			if (LineasPendientes.Contains(dto))
				LineasPendientes.Remove(dto);
		}

		// NUEVO COMANDO: APLICAR DESTINO COMÚN
		[RelayCommand]
		private async void AplicarDestinoComun()
		{
			// 1. Cambia almacén y espera a que se carguen ubicaciones
			foreach (var dto in LineasPendientes)
			{
				if (DestinoComunAlmacen != null)
				{
					dto.AlmacenDestino = DestinoComunAlmacen.CodigoAlmacen;
					await CargarUbicacionesDestinoAsync(dto);
				}
			}

			// 2. Ahora asigna la ubicación común SOLO como string
			if (DestinoComunUbicacion != null)
			{
				foreach (var dto in LineasPendientes)
				{
					dto.UbicacionDestino = DestinoComunUbicacion.Ubicacion;
				}
			}

			CollectionViewSource.GetDefaultView(LineasPendientes).Refresh();
		}

		[RelayCommand]
		private async Task ConfirmarAsync()
		{
			if (!LineasPendientes.Any())
			{
				new WarningDialog("Aviso", "No hay líneas para confirmar.").ShowDialog();
				return;
			}

			bool todoOk = true;

			foreach (var dto in LineasPendientes)
			{
				dto.TieneError = false;
				dto.ErrorMessage = null;

				if (string.IsNullOrWhiteSpace(dto.AlmacenDestino))
				{
					dto.TieneError = true;
					dto.ErrorMessage = "Selecciona almacén destino.";
					todoOk = false;
					continue;
				}
				// UbicacionDestino puede ser null o vacío (sin ubicar)
				if (dto.CantidadAMover <= 0 || dto.CantidadAMover > dto.UnidadSaldo)
				{
					dto.TieneError = true;
					dto.ErrorMessage = "Cantidad a mover no válida.";
					todoOk = false;
					continue;
				}

				var crearDto = new CrearTraspasoArticuloDto
				{
					AlmacenOrigen = dto.CodigoAlmacen,
					UbicacionOrigen = dto.Ubicacion,
					CodigoArticulo = dto.CodigoArticulo,
					Cantidad = dto.CantidadAMover,
					UsuarioId = SessionManager.UsuarioActual?.operario ?? 0,
					AlmacenDestino = dto.AlmacenDestino,
					UbicacionDestino = string.IsNullOrWhiteSpace(dto.UbicacionDestino) ? "" : dto.UbicacionDestino,
					FechaCaducidad = dto.FechaCaducidad,
					Partida = dto.Partida,
					CodigoEmpresa = SessionManager.EmpresaSeleccionada.Value,
					FechaInicio = _fechaInicioDialogo,
					Finalizar = true
				};
				System.Windows.MessageBox.Show($"Enviando traspaso:\nAlmacenDestino: '{crearDto.AlmacenDestino}'\nUbicacionDestino: '{crearDto.UbicacionDestino}'\nPartida: '{crearDto.Partida}'\nCodigoArticulo: '{crearDto.CodigoArticulo}'\n(longitudes: {crearDto.AlmacenDestino?.Length}, {crearDto.UbicacionDestino?.Length}, {crearDto.Partida?.Length})");
				var resultado = await _traspasosService.CrearTraspasoArticuloAsync(crearDto);

				if (!resultado.Success)
				{
					todoOk = false;
					dto.TieneError = true;
					dto.ErrorMessage = resultado.ErrorMessage ?? "Error al realizar el traspaso.";
				}
			}

			if (todoOk)
			{
				new WarningDialog("Éxito", "Traspasos realizados correctamente.").ShowDialog();
				Application.Current.Windows.OfType<Window>()
					.FirstOrDefault(w => w.DataContext == this)?.Close();
			}
			else
			{
				var errores = LineasPendientes
					.Where(x => x.TieneError)
					.Select(x => $"{x.DescripcionArticulo} ({x.CodigoArticulo}): {x.ErrorMessage}")
					.ToList();

				new WarningDialog("Errores en traspasos", string.Join("\n", errores)).ShowDialog();
			}

			CollectionViewSource.GetDefaultView(LineasPendientes).Refresh();
		}



	}
}
