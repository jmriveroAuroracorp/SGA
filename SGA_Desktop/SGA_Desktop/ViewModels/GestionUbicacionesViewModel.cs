using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Dialog;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using SGA_Desktop.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Linq;

public partial class UbicacionPasilloGroup : ObservableObject
{
	public int? Pasillo { get; set; }
	public bool EsEspecial { get; set; } = false; // Indica si es el grupo de ubicaciones especiales
	public ObservableCollection<UbicacionDetalladaDto> Ubicaciones { get; set; } = new();
	public ObservableCollection<int?> EstanteriasDisponibles { get; set; } = new();
	public ObservableCollection<int?> AlturasDisponibles { get; set; } = new();
	public string HeaderPasillo 
	{ 
		get 
		{
			if (EsEspecial) return "Ubicaciones especiales";
			return Pasillo.HasValue ? $"Pasillo {Pasillo}" : "Sin pasillo";
		}
	}
	public int TotalUbicaciones => Ubicaciones?.Count ?? 0;
	
	[ObservableProperty]
	private bool isExpanded = false; // Por defecto colapsado
}

public partial class GestionUbicacionesViewModel : ObservableObject
{
	private readonly StockService _stockService;
	private readonly UbicacionesService _ubicService;
	private readonly PaletService _paletService;
	private readonly PrintQueueService _printService;
	private readonly LoginService _loginService;

	public ObservableCollection<AlmacenDto> AlmacenesCombo { get; }
		= new ObservableCollection<AlmacenDto>();
	[ObservableProperty] private AlmacenDto? selectedAlmacenCombo;

	public ObservableCollection<UbicacionDetalladaDto> Ubicaciones { get; }
		= new ObservableCollection<UbicacionDetalladaDto>();
	[ObservableProperty] private UbicacionDetalladaDto? selectedUbicacion;
	
	public int TotalUbicaciones 
	{ 
		get 
		{
			// Si hay grupos, sumar las ubicaciones de todos los grupos (ya están filtradas)
			if (UbicacionesAgrupadas.Count > 0)
			{
				return UbicacionesAgrupadas.Sum(g => g.TotalUbicaciones);
			}
			// Si no hay grupos, devolver el total sin filtrar (por si acaso)
			return Ubicaciones.Count;
		}
	}

	// Agrupación por pasillo
	public ObservableCollection<UbicacionPasilloGroup> UbicacionesAgrupadas { get; }
		= new ObservableCollection<UbicacionPasilloGroup>();

	// Filtrado
	public ICollectionView UbicacionesView { get; }
	private string _filtroBusqueda = string.Empty;
	public string FiltroBusqueda
	{
		get => _filtroBusqueda;
		set
		{
			if (SetProperty(ref _filtroBusqueda, value))
			{
				UbicacionesView.Refresh();
				// Reagrupar cuando cambia el filtro
				ReagruparUbicaciones();
			}
		}
	}

	public ObservableCollection<ImpresoraDto> ImpresorasDisponibles { get; } = new();

	[ObservableProperty] private string? errorMessage;

	public GestionUbicacionesViewModel()
	: this(new StockService(), new UbicacionesService(), new PaletService())
	{ }

	[ObservableProperty]
	private bool haySeleccion;
	
	[ObservableProperty]
	private bool hayMasDeUnaSeleccionada;

	[ObservableProperty]
	private int seleccionadasCount;

	[ObservableProperty]
	private bool isBusy;
	
	[ObservableProperty]
	private bool mostrarObsoletas = false;

	public ObservableCollection<int?> AlturasDisponibles { get; } = new();
	[ObservableProperty] private int? alturaSeleccionada;

	public ObservableCollection<int?> PasillosDisponibles { get; } = new();
	[ObservableProperty] private int? pasilloSeleccionado;

	public ObservableCollection<int?> EstanteriasDisponibles { get; } = new();
	[ObservableProperty] private int? estanteriaSeleccionada;

	[RelayCommand]
	private void SeleccionarPorAltura(int? altura)
	{
		if (altura == null) return;
		foreach (var u in UbicacionesView.Cast<UbicacionDetalladaDto>())
			u.IsMarcada = u.Altura == altura;
		RecalcularSeleccion();
	}

	[RelayCommand]
	private void SeleccionarPorPasillo(int? pasillo)
	{
		if (pasillo == null) return;
		foreach (var u in UbicacionesView.Cast<UbicacionDetalladaDto>())
			u.IsMarcada = u.Pasillo == pasillo;
		RecalcularSeleccion();
	}

	[RelayCommand]
	private void SeleccionarPorEstanteria(int? estanteria)
	{
		if (estanteria == null) return;
		foreach (var u in UbicacionesView.Cast<UbicacionDetalladaDto>())
			u.IsMarcada = u.Estanteria == estanteria;
		RecalcularSeleccion();
	}

