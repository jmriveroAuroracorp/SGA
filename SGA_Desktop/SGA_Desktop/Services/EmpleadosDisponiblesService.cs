using System.Text.Json;
using System.Text;
using System.Net.Http;
using SGA_Desktop.Models;

namespace SGA_Desktop.Services
{
    public class EmpleadosDisponiblesService : ApiService
    {
        private const string BASE_URL = "OperariosConfiguracion";

        /// <summary>
        /// Obtiene la lista de empleados disponibles para dar de alta en SGA
        /// </summary>
        public async Task<List<EmpleadoDisponibleDto>?> ObtenerEmpleadosDisponiblesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BASE_URL}/empleados-disponibles");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var empleados = JsonSerializer.Deserialize<List<EmpleadoDisponibleDto>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    return empleados;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Error al obtener empleados disponibles: {response.StatusCode}");
                    return new List<EmpleadoDisponibleDto>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al obtener empleados disponibles: {ex.Message}");
                return new List<EmpleadoDisponibleDto>();
            }
        }

        /// <summary>
        /// Da de alta un empleado en SGA
        /// </summary>
        public async Task<(bool Exito, string Mensaje)> DarAltaEmpleadoAsync(DarAltaEmpleadoDto empleado)
        {
            try
            {
                var json = JsonSerializer.Serialize(empleado, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{BASE_URL}/dar-alta-empleado", content);
                
                var responseContent = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    var responseObj = JsonSerializer.Deserialize<JsonElement>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    var message = responseObj.TryGetProperty("message", out var msg) ? msg.GetString() : "Alta exitosa";
                    return (true, message ?? "Alta exitosa");
                }
                else
                {
                    // Intentar leer el mensaje de error del API
                    try
                    {
                        var errorObj = JsonSerializer.Deserialize<JsonElement>(responseContent, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        
                        var errorMessage = errorObj.TryGetProperty("message", out var msg) ? msg.GetString() : responseContent;
                        return (false, errorMessage ?? responseContent);
                    }
                    catch
                    {
                        return (false, responseContent);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al dar de alta empleado {empleado.CodigoEmpleado}: {ex.Message}");
                return (false, $"Error de conexión: {ex.Message}");
            }
        }

        /// <summary>
        /// Da de alta múltiples empleados en SGA
        /// </summary>
        public async Task<List<(int CodigoEmpleado, bool Exito, string Mensaje)>> DarAltaEmpleadosAsync(List<DarAltaEmpleadoDto> empleados)
        {
            var resultados = new List<(int CodigoEmpleado, bool Exito, string Mensaje)>();

            foreach (var empleado in empleados)
            {
                try
                {
                    var resultado = await DarAltaEmpleadoAsync(empleado);
                    resultados.Add((empleado.CodigoEmpleado, resultado.Exito, resultado.Mensaje));
                }
                catch (Exception ex)
                {
                    resultados.Add((empleado.CodigoEmpleado, false, $"Error: {ex.Message}"));
                }
            }

            return resultados;
        }
    }
}
