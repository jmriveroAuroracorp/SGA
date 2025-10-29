using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using System.Collections.ObjectModel;

namespace SGA_Desktop.ViewModels
{
    public partial class VerOrdenConteoDialogViewModel : ObservableObject
    {
        private readonly OrdenConteoDto _orden;
        private readonly ConteosService _conteosService;

        [ObservableProperty]
        private bool _isCargandoLineas = false;

        [ObservableProperty]
        private string _mensajeEstado = "";

        [ObservableProperty]
        private ObservableCollection<LecturaResponseDto> _lineasConteo = new();

        public bool TieneLineas => LineasConteo.Count > 0;

        public VerOrdenConteoDialogViewModel(OrdenConteoDto orden, ConteosService conteosService)
        {
            _orden = orden;
            _conteosService = conteosService;
            
            // Cargar líneas automáticamente al inicializar
            _ = CargarLineasConteoAsync();
        }

        #region Propiedades para el binding
        public string Titulo => _orden?.Titulo ?? "Sin título";
        public string GuidID => _orden?.GuidID.ToString() ?? "";
        public string EstadoFormateado => _orden?.EstadoFormateado ?? "";
        public string AlcanceFormateado => _orden?.AlcanceFormateado ?? "";
        public string PrioridadTexto => _orden?.PrioridadTexto ?? "";
        public string CodigoEmpresa => _orden?.CodigoEmpresa.ToString() ?? "";
        public string CodigoAlmacen => _orden?.CodigoAlmacen ?? "N/A";
        public string CodigoUbicacion => _orden?.CodigoUbicacion ?? "N/A";
        public string CodigoArticulo => _orden?.CodigoArticulo ?? "N/A";
        public string OperarioDisplay => !string.IsNullOrEmpty(_orden?.NombreOperario) 
            ? _orden.NombreOperario 
            : "Sin asignar";
        public string CreadoPorDisplay => _orden?.CreadorDisplay ?? "N/A";
        public string FechaPlan => _orden?.FechaPlan?.ToString("dd/MM/yyyy") ?? "N/A";
        public string FechaCreacion => _orden?.FechaCreacion.ToString("dd/MM/yyyy HH:mm") ?? "";
        public string FechaAsignacion => _orden?.FechaAsignacion?.ToString("dd/MM/yyyy HH:mm") ?? "N/A";
        public string FechaInicio => _orden?.FechaInicio?.ToString("dd/MM/yyyy HH:mm") ?? "N/A";
        public string FechaCierre => _orden?.FechaCierre?.ToString("dd/MM/yyyy HH:mm") ?? "N/A";
        public string ComentarioDisplay => string.IsNullOrEmpty(_orden?.Comentario) 
            ? "Sin comentarios" 
            : _orden.Comentario;

        public string EstadoColor
        {
            get
            {
                return _orden?.Estado switch
                {
                    "PLANIFICADO" => "#0D6EFD",
                    "ASIGNADO" => "#FD7E14", 
                    "EN_PROCESO" => "#198754",
                    "CERRADO" => "#6C757D",
                    "CANCELADO" => "#DC3545",
                    _ => "#6C757D"
                };
            }
        }
        #endregion

        [RelayCommand]
        private async Task CargarLineasConteoAsync()
        {
            try
            {
                IsCargandoLineas = true;
                MensajeEstado = "Consultando líneas de conteo...";

                var lineas = await _conteosService.ObtenerLineasConteoAsync(_orden.GuidID, _orden.CodigoOperario);
                
                LineasConteo.Clear();
                foreach (var linea in lineas)
                {
                    LineasConteo.Add(linea);
                }

                // Notificar cambio en TieneLineas
                OnPropertyChanged(nameof(TieneLineas));

                if (lineas.Count > 0)
                {
                    MensajeEstado = $"Se encontraron {lineas.Count} lecturas registradas";
                }
                else
                {
                    MensajeEstado = "No hay lecturas registradas";
                }
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error: {ex.Message}";
            }
            finally
            {
                IsCargandoLineas = false;
            }
        }
    }
}
