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

namespace SGA_Desktop.ViewModels
{
	public partial class ConsultaStockViewModel : ObservableObject
	{
		#region Constants
		private const string SIN_UBICACION = "Sin ubicación";
		private const string TODAS = "Todas";
		#endregion


		#region Variables
		private bool _busquedaPorDescripcion;
		#endregion

		#region Fields & Services
		private readonly StockService _stockService;
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

			// ② Inicializa ambas colecciones
			ResultadosStock = new ObservableCollection<StockDto>();
			ResultadosStockPorUbicacion = new ObservableCollection<StockDto>();

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
		public ObservableCollection<ArticuloResumenDto> ArticulosUnicos { get; } = new();
		public ObservableCollection<StockDto> StockFiltrado { get; } = new();

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
		private bool isArticleMode;

		[ObservableProperty]
		private bool isLocationMode;

		[ObservableProperty]
		private string filtroDescripcion;

		[ObservableProperty]
		private string? almacenSeleccionadoArticulo;

		[ObservableProperty]
		private string? almacenSeleccionadoUbicacion;

		[ObservableProperty] 
		private ArticuloResumenDto? articuloSeleccionado;
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

		public Visibility ArticleFiltersVisibility => IsArticleMode ? Visibility.Visible : Visibility.Collapsed;
		public Visibility LocationFiltersVisibility => IsLocationMode ? Visibility.Visible : Visibility.Collapsed;

		public Visibility ArticulosUnicosVisibility =>
	_busquedaPorDescripcion && ArticulosUnicos.Count > 1
		? Visibility.Visible
		: Visibility.Collapsed;

		public Visibility ListViewVisibility =>
			(!_busquedaPorDescripcion || StockFiltrado.Any())
				? Visibility.Visible
				: Visibility.Collapsed;

		public IRelayCommand BuscarCommand =>
			IsArticleMode ? BuscarPorArticuloCommand : BuscarPorUbicacionCommand;
		#endregion

		#region Property Change Callbacks
		partial void OnFiltroArticuloChanged(string oldValue, string newValue)
		{
			OnPropertyChanged(nameof(CanEnableInputs));
			OnPropertyChanged(nameof(CanEnableLocation));
		}

		partial void OnAlmacenSeleccionadoChanged(string oldValue, string newValue)
		{
			OnPropertyChanged(nameof(CanEnableLocation));
			_ = LoadUbicacionesAsync(newValue);
		}


		partial void OnIsArticleModeChanged(bool oldValue, bool newValue)
		{
			if (newValue)
			{
				SwitchMode(resetFilters: false, setArticle: true);
			}
			OnPropertyChanged(nameof(BuscarCommand));
		}

