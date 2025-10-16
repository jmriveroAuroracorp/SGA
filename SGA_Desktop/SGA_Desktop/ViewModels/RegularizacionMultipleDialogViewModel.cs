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
	
	// Colección separada para el ComboBox común (filtrable)
	public ObservableCollection<AlmacenDto> AlmacenesDestinoComun { get; } = new();
	
	// Vista filtrable para almacén común
	public ICollectionView AlmacenesDestinoView { get; private set; }
	
	[ObservableProperty]
	private string filtroAlmacenesComun = "";
	
	[ObservableProperty]
	private bool isDropDownOpenAlmacenesComun = false;

	// Vista filtrable para ubicaciones común
	public ICollectionView UbicacionesDestinoComunView { get; private set; }
	
	[ObservableProperty]
	private string filtroUbicacionesComun = "";
	
	[ObservableProperty]
	private bool isDropDownOpenUbicacionesComun = false;

		// NUEVAS PROPIEDADES DESTINO COMÚN
		[ObservableProperty]
		private AlmacenDto destinoComunAlmacen;

		[ObservableProperty]
		private string destinoComunAlmacenCodigo;

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

	// 🔷 NUEVO: Combo de almacenes para filtrar (como en TraspasosStockViewModel)
	public ObservableCollection<AlmacenDto> AlmacenesFiltro { get; } = new();
	public ICollectionView AlmacenesFiltroView { get; private set; }
	
	[ObservableProperty]
	private AlmacenDto almacenFiltroSeleccionado;
	
	[ObservableProperty]
	private string filtroAlmacenesTexto = "";

	// 🔷 NUEVO: Propiedad para controlar la visibilidad del combo de almacenes
	[ObservableProperty]
	private bool mostrarComboAlmacenes = false;

	// 🔷 NUEVO: Almacenar todos los resultados de stock para filtrado local
	private List<StockDisponibleDto> _todosLosResultadosStock = new();
	
	// 🔷 NUEVO: Control de estados de expansión para evitar que se cierren al filtrar
	private Dictionary<string, bool> _estadosExpansion = new();

	partial void OnDestinoComunAlmacenChanged(AlmacenDto value)
	{
		_ = CargarUbicacionesDestinoComunAsync();
	}

	partial void OnDestinoComunAlmacenCodigoChanged(string value)
	{
		// Buscar el almacén correspondiente al código
		DestinoComunAlmacen = AlmacenesDestinoComun.FirstOrDefault(a => a.CodigoAlmacen == value);
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
		
		// Inicializar la vista filtrable de almacenes
		AlmacenesFiltroView = CollectionViewSource.GetDefaultView(AlmacenesFiltro);
		AlmacenesFiltroView.Filter = FiltraAlmacenesFiltro;
	}

	public async Task InitializeAsync()
	{
		var empresa = SessionManager.EmpresaSeleccionada!.Value;
		var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
		
		// 🔷 NUEVA LÓGICA: Obtener todos los almacenes autorizados (individuales + centro)
		var almacenesAutorizados = await ObtenerAlmacenesAutorizadosAsync();
		
		var almacenes = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa, centro, almacenesAutorizados);

		// Poblar ambas colecciones con los mismos datos
		AlmacenesDestino.Clear();
		AlmacenesDestinoComun.Clear();
		
		foreach (var a in almacenes)
		{
			AlmacenesDestino.Add(a);          // Para ComboBoxes de líneas (sin filtrar)
			AlmacenesDestinoComun.Add(a);     // Para ComboBox común (filtrable)
		}

		OnPropertyChanged(nameof(AlmacenesDestino));
		OnPropertyChanged(nameof(AlmacenesDestinoComun));
		
		// Inicializar la vista filtrable SOLO para el ComboBox común
		AlmacenesDestinoView = CollectionViewSource.GetDefaultView(AlmacenesDestinoComun);
		AlmacenesDestinoView.Filter = FiltraAlmacenesComun;
		OnPropertyChanged(nameof(AlmacenesDestinoView));
	}

		public async Task CargarUbicacionesDestinoAsync(StockDisponibleDto linea)
		{
			try
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
			}
			catch (Exception ex)
			{
				// En caso de error, solo limpiar las ubicaciones
				linea.UbicacionesDestino.Clear();
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
				// 🔷 NUEVO: Limpiar combo de almacenes cuando no hay artículo
				AlmacenesFiltro.Clear();
				AlmacenFiltroSeleccionado = null;
				_todosLosResultadosStock.Clear();
				MostrarComboAlmacenes = false;
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

			if (resultados.Count == 0)
			{
				// 🔷 NUEVO: Limpiar combo cuando no hay stock
				AlmacenesFiltro.Clear();
				AlmacenFiltroSeleccionado = null;
				_todosLosResultadosStock.Clear();
				MostrarComboAlmacenes = false;
				new WarningDialog("Aviso", "No hay stock para ese artículo.").ShowDialog();
				return;
			}

			// 🔷 NUEVO: Guardar todos los resultados para filtrado local
			_todosLosResultadosStock = new List<StockDisponibleDto>(resultados);

			// 🔷 NUEVO: Cargar combo con los almacenes que realmente tienen stock del artículo
			await CargarAlmacenesConStockAsync(resultados);

			// 🔷 NUEVO: Aplicar filtrado por almacén si hay uno seleccionado
			await FiltrarResultadosPorAlmacen();
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
			if (!LineasPendientes.Any()) return;

			// 1. Cambiar almacén para todas las líneas
			if (DestinoComunAlmacen != null)
			{
				foreach (var dto in LineasPendientes)
				{
					dto.AlmacenDestino = DestinoComunAlmacen.CodigoAlmacen;
				}
			}

			// 2. Cargar ubicaciones solo para las líneas que lo necesiten (evitar duplicados)
			var almacenesUnicos = LineasPendientes
				.Where(dto => !string.IsNullOrEmpty(dto.AlmacenDestino))
				.Select(dto => dto.AlmacenDestino)
				.Distinct()
				.ToList();

			foreach (var almacenCodigo in almacenesUnicos)
			{
				var lineasDelAlmacen = LineasPendientes.Where(dto => dto.AlmacenDestino == almacenCodigo).ToList();
				
				// Cargar ubicaciones solo una vez por almacén
				if (lineasDelAlmacen.Any())
				{
					await CargarUbicacionesDestinoAsync(lineasDelAlmacen.First());
					
					// Copiar las ubicaciones a las demás líneas del mismo almacén
					var ubicaciones = lineasDelAlmacen.First().UbicacionesDestino.ToList();
					foreach (var linea in lineasDelAlmacen.Skip(1))
					{
						linea.UbicacionesDestino.Clear();
						foreach (var ubicacion in ubicaciones)
						{
							linea.UbicacionesDestino.Add(ubicacion);
						}
					}
				}
			}

			// 3. Asignar ubicación común
			if (DestinoComunUbicacion != null)
			{
				foreach (var dto in LineasPendientes)
				{
					dto.UbicacionDestino = DestinoComunUbicacion.Ubicacion;
					
					// Limpiar selección previa de palets
					dto.PaletDestinoSeleccionado = null;
					dto.PaletDestinoId = null;
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

	// Métodos para filtrado de almacén común
	private bool FiltraAlmacenesComun(object obj)
	{
		if (obj is not AlmacenDto almacen) return false;
		if (string.IsNullOrEmpty(FiltroAlmacenesComun)) return true;
		
		return System.Globalization.CultureInfo.CurrentCulture.CompareInfo
			.IndexOf(almacen.DescripcionCombo, FiltroAlmacenesComun, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
	}
	
	// Método para manejar cambios en el filtro de almacén común
	partial void OnFiltroAlmacenesComunChanged(string value)
	{
		// Si el filtro está vacío, limpiar la selección para evitar autocompletado
		if (string.IsNullOrEmpty(value))
		{
			DestinoComunAlmacen = null;
		}
		
		// Refresh más simple, sin Dispatcher
		AlmacenesDestinoView?.Refresh();
	}
	
	// Comandos para controlar dropdown de almacén común
	[RelayCommand]
	private void AbrirDropDownAlmacenesComun()
	{
		// Limpiar filtro y selección al abrir dropdown para permitir escribir desde cero
		FiltroAlmacenesComun = "";
		DestinoComunAlmacen = null;
		IsDropDownOpenAlmacenesComun = true;
	}
	
	[RelayCommand]
	private void CerrarDropDownAlmacenesComun()
	{
		IsDropDownOpenAlmacenesComun = false;
	}

	// Métodos para filtrado de ubicaciones común
	private bool FiltraUbicacionesComun(object obj)
	{
		if (obj is not UbicacionDto ubicacion) return false;
		if (string.IsNullOrEmpty(FiltroUbicacionesComun)) return true;
		
		return System.Globalization.CultureInfo.CurrentCulture.CompareInfo
			.IndexOf(ubicacion.Ubicacion, FiltroUbicacionesComun, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
	}
	
	// Método para manejar cambios en el filtro de ubicaciones común
	partial void OnFiltroUbicacionesComunChanged(string value)
	{
		// Solo limpiar selección si el filtro está vacío Y no hay selección actual
		if (string.IsNullOrEmpty(value) && DestinoComunUbicacion != null)
		{
			// Verificar si el texto del filtro coincide con la ubicación seleccionada
			if (DestinoComunUbicacion.Ubicacion != value)
			{
				DestinoComunUbicacion = null;
			}
		}
		
		UbicacionesDestinoComunView?.Refresh();
	}
	
	// Comandos para controlar dropdown de ubicaciones común
	[RelayCommand]
	private void AbrirDropDownUbicacionesComun()
	{
		IsDropDownOpenUbicacionesComun = true;
	}
	
	[RelayCommand]
	private void CerrarDropDownUbicacionesComun()
	{
		IsDropDownOpenUbicacionesComun = false;
	}

	// 🔷 NUEVO: Método para cargar almacenes basándose en el stock encontrado (igual que TraspasosStockViewModel)
	private async Task CargarAlmacenesConStockAsync(List<StockDisponibleDto> stock)
	{
		try
		{
			// Obtener códigos únicos de almacenes del stock encontrado
			var codigosAlmacenesStock = stock.Select(x => x.CodigoAlmacen).Distinct().ToList();
			
			if (!codigosAlmacenesStock.Any())
			{
				AlmacenesFiltro.Clear();
				MostrarComboAlmacenes = false;
				return;
			}

			// Obtener información completa de los almacenes
			var empresa = SessionManager.EmpresaSeleccionada!.Value;
			var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
			var permisos = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();
			
			if (!permisos.Any())
			{
				permisos = await _stockService.ObtenerAlmacenesAsync(centro);
			}
			
			// Obtener todos los almacenes autorizados
			var todosAlmacenes = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa, centro, permisos);
			
			// Filtrar solo los almacenes que tienen stock del artículo
			var almacenesConStock = todosAlmacenes
				.Where(a => codigosAlmacenesStock.Contains(a.CodigoAlmacen))
				.OrderBy(a => a.DescripcionCombo)
				.ToList();
			
			// Limpiar y poblar el combo
			AlmacenesFiltro.Clear();
			foreach (var almacen in almacenesConStock)
				AlmacenesFiltro.Add(almacen);
				
			// Limpiar selección previa si el almacén ya no está disponible
			if (AlmacenFiltroSeleccionado != null && 
				!almacenesConStock.Any(a => a.CodigoAlmacen == AlmacenFiltroSeleccionado.CodigoAlmacen))
			{
				AlmacenFiltroSeleccionado = null;
			}
			
			// 🔷 NUEVO: Mostrar combo solo si hay almacenes
			MostrarComboAlmacenes = AlmacenesFiltro.Count > 0;
				
			OnPropertyChanged(nameof(AlmacenesFiltro));
		}
		catch (Exception ex)
		{
			// En caso de error, continuar sin filtro de almacenes
			AlmacenesFiltro.Clear();
			MostrarComboAlmacenes = false;
		}
	}

	// 🔷 NUEVO: Método para filtrar almacenes en el combo
	private bool FiltraAlmacenesFiltro(object obj)
	{
		if (obj is not AlmacenDto almacen) return false;
		if (string.IsNullOrEmpty(FiltroAlmacenesTexto)) return true;
		
		return System.Globalization.CultureInfo.CurrentCulture.CompareInfo
			.IndexOf(almacen.DescripcionCombo, FiltroAlmacenesTexto, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
	}

	// 🔷 NUEVO: Método para manejar cambios en el filtro de almacenes
	partial void OnFiltroAlmacenesTextoChanged(string value)
	{
		AlmacenesFiltroView?.Refresh();
	}

	// 🔷 NUEVO: Método para filtrar resultados por almacén sin hacer nueva búsqueda
	private async Task FiltrarResultadosPorAlmacen()
	{
		// Guardar el estado de expansión antes de limpiar
		GuardarEstadosExpansion();
		
		// Limpiar resultados actuales
		ArticulosConStock.Clear();
		
		// Obtener stock filtrado
		var stockFiltrado = _todosLosResultadosStock;
		
		// Aplicar filtro por almacén si hay uno seleccionado
		if (AlmacenFiltroSeleccionado != null)
		{
			stockFiltrado = stockFiltrado.Where(x => x.CodigoAlmacen == AlmacenFiltroSeleccionado.CodigoAlmacen).ToList();
		}
		
		// Agrupar por artículo
		var grupos = stockFiltrado.GroupBy(x => new { x.CodigoArticulo, x.DescripcionArticulo })
								  .Select(g => new ArticuloConStockDto
								  {
									  CodigoArticulo = g.Key.CodigoArticulo,
									  DescripcionArticulo = g.Key.DescripcionArticulo,
									  Ubicaciones = new ObservableCollection<StockDisponibleDto>(
										  g.OrderBy(x => x.CodigoAlmacen)
											.ThenBy(x => x.Ubicacion)
											.ToList())
								  })
								  .OrderBy(a => a.CodigoArticulo)
								  .ToList();
		
		// Añadir grupos a la colección
		foreach (var g in grupos)
		{
			// Pre-rellenar la cantidad con el valor disponible para cada ubicación
			foreach (var ubicacion in g.Ubicaciones)
			{
				ubicacion.CantidadAMoverTexto = ubicacion.Disponible.ToString("F4");
			}
			ArticulosConStock.Add(g);
		}
		
		// Restaurar el estado de expansión después de añadir los elementos
		await RestaurarEstadosExpansion();
	}

	// 🔷 NUEVO: Método para manejar cambios en la selección del almacén
	partial void OnAlmacenFiltroSeleccionadoChanged(AlmacenDto value)
	{
		// Actualizar el texto del filtro con la selección
		if (value != null)
		{
			FiltroAlmacenesTexto = value.DescripcionCombo;
		}
		
		// 🔷 CORREGIDO: Solo filtrar los resultados existentes, NO hacer otra búsqueda
		_ = FiltrarResultadosPorAlmacen();
	}

	// 🔷 NUEVO: Método para guardar estados de expansión
	private void GuardarEstadosExpansion()
	{
		_estadosExpansion.Clear();
		foreach (var grupo in ArticulosConStock)
		{
			var clave = $"{grupo.CodigoArticulo}_{grupo.DescripcionArticulo}";
			_estadosExpansion[clave] = grupo.IsExpanded;
		}
	}

	// 🔷 NUEVO: Método para restaurar estados de expansión
	private async Task RestaurarEstadosExpansion()
	{
		// Pequeño delay para asegurar que la UI se actualice
		await Task.Delay(50);
		
		foreach (var grupo in ArticulosConStock)
		{
			var clave = $"{grupo.CodigoArticulo}_{grupo.DescripcionArticulo}";
			if (_estadosExpansion.ContainsKey(clave))
			{
				grupo.IsExpanded = _estadosExpansion[clave];
			}
		}
		
		// Forzar la actualización de la UI
		OnPropertyChanged(nameof(ArticulosConStock));
	}

}
}
