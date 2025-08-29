using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Dialog;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SGA_Desktop.Helpers;

namespace SGA_Desktop.ViewModels
{
    public partial class ContarInventarioDialogViewModel : ObservableObject
    {
        #region Fields & Services
        private readonly InventarioService _inventarioService;
        private readonly StockService _stockService;
        private readonly LoginService _loginService;
        private List<LineaTemporalInventarioDto> _todosLosArticulos = new();
        // Cache para precios medios para evitar múltiples consultas
        private readonly Dictionary<string, decimal> _cachePreciosMedios = new();
        #endregion

        #region Constructor
        public ContarInventarioDialogViewModel(InventarioService inventarioService, StockService stockService, LoginService loginService)
        {
            _inventarioService = inventarioService;
            _stockService = stockService;
            _loginService = loginService;
            
            ArticulosInventario = new ObservableCollection<LineaTemporalInventarioDto>();

            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                _ = InitializeAsync();
        }

        public ContarInventarioDialogViewModel() : this(new InventarioService(), new StockService(), new LoginService()) { }
        #endregion

        #region Observable Properties
        [ObservableProperty]
        private InventarioCabeceraDto? inventario;

        public ObservableCollection<LineaTemporalInventarioDto> ArticulosInventario { get; }

        [ObservableProperty]
        private string filtroUbicacion = string.Empty;

        [ObservableProperty]
        private string filtroArticulo = string.Empty;

        [ObservableProperty]
        private LineaTemporalInventarioDto? articuloSeleccionado;

        [ObservableProperty]
        private bool isCargando = false;

        [ObservableProperty]
        private string mensajeEstado = string.Empty;



        [ObservableProperty]
        private bool puedeGuardar = false;

        [ObservableProperty]
        private decimal limiteOperarioEuros = 1000m; // TODO: Obtener desde API

        [ObservableProperty]
        private decimal limiteOperarioUnidades = 0m; // Límite en unidades

        [ObservableProperty] 
        private decimal valorDiferenciasActual = 0;

        [ObservableProperty]
        private decimal unidadesDiferenciasActual = 0;

        [ObservableProperty]
        private bool limiteSuperado = false;


        #endregion

        #region Computed Properties
        public bool CanCargarArticulos => !IsCargando && Inventario != null;
        public string TotalArticulos => $"Total: {ArticulosInventario.Count} artículos";
        public string ArticulosContados => $"Contados: {ArticulosInventario.Count(a => a.CantidadContada.HasValue)}";
        public string EstadoLimite 
        {
            get
            {
                var estado = "";
                
                if (LimiteOperarioEuros > 0)
                {
                    var estadoEuros = ValorDiferenciasActual > LimiteOperarioEuros ? "⚠️" : "✅";
                    estado += $"{estadoEuros} Euros: {ValorDiferenciasActual:C2} / {LimiteOperarioEuros:C2}";
                }
                
                if (LimiteOperarioUnidades > 0)
                {
                    if (!string.IsNullOrEmpty(estado)) estado += " | ";
                    var estadoUnidades = UnidadesDiferenciasActual > LimiteOperarioUnidades ? "⚠️" : "✅";
                    estado += $"{estadoUnidades} Unidades: {UnidadesDiferenciasActual:F2} / {LimiteOperarioUnidades:F2}";
                }
                
                return string.IsNullOrEmpty(estado) ? "Sin límites establecidos" : estado;
            }
        }
        #endregion

        #region Property Change Callbacks
        partial void OnInventarioChanged(InventarioCabeceraDto? oldValue, InventarioCabeceraDto? newValue)
        {
            if (newValue != null)
            {
                _ = CargarArticulosAsync();
            }
            OnPropertyChanged(nameof(CanCargarArticulos));
        }

        partial void OnIsCargandoChanged(bool oldValue, bool newValue)
        {
            OnPropertyChanged(nameof(CanCargarArticulos));
            ValidarFormulario(); // Revalidar cuando cambie IsCargando
        }

        partial void OnFiltroUbicacionChanged(string oldValue, string newValue)
        {
            AplicarFiltros();
        }

        partial void OnFiltroArticuloChanged(string oldValue, string newValue)
        {
            AplicarFiltros();
        }



        partial void OnArticuloSeleccionadoChanged(LineaTemporalInventarioDto? oldValue, LineaTemporalInventarioDto? newValue)
        {
            ValidarFormulario();
        }
        #endregion

