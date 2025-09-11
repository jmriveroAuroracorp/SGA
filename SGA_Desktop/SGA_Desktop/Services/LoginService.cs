using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace SGA_Desktop.Services;
public class LoginService : ApiService
{
	public async Task<LoginResponse?> LoginAsync(LoginRequest request)
	{
		var json = JsonSerializer.Serialize(request);
		var content = new StringContent(json, Encoding.UTF8, "application/json");

		var response = await _httpClient.PostAsync("Login", content);

		// 🔹 Si credenciales inválidas (401 Unauthorized) → devolver null
		if (response.StatusCode == HttpStatusCode.Unauthorized)
			return null;

		// 🔹 Si otro error del servidor → lanzar excepción
		if (!response.IsSuccessStatusCode)
		{
			var errorMsg = await response.Content.ReadAsStringAsync();
			throw new HttpRequestException(
				$"Error en login: {response.StatusCode} - {errorMsg}");
		}

		// 🔹 Si todo OK → deserializar respuesta
		var result = await response.Content.ReadAsStringAsync();
		var loginResp = JsonSerializer.Deserialize<LoginResponse>(result,
							new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

		if (loginResp != null)
		{
			// Guardamos sesión
			SessionManager.UsuarioActual = loginResp;

			// Cargar impresora preferida del usuario
			var (okPrn, printerName) = await ObtenerImpresoraPreferidaAsync(loginResp.operario);
			if (okPrn && !string.IsNullOrWhiteSpace(printerName))
				SessionManager.PreferredPrinter = printerName!;
		}

		return loginResp;
	}



	public async Task<bool> RegistrarDispositivoAsync(Dispositivo dispositivo)
	{
		var json = JsonSerializer.Serialize(dispositivo);
		var content = new StringContent(json, Encoding.UTF8, "application/json");

		var response = await _httpClient.PostAsync("Dispositivo/registrar", content);

		return response.IsSuccessStatusCode;
	}

	public async Task RegistrarLogEventoAsync(LogEvento log)
	{
		// Asegurarse de que el token se está usando en este POST manual
		if (!string.IsNullOrWhiteSpace(SessionManager.Token))
		{
			_httpClient.DefaultRequestHeaders.Authorization =
				new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", SessionManager.Token);
		}

		var json = JsonSerializer.Serialize(log);
		var content = new StringContent(json, Encoding.UTF8, "application/json");

		var response = await _httpClient.PostAsync("LogEvento/crear", content);

		if (!response.IsSuccessStatusCode)
		{
			var error = await response.Content.ReadAsStringAsync();
			throw new Exception($"Error al registrar el evento de login: {error}");
		}
	}

	public async Task<bool> DesactivarDispositivoAsync(string idDispositivo, string tipo, int idUsuario)
	{
		var dispositivo = new
		{
			id = idDispositivo,
			tipo = tipo,
			idUsuario = idUsuario
		};

		var json = JsonSerializer.Serialize(dispositivo);
		var content = new StringContent(json, Encoding.UTF8, "application/json");

		if (!string.IsNullOrWhiteSpace(SessionManager.Token))
		{
			_httpClient.DefaultRequestHeaders.Authorization =
				new AuthenticationHeaderValue("Bearer", SessionManager.Token);
		}

		var response = await _httpClient.PostAsync("Dispositivo/desactivar", content);
		return response.IsSuccessStatusCode;
	}

	public async Task<(bool ok, string? detalle, HttpStatusCode status)> EstablecerEmpresaPreferidaAsync(
		int idUsuario, short codigoEmpresa)
	{
		var body = JsonSerializer.Serialize(new { idEmpresa = codigoEmpresa.ToString() });
		var content = new StringContent(body, Encoding.UTF8, "application/json");


		var req = new HttpRequestMessage(HttpMethod.Patch, $"Usuarios/{idUsuario}")  // 👈 nota 'api/'
		{ Content = content };

		Console.WriteLine(req.RequestUri);   // o pon un breakpoint

		if (!string.IsNullOrWhiteSpace(SessionManager.Token))
			_httpClient.DefaultRequestHeaders.Authorization =
				new AuthenticationHeaderValue("Bearer", SessionManager.Token);

		var resp = await _httpClient.SendAsync(req);
		var txt = await resp.Content.ReadAsStringAsync();
		return (resp.IsSuccessStatusCode, txt, resp.StatusCode);
	}


	public async Task<(bool ok, short? idEmpresa)> ObtenerEmpresaPreferidaAsync(int idUsuario)
	{
		// 1) Asegura que enviamos el Bearer token
		if (!string.IsNullOrWhiteSpace(SessionManager.Token))
		{
			_httpClient.DefaultRequestHeaders.Authorization =
				new AuthenticationHeaderValue("Bearer", SessionManager.Token);
		}

		// 2) Lanza la petición
		var response = await _httpClient.GetAsync($"Usuarios/{idUsuario}");
		if (!response.IsSuccessStatusCode)
			return (false, null);

		// 3) Lee y parsea el JSON
		var json = await response.Content.ReadAsStringAsync();
		using var doc = JsonDocument.Parse(json);

		if (doc.RootElement.TryGetProperty("idEmpresa", out var prop))
		{
			var str = prop.GetString()?.Trim();
			if (short.TryParse(str, out var idEmp))
				return (true, idEmp);
		}

		return (false, null);
	}

	/// <summary>
	/// PATCH api/Usuarios/{id}
	/// Actualiza solo la propiedad "impresora" del usuario.
	/// </summary>
	public async Task<(bool ok, string? detalle, HttpStatusCode status)> EstablecerImpresoraPreferidaAsync(
		int idUsuario, string nombreImpresora)
	{
		// Prepara el body JSON con la propiedad 'impresora'
		var body = JsonSerializer.Serialize(new { impresora = nombreImpresora });
		var content = new StringContent(body, Encoding.UTF8, "application/json");

		// Construye la petición PATCH a Usuarios/{id}
		var req = new HttpRequestMessage(HttpMethod.Patch, $"Usuarios/{idUsuario}")
		{
			Content = content
		};

		// Añade el token si existe
		if (!string.IsNullOrWhiteSpace(SessionManager.Token))
			_httpClient.DefaultRequestHeaders.Authorization =
				new AuthenticationHeaderValue("Bearer", SessionManager.Token);

		// Envía la petición
		var resp = await _httpClient.SendAsync(req);
		var detalle = await resp.Content.ReadAsStringAsync();
		return (resp.IsSuccessStatusCode, detalle, resp.StatusCode);
	}

	/// <summary>
	/// PATCH api/Usuarios/{id}
	/// Actualiza solo la propiedad "impresora" del usuario.
	/// </summary>
	public async Task<bool> ActualizarImpresoraAsync(int idUsuario, string nombreImpresora)
	{
		if (!string.IsNullOrEmpty(SessionManager.Token))
			_httpClient.DefaultRequestHeaders.Authorization =
				new AuthenticationHeaderValue("Bearer", SessionManager.Token);

		// Sólo enviamos la propiedad que queremos actualizar
		var body = JsonSerializer.Serialize(new { impresora = nombreImpresora });
		var req = new HttpRequestMessage(HttpMethod.Patch, $"Usuarios/{idUsuario}")
		{
			Content = new StringContent(body, Encoding.UTF8, "application/json")
		};
		var resp = await _httpClient.SendAsync(req);
		return resp.IsSuccessStatusCode;
	}

	public async Task<(bool ok, string? impresora)> ObtenerImpresoraPreferidaAsync(int idUsuario)
	{
		if (!string.IsNullOrWhiteSpace(SessionManager.Token))
			_httpClient.DefaultRequestHeaders.Authorization =
				new AuthenticationHeaderValue("Bearer", SessionManager.Token);

		var resp = await _httpClient.GetAsync($"Usuarios/{idUsuario}");
		if (!resp.IsSuccessStatusCode) return (false, null);

		var json = await resp.Content.ReadAsStringAsync();
		using var doc = JsonDocument.Parse(json);

		if (doc.RootElement.TryGetProperty("impresora", out var prop))
			return (true, prop.GetString());

		return (true, null);
	}

	/// <summary>
	/// Obtiene el límite de inventario en euros para un operario
	/// </summary>
	public async Task<decimal> ObtenerLimiteInventarioOperarioAsync(int operario)
	{
		try
		{
			var response = await _httpClient.GetAsync($"OperariosAcceso/limite-inventario/{operario}");
			
			if (response.IsSuccessStatusCode)
			{
				var jsonContent = await response.Content.ReadAsStringAsync();
				
				// 🔧 FIX: El API devuelve un número simple, no JSON
				if (decimal.TryParse(jsonContent, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal limite))
				{
					return limite;
				}
				
				return 0m; // Si no se puede parsear
			}
			
			return 0m; // Sin límite si no se encuentra
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error obteniendo límite operario: {ex.Message}");
			                return 0m; // Sin límite si hay error
            }
        }

        /// <summary>
        /// Obtiene el límite de unidades de inventario para un operario
        /// </summary>
        public async Task<decimal> ObtenerLimiteUnidadesOperarioAsync(int operario)
        {
            try
            {
                var response = await _httpClient.GetAsync($"OperariosAcceso/limite-unidades/{operario}");
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    
                    // El API devuelve un número simple, no JSON
                    if (decimal.TryParse(jsonContent, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal limite))
                    {
                        return limite;
                    }
                    
                    return 0m; // Si no se puede parsear
                }
                
                return 0m; // Sin límite si no se encuentra
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo límite de unidades operario: {ex.Message}");
                return 0m; // Sin límite si hay error
            }
        }

