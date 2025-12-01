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
		private List<StockDto> _resultadosArticuloBase = new();
		private List<StockDisponibleDto> _stockDisponibleArticuloBase = new();

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

			// ② Inicializa todas las colecciones
			ResultadosStock = new ObservableCollection<StockDto>();
			ResultadosStockPorUbicacion = new ObservableCollection<StockDto>();
			ResultadosStockPorPalet = new ObservableCollection<StockDto>();
			
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
	public ObservableCollection<StockDto> ResultadosStockPorPalet { get; }
	public ObservableCollection<ArticuloResumenDto> ArticulosUnicos { get; } = new();
	public ObservableCollection<StockDto> StockFiltrado { get; } = new();
	public ObservableCollection<AlmacenDto> AlmacenesCombo { get; } = new();
	public ObservableCollection<string> PartidasDisponibles { get; } = new();
	
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
		private bool isPaletMode = false;

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

	// Propiedades para filtrado de palet (modo palet)
	[ObservableProperty]
	private string filtroArticuloPalet = "";

	[ObservableProperty]
	private string filtroCodigoPalet = "";

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
		public Visibility PaletFiltersVisibility => IsPaletMode ? Visibility.Visible : Visibility.Collapsed;

		/// <summary>
		/// Determina si se deben mostrar los resultados de ubicación/palet (comparten el mismo ListView)
		/// </summary>
		public bool MostrarResultadosUbicacion => IsLocationMode || IsPaletMode;

		/// <summary>
		/// Determina si el TextBox de código de palet está habilitado (requiere almacén válido seleccionado)
		/// </summary>
		public bool CodigoPaletHabilitado => AlmacenSeleccionadoCombo != null && AlmacenSeleccionadoCombo.CodigoAlmacen != TODAS;
		
		/// <summary>
		/// Devuelve la colección de resultados activa según el modo
		/// </summary>
		public ObservableCollection<StockDto> ResultadosActivos => 
			IsLocationMode ? ResultadosStockPorUbicacion :
			IsPaletMode ? ResultadosStockPorPalet :
			ResultadosStock;

		public Visibility ArticulosUnicosVisibility =>
	_busquedaPorDescripcion && ArticulosUnicos.Count > 1
		? Visibility.Visible
		: Visibility.Collapsed;

		public Visibility ListViewVisibility =>
			(!_busquedaPorDescripcion || StockFiltrado.Any())
				? Visibility.Visible
				: Visibility.Collapsed;

		public IRelayCommand BuscarCommand =>
			IsArticleMode ? BuscarPorArticuloCommand :
			IsLocationMode ? BuscarPorUbicacionCommand :
			BuscarPorPaletCommand;

		/// <summary>
		/// Determina si el botón de refrescar debe estar habilitado
		/// </summary>
		public bool CanRefresh =>
			(IsArticleMode && StockFiltrado.Any()) ||
			(IsLocationMode && ResultadosStockPorUbicacion.Any()) ||
			(IsPaletMode && ResultadosStockPorPalet.Any());

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
								(AlmacenSeleccionadoCombo?.CodigoAlmacen != "Todas"))) ||
			(IsPaletMode && (!string.IsNullOrWhiteSpace(FiltroArticuloPalet) || 
							 !string.IsNullOrWhiteSpace(FiltroCodigoPalet) ||
							 !string.IsNullOrWhiteSpace(FiltroUbicacion) ||
							 (AlmacenSeleccionadoCombo?.CodigoAlmacen != "Todas")));

		/// <summary>
		/// Determina si el botón de exportar Excel debe estar habilitado
		/// </summary>
		public bool CanExportExcel =>
			(IsArticleMode && StockFiltrado.Any()) ||
			(IsLocationMode && ResultadosStockPorUbicacion.Any()) ||
			(IsPaletMode && ResultadosStockPorPalet.Any());

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
			if (newValue is null)
			{
				// Notificar cambio en CodigoPaletHabilitado cuando se deselecciona
				OnPropertyChanged(nameof(CodigoPaletHabilitado));
				return;
			}

			AlmacenSeleccionado = newValue.CodigoAlmacen;
			OnPropertyChanged(nameof(CanClearFilters));
			OnPropertyChanged(nameof(CanRefresh));
			OnPropertyChanged(nameof(CanExportExcel));
			OnPropertyChanged(nameof(CodigoPaletHabilitado)); // 🔷 NUEVO: Notificar cambio en habilitación del código de palet
			_ = LoadUbicacionesAsync(newValue.CodigoAlmacen);
		}







		partial void OnFiltroPartidaChanged(string oldValue, string newValue)
		{
			OnPropertyChanged(nameof(CanClearFilters));
			AplicarFiltroPartidaArticulo();
		}

		partial void OnFiltroUbicacionChanged(string oldValue, string newValue)
		{
			OnPropertyChanged(nameof(CanClearFilters));
		}

		partial void OnFiltroArticuloPaletChanged(string oldValue, string newValue)
		{
			OnPropertyChanged(nameof(CanClearFilters));
		}

		partial void OnFiltroCodigoPaletChanged(string oldValue, string newValue)
		{
			OnPropertyChanged(nameof(CanClearFilters));
		}

		partial void OnIsArticleModeChanged(bool oldValue, bool newValue)
		{
			if (newValue)
			{
				IsPaletMode = false;
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
			OnPropertyChanged(nameof(PaletFiltersVisibility));
			OnPropertyChanged(nameof(MostrarResultadosUbicacion));
			OnPropertyChanged(nameof(ResultadosActivos));
			if (newValue)
				AplicarFiltroPartidaArticulo();
			else
				PartidasDisponibles.Clear();
		}


		partial void OnIsLocationModeChanged(bool oldValue, bool newValue)
		{
			if (newValue)
			{
				IsPaletMode = false;
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
			else
			{
				PartidasDisponibles.Clear();
			}
			OnPropertyChanged(nameof(BuscarCommand));
			OnPropertyChanged(nameof(CanRefresh));
			OnPropertyChanged(nameof(CanClearFilters));
			OnPropertyChanged(nameof(CanExportExcel));
			OnPropertyChanged(nameof(PaletFiltersVisibility));
			OnPropertyChanged(nameof(MostrarResultadosUbicacion));
			OnPropertyChanged(nameof(ResultadosActivos));
		}

		partial void OnIsPaletModeChanged(bool oldValue, bool newValue)
		{
			if (newValue)
			{
				// Establecer los otros modos a false antes de limpiar
				IsArticleMode = false;
				IsLocationMode = false;
				
				// Limpiar filtros de otros modos
				FiltroArticulo = "";
				FiltroPartida = "";
				FiltroUbicacion = "";
				FiltroBusqueda = "";
				FiltroUbicacionesUbicacion = "";
				FiltroUbicaciones = "";
				FiltroAlmacenesCombo = "";
				FiltroAlmacenesComboLocation = "";
				
				// Limpiar selecciones
				AlmacenSeleccionadoCombo = null;
				
				// 🔷 NO limpiar resultados - conservarlos como en los otros modos
			}
			OnPropertyChanged(nameof(BuscarCommand));
			OnPropertyChanged(nameof(CanRefresh));
			OnPropertyChanged(nameof(CanClearFilters));
			OnPropertyChanged(nameof(CanExportExcel));
			OnPropertyChanged(nameof(ArticleFiltersVisibility));
			OnPropertyChanged(nameof(LocationFiltersVisibility));
			OnPropertyChanged(nameof(PaletFiltersVisibility));
			OnPropertyChanged(nameof(MostrarResultadosUbicacion));
			OnPropertyChanged(nameof(ResultadosActivos));
			OnPropertyChanged(nameof(CodigoPaletHabilitado)); // 🔷 NUEVO: Notificar cambio en habilitación del código de palet
			if (newValue)
			{
				PartidasDisponibles.Clear();
			}
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
			else if (IsPaletMode)
			{
				FiltroArticuloPalet = string.Empty;
				FiltroCodigoPalet = string.Empty;
				FiltroUbicacion = string.Empty;
				AlmacenSeleccionado = TODAS;
				AlmacenSeleccionadoCombo = AlmacenesCombo.FirstOrDefault(a => a.CodigoAlmacen == TODAS);
			}
			
			// Limpiar resultados al limpiar filtros
			ResultadosStock.Clear();
			ResultadosStockPorUbicacion.Clear();
			ResultadosStockPorPalet.Clear();
			StockFiltrado.Clear();
			ArticulosConUbicaciones.Clear();
			PartidasDisponibles.Clear();
			_resultadosArticuloBase.Clear();
			_stockDisponibleArticuloBase.Clear();
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
			FiltroPartida = string.Empty;
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
						codigoAlmacen: almacenParam,
						codigoUbicacion: ubicParam,
						descripcion: FiltroArticulo
					);
				}

				// 3) 🔷 NUEVA LÓGICA: Filtrar por permisos de almacén (individuales + centro)
				var almacenesAutorizados = ObtenerAlmacenesAutorizados();
				lista = lista.Where(x => almacenesAutorizados.Contains(x.CodigoAlmacen)).ToList();

				_resultadosArticuloBase = lista.ToList();

				// 🔷 ACTUALIZADO: El backend ya devuelve IsBloqueadoCalidad correctamente por ubicación
				// No necesitamos consultar bloqueos manualmente, usamos lo que viene del backend
				// Convertir StockDto a StockDisponibleDto para compatibilidad
				var stockDisponible = _resultadosArticuloBase.Select(s => 
				{
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
						// 🔷 ACTUALIZADO: Usar información de bloqueo que viene del backend (ya verificado por ubicación)
						IsBloqueadoCalidad = s.IsBloqueadoCalidad,
						MotivoBloqueoCalidad = s.MotivoBloqueoCalidad,
						FechaBloqueoCalidad = s.FechaBloqueoCalidad,
						TipoBloqueoCalidad = s.TipoBloqueoCalidad ?? "TOTAL", // 🔷 NUEVO
						// 🔷 NUEVO: Fecha del último traspaso
						FechaUltimoTraspaso = s.FechaUltimoTraspaso
					};
				}).ToList();
				_stockDisponibleArticuloBase = stockDisponible;

				ActualizarPartidasDisponibles();

				AplicarFiltroPartidaArticulo();

			}
			catch (Exception ex)
			{
				new WarningDialog("Error al consultar por artículo", ex.Message, "\uE783").ShowDialog();
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


				// 🔷 ACTUALIZADO: El backend ya devuelve IsBloqueadoCalidad correctamente por ubicación
				// No necesitamos consultar bloqueos manualmente, el backend ya lo hace

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
		private async Task BuscarPorPaletAsync()
		{
			try
			{
			// Validación: Al menos un filtro debe estar presente
			bool tieneArticulo = !string.IsNullOrWhiteSpace(FiltroArticuloPalet);
			bool tieneAlmacen = AlmacenSeleccionadoCombo != null && AlmacenSeleccionadoCombo.CodigoAlmacen != TODAS;
			// 🔷 MODIFICADO: Validar que el código de palet tenga al menos 3 caracteres Y que haya un almacén válido
			bool tieneCodigoPalet = !string.IsNullOrWhiteSpace(FiltroCodigoPalet) && 
			                        FiltroCodigoPalet.Trim().Length >= 3 && 
			                        tieneAlmacen; // Requiere almacén válido

			// 🔷 NUEVO: Si hay código de palet pero no hay almacén válido, mostrar error
			if (!string.IsNullOrWhiteSpace(FiltroCodigoPalet) && !tieneAlmacen)
			{
				var advertencia = new WarningDialog(
					"Buscar por palet",
					"Para buscar por código de palet, debes seleccionar un almacén válido (no 'Todas').",
					"\uE814"
				);
				if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
					advertencia.Owner = Application.Current.MainWindow;
				advertencia.ShowDialog();
				return;
			}

			if (!tieneArticulo && !tieneAlmacen && !tieneCodigoPalet)
			{
				var mensaje = string.IsNullOrWhiteSpace(FiltroCodigoPalet) 
					? "Debes introducir al menos un filtro: código de artículo, almacén o código de palet."
					: "El código de palet debe tener al menos 3 caracteres para realizar la búsqueda.";
				
				var advertencia = new WarningDialog(
					"Buscar por palet",
					mensaje,
					"\uE814"
				);
				if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
					advertencia.Owner = Application.Current.MainWindow;
				advertencia.ShowDialog();
				return;
			}

			// Limpiar resultados previos del modo palet
			ResultadosStockPorPalet.Clear();
			StockFiltrado.Clear();
			ArticulosConUbicaciones.Clear();

			// Construir parámetros para la búsqueda
			string? codigoArticuloParam = tieneArticulo ? FiltroArticuloPalet : null;
			string? codigoAlmacenParam = tieneAlmacen ? AlmacenSeleccionadoCombo?.CodigoAlmacen : null;

			// 🔷 NUEVO: Si se busca por código de palet específico, obtener todas las líneas del palet
			if (tieneCodigoPalet && tieneAlmacen && !string.IsNullOrWhiteSpace(codigoAlmacenParam))
			{
				try
				{
					var paletService = new PaletService();
					
					// Buscar el palet por código
					var palets = await paletService.ObtenerPaletsAsync(
						SessionManager.EmpresaSeleccionada!.Value,
						codigo: FiltroCodigoPalet.Trim(),
						almacen: codigoAlmacenParam
					);
					
					if (palets.Any())
					{
						var palet = palets.First();
						
						// Obtener todas las líneas del palet
						var lineasPalet = await paletService.ObtenerLineasAsync(palet.Id);
						
						if (lineasPalet.Any())
						{
							// Filtrar por ubicación si está especificado
							if (!string.IsNullOrWhiteSpace(FiltroUbicacion) && FiltroUbicacion != TODO_ALMACEN)
							{
								if (FiltroUbicacion == SIN_UBICACION)
								{
									lineasPalet = lineasPalet.Where(l => string.IsNullOrWhiteSpace(l.Ubicacion)).ToList();
								}
								else
								{
									lineasPalet = lineasPalet.Where(l => l.Ubicacion == FiltroUbicacion).ToList();
								}
							}
							
							// Filtrar por artículo si está especificado
							if (tieneArticulo && !string.IsNullOrWhiteSpace(FiltroArticuloPalet))
							{
								lineasPalet = lineasPalet
									.Where(l => l.CodigoArticulo.Contains(FiltroArticuloPalet, StringComparison.OrdinalIgnoreCase) ||
											   (l.DescripcionArticulo?.Contains(FiltroArticuloPalet, StringComparison.OrdinalIgnoreCase) == true))
									.ToList();
							}
							
							if (lineasPalet.Any())
							{
								// Obtener información adicional de artículos para CodigoAlternativo
								var codigosArticulos = lineasPalet.Select(l => l.CodigoArticulo).Distinct().ToList();
								var articulosInfo = new Dictionary<string, string>();
								
								// Intentar obtener información de artículos desde el servicio de stock
								foreach (var codigoArticulo in codigosArticulos)
								{
									try
									{
										var stockInfo = await _stockService.ObtenerPorArticuloAsync(
											SessionManager.EmpresaSeleccionada!.Value,
											codigoArticulo: codigoArticulo,
											codigoAlmacen: codigoAlmacenParam,
											codigoUbicacion: null,
											partida: null,
											descripcion: null
										);
										
										if (stockInfo != null && stockInfo.Any())
										{
											var primerStock = stockInfo.First();
											articulosInfo[codigoArticulo] = primerStock.CodigoAlternativo ?? "";
										}
									}
									catch
									{
										// Si falla, dejar vacío
										articulosInfo[codigoArticulo] = "";
									}
								}
								
								// Convertir las líneas del palet a StockDto
								var listaPaletEspecifico = lineasPalet.Select(linea =>
								{
									articulosInfo.TryGetValue(linea.CodigoArticulo, out var codigoAlternativo);
									
									return new StockDto
									{
										CodigoEmpresa = linea.CodigoEmpresa,
										CodigoArticulo = linea.CodigoArticulo,
										DescripcionArticulo = linea.DescripcionArticulo ?? "",
										CodigoAlternativo = codigoAlternativo ?? "",
										CodigoAlmacen = linea.CodigoAlmacen,
										Ubicacion = linea.Ubicacion ?? "",
										Partida = linea.Lote ?? "",
										FechaCaducidad = linea.FechaCaducidad,
										UnidadSaldo = linea.Cantidad,
										Palets = new List<PaletDetalleDto>
										{
											new PaletDetalleDto
											{
												PaletId = palet.Id,
												CodigoPalet = palet.Codigo,
												EstadoPalet = palet.Estado,
												Cantidad = linea.Cantidad,
												Ubicacion = linea.Ubicacion ?? "",
												Partida = linea.Lote ?? ""
											}
										},
										CodigoPalet = palet.Codigo,
										EstadoPalet = palet.Estado,
										IsBloqueadoCalidad = linea.IsBloqueadoCalidad,
										MotivoBloqueoCalidad = linea.MotivoBloqueoCalidad,
										FechaBloqueoCalidad = linea.FechaBloqueoCalidad,
										TipoBloqueoCalidad = linea.TipoBloqueoCalidad ?? "TOTAL"
									};
								}).ToList();
								
								// 🔷 CORREGIDO: Buscar el traspaso MÁS RECIENTE para cada artículo + partida en el palet
								// Agrupar por artículo + partida y buscar el traspaso más reciente de cada grupo
								var gruposArticuloPartida = listaPaletEspecifico
									.GroupBy(s => new { s.CodigoArticulo, s.Partida })
									.ToList();
								
								var fechasTraspaso = new Dictionary<(string CodigoArticulo, string Partida), DateTime?>();
								
								try
								{
									var traspasosService = new TraspasosService();
									
									// Buscar traspasos por código de palet para obtener los más recientes
									var codigoPalet = palet.Codigo;
									var traspasosPalet = await traspasosService.ObtenerTraspasosFiltradosAsync(
										estado: null,
										codigoPalet: codigoPalet,
										almacenOrigen: null,
										almacenDestino: null,
										fechaInicioDesde: DateTime.MinValue,
										fechaInicioHasta: DateTime.MaxValue
									);
									
									// Para cada grupo de artículo + partida, encontrar el traspaso más reciente
									foreach (var grupo in gruposArticuloPartida)
									{
										var traspasosRelevantes = traspasosPalet
											.Where(t => t.CodigoArticulo == grupo.Key.CodigoArticulo &&
													   (string.IsNullOrWhiteSpace(grupo.Key.Partida) ? string.IsNullOrWhiteSpace(t.Partida) : t.Partida == grupo.Key.Partida) &&
													   t.FechaFinalizacion.HasValue)
											.OrderByDescending(t => t.FechaFinalizacion)
											.ToList();
										
										if (traspasosRelevantes.Any())
										{
											fechasTraspaso[(grupo.Key.CodigoArticulo, grupo.Key.Partida)] = traspasosRelevantes.First().FechaFinalizacion;
										}
									}
								}
								catch
								{
									// Si falla, continuar sin fechas
								}
								
								// Asignar FechaUltimoTraspaso a cada item
								foreach (var stockDto in listaPaletEspecifico)
								{
									var key = (stockDto.CodigoArticulo, stockDto.Partida);
									if (fechasTraspaso.TryGetValue(key, out var fechaTraspaso))
									{
										stockDto.FechaUltimoTraspaso = fechaTraspaso;
									}
								}
								
								// Calcular totales globales y por almacén
								var totalesGlobalesPalet = listaPaletEspecifico
									.GroupBy(s => new { s.CodigoArticulo, s.Partida })
									.ToDictionary(
										g => (g.Key.CodigoArticulo, g.Key.Partida),
										g => g.Sum(x => x.UnidadSaldo)
									);
								
								var totalesPorAlmacenPalet = listaPaletEspecifico
									.GroupBy(s => new { s.CodigoArticulo, s.Partida, s.CodigoAlmacen })
									.ToDictionary(
										g => (g.Key.CodigoArticulo, g.Key.Partida, g.Key.CodigoAlmacen),
										g => g.Sum(x => x.UnidadSaldo)
									);
								
								// 🔷 NUEVO: Calcular totales por ubicación (artículo + partida + ubicación)
								var totalesPorUbicacionPalet = listaPaletEspecifico
									.GroupBy(s => new { s.CodigoArticulo, s.Partida, s.Ubicacion })
									.ToDictionary(
										g => (g.Key.CodigoArticulo, g.Key.Partida ?? "", g.Key.Ubicacion ?? ""),
										g => g.Sum(x => x.UnidadSaldo)
									);
								
								// Asignar totales a cada item
								foreach (var item in listaPaletEspecifico)
								{
									if (totalesGlobalesPalet.TryGetValue((item.CodigoArticulo, item.Partida), out var totalGlobal))
									{
										item.TotalArticuloGlobal = totalGlobal;
									}
									
									if (totalesPorAlmacenPalet.TryGetValue((item.CodigoArticulo, item.Partida, item.CodigoAlmacen), out var totalAlmacen))
									{
										item.TotalArticuloAlmacen = totalAlmacen;
									}
									
									// 🔷 MODIFICADO: Usar la cantidad total del artículo en esa ubicación específica
									var ubicacionKey = (item.CodigoArticulo, item.Partida ?? "", item.Ubicacion ?? "");
									if (totalesPorUbicacionPalet.TryGetValue(ubicacionKey, out var totalUbicacion))
									{
										item.UnidadSaldo = totalUbicacion;
									}
									else if (item.TotalArticuloAlmacen.HasValue)
									{
										// Si no hay total por ubicación, usar el del almacén
										item.UnidadSaldo = item.TotalArticuloAlmacen.Value;
									}
									else if (item.TotalArticuloGlobal.HasValue)
									{
										// Si no hay total por almacén, usar el global
										item.UnidadSaldo = item.TotalArticuloGlobal.Value;
									}
								}
								
								// 🔷 NUEVO: Ordenar primero por código de palet, luego por artículo
								var listaOrdenada = listaPaletEspecifico
									.OrderBy(x => x.CodigoPalet ?? "")
									.ThenBy(x => x.CodigoArticulo)
									.ThenBy(x => x.Partida)
									.ToList();
								
								// Llenar resultados
								LlenarResultados(listaOrdenada, filterByPermissions: false);
								OnPropertyChanged(nameof(CanRefresh));
								OnPropertyChanged(nameof(CanExportExcel));
								return;
							}
						}
					}
				}
				catch (Exception ex)
				{
					// Si falla la búsqueda por palet específico, continuar con la lógica normal
					// No mostrar error aquí, dejar que continúe con la búsqueda normal
				}
			}
				
			// 🔷 NUEVO: Determinar parámetro de ubicación (similar a modo ubicación)
				string? ubicacionParam = null;
				if (!string.IsNullOrWhiteSpace(FiltroUbicacion))
				{
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
				}

				List<StockDisponibleDto> stockDisponible;
				
				// Si solo hay almacén (sin artículo), usar el endpoint de ubicación
				if (!tieneArticulo && tieneAlmacen && !string.IsNullOrWhiteSpace(codigoAlmacenParam))
				{
					// Usar el endpoint de ubicación que permite buscar por almacén directamente
					var stockPorUbicacion = await _stockService.ObtenerPorUbicacionAsync(
						SessionManager.EmpresaSeleccionada!.Value,
						codigoAlmacenParam,
						codigoUbicacion: ubicacionParam // 🔷 NUEVO: Usar el parámetro de ubicación
					);
					
					// 🔷 CORREGIDO: Expandir cada StockDto en múltiples StockDisponibleDto (uno por palet)
					// El endpoint ubicacion puede devolver una entrada con múltiples palets en la lista Palets
					// pero con UnidadSaldo que es la suma total. Necesitamos expandir cada palet individualmente.
					stockDisponible = new List<StockDisponibleDto>();
					
					foreach (var s in stockPorUbicacion)
					{
						// Si tiene palets, crear una entrada por cada palet
						if (s.Palets != null && s.Palets.Any())
						{
							foreach (var palet in s.Palets)
							{
								stockDisponible.Add(new StockDisponibleDto
								{
									CodigoEmpresa = (short)s.CodigoEmpresa,
									CodigoArticulo = s.CodigoArticulo,
									DescripcionArticulo = s.DescripcionArticulo,
									CodigoAlternativo = s.CodigoAlternativo,
									CodigoAlmacen = s.CodigoAlmacen,
									Ubicacion = palet.Ubicacion ?? s.Ubicacion, // Usar ubicación del palet si está disponible
									Partida = palet.Partida ?? s.Partida, // Usar partida del palet si está disponible
									FechaCaducidad = s.FechaCaducidad,
									UnidadSaldo = palet.Cantidad, // 🔷 TEMPORAL: Usar cantidad del palet, se actualizará después
									Reservado = 0,
									Disponible = palet.Cantidad, // 🔷 TEMPORAL: Usar cantidad del palet, se actualizará después
									Palets = new List<PaletDetalleDto> { palet }, // Solo este palet en la lista
									TotalArticuloGlobal = s.TotalArticuloGlobal, // Preservar totales
									TotalArticuloAlmacen = s.TotalArticuloAlmacen, // Preservar totales
									TipoStock = "Paletizado",
									CodigoPalet = palet.CodigoPalet,
									EstadoPalet = palet.EstadoPalet,
									PaletId = palet.PaletId,
									// 🔷 NUEVO: Fecha del último traspaso
									FechaUltimoTraspaso = s.FechaUltimoTraspaso
								});
							}
						}
						else
						{
							// Si no tiene palets, crear una entrada suelta
							stockDisponible.Add(new StockDisponibleDto
							{
								CodigoEmpresa = (short)s.CodigoEmpresa,
								CodigoArticulo = s.CodigoArticulo,
								DescripcionArticulo = s.DescripcionArticulo,
								CodigoAlternativo = s.CodigoAlternativo,
								CodigoAlmacen = s.CodigoAlmacen,
								Ubicacion = s.Ubicacion,
								Partida = s.Partida,
								FechaCaducidad = s.FechaCaducidad,
								UnidadSaldo = s.UnidadSaldo,
								Reservado = 0,
								Disponible = s.UnidadSaldo,
								Palets = new List<PaletDetalleDto>(),
								TotalArticuloGlobal = s.TotalArticuloGlobal,
								TotalArticuloAlmacen = s.TotalArticuloAlmacen,
								TipoStock = "Suelto",
								CodigoPalet = null,
								EstadoPalet = null,
								PaletId = null,
								// 🔷 NUEVO: Fecha del último traspaso
								FechaUltimoTraspaso = s.FechaUltimoTraspaso
							});
						}
					}
					
					// 🔷 NUEVO: Calcular totales por ubicación y actualizar Disponible
					var totalesPorUbicacionUbicacion = stockDisponible
						.GroupBy(s => new { s.CodigoArticulo, s.Partida, s.Ubicacion })
						.ToDictionary(
							g => (g.Key.CodigoArticulo, g.Key.Partida ?? "", g.Key.Ubicacion ?? ""),
							g => g.Sum(x => 
							{
								// Sumar la cantidad del palet individual
								if (x.Palets != null && x.Palets.Any())
								{
									return x.Palets.Sum(p => p.Cantidad);
								}
								return x.Disponible;
							})
						);
					
					// Actualizar Disponible con el total por ubicación
					foreach (var item in stockDisponible)
					{
						var ubicacionKey = (item.CodigoArticulo, item.Partida ?? "", item.Ubicacion ?? "");
						if (totalesPorUbicacionUbicacion.TryGetValue(ubicacionKey, out var totalUbicacion))
						{
							item.Disponible = totalUbicacion;
							item.UnidadSaldo = totalUbicacion;
						}
					}
				}
				else
				{
					// Buscar stock disponible usando el endpoint articulo/disponible
					// Requiere código de artículo o descripción
					stockDisponible = await _stockService.ObtenerStockDisponibleAsync(
						codigoArticulo: codigoArticuloParam,
						descripcion: null
					);

					// Filtrar por almacén si está especificado
					if (tieneAlmacen && !string.IsNullOrWhiteSpace(codigoAlmacenParam))
					{
						stockDisponible = stockDisponible
							.Where(s => s.CodigoAlmacen == codigoAlmacenParam)
							.ToList();
					}
					
					// 🔷 NUEVO: Filtrar por ubicación si está especificado
					if (ubicacionParam != null)
					{
						if (ubicacionParam == string.Empty)
						{
							// Sin ubicación: filtrar ubicaciones vacías o null
							stockDisponible = stockDisponible
								.Where(s => string.IsNullOrWhiteSpace(s.Ubicacion))
								.ToList();
						}
						else
						{
							// Ubicación específica
							stockDisponible = stockDisponible
								.Where(s => s.Ubicacion == ubicacionParam)
								.ToList();
						}
					}
				}

				// Filtrar solo los que tienen stock paletizado
				// El endpoint articulo/disponible devuelve entradas con TipoStock = "Paletizado" cuando hay palets
				var stockConPalets = stockDisponible
					.Where(s => 
						// Tiene TipoStock = "Paletizado" (esto es lo que devuelve el API para stock paletizado)
						s.TipoStock == "Paletizado" ||
						// O tiene código de palet directo (por si acaso)
						!string.IsNullOrWhiteSpace(s.CodigoPalet) ||
						// O tiene palets en la lista (por compatibilidad con otros endpoints)
						(s.Palets != null && s.Palets.Any())
					)
					.ToList();

				// Filtrar por código de palet si está especificado
				var stockFiltradoPorPalet = stockConPalets;
				if (tieneCodigoPalet)
				{
					stockFiltradoPorPalet = stockConPalets
						.Where(s => 
							// Buscar en el campo CodigoPalet directo (el API lo devuelve así)
							(!string.IsNullOrWhiteSpace(s.CodigoPalet) && s.CodigoPalet.Contains(FiltroCodigoPalet, StringComparison.OrdinalIgnoreCase)) ||
							// O buscar en la lista de palets (por compatibilidad)
							(s.Palets?.Any(p => p.CodigoPalet?.Contains(FiltroCodigoPalet, StringComparison.OrdinalIgnoreCase) == true) == true)
						)
						.ToList();
				}

				// Filtrar por permisos de almacén
				var almacenesAutorizados = ObtenerAlmacenesAutorizados();
				stockFiltradoPorPalet = stockFiltradoPorPalet
					.Where(x => almacenesAutorizados.Contains(x.CodigoAlmacen))
					.ToList();

				// Calcular totales si no están disponibles (cuando viene del endpoint articulo/disponible)
				// Si ya vienen del endpoint ubicacion, los preservamos
				var todosLosArticulos = stockFiltradoPorPalet.Select(s => s.CodigoArticulo).Distinct().ToList();
				var todasLasPartidas = stockFiltradoPorPalet.Select(s => s.Partida).Distinct().ToList();
				
				// Calcular totales globales y por almacén si no están disponibles
				var totalesGlobales = new Dictionary<(string CodigoArticulo, string Partida), decimal>();
				var totalesPorAlmacen = new Dictionary<(string CodigoArticulo, string Partida, string CodigoAlmacen), decimal>();
				// 🔷 NUEVO: Calcular totales por ubicación (artículo + partida + ubicación)
				var totalesPorUbicacion = new Dictionary<(string CodigoArticulo, string Partida, string Ubicacion), decimal>();
				
				// Calcular totales por ubicación (siempre, para mostrar en "Disponible")
				var gruposPorUbicacion = stockFiltradoPorPalet
					.GroupBy(s => new { s.CodigoArticulo, s.Partida, s.Ubicacion })
					.ToDictionary(
						g => (g.Key.CodigoArticulo, g.Key.Partida ?? "", g.Key.Ubicacion ?? ""),
						g => g.Sum(x => 
						{
							// Sumar la cantidad del palet individual (no el total)
							if (x.Palets != null && x.Palets.Any())
							{
								return x.Palets.Sum(p => p.Cantidad);
							}
							return x.Disponible;
						})
					);
				
				totalesPorUbicacion = gruposPorUbicacion;
				
				// Si algún item no tiene totales, los calculamos
				if (stockFiltradoPorPalet.Any(s => !s.TotalArticuloGlobal.HasValue || !s.TotalArticuloAlmacen.HasValue))
				{
					// Agrupar por artículo/partida para calcular totales globales
					var gruposGlobales = stockFiltradoPorPalet
						.GroupBy(s => new { s.CodigoArticulo, s.Partida })
						.ToDictionary(
							g => (g.Key.CodigoArticulo, g.Key.Partida),
							g => g.Sum(x => x.Disponible)
						);
					
					// Agrupar por artículo/partida/almacén para calcular totales por almacén
					var gruposPorAlmacen = stockFiltradoPorPalet
						.GroupBy(s => new { s.CodigoArticulo, s.Partida, s.CodigoAlmacen })
						.ToDictionary(
							g => (g.Key.CodigoArticulo, g.Key.Partida, g.Key.CodigoAlmacen),
							g => g.Sum(x => x.Disponible)
						);
					
					totalesGlobales = gruposGlobales;
					totalesPorAlmacen = gruposPorAlmacen;
				}
				
				// 🔷 MODIFICADO: En modo palet, crear una entrada por cada artículo/palet (no agrupar)
				// Esto permite mostrar todas las líneas de todos los palets, ordenadas por palet
				var lista = stockFiltradoPorPalet
					.Where(x => x.TipoStock == "Paletizado" && (!string.IsNullOrWhiteSpace(x.CodigoPalet) || x.PaletId.HasValue))
					.Select(s =>
					{
						// Cada entrada de stock disponible ya representa un artículo en un palet
						var palets = new List<PaletDetalleDto>();
						
						if (s.Palets != null && s.Palets.Any())
						{
							// Usar los palets que ya vienen en el DTO
							palets = s.Palets.ToList();
						}
						else if (s.PaletId.HasValue || !string.IsNullOrWhiteSpace(s.CodigoPalet))
						{
							// Si no hay lista de palets pero hay información de palet, crear uno
							palets.Add(new PaletDetalleDto
							{
								PaletId = s.PaletId ?? Guid.Empty,
								CodigoPalet = s.CodigoPalet ?? "",
								EstadoPalet = s.EstadoPalet ?? "",
								Cantidad = s.Disponible,
								Ubicacion = s.Ubicacion,
								Partida = s.Partida
							});
						}
						
						// Obtener totales: usar los que vienen del API si están disponibles, sino calcular
						var totalGlobal = s.TotalArticuloGlobal;
						var totalAlmacen = s.TotalArticuloAlmacen;
						
						if (!totalGlobal.HasValue && totalesGlobales.TryGetValue((s.CodigoArticulo, s.Partida ?? ""), out var globalCalc))
						{
							totalGlobal = globalCalc;
						}
						
						if (!totalAlmacen.HasValue && totalesPorAlmacen.TryGetValue((s.CodigoArticulo, s.Partida ?? "", s.CodigoAlmacen), out var almacenCalc))
						{
							totalAlmacen = almacenCalc;
						}
						
						// 🔷 MODIFICADO: Usar la cantidad total del artículo en esa ubicación específica
						var ubicacionKey = (s.CodigoArticulo, s.Partida ?? "", s.Ubicacion ?? "");
						var cantidadTotal = totalesPorUbicacion.TryGetValue(ubicacionKey, out var totalUbicacion) 
							? totalUbicacion 
							: (totalAlmacen ?? totalGlobal ?? s.Disponible);
						
						return new StockDto
						{
							CodigoEmpresa = s.CodigoEmpresa, // short se convierte implícitamente a int
							CodigoArticulo = s.CodigoArticulo,
							DescripcionArticulo = s.DescripcionArticulo,
							CodigoAlternativo = s.CodigoAlternativo,
							CodigoAlmacen = s.CodigoAlmacen,
							Almacen = "", // No disponible en StockDisponibleDto
							Ubicacion = s.Ubicacion,
							Partida = s.Partida,
							FechaCaducidad = s.FechaCaducidad,
							UnidadSaldo = cantidadTotal, // 🔷 MODIFICADO: Usar la cantidad total del artículo
							Palets = palets,
							CodigoPalet = s.CodigoPalet,
							EstadoPalet = s.EstadoPalet,
							TotalArticuloGlobal = totalGlobal,
							TotalArticuloAlmacen = totalAlmacen,
							IsBloqueadoCalidad = s.IsBloqueadoCalidad,
							MotivoBloqueoCalidad = s.MotivoBloqueoCalidad,
							FechaBloqueoCalidad = s.FechaBloqueoCalidad,
							TipoBloqueoCalidad = s.TipoBloqueoCalidad ?? "TOTAL",
							// 🔷 NUEVO: Fecha del último traspaso
							FechaUltimoTraspaso = s.FechaUltimoTraspaso
						};
					})
					.ToList();

				// 🔷 ACTUALIZADO: El backend ya devuelve IsBloqueadoCalidad correctamente por ubicación
				// No necesitamos consultar bloqueos manualmente, el backend ya lo hace

				// 🔷 NUEVO: Ordenar primero por código de palet, luego por artículo
				lista = lista
					.OrderBy(x => x.CodigoPalet ?? "")
					.ThenBy(x => x.CodigoArticulo)
					.ThenBy(x => x.Partida)
					.ToList();

				// Llenar resultados en modo palet (usa ResultadosStockPorPalet)
				LlenarResultados(lista, filterByPermissions: false); // Ya filtrado por permisos arriba
				OnPropertyChanged(nameof(CanRefresh));
				OnPropertyChanged(nameof(CanExportExcel));
			}
			catch (Exception ex)
			{
				MostrarError("Error al consultar por palet", ex);
			}
		}



		[RelayCommand]
		private void ExportarExcel()
		{
			// ▶️ Cambiado: exportamos StockFiltrado en modo artículo
			var listaActiva = IsArticleMode
				? StockFiltrado.ToList()
				: IsLocationMode
					? ResultadosStockPorUbicacion.ToList()
					: ResultadosStockPorPalet.ToList();

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
            // Solo establecer Owner si la ventana principal está disponible y no es el propio diálogo
            if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                if (!ReferenceEquals(Application.Current.MainWindow, confirm))
                    confirm.Owner = Application.Current.MainWindow;
            }
			if (confirm.ShowDialog() != true)
				return;

			try
			{
				// 2) Generar nombre de archivo descriptivo
				var nombreArchivo = GenerarNombreArchivo();
				var rutaTemporal = Path.Combine(Path.GetTempPath(), $"SGA_{Guid.NewGuid()}_{nombreArchivo}");

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

				// 7) Guardar archivo temporal
				wb.SaveAs(rutaTemporal);

				// 8) 🔷 NUEVO: Abrir Excel directamente y mostrar opciones
                var previewDialog = new ExcelPreviewDialog(rutaTemporal, nombreArchivo);
                if (Application.Current.MainWindow != null && !ReferenceEquals(Application.Current.MainWindow, previewDialog) && Application.Current.MainWindow.IsVisible)
                    previewDialog.Owner = Application.Current.MainWindow;
				previewDialog.ShowDialog();

				// 9) Limpiar archivo temporal después de cerrar el diálogo
				try
				{
					if (File.Exists(rutaTemporal))
						File.Delete(rutaTemporal);
				}
				catch
				{
					// Ignorar errores al limpiar archivos temporales
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Error al generar el archivo Excel:\n{ex.Message}", 
							  "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
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

		// Validar que el artículo tenga EAN antes de imprimir
		if (string.IsNullOrWhiteSpace(codigoAlternativo))
		{
			var warning = new WarningDialog(
				"No se puede imprimir",
				"El artículo no tiene EAN. No se puede imprimir sin EAN.",
				"\uE814" // icono de advertencia
			)
			{ Owner = Application.Current.MainWindow };
			warning.ShowDialog();
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
			var errorDialog = new WarningDialog(
				"Error al encolar impresión",
				ex.Message,
				"\uE783" // Icono de error
			)
			{
				Owner = Application.Current.Windows.OfType<Window>()
					.FirstOrDefault(w => w.IsActive)
					?? Application.Current.MainWindow
			};
			errorDialog.ShowDialog();
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
				else if (IsPaletMode)
				{
					await BuscarPorPaletAsync();
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

		private void ActualizarPartidasDisponibles()
		{
			PartidasDisponibles.Clear();

			if (!_resultadosArticuloBase.Any())
				return;

			var partidas = _resultadosArticuloBase
				.Select(r => r.Partida)
				.Where(p => !string.IsNullOrWhiteSpace(p))
				.Distinct()
				.OrderBy(p => p);

			foreach (var partida in partidas)
			{
				PartidasDisponibles.Add(partida);
			}

			if (!string.IsNullOrWhiteSpace(FiltroPartida) && !PartidasDisponibles.Contains(FiltroPartida))
			{
				FiltroPartida = string.Empty;
			}
		}

		private void AplicarFiltroPartidaArticulo()
		{
			if (!IsArticleMode)
			{
				return;
			}

			if (!_resultadosArticuloBase.Any())
			{
				ResultadosStock.Clear();
				StockFiltrado.Clear();
				ArticulosConUbicaciones.Clear();
				ArticuloMostrado = string.Empty;
				OnPropertyChanged(nameof(ArticuloMostrado));
				OnPropertyChanged(nameof(ArticulosUnicosVisibility));
				OnPropertyChanged(nameof(ListViewVisibility));
				OnPropertyChanged(nameof(CanRefresh));
				OnPropertyChanged(nameof(CanExportExcel));
				return;
			}

			IEnumerable<StockDto> listaFiltrada = _resultadosArticuloBase;
			IEnumerable<StockDisponibleDto> stockFiltrado = _stockDisponibleArticuloBase;

			if (!string.IsNullOrWhiteSpace(FiltroPartida))
			{
				listaFiltrada = listaFiltrada.Where(s =>
					string.Equals(s.Partida, FiltroPartida, StringComparison.OrdinalIgnoreCase));

				stockFiltrado = stockFiltrado.Where(s =>
					string.Equals(s.Partida, FiltroPartida, StringComparison.OrdinalIgnoreCase));
			}

			var listaMaterializada = listaFiltrada.ToList();
			var stockMaterializado = stockFiltrado.ToList();

			ResultadosStock.Clear();
			foreach (var item in listaMaterializada)
				ResultadosStock.Add(item);

			StockFiltrado.Clear();
			foreach (var item in listaMaterializada)
				StockFiltrado.Add(item);

			var grupos = ConstruirGruposArticulo(stockMaterializado);
			ArticulosConUbicaciones.Clear();
			foreach (var grupo in grupos)
				ArticulosConUbicaciones.Add(grupo);

			ArticuloMostrado = listaMaterializada.FirstOrDefault()?.DescripcionArticulo ?? string.Empty;

			OnPropertyChanged(nameof(ArticuloMostrado));
			OnPropertyChanged(nameof(ArticulosUnicosVisibility));
			OnPropertyChanged(nameof(ListViewVisibility));
			OnPropertyChanged(nameof(CanRefresh));
			OnPropertyChanged(nameof(CanExportExcel));
		}

		private List<ArticuloStockGroup> ConstruirGruposArticulo(IEnumerable<StockDisponibleDto> stock)
		{
			return stock
				.GroupBy(x => new { x.CodigoArticulo, x.DescripcionArticulo })
				.Select(g => new ArticuloStockGroup
				{
					CodigoArticulo = g.Key.CodigoArticulo,
					DescripcionArticulo = g.Key.DescripcionArticulo,
					Ubicaciones = new ObservableCollection<StockDisponibleDto>(
						g.OrderBy(x => x.CodigoAlmacen)
						 .ThenBy(x => x.Ubicacion)
						 .ToList()),
					IsExpanded = !_busquedaPorDescripcion
				})
				.OrderBy(a => a.CodigoArticulo)
				.ToList();
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
			else if (IsLocationMode)
			{
				// en modo ubicación no mostramos destacado y clear/fill la colección de ubicación
				ArticuloMostrado = string.Empty;

				ResultadosStockPorUbicacion.Clear();
				filtrada.ForEach(x => ResultadosStockPorUbicacion.Add(x));
				PartidasDisponibles.Clear();
			}
			else if (IsPaletMode)
			{
				// en modo palet no mostramos destacado y clear/fill la colección de palet
				ArticuloMostrado = string.Empty;

				// 🔷 NUEVO: Ordenar primero por código de palet, luego por artículo
				var ordenada = filtrada
					.OrderBy(x => x.CodigoPalet ?? "")
					.ThenBy(x => x.CodigoArticulo)
					.ThenBy(x => x.Partida)
					.ToList();

				ResultadosStockPorPalet.Clear();
				ordenada.ForEach(x => ResultadosStockPorPalet.Add(x));
				PartidasDisponibles.Clear();
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

	/// <summary>
	/// Genera un nombre de archivo descriptivo basado en el modo y filtros activos
	/// </summary>
	private string GenerarNombreArchivo()
	{
		var nombreBase = "Stock";
		var fechaActual = DateTime.Now.ToString("dd-MM-yyyy");
		
		if (IsArticleMode)
		{
			// Modo artículo: Stock PR10002 en 201
			if (!string.IsNullOrWhiteSpace(FiltroArticulo))
			{
				nombreBase = $"Stock {FiltroArticulo}";
				
				// Agregar almacén si está seleccionado
				if (AlmacenSeleccionadoCombo != null && AlmacenSeleccionadoCombo.CodigoAlmacen != TODAS)
				{
					nombreBase += $" en {AlmacenSeleccionadoCombo.CodigoAlmacen}";
				}
			}
			else
			{
				nombreBase = "ConsultaPorArticulo";
			}
		}
		else
		{
			// Modo ubicación: Stock en almacen X
			if (AlmacenSeleccionadoCombo != null && AlmacenSeleccionadoCombo.CodigoAlmacen != TODAS)
			{
				nombreBase = $"Stock en almacén {AlmacenSeleccionadoCombo.CodigoAlmacen}";
			}
			else
			{
				nombreBase = "ConsultaPorUbicacion";
			}
		}
		
		// Agregar información de ubicación si está seleccionada
		if (!string.IsNullOrWhiteSpace(FiltroUbicacion) && FiltroUbicacion != TODAS && FiltroUbicacion != TODO_ALMACEN)
		{
			nombreBase += $" - {FiltroUbicacion}";
		}
		
		// Agregar información de partida si está filtrada
		if (!string.IsNullOrWhiteSpace(FiltroPartida))
		{
			nombreBase += $" - Partida {FiltroPartida}";
		}
		
		// 🔷 FECHA SIEMPRE AL FINAL
		nombreBase += $" {fechaActual}";
		
		// Limpiar caracteres no válidos para nombres de archivo
		var caracteresInvalidos = Path.GetInvalidFileNameChars();
		foreach (var caracter in caracteresInvalidos)
		{
			nombreBase = nombreBase.Replace(caracter, '_');
		}
		
		return $"{nombreBase}.xlsx";
	}
}
}



