using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Impresion;

namespace SGA_Api.Controllers.Impresion
{
	[ApiController]
	[Route("api/[controller]")]
	public class ImpresionController : ControllerBase
	{
		private readonly AuroraSgaDbContext _context;

		public ImpresionController(AuroraSgaDbContext context)
		{
			_context = context;
		}

		[HttpPost("log")]
		public async Task<IActionResult> InsertarLogImpresion([FromBody] LogImpresionDto dto)
		{
			try
			{
				var log = new LogImpresion
				{
					Usuario = dto.Usuario,
					Dispositivo = dto.Dispositivo,
					IdImpresora = dto.IdImpresora,
					EtiquetaImpresa = dto.EtiquetaImpresa,
					CodigoArticulo = dto.CodigoArticulo,
					DescripcionArticulo = dto.DescripcionArticulo,
					Copias = dto.Copias ?? 1,  // si nulo, mínimo 1
					CodigoAlternativo = dto.CodigoAlternativo,
					FechaCaducidad = dto.FechaCaducidad?.Date,
					Partida = dto.Partida,
					Alergenos = dto.Alergenos,
					PathEtiqueta = dto.PathEtiqueta,
					FechaRegistro = DateTime.Now 
				};

				_context.LogImpresiones.Add(log);
				await _context.SaveChangesAsync();

				return Ok(new { success = true, logId = log.Id });
			}
			catch (Exception ex)
			{
				return StatusCode(500, new
				{
					error = ex.Message,
					details = ex.InnerException?.Message // 👈 esto es clave
				});
			}
		}
		// GET api/Impresion/impresoras
		[HttpGet("impresoras")]
		public async Task<ActionResult<List<ImpresoraDto>>> GetImpresoras()
		{
			var lista = await _context.Impresoras
									  .AsNoTracking()
									  .OrderBy(p => p.Nombre)   // ahora se llama Nombre
									  .Select(p => new ImpresoraDto
									  {
										  Id = p.Id,
										  Nombre = p.Nombre
									  })
									  .ToListAsync();
			return Ok(lista);
		}


	}
}