        #region Commands

        [RelayCommand]
        private async Task InitializeAsync()
        {
            try
            {
                // Cargar límites del operario actual
                if (SessionManager.UsuarioActual?.operario != null)
                {
                    var operarioId = SessionManager.UsuarioActual.operario;
                    LimiteOperarioEuros = await _loginService.ObtenerLimiteInventarioOperarioAsync(operarioId);
                    LimiteOperarioUnidades = await _loginService.ObtenerLimiteUnidadesOperarioAsync(operarioId);
                }
                else
                {
                    LimiteOperarioEuros = 0m; // Sin operario = sin límite
                    LimiteOperarioUnidades = 0m; // Sin operario = sin límite
                }
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog("Error", $"Error al inicializar: {ex.Message}");
                var ownerInit = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                            ?? Application.Current.MainWindow;
                if (ownerInit != null && ownerInit != errorDialog)
                    errorDialog.Owner = ownerInit;
                errorDialog.ShowDialog();
            }
        }

        [RelayCommand]
        private void SeleccionarArticulo(LineaTemporalInventarioDto? articulo)
        {
            if (articulo == null) return;

            // Deseleccionar todos los artículos
            foreach (var item in ArticulosInventario)
            {
                item.IsSelected = false;
            }
            
            // Seleccionar el artículo actual
            articulo.IsSelected = true;
            ArticuloSeleccionado = articulo;
        }

        [RelayCommand]
        private async Task CargarArticulosAsync()
        {
            try
            {
                if (Inventario == null) return;

                IsCargando = true;
                MensajeEstado = "Cargando líneas temporales del inventario...";

                // Obtener líneas temporales del inventario
                var lineas = await _inventarioService.ObtenerLineasTemporalesAsync(Inventario.IdInventario);



                // Guardar todas las líneas y aplicar filtros
                _todosLosArticulos = lineas;
                AplicarFiltros();

                MensajeEstado = $"Cargadas {lineas.Count} líneas temporales";
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error: {ex.Message}";
                var errorDialog = new WarningDialog("Error", $"Error al cargar líneas temporales: {ex.Message}");
                var ownerLoad = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                            ?? Application.Current.MainWindow;
                if (ownerLoad != null && ownerLoad != errorDialog)
                    errorDialog.Owner = ownerLoad;
                errorDialog.ShowDialog();
            }
            finally
            {
                IsCargando = false;
                ValidarFormulario(); // Revalidar después de cambiar IsCargando
            }
        }



        [RelayCommand]
        private void LimpiarFiltros()
        {
            FiltroUbicacion = string.Empty;
            FiltroArticulo = string.Empty;
        }

        [RelayCommand]
        private void BuscarUbicacion()
        {
            // Este comando ya no es necesario con filtrado automático
            // Pero lo mantenemos por si se quiere usar para algo específico
        }





