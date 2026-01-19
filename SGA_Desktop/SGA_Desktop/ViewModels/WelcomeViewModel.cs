using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SGA_Desktop.ViewModels
{
    public partial class WelcomeViewModel : ObservableObject
    {
        private readonly OrdenTraspasoService _ordenTraspasoService;
        private readonly ConteosService _conteosService;
        private readonly InventarioService _inventarioService;

        [ObservableProperty]
        private string empresaNombre;

        [ObservableProperty]
        private string nombreOperario;

        [ObservableProperty]
        private int totalOrdenesPendientes;

        [ObservableProperty]
        private int ordenesEnProceso;

        [ObservableProperty]
        private int ordenesPrioridadAlta;

        [ObservableProperty]
        private int ordenesAsignadasAMi;

        [ObservableProperty]
        private int ordenesSinAsignar;

        [ObservableProperty]
        private bool cargandoOrdenes;

        [ObservableProperty]
        private int totalConteosPendientes;

        [ObservableProperty]
        private int conteosEnProceso;

        [ObservableProperty]
        private int conteosPendientesRevision;

        [ObservableProperty]
        private int conteosPrioridadAlta;

        [ObservableProperty]
        private int conteosCerrados;

        [ObservableProperty]
        private bool cargandoConteos;

        [ObservableProperty]
        private int totalInventariosAbiertos;

        [ObservableProperty]
        private int inventariosEnConteo;

        [ObservableProperty]
        private int inventariosConsolidados;

        [ObservableProperty]
        private int inventariosPendientesCierre;

        [ObservableProperty]
        private int inventariosCerrados;

        [ObservableProperty]
        private bool cargandoInventarios;

        public WelcomeViewModel()
        {
            _ordenTraspasoService = new OrdenTraspasoService();
            _conteosService = new ConteosService();
            _inventarioService = new InventarioService();

            // Obtener información de la sesión actual
            EmpresaNombre = SessionManager.EmpresaSeleccionadaNombre;
            NombreOperario = SessionManager.NombreOperario;

            // Suscribirse a cambios en la empresa
            SessionManager.EmpresaCambiada += OnEmpresaCambiada;

            // Cargar resumen de órdenes
            _ = CargarResumenOrdenesAsync();
            
            // Cargar resumen de conteos
            _ = CargarResumenConteosAsync();
            
            // Cargar resumen de inventarios
            _ = CargarResumenInventariosAsync();
        }

        private void OnEmpresaCambiada(object? sender, EventArgs e)
        {
            EmpresaNombre = SessionManager.EmpresaSeleccionadaNombre;
        }

        public async Task CargarResumenOrdenesAsync()
        {
            try
            {
                CargandoOrdenes = true;

                var ordenes = await _ordenTraspasoService.GetOrdenesTraspasoAsync();

                // Calcular contadores
                var idOperarioActual = SessionManager.UsuarioActual?.operario ?? 0;

                // Simplificado: Total pendientes = solo estado PENDIENTE
                TotalOrdenesPendientes = ordenes.Count(o => o.Estado == "PENDIENTE");
                
                // En proceso: solo estado EN_PROCESO
                OrdenesEnProceso = ordenes.Count(o => o.Estado == "EN_PROCESO");
                
                // Prioridad alta: solo PENDIENTES con prioridad >= 4
                OrdenesPrioridadAlta = ordenes.Count(o => o.Estado == "PENDIENTE" && o.Prioridad >= 4);
                
                // Asignadas a mí: solo PENDIENTES con líneas asignadas al operario actual
                OrdenesAsignadasAMi = ordenes.Count(o => 
                    o.Estado == "PENDIENTE" && 
                    o.Lineas.Any(l => l.IdOperarioAsignado == idOperarioActual && l.IdOperarioAsignado != 0));
                
                // Sin asignar: solo estado SIN_ASIGNAR
                OrdenesSinAsignar = ordenes.Count(o => o.Estado == "SIN_ASIGNAR");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar resumen de órdenes: {ex.Message}");
                // En caso de error, dejar los contadores en 0
            }
            finally
            {
                CargandoOrdenes = false;
            }
        }

        [RelayCommand]
        private void IrAOrdenesPendientes()
        {
            OrdenTraspasoFiltroStore.SolicitarFiltro(TipoFiltroOrden.TodasPendientes);
            NavegarAOrdenesTraspaso();
        }

        [RelayCommand]
        private void IrAOrdenesEnProceso()
        {
            OrdenTraspasoFiltroStore.SolicitarFiltro(TipoFiltroOrden.EnProceso);
            NavegarAOrdenesTraspaso();
        }

        [RelayCommand]
        private void IrAOrdenesPrioridadAlta()
        {
            OrdenTraspasoFiltroStore.SolicitarFiltro(TipoFiltroOrden.PrioridadAlta);
            NavegarAOrdenesTraspaso();
        }

        [RelayCommand]
        private void IrAOrdenesAsignadas()
        {
            OrdenTraspasoFiltroStore.SolicitarFiltro(TipoFiltroOrden.AsignadasAMi);
            NavegarAOrdenesTraspaso();
        }

        [RelayCommand]
        private void IrAOrdenesSinAsignar()
        {
            OrdenTraspasoFiltroStore.SolicitarFiltro(TipoFiltroOrden.SinAsignar);
            NavegarAOrdenesTraspaso();
        }

        private void NavegarAOrdenesTraspaso()
        {
            // Navegar y actualizar el header
            NavigationStore.Navigate("OrdenTraspaso");
            NavigationStore.RequestHeaderChange("ÓRDENES DE TRASPASO");
        }

        public async Task CargarResumenConteosAsync()
        {
            try
            {
                CargandoConteos = true;

                var codigoOperarioSesion = SessionManager.UsuarioActual?.operario.ToString();
                var creadoPorCodigo = SessionManager.UsuarioActual?.operario.ToString();

                // Obtener solo las órdenes creadas por el usuario actual
                var ordenes = await _conteosService.ListarTodasLasOrdenesAsync(
                    estado: null,
                    codigoOperario: null,
                    fechaDesde: null,
                    fechaHasta: null,
                    codigoOperarioSesion: codigoOperarioSesion,
                    creadoPorCodigo: creadoPorCodigo);

                // Calcular contadores
                // Pendientes: PLANIFICADO, ASIGNADO, EN_PROCESO
                TotalConteosPendientes = ordenes.Count(o => 
                    o.Estado == "PLANIFICADO" || 
                    o.Estado == "ASIGNADO" || 
                    o.Estado == "EN_PROCESO");
                
                // En proceso
                ConteosEnProceso = ordenes.Count(o => o.Estado == "EN_PROCESO");
                
                // Pendientes de revisión
                ConteosPendientesRevision = ordenes.Count(o => o.Estado == "PENDIENTE_REVISION");
                
                // Prioridad alta: PLANIFICADO/ASIGNADO con prioridad >= 4
                ConteosPrioridadAlta = ordenes.Count(o => 
                    (o.Estado == "PLANIFICADO" || o.Estado == "ASIGNADO") && 
                    o.Prioridad >= 4);
                
                // Cerradas
                ConteosCerrados = ordenes.Count(o => o.Estado == "CERRADO");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar resumen de conteos: {ex.Message}");
            }
            finally
            {
                CargandoConteos = false;
            }
        }

        [RelayCommand]
        private void IrAConteosPendientes()
        {
            NavegarAConteosRotativos();
            // Dar tiempo a que el ViewModel se cree y suscriba al evento
            Task.Delay(50).ContinueWith(_ =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ConteoFiltroStore.SolicitarFiltro(TipoFiltroConteo.Pendientes);
                });
            });
        }

        [RelayCommand]
        private void IrAConteosEnProceso()
        {
            NavegarAConteosRotativos();
            Task.Delay(50).ContinueWith(_ =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ConteoFiltroStore.SolicitarFiltro(TipoFiltroConteo.EnProceso);
                });
            });
        }

        [RelayCommand]
        private void IrAConteosPendientesRevision()
        {
            NavegarAConteosRotativos();
            Task.Delay(50).ContinueWith(_ =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ConteoFiltroStore.SolicitarFiltro(TipoFiltroConteo.PendientesRevision);
                });
            });
        }

        [RelayCommand]
        private void IrAConteosPrioridadAlta()
        {
            NavegarAConteosRotativos();
            Task.Delay(50).ContinueWith(_ =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ConteoFiltroStore.SolicitarFiltro(TipoFiltroConteo.PrioridadAlta);
                });
            });
        }

        [RelayCommand]
        private void IrAConteosCerrados()
        {
            NavegarAConteosRotativos();
            Task.Delay(50).ContinueWith(_ =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ConteoFiltroStore.SolicitarFiltro(TipoFiltroConteo.Cerrados);
                });
            });
        }

        private void NavegarAConteosRotativos()
        {
            NavigationStore.Navigate("ControlesRotativos");
            NavigationStore.RequestHeaderChange("CONTEOS ROTATIVOS");
        }

        public async Task CargarResumenInventariosAsync()
        {
            try
            {
                CargandoInventarios = true;

                // Verificar que haya una empresa seleccionada antes de cargar
                if (!SessionManager.EmpresaSeleccionada.HasValue)
                {
                    System.Diagnostics.Debug.WriteLine("[WelcomeViewModel] No hay empresa seleccionada, omitiendo carga de resumen de inventarios");
                    return;
                }

                var filtro = new FiltroInventarioDto
                {
                    CodigoEmpresa = SessionManager.EmpresaSeleccionada.Value,
                    CodigoAlmacen = null,
                    CodigosAlmacen = null,
                    FechaDesde = null, // Sin límite de fecha
                    FechaHasta = null,
                    EstadoInventario = null,
                    UsuarioCreacionId = SessionManager.UsuarioActual?.operario // Solo los propios
                };

                var inventarios = await _inventarioService.ObtenerInventariosAsync(filtro);

                // Calcular contadores
                TotalInventariosAbiertos = inventarios.Count(i => i.Estado == "ABIERTO");
                InventariosEnConteo = inventarios.Count(i => i.Estado == "EN_CONTEO");
                InventariosConsolidados = inventarios.Count(i => i.Estado == "CONSOLIDADO");
                InventariosPendientesCierre = inventarios.Count(i => i.Estado == "PENDIENTE_CIERRE");
                InventariosCerrados = inventarios.Count(i => i.Estado == "CERRADO");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar resumen de inventarios: {ex.Message}");
            }
            finally
            {
                CargandoInventarios = false;
            }
        }

        [RelayCommand]
        private void IrAInventariosAbiertos()
        {
            NavegarAInventarios();
            Task.Delay(50).ContinueWith(_ =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    InventarioFiltroStore.SolicitarFiltro(TipoFiltroInventario.Abiertos);
                });
            });
        }

        [RelayCommand]
        private void IrAInventariosEnConteo()
        {
            NavegarAInventarios();
            Task.Delay(50).ContinueWith(_ =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    InventarioFiltroStore.SolicitarFiltro(TipoFiltroInventario.EnConteo);
                });
            });
        }

        [RelayCommand]
        private void IrAInventariosConsolidados()
        {
            NavegarAInventarios();
            Task.Delay(50).ContinueWith(_ =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    InventarioFiltroStore.SolicitarFiltro(TipoFiltroInventario.Consolidados);
                });
            });
        }

        [RelayCommand]
        private void IrAInventariosPendientesCierre()
        {
            NavegarAInventarios();
            Task.Delay(50).ContinueWith(_ =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    InventarioFiltroStore.SolicitarFiltro(TipoFiltroInventario.PendientesCierre);
                });
            });
        }

        [RelayCommand]
        private void IrAInventariosCerrados()
        {
            NavegarAInventarios();
            Task.Delay(50).ContinueWith(_ =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    InventarioFiltroStore.SolicitarFiltro(TipoFiltroInventario.Cerrados);
                });
            });
        }

        private void NavegarAInventarios()
        {
            NavigationStore.Navigate("Inventario");
            NavigationStore.RequestHeaderChange("INVENTARIO");
        }
    }
}
