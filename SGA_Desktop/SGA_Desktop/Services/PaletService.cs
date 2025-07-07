using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;         // Asegúrate de que PaletDto y TipoPaletDto están aquí
namespace SGA_Desktop.Services
{
	public class PaletService : ApiService
	{
		public PaletService() : base() { }

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
		bool sinCierre = false
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

		
	}
}
