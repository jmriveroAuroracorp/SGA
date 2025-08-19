using SGA_Desktop.Helpers;
using System.Net.Http.Headers;
using System.Net.Http;

public class ApiService
{
	protected readonly HttpClient _httpClient;

	public ApiService()
	{
		_httpClient = new HttpClient
		{
			//BaseAddress = new Uri("http://10.0.0.175:5234/api/")
			BaseAddress = new Uri("http://localhost:5234/api/")

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
		var resp = await _httpClient.GetAsync(ruta);
		resp.EnsureSuccessStatusCode();
		return await resp.Content.ReadAsStringAsync();
	}
}