	[RelayCommand]
	private void LimpiarFiltros()
	{
		PasilloSeleccionado = null;
		EstanteriaSeleccionada = null;
		AlturaSeleccionada = null;
		FiltroBusqueda = string.Empty;
		// No deseleccionamos las ubicaciones, solo limpiamos los filtros
	}

	private void RecalcularSeleccion()
	{
		// Contar TODAS las ubicaciones seleccionadas, no solo las visibles (filtradas)
		SeleccionadasCount = Ubicaciones.Count(u => u.IsMarcada);
		HaySeleccion = SeleccionadasCount > 0;
		HayMasDeUnaSeleccionada = SeleccionadasCount > 1;
		EditarSeleccionadasCommand.NotifyCanExecuteChanged();
	}

	/// <summary>Comando que carga los alérgenos de una ubicación.</summary>
	public IAsyncRelayCommand<UbicacionDetalladaDto> LoadAlergenosCommand { get; }

	public IRelayCommand CreateUbicacionCommand { get; }
	public IRelayCommand<UbicacionDetalladaDto> EditarUbicacionCommand { get; }
	public IRelayCommand<AlmacenDto> OpenMasivoCommand { get; }
	public IRelayCommand RefrescarCommand { get; }
	public IRelayCommand EditarSeleccionadasCommand { get; }

	[RelayCommand]
	private async Task ImprimirUbicacionAsync(UbicacionDetalladaDto ubicacion)
	{
		if (ubicacion is null) return;

		// Mostrar confirmación al usuario con los datos que se van a imprimir
		string detalles =
$"Almacén: {ubicacion.CodigoAlmacen}\n" +
$"Ubicación: {ubicacion.Ubicacion}\n" +
$"Altura: {ubicacion.Altura}\n" +
$"Estantería: {ubicacion.Estanteria}\n" +
$"Pasillo: {ubicacion.Pasillo}\n" +
$"Posición: {ubicacion.Posicion}";
		var confirm = new ConfirmationDialog(
			"Confirmar impresión de ubicación",
			detalles,
			"\uE946" // icono de información
		);
		var confirmOwner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
			?? Application.Current.MainWindow;
		if (confirmOwner != null && confirmOwner != confirm)
			confirm.Owner = confirmOwner;
		if (confirm.ShowDialog() != true) return;

		// Abrimos diálogo de impresión
		// usa el nombre preferido que tengas (sesión o BD). Si no, el primero.
		string? preNombre = SessionManager.PreferredPrinter
	?? ImpresorasDisponibles.FirstOrDefault()?.Nombre;

		var dlgVm = new ConfirmarImpresionDialogViewModel(
			ImpresorasDisponibles,
			preNombre,
			_loginService
		);

		var dlg = new ConfirmarImpresionDialog
		{
			DataContext = dlgVm
		};
		var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
			 ?? Application.Current.MainWindow;
		if (owner != null && owner != dlg)
			dlg.Owner = owner;

		if (dlg.ShowDialog() != true) return;

		// ya está guardado en BD y en SessionManager por el propio diálogo
		var seleccionada = dlgVm.ImpresoraSeleccionada;

		try
		{
			var dto = new LogImpresionDto
			{
				Usuario = SessionManager.NombreOperario,
				Dispositivo = System.Environment.MachineName,
				IdImpresora = dlgVm.ImpresoraSeleccionada?.Id ?? 0,
				EtiquetaImpresa = 0,
				Copias = dlgVm.NumeroCopias,
				CodigoArticulo = null,
				DescripcionArticulo = null,
				CodigoAlternativo = null,
				FechaCaducidad = null,
				Partida = null,
				Alergenos = null,
				PathEtiqueta = @"\\Sage200\mrh\Servicios\PrintCenter\ETIQUETAS\UBICACIONES.nlbl",
				TipoEtiqueta = 3,
				CodigoGS1 = null,
				CodigoPalet = null,
				CodAlmacen = ubicacion.CodigoAlmacen,
				CodUbicacion = ubicacion.Ubicacion,
				Altura = ubicacion.Altura,
				Estanteria = ubicacion.Estanteria,
				Pasillo = ubicacion.Pasillo,
				Posicion = ubicacion.Posicion
			};

			await _printService.InsertarRegistroImpresionAsync(dto);

		}
		catch (Exception ex)
		{
			MessageBox.Show(
				ex.Message,
				"Error al imprimir",
				MessageBoxButton.OK,
				MessageBoxImage.Error);
		}
	}

