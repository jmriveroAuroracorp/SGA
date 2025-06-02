using SGA_Desktop.Models;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SGA_Desktop.Services;
public class LoginService : ApiService
{
	public async Task<LoginResponse?> LoginAsync(LoginRequest request)
	{
		var json = JsonSerializer.Serialize(request);
		var content = new StringContent(json, Encoding.UTF8, "application/json");

		var response = await _httpClient.PostAsync("Login", content);

		if (response.IsSuccessStatusCode)
		{
			var result = await response.Content.ReadAsStringAsync();
			return JsonSerializer.Deserialize<LoginResponse>(result, new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			});
		}

		return null;
	}
}
