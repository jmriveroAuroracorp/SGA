using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Dialog;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;

namespace SGA_Desktop.ViewModels
{
    /// <summary>
    /// ViewModel para la pantalla de configuración de operarios desde Aurora
    /// </summary>
    public partial class ConfiguracionOperariosViewModel : ObservableObject
    {
        #region Fields & Services
        private readonly OperariosConfiguracionService _operariosService;
        private readonly ConfiguracionesPredefinidasService _configuracionesService;
        private readonly ICollectionView _operariosView;
        #endregion

        #region Observable Properties
        [ObservableProperty]
        private ObservableCollection<OperarioListaDto> operarios = new();

        [ObservableProperty]
        private OperarioListaDto? operarioSeleccionado;

        [ObservableProperty]
        private string filtroNombre = string.Empty;


        [ObservableProperty]
        private bool cargando = false;

        [ObservableProperty]
        private string mensaje = string.Empty;

        // Propiedades para configuraciones predefinidas
        [ObservableProperty]
        private ObservableCollection<ConfiguracionPredefinidaDto> configuracionesPredefinidas = new();

        [ObservableProperty]
        private bool cargandoConfiguraciones = false;

        [ObservableProperty]
        private string mensajeConfiguraciones = string.Empty;

        // Propiedades para control de pestañas
        [ObservableProperty]
        private bool mostrandoOperarios = true;

        [ObservableProperty]
        private bool mostrandoConfiguraciones = false;
        #endregion

        #region Constructor
        public ConfiguracionOperariosViewModel(OperariosConfiguracionService operariosService)
        {
            _operariosService = operariosService;
            _configuracionesService = new ConfiguracionesPredefinidasService();
            
            // Configurar vista filtrable
            _operariosView = CollectionViewSource.GetDefaultView(Operarios);
            _operariosView.Filter = FiltrarOperario;
            
            // Cargar datos iniciales
            _ = CargarOperariosAsync();
            
            // Cargar configuraciones predefinidas al inicializar
            _ = CargarConfiguracionesPredefinidasAsync();
        }

        public ConfiguracionOperariosViewModel() : this(new OperariosConfiguracionService())
        {
            // Constructor sin parámetros para el diseñador
        }
        #endregion

        #region Commands
        [RelayCommand]
        private async Task CargarOperarios()
        {
            await CargarOperariosAsync();
        }

        [RelayCommand]
        private async Task ConfigurarOperario(OperarioListaDto? operario = null)
        {
            // Usar el parámetro si se proporciona, sino usar el seleccionado
            var operarioAConfigurar = operario ?? OperarioSeleccionado;
            
            if (operarioAConfigurar == null)
            {
                await MostrarMensajeAsync("Por favor seleccione un operario para configurar.");
                return;
            }

            try
            {
                Cargando = true;
                
                // Obtener configuración completa del operario
                var configuracion = await _operariosService.ObtenerConfiguracionOperarioAsync(operarioAConfigurar.Id);
                
                if (configuracion != null)
                {
                    // Abrir diálogo de configuración
                    var dialog = new ConfiguracionOperarioDialog(configuracion, _operariosService);
                    var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                             ?? Application.Current.MainWindow;
                    if (owner != null && owner != dialog)
                        dialog.Owner = owner;
                    
                    var result = dialog.ShowDialog();
                    
                    if (result == true)
                    {
                        // Recargar lista después de cambios
                        await CargarOperariosAsync();
                        // No mostrar mensaje aquí porque ya se muestra en el diálogo
                    }
                }
                else
                {
                    await MostrarMensajeAsync("Error al cargar la configuración del operario.");
                }
            }
            catch (Exception ex)
            {
                await MostrarMensajeAsync($"Error al configurar operario: {ex.Message}");
            }
            finally
            {
                Cargando = false;
            }
        }