	public GestionUbicacionesViewModel(
		StockService stockService,
		UbicacionesService ubicService,
		PaletService paletService)
	{

		_stockService = stockService;
		_ubicService = ubicService;
		_paletService = paletService;
		_printService = new PrintQueueService();
		_loginService = new LoginService();
		
		// Inicializar CollectionView para filtrado
		UbicacionesView = CollectionViewSource.GetDefaultView(Ubicaciones);
		UbicacionesView.Filter = FiltroUbicacion;
		
		// Suscribirse a cambios en la colección para actualizar TotalUbicaciones
		Ubicaciones.CollectionChanged += (s, e) => OnPropertyChanged(nameof(TotalUbicaciones));
		
		LoadAlergenosCommand = new AsyncRelayCommand<UbicacionDetalladaDto>(LoadAlergenosAsync);
		CreateUbicacionCommand = new RelayCommand<AlmacenDto>(
		OpenCrearUbicacionDialog,
		alm => alm != null
		);
		EditarUbicacionCommand = new RelayCommand<UbicacionDetalladaDto>(
	  OpenEditarUbicacionDialog,
	  dto => dto != null
  );
		_ = InitializeAsync();
		OpenMasivoCommand = new RelayCommand<AlmacenDto>(OpenMasivoDialog, alm => alm != null);
		RefrescarCommand = new RelayCommand(RefrescarUbicaciones, () => SelectedAlmacenCombo != null);
		EditarSeleccionadasCommand = new RelayCommand(OpenEditarMasivoDialog, () => HaySeleccion);
		
		// Solo cargar impresoras si la aplicación no se está cerrando
		if (!SessionManager.IsClosing)
		{
			_ = LoadImpresorasAsync();
		}

	}

	[RelayCommand]
	private void SeleccionarTodo()
	{
		foreach (var u in UbicacionesView.Cast<UbicacionDetalladaDto>())
			u.IsMarcada = true;
		RecalcularSeleccion();
	}

	[RelayCommand]
	private void SeleccionarTodoElPasillo(UbicacionPasilloGroup grupo)
	{
		if (grupo == null || grupo.Ubicaciones == null) return;
		
		foreach (var ubicacion in grupo.Ubicaciones)
		{
			ubicacion.IsMarcada = true;
		}
		RecalcularSeleccion();
	}

	[RelayCommand]
	private void SeleccionarPorEstanteriaEnGrupo(object[] parametros)
	{
		Debug.WriteLine("=== SeleccionarPorEstanteriaEnGrupo ===");
		Debug.WriteLine($"Parametros: {parametros?.Length ?? 0}");
		
		if (parametros == null || parametros.Length < 2)
		{
			Debug.WriteLine("Parametros nulos o insuficientes");
			return;
		}
		
		Debug.WriteLine($"Parametros[0]: {parametros[0]} (tipo: {parametros[0]?.GetType().Name ?? "null"})");
		Debug.WriteLine($"Parametros[1]: {parametros[1]} (tipo: {parametros[1]?.GetType().Name ?? "null"})");
		
		var grupo = parametros[0] as UbicacionPasilloGroup;
		Debug.WriteLine($"Grupo después de cast: {grupo?.HeaderPasillo ?? "null"} con {grupo?.Ubicaciones?.Count ?? 0} ubicaciones");
		
		// Si el cast falla, intentar obtener el grupo de otra manera
		if (grupo == null && parametros[0] != null)
		{
			Debug.WriteLine($"El cast falló. Tipo real: {parametros[0].GetType().FullName}");
			// Intentar obtener el grupo desde UbicacionesAgrupadas
			var estanteriaTemp = parametros[1] is int intValTemp ? intValTemp : (int.TryParse(parametros[1]?.ToString(), out int parsedTemp) ? parsedTemp : (int?)null);
			if (estanteriaTemp.HasValue)
			{
				Debug.WriteLine($"Intentando encontrar grupo por estantería {estanteriaTemp.Value}");
				// Buscar en todos los grupos
				foreach (var g in UbicacionesAgrupadas)
				{
					if (g.Ubicaciones.Any(u => u.Estanteria == estanteriaTemp.Value))
					{
						grupo = g;
						Debug.WriteLine($"Grupo encontrado: {grupo.HeaderPasillo}");
						break;
					}
				}
			}
		}
		
		if (grupo == null || grupo.Ubicaciones == null)
		{
			Debug.WriteLine("Grupo nulo o sin ubicaciones - ABORTANDO");
			return;
		}
		
		// Convertir el segundo parámetro a int?
		int? estanteria = null;
		var valor = parametros[1];
		Debug.WriteLine($"Valor recibido: {valor} (tipo: {valor?.GetType().Name ?? "null"})");
		
		if (valor is int intVal)
		{
			estanteria = intVal;
		}
		else if (valor != null)
		{
			// Intentar conversión desde string o nullable
			if (int.TryParse(valor.ToString(), out int parsed))
			{
				estanteria = parsed;
			}
		}
		
		Debug.WriteLine($"Estanteria a seleccionar: {estanteria}");
		
		if (!estanteria.HasValue)
		{
			Debug.WriteLine("No se pudo obtener valor de estantería");
			return;
		}
		
		int seleccionadas = 0;
		foreach (var ubicacion in grupo.Ubicaciones)
		{
			// Solo seleccionar las que coinciden, sin deseleccionar las demás
			if (ubicacion.Estanteria == estanteria)
			{
				ubicacion.IsMarcada = true;
				seleccionadas++;
				Debug.WriteLine($"  Seleccionada: {ubicacion.Ubicacion} (Est: {ubicacion.Estanteria})");
			}
		}
		Debug.WriteLine($"Total seleccionadas: {seleccionadas}");
		RecalcularSeleccion();
	}

