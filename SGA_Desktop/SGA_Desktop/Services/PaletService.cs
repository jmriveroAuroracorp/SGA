using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using SGA_Desktop.Models;

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

	}
}
