using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using SGA_Desktop.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using SGA_Desktop.Dialog;

namespace SGA_Desktop.ViewModels
{
    public class ArticuloStockGroup
    {
        public string CodigoArticulo { get; set; } = string.Empty;
        public string DescripcionArticulo { get; set; } = string.Empty;
        public ObservableCollection<StockDisponibleDto> Ubicaciones { get; set; } = new();
        public string HeaderArticulo => $"{CodigoArticulo} - {DescripcionArticulo}";
    }

    public partial class TraspasosStockViewModel : ObservableObject
    {
        private readonly StockService _stockService;
        private readonly TraspasosService _traspasosService;
        private DateTime? _fechaUltimaBusqueda;

        public TraspasosStockViewModel(StockService stockService, TraspasosService traspasosService)
        {
            _stockService = stockService;
            _traspasosService = traspasosService;
            ArticulosConUbicaciones = new ObservableCollection<ArticuloStockGroup>();
            UltimosTraspasos = new ObservableCollection<TraspasoArticuloDto>();
            AlmacenesDestino = new ObservableCollection<string>();
            UbicacionesDestino = new ObservableCollection<string>();
        }

        // Buscador de artículo
        [ObservableProperty]
        private string articuloBuscado;

        // Siempre usaremos los cards agrupados
        public ObservableCollection<ArticuloStockGroup> ArticulosConUbicaciones { get; } = new();
        [ObservableProperty]
        private StockDisponibleDto? stockSeleccionado;

        // Formulario de traspaso
        [ObservableProperty]
        private string? almacenDestino;
        [ObservableProperty]
        private string? ubicacionDestino;
        public ObservableCollection<string> AlmacenesDestino { get; }
        public ObservableCollection<string> UbicacionesDestino { get; }
        [ObservableProperty]
        private decimal cantidadMover;

        // Feedback y últimos traspasos
        [ObservableProperty]
        private string feedback;
        public ObservableCollection<TraspasoArticuloDto> UltimosTraspasos { get; }

        [ObservableProperty]
        private bool mostrarCardsAgrupados;

        [RelayCommand]
        public async Task BuscarStockAsync()
        {
            _fechaUltimaBusqueda = DateTime.Now;
            ArticulosConUbicaciones.Clear();
            if (string.IsNullOrWhiteSpace(ArticuloBuscado))
            {
                Feedback = "Introduce un código o descripción de artículo.";
                return;
            }
            try
            {
                // Nuevo: buscar stock disponible con Reservado y Disponible
                var stock = await _stockService.ObtenerStockDisponibleAsync(ArticuloBuscado, null);

                // Si no hay resultados, buscar por descripción
                if (stock == null || stock.Count == 0)
                {
                    stock = await _stockService.ObtenerStockDisponibleAsync(null, ArticuloBuscado);
                }

                if (stock.Count == 0)
                {
                    Feedback = "No hay stock para ese artículo.";
                    return;
                }

                // 🔷 NUEVA LÓGICA: Obtener todos los almacenes autorizados (individuales + centro)
                var almacenesAutorizados = await ObtenerAlmacenesAutorizadosAsync();
                
                // Filtrar por almacenes autorizados
                stock = stock.Where(x => almacenesAutorizados.Contains(x.CodigoAlmacen)).ToList();

                // Siempre agrupa por artículo
                var grupos = stock.GroupBy(x => new { x.CodigoArticulo, x.DescripcionArticulo })
                                  .Select(g => new ArticuloStockGroup
                                  {
                                      CodigoArticulo = g.Key.CodigoArticulo,
                                      DescripcionArticulo = g.Key.DescripcionArticulo,
                                      Ubicaciones = new ObservableCollection<StockDisponibleDto>(g.ToList())
                                  })
                                  .OrderBy(a => a.CodigoArticulo)
                                  .ToList();
                foreach (var g in grupos)
                    ArticulosConUbicaciones.Add(g);
                Feedback = string.Empty;
            }
            catch (Exception ex)
            {
                Feedback = $"Error al buscar stock: {ex.Message}";
            }
        }

        [RelayCommand]
        public void SeleccionarStock(StockDisponibleDto? seleccionado)
        {
            StockSeleccionado = seleccionado;
        }

