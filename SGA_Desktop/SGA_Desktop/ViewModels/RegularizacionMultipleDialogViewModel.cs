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
using System.ComponentModel;
using System.Windows.Controls;

namespace SGA_Desktop.ViewModels
{
	public partial class RegularizacionMultipleDialogViewModel : ObservableObject
	{
		private readonly TraspasosService _traspasosService;
		private readonly StockService _stockService;

	public ObservableCollection<ArticuloConStockDto> ArticulosConStock { get; } = new();
	public ObservableCollection<StockDisponibleDto> LineasPendientes { get; } = new();
	public ObservableCollection<AlmacenDto> AlmacenesDestino { get; } = new();
	
	// Vista filtrable para almacenes destino

		// NUEVAS PROPIEDADES DESTINO COMÚN
		[ObservableProperty]
		private AlmacenDto destinoComunAlmacen;

		// Cambia la propiedad DestinoComunUbicacion a UbicacionDto
		[ObservableProperty]
		private UbicacionDto destinoComunUbicacion;

		// NUEVO: Campo de comentarios para la regularización múltiple
		[ObservableProperty]
		private string comentariosTexto = "";

	// NUEVO: Ubicaciones destino común
	public ObservableCollection<UbicacionDto> UbicacionesDestinoComun { get; } = new();

	// NUEVO: Palet común
	[ObservableProperty]
	private PaletDto paletComunSeleccionado;

	// NUEVO: Palets disponibles para el palet común
	public ObservableCollection<PaletDto> PaletsComunDisponibles { get; } = new();

	partial void OnDestinoComunAlmacenChanged(AlmacenDto value)
	{
		_ = CargarUbicacionesDestinoComunAsync();
	}

	partial void OnDestinoComunUbicacionChanged(UbicacionDto value)
	{
		_ = ConsultarPaletsComunDisponiblesAsync();
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
			
			// 🔷 NUEVA LÓGICA: Obtener todos los almacenes autorizados (individuales + centro)
			var almacenesAutorizados = await ObtenerAlmacenesAutorizadosAsync();
			
			var almacenes = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa, centro, almacenesAutorizados);

			AlmacenesDestino.Clear();
			foreach (var a in almacenes)
				AlmacenesDestino.Add(a);

			OnPropertyChanged(nameof(AlmacenesDestino));
			
		}

