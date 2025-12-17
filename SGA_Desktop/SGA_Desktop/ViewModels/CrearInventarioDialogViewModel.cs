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
    public partial class CrearInventarioDialogViewModel : ObservableObject
    {
        #region Fields & Services
        private readonly InventarioService _inventarioService;
        private readonly StockService _stockService;
        #endregion

        #region Constructor
        public CrearInventarioDialogViewModel(InventarioService inventarioService, StockService stockService)
        {
            _inventarioService = inventarioService;
            _stockService = stockService;
            
            AlmacenesDisponibles = new ObservableCollection<AlmacenDto>();
            TiposInventario = new ObservableCollection<string> { "TOTAL", "PARCIAL" };
            OpcionesArticulos = new ObservableCollection<string> { "Todos", "Con stock" };
            OpcionesValoracion = new ObservableCollection<string> 
            { 
                "Precio medio de las entradas", 
                "Precio estándar", 
                "Último precio de compra",
                "Precio de venta"
            };
            
            // Valores por defecto
            TipoInventarioSeleccionado = "TOTAL";
            OpcionArticulosSeleccionados = "Todos"; // Por defecto "Todos" para incluir artículos con stock 0
            IncluirUnidadesCero = false; // Por defecto false (no inicializar a 0)
            IncluirUbicacionesEspeciales = false; // Por defecto false
            Comentarios = string.Empty; // Sin comentario predeterminado

            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                _ = InitializeAsync();
        }

        public CrearInventarioDialogViewModel() : this(new InventarioService(), new StockService()) { }
        #endregion

        #region Observable Properties
        public ObservableCollection<AlmacenDto> AlmacenesDisponibles { get; }
        public ObservableCollection<string> TiposInventario { get; }
        public ObservableCollection<string> OpcionesArticulos { get; }
        public ObservableCollection<string> OpcionesValoracion { get; }

        [ObservableProperty]
        private AlmacenDto? almacenSeleccionado;

        // NUEVO: Propiedades para multialmacén
        [ObservableProperty]
        private bool modoMultialmacen = false;

        // Propiedades computadas para almacenes seleccionados
        public List<AlmacenDto> AlmacenesSeleccionados => 
            AlmacenesDisponibles.Where(a => a.IsSelected).ToList();

        public string DescripcionAlmacenesSeleccionados
        {
            get
            {
                var seleccionados = AlmacenesSeleccionados;
                if (!seleccionados.Any())
                    return "Ningún almacén seleccionado";
                    
                if (seleccionados.Count == 1)
                    return seleccionados.First().DescripcionCombo;
                    
                return seleccionados.Count <= 3 
                    ? string.Join(", ", seleccionados.Select(a => a.CodigoAlmacen))
                    : $"{string.Join(", ", seleccionados.Take(2).Select(a => a.CodigoAlmacen))} y {seleccionados.Count - 2} más";
            }
        }

        [ObservableProperty]
        private string tipoInventarioSeleccionado = "PARCIAL";

        [ObservableProperty]
        private string codigoInventario = string.Empty;

        [ObservableProperty]
        private DateTime fechaInventario = DateTime.Today.Date;

        [ObservableProperty]
        private string opcionArticulosSeleccionados = "Todos";

        [ObservableProperty]
        private string valoracionSeleccionada = "Precio medio de las entradas";

        [ObservableProperty]
        private bool usarRangoArticulos = false;

        [ObservableProperty]
        private string articuloDesde = string.Empty;

        [ObservableProperty]
        private string articuloHasta = string.Empty;

        [ObservableProperty]
        private bool usarRangoUbicaciones = false;

        // Rangos de ubicaciones por dimensiones
        [ObservableProperty]
        private int pasilloDesde = 0;

        [ObservableProperty]
        private int pasilloHasta = 0;

        [ObservableProperty]
        private int estanteriaDesde = 0;

        [ObservableProperty]
        private int estanteriaHasta = 0;

        [ObservableProperty]
        private int alturaDesde = 0;

        [ObservableProperty]
        private int alturaHasta = 0;

        [ObservableProperty]
        private int posicionDesde = 0;

        [ObservableProperty]
        private int posicionHasta = 0;

        // Rangos disponibles (para los combos)
        [ObservableProperty]
        private ObservableCollection<int> pasillosDisponibles = new();

        [ObservableProperty]
        private ObservableCollection<int> estanteriasDisponibles = new();

        [ObservableProperty]
        private ObservableCollection<int> alturasDisponibles = new();

        [ObservableProperty]
        private ObservableCollection<int> posicionesDisponibles = new();



        [ObservableProperty]
        private bool incluirUnidadesCero = false;

        [ObservableProperty]
        private bool incluirUbicacionesEspeciales = false;

        [ObservableProperty]
        private bool noGenerarLineas = false;

        [ObservableProperty]
        private string comentarios = string.Empty;

        [ObservableProperty]
        private bool puedeCrear = false;

        // Propiedades para habilitar/deshabilitar niveles jerárquicos
        [ObservableProperty]
        private bool usarEstanteria = false;

        [ObservableProperty]
        private bool usarAltura = false;

        [ObservableProperty]
        private bool usarPosicion = false;

        // Propiedades para habilitar/deshabilitar controles
        [ObservableProperty]
        private bool estanteriaHabilitada = false;

        [ObservableProperty]
        private bool alturaHabilitada = false;

        [ObservableProperty]
        private bool posicionHabilitada = false;

        [ObservableProperty]
        private string mensajeErrorCodigo = string.Empty;

        [ObservableProperty]
        private bool codigoExiste = false;

        // NUEVO: Propiedades para filtro de artículos específicos (múltiples)
        [ObservableProperty]
        private bool usarFiltroArticulo = false;

        [ObservableProperty]
        private string articuloBuscado = string.Empty;

        [ObservableProperty]
        private ObservableCollection<ArticuloResumenDto> articulosEncontrados = new();

        [ObservableProperty]
        private ArticuloResumenDto? articuloSeleccionado; // Para el ComboBox de resultados

        [ObservableProperty]
        private ObservableCollection<ArticuloResumenDto> articulosSeleccionados = new();

        // Propiedades calculadas para la UI
        public bool MostrarListaArticulos => ArticulosEncontrados.Count > 1;
        public bool MostrarInfoArticulo => ArticuloSeleccionado != null;

        public string DescripcionArticulosSeleccionados
        {
            get
            {
                if (!ArticulosSeleccionados.Any())
                    return "Ningún artículo seleccionado";
                    
                if (ArticulosSeleccionados.Count == 1)
                    return $"{ArticulosSeleccionados.First().CodigoArticulo} - {ArticulosSeleccionados.First().DescripcionArticulo}";
                    
                return ArticulosSeleccionados.Count <= 3 
                    ? string.Join(", ", ArticulosSeleccionados.Select(a => a.CodigoArticulo))
                    : $"{string.Join(", ", ArticulosSeleccionados.Take(2).Select(a => a.CodigoArticulo))} y {ArticulosSeleccionados.Count - 2} más";
            }
        }

        // Propiedades calculadas para UI de rango de artículos
        public bool MostrarInfoRango => !string.IsNullOrWhiteSpace(ArticuloDesde) && !string.IsNullOrWhiteSpace(ArticuloHasta) && UsarRangoArticulos;
        #endregion



        #region Property Change Callbacks
        partial void OnModoMultialmacenChanged(bool oldValue, bool newValue)
        {
            if (newValue)
            {
                // Activar modo multialmacén: Deseleccionar el almacén único
                if (AlmacenSeleccionado != null)
                {
                    AlmacenSeleccionado.IsSelected = false;
                    AlmacenSeleccionado = null;
                }
                
                // Permitir selección múltiple
                foreach (var almacen in AlmacenesDisponibles)
                {
                    almacen.IsSelected = false; // Empezar limpio
                }
                
                // Desactivar rangos en modo multialmacén
                UsarRangoUbicaciones = false;
            }
            else
            {
                // Volver a modo único: Deseleccionar todos
                foreach (var almacen in AlmacenesDisponibles)
                {
                    almacen.IsSelected = false;
                }
                
                // Seleccionar el primero por defecto
                if (AlmacenesDisponibles.Any())
                {
                    AlmacenSeleccionado = AlmacenesDisponibles.First();
                    AlmacenSeleccionado.IsSelected = true;
                }
            }
            
            OnPropertyChanged(nameof(DescripcionAlmacenesSeleccionados));
            ValidarFormulario();
        }

        partial void OnAlmacenSeleccionadoChanged(AlmacenDto? oldValue, AlmacenDto? newValue)
        {
            // Cargar rangos disponibles siempre que se seleccione un almacén
            if (newValue != null)
            {
                // Limpiar valores actuales antes de cargar nuevos
                PasilloDesde = 0;
                PasilloHasta = 0;
                EstanteriaDesde = 0;
                EstanteriaHasta = 0;
                AlturaDesde = 0;
                AlturaHasta = 0;
                PosicionDesde = 0;
                PosicionHasta = 0;
                
                _ = CargarRangosDisponiblesAsync();
            }
            else
            {
                // Limpiar combos si no hay almacén seleccionado
                PasillosDisponibles.Clear();
                EstanteriasDisponibles.Clear();
                AlturasDisponibles.Clear();
                PosicionesDisponibles.Clear();
            }
            ValidarFormulario();
        }

        partial void OnCodigoInventarioChanged(string oldValue, string newValue)
        {
            ValidarFormulario();
            // Verificar si el código ya existe cuando el usuario termine de escribir
            if (!string.IsNullOrWhiteSpace(newValue))
            {
                _ = VerificarCodigoExistenteAsync(newValue);
            }
        }

        partial void OnUsarRangoArticulosChanged(bool value)
        {
            OnPropertyChanged(nameof(MostrarInfoRango));
            if (value)
            {
                // Si se activa el rango, desactivar filtro específico
                UsarFiltroArticulo = false;
            }
            ValidarFormulario();
        }

        partial void OnArticuloDesdeChanged(string value)
        {
            OnPropertyChanged(nameof(MostrarInfoRango));
            ValidarFormulario();
        }

        partial void OnArticuloHastaChanged(string value)
        {
            OnPropertyChanged(nameof(MostrarInfoRango));
            ValidarFormulario();
        }

        partial void OnUsarRangoUbicacionesChanged(bool oldValue, bool newValue)
        {
            // Los combos ya se cargan automáticamente al seleccionar almacén
            ValidarFormulario();
        }

        partial void OnUsarEstanteriaChanged(bool oldValue, bool newValue)
        {
            if (newValue && EstanteriasDisponibles.Any())
            {
                // Establecer valores por defecto cuando se activa
                EstanteriaDesde = EstanteriasDisponibles.Min();
                EstanteriaHasta = EstanteriasDisponibles.Max();
            }
            ActualizarHabilitacionJerarquica();
            ValidarFormulario();
        }

        partial void OnUsarAlturaChanged(bool oldValue, bool newValue)
        {
            if (newValue && AlturasDisponibles.Any())
            {
                // Establecer valores por defecto cuando se activa
                AlturaDesde = AlturasDisponibles.Min();
                AlturaHasta = AlturasDisponibles.Max();
            }
            ActualizarHabilitacionJerarquica();
            ValidarFormulario();
        }

        partial void OnUsarPosicionChanged(bool oldValue, bool newValue)
        {
            if (newValue && PosicionesDisponibles.Any())
            {
                // Establecer valores por defecto cuando se activa
                PosicionDesde = PosicionesDisponibles.Min();
                PosicionHasta = PosicionesDisponibles.Max();
            }
            ActualizarHabilitacionJerarquica();
            ValidarFormulario();
        }

        partial void OnOpcionArticulosSeleccionadosChanged(string oldValue, string newValue)
        {
            // Si selecciona "Todos", deshabilitar rango de ubicaciones
            if (newValue == "Todos")
            {
                UsarRangoUbicaciones = false;
            }
            
            ValidarFormulario();
        }


        // Callbacks para control jerárquico
        partial void OnPasilloDesdeChanged(int oldValue, int newValue) 
        { 
            ActualizarHabilitacionJerarquica();
            ValidarFormulario();
        }
        partial void OnPasilloHastaChanged(int oldValue, int newValue) 
        { 
            ActualizarHabilitacionJerarquica();
            ValidarFormulario();
        }
        partial void OnEstanteriaDesdeChanged(int oldValue, int newValue) 
        { 
            ActualizarHabilitacionJerarquica();
            ValidarFormulario();
        }
        partial void OnEstanteriaHastaChanged(int oldValue, int newValue) 
        { 
            ActualizarHabilitacionJerarquica();
            ValidarFormulario();
        }
        partial void OnAlturaDesdeChanged(int oldValue, int newValue) 
        { 
            ActualizarHabilitacionJerarquica();
            ValidarFormulario();
        }
        partial void OnAlturaHastaChanged(int oldValue, int newValue) 
        { 
            ActualizarHabilitacionJerarquica();
            ValidarFormulario();
        }
        partial void OnPosicionDesdeChanged(int oldValue, int newValue) 
        { 
            ValidarFormulario();
        }
        partial void OnPosicionHastaChanged(int oldValue, int newValue) 
        { 
            ValidarFormulario();
        }

        // NUEVO: Callbacks para filtro de artículo
        partial void OnUsarFiltroArticuloChanged(bool value)
        {
            OnPropertyChanged(nameof(MostrarInfoArticulo));
            OnPropertyChanged(nameof(DescripcionArticulosSeleccionados));
            if (value)
            {
                // Si se activa el filtro específico, desactivar rango
                UsarRangoArticulos = false;
            }
            else
            {
                // Si se desactiva el filtro, limpiar la lista de artículos seleccionados
                ArticulosSeleccionados.Clear();
                ArticuloSeleccionado = null;
                ArticuloBuscado = string.Empty;
                ArticulosEncontrados.Clear();
            }
            ValidarFormulario();
        }

        partial void OnArticuloSeleccionadoChanged(ArticuloResumenDto? oldValue, ArticuloResumenDto? newValue)
        {
            // Notificar cambios en la visibilidad del Border de información
            OnPropertyChanged(nameof(MostrarInfoArticulo));
            
            // Si hay múltiples resultados y se selecciona uno, mostrar ConfirmationDialog automáticamente
            if (newValue != null && ArticulosEncontrados.Count > 1)
            {
                var mensaje = $"¿Deseas agregar el artículo a la lista?\n\n{newValue.CodigoArticulo} - {newValue.DescripcionArticulo}";
                var confirmacion = new ConfirmationDialog("Agregar artículo", mensaje);
                ShowDialog(confirmacion);
                
                if (confirmacion.DialogResult == true)
                {
                    // Verificar si ya está en la lista
                    if (!ArticulosSeleccionados.Any(a => a.CodigoArticulo == newValue.CodigoArticulo))
                    {
                        ArticulosSeleccionados.Add(newValue);
                        ArticuloSeleccionado = null; // Limpiar selección
                        ArticuloBuscado = string.Empty; // Limpiar búsqueda
                        ArticulosEncontrados.Clear();
                        
                        OnPropertyChanged(nameof(DescripcionArticulosSeleccionados));
                        OnPropertyChanged(nameof(MostrarListaArticulos));
                        ValidarFormulario();
                    }
                    else
                    {
                        ShowDialog(new WarningDialog("Artículo duplicado", $"El artículo {newValue.CodigoArticulo} ya está en la lista."));
                        ArticuloSeleccionado = null; // Limpiar selección para evitar confusión
                    }
                }
                else
                {
                    // Si cancela, limpiar la selección
                    ArticuloSeleccionado = null;
                }
            }
        }

        partial void OnArticulosEncontradosChanged(ObservableCollection<ArticuloResumenDto> oldValue, ObservableCollection<ArticuloResumenDto> newValue)
        {
            OnPropertyChanged(nameof(MostrarListaArticulos));
        }

        partial void OnArticulosSeleccionadosChanged(ObservableCollection<ArticuloResumenDto> oldValue, ObservableCollection<ArticuloResumenDto> newValue)
        {
            OnPropertyChanged(nameof(DescripcionArticulosSeleccionados));
            ValidarFormulario();
        }

        partial void OnIncluirUnidadesCeroChanged(bool value)
        {
            // Solo mostrar advertencia cuando se ACTIVA el checkbox (value == true)
            if (value)
            {
                var mensaje = "⚠️ ADVERTENCIA CRÍTICA ⚠️\n\n" +
                              "Has activado la opción 'Inicializar a 0'.\n\n" +
                              "🔴 RIESGO GRAVE:\n" +
                              "Si cierras el inventario sin modificar todas las cantidades contadas, " +
                              "el sistema generará ajustes que ELIMINARÁN TODO EL STOCK del almacén.\n\n" +
                              "📋 IMPORTANTE:\n" +
                              "• Debes contar y modificar TODAS las líneas del inventario\n" +
                              "• NO cierres el inventario hasta haber verificado todas las cantidades\n" +
                              "• Si no modificas una línea, se considerará como 0 y se eliminará el stock\n\n" +
                              "¿Estás seguro de que quieres activar esta opción?";

                var confirmacion = new ConfirmationDialog(
                    "⚠️ ADVERTENCIA: Inicializar a 0", 
                    mensaje);
                
                ShowDialog(confirmacion);
                
                // Si el usuario cancela, desactivar el checkbox
                if (confirmacion.DialogResult != true)
                {
                    // Usar Dispatcher para evitar problemas de threading
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        IncluirUnidadesCero = false;
                    });
                }
            }
        }
        #endregion

        #region Commands
        [RelayCommand]
        private async Task InitializeAsync()
        {
            try
            {
                await CargarAlmacenesAsync();
                ValidarFormulario();
                

            }
            catch (Exception ex)
            {
                ShowDialog(new WarningDialog("Error", $"Error al inicializar: {ex.Message}"));
            }
        }

        [RelayCommand]
        private async Task CrearAsync()
        {
            try
            {
                if (!PuedeCrear) return;

                // Verificar si el código ya existe antes de crear
                if (CodigoExiste)
                {
                    ShowDialog(new WarningDialog("Código Duplicado", $"El código '{CodigoInventario}' ya existe en esta empresa. Por favor, elija un código diferente."));
                    return;
                }

                // Determinar tipo de inventario
                // Si el usuario selecciona "Con stock", siempre debe ser PARCIAL (independientemente de filtros)
                // Si selecciona "Todos" o tiene filtros específicos, respetar la selección de tipo
                var tipoInventario = TipoInventarioSeleccionado;

                // Forzar a PARCIAL si:
                // 1. El usuario seleccionó PARCIAL explícitamente, O
                // 2. Seleccionó "Con stock" (siempre, con o sin filtros específicos)
                if (TipoInventarioSeleccionado == "PARCIAL" || 
                    OpcionArticulosSeleccionados == "Con stock")
                {
                    tipoInventario = "PARCIAL";
                }

                // Calcular si incluir artículos con stock 0 (para el mensaje de confirmación)
                var incluirStockCeroDebug = tipoInventario == "TOTAL" 
                    ? true 
                    : OpcionArticulosSeleccionados == "Todos";

                // Mostrar diálogo de confirmación
                var mensaje = $"Se va a crear un inventario con las siguientes características:\n\n";
                mensaje += $"• Código: {CodigoInventario}\n";
                
                if (ModoMultialmacen)
                {
                    mensaje += $"• Almacenes: {DescripcionAlmacenesSeleccionados}\n";
                }
                else
                {
                    mensaje += $"• Almacén: {AlmacenSeleccionado?.DescripcionCombo}\n";
                }
                
                mensaje += $"• Tipo: {tipoInventario}\n";
                mensaje += $"• Fecha: {FechaInventario:dd/MM/yyyy}\n";
                mensaje += $"• Artículos: {OpcionArticulosSeleccionados}\n";
                mensaje += $"• Valoración: {ValoracionSeleccionada}\n";
                
                // Mostrar información de filtrado de stock (para depuración)
                mensaje += $"• Incluir artículos con stock 0: {(incluirStockCeroDebug ? "Sí" : "No")}\n";

                if (UsarRangoArticulos)
                    mensaje += $"• Rango artículos: {ArticuloDesde} - {ArticuloHasta}\n";

                if (UsarFiltroArticulo && ArticulosSeleccionados.Any())
                {
                    mensaje += $"• Artículos específicos ({ArticulosSeleccionados.Count}):\n";
                    foreach (var articulo in ArticulosSeleccionados.Take(5))
                    {
                        mensaje += $"  - {articulo.CodigoArticulo} - {articulo.DescripcionArticulo}\n";
                    }
                    if (ArticulosSeleccionados.Count > 5)
                    {
                        mensaje += $"  ... y {ArticulosSeleccionados.Count - 5} más\n";
                    }
                }

                if (UsarRangoUbicaciones)
                {
                    mensaje += $"• Rango ubicaciones:\n";
                    mensaje += $"  - Pasillo: {PasilloDesde} a {PasilloHasta}\n";
                    if (UsarEstanteria)
                        mensaje += $"  - Estantería: {EstanteriaDesde} a {EstanteriaHasta}\n";
                    if (UsarAltura)
                        mensaje += $"  - Altura: {AlturaDesde} a {AlturaHasta}\n";
                    if (UsarPosicion)
                        mensaje += $"  - Posición: {PosicionDesde} a {PosicionHasta}\n";
                }

                if (IncluirUnidadesCero)
                    mensaje += $"• Incluir unidades a 0: Sí\n";
                
                if (IncluirUbicacionesEspeciales)
                    mensaje += $"• Incluir ubicaciones especiales: Sí\n";



                mensaje += $"\n¿Desea continuar con la creación del inventario?";

                var confirmacion = new ConfirmationDialog("Confirmar creación de inventario", mensaje);
                ShowDialog(confirmacion);
                if (confirmacion.DialogResult != true) return;

                // Determinar si incluir artículos con stock 0
                // Reglas:
                // 1. Si es inventario TOTAL → siempre incluir artículos con stock 0
                // 2. Si es PARCIAL y "Con stock" → NO incluir artículos con stock 0 (solo con stock > 0)
                // 3. Si es PARCIAL y "Todos" → incluir artículos con stock 0
                // 4. Si hay filtro de artículo específico o rango:
                //    - Si "Con stock" → NO incluir artículos con stock 0 (solo los específicos con stock)
                //    - Si "Todos" → incluir todos los artículos específicos (incluso a 0)
                bool incluirArticulosConStockCero;
                if (tipoInventario == "TOTAL")
                {
                    incluirArticulosConStockCero = true;
                }
                else
                {
                // Si es PARCIAL, usar la selección del combo
                    // "Con stock" = false, "Todos" = true
                    incluirArticulosConStockCero = OpcionArticulosSeleccionados == "Todos";
                }

                var dto = new CrearInventarioDto
                {
                    CodigoInventario = CodigoInventario,
                    CodigoEmpresa = SessionManager.EmpresaSeleccionada!.Value,
                    TipoInventario = tipoInventario,
                    FechaInventario = FechaInventario.Date, // Asegurar que solo se envía la fecha sin hora
                    Comentarios = Comentarios,
                    UsuarioCreacionId = SessionManager.UsuarioActual!.operario,
                    IncluirUnidadesCero = IncluirUnidadesCero, // Checkbox "Inicializar a 0"
                    IncluirArticulosConStockCero = incluirArticulosConStockCero, // TRUE si es TOTAL, o según combo si es PARCIAL
                    IncluirUbicacionesEspeciales = IncluirUbicacionesEspeciales,
                    NoGenerarLineas = NoGenerarLineas, // Crear inventario vacío
                    // NUEVO: Filtro de artículos específicos (múltiples)
                    CodigosArticuloFiltro = UsarFiltroArticulo && ArticulosSeleccionados.Any() 
                        ? ArticulosSeleccionados.Select(a => a.CodigoArticulo).ToList() 
                        : null,
                    // NUEVO: Rango de artículos
                    ArticuloDesde = UsarRangoArticulos ? ArticuloDesde : null,
                    ArticuloHasta = UsarRangoArticulos ? ArticuloHasta : null
                };

                // Agregar rangos de ubicaciones basándose en los checkboxes individuales
                // Se envía el rango si está especificado, independientemente del tipo de inventario
                if (UsarRangoUbicaciones)
                {
                    // Pasillo siempre se envía si hay rango de ubicaciones
                    dto.PasilloDesde = PasilloDesde;
                    dto.PasilloHasta = PasilloHasta;
                    
                    // Estantería solo si está habilitada
                    if (UsarEstanteria)
                    {
                        dto.EstanteriaDesde = EstanteriaDesde;
                        dto.EstanteriaHasta = EstanteriaHasta;
                    }
                    
                    // Altura solo si está habilitada
                    if (UsarAltura)
                    {
                        dto.AlturaDesde = AlturaDesde;
                        dto.AlturaHasta = AlturaHasta;
                    }
                    
                    // Posición solo si está habilitada
                    if (UsarPosicion)
                    {
                        dto.PosicionDesde = PosicionDesde;
                        dto.PosicionHasta = PosicionHasta;
                    }
                }

                // Configurar almacenes según el modo seleccionado
                if (ModoMultialmacen)
                {
                    // Modo multialmacén: usar lista de códigos
                    dto.CodigosAlmacen = AlmacenesSeleccionados.Select(a => a.CodigoAlmacen).ToList();
                    dto.CodigoAlmacen = dto.CodigosAlmacen.FirstOrDefault() ?? ""; // Compatibilidad hacia atrás
                }
                else
                {
                    // Modo único: usar almacén seleccionado
                    dto.CodigoAlmacen = AlmacenSeleccionado!.CodigoAlmacen;
                    dto.CodigosAlmacen = new List<string> { dto.CodigoAlmacen }; // Para que la API funcione
                }

                var resultado = await _inventarioService.CrearInventarioAsync(dto);

                if (resultado != null)
                {
                    // Construir mensaje con información detallada
                    var mensajeExito = $"✅ {resultado.Mensaje}\n\n";
                    
                    if (NoGenerarLineas)
                    {
                        mensajeExito += "📋 Inventario vacío creado.\n";
                        mensajeExito += "Puede agregar líneas manualmente usando el botón 'Agregar línea'.";
                    }
                    else
                    {
                        mensajeExito += $"📊 Líneas generadas: {resultado.LineasGeneradas}\n";
                        if (resultado.UbicacionesEnRango > 0)
                        {
                            mensajeExito += $"📍 Ubicaciones en rango: {resultado.UbicacionesEnRango}\n";
                        }
                        if (resultado.StockEncontrado > 0)
                        {
                            mensajeExito += $"📦 Stock encontrado: {resultado.StockEncontrado}";
                        }
                    }
                    
                    ShowDialog(new WarningDialog("Éxito", mensajeExito));
                    CerrarDialogo(true);
                }
                else
                {
                    ShowDialog(new WarningDialog("Error", "Error al crear el inventario."));
                }
            }
            catch (Exception ex)
            {
                ShowDialog(new WarningDialog("Error", $"Error al crear inventario: {ex.Message}"));
            }
        }

        [RelayCommand]
        private async Task BuscarArticuloAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ArticuloBuscado))
                {
                    ShowDialog(new WarningDialog("Buscar artículo", "Introduce un código o descripción para buscar."));
                    return;
                }

                // Validar según el modo (multialmacén o único)
                List<string> almacenesParaBuscar = new();
                if (ModoMultialmacen)
                {
                    var seleccionados = AlmacenesSeleccionados;
                    if (!seleccionados.Any())
                    {
                        ShowDialog(new WarningDialog("Buscar artículo", "Primero selecciona al menos un almacén."));
                        return;
                    }
                    almacenesParaBuscar = seleccionados.Select(a => a.CodigoAlmacen).ToList();
                }
                else
                {
                    if (AlmacenSeleccionado == null)
                    {
                        ShowDialog(new WarningDialog("Buscar artículo", "Primero selecciona un almacén."));
                        return;
                    }
                    almacenesParaBuscar.Add(AlmacenSeleccionado.CodigoAlmacen);
                }

                ArticulosEncontrados.Clear();
                ArticuloSeleccionado = null;

                var empresa = SessionManager.EmpresaSeleccionada!.Value;
                var terminoBusqueda = ArticuloBuscado.Trim();
                
                List<StockDto> resultados = new();
                string tipoBusqueda = "";

                // Buscar en cada almacén seleccionado
                foreach (var codigoAlmacen in almacenesParaBuscar)
                {
                    List<StockDto> resultadosAlmacen = new();

                    // Intentar buscar por código primero (si parece un código)
                    if (terminoBusqueda.Length <= 20 && !terminoBusqueda.Contains(" "))
                    {
                        tipoBusqueda = "código";
                        resultadosAlmacen = await _stockService.ObtenerPorArticuloAsync(
                            empresa,
                            codigoArticulo: terminoBusqueda,
                            codigoAlmacen: codigoAlmacen
                        );
                    }

                    // Si no encuentra por código o el término parece una descripción, buscar por descripción
                    if (!resultadosAlmacen.Any())
                    {
                        tipoBusqueda = terminoBusqueda.Length <= 20 && !terminoBusqueda.Contains(" ") ? 
                            "código (sin resultados), luego descripción" : "descripción";
                        
                        resultadosAlmacen = await _stockService.ObtenerPorArticuloAsync(
                            empresa,
                            codigoArticulo: null,
                            codigoAlmacen: codigoAlmacen,
                            descripcion: terminoBusqueda
                        );
                    }

                    resultados.AddRange(resultadosAlmacen);
                }

                // Agrupar por artículo (eliminar duplicados si el mismo artículo está en múltiples almacenes)
                var grupos = resultados
                    .GroupBy(x => new { x.CodigoArticulo, x.DescripcionArticulo })
                    .Select(g => new ArticuloResumenDto
                    {
                        CodigoArticulo = g.Key.CodigoArticulo,
                        DescripcionArticulo = g.Key.DescripcionArticulo ?? ""
                    })
                    .OrderBy(a => a.CodigoArticulo)
                    .ToList();

                foreach (var articulo in grupos)
                {
                    ArticulosEncontrados.Add(articulo);
                }

                // Mostrar mensaje apropiado según los resultados
                if (ArticulosEncontrados.Count == 1)
                {
                    ArticuloSeleccionado = ArticulosEncontrados.First();
                    var almacenesTexto = ModoMultialmacen 
                        ? $" en {almacenesParaBuscar.Count} almacén{(almacenesParaBuscar.Count > 1 ? "es" : "")}" 
                        : $" en el almacén {almacenesParaBuscar.First()}";
                    var mensaje = $"✓ Encontrado por {tipoBusqueda}{almacenesTexto}:\n{ArticuloSeleccionado.CodigoArticulo} - {ArticuloSeleccionado.DescripcionArticulo}\n\n¿Deseas agregarlo a la lista?";
                    
                    var confirmacion = new ConfirmationDialog("Artículo encontrado", mensaje);
                    ShowDialog(confirmacion);
                    
                    // Si el usuario confirma, agregar directamente
                    if (confirmacion.DialogResult == true)
                    {
                        // Verificar si ya está en la lista
                        if (!ArticulosSeleccionados.Any(a => a.CodigoArticulo == ArticuloSeleccionado!.CodigoArticulo))
                        {
                            ArticulosSeleccionados.Add(ArticuloSeleccionado);
                            ArticuloSeleccionado = null; // Limpiar selección
                            ArticuloBuscado = string.Empty; // Limpiar búsqueda
                            ArticulosEncontrados.Clear();
                            
                            OnPropertyChanged(nameof(DescripcionArticulosSeleccionados));
                            OnPropertyChanged(nameof(MostrarListaArticulos));
                            ValidarFormulario();
                        }
                        else
                        {
                            ShowDialog(new WarningDialog("Artículo duplicado", $"El artículo {ArticuloSeleccionado.CodigoArticulo} ya está en la lista."));
                            ArticuloSeleccionado = null; // Limpiar selección
                        }
                    }
                    else
                    {
                        // Si cancela, limpiar la selección
                        ArticuloSeleccionado = null;
                    }
                }
                else if (ArticulosEncontrados.Count > 1)
                {
                    var almacenesTexto = ModoMultialmacen 
                        ? $" en {almacenesParaBuscar.Count} almacén{(almacenesParaBuscar.Count > 1 ? "es" : "")}" 
                        : $" en el almacén {almacenesParaBuscar.First()}";
                    var mensaje = $"Se encontraron {ArticulosEncontrados.Count} artículos por {tipoBusqueda}{almacenesTexto}.\nSelecciona uno de la lista.";
                    ShowDialog(new WarningDialog("Múltiples resultados", mensaje));
                }
                else
                {
                    var almacenesTexto = ModoMultialmacen 
                        ? $" en los almacenes seleccionados ({string.Join(", ", almacenesParaBuscar)})" 
                        : $" en el almacén {almacenesParaBuscar.First()}";
                    var mensaje = $"No se encontraron artículos buscando '{terminoBusqueda}' por {tipoBusqueda}{almacenesTexto}.\n\n";
                    mensaje += "💡 Consejos:\n";
                    mensaje += "• Para buscar por código: introduce el código exacto (ej: 10000)\n";
                    mensaje += "• Para buscar por descripción: introduce parte de la descripción (ej: azúcar)\n";
                    mensaje += "• Verifica que el artículo tiene stock en los almacenes seleccionados";
                    
                    ShowDialog(new WarningDialog("Sin resultados", mensaje));
                }

                // Notificar cambios en visibilidad
                OnPropertyChanged(nameof(MostrarListaArticulos));
            }
            catch (Exception ex)
            {
                ShowDialog(new WarningDialog("Error", $"Error al buscar artículo: {ex.Message}"));
            }
        }




        [RelayCommand]
        private void AgregarArticulo()
        {
            if (ArticuloSeleccionado == null)
            {
                ShowDialog(new WarningDialog("Agregar artículo", "Primero selecciona un artículo de los resultados de búsqueda."));
                return;
            }

            // Verificar si ya está en la lista
            if (ArticulosSeleccionados.Any(a => a.CodigoArticulo == ArticuloSeleccionado.CodigoArticulo))
            {
                ShowDialog(new WarningDialog("Artículo duplicado", $"El artículo {ArticuloSeleccionado.CodigoArticulo} ya está en la lista."));
                return;
            }

            ArticulosSeleccionados.Add(ArticuloSeleccionado);
            ArticuloSeleccionado = null; // Limpiar selección (esto disparará OnArticuloSeleccionadoChanged que notificará MostrarInfoArticulo)
            ArticuloBuscado = string.Empty; // Limpiar búsqueda
            ArticulosEncontrados.Clear();
            
            OnPropertyChanged(nameof(DescripcionArticulosSeleccionados));
            OnPropertyChanged(nameof(MostrarListaArticulos));
            ValidarFormulario();
        }

        [RelayCommand]
        private void EliminarArticulo(ArticuloResumenDto articulo)
        {
            if (articulo == null) return;
            
            ArticulosSeleccionados.Remove(articulo);
            OnPropertyChanged(nameof(DescripcionArticulosSeleccionados));
            ValidarFormulario();
        }

        [RelayCommand]
        private void BuscarUbicacionDesde()
        {
            // TODO: Implementar búsqueda de ubicaciones
            ShowDialog(new WarningDialog("Info", "Búsqueda de ubicaciones - En desarrollo"));
        }

        [RelayCommand]
        private void BuscarUbicacionHasta()
        {
            // TODO: Implementar búsqueda de ubicaciones
            ShowDialog(new WarningDialog("Info", "Búsqueda de ubicaciones - En desarrollo"));
        }

        [RelayCommand]
        private void Cancelar()
        {
            CerrarDialogo(false);
        }

        [RelayCommand]
        private void MarcarTodos()
        {
            if (!ModoMultialmacen) return; // Solo funciona en modo multialmacén
            
            var todosMarcados = AlmacenesDisponibles.All(a => a.IsSelected);
            foreach (var almacen in AlmacenesDisponibles)
            {
                almacen.IsSelected = !todosMarcados;
            }
            OnPropertyChanged(nameof(DescripcionAlmacenesSeleccionados));
            ValidarFormulario();
        }

        public void NotificarCambioSeleccionAlmacen()
        {
            OnPropertyChanged(nameof(DescripcionAlmacenesSeleccionados));
            ValidarFormulario();
        }

        private void AlmacenDto_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AlmacenDto.IsSelected))
            {
                OnPropertyChanged(nameof(DescripcionAlmacenesSeleccionados));
                ValidarFormulario();
            }
        }
        #endregion

        #region Private Methods
        private async Task CargarAlmacenesAsync()
        {
            try
            {
                var empresa = SessionManager.EmpresaSeleccionada!.Value;
                var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
                var desdeLogin = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();

                var resultado = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa, centro, desdeLogin);

                AlmacenesDisponibles.Clear();
                
                // Si no hay almacenes autorizados, agregar algunos de prueba
                if (!resultado.Any())
                {
                    var almacenesPrueba = new List<AlmacenDto>
                    {
                        new AlmacenDto
                        {
                            CodigoAlmacen = "000",
                            NombreAlmacen = "SALIDA EXPEDICIONES",
                            CodigoEmpresa = empresa,
                            EsDelCentro = true
                        },
                        new AlmacenDto
                        {
                            CodigoAlmacen = "001",
                            NombreAlmacen = "ALMACEN MATERIAS PRI",
                            CodigoEmpresa = empresa,
                            EsDelCentro = true
                        },
                        new AlmacenDto
                        {
                            CodigoAlmacen = "002",
                            NombreAlmacen = "FABRICACIÓN CANELA",
                            CodigoEmpresa = empresa,
                            EsDelCentro = true
                        },
                        new AlmacenDto
                        {
                            CodigoAlmacen = "003",
                            NombreAlmacen = "ALMACEN DE RECHAZOS",
                            CodigoEmpresa = empresa,
                            EsDelCentro = true
                        },
                        new AlmacenDto
                        {
                            CodigoAlmacen = "004",
                            NombreAlmacen = "TRANSITO",
                            CodigoEmpresa = empresa,
                            EsDelCentro = true
                        }
                    };

                    foreach (var almacen in almacenesPrueba)
                    {
                        almacen.PropertyChanged += AlmacenDto_PropertyChanged;
                        AlmacenesDisponibles.Add(almacen);
                    }
                }
                else
                {
                                    foreach (var almacen in resultado)
                {
                    almacen.PropertyChanged += AlmacenDto_PropertyChanged;
                    AlmacenesDisponibles.Add(almacen);
                }
                }

                // Seleccionar el primer almacén por defecto
                if (AlmacenesDisponibles.Any())
                {
                    AlmacenSeleccionado = AlmacenesDisponibles.First();
                    AlmacenSeleccionado.IsSelected = true;
                }
            }
            catch (Exception ex)
            {
                ShowDialog(new WarningDialog("Error", $"Error al cargar almacenes: {ex.Message}"));
                
                // En caso de error, agregar almacenes de prueba
                AlmacenesDisponibles.Clear();
                var almacenesError = new List<AlmacenDto>
                {
                    new AlmacenDto
                    {
                        CodigoAlmacen = "000",
                        NombreAlmacen = "SALIDA EXPEDICIONES",
                        CodigoEmpresa = SessionManager.EmpresaSeleccionada!.Value,
                        EsDelCentro = true
                    },
                    new AlmacenDto
                    {
                        CodigoAlmacen = "001",
                        NombreAlmacen = "ALMACEN MATERIAS PRI",
                        CodigoEmpresa = SessionManager.EmpresaSeleccionada!.Value,
                        EsDelCentro = true
                    }
                };

                foreach (var almacen in almacenesError)
                {
                    almacen.PropertyChanged += AlmacenDto_PropertyChanged;
                    AlmacenesDisponibles.Add(almacen);
                }
                
                if (AlmacenesDisponibles.Any())
                {
                    AlmacenSeleccionado = AlmacenesDisponibles.First();
                    AlmacenSeleccionado.IsSelected = true;
                }
            }
        }

        private void ValidarFormulario()
        {
            // Validar almacenes según el modo
            bool almacenesValidos;
            if (ModoMultialmacen)
            {
                almacenesValidos = AlmacenesSeleccionados.Any();
            }
            else
            {
                almacenesValidos = AlmacenSeleccionado != null;
            }

            var esValido = almacenesValidos &&
                          !string.IsNullOrWhiteSpace(TipoInventarioSeleccionado) &&
                          !string.IsNullOrWhiteSpace(CodigoInventario) &&
                          ValidarRangos();

            PuedeCrear = esValido;
        }

        private async Task VerificarCodigoExistenteAsync(string codigo)
        {
            try
            {
                // Verificar si el código ya existe en la empresa actual
                var inventarios = await _inventarioService.ObtenerInventariosAsync();
                var existe = inventarios.Any(i => i.CodigoInventario.Equals(codigo, StringComparison.OrdinalIgnoreCase));
                
                CodigoExiste = existe;
                MensajeErrorCodigo = existe ? $"El código '{codigo}' ya existe en esta empresa" : string.Empty;
                
                // Actualizar validación
                ValidarFormulario();
            }
            catch (Exception ex)
            {
                // En caso de error, no bloquear la creación
                CodigoExiste = false;
                MensajeErrorCodigo = string.Empty;
            }
        }

        private bool ValidarRangos()
        {
            // Validar rangos de artículos si están habilitados
            if (UsarRangoArticulos)
            {
                if (string.IsNullOrWhiteSpace(ArticuloDesde) || string.IsNullOrWhiteSpace(ArticuloHasta))
                    return false;
            }

            // Validar filtro de artículos específicos
            if (UsarFiltroArticulo)
            {
                return ArticulosSeleccionados.Any();
            }

            return true;
        }



        private async Task CargarRangosDisponiblesAsync()
        {
            try
            {
                // Solo cargar rangos en modo almacén único
                if (ModoMultialmacen || AlmacenSeleccionado == null) return;

                var rangos = await _inventarioService.ObtenerRangosDisponiblesAsync(
                    SessionManager.EmpresaSeleccionada!.Value,
                    AlmacenSeleccionado.CodigoAlmacen
                );

                // Limpiar y cargar las colecciones
                PasillosDisponibles.Clear();
                EstanteriasDisponibles.Clear();
                AlturasDisponibles.Clear();
                PosicionesDisponibles.Clear();

                foreach (var pasillo in rangos.Pasillos ?? new List<int>())
                    PasillosDisponibles.Add(pasillo);

                foreach (var estanteria in rangos.Estanterias ?? new List<int>())
                    EstanteriasDisponibles.Add(estanteria);

                foreach (var altura in rangos.Alturas ?? new List<int>())
                    AlturasDisponibles.Add(altura);

                foreach (var posicion in rangos.Posiciones ?? new List<int>())
                    PosicionesDisponibles.Add(posicion);

                // Solo establecer valores por defecto para Pasillo (siempre habilitado)
                if (PasillosDisponibles.Any())
                {
                    var minPasillo = PasillosDisponibles.Min();
                    var maxPasillo = PasillosDisponibles.Max();
                    PasilloDesde = minPasillo;
                    PasilloHasta = maxPasillo;
                }

                // Los demás niveles se inicializan en 0 hasta que el usuario los active
                EstanteriaDesde = 0;
                EstanteriaHasta = 0;
                AlturaDesde = 0;
                AlturaHasta = 0;
                PosicionDesde = 0;
                PosicionHasta = 0;

                // Inicializar habilitación jerárquica
                ActualizarHabilitacionJerarquica();
            }
            catch (Exception ex)
            {
                ShowDialog(new WarningDialog("Error", $"Error al cargar rangos disponibles: {ex.Message}"));
            }
        }

        private void ActualizarHabilitacionJerarquica()
        {
            // Estantería se habilita si el usuario activa el checkbox y hay un rango válido de pasillo
            EstanteriaHabilitada = UsarEstanteria && PasilloDesde > 0 && PasilloHasta > 0 && PasilloDesde <= PasilloHasta;

            // Altura se habilita si el usuario activa el checkbox y hay un rango válido de estantería
            AlturaHabilitada = UsarAltura && EstanteriaHabilitada && EstanteriaDesde > 0 && EstanteriaHasta > 0 && EstanteriaDesde <= EstanteriaHasta;

            // Posición se habilita si el usuario activa el checkbox y hay un rango válido de altura
            PosicionHabilitada = UsarPosicion && AlturaHabilitada && AlturaDesde > 0 && AlturaHasta > 0 && AlturaDesde <= AlturaHasta;

            // Si se deshabilita un nivel, limpiar los niveles inferiores
            if (!UsarEstanteria)
            {
                EstanteriaDesde = 0;
                EstanteriaHasta = 0;
                UsarAltura = false;
                UsarPosicion = false;
            }
            if (!UsarAltura)
            {
                AlturaDesde = 0;
                AlturaHasta = 0;
                UsarPosicion = false;
            }
            if (!UsarPosicion)
            {
                PosicionDesde = 0;
                PosicionHasta = 0;
            }
        }



        private void ShowDialog(Window dialog)
        {
            var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                     ?? Application.Current.MainWindow;
            if (owner != null && owner != dialog)
                dialog.Owner = owner;
            dialog.ShowDialog();
        }

        private void CerrarDialogo(bool resultado)
        {
            if (Application.Current.Windows.OfType<CrearInventarioDialog>().FirstOrDefault() is CrearInventarioDialog dialog)
            {
                dialog.DialogResult = resultado;
                dialog.Close();
            }
        }
        #endregion
    }
} 