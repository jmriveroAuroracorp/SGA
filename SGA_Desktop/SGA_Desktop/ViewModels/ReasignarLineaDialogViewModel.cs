using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Dialog;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SGA_Desktop.ViewModels
{
    public partial class ReasignarLineaDialogViewModel : ObservableObject
    {
        #region Fields & Services
        private readonly ConteosService _conteosService;
        private readonly LoginService _loginService;
        #endregion

        #region Constructor
        public ReasignarLineaDialogViewModel(ConteosService conteosService, LoginService loginService)
        {
            _conteosService = conteosService;
            _loginService = loginService;
            
            OperariosDisponibles = new ObservableCollection<OperariosAccesoDto>();
        }

        public ReasignarLineaDialogViewModel() : this(new ConteosService(), new LoginService()) { }
        #endregion

        #region Observable Properties
        public ObservableCollection<OperariosAccesoDto> OperariosDisponibles { get; }

        [ObservableProperty]
        private ResultadoConteoDetalladoDto? resultadoSeleccionado;

        [ObservableProperty]
        private OperariosAccesoDto? operarioSeleccionado;

        [ObservableProperty]
        private string comentario = string.Empty;

        [ObservableProperty]
        private bool isCargando = false;

        [ObservableProperty]
        private string mensajeEstado = string.Empty;

        // Referencia al diálogo para cerrarlo
        public Window? DialogResult { get; set; }
        #endregion

        #region Computed Properties
        public bool PuedeReasignar => !IsCargando && 
                                   ResultadoSeleccionado != null && 
                                   OperarioSeleccionado != null &&
                                   OperarioSeleccionado.Operario != 0;

        public string TituloDialogo => $"Reasignar Línea - {ResultadoSeleccionado?.Titulo ?? ""}";
        #endregion

        #region Commands
        [RelayCommand]
        private async Task ReasignarLinea()
        {
            if (ResultadoSeleccionado == null || OperarioSeleccionado == null)
                return;

            try
            {
                IsCargando = true;
                MensajeEstado = "Reasignando línea de conteo...";

                var nuevaOrden = await _conteosService.ReasignarLineaAsync(
                    ResultadoSeleccionado.GuidID, 
                    OperarioSeleccionado.Operario.ToString(),
                    string.IsNullOrWhiteSpace(Comentario) ? "Reasignación por supervisión" : Comentario.Trim(),
                    SessionManager.UsuarioActual?.operario.ToString());

                // Mostrar mensaje de éxito
                var successDialog = new WarningDialog(
                    "Línea Reasignada",
                    $"La línea ha sido reasignada exitosamente.\n\n" +
                    $"Nueva Orden: {nuevaOrden.Titulo}\n" +
                    $"GUID: {nuevaOrden.GuidID}\n" +
                    $"Operario: {OperarioSeleccionado.NombreCompleto}\n\n" +
                    $"La nueva orden está lista para ser ejecutada.",
                    "\uE930"); // Ícono de éxito/checkmark
                ShowCenteredDialog(successDialog);

                // Cerrar el diálogo con resultado exitoso
                var window = Application.Current.Windows.OfType<ReasignarLineaDialog>().FirstOrDefault();
                if (window != null)
                {
                    window.DialogResult = true;
                    window.Close();
                }
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog(
                    "Error al reasignar línea",
                    $"No se pudo reasignar la línea de conteo: {ex.Message}");
                ShowCenteredDialog(errorDialog);
                MensajeEstado = "Error al reasignar línea";
            }
            finally
            {
                IsCargando = false;
            }
        }

        [RelayCommand]
        private void Cancelar()
        {
            var window = Application.Current.Windows.OfType<ReasignarLineaDialog>().FirstOrDefault();
            if (window != null)
            {
                window.DialogResult = false;
                window.Close();
            }
        }
        #endregion

        #region Private Methods
        private void ShowCenteredDialog(WarningDialog dialog)
        {
            var mainWindow = Application.Current.Windows.OfType<Window>()
                .FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
            
            if (mainWindow != null && mainWindow != dialog)
            {
                dialog.Owner = mainWindow;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
                
            dialog.ShowDialog();
        }

        private async Task InitializeAsync()
        {
            try
            {
                await CargarOperariosAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en InitializeAsync: {ex.Message}");
            }
        }

        public async Task CargarOperariosAsync()
        {
            try
            {
                var operarios = await _loginService.ObtenerOperariosConAccesoConteosAsync();

                OperariosDisponibles.Clear();

                foreach (var operario in operarios.OrderBy(o => o.NombreOperario))
                {
                    OperariosDisponibles.Add(operario);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cargando operarios: {ex.Message}");
                OperariosDisponibles.Clear();
            }
        }

        // Métodos de cambio de propiedades
        partial void OnResultadoSeleccionadoChanged(ResultadoConteoDetalladoDto? value)
        {
            OnPropertyChanged(nameof(PuedeReasignar));
            OnPropertyChanged(nameof(TituloDialogo));
        }

        partial void OnOperarioSeleccionadoChanged(OperariosAccesoDto? value)
        {
            OnPropertyChanged(nameof(PuedeReasignar));
        }

        partial void OnIsCargandoChanged(bool value)
        {
            OnPropertyChanged(nameof(PuedeReasignar));
        }
        #endregion
    }
} 