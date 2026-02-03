using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using SGA_Desktop.ViewModels;

namespace SGA_Desktop.Services
{
    public class InventarioService : ApiService
    {
        /// <summary>
        /// Obtiene el stock actual del sistema para un artículo en una ubicación específica
        /// </summary>
        public async Task<List<StockDto>> ObtenerStockSistemaAsync(
            int codigoEmpresa,
            string codigoAlmacen,
            string? ubicacion = null,
            string? codigoArticulo = null)
        {
            try
            {
                var qs = $"?codigoEmpresa={codigoEmpresa}&codigoAlmacen={Uri.EscapeDataString(codigoAlmacen)}";
                
                if (!string.IsNullOrWhiteSpace(ubicacion))
                    qs += $"&codigoUbicacion={Uri.EscapeDataString(ubicacion)}";
                
                if (!string.IsNullOrWhiteSpace(codigoArticulo))
                    qs += $"&codigoArticulo={Uri.EscapeDataString(codigoArticulo)}";

                return await GetAsync<List<StockDto>>($"Stock/ubicacion{qs}");
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return new List<StockDto>();
            }
        }

        /// <summary>
        /// Obtiene el stock de ubicaciones para el grid de inventario
        /// </summary>
        public async Task<List<StockUbicacionDto>> ObtenerStockUbicacionesAsync(
            int codigoEmpresa,
            string codigoAlmacen,
            int? pasilloDesde = null,
            int? pasilloHasta = null,
            int? estanteriaDesde = null,
            int? estanteriaHasta = null,
            int? alturaDesde = null,
            int? alturaHasta = null,
            int? posicionDesde = null,
            int? posicionHasta = null)
        {
            try
            {
                var qs = $"?codigoEmpresa={codigoEmpresa}&codigoAlmacen={Uri.EscapeDataString(codigoAlmacen)}";
                
                if (pasilloDesde.HasValue) qs += $"&pasilloDesde={pasilloDesde.Value}";
                if (pasilloHasta.HasValue) qs += $"&pasilloHasta={pasilloHasta.Value}";
                if (estanteriaDesde.HasValue) qs += $"&estanteriaDesde={estanteriaDesde.Value}";
                if (estanteriaHasta.HasValue) qs += $"&estanteriaHasta={estanteriaHasta.Value}";
                if (alturaDesde.HasValue) qs += $"&alturaDesde={alturaDesde.Value}";
                if (alturaHasta.HasValue) qs += $"&alturaHasta={alturaHasta.Value}";
                if (posicionDesde.HasValue) qs += $"&posicionDesde={posicionDesde.Value}";
                if (posicionHasta.HasValue) qs += $"&posicionHasta={posicionHasta.Value}";

                return await GetAsync<List<StockUbicacionDto>>($"Inventario/stock-ubicaciones{qs}");
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return new List<StockUbicacionDto>();
            }
        }



        /// <summary>
        /// Obtiene los rangos disponibles de ubicaciones en un almacén
        /// </summary>
        public async Task<RangosDisponiblesDto> ObtenerRangosDisponiblesAsync(int codigoEmpresa, string codigoAlmacen)
        {
            try
            {
                var qs = $"?codigoEmpresa={codigoEmpresa}&codigoAlmacen={Uri.EscapeDataString(codigoAlmacen)}";
                return await GetAsync<RangosDisponiblesDto>($"Inventario/rangos-disponibles{qs}");
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return new RangosDisponiblesDto();
            }
        }

