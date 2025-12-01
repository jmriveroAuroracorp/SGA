using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models.Calidad;
using SGA_Desktop.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace SGA_Desktop.ViewModels
{
    public partial class CalidadViewModel : ObservableObject
    {
        private readonly CalidadService calidadService;

        public CalidadViewModel()
        {
            this.calidadService = new CalidadService();

            // Inicializar propiedades
            CodigoEmpresa = SessionManager.EmpresaSeleccionada ?? 0;
            CargarBloqueos();
        }

        #region Propiedades de Búsqueda
        [ObservableProperty] private short codigoEmpresa;
        [ObservableProperty] private string codigoArticulo = string.Empty;
        [ObservableProperty] private string lotePartida = string.Empty;
        #endregion

        #region Propiedades de Resultados
        [ObservableProperty] private ObservableCollection<StockCalidadDto> stockDisponible = new();
        [ObservableProperty] private ObservableCollection<BloqueoCalidadDto> bloqueos = new();
        [ObservableProperty] private ObservableCollection<BloqueoCalidadDto> bloqueosFiltrados = new();
        [ObservableProperty] private StockCalidadDto? stockSeleccionado;
        [ObservableProperty] private BloqueoCalidadDto? bloqueoSeleccionado;
        #endregion

        #region Propiedades de Estado
        [ObservableProperty] private bool estaCargando = false;
        [ObservableProperty] private string mensajeEstado = string.Empty;
        [ObservableProperty] private bool mostrarSoloBloqueados = true;
        #endregion

        #region Propiedades de Filtros de Bloqueos
        [ObservableProperty] private string filtroCodigoArticulo = string.Empty;
        [ObservableProperty] private string filtroLotePartida = string.Empty;
        #endregion

        #region Propiedades de Pestañas
        [ObservableProperty] private bool mostrandoStock = true;
        [ObservableProperty] private bool mostrandoBloqueos = false;
        #endregion

        #region Propiedades de Bloqueo/Desbloqueo
        [ObservableProperty] private string comentarioBloqueo = string.Empty;
        [ObservableProperty] private string comentarioDesbloqueo = string.Empty;
        [ObservableProperty] private string tipoBloqueo = "TOTAL"; // "TOTAL" o "SOLO_PULMON"
        [ObservableProperty] private bool esBloqueoGlobal = false; // 🔷 NUEVO: Indica si es bloqueo en todas las ubicaciones
        [ObservableProperty] private bool esDesbloqueoGlobal = false; // 🔷 NUEVO: Indica si es desbloqueo en todas las ubicaciones
        #endregion

        #region Comandos
        [RelayCommand]
        private async Task BuscarStock()
        {
            if (!ValidarParametrosBusqueda())
                return;

            try
            {
                EstaCargando = true;
                MensajeEstado = "Buscando stock...";

                var filtros = new BuscarStockCalidadDto
                {
                    CodigoEmpresa = CodigoEmpresa,
                    CodigoArticulo = CodigoArticulo,
                    Partida = LotePartida
                };

                var resultado = await calidadService.BuscarStockAsync(filtros);

                StockDisponible.Clear();
                foreach (var item in resultado)
                {
                    // 🔷 SIMPLIFICADO: El servicio ya devuelve EstaBloqueado correctamente por ubicación
                    // Solo establecemos el Estado para la vista basándonos en EstaBloqueado
                    item.Estado = item.EstaBloqueado ? "Bloqueado" : "Disponible";
                    
                    StockDisponible.Add(item);
                }

                MensajeEstado = $"Encontrados {resultado.Count} registros de stock";
                System.Diagnostics.Debug.WriteLine($"Búsqueda de stock completada. Resultados: {resultado.Count}");
            }
            catch (Exception ex)
            {
                MensajeEstado = "Error al buscar stock";
                System.Diagnostics.Debug.WriteLine($"Error en búsqueda de stock: {ex.Message}");
                MessageBox.Show($"Error al buscar stock: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                EstaCargando = false;
            }
        }

        [RelayCommand]
        private async Task BloquearStock()
        {
            if (StockSeleccionado == null)
            {
                MessageBox.Show("Seleccione un stock para bloquear", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(ComentarioBloqueo))
            {
                MessageBox.Show("El comentario de bloqueo es obligatorio", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                EstaCargando = true;
                MensajeEstado = "Bloqueando stock...";

                var dto = new BloquearStockDto
                {
                    CodigoEmpresa = CodigoEmpresa,
                    CodigoArticulo = StockSeleccionado.CodigoArticulo,
                    LotePartida = StockSeleccionado.LotePartida,
                    CodigoAlmacen = StockSeleccionado.CodigoAlmacen,
                    Ubicacion = StockSeleccionado.Ubicacion,
                    ComentarioBloqueo = ComentarioBloqueo,
                    TipoBloqueo = TipoBloqueo, // 🔷 NUEVO: Tipo de bloqueo seleccionado
                    UsuarioId = SessionManager.UsuarioActual?.operario ?? 0,
                    EsBloqueoGlobal = EsBloqueoGlobal // 🔷 NUEVO: Bloqueo global
                };

                var resultado = await calidadService.BloquearStockAsync(dto);

                // Guardar información antes de limpiar
                var articuloBloqueado = StockSeleccionado.CodigoArticulo;
                var loteBloqueado = StockSeleccionado.LotePartida;

                // 🔷 NUEVO: Mensaje personalizado según tipo de bloqueo
                string mensajeExito;
                if (EsBloqueoGlobal)
                {
                    // Intentar obtener información del resultado si es bloqueo global
                    try
                    {
                        var jsonString = System.Text.Json.JsonSerializer.Serialize(resultado);
                        var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonString);
                        var root = jsonDoc.RootElement;

                        if (root.TryGetProperty("UbicacionesBloqueadas", out var ubicacionesProp))
                        {
                            var ubicacionesBloqueadas = ubicacionesProp.GetInt32();
                            var ubicacionesYaBloqueadas = root.TryGetProperty("UbicacionesYaBloqueadas", out var yaBloqueadasProp) 
                                ? yaBloqueadasProp.GetInt32() 
                                : 0;
                            
                            if (ubicacionesYaBloqueadas > 0)
                            {
                                mensajeExito = $"Bloqueo global aplicado exitosamente.\n{ubicacionesBloqueadas} ubicaciones bloqueadas.\n{ubicacionesYaBloqueadas} ubicaciones ya estaban bloqueadas.";
                            }
                            else
                            {
                                mensajeExito = $"Bloqueo global aplicado exitosamente.\n{ubicacionesBloqueadas} ubicaciones bloqueadas.";
                            }
                        }
                        else if (root.TryGetProperty("Mensaje", out var mensajeProp))
                        {
                            mensajeExito = mensajeProp.GetString() ?? "Bloqueo global aplicado exitosamente.";
                        }
                        else
                        {
                            mensajeExito = "Bloqueo global aplicado exitosamente en todas las ubicaciones.";
                        }
                    }
                    catch
                    {
                        mensajeExito = "Bloqueo global aplicado exitosamente en todas las ubicaciones.";
                    }
                }
                else
                {
                    mensajeExito = "Stock bloqueado exitosamente";
                }

                MensajeEstado = mensajeExito;
                ComentarioBloqueo = string.Empty;
                EsBloqueoGlobal = false; // Resetear después del bloqueo

                // Actualizar listas
                await CargarBloqueosInterno(false);
                await BuscarStock();

                MessageBox.Show(mensajeExito, "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                System.Diagnostics.Debug.WriteLine($"Stock bloqueado exitosamente para artículo {articuloBloqueado}, lote {loteBloqueado}");
            }
            catch (Exception ex)
            {
                MensajeEstado = "Error al bloquear stock";
                System.Diagnostics.Debug.WriteLine($"Error al bloquear stock: {ex.Message}");
                MessageBox.Show($"Error al bloquear stock: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                EstaCargando = false;
            }
        }

        [RelayCommand]
        private async Task DesbloquearStock()
        {
            if (BloqueoSeleccionado == null)
            {
                MessageBox.Show("Seleccione un bloqueo para desbloquear", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(ComentarioDesbloqueo))
            {
                MessageBox.Show("El comentario de desbloqueo es obligatorio", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 🔷 NUEVO: Mensaje de confirmación personalizado según tipo de desbloqueo
            string mensajeConfirmacion;
            if (EsDesbloqueoGlobal)
            {
                mensajeConfirmacion = $"¿Está seguro de que desea DESBLOQUEAR GLOBALMENTE el stock del artículo {BloqueoSeleccionado.CodigoArticulo} (lote {BloqueoSeleccionado.LotePartida})?\n\nEsto desbloqueará el artículo y lote en TODAS las ubicaciones donde esté bloqueado.";
            }
            else
            {
                mensajeConfirmacion = $"¿Está seguro de que desea desbloquear el stock del artículo {BloqueoSeleccionado.CodigoArticulo} (lote {BloqueoSeleccionado.LotePartida}) en la ubicación {BloqueoSeleccionado.CodigoAlmacen}-{BloqueoSeleccionado.Ubicacion ?? "(sin ubicación)"}?";
            }

            var resultado = MessageBox.Show(
                mensajeConfirmacion,
                "Confirmar Desbloqueo",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (resultado != MessageBoxResult.Yes)
                return;

            try
            {
                EstaCargando = true;
                MensajeEstado = "Desbloqueando stock...";

                var dto = new DesbloquearStockDto
                {
                    IdBloqueo = EsDesbloqueoGlobal ? null : BloqueoSeleccionado.Id, // 🔷 NUEVO: Null si es global
                    CodigoEmpresa = EsDesbloqueoGlobal ? CodigoEmpresa : null, // 🔷 NUEVO: Para desbloqueo global
                    CodigoArticulo = EsDesbloqueoGlobal ? BloqueoSeleccionado.CodigoArticulo : null, // 🔷 NUEVO
                    LotePartida = EsDesbloqueoGlobal ? BloqueoSeleccionado.LotePartida : null, // 🔷 NUEVO
                    ComentarioDesbloqueo = ComentarioDesbloqueo,
                    UsuarioId = SessionManager.UsuarioActual?.operario ?? 0,
                    EsDesbloqueoGlobal = EsDesbloqueoGlobal // 🔷 NUEVO
                };

                // Guardar información antes de limpiar
                var bloqueoId = BloqueoSeleccionado.Id;
                var articuloDesbloqueado = BloqueoSeleccionado.CodigoArticulo;
                var loteDesbloqueado = BloqueoSeleccionado.LotePartida;

                var resultadoDesbloqueo = await calidadService.DesbloquearStockAsync(dto);

                // 🔷 NUEVO: Mensaje personalizado según tipo de desbloqueo
                string mensajeExito;
                if (EsDesbloqueoGlobal)
                {
                    try
                    {
                        var jsonString = System.Text.Json.JsonSerializer.Serialize(resultadoDesbloqueo);
                        var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonString);
                        var root = jsonDoc.RootElement;

                        if (root.TryGetProperty("UbicacionesDesbloqueadas", out var ubicacionesProp))
                        {
                            var ubicacionesDesbloqueadas = ubicacionesProp.GetInt32();
                            mensajeExito = $"Desbloqueo global aplicado exitosamente.\n{ubicacionesDesbloqueadas} ubicaciones desbloqueadas.";
                        }
                        else if (root.TryGetProperty("Mensaje", out var mensajeProp))
                        {
                            mensajeExito = mensajeProp.GetString() ?? "Desbloqueo global aplicado exitosamente.";
                        }
                        else
                        {
                            mensajeExito = "Desbloqueo global aplicado exitosamente en todas las ubicaciones.";
                        }
                    }
                    catch
                    {
                        mensajeExito = "Desbloqueo global aplicado exitosamente en todas las ubicaciones.";
                    }
                }
                else
                {
                    mensajeExito = "Stock desbloqueado exitosamente";
                }

                MensajeEstado = mensajeExito;
                ComentarioDesbloqueo = string.Empty;
                EsDesbloqueoGlobal = false; // Resetear después del desbloqueo

                // Actualizar listas
                await CargarBloqueosInterno(false);

                MessageBox.Show(mensajeExito, "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                System.Diagnostics.Debug.WriteLine($"Stock desbloqueado exitosamente para bloqueo ID {bloqueoId}");
            }
            catch (Exception ex)
            {
                MensajeEstado = "Error al desbloquear stock";
                System.Diagnostics.Debug.WriteLine($"Error al desbloquear stock: {ex.Message}");
                MessageBox.Show($"Error al desbloquear stock: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                EstaCargando = false;
            }
        }

        [RelayCommand]
        private async Task CargarBloqueos()
        {
            await CargarBloqueosInterno();
        }

        [RelayCommand]
        private void FiltrarBloqueos()
        {
            AplicarFiltrosBloqueos();
        }

        [RelayCommand]
        private void LimpiarFiltrosBloqueos()
        {
            FiltroCodigoArticulo = string.Empty;
            FiltroLotePartida = string.Empty;
            AplicarFiltrosBloqueos();
        }

        private void AplicarFiltrosBloqueos()
        {
            BloqueosFiltrados.Clear();

            foreach (var bloqueo in Bloqueos)
            {
                // Aplicar filtros de búsqueda
                bool cumpleFiltros = true;

                if (!string.IsNullOrWhiteSpace(FiltroCodigoArticulo))
                {
                    cumpleFiltros &= bloqueo.CodigoArticulo.Contains(FiltroCodigoArticulo, StringComparison.OrdinalIgnoreCase);
                }

                if (!string.IsNullOrWhiteSpace(FiltroLotePartida))
                {
                    cumpleFiltros &= bloqueo.LotePartida.Contains(FiltroLotePartida, StringComparison.OrdinalIgnoreCase);
                }

                if (cumpleFiltros)
                {
                    BloqueosFiltrados.Add(bloqueo);
                }
            }
        }

        private async Task CargarBloqueosInterno(bool mostrarMensajes = true)
        {
            try
            {
                if (mostrarMensajes)
                {
                    EstaCargando = true;
                    MensajeEstado = "Cargando bloqueos...";
                }

                // Siempre traer todos los bloqueos (bloqueados y desbloqueados)
                var bloqueos = await calidadService.ObtenerBloqueosAsync(CodigoEmpresa, null);

                Bloqueos.Clear();
                foreach (var bloqueo in bloqueos)
                {
                    // Filtrar en el frontend según MostrarSoloBloqueados
                    if (MostrarSoloBloqueados && !bloqueo.Bloqueado)
                        continue; // Si solo queremos bloqueados, saltar los desbloqueados
                    
                    Bloqueos.Add(bloqueo);
                }

                // Aplicar filtros adicionales
                AplicarFiltrosBloqueos();

                if (mostrarMensajes)
                {
                    MensajeEstado = $"Cargados {bloqueos.Count} bloqueos";
                }
                System.Diagnostics.Debug.WriteLine($"Bloqueos cargados. Total: {bloqueos.Count}");
            }
            catch (Exception ex)
            {
                if (mostrarMensajes)
                {
                    MensajeEstado = "Error al cargar bloqueos";
                    MessageBox.Show($"Error al cargar bloqueos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                System.Diagnostics.Debug.WriteLine($"Error al cargar bloqueos: {ex.Message}");
            }
            finally
            {
                if (mostrarMensajes)
                {
                    EstaCargando = false;
                }
            }
        }

        [RelayCommand]
        private void LimpiarBusqueda()
        {
            CodigoArticulo = string.Empty;
            LotePartida = string.Empty;
            StockDisponible.Clear();
            StockSeleccionado = null;
            MensajeEstado = "Búsqueda limpiada";
        }

        [RelayCommand]
        private void LimpiarComentarios()
        {
            ComentarioBloqueo = string.Empty;
            ComentarioDesbloqueo = string.Empty;
        }
        #endregion

        #region Métodos Privados
        private bool ValidarParametrosBusqueda()
        {
            if (CodigoEmpresa <= 0)
            {
                MessageBox.Show("Código de empresa es obligatorio", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(CodigoArticulo))
            {
                MessageBox.Show("Código de artículo es obligatorio", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(LotePartida))
            {
                MessageBox.Show("Lote/partida es obligatorio", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        [RelayCommand]
        private void CambiarAStock()
        {
            MostrandoStock = true;
            MostrandoBloqueos = false;
        }

        [RelayCommand]
        private void CambiarABloqueos()
        {
            MostrandoStock = false;
            MostrandoBloqueos = true;
        }
        #endregion
    }
}
