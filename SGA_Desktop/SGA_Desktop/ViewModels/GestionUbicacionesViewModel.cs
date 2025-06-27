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


public partial class GestionUbicacionesViewModel : ObservableObject
{
	private readonly StockService _stockService;
	private readonly UbicacionesService _ubicService;
	private readonly PaletService _paletService;

	public ObservableCollection<AlmacenDto> AlmacenesCombo { get; }
		= new ObservableCollection<AlmacenDto>();
	[ObservableProperty] private AlmacenDto? selectedAlmacenCombo;

	public ObservableCollection<UbicacionDetalladaDto> Ubicaciones { get; }
		= new ObservableCollection<UbicacionDetalladaDto>();
	[ObservableProperty] private UbicacionDetalladaDto? selectedUbicacion;

	public GestionUbicacionesViewModel()
	: this(new StockService(), new UbicacionesService(), new PaletService())
	{ }

	/// <summary>Comando que carga los alérgenos de una ubicación.</summary>
	public IAsyncRelayCommand<UbicacionDetalladaDto> LoadAlergenosCommand { get; }

	public IRelayCommand CreateUbicacionCommand { get; }
	public IRelayCommand<UbicacionDetalladaDto> EditarUbicacionCommand { get; }
	public IRelayCommand<AlmacenDto> OpenMasivoCommand { get; }


	public GestionUbicacionesViewModel(
		StockService stockService,
		UbicacionesService ubicService,
		PaletService paletService)
	{

		_stockService = stockService;
		_ubicService = ubicService;
		_paletService = paletService;
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
			DataContext = dialogVm,
			Owner = Application.Current.MainWindow
		};

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
			DataContext = dialogVm,
			Owner = Application.Current.MainWindow
		};

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
		var dlg = new UbicacionMasivoDialog(almacen)
		{
			Owner = Application.Current.MainWindow
		};

		// Si tu diálogo devuelve true al cerrar, recarga la lista
		if (dlg.ShowDialog() == true)
		{
			_ = LoadUbicacionesAsync(almacen.CodigoAlmacen);
		}
	}


}
