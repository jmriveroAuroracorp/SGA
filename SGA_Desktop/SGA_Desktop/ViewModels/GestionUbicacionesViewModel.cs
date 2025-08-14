using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Dialog;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using SGA_Desktop.ViewModels;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;


public partial class GestionUbicacionesViewModel : ObservableObject
{
	private readonly StockService _stockService;
	private readonly UbicacionesService _ubicService;
	private readonly PaletService _paletService;
	private readonly PrintQueueService _printService;

	public ObservableCollection<AlmacenDto> AlmacenesCombo { get; }
		= new ObservableCollection<AlmacenDto>();
	[ObservableProperty] private AlmacenDto? selectedAlmacenCombo;

	public ObservableCollection<UbicacionDetalladaDto> Ubicaciones { get; }
		= new ObservableCollection<UbicacionDetalladaDto>();
	[ObservableProperty] private UbicacionDetalladaDto? selectedUbicacion;

	public ObservableCollection<ImpresoraDto> ImpresorasDisponibles { get; } = new();

	[ObservableProperty] private string? errorMessage;

	public GestionUbicacionesViewModel()
	: this(new StockService(), new UbicacionesService(), new PaletService())
	{ }

	/// <summary>Comando que carga los alérgenos de una ubicación.</summary>
	public IAsyncRelayCommand<UbicacionDetalladaDto> LoadAlergenosCommand { get; }

	public IRelayCommand CreateUbicacionCommand { get; }
	public IRelayCommand<UbicacionDetalladaDto> EditarUbicacionCommand { get; }
	public IRelayCommand<AlmacenDto> OpenMasivoCommand { get; }

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
		)
		{ Owner = Application.Current.MainWindow };
		if (confirm.ShowDialog() != true) return;

		// Abrimos diálogo de impresión
		var dlgVm = new ConfirmarImpresionDialogViewModel(
			ImpresorasDisponibles,
			ImpresorasDisponibles.FirstOrDefault());

		var dlg = new ConfirmarImpresionDialog
		{
			DataContext = dlgVm
		};
		var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
			 ?? Application.Current.MainWindow;
		if (owner != null && owner != dlg)
			dlg.Owner = owner;

		if (dlg.ShowDialog() != true) return;

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
		_ = LoadImpresorasAsync();

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
			Ubicaciones.Add(dto);
		}

		SelectedUbicacion = Ubicaciones.FirstOrDefault();
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
		try
		{
			var lista = await _printService.ObtenerImpresorasAsync();
			ImpresorasDisponibles.Clear();
			foreach (var imp in lista.OrderBy(x => x.Nombre))
				ImpresorasDisponibles.Add(imp);
		}
		catch (Exception ex)
		{
			MessageBox.Show(
				$"Error al cargar impresoras: {ex.Message}",
				"Error de impresoras",
				MessageBoxButton.OK,
				MessageBoxImage.Error);
		}
	}


}
