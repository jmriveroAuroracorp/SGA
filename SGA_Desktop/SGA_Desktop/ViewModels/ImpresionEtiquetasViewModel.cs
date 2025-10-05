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
	private readonly LoginService _loginService;    // ← añade esto

	// Ctor principal con DI
	public ImpresionEtiquetasViewModel(
		StockService stockService,
		PrintQueueService printService,
		LoginService loginService)       // ← y esto
	{
		_stockService = stockService;
		_printService = printService;
		_loginService = loginService;   // ← y esto

		Articulos = new ObservableCollection<StockDto>();
		Almacenes = new ObservableCollection<string>();
		Impresoras = new ObservableCollection<ImpresoraDto>();

		_ = InitializeAsync();
		
		// Solo cargar impresoras si la aplicación no se está cerrando
		if (!SessionManager.IsClosing)
		{
			_ = LoadImpresorasAsync();
		}
	}

	// Ctor sin parámetros para el diseñador / XAML
	public ImpresionEtiquetasViewModel()
		: this(new StockService(),
			   new PrintQueueService(),
			   new LoginService())          // ← aquí creas LoginService
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

	// Colección de impresoras
	public ObservableCollection<ImpresoraDto> Impresoras { get; }
		= new ObservableCollection<ImpresoraDto>();

	// Propiedad de la impresora seleccionada (añádela si aún no la tienes)
	[ObservableProperty]
	private ImpresoraDto? impresoraSeleccionada;

	partial void OnImpresoraSeleccionadaChanged(ImpresoraDto? nueva)
	{
		if (nueva is null) return;

		// 1) Guardar en sesión (opcional, para recargar en esta sesión)
		SessionManager.PreferredPrinter = nueva.Nombre;

		// 2) Llamar al servicio para que se escriba en la BD
		_ = _loginService
			.EstablecerImpresoraPreferidaAsync(SessionManager.Operario, nueva.Nombre)
			.ContinueWith(t =>
			{
				if (!t.Result.ok)
				{
					// Opcional: mostrar error si no guardó
					MessageBox.Show($"No se pudo guardar la impresora preferida:\n{t.Result.detalle}",
									"Error al guardar", MessageBoxButton.OK, MessageBoxImage.Warning);
				}
			}, TaskScheduler.FromCurrentSynchronizationContext());
	}
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

	/// <summary>
	/// Carga del endpoint la lista de impresoras y pre-selecciona la preferida.
	/// </summary>
	private async Task LoadImpresorasAsync()
	{
		// Si la aplicación se está cerrando, no cargar impresoras
		if (SessionManager.IsClosing)
			return;

		try
		{
			// 1) Obtén todas las impresoras
			var lista = await _printService.ObtenerImpresorasAsync();

			// 2) Actualiza la ObservableCollection
			Impresoras.Clear();
			foreach (var imp in lista.OrderBy(x => x.Nombre))
				Impresoras.Add(imp);

			// 3) Decide cuál seleccionar
			var nombrePref = SessionManager.PreferredPrinter;
			// Busca la impresora cuya propiedad Nombre coincida
			var preseleccion = Impresoras.FirstOrDefault(x => x.Nombre == nombrePref)
							  // Si no la encuentra, simplemente la primera de la lista
							  ?? Impresoras.FirstOrDefault();

			ImpresoraSeleccionada = preseleccion;
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
			IdImpresora = ImpresoraSeleccionada?.Id, 
			EtiquetaImpresa = 0,
			Copias = null,
			CodigoArticulo = SelectedArticulo.CodigoArticulo,
			DescripcionArticulo = SelectedArticulo.DescripcionArticulo ?? string.Empty,
			CodigoAlternativo = SelectedArticulo.CodigoAlternativo,
			FechaCaducidad = SelectedArticulo.FechaCaducidad,
			Partida = SelectedArticulo.Partida,
			Alergenos = alergenos,
			PathEtiqueta = "\\\\Sage200\\mrh\\Servicios\\PrintCenter\\ETIQUETAS\\MMPP_MES.nlbl",
			TipoEtiqueta = 1, // o 2 si quieres imprimir palet
			CodigoGS1 = null, // o el valor cuando sea tipo 2
			CodigoPalet = null // o el valor cuando sea tipo 2
		};
		
		try
		{
			// Inserción en la base de datos
			await _printService.InsertarRegistroImpresionAsync(dto);
			await _loginService.RegistrarLogEventoAsync(new LogEvento
			{
				fecha = DateTime.Now,
				idUsuario = SessionManager.Operario,
				tipo = "IMPRESION_ETIQUETA",
				origen = "ImpresionEtiquetasView",
				descripcion = $"Impresión de etiqueta artículo {dto.CodigoArticulo}",
				detalle = $"Copias={dto.Copias}, ImpresoraId={dto.IdImpresora}",
				idDispositivo = dto.Dispositivo
			});
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
