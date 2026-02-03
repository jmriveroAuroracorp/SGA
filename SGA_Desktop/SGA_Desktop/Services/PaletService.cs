using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;         // Asegúrate de que PaletDto y TipoPaletDto están aquí
namespace SGA_Desktop.Services
{
	public class PaletService : ApiService
	{
		public PaletService() : base() { }

		public async Task<List<TraspasoErrorDto>> ObtenerTraspasosErrorErpAsync(short codigoEmpresa)
		{
			var uri = $"palet/traspasos/error-erp?codigoEmpresa={codigoEmpresa}";
			return await _httpClient.GetFromJsonAsync<List<TraspasoErrorDto>>(uri)
				?? new List<TraspasoErrorDto>();
		}

		public async Task<List<PaletPendienteVaciadoDto>> ObtenerPaletsPendientesVaciadoAsync(short codigoEmpresa)
		{
			var uri = $"palet/pendientes-vaciado?codigoEmpresa={codigoEmpresa}";
			return await _httpClient.GetFromJsonAsync<List<PaletPendienteVaciadoDto>>(uri)
				?? new List<PaletPendienteVaciadoDto>();
		}

		public async Task<(bool exito, string? mensaje)> RelanzarTraspasoAsync(Guid traspasoId, RelanzarTraspasoRequest request)
		{
			var resp = await _httpClient.PostAsJsonAsync($"palet/traspasos/{traspasoId}/relanzar", request);
			if (resp.IsSuccessStatusCode)
			{
				return (true, null);
			}

			var mensaje = await resp.Content.ReadAsStringAsync();
			if (string.IsNullOrWhiteSpace(mensaje))
			{
				mensaje = "No se pudo relanzar el traspaso.";
			}

			return (false, mensaje);
		}

		public async Task<(bool exito, string? mensaje)> VaciarPaletPendienteAsync(Guid paletId, int usuarioId)
		{
			var payload = new { UsuarioId = usuarioId };
			var resp = await _httpClient.PostAsJsonAsync($"palet/{paletId}/vaciar-pendiente", payload);
			if (resp.IsSuccessStatusCode)
			{
				return (true, null);
			}

			var mensaje = await resp.Content.ReadAsStringAsync();
			if (string.IsNullOrWhiteSpace(mensaje))
			{
				mensaje = "No se pudo vaciar el palet.";
			}

			return (false, mensaje);
		}

		public async Task<List<TipoPaletDto>> ObtenerTiposPaletAsync()
		{
			return await _httpClient
				.GetFromJsonAsync<List<TipoPaletDto>>("palet/maestros")
				?? new List<TipoPaletDto>();
		}

		/// <summary>
		/// Obtiene pallets aplicando uno o más filtros:
		/// codigo, estado, fechaApertura, fechaCierre,
		/// fechaAperturaDesde, fechaCierreHasta,
		/// usuarioApertura, usuarioCierre.
		/// </summary>
		public async Task<List<PaletDto>> ObtenerPaletsAsync(
		short codigoEmpresa,
		string? codigo = null,
		string? estado = null,
		string? tipoPaletCodigo = null,
		DateTime? fechaApertura = null,
		DateTime? fechaCierre = null,
		DateTime? fechaDesde = null,
		DateTime? fechaHasta = null,
		int? usuarioApertura = null,
		int? usuarioCierre = null,
		bool sinCierre = false,
		string? almacen = null,
		string? tipoUltimaActividad = null,
		int? usuarioUltimaActividad = null,
		int? limite = null
	)
		{
			var query = new List<string> { $"codigoEmpresa={codigoEmpresa}" };

			if (!string.IsNullOrWhiteSpace(codigo)) query.Add($"codigo={codigo}");
			if (!string.IsNullOrWhiteSpace(estado)) query.Add($"estado={estado}");
			if (!string.IsNullOrWhiteSpace(tipoPaletCodigo)) query.Add($"tipoPaletCodigo={tipoPaletCodigo}");
			if (fechaApertura.HasValue) query.Add($"fechaApertura={fechaApertura:yyyy-MM-dd}");
			if (fechaCierre.HasValue) query.Add($"fechaCierre={fechaCierre:yyyy-MM-dd}");
			if (fechaDesde.HasValue) query.Add($"fechaDesde={fechaDesde:yyyy-MM-dd}");
			if (fechaHasta.HasValue) query.Add($"fechaHasta={fechaHasta:yyyy-MM-dd}");
			if (usuarioApertura.HasValue) query.Add($"usuarioApertura={usuarioApertura}");
			if (usuarioCierre.HasValue) query.Add($"usuarioCierre={usuarioCierre}");
			if (sinCierre) query.Add("sinCierre=true");
			if (!string.IsNullOrWhiteSpace(almacen)) query.Add($"almacen={almacen}");
			if (!string.IsNullOrWhiteSpace(tipoUltimaActividad)) query.Add($"tipoUltimaActividad={tipoUltimaActividad}");
			if (usuarioUltimaActividad.HasValue) query.Add($"usuarioUltimaActividad={usuarioUltimaActividad}");
			if (limite.HasValue) query.Add($"limite={limite.Value}");

			var uri = "palet/filtros?" + string.Join("&", query);
			return await _httpClient.GetFromJsonAsync<List<PaletDto>>(uri)
								  ?? new List<PaletDto>();
		}

