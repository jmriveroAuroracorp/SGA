using System.Net.Http;
using System.Net.Http.Headers;

public class ApiService
{
	protected readonly HttpClient _httpClient;

	public ApiService()
	{
		_httpClient = new HttpClient
		{
			BaseAddress = new Uri("http://10.0.0.175:5234/api/")
		};
		_httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
	}
}
