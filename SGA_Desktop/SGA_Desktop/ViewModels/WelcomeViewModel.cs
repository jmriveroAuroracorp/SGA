using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Helpers;
using SGA_Desktop.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SGA_Desktop.ViewModels
{
    public partial class WelcomeViewModel : ObservableObject
    {
        private readonly OrdenTraspasoService _ordenTraspasoService;

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

        public WelcomeViewModel()
        {
            _ordenTraspasoService = new OrdenTraspasoService();

            // Obtener información de la sesión actual
            EmpresaNombre = SessionManager.EmpresaSeleccionadaNombre;
            NombreOperario = SessionManager.NombreOperario;

            // Suscribirse a cambios en la empresa
            SessionManager.EmpresaCambiada += OnEmpresaCambiada;

            // Cargar resumen de órdenes
            _ = CargarResumenOrdenesAsync();
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
    }
}
