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
        public async Task<List<OperarioDisponibleDto>?> ObtenerOperariosDisponiblesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BASE_URL}/disponibles");
                
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
        public async Task<bool> ActualizarOperarioAsync(int operarioId, OperarioUpdateDto operarioUpdate)
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
                
                return true;
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
    }
}
