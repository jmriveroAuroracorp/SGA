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
	DateTime? fechaInicioHasta,
	int? usuarioId = null,
	int? limite = null) // Si es null, la API usará un límite más alto automáticamente
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
			query.Add($"fechaDesde={fechaInicioDesde:yyyy-MM-dd}");
		if (fechaInicioHasta.HasValue)
			query.Add($"fechaHasta={fechaInicioHasta:yyyy-MM-dd}"); // API ahora maneja el fin de día automáticamente
		if (usuarioId.HasValue && usuarioId.Value > 0)
			query.Add($"usuarioId={usuarioId.Value}");

		// 🚀 Calcular límite dinámico basado en el rango de fechas
		// Si hay un rango de fechas amplio, aumentar el límite para evitar cortar resultados
		int limiteFinal = limite ?? 5000; // Límite más alto por defecto cuando hay rango de fechas
		if (fechaInicioDesde.HasValue && fechaInicioHasta.HasValue)
		{
			var diasRango = (fechaInicioHasta.Value - fechaInicioDesde.Value).Days + 1;
			// Si el rango es de más de 7 días, aumentar el límite significativamente
			if (diasRango > 7)
			{
				limiteFinal = Math.Max(limiteFinal, 10000); // 10,000 para rangos grandes
			}
			else if (diasRango > 3)
			{
				limiteFinal = Math.Max(limiteFinal, 5000); // 5,000 para rangos medianos
			}
		}
		
		query.Add($"limite={limiteFinal}");

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

	public async Task<List<PaletMovibleDto>> ObtenerPaletsConUbicacionAsync()
	{
		var resp = await _httpClient.GetAsync("traspasos/palets-con-ubicacion");
		if (!resp.IsSuccessStatusCode)
			return new List<PaletMovibleDto>();
		var text = await resp.Content.ReadAsStringAsync();
		return System.Text.Json.JsonSerializer.Deserialize<List<PaletMovibleDto>>(text, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)) ?? new List<PaletMovibleDto>();
	}

	// 🔷 OPTIMIZADO: Método sobrecargado que acepta IDs específicos para cargar solo esos palets
	public async Task<List<PaletMovibleDto>> ObtenerPaletsConUbicacionAsync(List<Guid> paletIds)
	{
		if (paletIds == null || !paletIds.Any())
			return new List<PaletMovibleDto>();

		// Construir query string con IDs separados por comas
		var idsString = string.Join(",", paletIds);
		var resp = await _httpClient.GetAsync($"traspasos/palets-con-ubicacion?paletIds={Uri.EscapeDataString(idsString)}");
		
		if (!resp.IsSuccessStatusCode)
			return new List<PaletMovibleDto>();
		
		var text = await resp.Content.ReadAsStringAsync();
		return System.Text.Json.JsonSerializer.Deserialize<List<PaletMovibleDto>>(text, 
			new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)) 
			?? new List<PaletMovibleDto>();
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

		// NUEVO: Consultar palets disponibles en una ubicación (precheck)
		public async Task<PrecheckFinalizarArticuloResponse> PrecheckFinalizarArticuloAsync(
			int codigoEmpresa,
			string almacenDestino,
			string? ubicacionDestino = null)
		{
			var url = $"traspasos/articulo/precheck-finalizar?codigoEmpresa={codigoEmpresa}&almacenDestino={almacenDestino}";
			if (!string.IsNullOrWhiteSpace(ubicacionDestino))
				url += $"&ubicacionDestino={ubicacionDestino}";

			var resp = await _httpClient.GetAsync(url);
			if (!resp.IsSuccessStatusCode)
				return new PrecheckFinalizarArticuloResponse { Existe = false };

			var text = await resp.Content.ReadAsStringAsync();
			return JsonSerializer.Deserialize<PrecheckFinalizarArticuloResponse>(text,
				new JsonSerializerOptions(JsonSerializerDefaults.Web))
				?? new PrecheckFinalizarArticuloResponse { Existe = false };
		}
		/// <summary>
		/// 🔷 NUEVO: Validar traspaso de artículo individual
		/// </summary>
		/// <param name="request">Datos del traspaso a validar</param>
		/// <returns>Resultado de la validación</returns>
		public async Task<ValidacionTraspasoResult> ValidarTraspasoArticuloAsync(ValidacionTraspasoRequest request)
		{
			try
			{
				System.Diagnostics.Debug.WriteLine($"🔍 Llamando API validación - Artículo: {request.CodigoArticulo}, Ubicación: '{request.UbicacionDestino}', Empresa: {request.CodigoEmpresa}");
				
				var response = await _httpClient.PostAsJsonAsync("traspasos/validar-articulo", request);

				System.Diagnostics.Debug.WriteLine($"🔍 Respuesta API - Status: {response.StatusCode}, Success: {response.IsSuccessStatusCode}");

				if (response.IsSuccessStatusCode)
				{
					var json = await response.Content.ReadAsStringAsync();
					System.Diagnostics.Debug.WriteLine($"🔍 JSON respuesta: {json}");
					
					var resultado = JsonSerializer.Deserialize<ValidacionTraspasoResult>(json);
					return resultado ?? new ValidacionTraspasoResult { EsValido = true };
				}
				else
				{
					System.Diagnostics.Debug.WriteLine($"🔍 Error API - Status: {response.StatusCode}");
					// En caso de error de API, permitir traspaso para no bloquear operaciones
					return new ValidacionTraspasoResult { EsValido = true };
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error validando traspaso: {ex.Message}");
				// En caso de error, permitir traspaso para no bloquear operaciones
				return new ValidacionTraspasoResult { EsValido = true };
			}
		}

		/// <summary>
		/// Obtener traspasos de StorageControl con filtros
		/// </summary>
		public async Task<List<TraspasoStorageControlDto>> ObtenerTraspasosStorageControlAsync(
			DateTime? fechaDesde = null,
			DateTime? fechaHasta = null,
			string? almacenOrigen = null,
			string? almacenDestino = null,
			string? codigoArticulo = null,
			string? partida = null)
		{
			var query = new List<string>();
			
			if (fechaDesde.HasValue)
				query.Add($"fechaDesde={fechaDesde:yyyy-MM-dd}");
			if (fechaHasta.HasValue)
				query.Add($"fechaHasta={fechaHasta:yyyy-MM-dd}");
			if (!string.IsNullOrWhiteSpace(almacenOrigen))
				query.Add($"almacenOrigen={Uri.EscapeDataString(almacenOrigen)}");
			if (!string.IsNullOrWhiteSpace(almacenDestino))
				query.Add($"almacenDestino={Uri.EscapeDataString(almacenDestino)}");
			if (!string.IsNullOrWhiteSpace(codigoArticulo))
				query.Add($"codigoArticulo={Uri.EscapeDataString(codigoArticulo)}");
			if (!string.IsNullOrWhiteSpace(partida))
				query.Add($"partida={Uri.EscapeDataString(partida)}");

			var url = "traspasos/storagecontrol";
			if (query.Count > 0)
				url += "?" + string.Join("&", query);

			var resp = await _httpClient.GetAsync(url);
			if (!resp.IsSuccessStatusCode)
				return new List<TraspasoStorageControlDto>();

			var text = await resp.Content.ReadAsStringAsync();
			return JsonSerializer.Deserialize<List<TraspasoStorageControlDto>>(text,
				new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new List<TraspasoStorageControlDto>();
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
