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
    public partial class TraspasoStockDialogViewModel : ObservableObject
    {
        // Propiedades para binding
        public string ArticuloDescripcion { get; set; }
        public string AlmacenOrigenNombre { get; set; }
        public decimal CantidadDisponible { get; set; }
        public ObservableCollection<AlmacenDto> AlmacenesDestino { get; set; }
        [ObservableProperty]
        private AlmacenDto almacenDestinoSeleccionado;
        [ObservableProperty]
        private string cantidadATraspasarTexto;

        partial void OnCantidadATraspasarTextoChanged(string value)
        {
            ConfirmarCommand.NotifyCanExecuteChanged();
        }

        partial void OnAlmacenDestinoSeleccionadoChanged(AlmacenDto value)
        {
            ConfirmarCommand.NotifyCanExecuteChanged();
            _ = CargarUbicacionesDestinoAsync();
        }

        [ObservableProperty]
        private ObservableCollection<UbicacionDto> ubicacionesDestino = new();

        [ObservableProperty]
        private UbicacionDto ubicacionDestinoSeleccionada;

        private readonly UbicacionesService _ubicacionesService = new UbicacionesService();

        private async Task CargarUbicacionesDestinoAsync()
        {
            UbicacionesDestino.Clear();
            if (AlmacenDestinoSeleccionado == null) return;
            var lista = await _ubicacionesService.ObtenerUbicacionesAsync(
                AlmacenDestinoSeleccionado.CodigoAlmacen,
                SessionManager.EmpresaSeleccionada.Value
            );
            if (lista != null)
            {
                foreach (var u in lista)
                    UbicacionesDestino.Add(u);
            }
        }

        public string CodigoArticulo { get; set; }
        public string UbicacionOrigen => _stockSeleccionado.Ubicacion;
        public decimal Reservado => _stockSeleccionado.Reservado;
        public decimal Disponible => _stockSeleccionado.Disponible;

        // Eliminar las propiedades y la inicialización manual de los comandos
        // Comandos generados automáticamente por [RelayCommand]

        // Eventos
        public event Action<bool> RequestClose;

        private readonly TraspasosService _traspasoService;
        private readonly StockDisponibleDto _stockSeleccionado;
        private DateTime? _fechaBusqueda = null;

        public TraspasoStockDialogViewModel(StockDisponibleDto stockSeleccionado, TraspasosService traspasoService, DateTime? fechaBusqueda)
        {
            _stockSeleccionado = stockSeleccionado;
            CodigoArticulo = stockSeleccionado.CodigoArticulo;
            ArticuloDescripcion = stockSeleccionado.DescripcionArticulo;
            AlmacenOrigenNombre = stockSeleccionado.CodigoAlmacen;
            CantidadDisponible = stockSeleccionado.Disponible;
            AlmacenesDestino = new ObservableCollection<AlmacenDto>();
            _traspasoService = traspasoService;
            _fechaBusqueda = fechaBusqueda;
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                var empresa = SessionManager.EmpresaSeleccionada!.Value;
                var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
                var permisos = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();
                if (!permisos.Any())
                {
                    permisos = await new StockService().ObtenerAlmacenesAsync(centro);
                }
                var almacenes = await new StockService().ObtenerAlmacenesAutorizadosAsync(empresa, centro, permisos);
                AlmacenesDestino.Clear();
                foreach (var a in almacenes)
                    AlmacenesDestino.Add(a);
                OnPropertyChanged(nameof(AlmacenesDestino));
            }
            catch (Exception ex)
            {
                // Puedes mostrar un mensaje de error si lo deseas
            }
        }

        // Método que se llama al pulsar Buscar
        public void RegistrarBusqueda()
        {
            _fechaBusqueda = DateTime.UtcNow;
        }

        [ObservableProperty]
        private string? feedback;

        [RelayCommand(CanExecute = nameof(PuedeConfirmar))]
        private async Task ConfirmarAsync()
        {
            // Depuración: mostrar valores clave antes de llamar al servicio
            System.Windows.MessageBox.Show($"Traspaso:\nCantidad: {CantidadATraspasarTexto}\nAlmacén destino: {AlmacenDestinoSeleccionado?.CodigoAlmacen}\nUbicación destino: {UbicacionDestinoSeleccionada?.Ubicacion}", "Debug traspaso");

            if (!decimal.TryParse(CantidadATraspasarTexto.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var cantidad) || cantidad <= 0)
            {
                Feedback = "Cantidad no válida.";
                return;
            }
            if (AlmacenDestinoSeleccionado == null)
            {
                Feedback = "Selecciona un almacén destino.";
                return;
            }
            // Ya no exigimos ubicación destino, puede ser null o vacío (sin ubicar)
            var dto = new CrearTraspasoArticuloDto
            {
                AlmacenOrigen = _stockSeleccionado.CodigoAlmacen,
                UbicacionOrigen = _stockSeleccionado.Ubicacion ?? string.Empty,
                CodigoArticulo = _stockSeleccionado.CodigoArticulo,
                Cantidad = cantidad,
                UsuarioId = SessionManager.UsuarioActual?.operario ?? 0,
                AlmacenDestino = AlmacenDestinoSeleccionado.CodigoAlmacen,
                UbicacionDestino = string.IsNullOrWhiteSpace(UbicacionDestinoSeleccionada?.Ubicacion) ? "" : UbicacionDestinoSeleccionada.Ubicacion,
                FechaCaducidad = _stockSeleccionado.FechaCaducidad,
                Partida = _stockSeleccionado.Partida,
                Finalizar = true,
                CodigoEmpresa = SessionManager.EmpresaSeleccionada.Value,
                FechaInicio = _fechaBusqueda
            };

            // Depuración: mostrar valores en el DTO
            System.Windows.MessageBox.Show($"DTO -> FechaCaducidad: {dto.FechaCaducidad}\nPartida: {dto.Partida}");
            var resultado = await _traspasoService.CrearTraspasoArticuloAsync(dto);
            if (resultado.Success)
            {
                Feedback = "Traspaso realizado correctamente.";
                RequestClose?.Invoke(true);
            }
            else
            {
                Feedback = resultado.ErrorMessage ?? "Error al realizar el traspaso.";
            }
        }

        private bool PuedeConfirmar()
        {
            if (string.IsNullOrWhiteSpace(CantidadATraspasarTexto) || AlmacenDestinoSeleccionado == null)
                return false;

            // Permitir tanto coma como punto
            var texto = CantidadATraspasarTexto.Replace(',', '.');
            if (!decimal.TryParse(texto, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var cantidad))
                return false;

            return cantidad > 0 && cantidad <= CantidadDisponible;
        }

        [RelayCommand]
        private void Cancelar()
        {
            RequestClose?.Invoke(false);
        }

        // No es necesario implementar PropertyChanged, lo gestiona ObservableObject
    }
} 