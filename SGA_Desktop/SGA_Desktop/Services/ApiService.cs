using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

public class ApiService
{
	protected readonly HttpClient _httpClient;

	public ApiService()
	{
		_httpClient = new HttpClient
		{
			BaseAddress = new Uri("http://10.0.0.175:5234/api/")
			//BaseAddress = new Uri("http://localhost:5234/api/")

		};
		_httpClient.DefaultRequestHeaders.Accept
				   .Add(new MediaTypeWithQualityHeaderValue("application/json"));

		if (!string.IsNullOrWhiteSpace(SessionManager.Token))
			_httpClient.DefaultRequestHeaders.Authorization =
				new AuthenticationHeaderValue("Bearer", SessionManager.Token);
	}

	/// <summary>
	/// Hace GET a la ruta relativa (añade BaseAddress y token automáticamente)
	/// y devuelve el contenido como string o lanza excepción si no es 2xx.
	/// </summary>
	protected async Task<string> GetStringAsync(string ruta)
	{
		// Si la aplicación se está cerrando, no hacer llamadas HTTP
		if (SessionManager.IsClosing)
			throw new OperationCanceledException("La aplicación se está cerrando");

		var resp = await _httpClient.GetAsync(ruta);
		resp.EnsureSuccessStatusCode();
		return await resp.Content.ReadAsStringAsync();
	}

	/// <summary>
	/// Hace POST a la ruta relativa con el objeto serializado como JSON
	/// </summary>
	protected async Task<string> PostAsync<T>(string ruta, T objeto)
	{
		// Si la aplicación se está cerrando, no hacer llamadas HTTP
		if (SessionManager.IsClosing)
			throw new OperationCanceledException("La aplicación se está cerrando");

		var json = JsonSerializer.Serialize(objeto);
		var content = new StringContent(json, Encoding.UTF8, "application/json");
		
		var resp = await _httpClient.PostAsync(ruta, content);
		resp.EnsureSuccessStatusCode();
		return await resp.Content.ReadAsStringAsync();
	}

	/// <summary>
	/// Hace POST a la ruta relativa con el objeto serializado como JSON
	/// Versión que permite operaciones críticas durante el cierre (como logout)
	/// </summary>
	protected async Task<string> PostAsync<T>(string ruta, T objeto, bool permitirDuranteCierre)
	{
		// Si la aplicación se está cerrando y no se permite durante el cierre, no hacer llamadas HTTP
		if (SessionManager.IsClosing && !permitirDuranteCierre)
			throw new OperationCanceledException("La aplicación se está cerrando");

		var json = JsonSerializer.Serialize(objeto);
		var content = new StringContent(json, Encoding.UTF8, "application/json");
		
		var resp = await _httpClient.PostAsync(ruta, content);
		resp.EnsureSuccessStatusCode();
		return await resp.Content.ReadAsStringAsync();
	}

	/// <summary>
	/// Hace DELETE a la ruta relativa
	/// </summary>
	protected async Task<string> DeleteAsync(string ruta)
	{
		// Si la aplicación se está cerrando, no hacer llamadas HTTP
		if (SessionManager.IsClosing)
			throw new OperationCanceledException("La aplicación se está cerrando");

		var resp = await _httpClient.DeleteAsync(ruta);
		resp.EnsureSuccessStatusCode();
		return await resp.Content.ReadAsStringAsync();
	}

	/// <summary>
	/// Obtiene las notificaciones pendientes de un usuario desde la API
	/// </summary>
	public async Task<List<NotificacionApiDto>> ObtenerNotificacionesPendientesAsync(int usuarioId)
	{
		try
		{
			var json = await GetStringAsync($"notificaciones/{usuarioId}");
			var response = JsonSerializer.Deserialize<ApiResponse<List<NotificacionApiDto>>>(json);
			return response?.Data ?? new List<NotificacionApiDto>();
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error al obtener notificaciones pendientes: {ex.Message}");
			return new List<NotificacionApiDto>();
		}
	}

	/// <summary>
	/// Obtiene el contador de notificaciones pendientes desde la API
	/// </summary>
	public async Task<int> ObtenerContadorPendientesAsync(int usuarioId)
	{
		try
		{
			var json = await GetStringAsync($"notificaciones/{usuarioId}/contador");
			var response = JsonSerializer.Deserialize<ApiResponse<ContadorResponse>>(json);
			return response?.Data?.Contador ?? 0;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"❌ Error al obtener contador de notificaciones: {ex.Message}");
			return 0;
		}
	}

	/// <summary>
	/// Marca una notificación como leída en la API
	/// </summary>
	public async Task<bool> MarcarComoLeidaAsync(Guid idNotificacion, int usuarioId)
	{
		try
		{
			var request = new MarcarLeidaApiDto { UsuarioId = usuarioId };
			await PostAsync($"notificaciones/{idNotificacion}/marcar-leida", request);
			return true;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"❌ Error al marcar notificación como leída: {ex.Message}");
			return false;
		}
	}

	/// <summary>
	/// Marca todas las notificaciones como leídas en la API
	/// </summary>
	public async Task<int> MarcarTodasComoLeidasAsync(int usuarioId)
	{
		try
		{
			var json = await PostAsync("notificaciones/marcar-todas-leidas", usuarioId);
			var response = JsonSerializer.Deserialize<ApiResponse<MarcarTodasResponse>>(json);
			return response?.Data?.Count ?? 0;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"❌ Error al marcar todas las notificaciones como leídas: {ex.Message}");
			return 0;
		}
	}

	/// <summary>
	/// Elimina una notificación en la API
	/// </summary>
	public async Task<bool> EliminarNotificacionAsync(Guid idNotificacion, int usuarioId)
	{
		try
		{
			await DeleteAsync($"notificaciones/{idNotificacion}?usuarioId={usuarioId}");
			return true;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"❌ Error al eliminar notificación: {ex.Message}");
			return false;
		}
	}
}

/// <summary>
/// Clase para deserializar respuestas de la API
/// </summary>
public class ApiResponse<T>
{
	[JsonPropertyName("success")]
	public bool Success { get; set; }
	
	[JsonPropertyName("data")]
	public T? Data { get; set; }
	
	[JsonPropertyName("message")]
	public string? Message { get; set; }
	
	[JsonPropertyName("count")]
	public int Count { get; set; }
}

/// <summary>
/// Respuesta del contador de notificaciones
/// </summary>
public class ContadorResponse
{
	public int Contador { get; set; }
}

/// <summary>
/// Respuesta de marcar todas como leídas
/// </summary>
public class MarcarTodasResponse
{
	public int Count { get; set; }
}
