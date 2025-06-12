using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SGA_Desktop.ViewModels
{
    public partial class ConsultaStockViewModel : ObservableObject
    {
        private readonly StockService _stockService;

        // ----------------------
        // CONSTRUCTORES
        // ----------------------

        // Constructor principal (inyección de StockService)
        public ConsultaStockViewModel(StockService stockService)
        {
            _stockService    = stockService;
            Almacenes        = new ObservableCollection<string>();
            ResultadosStock  = new ObservableCollection<StockDto>();

            // Inicializa filtros y cabecera
            FiltroArticulo   = string.Empty;
            FiltroUbicacion  = string.Empty;
            FiltroPartida    = string.Empty;
            EmpresaActual    = ObtenerNombreEmpresaActual();

			if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
			{
				_ = InitializeAsync();
			}
		}

        // Constructor sin parámetros (para XAML/code-behind)
        public ConsultaStockViewModel()
            : this(new StockService())
        { }

        // ----------------------
        // PROPIEDADES BOUND
        // ----------------------

        /// <summary>Cabecera: “[Código] Nombre” de la empresa.</summary>
        [ObservableProperty]
        private string empresaActual = string.Empty;

        /// <summary>Lista de almacenes para el ComboBox.</summary>
        public ObservableCollection<string> Almacenes { get; }

        [ObservableProperty]
        private string? almacenSeleccionado;

        [ObservableProperty]
        private string filtroArticulo = string.Empty;

		// Indica cuándo hay texto en Artículo
		public bool CanEnableInputs =>
			!string.IsNullOrWhiteSpace(FiltroArticulo);

		// Asegúrate de notificar cuando cambie FiltroArticulo:
		partial void OnFiltroArticuloChanged(string oldValue, string newValue)
		{
			// Fource reevaluación de CanEnableInputs
			OnPropertyChanged(nameof(CanEnableInputs));
		}

		[ObservableProperty]
        private string filtroUbicacion = string.Empty;

        [ObservableProperty]
        private string filtroPartida = string.Empty;
		[ObservableProperty]
		private string articuloMostrado = string.Empty;

		/// <summary>Resultados que muestra el DataGrid.</summary>
		public ObservableCollection<StockDto> ResultadosStock { get; }

		// ----------------------
		// COMANDOS
		// ----------------------

		/// <summary>Lanza la consulta al endpoint de stock con los filtros y la empresa/almacén seleccionados.</summary>
		[RelayCommand]
		private async Task BuscarStockAsync()
		{
			try
			{
				// 1) Llamada al servicio
				var json = await _stockService.ConsultaStockRawAsync(
					codigoEmpresa: SessionManager.EmpresaSeleccionada!.Value,
					codigoUbicacion: FiltroUbicacion,
					codigoAlmacen: AlmacenSeleccionado == "Todos" ? string.Empty : AlmacenSeleccionado!,
					codigoArticulo: FiltroArticulo,
					codigoCentro: SessionManager.UsuarioActual!.codigoCentro,
					almacen: AlmacenSeleccionado == "Todos" ? string.Empty : AlmacenSeleccionado!,
					partida: FiltroPartida);

				var lista = JsonConvert
					.DeserializeObject<List<StockDto>>(json)
					?? new List<StockDto>();

				// 2) Combina los almacenes que cargaste por centro + los del login
				var desdeCentro = Almacenes;
				var desdeLogin = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();
				var permitidos = desdeCentro.Concat(desdeLogin).Distinct().ToList();

				// 3) Si seleccionaste “Todos”, mantén el conjunto completo;
				//    si no, solo el único seleccionado
				if (AlmacenSeleccionado != "Todos")
					permitidos = new List<string> { AlmacenSeleccionado! };

				// 4) Filtra la lista por esos códigos combinados
				var filtrada = lista.Where(s => permitidos.Contains(s.CodigoAlmacen)).ToList();

				// 5) Asigna ArticuloMostrado: descripción si existe, sino el código
				var primero = filtrada.FirstOrDefault();
				ArticuloMostrado = primero?.DescripcionArticulo
								   ?? primero?.CodigoArticulo
								   ?? string.Empty;

				// 6) Rellena el DataGrid
				ResultadosStock.Clear();
				foreach (var item in filtrada)
					ResultadosStock.Add(item);
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message, "Error al consultar Stock",
								MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}



			// ----------------------
			// MÉTODOS PRIVADOS
			// ----------------------

			/// <summary>Lee el nombre de la empresa actual desde SessionManager.UsuarioActual.empresas.</summary>
			private string ObtenerNombreEmpresaActual()
        {
            var code = SessionManager.EmpresaSeleccionada;
            var dto  = SessionManager.UsuarioActual?.empresas
                         .FirstOrDefault(e => e.Codigo == code);
            return dto != null ? $"{dto.Nombre}" : $"[{code}]";
        }

		/// <summary>Inicializa la lista de almacenes a partir del códigoCentro del usuario.</summary>
		private async Task InitializeAsync()
		{
			try
			{
				// 1) Carga desde el centro logístico
				var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
				var desdeCentro = await _stockService.ObtenerAlmacenesAsync(centro); // List<string>

				// 2) Toma los permisos individuales del login
				var desdeLogin = SessionManager.UsuarioActual?.codigosAlmacen
								 ?? new List<string>();

				// 3) Une ambas listas y elimina duplicados
				var todosCodigos = desdeCentro
					.Concat(desdeLogin)
					.Distinct()
					.OrderBy(c => c)     // opcional: orden alfabético
					.ToList();

				// 4) Limpia y rellena tu ObservableCollection
				Almacenes.Clear();

				// Inserta “Todos” al principio
				Almacenes.Add("Todos");

				foreach (var codigo in todosCodigos)
					Almacenes.Add(codigo);

				// 5) Pre‐selecciona “Todos”
				AlmacenSeleccionado = "Todos";
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message, "Error cargando almacenes",
								MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}


	}
}
