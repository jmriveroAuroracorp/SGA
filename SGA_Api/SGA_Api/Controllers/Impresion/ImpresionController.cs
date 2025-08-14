using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Impresion;
using SGA_Api.Models.Impresion.ImpUbiMultiple;

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
				// Validación: si es etiqueta de palet (2), los dos campos extra son obligatorios
				if (dto.TipoEtiqueta == 2 &&
					(string.IsNullOrWhiteSpace(dto.CodigoGS1) || string.IsNullOrWhiteSpace(dto.CodigoPalet)))
				{
					return BadRequest("Para etiquetas de tipo 2 (palet), debe enviar CodigoGS1 y CodigoPalet.");
				}

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
					FechaRegistro = DateTime.Now,

					TipoEtiqueta = dto.TipoEtiqueta,
					CodigoGS1 = dto.CodigoGS1,
					CodigoPalet = dto.CodigoPalet,

					// Nuevos campos de ubicación
					CodAlmacen = dto.CodAlmacen,
					CodUbicacion = dto.CodUbicacion,
					Altura = dto.Altura,
					Estanteria = dto.Estanteria,
					Pasillo = dto.Pasillo,
					Posicion = dto.Posicion
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

		// POST api/Impresion/ubicaciones-lote
		[HttpPost("ubicaciones-lote")]
		public async Task<ActionResult<UbicacionesLoteRespuestaDto>> InsertarLogImpresionUbicacionesLote(
			[FromBody] UbicacionesLoteDto req)
		{
			if (req == null) return BadRequest("Solicitud vacía.");
			if (req.Ubicaciones == null || req.Ubicaciones.Count == 0)
				return BadRequest("Debe indicar al menos una ubicación.");
			if (req.Ubicaciones.Count > 2000)
				return BadRequest("El tamaño máximo del lote es 2000 ubicaciones.");

			// Validar impresora
			var impresoraExiste = await _context.Impresoras
				.AsNoTracking()
				.AnyAsync(i => i.Id == req.ImpresoraId);
			if (!impresoraExiste)
				return BadRequest("Impresora no válida.");

			var respuesta = new UbicacionesLoteRespuestaDto
			{
				Exito = true,
				Total = req.Ubicaciones.Count
			};

			var fechaAhora = DateTime.Now;
			var rutaEtiqueta = req.RutaEtiqueta;
			foreach (var (item, indice) in req.Ubicaciones.Select((x, i) => (x, i)))
			{
				try
				{
					if (string.IsNullOrWhiteSpace(item.CodigoAlmacen) || string.IsNullOrWhiteSpace(item.CodigoUbicacion))
						throw new ArgumentException("CodigoAlmacen y CodigoUbicacion son obligatorios.");

					var log = new LogImpresion
					{
						Usuario = req.Usuario,
						Dispositivo = req.Dispositivo,
						FechaRegistro = fechaAhora,
						IdImpresora = req.ImpresoraId,
						EtiquetaImpresa = 0,
						Copias = 1, // fijo 1 copia
						PathEtiqueta = rutaEtiqueta,

						TipoEtiqueta = 3, // ubicaciones
						CodigoGS1 = null,
						CodigoPalet = null,

						CodigoArticulo = null,
						DescripcionArticulo = null,
						CodigoAlternativo = null,
						FechaCaducidad = null,
						Partida = null,
						Alergenos = null,

						CodAlmacen = item.CodigoAlmacen,
						CodUbicacion = item.CodigoUbicacion,
						Altura = item.Altura,
						Estanteria = item.Estanteria,
						Pasillo = item.Pasillo,
						Posicion = item.Posicion
					};

					_context.LogImpresiones.Add(log);
				}
				catch (Exception ex)
				{
					respuesta.Exito = false;
					respuesta.Errores.Add(new UbicacionLoteErrorDto
					{
						Indice = indice,
						CodigoAlmacen = item?.CodigoAlmacen,
						CodigoUbicacion = item?.CodigoUbicacion,
						Mensaje = ex.Message
					});
				}
			}

			await _context.SaveChangesAsync();

			respuesta.Insertados = respuesta.Total - respuesta.Errores.Count;
			return Ok(respuesta);
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