        /// <summary>
        /// Obtiene las diferencias acumuladas del operario para un artículo específico en el día actual
        /// </summary>
        public async Task<(decimal totalUnidades, decimal totalValorEuros)> ObtenerDiferenciasOperarioArticuloDiaAsync(int operario, string codigoArticulo, Guid idInventarioActual)
        {
            try
            {
                var response = await _httpClient.GetAsync($"OperariosAcceso/diferencias-dia/{operario}/{Uri.EscapeDataString(codigoArticulo)}/{idInventarioActual}");
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    
                    using var document = JsonDocument.Parse(jsonContent);
                    var root = document.RootElement;
                    
                    var totalUnidades = root.GetProperty("totalUnidades").GetDecimal();
                    var totalValorEuros = root.GetProperty("totalValorEuros").GetDecimal();
                    
                    return (totalUnidades, totalValorEuros);
                }
                
                return (0m, 0m); // Sin diferencias si no se encuentra
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo diferencias del operario: {ex.Message}");
                return (0m, 0m); // Sin diferencias si hay error
            }
        }

        /// <summary>
        /// Obtiene la lista de operarios con acceso al sistema
        /// </summary>
        public async Task<List<OperariosAccesoDto>> ObtenerOperariosConAccesoAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("OperariosAcceso");
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var operarios = JsonSerializer.Deserialize<List<OperariosAccesoDto>>(jsonContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    return operarios ?? new List<OperariosAccesoDto>();
                }
                
                return new List<OperariosAccesoDto>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo operarios con acceso: {ex.Message}");
                return new List<OperariosAccesoDto>();
            }
        }

        public async Task<List<OperariosAccesoDto>> ObtenerOperariosConAccesoConteosAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("OperariosAcceso/conteos");
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var operarios = JsonSerializer.Deserialize<List<OperariosAccesoDto>>(jsonContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    return operarios ?? new List<OperariosAccesoDto>();
                }
                
                return new List<OperariosAccesoDto>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo operarios con acceso a conteos: {ex.Message}");
                return new List<OperariosAccesoDto>();
            }
        }

}
