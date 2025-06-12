using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace SGA_Desktop.Services;
public class LoginService : ApiService
{
	public async Task<LoginResponse?> LoginAsync(LoginRequest request)
	{
		try
		{
			var json = JsonSerializer.Serialize(request);
			var content = new StringContent(json, Encoding.UTF8, "application/json");

			var response = await _httpClient.PostAsync("Login", content);

			if (response.IsSuccessStatusCode)
			{
				var result = await response.Content.ReadAsStringAsync();

				var loginResp = JsonSerializer.Deserialize<LoginResponse>(result,
								 new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

				var codigoInicial = loginResp.empresas.FirstOrDefault()?.Codigo ?? 0;

				if (loginResp != null)
				{
					SessionManager.UsuarioActual = loginResp;
				}
				return loginResp;
			}

			return null;
		}
		catch (HttpRequestException ex)
		{
			// Error de red: la API no responde
			Console.WriteLine($"Error de conexión: {ex.Message}");
			MessageBox.Show("No se pudo conectar con el servidor. Verifica tu conexión o inténtalo más tarde.",
							"Error de conexión",
							MessageBoxButton.OK,
							MessageBoxImage.Error);
			return null;
		}
		catch (TaskCanceledException ex)
		{
			// Timeout
			Console.WriteLine($"Timeout al intentar conectar: {ex.Message}");
			MessageBox.Show("La solicitud tardó demasiado y fue cancelada. Inténtalo nuevamente más tarde.",
							"Tiempo de espera agotado",
							MessageBoxButton.OK,
							MessageBoxImage.Warning);
			return null;
		}
		catch (Exception ex)
		{
			// Otros errores no controlados
			Console.WriteLine($"Error inesperado: {ex.Message}");
			MessageBox.Show("Ocurrió un error inesperado al iniciar sesión.",
							"Error",
							MessageBoxButton.OK,
							MessageBoxImage.Error);
			return null;
		}
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


}
