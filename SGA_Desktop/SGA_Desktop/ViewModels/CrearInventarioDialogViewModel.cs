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
            OpcionesArticulos = new ObservableCollection<string> { "Todos", "Con stock", "Sin stock" };
            OpcionesValoracion = new ObservableCollection<string> 
            { 
                "Precio medio de las entradas", 
                "Precio estándar", 
                "Último precio de compra",
                "Precio de venta"
            };
            
            // Valores por defecto
            TipoInventarioSeleccionado = "TOTAL";
            Comentarios = $"Inventario creado el {DateTime.Now:dd/MM/yyyy HH:mm}";

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

        [ObservableProperty]
        private string tipoInventarioSeleccionado = "TOTAL";

        [ObservableProperty]
        private string codigoInventario = string.Empty;

        [ObservableProperty]
        private DateTime fechaInventario = DateTime.Today;

        [ObservableProperty]
        private string articulosSeleccionados = "Todos";

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
        #endregion



        #region Property Change Callbacks
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
        }

        partial void OnUsarRangoArticulosChanged(bool oldValue, bool newValue)
        {
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
        #endregion

        #region Commands
        [RelayCommand]
        private async Task InitializeAsync()
        {
            try
            {
                await CargarAlmacenesAsync();
                ValidarFormulario();
                
                // Debug: mostrar cuántos almacenes se cargaron
                MessageBox.Show($"Almacenes cargados: {AlmacenesDisponibles.Count}", "Debug", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al inicializar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task CrearAsync()
        {
            try
            {
                if (!PuedeCrear) return;

                // Mostrar diálogo de confirmación
                var mensaje = $"Se va a crear un inventario con las siguientes características:\n\n";
                mensaje += $"• Código: {CodigoInventario}\n";
                mensaje += $"• Almacén: {AlmacenSeleccionado?.DescripcionCombo}\n";
                mensaje += $"• Tipo: {TipoInventarioSeleccionado}\n";
                mensaje += $"• Fecha: {FechaInventario:dd/MM/yyyy}\n";
                mensaje += $"• Artículos: {ArticulosSeleccionados}\n";
                mensaje += $"• Valoración: {ValoracionSeleccionada}\n";

                if (UsarRangoArticulos)
                    mensaje += $"• Rango artículos: {ArticuloDesde} - {ArticuloHasta}\n";

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

                mensaje += $"\n¿Desea continuar con la creación del inventario?";

                var confirmacion = MessageBox.Show(mensaje, "Confirmar creación de inventario", 
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (confirmacion != MessageBoxResult.Yes) return;

                var dto = new CrearInventarioDto
                {
                    CodigoEmpresa = SessionManager.EmpresaSeleccionada!.Value,
                    CodigoAlmacen = AlmacenSeleccionado!.CodigoAlmacen,
                    TipoInventario = TipoInventarioSeleccionado,
                    Comentarios = Comentarios,
                    UsuarioCreacionId = SessionManager.UsuarioActual!.operario
                };

                // Agregar rangos de ubicaciones si están habilitados
                if (UsarRangoUbicaciones)
                {
                    dto.PasilloDesde = PasilloDesde;
                    dto.PasilloHasta = PasilloHasta;
                    dto.EstanteriaDesde = EstanteriaDesde;
                    dto.EstanteriaHasta = EstanteriaHasta;
                    dto.AlturaDesde = AlturaDesde;
                    dto.AlturaHasta = AlturaHasta;
                    dto.PosicionDesde = PosicionDesde;
                    dto.PosicionHasta = PosicionHasta;
                }

                var resultado = await _inventarioService.CrearInventarioAsync(dto);

                if (resultado)
                {
                    MessageBox.Show("Inventario creado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    CerrarDialogo(true);
                }
                else
                {
                    MessageBox.Show("Error al crear el inventario.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al crear inventario: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        [RelayCommand]
        private void MarcarTodos()
        {
            var todosMarcados = AlmacenesDisponibles.All(a => a.IsSelected);
            foreach (var almacen in AlmacenesDisponibles)
            {
                almacen.IsSelected = !todosMarcados;
            }
            ValidarFormulario();
        }

        [RelayCommand]
        private void BuscarArticuloDesde()
        {
            // TODO: Implementar búsqueda de artículos
            MessageBox.Show("Búsqueda de artículos - En desarrollo", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void BuscarArticuloHasta()
        {
            // TODO: Implementar búsqueda de artículos
            MessageBox.Show("Búsqueda de artículos - En desarrollo", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void BuscarUbicacionDesde()
        {
            // TODO: Implementar búsqueda de ubicaciones
            MessageBox.Show("Búsqueda de ubicaciones - En desarrollo", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void BuscarUbicacionHasta()
        {
            // TODO: Implementar búsqueda de ubicaciones
            MessageBox.Show("Búsqueda de ubicaciones - En desarrollo", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void Cancelar()
        {
            CerrarDialogo(false);
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
                    AlmacenesDisponibles.Add(new AlmacenDto
                    {
                        CodigoAlmacen = "000",
                        NombreAlmacen = "SALIDA EXPEDICIONES",
                        CodigoEmpresa = empresa,
                        EsDelCentro = true
                    });
                    AlmacenesDisponibles.Add(new AlmacenDto
                    {
                        CodigoAlmacen = "001",
                        NombreAlmacen = "ALMACEN MATERIAS PRI",
                        CodigoEmpresa = empresa,
                        EsDelCentro = true
                    });
                    AlmacenesDisponibles.Add(new AlmacenDto
                    {
                        CodigoAlmacen = "002",
                        NombreAlmacen = "FABRICACIÓN CANELA",
                        CodigoEmpresa = empresa,
                        EsDelCentro = true
                    });
                    AlmacenesDisponibles.Add(new AlmacenDto
                    {
                        CodigoAlmacen = "003",
                        NombreAlmacen = "ALMACEN DE RECHAZOS",
                        CodigoEmpresa = empresa,
                        EsDelCentro = true
                    });
                    AlmacenesDisponibles.Add(new AlmacenDto
                    {
                        CodigoAlmacen = "004",
                        NombreAlmacen = "TRANSITO",
                        CodigoEmpresa = empresa,
                        EsDelCentro = true
                    });
                }
                else
                {
                    foreach (var almacen in resultado)
                    {
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
                MessageBox.Show($"Error al cargar almacenes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // En caso de error, agregar almacenes de prueba
                AlmacenesDisponibles.Clear();
                AlmacenesDisponibles.Add(new AlmacenDto
                {
                    CodigoAlmacen = "000",
                    NombreAlmacen = "SALIDA EXPEDICIONES",
                    CodigoEmpresa = SessionManager.EmpresaSeleccionada!.Value,
                    EsDelCentro = true
                });
                AlmacenesDisponibles.Add(new AlmacenDto
                {
                    CodigoAlmacen = "001",
                    NombreAlmacen = "ALMACEN MATERIAS PRI",
                    CodigoEmpresa = SessionManager.EmpresaSeleccionada!.Value,
                    EsDelCentro = true
                });
                
                if (AlmacenesDisponibles.Any())
                {
                    AlmacenSeleccionado = AlmacenesDisponibles.First();
                    AlmacenSeleccionado.IsSelected = true;
                }
            }
        }

        private void ValidarFormulario()
        {
            var esValido = AlmacenSeleccionado != null &&
                          !string.IsNullOrWhiteSpace(TipoInventarioSeleccionado) &&
                          !string.IsNullOrWhiteSpace(CodigoInventario) &&
                          ValidarRangos();

            PuedeCrear = esValido;
        }

        private bool ValidarRangos()
        {
            // Validar rangos de artículos si están habilitados
            if (UsarRangoArticulos)
            {
                if (string.IsNullOrWhiteSpace(ArticuloDesde) || string.IsNullOrWhiteSpace(ArticuloHasta))
                    return false;
            }



            return true;
        }



        private async Task CargarRangosDisponiblesAsync()
        {
            try
            {
                if (AlmacenSeleccionado == null) return;

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
                MessageBox.Show($"Error al cargar rangos disponibles: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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