		partial void OnIsLocationModeChanged(bool oldValue, bool newValue)
		{
			if (newValue)
			{
				SwitchMode(resetFilters: false, setArticle: false);
			}
			OnPropertyChanged(nameof(BuscarCommand));
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



		#endregion

		#region Commands
		//[RelayCommand]
		//private async Task BuscarPorArticuloAsync()
		//{
		//	try
		//	{
		//		var (almacenParam, ubicParam) = BuildArticleParams();
		//		var lista = await _stockService.ObtenerPorArticuloAsync(
		//			SessionManager.EmpresaSeleccionada!.Value,
		//			FiltroArticulo,
		//			string.IsNullOrWhiteSpace(FiltroPartida) ? null : FiltroPartida,
		//			almacenParam,
		//			ubicParam);
		//		LlenarResultados(lista, filterByPermissions: true);
		//	}
		//	catch (Exception ex)
		//	{
		//		MostrarError("Error al consultar por artículo", ex);
		//	}
		//}
		[RelayCommand]
		private void LimpiarFiltros()
		{
			if (IsArticleMode)
			{
				// Solo limpia filtros del modo artículo
				FiltroArticulo = string.Empty;
				FiltroPartida = string.Empty;
				AlmacenSeleccionado = TODAS;
		
			}
			else if (IsLocationMode)
			{
				// Solo limpia filtros del modo ubicación
				AlmacenSeleccionado = TODAS;
	
			}
		}


		// INTRODUCE BUSQUEDA POR ARTÍCULO
		//[RelayCommand]
		//private async Task BuscarPorArticuloAsync()
		//{
		//	try
		//	{
		//		if (string.IsNullOrWhiteSpace(FiltroArticulo))
		//		{
		//			var advertencia = new WarningDialog(
		//				"Buscar artículo",
		//				"Debes introducir un código o descripción para buscar.",
		//				"\uE814" // ícono advertencia
		//			)
		//			{ Owner = Application.Current.MainWindow };

		//			advertencia.ShowDialog();
		//			return;
		//		}
		//		var (almacenParam, ubicParam) = BuildArticleParams();

		//		List<StockDto> lista = await _stockService.ObtenerPorArticuloAsync(
		//			SessionManager.EmpresaSeleccionada!.Value,
		//			codigoArticulo: string.IsNullOrWhiteSpace(FiltroArticulo) ? null : FiltroArticulo,
		//			partida: string.IsNullOrWhiteSpace(FiltroPartida) ? null : FiltroPartida,
		//			codigoAlmacen: almacenParam,
		//			codigoUbicacion: ubicParam,
		//			descripcion: null // primero intentar por código, descripción null
		//		);

		//		if (lista == null || !lista.Any())
		//		{
		//			// Si no encontró por código, busca por descripción
		//			lista = await _stockService.ObtenerPorArticuloAsync(
		//				SessionManager.EmpresaSeleccionada!.Value,
		//				codigoArticulo: null, // ahora null
		//				partida: string.IsNullOrWhiteSpace(FiltroPartida) ? null : FiltroPartida,
		//				codigoAlmacen: almacenParam,
		//				codigoUbicacion: ubicParam,
		//				descripcion: string.IsNullOrWhiteSpace(FiltroArticulo) ? null : FiltroArticulo
		//			);
		//		}

		//		LlenarResultados(lista, filterByPermissions: true);
		//	}
		//	catch (Exception ex)
		//	{
		//		MostrarError("Error al consultar por artículo", ex);
		//	}
		//}


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
					)
					{ Owner = Application.Current.MainWindow };
					advertencia.ShowDialog();
					return;
				}