        [RelayCommand]
        private async Task GuardarConteoAsync()
        {

            try
            {

                
                if (!PuedeGuardar) 
                {
                    return;
                }

                // SIEMPRE enviar TODAS las líneas del inventario, aunque no haya modificaciones
                var lineasParaGuardar = ArticulosInventario.ToList();

                var lineasConDiferencias = lineasParaGuardar
                    .Where(a => Math.Abs(ObtenerCantidadContada(a) - a.StockActual) > 0.0001m)
                    .ToList();

                var totalArticulos = ArticulosInventario.Count;
                var articulosContados = lineasParaGuardar.Count;

                string mensajeConfirmacion;
                if (lineasConDiferencias.Any())
                {
                    mensajeConfirmacion = $"¿Está seguro de que desea guardar el conteo?\n\n" +
                                         $"• {lineasConDiferencias.Count} líneas con diferencias\n" +
                                         $"• {articulosContados - lineasConDiferencias.Count} líneas sin diferencias\n" +
                                         $"• {totalArticulos - articulosContados} líneas sin contar";
                }
                else
                {
                    mensajeConfirmacion = $"¿Está seguro de que desea guardar el conteo?\n\n" +
                                         $"• {articulosContados} artículos contados sin diferencias\n" +
                                         $"• {totalArticulos - articulosContados} líneas sin contar";
                }

                var confirmacion = new ConfirmationDialog("Confirmar guardado", mensajeConfirmacion);
                var ownerConfirm = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                               ?? Application.Current.MainWindow;
                if (ownerConfirm != null && ownerConfirm != confirmacion)
                    confirmacion.Owner = ownerConfirm;
                if (confirmacion.ShowDialog() != true) return;

                IsCargando = true;
                MensajeEstado = "Guardando conteo...";

                var dto = new GuardarConteoInventarioDto
                {
                    IdInventario = Inventario!.IdInventario,
                    Articulos = lineasParaGuardar.Select(a => new ArticuloConteoDto
                    {
                        CodigoArticulo = a.CodigoArticulo,
                        CodigoUbicacion = a.CodigoUbicacion,
                        Partida = a.Partida ?? "", // Usar la partida real de la línea temporal
                        FechaCaducidad = a.FechaCaducidad, // Agregar fecha de caducidad
                        CantidadInventario = ObtenerCantidadContada(a), // Obtener valor contado correctamente
                        UsuarioConteo = SessionManager.UsuarioActual!.operario
                    }).ToList()
                };



                var resultado = await _inventarioService.GuardarConteoInventarioAsync(dto);

                if (resultado)
                {
                    var successDialog = new WarningDialog("Éxito", "Conteo guardado correctamente.");
                    var ownerSuccess = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                                   ?? Application.Current.MainWindow;
                    if (ownerSuccess != null && ownerSuccess != successDialog)
                        successDialog.Owner = ownerSuccess;
                    successDialog.ShowDialog();
                    CerrarDialogo(true);
                }
                else
                {
                    var errorDialog = new WarningDialog("Error", "Error al guardar el conteo.");
                    var ownerError = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                                 ?? Application.Current.MainWindow;
                    if (ownerError != null && ownerError != errorDialog)
                        errorDialog.Owner = ownerError;
                    errorDialog.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error: {ex.Message}";
                var errorDialog = new WarningDialog("Error", $"Error al guardar conteo: {ex.Message}");
                var ownerCatch = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                             ?? Application.Current.MainWindow;
                if (ownerCatch != null && ownerCatch != errorDialog)
                    errorDialog.Owner = ownerCatch;
                errorDialog.ShowDialog();
            }
            finally
            {
                IsCargando = false;
                ValidarFormulario(); // Revalidar después del guardado
            }
        }

        [RelayCommand]
        private void Cancelar()
        {
            CerrarDialogo(false);
        }

        [RelayCommand]
        private void CopiarCodigo(string codigo)
        {
            if (!string.IsNullOrWhiteSpace(codigo))
            {
                System.Windows.Clipboard.SetText(codigo);
                FiltroArticulo = codigo; // Escribir en el campo de búsqueda de artículo
                SeleccionarTextoFiltroArticulo(); // Seleccionar todo el texto
            }
        }

        [RelayCommand]
        private void CopiarDescripcion(string descripcion)
        {
            if (!string.IsNullOrWhiteSpace(descripcion))
            {
                System.Windows.Clipboard.SetText(descripcion);
                FiltroArticulo = descripcion; // Escribir en el campo de búsqueda de artículo
                SeleccionarTextoFiltroArticulo(); // Seleccionar todo el texto
            }
        }

        [RelayCommand]
        private void CopiarUbicacion(string ubicacion)
        {
            if (!string.IsNullOrWhiteSpace(ubicacion))
            {
                System.Windows.Clipboard.SetText(ubicacion);
                FiltroUbicacion = ubicacion; // Escribir en el campo de búsqueda de ubicación
                SeleccionarTextoFiltroUbicacion(); // Seleccionar todo el texto
            }
        }

        /// <summary>
        /// Selecciona todo el texto en el TextBox de filtro de artículo
        /// </summary>
        private void SeleccionarTextoFiltroArticulo()
        {
            // Usar Dispatcher para asegurar que se ejecute en el hilo de UI
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                // Buscar el TextBox de filtro de artículo en la ventana
                if (System.Windows.Application.Current.Windows.OfType<ContarInventarioDialog>().FirstOrDefault() is ContarInventarioDialog dialog)
                {
                    // Buscar el TextBox por nombre o usando VisualTreeHelper
                    var textBox = FindTextBoxInVisualTree(dialog, "FiltroArticuloTextBox");
                    if (textBox != null)
                    {
                        textBox.SelectAll();
                        textBox.Focus();
                    }
                }
            }));
        }

        /// <summary>
        /// Selecciona todo el texto en el TextBox de filtro de ubicación
        /// </summary>
        private void SeleccionarTextoFiltroUbicacion()
        {
            // Usar Dispatcher para asegurar que se ejecute en el hilo de UI
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                // Buscar el TextBox de filtro de ubicación en la ventana
                if (System.Windows.Application.Current.Windows.OfType<ContarInventarioDialog>().FirstOrDefault() is ContarInventarioDialog dialog)
                {
                    // Buscar el TextBox por nombre o usando VisualTreeHelper
                    var textBox = FindTextBoxInVisualTree(dialog, "FiltroUbicacionTextBox");
                    if (textBox != null)
                    {
                        textBox.SelectAll();
                        textBox.Focus();
                    }
                }
            }));
        }

        /// <summary>
        /// Busca un TextBox en el árbol visual por nombre
        /// </summary>
        private System.Windows.Controls.TextBox? FindTextBoxInVisualTree(System.Windows.DependencyObject parent, string name)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                
                if (child is System.Windows.Controls.TextBox textBox && textBox.Name == name)
                    return textBox;
                
                var result = FindTextBoxInVisualTree(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }
        #endregion

        #region Private Methods
        private void AplicarFiltros()
        {
            if (_todosLosArticulos == null) return;

            // Desuscribirse de los eventos anteriores
            foreach (var articulo in ArticulosInventario)
            {
                articulo.PropertyChanged -= OnArticuloPropertyChanged;
            }

            var articulosFiltrados = _todosLosArticulos.AsEnumerable();

            // Aplicar filtro de ubicación
            if (!string.IsNullOrWhiteSpace(FiltroUbicacion))
            {
                articulosFiltrados = articulosFiltrados.Where(a => 
                    a.CodigoUbicacion.Contains(FiltroUbicacion, StringComparison.OrdinalIgnoreCase));
            }

            // Aplicar filtro de artículo
            if (!string.IsNullOrWhiteSpace(FiltroArticulo))
            {
                articulosFiltrados = articulosFiltrados.Where(a => 
                    a.CodigoArticulo.Contains(FiltroArticulo, StringComparison.OrdinalIgnoreCase) ||
                    a.DescripcionArticulo.Contains(FiltroArticulo, StringComparison.OrdinalIgnoreCase));
            }

            // Actualizar la colección visible
            ArticulosInventario.Clear();
            foreach (var articulo in articulosFiltrados)
            {
                ArticulosInventario.Add(articulo);
                // Suscribirse a los cambios del artículo
                articulo.PropertyChanged += OnArticuloPropertyChanged;
            }

            // Notificar cambios en las propiedades computadas
            OnPropertyChanged(nameof(TotalArticulos));
            OnPropertyChanged(nameof(ArticulosContados));
            
            // Validar formulario después de aplicar filtros
            ValidarFormulario();
        }



        private void ValidarFormulario()
        {
            // Siempre permitir guardar si hay líneas en el inventario
            // Un inventario puede estar perfecto sin modificaciones
            var tieneLineasEnInventario = ArticulosInventario.Any();
            var nuevoPuedeGuardar = Inventario != null && tieneLineasEnInventario && !IsCargando;
            
            PuedeGuardar = nuevoPuedeGuardar;
            
            // Notificar cambios en las propiedades computadas
            OnPropertyChanged(nameof(TotalArticulos));
            OnPropertyChanged(nameof(ArticulosContados));
            OnPropertyChanged(nameof(PuedeGuardar)); // Notificar explícitamente el cambio
        }

        private void CerrarDialogo(bool resultado)
        {
            if (Application.Current.Windows.OfType<ContarInventarioDialog>().FirstOrDefault() is ContarInventarioDialog dialog)
            {
                dialog.DialogResult = resultado;
                dialog.Close();
            }
        }

        private async void OnArticuloPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LineaTemporalInventarioDto.CantidadContada))
            {
                if (sender is LineaTemporalInventarioDto linea)
                {
                    await ValidarLimiteOperario(linea); // Nueva validación
                }
                ValidarFormulario();
            }
        }

        /// <summary>
        /// Obtiene la cantidad contada de una línea, considerando tanto el valor decimal como el texto
        /// </summary>
        private decimal ObtenerCantidadContada(LineaTemporalInventarioDto linea)
        {
            // Si tiene valor decimal, usarlo
            if (linea.CantidadContada.HasValue)
                return linea.CantidadContada.Value;

            // Si no tiene valor decimal pero tiene texto, intentar parsearlo
            if (!string.IsNullOrWhiteSpace(linea.CantidadContadaTexto))
            {
                if (decimal.TryParse(linea.CantidadContadaTexto, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal result))
                    return result;
            }

            // Si no se puede obtener, usar el stock actual
            return linea.StockActual;
        }

        /// <summary>
        /// Valida que el operario no supere su límite de inventario
        /// </summary>
        private async Task ValidarLimiteOperario(LineaTemporalInventarioDto lineaCambiada)
        {
            try
            {
                if (SessionManager.UsuarioActual?.operario == null) return;

                bool limiteSuperadoEuros = false;
                bool limiteSuperadoUnidades = false;
                string tipoLimiteSuperado = "";

                var operarioId = SessionManager.UsuarioActual.operario;
                var codigoArticulo = lineaCambiada.CodigoArticulo;
                var cantidadContada = ObtenerCantidadContada(lineaCambiada);
                var nuevaDiferencia = Math.Abs(cantidadContada - lineaCambiada.StockActual);

                // DEBUG: Log información básica (comentado para producción)
                // MessageBox.Show($"🔍 VALIDACIÓN LÍMITES\n\n" +
                //     $"Operario: {operarioId}\n" +
                //     $"Artículo: {codigoArticulo}\n" +
                //     $"Stock Actual: {lineaCambiada.StockActual}\n" +
                //     $"Cantidad Contada: {cantidadContada}\n" +
                //     $"Nueva Diferencia: {nuevaDiferencia}\n" +
                //     $"Límite Euros: {LimiteOperarioEuros}\n" +
                //     $"Límite Unidades: {LimiteOperarioUnidades}", 
                //     "DEBUG - Datos Básicos");

                // Obtener diferencias acumuladas del artículo en el día (excluyendo inventario actual)
                var (unidadesAcumuladas, eurosAcumulados) = await _loginService.ObtenerDiferenciasOperarioArticuloDiaAsync(operarioId, codigoArticulo, Inventario.IdInventario);
                
                // AÑADIR: diferencias del mismo artículo en la sesión actual (excluyendo la línea que estamos validando)
                var diferenciasEnSesion = CalcularDiferenciasArticuloEnSesion(codigoArticulo, lineaCambiada.IdTemp);
                unidadesAcumuladas += diferenciasEnSesion.unidades;
                eurosAcumulados += diferenciasEnSesion.euros;

                // DEBUG: Log diferencias acumuladas (comentado para producción)
                // MessageBox.Show($"📊 DIFERENCIAS ACUMULADAS\n\n" +
                //     $"Diferencias Anteriores Hoy:\n" +
                //     $"  • Unidades: {unidadesAcumuladas - diferenciasEnSesion.unidades:F2}\n" +
                //     $"  • Euros: {eurosAcumulados - diferenciasEnSesion.euros:F2}\n\n" +
                //     $"Diferencias en Sesión Actual:\n" +
                //     $"  • Unidades: {diferenciasEnSesion.unidades:F2}\n" +
                //     $"  • Euros: {diferenciasEnSesion.euros:F2}\n\n" +
                //     $"TOTAL ACUMULADO:\n" +
                //     $"  • Unidades: {unidadesAcumuladas:F2}\n" +
                //     $"  • Euros: {eurosAcumulados:F2}", 
                //     "DEBUG - Acumulados Detallados");

                // Validar límite de euros (acumulado del día + nueva diferencia)
                if (LimiteOperarioEuros > 0)
                {
                    var precioMedio = await ObtenerPrecioMedioAsync(codigoArticulo, lineaCambiada.CodigoAlmacen ?? "");
                    var nuevaDiferenciaEuros = nuevaDiferencia * precioMedio;
                    var totalEurosArticulo = eurosAcumulados + nuevaDiferenciaEuros;

                    // También calcular total global para mostrar en UI
                    var valorTotalGlobal = await CalcularValorTotalDiferenciasAsync();
                    ValorDiferenciasActual = valorTotalGlobal;

                    // DEBUG: Log cálculos de euros (comentado para producción)
                    // MessageBox.Show($"💰 VALIDACIÓN EUROS\n\n" +
                    //     $"Precio Medio: {precioMedio:F4}\n" +
                    //     $"Nueva Diferencia: {nuevaDiferencia} unidades\n" +
                    //     $"Nueva Diferencia €: {nuevaDiferenciaEuros:F2}\n" +
                    //     $"Euros Acumulados: {eurosAcumulados:F2}\n" +
                    //     $"Total Euros Artículo: {totalEurosArticulo:F2}\n" +
                    //     $"Límite: {LimiteOperarioEuros:F2}\n" +
                    //     $"¿Supera límite?: {totalEurosArticulo > LimiteOperarioEuros}", 
                    //     "DEBUG - Euros");

                    if (totalEurosArticulo > LimiteOperarioEuros)
                    {
                        limiteSuperadoEuros = true;
                        tipoLimiteSuperado = $"valor en euros para el artículo {codigoArticulo}";
                    }
                }

                // Validar límite de unidades (acumulado del día + nueva diferencia)
                if (LimiteOperarioUnidades > 0)
                {
                    var totalUnidadesArticulo = unidadesAcumuladas + nuevaDiferencia;

                    // También calcular total global para mostrar en UI
                    var unidadesTotalGlobal = CalcularUnidadesTotalDiferencias();
                    UnidadesDiferenciasActual = unidadesTotalGlobal;

                    // DEBUG: Log cálculos de unidades (comentado para producción)
                    // MessageBox.Show($"📦 VALIDACIÓN UNIDADES\n\n" +
                    //     $"Diferencias Anteriores: {unidadesAcumuladas - diferenciasEnSesion.unidades:F2}\n" +
                    //     $"Diferencias en Sesión: {diferenciasEnSesion.unidades:F2}\n" +
                    //     $"Nueva Diferencia: {nuevaDiferencia:F2}\n\n" +
                    //     $"Total Acumulado: {unidadesAcumuladas:F2}\n" +
                    //     $"Total + Nueva: {totalUnidadesArticulo:F2}\n" +
                    //     $"Límite: {LimiteOperarioUnidades:F2}\n\n" +
                    //     $"¿Supera límite?: {totalUnidadesArticulo > LimiteOperarioUnidades}", 
                    //     "DEBUG - Unidades Detallado");

                    if (totalUnidadesArticulo > LimiteOperarioUnidades)
                    {
                        limiteSuperadoUnidades = true;
                        if (limiteSuperadoEuros)
                            tipoLimiteSuperado = $"valor en euros y unidades para el artículo {codigoArticulo}";
                        else
                            tipoLimiteSuperado = $"unidades para el artículo {codigoArticulo}";
                    }
                }

                // DEBUG: Log resultado final (comentado para producción)
                // MessageBox.Show($"🏁 RESULTADO VALIDACIÓN\n\n" +
                //     $"Límite Euros Superado: {limiteSuperadoEuros}\n" +
                //     $"Límite Unidades Superado: {limiteSuperadoUnidades}\n" +
                //     $"Tipo Límite Superado: {tipoLimiteSuperado}", 
                //     "DEBUG - Resultado");

                // Si se supera algún límite
                if (limiteSuperadoEuros || limiteSuperadoUnidades)
                {
                    LimiteSuperado = true;
                    
                    // Resetear el valor que causó el problema
                    lineaCambiada.CantidadContadaTexto = lineaCambiada.StockActual.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
                    
                    // Mostrar warning más específico
                    var warning = new WarningDialog(
                        "⚠️ Límite Diario Superado", 
                        $"Las diferencias diarias superan su límite autorizado de {tipoLimiteSuperado}.\n\n" +
                        $"Se ha restablecido la cantidad original para:\n" +
                        $"• {lineaCambiada.CodigoArticulo} - {lineaCambiada.DescripcionArticulo}\n" +
                        $"• Ubicación: {lineaCambiada.CodigoUbicacion}\n\n" +
                        $"💡 El límite se aplica por artículo y por día en todos los almacenes.");
                    warning.ShowDialog();
                    
                    // Recalcular después del reset
                    if (LimiteOperarioEuros > 0)
                        ValorDiferenciasActual = await CalcularValorTotalDiferenciasAsync();
                    if (LimiteOperarioUnidades > 0)
                        UnidadesDiferenciasActual = CalcularUnidadesTotalDiferencias();
                }
                else
                {
                    LimiteSuperado = false;
                }
                
                OnPropertyChanged(nameof(EstadoLimite));
            }
            catch (Exception ex)
            {
                // Log error pero no interrumpir el flujo
                System.Diagnostics.Debug.WriteLine($"Error validando límite: {ex.Message}");
            }
        }

        /// <summary>
        /// Calcula el valor total de todas las diferencias de inventario
        /// Cuenta tanto sobrestocks como faltantes (valor absoluto)
        /// </summary>
        private async Task<decimal> CalcularValorTotalDiferenciasAsync()
        {
            decimal valorTotal = 0;
            
            foreach (var linea in ArticulosInventario)
            {
                var cantidadContada = ObtenerCantidadContada(linea);
                
                // 🔧 CONTROLAR tanto sobrestocks como faltantes (valor absoluto)
                var diferencia = Math.Abs(cantidadContada - linea.StockActual);
                
                if (diferencia > 0.01m) // Tolerancia para evitar diferencias mínimas por redondeo
                {
                    var precioMedio = await ObtenerPrecioMedioAsync(linea.CodigoArticulo, linea.CodigoAlmacen ?? "");
                    valorTotal += diferencia * precioMedio;
                }
            }
            
            return valorTotal;
        }

        /// <summary>
        /// Calcula el total de unidades de diferencias de inventario
        /// Cuenta tanto sobrestocks como faltantes (valor absoluto)
        /// </summary>
        private decimal CalcularUnidadesTotalDiferencias()
        {
            decimal unidadesTotal = 0;
            
            foreach (var linea in ArticulosInventario)
            {
                var cantidadContada = ObtenerCantidadContada(linea);
                
                // Controlar tanto sobrestocks como faltantes (valor absoluto)
                var diferencia = Math.Abs(cantidadContada - linea.StockActual);
                
                if (diferencia > 0.01m) // Tolerancia para evitar diferencias mínimas por redondeo
                {
                    unidadesTotal += diferencia;
                }
            }
            
            return unidadesTotal;
        }

        /// <summary>
        /// Calcula las diferencias del mismo artículo en la sesión actual de inventario
        /// Excluye la línea que se está validando para evitar contarla dos veces
        /// </summary>
        private (decimal unidades, decimal euros) CalcularDiferenciasArticuloEnSesion(string codigoArticulo, Guid idTempExcluir)
        {
            decimal totalUnidades = 0;
            decimal totalEuros = 0;
            
            // Buscar todas las líneas del mismo artículo en la sesión actual (excluyendo la que estamos validando)
            var lineasMismoArticulo = ArticulosInventario
                .Where(l => l.CodigoArticulo == codigoArticulo && l.IdTemp != idTempExcluir)
                .ToList();
            
            foreach (var linea in lineasMismoArticulo)
            {
                var cantidadContada = ObtenerCantidadContada(linea);
                var diferencia = Math.Abs(cantidadContada - linea.StockActual);
                
                if (diferencia > 0.01m) // Tolerancia para diferencias mínimas
                {
                    totalUnidades += diferencia;
                    
                    // Para euros, usar precio medio en cache si existe
                    var clave = $"{codigoArticulo}|{linea.CodigoAlmacen ?? ""}";
                    if (_cachePreciosMedios.TryGetValue(clave, out var precio))
                    {
                        totalEuros += diferencia * precio;
                    }
                    // Si no hay precio en cache, no sumar euros (será cálculo conservador)
                }
            }
            
            return (totalUnidades, totalEuros);
        }

        /// <summary>
        /// Obtiene el precio medio de un artículo desde el API
        /// </summary>
        private async Task<decimal> ObtenerPrecioMedioAsync(string codigoArticulo, string codigoAlmacen)
        {
            var clave = $"{codigoArticulo}|{codigoAlmacen}";
            
            // Usar cache si existe
            if (_cachePreciosMedios.TryGetValue(clave, out var precioCache))
                return precioCache;
            
            try
            {
                var empresa = SessionManager.EmpresaSeleccionada ?? 1;
                var precio = await _stockService.ObtenerPrecioMedioAsync(empresa, codigoArticulo, codigoAlmacen);
                
                // Guardar en cache
                _cachePreciosMedios[clave] = precio;
                return precio;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo precio medio: {ex.Message}");
                return 0m; // Sin precio si hay error
            }
        }
        #endregion
    }
} 