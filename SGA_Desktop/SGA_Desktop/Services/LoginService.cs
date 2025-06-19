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
		try
		{
			var json = JsonSerializer.Serialize(request);
			var content = new StringContent(json, Encoding.UTF8, "application/json");

			var response = await _httpClient.PostAsync("Login", content);
			if (!response.IsSuccessStatusCode)
				return null;

			var result = await response.Content.ReadAsStringAsync();
			var loginResp = JsonSerializer.Deserialize<LoginResponse>(result,
								new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

			if (loginResp != null)
			{
				// 1) Guardamos la respuesta de login en sesión
				SessionManager.UsuarioActual = loginResp;

				// 2) A continuación, cargamos la impresora preferida del usuario
				var (okPrn, printerName) = await ObtenerImpresoraPreferidaAsync(loginResp.operario);
				if (okPrn && !string.IsNullOrWhiteSpace(printerName))
				{
					SessionManager.PreferredPrinter = printerName!;
				}
			}

			return loginResp;
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

}
