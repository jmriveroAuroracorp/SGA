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
        public ObservableCollection<StockDto> Ubicaciones { get; set; } = new();
        public string HeaderArticulo => $"{CodigoArticulo} - {DescripcionArticulo}";
    }

    public partial class TraspasosStockViewModel : ObservableObject
    {
        private readonly StockService _stockService;
        private readonly TraspasosService _traspasosService;

        public TraspasosStockViewModel(StockService stockService, TraspasosService traspasosService)
        {
            _stockService = stockService;
            _traspasosService = traspasosService;
            ArticulosConUbicaciones = new ObservableCollection<ArticuloStockGroup>();
            StockDisponible = new ObservableCollection<StockDto>();
            UltimosTraspasos = new ObservableCollection<TraspasoArticuloDto>();
            AlmacenesDestino = new ObservableCollection<string>();
            UbicacionesDestino = new ObservableCollection<string>();
        }

        // Buscador de artículo
        [ObservableProperty]
        private string articuloBuscado;

        // Para cards agrupados
        public ObservableCollection<ArticuloStockGroup> ArticulosConUbicaciones { get; }
        // Para el caso de búsqueda directa por código
        public ObservableCollection<StockDto> StockDisponible { get; }
        [ObservableProperty]
        private StockDto? stockSeleccionado;

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
            StockDisponible.Clear();
            ArticulosConUbicaciones.Clear();
            MostrarCardsAgrupados = false;
            if (string.IsNullOrWhiteSpace(ArticuloBuscado))
            {
                Feedback = "Introduce un código o descripción de artículo.";
                return;
            }
            try
            {
                // 1) Intentar buscar por código
                var stock = await _stockService.ObtenerPorArticuloAsync(
                    SessionManager.EmpresaSeleccionada!.Value,
                    codigoArticulo: ArticuloBuscado,
                    descripcion: null);

                // 2) Si no hay resultados, buscar por descripción
                if (stock == null || stock.Count == 0)
                {
                    stock = await _stockService.ObtenerPorArticuloAsync(
                        SessionManager.EmpresaSeleccionada!.Value,
                        codigoArticulo: null,
                        descripcion: ArticuloBuscado);
                }

                if (stock.Count == 0)
                {
                    Feedback = "No hay stock para ese artículo.";
                    return;
                }
                // Filtrar por almacenes autorizados (igual que en ConsultaStockViewModel)
                var permisos = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();
                if (!permisos.Any())
                {
                    var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
                    permisos = await _stockService.ObtenerAlmacenesAsync(centro);
                }
                stock = stock.Where(x => permisos.Contains(x.CodigoAlmacen)).ToList();
                // ¿La búsqueda es por descripción y hay varios artículos distintos?
                var grupos = stock.GroupBy(x => new { x.CodigoArticulo, x.DescripcionArticulo })
                                  .Select(g => new ArticuloStockGroup
                                  {
                                      CodigoArticulo = g.Key.CodigoArticulo,
                                      DescripcionArticulo = g.Key.DescripcionArticulo,
                                      Ubicaciones = new ObservableCollection<StockDto>(g.ToList())
                                  })
                                  .OrderBy(a => a.CodigoArticulo)
                                  .ToList();
                if (grupos.Count > 1)
                {
                    foreach (var g in grupos)
                        ArticulosConUbicaciones.Add(g);
                    MostrarCardsAgrupados = true;
                }
                else
                {
                    foreach (var s in stock)
                        StockDisponible.Add(s);
                    MostrarCardsAgrupados = false;
                }
                Feedback = string.Empty;
            }
            catch (Exception ex)
            {
                Feedback = $"Error al buscar stock: {ex.Message}";
            }
        }

        [RelayCommand]
        public void SeleccionarStock(StockDto? seleccionado)
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
            if (CantidadMover <= 0 || CantidadMover > StockSeleccionado.UnidadSaldo)
            {
                Feedback = "Cantidad a mover no válida.";
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
        public void AbrirDialogoTraspaso()
        {
            if (StockSeleccionado == null)
                return;

            // Obtener almacenes destino (puedes adaptar según tu lógica)
            var almacenesDestino = new ObservableCollection<AlmacenDto>();
            // Aquí deberías poblar almacenesDestino según tu lógica de permisos, etc.
            // Por simplicidad, se deja vacío, pero deberías rellenarlo como en ConsultaStockViewModel

            var vm = new TraspasoStockDialogViewModel(StockSeleccionado, _traspasosService);
            var dlg = new TraspasoStockDialog(vm)
            {
                Owner = Application.Current.MainWindow
            };
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
    }
} 