	[RelayCommand]
	private void SeleccionarPorAlturaEnGrupo(object[] parametros)
	{
		Debug.WriteLine("=== SeleccionarPorAlturaEnGrupo ===");
		Debug.WriteLine($"Parametros: {parametros?.Length ?? 0}");
		
		if (parametros == null || parametros.Length < 2)
		{
			Debug.WriteLine("Parametros nulos o insuficientes");
			return;
		}
		
		Debug.WriteLine($"Parametros[0]: {parametros[0]} (tipo: {parametros[0]?.GetType().Name ?? "null"})");
		Debug.WriteLine($"Parametros[1]: {parametros[1]} (tipo: {parametros[1]?.GetType().Name ?? "null"})");
		
		var grupo = parametros[0] as UbicacionPasilloGroup;
		Debug.WriteLine($"Grupo después de cast: {grupo?.HeaderPasillo ?? "null"} con {grupo?.Ubicaciones?.Count ?? 0} ubicaciones");
		
		// Si el cast falla, intentar obtener el grupo de otra manera
		if (grupo == null && parametros[0] != null)
		{
			Debug.WriteLine($"El cast falló. Tipo real: {parametros[0].GetType().FullName}");
			// Intentar obtener el grupo desde UbicacionesAgrupadas
			var alturaTemp = parametros[1] is int intValTemp ? intValTemp : (int.TryParse(parametros[1]?.ToString(), out int parsedTemp) ? parsedTemp : (int?)null);
			if (alturaTemp.HasValue)
			{
				Debug.WriteLine($"Intentando encontrar grupo por altura {alturaTemp.Value}");
				// Buscar en todos los grupos
				foreach (var g in UbicacionesAgrupadas)
				{
					if (g.Ubicaciones.Any(u => u.Altura == alturaTemp.Value))
					{
						grupo = g;
						Debug.WriteLine($"Grupo encontrado: {grupo.HeaderPasillo}");
						break;
					}
				}
			}
		}
		
		if (grupo == null || grupo.Ubicaciones == null)
		{
			Debug.WriteLine("Grupo nulo o sin ubicaciones - ABORTANDO");
			return;
		}
		
		// Convertir el segundo parámetro a int?
		int? altura = null;
		var valor = parametros[1];
		Debug.WriteLine($"Valor recibido: {valor} (tipo: {valor?.GetType().Name ?? "null"})");
		
		if (valor is int intVal)
		{
			altura = intVal;
		}
		else if (valor != null)
		{
			// Intentar conversión desde string o nullable
			if (int.TryParse(valor.ToString(), out int parsed))
			{
				altura = parsed;
			}
		}
		
		Debug.WriteLine($"Altura a seleccionar: {altura}");
		
		if (!altura.HasValue)
		{
			Debug.WriteLine("No se pudo obtener valor de altura");
			return;
		}
		
		int seleccionadas = 0;
		foreach (var ubicacion in grupo.Ubicaciones)
		{
			// Solo seleccionar las que coinciden, sin deseleccionar las demás
			if (ubicacion.Altura == altura)
			{
				ubicacion.IsMarcada = true;
				seleccionadas++;
				Debug.WriteLine($"  Seleccionada: {ubicacion.Ubicacion} (Alt: {ubicacion.Altura})");
			}
		}
		Debug.WriteLine($"Total seleccionadas: {seleccionadas}");
		RecalcularSeleccion();
	}

	[RelayCommand]
	private void LimpiarSeleccion()
	{
		foreach (var u in UbicacionesView.Cast<UbicacionDetalladaDto>())
			u.IsMarcada = false;
		RecalcularSeleccion();
	}

	[RelayCommand]
	private void RefrescarUbicaciones()
	{
		if (SelectedAlmacenCombo != null)
		{
			_ = LoadUbicacionesAsync(SelectedAlmacenCombo.CodigoAlmacen);
		}
	}