		public async Task CargarUbicacionesDestinoAsync(StockDisponibleDto linea)
		{
			linea.UbicacionesDestino.Clear();
			linea.UbicacionDestino = null;
			
			// Limpiar palets disponibles al cambiar ubicación
			linea.PaletsDisponibles.Clear();
			linea.PaletDestinoSeleccionado = null;
			linea.MostrarSelectorPalets = false;

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
		
		// NUEVO: Consultar palets disponibles para el palet común
		private async Task ConsultarPaletsComunDisponiblesAsync()
		{
			PaletsComunDisponibles.Clear();
			PaletComunSeleccionado = null;

			if (DestinoComunAlmacen == null || DestinoComunUbicacion == null) return;

			try
			{
				// Usar el primer artículo de las líneas pendientes para el precheck
				var primeraLinea = LineasPendientes.FirstOrDefault();
				if (primeraLinea == null) return;

				var resultado = await _traspasosService.PrecheckFinalizarArticuloAsync(
					SessionManager.EmpresaSeleccionada.Value,
					DestinoComunAlmacen.CodigoAlmacen,
					DestinoComunUbicacion.Ubicacion
				);

				if (resultado != null && resultado.CantidadPalets > 0)
				{
					foreach (var palet in resultado.Palets)
					{
						PaletsComunDisponibles.Add(new PaletDto
						{
							Id = palet.PaletId,
							Codigo = palet.CodigoPalet,
							Estado = palet.Estado
						});
					}
				}
			}
			catch (Exception ex)
			{
				// Si falla el precheck, no pasa nada
			}
		}

		// NUEVO: Método para consultar palets disponibles cuando cambia la ubicación destino
		public async Task ConsultarPaletsDisponiblesAsync(StockDisponibleDto linea)
		{
			// Limpiar lista anterior
			linea.PaletsDisponibles.Clear();
			linea.PaletDestinoSeleccionado = null;
			linea.MostrarSelectorPalets = false;

			// Validar que tengamos almacén y ubicación destino
			if (string.IsNullOrWhiteSpace(linea.AlmacenDestino) || string.IsNullOrWhiteSpace(linea.UbicacionDestino))
				return;

			try
			{
				// Usar el mismo método que funciona en TraspasoStockDialogViewModel
				var resultado = await _traspasosService.PrecheckFinalizarArticuloAsync(
					SessionManager.EmpresaSeleccionada.Value,
					linea.AlmacenDestino,
					linea.UbicacionDestino
				);

				if (resultado != null && resultado.CantidadPalets > 0)
				{
					// Limpiar lista antes de agregar
					linea.PaletsDisponibles.Clear();
					
					// Añadir palets a la lista usando la estructura correcta
					foreach (var palet in resultado.Palets)
					{
						linea.PaletsDisponibles.Add(new PaletDto
						{
							Id = palet.PaletId,
							Codigo = palet.CodigoPalet,
							Estado = palet.Estado
						});
					}

					if (resultado.CantidadPalets == 1)
					{
						// Solo hay 1 palet → seleccionarlo automáticamente
						linea.PaletDestinoSeleccionado = linea.PaletsDisponibles.First();
						linea.PaletDestinoId = linea.PaletDestinoSeleccionado.Id.ToString();
						linea.MostrarSelectorPalets = false;
					}
					else
					{
						// Hay múltiples palets → mostrar selector
						linea.MostrarSelectorPalets = true;
					}
				}
			}
			catch (Exception ex)
			{
				// Si falla el precheck, no pasa nada, funcionará como antes (sin selector)
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

				// 🔷 NUEVA LÓGICA: Obtener todos los almacenes autorizados (individuales + centro)
				var almacenesAutorizados = await ObtenerAlmacenesAutorizadosAsync();

				resultados = resultados
					.Where(x => x?.CodigoAlmacen != null && almacenesAutorizados.Contains(x.CodigoAlmacen))
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
			
			// NUEVO: Evento para cuando cambie la ubicación destino
			dto.PropertyChanged += async (s, e) => {
				if (e.PropertyName == nameof(StockDisponibleDto.UbicacionDestino))
				{
					await ConsultarPaletsDisponiblesAsync(dto);
				}
				else if (e.PropertyName == nameof(StockDisponibleDto.PaletDestinoSeleccionado))
				{
					// Actualizar el ID del palet cuando se seleccione uno
					if (dto.PaletDestinoSeleccionado != null)
					{
						dto.PaletDestinoId = dto.PaletDestinoSeleccionado.Id.ToString();
					}
					else
					{
						dto.PaletDestinoId = null;
					}
				}
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
					
					// NUEVO: Limpiar selección previa de palets antes de consultar nuevos
					dto.PaletDestinoSeleccionado = null;
					dto.PaletDestinoId = null;
					
					// NUEVO: Consultar palets disponibles después de asignar ubicación
					await ConsultarPaletsDisponiblesAsync(dto);
				}
			}

			CollectionViewSource.GetDefaultView(LineasPendientes).Refresh();
		}

		// NUEVO: Comando para aplicar palet común
		[RelayCommand]
		private void AplicarPaletComun()
		{
			if (PaletComunSeleccionado == null) return;

			// Aplicar el palet común a todas las líneas que van a la misma ubicación
			foreach (var dto in LineasPendientes)
			{
				if (dto.AlmacenDestino == DestinoComunAlmacen?.CodigoAlmacen && 
				    dto.UbicacionDestino == DestinoComunUbicacion?.Ubicacion)
				{
					dto.PaletDestinoSeleccionado = PaletComunSeleccionado;
					dto.PaletDestinoId = PaletComunSeleccionado.Id.ToString();
					dto.MostrarSelectorPalets = false;
				}
			}

			CollectionViewSource.GetDefaultView(LineasPendientes).Refresh();
		}

		//[RelayCommand]
		//private async Task ConfirmarAsync()
		//{
		//	if (!LineasPendientes.Any())
		//	{
		//		new WarningDialog("Aviso", "No hay líneas para confirmar.").ShowDialog();
		//		return;
		//	}

		//	// 1. Consultar el estado de palet para cada línea pendiente
		//	foreach (var dto in LineasPendientes)
		//	{
		//		dto.EstadoPaletDestino = await _traspasosService.ConsultarEstadoPaletDestinoAsync(
		//			SessionManager.EmpresaSeleccionada.Value,
		//			dto.AlmacenDestino,
		//			dto.UbicacionDestino
		//		);
		//	}

		//	// 2. Agrupar los resultados
		//	var paletsAbiertos = LineasPendientes
		//		.Where(x => x.EstadoPaletDestino == "Abierto")
		//		.Select(x => $"{x.CodigoArticulo} → {x.AlmacenDestino}/{x.UbicacionDestino}")
		//		.ToList();

		//	var paletsCerrados = LineasPendientes
		//		.Where(x => x.EstadoPaletDestino == "Cerrado")
		//		.Select(x => $"{x.CodigoArticulo} → {x.AlmacenDestino}/{x.UbicacionDestino}")
		//		.ToList();

		//	var sinPalet = LineasPendientes
		//		.Where(x => x.EstadoPaletDestino == "NINGUNO")
		//		.Select(x => $"{x.CodigoArticulo} → {x.AlmacenDestino}/{x.UbicacionDestino}")
		//		.ToList();

		//	// 3. Construir el mensaje resumen
		//	var mensaje = "";
		//	if (paletsAbiertos.Any())
		//		mensaje += "Se agregarán artículos a los siguientes palets ABIERTOS:\n" + string.Join("\n", paletsAbiertos) + "\n\n";
		//	if (paletsCerrados.Any())
		//		mensaje += "Se reabrirán los siguientes palets CERRADOS:\n" + string.Join("\n", paletsCerrados) + "\n\n";
		//	if (sinPalet.Any())
		//		mensaje += "En las siguientes ubicaciones NO hay palet (el stock quedará sin paletizar):\n" + string.Join("\n", sinPalet);
		//	mensaje += "\n¿Deseas continuar con la regularización múltiple?";

		//	// 4. Mostrar el diálogo de confirmación
		//	var confirm = new ConfirmationDialog("Resumen de regularización múltiple", mensaje);
		//	if (confirm.ShowDialog() != true)
		//	{
		//		// El usuario cancela
		//		return;
		//	}

		//	bool todoOk = true;

		//	foreach (var dto in LineasPendientes)
		//	{
		//		dto.TieneError = false;
		//		dto.ErrorMessage = null;

		//		if (string.IsNullOrWhiteSpace(dto.AlmacenDestino))
		//		{
		//			dto.TieneError = true;
		//			dto.ErrorMessage = "Selecciona almacén destino.";
		//			todoOk = false;
		//			continue;
		//		}
		//		// UbicacionDestino puede ser null o vacío (sin ubicar)
		//		if (dto.CantidadAMover <= 0 || dto.CantidadAMover > dto.UnidadSaldo)
		//		{
		//			dto.TieneError = true;
		//			dto.ErrorMessage = "Cantidad a mover no válida.";
		//			todoOk = false;
		//			continue;
		//		}

		//		var crearDto = new CrearTraspasoArticuloDto
		//		{
		//			AlmacenOrigen = dto.CodigoAlmacen,
		//			UbicacionOrigen = dto.Ubicacion,
		//			CodigoArticulo = dto.CodigoArticulo,
		//			Cantidad = dto.CantidadAMover,
		//			UsuarioId = SessionManager.UsuarioActual?.operario ?? 0,
		//			AlmacenDestino = dto.AlmacenDestino,
		//			UbicacionDestino = string.IsNullOrWhiteSpace(dto.UbicacionDestino) ? "" : dto.UbicacionDestino,
		//			FechaCaducidad = dto.FechaCaducidad,
		//			Partida = dto.Partida,
		//			CodigoEmpresa = SessionManager.EmpresaSeleccionada.Value,
		//			FechaInicio = _fechaInicioDialogo,
		//			Finalizar = true,
		//			DescripcionArticulo = dto.DescripcionArticulo // Añadido para que se guarde la descripción
		//		};
		//		var resultado = await _traspasosService.CrearTraspasoArticuloAsync(crearDto);

		//		if (!resultado.Success)
		//		{
		//			todoOk = false;
		//			dto.TieneError = true;
		//			dto.ErrorMessage = resultado.ErrorMessage ?? "Error al realizar el traspaso.";
		//		}
		//	}

		//	if (todoOk)
		//	{
		//		new WarningDialog("Éxito", "Traspasos realizados correctamente.").ShowDialog();
		//		Application.Current.Windows.OfType<Window>()
		//			.FirstOrDefault(w => w.DataContext == this)?.Close();
		//	}
		//	else
		//	{
		//		var errores = LineasPendientes
		//			.Where(x => x.TieneError)
		//			.Select(x => $"{x.DescripcionArticulo} ({x.CodigoArticulo}): {x.ErrorMessage}")
		//			.ToList();

		//		new WarningDialog("Errores en traspasos", string.Join("\n", errores)).ShowDialog();
		//	}

		//	CollectionViewSource.GetDefaultView(LineasPendientes).Refresh();
		//}
		[RelayCommand]
		private async Task ConfirmarAsync()
		{
			if (!LineasPendientes.Any())
			{
				new WarningDialog("Aviso", "No hay líneas para confirmar.").ShowDialog();
				return;
			}

			var empresa = SessionManager.EmpresaSeleccionada.Value;

			// 1) Consultar estado de palet ORIGEN y DESTINO para cada línea
			foreach (var dto in LineasPendientes)
			{
				// Origen
				dto.EstadoPaletOrigen = await _traspasosService.ConsultarEstadoPaletOrigenAsync(
					empresa,
					dto.CodigoAlmacen,
					dto.Ubicacion
				);

				// Destino (puede ser vacío → “NINGUNO”)
				dto.EstadoPaletDestino = await _traspasosService.ConsultarEstadoPaletDestinoAsync(
					empresa,
					dto.AlmacenDestino,
					dto.UbicacionDestino ?? ""
				);
			}

			// 2) Agrupar para el mensaje resumen (usamos capitalización de BD: Abierto/Cerrado)
			var origenCerrados = LineasPendientes
				.Where(x => string.Equals(x.EstadoPaletOrigen, "Cerrado", StringComparison.OrdinalIgnoreCase))
				.Select(x => $"{x.CodigoArticulo} → {x.CodigoAlmacen}/{x.Ubicacion}")
				.ToList();

			var destinoAbiertos = LineasPendientes
				.Where(x => string.Equals(x.EstadoPaletDestino, "Abierto", StringComparison.OrdinalIgnoreCase))
				.Select(x => $"{x.CodigoArticulo} → {x.AlmacenDestino}/{x.UbicacionDestino}")
				.ToList();

			var destinoCerrados = LineasPendientes
				.Where(x => string.Equals(x.EstadoPaletDestino, "Cerrado", StringComparison.OrdinalIgnoreCase))
				.Select(x => $"{x.CodigoArticulo} → {x.AlmacenDestino}/{x.UbicacionDestino}")
				.ToList();

			var destinoSinPalet = LineasPendientes
				.Where(x => string.Equals(x.EstadoPaletDestino, "NINGUNO", StringComparison.OrdinalIgnoreCase))
				.Select(x => $"{x.CodigoArticulo} → {x.AlmacenDestino}/{x.UbicacionDestino}")
				.ToList();

			// 3) Construir mensaje (incluye aviso explícito del ORIGEN)
			var msg = "";
			if (origenCerrados.Any())
				msg += "Se reabrirán los siguientes palets de ORIGEN (están Cerrados y quedarán Abiertos):\n" +
					   string.Join("\n", origenCerrados) + "\n\n";
			if (destinoAbiertos.Any())
				msg += "Se agregarán artículos a los siguientes palets en DESTINO (Abiertos):\n" +
					   string.Join("\n", destinoAbiertos) + "\n\n";
			if (destinoCerrados.Any())
				msg += "Se reabrirán los siguientes palets en DESTINO (Cerrados → Abiertos):\n" +
					   string.Join("\n", destinoCerrados) + "\n\n";
			if (destinoSinPalet.Any())
				msg += "En las siguientes ubicaciones DESTINO no hay palet (quedará sin paletizar):\n" +
					   string.Join("\n", destinoSinPalet) + "\n\n";

			msg += "¿Deseas continuar con la regularización múltiple?";

			// 4) Confirmación
			var confirm = new ConfirmationDialog("Resumen de regularización múltiple", msg);
			if (confirm.ShowDialog() != true) return;

			// 5) Validaciones por línea y envío
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

				// UbicacionDestino puede ser null/vacía (sin ubicar)
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
					CodigoEmpresa = empresa,
					FechaInicio = _fechaInicioDialogo,
					Finalizar = true,
					DescripcionArticulo = dto.DescripcionArticulo,
					Comentario = comentariosTexto, // Añadir comentarios
					
					// NUEVO: Incluir ID del palet destino si está seleccionado
					PaletIdDestino = !string.IsNullOrWhiteSpace(dto.PaletDestinoId) && Guid.TryParse(dto.PaletDestinoId, out var paletId) ? paletId : null,

					// 🔹 clave: si el ORIGEN está Cerrado, pedimos reapertura automática
					ReabrirSiCerradoOrigen = string.Equals(dto.EstadoPaletOrigen, "Cerrado", StringComparison.OrdinalIgnoreCase)
				};

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



		//  NUEVA FUNCIÓN: Obtener todos los almacenes autorizados (individuales + centro)
		private async Task<List<string>> ObtenerAlmacenesAutorizadosAsync()
		{
			var almacenesIndividuales = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();
			var centroLogistico = SessionManager.UsuarioActual?.codigoCentro ?? "0";

			// Si el usuario tiene almacenes individuales, incluir también los del centro
			if (almacenesIndividuales.Any())
			{
				// Obtener almacenes del centro logístico de forma asíncrona
				var almacenesCentro = await _stockService.ObtenerAlmacenesAsync(centroLogistico);
				
				// Combinar almacenes individuales + almacenes del centro
				var todosLosAlmacenes = new List<string>(almacenesIndividuales);
				todosLosAlmacenes.AddRange(almacenesCentro);
				
				// Eliminar duplicados
				return todosLosAlmacenes.Distinct().ToList();
			}
			else
			{
				// Si no tiene almacenes individuales, usar solo los del centro
				return await _stockService.ObtenerAlmacenesAsync(centroLogistico);
			}
		}


	}
}
