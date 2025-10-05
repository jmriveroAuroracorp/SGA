using SGA_Desktop.Models;
using SGA_Desktop.Helpers;
using System.Text.Json;
using System.Text;
using System.Net.Http;

namespace SGA_Desktop.Services
{
    public class ConfiguracionesPredefinidasService : ApiService
    {
        private const string BASE_URL = "ConfiguracionesPredefinidas";

        /// <summary>
        /// Obtiene lista de configuraciones predefinidas
        /// </summary>
        public async Task<List<ConfiguracionPredefinidaDto>?> ObtenerConfiguracionesPredefinidasAsync()
        {
            try
            {
                var content = await GetStringAsync(BASE_URL);
                var configuracionesLista = JsonSerializer.Deserialize<List<ConfiguracionPredefinidaListaDto>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (configuracionesLista == null)
                    return null;

               // Mapear de ConfiguracionPredefinidaListaDto a ConfiguracionPredefinidaDto
               return configuracionesLista.Select(c => new ConfiguracionPredefinidaDto
               {
                   Id = c.Id,
                   Nombre = c.Nombre,
                   Descripcion = c.Descripcion,
                   FechaCreacion = c.FechaCreacion,
                   FechaModificacion = c.FechaModificacion,
                   Activa = c.Activa,
                   CantidadPermisos = c.CantidadPermisos,
                   CantidadEmpresas = c.CantidadEmpresas,
                   CantidadAlmacenes = c.CantidadAlmacenes,
                   
                   // Límites
                   LimiteEuros = c.LimiteEuros,
                   LimiteUnidades = c.LimiteUnidades,
                   
                   // Usuarios de auditoría
                   UsuarioCreacion = c.UsuarioCreacion,
                   UsuarioModificacion = c.UsuarioModificacion,
                   
                   OperariosUsando = c.OperariosUsando // Mapear el contador real del API
               }).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo configuraciones predefinidas: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Obtiene una configuración predefinida completa por ID
        /// </summary>
        public async Task<ConfiguracionPredefinidaCompletaDto?> ObtenerConfiguracionPredefinidaAsync(int id)
        {
            try
            {
                var content = await GetStringAsync($"{BASE_URL}/{id}");
                return JsonSerializer.Deserialize<ConfiguracionPredefinidaCompletaDto>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo configuración predefinida {id}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Crea una nueva configuración predefinida
        /// </summary>
        public async Task<ConfiguracionPredefinidaDto?> CrearConfiguracionPredefinidaAsync(ConfiguracionPredefinidaCrearDto dto)
        {
            try
            {
                var json = JsonSerializer.Serialize(dto);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{BASE_URL}", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<ConfiguracionPredefinidaDto>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creando configuración predefinida: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Actualiza una configuración predefinida
        /// </summary>
        public async Task<ActualizarConfiguracionResult> ActualizarConfiguracionPredefinidaAsync(int id, ConfiguracionPredefinidaCrearDto dto)
        {
            try
            {
                var json = JsonSerializer.Serialize(dto);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PutAsync($"{BASE_URL}/{id}", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    // Debug: Log de la respuesta de la API
                    System.Diagnostics.Debug.WriteLine($"=== DEBUG Desktop - Respuesta API ===");
                    System.Diagnostics.Debug.WriteLine($"Response Content: {responseContent}");
                    System.Diagnostics.Debug.WriteLine("=== FIN DEBUG ===");
                    
                    // Intentar deserializar como objeto con operariosAfectados
                    try
                    {
                        var result = JsonSerializer.Deserialize<ActualizarConfiguracionResult>(responseContent, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        
                        if (result != null)
                        {
                            // Debug: Log del resultado deserializado
                            System.Diagnostics.Debug.WriteLine($"=== DEBUG Desktop - Resultado deserializado ===");
                            System.Diagnostics.Debug.WriteLine($"Success: {result.Success}");
                            System.Diagnostics.Debug.WriteLine($"Message: {result.Message}");
                            System.Diagnostics.Debug.WriteLine($"OperariosAfectados Count: {result.OperariosAfectados?.Count ?? 0}");
                            if (result.OperariosAfectados != null)
                            {
                                foreach (var op in result.OperariosAfectados)
                                {
                                    System.Diagnostics.Debug.WriteLine($"  - OperarioId: {op.OperarioId}, OperarioNombre: '{op.OperarioNombre}'");
                                }
                            }
                            System.Diagnostics.Debug.WriteLine("=== FIN DEBUG ===");
                            
                            return result;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Debug: Log del error de deserialización
                        System.Diagnostics.Debug.WriteLine($"=== DEBUG Desktop - Error deserialización ===");
                        System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine("=== FIN DEBUG ===");
                    }
                    
                    // Si no se puede deserializar, asumir éxito
                    return new ActualizarConfiguracionResult { Success = true };
                }
                
                return new ActualizarConfiguracionResult { Success = false };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error actualizando configuración predefinida {id}: {ex.Message}");
                return new ActualizarConfiguracionResult { Success = false };
            }
        }


        /// <summary>
        /// Aplica una configuración predefinida a un operario
        /// </summary>
        public async Task<bool> AplicarConfiguracionPredefinidaAsync(int operarioId, int configuracionId, bool reemplazarExistente = false)
        {
            try
            {
                var dto = new AplicarConfiguracionPredefinidaDto
                {
                    OperarioId = operarioId,
                    ConfiguracionId = configuracionId,
                    ReemplazarExistente = reemplazarExistente
                };

                var json = JsonSerializer.Serialize(dto);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{BASE_URL}/aplicar", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error aplicando configuración predefinida: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Aplica una plantilla actualizada a operarios específicos
        /// </summary>
        public async Task<bool> AplicarPlantillaAOperariosAsync(int configuracionId, List<int> operarioIds)
        {
            try
            {
                var dto = new
                {
                    OperarioIds = operarioIds,
                    UsuarioAplicacion = SessionManager.UsuarioActual?.operario ?? 0
                };

                var json = JsonSerializer.Serialize(dto);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{BASE_URL}/{configuracionId}/aplicar-a-operarios", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error aplicando plantilla a operarios: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        /// <summary>
        /// Desasocia a un operario de su plantilla actual
        /// </summary>
        public async Task<bool> DesasociarPlantillaAsync(int operarioId)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{BASE_URL}/desasociar/{operarioId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error desasociando plantilla del operario {operarioId}: {ex.Message}");
                return false;
            }
        }


        /// <summary>
        /// Verifica si una configuración tiene operarios asociados
        /// </summary>
        public async Task<OperariosAsociadosResult?> VerificarOperariosAsociadosAsync(int configuracionId)
        {
            try
            {
                var content = await GetStringAsync($"{BASE_URL}/{configuracionId}/operarios-asociados");
                return JsonSerializer.Deserialize<OperariosAsociadosResult>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error verificando operarios asociados para configuración {configuracionId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Elimina una configuración predefinida
        /// </summary>
        public async Task<EliminarConfiguracionResult> EliminarConfiguracionPredefinidaAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{BASE_URL}/{id}");
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<EliminarConfiguracionResult>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new EliminarConfiguracionResult { Success = false };
                }
                
                return new EliminarConfiguracionResult { Success = false };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error eliminando configuración predefinida {id}: {ex.Message}");
                return new EliminarConfiguracionResult { Success = false };
            }
        }
    }

    /// <summary>
    /// Resultado de actualizar una configuración predefinida
    /// </summary>
    public class ActualizarConfiguracionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<OperarioAfectado> OperariosAfectados { get; set; } = new List<OperarioAfectado>();
    }

    /// <summary>
    /// Información de un operario afectado por una plantilla
    /// </summary>
    public class OperarioAfectado
    {
        public int OperarioId { get; set; }
        public string OperarioNombre { get; set; } = string.Empty;
        public string ConfiguracionPredefinidaNombre { get; set; } = string.Empty;
    }

    /// <summary>
    /// Resultado de verificar operarios asociados a una configuración
    /// </summary>
    public class OperariosAsociadosResult
    {
        public bool TieneOperariosAsociados { get; set; }
        public int CantidadOperarios { get; set; }
        public List<OperarioAsociado> Operarios { get; set; } = new List<OperarioAsociado>();
    }

    /// <summary>
    /// Información de un operario asociado a una configuración
    /// </summary>
    public class OperarioAsociado
    {
        public int OperarioId { get; set; }
        public string OperarioNombre { get; set; } = string.Empty;
        public string ConfiguracionPredefinidaNombre { get; set; } = string.Empty;
    }

    /// <summary>
    /// Resultado de eliminar una configuración predefinida
    /// </summary>
    public class EliminarConfiguracionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int OperariosDesasociados { get; set; }
        public List<OperarioAsociado> OperariosAfectados { get; set; } = new List<OperarioAsociado>();
    }
}
