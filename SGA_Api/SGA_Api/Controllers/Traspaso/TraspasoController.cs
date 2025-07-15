using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Traspasos;

namespace SGA_Api.Controllers.Traspasos;

[ApiController]
[Route("api/[controller]")]
public class TraspasosController : ControllerBase
{
    private readonly AuroraSgaDbContext _context;

    public TraspasosController(AuroraSgaDbContext context)
    {
        _context = context;
    }

    #region POST: Crear traspaso (fase 1)
    [HttpPost]
    public async Task<IActionResult> CrearTraspaso([FromBody] CrearTraspasoDto dto)
    {
        try
        {
            var palet = await _context.Palets.FindAsync(dto.PaletId);

            if (palet == null)
                return NotFound($"Palet con ID {dto.PaletId} no encontrado.");

            if (!string.Equals(palet.Estado, "Cerrado", StringComparison.OrdinalIgnoreCase))
                return BadRequest("No se puede traspasar un palet que no está en estado 'Cerrado'.");

            var traspaso = new Traspaso
            {
                AlmacenOrigen = dto.AlmacenOrigen,
                AlmacenDestino = dto.AlmacenDestino,
                CodigoEstado = "PENDIENTE",
                FechaInicio = DateTime.UtcNow,
                UsuarioInicioId = dto.UsuarioInicioId,
                PaletId = dto.PaletId
            };

            _context.Traspasos.Add(traspaso);

            await _context.SaveChangesAsync();

            return Ok(new { message = "Traspaso creado correctamente", traspaso.Id });
        }
        catch (Exception ex)
        {
            return Problem(detail: ex.ToString(), statusCode: 500, title: "Error creando traspaso");
        }
    }
    #endregion

    #region PUT: Finalizar traspaso (fase 2)
    [HttpPut("{id}/ubicar")]
    public async Task<IActionResult> FinalizarTraspaso(int id, [FromBody] FinalizarTraspasoDto dto)
    {
        var traspaso = await _context.Traspasos.FindAsync(id);

        if (traspaso == null)
            return NotFound();

        if (!string.Equals(traspaso.CodigoEstado, "PENDIENTE", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(traspaso.CodigoEstado, "EN_TRANSITO", StringComparison.OrdinalIgnoreCase))
            return BadRequest("El traspaso no está en un estado válido para ser completado.");

        traspaso.CodigoEstado = "COMPLETADO";
        traspaso.FechaFinalizacion = DateTime.UtcNow;
        traspaso.UsuarioFinalizacionId = dto.UsuarioFinalizacionId;
        traspaso.UbicacionDestino = dto.UbicacionDestino;

        await _context.SaveChangesAsync();

        return Ok(new { message = "Traspaso finalizado correctamente" });
    }
	#endregion

	#region GET: Por Id
	[HttpGet("{id:guid}")]
	public async Task<IActionResult> GetTraspasoById(Guid id)
	{
		var traspaso = await _context.Traspasos.FindAsync(id);

		if (traspaso == null)
			return NotFound();

		var dto = new TraspasoDto
		{
			Id = traspaso.Id,
			AlmacenOrigen = traspaso.AlmacenOrigen,
			AlmacenDestino = traspaso.AlmacenDestino,
			CodigoEstado = traspaso.CodigoEstado,
			FechaInicio = traspaso.FechaInicio,
			UsuarioInicioId = traspaso.UsuarioInicioId,
			PaletId = traspaso.PaletId,
			FechaFinalizacion = traspaso.FechaFinalizacion,
			UsuarioFinalizacionId = traspaso.UsuarioFinalizacionId,
			UbicacionDestino = traspaso.UbicacionDestino
		};

		return Ok(dto);
	}


	#endregion

	#region GET: Listado con filtros básicos
	[HttpGet]
	public async Task<IActionResult> GetTraspasos(
		[FromQuery] Guid? paletId = null,
		[FromQuery] string? codigoEstado = null,
		[FromQuery] DateTime? fechaDesde = null,
		[FromQuery] DateTime? fechaHasta = null)
	{
		var q = _context.Traspasos.AsQueryable();

		if (paletId.HasValue)
			q = q.Where(t => t.PaletId == paletId.Value);

		if (!string.IsNullOrWhiteSpace(codigoEstado))
			q = q.Where(t => t.CodigoEstado == codigoEstado);

		if (fechaDesde.HasValue)
			q = q.Where(t => t.FechaInicio >= fechaDesde.Value);

		if (fechaHasta.HasValue)
			q = q.Where(t => t.FechaInicio <= fechaHasta.Value);

		var lista = await q
			.Include(t => t.Palet)
			.OrderByDescending(t => t.FechaInicio)
			.Select(t => new TraspasoDto
			{
				Id = t.Id,
				AlmacenOrigen = t.AlmacenOrigen,
				AlmacenDestino = t.AlmacenDestino,
				CodigoEstado = t.CodigoEstado,
				FechaInicio = t.FechaInicio,
				UsuarioInicioId = t.UsuarioInicioId,
				PaletId = t.PaletId,
				CodigoPalet = t.Palet.Codigo,     // ✅ aquí
				FechaFinalizacion = t.FechaFinalizacion,
				UsuarioFinalizacionId = t.UsuarioFinalizacionId,
				UbicacionDestino = t.UbicacionDestino
			})
			.ToListAsync();


		return Ok(lista);
	}
	#endregion


	#region GET: Estados posibles
	[HttpGet("estados")]
    public async Task<IActionResult> GetEstados()
    {
        var estados = await _context.TipoEstadosTraspaso
            .OrderBy(e => e.CodigoEstado)
            .ToListAsync();

        return Ok(estados);
    }
    #endregion


}
