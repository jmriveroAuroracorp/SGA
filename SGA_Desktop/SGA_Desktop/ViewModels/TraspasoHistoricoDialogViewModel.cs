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

        private async Task InitializeAsync()
        {
            try
            {
                // Establecer fechas por defecto (último mes)
                FechaDesde = DateTime.Today.AddMonths(-1);
                FechaHasta = DateTime.Today;

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
                var empresa = SessionManager.EmpresaSeleccionada!.Value;
                
                var traspasos = await _traspasosService.ObtenerTraspasosFiltradosAsync(
                    estado: EstadoSeleccionado?.CodigoEstado,
                    codigoPalet: null, // No filtramos por palet en este caso
                    almacenOrigen: AlmacenOrigenSeleccionado?.CodigoAlmacen,
                    almacenDestino: AlmacenDestinoSeleccionado?.CodigoAlmacen,
                    fechaInicioDesde: FechaDesde,
                    fechaInicioHasta: FechaHasta?.AddDays(1) // Incluir todo el día hasta
                );

                Traspasos.Clear();
                foreach (var traspaso in traspasos.OrderByDescending(t => t.FechaInicio))
                {
                    Traspasos.Add(traspaso);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando traspasos: {ex.Message}");
            }
        }

        private async Task AplicarFiltrosAsync()
        {
            await CargarTraspasosAsync();
        }

        private void LimpiarFiltros()
        {
            FechaDesde = DateTime.Today.AddMonths(-1);
            FechaHasta = DateTime.Today;
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