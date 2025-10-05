using SGA_Desktop.Models;
using System.Text.Json;
using System.Text;
using System.Net.Http;

namespace SGA_Desktop.Services
{
    public class OrdenTraspasoService : ApiService
    {
        public async Task<IEnumerable<OrdenTraspasoDto>> GetOrdenesTraspasoAsync(short? codigoEmpresa = null, string? estado = null)
        {
            var queryParams = new List<string>();
            
            if (codigoEmpresa.HasValue)
                queryParams.Add($"codigoEmpresa={codigoEmpresa.Value}");
            
            if (!string.IsNullOrEmpty(estado))
                queryParams.Add($"estado={estado}");

            var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
            return await GetAsync<IEnumerable<OrdenTraspasoDto>>($"OrdenTraspaso{queryString}");
        }

        public async Task<OrdenTraspasoDto> GetOrdenTraspasoAsync(Guid id)
        {
            return await GetAsync<OrdenTraspasoDto>($"OrdenTraspaso/{id}");
        }

        public async Task<OrdenTraspasoDto> CrearOrdenTraspasoAsync(CrearOrdenTraspasoDto dto)
        {
            var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var response = await _httpClient.PostAsync("OrdenTraspaso", new StringContent(json, Encoding.UTF8, "application/json"));
            
            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<OrdenTraspasoDto>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            
            throw new Exception($"Error al crear orden de traspaso: {response.StatusCode}");
        }

        public async Task<bool> CompletarOrdenTraspasoAsync(Guid id)
        {
            var response = await _httpClient.PostAsync($"OrdenTraspaso/{id}/completar", new StringContent("", Encoding.UTF8, "application/json"));
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> CancelarOrdenTraspasoAsync(Guid id)
        {
            var response = await _httpClient.PostAsync($"OrdenTraspaso/{id}/cancelar", new StringContent("", Encoding.UTF8, "application/json"));
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> CancelarLineasPendientesAsync(Guid id)
        {
            var response = await _httpClient.PostAsync($"OrdenTraspaso/{id}/cancelar-lineas-pendientes", new StringContent("", Encoding.UTF8, "application/json"));
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> EliminarOrdenTraspasoAsync(Guid id)
        {
            var response = await _httpClient.DeleteAsync($"OrdenTraspaso/{id}");
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> ActualizarLineaOrdenTraspasoAsync(Guid id, ActualizarLineaOrdenTraspasoDto dto)
        {
            var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var response = await _httpClient.PutAsync($"OrdenTraspaso/linea/{id}", new StringContent(json, Encoding.UTF8, "application/json"));
            return response.IsSuccessStatusCode;
        }

        public async Task<LineaOrdenTraspasoDetalleDto> CrearLineaOrdenTraspasoAsync(Guid idOrden, CrearLineaOrdenTraspasoDto dto)
        {
            var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var response = await _httpClient.PostAsync($"OrdenTraspaso/{idOrden}/linea", new StringContent(json, Encoding.UTF8, "application/json"));
            
            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<LineaOrdenTraspasoDetalleDto>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            
            throw new Exception($"Error al crear línea de traspaso: {response.StatusCode}");
        }

        private async Task<T> GetAsync<T>(string relativeUrl)
        {
            var response = await _httpClient.GetAsync(relativeUrl);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
    }
} 