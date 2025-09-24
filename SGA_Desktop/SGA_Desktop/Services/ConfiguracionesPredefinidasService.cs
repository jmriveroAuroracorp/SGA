using SGA_Desktop.Models;
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
                   
                   OperariosUsando = 0 // TODO: Implementar contador
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
        public async Task<bool> ActualizarConfiguracionPredefinidaAsync(int id, ConfiguracionPredefinidaCrearDto dto)
        {
            try
            {
                var json = JsonSerializer.Serialize(dto);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PutAsync($"{BASE_URL}/{id}", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error actualizando configuración predefinida {id}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Elimina una configuración predefinida
        /// </summary>
        public async Task<bool> EliminarConfiguracionPredefinidaAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{BASE_URL}/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error eliminando configuración predefinida {id}: {ex.Message}");
                return false;
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

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
