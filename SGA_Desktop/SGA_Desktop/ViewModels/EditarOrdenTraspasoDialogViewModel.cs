using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using SGA_Desktop.Dialog;
using SGA_Desktop.Helpers;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SGA_Desktop.ViewModels
{
    public partial class EditarOrdenTraspasoDialogViewModel : ObservableObject
    {
        #region Fields & Services
        private readonly OrdenTraspasoService _ordenTraspasoService;
        private readonly LoginService _loginService;
        private readonly StockService _stockService;
        #endregion

        #region Observable Properties
        [ObservableProperty]
        private bool isCargando;

        partial void OnIsCargandoChanged(bool value)
        {
            OnPropertyChanged(nameof(PuedeCrearOrden));
        }

        [ObservableProperty]
        private string mensajeEstado = "";

        // Información de la orden a editar
        [ObservableProperty]
        private OrdenTraspasoDto ordenOriginal;

        // Información básica
        [ObservableProperty]
        private ObservableCollection<PrioridadItem> prioridadesDisponibles = new();

        [ObservableProperty]
        private PrioridadItem? prioridadSeleccionada;

        [ObservableProperty]
        private DateTime? fechaPlan;

        [ObservableProperty]
        private string tipoOrigen = "SGA";

        [ObservableProperty]
        private ObservableCollection<AlmacenDto> almacenesDisponibles = new();

        [ObservableProperty]
        private AlmacenDto? almacenDestinoSeleccionado;

        [ObservableProperty]
        private bool almacenDestinoHabilitado = true;

        [ObservableProperty]
        private bool almacenDestinoBloqueado = false;

        [ObservableProperty]
        private string comentarios = "";

        // Líneas de la orden
        [ObservableProperty]
        private ObservableCollection<CrearLineaOrdenTraspasoDto> lineas = new();

        [ObservableProperty]
        private CrearLineaOrdenTraspasoDto? lineaSeleccionada;

        partial void OnLineaSeleccionadaChanged(CrearLineaOrdenTraspasoDto? value)
        {
            ActualizarEstadoEliminacion();
        }

        [ObservableProperty]
        private bool puedeEliminarLinea = false;

        // Operarios disponibles
        [ObservableProperty]
        private ObservableCollection<OperariosAccesoDto> operariosDisponibles = new();

        #endregion

        #region Constructor
        public EditarOrdenTraspasoDialogViewModel()
        {
            _ordenTraspasoService = new OrdenTraspasoService();
            _loginService = new LoginService();
            _stockService = new StockService();

            InicializarPrioridades();
            CargarAlmacenes();
            CargarOperarios();
        }

        public EditarOrdenTraspasoDialogViewModel(OrdenTraspasoDto orden) : this()
        {
            OrdenOriginal = orden;
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                await CargarAlmacenes();
                await CargarOperarios();
                
                // Cargar datos de la orden después de tener operarios disponibles
                CargarDatosOrden();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en InitializeAsync: {ex.Message}");
            }
        }
        #endregion

        #region Inicialización
        private void InicializarPrioridades()
        {
            PrioridadesDisponibles.Clear();
            PrioridadesDisponibles.Add(new PrioridadItem { Valor = 1, Texto = "Muy Baja" });
            PrioridadesDisponibles.Add(new PrioridadItem { Valor = 2, Texto = "Baja" });
            PrioridadesDisponibles.Add(new PrioridadItem { Valor = 3, Texto = "Normal" });
            PrioridadesDisponibles.Add(new PrioridadItem { Valor = 4, Texto = "Alta" });
            PrioridadesDisponibles.Add(new PrioridadItem { Valor = 5, Texto = "Muy Alta" });
        }

        private async Task CargarAlmacenes()
        {
            try
            {
                var empresa = SessionManager.EmpresaSeleccionada!.Value;
                var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
                var desdeLogin = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();

                var resultado = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa, centro, desdeLogin);

                AlmacenesDisponibles.Clear();

                foreach (var a in resultado)
                    AlmacenesDisponibles.Add(a);

                AlmacenDestinoSeleccionado = AlmacenesDisponibles.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando almacenes: {ex.Message}");
                // En caso de error, agregar almacén por defecto
                AlmacenesDisponibles.Clear();
                var almacenDefecto = new AlmacenDto
                {
                    CodigoAlmacen = "01",
                    NombreAlmacen = "Almacén Principal",
                    CodigoEmpresa = (short)(SessionManager.EmpresaSeleccionada ?? 1)
                };
                AlmacenesDisponibles.Add(almacenDefecto);
                AlmacenDestinoSeleccionado = almacenDefecto;
            }
        }

        private async Task CargarOperarios()
        {
            try
            {
                // Usar el endpoint específico de conteos (mismo que otros ViewModels)
                var operarios = await _loginService.ObtenerOperariosConAccesoConteosAsync();

                OperariosDisponibles.Clear();

                foreach (var operario in operarios.OrderBy(o => o.NombreOperario))
                {
                    OperariosDisponibles.Add(operario);
                }

                // Inicializar filtrado para líneas existentes
                foreach (var linea in Lineas)
                {
                    InicializarFiltradoLinea(linea);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando operarios: {ex.Message}");
                OperariosDisponibles.Clear();
            }
        }

        private void CargarDatosOrden()
        {
            if (OrdenOriginal == null) return;

            // Cargar información básica
            PrioridadSeleccionada = PrioridadesDisponibles.FirstOrDefault(p => p.Valor == OrdenOriginal.Prioridad);
            FechaPlan = OrdenOriginal.FechaPlan;
            Comentarios = OrdenOriginal.Comentarios ?? "";
            
            // Cargar almacén destino
            AlmacenDestinoSeleccionado = AlmacenesDisponibles.FirstOrDefault(a => a.CodigoAlmacen == OrdenOriginal.CodigoAlmacenDestino);

            // Cargar líneas
            Lineas.Clear();
            foreach (var linea in OrdenOriginal.Lineas)
            {
                var nuevaLinea = new CrearLineaOrdenTraspasoDto
                {
                    Orden = linea.Orden,
                    CodigoArticulo = linea.CodigoArticulo,
                    DescripcionArticulo = linea.DescripcionArticulo,
                    FechaCaducidad = linea.FechaCaducidad,
                    CantidadPlan = linea.CantidadPlan,
                    CodigoAlmacenOrigen = linea.CodigoAlmacenOrigen,
                    UbicacionOrigen = linea.UbicacionOrigen,
                    Partida = linea.Partida,
                    PaletOrigen = linea.PaletOrigen,
                    CodigoAlmacenDestino = linea.CodigoAlmacenDestino,
                    UbicacionDestino = linea.UbicacionDestino,
                    PaletDestino = linea.PaletDestino,
                    IdOperarioAsignado = linea.IdOperarioAsignado,
                    Estado = linea.Estado
                };
                
                // Buscar y asignar el operario si existe
                if (linea.IdOperarioAsignado > 0)
                {
                    var operarioAsignado = OperariosDisponibles.FirstOrDefault(o => o.Operario == linea.IdOperarioAsignado);
                    if (operarioAsignado != null)
                    {
                        nuevaLinea.OperarioSeleccionado = operarioAsignado;
                        System.Diagnostics.Debug.WriteLine($"Operario precargado para línea {linea.CodigoArticulo}: {operarioAsignado.NombreOperario}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Operario {linea.IdOperarioAsignado} no encontrado en la lista de operarios disponibles");
                    }
                }

                // Inicializar filtrado para esta línea
                InicializarFiltradoLinea(nuevaLinea);
                Lineas.Add(nuevaLinea);
            }

            // Verificar si se puede cambiar el almacén destino
            AlmacenDestinoHabilitado = Lineas.Count == 0;
            AlmacenDestinoBloqueado = Lineas.Count > 0;
        }
        #endregion

        #region Filtrado de Operarios
        private void InicializarFiltradoLinea(CrearLineaOrdenTraspasoDto linea)
        {
            if (linea.OperariosViewLinea == null)
            {
                linea.OperariosViewLinea = CollectionViewSource.GetDefaultView(OperariosDisponibles);
                linea.OperariosViewLinea.Filter = item => FiltraOperarioLinea(item, linea);
            }
        }

        private bool FiltraOperarioLinea(object item, CrearLineaOrdenTraspasoDto linea)
        {
            if (item is not OperariosAccesoDto operario) return false;
            if (string.IsNullOrWhiteSpace(linea.FiltroOperarioLinea)) return true;

            return operario.NombreOperario.Contains(linea.FiltroOperarioLinea, StringComparison.OrdinalIgnoreCase);
        }
        #endregion

        #region Commands
        [RelayCommand]
        private void Cerrar()
        {
            var window = Application.Current.Windows.OfType<EditarOrdenTraspasoDialog>().FirstOrDefault();
            window?.Close();
        }

        [RelayCommand]
        private void Cancelar()
        {
            Cerrar();
        }

        [RelayCommand]
        private async void GuardarCambios()
        {
            try
            {
                IsCargando = true;
                MensajeEstado = "Procesando líneas...";

                var lineasActualizadas = 0;
                var lineasCreadas = 0;
                var lineasSinOperario = 0;
                
                foreach (var linea in Lineas)
                {
                    try
                    {
                        // Verificar si es una línea nueva (no existe en OrdenOriginal)
                        var lineaOriginal = OrdenOriginal.Lineas.FirstOrDefault(l => 
                            l.CodigoArticulo == linea.CodigoArticulo && 
                            l.CantidadPlan == linea.CantidadPlan);

                        if (lineaOriginal != null)
                        {
                            // Actualizar línea existente
                            var dtoActualizacion = new ActualizarLineaOrdenTraspasoDto
                            {
                                IdOperarioAsignado = linea.IdOperarioAsignado
                            };

                            var resultado = await _ordenTraspasoService.ActualizarLineaOrdenTraspasoAsync(
                                lineaOriginal.IdLineaOrden, dtoActualizacion);
                            
                            if (resultado)
                            {
                                lineasActualizadas++;
                                System.Diagnostics.Debug.WriteLine($"Línea actualizada: {linea.CodigoArticulo} -> Operario: {linea.IdOperarioAsignado}");
                            }
                        }
                        else
                        {
                            // Crear nueva línea
                            var dtoCreacion = new CrearLineaOrdenTraspasoDto
                            {
                                Orden = linea.Orden,
                                CodigoArticulo = linea.CodigoArticulo,
                                DescripcionArticulo = linea.DescripcionArticulo,
                                CantidadPlan = linea.CantidadPlan,
                                CodigoAlmacenOrigen = linea.CodigoAlmacenOrigen,
                                UbicacionOrigen = linea.UbicacionOrigen,
                                Partida = linea.Partida,
                                PaletOrigen = linea.PaletOrigen,
                                CodigoAlmacenDestino = linea.CodigoAlmacenDestino,
                                UbicacionDestino = linea.UbicacionDestino,
                                PaletDestino = linea.PaletDestino,
                                FechaCaducidad = linea.FechaCaducidad,
                                IdOperarioAsignado = linea.IdOperarioAsignado,
                                Estado = linea.Estado
                            };

                            var lineaCreada = await _ordenTraspasoService.CrearLineaOrdenTraspasoAsync(
                                OrdenOriginal.IdOrdenTraspaso, dtoCreacion);
                            
                            if (lineaCreada != null)
                            {
                                lineasCreadas++;
                                System.Diagnostics.Debug.WriteLine($"Línea creada: {linea.CodigoArticulo} -> Operario: {linea.IdOperarioAsignado}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error procesando línea {linea.CodigoArticulo}: {ex.Message}");
                    }
                    
                    // Contar líneas sin operario para el mensaje
                    if (linea.IdOperarioAsignado <= 0)
                    {
                        lineasSinOperario++;
                    }
                }

                MensajeEstado = $"Procesadas {lineasActualizadas + lineasCreadas} líneas";
                
                // Mostrar resultado con MessageBox
                var totalProcesadas = lineasActualizadas + lineasCreadas;
                if (totalProcesadas > 0)
                {
                    var mensaje = $"Se han procesado {totalProcesadas} líneas:\n";
                    
                    if (lineasActualizadas > 0)
                        mensaje += $"✅ {lineasActualizadas} líneas actualizadas\n";
                    
                    if (lineasCreadas > 0)
                        mensaje += $"➕ {lineasCreadas} líneas nuevas creadas\n";
                    
                    if (lineasSinOperario > 0)
                    {
                        mensaje += $"\n⚠️ {lineasSinOperario} líneas quedaron sin operario asignado.\n";
                        mensaje += "La orden volverá al estado 'Sin Asignar'.\n";
                    }
                    
                    MessageBox.Show(mensaje, "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        "No se procesó ninguna línea.\n\n" +
                        "Verifica que las líneas estén correctamente configuradas.", 
                        "Información", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Warning);
                }
                
                Cerrar();
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error: {ex.Message}";
                MessageBox.Show(
                    $"Error al guardar cambios:\n\n{ex.Message}", 
                    "Error", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }
            finally
            {
                IsCargando = false;
                MensajeEstado = string.Empty;
            }
        }

        [RelayCommand]
        private async Task AgregarMultiplesLineas()
        {
            try
            {
                // Obtener la información del destino desde el encabezado
                var codigoDestino = AlmacenDestinoSeleccionado?.CodigoAlmacen ?? "";
                var nombreDestino = AlmacenDestinoSeleccionado?.NombreAlmacen ?? "";
                
                var dialog = new AgregarLineasOrdenTraspasoDialog(codigoDestino, nombreDestino);
                
                // Establecer el owner para que se centre correctamente
                var owner = System.Windows.Application.Current.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.IsActive)
                           ?? System.Windows.Application.Current.MainWindow;
                if (owner != null && owner != dialog)
                    dialog.Owner = owner;
                
                var result = dialog.ShowDialog();
                
                if (result == true && dialog.DataContext is AgregarLineasOrdenTraspasoDialogViewModel viewModel)
                {
                    // Agregar las líneas seleccionadas
                    foreach (var lineaItem in viewModel.LineasPendientes)
                    {
                        var nuevaLinea = new CrearLineaOrdenTraspasoDto
                        {
                            Orden = Lineas.Count + 1,
                            CodigoArticulo = lineaItem.CodigoArticulo,
                            DescripcionArticulo = lineaItem.DescripcionArticulo,
                            CantidadPlan = lineaItem.CantidadPlan,
                            CodigoAlmacenOrigen = lineaItem.CodigoAlmacenOrigen,
                            CodigoAlmacenDestino = lineaItem.CodigoAlmacenDestino,
                            UbicacionOrigen = lineaItem.UbicacionOrigen,
                            UbicacionDestino = lineaItem.UbicacionDestino,
                            Partida = lineaItem.Partida,
                            FechaCaducidad = lineaItem.FechaCaducidad,
                            PaletOrigen = "",
                            PaletDestino = "",
                            IdOperarioAsignado = 0, // Se asignará manualmente por línea
                            OperarioSeleccionado = null, // Inicializar sin operario seleccionado
                            Estado = "PENDIENTE" // Nuevas líneas siempre en PENDIENTE
                        };

                        // Inicializar el filtrado individual para esta línea
                        InicializarFiltradoLinea(nuevaLinea);

                        Lineas.Add(nuevaLinea);
                        
                        // Verificar que se inicializó correctamente
                        if (nuevaLinea.OperariosViewLinea == null)
                        {
                            System.Diagnostics.Debug.WriteLine($"ADVERTENCIA: OperariosViewLinea no se inicializó para línea: {nuevaLinea.CodigoArticulo}");
                        }
                    }

                    // Seleccionar la última línea agregada
                    LineaSeleccionada = Lineas.LastOrDefault();
                    NotificarCambioEnLineas();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al agregar múltiples líneas: {ex.Message}");
                var errorDialog = new WarningDialog("Error", $"Error al agregar líneas: {ex.Message}");
                errorDialog.ShowDialog();
            }
        }

        [RelayCommand]
        private async void EliminarLinea()
        {
            if (LineaSeleccionada != null)
            {
                // Verificar si la línea se puede cancelar
                if (!LineaSeleccionada.PuedeEditarOperario)
                {
                    MessageBox.Show(
                        $"No se puede cancelar esta línea porque está en estado '{LineaSeleccionada.Estado}'.\n\n" +
                        "Solo se pueden cancelar líneas en estado 'PENDIENTE' o 'SIN_ASIGNAR'.",
                        "No se puede cancelar",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Verificar si es una línea nueva o existente
                var lineaOriginal = OrdenOriginal.Lineas.FirstOrDefault(l => 
                    l.CodigoArticulo == LineaSeleccionada.CodigoArticulo && 
                    l.CantidadPlan == LineaSeleccionada.CantidadPlan);

                string mensaje;
                if (lineaOriginal != null)
                {
                    mensaje = $"¿Está seguro de que desea cancelar la línea '{LineaSeleccionada.CodigoArticulo}'?\n\n" +
                             "La línea se marcará como 'CANCELADA' y no se podrá editar.";
                }
                else
                {
                    mensaje = $"¿Está seguro de que desea eliminar la línea '{LineaSeleccionada.CodigoArticulo}'?\n\n" +
                             "Esta línea se eliminará completamente de la orden.";
                }

                // Confirmar cancelación/eliminación
                var resultado = MessageBox.Show(
                    mensaje,
                    lineaOriginal != null ? "Confirmar cancelación" : "Confirmar eliminación",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (resultado == MessageBoxResult.Yes)
                {
                    try
                    {
                        IsCargando = true;
                        MensajeEstado = lineaOriginal != null ? "Cancelando línea..." : "Eliminando línea...";

                        if (lineaOriginal != null)
                        {
                            // Es una línea existente - actualizar en la base de datos
                            var dtoActualizacion = new ActualizarLineaOrdenTraspasoDto
                            {
                                Estado = "CANCELADA"
                            };

                            var resultadoActualizacion = await _ordenTraspasoService.ActualizarLineaOrdenTraspasoAsync(
                                lineaOriginal.IdLineaOrden, dtoActualizacion);

                            if (resultadoActualizacion)
                            {
                                // Actualizar el estado local de la línea
                                LineaSeleccionada.Estado = "CANCELADA";
                                
                                // Forzar actualización de la UI
                                OnPropertyChanged(nameof(LineaSeleccionada));
                                OnPropertyChanged(nameof(Lineas));
                                
                                // Actualizar estado de eliminación
                                ActualizarEstadoEliminacion();
                                
                                // Forzar refresh de la colección para actualizar la UI
                                var tempLineas = new ObservableCollection<CrearLineaOrdenTraspasoDto>(Lineas);
                                Lineas.Clear();
                                foreach (var linea in tempLineas)
                                {
                                    Lineas.Add(linea);
                                }
                                
                                MessageBox.Show("Línea cancelada correctamente.", "Éxito", 
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else
                            {
                                MessageBox.Show("Error al cancelar la línea en el servidor.", "Error", 
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                        else
                        {
                            // Es una línea nueva - simplemente eliminarla de la colección local
                            Lineas.Remove(LineaSeleccionada);
                            
                            // Actualizar estado de eliminación
                            ActualizarEstadoEliminacion();
                            
                            MessageBox.Show("Línea eliminada correctamente.", "Éxito", 
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error al cancelar la línea: {ex.Message}", "Error", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        IsCargando = false;
                        MensajeEstado = string.Empty;
                    }
                }
            }
        }

        [RelayCommand]
        private void ActualizarFiltroLinea(CrearLineaOrdenTraspasoDto linea)
        {
            if (linea?.OperariosViewLinea == null) return;

            linea.OperariosViewLinea.Filter = item => FiltraOperarioLinea(item, linea);
            linea.OperariosViewLinea.Refresh();
            linea.IsDropDownOpenLinea = true;
            
            // Notificar cambio para actualizar el botón de guardar
            OnPropertyChanged(nameof(PuedeCrearOrden));
        }

        [RelayCommand]
        private void LimpiarFiltroLinea(CrearLineaOrdenTraspasoDto linea)
        {
            if (linea == null) return;
            
            linea.FiltroOperarioLinea = "";
            linea.OperariosViewLinea?.Refresh();
        }

        [RelayCommand]
        private void OperarioSeleccionadoChanged(CrearLineaOrdenTraspasoDto linea)
        {
            if (linea?.OperarioSeleccionado != null)
            {
                linea.IdOperarioAsignado = linea.OperarioSeleccionado.Operario;
                System.Diagnostics.Debug.WriteLine($"Operario seleccionado para línea {linea.CodigoArticulo}: {linea.OperarioSeleccionado.NombreOperario} (ID: {linea.IdOperarioAsignado})");
            }
            else
            {
                linea.IdOperarioAsignado = 0;
                System.Diagnostics.Debug.WriteLine($"Operario deseleccionado para línea {linea.CodigoArticulo}");
            }
            
            // Notificar cambio para actualizar el botón de guardar
            OnPropertyChanged(nameof(PuedeCrearOrden));
        }

        [RelayCommand]
        private void NotificarCambioEnLineas()
        {
            // Notificar cambios en las líneas para actualizar el botón de guardar
            OnPropertyChanged(nameof(PuedeCrearOrden));
        }
        #endregion

        #region Validaciones
        private bool ValidarDatos()
        {
            System.Diagnostics.Debug.WriteLine("=== DEBUG ValidarDatos ===");
            System.Diagnostics.Debug.WriteLine($"PrioridadSeleccionada == null: {PrioridadSeleccionada == null}");
            System.Diagnostics.Debug.WriteLine($"FechaPlan == null: {FechaPlan == null}");
            System.Diagnostics.Debug.WriteLine($"AlmacenDestinoSeleccionado == null: {AlmacenDestinoSeleccionado == null}");
            
            if (PrioridadSeleccionada == null)
            {
                System.Diagnostics.Debug.WriteLine("ERROR: PrioridadSeleccionada es null");
                MessageBox.Show("Debe seleccionar una prioridad.", "Validación", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (FechaPlan == null)
            {
                System.Diagnostics.Debug.WriteLine("ERROR: FechaPlan es null");
                MessageBox.Show("Debe seleccionar una fecha planificada.", "Validación", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (AlmacenDestinoSeleccionado == null)
            {
                System.Diagnostics.Debug.WriteLine("ERROR: AlmacenDestinoSeleccionado es null");
                MessageBox.Show("Debe seleccionar un almacén destino.", "Validación", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            System.Diagnostics.Debug.WriteLine("ValidarDatos() - Todas las validaciones pasaron");
            return true;
        }

        // Propiedad para habilitar/deshabilitar el botón de guardar
        public bool PuedeCrearOrden 
        { 
            get 
            {
                // No permitir guardar si está cargando
                if (IsCargando) return false;
                
                // No permitir guardar si no hay líneas
                if (!Lineas.Any()) return false;
                
                // No permitir guardar si alguna línea no tiene operario asignado
                var lineasSinOperario = Lineas.Where(l => l.IdOperarioAsignado <= 0 && l.PuedeEditarOperario).Any();
                if (lineasSinOperario) return false;
                
                return true;
            }
        }
        #endregion

        #region Helpers
        private void ActualizarEstadoEliminacion()
        {
            PuedeEliminarLinea = LineaSeleccionada != null && LineaSeleccionada.PuedeEditarOperario;
            AlmacenDestinoHabilitado = Lineas.Count == 0;
            AlmacenDestinoBloqueado = Lineas.Count > 0;
        }
        #endregion
    }
}
