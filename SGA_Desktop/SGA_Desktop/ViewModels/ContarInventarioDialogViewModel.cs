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
using SGA_Desktop.Helpers;

namespace SGA_Desktop.ViewModels
{
    public partial class ContarInventarioDialogViewModel : ObservableObject
    {
        #region Fields & Services
        private readonly InventarioService _inventarioService;
        private readonly StockService _stockService;
        private List<LineaTemporalInventarioDto> _todosLosArticulos = new();
        #endregion

        #region Constructor
        public ContarInventarioDialogViewModel(InventarioService inventarioService, StockService stockService)
        {
            _inventarioService = inventarioService;
            _stockService = stockService;
            
            ArticulosInventario = new ObservableCollection<LineaTemporalInventarioDto>();

            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                _ = InitializeAsync();
        }

        public ContarInventarioDialogViewModel() : this(new InventarioService(), new StockService()) { }
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


        #endregion

        #region Computed Properties
        public bool CanCargarArticulos => !IsCargando && Inventario != null;
        public string TotalArticulos => $"Total: {ArticulosInventario.Count} artículos";
        public string ArticulosContados => $"Contados: {ArticulosInventario.Count(a => a.CantidadContada.HasValue)}";
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
                // Ya no necesitamos cargar almacenes para filtros
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

        private void OnArticuloPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LineaTemporalInventarioDto.CantidadContada))
            {
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
        #endregion
    }
} 