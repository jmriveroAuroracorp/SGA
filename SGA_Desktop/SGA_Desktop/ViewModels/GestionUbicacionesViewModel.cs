using CommunityToolkit.Mvvm.ComponentModel;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using System.Collections.ObjectModel;

public partial class GestionUbicacionesViewModel : ObservableObject
{
	private readonly StockService _stockService;
	private readonly UbicacionesService _ubicService;

	public ObservableCollection<AlmacenDto> AlmacenesCombo { get; }
		= new ObservableCollection<AlmacenDto>();
	[ObservableProperty] private AlmacenDto? selectedAlmacenCombo;

	public ObservableCollection<UbicacionDetalladaDto> Ubicaciones { get; }
		= new ObservableCollection<UbicacionDetalladaDto>();
	[ObservableProperty] private UbicacionDetalladaDto? selectedUbicacion;

	public GestionUbicacionesViewModel()
		: this(new StockService(), new UbicacionesService()) { }

	public GestionUbicacionesViewModel(
		StockService stockService,
		UbicacionesService ubicService)
	{
		_stockService = stockService;
		_ubicService = ubicService;
		_ = InitializeAsync();
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

		// 3) Llamamos al endpoint de DETALLE
		var lista = await _ubicService
			.ObtenerUbicacionesDetalladasAsync(empresa, almacen);

		foreach (var dto in lista)
			Ubicaciones.Add(dto);

		SelectedUbicacion = Ubicaciones.FirstOrDefault();
	}
}
