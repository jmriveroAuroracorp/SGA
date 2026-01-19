using System;
using System.Collections.Generic;
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
    public partial class FiltrosInventarioDialogViewModel : ObservableObject
    {
        private readonly StockService _stockService;

        // Propiedades para filtros
        [ObservableProperty] private AlmacenDto? almacenSeleccionadoCombo;
        [ObservableProperty] private DateTime fechaDesde = DateTime.Today.AddDays(-2);
        [ObservableProperty] private DateTime fechaHasta = DateTime.Today;
        [ObservableProperty] private string estadoFiltro = "TODOS";
        [ObservableProperty] private string idInventarioFiltro = string.Empty;

        [ObservableProperty] private bool verTodosLosInventarios = false; // Por defecto, solo ver los propios

        // Colecciones para filtros
        public ObservableCollection<AlmacenDto> AlmacenesCombo { get; } = new();
        public ObservableCollection<string> EstadosCombo { get; } = new();

        // Propiedades para autocompletado de almacenes
        [ObservableProperty] private string filtroAlmacenesTexto = "";
        [ObservableProperty] private bool isDropDownOpenAlmacenes = false;
        public ICollectionView AlmacenesComboView { get; private set; }

        // Comandos
        public IAsyncRelayCommand AplicarFiltrosCommand { get; }
        public IRelayCommand LimpiarFiltrosCommand { get; }
        public IRelayCommand CerrarCommand { get; }

        // Comandos para controlar dropdown de almacenes
        public IRelayCommand AbrirDropDownAlmacenesCommand { get; }
        public IRelayCommand CerrarDropDownAlmacenesCommand { get; }

        // Evento para comunicar con el diálogo
        public event Action<bool> RequestClose;

        public FiltrosInventarioDialogViewModel()
        {
            _stockService = new StockService();

            // Inicializar ICollectionView para filtrado de almacenes
            AlmacenesComboView = CollectionViewSource.GetDefaultView(AlmacenesCombo);
            AlmacenesComboView.Filter = FiltraAlmacenes;

            // Inicializar estados
            EstadosCombo.Add("TODOS");
            EstadosCombo.Add("ABIERTO");
            EstadosCombo.Add("EN_CONTEO");
            EstadosCombo.Add("CONSOLIDADO");
            EstadosCombo.Add("CERRADO");

            // Inicializar comandos
            AplicarFiltrosCommand = new AsyncRelayCommand(AplicarFiltrosAsync);
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

            // Inicialización
            _ = InitializeAsync();
        }

        // Constructor con valores iniciales desde el ViewModel principal
        public FiltrosInventarioDialogViewModel(
            AlmacenDto? almacenSeleccionado,
            DateTime fechaDesde,
            DateTime fechaHasta,
            string estadoFiltro,
            string idInventarioFiltro,
            bool verTodosLosInventarios) : this()
        {
            AlmacenSeleccionadoCombo = almacenSeleccionado;
            FechaDesde = fechaDesde;
            FechaHasta = fechaHasta;
            EstadoFiltro = estadoFiltro;
            IdInventarioFiltro = idInventarioFiltro;
            VerTodosLosInventarios = verTodosLosInventarios;
        }

        // Validaciones de fechas
        partial void OnFechaDesdeChanged(DateTime value)
        {
            if (FechaHasta < value)
            {
                FechaHasta = value;
            }
        }

        partial void OnFechaHastaChanged(DateTime value)
        {
            if (value < FechaDesde)
            {
                FechaHasta = FechaDesde;
            }
        }

        // Métodos para manejar cambios en los filtros
        partial void OnFiltroAlmacenesTextoChanged(string value)
        {
            AlmacenesComboView?.Refresh();
        }

        private async Task InitializeAsync()
        {
            try
            {
                // Cargar almacenes
                await CargarAlmacenesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en inicialización: {ex.Message}");
            }
        }

        private async Task CargarAlmacenesAsync()
        {
            try
            {
                var empresa = SessionManager.EmpresaSeleccionada!.Value;
                var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
                var desdeLogin = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();

                var resultado = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa, centro, desdeLogin, SessionManager.Operario);

                AlmacenesCombo.Clear();

                // Añadir opción "Todas"
                AlmacenesCombo.Add(new AlmacenDto
                {
                    CodigoAlmacen = "Todas",
                    NombreAlmacen = "Todas",
                    CodigoEmpresa = empresa
                });

                foreach (var a in resultado)
                    AlmacenesCombo.Add(a);

                // Si no hay almacén seleccionado, seleccionar "Todas"
                if (AlmacenSeleccionadoCombo == null)
                {
                    AlmacenSeleccionadoCombo = AlmacenesCombo.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando almacenes: {ex.Message}");
            }
        }

        private async Task AplicarFiltrosAsync()
        {
            // Cerrar el diálogo con resultado true (aplicar filtros)
            RequestClose?.Invoke(true);
        }

        private void LimpiarFiltros()
        {
            EstadoFiltro = "TODOS";
            IdInventarioFiltro = string.Empty;
            VerTodosLosInventarios = false; // Por defecto, solo ver los propios

            // Seleccionar "Todas" en almacenes
            if (AlmacenesCombo?.Any() == true)
            {
                AlmacenSeleccionadoCombo = AlmacenesCombo.FirstOrDefault();
                FiltroAlmacenesTexto = "";
            }

            // Establecer fechas: desde hace 2 días hasta hoy
            FechaDesde = DateTime.Today.AddDays(-2);
            FechaHasta = DateTime.Today;
        }

        private void Cerrar()
        {
            // Cerrar el diálogo sin aplicar filtros
            RequestClose?.Invoke(false);
        }

        // Método de filtrado para almacenes
        private bool FiltraAlmacenes(object obj)
        {
            if (obj is not AlmacenDto almacen) return false;
            if (string.IsNullOrEmpty(FiltroAlmacenesTexto)) return true;

            return System.Globalization.CultureInfo.CurrentCulture.CompareInfo
                .IndexOf(almacen.DescripcionCombo, FiltroAlmacenesTexto, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
        }
    }
}

