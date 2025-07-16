using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Traspasos;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

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

	/// <summary>
	/// Fase 1: Crear traspaso (inicio). Solo requiere datos de origen, usuario y palet.
	/// </summary>
	[HttpPost]
	public async Task<IActionResult> CrearTraspaso([FromBody] CrearTraspasoDto dto)
	{
		try
		{
			// Validar que el palet existe y está en estado válido (ejemplo: Cerrado)
			var palet = await _context.Palets.FindAsync(dto.PaletId);
			if (palet == null)
				return NotFound($"Palet con ID {dto.PaletId} no encontrado.");

			// Validar que no hay otro traspaso abierto para este palet
			var traspasoAbierto = await _context.Traspasos.AnyAsync(t => t.PaletId == dto.PaletId && t.CodigoEstado != "COMPLETADO");
			if (traspasoAbierto)
				return BadRequest("Ya existe un traspaso abierto para este palet.");

			var traspaso = new Traspaso
			{
				Id = Guid.NewGuid(),
				AlmacenOrigen = dto.AlmacenOrigen,
				UbicacionOrigen = dto.UbicacionOrigen,
				UsuarioInicioId = dto.UsuarioInicioId,
				PaletId = dto.PaletId,
				CodigoPalet = dto.CodigoPalet,
				FechaInicio = DateTime.UtcNow,
				CodigoEstado = "PENDIENTE"
			};

			_context.Traspasos.Add(traspaso);
			await _context.SaveChangesAsync();

			return Ok(new { message = "Traspaso creado correctamente", traspaso.Id, traspaso.CodigoEstado });
		}
		catch (Exception ex)
		{
			return Problem(detail: ex.ToString(), statusCode: 500, title: "Error creando traspaso");
		}
	}

	/// <summary>
	/// Fase 2: Finalizar traspaso (entrega). Requiere datos de destino y usuario finalizador.
	/// </summary>
	[HttpPut("{id}/finalizar")]
	public async Task<IActionResult> FinalizarTraspaso(Guid id, [FromBody] FinalizarTraspasoDto dto)
	{
		var traspaso = await _context.Traspasos.FindAsync(id);
		if (traspaso == null)
			return NotFound();

		if (traspaso.CodigoEstado == "COMPLETADO")
			return BadRequest("El traspaso ya está finalizado.");

		if (traspaso.CodigoEstado != "PENDIENTE" && traspaso.CodigoEstado != "EN_TRANSITO")
			return BadRequest("El traspaso no está en un estado válido para ser completado.");

		traspaso.AlmacenDestino = dto.AlmacenDestino;
		traspaso.UbicacionDestino = dto.UbicacionDestino;
		traspaso.UsuarioFinalizacionId = dto.UsuarioFinalizacionId;
		traspaso.FechaFinalizacion = DateTime.UtcNow;
		traspaso.CodigoEstado = "COMPLETADO";

		await _context.SaveChangesAsync();

		return Ok(new { message = "Traspaso finalizado correctamente", traspaso.Id, traspaso.CodigoEstado });
	}

	/// <summary>
	/// Obtener detalle de traspaso por ID.
	/// </summary>
	[HttpGet("{id}")]
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
			UbicacionDestino = traspaso.UbicacionDestino,
			UbicacionOrigen = traspaso.UbicacionOrigen,
			CodigoPalet = traspaso.CodigoPalet
		};
		return Ok(dto);
	}

	/// <summary>
	/// Listar traspasos con filtros (usuario, estado, fechas, etc.).
	/// </summary>
	[HttpGet]
	public async Task<IActionResult> GetTraspasos([
		FromQuery] Guid? paletId = null,
		[FromQuery] string? codigoEstado = null,
		[FromQuery] DateTime? fechaDesde = null,
		[FromQuery] DateTime? fechaHasta = null,
		[FromQuery] int? usuarioId = null)
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
		if (usuarioId.HasValue)
			q = q.Where(t => t.UsuarioInicioId == usuarioId.Value || t.UsuarioFinalizacionId == usuarioId.Value);

		var lista = await q.OrderByDescending(t => t.FechaInicio)
			.Select(t => new TraspasoDto
			{
				Id = t.Id,
				AlmacenOrigen = t.AlmacenOrigen,
				AlmacenDestino = t.AlmacenDestino,
				CodigoEstado = t.CodigoEstado,
				FechaInicio = t.FechaInicio,
				UsuarioInicioId = t.UsuarioInicioId,
				PaletId = t.PaletId,
				FechaFinalizacion = t.FechaFinalizacion,
				UsuarioFinalizacionId = t.UsuarioFinalizacionId,
				UbicacionDestino = t.UbicacionDestino,
				UbicacionOrigen = t.UbicacionOrigen,
				CodigoPalet = t.CodigoPalet
			})
			.ToListAsync();

		return Ok(lista);
	}

	/// <summary>
	/// Listar traspasos pendientes/asignados a un usuario (para mobility).
	/// </summary>
	[HttpGet("mis-traspasos")]
	public async Task<IActionResult> GetMisTraspasos([FromQuery] int usuarioId)
	{
		var lista = await _context.Traspasos
			.Where(t => (t.UsuarioInicioId == usuarioId || t.UsuarioFinalizacionId == usuarioId)
						&& (t.CodigoEstado == "PENDIENTE" || t.CodigoEstado == "EN_TRANSITO"))
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
				FechaFinalizacion = t.FechaFinalizacion,
				UsuarioFinalizacionId = t.UsuarioFinalizacionId,
				UbicacionDestino = t.UbicacionDestino,
				UbicacionOrigen = t.UbicacionOrigen,
				CodigoPalet = t.CodigoPalet
			})
			.ToListAsync();
		return Ok(lista);
	}

	/// <summary>
	/// Catálogo de estados posibles de traspaso.
	/// </summary>
	[HttpGet("estados")]
	public async Task<IActionResult> GetEstados()
	{
		var estados = await _context.TipoEstadosTraspaso
			.OrderBy(e => e.CodigoEstado)
			.ToListAsync();
		return Ok(estados);
	}

    /// <summary>
    /// Crear traspaso de artículo individual (no paletizado). Si 'finalizar' es true o no se indica, se crea como COMPLETADO (escritorio). Si es false, se crea como PENDIENTE (mobility).
    /// </summary>
    [HttpPost("articulo")]
    public async Task<IActionResult> CrearTraspasoArticulo([FromBody] CrearTraspasoArticuloDto dto)
    {
        try
        {
            // Validaciones mínimas
            if (string.IsNullOrWhiteSpace(dto.CodigoArticulo))
                return BadRequest("Debe indicar el código de artículo.");
            if (dto.Cantidad == null || dto.Cantidad <= 0)
                return BadRequest("Debe indicar una cantidad válida.");
            if (string.IsNullOrWhiteSpace(dto.AlmacenOrigen) || dto.UbicacionOrigen == null)
                return BadRequest("Debe indicar almacén y ubicación de origen.");

            // Si es escritorio o se indica finalizar, se requiere destino
            if ((dto.Finalizar ?? true) && (string.IsNullOrWhiteSpace(dto.AlmacenDestino) || string.IsNullOrWhiteSpace(dto.UbicacionDestino)))
                return BadRequest("Debe indicar almacén y ubicación de destino para finalizar el traspaso.");

            var traspaso = new Traspaso
            {
                Id = Guid.NewGuid(),
                AlmacenOrigen = dto.AlmacenOrigen,
                UbicacionOrigen = dto.UbicacionOrigen,
                UsuarioInicioId = dto.UsuarioId,
                FechaInicio = DateTime.UtcNow,
                CodigoArticulo = dto.CodigoArticulo,
                Cantidad = dto.Cantidad,
                TipoTraspaso = "ARTICULO",
                CodigoEstado = "PENDIENTE_ERP",
                AlmacenDestino = (dto.Finalizar ?? true) ? dto.AlmacenDestino : null,
                UbicacionDestino = (dto.Finalizar ?? true) ? dto.UbicacionDestino : null,
                FechaFinalizacion = (dto.Finalizar ?? true) ? DateTime.UtcNow : null,
                UsuarioFinalizacionId = (dto.Finalizar ?? true) ? dto.UsuarioId : null,
                FechaCaducidad = dto.FechaCaducidad,
                Partida = dto.Partida,
                MovPosicionOrigen = dto.MovPosicionOrigen ?? Guid.Empty,
                MovPosicionDestino = dto.MovPosicionDestino ?? Guid.Empty
            };

            _context.Traspasos.Add(traspaso);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Traspaso de artículo creado correctamente", traspaso.Id, traspaso.CodigoEstado });
        }
        catch (Exception ex)
        {
            return Problem(detail: ex.ToString(), statusCode: 500, title: "Error creando traspaso de artículo");
        }
    }

    /// <summary>
    /// Finalizar traspaso de artículo individual (mobility, segunda fase).
    /// </summary>
    [HttpPut("articulo/{id}/finalizar")]
    public async Task<IActionResult> FinalizarTraspasoArticulo(Guid id, [FromBody] FinalizarTraspasoArticuloDto dto)
    {
        var traspaso = await _context.Traspasos.FindAsync(id);
        if (traspaso == null)
            return NotFound();
        if (traspaso.TipoTraspaso != "ARTICULO")
            return BadRequest("El traspaso no es de tipo ARTICULO.");
        if (traspaso.CodigoEstado == "COMPLETADO")
            return BadRequest("El traspaso ya está finalizado.");
        if (traspaso.CodigoEstado != "PENDIENTE")
            return BadRequest("El traspaso no está en estado pendiente.");
        if (string.IsNullOrWhiteSpace(dto.AlmacenDestino) || string.IsNullOrWhiteSpace(dto.UbicacionDestino))
            return BadRequest("Debe indicar almacén y ubicación de destino.");

        traspaso.AlmacenDestino = dto.AlmacenDestino;
        traspaso.UbicacionDestino = dto.UbicacionDestino;
        traspaso.UsuarioFinalizacionId = dto.UsuarioId;
        traspaso.FechaFinalizacion = DateTime.UtcNow;
        traspaso.CodigoEstado = "COMPLETADO";

        await _context.SaveChangesAsync();

        return Ok(new { message = "Traspaso de artículo finalizado correctamente", traspaso.Id });
    }

    /// <summary>
    /// Listar traspasos de artículos individuales (no paletizados).
    /// </summary>
    [HttpGet("articulos")]
    public async Task<IActionResult> GetTraspasosArticulos([
        FromQuery] string? codigoArticulo = null,
        [FromQuery] string? almacenOrigen = null,
        [FromQuery] string? almacenDestino = null,
        [FromQuery] int? usuarioId = null,
        [FromQuery] DateTime? fechaDesde = null,
        [FromQuery] DateTime? fechaHasta = null)
    {
        var q = _context.Traspasos.AsQueryable();
        q = q.Where(t => t.TipoTraspaso == "ARTICULO");
        if (!string.IsNullOrWhiteSpace(codigoArticulo))
            q = q.Where(t => t.CodigoArticulo == codigoArticulo);
        if (!string.IsNullOrWhiteSpace(almacenOrigen))
            q = q.Where(t => t.AlmacenOrigen == almacenOrigen);
        if (!string.IsNullOrWhiteSpace(almacenDestino))
            q = q.Where(t => t.AlmacenDestino == almacenDestino);
        if (usuarioId.HasValue)
            q = q.Where(t => t.UsuarioInicioId == usuarioId);
        if (fechaDesde.HasValue)
            q = q.Where(t => t.FechaInicio >= fechaDesde.Value);
        if (fechaHasta.HasValue)
            q = q.Where(t => t.FechaInicio <= fechaHasta.Value);

        var lista = await q.OrderByDescending(t => t.FechaInicio)
            .Select(t => new TraspasoArticuloDto
            {
                Id = t.Id,
                AlmacenOrigen = t.AlmacenOrigen,
                UbicacionOrigen = t.UbicacionOrigen,
                AlmacenDestino = t.AlmacenDestino,
                UbicacionDestino = t.UbicacionDestino,
                UsuarioId = t.UsuarioInicioId,
                Fecha = t.FechaInicio,
                CodigoArticulo = t.CodigoArticulo,
                Cantidad = t.Cantidad ?? 0,
                Estado = t.CodigoEstado
            })
            .ToListAsync();

        return Ok(lista);
    }
}
