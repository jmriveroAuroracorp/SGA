using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Alergenos;

namespace SGA_Api.Controllers.Alergenos
{

	[ApiController]
	[Route("api/[controller]")]
	public class AlergenosController : ControllerBase
	{
		private readonly AuroraSgaDbContext _auroraSgaContext;

		public AlergenosController(
		AuroraSgaDbContext auroraSgaContext)
		{
			_auroraSgaContext = auroraSgaContext;
		}

		/// <summary>
		/// GET api/alergenos/maestros
		/// Devuelve el catálogo completo de alérgenos (código + descripción).
		/// </summary>
		[HttpGet("maestros")]
		public async Task<ActionResult<List<AlergenoDto>>> GetAlergenosMaestros()
		{
			var lista = await _auroraSgaContext.AlergenoMaestros
				.Select(am => new AlergenoDto
				{
					Codigo = am.VCodigoAlergeno,
					Descripcion = am.VDescripcionAlergeno
				})
				.ToListAsync();

			return Ok(lista);
		}
	}
}