				// Limpiar estados previos
				ArticulosUnicos.Clear();
				StockFiltrado.Clear();
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
						partida: string.IsNullOrWhiteSpace(FiltroPartida) ? null : FiltroPartida,
						codigoAlmacen: almacenParam,
						codigoUbicacion: ubicParam,
						descripcion: FiltroArticulo
					);
				}

				// 3) Filtrar por permisos de almacén
				var permisos = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();
				if (!permisos.Any())
				{
					var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
					permisos = await _stockService.ObtenerAlmacenesAsync(centro);
				}
				lista = lista.Where(x => permisos.Contains(x.CodigoAlmacen)).ToList();

				// 4) Guardar todo el stock filtrado para detalle
				ResultadosStock.Clear();
				foreach (var s in lista)
					ResultadosStock.Add(s);

				// 5) Agrupar en artículos únicos
				var grupos = lista
					.GroupBy(x => new { x.CodigoArticulo, x.DescripcionArticulo })
					.Select(g => new ArticuloResumenDto
					{
						CodigoArticulo = g.Key.CodigoArticulo,
						DescripcionArticulo = g.Key.DescripcionArticulo
					})
					.OrderBy(a => a.CodigoArticulo)
					.ToList();

				// 6) Si no venimos de descripción o solo hay un artículo único,
				//    mostramos detalle directo; si no, llenamos el combo
				if (!_busquedaPorDescripcion || grupos.Count == 1)
				{
					// Mostrar directamente partidas/ubicaciones
					ArticuloMostrado = grupos.FirstOrDefault()?.DescripcionArticulo ?? string.Empty;

					StockFiltrado.Clear();
					foreach (var s in lista)
						StockFiltrado.Add(s);
				}
				else
				{
					// Mostrar lista de artículos únicos en el ComboBox
					ArticulosUnicos.Clear();
					foreach (var art in grupos)
						ArticulosUnicos.Add(art);
				}

				// 7) Actualizar visibilidades
				OnPropertyChanged(nameof(ArticuloMostrado));
				OnPropertyChanged(nameof(ArticulosUnicosVisibility));
				OnPropertyChanged(nameof(ListViewVisibility));
			}
			catch (Exception ex)
			{
				MostrarError("Error al consultar por artículo", ex);
			}
		}


		[RelayCommand]
		private async Task BuscarPorUbicacionAsync()
		{
			try
			{
				var lista = await _stockService.ObtenerPorUbicacionAsync(
					SessionManager.EmpresaSeleccionada!.Value,
					AlmacenSeleccionado,
					FiltroUbicacion == SIN_UBICACION ? string.Empty : FiltroUbicacion);
				LlenarResultados(lista, filterByPermissions: true);
			}
			catch (Exception ex)
			{
				MostrarError("Error al consultar por ubicación", ex);
			}
		}

		[RelayCommand]
		private void ExportarExcel()
		{
			// ▶️ Cambiado: exportamos StockFiltrado en modo artículo
			var listaActiva = IsArticleMode
				? StockFiltrado.ToList()
				: ResultadosStockPorUbicacion.ToList();

			if (!listaActiva.Any())
			{
				var advertencia = new WarningDialog(
					"Exportar Excel",
					"No hay datos para exportar.",
					"\uE814" // ícono de advertencia
				)
				{ Owner = Application.Current.MainWindow };
				advertencia.ShowDialog();
				return;
			}

			// 1) Confirmar con nuestro dialog
			var confirm = new ConfirmationDialog(
				"Confirmar exportación",
				$"Se van a exportar {listaActiva.Count} registros.\n¿Deseas continuar?",
				"\uE11B"    // ícono de pregunta
			)
			{ Owner = Application.Current.MainWindow };
			if (confirm.ShowDialog() != true)
				return;

			// 2) Diálogo para elegir fichero
			var dlg = new SaveFileDialog
			{
				Filter = "Libro de Excel (*.xlsx)|*.xlsx",
				FileName = IsArticleMode
					? "ConsultaPorArticulo.xlsx"
					: "ConsultaPorUbicacion.xlsx"
			};
			if (dlg.ShowDialog() != true) return;

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

			// 7) Guardar y aviso final
			wb.SaveAs(dlg.FileName);
			var info = new WarningDialog(
				"Exportación completada",
				$"Datos exportados correctamente a:\n{dlg.FileName}",
				"\uE946" // ícono de información
			)
			{ Owner = Application.Current.MainWindow };
			info.ShowDialog();
		}


		#endregion

		#region Initialization & Data Loading
		private async Task InitializeAsync()
		{
			try
			{
				var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
				var desdeCentro = await _stockService.ObtenerAlmacenesAsync(centro);
				var desdeLogin = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();

				var todosCodigos = desdeCentro.Concat(desdeLogin)
					.Distinct()
					.OrderBy(c => c)
					.ToList();

				Almacenes.Clear();
				Almacenes.Add(TODAS);
				todosCodigos.ForEach(c => Almacenes.Add(c));
				AlmacenSeleccionado = TODAS;
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

			var lista = await _stockService.ObtenerUbicacionesAsync(codigoAlmacen);
			if (IsArticleMode) Ubicaciones.Add(TODAS);
			lista.ForEach(u => Ubicaciones.Add(string.IsNullOrEmpty(u) ? SIN_UBICACION : u));

			FiltroUbicacion = IsArticleMode ? TODAS : Ubicaciones.FirstOrDefault();
		}
		#endregion

		#region Private Helpers
		private (string? almacenParam, string? ubicParam) BuildArticleParams()
		{
			string? almacenParam = AlmacenSeleccionado == TODAS ? null : AlmacenSeleccionado;
			string? ubicParam = null;

			if (almacenParam != null)
			{
				if (FiltroUbicacion == SIN_UBICACION) ubicParam = string.Empty;
				else if (FiltroUbicacion != TODAS) ubicParam = FiltroUbicacion;
			}

			return (almacenParam, ubicParam);
		}

		private void LlenarResultados(List<StockDto> lista, bool filterByPermissions)
		{
			var basePerm = filterByPermissions
				? SessionManager.UsuarioActual?.codigosAlmacen
				: null;

			var permitidos = filterByPermissions
				? (AlmacenSeleccionado == TODAS
					? Almacenes.Concat(basePerm ?? Enumerable.Empty<string>())
					: new[] { AlmacenSeleccionado })
				: lista.Select(s => s.CodigoAlmacen);

			var filtrada = lista
				.Where(s => permitidos.Contains(s.CodigoAlmacen))
				.ToList();

			if (IsArticleMode)
			{
				// en modo artículo actualiza ArticuloMostrado y clear/fill ResultadosStock
				ArticuloMostrado = filtrada
					.FirstOrDefault()?.DescripcionArticulo
					?? string.Empty;

				ResultadosStock.Clear();
				filtrada.ForEach(x => ResultadosStock.Add(x));
			}
			else
			{
				// en modo ubicación no mostramos destacado y clear/fill la otra colección
				ArticuloMostrado = string.Empty;

				ResultadosStockPorUbicacion.Clear();
				filtrada.ForEach(x => ResultadosStockPorUbicacion.Add(x));
			}
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
	}
}


