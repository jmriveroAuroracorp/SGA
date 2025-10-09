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
    /// Opciones para el filtro de estado de operarios
    /// </summary>
    public enum FiltroEstadoOperario
    {
        Activos = 0,
        Inactivos = 1
    }

    /// <summary>
    /// ViewModel para la pantalla de configuración de operarios desde Aurora
    /// </summary>
    public partial class ConfiguracionOperariosViewModel : ObservableObject
    {
        #region Fields & Services
        private readonly OperariosConfiguracionService _operariosService;
        private readonly ConfiguracionesPredefinidasService _configuracionesService;
        private readonly EmpleadosDisponiblesService _empleadosService;
        private readonly ICollectionView _operariosView;
        private readonly ICollectionView _configuracionesView;
        #endregion

        #region Observable Properties
        [ObservableProperty]
        private ObservableCollection<OperarioListaDto> operarios = new();

        [ObservableProperty]
        private OperarioListaDto? operarioSeleccionado;

        [ObservableProperty]
        private string filtroNombre = string.Empty;

        [ObservableProperty]
        private FiltroEstadoOperario filtroEstado = FiltroEstadoOperario.Activos; // Por defecto mostrar solo activos

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

        [ObservableProperty]
        private string filtroConfiguracion = string.Empty;

        // Propiedades para control de pestañas
        [ObservableProperty]
        private bool mostrandoOperarios = true;

        [ObservableProperty]
        private bool mostrandoConfiguraciones = false;

        [ObservableProperty]
        private bool mostrandoAltaSga = false;

        // Propiedades para Alta SGA
        [ObservableProperty]
        private ObservableCollection<EmpleadoDisponibleDto> empleadosDisponibles = new();

        [ObservableProperty]
        private string filtroEmpleado = string.Empty;

        [ObservableProperty]
        private bool cargandoEmpleados = false;

        [ObservableProperty]
        private string mensajeEmpleados = "Cargando empleados disponibles...";

        // Vista filtrable para empleados
        private readonly ICollectionView _empleadosView;
        #endregion

        #region Constructor
        public ConfiguracionOperariosViewModel(OperariosConfiguracionService operariosService)
        {
            _operariosService = operariosService;
            _configuracionesService = new ConfiguracionesPredefinidasService();
            _empleadosService = new EmpleadosDisponiblesService();
            
            // Configurar vista filtrable
            _operariosView = CollectionViewSource.GetDefaultView(Operarios);
            _operariosView.Filter = FiltrarOperario;
            
            // Configurar vista filtrable para empleados
            _empleadosView = CollectionViewSource.GetDefaultView(EmpleadosDisponibles);
            _empleadosView.Filter = FiltrarEmpleado;
            
            // Configurar vista filtrable para configuraciones
            _configuracionesView = CollectionViewSource.GetDefaultView(ConfiguracionesPredefinidas);
            _configuracionesView.Filter = FiltrarConfiguracion;
            
            // Cargar datos iniciales
            _ = CargarOperariosAsync();
            
            // Cargar configuraciones predefinidas al inicializar
            _ = CargarConfiguracionesPredefinidasAsync();
            
            // Cargar empleados disponibles al inicializar (para la pestaña Alta SGA)
            _ = CargarEmpleadosDisponiblesAsync();
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
            FiltroEmpleado = string.Empty;
            FiltroConfiguracion = string.Empty;
            FiltroEstado = FiltroEstadoOperario.Activos; // Resetear a activos por defecto
            _operariosView.Refresh();
            _empleadosView?.Refresh();
            _configuracionesView?.Refresh();
        }

        [RelayCommand]
        private void LimpiarFiltrosConfiguracion()
        {
            FiltroConfiguracion = string.Empty;
            _configuracionesView?.Refresh();
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
                bool? soloActivos = FiltroEstado switch
                {
                    FiltroEstadoOperario.Activos => true,
                    FiltroEstadoOperario.Inactivos => false,
                    _ => true
                };
                var operariosDisponibles = await _operariosService.ObtenerOperariosDisponiblesAsync(soloActivos);
                
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
                            FechaBaja = operario.FechaBaja,
                            Activo = operario.Activo,
                            Permisos = operario.Permisos,
                            Empresas = operario.Empresas,
                            Almacenes = operario.Almacenes,
                            CantidadPermisos = operario.CantidadPermisos,
                            CantidadAlmacenes = operario.CantidadAlmacenes,
                            PlantillaAplicada = operario.PlantillaAplicada,
                            RolNombre = operario.RolNombre,
                            NivelJerarquico = operario.NivelJerarquico
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

            // Filtro por nombre (ignorando tildes y acentos)
            if (!string.IsNullOrWhiteSpace(FiltroNombre))
            {
                var nombre = operario.Nombre ?? string.Empty;
                var nombreNormalizado = NormalizarTexto(nombre);
                var filtroNormalizado = NormalizarTexto(FiltroNombre);
                
                if (!nombreNormalizado.Contains(filtroNormalizado, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        private bool FiltrarEmpleado(object item)
        {
            if (item is not EmpleadoDisponibleDto empleado)
                return false;

            // Filtro por nombre (ignorando tildes y acentos)
            if (!string.IsNullOrWhiteSpace(FiltroEmpleado))
            {
                var nombre = empleado.Nombre ?? string.Empty;
                var nombreNormalizado = NormalizarTexto(nombre);
                var filtroNormalizado = NormalizarTexto(FiltroEmpleado);
                
                if (!nombreNormalizado.Contains(filtroNormalizado, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        private bool FiltrarConfiguracion(object item)
        {
            if (item is not ConfiguracionPredefinidaDto configuracion)
                return false;

            // Filtro por nombre (ignorando tildes y acentos)
            if (!string.IsNullOrWhiteSpace(FiltroConfiguracion))
            {
                var nombre = configuracion.Nombre ?? string.Empty;
                var nombreNormalizado = NormalizarTexto(nombre);
                var filtroNormalizado = NormalizarTexto(FiltroConfiguracion);
                
                if (!nombreNormalizado.Contains(filtroNormalizado, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Normaliza texto quitando tildes, acentos y signos de puntuación para facilitar la búsqueda
        /// </summary>
        private static string NormalizarTexto(string texto)
        {
            if (string.IsNullOrEmpty(texto))
                return string.Empty;

            // Quitar tildes y acentos
            var textoNormalizado = texto.Normalize(System.Text.NormalizationForm.FormD);
            var sinAcentos = new System.Text.StringBuilder();
            
            foreach (char c in textoNormalizado)
            {
                var categoria = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (categoria != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    sinAcentos.Append(c);
                }
            }

            // Convertir a minúsculas y quitar espacios extra
            return sinAcentos.ToString().ToLowerInvariant().Trim();
        }

        private async Task MostrarMensajeAsync(string mensaje)
        {
            Mensaje = mensaje;
            
            // También mostrar en diálogo si es necesario
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var infoDialog = new WarningDialog(
                    "Configuración de Operarios",
                    mensaje,
                    "\uE946" // ícono de información
                );
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                           ?? Application.Current.MainWindow;
                if (owner != null && owner != infoDialog)
                    infoDialog.Owner = owner;
                infoDialog.ShowDialog();
            });
        }
        #endregion

        #region Property Changed Handlers
        partial void OnFiltroNombreChanged(string value)
        {
            // Solo filtrar si hay 3 o más caracteres para reducir carga
            if (string.IsNullOrEmpty(value) || value.Length >= 3)
            {
                _operariosView?.Refresh();
            }
        }

        partial void OnFiltroEstadoChanged(FiltroEstadoOperario value)
        {
            // Recargar operarios cuando cambie el filtro de estado
            _ = CargarOperariosAsync();
        }

        partial void OnFiltroEmpleadoChanged(string value)
        {
            // Solo filtrar si hay 3 o más caracteres para reducir carga
            if (string.IsNullOrEmpty(value) || value.Length >= 3)
            {
                _empleadosView?.Refresh();
            }
        }

        partial void OnFiltroConfiguracionChanged(string value)
        {
            // Solo filtrar si hay 3 o más caracteres para reducir carga
            if (string.IsNullOrEmpty(value) || value.Length >= 3)
            {
                _configuracionesView?.Refresh();
            }
        }

        #endregion

        #region Comandos para Control de Pestañas
        [RelayCommand]
        private void CambiarAOperarios()
        {
            MostrandoOperarios = true;
            MostrandoConfiguraciones = false;
            MostrandoAltaSga = false;
        }

        [RelayCommand]
        private void CambiarAConfiguraciones()
        {
            MostrandoOperarios = false;
            MostrandoConfiguraciones = true;
            MostrandoAltaSga = false;
        }

        [RelayCommand]
        private async Task CambiarAAltaSga()
        {
            MostrandoOperarios = false;
            MostrandoConfiguraciones = false;
            MostrandoAltaSga = true;
            
            // Cargar empleados disponibles cuando se cambie a esta pestaña
            await CargarEmpleadosDisponiblesAsync();
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
                // Verificar si hay operarios asociados a esta configuración
                var operariosAsociados = await _configuracionesService.VerificarOperariosAsociadosAsync(configuracion.Id);
                
                string mensajeConfirmacion;
                if (operariosAsociados?.TieneOperariosAsociados == true)
                {
                    mensajeConfirmacion = $"¿Estás seguro de que quieres eliminar la configuración '{configuracion.Nombre}'?\n\n" +
                                        $"⚠️ ADVERTENCIA: Esta configuración está aplicada a {operariosAsociados.CantidadOperarios} operario(s).\n\n" +
                                        $"Al eliminar la configuración:\n" +
                                        $"• Los operarios serán desasociados de la plantilla\n" +
                                        $"• Su configuración actual (permisos, empresas, almacenes, límites) se mantendrá sin cambios\n" +
                                        $"• Ya no recibirán actualizaciones automáticas de esta plantilla\n\n" +
                                        $"¿Deseas continuar con la eliminación?";
                }
                else
                {
                    mensajeConfirmacion = $"¿Estás seguro de que quieres eliminar la configuración '{configuracion.Nombre}'?";
                }

                var confirmacionEliminar = new ConfirmationDialog(
                    "Confirmar eliminación",
                    mensajeConfirmacion
                );
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                           ?? Application.Current.MainWindow;
                if (owner != null && owner != confirmacionEliminar)
                    confirmacionEliminar.Owner = owner;
                    
                var result = confirmacionEliminar.ShowDialog();

                if (result == true)
                {
                    var resultadoEliminacion = await _configuracionesService.EliminarConfiguracionPredefinidaAsync(configuracion.Id);
                    if (resultadoEliminacion.Success)
                    {
                        string mensajeExito = "Configuración eliminada exitosamente.";
                        if (resultadoEliminacion.OperariosDesasociados > 0)
                        {
                            mensajeExito += $"\n\nSe desasociaron {resultadoEliminacion.OperariosDesasociados} operario(s) de esta plantilla.";
                        }
                        
                        await MostrarMensajeAsync(mensajeExito);
                        await CargarConfiguracionesPredefinidasAsync();
                    }
                    else
                    {
                        await MostrarMensajeAsync($"Error al eliminar la configuración: {resultadoEliminacion.Message}");
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

        #region Comandos para Alta SGA
        [RelayCommand]
        private async Task CargarEmpleadosDisponibles()
        {
            await CargarEmpleadosDisponiblesAsync();
        }

        [RelayCommand]
        private async Task DarAltaEmpleado(EmpleadoDisponibleDto? empleado = null)
        {
            if (empleado == null)
            {
                await MostrarMensajeAsync("No se ha seleccionado ningún empleado.");
                return;
            }

            try
            {
                // Crear DTO para dar de alta
                var darAltaDto = new DarAltaEmpleadoDto
                {
                    CodigoEmpleado = empleado.CodigoEmpleado,
                    Nombre = empleado.Nombre,
                    Contraseña = empleado.CodigoEmpleado.ToString(), // Por defecto, la contraseña es el código
                    CodigoCentro = "1", // Por defecto
                    PermisosIniciales = new List<short>(), // El API asigna el permiso 10 por defecto
                    EmpresasIniciales = new List<short> { 1 }, // Empresa por defecto
                    AlmacenesIniciales = new List<string>() // Sin almacenes iniciales
                };

                var resultado = await _empleadosService.DarAltaEmpleadoAsync(darAltaDto);

                if (resultado.Exito)
                {
                    await MostrarMensajeAsync($"Empleado {empleado.Nombre} dado de alta correctamente en SGA.");
                    
                    // Remover de la lista de disponibles
                    EmpleadosDisponibles.Remove(empleado);
                    
                    // Actualizar mensaje
                    MensajeEmpleados = $"Se cargaron {EmpleadosDisponibles.Count} empleados disponibles.";
                }
                else
                {
                    await MostrarMensajeAsync($"Error al dar de alta al empleado {empleado.Nombre}.\n{resultado.Mensaje}");
                }
            }
            catch (Exception ex)
            {
                await MostrarMensajeAsync($"Error al dar de alta empleado: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task DarAltaSeleccionados()
        {
            var empleadosSeleccionados = EmpleadosDisponibles.Where(e => e.IsSelected).ToList();

            if (!empleadosSeleccionados.Any())
            {
                await MostrarMensajeAsync("No se ha seleccionado ningún empleado para dar de alta.");
                return;
            }

            try
            {
                var darAltaDtos = empleadosSeleccionados.Select(e => new DarAltaEmpleadoDto
                {
                    CodigoEmpleado = e.CodigoEmpleado,
                    Nombre = e.Nombre,
                    Contraseña = e.CodigoEmpleado.ToString(),
                    CodigoCentro = "1",
                    PermisosIniciales = new List<short>(), // El API asigna el permiso 10 por defecto
                    EmpresasIniciales = new List<short> { 1 },
                    AlmacenesIniciales = new List<string>()
                }).ToList();

                var resultados = await _empleadosService.DarAltaEmpleadosAsync(darAltaDtos);

                var exitosos = resultados.Count(r => r.Exito);
                var fallidos = resultados.Count(r => !r.Exito);

                if (exitosos > 0)
                {
                    // Remover empleados exitosos de la lista
                    foreach (var resultado in resultados.Where(r => r.Exito))
                    {
                        var empleado = EmpleadosDisponibles.FirstOrDefault(e => e.CodigoEmpleado == resultado.CodigoEmpleado);
                        if (empleado != null)
                        {
                            EmpleadosDisponibles.Remove(empleado);
                        }
                    }
                }

                var mensaje = $"Proceso completado:\n✅ {exitosos} empleados dados de alta correctamente";
                if (fallidos > 0)
                {
                    mensaje += $"\n❌ {fallidos} empleados con errores";
                }

                await MostrarMensajeAsync(mensaje);
                MensajeEmpleados = $"Se cargaron {EmpleadosDisponibles.Count} empleados disponibles.";
            }
            catch (Exception ex)
            {
                await MostrarMensajeAsync($"Error al dar de alta empleados: {ex.Message}");
            }
        }

        private async Task CargarEmpleadosDisponiblesAsync()
        {
            try
            {
                CargandoEmpleados = true;
                MensajeEmpleados = "Cargando empleados disponibles...";
                
                var empleados = await _empleadosService.ObtenerEmpleadosDisponiblesAsync();

                if (empleados != null)
                {
                    EmpleadosDisponibles.Clear();
                    foreach (var empleado in empleados)
                    {
                        EmpleadosDisponibles.Add(empleado);
                    }

                    MensajeEmpleados = $"Se cargaron {empleados.Count} empleados disponibles para dar de alta.";
                }
                else
                {
                    MensajeEmpleados = "Error al cargar empleados disponibles.";
                }
            }
            catch (Exception ex)
            {
                MensajeEmpleados = $"Error: {ex.Message}";
            }
            finally
            {
                CargandoEmpleados = false;
            }
        }
        #endregion
    }
}
