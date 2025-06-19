using CommunityToolkit.Mvvm.ComponentModel;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using System.Collections.ObjectModel;

public partial class GestionUbicacionesViewModel : ObservableObject
{
	private readonly StockService _stockService;
	private readonly UbicacionesService _ubicService;

	public GestionUbicacionesViewModel()
		: this(new StockService(), new UbicacionesService())
	{ }

	public GestionUbicacionesViewModel(
		StockService stockService,
		UbicacionesService ubicService)
	{
		_stockService = stockService;
		_ubicService = ubicService;

		Almacenes = new ObservableCollection<string>();
		Ubicaciones = new ObservableCollection<UbicacionDetalladaDto>();

		_ = InitializeAsync();
	}

	public ObservableCollection<string> Almacenes { get; }
	[ObservableProperty] private string selectedAlmacen;

	public ObservableCollection<UbicacionDetalladaDto> Ubicaciones { get; }

	private async Task InitializeAsync()
	{
		var desdeLogin = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();
		var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
		var desdeStock = await _stockService.ObtenerAlmacenesAsync(centro);

		var todos = desdeLogin.Concat(desdeStock)
							  .Distinct()
							  .OrderBy(x => x)
							  .ToList();

		Almacenes.Clear();
		foreach (var a in todos) Almacenes.Add(a);
		SelectedAlmacen = Almacenes.FirstOrDefault();
	}

	partial void OnSelectedAlmacenChanged(string almacen)
	{
		_ = LoadUbicacionesAsync(almacen);
	}

	private async Task LoadUbicacionesAsync(string almacen)
	{
		Ubicaciones.Clear();
		if (string.IsNullOrWhiteSpace(almacen)) return;

		// Llama a tu GET detallado:
		var empresa = SessionManager.EmpresaSeleccionada ?? 0;
		var lista = await _ubicService
			.ObtenerUbicacionesDetalladasAsync(empresa, almacen);

		foreach (var dto in lista)
			Ubicaciones.Add(dto);
	}
}
