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
    public class ConteosService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public ConteosService()
        {
            _httpClient = new HttpClient();
            _baseUrl = "http://10.0.0.175:5234/api/conteos";
            
            // Configurar headers comunes
            if (!string.IsNullOrEmpty(SessionManager.Token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", SessionManager.Token);
            }
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

                var response = await _httpClient.PostAsync($"{_baseUrl}/ordenes", content);
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
        /// Obtener una orden de conteo por ID
        /// </summary>
        public async Task<OrdenConteoDto?> ObtenerOrdenAsync(long id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/ordenes/{id}");
                
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
                var url = $"{_baseUrl}/ordenes";
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
        /// Cerrar una orden de conteo
        /// </summary>
        public async Task<bool> CerrarOrdenAsync(long id)
        {
            try
            {
                var content = new StringContent("", Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/ordenes/{id}/cerrar", content);
                response.EnsureSuccessStatusCode();

                return true;
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Error de comunicación con el servidor: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al cerrar la orden de conteo: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Liberar recursos
        /// </summary>
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
} 