        [RelayCommand]
        public async Task ConfirmarTraspasoAsync()
        {
            Feedback = string.Empty;
            if (StockSeleccionado == null)
            {
                Feedback = "Selecciona una línea de stock de origen.";
                return;
            }
            if (string.IsNullOrWhiteSpace(AlmacenDestino) || string.IsNullOrWhiteSpace(UbicacionDestino))
            {
                Feedback = "Selecciona almacén y ubicación destino.";
                return;
            }
            if (CantidadMover <= 0 || CantidadMover > StockSeleccionado.Disponible)
            {
                Feedback = $"Cantidad a mover no válida. Disponible real: {StockSeleccionado.Disponible}";
                return;
            }
            var resultado = await _traspasosService.CrearTraspasoArticuloAsync(new CrearTraspasoArticuloDto
            {
                AlmacenOrigen = StockSeleccionado.CodigoAlmacen,
                UbicacionOrigen = StockSeleccionado.Ubicacion,
                CodigoArticulo = StockSeleccionado.CodigoArticulo,
                Cantidad = CantidadMover,
                UsuarioId = SessionManager.UsuarioActual?.operario ?? 0,
                AlmacenDestino = AlmacenDestino,
                UbicacionDestino = UbicacionDestino,
                Finalizar = true
            });
            if (resultado.Success)
            {
                Feedback = "Traspaso realizado correctamente.";
                await BuscarStockAsync();
                await CargarUltimosTraspasosAsync();
            }
            else
            {
                Feedback = resultado.ErrorMessage ?? "Error al realizar el traspaso.";
            }
        }

        [RelayCommand]
        public async Task CargarUltimosTraspasosAsync()
        {
            UltimosTraspasos.Clear();
            var lista = await _traspasosService.GetUltimosTraspasosArticulosAsync();
            foreach (var t in lista)
                UltimosTraspasos.Add(t);
        }

        [RelayCommand]
        public async void AbrirDialogoTraspaso()
        {
            if (StockSeleccionado == null)
                return;

            // 🔷 NUEVA LÓGICA: Obtener todos los almacenes autorizados (individuales + centro)
            var almacenesAutorizados = await ObtenerAlmacenesAutorizadosAsync();
            
            var almacenesDto = await _stockService.ObtenerAlmacenesAutorizadosAsync(
                SessionManager.EmpresaSeleccionada!.Value, 
                SessionManager.UsuarioActual?.codigoCentro ?? "0", 
                almacenesAutorizados
            );
            
            var vm = new TraspasoStockDialogViewModel(StockSeleccionado, _traspasosService, _fechaUltimaBusqueda)
            {
                AlmacenesDestino = new ObservableCollection<AlmacenDto>(almacenesDto)
            };
            var dlg = new TraspasoStockDialog(vm);
            var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
            if (owner != null && owner != dlg)
                dlg.Owner = owner;
            // Suscribirse al cierre para refrescar si fue correcto
            vm.RequestClose += (ok) =>
            {
                dlg.DialogResult = ok;
                dlg.Close();
                if (ok)
                {
                    _ = BuscarStockAsync();
                    _ = CargarUltimosTraspasosAsync();
                }
            };
            dlg.ShowDialog();
        }

		[RelayCommand]
		public async Task AbrirDialogoRegularizacionMultipleAsync()
		{
			var vm = new RegularizacionMultipleDialogViewModel(_traspasosService, _stockService);
			await vm.InitializeAsync(); // <- Espera a que cargue datos antes de abrir la ventana

			var dlg = new SGA_Desktop.Dialog.RegularizacionMultipleDialog(vm);
			var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
					 ?? Application.Current.MainWindow;
			if (owner != null && owner != dlg)
				dlg.Owner = owner;
			dlg.ShowDialog();
		}

		[RelayCommand]
		public async Task VerHistorialAsync()
		{
			var vm = new TraspasoHistoricoDialogViewModel(_traspasosService);
			var dlg = new TraspasoHistoricoDialog(vm);
			var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
					 ?? Application.Current.MainWindow;
			if (owner != null && owner != dlg)
				dlg.Owner = owner;
			dlg.ShowDialog();
		}

        //  NUEVA FUNCIÓN: Obtener todos los almacenes autorizados (individuales + centro)
        private async Task<List<string>> ObtenerAlmacenesAutorizadosAsync()
        {
            var almacenesIndividuales = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();
            var centroLogistico = SessionManager.UsuarioActual?.codigoCentro ?? "0";

            // Si el usuario tiene almacenes individuales, incluir también los del centro
            if (almacenesIndividuales.Any())
            {
                // Obtener almacenes del centro logístico de forma asíncrona
                var almacenesCentro = await _stockService.ObtenerAlmacenesAsync(centroLogistico);
                
                // Combinar almacenes individuales + almacenes del centro
                var todosLosAlmacenes = new List<string>(almacenesIndividuales);
                todosLosAlmacenes.AddRange(almacenesCentro);
                
                // Eliminar duplicados
                return todosLosAlmacenes.Distinct().ToList();
            }
            else
            {
                // Si no tiene almacenes individuales, usar solo los del centro
                return await _stockService.ObtenerAlmacenesAsync(centroLogistico);
            }
        }
	}
} 