using SGA_Desktop.Models.Calidad;
using System.Text.Json;
using SGA_Desktop.Helpers;
using System.Net.Http.Headers;

namespace SGA_Desktop.Services
{
    public class CalidadService : ApiService
    {
        public CalidadService() : base()
        {
        }

        private void ActualizarToken()
        {
            if (!string.IsNullOrWhiteSpace(SessionManager.Token))
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SessionManager.Token);
        }

        public async Task<object> BloquearStockAsync(BloquearStockDto dto)
        {
            try
            {
                var json = await PostAsync("Calidad/bloquear-stock", dto);
                return JsonSerializer.Deserialize<object>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al bloquear stock: {ex.Message}");
                throw;
            }
        }

        public async Task<object> DesbloquearStockAsync(DesbloquearStockDto dto)
        {
            try
            {
                var json = await PostAsync("Calidad/desbloquear-stock", dto);
                return JsonSerializer.Deserialize<object>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al desbloquear stock: {ex.Message}");
                throw;
            }
        }

        public async Task<List<BloqueoCalidadDto>> ObtenerBloqueosAsync(short codigoEmpresa, bool? soloBloqueados)
        {
            try
            {
                var queryParams = new List<string>();
                queryParams.Add($"codigoEmpresa={codigoEmpresa}");
                
                if (soloBloqueados.HasValue)
                    queryParams.Add($"soloBloqueados={soloBloqueados.Value}");

                var url = $"Calidad/bloqueos?{string.Join("&", queryParams)}";
                var json = await GetStringAsync(url);
                var resultado = JsonSerializer.Deserialize<List<BloqueoCalidadDto>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return resultado ?? new List<BloqueoCalidadDto>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al obtener bloqueos: {ex.Message}");
                throw;
            }
        }

        public async Task<EstadisticasCalidadDto> ObtenerEstadisticasAsync(short codigoEmpresa)
        {
            try
            {
                ActualizarToken();
                var url = $"Calidad/estadisticas?codigoEmpresa={codigoEmpresa}";
                System.Diagnostics.Debug.WriteLine($"[CalidadService] Llamando a: {url}");
                
                var json = await GetStringAsync(url);
                if (json != null && json.Length > 0)
                {
                    var preview = json.Length > 200 ? json.Substring(0, 200) : json;
                    System.Diagnostics.Debug.WriteLine($"[CalidadService] Respuesta recibida: {preview}...");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[CalidadService] Respuesta recibida: (vacía o null)");
                }
                
                var resultado = JsonSerializer.Deserialize<EstadisticasCalidadDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (resultado == null)
                {
                    System.Diagnostics.Debug.WriteLine("[CalidadService] WARNING: resultado es null, retornando DTO vacío");
                    return new EstadisticasCalidadDto();
                }

                System.Diagnostics.Debug.WriteLine($"[CalidadService] Estadísticas deserializadas - Bloqueados: {resultado.TotalBloqueados}");
                return resultado;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CalidadService] Error al obtener estadísticas: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[CalidadService] StackTrace: {ex.StackTrace}");
                throw;
            }
        }
    }
}