        /// <summary>
        /// Obtiene los registros de inventario existentes
        /// </summary>
        public async Task<List<InventarioCabeceraDto>> ObtenerInventariosAsync(FiltroInventarioDto filtro)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("Inventario/consultar", filtro);
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<InventarioCabeceraDto>>(json) ?? new List<InventarioCabeceraDto>();
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return new List<InventarioCabeceraDto>();
            }
        }

        /// <summary>
        /// Obtiene todos los registros de inventario existentes (sin filtro)
        /// </summary>
        public async Task<List<InventarioCabeceraDto>> ObtenerInventariosAsync()
        {
            try
            {
                var filtro = new FiltroInventarioDto
                {
                    CodigoEmpresa = SessionManager.EmpresaSeleccionada!.Value,
                    FechaDesde = DateTime.Today.AddDays(-365), // Último año
                    FechaHasta = DateTime.Today.AddDays(1)
                };
                
                return await ObtenerInventariosAsync(filtro);
            }
            catch (Exception ex)
            {
                // En servicios es mejor lanzar la excepción para que el ViewModel la maneje
                throw new Exception($"Error al obtener inventarios: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Obtiene un inventario por su ID
        /// </summary>
        public async Task<InventarioCabeceraDto?> ObtenerInventarioPorIdAsync(Guid idInventario)
        {
            try
            {
                var filtro = new FiltroInventarioDto
                {
                    CodigoEmpresa = SessionManager.EmpresaSeleccionada!.Value,
                    FechaDesde = DateTime.Today.AddDays(-365), // Último año
                    FechaHasta = DateTime.Today.AddDays(1)
                };
                
                var inventarios = await ObtenerInventariosAsync(filtro);
                return inventarios.FirstOrDefault(i => i.IdInventario == idInventario);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Crea un nuevo registro de inventario
        /// </summary>
        public async Task<CrearInventarioResponseDto?> CrearInventarioAsync(CrearInventarioDto inventario)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("Inventario/crear", inventario);
                
                if (!response.IsSuccessStatusCode)
                {
                    // Capturar el mensaje específico del error del backend
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorMessage = !string.IsNullOrEmpty(errorContent) ? errorContent : response.ReasonPhrase;
                    
                    // Si el error viene como string JSON, intentar extraer el mensaje
                    if (errorContent.StartsWith("\"") && errorContent.EndsWith("\""))
                    {
                        try
                        {
                            errorMessage = JsonConvert.DeserializeObject<string>(errorContent) ?? errorMessage;
                        }
                        catch
                        {
                            // Si no se puede deserializar, usar el contenido tal cual
                        }
                    }
                    
                    // Lanzar excepción con el mensaje del servidor tal cual (sin prefijos)
                    throw new Exception(errorMessage);
                }
                
                var json = await response.Content.ReadAsStringAsync();
                var resultado = JsonConvert.DeserializeObject<CrearInventarioResponseDto>(json);
                return resultado;
            }
            catch (Exception ex) when (ex.Message.Contains("Inventarios abiertos") || 
                                       ex.Message.Contains("solapamiento") ||
                                       ex.Message.Contains("No se puede crear"))
            {
                // Si el mensaje ya viene del servidor con información descriptiva, re-lanzarlo tal cual
                throw;
            }
            catch (Exception ex)
            {
                // Para otros errores, agregar prefijo genérico
                throw new Exception($"Error al crear inventario: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Registra un conteo de inventario (línea temporal)
        /// </summary>
        public async Task<bool> ContarInventarioAsync(ContarInventarioDto conteo)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("Inventario/contar", conteo);
                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al registrar conteo: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Consolida las líneas temporales de un inventario de forma inteligente
        /// </summary>
        public async Task<(bool success, bool tieneAdvertencias, List<object> lineasConStockCambiado)> ConsolidarInventarioAsync(Guid idInventario)
        {
            try
            {
                var usuarioId = SessionManager.UsuarioActual?.operario ?? 0;
                var response = await _httpClient.PostAsync($"Inventario/consolidar-inteligente/{idInventario}?usuarioValidacionId={usuarioId}", null);
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                var resultado = JsonConvert.DeserializeObject<dynamic>(json);
                
                bool tieneAdvertencias = resultado?.tieneAdvertencias ?? false;
                var lineasConStockCambiado = new List<object>();
                
                if (tieneAdvertencias && resultado?.lineasConStockCambiado != null)
                {
                    lineasConStockCambiado = resultado.lineasConStockCambiado.ToObject<List<object>>();
                }
                
                return (true, tieneAdvertencias, lineasConStockCambiado);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al consolidar inventario: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Verifica si hay advertencias de consolidación sin consolidar el inventario
        /// </summary>
        public async Task<(bool success, bool tieneAdvertencias, List<object> lineasConStockCambiado)> VerificarAdvertenciasConsolidacionAsync(Guid idInventario)
        {
            try
            {
                var response = await _httpClient.GetAsync($"Inventario/verificar-advertencias/{idInventario}");
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                var resultado = JsonConvert.DeserializeObject<dynamic>(json);
                
                bool tieneAdvertencias = resultado?.tieneAdvertencias ?? false;
                var lineasConStockCambiado = new List<object>();
                
                if (tieneAdvertencias && resultado?.lineasConStockCambiado != null)
                {
                    lineasConStockCambiado = resultado.lineasConStockCambiado.ToObject<List<object>>();
                }
                
                return (true, tieneAdvertencias, lineasConStockCambiado);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al verificar advertencias: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Cierra un inventario y genera ajustes
        /// </summary>
        public async Task<bool> CerrarInventarioAsync(Guid idInventario)
        {
            try
            {
                var response = await _httpClient.PostAsync($"Inventario/cerrar/{idInventario}", null);
                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al cerrar inventario: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Obtiene las líneas de un inventario
        /// </summary>
        public async Task<List<LineaInventarioDto>> ObtenerLineasInventarioAsync(Guid idInventario)
        {
            try
            {
                return await GetAsync<List<LineaInventarioDto>>($"Inventario/lineas/{idInventario}");
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return new List<LineaInventarioDto>();
            }
        }

        /// <summary>
        /// Obtiene ajustes filtrados para el historial
        /// </summary>
        public async Task<List<AjusteDto>> ObtenerAjustesFiltradosAsync(
            int? codigoEmpresa = null,
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null,
            string? codigoArticulo = null,
            string? codigoAlmacen = null,
            string? codigoUbicacion = null,
            string? estado = null,
            int? usuarioId = null,
            string? partida = null,
            string? codigoPalet = null,
            int? limite = null)
        {
            try
            {
                var query = new List<string>();
                if (codigoEmpresa.HasValue)
                    query.Add($"codigoEmpresa={codigoEmpresa.Value}");

                if (fechaDesde.HasValue)
                    query.Add($"fechaDesde={fechaDesde.Value:yyyy-MM-dd}");
                if (fechaHasta.HasValue)
                    query.Add($"fechaHasta={fechaHasta.Value:yyyy-MM-dd}");
                if (!string.IsNullOrWhiteSpace(codigoArticulo))
                    query.Add($"codigoArticulo={Uri.EscapeDataString(codigoArticulo)}");
                if (!string.IsNullOrWhiteSpace(codigoAlmacen))
                    query.Add($"codigoAlmacen={Uri.EscapeDataString(codigoAlmacen)}");
                if (!string.IsNullOrWhiteSpace(codigoUbicacion))
                    query.Add($"codigoUbicacion={Uri.EscapeDataString(codigoUbicacion)}");
                if (!string.IsNullOrWhiteSpace(estado))
                    query.Add($"estado={Uri.EscapeDataString(estado)}");
                if (usuarioId.HasValue && usuarioId.Value > 0)
                    query.Add($"usuarioId={usuarioId.Value}");
                if (!string.IsNullOrWhiteSpace(partida))
                    query.Add($"partida={Uri.EscapeDataString(partida)}");
                if (!string.IsNullOrWhiteSpace(codigoPalet))
                    query.Add($"codigoPalet={Uri.EscapeDataString(codigoPalet)}");
                if (limite.HasValue)
                    query.Add($"limite={limite.Value}");

                var url = "Inventario/ajustes";
                if (query.Count > 0)
                    url += "?" + string.Join("&", query);

                return await GetAsync<List<AjusteDto>>(url);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return new List<AjusteDto>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener ajustes filtrados: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Procesa un ajuste de inventario
        /// </summary>
        public async Task<bool> ProcesarAjusteAsync(Guid idAjuste)
        {
            try
            {
                var response = await _httpClient.PostAsync($"Inventario/procesar-ajuste/{idAjuste}", null);
                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al procesar ajuste: {ex.Message}");
            }
        }

        /// <summary>
        /// Rechaza un ajuste de inventario
        /// </summary>
        public async Task<bool> RechazarAjusteAsync(Guid idAjuste, string motivo)
        {
            try
            {
                var request = new { motivo };
                var response = await _httpClient.PostAsJsonAsync($"Inventario/rechazar-ajuste/{idAjuste}", request);
                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al rechazar ajuste: {ex.Message}");
            }
        }

        /// <summary>
        /// Guarda el conteo de un inventario
        /// </summary>
        public async Task<bool> GuardarConteoInventarioAsync(GuardarConteoInventarioDto conteo)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("Inventario/guardar-conteo", conteo);
                
                if (!response.IsSuccessStatusCode)
                {
                    // Capturar el mensaje específico del error del backend
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorMessage = !string.IsNullOrEmpty(errorContent) ? errorContent : response.ReasonPhrase;
                    throw new Exception($"Error al guardar conteo: {errorMessage}");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al guardar conteo: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Obtiene las líneas temporales de un inventario
        /// </summary>
        public async Task<List<LineaTemporalInventarioDto>> ObtenerLineasTemporalesAsync(Guid idInventario)
        {
            try
            {
                return await GetAsync<List<LineaTemporalInventarioDto>>($"Inventario/lineas-temporales/{idInventario}");
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return new List<LineaTemporalInventarioDto>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener líneas temporales: {ex.Message}", ex);
            }
        }



        /// <summary>
        /// Obtiene las líneas problemáticas de un inventario (con stock cambiado)
        /// </summary>
        public async Task<List<Models.LineaProblematicaDto>> ObtenerLineasProblematicasAsync(Guid idInventario)
        {
            try
            {
                return await GetAsync<List<Models.LineaProblematicaDto>>($"Inventario/lineas-problematicas/{idInventario}");
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return new List<Models.LineaProblematicaDto>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener líneas problemáticas: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Guarda el reconteo de líneas problemáticas
        /// </summary>
        public async Task<bool> GuardarReconteoAsync(GuardarReconteoDto reconteo)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("Inventario/guardar-reconteo", reconteo);
                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al guardar reconteo: {ex.Message}", ex);
            }
        }



        /// <summary>
        /// Valida que una ubicación pertenece a un almacén específico
        /// </summary>
        public async Task<bool> ValidarUbicacionAlmacenAsync(string codigoAlmacen, string ubicacion)
        {
            try
            {
                var empresa = SessionManager.EmpresaSeleccionada ?? 1;
                var qs = $"?codigoEmpresa={empresa}&codigoAlmacen={Uri.EscapeDataString(codigoAlmacen)}&ubicacion={Uri.EscapeDataString(ubicacion)}";
                var response = await _httpClient.GetAsync($"ubicaciones/validar-almacen{qs}");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<ValidarUbicacionAlmacenResponse>(json);
                    return result?.Valido ?? false;
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Helper genérico para GET + deserializar JSON
        /// </summary>
        private async Task<T> GetAsync<T>(string relativeUrl)
        {
            var response = await _httpClient.GetAsync(relativeUrl);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<T>(json)!;
        }

        /// <summary>
        /// Cambia el código de artículo o fecha de caducidad mediante ajustes de inventario
        /// </summary>
        public async Task<bool> CambiarArticuloAsync(CambioArticuloDto dto)
        {
            try
            {
                var json = await PostAsync("Inventario/cambiar-articulo", dto);
                return !string.IsNullOrWhiteSpace(json);
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Error al cambiar artículo: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// DTO para respuesta de validación de ubicación-almacén
    /// </summary>
    public class ValidarUbicacionAlmacenResponse
    {
        public bool Valido { get; set; }
        public string? Mensaje { get; set; }
    }
} 