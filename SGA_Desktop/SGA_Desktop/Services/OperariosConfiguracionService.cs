using SGA_Desktop.Models;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace SGA_Desktop.Services
{
    /// <summary>
    /// Servicio para gestionar la configuración de operarios desde Aurora
    /// </summary>
    public class OperariosConfiguracionService : ApiService
    {
        private const string BASE_URL = "OperariosConfiguracion";

        /// <summary>
        /// Obtiene lista de operarios disponibles para seleccionar
        /// </summary>
        public async Task<List<OperarioDisponibleDto>?> ObtenerOperariosDisponiblesAsync(bool? soloActivos = true)
        {
            try
            {
                var url = $"{BASE_URL}/disponibles";
                if (soloActivos.HasValue)
                {
                    url += $"?soloActivos={soloActivos.Value.ToString().ToLower()}";
                }
                
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<List<OperarioDisponibleDto>>(content, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                }
                
                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener operarios disponibles: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Obtiene lista de todos los operarios
        /// </summary>
        public async Task<List<OperarioListaDto>?> ObtenerOperariosAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync(BASE_URL);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<List<OperarioListaDto>>(content, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                }
                
                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener operarios: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Obtiene configuración completa de un operario
        /// </summary>
        public async Task<OperarioConfiguracionDto?> ObtenerConfiguracionOperarioAsync(int operarioId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BASE_URL}/{operarioId}");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<OperarioConfiguracionDto>(content, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                }
                
                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener configuración del operario {operarioId}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Actualiza la configuración de un operario
        /// </summary>
        public async Task<ActualizarOperarioResult> ActualizarOperarioAsync(int operarioId, OperarioUpdateDto operarioUpdate)
        {
            try
            {
                var json = JsonSerializer.Serialize(operarioUpdate, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                // Crear un HttpClient con timeout más corto para evitar esperas largas
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(15); // 15 segundos máximo
                httpClient.BaseAddress = _httpClient.BaseAddress;
                
                // Copiar headers de autenticación si existen
                foreach (var header in _httpClient.DefaultRequestHeaders)
                {
                    httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
                
                var response = await httpClient.PutAsync($"{BASE_URL}/{operarioId}", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error del servidor: {response.StatusCode} - {errorContent}");
                }
                
                // Leer la respuesta para obtener información sobre los cambios
                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ActualizarOperarioResult>(responseContent, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                
                return result ?? new ActualizarOperarioResult { Success = true, HuboCambios = true };
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al actualizar operario {operarioId}: {ex.Message}", ex);
            }
        }


        /// <summary>
        /// Obtiene permisos disponibles
        /// </summary>
        public async Task<List<PermisoDisponibleDto>?> ObtenerPermisosDisponiblesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BASE_URL}/permisos-disponibles");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<List<PermisoDisponibleDto>>(content, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                }
                
                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener permisos disponibles: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Obtiene empresas disponibles
        /// </summary>
        public async Task<List<EmpresaConfiguracionDto>?> ObtenerEmpresasDisponiblesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BASE_URL}/empresas-disponibles");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<List<EmpresaConfiguracionDto>>(content, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                }
                
                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener empresas disponibles: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Obtiene almacenes disponibles por empresa
        /// </summary>
        public async Task<List<AlmacenConfiguracionDto>?> ObtenerAlmacenesDisponiblesAsync(short codigoEmpresa)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BASE_URL}/almacenes-disponibles/{codigoEmpresa}");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<List<AlmacenConfiguracionDto>>(content, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                }
                
                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener almacenes para empresa {codigoEmpresa}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Obtiene las empresas asignadas a un operario con información del operario
        /// </summary>
        public async Task<EmpresasOperarioResponseDto?> ObtenerEmpresasOperarioAsync(int operarioId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BASE_URL}/{operarioId}/empresas");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<EmpresasOperarioResponseDto>(content, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                }
                
                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener empresas del operario {operarioId}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Asigna una empresa a un operario
        /// </summary>
        public async Task<bool> AsignarEmpresaOperarioAsync(int operarioId, AsignarEmpresaDto empresaDto)
        {
            try
            {
                var json = JsonSerializer.Serialize(empresaDto, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{BASE_URL}/{operarioId}/empresas", content);
                
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al asignar empresa al operario {operarioId}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Elimina la asignación de una empresa a un operario
        /// </summary>
        public async Task<bool> EliminarEmpresaOperarioAsync(int operarioId, short codigoEmpresa, short empresaOrigen)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{BASE_URL}/{operarioId}/empresas/{codigoEmpresa}/{empresaOrigen}");
                
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al eliminar empresa del operario {operarioId}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Aplica una plantilla a un operario
        /// </summary>
        public async Task AplicarPlantillaAsync(object dto)
        {
            try
            {
                var json = JsonSerializer.Serialize(dto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{BASE_URL}/aplicar-plantilla", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error al aplicar plantilla: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al aplicar plantilla: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Da de baja a un operario estableciendo su FechaBaja
        /// </summary>
        public async Task<bool> DarDeBajaOperarioAsync(int operarioId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"{BASE_URL}/{operarioId}/dar-de-baja", null);
                
                System.Diagnostics.Debug.WriteLine($"Status Code: {response.StatusCode}");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Respuesta del API: {content}");
                    
                    // Verificar que la respuesta contenga "success": true
                    if (content.Contains("\"success\":true") || content.Contains("\"success\": true"))
                    {
                        System.Diagnostics.Debug.WriteLine("Operación exitosa detectada");
                        return true;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("No se encontró 'success': true en la respuesta");
                        return false;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Error HTTP: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al dar de baja operario {operarioId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Da de alta a un operario estableciendo su FechaBaja como null
        /// </summary>
        public async Task<bool> DarDeAltaOperarioAsync(int operarioId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"{BASE_URL}/{operarioId}/dar-de-alta", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al dar de alta operario {operarioId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Obtiene los roles SGA disponibles
        /// </summary>
        public async Task<List<RolSgaDto>?> ObtenerRolesSgaAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("rolessga/all");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    
                    var roles = JsonSerializer.Deserialize<List<RolSgaDto>>(content, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        PropertyNameCaseInsensitive = true
                    });
                    
                    return roles;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener roles SGA: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Obtiene el rol sugerido para un operario basado en sus permisos ERP
        /// </summary>
        public async Task<RolSugeridoDto?> ObtenerRolSugeridoAsync(int operarioId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/roles-sga/sugerido/{operarioId}");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<RolSugeridoDto>(content, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                }
                
                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener rol sugerido para operario {operarioId}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Asigna un rol SGA a un operario
        /// </summary>
        public async Task<bool> AsignarRolOperarioAsync(int operarioId, int rolId)
        {
            try
            {
                var json = JsonSerializer.Serialize(rolId, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync($"usuarios/{operarioId}/rol", content);
                
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al asignar rol al operario {operarioId}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Obtiene el rol asignado a un operario
        /// </summary>
        public async Task<int?> ObtenerRolOperarioAsync(int operarioId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/usuarios/{operarioId}/rol");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<Dictionary<string, object>>(content, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    
                    if (result != null && result.ContainsKey("rolId"))
                    {
                        return Convert.ToInt32(result["rolId"]);
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener rol del operario {operarioId}: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Resultado de la actualización de un operario
    /// </summary>
    public class ActualizarOperarioResult
    {
        public bool Success { get; set; }
        public bool HuboCambios { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
