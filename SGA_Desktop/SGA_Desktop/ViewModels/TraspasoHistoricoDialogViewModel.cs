using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Helpers;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Linq;

namespace SGA_Desktop.ViewModels
{
    public partial class TraspasoHistoricoDialogViewModel : ObservableObject
    {
        private readonly TraspasosService _traspasosService;
        private readonly StockService _stockService;

        // Propiedades para filtros
        [ObservableProperty] private DateTime? fechaDesde;
        [ObservableProperty] private DateTime? fechaHasta;
        [ObservableProperty] private string codigoArticulo = "";
        [ObservableProperty] private AlmacenDto? almacenOrigenSeleccionado;
        [ObservableProperty] private AlmacenDto? almacenDestinoSeleccionado;
        [ObservableProperty] private EstadoTraspasoDto? estadoSeleccionado;
        [ObservableProperty] private bool estaCargando = false;

        // Colecciones para filtros
        public ObservableCollection<AlmacenDto> AlmacenesOrigen { get; } = new();
        public ObservableCollection<AlmacenDto> AlmacenesDestino { get; } = new();
        public ObservableCollection<EstadoTraspasoDto> Estados { get; } = new();

        // Datos principales
        public ObservableCollection<TraspasoDto> Traspasos { get; } = new();
        [ObservableProperty] private TraspasoDto? traspasoSeleccionado;

        // Comandos
        public IAsyncRelayCommand AplicarFiltrosCommand { get; }
        public IRelayCommand LimpiarFiltrosCommand { get; }
        public IRelayCommand CerrarCommand { get; }
        public IRelayCommand VerDetallesCommand { get; }

        // Eventos
        public event Action<bool> RequestClose;

        public TraspasoHistoricoDialogViewModel(TraspasosService traspasosService)
        {
            _traspasosService = traspasosService;
            _stockService = new StockService();

            // Inicializar comandos
            AplicarFiltrosCommand = new AsyncRelayCommand(AplicarFiltrosAsync);
            LimpiarFiltrosCommand = new RelayCommand(LimpiarFiltros);
            CerrarCommand = new RelayCommand(Cerrar);
            VerDetallesCommand = new RelayCommand(VerDetalles, PuedeVerDetalles);

            // Inicialización
            _ = InitializeAsync();
        }

        public TraspasoHistoricoDialogViewModel() : this(new TraspasosService()) { }

        // Aplicar filtros automáticamente cuando cambien las propiedades
        partial void OnFechaDesdeChanged(DateTime? oldValue, DateTime? newValue)
        {
            if (newValue.HasValue && FechaHasta.HasValue && FechaHasta.Value < newValue.Value)
            {
                FechaHasta = newValue.Value;
            }
            _ = AplicarFiltrosAsync();
        }

        partial void OnFechaHastaChanged(DateTime? oldValue, DateTime? newValue)
        {
            if (newValue.HasValue && FechaDesde.HasValue && newValue.Value < FechaDesde.Value)
            {
                FechaHasta = FechaDesde.Value;
            }
            _ = AplicarFiltrosAsync();
        }

        partial void OnEstadoSeleccionadoChanged(EstadoTraspasoDto? value) => _ = AplicarFiltrosAsync();
        partial void OnAlmacenOrigenSeleccionadoChanged(AlmacenDto? value) => _ = AplicarFiltrosAsync();
        partial void OnAlmacenDestinoSeleccionadoChanged(AlmacenDto? value) => _ = AplicarFiltrosAsync();

        private async Task InitializeAsync()
        {
            try
            {
                // Establecer fechas por defecto (últimos días para ver traspasos recientes)
                FechaDesde = DateTime.Today.AddDays(-2); // Últimos 2 días como InventarioViewModel
                FechaHasta = DateTime.Today; // Solo la fecha, hora 00:00:00

                // Cargar almacenes
                await CargarAlmacenesAsync();

                // Cargar estados
                await CargarEstadosAsync();

                // Cargar traspasos iniciales
                await CargarTraspasosAsync();
            }
            catch (Exception ex)
            {
                // Manejar error de inicialización
                System.Diagnostics.Debug.WriteLine($"Error en inicialización: {ex.Message}");
            }
        }

        private async Task CargarAlmacenesAsync()
        {
            try
            {
                var empresa = SessionManager.EmpresaSeleccionada!.Value;
                var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
                var permisos = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();
                
                if (!permisos.Any())
                {
                    permisos = await _stockService.ObtenerAlmacenesAsync(centro);
                }

                var almacenes = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa, centro, permisos);

                AlmacenesOrigen.Clear();
                AlmacenesDestino.Clear();

                foreach (var almacen in almacenes)
                {
                    AlmacenesOrigen.Add(almacen);
                    AlmacenesDestino.Add(almacen);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando almacenes: {ex.Message}");
            }
        }

