using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SGA_Desktop.Dialog
{
    /// <summary>
    /// Lógica de interacción para ConfiguracionPredefinidaDialog.xaml
    /// </summary>
    public partial class ConfiguracionPredefinidaDialog : Window
    {
        private readonly ConfiguracionesPredefinidasService _configuracionesService;
        private readonly OperariosConfiguracionService _operariosService;
        private ConfiguracionPredefinidaCompletaDto? _configuracion;

        // Colecciones para los controles
        private readonly ObservableCollection<EmpresaConfiguracionDto> _empresas;
        private readonly ObservableCollection<AlmacenConfiguracionDto> _almacenes;
        
        // Lista original de permisos disponibles (sin filtrar)
        private List<PermisoDisponibleDto> _todosLosPermisos;
        
        // Lista original de empresas disponibles (sin filtrar)
        private List<EmpresaConfiguracionDto> _todasLasEmpresas;
        
        // Permisos originales de la configuración (para calcular diferencias al guardar)
        private List<short> _permisosOriginales = new List<short>();
        // Permisos actuales (que se modifican al agregar/quitar)
        private List<short> _permisosActuales = new List<short>();
        
        // Empresas originales de la configuración (para calcular diferencias al guardar)
        private List<EmpresaConfiguracionDto> _empresasOriginales;
        
        // Lista original de almacenes disponibles (sin filtrar)
        private List<AlmacenConfiguracionDto> _todosLosAlmacenes;
        
        // Almacenes originales de la configuración (para calcular diferencias al guardar)
        private List<AlmacenConfiguracionDto> _almacenesOriginales;
        
        // Flag para evitar múltiples operaciones de guardado simultáneas
        private bool _guardandoEnProgreso = false;

        public bool EsEdicion { get; set; }

        public ConfiguracionPredefinidaDialog(ConfiguracionPredefinidaCompletaDto? configuracion = null)
        {
            InitializeComponent();
            
            _configuracion = configuracion;
            _configuracionesService = new ConfiguracionesPredefinidasService();
            _operariosService = new OperariosConfiguracionService();
            EsEdicion = configuracion != null;
            
            // Debug: Log de inicialización
            Console.WriteLine($"Constructor - EsEdicion: {EsEdicion}");
            Console.WriteLine($"Constructor - _configuracion: {_configuracion?.Nombre ?? "null"}");
            Console.WriteLine($"Constructor - _permisosActuales inicial: {_permisosActuales.Count}");
            
            // Debug: MessageBox para verificar inicialización
            // MessageBox.Show($"Constructor:\nEsEdicion: {EsEdicion}\n_configuracion: {_configuracion?.Nombre ?? "null"}\n_permisosActuales inicial: {_permisosActuales.Count}", "Debug Constructor");

            // Inicializar colecciones
            _empresas = new ObservableCollection<EmpresaConfiguracionDto>();
            _almacenes = new ObservableCollection<AlmacenConfiguracionDto>();

            // Configurar controles
            DataContext = this;
            
            // Cargar empresas en el StackPanel
            CargarEmpresasEnStackPanel();

            // Cargar datos
            _ = CargarDatosAsync();
        }

        private async Task CargarDatosAsync()
        {
            try
            {
                // Cargar información básica
                if (EsEdicion && _configuracion != null)
                {
                    TxtNombre.Text = _configuracion.Nombre;
                    TxtDescripcion.Text = _configuracion.Descripcion ?? string.Empty;
                }
                else
                {
                    TxtNombre.Text = string.Empty;
                    TxtDescripcion.Text = string.Empty;
                }

                // Cargar límites
                TxtLimiteEuros.Text = "0,0000";
                TxtLimiteUnidades.Text = "0,0000";

                // Cargar empresas disponibles
                try
                {
                    var empresasDisponibles = await _operariosService.ObtenerEmpresasDisponiblesAsync();
                    if (empresasDisponibles != null && empresasDisponibles.Any())
                    {
                        _todasLasEmpresas = empresasDisponibles; // Guardar lista original
                        CargarEmpresasEnStackPanel(); // Esto actualizará el ComboBox
                    }
                    else
                    {
                        MessageBox.Show("No se pudieron cargar las empresas disponibles.", "Advertencia", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al cargar empresas disponibles: {ex.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }

                // Cargar permisos disponibles
                var permisosDisponibles = await _operariosService.ObtenerPermisosDisponiblesAsync();
                if (permisosDisponibles != null)
                {
                    _todosLosPermisos = permisosDisponibles; // Guardar lista original
                    CargarPermisosEnStackPanel();
                }

                // Cargar almacenes disponibles de todas las empresas (1, 3, 999)
                try
                {
                    var todosLosAlmacenes = new List<AlmacenConfiguracionDto>();
                    var empresasParaAlmacenes = new[] { (short)1, (short)3, (short)999 };
                    
                    foreach (var codigoEmpresa in empresasParaAlmacenes)
                    {
                        var almacenesEmpresa = await _operariosService.ObtenerAlmacenesDisponiblesAsync(codigoEmpresa);
                        if (almacenesEmpresa != null && almacenesEmpresa.Any())
                        {
                            todosLosAlmacenes.AddRange(almacenesEmpresa);
                        }
                    }
                    
                    if (todosLosAlmacenes.Any())
                    {
                        _todosLosAlmacenes = todosLosAlmacenes; // Guardar lista original
                        CargarAlmacenesEnStackPanel(); // Esto actualizará el ComboBox
                    }
                    else
                    {
                        MessageBox.Show("No se pudieron cargar los almacenes disponibles.", "Advertencia", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al cargar almacenes disponibles: {ex.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }

                // Configurar título
                if (EsEdicion && _configuracion != null)
                {
                    TitleText.Text = $"EDITAR CONFIGURACIÓN: {_configuracion.Nombre}";
                    
                    // Cargar datos existentes
                    CargarDatosExistentes();
                }
                else
                {
                    TitleText.Text = "NUEVA CONFIGURACIÓN PREDEFINIDA";
                }

                // Inicializar listas originales después de cargar todos los datos
                // _permisosOriginales y _permisosActuales ya se inicializaron en CargarDatosExistentes()
                _empresasOriginales = new List<EmpresaConfiguracionDto>(_empresas);
                _almacenesOriginales = new List<AlmacenConfiguracionDto>(_almacenes);
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog(
                    "Error",
                    $"Error al cargar datos: {ex.Message}",
                    "\uE814" // ícono de error
                );
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                           ?? Application.Current.MainWindow;
                if (owner != null && owner != errorDialog)
                    errorDialog.Owner = owner;
                errorDialog.ShowDialog();
            }
        }

        private void CargarDatosExistentes()
        {
            if (_configuracion == null) return;

            // Cargar permisos existentes
            _permisosOriginales = _configuracion.Permisos.Select(p => p.Codigo).ToList();
            _permisosActuales = new List<short>(_permisosOriginales);
            
            // Cargar límites existentes
            TxtLimiteEuros.Text = _configuracion.LimiteEuros?.ToString("F4") ?? "0,0000";
            TxtLimiteUnidades.Text = _configuracion.LimiteUnidades?.ToString("F4") ?? "0,0000";
            
            // Debug: Log de permisos cargados
            Console.WriteLine($"Cargando datos existentes:");
            Console.WriteLine($"Permisos originales: {_permisosOriginales.Count}");
            Console.WriteLine($"Permisos actuales: {_permisosActuales.Count}");
            foreach (var permiso in _permisosActuales)
            {
                Console.WriteLine($"Permiso cargado: {permiso}");
            }
            
            // Debug: MessageBox para verificar permisos cargados
            // MessageBox.Show($"Cargando datos existentes:\nPermisos originales: {_permisosOriginales.Count}\nPermisos actuales: {_permisosActuales.Count}\nPermisos: {string.Join(", ", _permisosActuales)}", "Debug CargarDatosExistentes");
            
            CargarPermisosEnStackPanel();

            // Cargar empresas existentes
            _empresas.Clear();
            foreach (var empresa in _configuracion.Empresas)
            {
                _empresas.Add(empresa);
            }
            CargarEmpresasEnStackPanel();

            // Cargar almacenes existentes
            _almacenes.Clear();
            foreach (var almacen in _configuracion.Almacenes)
            {
                _almacenes.Add(almacen);
            }
            CargarAlmacenesEnStackPanel();
        }

        private async void GuardarButton_Click(object sender, RoutedEventArgs e)
        {
            // Verificar si ya hay una operación de guardado en progreso
            if (_guardandoEnProgreso)
            {
                var infoDialog = new WarningDialog(
                    "Operación en curso",
                    "Ya hay una operación de guardado en progreso. Por favor, espere.",
                    "\uE946" // ícono de información
                );
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                           ?? Application.Current.MainWindow;
                if (owner != null && owner != infoDialog)
                    infoDialog.Owner = owner;
                infoDialog.ShowDialog();
                return;
            }

            // Marcar que la operación está en progreso
            _guardandoEnProgreso = true;

            // Deshabilitar botón y mostrar indicador de carga
            var botonGuardar = sender as Button;
            if (botonGuardar != null)
            {
                botonGuardar.IsEnabled = false;
                botonGuardar.Content = "Guardando...";
            }

            // Deshabilitar toda la ventana para evitar interacciones
            this.IsEnabled = false;
            
            // Cambiar cursor a espera
            this.Cursor = Cursors.Wait;

            try
            {
                // Validar datos básicos
                if (string.IsNullOrWhiteSpace(TxtNombre.Text))
                {
                    MessageBox.Show("El nombre es obligatorio.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                    RestaurarEstadoVentana(botonGuardar);
                    return;
                }

                if (string.IsNullOrWhiteSpace(TxtLimiteEuros.Text) || string.IsNullOrWhiteSpace(TxtLimiteUnidades.Text))
                {
                    MessageBox.Show("Los límites son obligatorios.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                    RestaurarEstadoVentana(botonGuardar);
                    return;
                }

                // Debug: Log de permisos que se van a enviar
                Console.WriteLine($"Enviando {_permisosActuales.Count} permisos al API:");
                foreach (var permiso in _permisosActuales)
                {
                    Console.WriteLine($"Permiso a enviar: {permiso}");
                }
                
                // Debug: MessageBox para verificar permisos antes de enviar
                // MessageBox.Show($"Antes de enviar al API:\n_permisosActuales: {_permisosActuales.Count}\nPermisos: {string.Join(", ", _permisosActuales)}", "Debug Antes de Enviar");
                
                // Crear DTO de configuración
                var dto = new ConfiguracionPredefinidaCrearDto
                {
                    Nombre = TxtNombre.Text.Trim(),
                    Descripcion = TxtDescripcion.Text?.Trim(),
                    Permisos = _permisosActuales,
                    Empresas = _empresas.Select(e => e.CodigoEmpresa).ToList(),
                    Almacenes = _almacenes.Select(a => a.CodigoAlmacen).ToList(),
                    
                    // Límites
                    LimiteEuros = decimal.TryParse(TxtLimiteEuros.Text?.Replace(",", "."), out var limiteEuros) ? limiteEuros : (decimal?)null,
                    LimiteUnidades = decimal.TryParse(TxtLimiteUnidades.Text?.Replace(",", "."), out var limiteUnidades) ? limiteUnidades : (decimal?)null,
                    
                    // Usuario actual (ID del operario)
                    Usuario = SessionManager.UsuarioActual?.operario ?? 0
                };

                bool exito;
                if (EsEdicion && _configuracion != null)
                {
                    exito = await _configuracionesService.ActualizarConfiguracionPredefinidaAsync(_configuracion.Id, dto);
                }
                else
                {
                    var resultado = await _configuracionesService.CrearConfiguracionPredefinidaAsync(dto);
                    exito = resultado != null;
                }

                if (exito)
                {
                    var successDialog = new WarningDialog(
                        "Configuración Predefinida",
                        "Configuración guardada correctamente.",
                        "\uE946" // ícono de información/éxito
                    );
                    var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                               ?? Application.Current.MainWindow;
                    if (owner != null && owner != successDialog)
                        successDialog.Owner = owner;
                    successDialog.ShowDialog();
                    
                    DialogResult = true;
                    Close();
                }
                else
                {
                    var errorDialog = new WarningDialog(
                        "Error",
                        "Error al guardar la configuración.",
                        "\uE814" // ícono de error/advertencia
                    );
                    var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                               ?? Application.Current.MainWindow;
                    if (owner != null && owner != errorDialog)
                        errorDialog.Owner = owner;
                    errorDialog.ShowDialog();
                    
                    // Restaurar estado de la ventana
                    RestaurarEstadoVentana(botonGuardar);
                }
            }
            catch (Exception ex)
            {
                string mensajeError;
                string icono = "\uE814"; // ícono de error por defecto
                
                if (ex.InnerException is TaskCanceledException || ex.Message.Contains("timeout"))
                {
                    mensajeError = "La operación tardó demasiado tiempo. Por favor, inténtelo de nuevo.";
                    icono = "\uE946"; // ícono de información para timeout
                }
                else if (ex.Message.Contains("Error al guardar configuración"))
                {
                    mensajeError = "Error al guardar la configuración. Verifique la conexión y vuelva a intentarlo.";
                }
                else
                {
                    mensajeError = $"Error al guardar: {ex.Message}";
                }
                
                var errorDialog = new WarningDialog(
                    "Error",
                    mensajeError,
                    icono
                );
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                           ?? Application.Current.MainWindow;
                if (owner != null && owner != errorDialog)
                    errorDialog.Owner = owner;
                errorDialog.ShowDialog();
                
                // Restaurar estado de la ventana
                RestaurarEstadoVentana(botonGuardar);
            }
        }

        private void RestaurarEstadoVentana(Button botonGuardar)
        {
            // Resetear flag de operación en progreso
            _guardandoEnProgreso = false;
            
            // Restaurar cursor normal
            this.Cursor = Cursors.Arrow;
            
            // Rehabilitar ventana
            this.IsEnabled = true;
            
            // Restaurar botón
            if (botonGuardar != null)
            {
                botonGuardar.IsEnabled = true;
                botonGuardar.Content = "Guardar";
            }
        }

        private void CancelarButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CargarEmpresasEnStackPanel()
        {
            SpEmpresasAsignadas.Children.Clear();
            
            foreach (var empresa in _empresas)
            {
                CrearElementoEmpresa(empresa);
            }
            
            // Actualizar ComboBox para excluir empresas ya asignadas
            ActualizarComboEmpresasDisponibles();
        }

        private void ActualizarComboEmpresasDisponibles()
        {
            if (_todasLasEmpresas != null)
            {
                var empresasDisponibles = _todasLasEmpresas
                    .Where(e => !_empresas.Any(emp => emp.CodigoEmpresa == e.CodigoEmpresa))
                    .ToList();
                
                CmbEmpresasDisponibles.ItemsSource = empresasDisponibles;
                CmbEmpresasDisponibles.SelectedItem = null; // Limpiar selección
                
                // Actualizar estado del combo y botón
                bool hayEmpresasDisponibles = empresasDisponibles.Any();
                CmbEmpresasDisponibles.IsEnabled = hayEmpresasDisponibles;
                BtnAgregarEmpresa.IsEnabled = hayEmpresasDisponibles;
                
                // Mostrar/ocultar mensaje informativo
                if (!hayEmpresasDisponibles)
                {
                    TxtMensajeEmpresas.Text = "No hay más empresas disponibles para agregar";
                    TxtMensajeEmpresas.Visibility = Visibility.Visible;
                }
                else
                {
                    TxtMensajeEmpresas.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                CmbEmpresasDisponibles.IsEnabled = false;
                BtnAgregarEmpresa.IsEnabled = false;
                TxtMensajeEmpresas.Text = "No hay empresas disponibles";
                TxtMensajeEmpresas.Visibility = Visibility.Visible;
            }
        }

        private void CrearElementoEmpresa(EmpresaConfiguracionDto empresa)
        {
            var border = new Border
            {
                Background = Brushes.White,
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 2, 0, 2),
                Padding = new Thickness(10, 8, 10, 8)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };
            
            var empresaText = new TextBlock
            {
                Text = empresa.Nombre,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            var codigoText = new TextBlock
            {
                Text = $"(Código: {empresa.CodigoEmpresa})",
                FontSize = 12,
                Foreground = Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center
            };

            stackPanel.Children.Add(empresaText);
            stackPanel.Children.Add(codigoText);

            var eliminarButton = new Button
            {
                Content = "Eliminar",
                Width = 80,
                Height = 25,
                Background = Brushes.Red,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Tag = empresa
            };
            eliminarButton.Click += EliminarEmpresa_Click;

            Grid.SetColumn(stackPanel, 0);
            Grid.SetColumn(eliminarButton, 1);

            grid.Children.Add(stackPanel);
            grid.Children.Add(eliminarButton);

            border.Child = grid;
            SpEmpresasAsignadas.Children.Add(border);
        }

        private void EliminarEmpresa_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is EmpresaConfiguracionDto empresa)
            {
                _empresas.Remove(empresa);
                CargarEmpresasEnStackPanel();
            }
        }

        private async void AgregarEmpresa_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CmbEmpresasDisponibles.SelectedItem is EmpresaConfiguracionDto empresaSeleccionada)
                {
                    _empresas.Add(empresaSeleccionada);
                    CargarEmpresasEnStackPanel();

                    // Limpiar controles
                    CmbEmpresasDisponibles.SelectedItem = null;
                }
                else
                {
                    var warningDialog = new WarningDialog(
                        "Validación",
                        "Por favor seleccione una empresa.",
                        "\uE814" // ícono de advertencia
                    );
                    var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                               ?? Application.Current.MainWindow;
                    if (owner != null && owner != warningDialog)
                        warningDialog.Owner = owner;
                    warningDialog.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al agregar empresa: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CargarPermisosEnStackPanel()
        {
            SpPermisosAsignados.Children.Clear();
            
            // Debug: Log de permisos antes de cargar en UI
            Console.WriteLine($"CargarPermisosEnStackPanel - _permisosActuales: {_permisosActuales?.Count ?? -1}");
            if (_permisosActuales != null)
            {
                foreach (var permiso in _permisosActuales)
                {
                    Console.WriteLine($"Cargando permiso en UI: {permiso}");
                    CrearElementoPermiso(permiso);
                }
            }
            
            // Debug: MessageBox para verificar permisos en UI
            // MessageBox.Show($"CargarPermisosEnStackPanel:\n_permisosActuales: {_permisosActuales?.Count ?? -1}\nPermisos: {(_permisosActuales != null ? string.Join(", ", _permisosActuales) : "null")}", "Debug CargarPermisosEnStackPanel");
            
            // Actualizar ComboBox para excluir permisos ya asignados
            ActualizarComboPermisosDisponibles();
        }

        private void ActualizarComboPermisosDisponibles()
        {
            if (_todosLosPermisos != null)
            {
                var permisosDisponibles = _todosLosPermisos
                    .Where(p => !_permisosActuales.Contains(p.Codigo))
                    .ToList();
                
                CmbPermisosDisponibles.ItemsSource = permisosDisponibles;
                CmbPermisosDisponibles.SelectedItem = null; // Limpiar selección
                
                // Actualizar estado del combo y botón
                bool hayPermisosDisponibles = permisosDisponibles.Any();
                CmbPermisosDisponibles.IsEnabled = hayPermisosDisponibles;
                BtnAgregarPermiso.IsEnabled = hayPermisosDisponibles;
                
                // Mostrar/ocultar mensaje informativo
                if (!hayPermisosDisponibles)
                {
                    TxtMensajePermisos.Text = "No hay más permisos disponibles para agregar";
                    TxtMensajePermisos.Visibility = Visibility.Visible;
                }
                else
                {
                    TxtMensajePermisos.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void CrearElementoPermiso(short codigoPermiso)
        {
            // Buscar la descripción del permiso en la lista original
            var permisoDisponible = _todosLosPermisos?.FirstOrDefault(p => p.Codigo == codigoPermiso);
            
            var descripcion = permisoDisponible?.Descripcion ?? $"Permiso {codigoPermiso}";

            var border = new Border
            {
                Background = Brushes.White,
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 2, 0, 2),
                Padding = new Thickness(10, 8, 10, 8)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };
            
            // Texto principal en negrita
            var permisoText = new TextBlock
            {
                Text = descripcion,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold
            };

            // Texto entre paréntesis en gris claro
            var codigoText = new TextBlock
            {
                Text = $" (Código: {codigoPermiso})",
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Normal,
                Foreground = Brushes.Gray
            };

            stackPanel.Children.Add(permisoText);
            stackPanel.Children.Add(codigoText);
            Grid.SetColumn(stackPanel, 0);
            grid.Children.Add(stackPanel);

            var eliminarButton = new Button
            {
                Content = "Eliminar",
                Background = Brushes.Red,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(10, 0, 0, 0),
                Tag = codigoPermiso
            };

            eliminarButton.Click += EliminarPermiso_Click;
            Grid.SetColumn(eliminarButton, 1);
            grid.Children.Add(eliminarButton);

            border.Child = grid;
            SpPermisosAsignados.Children.Add(border);
        }

        private void EliminarPermiso_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is short codigoPermiso)
            {
                _permisosActuales.Remove(codigoPermiso);
                CargarPermisosEnStackPanel();
            }
        }

        private void AgregarPermiso_Click(object sender, RoutedEventArgs e)
        {
            if (CmbPermisosDisponibles.SelectedItem is PermisoDisponibleDto permisoSeleccionado)
            {
                if (!_permisosActuales.Contains(permisoSeleccionado.Codigo))
                {
                    _permisosActuales.Add(permisoSeleccionado.Codigo);
                    CargarPermisosEnStackPanel();
                }
                else
                {
                    MessageBox.Show("Este permiso ya está asignado.", "Información", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                MessageBox.Show("Por favor, seleccione un permiso para agregar.", "Información", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void CargarAlmacenesEnStackPanel()
        {
            SpAlmacenesAsignados.Children.Clear();
            
            foreach (var almacen in _almacenes)
            {
                CrearElementoAlmacen(almacen);
            }
            
            // Actualizar ComboBox para excluir almacenes ya asignados
            ActualizarComboAlmacenesDisponibles();
        }

        private void ActualizarComboAlmacenesDisponibles()
        {
            if (_todosLosAlmacenes != null)
            {
                var almacenesDisponibles = _todosLosAlmacenes
                    .Where(a => !_almacenes.Any(alm => alm.CodigoAlmacen == a.CodigoAlmacen))
                    .ToList();
                
                // El ComboBox fue reemplazado por el diálogo de selección múltiple
            }
        }

        private void CrearElementoAlmacen(AlmacenConfiguracionDto almacen)
        {
            var border = new Border
            {
                Background = Brushes.White,
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 2, 0, 2),
                Padding = new Thickness(10, 8, 10, 8)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };
            
            // Texto principal en negrita
            var almacenText = new TextBlock
            {
                Text = $"{almacen.CodigoAlmacen} - {almacen.Descripcion}",
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold
            };

            // Texto entre paréntesis en gris claro
            var empresaText = new TextBlock
            {
                Text = $" ({almacen.NombreEmpresa})",
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Normal,
                Foreground = Brushes.Gray
            };

            stackPanel.Children.Add(almacenText);
            stackPanel.Children.Add(empresaText);
            Grid.SetColumn(stackPanel, 0);
            grid.Children.Add(stackPanel);

            var eliminarButton = new Button
            {
                Content = "Eliminar",
                Background = Brushes.Red,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(10, 0, 0, 0),
                Tag = almacen
            };

            eliminarButton.Click += EliminarAlmacen_Click;
            Grid.SetColumn(eliminarButton, 1);
            grid.Children.Add(eliminarButton);

            border.Child = grid;
            SpAlmacenesAsignados.Children.Add(border);
        }

        private void EliminarAlmacen_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is AlmacenConfiguracionDto almacen)
            {
                _almacenes.Remove(almacen);
                CargarAlmacenesEnStackPanel();
            }
        }

        private void SeleccionarAlmacenes_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Convertir AlmacenConfiguracionDto a AlmacenOperarioDto para el diálogo
                var almacenesOperario = _almacenes.Select(a => new AlmacenOperarioDto
                {
                    CodigoEmpresa = a.CodigoEmpresa,
                    CodigoAlmacen = a.CodigoAlmacen,
                    DescripcionAlmacen = a.Descripcion ?? string.Empty,
                    NombreEmpresa = a.NombreEmpresa
                }).ToList();

                var dialog = new SeleccionAlmacenesDialog(_todosLosAlmacenes, new ObservableCollection<AlmacenOperarioDto>(almacenesOperario));
                dialog.Owner = this;
                
                if (dialog.ShowDialog() == true)
                {
                    // Reemplazar la lista actual con la selección del diálogo
                    _almacenes.Clear();
                    foreach (var almacen in dialog.AlmacenesSeleccionados)
                    {
                        var almacenConfig = new AlmacenConfiguracionDto
                        {
                            CodigoEmpresa = almacen.CodigoEmpresa,
                            CodigoAlmacen = almacen.CodigoAlmacen,
                            Descripcion = almacen.DescripcionAlmacen,
                            NombreEmpresa = almacen.NombreEmpresa
                        };
                        _almacenes.Add(almacenConfig);
                    }
                    
                    CargarAlmacenesEnStackPanel();
                    
                    MessageBox.Show($"Se asignaron {dialog.AlmacenesSeleccionados.Count} almacenes a la configuración.", 
                                   "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al seleccionar almacenes: {ex.Message}", "Error", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LimpiarAlmacenes_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_almacenes.Count == 0)
                {
                    MessageBox.Show("No hay almacenes asignados para limpiar.", "Información", 
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var resultado = MessageBox.Show($"¿Está seguro de que desea eliminar todos los {_almacenes.Count} almacenes asignados?", 
                                               "Confirmar eliminación", 
                                               MessageBoxButton.YesNo, 
                                               MessageBoxImage.Question);

                if (resultado == MessageBoxResult.Yes)
                {
                    _almacenes.Clear();
                    CargarAlmacenesEnStackPanel();
                    
                    MessageBox.Show("Se eliminaron todos los almacenes asignados.", 
                                   "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al limpiar almacenes: {ex.Message}", "Error", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        /// <summary>
        /// Valida que solo se puedan ingresar números positivos con máximo 4 decimales
        /// </summary>
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            // Permitir solo números, punto y coma (no permitir signo negativo)
            if (!char.IsDigit(e.Text, 0) && e.Text != "." && e.Text != ",")
            {
                e.Handled = true;
                return;
            }

            // Obtener el texto completo que resultaría
            var newText = textBox.Text.Insert(textBox.SelectionStart, e.Text);
            
            // Validar que no haya más de un separador decimal
            var separadores = newText.Count(c => c == '.' || c == ',');
            if (separadores > 1)
            {
                e.Handled = true;
                return;
            }

            // Validar máximo 4 decimales
            var separadorIndex = newText.LastIndexOfAny(new char[] { '.', ',' });
            if (separadorIndex >= 0 && newText.Length - separadorIndex - 1 > 4)
            {
                e.Handled = true;
                return;
            }

            // Validar que el número sea positivo o cero (mayor o igual que 0)
            if (decimal.TryParse(newText.Replace(',', '.'), out var valor))
            {
                if (valor < 0)
                {
                    e.Handled = true;
                    return;
                }
            }
        }
    }
}