using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using SGA_Desktop.Services;

namespace SGA_Desktop.ViewModels
{
    public partial class FiltrosSupervisionDialogViewModel : ObservableObject
    {
        private readonly LoginService _loginService;

        // Propiedades para filtros
        [ObservableProperty] private bool verTodosLosResultados = false; // Por defecto, solo ver los propios
        [ObservableProperty] private DateTime? fechaDesde;
        [ObservableProperty] private DateTime? fechaHasta;
        [ObservableProperty] private decimal? diferenciaDesde;
        [ObservableProperty] private decimal? diferenciaHasta;
        [ObservableProperty] private string? almacenSeleccionado;
        [ObservableProperty] private string? ubicacionSeleccionada;
        [ObservableProperty] private OperariosAccesoDto? operarioSeleccionado;

        // Colecciones para filtros
        public ObservableCollection<string> AlmacenesCombo { get; } = new();
        public ObservableCollection<string> UbicacionesCombo { get; } = new();
        public ObservableCollection<OperariosAccesoDto> OperariosCombo { get; } = new();

        // Propiedades para autocompletado
        [ObservableProperty] private string filtroAlmacenesTexto = "";
        [ObservableProperty] private string filtroUbicacionesTexto = "";
        [ObservableProperty] private string filtroOperariosTexto = "";
        [ObservableProperty] private bool isDropDownOpenAlmacenes = false;
        [ObservableProperty] private bool isDropDownOpenUbicaciones = false;
        [ObservableProperty] private bool isDropDownOpenOperarios = false;

        // Vistas filtrables para autocompletado
        public ICollectionView AlmacenesComboView { get; private set; }
        public ICollectionView UbicacionesComboView { get; private set; }
        public ICollectionView OperariosComboView { get; private set; }

        // Comandos
        public IRelayCommand AplicarFiltrosCommand { get; }
        public IRelayCommand LimpiarFiltrosCommand { get; }
        public IRelayCommand CerrarCommand { get; }

        // Comandos para controlar dropdown
        public IRelayCommand AbrirDropDownAlmacenesCommand { get; }
        public IRelayCommand CerrarDropDownAlmacenesCommand { get; }
        public IRelayCommand AbrirDropDownUbicacionesCommand { get; }
        public IRelayCommand CerrarDropDownUbicacionesCommand { get; }
        public IRelayCommand AbrirDropDownOperariosCommand { get; }
        public IRelayCommand CerrarDropDownOperariosCommand { get; }

        // Evento para comunicar con el diálogo
        public event Action<bool> RequestClose;

        public FiltrosSupervisionDialogViewModel()
        {
            _loginService = new LoginService();

            // Inicializar ICollectionView para filtrado
            AlmacenesComboView = CollectionViewSource.GetDefaultView(AlmacenesCombo);
            AlmacenesComboView.Filter = FiltraAlmacenes;

            UbicacionesComboView = CollectionViewSource.GetDefaultView(UbicacionesCombo);
            UbicacionesComboView.Filter = FiltraUbicaciones;

            OperariosComboView = CollectionViewSource.GetDefaultView(OperariosCombo);
            OperariosComboView.Filter = FiltraOperarios;

            // Inicializar comandos
            AplicarFiltrosCommand = new RelayCommand(AplicarFiltros);
            LimpiarFiltrosCommand = new RelayCommand(LimpiarFiltros);
            CerrarCommand = new RelayCommand(Cerrar);

            // Comandos para dropdown de almacenes
            AbrirDropDownAlmacenesCommand = new RelayCommand(() =>
            {
                FiltroAlmacenesTexto = "";
                IsDropDownOpenAlmacenes = true;
            });

            CerrarDropDownAlmacenesCommand = new RelayCommand(() =>
            {
                IsDropDownOpenAlmacenes = false;
            });

            // Comandos para dropdown de ubicaciones
            AbrirDropDownUbicacionesCommand = new RelayCommand(() =>
            {
                FiltroUbicacionesTexto = "";
                IsDropDownOpenUbicaciones = true;
            });

            CerrarDropDownUbicacionesCommand = new RelayCommand(() =>
            {
                IsDropDownOpenUbicaciones = false;
            });

            // Comandos para dropdown de operarios
            AbrirDropDownOperariosCommand = new RelayCommand(() =>
            {
                FiltroOperariosTexto = "";
                IsDropDownOpenOperarios = true;
            });

            CerrarDropDownOperariosCommand = new RelayCommand(() =>
            {
                IsDropDownOpenOperarios = false;
            });

            // Cargar datos iniciales
            _ = InitializeAsync();
        }

