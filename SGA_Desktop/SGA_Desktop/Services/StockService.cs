using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json.Linq;

namespace SGA_Desktop.Services
{
	public class StockService : ApiService
	{

		public async Task<List<StockDto>> ObtenerPorArticuloAsync(
		int codigoEmpresa,
		string? codigoArticulo,
		string? partida = null,
		string? codigoAlmacen = null,
		string? codigoUbicacion = null,
		string? descripcion = null) // Nuevo parámetro
		{
			if (string.IsNullOrWhiteSpace(codigoArticulo) && string.IsNullOrWhiteSpace(descripcion))
				throw new ArgumentException("Se debe proporcionar codigoArticulo o descripcion.", nameof(codigoArticulo));

			var qs = $"?codigoEmpresa={codigoEmpresa}";

			if (!string.IsNullOrWhiteSpace(codigoArticulo))
				qs += $"&codigoArticulo={Uri.EscapeDataString(codigoArticulo)}";

			if (!string.IsNullOrWhiteSpace(partida))
				qs += $"&partida={Uri.EscapeDataString(partida!)}";

			if (!string.IsNullOrWhiteSpace(codigoAlmacen))
				qs += $"&codigoAlmacen={Uri.EscapeDataString(codigoAlmacen!)}";

			if (codigoUbicacion != null)
				qs += $"&codigoUbicacion={Uri.EscapeDataString(codigoUbicacion)}";

			// Agregar el nuevo parámetro de descripción si no se proporciona código de artículo
			if (!string.IsNullOrWhiteSpace(descripcion))
				qs += $"&descripcion={Uri.EscapeDataString(descripcion)}";

			return await GetAsync<List<StockDto>>($"Stock/articulo{qs}");
		}



		/// <summary>
		/// GET /api/Stock/ubicacion
		/// Búsqueda por almacén + ubicación (-> "" para Sin ubicación)
		/// </summary>
		//public async Task<List<StockDto>> ObtenerPorUbicacionAsync(
		//	int codigoEmpresa,
		//	string codigoAlmacen,
		//	string codigoUbicacion)
		//{
		//	if (string.IsNullOrWhiteSpace(codigoAlmacen))
		//		throw new ArgumentException("codigoAlmacen es obligatorio.", nameof(codigoAlmacen));
		//	if (codigoUbicacion == null)
		//		throw new ArgumentNullException(nameof(codigoUbicacion));

		//	var qs = $"?codigoEmpresa={codigoEmpresa}"
		//		   + $"&codigoAlmacen={Uri.EscapeDataString(codigoAlmacen)}"
		//		   // Siempre incluimos codigoUbicacion, aunque sea cadena vacía:
		//		   + $"&codigoUbicacion={Uri.EscapeDataString(codigoUbicacion)}";

		//	try
		//	{
		//		return await GetAsync<List<StockDto>>($"Stock/ubicacion{qs}");
		//	}
		//	catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		//	{
		//		// No hay nada en esa ubicación → lista vacía
		//		return new List<StockDto>();
		//	}
		//}
		/// <summary>
		/// GET /api/Stock/ubicacion?codigoEmpresa=…&codigoAlmacen=…&codigoUbicacion=…
		/// Devuelve el stock de una ubicación específica o de todo el almacén si codigoUbicacion es null.
		/// </summary>
		public async Task<List<StockDto>> ObtenerPorUbicacionAsync(
			int codigoEmpresa,
			string codigoAlmacen,
			string? codigoUbicacion) // 🔷 MODIFICADO: Ahora permite null
		{
			if (string.IsNullOrWhiteSpace(codigoAlmacen))
				throw new ArgumentException("codigoAlmacen es obligatorio.", nameof(codigoAlmacen));

			// 🔷 MODIFICADO: Ya no validamos que codigoUbicacion sea null

			var qs = $"?codigoEmpresa={codigoEmpresa}"
				   + $"&codigoAlmacen={Uri.EscapeDataString(codigoAlmacen)}";

			// �� CORREGIDO: Siempre incluir codigoUbicacion, incluso si es string.Empty
			if (codigoUbicacion != null)
			{
				qs += $"&codigoUbicacion={Uri.EscapeDataString(codigoUbicacion)}";
			}
			// Si es null, no se incluye el parámetro (consulta todo el almacén)

			try
			{
				return await GetAsync<List<StockDto>>($"Stock/ubicacion{qs}");
			}
			catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
			{
				// No hay nada en esa ubicación → lista vacía
				return new List<StockDto>();
			}
		}

