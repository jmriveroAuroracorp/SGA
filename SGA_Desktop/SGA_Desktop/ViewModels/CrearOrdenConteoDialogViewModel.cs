using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Dialog;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace SGA_Desktop.ViewModels
{
    public partial class CrearOrdenConteoDialogViewModel : ObservableObject
    {
        #region Fields & Services
        private readonly ConteosService _conteosService;
        private readonly StockService _stockService;
        #endregion

        #region Constructor
        public CrearOrdenConteoDialogViewModel(ConteosService conteosService, StockService stockService)
        {
            _conteosService = conteosService;
            _stockService = stockService;
            
            // Inicializar colecciones
            AlcancesDisponibles = new ObservableCollection<string>
            {
                "ALMACEN",
                "PASILLO", 
                "ESTANTERIA",
                "UBICACION",
                "ARTICULO",
                "PALET"
            };

            PrioridadesDisponibles = new ObservableCollection<PrioridadItem>
            {
                new() { Valor = 1, Texto = "1 - Muy Baja" },
                new() { Valor = 2, Texto = "2 - Baja" },
                new() { Valor = 3, Texto = "3 - Normal" },
                new() { Valor = 4, Texto = "4 - Alta" },
                new() { Valor = 5, Texto = "5 - Muy Alta" }
            };

            AlmacenesDisponibles = new ObservableCollection<AlmacenDto>();

            // Valores por defecto
            AlcanceSeleccionado = "ALMACEN";
            PrioridadSeleccionada = PrioridadesDisponibles.FirstOrDefault(p => p.Valor == 3);
            FechaPlan = DateTime.Today.AddDays(1);
            CodigoOperario = SessionManager.UsuarioActual?.operario.ToString() ?? "";

            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                _ = InitializeAsync();
        }

        public CrearOrdenConteoDialogViewModel() : this(new ConteosService(), new StockService()) { }
        #endregion

        #region Observable Properties
        public ObservableCollection<string> AlcancesDisponibles { get; }
        public ObservableCollection<PrioridadItem> PrioridadesDisponibles { get; }
        public ObservableCollection<AlmacenDto> AlmacenesDisponibles { get; }

        [ObservableProperty]
        private string titulo = string.Empty;

        [ObservableProperty]
        private string alcanceSeleccionado = "ALMACEN";

        [ObservableProperty]
        private PrioridadItem? prioridadSeleccionada;

        [ObservableProperty]
        private AlmacenDto? almacenSeleccionado;

        [ObservableProperty]
        private string codigoOperario = string.Empty;

        [ObservableProperty]
        private DateTime? fechaPlan;



        [ObservableProperty]
        private string comentario = string.Empty;

        // Filtros específicos
        [ObservableProperty]
        private string pasillo = string.Empty;

        [ObservableProperty]
        private string estanteria = string.Empty;

        [ObservableProperty]
        private string altura = string.Empty;

        [ObservableProperty]
        private string posicion = string.Empty;

        [ObservableProperty]
        private string codigoArticulo = string.Empty;

        // Propiedades para búsqueda de artículos
        [ObservableProperty]
        private string articuloBuscado = string.Empty;

        [ObservableProperty]
        private ObservableCollection<ArticuloResumenDto> articulosEncontrados = new();

        [ObservableProperty]
        private ArticuloResumenDto? articuloSeleccionado;

        // Estados
        [ObservableProperty]
        private bool isCargando = false;

        [ObservableProperty]
        private string mensajeEstado = string.Empty;

        // Referencia al diálogo para cerrarlo
        public Window? DialogResult { get; set; }
        #endregion

        #region Computed Properties
        public bool PuedeCrearOrden => !IsCargando && !string.IsNullOrWhiteSpace(Titulo);

        public bool MostrarFiltrosUbicacion => AlcanceSeleccionado is "PASILLO" or "ESTANTERIA" or "UBICACION";
        public bool MostrarFiltroArticulo => AlcanceSeleccionado == "ARTICULO";
        public bool MostrarListaArticulos => ArticulosEncontrados.Count > 1;
        public bool MostrarInfoArticulo => ArticuloSeleccionado != null;
        #endregion

        #region Commands
        [RelayCommand]
        private async Task CrearOrden()
        {
            try
            {
                IsCargando = true;
                MensajeEstado = "Creando orden de conteo...";

                // Validaciones
                if (string.IsNullOrWhiteSpace(Titulo))
                {
                    var warningDialog = new WarningDialog("Error de validación", "El título es obligatorio");
                    warningDialog.ShowDialog();
                    return;
                }

                // Crear el DTO
                var dto = new CrearOrdenConteoDto
                {
                    CodigoEmpresa = SessionManager.EmpresaSeleccionada ?? 1,
                    Titulo = Titulo.Trim(),
                    Visibilidad = "VISIBLE",
                    Estado = "ASIGNADO",
                    ModoGeneracion = "AUTOMATICO",
                    Alcance = AlcanceSeleccionado,
                    FiltrosJson = GenerarFiltrosJson(),
                    FechaPlan = FechaPlan,
                    CreadoPorCodigo = SessionManager.UsuarioActual?.operario.ToString() ?? "ADMIN",
                    Prioridad = (byte)(PrioridadSeleccionada?.Valor ?? 3),
                    CodigoOperario = string.IsNullOrWhiteSpace(CodigoOperario) ? null : CodigoOperario.Trim(),
                    CodigoAlmacen = AlmacenSeleccionado?.CodigoAlmacen,
                    Comentario = string.IsNullOrWhiteSpace(Comentario) ? null : Comentario.Trim()
                };

                // Si el alcance es ARTICULO, agregar el código del artículo
                if (AlcanceSeleccionado == "ARTICULO" && !string.IsNullOrWhiteSpace(CodigoArticulo))
                {
                    dto.CodigoArticulo = CodigoArticulo.Trim();
                }

                // Crear la orden
                var ordenCreada = await _conteosService.CrearOrdenAsync(dto);

                // Mostrar mensaje de éxito
                var successDialog = new WarningDialog(
                    "Orden Creada", 
                    $"La orden #{ordenCreada.Id} '{ordenCreada.Titulo}' ha sido creada exitosamente.");
                successDialog.ShowDialog();

                // Cerrar el diálogo
                DialogResult?.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al crear orden: {ex.Message}");
                var errorDialog = new WarningDialog(
                    "Error al crear orden", 
                    $"No se pudo crear la orden de conteo: {ex.Message}");
                errorDialog.ShowDialog();
            }
            finally
            {
                IsCargando = false;
                MensajeEstado = string.Empty;
            }
        }

        [RelayCommand]
        private async Task BuscarArticulo()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ArticuloBuscado))
                {
                    var warningDialog = new WarningDialog("Buscar artículo", "Introduce un código o descripción para buscar.");
                    warningDialog.ShowDialog();
                    return;
                }

                if (AlmacenSeleccionado == null)
                {
                    var warningDialog = new WarningDialog("Buscar artículo", "Primero selecciona un almacén.");
                    warningDialog.ShowDialog();
                    return;
                }

                ArticulosEncontrados.Clear();
                ArticuloSeleccionado = null;

                var empresa = SessionManager.EmpresaSeleccionada!.Value;
                var terminoBusqueda = ArticuloBuscado.Trim();
                
                List<StockDto> resultados = new();
                string tipoBusqueda = "";

                // Intentar buscar por código primero (si parece un código)
                if (terminoBusqueda.Length <= 20 && !terminoBusqueda.Contains(" "))
                {
                    tipoBusqueda = "código";
                    resultados = await _stockService.ObtenerPorArticuloAsync(
                        empresa,
                        codigoArticulo: terminoBusqueda,
                        codigoAlmacen: AlmacenSeleccionado.CodigoAlmacen
                    );
                }

                // Si no encuentra por código o el término parece una descripción, buscar por descripción
                if (!resultados.Any())
                {
                    tipoBusqueda = terminoBusqueda.Length <= 20 && !terminoBusqueda.Contains(" ") ? 
                        "código (sin resultados), luego descripción" : "descripción";
                    
                    resultados = await _stockService.ObtenerPorArticuloAsync(
                        empresa,
                        codigoArticulo: null,
                        codigoAlmacen: AlmacenSeleccionado.CodigoAlmacen,
                        descripcion: terminoBusqueda
                    );
                }

                // Agrupar por artículo
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
                    CodigoArticulo = ArticuloSeleccionado.CodigoArticulo;
                    var mensaje = $"✓ Encontrado por {tipoBusqueda}:\n{ArticuloSeleccionado.CodigoArticulo} - {ArticuloSeleccionado.DescripcionArticulo}";
                    var successDialog = new WarningDialog("Artículo encontrado", mensaje);
                    successDialog.ShowDialog();
                }
                else if (ArticulosEncontrados.Count > 1)
                {
                    var mensaje = $"Se encontraron {ArticulosEncontrados.Count} artículos por {tipoBusqueda}.\nSelecciona uno de la lista desplegable.";
                    var infoDialog = new WarningDialog("Múltiples resultados", mensaje);
                    infoDialog.ShowDialog();
                }
                else
                {
                    var mensaje = $"No se encontraron artículos buscando '{terminoBusqueda}' por {tipoBusqueda} en el almacén {AlmacenSeleccionado.CodigoAlmacen}.\n\n";
                    mensaje += "💡 Consejos:\n";
                    mensaje += "• Para buscar por código: introduce el código exacto (ej: 10000)\n";
                    mensaje += "• Para buscar por descripción: introduce parte de la descripción (ej: azúcar)\n";
                    mensaje += "• Verifica que el artículo tiene stock en este almacén";
                    
                    var warningDialog = new WarningDialog("Sin resultados", mensaje);
                    warningDialog.ShowDialog();
                }

                // Notificar cambios en visibilidad
                OnPropertyChanged(nameof(MostrarListaArticulos));
                OnPropertyChanged(nameof(MostrarInfoArticulo));
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog("Error", $"Error al buscar artículo: {ex.Message}");
                errorDialog.ShowDialog();
            }
        }

        [RelayCommand]
        private void Cancelar()
        {
            DialogResult?.Close();
        }
        #endregion

        #region Private Methods
        private async Task InitializeAsync()
        {
            try
            {
                await CargarAlmacenes();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en InitializeAsync: {ex.Message}");
            }
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

                AlmacenSeleccionado = AlmacenesDisponibles.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cargando almacenes: {ex.Message}");
                // En caso de error, agregar almacén por defecto
                AlmacenesDisponibles.Clear();
                var almacenDefecto = new AlmacenDto
                {
                    CodigoAlmacen = "01",
                    NombreAlmacen = "Almacén Principal",
                    CodigoEmpresa = (short)(SessionManager.EmpresaSeleccionada ?? 1)
                };
                AlmacenesDisponibles.Add(almacenDefecto);
                AlmacenSeleccionado = almacenDefecto;
            }
        }

        private string? GenerarFiltrosJson()
        {
            try
            {
                var filtros = new Dictionary<string, object>();

                // Agregar almacén si está seleccionado
                if (AlmacenSeleccionado != null)
                {
                    filtros["almacen"] = AlmacenSeleccionado.CodigoAlmacen;
                }

                // Agregar filtros específicos según el alcance
                switch (AlcanceSeleccionado)
                {
                    case "PASILLO":
                        if (!string.IsNullOrWhiteSpace(Pasillo))
                            filtros["pasillo"] = Pasillo.Trim();
                        break;

                    case "ESTANTERIA":
                        if (!string.IsNullOrWhiteSpace(Pasillo))
                            filtros["pasillo"] = Pasillo.Trim();
                        if (!string.IsNullOrWhiteSpace(Estanteria))
                            filtros["estanteria"] = Estanteria.Trim();
                        break;

                    case "UBICACION":
                        if (!string.IsNullOrWhiteSpace(Pasillo))
                            filtros["pasillo"] = Pasillo.Trim();
                        if (!string.IsNullOrWhiteSpace(Estanteria))
                            filtros["estanteria"] = Estanteria.Trim();
                        if (!string.IsNullOrWhiteSpace(Altura))
                            filtros["altura"] = Altura.Trim();
                        if (!string.IsNullOrWhiteSpace(Posicion))
                            filtros["posicion"] = Posicion.Trim();
                        break;

                    case "ARTICULO":
                        if (!string.IsNullOrWhiteSpace(CodigoArticulo))
                            filtros["articulo"] = CodigoArticulo.Trim();
                        break;
                }

                return filtros.Count > 0 ? JsonSerializer.Serialize(filtros) : null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error generando filtros JSON: {ex.Message}");
                return null;
            }
        }

        // Métodos de cambio de propiedades
        partial void OnAlcanceSeleccionadoChanged(string value)
        {
            OnPropertyChanged(nameof(MostrarFiltrosUbicacion));
            OnPropertyChanged(nameof(MostrarFiltroArticulo));
            
            // Limpiar filtros cuando cambia el alcance
            Pasillo = string.Empty;
            Estanteria = string.Empty;
            Altura = string.Empty;
            Posicion = string.Empty;
            CodigoArticulo = string.Empty;
            
            // Limpiar búsqueda de artículos
            ArticuloBuscado = string.Empty;
            ArticulosEncontrados.Clear();
            ArticuloSeleccionado = null;
            OnPropertyChanged(nameof(MostrarListaArticulos));
            OnPropertyChanged(nameof(MostrarInfoArticulo));
        }

        partial void OnTituloChanged(string value)
        {
            OnPropertyChanged(nameof(PuedeCrearOrden));
        }

        partial void OnIsCargandoChanged(bool value)
        {
            OnPropertyChanged(nameof(PuedeCrearOrden));
        }

        partial void OnArticuloSeleccionadoChanged(ArticuloResumenDto? value)
        {
            if (value != null)
            {
                CodigoArticulo = value.CodigoArticulo;
            }
            OnPropertyChanged(nameof(MostrarInfoArticulo));
        }

        partial void OnArticulosEncontradosChanged(ObservableCollection<ArticuloResumenDto> value)
        {
            OnPropertyChanged(nameof(MostrarListaArticulos));
        }

        partial void OnAlmacenSeleccionadoChanged(AlmacenDto? value)
        {
            // Limpiar búsqueda de artículos cuando cambia el almacén
            ArticuloBuscado = string.Empty;
            ArticulosEncontrados.Clear();
            ArticuloSeleccionado = null;
            CodigoArticulo = string.Empty;
            OnPropertyChanged(nameof(MostrarListaArticulos));
            OnPropertyChanged(nameof(MostrarInfoArticulo));
        }
        #endregion
    }

    // Clase auxiliar para prioridades
    public class PrioridadItem
    {
        public byte Valor { get; set; }
        public string Texto { get; set; } = string.Empty;
    }
} 