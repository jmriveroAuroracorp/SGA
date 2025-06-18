using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Dialog;
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
			if (string.IsNullOrWhiteSpace(FiltroArticulo))
			{
				var advertencia = new WarningDialog(
					"Buscar artículo",
					"Debes introducir un código o descripción para buscar.",
					"\uE814" // ícono advertencia
				)
				{ Owner = Application.Current.MainWindow };

				advertencia.ShowDialog();
				return;
			}
			// 1) Primero intenta búsqueda por código
			var lista = await _stockService.ObtenerPorArticuloAsync(
				SessionManager.EmpresaSeleccionada!.Value,
				codigoArticulo: string.IsNullOrWhiteSpace(FiltroArticulo) ? null : FiltroArticulo,
				descripcion: null);

			// 2) Si no encuentra, intenta búsqueda por descripción
			if (!lista.Any())
			{
				lista = await _stockService.ObtenerPorArticuloAsync(
					SessionManager.EmpresaSeleccionada!.Value,
					codigoArticulo: null,
					descripcion: string.IsNullOrWhiteSpace(FiltroArticulo) ? null : FiltroArticulo);
			}

			// 3) Obtén permisos del usuario
			var permisos = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();

			if (!permisos.Any())
			{
				var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
				permisos = await _stockService.ObtenerAlmacenesAsync(centro);
			}

			// 4) Manejo si no hay permisos
			if (!permisos.Any())
			{
				MessageBox.Show(
					"No tienes permisos para ver existencias de este artículo.",
					"Sin permisos",
					MessageBoxButton.OK,
					MessageBoxImage.Warning);
				return;
			}

			// 5) Filtra según selección del almacén
			var filtrada = AlmacenSeleccionado == TODAS
				? lista.Where(x => permisos.Contains(x.CodigoAlmacen))
				: lista.Where(x => x.CodigoAlmacen == AlmacenSeleccionado);

			// 6) Llena los artículos
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
		if (SelectedArticulo == null)
		{
			var advertencia = new WarningDialog(
				"Artículo no seleccionado",
				"Debes seleccionar un artículo antes de imprimir.",
				"\uE814" // icono de advertencia (exclamación)
			)
			{ Owner = Application.Current.MainWindow };

			advertencia.ShowDialog();
			return;
		}


		var detalles =
	$"Artículo: {SelectedArticulo.CodigoArticulo} – {SelectedArticulo.DescripcionArticulo}\n" +
	$"Partida: {SelectedArticulo.Partida}\n" +
	$"Almacén: {SelectedArticulo.CodigoAlmacen} – {SelectedArticulo.Almacen}\n" +
	$"Caducidad: {SelectedArticulo.FechaCaducidad:dd/MM/yyyy}\n\n" +
	"¿Deseas continuar?";
		var dialog = new ConfirmationDialog(
			"Confirmar impresión",
			detalles,
			"\uE946"   // glyph de INFO
		)
		{ Owner = Application.Current.MainWindow };
		if (dialog.ShowDialog() != true)
			return;


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
