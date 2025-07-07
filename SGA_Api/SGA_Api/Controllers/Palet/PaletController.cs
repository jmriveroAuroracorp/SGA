using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Alergenos;
using SGA_Api.Models.Palet;
using System.Data;
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
			// Traemos el diccionario de nombres desde la vista
			var nombreDict = await _auroraSgaContext.vUsuariosConNombre
				.ToDictionaryAsync(x => x.UsuarioId, x => x.NombreOperario);

			var q = _auroraSgaContext.Palets
				.Where(p =>
					p.CodigoEmpresa == codigoEmpresa &&
					p.FechaVaciado == null
				)
				.AsQueryable();

			// Aplicamos los filtros opcionales
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

			// Proyección a DTO
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

			// Enriquecer con los nombres
			foreach (var palet in lista)
			{
				if (palet.UsuarioAperturaId.HasValue &&
					nombreDict.TryGetValue(palet.UsuarioAperturaId.Value, out var nombreApertura))
				{
					palet.UsuarioAperturaNombre = nombreApertura;
				}

				if (palet.UsuarioCierreId.HasValue &&
					nombreDict.TryGetValue(palet.UsuarioCierreId.Value, out var nombreCierre))
				{
					palet.UsuarioCierreNombre = nombreCierre;
				}
			}

			return Ok(lista);
		}




		// GET api/palet/{id}
		[HttpGet("{id:guid}", Name = "GetPaletById")]
		public async Task<ActionResult<PaletDto>> GetPaletById(Guid id)
		{
			var palet = await _auroraSgaContext.Palets.FindAsync(id);
			if (palet == null) return NotFound();

			return Ok(new PaletDto
			{
				Id = palet.Id,
				CodigoEmpresa = palet.CodigoEmpresa,
				Codigo = palet.Codigo,
				Estado = palet.Estado,
				TipoPaletCodigo = palet.TipoPaletCodigo,
				FechaApertura = palet.FechaApertura,
				FechaCierre = palet.FechaCierre,
				UsuarioAperturaId = palet.UsuarioAperturaId,
				UsuarioCierreId = palet.UsuarioCierreId,
				OrdenTrabajoId = palet.OrdenTrabajoId,
				Altura = palet.Altura,
				Peso = palet.Peso,
				EtiquetaGenerada = palet.EtiquetaGenerada,
				IsVaciado = palet.IsVaciado,
				FechaVaciado = palet.FechaVaciado
			});
		}

		// GET api/palet/estados
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

			return Ok(lista);  // <— Aquí faltaba el return
		}


		// POST api/palet
		[HttpPost]
		public async Task<ActionResult<PaletDto>> CrearPalet([FromBody] PaletCrearDto dto)
		{
			try
			{
				var pCanal = new SqlParameter("@Canal", SqlDbType.VarChar, 10) { Value = "" };
				var pSerie = new SqlParameter("@Serie", SqlDbType.Int) { Value = 0 };
				var pCodigoEmpresa = new SqlParameter("@CodigoEmpresa", SqlDbType.SmallInt) { Value = dto.CodigoEmpresa };
				var pEstado = new SqlParameter("@Estado", SqlDbType.NVarChar, 50) { Value = "Abierto" };
				var pTipoPaletCodigo = new SqlParameter("@TipoPaletCodigo", SqlDbType.NVarChar, 10) { Value = (object)dto.TipoPaletCodigo ?? DBNull.Value };
				var pUsuarioAperturaId = new SqlParameter("@UsuarioAperturaId", SqlDbType.Int) { Value = dto.UsuarioAperturaId };
				var pOrdenTrabajoId = new SqlParameter("@OrdenTrabajoId", SqlDbType.VarChar, 50) { Value = string.IsNullOrWhiteSpace(dto.OrdenTrabajoId) ? "" : (object)dto.OrdenTrabajoId! };
				var pNuevoCodigo = new SqlParameter("@NuevoCodigo", SqlDbType.VarChar, 20) { Direction = ParameterDirection.Output };

				await _auroraSgaContext.Database.ExecuteSqlRawAsync(
					"EXEC dbo.CrearPalet @Canal, @Serie, @CodigoEmpresa, @Estado, @TipoPaletCodigo, @UsuarioAperturaId, @OrdenTrabajoId, @NuevoCodigo OUTPUT",
					pCanal, pSerie, pCodigoEmpresa, pEstado, pTipoPaletCodigo, pUsuarioAperturaId, pOrdenTrabajoId, pNuevoCodigo);

				var codigoGenerado = (string)pNuevoCodigo.Value!;
				var palet = await _auroraSgaContext.Palets.SingleAsync(x => x.Codigo == codigoGenerado);
				var resultado = new PaletDto
				{
					Id = palet.Id,
					CodigoEmpresa = palet.CodigoEmpresa,
					Codigo = palet.Codigo,
					Estado = palet.Estado,
					TipoPaletCodigo = palet.TipoPaletCodigo,
					FechaApertura = palet.FechaApertura,
					FechaCierre = palet.FechaCierre,
					UsuarioAperturaId = palet.UsuarioAperturaId,
					UsuarioCierreId = palet.UsuarioCierreId,
					OrdenTrabajoId = palet.OrdenTrabajoId,
					Altura = palet.Altura,
					Peso = palet.Peso,
					EtiquetaGenerada = palet.EtiquetaGenerada,
					IsVaciado = palet.IsVaciado,
					FechaVaciado = palet.FechaVaciado
				};

				return CreatedAtRoute("GetPaletById", new { id = palet.Id }, resultado);
			}
			catch (Exception ex)
			{
				return Problem(detail: ex.ToString(), statusCode: 500, title: "Error creando palet");
			}
		}
	}
}