	public async Task<string> ObtenerAlergenosArticuloAsync(int codigoEmpresa, string codigoArticulo)
	{
		var url = $"Stock/articulo/alergenos?codigoEmpresa={codigoEmpresa}&codigoArticulo={Uri.EscapeDataString(codigoArticulo)}";
		var response = await _httpClient.GetAsync(url);

		if (!response.IsSuccessStatusCode)
			return string.Empty;

		var jsonRaw = await response.Content.ReadAsStringAsync();
		var wrapper = JsonConvert.DeserializeObject<AlergenosWrapper>(jsonRaw);
		return wrapper?.Alergenos ?? string.Empty;
	}

		/// <summary>
		/// Helper genérico para GET + deserializar JSON
		/// </summary>
		private async Task<T> GetAsync<T>(string relativeUrl)
		{
			var response = await _httpClient.GetAsync(relativeUrl);
			response.EnsureSuccessStatusCode();
			var json = await response.Content.ReadAsStringAsync();
			return JsonConvert.DeserializeObject<T>(json)!;
		}

		// ----------------------
		// Métodos existentes para /api/Almacen
		// ----------------------

		/// <summary>
		/// GET /api/Almacen?codigoCentro=…
		/// Devuelve solo la lista de códigos de almacén.
		/// </summary>
		public async Task<List<string>> ObtenerAlmacenesAsync(string codigoCentro)
		{
			var resp = await _httpClient.GetAsync($"Almacen?codigoCentro={Uri.EscapeDataString(codigoCentro)}");
			resp.EnsureSuccessStatusCode();
			return JsonConvert.DeserializeObject<List<string>>(await resp.Content.ReadAsStringAsync())
				   ?? new List<string>();
		}

		/// <summary>
		/// GET /api/Almacen/Ubicaciones?codigoAlmacen=...
		/// Devuelve la lista de códigos de ubicación para el almacén dado.
		/// </summary>
		/// 
		public async Task<List<UbicacionDto>> ObtenerUbicacionesAsync(string codigoAlmacen, short codigoEmpresa, bool soloConStock = false)
		{
			var url = $"Almacen/Ubicaciones?codigoAlmacen={Uri.EscapeDataString(codigoAlmacen)}" +
					  $"&codigoEmpresa={codigoEmpresa}" +
					  $"&soloConStock={soloConStock.ToString().ToLower()}";

			using var resp = await _httpClient.GetAsync(url);
			resp.EnsureSuccessStatusCode();
			var json = await resp.Content.ReadAsStringAsync();
			return JsonConvert.DeserializeObject<List<UbicacionDto>>(json) ?? new();
		}




		// VERSION QUE NO FILTRA UBICACIONES POR SI TIENEN O NO STOCK

		//public async Task<List<string>> ObtenerUbicacionesAsync(string codigoAlmacen)
		//{
		//	var url = $"Almacen/Ubicaciones?codigoAlmacen={Uri.EscapeDataString(codigoAlmacen)}";
		//	using var resp = await _httpClient.GetAsync(url);
		//	resp.EnsureSuccessStatusCode();
		//	var json = await resp.Content.ReadAsStringAsync();
		//	var dtoList = JsonConvert
		//		.DeserializeObject<List<UbicacionDto>>(json)
		//		?? new List<UbicacionDto>();
		//	return dtoList.Select(x => x.Ubicacion).ToList();
		//}

	public async Task<List<AlmacenDto>> ObtenerAlmacenesAutorizadosAsync(short empresa, string centro, List<string> codigos, int? operarioId = null)
		{
			var request = new AlmacenesAutorizadosRequest
			{
				CodigoEmpresa = empresa,
				CodigoCentro = centro,
			CodigosAlmacen = codigos,
			OperarioId = operarioId
			};

			var resp = await _httpClient.PostAsJsonAsync("Almacen/Combos/Autorizados", request);
			resp.EnsureSuccessStatusCode();

			var json = await resp.Content.ReadAsStringAsync();
			return JsonConvert.DeserializeObject<List<AlmacenDto>>(json) ?? new List<AlmacenDto>();
		}

		private class AlergenosWrapper
		{
			[JsonProperty("alergenos")]       // coincide con la clave JSON
			public string Alergenos { get; set; }
		}

	

