using SGA_Desktop.Models;
using SGA_Desktop.Services;
using SGA_Desktop.Helpers;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SGA_Desktop.Dialog
{
    /// <summary>
    /// Lógica de interacción para ConfiguracionOperarioDialog.xaml
    /// </summary>
    public partial class ConfiguracionOperarioDialog : Window
    {
        private readonly OperariosConfiguracionService _service;
        private readonly ConfiguracionesPredefinidasService _configuracionesService;
        private readonly OperarioConfiguracionDto _operario;

        // Colecciones para los controles
        private readonly ObservableCollection<EmpresaOperarioDto> _empresas;
        private readonly ObservableCollection<AlmacenOperarioDto> _almacenes;
        
        // Lista original de permisos disponibles (sin filtrar)
        private List<PermisoDisponibleDto> _todosLosPermisos;
        
        // Lista original de empresas disponibles (sin filtrar)
        private List<EmpresaConfiguracionDto> _todasLasEmpresas;
        
        // Permisos originales del operario (para calcular diferencias al guardar)
        private List<short> _permisosOriginales;
        
        // Empresas originales del operario (para calcular diferencias al guardar)
        private List<EmpresaOperarioDto> _empresasOriginales;
        
        // Lista original de almacenes disponibles (sin filtrar)
        private List<AlmacenConfiguracionDto> _todosLosAlmacenes;
        
        // Almacenes originales del operario (para calcular diferencias al guardar)
        private List<AlmacenOperarioDto> _almacenesOriginales;
        
        // Flag para evitar múltiples operaciones de guardado simultáneas
        private bool _guardandoEnProgreso = false;


        public ConfiguracionOperarioDialog(OperarioConfiguracionDto operario, OperariosConfiguracionService service)
        {
            InitializeComponent();
            
            _operario = operario;
            _service = service;
            _configuracionesService = new ConfiguracionesPredefinidasService();

            // Inicializar colecciones
            _empresas = new ObservableCollection<EmpresaOperarioDto>(operario.Empresas);
            _almacenes = new ObservableCollection<AlmacenOperarioDto>(operario.Almacenes);
            
            // Inicializar listas originales directamente con los datos del operario
            _almacenesOriginales = new List<AlmacenOperarioDto>(operario.Almacenes);
            
            // Inicialización completada correctamente

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
                // Cargar configuraciones predefinidas
                await CargarConfiguracionesPredefinidasAsync();
                
                // Cargar límites
                TxtLimiteEuros.Text = _operario.LimiteInventarioEuros?.ToString("F4") ?? "";
                TxtLimiteUnidades.Text = _operario.LimiteInventarioUnidades?.ToString("F4") ?? "";

                // Cargar empresas disponibles
                try
                {
                    var empresasDisponibles = await _service.ObtenerEmpresasDisponiblesAsync();
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
                var permisosDisponibles = await _service.ObtenerPermisosDisponiblesAsync();
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
                        var almacenesEmpresa = await _service.ObtenerAlmacenesDisponiblesAsync(codigoEmpresa);
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
                TitleText.Text = $"CONFIGURAR OPERARIO: {_operario.Nombre}";

                // Inicializar listas originales después de cargar todos los datos
                _permisosOriginales = new List<short>(_operario.Permisos);
                _empresasOriginales = new List<EmpresaOperarioDto>(_empresas);
                // _almacenesOriginales ya se inicializó en el constructor
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
                // No hay validaciones básicas ya que solo se editan los límites

                // Construir DTO de actualización (solo límites)
                var updateDto = new OperarioUpdateDto
                {
                    Id = _operario.Id
                };

                // Parsear límites - siempre actualizar, incluso si está vacío o en 0
                if (decimal.TryParse(TxtLimiteEuros.Text, out var limiteEuros))
                {
                    updateDto.LimiteInventarioEuros = limiteEuros >= 0 ? limiteEuros : 0;
                }
                else
                {
                    // Si está vacío o no es válido, establecer a 0
                    updateDto.LimiteInventarioEuros = 0;
                }

                if (decimal.TryParse(TxtLimiteUnidades.Text, out var limiteUnidades))
                {
                    updateDto.LimiteInventarioUnidades = limiteUnidades >= 0 ? limiteUnidades : 0;
                }
                else
                {
                    // Si está vacío o no es válido, establecer a 0
                    updateDto.LimiteInventarioUnidades = 0;
                }

                // Calcular diferencias de permisos
                updateDto.PermisosAsignar = _operario.Permisos.Except(_permisosOriginales).ToList();
                updateDto.PermisosQuitar = _permisosOriginales.Except(_operario.Permisos).ToList();

                // Calcular diferencias de empresas
                var empresasActuales = _empresas.ToList();
                updateDto.EmpresasAsignar = empresasActuales.Except(_empresasOriginales, new EmpresaOperarioDtoComparer()).ToList();
                updateDto.EmpresasQuitar = _empresasOriginales.Except(empresasActuales, new EmpresaOperarioDtoComparer()).Select(e => e.EmpresaOrigen).ToList();
                
                // SOLUCIÓN SEGURA: Solo hacer cambios si hay diferencias reales
                var almacenesActuales = _almacenes.ToList();
                
                // Si no hay almacenes originales, no quitar nada
                if (_almacenesOriginales.Count == 0)
                {
                    updateDto.AlmacenesQuitar = new List<string>();
                    updateDto.AlmacenesAsignar = almacenesActuales.ToList();
                }
                else
                {
                    // Quitar todos los almacenes originales
                    updateDto.AlmacenesQuitar = _almacenesOriginales.Select(a => a.CodigoAlmacen).ToList();
                    
                    // Asignar todos los almacenes actuales
                    updateDto.AlmacenesAsignar = almacenesActuales.ToList();
                }

                // Guardar
                try
                {
                    await _service.ActualizarOperarioAsync(updateDto.Id, updateDto);
                    
                    var successDialog = new WarningDialog(
                        "Configuración de Operarios",
                        "Configuración actualizada correctamente.",
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
                catch (Exception ex)
                {
                    var errorDialog = new WarningDialog(
                        "Error",
                        $"Error al guardar el operario: {ex.Message}",
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
                else if (ex.Message.Contains("Error al actualizar operario"))
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
                    .Where(e => !_empresas.Any(emp => emp.EmpresaOrigen == e.CodigoEmpresa))
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

        private void CrearElementoEmpresa(EmpresaOperarioDto empresa)
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
                Text = empresa.Empresa,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            var codigoText = new TextBlock
            {
                Text = $"(Código: {empresa.CodigoEmpresa}, Origen: {empresa.EmpresaOrigen})",
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
            if (sender is Button button && button.Tag is EmpresaOperarioDto empresa)
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
                    // CodigoEmpresa siempre es 1, EmpresaOrigen es el código real de la empresa
                    var nuevaEmpresa = new EmpresaOperarioDto
                    {
                        CodigoEmpresa = 1, // Siempre 1 para SGA
                        Empresa = empresaSeleccionada.Nombre,
                        EmpresaOrigen = empresaSeleccionada.CodigoEmpresa // El código real de la empresa
                    };

                    _empresas.Add(nuevaEmpresa);
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
            
            foreach (var permiso in _operario.Permisos)
            {
                CrearElementoPermiso(permiso);
            }
            
            // Actualizar ComboBox para excluir permisos ya asignados
            ActualizarComboPermisosDisponibles();
        }

        private void ActualizarComboPermisosDisponibles()
        {
            if (_todosLosPermisos != null)
            {
                var permisosDisponibles = _todosLosPermisos
                    .Where(p => !_operario.Permisos.Contains(p.Codigo))
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
                _operario.Permisos.Remove(codigoPermiso);
                CargarPermisosEnStackPanel();
            }
        }

        private void AgregarPermiso_Click(object sender, RoutedEventArgs e)
        {
            if (CmbPermisosDisponibles.SelectedItem is PermisoDisponibleDto permisoSeleccionado)
            {
                if (!_operario.Permisos.Contains(permisoSeleccionado.Codigo))
                {
                    _operario.Permisos.Add(permisoSeleccionado.Codigo);
                    CargarPermisosEnStackPanel();
                }
                else
                {
                    MessageBox.Show("Este permiso ya está asignado al operario.", "Información", 
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

        private void CrearElementoAlmacen(AlmacenOperarioDto almacen)
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
                Text = $"{almacen.CodigoAlmacen} - {almacen.DescripcionAlmacen}",
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
            if (sender is Button button && button.Tag is AlmacenOperarioDto almacen)
            {
                _almacenes.Remove(almacen);
                CargarAlmacenesEnStackPanel();
            }
        }

        private void SeleccionarAlmacenes_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Crear una copia de los almacenes actuales para el diálogo
                var almacenesActuales = new ObservableCollection<AlmacenOperarioDto>(_almacenes);
                var dialog = new SeleccionAlmacenesDialog(_todosLosAlmacenes, almacenesActuales);
                dialog.Owner = this;
                
                if (dialog.ShowDialog() == true)
                {
                    // Reemplazar la lista actual con la selección del diálogo
                    _almacenes.Clear();
                    foreach (var almacen in dialog.AlmacenesSeleccionados)
                    {
                        _almacenes.Add(almacen);
                    }
                    
                    CargarAlmacenesEnStackPanel();
                    
                    MessageBox.Show($"Se asignaron {dialog.AlmacenesSeleccionados.Count} almacenes al operario.", 
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

        #region Configuraciones Predefinidas

        private async Task CargarConfiguracionesPredefinidasAsync()
        {
            try
            {
                var configuraciones = await _configuracionesService.ObtenerConfiguracionesPredefinidasAsync();
                if (configuraciones != null && configuraciones.Any())
                {
                    // Agregar opción "Sin configuración"
                    var configuracionesConOpcionVacia = new List<ConfiguracionPredefinidaDto>
                    {
                        new ConfiguracionPredefinidaDto { Id = 0, Nombre = "-- Seleccionar configuración --" }
                    };
                    configuracionesConOpcionVacia.AddRange(configuraciones);
                    
                    CmbConfiguracionPredefinida.ItemsSource = configuracionesConOpcionVacia;
                    CmbConfiguracionPredefinida.SelectedIndex = 0; // Seleccionar la opción vacía por defecto
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar configuraciones predefinidas: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CmbConfiguracionPredefinida_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbConfiguracionPredefinida.SelectedItem is ConfiguracionPredefinidaDto configuracion)
            {
                BtnAplicarConfiguracion.IsEnabled = configuracion.Id > 0;
            }
        }

        private async void BtnAplicarConfiguracion_Click(object sender, RoutedEventArgs e)
        {
            if (CmbConfiguracionPredefinida.SelectedItem is ConfiguracionPredefinidaDto configuracion && configuracion.Id > 0)
            {
                try
                {
                    // Confirmar aplicación
                    var resultado = MessageBox.Show(
                        $"¿Está seguro de que desea aplicar la configuración '{configuracion.Nombre}'?\n\n" +
                        "Esto sobrescribirá los límites, permisos, empresas y almacenes actuales.",
                        "Confirmar aplicación de configuración",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (resultado == MessageBoxResult.Yes)
                    {
                        // Obtener la configuración completa
                        var configuracionCompleta = await _configuracionesService.ObtenerConfiguracionPredefinidaAsync(configuracion.Id);
                        if (configuracionCompleta != null)
                        {
                            await AplicarConfiguracionPredefinida(configuracionCompleta);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al aplicar la configuración: {ex.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async Task AplicarConfiguracionPredefinida(ConfiguracionPredefinidaCompletaDto configuracion)
        {
            // Aplicar límites
            TxtLimiteEuros.Text = configuracion.LimiteEuros?.ToString("F4") ?? "0,0000";
            TxtLimiteUnidades.Text = configuracion.LimiteUnidades?.ToString("F4") ?? "0,0000";

            // Aplicar permisos
            await AplicarPermisos(configuracion.Permisos);

            // Aplicar empresas
            await AplicarEmpresas(configuracion.Empresas);

            // Aplicar almacenes
            await AplicarAlmacenes(configuracion.Almacenes);

            // Guardar la plantilla aplicada
            await GuardarPlantillaAplicada(configuracion);

            MessageBox.Show($"Configuración '{configuracion.Nombre}' aplicada correctamente.", "Éxito", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task AplicarPermisos(List<PermisoDisponibleDto> permisos)
        {
            // NO limpiar permisos existentes - solo agregar los nuevos
            // Limpiar solo la UI para recargar
            SpPermisosAsignados.Children.Clear();

            // Agregar nuevos permisos (sin duplicar los existentes)
            foreach (var permiso in permisos)
            {
                if (!_operario.Permisos.Contains(permiso.Codigo))
                {
                    _operario.Permisos.Add(permiso.Codigo);
                }
            }

            // Recargar todos los permisos en la UI (existentes + nuevos)
            foreach (var permiso in _operario.Permisos)
            {
                CrearElementoPermiso(permiso);
            }

            // Actualizar ComboBox de permisos disponibles
            ActualizarComboPermisosDisponibles();
        }

        private async Task AplicarEmpresas(List<EmpresaConfiguracionDto> empresas)
        {
            // Limpiar empresas actuales
            _empresas.Clear();

            // Agregar nuevas empresas
            foreach (var empresa in empresas)
            {
                var empresaOperario = new EmpresaOperarioDto
                {
                    CodigoEmpresa = empresa.CodigoEmpresa,
                    EmpresaOrigen = empresa.EmpresaOrigen,
                    Empresa = empresa.Nombre
                };
                _empresas.Add(empresaOperario);
            }

            // Actualizar UI
            CargarEmpresasEnStackPanel();
        }

        private async Task AplicarAlmacenes(List<AlmacenConfiguracionDto> almacenes)
        {
            // Limpiar almacenes actuales
            _almacenes.Clear();
            _almacenesOriginales.Clear();

            // Agregar nuevos almacenes
            foreach (var almacen in almacenes)
            {
                var almacenOperario = new AlmacenOperarioDto
                {
                    CodigoAlmacen = almacen.CodigoAlmacen,
                    DescripcionAlmacen = almacen.Descripcion ?? "",
                    NombreEmpresa = almacen.NombreEmpresa
                };
                _almacenes.Add(almacenOperario);
                _almacenesOriginales.Add(almacenOperario);
            }

            // Actualizar UI
            CargarAlmacenesEnStackPanel();
        }

        private async Task GuardarPlantillaAplicada(ConfiguracionPredefinidaCompletaDto configuracion)
        {
            try
            {
                var dto = new
                {
                    OperarioId = _operario.Id,
                    ConfiguracionPredefinidaId = configuracion.Id,
                    ConfiguracionPredefinidaNombre = configuracion.Nombre,
                    UsuarioAplicacion = SessionManager.UsuarioActual?.operario ?? 0
                };

                await _service.AplicarPlantillaAsync(dto);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar la plantilla aplicada: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #endregion
    }

    /// <summary>
    /// Comparador para EmpresaOperarioDto
    /// </summary>
    public class EmpresaOperarioDtoComparer : IEqualityComparer<EmpresaOperarioDto>
    {
        public bool Equals(EmpresaOperarioDto x, EmpresaOperarioDto y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;
            return x.CodigoEmpresa == y.CodigoEmpresa && x.EmpresaOrigen == y.EmpresaOrigen;
        }

        public int GetHashCode(EmpresaOperarioDto obj)
        {
            if (obj == null) return 0;
            return HashCode.Combine(obj.CodigoEmpresa, obj.EmpresaOrigen);
        }
    }

    /// <summary>
    /// Comparador para AlmacenOperarioDto
    /// </summary>
    public class AlmacenOperarioDtoComparer : IEqualityComparer<AlmacenOperarioDto>
    {
        public bool Equals(AlmacenOperarioDto x, AlmacenOperarioDto y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;
            return x.CodigoEmpresa == y.CodigoEmpresa && x.CodigoAlmacen == y.CodigoAlmacen;
        }

        public int GetHashCode(AlmacenOperarioDto obj)
        {
            if (obj == null) return 0;
            return HashCode.Combine(obj.CodigoEmpresa, obj.CodigoAlmacen);
        }

    }

    /// <summary>
    /// Clase auxiliar para mostrar permisos en el ListBox
    /// </summary>
    public class PermisoItem
    {
        public short Codigo { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public bool Seleccionado { get; set; }
        
        public override string ToString()
        {
            return $"[{Codigo}] {Descripcion}";
        }
    }

}
