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
    }
}