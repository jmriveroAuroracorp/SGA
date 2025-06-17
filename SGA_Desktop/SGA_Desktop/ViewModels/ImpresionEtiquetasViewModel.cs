using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using System.Collections.ObjectModel;
using System.Windows;

public partial class ImpresionEtiquetasViewModel : ObservableObject
{
	private const string TODAS = "Todas";
	private readonly StockService _stockService;
	private readonly PrintQueueService _printService;

	public ImpresionEtiquetasViewModel(StockService stockService, PrintQueueService printService)
	{
		_stockService = stockService;
		_printService = printService;

		Articulos = new ObservableCollection<StockDto>();
		Almacenes = new ObservableCollection<string>();

		// Arranca la carga de almacenes
		_ = InitializeAsync();
	}

	// Constructor sin parámetros para el designer
	public ImpresionEtiquetasViewModel()
		: this(new StockService(), new PrintQueueService())
	{ }

	// -------------------
	// Propiedades Bound
	// -------------------
	[ObservableProperty] private string filtroArticulo = string.Empty;
	public ObservableCollection<StockDto> Articulos { get; }
	[ObservableProperty] private StockDto? selectedArticulo;


	// Este combo de almacenes
	public ObservableCollection<string> Almacenes { get; }
	[ObservableProperty] private string almacenSeleccionado = TODAS;

	// -------------------
	// Comando de Búsqueda
	// -------------------
	[RelayCommand]
	private async Task BuscarArticuloAsync()
	{
		try
		{
			Articulos.Clear();

			// 1) Trae todo el stock
			var lista = await _stockService.ObtenerPorArticuloAsync(
				SessionManager.EmpresaSeleccionada!.Value,
				FiltroArticulo, null, null, null);

			// 2) Intenta permisos directos
			var permisos = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();

			// 3) Si no trae ninguno, descompón el centro
			if (!permisos.Any())
			{
				var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
				permisos = await _stockService.ObtenerAlmacenesAsync(centro);
			}

			// 4) Si tras el fallback sigue sin permisos, avisamos y salimos
			if (!permisos.Any())
			{
				MessageBox.Show(
					"No tienes permisos para ver existencias de este artículo.",
					"Sin permisos",
					MessageBoxButton.OK,
					MessageBoxImage.Warning);
				return;
			}

			// 5) Filtra según combo (Todas = todos los permisos)
			IEnumerable<StockDto> filtrada = AlmacenSeleccionado == TODAS
				? lista.Where(x => permisos.Contains(x.CodigoAlmacen))
				: lista.Where(x => x.CodigoAlmacen == AlmacenSeleccionado);

			// 6) Rellena grid y combo de lotes
			foreach (var art in filtrada)
				Articulos.Add(art);

		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.Message, "Error al buscar artículos",
							MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}




	[RelayCommand]
	private async Task ImprimirEtiquetaAsync()
	{
		if (SelectedArticulo is null) return;

		var alergenos = await _stockService.ObtenerAlergenosArticuloAsync(
	SessionManager.EmpresaSeleccionada!.Value,
	SelectedArticulo.CodigoArticulo);

		var dto = new LogImpresionDto
		{
			Usuario = SessionManager.Operario.ToString(),
			Dispositivo = Environment.MachineName,
			IdImpresora = 2, // Asegúrate de que este ID sea correcto
			EtiquetaImpresa = 1,
			Copias = null,
			CodigoArticulo = SelectedArticulo.CodigoArticulo,
			DescripcionArticulo = SelectedArticulo.DescripcionArticulo ?? string.Empty,
			CodigoAlternativo = SelectedArticulo.CodigoAlternativo,
			FechaCaducidad = SelectedArticulo.FechaCaducidad,
			Partida = SelectedArticulo.Partida,
			Alergenos = alergenos,
			PathEtiqueta = "\\\\Sage200\\mrh\\Servicios\\PrintCenter\\ETIQUETAS\\MMPP_MES.nlbl"
		};
		
		try
		{
			// Inserción en la base de datos
			await _printService.InsertarRegistroImpresionAsync(dto);
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.Message, "Error al encolar impresión", MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	// -------------------
	// Inicialización de Almacenes (idéntica a ConsultaStock)
	// -------------------
	private async Task InitializeAsync()
	{
		try
		{
			var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
			var desdeCentro = await _stockService.ObtenerAlmacenesAsync(centro);
			var desdeLogin = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();

			// Une ambas listas y quita duplicados
			var todosCodigos = desdeCentro
				.Concat(desdeLogin)
				.Distinct()
				.OrderBy(c => c)
				.ToList();

			Almacenes.Clear();
			Almacenes.Add(TODAS);
			todosCodigos.ForEach(c => Almacenes.Add(c));

			// Pre‐selección
			AlmacenSeleccionado = TODAS;
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.Message, "Error cargando almacenes", MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}
}
