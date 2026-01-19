using Newtonsoft.Json;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SGA_Desktop.Services
{
    public class ConteosService : ApiService
    {
        public ConteosService() : base()
        {
        }

        /// <summary>
        /// Crear una nueva orden de conteo
        /// </summary>
        public async Task<OrdenConteoDto> CrearOrdenAsync(CrearOrdenConteoDto dto)
        {
            try
            {
                var json = JsonConvert.SerializeObject(dto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("conteos/ordenes", content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var orden = JsonConvert.DeserializeObject<OrdenConteoDto>(responseContent);
                
                return orden ?? throw new Exception("Error al deserializar la respuesta");
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Error de comunicación con el servidor: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al crear la orden de conteo: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Actualizar una orden de conteo existente
        /// </summary>
        public async Task<OrdenConteoDto> ActualizarOrdenAsync(Guid guid, CrearOrdenConteoDto dto)
        {
            try
            {
                var json = JsonConvert.SerializeObject(dto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync($"conteos/ordenes/{guid}", content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var orden = JsonConvert.DeserializeObject<OrdenConteoDto>(responseContent);
                
                return orden ?? throw new Exception("Error al deserializar la respuesta");
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Error de comunicación con el servidor: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al actualizar la orden de conteo: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Obtener una orden de conteo por GUID
        /// </summary>
        public async Task<OrdenConteoDto?> ObtenerOrdenAsync(Guid guid)
        {
            try
            {
                var response = await _httpClient.GetAsync($"conteos/ordenes/{guid}");
                
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }

                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<OrdenConteoDto>(responseContent);
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Error de comunicación con el servidor: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener la orden de conteo: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Listar órdenes de conteo con filtros
        /// </summary>
        public async Task<List<OrdenConteoDto>> ListarOrdenesAsync(string? codigoOperario = null, string? estado = null)
        {
            try
            {
                var url = "conteos/ordenes";
                var queryParams = new List<string>();

                if (!string.IsNullOrEmpty(codigoOperario))
                    queryParams.Add($"codigoOperario={Uri.EscapeDataString(codigoOperario)}");

                if (!string.IsNullOrEmpty(estado))
                    queryParams.Add($"estado={Uri.EscapeDataString(estado)}");

                if (queryParams.Count > 0)
                    url += "?" + string.Join("&", queryParams);

               
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var ordenes = JsonConvert.DeserializeObject<List<OrdenConteoDto>>(responseContent);
                
                return ordenes ?? new List<OrdenConteoDto>();
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Error de comunicación con el servidor: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al listar las órdenes de conteo: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Listar todas las órdenes de conteo con filtros (para Desktop)
        /// </summary>
        public async Task<List<OrdenConteoDto>> ListarTodasLasOrdenesAsync(
            string? estado = null, 
            string? codigoOperario = null,
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null,
            string? codigoOperarioSesion = null,
            string? creadoPorCodigo = null)
        {
            try
            {
                var url = "conteos/ordenes/todas";
                var queryParams = new List<string>();

                if (!string.IsNullOrEmpty(estado))
                    queryParams.Add($"estado={Uri.EscapeDataString(estado)}");

                if (!string.IsNullOrEmpty(codigoOperario))
                    queryParams.Add($"codigoOperario={Uri.EscapeDataString(codigoOperario)}");
                
                if (!string.IsNullOrEmpty(codigoOperarioSesion))
                    queryParams.Add($"codigoOperarioSesion={Uri.EscapeDataString(codigoOperarioSesion)}");
                
                if (!string.IsNullOrEmpty(creadoPorCodigo))
                    queryParams.Add($"creadoPorCodigo={Uri.EscapeDataString(creadoPorCodigo)}");

                if (fechaDesde.HasValue)
                    queryParams.Add($"fechaDesde={fechaDesde.Value:yyyy-MM-dd}");

                if (fechaHasta.HasValue)
                    queryParams.Add($"fechaHasta={fechaHasta.Value:yyyy-MM-dd}");

                if (queryParams.Count > 0)
                    url += "?" + string.Join("&", queryParams);

                
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var ordenes = JsonConvert.DeserializeObject<List<OrdenConteoDto>>(responseContent);
                
                return ordenes ?? new List<OrdenConteoDto>();
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Error de comunicación con el servidor: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al listar todas las órdenes de conteo: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Cerrar una orden de conteo
        /// </summary>
        public async Task<bool> CerrarOrdenAsync(Guid guid)
        {
            try
            {
                var content = new StringContent("", Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"conteos/ordenes/{guid}/cerrar", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    
                    // Intentar deserializar como ProblemDetails para obtener el mensaje específico
                    try
                    {
                        var problemDetails = JsonConvert.DeserializeObject<dynamic>(errorContent);
                        if (problemDetails != null && problemDetails.detail != null)
                        {
                            throw new Exception(problemDetails.detail.ToString());
                        }
                    }
                    catch
                    {
                        // Si no se puede deserializar, usar el contenido completo
                    }
                    
                    throw new Exception($"Error del servidor ({(int)response.StatusCode}): {errorContent}");
                }

                return true;
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Error de comunicación con el servidor: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                // Si el mensaje ya contiene información del servidor, no agregar más contexto
                if (ex.Message.Contains("Error del servidor") || ex.Message.Contains("Error de comunicación"))
                {
                    throw;
                }
                throw new Exception($"Error al cerrar la orden de conteo: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Obtener resultados de conteo que requieren supervisión
        /// </summary>
        public async Task<List<ResultadoConteoDetalladoDto>> ObtenerResultadosSupervisionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("conteos/resultados?accion=SUPERVISION");
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var resultados = JsonConvert.DeserializeObject<List<ResultadoConteoDetalladoDto>>(responseContent);
                
                return resultados ?? new List<ResultadoConteoDetalladoDto>();
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Error de comunicación con el servidor: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener resultados de supervisión: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Obtener las líneas de conteo registradas (lecturas definitivas) de una orden específica
        /// </summary>
        public async Task<List<LecturaResponseDto>> ObtenerLineasConteoAsync(Guid ordenGuid, string? codigoOperario = null)
        {
            try
            {
                var url = $"conteos/ordenes/{ordenGuid}/lecturas-registradas";
                if (!string.IsNullOrEmpty(codigoOperario))
                {
                    url += $"?codigoOperario={Uri.EscapeDataString(codigoOperario)}";
                }

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var lineas = JsonConvert.DeserializeObject<List<LecturaResponseDto>>(responseContent);
                
                return lineas ?? new List<LecturaResponseDto>();
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Error de comunicación con el servidor: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener líneas de conteo: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Obtener todos los resultados de conteo con filtro opcional
        /// </summary>
        public async Task<List<ResultadoConteoDetalladoDto>> ObtenerResultadosConteoAsync(string? accion = null)
        {
            try
            {
                var url = "conteos/resultados";
                if (!string.IsNullOrEmpty(accion))
                {
                    url += $"?accion={Uri.EscapeDataString(accion)}";
                }

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var resultados = JsonConvert.DeserializeObject<List<ResultadoConteoDetalladoDto>>(responseContent);
                
                return resultados ?? new List<ResultadoConteoDetalladoDto>();
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Error de comunicación con el servidor: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener resultados de conteo: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Aprobar un resultado de conteo que requiere supervisión
        /// </summary>
        public async Task<ResultadoConteoDetalladoDto> AprobarResultadoAsync(Guid resultadoGuid, string aprobadoPorCodigo)
        {
            try
            {
                var dto = new ActualizarAprobadorDto
                {
                    AprobadoPorCodigo = aprobadoPorCodigo
                };

                var json = JsonConvert.SerializeObject(dto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"conteos/resultados/{resultadoGuid}/aprobador", content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var resultado = JsonConvert.DeserializeObject<ResultadoConteoDetalladoDto>(responseContent);
                
                return resultado ?? throw new Exception("Error al deserializar la respuesta");
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Error de comunicación con el servidor: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al aprobar resultado de conteo: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Reasignar una línea de conteo creando una nueva orden automáticamente
        /// </summary>
        public async Task<OrdenConteoDto> ReasignarLineaAsync(Guid resultadoGuid, string codigoOperario, string? comentario = null, string? supervisorCodigo = null)
        {
            try
            {
                var dto = new ReasignarLineaDto
                {
                    CodigoOperario = codigoOperario,
                    Comentario = comentario,
                    SupervisorCodigo = supervisorCodigo
                };

                var json = JsonConvert.SerializeObject(dto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"conteos/resultados/{resultadoGuid}/reasignar", content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var nuevaOrden = JsonConvert.DeserializeObject<OrdenConteoDto>(responseContent);
                
                return nuevaOrden ?? throw new Exception("Error al deserializar la respuesta");
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Error de comunicación con el servidor: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al reasignar línea de conteo: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Listar todos los conteos periódicos
        /// </summary>
        public async Task<List<ConteoPeriodicoDto>> ListarConteosPeriodicosAsync(
            string? codigoAlmacen = null,
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null,
            bool? activo = null,
            string? codigoOperario = null,
            string? codigoOperarioSesion = null,
            string? creadoPorCodigo = null)
        {
            try
            {
                var queryParams = new List<string>();
                
                if (!string.IsNullOrEmpty(codigoAlmacen) && codigoAlmacen != "Todas")
                {
                    queryParams.Add($"codigoAlmacen={Uri.EscapeDataString(codigoAlmacen)}");
                }
                
                if (fechaDesde.HasValue)
                {
                    queryParams.Add($"fechaDesde={Uri.EscapeDataString(fechaDesde.Value.ToString("yyyy-MM-dd"))}");
                }
                
                if (fechaHasta.HasValue)
                {
                    queryParams.Add($"fechaHasta={Uri.EscapeDataString(fechaHasta.Value.ToString("yyyy-MM-dd"))}");
                }
                
                if (activo.HasValue)
                {
                    queryParams.Add($"activo={activo.Value.ToString().ToLower()}");
                }
                
                if (!string.IsNullOrEmpty(codigoOperario))
                {
                    queryParams.Add($"codigoOperario={Uri.EscapeDataString(codigoOperario)}");
                }
                
                if (!string.IsNullOrEmpty(codigoOperarioSesion))
                {
                    queryParams.Add($"codigoOperarioSesion={Uri.EscapeDataString(codigoOperarioSesion)}");
                }
                
                if (!string.IsNullOrEmpty(creadoPorCodigo))
                {
                    queryParams.Add($"creadoPorCodigo={Uri.EscapeDataString(creadoPorCodigo)}");
                }
                
                var url = "conteos/periodicos";
                if (queryParams.Any())
                {
                    url += "?" + string.Join("&", queryParams);
                }
                
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var conteos = JsonConvert.DeserializeObject<List<ConteoPeriodicoDto>>(responseContent);
                
                return conteos ?? new List<ConteoPeriodicoDto>();
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Error de comunicación con el servidor: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al listar conteos periódicos: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Activar la periodicidad de un conteo
        /// </summary>
        public async Task ActivarPeriodicidadAsync(Guid guid)
        {
            try
            {
                var response = await _httpClient.PostAsync($"conteos/ordenes/{guid}/activar-periodicidad", null);
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Error de comunicación con el servidor: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al activar periodicidad: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Desactivar la periodicidad de un conteo
        /// </summary>
        public async Task DesactivarPeriodicidadAsync(Guid guid)
        {
            try
            {
                var response = await _httpClient.PostAsync($"conteos/ordenes/{guid}/desactivar-periodicidad", null);
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Error de comunicación con el servidor: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al desactivar periodicidad: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Obtener códigos de operarios que han creado conteos
        /// </summary>
        public async Task<List<string>> ObtenerCreadoresConteosAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("conteos/creadores");
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var creadores = JsonConvert.DeserializeObject<List<string>>(responseContent);
                
                return creadores ?? new List<string>();
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Error de comunicación con el servidor: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener creadores de conteos: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Obtener códigos de operarios que han creado conteos periódicos
        /// </summary>
        public async Task<List<string>> ObtenerCreadoresConteosPeriodicosAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("conteos/creadores-periodicos");
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var creadores = JsonConvert.DeserializeObject<List<string>>(responseContent);
                
                return creadores ?? new List<string>();
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Error de comunicación con el servidor: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener creadores de conteos periódicos: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Obtener el historial de renovaciones de un conteo periódico
        /// </summary>
        public async Task<List<OrdenConteoDto>> ObtenerRenovacionesAsync(Guid guid)
        {
            try
            {
                var response = await _httpClient.GetAsync($"conteos/ordenes/{guid}/renovaciones");
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var renovaciones = JsonConvert.DeserializeObject<List<OrdenConteoDto>>(responseContent);
                
                return renovaciones ?? new List<OrdenConteoDto>();
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Error de comunicación con el servidor: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener renovaciones: {ex.Message}", ex);
            }
        }
    }
} 