		public async Task<List<EstadoPaletDto>> ObtenerEstadosAsync()
		{
			return await _httpClient
				.GetFromJsonAsync<List<EstadoPaletDto>>("palet/estados")
				?? new List<EstadoPaletDto>();
		}

		public async Task<PaletDto> PaletCrearAsync(PaletCrearDto req)
		{
			var resp = await _httpClient.PostAsJsonAsync("palet", req);
			var text = await resp.Content.ReadAsStringAsync();
			if (!resp.IsSuccessStatusCode)
				throw new ApplicationException($"API error {(int)resp.StatusCode}: {text}");

			return JsonSerializer.Deserialize<PaletDto>(text,
				new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
		}


		public async Task<List<string>> ObtenerAlmacenesAsync()
		{
			return await _httpClient.GetFromJsonAsync<List<string>>("palet/almacenes")
				?? new List<string>();
		}

		/// <summary>
		/// Trae todos los usuarios que han abierto o cerrado pallets
		/// en la empresa seleccionada.
		/// </summary>
		public async Task<List<UsuarioDto>> ObtenerUsuariosAsync()
		{
			var empresa = SessionManager.EmpresaSeleccionada!.Value;
			var uri = $"palet/operarios?codigoEmpresa={empresa}";

			return await _httpClient
				.GetFromJsonAsync<List<UsuarioDto>>(uri)
				?? new List<UsuarioDto>();
		}


		/// <summary>
		/// Obtiene las líneas de un pallet específico.
		/// </summary>
		public async Task<List<LineaPaletDto>> ObtenerLineasAsync(Guid paletId)
		{
			return await _httpClient
				.GetFromJsonAsync<List<LineaPaletDto>>($"palet/{paletId}/lineas")
				?? new List<LineaPaletDto>();
		}

		/// <summary>
		/// Lanza la impresión de la etiqueta para un pallet.
		/// </summary>
		public async Task ImprimirEtiquetaAsync(Guid paletId)
		{
			var resp = await _httpClient.PostAsync($"palet/{paletId}/imprimir", null);
			resp.EnsureSuccessStatusCode();
		}

		public async Task<List<StockDisponibleDto>> BuscarStockAsync(string articulo)
		{
			var empresa = SessionManager.EmpresaSeleccionada!.Value;

			// puedes mejorar pasando también filtros opcionales aquí
			var uri = $"stock/articulo?codigoEmpresa={empresa}&codigoArticulo={articulo}";

			var resultado = await _httpClient.GetFromJsonAsync<List<StockDisponibleDto>>(uri)
						   ?? new List<StockDisponibleDto>();

			return resultado;
		}

		//public async Task<List<StockDisponibleDto>> BuscarStockAsync(string codigoArticulo, string descripcion)
		//{
		//	var queryParams = new Dictionary<string, string>();

		//	if (!string.IsNullOrWhiteSpace(codigoArticulo))
		//		queryParams["codigoArticulo"] = codigoArticulo;

		//	if (!string.IsNullOrWhiteSpace(descripcion))
		//		queryParams["descripcion"] = descripcion;

		//	// si necesitas empresa, añade también:
		//	queryParams["codigoEmpresa"] = SessionManager.EmpresaSeleccionada.ToString();

		//	var queryString = string.Join("&", queryParams.Select(kv => $"{kv.Key}={HttpUtility.UrlEncode(kv.Value)}"));
		//	var url = $"/api/stock/articulo?{queryString}";

		//	var response = await _httpClient.GetAsync(url);
		//	response.EnsureSuccessStatusCode();

		//	return await response.Content.ReadFromJsonAsync<List<StockDisponibleDto>>() ?? new();
		//}

		public async Task<List<StockDisponibleDto>> BuscarStockDisponibleAsync(string codigoArticulo, string descripcion)
		{
			var queryParams = new Dictionary<string, string>();

			if (!string.IsNullOrWhiteSpace(codigoArticulo))
				queryParams["codigoArticulo"] = codigoArticulo;

			if (!string.IsNullOrWhiteSpace(descripcion))
				queryParams["descripcion"] = descripcion;

			// si necesitas empresa, añade también:
			queryParams["codigoEmpresa"] = SessionManager.EmpresaSeleccionada.ToString();

			var queryString = string.Join("&", queryParams.Select(kv => $"{kv.Key}={HttpUtility.UrlEncode(kv.Value)}"));
			var url = $"/api/stock/articulo/disponible?{queryString}";

			var response = await _httpClient.GetAsync(url);
			response.EnsureSuccessStatusCode();

			var resultado = await response.Content.ReadFromJsonAsync<List<StockDisponibleDto>>() ?? new();
			
			// 🔷 NUEVO: Inicializar CantidadAMoverTexto con el valor máximo disponible
			// 🔷 CAMBIADO: Usar formato adaptativo que muestra solo decimales significativos
			foreach (var stock in resultado)
			{
				stock.CantidadAMoverTexto = Helpers.DecimalFormatHelper.FormatearCantidad(stock.Disponible);
			}
			
			return resultado;
		}


		public async Task<(bool exito, string? mensaje)> AnhadirLineaPaletAsync(Guid paletId, StockDisponibleDto dto)
		{
			var payload = new
			{
				CodigoEmpresa = SessionManager.EmpresaSeleccionada!.Value,
				CodigoArticulo = dto.CodigoArticulo,
				DescripcionArticulo = dto.DescripcionArticulo,
				Cantidad = dto.CantidadAMover,
				Lote = dto.Partida,
				FechaCaducidad = dto.FechaCaducidad,
				CodigoAlmacen = dto.CodigoAlmacen,
				Ubicacion = dto.Ubicacion,
				UsuarioId = SessionManager.UsuarioActual?.operario ?? 0,
				Observaciones = ""
			};

			var resp = await _httpClient.PostAsJsonAsync($"palet/{paletId}/lineas", payload);

			if (resp.IsSuccessStatusCode)
			{
				return (true, null);
			}

			// lee el mensaje de error que envía el servidor
			var mensaje = await resp.Content.ReadAsStringAsync();

			if (string.IsNullOrWhiteSpace(mensaje))
				mensaje = $"Error desconocido al mover el artículo {dto.CodigoArticulo} desde {dto.Ubicacion}.";

			return (false, mensaje);
		}

		public async Task<bool> EliminarLineaPaletAsync(Guid lineaId, int usuarioId)
		{
			var resp = await _httpClient.DeleteAsync($"palet/lineas/{lineaId}?usuarioId={usuarioId}");
			return resp.IsSuccessStatusCode;
		}
		//public async Task<bool> CerrarPaletAsync(Guid paletId, int usuarioId)
		//{
		//	var resp = await _httpClient.PostAsync(
		//		$"palet/{paletId}/cerrar?usuarioId={usuarioId}", null);

		//	return resp.IsSuccessStatusCode;
		//}
	public async Task<(bool exito, string? mensaje)> CerrarPaletAsync(
		Guid paletId,
		int usuarioId,
		string codigoAlmacen,             // origen (de las líneas)
		string codigoAlmacenDestino,
		string ubicacionDestino,
		string? comentario = null, // Nuevo parámetro opcional
		decimal? altura = null,
		decimal? peso = null
	)
	{
		try
		{
			var dto = new
			{
				UsuarioId = usuarioId,
				CodigoAlmacen = codigoAlmacen,
				CodigoAlmacenDestino = codigoAlmacenDestino,
				UbicacionDestino = ubicacionDestino,
				TipoTraspaso = "PALET",
				CodigoEstado = "PENDIENTE_ERP",
				UsuarioFinalizacionId = usuarioId,
				CodigoEmpresa = SessionManager.EmpresaSeleccionada!.Value,
				Comentario = comentario,
				Altura = altura,
				Peso = peso
			};

			var resp = await _httpClient.PostAsJsonAsync(
				$"palet/{paletId}/cerrar", dto);

			if (resp.IsSuccessStatusCode)
			{
				return (true, null);
			}

			// Leer el mensaje de error que envía el servidor (incluye los valores de SAGE y StorageControl)
			var mensaje = await resp.Content.ReadAsStringAsync();
			if (string.IsNullOrWhiteSpace(mensaje))
			{
				mensaje = $"Error al cerrar palet: {resp.StatusCode}";
			}

			return (false, mensaje);
		}
		catch (Exception ex)
		{
			return (false, $"Error al cerrar el palet: {ex.Message}");
		}
	}





	public async Task<(bool success, string? errorMessage)> ReabrirPaletAsync(Guid paletId, int usuarioId)
	{
		try
		{
			var resp = await _httpClient.PostAsync($"palet/{paletId}/reabrir?usuarioId={usuarioId}", null);
			
			if (resp.IsSuccessStatusCode)
			{
				return (true, null);
			}

			// Leer el mensaje de error que envía el servidor
			var mensaje = await resp.Content.ReadAsStringAsync();
			if (string.IsNullOrWhiteSpace(mensaje))
			{
				mensaje = $"Error al reabrir palet: {resp.StatusCode}";
			}

			return (false, mensaje);
		}
		catch (Exception ex)
		{
			return (false, $"Error al reabrir el palet: {ex.Message}");
		}
	}

		public async Task<PaletDto?> ObtenerPaletPorIdAsync(Guid id)
		{
			var resp = await _httpClient.GetAsync($"palet/{id}");
			if (!resp.IsSuccessStatusCode) return null;

			var text = await resp.Content.ReadAsStringAsync();
			return JsonSerializer.Deserialize<PaletDto>(text,
				new JsonSerializerOptions(JsonSerializerDefaults.Web));
		}
	} 
}
