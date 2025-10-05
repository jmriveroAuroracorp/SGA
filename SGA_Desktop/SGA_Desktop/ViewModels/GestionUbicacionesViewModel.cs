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
	private int seleccionadasCount;

	[ObservableProperty]
	private bool isBusy;

	public ObservableCollection<int?> AlturasDisponibles { get; } = new();
	[ObservableProperty] private int? alturaSeleccionada;

	[RelayCommand]
	private void SeleccionarPorAltura(int? altura)
	{
		if (altura == null) return;
		foreach (var u in Ubicaciones)
			u.IsMarcada = u.Altura == altura;
		RecalcularSeleccion();
	}
	private void RecalcularSeleccion()
	{
		SeleccionadasCount = Ubicaciones.Count(u => u.IsMarcada);
		HaySeleccion = SeleccionadasCount > 0;
	}

	/// <summary>Comando que carga los alérgenos de una ubicación.</summary>
	public IAsyncRelayCommand<UbicacionDetalladaDto> LoadAlergenosCommand { get; }

	public IRelayCommand CreateUbicacionCommand { get; }
	public IRelayCommand<UbicacionDetalladaDto> EditarUbicacionCommand { get; }
	public IRelayCommand<AlmacenDto> OpenMasivoCommand { get; }
	public IRelayCommand RefrescarCommand { get; }

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
			_loginService ?? new LoginService()
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
				Usuario = SessionManager.Operario.ToString(),
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
		
		// Inicializar CollectionView para filtrado
		UbicacionesView = CollectionViewSource.GetDefaultView(Ubicaciones);
		UbicacionesView.Filter = FiltroUbicacion;
		
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
		
		// Solo cargar impresoras si la aplicación no se está cerrando
		if (!SessionManager.IsClosing)
		{
			_ = LoadImpresorasAsync();
		}

	}

	[RelayCommand]
	private void SeleccionarTodo()
	{
		foreach (var u in Ubicaciones)
			u.IsMarcada = true;
		RecalcularSeleccion();
	}

	[RelayCommand]
	private void LimpiarSeleccion()
	{
		foreach (var u in Ubicaciones)
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
			_loginService ?? new LoginService()
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
					Usuario = SessionManager.Operario.ToString(),
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
			.ObtenerAlmacenesAutorizadosAsync(empresa, centro, desdeLogin);

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
		Ubicaciones.Clear();
		if (string.IsNullOrWhiteSpace(almacen)) return;

		var empresa = SessionManager.EmpresaSeleccionada!.Value;

		// Llama al endpoint ligero
		var listaBasica = await _ubicService
			.ObtenerUbicacionesBasicoAsync(empresa, almacen);

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
		AlturasDisponibles.Clear();
		foreach (var alt in Ubicaciones
							.Select(u => u.Altura)
							.Distinct()
							.OrderBy(a => a))
		{
			AlturasDisponibles.Add(alt);
		}
		SelectedUbicacion = Ubicaciones.FirstOrDefault();
		RecalcularSeleccion();
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
		if (string.IsNullOrWhiteSpace(FiltroBusqueda)) return true;

		return (ubicacion.Ubicacion?.Contains(FiltroBusqueda, StringComparison.OrdinalIgnoreCase) ?? false) ||
			   (ubicacion.DescripcionUbicacion?.Contains(FiltroBusqueda, StringComparison.OrdinalIgnoreCase) ?? false) ||
			   (ubicacion.TipoUbicacionDescripcion?.Contains(FiltroBusqueda, StringComparison.OrdinalIgnoreCase) ?? false) ||
			   (ubicacion.Pasillo?.ToString().Contains(FiltroBusqueda, StringComparison.OrdinalIgnoreCase) ?? false) ||
			   (ubicacion.Estanteria?.ToString().Contains(FiltroBusqueda, StringComparison.OrdinalIgnoreCase) ?? false) ||
			   (ubicacion.Altura?.ToString().Contains(FiltroBusqueda, StringComparison.OrdinalIgnoreCase) ?? false) ||
			   (ubicacion.Posicion?.ToString().Contains(FiltroBusqueda, StringComparison.OrdinalIgnoreCase) ?? false);
	}


}