        private async Task CargarEstadosAsync()
        {
            try
            {
                Estados.Clear();
                
                // Estados básicos de traspasos
                Estados.Add(new EstadoTraspasoDto { CodigoEstado = "PENDIENTE", Descripcion = "Pendiente" });
                Estados.Add(new EstadoTraspasoDto { CodigoEstado = "EN_PROCESO", Descripcion = "En Proceso" });
                Estados.Add(new EstadoTraspasoDto { CodigoEstado = "COMPLETADO", Descripcion = "Completado" });
                Estados.Add(new EstadoTraspasoDto { CodigoEstado = "CANCELADO", Descripcion = "Cancelado" });
                Estados.Add(new EstadoTraspasoDto { CodigoEstado = "ERROR", Descripcion = "Error" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando estados: {ex.Message}");
            }
        }

        private async Task CargarTraspasosAsync()
        {
            try
            {
                EstaCargando = true;
                var empresa = SessionManager.EmpresaSeleccionada!.Value;
                
                // Asegurar que las fechas estén bien configuradas (misma lógica que InventarioViewModel)
                var fechaDesde = FechaDesde ?? DateTime.Today.AddDays(-2);
                var fechaHasta = FechaHasta ?? DateTime.Today;
                
                System.Diagnostics.Debug.WriteLine($"Cargando traspasos desde: {fechaDesde:yyyy-MM-dd} hasta: {fechaHasta:yyyy-MM-dd}");
                
                var traspasos = await _traspasosService.ObtenerTraspasosFiltradosAsync(
                    estado: EstadoSeleccionado?.CodigoEstado,
                    codigoPalet: null, // No filtramos por palet en este caso
                    almacenOrigen: AlmacenOrigenSeleccionado?.CodigoAlmacen,
                    almacenDestino: AlmacenDestinoSeleccionado?.CodigoAlmacen,
                    fechaInicioDesde: fechaDesde.Date, // Solo la fecha, hora 00:00:00
                    fechaInicioHasta: fechaHasta.Date // Solo la fecha, la API se encarga de incluir todo el día
                );

                System.Diagnostics.Debug.WriteLine($"API devolvió {traspasos.Count} traspasos");

                Traspasos.Clear();
                
                // Aplicar filtro de código de artículo si está especificado
                var traspasosFiltrados = traspasos;
                if (!string.IsNullOrWhiteSpace(CodigoArticulo))
                {
                    traspasosFiltrados = traspasos.Where(t => 
                        !string.IsNullOrWhiteSpace(t.CodigoArticulo) && 
                        t.CodigoArticulo.Contains(CodigoArticulo, StringComparison.OrdinalIgnoreCase)
                    ).ToList();
                    
                    System.Diagnostics.Debug.WriteLine($"Después del filtro de artículo: {traspasosFiltrados.Count} traspasos");
                }

                foreach (var traspaso in traspasosFiltrados.OrderByDescending(t => t.FechaInicio))
                {
                    Traspasos.Add(traspaso);
                    System.Diagnostics.Debug.WriteLine($"Traspaso: {traspaso.CodigoArticulo} - {traspaso.FechaInicio:yyyy-MM-dd HH:mm}");
                }
                
                System.Diagnostics.Debug.WriteLine($"Total final: {Traspasos.Count} traspasos");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando traspasos: {ex.Message}");
                // Aquí podrías mostrar un mensaje de error al usuario
            }
            finally
            {
                EstaCargando = false;
            }
        }

        private async Task AplicarFiltrosAsync()
        {
            await CargarTraspasosAsync();
        }

        private void LimpiarFiltros()
        {
            FechaDesde = DateTime.Today.AddDays(-2); // Últimos 2 días
            FechaHasta = DateTime.Today; // Solo la fecha, hora 00:00:00
            CodigoArticulo = "";
            AlmacenOrigenSeleccionado = null;
            AlmacenDestinoSeleccionado = null;
            EstadoSeleccionado = null;
            
            // Recargar traspasos con filtros limpios
            _ = CargarTraspasosAsync();
        }

        private void Cerrar()
        {
            RequestClose?.Invoke(false);
        }

        private bool PuedeVerDetalles()
        {
            return TraspasoSeleccionado != null;
        }

        private void VerDetalles()
        {
            if (TraspasoSeleccionado == null) return;

            // Aquí puedes implementar la lógica para mostrar detalles del traspaso
            // Por ejemplo, abrir otro dialog con información detallada
            System.Diagnostics.Debug.WriteLine($"Ver detalles del traspaso: {TraspasoSeleccionado.Id}");
        }
    }
} 