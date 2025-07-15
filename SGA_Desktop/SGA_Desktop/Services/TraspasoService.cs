using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SGA_Desktop.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace SGA_Desktop.Services
{
	
	public class TraspasosService : ApiService
	{
		public TraspasosService() : base() { }

		public async Task<int> CrearTraspasoAsync(CrearTraspasoDto dto)
		{
			var resp = await _httpClient.PostAsJsonAsync("traspasos", dto);
			var text = await resp.Content.ReadAsStringAsync();

			if (!resp.IsSuccessStatusCode)
				throw new ApplicationException($"API error {(int)resp.StatusCode}: {text}");

			var json = JsonSerializer.Deserialize<JsonElement>(text);
			return json.GetProperty("id").GetInt32();
		}

		public async Task<bool> FinalizarTraspasoAsync(Guid traspasoId, FinalizarTraspasoDto dto)
		{
			var resp = await _httpClient.PutAsJsonAsync($"traspasos/{traspasoId}/ubicar", dto);

			if (!resp.IsSuccessStatusCode)
			{
				var mensaje = await resp.Content.ReadAsStringAsync();
				throw new ApplicationException($"Error al finalizar traspaso: {mensaje}");
			}

			return true;
		}

		public async Task<TraspasoDto?> ObtenerTraspasoPorIdAsync(Guid id)
		{
			var resp = await _httpClient.GetAsync($"traspasos/{id}");
			if (!resp.IsSuccessStatusCode) return null;

			var text = await resp.Content.ReadAsStringAsync();
			return JsonSerializer.Deserialize<TraspasoDto>(text,
				new JsonSerializerOptions(JsonSerializerDefaults.Web));
		}

		public async Task<List<TraspasoDto>> ObtenerTraspasosAsync(
			Guid? paletId = null,
			string? codigoEstado = null,
			DateTime? fechaDesde = null,
			DateTime? fechaHasta = null)
		{
			var query = new List<string>();

			if (paletId.HasValue) query.Add($"paletId={paletId}");
			if (!string.IsNullOrWhiteSpace(codigoEstado)) query.Add($"codigoEstado={codigoEstado}");
			if (fechaDesde.HasValue) query.Add($"fechaDesde={fechaDesde:yyyy-MM-dd}");
			if (fechaHasta.HasValue) query.Add($"fechaHasta={fechaHasta:yyyy-MM-dd}");

			var uri = "traspasos";
			if (query.Count > 0)
				uri += "?" + string.Join("&", query);

			return await _httpClient.GetFromJsonAsync<List<TraspasoDto>>(uri)
				   ?? new List<TraspasoDto>();
		}

		public async Task<List<EstadoTraspasoDto>> ObtenerEstadosAsync()
		{
			return await _httpClient
				.GetFromJsonAsync<List<EstadoTraspasoDto>>("traspasos/estados")
				?? new List<EstadoTraspasoDto>();
		}

		public async Task<List<TraspasoDto>> ObtenerTraspasosFiltradosAsync(string? estado)
		{
			var url = $"traspasos";

			if (!string.IsNullOrWhiteSpace(estado))
				url += $"?codigoEstado={estado}";

			var resp = await _httpClient.GetFromJsonAsync<List<TraspasoDto>>(url);
			return resp ?? new List<TraspasoDto>();
		}

	}

}
