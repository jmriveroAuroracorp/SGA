using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Newtonsoft.Json;
using SGA_Desktop.Dialog;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace SGA_Desktop.ViewModels
{
	public partial class ConsultaStockViewModel : ObservableObject
	{
		#region Constants
		private const string SIN_UBICACION = "Sin ubicación";
		private const string TODAS = "Todas";
		private const string TODO_ALMACEN = "Todo el almacén";


		#endregion


		#region Variables
		private bool _busquedaPorDescripcion;
		private AlmacenDto? almacenArticuloPorDefecto;
		private AlmacenDto? almacenUbicacionPorDefecto;
		private Dictionary<string, bool> _estadosExpansion = new();

		#endregion

	#region Fields & Services
	private readonly StockService _stockService;
	private readonly PrintQueueService _printService = new PrintQueueService();
	private readonly LoginService _loginService = new LoginService();
	public ObservableCollection<ImpresoraDto> ImpresorasDisponibles { get; } = new();

	#endregion

	#region Constructor
	public ConsultaStockViewModel(StockService stockService)
	{
		_stockService = stockService;
			EmpresaActual = ObtenerNombreEmpresaActual();
			Almacenes = new ObservableCollection<string>();
			Ubicaciones = new ObservableCollection<string>();
			ResultadosStock = new ObservableCollection<StockDto>();

			FiltroArticulo = string.Empty;
			FiltroUbicacion = string.Empty;
			FiltroPartida = string.Empty;

			// ② Inicializa ambas colecciones
			ResultadosStock = new ObservableCollection<StockDto>();
			ResultadosStockPorUbicacion = new ObservableCollection<StockDto>();
			
			// 🔷 NUEVO: Inicializar colección de artículos agrupados
			ArticulosConUbicaciones = new ObservableCollection<ArticuloStockGroup>();

			ResultadosStockPorUbicacionView = CollectionViewSource.GetDefaultView(ResultadosStockPorUbicacion);
			ResultadosStockPorUbicacionView.Filter = FiltroStock;

			if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
				_ = InitializeAsync();
		}

		public ConsultaStockViewModel() : this(new StockService()) { }


		#endregion

		#region Observable Properties
		[ObservableProperty]
		private string empresaActual;

	public ObservableCollection<string> Almacenes { get; }
	public ObservableCollection<string> Ubicaciones { get; }
	public ObservableCollection<StockDto> ResultadosStock { get; }
	public ObservableCollection<StockDto> ResultadosStockPorUbicacion { get; }
	public ObservableCollection<ArticuloResumenDto> ArticulosUnicos { get; } = new();
	public ObservableCollection<StockDto> StockFiltrado { get; } = new();
	public ObservableCollection<AlmacenDto> AlmacenesCombo { get; } = new();
	
	// 🔷 NUEVO: Colección para artículos agrupados con expanders
	public ObservableCollection<ArticuloStockGroup> ArticulosConUbicaciones { get; } = new();
	
	// Vista filtrable para almacenes combo
	public ICollectionView AlmacenesComboView { get; private set; }
	
	// Vista filtrable para ubicaciones (modo artículo)
	public ICollectionView UbicacionesView { get; private set; }
	
	// Vista filtrable para ubicaciones (modo ubicación)
	public ICollectionView UbicacionesUbicacionView { get; private set; }

		[ObservableProperty]
		private string almacenSeleccionado;

		[ObservableProperty]
		private string filtroArticulo;

		[ObservableProperty]
		private string filtroUbicacion;

		[ObservableProperty]
		private string filtroPartida;

		[ObservableProperty]
		private string articuloMostrado;

		[ObservableProperty]
		private bool isArticleMode = true;

		[ObservableProperty]
		private bool isLocationMode = false;

		[ObservableProperty]
		private string filtroDescripcion;

		[ObservableProperty]
		private string? almacenSeleccionadoArticulo;

		[ObservableProperty]
		private string? almacenSeleccionadoUbicacion;

		[ObservableProperty]
		private ArticuloResumenDto? articuloSeleccionado;

		[ObservableProperty]
		private AlmacenDto? almacenSeleccionadoCombo;

		[ObservableProperty]
		private bool filtrarUbicacionesConStock = true;

	[ObservableProperty]
	private object? articuloSeleccionadoParaImprimir;
	
	// Propiedades para filtrado de almacenes (modo artículo)
	[ObservableProperty]
	private string filtroAlmacenesCombo = "";
	
	[ObservableProperty]
	private bool isDropDownOpenAlmacenes = false;
	
	// Propiedades para filtrado de almacenes (modo ubicación)
	[ObservableProperty]
	private string filtroAlmacenesComboLocation = "";
	
	[ObservableProperty]
	private bool isDropDownOpenAlmacenesLocation = false;
	
	// Vistas filtrables para almacenes (una por modo)
	public ICollectionView AlmacenesComboArticleView { get; set; }
	public ICollectionView AlmacenesComboLocationView { get; set; }
	
	// Propiedades para filtrado de ubicaciones (modo artículo)
	[ObservableProperty]
	private string filtroUbicaciones = "";
	
	[ObservableProperty]
	private bool isDropDownOpenUbicaciones = false;
	
	// Propiedades para filtrado de ubicaciones (modo ubicación)
	[ObservableProperty]
	private string filtroUbicacionesUbicacion = "";
	
	[ObservableProperty]
	private bool isDropDownOpenUbicacionesUbicacion = false;

		#endregion

		#region Computed Properties


		public IEnumerable<StockDto> ResultadosStockActive =>
			IsLocationMode
				? ResultadosStockPorUbicacion
				: ResultadosStock;
		public bool CanEnableInputs => !string.IsNullOrWhiteSpace(FiltroArticulo);

		public bool CanEnableLocation =>
			IsLocationMode &&
			!string.IsNullOrWhiteSpace(AlmacenSeleccionado) &&
			AlmacenSeleccionado != TODAS;

		public ICollectionView ResultadosStockPorUbicacionView { get; }

		private string _filtroBusqueda;
		public string FiltroBusqueda
		{
			get => _filtroBusqueda;
			set
			{
				if (SetProperty(ref _filtroBusqueda, value))
				{
					ResultadosStockPorUbicacionView.Refresh();
					OnPropertyChanged(nameof(CanClearFilters));
				}
			}
		}

		public Visibility ArticleFiltersVisibility => IsArticleMode ? Visibility.Visible : Visibility.Collapsed;
		public Visibility LocationFiltersVisibility => IsLocationMode ? Visibility.Visible : Visibility.Collapsed;

		public Visibility ArticulosUnicosVisibility =>
	_busquedaPorDescripcion && ArticulosUnicos.Count > 1
		? Visibility.Visible
		: Visibility.Collapsed;

		public Visibility ListViewVisibility =>
			(!_busquedaPorDescripcion || StockFiltrado.Any())
				? Visibility.Visible
				: Visibility.Collapsed;

		public IRelayCommand BuscarCommand =>
			IsArticleMode ? BuscarPorArticuloCommand : BuscarPorUbicacionCommand;

		/// <summary>
		/// Determina si el botón de refrescar debe estar habilitado
		/// </summary>
		public bool CanRefresh =>
			(IsArticleMode && StockFiltrado.Any()) ||
			(IsLocationMode && ResultadosStockPorUbicacion.Any());

		/// <summary>
		/// Determina si el botón de limpiar filtros debe estar habilitado
		/// </summary>
		public bool CanClearFilters =>
			(IsArticleMode && (!string.IsNullOrWhiteSpace(FiltroArticulo) || 
							   !string.IsNullOrWhiteSpace(FiltroPartida) || 
							   !string.IsNullOrWhiteSpace(FiltroUbicacion) ||
							   (AlmacenSeleccionadoCombo?.CodigoAlmacen != "Todas"))) ||
			(IsLocationMode && (!string.IsNullOrWhiteSpace(FiltroUbicacion) || 
								!string.IsNullOrWhiteSpace(FiltroBusqueda) ||
								(AlmacenSeleccionadoCombo?.CodigoAlmacen != "Todas")));

		/// <summary>
		/// Determina si el botón de exportar Excel debe estar habilitado
		/// </summary>
		public bool CanExportExcel =>
			(IsArticleMode && StockFiltrado.Any()) ||
			(IsLocationMode && ResultadosStockPorUbicacion.Any());

		/// <summary>
		/// Determina si el botón de imprimir etiqueta debe estar habilitado
		/// Solo se activa cuando se selecciona un card interno (ubicación específica), no el card padre
		/// </summary>
		public bool CanImprimirEtiqueta =>
			ArticuloSeleccionadoParaImprimir != null &&
			(ArticuloSeleccionadoParaImprimir is StockDto || ArticuloSeleccionadoParaImprimir is StockDisponibleDto);
		
		
		#endregion


		#region Property Change Callbacks
		partial void OnFiltroArticuloChanged(string oldValue, string newValue)
		{
			OnPropertyChanged(nameof(CanEnableInputs));
			OnPropertyChanged(nameof(CanEnableLocation));
			OnPropertyChanged(nameof(CanClearFilters));
		}

		partial void OnAlmacenSeleccionadoComboChanged(AlmacenDto? oldValue, AlmacenDto? newValue)
		{
			if (newValue is null) return;

			AlmacenSeleccionado = newValue.CodigoAlmacen;
			OnPropertyChanged(nameof(CanClearFilters));
			OnPropertyChanged(nameof(CanRefresh));
			OnPropertyChanged(nameof(CanExportExcel));
			_ = LoadUbicacionesAsync(newValue.CodigoAlmacen);
		}







		partial void OnFiltroPartidaChanged(string oldValue, string newValue)
		{
			OnPropertyChanged(nameof(CanClearFilters));
		}

		partial void OnFiltroUbicacionChanged(string oldValue, string newValue)
		{
			OnPropertyChanged(nameof(CanClearFilters));
		}

		partial void OnIsArticleModeChanged(bool oldValue, bool newValue)
		{
			if (newValue)
			{
				almacenUbicacionPorDefecto = AlmacenSeleccionadoCombo;
				AlmacenSeleccionadoCombo = almacenArticuloPorDefecto; // No seleccionar automáticamente
				SwitchMode(resetFilters: false, setArticle: true);
				
				// Sincronizar filtros de texto
				FiltroUbicaciones = "";
				FiltroUbicacionesUbicacion = "";
				FiltroAlmacenesCombo = "";
				FiltroAlmacenesComboLocation = "";
				
				// Limpiar selecciones
				AlmacenSeleccionadoCombo = null;
				FiltroUbicacion = "";
				
				// 🔷 CORREGIDO: NO recrear vistas aquí, SwitchMode() ya llama a LoadUbicacionesAsync()
			}
			OnPropertyChanged(nameof(BuscarCommand));
			OnPropertyChanged(nameof(CanRefresh));
			OnPropertyChanged(nameof(CanClearFilters));
			OnPropertyChanged(nameof(CanExportExcel));
		}


		partial void OnIsLocationModeChanged(bool oldValue, bool newValue)
		{
			if (newValue)
			{
				almacenArticuloPorDefecto = AlmacenSeleccionadoCombo;
				AlmacenSeleccionadoCombo = almacenUbicacionPorDefecto; // No seleccionar automáticamente
				SwitchMode(resetFilters: false, setArticle: false);
				
				// Sincronizar filtros de texto
				FiltroUbicacionesUbicacion = "";
				FiltroUbicaciones = "";
				FiltroAlmacenesCombo = "";
				FiltroAlmacenesComboLocation = "";
				
				// Limpiar selecciones
				AlmacenSeleccionadoCombo = null;
				FiltroUbicacion = "";
				
				// 🔷 CORREGIDO: NO recrear vistas aquí, SwitchMode() ya llama a LoadUbicacionesAsync()
			}
			OnPropertyChanged(nameof(BuscarCommand));
			OnPropertyChanged(nameof(CanRefresh));
			OnPropertyChanged(nameof(CanClearFilters));
			OnPropertyChanged(nameof(CanExportExcel));
		}

		partial void OnArticuloSeleccionadoChanged(ArticuloResumenDto? oldValue, ArticuloResumenDto? newValue)
		{
			if (newValue == null)
				return;

			// 1) Pongo la descripción como ArticuloMostrado
			ArticuloMostrado = newValue.DescripcionArticulo;

			// 2) Relleno StockFiltrado
			StockFiltrado.Clear();
			foreach (var s in ResultadosStock.Where(x => x.CodigoArticulo == newValue.CodigoArticulo))
				StockFiltrado.Add(s);

			// 3) Ahora ya no estamos en búsqueda por descripción
			_busquedaPorDescripcion = false;

			// 4) Disparo todas las notificaciones
			OnPropertyChanged(nameof(ArticuloMostrado));
			OnPropertyChanged(nameof(ArticulosUnicosVisibility));
			OnPropertyChanged(nameof(ListViewVisibility));
		}

		partial void OnArticuloSeleccionadoParaImprimirChanged(object? oldValue, object? newValue)
		{
			// 🔷 NUEVO: Solo permitir selección de items internos (ubicaciones específicas)
			// Si se selecciona un ArticuloStockGroup (card padre), limpiar la selección
			if (newValue != null && newValue is ArticuloStockGroup)
			{
				ArticuloSeleccionadoParaImprimir = null;
				return;
			}
			
			OnPropertyChanged(nameof(CanImprimirEtiqueta));
		}



		#endregion

		#region Commands
		[RelayCommand]
		private void LimpiarFiltros()
		{
			if (IsArticleMode)
			{
				// Solo limpia filtros del modo artículo
				FiltroArticulo = string.Empty;
				FiltroPartida = string.Empty;
				FiltroUbicacion = string.Empty;
				AlmacenSeleccionado = TODAS;
				// 👇 Añade esta línea para reiniciar el ComboBox de almacenes
				AlmacenSeleccionadoCombo = AlmacenesCombo.FirstOrDefault(a => a.CodigoAlmacen == TODAS);
			}
			else if (IsLocationMode)
			{
				AlmacenSeleccionado = TODAS;
				FiltroUbicacion = string.Empty;
				FiltroBusqueda = string.Empty;
				AlmacenSeleccionadoCombo = AlmacenesCombo.FirstOrDefault(a => a.CodigoAlmacen == TODAS);
			}
			
			// Limpiar resultados al limpiar filtros
			ResultadosStock.Clear();
			ResultadosStockPorUbicacion.Clear();
			StockFiltrado.Clear();
			ArticulosConUbicaciones.Clear();
			ArticuloMostrado = string.Empty;
			
			// Notificar cambios
			OnPropertyChanged(nameof(CanRefresh));
			OnPropertyChanged(nameof(CanClearFilters));
			OnPropertyChanged(nameof(CanExportExcel));
		}


		[RelayCommand]
		private async Task BuscarPorArticuloAsync()
		{
			try
			{
				// 0) Validación básica
				if (string.IsNullOrWhiteSpace(FiltroArticulo))
				{
					var advertencia = new WarningDialog(
						"Buscar artículo",
						"Debes introducir un código o descripción para buscar.",
						"\uE814"
					);
			// Solo establecer Owner si la ventana principal está disponible
			if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
				advertencia.Owner = Application.Current.MainWindow;
					advertencia.ShowDialog();
					return;
				}

				// Limpiar estados previos
				ArticulosUnicos.Clear();
				StockFiltrado.Clear();
				ArticulosConUbicaciones.Clear();
				ArticuloMostrado = string.Empty;
				_busquedaPorDescripcion = false;
				OnPropertyChanged(nameof(ArticuloMostrado));
				OnPropertyChanged(nameof(ArticulosUnicosVisibility));
				OnPropertyChanged(nameof(ListViewVisibility));

				var (almacenParam, ubicParam) = BuildArticleParams();

				// 1) Intento buscar por código
				var lista = await _stockService.ObtenerPorArticuloAsync(
					SessionManager.EmpresaSeleccionada!.Value,
					codigoArticulo: FiltroArticulo,
					partida: string.IsNullOrWhiteSpace(FiltroPartida) ? null : FiltroPartida,
					codigoAlmacen: almacenParam,
					codigoUbicacion: ubicParam,
					descripcion: null
				);

				// 2) Si no hay resultados por código, intento por descripción
				if (lista == null || !lista.Any())
				{
					_busquedaPorDescripcion = true;
					lista = await _stockService.ObtenerPorArticuloAsync(
						SessionManager.EmpresaSeleccionada!.Value,
						codigoArticulo: null,
						partida: string.IsNullOrWhiteSpace(FiltroPartida) ? null : FiltroPartida,
						codigoAlmacen: almacenParam,
						codigoUbicacion: ubicParam,
						descripcion: FiltroArticulo
					);
				}

				// 3) 🔷 NUEVA LÓGICA: Filtrar por permisos de almacén (individuales + centro)
				var almacenesAutorizados = ObtenerAlmacenesAutorizados();
				lista = lista.Where(x => almacenesAutorizados.Contains(x.CodigoAlmacen)).ToList();

				// 4) Guardar todo el stock filtrado para detalle
				ResultadosStock.Clear();
				foreach (var s in lista)
					ResultadosStock.Add(s);

				// 5) 🔷 NUEVA LÓGICA: Consultar bloqueos de calidad
				var codigosArticulos = lista.Select(s => s.CodigoArticulo).Distinct().ToList();
				var bloqueosCalidad = await _stockService.ObtenerBloqueosCalidadAsync(
					SessionManager.EmpresaSeleccionada!.Value, 
					codigosArticulos);

				// 6) 🔷 NUEVA LÓGICA: Agrupar en artículos con expanders (como TraspasosStockViewModel)
				// Convertir StockDto a StockDisponibleDto para compatibilidad
				var stockDisponible = lista.Select(s => 
				{
					var bloqueo = bloqueosCalidad.GetValueOrDefault(s.CodigoArticulo);
					return new StockDisponibleDto
					{
						CodigoArticulo = s.CodigoArticulo,
						DescripcionArticulo = s.DescripcionArticulo,
						CodigoAlternativo = s.CodigoAlternativo,
						CodigoAlmacen = s.CodigoAlmacen,
						Ubicacion = s.Ubicacion,
						Partida = s.Partida,
						FechaCaducidad = s.FechaCaducidad,
						Disponible = s.UnidadSaldo,
						Reservado = 0, // No tenemos esta información en StockDto
						UnidadSaldo = s.UnidadSaldo,
						// 🔷 NUEVAS PROPIEDADES para compatibilidad
						Palets = s.Palets ?? new List<PaletDetalleDto>(),
						TotalArticuloGlobal = s.TotalArticuloGlobal,
						TotalArticuloAlmacen = s.TotalArticuloAlmacen,
						// 🔷 NUEVO: Información de bloqueo por calidad
						IsBloqueadoCalidad = bloqueo?.IsBloqueado ?? false,
						MotivoBloqueoCalidad = bloqueo?.MotivoBloqueo,
						FechaBloqueoCalidad = bloqueo?.FechaBloqueo
					};
				}).ToList();
				
				var grupos = stockDisponible
					.GroupBy(x => new { x.CodigoArticulo, x.DescripcionArticulo })
					.Select(g => new ArticuloStockGroup
					{
						CodigoArticulo = g.Key.CodigoArticulo,
						DescripcionArticulo = g.Key.DescripcionArticulo,
						Ubicaciones = new ObservableCollection<StockDisponibleDto>(
							g.OrderBy(x => x.CodigoAlmacen)
							  .ThenBy(x => x.Ubicacion)
							  .ToList()),
						// 🔷 NUEVO: Expandir automáticamente cuando se busca por código
						IsExpanded = !_busquedaPorDescripcion
					})
					.OrderBy(a => a.CodigoArticulo)
					.ToList();

				// 6) 🔷 NUEVO: Siempre mostrar con expanders (como TraspasosStockViewModel)
				ArticulosConUbicaciones.Clear();
				foreach (var grupo in grupos)
					ArticulosConUbicaciones.Add(grupo);

				// 7) 🔷 NUEVO: También llenar StockFiltrado para compatibilidad
				StockFiltrado.Clear();
				foreach (var s in lista)
					StockFiltrado.Add(s);

				// 7) Actualizar visibilidades
				OnPropertyChanged(nameof(ArticuloMostrado));
				OnPropertyChanged(nameof(ArticulosUnicosVisibility));
				OnPropertyChanged(nameof(ListViewVisibility));
				OnPropertyChanged(nameof(CanRefresh));
				OnPropertyChanged(nameof(CanExportExcel));

			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message, "Error al consultar por artículo", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		[RelayCommand]
		private async Task BuscarPorUbicacionAsync()
		{
			try
			{
				var almacen = AlmacenSeleccionadoCombo ?? AlmacenesCombo
					.FirstOrDefault(a => a.CodigoAlmacen == AlmacenSeleccionado);

				if (almacen == null || almacen.CodigoAlmacen == TODAS)
				{
					return;
				}

				// 🔷 NUEVA LÓGICA: Determinar qué consultar según la selección
				string? ubicacionParam;

				switch (FiltroUbicacion)
				{
					case TODO_ALMACEN:
						// Consultar todo el almacén (sin especificar ubicación)
						ubicacionParam = null;
						break;

					case SIN_UBICACION:
						// Consultar ubicaciones vacías (artículos sin ubicar)
						ubicacionParam = string.Empty;
						break;

					default:
						// Consultar ubicación específica
						ubicacionParam = FiltroUbicacion;
						break;
				}


				var lista = await _stockService.ObtenerPorUbicacionAsync(
					SessionManager.EmpresaSeleccionada!.Value,
					almacen.CodigoAlmacen,
					ubicacionParam
				);


				// 🔷 NUEVO: Consultar bloqueos de calidad para modo ubicación
				var codigosArticulos = lista.Select(s => s.CodigoArticulo).Distinct().ToList();
				var bloqueosCalidad = await _stockService.ObtenerBloqueosCalidadAsync(
					SessionManager.EmpresaSeleccionada!.Value, 
					codigosArticulos);

				// Aplicar información de bloqueos a los resultados
				foreach (var stock in lista)
				{
					var bloqueo = bloqueosCalidad.GetValueOrDefault(stock.CodigoArticulo);
					if (bloqueo != null)
					{
						stock.IsBloqueadoCalidad = bloqueo.IsBloqueado;
						stock.MotivoBloqueoCalidad = bloqueo.MotivoBloqueo;
						stock.FechaBloqueoCalidad = bloqueo.FechaBloqueo;
					}
				}

				// 🔷 MODIFICADO: Ahora siempre filtramos por permisos usando la nueva lógica
				LlenarResultados(lista, filterByPermissions: true);
				OnPropertyChanged(nameof(CanRefresh));
				OnPropertyChanged(nameof(CanExportExcel));
			}
			catch (Exception ex)
			{
				MostrarError("Error al consultar por ubicación", ex);
			}
		}



		[RelayCommand]
		private void ExportarExcel()
		{
			// ▶️ Cambiado: exportamos StockFiltrado en modo artículo
			var listaActiva = IsArticleMode
				? StockFiltrado.ToList()
				: ResultadosStockPorUbicacion.ToList();

			if (!listaActiva.Any())
			{
				var advertencia = new WarningDialog(
					"Exportar Excel",
					"No hay datos para exportar.",
					"\uE814" // ícono de advertencia
				);
				// Solo establecer Owner si la ventana principal está disponible
				if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
					advertencia.Owner = Application.Current.MainWindow;
				advertencia.ShowDialog();
				return;
			}

			// 1) Confirmar con nuestro dialog
			var confirm = new ConfirmationDialog(
				"Confirmar exportación",
				$"Se van a exportar {listaActiva.Count} registros.\n¿Deseas continuar?",
				"\uE11B"    // ícono de pregunta
			);
			// Solo establecer Owner si la ventana principal está disponible
			if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
				confirm.Owner = Application.Current.MainWindow;
			if (confirm.ShowDialog() != true)
				return;

			// 2) Diálogo para elegir fichero
			var dlg = new SaveFileDialog
			{
				Filter = "Libro de Excel (*.xlsx)|*.xlsx",
				FileName = IsArticleMode
					? "ConsultaPorArticulo.xlsx"
					: "ConsultaPorUbicacion.xlsx"
			};
			if (dlg.ShowDialog() != true) return;

			// 3) Crear workbook...
			using var wb = new XLWorkbook();
			var ws = wb.Worksheets.Add("Stock");

			// 4) Cabeceras
			var headers = new[] {
		"Código Empresa",
		"Código Artículo",
		"Descripción",
		"Almacén",
		"Ubicación",
		"Partida",
		"Fecha Caducidad",
		"Saldo"
	};
			for (int i = 0; i < headers.Length; i++)
				ws.Cell(1, i + 1).Value = headers[i];

			// 5) Filas
			int row = 2;
			foreach (var item in listaActiva)
			{
				ws.Cell(row, 1).Value = item.CodigoEmpresa;
				ws.Cell(row, 2).Value = item.CodigoArticulo;
				ws.Cell(row, 3).Value = item.DescripcionArticulo ?? "";
				ws.Cell(row, 4).Value = $"{item.CodigoAlmacen} – {item.Almacen}";
				ws.Cell(row, 5).Value = item.Ubicacion;
				ws.Cell(row, 6).Value = item.Partida;
				ws.Cell(row, 7).Value = item.FechaCaducidad;
				ws.Cell(row, 8).Value = item.UnidadSaldo;
				row++;
			}

			// 6) Auto‐ajustar anchos
			ws.Columns().AdjustToContents();

			// 7) Guardar y aviso final
			wb.SaveAs(dlg.FileName);
			var info = new WarningDialog(
				"Exportación completada",
				$"Datos exportados correctamente a:\n{dlg.FileName}",
				"\uE946" // ícono de información
			);
			// Solo establecer Owner si la ventana principal está disponible
			if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
				info.Owner = Application.Current.MainWindow;
			info.ShowDialog();
		}

		[RelayCommand]
		private async Task ImprimirEtiquetaStockAsync()
		{
			if (ArticuloSeleccionadoParaImprimir == null)
			{
				var advertencia = new WarningDialog(
					"Impresión de etiqueta",
					"Debes seleccionar un artículo para imprimir la etiqueta.",
					"\uE814" // icono de advertencia
				)
				{ Owner = Application.Current.MainWindow };
				advertencia.ShowDialog();
				return;
			}

			// Cargar impresoras si no están cargadas
			if (ImpresorasDisponibles.Count == 0)
			{
				var impresoras = await _printService.ObtenerImpresorasAsync();
				ImpresorasDisponibles.Clear();
				foreach (var imp in impresoras)
					ImpresorasDisponibles.Add(imp);
			}

			// usa el nombre preferido que tengas (sesión o BD). Si no, el primero.
			string? preNombre = SessionManager.PreferredPrinter
	?? ImpresorasDisponibles.FirstOrDefault()?.Nombre;

			var dlgVm = new ConfirmarImpresionDialogViewModel(
				ImpresorasDisponibles,
				preNombre,
				_loginService ?? new LoginService()// importante: el mismo que usas en el resto de la app
			);

			var dlg = new ConfirmarImpresionDialog
			{
				DataContext = dlgVm
			};
			
			// Solo establecer Owner si la ventana principal está disponible
			if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
			{
				dlg.Owner = Application.Current.MainWindow;
			}

		if (dlg.ShowDialog() != true) return;

		// ya está guardado en BD y en SessionManager por el propio diálogo
		var seleccionada = dlgVm.ImpresoraSeleccionada;
		
		// Extraer propiedades según el tipo de objeto seleccionado
		string codigoArticulo;
		string descripcionArticulo;
		string? codigoAlternativo;
		DateTime? fechaCaducidad;
		string? partida;

		if (ArticuloSeleccionadoParaImprimir is StockDto stockDto)
		{
			// Modo ubicación: StockDto directo
			codigoArticulo = stockDto.CodigoArticulo;
			descripcionArticulo = stockDto.DescripcionArticulo ?? string.Empty;
			codigoAlternativo = stockDto.CodigoAlternativo;
			fechaCaducidad = stockDto.FechaCaducidad;
			partida = stockDto.Partida;
		}
		else if (ArticuloSeleccionadoParaImprimir is StockDisponibleDto stockDisponible)
		{
			// Modo artículo: StockDisponibleDto directo (ubicación específica seleccionada)
			codigoArticulo = stockDisponible.CodigoArticulo;
			descripcionArticulo = stockDisponible.DescripcionArticulo ?? string.Empty;
			codigoAlternativo = stockDisponible.CodigoAlternativo;
			fechaCaducidad = stockDisponible.FechaCaducidad;
			partida = stockDisponible.Partida;
		}
		else
		{
			var error = new WarningDialog(
				"Error de impresión",
				"Tipo de objeto no válido para la impresión.",
				"\uE814"
			)
			{ Owner = Application.Current.MainWindow };
			error.ShowDialog();
			return;
		}

		// Obtener alérgenos del artículo
		var alergenos = await _stockService.ObtenerAlergenosArticuloAsync(
			SessionManager.EmpresaSeleccionada!.Value,
			codigoArticulo);

		// Construir el DTO para impresión
		var dto = new LogImpresionDto
		{
			Usuario = SessionManager.Operario.ToString(),
			Dispositivo = Environment.MachineName,
			IdImpresora = dlgVm.ImpresoraSeleccionada?.Id ?? 0,
			EtiquetaImpresa = 0,
			Copias = dlgVm.NumeroCopias,
			CodigoArticulo = codigoArticulo,
			DescripcionArticulo = descripcionArticulo,
			CodigoAlternativo = codigoAlternativo,
			FechaCaducidad = fechaCaducidad,
			Partida = partida,
			Alergenos = alergenos,
			PathEtiqueta = "\\\\Sage200\\mrh\\Servicios\\PrintCenter\\ETIQUETAS\\MMPP_MES.nlbl",
			TipoEtiqueta = 1, // Etiqueta de stock
			CodigoGS1 = null,
			CodigoPalet = null
		};
		
		try
		{
			await _printService.InsertarRegistroImpresionAsync(dto);
			await _loginService.RegistrarLogEventoAsync(new LogEvento
			{
				fecha = DateTime.Now,
				idUsuario = SessionManager.Operario,
				tipo = "IMPRESION_ETIQUETA",
				origen = "ConsultaStockView",
				descripcion = $"Impresión de etiqueta artículo {dto.CodigoArticulo}",
				detalle = $"Copias={dto.Copias}, ImpresoraId={dto.IdImpresora}, Alergenos={dto.Alergenos}",
				idDispositivo = dto.Dispositivo
			});

			// Confirmar impresión
			var confirmacion = new WarningDialog(
				"Impresión registrada",
				$"La etiqueta se ha encolado correctamente.\n\nAlérgenos guardados: {(string.IsNullOrEmpty(dto.Alergenos) ? "Ninguno" : dto.Alergenos)}",
				"\uE73E" // icono de check
			);
			
			// Solo establecer Owner si la ventana principal está disponible
			if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
			{
				confirmacion.Owner = Application.Current.MainWindow;
			}
			confirmacion.ShowDialog();
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.Message, "Error al encolar impresión", MessageBoxButton.OK, MessageBoxImage.Error);
		}
		}

		[RelayCommand]
		private void CopiarCodigo(string codigo)
		{
			if (!string.IsNullOrWhiteSpace(codigo))
				Clipboard.SetText(codigo);
		}

		[RelayCommand]
		private void CopiarDescripcion(string descripcion)
		{
			if (!string.IsNullOrWhiteSpace(descripcion))
				Clipboard.SetText(descripcion);
		}

		[RelayCommand]
		private void CopiarLote(string lote)
		{
			if (!string.IsNullOrWhiteSpace(lote))
				Clipboard.SetText(lote);
		}

		[RelayCommand]
		private void CopiarFechaCaducidad(DateTime? fechaCaducidad)
		{
			if (fechaCaducidad.HasValue)
				Clipboard.SetText(fechaCaducidad.Value.ToString("dd/MM/yyyy"));
		}

		[RelayCommand]
		private async Task RefrescarAsync()
		{
			try
			{
				// Guardar el estado de expansión antes de refrescar
				GuardarEstadosExpansion();
				
				// Ejecutar la búsqueda actual según el modo activo
				if (IsArticleMode)
				{
					await BuscarPorArticuloAsync();
				}
				else if (IsLocationMode)
				{
					await BuscarPorUbicacionAsync();
				}
				
				// Pequeño delay para asegurar que la UI se actualice
				await Task.Delay(50);
				// Restaurar el estado de expansión después de refrescar
				RestaurarEstadosExpansion();
			}
			catch (Exception ex)
			{
				MostrarError("Error al refrescar", ex);
			}
		}


		#endregion

		#region Initialization & Data Loading
		private async Task InitializeAsync()
		{
			try
			{
				var empresa = SessionManager.EmpresaSeleccionada!.Value;
				var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
				var desdeLogin = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();


				var resultado = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa, centro, desdeLogin);


				AlmacenesCombo.Clear();

				// Añadir opción "Todas"
				AlmacenesCombo.Add(new AlmacenDto
				{
					CodigoAlmacen = "Todas",
					NombreAlmacen = "Todas",
					CodigoEmpresa = empresa
				});

				foreach (var a in resultado)
					AlmacenesCombo.Add(a);

				// No seleccionar nada por defecto - que el usuario elija
				AlmacenSeleccionadoCombo = null;
				
				// 🔷 NUEVO: Inicializar la vista filtrable después de cargar los datos
				// 🔷 NUEVO: Crear UNA SOLA vista filtrable que funcione para ambos modos
				AlmacenesComboArticleView = CollectionViewSource.GetDefaultView(AlmacenesCombo);
				AlmacenesComboArticleView.Filter = FiltraAlmacenesCombo;
				
				AlmacenesComboLocationView = AlmacenesComboArticleView; // Usar la misma vista
				
				OnPropertyChanged(nameof(AlmacenesComboArticleView));
				OnPropertyChanged(nameof(AlmacenesComboLocationView));
			}
			catch (Exception ex)
			{
				MostrarError("Error cargando almacenes", ex);
			}
		}

		private async Task LoadUbicacionesAsync(string codigoAlmacen)
		{
			Ubicaciones.Clear();

			if (string.IsNullOrWhiteSpace(codigoAlmacen) || codigoAlmacen == TODAS)
			{
				FiltroUbicacion = string.Empty;
				return;
			}

			try
			{
				short codigoEmpresa = SessionManager.EmpresaSeleccionada ?? 0;
				bool soloConStock = FiltrarUbicacionesConStock;

				var lista = await _stockService.ObtenerUbicacionesAsync(codigoAlmacen, codigoEmpresa, soloConStock);

				if (lista.Count == 0)
				{
					const string SIN_STOCK = "SIN STOCK";
					Ubicaciones.Add(SIN_STOCK);
					FiltroUbicacion = SIN_STOCK;
					return;
				}

				// 🔷 NUEVO: Solo una opción genérica para ambos modos
				Ubicaciones.Add(TODO_ALMACEN);
				
				// 🔷 CORREGIDO: Solo añadir "Sin ubicación" en modo ubicación
				if (!IsArticleMode)
				{
					// En modo ubicación: "Sin ubicación"
					Ubicaciones.Add(SIN_UBICACION);
				}

				// �� SIMPLIFICADO: Solo añadir ubicaciones con valor (sin duplicados)
				var ubicacionesConValor = lista
					.Where(u => !string.IsNullOrEmpty(u.Ubicacion))
					.Select(u => u.Ubicacion)
					.Distinct()
					.OrderBy(u => u);

				foreach (var ubic in ubicacionesConValor)
				{
					Ubicaciones.Add(ubic);
				}

			// 🔷 NUEVO: No seleccionar nada por defecto
			FiltroUbicacion = "";
			FiltroUbicaciones = "";
			FiltroUbicacionesUbicacion = "";
			
			// 🔷 CORREGIDO: Crear vistas solo para el modo activo
			if (IsArticleMode)
			{
				UbicacionesView = CollectionViewSource.GetDefaultView(Ubicaciones);
				UbicacionesView.Filter = FiltraUbicaciones;
				OnPropertyChanged(nameof(UbicacionesView));
			}
			else if (IsLocationMode)
			{
				UbicacionesUbicacionView = CollectionViewSource.GetDefaultView(Ubicaciones);
				UbicacionesUbicacionView.Filter = FiltraUbicacionesUbicacion;
				OnPropertyChanged(nameof(UbicacionesUbicacionView));
			}
		}
		catch (Exception ex)
		{
			MostrarError("Error cargando ubicaciones", ex);
		}
	}


		#endregion

		#region Private Helpers
		private (string? almacenParam, string? ubicParam) BuildArticleParams()
		{
			string? almacenParam = AlmacenSeleccionadoCombo?.CodigoAlmacen == TODAS ? null : AlmacenSeleccionadoCombo?.CodigoAlmacen;
			string? ubicParam = null;

			if (almacenParam != null)
			{
				if (FiltroUbicacion == SIN_UBICACION) ubicParam = string.Empty;
				else if (FiltroUbicacion != TODAS) ubicParam = FiltroUbicacion;
			}

			return (almacenParam, ubicParam);
		}


		private void LlenarResultados(List<StockDto> lista, bool filterByPermissions)
		{
			List<StockDto> resultadosFiltrados;

			if (!filterByPermissions)
			{
				// Si no se filtran permisos, mostrar todos los resultados
				resultadosFiltrados = lista.ToList();
			}
			else
			{
				// 🔷 NUEVA LÓGICA: Obtener todos los almacenes autorizados (individuales + centro)
				var almacenesAutorizados = ObtenerAlmacenesAutorizados();

				var almacenesPermitidos = AlmacenSeleccionado == TODAS
					? almacenesAutorizados
					: new List<string> { AlmacenSeleccionado };

				resultadosFiltrados = lista
					.Where(s => almacenesPermitidos.Contains(s.CodigoAlmacen))
					.ToList();
			}

			LlenarResultadosSegunModo(resultadosFiltrados);
		}

		private List<string> ObtenerAlmacenesAutorizados()
		{
			var almacenesIndividuales = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();
			var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";

			// Si no hay almacenes individuales, usar solo los del centro
			if (!almacenesIndividuales.Any())
			{
				// En este caso, los almacenes del centro ya están en AlmacenesCombo
				return AlmacenesCombo
					.Where(a => a.CodigoAlmacen != TODAS)
					.Select(a => a.CodigoAlmacen)
					.ToList();
			}

			// Si hay almacenes individuales, incluir también los del centro
			var almacenesDelCentro = AlmacenesCombo
				.Where(a => a.CodigoAlmacen != TODAS && a.EsDelCentro)
				.Select(a => a.CodigoAlmacen)
				.ToList();

			// Combinar almacenes individuales + almacenes del centro
			return almacenesIndividuales
				.Concat(almacenesDelCentro)
				.Distinct()
				.ToList();
		}

		private void LlenarResultadosSegunModo(List<StockDto> filtrada)
		{
			if (IsArticleMode)
			{
				// en modo artículo actualiza ArticuloMostrado y clear/fill ResultadosStock
				ArticuloMostrado = filtrada
					.FirstOrDefault()?.DescripcionArticulo
					?? string.Empty;

				ResultadosStock.Clear();
				filtrada.ForEach(x => ResultadosStock.Add(x));
			}
			else
			{
				// en modo ubicación no mostramos destacado y clear/fill la otra colección
				ArticuloMostrado = string.Empty;

				ResultadosStockPorUbicacion.Clear();
				filtrada.ForEach(x => ResultadosStockPorUbicacion.Add(x));
			}
			
			// Notificar cambios en las propiedades calculadas
			OnPropertyChanged(nameof(CanRefresh));
			OnPropertyChanged(nameof(CanClearFilters));
			OnPropertyChanged(nameof(CanExportExcel));
		}


		private void SwitchMode(bool resetFilters, bool setArticle)
		{
			if (resetFilters)
			{
				FiltroArticulo = string.Empty;
				FiltroUbicacion = string.Empty;
				FiltroPartida = string.Empty;
				AlmacenSeleccionado = TODAS;
				//ArticuloMostrado = string.Empty;
			}
			IsArticleMode = setArticle;
			IsLocationMode = !setArticle;
			OnPropertyChanged(nameof(ArticleFiltersVisibility));
			OnPropertyChanged(nameof(LocationFiltersVisibility));
		}

		private void MostrarError(string titulo, Exception ex)
		{
			MessageBox.Show(ex.Message, titulo, MessageBoxButton.OK, MessageBoxImage.Error);
		}

		private string ObtenerNombreEmpresaActual()
		{
			var code = SessionManager.EmpresaSeleccionada;
			var dto = SessionManager.UsuarioActual?.empresas
					 .FirstOrDefault(e => e.Codigo == code);
			return dto != null ? dto.Nombre : $"[{code}]";
		}

		private static string EscapeCsv(string campo)
		{
			if (campo.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
				return $"\"{campo.Replace("\"", "\"\"")}\"";
			return campo;
		}
		#endregion



		private bool FiltroStock(object obj)
		{
			if (obj is not StockDto stock) return false;
			if (string.IsNullOrWhiteSpace(FiltroBusqueda)) return true;

			return (stock.CodigoArticulo?.Contains(FiltroBusqueda, StringComparison.OrdinalIgnoreCase) ?? false)
				|| (stock.DescripcionArticulo?.Contains(FiltroBusqueda, StringComparison.OrdinalIgnoreCase) ?? false);
		}

	
	// Métodos para filtrado de almacenes (modo artículo)
	private bool FiltraAlmacenesCombo(object obj)
	{
		if (obj is not AlmacenDto almacen) return false;
		if (string.IsNullOrEmpty(FiltroAlmacenesCombo)) return true;
		
		return System.Globalization.CultureInfo.CurrentCulture.CompareInfo
			.IndexOf(almacen.DescripcionCombo, FiltroAlmacenesCombo, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
	}
	
	// Método para manejar cambios en el filtro de almacenes (modo artículo)
	partial void OnFiltroAlmacenesComboChanged(string value)
	{
		AlmacenesComboArticleView?.Refresh();
	}
	
	// Método para manejar cambios en el filtro de almacenes (modo ubicación)
	partial void OnFiltroAlmacenesComboLocationChanged(string value)
	{
		// Usar el mismo filtro que el modo artículo
		FiltroAlmacenesCombo = value;
		AlmacenesComboArticleView?.Refresh();
	}
	
	// Comandos para controlar dropdown (ambos modos usan el mismo)
	[RelayCommand]
	private void AbrirDropDownAlmacenes()
	{
		FiltroAlmacenesCombo = ""; // Limpiar filtro para mostrar todo
		IsDropDownOpenAlmacenes = true;
	}
	
	[RelayCommand]
	private void CerrarDropDownAlmacenes()
	{
		IsDropDownOpenAlmacenes = false;
	}
	
	[RelayCommand]
	private void AbrirDropDownAlmacenesLocation()
	{
		FiltroAlmacenesCombo = ""; // Limpiar filtro para mostrar todo
		IsDropDownOpenAlmacenesLocation = true;
	}
	
	[RelayCommand]
	private void CerrarDropDownAlmacenesLocation()
	{
		IsDropDownOpenAlmacenesLocation = false;
	}
	
	// Métodos para filtrado de ubicaciones (modo artículo)
	private bool FiltraUbicaciones(object obj)
	{
		if (obj is not string ubicacion) return false;
		if (string.IsNullOrEmpty(FiltroUbicaciones)) return true;
		
		return System.Globalization.CultureInfo.CurrentCulture.CompareInfo
			.IndexOf(ubicacion, FiltroUbicaciones, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
	}
	
	// Método para manejar cambios en el filtro de ubicaciones (modo artículo)
	partial void OnFiltroUbicacionesChanged(string value)
	{
		UbicacionesView?.Refresh();
	}
	
	// Comandos para controlar dropdown de ubicaciones (modo artículo)
	[RelayCommand]
	private void AbrirDropDownUbicaciones()
	{
		IsDropDownOpenUbicaciones = true;
	}
	
	// Métodos para filtrado de ubicaciones (modo ubicación)
	private bool FiltraUbicacionesUbicacion(object obj)
	{
		if (obj is not string ubicacion) return false;
		if (string.IsNullOrEmpty(FiltroUbicacionesUbicacion)) return true;
		
		return System.Globalization.CultureInfo.CurrentCulture.CompareInfo
			.IndexOf(ubicacion, FiltroUbicacionesUbicacion, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
	}
	
	// Método para manejar cambios en el filtro de ubicaciones (modo ubicación)
	partial void OnFiltroUbicacionesUbicacionChanged(string value)
	{
		UbicacionesUbicacionView?.Refresh();
	}
	
	// Comandos para controlar dropdown de ubicaciones (modo ubicación)
	[RelayCommand]
	private void AbrirDropDownUbicacionesUbicacion()
	{
		IsDropDownOpenUbicacionesUbicacion = true;
	}
	
	// Comandos para limpiar selección cuando se borra el texto
	[RelayCommand]
	private void LimpiarSeleccionAlmacenes()
	{
		AlmacenSeleccionadoCombo = null;
		FiltroAlmacenesCombo = ""; // Limpiar también el filtro de texto
	}
	
	[RelayCommand]
	private void LimpiarSeleccionAlmacenesLocation()
	{
		AlmacenSeleccionadoCombo = null;
		FiltroAlmacenesComboLocation = ""; // Limpiar también el filtro de texto
	}
	
	[RelayCommand]
	private void LimpiarSeleccionUbicaciones()
	{
		FiltroUbicacion = "";
		FiltroUbicaciones = ""; // Limpiar también el filtro de texto
	}
	
	[RelayCommand]
	private void LimpiarSeleccionUbicacionesUbicacion()
	{
		FiltroUbicacion = "";
		FiltroUbicacionesUbicacion = ""; // Limpiar también el filtro de texto
	}
	
	// 🔷 NUEVO: Métodos para manejar estados de expansión (como TraspasosStockViewModel)
	private void GuardarEstadosExpansion()
	{
		_estadosExpansion.Clear();
		foreach (var grupo in ArticulosConUbicaciones)
		{
			var clave = $"{grupo.CodigoArticulo}_{grupo.DescripcionArticulo}";
			_estadosExpansion[clave] = grupo.IsExpanded;
		}
	}

	private void RestaurarEstadosExpansion()
	{
		foreach (var grupo in ArticulosConUbicaciones)
		{
			var clave = $"{grupo.CodigoArticulo}_{grupo.DescripcionArticulo}";
			if (_estadosExpansion.ContainsKey(clave))
			{
				grupo.IsExpanded = _estadosExpansion[clave];
			}
		}
		
		// Forzar la actualización de la UI
		OnPropertyChanged(nameof(ArticulosConUbicaciones));
	}
}
}