		/// <summary>
		/// Obtiene el stock disponible (con Reservado y Disponible) por artículo y/o descripción.
		/// Llama a /api/stock/articulo/disponible
		/// </summary>
		public async Task<List<StockDisponibleDto>> ObtenerStockDisponibleAsync(string? codigoArticulo, string? descripcion)
		{
			var queryParams = new Dictionary<string, string>();

			if (!string.IsNullOrWhiteSpace(codigoArticulo))
				queryParams["codigoArticulo"] = codigoArticulo;

			if (!string.IsNullOrWhiteSpace(descripcion))
				queryParams["descripcion"] = descripcion;

			// Añade empresa actual
			queryParams["codigoEmpresa"] = SessionManager.EmpresaSeleccionada.ToString();

			var queryString = string.Join("&", queryParams.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
			var url = $"/api/stock/articulo/disponible?{queryString}";

			var response = await _httpClient.GetAsync(url);
			if (response.IsSuccessStatusCode)
			{
				var json = await response.Content.ReadAsStringAsync();
				var stockData = JsonConvert.DeserializeObject<List<object>>(json);
				
				var resultado = new List<StockDisponibleDto>();
				foreach (var item in stockData)
				{
					var jObject = JObject.FromObject(item);
					var stock = new StockDisponibleDto
					{
						// Campos originales
						DescripcionArticulo = jObject["descripcionArticulo"]?.ToString(),
						CodigoArticulo = jObject["codigoArticulo"]?.ToString(),
						CodigoEmpresa = jObject["codigoEmpresa"]?.ToObject<short>() ?? 0,
						CodigoAlmacen = jObject["codigoAlmacen"]?.ToString(),
						Ubicacion = jObject["ubicacion"]?.ToString(),
						Partida = jObject["partida"]?.ToString(),
						FechaCaducidad = jObject["fechaCaducidad"]?.ToObject<DateTime?>(),
						UnidadSaldo = jObject["unidadSaldo"]?.ToObject<decimal>() ?? 0,
						Reservado = jObject["reservado"]?.ToObject<decimal>() ?? 0,
						Disponible = jObject["disponible"]?.ToObject<decimal>() ?? 0,
						
						// 🔷 NUEVOS CAMPOS
						TipoStock = jObject["tipoStock"]?.ToString() ?? "Suelto",
						PaletId = jObject["paletId"]?.ToObject<Guid?>(),
						CodigoPalet = jObject["codigoPalet"]?.ToString(),
						EstadoPalet = jObject["estadoPalet"]?.ToString(),
						
						// 🔷 NUEVO: Deserializar lista de palets
						Palets = jObject["palets"]?.ToObject<List<PaletDetalleDto>>() ?? new List<PaletDetalleDto>(),
						
						// 🔷 NUEVO: Información de bloqueo por calidad
						IsBloqueadoCalidad = jObject["isBloqueadoCalidad"]?.ToObject<bool>() ?? false,
						MotivoBloqueoCalidad = jObject["motivoBloqueoCalidad"]?.ToString(),
						FechaBloqueoCalidad = jObject["fechaBloqueoCalidad"]?.ToObject<DateTime?>(),
						TipoBloqueoCalidad = jObject["tipoBloqueoCalidad"]?.ToString() ?? "TOTAL",
						
						// 🔷 NUEVO: Fecha del último traspaso
						FechaUltimoTraspaso = jObject["fechaUltimoTraspaso"]?.ToObject<DateTime?>(),
						
						// 🔷 NUEVO: Inicializar CantidadAMoverTexto con el valor máximo disponible
						// 🔷 CAMBIADO: Usar formato adaptativo que muestra solo decimales significativos
						CantidadAMoverTexto = Helpers.DecimalFormatHelper.FormatearCantidad(jObject["disponible"]?.ToObject<decimal>() ?? 0)
					};
					resultado.Add(stock);
				}
				return resultado;
			}
			
			return new List<StockDisponibleDto>();
		}

		/// <summary>
		/// Busca un artículo por código sin filtrar por almacén o stock
		/// Usa el endpoint /api/Stock/buscar-articulo para validar que el artículo existe en el sistema
		/// </summary>
		public async Task<ArticuloResumenDto?> BuscarArticuloPorCodigoAsync(int codigoEmpresa, string codigoArticulo)
		{
			try
			{
				var qs = $"?codigoEmpresa={codigoEmpresa}&codigoArticulo={Uri.EscapeDataString(codigoArticulo)}";
				var response = await _httpClient.GetAsync($"Stock/buscar-articulo{qs}");
				
				if (response.IsSuccessStatusCode)
				{
					var json = await response.Content.ReadAsStringAsync();
					var articulos = JsonConvert.DeserializeObject<List<ArticuloDto>>(json);
					
					if (articulos != null && articulos.Any())
					{
						var articulo = articulos.First();
						return new ArticuloResumenDto
						{
							CodigoArticulo = articulo.CodigoArticulo,
							DescripcionArticulo = articulo.Descripcion ?? string.Empty
						};
					}
				}
				
				return null;
			}
			catch
			{
				return null;
			}
		}

		/// <summary>
		/// Obtiene los lotes activos de un artículo (incluso sin stock) con su fecha de caducidad
		/// </summary>
		public async Task<List<LoteDto>> ObtenerLotesActivosAsync(short codigoEmpresa, string codigoArticulo, bool incluirHistoricos = false)
		{
			var qs = $"?codigoEmpresa={codigoEmpresa}&codigoArticulo={Uri.EscapeDataString(codigoArticulo)}&incluirHistoricos={incluirHistoricos}";
			try
			{
				var response = await _httpClient.GetAsync($"Stock/articulo/lotes-activos{qs}");
				response.EnsureSuccessStatusCode();
				var json = await response.Content.ReadAsStringAsync();
				
				// Deserializar manualmente porque el endpoint devuelve objetos anónimos
				var lotesRaw = JsonConvert.DeserializeObject<List<dynamic>>(json) ?? new List<dynamic>();
				var lotes = new List<LoteDto>();
				
				foreach (var item in lotesRaw)
				{
					var partida = item.partida?.ToString() ?? item.Partida?.ToString() ?? string.Empty;
					DateTime? fechaCaducidad = null;
					
					if (item.fechaCaducidad != null || item.FechaCaducidad != null)
					{
						var fechaStr = item.fechaCaducidad?.ToString() ?? item.FechaCaducidad?.ToString();
						if (!string.IsNullOrWhiteSpace(fechaStr))
						{
							if (DateTime.TryParse(fechaStr, out DateTime fechaParsed))
							{
								fechaCaducidad = fechaParsed.Date;
							}
						}
					}
					
					if (!string.IsNullOrWhiteSpace(partida))
					{
						lotes.Add(new LoteDto
						{
							Partida = partida,
							FechaCaducidad = fechaCaducidad
						});
					}
				}
				
				return lotes;
			}
			catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
			{
				return new List<LoteDto>();
			}
			catch (Exception ex)
			{
				throw new Exception($"Error al obtener lotes activos: {ex.Message}", ex);
			}
		}

		/// <summary>
		/// DTO para respuesta del endpoint buscar-articulo
		/// </summary>
		private class ArticuloDto
		{
			[JsonProperty("codigoArticulo")]
			public string CodigoArticulo { get; set; } = string.Empty;

			[JsonProperty("descripcion")]
			public string? Descripcion { get; set; }

			[JsonProperty("codigoAlternativo")]
			public string? CodigoAlternativo { get; set; }
		}

		/// <summary>
		/// Obtiene el precio medio de un artículo desde el API
		/// </summary>
		public async Task<decimal> ObtenerPrecioMedioAsync(int codigoEmpresa, string codigoArticulo, string codigoAlmacen)
		{
			try
			{
				var qs = $"?codigoEmpresa={codigoEmpresa}&codigoArticulo={Uri.EscapeDataString(codigoArticulo)}&codigoAlmacen={Uri.EscapeDataString(codigoAlmacen)}";
				var response = await _httpClient.GetAsync($"stock/precio-medio{qs}");
				
				if (response.IsSuccessStatusCode)
				{
					var jsonContent = await response.Content.ReadAsStringAsync();
					
					// 🔧 FIX: El API devuelve un número simple, no JSON
					if (decimal.TryParse(jsonContent, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal precio))
					{
						return precio;
					}
					
					return 0m; // Si no se puede parsear
				}
				
				return 0m; // Sin precio si no se encuentra
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error obteniendo precio medio: {ex.Message}");
				return 0m;
			}
		}

		/// <summary>
		/// 🔷 NUEVO: Consulta bloqueos de calidad para una lista de artículos
		/// </summary>
		public async Task<Dictionary<string, BloqueoCalidadInfo>> ObtenerBloqueosCalidadAsync(
			int codigoEmpresa, 
			List<string> codigosArticulos)
		{
			if (codigosArticulos == null || !codigosArticulos.Any())
				return new Dictionary<string, BloqueoCalidadInfo>();

			try
			{
				var request = new
				{
					codigoEmpresa = codigoEmpresa,
					codigosArticulos = codigosArticulos
				};

				var response = await _httpClient.PostAsJsonAsync("Calidad/bloqueos-por-articulos", request);
				
				if (response.IsSuccessStatusCode)
				{
					var json = await response.Content.ReadAsStringAsync();
					var bloqueos = JsonConvert.DeserializeObject<Dictionary<string, BloqueoCalidadInfo>>(json);
					return bloqueos ?? new Dictionary<string, BloqueoCalidadInfo>();
				}
				
				return new Dictionary<string, BloqueoCalidadInfo>();
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error obteniendo bloqueos de calidad: {ex.Message}");
				return new Dictionary<string, BloqueoCalidadInfo>();
			}
		}
	}
}