///// <summary>Lanza la consulta al endpoint de stock con los filtros y la empresa/almacén seleccionados.</summary>
//[RelayCommand]
//private async Task BuscarStockAsync()
//{
//	try
//	{
//		// 0) Traducimos "Sin ubicación" -> "" para la llamada a la API
//		var ubicacionParaApi = FiltroUbicacion == SIN_UBICACION
//							  ? string.Empty
//							  : FiltroUbicacion;

//		// Log de los parámetros que se envían a la API
//		Console.WriteLine($"Llamada a la API - Empresa: {SessionManager.EmpresaSeleccionada!.Value}, Ubicación: '{ubicacionParaApi}', Almacén: '{(AlmacenSeleccionado == "Todos" ? string.Empty : AlmacenSeleccionado!)}', Artículo: '{FiltroArticulo}', Centro: '{SessionManager.UsuarioActual!.codigoCentro}', Partida: '{FiltroPartida}'");

//		// 1) Llamada al servicio usando el valor traducido
//		var json = await _stockService.ConsultaStockRawAsync(
//			codigoEmpresa: SessionManager.EmpresaSeleccionada!.Value,
//			codigoUbicacion: ubicacionParaApi,
//			codigoAlmacen: AlmacenSeleccionado == "Todos" ? string.Empty : AlmacenSeleccionado!,
//			codigoArticulo: FiltroArticulo,
//			codigoCentro: SessionManager.UsuarioActual!.codigoCentro,
//			almacen: AlmacenSeleccionado == "Todos" ? string.Empty : AlmacenSeleccionado!,
//			partida: FiltroPartida);

//		var lista = JsonConvert
//			.DeserializeObject<List<StockDto>>(json)
//			?? new List<StockDto>();

//		// 2) Combina los almacenes que cargaste por centro + los del login
//		var desdeCentro = Almacenes;
//		var desdeLogin = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();
//		var permitidos = desdeCentro.Concat(desdeLogin).Distinct().ToList();

//		// 3) Si seleccionaste “Todos”, mantén el conjunto completo;
//		//    si no, solo el único seleccionado
//		if (AlmacenSeleccionado != "Todos")
//			permitidos = new List<string> { AlmacenSeleccionado! };

//		// 4) Filtra la lista por esos códigos combinados
//		var filtrada = lista.Where(s => permitidos.Contains(s.CodigoAlmacen)).ToList();

//		// 5) Asigna ArticuloMostrado solo si estamos en modo de búsqueda por artículo
//		if (IsArticleMode)
//		{
//			var primero = filtrada.FirstOrDefault();
//			ArticuloMostrado = primero?.DescripcionArticulo
//							   ?? primero?.CodigoArticulo
//							   ?? string.Empty;
//		}
//		else
//		{
//			ArticuloMostrado = string.Empty; // Limpia el artículo mostrado si no es por artículo
//		}

//		// 6) Rellena el DataGrid
//		ResultadosStock.Clear();
//		foreach (var item in filtrada)
//			ResultadosStock.Add(item);
//	}
//	catch (Exception ex)
//	{
//		MessageBox.Show(ex.Message, "Error al consultar Stock",
//						MessageBoxButton.OK, MessageBoxImage.Error);
//	}
//}