        [RelayCommand]
        private void GestionarEmpresas(OperarioListaDto operario = null)
        {
            try
            {
                // Abrir diálogo de selección de operario
                var seleccionDialog = new SeleccionOperarioDialog(_operariosService);
                var result = seleccionDialog.ShowDialog();
                
                if (result == true && seleccionDialog.OperarioSeleccionado != null)
                {
                    var operarioSeleccionado = seleccionDialog.OperarioSeleccionado;
                    
                    // Abrir diálogo de gestión de empresas
                    var gestionDialog = new GestionEmpresasOperarioDialog(
                        operarioSeleccionado.Id, 
                        operarioSeleccionado.Nombre, 
                        _operariosService);
                    gestionDialog.ShowDialog();
                    
                    // Recargar lista después de posibles cambios
                    _ = CargarOperariosAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al abrir gestión de empresas: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        [RelayCommand]
        private void AplicarFiltros()
        {
            _operariosView.Refresh();
        }

        [RelayCommand]
        private void LimpiarFiltros()
        {
            FiltroNombre = string.Empty;
            _operariosView.Refresh();
        }
        #endregion

        #region Private Methods
        private async Task CargarOperariosAsync()
        {
            try
            {
                Cargando = true;
                Mensaje = "Cargando operarios con acceso SGA...";
                
                // Usar el endpoint que filtra por operarios con acceso SGA
                var operariosDisponibles = await _operariosService.ObtenerOperariosDisponiblesAsync();
                
                if (operariosDisponibles != null)
                {
                    Operarios.Clear();
                    foreach (var operario in operariosDisponibles)
                    {
                        // Convertir OperarioDisponibleDto a OperarioListaDto para la vista
                        var operarioLista = new OperarioListaDto
                        {
                            Id = operario.Id,
                            Nombre = operario.Nombre,
                            CodigoCentro = operario.CodigoCentro,
                            Permisos = operario.Permisos,
                            Empresas = operario.Empresas,
                            Almacenes = operario.Almacenes,
                            CantidadPermisos = operario.CantidadPermisos,
                            CantidadAlmacenes = operario.CantidadAlmacenes,
                            PlantillaAplicada = operario.PlantillaAplicada // ¡FALTABA ESTA LÍNEA!
                        };
                        Operarios.Add(operarioLista);
                    }
                    
                    Mensaje = $"Se cargaron {operariosDisponibles.Count} operarios con acceso SGA.";
                }
                else
                {
                    Mensaje = "Error al cargar operarios.";
                }
            }
            catch (Exception ex)
            {
                Mensaje = $"Error: {ex.Message}";
            }
            finally
            {
                Cargando = false;
            }
        }

        private bool FiltrarOperario(object item)
        {
            if (item is not OperarioListaDto operario)
                return false;


            // Filtro por nombre
            if (!string.IsNullOrWhiteSpace(FiltroNombre))
            {
                var nombre = operario.Nombre ?? string.Empty;
                if (!nombre.Contains(FiltroNombre, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        private async Task MostrarMensajeAsync(string mensaje)
        {
            Mensaje = mensaje;
            
            // También mostrar en diálogo si es necesario
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(mensaje, "Configuración de Operarios", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }
        #endregion

        #region Property Changed Handlers
        partial void OnFiltroNombreChanged(string value)
        {
            _operariosView?.Refresh();
        }

        #endregion

        #region Comandos para Control de Pestañas
        [RelayCommand]
        private void CambiarAOperarios()
        {
            MostrandoOperarios = true;
            MostrandoConfiguraciones = false;
        }

        [RelayCommand]
        private void CambiarAConfiguraciones()
        {
            MostrandoOperarios = false;
            MostrandoConfiguraciones = true;
        }
        #endregion

        #region Comandos para Configuraciones Predefinidas
        [RelayCommand]
        private async Task CrearConfiguracion()
        {
            try
            {
                var dialog = new ConfiguracionPredefinidaDialog();
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
                dialog.Owner = owner;
                
                var result = dialog.ShowDialog();
                if (result == true)
                {
                    await CargarConfiguracionesPredefinidasAsync();
                }
            }
            catch (Exception ex)
            {
                await MostrarMensajeAsync($"Error al crear configuración: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task RecargarConfiguraciones()
        {
            await CargarConfiguracionesPredefinidasAsync();
        }

        [RelayCommand]
        private async Task EditarConfiguracion(ConfiguracionPredefinidaDto configuracion)
        {
            if (configuracion == null) return;

            try
            {
                // Obtener la configuración completa
                var configuracionCompleta = await _configuracionesService.ObtenerConfiguracionPredefinidaAsync(configuracion.Id);
                if (configuracionCompleta == null)
                {
                    await MostrarMensajeAsync("No se pudo cargar la configuración para editar.");
                    return;
                }

                var dialog = new ConfiguracionPredefinidaDialog(configuracionCompleta);
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
                dialog.Owner = owner;
                
                var result = dialog.ShowDialog();
                if (result == true)
                {
                    await CargarConfiguracionesPredefinidasAsync();
                }
            }
            catch (Exception ex)
            {
                await MostrarMensajeAsync($"Error al editar configuración: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task VerConfiguracion(ConfiguracionPredefinidaDto configuracion)
        {
            if (configuracion == null) return;

            try
            {
                // TODO: Implementar diálogo de vista de configuración
                await MostrarMensajeAsync($"Ver configuración: {configuracion.Nombre}");
            }
            catch (Exception ex)
            {
                await MostrarMensajeAsync($"Error al ver configuración: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task EliminarConfiguracion(ConfiguracionPredefinidaDto configuracion)
        {
            if (configuracion == null) return;

            try
            {
                var result = MessageBox.Show(
                    $"¿Estás seguro de que quieres eliminar la configuración '{configuracion.Nombre}'?",
                    "Confirmar eliminación",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var exito = await _configuracionesService.EliminarConfiguracionPredefinidaAsync(configuracion.Id);
                    if (exito)
                    {
                        await MostrarMensajeAsync("Configuración eliminada exitosamente.");
                        await CargarConfiguracionesPredefinidasAsync();
                    }
                    else
                    {
                        await MostrarMensajeAsync("Error al eliminar la configuración.");
                    }
                }
            }
            catch (Exception ex)
            {
                await MostrarMensajeAsync($"Error al eliminar configuración: {ex.Message}");
            }
        }

        private async Task CargarConfiguracionesPredefinidasAsync()
        {
            try
            {
                CargandoConfiguraciones = true;
                MensajeConfiguraciones = "Cargando configuraciones predefinidas...";

                var configuraciones = await _configuracionesService.ObtenerConfiguracionesPredefinidasAsync();

                if (configuraciones != null)
                {
                    ConfiguracionesPredefinidas.Clear();
                    foreach (var config in configuraciones)
                    {
                        ConfiguracionesPredefinidas.Add(config);
                    }

                    MensajeConfiguraciones = $"Se cargaron {configuraciones.Count} configuraciones predefinidas.";
                }
                else
                {
                    MensajeConfiguraciones = "Error al cargar configuraciones predefinidas.";
                }
            }
            catch (Exception ex)
            {
                MensajeConfiguraciones = $"Error: {ex.Message}";
            }
            finally
            {
                CargandoConfiguraciones = false;
            }
        }
        #endregion
    }
}