        // Constructor con valores iniciales desde el ViewModel principal
        public FiltrosSupervisionDialogViewModel(
            bool verTodosLosResultados,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            decimal? diferenciaDesde,
            decimal? diferenciaHasta,
            string? almacenSeleccionado,
            string? ubicacionSeleccionada,
            OperariosAccesoDto? operarioSeleccionado,
            ObservableCollection<string> almacenesDisponibles,
            ObservableCollection<string> ubicacionesDisponibles,
            ObservableCollection<OperariosAccesoDto> operariosDisponibles) : this()
        {
            VerTodosLosResultados = verTodosLosResultados;
            FechaDesde = fechaDesde;
            FechaHasta = fechaHasta;
            DiferenciaDesde = diferenciaDesde;
            DiferenciaHasta = diferenciaHasta;
            AlmacenSeleccionado = almacenSeleccionado;
            UbicacionSeleccionada = ubicacionSeleccionada;
            OperarioSeleccionado = operarioSeleccionado;

            // Copiar las colecciones disponibles
            AlmacenesCombo.Clear();
            foreach (var almacen in almacenesDisponibles)
            {
                AlmacenesCombo.Add(almacen);
            }

            UbicacionesCombo.Clear();
            foreach (var ubicacion in ubicacionesDisponibles)
            {
                UbicacionesCombo.Add(ubicacion);
            }

            OperariosCombo.Clear();
            foreach (var operario in operariosDisponibles)
            {
                OperariosCombo.Add(operario);
            }
        }

        private async Task InitializeAsync()
        {
            try
            {
                await CargarOperariosAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en inicialización: {ex.Message}");
            }
        }

        private async Task CargarOperariosAsync()
        {
            try
            {
                var operarios = await _loginService.ObtenerOperariosConAccesoConteosAsync();

                OperariosCombo.Clear();

                // Agregar opción "Todos"
                OperariosCombo.Add(new OperariosAccesoDto
                {
                    Operario = 0,
                    NombreOperario = "TODOS",
                    Contraseña = "",
                    MRH_CodigoAplicacion = 0
                });

                foreach (var operario in operarios.OrderBy(o => o.NombreOperario))
                {
                    OperariosCombo.Add(operario);
                }

                // Si no hay operario seleccionado, seleccionar "TODOS"
                if (OperarioSeleccionado == null)
                {
                    OperarioSeleccionado = OperariosCombo.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando operarios: {ex.Message}");
            }
        }

        // Validaciones de fechas
        partial void OnFechaDesdeChanged(DateTime? value)
        {
            if (FechaHasta.HasValue && value.HasValue && FechaHasta < value)
            {
                FechaHasta = value;
            }
        }

        partial void OnFechaHastaChanged(DateTime? value)
        {
            if (value.HasValue && FechaDesde.HasValue && value < FechaDesde)
            {
                FechaHasta = FechaDesde;
            }
        }

        // Validaciones de diferencia
        partial void OnDiferenciaDesdeChanged(decimal? value)
        {
            if (DiferenciaHasta.HasValue && value.HasValue && DiferenciaHasta < value)
            {
                DiferenciaHasta = value;
            }
        }

        partial void OnDiferenciaHastaChanged(decimal? value)
        {
            if (value.HasValue && DiferenciaDesde.HasValue && value < DiferenciaDesde)
            {
                DiferenciaHasta = DiferenciaDesde;
            }
        }

        // Métodos para manejar cambios en los filtros
        partial void OnFiltroAlmacenesTextoChanged(string value)
        {
            AlmacenesComboView?.Refresh();
        }

        partial void OnFiltroUbicacionesTextoChanged(string value)
        {
            UbicacionesComboView?.Refresh();
        }

        partial void OnFiltroOperariosTextoChanged(string value)
        {
            OperariosComboView?.Refresh();
        }

        private void AplicarFiltros()
        {
            // Cerrar el diálogo con resultado true (aplicar filtros)
            RequestClose?.Invoke(true);
        }

        private void LimpiarFiltros()
        {
            VerTodosLosResultados = false; // Por defecto, solo ver los propios
            FechaDesde = null;
            FechaHasta = null;
            DiferenciaDesde = null;
            DiferenciaHasta = null;
            AlmacenSeleccionado = null;
            UbicacionSeleccionada = null;
            
            // Seleccionar "TODOS" en operarios
            if (OperariosCombo?.Any() == true)
            {
                OperarioSeleccionado = OperariosCombo.FirstOrDefault();
                FiltroOperariosTexto = "";
            }
            
            FiltroAlmacenesTexto = "";
            FiltroUbicacionesTexto = "";
        }

        private void Cerrar()
        {
            // Cerrar el diálogo sin aplicar filtros
            RequestClose?.Invoke(false);
        }

        // Método de filtrado para almacenes
        private bool FiltraAlmacenes(object obj)
        {
            if (obj is not string almacen) return false;
            if (string.IsNullOrEmpty(FiltroAlmacenesTexto)) return true;

            return System.Globalization.CultureInfo.CurrentCulture.CompareInfo
                .IndexOf(almacen, FiltroAlmacenesTexto, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
        }

        // Método de filtrado para ubicaciones
        private bool FiltraUbicaciones(object obj)
        {
            if (obj is not string ubicacion) return false;
            if (string.IsNullOrEmpty(FiltroUbicacionesTexto)) return true;

            return System.Globalization.CultureInfo.CurrentCulture.CompareInfo
                .IndexOf(ubicacion, FiltroUbicacionesTexto, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
        }

        // Método de filtrado para operarios
        private bool FiltraOperarios(object obj)
        {
            if (obj is not OperariosAccesoDto operario) return false;
            if (string.IsNullOrEmpty(FiltroOperariosTexto)) return true;

            return System.Globalization.CultureInfo.CurrentCulture.CompareInfo
                .IndexOf(operario.NombreOperario, FiltroOperariosTexto, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
        }
    }
}
