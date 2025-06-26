using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Alergenos;
using SGA_Api.Models.Palet;

namespace SGA_Api.Controllers.Palet
{
	[ApiController]
	[Route("api/[controller]")]
	public class PaletController : ControllerBase
	{
		private readonly AuroraSgaDbContext _auroraSgaContext;
		public PaletController(AuroraSgaDbContext auroraSgaContext)
		{
			_auroraSgaContext = auroraSgaContext;
		}
		/// <summary>
		/// GET api/palets/maestros
		/// Devuelve el catálogo completo de palets (código + descripción).
		/// </summary>
		[HttpGet("maestros")]
		public async Task<ActionResult<List<TipoPaletDto>>> GetTipoPalets()
		{
			var lista = await _auroraSgaContext.TipoPalets
				.Select(p => new TipoPaletDto
				{
					CodigoPalet = p.CodigoPalet,
					Descripcion = p.Descripcion
				})
				.ToListAsync();
			return Ok(lista);
		}
	}
}

