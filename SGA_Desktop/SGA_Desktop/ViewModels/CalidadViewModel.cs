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
        [ObservableProperty] private string? codigoAlmacen;
        [ObservableProperty] private string? codigoUbicacion;
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
        [ObservableProperty] private string filtroAlmacen = string.Empty;
        #endregion

        #region Propiedades de Pestañas
        [ObservableProperty] private bool mostrandoStock = true;
        [ObservableProperty] private bool mostrandoBloqueos = false;
        #endregion

        #region Propiedades de Bloqueo/Desbloqueo
        [ObservableProperty] private string comentarioBloqueo = string.Empty;
        [ObservableProperty] private string comentarioDesbloqueo = string.Empty;
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

                // Cargar bloqueos primero para poder comparar correctamente
                await CargarBloqueosInterno(false);

                var filtros = new BuscarStockCalidadDto
                {
                    CodigoEmpresa = CodigoEmpresa,
                    CodigoArticulo = CodigoArticulo,
                    Partida = LotePartida,
                    CodigoAlmacen = CodigoAlmacen,
                    CodigoUbicacion = CodigoUbicacion
                };

                var resultado = await calidadService.BuscarStockAsync(filtros);

                StockDisponible.Clear();
                foreach (var item in resultado)
                {
                    // Verificar si este registro específico está bloqueado
                    // comparando con los bloqueos existentes por ubicación específica
                    bool estaBloqueadoEspecifico = Bloqueos.Any(b => 
                        b.CodigoArticulo == item.CodigoArticulo &&
                        b.LotePartida == item.LotePartida &&
                        b.Almacen == item.Almacen &&
                        b.Ubicacion == item.Ubicacion &&
                        b.Bloqueado);

                    if (estaBloqueadoEspecifico)
                    {
                        item.Estado = "Bloqueado";
                        item.EstaBloqueado = true;
                        // Obtener información del bloqueo
                        var bloqueo = Bloqueos.FirstOrDefault(b => 
                            b.CodigoArticulo == item.CodigoArticulo &&
                            b.LotePartida == item.LotePartida &&
                            b.Almacen == item.Almacen &&
                            b.Ubicacion == item.Ubicacion &&
                            b.Bloqueado);
                        
                        if (bloqueo != null)
                        {
                            item.ComentarioBloqueo = bloqueo.ComentarioBloqueo;
                            item.FechaBloqueo = bloqueo.FechaBloqueo;
                            item.UsuarioBloqueo = bloqueo.UsuarioBloqueo;
                        }
                    }
                    else
                    {
                        item.Estado = "Disponible";
                        item.EstaBloqueado = false;
                    }
                    
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
                    UsuarioId = SessionManager.UsuarioActual?.operario ?? 0
                };

                var resultado = await calidadService.BloquearStockAsync(dto);

                // Guardar información antes de limpiar
                var articuloBloqueado = StockSeleccionado.CodigoArticulo;
                var loteBloqueado = StockSeleccionado.LotePartida;

                MensajeEstado = "Stock bloqueado exitosamente";
                ComentarioBloqueo = string.Empty;

                // Actualizar listas
                await CargarBloqueosInterno(false);
                await BuscarStock();

                MessageBox.Show("Stock bloqueado exitosamente", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
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

            var resultado = MessageBox.Show(
                $"¿Está seguro de que desea desbloquear el stock del artículo {BloqueoSeleccionado.CodigoArticulo} (lote {BloqueoSeleccionado.LotePartida})?",
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
                    IdBloqueo = BloqueoSeleccionado.Id,
                    ComentarioDesbloqueo = ComentarioDesbloqueo,
                    UsuarioId = SessionManager.UsuarioActual?.operario ?? 0
                };

                // Guardar información antes de limpiar
                var bloqueoId = BloqueoSeleccionado.Id;

                await calidadService.DesbloquearStockAsync(dto);

                MensajeEstado = "Stock desbloqueado exitosamente";
                ComentarioDesbloqueo = string.Empty;

                // Actualizar listas
                await CargarBloqueosInterno(false);

                MessageBox.Show("Stock desbloqueado exitosamente", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
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
            FiltroAlmacen = string.Empty;
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

                if (!string.IsNullOrWhiteSpace(FiltroAlmacen))
                {
                    cumpleFiltros &= bloqueo.Almacen.Contains(FiltroAlmacen, StringComparison.OrdinalIgnoreCase);
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
            CodigoAlmacen = null;
            CodigoUbicacion = null;
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
