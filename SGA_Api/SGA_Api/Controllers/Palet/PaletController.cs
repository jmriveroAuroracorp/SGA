using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Alergenos;
using SGA_Api.Models.Palet;
using System.Security.Claims;

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

		/// <summary>
		/// GET api/palet
		/// Filtra por:
		///  • id (GUID)
		///  • codigo
		///  • estado
		///  • fechaCreacion (igual) y opcionalmente sólo sin cierre
		///  • rango fechaCreacionDesde – fechaCierreHasta
		///  • usuarioCreacion
		///  • usuarioCierre
		/// Devuelve por defecto los primeros “top” (100) ordenados por FechaCreacion.
		/// </summary>
		[HttpGet("filtros")]
		public async Task<ActionResult<List<PaletDto>>> GetPalets(
	[FromQuery] short codigoEmpresa,
	[FromQuery] string? codigo = null,
	[FromQuery] string? estado = null,
	[FromQuery] string? tipoPaletCodigo = null,
	[FromQuery] DateTime? fechaApertura = null,
	[FromQuery] DateTime? fechaCierre = null,
	[FromQuery] DateTime? fechaDesde = null,
	[FromQuery] DateTime? fechaHasta = null,
	[FromQuery] int? usuarioApertura = null,
	[FromQuery] int? usuarioCierre = null,
	[FromQuery] bool sinCierre = false
)
		{
			// 1) Solo palets de esta empresa Y que NO estén vaciados
			var q = _auroraSgaContext.Palets
				.Where(p =>
					p.CodigoEmpresa == codigoEmpresa &&
					// p.IsVaciado == false   ó bien:
					p.FechaVaciado == null
				)
				.AsQueryable();

			// 2) Filtros opcionales (código, estado, tipoPaletCodigo…)
			if (!string.IsNullOrWhiteSpace(codigo))
				q = q.Where(p => p.Codigo == codigo);

			if (!string.IsNullOrWhiteSpace(estado))
				q = q.Where(p => p.Estado == estado);

			if (!string.IsNullOrWhiteSpace(tipoPaletCodigo))
				q = q.Where(p => p.TipoPaletCodigo == tipoPaletCodigo);

			if (fechaApertura.HasValue)
				q = q.Where(p => p.FechaApertura.Date == fechaApertura.Value.Date);

			if (fechaCierre.HasValue)
				q = q.Where(p =>
					p.FechaCierre.HasValue &&
					p.FechaCierre.Value.Date == fechaCierre.Value.Date
				);

			if (sinCierre)
				q = q.Where(p => p.FechaCierre == null);

			if (fechaDesde.HasValue && fechaHasta.HasValue)
				q = q.Where(p =>
					p.FechaApertura >= fechaDesde.Value &&
					p.FechaCierre <= fechaHasta.Value
				);

			if (usuarioApertura.HasValue)
				q = q.Where(p => p.UsuarioAperturaId == usuarioApertura.Value);

			if (usuarioCierre.HasValue)
				q = q.Where(p => p.UsuarioCierreId == usuarioCierre.Value);

			// 3) Proyección a DTO sin Take()
			var lista = await q
				.OrderBy(p => p.FechaApertura)
				.Select(p => new PaletDto
				{
					CodigoEmpresa = p.CodigoEmpresa,
					Id = p.Id,
					Codigo = p.Codigo,
					Estado = p.Estado,
					TipoPaletCodigo = p.TipoPaletCodigo,
					FechaApertura = p.FechaApertura,
					FechaCierre = p.FechaCierre,
					UsuarioAperturaId = p.UsuarioAperturaId,
					UsuarioCierreId = p.UsuarioCierreId,
					Altura = p.Altura,
					Peso = p.Peso,
					EtiquetaGenerada = p.EtiquetaGenerada,
					IsVaciado = p.IsVaciado,
					FechaVaciado = p.FechaVaciado
				})
				.ToListAsync();

			return Ok(lista);
		}

		/// <summary>
		/// POST api/palet
		/// Crea un palet nuevo. CódigoEmpresa y UsuarioAperturaId se toman del token.
		/// </summary>
		// SGA_Api/Controllers/Palet/PaletController.cs
		[HttpPost]
		public async Task<ActionResult<PaletDto>> CrearPalet([FromBody] PaletCrearDto dto)
		{
			try
			{
				var p = new Models.Palet.Palet
				{
					CodigoEmpresa = dto.CodigoEmpresa,
					Codigo = dto.Codigo,
					Estado = "Abierto",
					TipoPaletCodigo = dto.TipoPaletCodigo,
					FechaApertura = DateTime.Now,
					UsuarioAperturaId = dto.UsuarioAperturaId,
					Altura = dto.Altura,
					Peso = dto.Peso,
					EtiquetaGenerada = false,
					IsVaciado = false
				};

				_auroraSgaContext.Palets.Add(p);
				await _auroraSgaContext.SaveChangesAsync();

				var resultado = new PaletDto
				{
					CodigoEmpresa = p.CodigoEmpresa,
					Id = p.Id,
					Codigo = p.Codigo,
					Estado = p.Estado,
					TipoPaletCodigo = p.TipoPaletCodigo,
					FechaApertura = p.FechaApertura,
					UsuarioAperturaId = p.UsuarioAperturaId,
					Altura = p.Altura,
					Peso = p.Peso,
					EtiquetaGenerada = p.EtiquetaGenerada,
					IsVaciado = p.IsVaciado
				};

				return CreatedAtAction(nameof(GetPaletById), new { id = p.Id }, resultado);
			}
			catch (Exception ex)
			{
				return Problem(detail: ex.ToString(),
							   statusCode: 500,
							   title: "Error creando palet");
			}
		}



		// Controllers/Palet/PaletController.cs
		[HttpGet("estados")]
		public async Task<ActionResult<List<EstadoPaletDto>>> GetEstadosPalet()
		{
			var lista = await _auroraSgaContext.TipoEstadoPalet
				.OrderBy(e => e.Orden)
				.Select(e => new EstadoPaletDto
				{
					CodigoEstado = e.CodigoEstado,
					Descripcion = e.Descripcion,
					Orden = e.Orden
				})
				.ToListAsync();

			return Ok(lista);
		}
		/// <summary>
		/// GET api/palet/{id}
		/// Recupera un palet por su GUID dentro de la empresa del token.
		/// </summary>
		[HttpGet("{id:guid}")]
		public async Task<ActionResult<PaletDto>> GetPaletById(Guid id)
		{
			// Extrae empresa del token, igual que en CrearPalet
			var empresaClaim = User.FindFirst("empresa");
			if (empresaClaim == null) return Forbid();
			var empresa = short.Parse(empresaClaim.Value);

			// Busca la entidad
			var entidad = await _auroraSgaContext.Palets
				.Where(p => p.CodigoEmpresa == empresa && p.Id == id)
				.FirstOrDefaultAsync();

			if (entidad == null)
				return NotFound();

			// Mapea a DTO
			var dto = new PaletDto
			{
				CodigoEmpresa = entidad.CodigoEmpresa,
				Id = entidad.Id,
				Codigo = entidad.Codigo,
				Estado = entidad.Estado,
				TipoPaletCodigo = entidad.TipoPaletCodigo,
				FechaApertura = entidad.FechaApertura,
				FechaCierre = entidad.FechaCierre,
				UsuarioAperturaId = entidad.UsuarioAperturaId,
				UsuarioCierreId = entidad.UsuarioCierreId,
				Altura = entidad.Altura,
				Peso = entidad.Peso,
				EtiquetaGenerada = entidad.EtiquetaGenerada,
				IsVaciado = entidad.IsVaciado,
				FechaVaciado = entidad.FechaVaciado
			};

			return Ok(dto);
		}

	}
}

