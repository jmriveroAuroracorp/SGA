using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SGA_Desktop.Models;
using System.Net.Http.Json;
using System.Text.Json;
using System.Net.Http;

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

			var resp = await _httpClient.GetAsync(uri);
			if (!resp.IsSuccessStatusCode)
				return new List<TraspasoDto>();

			var text = await resp.Content.ReadAsStringAsync();
			return JsonSerializer.Deserialize<List<TraspasoDto>>(text, 
				new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new List<TraspasoDto>();
		}

		public async Task<List<EstadoTraspasoDto>> ObtenerEstadosAsync()
		{
			return await _httpClient
				.GetFromJsonAsync<List<EstadoTraspasoDto>>("traspasos/estados")
				?? new List<EstadoTraspasoDto>();
		}

		public async Task<List<TraspasoDto>> ObtenerTraspasosFiltradosAsync(
	string? estado,
	string? codigoPalet,
	string? almacenOrigen,
	string? almacenDestino,
	DateTime? fechaInicioDesde,
	DateTime? fechaInicioHasta)
		{
			var query = new List<string>();
			if (!string.IsNullOrWhiteSpace(estado))
				query.Add($"codigoEstado={estado}");
			if (!string.IsNullOrWhiteSpace(codigoPalet))
				query.Add($"codigoPalet={codigoPalet}");
			if (!string.IsNullOrWhiteSpace(almacenOrigen))
				query.Add($"almacenOrigen={almacenOrigen}");
			if (!string.IsNullOrWhiteSpace(almacenDestino))
				query.Add($"almacenDestino={almacenDestino}");
			if (fechaInicioDesde.HasValue)
				query.Add($"fechaInicioDesde={fechaInicioDesde:yyyy-MM-dd}");
			if (fechaInicioHasta.HasValue)
				query.Add($"fechaInicioHasta={fechaInicioHasta:yyyy-MM-dd}");

			var url = "traspasos";
			if (query.Count > 0)
				url += "?" + string.Join("&", query);

			var resp = await _httpClient.GetAsync(url);
			if (!resp.IsSuccessStatusCode)
				return new List<TraspasoDto>();

			var text = await resp.Content.ReadAsStringAsync();
			return JsonSerializer.Deserialize<List<TraspasoDto>>(text,
				new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new List<TraspasoDto>();
		}

		public async Task<ApiResult> CrearTraspasoArticuloAsync(CrearTraspasoArticuloDto dto)
		{
			var resp = await _httpClient.PostAsJsonAsync("traspasos/articulo", dto);
			var text = await resp.Content.ReadAsStringAsync();
			if (!resp.IsSuccessStatusCode)
			{
				return new ApiResult { Success = false, ErrorMessage = text };
			}
			// Deserializa el JSON para recoger paletInfo y message
			var json = System.Text.Json.JsonDocument.Parse(text).RootElement;
			string? paletInfo = json.TryGetProperty("paletInfo", out var pi) ? pi.GetString() : null;
			string? message = json.TryGetProperty("message", out var m) ? m.GetString() : null;
			return new ApiResult
			{
				Success = true,
				ErrorMessage = message,
				PaletInfo = paletInfo
			};
		}

		public async Task<List<TraspasoArticuloDto>> GetUltimosTraspasosArticulosAsync()
		{
			var resp = await _httpClient.GetAsync("traspasos/articulos");
			if (!resp.IsSuccessStatusCode)
				return new List<TraspasoArticuloDto>();
			var text = await resp.Content.ReadAsStringAsync();
			return System.Text.Json.JsonSerializer.Deserialize<List<TraspasoArticuloDto>>(text, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)) ?? new List<TraspasoArticuloDto>();
		}

		public async Task<List<PaletMovibleDto>> ObtenerPaletsCerradosMoviblesAsync()
		{
			var resp = await _httpClient.GetAsync("traspasos/palets-cerrados-movibles");
			if (!resp.IsSuccessStatusCode)
				return new List<PaletMovibleDto>();
			var text = await resp.Content.ReadAsStringAsync();
			return System.Text.Json.JsonSerializer.Deserialize<List<PaletMovibleDto>>(text, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)) ?? new List<PaletMovibleDto>();
		}

		public async Task<ApiResult> MoverPaletAsync(MoverPaletDto dto)
		{
			var resp = await _httpClient.PostAsJsonAsync("traspasos/mover-palet", dto);
			var text = await resp.Content.ReadAsStringAsync();
			if (!resp.IsSuccessStatusCode)
				return new ApiResult { Success = false, ErrorMessage = text };
			return new ApiResult { Success = true };
		}



		// Deja solo el helper y dos wrappers públicos

		private async Task<string> ConsultarEstadoPaletAsync(int codigoEmpresa, string codigoAlmacen, string ubicacion)
		{
			if (string.IsNullOrWhiteSpace(ubicacion)) return "NINGUNO";

			var url = $"palet/estado-en-ubicacion?codigoEmpresa={codigoEmpresa}&codigoAlmacen={codigoAlmacen}&ubicacion={ubicacion}";
			var resp = await _httpClient.GetAsync(url);
			var text = await resp.Content.ReadAsStringAsync();
			if (!resp.IsSuccessStatusCode) return "NINGUNO";

			var json = System.Text.Json.JsonDocument.Parse(text).RootElement;
			return json.TryGetProperty("estado", out var estado)
				? (estado.GetString() ?? "NINGUNO")
				: "NINGUNO";
		}

		public Task<string> ConsultarEstadoPaletDestinoAsync(int codigoEmpresa, string codigoAlmacen, string ubicacion)
			=> ConsultarEstadoPaletAsync(codigoEmpresa, codigoAlmacen, ubicacion);

		public Task<string> ConsultarEstadoPaletOrigenAsync(int codigoEmpresa, string codigoAlmacen, string ubicacion)
			=> ConsultarEstadoPaletAsync(codigoEmpresa, codigoAlmacen, ubicacion);

		// NUEVO: reabrir palet
		public async Task<bool> ReabrirPaletAsync(int codigoEmpresa, string codigoAlmacen, string ubicacion)
		{
			var payload = new { codigoEmpresa, codigoAlmacen, ubicacion };
			var resp = await _httpClient.PostAsJsonAsync("palet/reabrir", payload); // ajusta ruta si hace falta
			return resp.IsSuccessStatusCode;
		}


	}

	public class ApiResult
	{
		public bool Success { get; set; }
		public string? ErrorMessage { get; set; }
		public string? PaletInfo { get; set; } 
	}

	public class PaletMovibleDto
	{
		public Guid Id { get; set; }
		public string Codigo { get; set; }
		public string Estado { get; set; }
		public string? AlmacenOrigen { get; set; }
		public string? UbicacionOrigen { get; set; }
		public DateTime? FechaUltimoTraspaso { get; set; }
		public int? UsuarioUltimoTraspaso { get; set; }
	}

}