	[RelayCommand]
	private async Task ImprimirSeleccionadasAsync()
	{
		// Filtrar ubicaciones marcadas
		var seleccionadas = Ubicaciones.Where(u => u.IsMarcada).ToList();
		if (!seleccionadas.Any())
		{
			MessageBox.Show("No hay ubicaciones seleccionadas para imprimir.",
				"Impresión", MessageBoxButton.OK, MessageBoxImage.Information);
			return;
		}

		// Confirmación previa
		var confirm = new ConfirmationDialog(
			"Confirmar impresión",
			$"Se imprimirán {seleccionadas.Count} ubicaciones en la impresora seleccionada.\n¿Deseas continuar?",
			"\uE946" // icono info
		);
		var confirmOwner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
				?? Application.Current.MainWindow;
		if (confirmOwner != null && confirmOwner != confirm)
			confirm.Owner = confirmOwner;

		if (confirm.ShowDialog() != true)
			return;

		// Selección de impresora y copias (una sola vez)
		// usa el nombre preferido que tengas (sesión o BD). Si no, el primero.
		string? preNombre = SessionManager.PreferredPrinter
	?? ImpresorasDisponibles.FirstOrDefault()?.Nombre;

		var dlgVm = new ConfirmarImpresionDialogViewModel(
			ImpresorasDisponibles,
			preNombre,
			_loginService
		);

		var dlg = new ConfirmarImpresionDialog
		{
			DataContext = dlgVm
		};
		
		var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
				?? Application.Current.MainWindow;
		if (owner != null && owner != dlg)
			dlg.Owner = owner;

		if (dlg.ShowDialog() != true) return;

		// ya está guardado en BD y en SessionManager por el propio diálogo
		var seleccionada = dlgVm.ImpresoraSeleccionada;
		// …continúas con tu impresión de palet…

		try
		{
			foreach (var ubicacion in seleccionadas)
			{
				var dto = new LogImpresionDto
				{
					Usuario = SessionManager.NombreOperario,
					Dispositivo = Environment.MachineName,
					IdImpresora = dlgVm.ImpresoraSeleccionada?.Id ?? 0,
					EtiquetaImpresa = 0,
					Copias = dlgVm.NumeroCopias,
					PathEtiqueta = @"\\Sage200\mrh\Servicios\PrintCenter\ETIQUETAS\UBICACIONES.nlbl",
					TipoEtiqueta = 3,
					CodAlmacen = ubicacion.CodigoAlmacen,
					CodUbicacion = ubicacion.Ubicacion,
					Altura = ubicacion.Altura,
					Estanteria = ubicacion.Estanteria,
					Pasillo = ubicacion.Pasillo,
					Posicion = ubicacion.Posicion
				};

				await _printService.InsertarRegistroImpresionAsync(dto);
			}

			MessageBox.Show(
				$"Se han enviado {seleccionadas.Count} impresiones.",
				"Impresión completada",
				MessageBoxButton.OK,
				MessageBoxImage.Information);
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.Message, "Error al imprimir", MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}




	private async Task InitializeAsync()
	{
		var empresa = SessionManager.EmpresaSeleccionada!.Value;
		var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
		var desdeLogin = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();

		// 1) Filtramos almacenes
		var autorizados = await _stockService
			.ObtenerAlmacenesAutorizadosAsync(empresa, centro, desdeLogin, SessionManager.Operario);

		AlmacenesCombo.Clear();
		foreach (var a in autorizados)
			AlmacenesCombo.Add(a);

		// 2) Seleccionamos el primero y disparamos carga de ubicaciones
		SelectedAlmacenCombo = AlmacenesCombo.FirstOrDefault();
	}

	partial void OnSelectedAlmacenComboChanged(
		AlmacenDto? old, AlmacenDto? nuev)
	{
		if (nuev is not null)
			_ = LoadUbicacionesAsync(nuev.CodigoAlmacen);
		
		// Actualizar el estado del comando de refrescar
		RefrescarCommand.NotifyCanExecuteChanged();
	}

	private async Task LoadUbicacionesAsync(string almacen)
	{
		IsBusy = true;
		try
		{
			Ubicaciones.Clear();
			UbicacionesAgrupadas.Clear();
			OnPropertyChanged(nameof(TotalUbicaciones));
			if (string.IsNullOrWhiteSpace(almacen)) return;

			var empresa = SessionManager.EmpresaSeleccionada!.Value;

		// Llama al endpoint ligero
		var listaBasica = await _ubicService
			.ObtenerUbicacionesBasicoAsync(empresa, almacen, MostrarObsoletas);

		foreach (var dto in listaBasica)
		{
			// Asegúrate de que estos campos existen en tu DTO
			dto.AlergenosPresentes = "";
			dto.AlergenosPermitidos = "";
			dto.RiesgoContaminacion = false;
			dto.PropertyChanged += (s, e) =>
			{
				if (e.PropertyName == nameof(UbicacionDetalladaDto.IsMarcada))
					RecalcularSeleccion();
			};
			Ubicaciones.Add(dto);
		}

		// Aplicar filtro antes de agrupar
		var ubicacionesFiltradas = Ubicaciones.Where(u => FiltroUbicacion(u)).ToList();

		// Separar ubicaciones especiales (que no empiezan por "UB" o son cadena vacía)
		var ubicacionesEspeciales = ubicacionesFiltradas
			.Where(u => string.IsNullOrWhiteSpace(u.Ubicacion) || 
			           !u.Ubicacion.StartsWith("UB", StringComparison.OrdinalIgnoreCase))
			.OrderBy(u => u.Ubicacion)
			.ToList();

		// Ubicaciones normales (que empiezan por "UB")
		var ubicacionesNormales = ubicacionesFiltradas
			.Where(u => !string.IsNullOrWhiteSpace(u.Ubicacion) && 
			           u.Ubicacion.StartsWith("UB", StringComparison.OrdinalIgnoreCase))
			.ToList();

		// Crear grupo de ubicaciones especiales si hay alguna
		if (ubicacionesEspeciales.Any())
		{
			var grupoEspecial = new UbicacionPasilloGroup
			{
				EsEspecial = true,
				Pasillo = null,
				IsExpanded = true, // Las ubicaciones especiales siempre expandidas
				Ubicaciones = new ObservableCollection<UbicacionDetalladaDto>(ubicacionesEspeciales)
			};
			UbicacionesAgrupadas.Add(grupoEspecial);
		}

		// Agrupar ubicaciones normales por pasillo
		var grupos = ubicacionesNormales
			.GroupBy(u => u.Pasillo)
			.OrderBy(g => g.Key ?? int.MaxValue)
			.Select(g =>
			{
				var ubicacionesGrupo = g.OrderBy(u => u.Estanteria).ThenBy(u => u.Altura).ThenBy(u => u.Posicion).ToList();
				var grupo = new UbicacionPasilloGroup
				{
					EsEspecial = false,
					Pasillo = g.Key,
					IsExpanded = false, // Los pasillos colapsados por defecto
					Ubicaciones = new ObservableCollection<UbicacionDetalladaDto>(ubicacionesGrupo)
				};
				// Cargar estanterías y alturas disponibles de este pasillo
				grupo.EstanteriasDisponibles = new ObservableCollection<int?>(
					ubicacionesGrupo.Select(u => u.Estanteria).Distinct().Where(e => e.HasValue).OrderBy(e => e));
				grupo.AlturasDisponibles = new ObservableCollection<int?>(
					ubicacionesGrupo.Select(u => u.Altura).Distinct().Where(a => a.HasValue).OrderBy(a => a));
				return grupo;
			});

		foreach (var grupo in grupos)
		{
			UbicacionesAgrupadas.Add(grupo);
		}

		AlturasDisponibles.Clear();
		foreach (var alt in Ubicaciones
							.Select(u => u.Altura)
							.Distinct()
							.Where(a => a.HasValue)
							.OrderBy(a => a))
		{
			AlturasDisponibles.Add(alt);
		}

		PasillosDisponibles.Clear();
		foreach (var pas in Ubicaciones
							.Select(u => u.Pasillo)
							.Distinct()
							.Where(p => p.HasValue)
							.OrderBy(p => p))
		{
			PasillosDisponibles.Add(pas);
		}

		EstanteriasDisponibles.Clear();
		foreach (var est in Ubicaciones
							.Select(u => u.Estanteria)
							.Distinct()
							.Where(e => e.HasValue)
							.OrderBy(e => e))
		{
			EstanteriasDisponibles.Add(est);
		}

		SelectedUbicacion = Ubicaciones.FirstOrDefault();
		RecalcularSeleccion();
		OnPropertyChanged(nameof(TotalUbicaciones));
		OnPropertyChanged(nameof(UbicacionesAgrupadas));
		}
		finally
		{
			IsBusy = false;
		}
	}

	public async Task LoadAlergenosAsync(UbicacionDetalladaDto dto)
	{
		if (dto.AlergenosPresentesList.Any()) return;

		var empresa = SessionManager.EmpresaSeleccionada!.Value;
		var almacen = SelectedAlmacenCombo!.CodigoAlmacen;
		var ubic = dto.Ubicacion;

		List<AlergenoDto> presentes;
		try
		{
			//Presentes
			presentes = await _ubicService
				.ObtenerAlergenosPresentesAsync(empresa, almacen, ubic);
			// Permitidos
			var permitidos = await _ubicService.ObtenerAlergenosPermitidosAsync(empresa, almacen, ubic);
			dto.AlergenosPermitidosList.Clear();
			foreach (var a in permitidos)
				dto.AlergenosPermitidosList.Add(a);
			// (Opcional) recalcula el riesgo:
			dto.RiesgoContaminacion = dto.AlergenosPresentesList
				.Any(p => !dto.AlergenosPermitidosList.Any(q => q.Codigo == p.Codigo));
		}
		catch (HttpRequestException ex)
		{
			Debug.WriteLine($"HTTP error cargando presentes: {ex.Message}");
			presentes = new List<AlergenoDto>();
		}

		dto.AlergenosPresentesList.Clear();
		foreach (var a in presentes)
			dto.AlergenosPresentesList.Add(a);

		// (igual para permitidos si lo necesitas)
	}
	private void OpenCrearUbicacionDialog(AlmacenDto almacen)
	{
		// Recupera la empresa seleccionada del SessionManager
		var empresa = SessionManager.EmpresaSeleccionada!.Value;

		// 3) Instancia del VM de diálogo
		var dialogVm = new UbicacionDialogViewModel(
			_ubicService, 
			_paletService,// tu servicio inyectado en este VM
			empresa,              // CódigoEmpresa
			almacen.CodigoAlmacen // CódigoAlmacen
								  // el cuarto parámetro es 'existing' y al no pasarlo, será null => modo Crear
		);

		// 4) Instancia de la ventana
		var dlg = new UbicacionDialogWindow
		{
			DataContext = dialogVm
		};
		var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
             ?? Application.Current.MainWindow;
		if (owner != null && owner != dlg)
			dlg.Owner = owner;

		// 5) Mostrar y, si OK, recargar la lista
		if (dlg.ShowDialog() == true)
		{
			// recarga poste creación
			_ = LoadUbicacionesAsync(almacen.CodigoAlmacen);
		}
	}
	private void OpenEditarUbicacionDialog(UbicacionDetalladaDto dto)
	{
		if (dto == null) return;

		// Recupera la empresa y el almacén
		var empresa = SessionManager.EmpresaSeleccionada!.Value;
		var almacen = dto.CodigoAlmacen;

		// 1) VM del diálogo en modo edición (pasamos el DTO existente)
		var dialogVm = new UbicacionDialogViewModel(
			_ubicService,
			_paletService,
			empresa,
			almacen,
			existing: dto
		);

		// 2) Ventana del diálogo
		var dlg = new UbicacionDialogWindow
		{
			DataContext = dialogVm
		};
		var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
             ?? Application.Current.MainWindow;
		if (owner != null && owner != dlg)
			dlg.Owner = owner;

		// 3) Mostrar modal y, al volver true, recargar la lista
		if (dlg.ShowDialog() == true)
		{
			_ = LoadUbicacionesAsync(almacen);
		}
	}
	private void OpenMasivoDialog(AlmacenDto almacen)
	{
		if (almacen == null) return;

		// Pasa el CódigoAlmacen al constructor
		var dlg = new UbicacionMasivoDialog(almacen);
		var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
             ?? Application.Current.MainWindow;
		if (owner != null && owner != dlg)
			dlg.Owner = owner;

		// Si tu diálogo devuelve true al cerrar, recarga la lista
		if (dlg.ShowDialog() == true)
		{
			_ = LoadUbicacionesAsync(almacen.CodigoAlmacen);
		}
	}

	private void OpenEditarMasivoDialog()
	{
		// Obtener ubicaciones seleccionadas
		var seleccionadas = Ubicaciones.Where(u => u.IsMarcada).ToList();
		if (!seleccionadas.Any())
		{
			MessageBox.Show("No hay ubicaciones seleccionadas para editar.",
				"Edición masiva", MessageBoxButton.OK, MessageBoxImage.Information);
			return;
		}

		// Verificar que todas sean del mismo almacén
		var almacen = seleccionadas.First().CodigoAlmacen;
		if (seleccionadas.Any(u => u.CodigoAlmacen != almacen))
		{
			MessageBox.Show("Todas las ubicaciones seleccionadas deben ser del mismo almacén.",
				"Edición masiva", MessageBoxButton.OK, MessageBoxImage.Warning);
			return;
		}

		var empresa = SessionManager.EmpresaSeleccionada!.Value;

		// Crear y mostrar diálogo
		var dlg = new EditarUbicacionesMasivoDialog(
			seleccionadas,
			_ubicService,
			_paletService,
			empresa);

		var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
			?? Application.Current.MainWindow;
		if (owner != null && owner != dlg)
			dlg.Owner = owner;

		// Si se guardó correctamente, recargar la lista
		if (dlg.ShowDialog() == true)
		{
			_ = LoadUbicacionesAsync(almacen);
		}
	}

	private async Task LoadImpresorasAsync()
	{
		// Si la aplicación se está cerrando, no cargar impresoras
		if (SessionManager.IsClosing)
			return;

		try
		{
			var lista = await _printService.ObtenerImpresorasAsync();
			ImpresorasDisponibles.Clear();
			foreach (var imp in lista.OrderBy(x => x.Nombre))
				ImpresorasDisponibles.Add(imp);
		}
		catch (Exception ex)
		{
			// Solo mostrar el diálogo si la aplicación no se está cerrando
			if (!SessionManager.IsClosing)
			{
				MessageBox.Show(
					$"Error al cargar impresoras: {ex.Message}",
					"Error de impresoras",
					MessageBoxButton.OK,
					MessageBoxImage.Error);
			}
		}
	}

	private bool FiltroUbicacion(object obj)
	{
		if (obj is not UbicacionDetalladaDto ubicacion) return false;
		
		// Si no queremos mostrar obsoletas, excluirlas
		if (!MostrarObsoletas && ubicacion.Obsoleta == 1) return false;
		
		if (string.IsNullOrWhiteSpace(FiltroBusqueda)) return true;

		return (ubicacion.Ubicacion?.Contains(FiltroBusqueda, StringComparison.OrdinalIgnoreCase) ?? false) ||
			   (ubicacion.DescripcionUbicacion?.Contains(FiltroBusqueda, StringComparison.OrdinalIgnoreCase) ?? false) ||
			   (ubicacion.TipoUbicacionDescripcion?.Contains(FiltroBusqueda, StringComparison.OrdinalIgnoreCase) ?? false) ||
			   (ubicacion.Pasillo?.ToString().Contains(FiltroBusqueda, StringComparison.OrdinalIgnoreCase) ?? false) ||
			   (ubicacion.Estanteria?.ToString().Contains(FiltroBusqueda, StringComparison.OrdinalIgnoreCase) ?? false) ||
			   (ubicacion.Altura?.ToString().Contains(FiltroBusqueda, StringComparison.OrdinalIgnoreCase) ?? false) ||
			   (ubicacion.Posicion?.ToString().Contains(FiltroBusqueda, StringComparison.OrdinalIgnoreCase) ?? false);
	}
	
	// Método para manejar cambios en mostrar obsoletas
	partial void OnMostrarObsoletasChanged(bool value)
	{
		if (SelectedAlmacenCombo != null)
		{
			_ = LoadUbicacionesAsync(SelectedAlmacenCombo.CodigoAlmacen);
		}
	}
	
	// Método para reagrupar ubicaciones cuando cambia el filtro
	private void ReagruparUbicaciones()
	{
		UbicacionesAgrupadas.Clear();
		
		// Aplicar filtro antes de agrupar
		var ubicacionesFiltradas = Ubicaciones.Where(u => FiltroUbicacion(u)).ToList();

		// Separar ubicaciones especiales (que no empiezan por "UB" o son cadena vacía)
		var ubicacionesEspeciales = ubicacionesFiltradas
			.Where(u => string.IsNullOrWhiteSpace(u.Ubicacion) || 
			           !u.Ubicacion.StartsWith("UB", StringComparison.OrdinalIgnoreCase))
			.OrderBy(u => u.Ubicacion)
			.ToList();

		// Ubicaciones normales (que empiezan por "UB")
		var ubicacionesNormales = ubicacionesFiltradas
			.Where(u => !string.IsNullOrWhiteSpace(u.Ubicacion) && 
			           u.Ubicacion.StartsWith("UB", StringComparison.OrdinalIgnoreCase))
			.ToList();

		// Crear grupo de ubicaciones especiales si hay alguna
		if (ubicacionesEspeciales.Any())
		{
			var grupoEspecial = new UbicacionPasilloGroup
			{
				EsEspecial = true,
				Pasillo = null,
				IsExpanded = true, // Las ubicaciones especiales siempre expandidas
				Ubicaciones = new ObservableCollection<UbicacionDetalladaDto>(ubicacionesEspeciales)
			};
			// Cargar estanterías y alturas disponibles del grupo especial
			grupoEspecial.EstanteriasDisponibles = new ObservableCollection<int?>(
				ubicacionesEspeciales.Select(u => u.Estanteria).Distinct().Where(e => e.HasValue).OrderBy(e => e));
			grupoEspecial.AlturasDisponibles = new ObservableCollection<int?>(
				ubicacionesEspeciales.Select(u => u.Altura).Distinct().Where(a => a.HasValue).OrderBy(a => a));
			UbicacionesAgrupadas.Add(grupoEspecial);
	}
	
		// Agrupar ubicaciones normales por pasillo
		var grupos = ubicacionesNormales
			.GroupBy(u => u.Pasillo)
			.OrderBy(g => g.Key ?? int.MaxValue)
			.Select(g =>
			{
				var ubicacionesGrupo = g.OrderBy(u => u.Estanteria).ThenBy(u => u.Altura).ThenBy(u => u.Posicion).ToList();
				var grupo = new UbicacionPasilloGroup
				{
					EsEspecial = false,
					Pasillo = g.Key,
					IsExpanded = false, // Los pasillos colapsados por defecto
					Ubicaciones = new ObservableCollection<UbicacionDetalladaDto>(ubicacionesGrupo)
				};
				// Cargar estanterías y alturas disponibles de este pasillo
				grupo.EstanteriasDisponibles = new ObservableCollection<int?>(
					ubicacionesGrupo.Select(u => u.Estanteria).Distinct().Where(e => e.HasValue).OrderBy(e => e));
				grupo.AlturasDisponibles = new ObservableCollection<int?>(
					ubicacionesGrupo.Select(u => u.Altura).Distinct().Where(a => a.HasValue).OrderBy(a => a));
				return grupo;
			});

		foreach (var grupo in grupos)
		{
			UbicacionesAgrupadas.Add(grupo);
		}
		
		OnPropertyChanged(nameof(UbicacionesAgrupadas));
		OnPropertyChanged(nameof(TotalUbicaciones));
	}

}
