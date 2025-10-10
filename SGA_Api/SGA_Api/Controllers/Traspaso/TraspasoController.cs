using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Traspasos;
using SGA_Api.Models.Palet;
using SGA_Api.Services;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using SGA_Api.Models.Stock;
using Microsoft.Extensions.Logging;

namespace SGA_Api.Controllers.Traspasos;

[ApiController]
[Route("api/[controller]")]
public class TraspasosController : ControllerBase
{
	private readonly AuroraSgaDbContext _context;
	private readonly ILogger<TraspasosController> _logger;

	public TraspasosController(AuroraSgaDbContext context, ILogger<TraspasosController> logger)
	{
		_context = context;
		_logger = logger;
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
				FechaInicio = DateTime.Now, // Siempre usar la hora del servidor/API
				CodigoEstado = "PENDIENTE",
				EsNotificado = false
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
		traspaso.FechaFinalizacion = DateTime.Now; // Siempre usar la hora del servidor/API
		traspaso.CodigoEstado = "COMPLETADO";

		await _context.SaveChangesAsync();

		// COMENTADO: La finalización la hace un servicio externo, no este Controller
		// La notificación se envía desde TraspasoFinalizacionBackgroundService.cs

		return Ok(new { message = "Traspaso finalizado correctamente", traspaso.Id, traspaso.CodigoEstado });
	}

	/// <summary>
	/// Obtiene información adicional del traspaso para enriquecer la notificación
	/// </summary>
	private async Task<string> ObtenerInformacionAdicionalTraspasoAsync(Guid traspasoId, string? tipoTraspaso)
	{
		try
		{
			// Obtener el traspaso completo
			var traspaso = await _context.Traspasos.FindAsync(traspasoId);
			if (traspaso == null) return "";

			var informacion = new List<string>();

			// Formatear ubicación origen
			string ubicacionOrigen = "";
			if (!string.IsNullOrEmpty(traspaso.AlmacenOrigen) && !string.IsNullOrEmpty(traspaso.UbicacionOrigen) && traspaso.UbicacionOrigen.Trim() != "")
			{
				ubicacionOrigen = $"{traspaso.AlmacenOrigen}-{traspaso.UbicacionOrigen}";
			}
			else if (!string.IsNullOrEmpty(traspaso.UbicacionOrigen) && traspaso.UbicacionOrigen.Trim() != "")
			{
				ubicacionOrigen = traspaso.UbicacionOrigen;
			}
			else if (!string.IsNullOrEmpty(traspaso.AlmacenOrigen))
			{
				ubicacionOrigen = $"{traspaso.AlmacenOrigen}-SinUbicar";
			}

			// Formatear ubicación destino
			string ubicacionDestino = "";
			if (!string.IsNullOrEmpty(traspaso.AlmacenDestino) && !string.IsNullOrEmpty(traspaso.UbicacionDestino) && traspaso.UbicacionDestino.Trim() != "")
			{
				ubicacionDestino = $"{traspaso.AlmacenDestino}-{traspaso.UbicacionDestino}";
			}
			else if (!string.IsNullOrEmpty(traspaso.UbicacionDestino) && traspaso.UbicacionDestino.Trim() != "")
			{
				ubicacionDestino = traspaso.UbicacionDestino;
			}
			else if (!string.IsNullOrEmpty(traspaso.AlmacenDestino))
			{
				ubicacionDestino = $"{traspaso.AlmacenDestino}-SinUbicar";
			}

			// Agregar ubicación formateada
			if (!string.IsNullOrEmpty(ubicacionOrigen) || !string.IsNullOrEmpty(ubicacionDestino))
			{
				informacion.Add($" Ubicación: {ubicacionOrigen} → {ubicacionDestino}");
			}

			// Para traspasos de artículo, obtener cantidad y descripción
			if (tipoTraspaso == "ARTICULO" && !string.IsNullOrEmpty(traspaso.CodigoArticulo))
			{
				var cantidadEncontrada = false;

				// 1. PRIMERO: Buscar en la tabla Traspasos directamente (para artículos sueltos)
				if (traspaso.Cantidad != null && traspaso.Cantidad != 0)
				{
					informacion.Add($" Cantidad: {Math.Abs(traspaso.Cantidad.Value):F4}");
					cantidadEncontrada = true;
				}

				// 2. SEGUNDO: Buscar en TempPaletLineas (para artículos en palets)
				if (!cantidadEncontrada)
				{
					var tempLinea = await _context.TempPaletLineas
						.Where(tl => tl.TraspasoId == traspasoId && tl.CodigoArticulo == traspaso.CodigoArticulo)
						.FirstOrDefaultAsync();

					if (tempLinea != null && tempLinea.Cantidad != 0)
					{
						informacion.Add($" Cantidad: {Math.Abs(tempLinea.Cantidad):F4}");
						cantidadEncontrada = true;
					}
				}

				// 3. TERCERO: Buscar en PaletLineas (para líneas ya consolidadas)
				if (!cantidadEncontrada)
				{
					var paletLinea = await _context.PaletLineas
						.Where(pl => pl.TraspasoId == traspasoId && pl.CodigoArticulo == traspaso.CodigoArticulo)
						.FirstOrDefaultAsync();

					if (paletLinea != null && paletLinea.Cantidad != 0)
					{
						informacion.Add($" Cantidad: {Math.Abs(paletLinea.Cantidad):F4}");
						cantidadEncontrada = true;
					}
				}
			}

			return string.Join("", informacion);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Error al obtener información adicional del traspaso {TraspasoId}", traspasoId);
			return "";
		}
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

		var nombreDict = await _context.vUsuariosConNombre
			.ToDictionaryAsync(x => x.UsuarioId, x => x.NombreOperario);

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
			CodigoPalet = traspaso.CodigoPalet,
			CodigoArticulo = traspaso.CodigoArticulo,
			TipoTraspaso = traspaso.TipoTraspaso,
			Comentarios = traspaso.Comentario
		};

		if (dto.UsuarioInicioId > 0 && nombreDict.TryGetValue(dto.UsuarioInicioId, out var nombreInicio))
			dto.UsuarioInicioNombre = nombreInicio;

		if (dto.UsuarioFinalizacionId.HasValue && nombreDict.TryGetValue(dto.UsuarioFinalizacionId.Value, out var nombreFinalizacion))
			dto.UsuarioFinalizacionNombre = nombreFinalizacion;

		// Usa el PaletId correcto para buscar las líneas
		var paletId = dto.PaletId;

		// Cargar líneas del palet (definitivas)
		var lineas = await _context.PaletLineas
			.Where(pl => pl.PaletId == paletId)
			.Select(pl => new LineaPaletDto
			{
				Id = pl.Id,
				PaletId = pl.PaletId,
				CodigoEmpresa = pl.CodigoEmpresa,
				CodigoArticulo = pl.CodigoArticulo,
				DescripcionArticulo = pl.DescripcionArticulo,
				Cantidad = pl.Cantidad,
				UnidadMedida = pl.UnidadMedida,
				Lote = pl.Lote,
				FechaCaducidad = pl.FechaCaducidad,
				CodigoAlmacen = pl.CodigoAlmacen,
				Ubicacion = pl.Ubicacion,
				UsuarioId = pl.UsuarioId,
				FechaAgregado = pl.FechaAgregado,
				Observaciones = pl.Observaciones
			})
			.ToListAsync();

		// Si no hay líneas, busca en TempPaletLineas
		if (lineas.Count == 0)
		{
			lineas = await _context.TempPaletLineas
				.Where(pl => pl.PaletId == paletId)
				.Select(pl => new LineaPaletDto
				{
					Id = pl.Id,
					PaletId = pl.PaletId,
					CodigoEmpresa = pl.CodigoEmpresa,
					CodigoArticulo = pl.CodigoArticulo,
					DescripcionArticulo = pl.DescripcionArticulo,
					Cantidad = pl.Cantidad,
					UnidadMedida = pl.UnidadMedida,
					Lote = pl.Lote,
					FechaCaducidad = pl.FechaCaducidad,
					CodigoAlmacen = pl.CodigoAlmacen,
					Ubicacion = pl.Ubicacion,
					UsuarioId = pl.UsuarioId,
					FechaAgregado = pl.FechaAgregado,
					Observaciones = pl.Observaciones
				})
				.ToListAsync();
		}
		dto.LineasPalet = lineas;

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
		[FromQuery] int? usuarioId = null,
		[FromQuery] string? codigoPalet = null,
		[FromQuery] string? almacenOrigen = null,
		[FromQuery] string? almacenDestino = null)
	{
		var q = _context.Traspasos.AsQueryable();
		if (paletId.HasValue)
			q = q.Where(t => t.PaletId == paletId.Value);
		if (!string.IsNullOrWhiteSpace(codigoEstado))
			q = q.Where(t => t.CodigoEstado == codigoEstado);
		if (fechaDesde.HasValue)
			q = q.Where(t => t.FechaInicio >= fechaDesde.Value);
		if (fechaHasta.HasValue)
			q = q.Where(t => t.FechaInicio <= fechaHasta.Value.AddDays(1).AddSeconds(-1)); // Incluir todo el día hasta 23:59:59
		if (usuarioId.HasValue)
			q = q.Where(t => t.UsuarioInicioId == usuarioId.Value || t.UsuarioFinalizacionId == usuarioId.Value);
		if (!string.IsNullOrWhiteSpace(codigoPalet))
			q = q.Where(t => t.CodigoPalet.Contains(codigoPalet));
		if (!string.IsNullOrWhiteSpace(almacenOrigen))
			q = q.Where(t => t.AlmacenOrigen == almacenOrigen);
		if (!string.IsNullOrWhiteSpace(almacenDestino))
			q = q.Where(t => t.AlmacenDestino == almacenDestino);

		var nombreDict = await _context.vUsuariosConNombre
			.ToDictionaryAsync(x => x.UsuarioId, x => x.NombreOperario);

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
				CodigoPalet = t.CodigoPalet,
				CodigoArticulo = t.CodigoArticulo,
				TipoTraspaso = t.TipoTraspaso,
				Cantidad = t.Cantidad,
				Comentarios = t.Comentario,
				DescripcionArticulo = null // Se llenará después si es necesario
			})
			.ToListAsync();

		foreach (var traspaso in lista)
		{
			if (traspaso.UsuarioInicioId > 0 && nombreDict.TryGetValue(traspaso.UsuarioInicioId, out var nombreInicio))
				traspaso.UsuarioInicioNombre = nombreInicio;

			// Obtener descripción del artículo desde StockDisponible
			if (!string.IsNullOrWhiteSpace(traspaso.CodigoArticulo))
			{
				var stockInfo = await _context.StockDisponible
					.Where(s => s.CodigoArticulo == traspaso.CodigoArticulo)
					.Select(s => new { s.DescripcionArticulo })
					.FirstOrDefaultAsync();

				if (stockInfo != null)
				{
					traspaso.DescripcionArticulo = stockInfo.DescripcionArticulo;
				}
			}

			// Cargar líneas del palet
			if (traspaso.PaletId != Guid.Empty)
			{
				var lineas = await _context.PaletLineas
					.Where(pl => pl.PaletId == traspaso.PaletId)
					.Select(pl => new LineaPaletDto
					{
						Id = pl.Id,
						CodigoArticulo = pl.CodigoArticulo,
						DescripcionArticulo = pl.DescripcionArticulo,
						Cantidad = pl.Cantidad,
						CodigoAlmacen = pl.CodigoAlmacen,
						Ubicacion = pl.Ubicacion,
						Lote = pl.Lote,
						FechaCaducidad = pl.FechaCaducidad
					})
					.ToListAsync();

				traspaso.LineasPalet = lineas;
			}
		}

		return Ok(lista);
	}

	/// <summary>
	/// Listar traspasos pendientes/asignados a un usuario (para mobility).
	/// </summary>
	[HttpGet("mis-traspasos")]
	public async Task<IActionResult> GetMisTraspasos([FromQuery] int usuarioId)
	{
		var nombreDict = await _context.vUsuariosConNombre
			.ToDictionaryAsync(x => x.UsuarioId, x => x.NombreOperario);

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
				CodigoPalet = t.CodigoPalet,
				CodigoArticulo = t.CodigoArticulo,
				TipoTraspaso = t.TipoTraspaso,
				Cantidad = t.Cantidad,
				Comentarios = t.Comentario
			})
			.ToListAsync();

		foreach (var traspaso in lista)
		{
			if (traspaso.UsuarioInicioId > 0 && nombreDict.TryGetValue(traspaso.UsuarioInicioId, out var nombreInicio))
				traspaso.UsuarioInicioNombre = nombreInicio;

			// Obtener descripción del artículo desde StockDisponible
			if (!string.IsNullOrWhiteSpace(traspaso.CodigoArticulo))
			{
				var stockInfo = await _context.StockDisponible
					.Where(s => s.CodigoArticulo == traspaso.CodigoArticulo)
					.Select(s => new { s.DescripcionArticulo })
					.FirstOrDefaultAsync();

				if (stockInfo != null)
				{
					traspaso.DescripcionArticulo = stockInfo.DescripcionArticulo;
				}
			}

			// Cargar líneas del palet
			if (traspaso.PaletId != Guid.Empty)
			{
				var lineas = await _context.PaletLineas
					.Where(pl => pl.PaletId == traspaso.PaletId)
					.Select(pl => new LineaPaletDto
					{
						Id = pl.Id,
						CodigoArticulo = pl.CodigoArticulo,
						DescripcionArticulo = pl.DescripcionArticulo,
						Cantidad = pl.Cantidad,
						CodigoAlmacen = pl.CodigoAlmacen,
						Ubicacion = pl.Ubicacion,
						Lote = pl.Lote,
						FechaCaducidad = pl.FechaCaducidad
					})
					.ToListAsync();

				traspaso.LineasPalet = lineas;
			}
		}
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
			// Debug: Log para verificar los datos recibidos
			_logger.LogInformation($"DEBUG: Recibido DTO - DescripcionArticulo: '{dto.DescripcionArticulo}', CodigoEmpresa: {dto.CodigoEmpresa}");
			// Validaciones mínimas
			if (string.IsNullOrWhiteSpace(dto.CodigoArticulo))
				return BadRequest("Debe indicar el código de artículo.");
			if (dto.Cantidad == null || dto.Cantidad <= 0)
				return BadRequest("Debe indicar una cantidad válida.");
			if ((dto.Finalizar ?? true) && string.IsNullOrWhiteSpace(dto.AlmacenDestino))
				return BadRequest("Debe indicar el almacén de destino para finalizar el traspaso.");
			// UbicacionDestino puede ser null o vacío (sin ubicar)

			// Comprobación de stock disponible usando la vista vStockDisponible
			var stock = await _context.Set<StockDisponible>()
				.FirstOrDefaultAsync(s =>
					s.CodigoEmpresa == dto.CodigoEmpresa &&
					s.CodigoArticulo == dto.CodigoArticulo &&
					s.CodigoAlmacen == dto.AlmacenOrigen &&
					s.Ubicacion == dto.UbicacionOrigen &&
					s.Partida == dto.Partida);

			if (stock == null)
				return BadRequest("No se encontró stock para el artículo, almacén y ubicación especificados.");

			if (dto.Cantidad > stock.Disponible)
				return BadRequest($"No puedes traspasar más de lo disponible: {stock.Disponible:N2} unidades.");

			// === NUEVA COMPROBACIÓN: ¿El stock está en un palet? ===
			Guid? paletIdOrigen = null;
			string codigoPaletOrigen = null;
			
			var loteDto = dto.Partida?.Trim() ?? "";
			
			// Primero: calcular cuánto stock hay paletizado en esta ubicación
			var stockPaletizado = await _context.PaletLineas
				.Where(pl =>
					pl.CodigoArticulo == dto.CodigoArticulo &&
					pl.CodigoAlmacen.Trim().ToUpper() == dto.AlmacenOrigen.Trim().ToUpper() &&
					pl.Ubicacion.Trim().ToUpper() == dto.UbicacionOrigen.Trim().ToUpper() &&
					(pl.Lote ?? "") == loteDto)
				.SumAsync(pl => pl.Cantidad);
			
			// Segundo: comparar con el stock total disponible
			var stockTotal = stock.Disponible; // Ya lo tenemos de la validación anterior
			
			// SOLO si TODO el stock está paletizado, entonces validar el palet cerrado
			// Si hay stock suelto (stockTotal > stockPaletizado), permitir el traspaso sin validar palets
			if (stockPaletizado > 0 && stockPaletizado >= stockTotal)
			{
				// El stock está completamente paletizado, buscar el palet específico
				var lineaPalet = await _context.PaletLineas
					.FirstOrDefaultAsync(pl =>
						pl.CodigoArticulo == dto.CodigoArticulo &&
						pl.CodigoAlmacen.Trim().ToUpper() == dto.AlmacenOrigen.Trim().ToUpper() &&
						pl.Ubicacion.Trim().ToUpper() == dto.UbicacionOrigen.Trim().ToUpper() &&
						(pl.Lote ?? "") == loteDto);

				if (lineaPalet != null)
				{
					paletIdOrigen = lineaPalet.PaletId;
					var palet = await _context.Palets.FindAsync(lineaPalet.PaletId);
					codigoPaletOrigen = palet?.Codigo;
					
					if (palet != null && palet.Estado != null && palet.Estado.ToUpper() == "CERRADO")
					{
						if (dto.ReabrirSiCerradoOrigen == true)
						{
							// Reabrir palet de ORIGEN
							palet.Estado = "Abierto";
							palet.FechaApertura = DateTime.Now;
							palet.UsuarioAperturaId = dto.UsuarioId;
							palet.FechaCierre = null;
							palet.UsuarioCierreId = null;
							_context.Palets.Update(palet);
							_context.LogPalet.Add(new LogPalet
							{
								PaletId = palet.Id,
								Fecha = DateTime.Now,
								IdUsuario = dto.UsuarioId,
								Accion = "Reabrir",
								Detalle = "Reapertura de palet en ORIGEN desde traspaso de artículo"
							});

							await _context.SaveChangesAsync();
						}
						else
						{
							return BadRequest("No se puede extraer stock de un palet cerrado. Debe abrirlo o habilitar la reapertura automática.");
						}
					}
				}
			}
			// Si stockPaletizado < stockTotal, significa que hay stock suelto disponible
			// En ese caso, NO asociamos paletIdOrigen y permitimos el traspaso libremente
				////// RESTAR la cantidad traspasada de la línea de palet
				////lineaPalet.Cantidad -= dto.Cantidad ?? 0;
				////            if (lineaPalet.Cantidad <= 0)
				////            {
				////                _context.PaletLineas.Remove(lineaPalet);
				////            }
				////            else
				////            {
				////                _context.PaletLineas.Update(lineaPalet);
				////            }
				////            await _context.SaveChangesAsync();
				//// --- RESTAR EN ORIGEN SIN MOVER SU UBICACIÓN ---
				//var idLinea = lineaPalet.Id;
				//var nuevaCant = lineaPalet.Cantidad - (dto.Cantidad ?? 0m);

				//// Soltar del contexto
				//_context.Entry(lineaPalet).State = EntityState.Detached;

				//if (nuevaCant <= 0)
				//{
				//	_context.PaletLineas.Remove(new PaletLinea { Id = idLinea });
				//}
				//else
				//{
				//	var stub = new PaletLinea { Id = idLinea, Cantidad = nuevaCant };

				//	_context.PaletLineas.Attach(stub);
				//	var entry = _context.Entry(stub);
				//	entry.Property(x => x.Cantidad).IsModified = true;
				//	entry.Property(x => x.CodigoAlmacen).IsModified = false;
				//	entry.Property(x => x.Ubicacion).IsModified = false;
				//}

				//await _context.SaveChangesAsync();

				//// 🔸 DESPUÉS DE RESTAR: si el palet de ORIGEN se queda vacío → marcar VACÍADO
				//if (paletIdOrigen.HasValue)
				//{
				//	var quedanLineas = await _context.PaletLineas
				//		.AnyAsync(pl => pl.PaletId == paletIdOrigen.Value);

				//	if (!quedanLineas)
				//	{
				//		var paletOrigen = await _context.Palets.FindAsync(paletIdOrigen.Value);
				//		if (paletOrigen != null && !string.Equals(paletOrigen.Estado, "Vaciado", StringComparison.OrdinalIgnoreCase))
				//		{
				//			paletOrigen.Estado = "Vaciado";

				//			// Si añadiste estos campos, rellénalos; si no, puedes omitirlos o reutilizar FechaCierre/UsuarioCierreId
				//			paletOrigen.FechaVaciado = DateTime.Now;       // <-- si existe
				//			paletOrigen.UsuarioVaciadoId = dto.UsuarioId;  // <-- si existe

				//			// (opcional) también cerrarlo
				//			paletOrigen.FechaCierre = DateTime.Now;
				//			paletOrigen.UsuarioCierreId = dto.UsuarioId;

				//			_context.Palets.Update(paletOrigen);

				//			_context.LogPalet.Add(new LogPalet
				//			{
				//				PaletId = paletOrigen.Id,
				//				Fecha = DateTime.Now,
				//				IdUsuario = dto.UsuarioId,
				//				Accion = "Vaciado",
				//				Detalle = "Palet sin líneas tras traspaso; marcado como Vaciado."
				//			});

				//			await _context.SaveChangesAsync();
				//		}
				//	}
				//}

		// Determinar palet destino: manual (especificado por usuario) o automático (búsqueda)
			Guid? paletIdDestino = null;
			string codigoPaletDestino = null;
		
		// OPCIÓN 1: Usuario especificó manualmente el palet destino
		if (dto.PaletIdDestino.HasValue)
		{
			var paletSeleccionado = await _context.Palets.FindAsync(dto.PaletIdDestino.Value);
			if (paletSeleccionado != null && paletSeleccionado.CodigoEmpresa == dto.CodigoEmpresa)
			{
				paletIdDestino = paletSeleccionado.Id;
				codigoPaletDestino = paletSeleccionado.Codigo;
				
				// Si el palet está cerrado, reabrirlo
				if (string.Equals(paletSeleccionado.Estado, "Cerrado", StringComparison.OrdinalIgnoreCase))
				{
					paletSeleccionado.Estado = "Abierto";
					paletSeleccionado.FechaApertura = DateTime.Now;
					paletSeleccionado.UsuarioAperturaId = dto.UsuarioId;
					paletSeleccionado.FechaCierre = null;
					paletSeleccionado.UsuarioCierreId = null;
					_context.Palets.Update(paletSeleccionado);
					_context.LogPalet.Add(new LogPalet
					{
						PaletId = paletSeleccionado.Id,
						Fecha = DateTime.Now,
						IdUsuario = dto.UsuarioId,
						Accion = "Reabrir",
						Detalle = "Reapertura manual al recibir stock (traspaso de artículo)"
					});
					await _context.SaveChangesAsync();
				}
			}
		}
		// OPCIÓN 2: Búsqueda automática (lógica original)
		else if (!string.IsNullOrWhiteSpace(dto.AlmacenDestino) && !string.IsNullOrWhiteSpace(dto.UbicacionDestino))
			{
				// Buscar palets abiertos en la ubicación destino
				var paletsAbiertos = await (
					from p in _context.Palets
					join l in _context.PaletLineas on p.Id equals l.PaletId
					where p.Estado == "Abierto"
						&& p.CodigoEmpresa == dto.CodigoEmpresa
						&& l.CodigoAlmacen == dto.AlmacenDestino
						&& l.Ubicacion == dto.UbicacionDestino
					select new { p, l }
				).ToListAsync();

				var paletAbiertoEnUbicacion = paletsAbiertos
					.GroupBy(x => new { x.p.Id, x.p.Codigo, x.p.Estado })
					.Select(g => new
					{
						Palet = g.Key,
						UltimaLinea = g.OrderByDescending(x => x.l.FechaAgregado).FirstOrDefault()
					})
					.Where(x => x.UltimaLinea != null && x.UltimaLinea.l.CodigoAlmacen == dto.AlmacenDestino && x.UltimaLinea.l.Ubicacion == dto.UbicacionDestino)
					.Select(x => x.Palet.Id)
					.FirstOrDefault();

				if (paletAbiertoEnUbicacion != Guid.Empty)
				{
					paletIdDestino = paletAbiertoEnUbicacion;
					var palet = await _context.Palets.FindAsync(paletIdDestino);
					codigoPaletDestino = palet?.Codigo;
				}
				else
				{
					// Buscar palets cerrados en la ubicación destino
					var paletsCerrados = await (
						from p in _context.Palets
						join l in _context.PaletLineas on p.Id equals l.PaletId
						where p.Estado == "Cerrado"
							&& p.CodigoEmpresa == dto.CodigoEmpresa
							&& l.CodigoAlmacen == dto.AlmacenDestino
							&& l.Ubicacion == dto.UbicacionDestino
						select new { p, l }
					).ToListAsync();

					var paletCerradoEnUbicacion = paletsCerrados
						.GroupBy(x => new { x.p.Id, x.p.Codigo, x.p.Estado })
						.Select(g => new
						{
							Palet = g.Key,
							UltimaLinea = g.OrderByDescending(x => x.l.FechaAgregado).FirstOrDefault()
						})
						.Where(x => x.UltimaLinea != null && x.UltimaLinea.l.CodigoAlmacen == dto.AlmacenDestino && x.UltimaLinea.l.Ubicacion == dto.UbicacionDestino)
						.Select(x => x.Palet.Id)
						.FirstOrDefault();

					if (paletCerradoEnUbicacion != Guid.Empty)
					{
						var palet = await _context.Palets.FindAsync(paletCerradoEnUbicacion);
						// Reabrir el palet
						palet.Estado = "Abierto";
						palet.FechaApertura = DateTime.Now; // Siempre usar la hora del servidor/API
						palet.UsuarioAperturaId = dto.UsuarioId;
						palet.FechaCierre = null;
						palet.UsuarioCierreId = null;
						_context.Palets.Update(palet);
						_context.LogPalet.Add(new LogPalet
						{       // (opcional)
							PaletId = palet.Id,
							Fecha = DateTime.Now, // Siempre usar la hora del servidor/API
							IdUsuario = dto.UsuarioId,
							Accion = "Reabrir",
							Detalle = "Reapertura automática al recibir stock en DESTINO"
						});

						await _context.SaveChangesAsync();
						paletIdDestino = palet.Id;
						codigoPaletDestino = palet.Codigo;
					}
				}
			}

			var traspaso = new Traspaso
			{
				Id = Guid.NewGuid(),
				AlmacenOrigen = dto.AlmacenOrigen,
				UbicacionOrigen = dto.UbicacionOrigen,
				UsuarioInicioId = dto.UsuarioId,
				FechaInicio = dto.FechaInicio ?? DateTime.Now,
				CodigoArticulo = dto.CodigoArticulo,
				Cantidad = dto.Cantidad,
				TipoTraspaso = "ARTICULO",
				CodigoEstado = (dto.Finalizar ?? true) ? "PENDIENTE_ERP" : "PENDIENTE",
				AlmacenDestino = (dto.Finalizar ?? true) ? dto.AlmacenDestino : null,
				UbicacionDestino = (dto.Finalizar ?? true) ? dto.UbicacionDestino : null,
				FechaFinalizacion = (dto.Finalizar ?? true) ? DateTime.Now : null, // Siempre usar la hora del servidor/API
				UsuarioFinalizacionId = (dto.Finalizar ?? true) ? dto.UsuarioId : null,
				FechaCaducidad = dto.FechaCaducidad,
				Partida = dto.Partida,
				MovPosicionOrigen = Guid.NewGuid(),
				MovPosicionDestino = dto.MovPosicionDestino ?? Guid.Empty,
				CodigoEmpresa = dto.CodigoEmpresa,
				PaletId = paletIdOrigen ?? Guid.Empty, // ASOCIA EL PALET DE ORIGEN SI EXISTE
				CodigoPalet = codigoPaletOrigen, // OPCIONAL, para trazabilidad
				Comentario = dto.Comentario
			};

			_context.Traspasos.Add(traspaso);
			await _context.SaveChangesAsync();

			// --- TEMPORAL NEGATIVA (ORIGEN) ---
			if (paletIdOrigen.HasValue)   // solo si el stock que sacas estaba paletizado
			{
				var tempOrigen = new TempPaletLinea
				{
					PaletId = paletIdOrigen.Value,
					CodigoEmpresa = dto.CodigoEmpresa,
					CodigoArticulo = dto.CodigoArticulo,
					DescripcionArticulo = dto.DescripcionArticulo,
					Cantidad = -(dto.Cantidad ?? 0m),      // << DELTA NEGATIVO
					UnidadMedida = dto.UnidadMedida,
					Lote = dto.Partida,
					FechaCaducidad = dto.FechaCaducidad,
					CodigoAlmacen = dto.AlmacenOrigen,
					Ubicacion = dto.UbicacionOrigen,
					UsuarioId = dto.UsuarioId,
					FechaAgregado = DateTime.Now, // Siempre usar la hora del servidor/API
					Observaciones = "Delta origen (traspaso de artículo)",
					Procesada = false,
					EsHeredada = false,
					TraspasoId = traspaso.Id
				};

				_context.TempPaletLineas.Add(tempOrigen);
				await _context.SaveChangesAsync();
			}

			// Si hay palet destino, agregar línea temporal y consolidar en PaletLineas
			if (paletIdDestino != null)
			{
				var tempLinea = new TempPaletLinea
				{
					PaletId = paletIdDestino.Value,
					CodigoEmpresa = dto.CodigoEmpresa,
					CodigoArticulo = dto.CodigoArticulo,
					DescripcionArticulo = dto.DescripcionArticulo,
					Cantidad = dto.Cantidad ?? 0,
					UnidadMedida = dto.UnidadMedida,
					Lote = dto.Partida,
					FechaCaducidad = dto.FechaCaducidad,
					CodigoAlmacen = dto.AlmacenDestino,
					Ubicacion = dto.UbicacionDestino,
					UsuarioId = dto.UsuarioId,
					FechaAgregado = DateTime.Now, // Siempre usar la hora del servidor/API
					Observaciones = "", // Comentario vacío
					Procesada = false,
					TraspasoId = traspaso.Id // Asociar el Guid del traspaso
				};
				_context.TempPaletLineas.Add(tempLinea);
				await _context.SaveChangesAsync();

				//// Consolidar en PaletLineas (unificar si ya existe)
				//var lineaExistente = await _context.PaletLineas.FirstOrDefaultAsync(pl =>
				//	pl.PaletId == paletIdDestino.Value &&
				//	pl.CodigoArticulo == dto.CodigoArticulo &&
				//	pl.CodigoAlmacen == dto.AlmacenDestino &&
				//	pl.Ubicacion == dto.UbicacionDestino &&
				//	(pl.Lote ?? "") == (dto.Partida ?? ""));

				//if (lineaExistente != null)
				//{
				//	lineaExistente.Cantidad += dto.Cantidad ?? 0;
				//	_context.PaletLineas.Update(lineaExistente);
				//}
				//else
				//{
				//	var nuevaLinea = new PaletLinea
				//	{
				//		PaletId = paletIdDestino.Value,
				//		CodigoEmpresa = dto.CodigoEmpresa,
				//		CodigoArticulo = dto.CodigoArticulo,
				//		DescripcionArticulo = dto.DescripcionArticulo,
				//		Cantidad = dto.Cantidad ?? 0,
				//		UnidadMedida = dto.UnidadMedida,
				//		Lote = dto.Partida,
				//		FechaCaducidad = dto.FechaCaducidad,
				//		CodigoAlmacen = dto.AlmacenDestino,
				//		Ubicacion = dto.UbicacionDestino,
				//		UsuarioId = dto.UsuarioId,
				//		FechaAgregado = DateTime.Now,
				//		Observaciones = ""
				//	};
				//	_context.PaletLineas.Add(nuevaLinea);
				//}
				//await _context.SaveChangesAsync();

				// === INTEGRACIÓN: Asociar el traspaso al palet destino, manteniendo tipo ARTICULO ===
				traspaso.PaletId = paletIdDestino.Value;
				traspaso.CodigoPalet = codigoPaletDestino;
				// NO cambiar traspaso.TipoTraspaso (debe seguir siendo "ARTICULO")
				_context.Traspasos.Update(traspaso);
				await _context.SaveChangesAsync();



			}

			string paletInfo = null;
			if (paletIdDestino != null)
			{
				var palet = await _context.Palets.FindAsync(paletIdDestino);
				if (palet != null)
				{
					if (palet.FechaCierre == null)
					{
						paletInfo = $"Palet abierto detectado en la ubicación destino (ID: {palet.Id}, Código: {palet.Codigo})";
					}
					else
					{
						paletInfo = $"Palet cerrado detectado y reabierto en la ubicación destino (ID: {palet.Id}, Código: {palet.Codigo})";
					}
				}
			}
			else
			{
				paletInfo = "No se ha detectado ningún palet en la ubicación destino. El stock queda sin asociar a palet.";
			}

			return Ok(new { message = "Traspaso de artículo creado correctamente", traspaso.Id, traspaso.CodigoEstado, paletInfo });
		}
		catch (Exception ex)
		{
			return Problem(detail: ex.ToString(), statusCode: 500, title: "Error creando traspaso de artículo");
		}
	}


	[HttpGet("articulo/precheck-finalizar")]
	public async Task<IActionResult> PrecheckFinalizarArticulo(
	[FromQuery] short codigoEmpresa,
	[FromQuery] string almacenDestino,
	[FromQuery] string? ubicacionDestino = null)
	{
		try
		{
			_logger.LogInformation("PrecheckFinalizarArticulo iniciado - CodigoEmpresa: {CodigoEmpresa}, AlmacenDestino: '{AlmacenDestino}', UbicacionDestino: '{UbicacionDestino}'",
				codigoEmpresa, almacenDestino, ubicacionDestino);

			if (string.IsNullOrWhiteSpace(almacenDestino))
				return BadRequest("Debe indicar almacén de destino.");

			// Normalizar ubicación: null o vacío = "sin ubicar" (igual que en FinalizarTraspasoArticulo)
			var ubicacionDestinoNormalizada = string.IsNullOrWhiteSpace(ubicacionDestino) ? "" : ubicacionDestino.Trim();

			var almKey = almacenDestino.Trim().ToUpper();
			var ubiKey = ubicacionDestinoNormalizada.ToUpper();

			_logger.LogInformation("Parámetros normalizados - almKey: '{almKey}', ubiKey: '{ubiKey}'", almKey, ubiKey);

	// CORREGIDO: Buscar palets que ACTUALMENTE tienen líneas en esa ubicación
	// (no por traspasos históricos, sino por dónde están sus líneas ahora)
	var paletsEnUbicacion = await (
		from l in _context.PaletLineas.AsNoTracking()
		join p in _context.Palets.AsNoTracking() on l.PaletId equals p.Id
		where p.CodigoEmpresa == codigoEmpresa
		   && p.Estado != "Vaciado"  // Excluir palets vaciados
		   && (l.CodigoAlmacen ?? "").Trim().ToUpper() == almKey
		   && (l.Ubicacion ?? "").Trim().ToUpper() == ubiKey
		   && l.Cantidad > 0  // Solo líneas con stock positivo
		group l by new { l.PaletId, p.Codigo, p.CodigoGS1, p.Estado, p.FechaApertura, p.FechaCierre } into g
		select new 
		{ 
			PaletId = g.Key.PaletId,
			CodigoPalet = g.Key.Codigo,
			CodigoGS1 = g.Key.CodigoGS1,
			Estado = g.Key.Estado,
			FechaApertura = g.Key.FechaApertura,
			FechaCierre = g.Key.FechaCierre,
			CantidadTotal = g.Sum(x => x.Cantidad)
		}
	).ToListAsync();

		_logger.LogInformation("Palets encontrados ACTUALMENTE en ubicación {Almacen}-{Ubicacion}: {Count}",
			almacenDestino, ubicacionDestinoNormalizada, paletsEnUbicacion.Count);

		if (paletsEnUbicacion.Count == 0)
		{
			_logger.LogInformation("No se encontraron palets en la ubicación especificada");
			return Ok(new { existe = false, palets = new List<object>() });
		}

	// Construir lista de palets con su información
	var paletsList = paletsEnUbicacion.Select(p => new
	{
		paletId = p.PaletId,
		codigoPalet = p.CodigoPalet,
		codigoGS1 = p.CodigoGS1,  // Código GS1 (código de barras)
		estado = p.Estado,
		cerrado = string.Equals(p.Estado ?? "", "CERRADO", StringComparison.OrdinalIgnoreCase),
		fechaApertura = p.FechaApertura,
		fechaCierre = p.FechaCierre,
		cantidadTotal = p.CantidadTotal, // Total de stock en ese palet en esa ubicación
		// Información adicional para mostrar al usuario
		descripcion = string.Equals(p.Estado ?? "", "CERRADO", StringComparison.OrdinalIgnoreCase)
			? $"{p.CodigoPalet} - CERRADO (se reabrirá)"
			: $"{p.CodigoPalet} - ABIERTO"
	}).OrderBy(p => p.codigoPalet).ToList();

		_logger.LogInformation("Palets encontrados en ubicación: {Count} - Códigos: {Codigos}",
			paletsList.Count, string.Join(", ", paletsList.Select(p => p.codigoPalet)));

		// Mantener compatibilidad: devolver el primer palet como "principal"
		var primerPalet = paletsList.First();
		var mensaje = paletsList.Count > 1
			? $"Hay {paletsList.Count} palets en {almacenDestino}-{ubicacionDestinoNormalizada}. Seleccione uno."
			: primerPalet.cerrado
				? $"Hay un palet CERRADO en {almacenDestino}-{ubicacionDestinoNormalizada} (Código: {primerPalet.codigoPalet}). Se abrirá automáticamente."
				: $"Hay un palet ABIERTO en {almacenDestino}-{ubicacionDestinoNormalizada} (Código: {primerPalet.codigoPalet}).";

			return Ok(new
			{
				existe = true,
			// Compatibilidad con código existente (primer palet)
			paletId = primerPalet.paletId,
			codigoPalet = primerPalet.codigoPalet,
			cerrado = primerPalet.cerrado,
			// NUEVO: Lista completa de palets
			cantidadPalets = paletsList.Count,
			palets = paletsList,
			aviso = mensaje
			});
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error en PrecheckFinalizarArticulo - CodigoEmpresa: {CodigoEmpresa}, AlmacenDestino: '{AlmacenDestino}', UbicacionDestino: '{UbicacionDestino}'",
				codigoEmpresa, almacenDestino, ubicacionDestino);
			return Problem(detail: ex.ToString(), statusCode: 500, title: "Error en precheck de finalización");
		}
	}
	/// </summary>
	[HttpPut("articulo/{id}/finalizar")]
	public async Task<IActionResult> FinalizarTraspasoArticulo(Guid id, [FromBody] FinalizarTraspasoArticuloDto dto)
	{
		// 1) Validaciones básicas
		var traspaso = await _context.Traspasos.FindAsync(id);
		if (traspaso == null)
			return NotFound();

		if (!string.Equals(traspaso.TipoTraspaso, "ARTICULO", StringComparison.OrdinalIgnoreCase))
			return BadRequest("El traspaso no es de tipo ARTICULO.");

		if (string.Equals(traspaso.CodigoEstado, "COMPLETADO", StringComparison.OrdinalIgnoreCase))
			return BadRequest("El traspaso ya está finalizado.");

		if (!string.Equals(traspaso.CodigoEstado, "PENDIENTE", StringComparison.OrdinalIgnoreCase))
			return BadRequest("El traspaso no está en estado pendiente.");

		if (string.IsNullOrWhiteSpace(dto.AlmacenDestino))
			return BadRequest("Debe indicar el almacén de destino.");

		// Normalizar ubicación: null o vacío = "sin ubicar" (igual que en Stock)
		var ubicacionDestino = string.IsNullOrWhiteSpace(dto.UbicacionDestino) ? "" : dto.UbicacionDestino.Trim();

		// Normalizamos claves de comparación (pero guardamos el valor limpio, no en mayúsculas)
		var almDestino = dto.AlmacenDestino.Trim();
		var ubiDestino = ubicacionDestino;  // Ya normalizada arriba
		var almKey = almDestino.ToUpper();
		var ubiKey = ubiDestino.ToUpper();

		// ─────────────────────────────────────────────────────────────────────────────
		// NUEVO (mínimo cambio): si hay palet en destino, pedir confirmación SIEMPRE,
		// esté ABIERTO o CERRADO. No tocamos nada si no confirman.
		// ─────────────────────────────────────────────────────────────────────────────
	// CORREGIDO: Buscar palets que ACTUALMENTE tienen líneas en esa ubicación
	// (igual que en PrecheckFinalizarArticulo)
	Guid? paletDestinoIdPre = null;

	if (dto.PaletIdConfirmado.HasValue && dto.PaletIdConfirmado.Value != Guid.Empty)
	{
		// El usuario especificó un palet concreto → usarlo directamente
		paletDestinoIdPre = dto.PaletIdConfirmado.Value;
	}
	else
	{
		// Búsqueda automática: buscar palets que ACTUALMENTE tienen líneas en esa ubicación
		var paletEnDestino = await (
			from l in _context.PaletLineas.AsNoTracking()
			join p in _context.Palets.AsNoTracking() on l.PaletId equals p.Id
			where p.CodigoEmpresa == traspaso.CodigoEmpresa
			   && p.Estado != "Vaciado"
			   && (l.CodigoAlmacen ?? "").Trim().ToUpper() == almKey
			   && (l.Ubicacion ?? "").Trim().ToUpper() == ubiKey
			   && l.Cantidad > 0
			group l by l.PaletId into g
			select g.Key
		).FirstOrDefaultAsync();
		
		paletDestinoIdPre = paletEnDestino != Guid.Empty ? paletEnDestino : null;
	}

		bool hayPaletEnDestino = paletDestinoIdPre.HasValue && paletDestinoIdPre.Value != Guid.Empty;
		if (hayPaletEnDestino)
		{
			var paletPre = await _context.Palets.AsNoTracking()
							.FirstOrDefaultAsync(p => p.Id == paletDestinoIdPre.Value);

			if (paletPre != null && dto.ConfirmarAgregarAPalet != true)
			{
				bool cerrado = string.Equals(paletPre.Estado ?? "", "CERRADO", StringComparison.OrdinalIgnoreCase);
				string estadoTxt = cerrado ? "CERRADO" : "ABIERTO";

				return StatusCode(StatusCodes.Status409Conflict, new
				{
					message = $"Hay un palet {estadoTxt} en {almDestino}-{ubiDestino} (Código: {paletPre.Codigo}). " +
							  $"Si confirmas, el artículo pasará a estar paletizado en ese palet" + (cerrado ? " (y se reabrirá)." : "."),
					requiereConfirmacion = true,
					paletDetectado = true,
					paletCerrado = cerrado,
					paletId = paletPre.Id,
					codigoPalet = paletPre.Codigo,
					almacen = almDestino,
					ubicacion = ubiDestino
				});
			}
		}

		await using var tx = await _context.Database.BeginTransactionAsync();

		// 2) Finalizamos el traspaso (como ya tenías)
		traspaso.AlmacenDestino = almDestino;
		traspaso.UbicacionDestino = ubiDestino;
		traspaso.UsuarioFinalizacionId = dto.UsuarioId;
		traspaso.FechaFinalizacion = DateTime.Now;
		traspaso.MovPosicionDestino = Guid.NewGuid();
		traspaso.CodigoEstado = "PENDIENTE_ERP";

		// ⬇️ CAMBIO: NO vuelvas a consultar Traspasos.
		// Antes tenías un bloque con "Buscar si hay un PALET físico..." que repetía la query.
		// Usa directamente el id ya calculado en el pre-check:

		Guid? paletDestinoId = paletDestinoIdPre;   // ← reutilizado

		string paletInfo;

		if (paletDestinoId.HasValue && paletDestinoId.Value != Guid.Empty)
		{
			// Seguimos igual: cargamos el palet (esta FindAsync es barata) y actualizamos
			var palet = await _context.Palets.FindAsync(paletDestinoId.Value);
			if (palet != null)
			{
				var estadoPalet = (palet.Estado ?? string.Empty).ToUpper();
				if (estadoPalet == "CERRADO")
				{
					palet.Estado = "Abierto";
					palet.FechaApertura = DateTime.Now; // Siempre usar la hora del servidor/API
					palet.UsuarioAperturaId = dto.UsuarioId;
					palet.FechaCierre = null;
					palet.UsuarioCierreId = null;

					_context.Palets.Update(palet);
					_context.LogPalet.Add(new LogPalet
					{
						PaletId = palet.Id,
						Fecha = DateTime.Now, // Siempre usar la hora del servidor/API
						IdUsuario = dto.UsuarioId,
						Accion = "Reabrir",
						Detalle = "Reapertura automática al agregar artículo (finalización mobility)"
					});
				}

				traspaso.PaletId = palet.Id;
				traspaso.CodigoPalet = palet.Codigo;

				// Buscar la descripción del artículo en StockDisponible
				string descripcionArticulo = null;
				if (!string.IsNullOrWhiteSpace(traspaso.CodigoArticulo))
				{
					var stockInfo = await _context.StockDisponible
						.Where(s => s.CodigoArticulo == traspaso.CodigoArticulo)
						.Select(s => s.DescripcionArticulo)
						.FirstOrDefaultAsync();
					descripcionArticulo = stockInfo;
				}

				var tempLineaDestino = new TempPaletLinea
				{
					PaletId = palet.Id,
					CodigoEmpresa = traspaso.CodigoEmpresa,
					CodigoArticulo = traspaso.CodigoArticulo,
					DescripcionArticulo = descripcionArticulo,
					Cantidad = traspaso.Cantidad ?? 0m,
					UnidadMedida = null,
					Lote = traspaso.Partida,
					FechaCaducidad = traspaso.FechaCaducidad,
					CodigoAlmacen = almDestino,
					Ubicacion = ubiDestino,
					UsuarioId = dto.UsuarioId,
					FechaAgregado = DateTime.Now, // Siempre usar la hora del servidor/API
					Observaciones = "Delta destino (finalización mobility)",
					Procesada = false,
					TraspasoId = traspaso.Id,
					EsHeredada = false
				};
				_context.TempPaletLineas.Add(tempLineaDestino);

				paletInfo = $"Palet detectado en destino (Código: {palet.Codigo}). El artículo se ha agregado al palet.";
			}
			else
			{
				paletInfo = "Se detectó un palet por traza de traspasos, pero no existe el registro del palet. El artículo queda sin asociar a palet.";
			}
		}
		else
		{
			paletInfo = "No hay palet en destino. El artículo queda sin asociar a palet.";
		}

		await _context.SaveChangesAsync();
		await tx.CommitAsync();

		return Ok(new
		{
			message = "Traspaso de artículo finalizado correctamente",
			traspaso.Id,
			traspaso.CodigoEstado,
			paletInfo
		});
	}


	//[HttpPut("articulo/{id}/finalizar")]
	//public async Task<IActionResult> FinalizarTraspasoArticulo(Guid id, [FromBody] FinalizarTraspasoArticuloDto dto)
	//{
	//	var traspaso = await _context.Traspasos.FindAsync(id);
	//	if (traspaso == null)
	//		return NotFound();
	//	if (traspaso.TipoTraspaso != "ARTICULO")
	//		return BadRequest("El traspaso no es de tipo ARTICULO.");
	//	if (traspaso.CodigoEstado == "COMPLETADO")
	//		return BadRequest("El traspaso ya está finalizado.");
	//	if (traspaso.CodigoEstado != "PENDIENTE")
	//		return BadRequest("El traspaso no está en estado pendiente.");
	//	if (string.IsNullOrWhiteSpace(dto.AlmacenDestino) || string.IsNullOrWhiteSpace(dto.UbicacionDestino))
	//		return BadRequest("Debe indicar almacén y ubicación de destino.");

	//	traspaso.AlmacenDestino = dto.AlmacenDestino;
	//	traspaso.UbicacionDestino = dto.UbicacionDestino;
	//	traspaso.UsuarioFinalizacionId = dto.UsuarioId;
	//	traspaso.FechaFinalizacion = DateTime.Now;
	//	traspaso.MovPosicionDestino = Guid.NewGuid();
	//	traspaso.CodigoEstado = "PENDIENTE_ERP";

	//	await _context.SaveChangesAsync();

	//	return Ok(new { message = "Traspaso de artículo finalizado correctamente", traspaso.Id });
	//}

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

	/// <summary>
	/// Mover un palet de una ubicación a otra usando la última ubicación destino como origen.
	/// </summary>
	[HttpPost("mover-palet")]
	public async Task<IActionResult> MoverPalet([FromBody] MoverPaletDto dto)
	{
		// 1. Validar que el palet existe y está cerrado
		var palet = await _context.Palets.FindAsync(dto.PaletId);
		if (palet == null)
			return NotFound("Palet no encontrado.");
		if (!string.Equals(palet.Estado, "CERRADO", StringComparison.OrdinalIgnoreCase))
			return BadRequest("El palet debe estar cerrado para poder moverlo.");

		// NUEVA VALIDACIÓN: Impedir mover si hay traspasos pendientes
		var traspasoPendiente = await _context.Traspasos.AnyAsync(
			t => t.PaletId == dto.PaletId && t.CodigoEstado != "COMPLETADO"
		);
		if (traspasoPendiente)
			return BadRequest("No se puede mover el palet porque tiene un traspaso pendiente de completar.");

		// 2. Buscar el último traspaso COMPLETADO para ese palet
		var ultimoTraspaso = await _context.Traspasos
			.Where(t => t.PaletId == dto.PaletId && t.CodigoEstado == "COMPLETADO")
			.OrderByDescending(t => t.FechaFinalizacion)
			.FirstOrDefaultAsync();

		if (ultimoTraspaso == null)
			return BadRequest("No hay traspasos completados para este palet.");

		// 3. Soportar ambos flujos: desktop (todo de una) y mobility (dos fases)
		// Si el cliente envía PENDIENTE_ERP, significa que quiere finalizar inmediatamente
		bool esFinalizado = !string.IsNullOrWhiteSpace(dto.AlmacenDestino)
			&& !string.IsNullOrWhiteSpace(dto.CodigoEstado)
			&& dto.CodigoEstado == "PENDIENTE_ERP";

		// Log temporal para depuración
		_logger.LogInformation($"DEBUG: AlmacenDestino='{dto.AlmacenDestino}', CodigoEstado='{dto.CodigoEstado}', esFinalizado={esFinalizado}");



		// 1. Obtener todas las líneas del palet (solo definitivas)
		var lineas = await _context.PaletLineas
			.Where(l => l.PaletId == dto.PaletId)
			.ToListAsync();

		if (lineas.Count == 0)
			return BadRequest("No hay líneas definitivas para este palet. No se puede mover.");

		var traspasosCreados = new List<Guid>();

		foreach (var linea in lineas)
		{
			var traspasoArticulo = new Traspaso
			{
				Id = Guid.NewGuid(),
				PaletId = dto.PaletId,
				CodigoPalet = dto.CodigoPalet,
				TipoTraspaso = "PALET",
				CodigoEstado = esFinalizado ? "PENDIENTE_ERP" : "PENDIENTE",
				FechaInicio = dto.FechaInicio ?? DateTime.Now, // Siempre usar la hora del servidor/API
				UsuarioInicioId = dto.UsuarioId,
				AlmacenOrigen = ultimoTraspaso.AlmacenDestino,
				AlmacenDestino = dto.AlmacenDestino,
				UbicacionOrigen = ultimoTraspaso.UbicacionDestino ?? "",
				UbicacionDestino = dto.UbicacionDestino,
				FechaFinalizacion = esFinalizado ? DateTime.Now : (DateTime?)null, // Siempre usar la hora del servidor/API
				UsuarioFinalizacionId = esFinalizado ? dto.UsuarioFinalizacionId : (int?)null,
				CodigoEmpresa = dto.CodigoEmpresa,
				CodigoArticulo = linea.CodigoArticulo,
				Cantidad = linea.Cantidad,
				Partida = linea.Lote,
				FechaCaducidad = linea.FechaCaducidad,
				Comentario = dto.Comentario, // Comentarios del usuario para el palet
				EsNotificado = esFinalizado ? true : false // Marcar como notificado si es finalizado
			};
			_context.Traspasos.Add(traspasoArticulo);
			traspasosCreados.Add(traspasoArticulo.Id);

			// Log temporal para depuración
			_logger.LogInformation($"DEBUG: Traspaso creado - ID: {traspasoArticulo.Id}, Estado: {traspasoArticulo.CodigoEstado}, FechaFinalizacion: {traspasoArticulo.FechaFinalizacion}, esFinalizado: {esFinalizado}");



			// Crear la línea temporal asociada al traspaso
			var tempLinea = new TempPaletLinea
			{
				PaletId = dto.PaletId,
				CodigoEmpresa = dto.CodigoEmpresa,
				CodigoArticulo = linea.CodigoArticulo,
				DescripcionArticulo = linea.DescripcionArticulo,
				Cantidad = linea.Cantidad,
				UnidadMedida = linea.UnidadMedida,
				Lote = linea.Lote,
				FechaCaducidad = linea.FechaCaducidad,
				CodigoAlmacen = ultimoTraspaso.AlmacenDestino,
				Ubicacion = ultimoTraspaso.UbicacionDestino ?? "",
				UsuarioId = dto.UsuarioId,
				FechaAgregado = DateTime.Now, // Siempre usar la hora del servidor/API
				Observaciones = linea.Observaciones,
				Procesada = false,
				TraspasoId = traspasoArticulo.Id,
				EsHeredada = true // Marcar como heredada
			};
			_context.TempPaletLineas.Add(tempLinea);
		}

		await _context.SaveChangesAsync();

		return Ok(new { message = esFinalizado ? "Traspasos de palet creados y finalizados correctamente" : "Traspasos de palet creados correctamente", traspasosIds = traspasosCreados });
	}

	/// <summary>

	/// Finaliza TODOS los traspasos (en "PENDIENTE" o "EN_TRANSITO") que pertenezcan

	/// al mismo palet que el traspaso indicado (por traspasoId) o directamente por paletId.

	/// Devuelve la lista de IDs actualizados y el nuevo estado ("PENDIENTE_ERP").

	/// </summary>

	[HttpPut("{id}/finalizar-palet")]

	//public async Task<IActionResult> FinalizarTraspasoPalet(

	//	Guid id,

	//	[FromBody] FinalizarTraspasoPaletDto dto)

	//{

	//	//1.Localizar el traspaso de referencia

	//	var traspaso = await _context.Traspasos.FindAsync(id);

	//	if (traspaso is null)

	//		return NotFound("Traspaso no encontrado.");

	//	//2.Comprobar que está en un estado finalizable

	//	if (traspaso.CodigoEstado == "COMPLETADO")

	//		return BadRequest("El traspaso ya está finalizado.");

	//	if (traspaso.CodigoEstado is not ("PENDIENTE" or "EN_TRANSITO"))

	//		return BadRequest("El traspaso no está en un estado válido para ser finalizado.");

	//	//3.Obtener TODOS los traspasos del mismo palet que sigan pendientes

	//   var traspasosPalet = await _context.Traspasos

	//	   .Where(t => t.PaletId == traspaso.PaletId &&

	//				   (t.CodigoEstado == "PENDIENTE" || t.CodigoEstado == "EN_TRANSITO"))

	//	   .ToListAsync();

	//	if (traspasosPalet.Count == 0)

	//		return BadRequest("No hay traspasos pendientes para este palet.");

	//	//4.Actualizar cada traspaso y marcarlo explícitamente como "Modified"

	//	foreach (var t in traspasosPalet)

	//	{

	//		t.AlmacenDestino = dto.AlmacenDestino;

	//		t.UbicacionDestino = dto.UbicacionDestino;

	//		t.UsuarioFinalizacionId = dto.UsuarioFinalizacionId;

	//		t.FechaFinalizacion = DateTime.Now;

	//		t.CodigoEstado = "PENDIENTE_ERP";

	//		_context.Entry(t).State = EntityState.Modified;   // ← fuerza el UPDATE

	//	}

	//	await _context.SaveChangesAsync();

	//	//5.Respuesta

	//	return Ok(new

	//	{

	//		message = "Traspasos de palet finalizados correctamente",

	//		traspasoIds = traspasosPalet.Select(t => t.Id).ToList(),

	//		nuevoEstado = "PENDIENTE_ERP"

	//	});

	//}



	//[HttpPut("{id}/finalizar-palet")]
	//public async Task<IActionResult> FinalizarTraspasoPalet(Guid id, [FromBody] FinalizarTraspasoPaletDto dto)
	//{
	//	var traspaso = await _context.Traspasos.FindAsync(id);
	//	if (traspaso == null)
	//		return NotFound();

	//	if (traspaso.CodigoEstado == "COMPLETADO")
	//		return BadRequest("El traspaso ya está finalizado.");
	//	if (traspaso.CodigoEstado != "PENDIENTE" && traspaso.CodigoEstado != "EN_TRANSITO")
	//		return BadRequest("El traspaso no está en un estado válido para ser finalizado.");

	//	if (string.IsNullOrWhiteSpace(dto.AlmacenDestino) || string.IsNullOrWhiteSpace(dto.UbicacionDestino))
	//		return BadRequest("Debe indicar almacén y ubicación de destino.");

	//	var alm = dto.AlmacenDestino.Trim();
	//	var ubi = dto.UbicacionDestino.Trim();

	//	// --- Comprobar si la ubicación ya está ocupada por el último COMPLETADO de cualquier palet ---
	//	var lastCompletedPerPalet =
	//		from t in _context.Traspasos
	//		where t.TipoTraspaso == "PALET"
	//&& t.CodigoEstado == "COMPLETADO"
	//&& t.PaletId != null
	//		group t by t.PaletId into g
	//		select new
	//		{
	//			PaletId = g.Key,
	//			FechaUltima = g.Max(x => x.FechaFinalizacion)
	//		};

	//	var ocupada = await (
	//		from t in _context.Traspasos
	//		join ult in lastCompletedPerPalet
	//			on new { t.PaletId, t.FechaFinalizacion }
	//			equals new { ult.PaletId, FechaFinalizacion = ult.FechaUltima }
	//		where t.TipoTraspaso == "PALET"
	//&& t.CodigoEstado == "COMPLETADO"
	//&& t.AlmacenDestino == alm
	//&& t.UbicacionDestino == ubi
	//		select t.Id
	//	).AnyAsync();

	//	if (ocupada)
	//	{
	//		traspaso.CodigoEstado = "CANCELADO";
	//		traspaso.UsuarioFinalizacionId = dto.UsuarioFinalizacionId;
	//		traspaso.FechaFinalizacion = DateTime.Now;
	//		traspaso.Comentario = $"Cancelado automáticamente: ubicación ocupada ({alm}-{ubi}).";

	//		await _context.SaveChangesAsync();
	//		return StatusCode(StatusCodes.Status409Conflict, new
	//		{
	//			message = "Ubicación ocupada por otro palet. Traspaso CANCELADO.",
	//			traspaso.Id,
	//			traspaso.CodigoEstado
	//		});
	//	}

	//	// --- Finalizar normalmente ---
	//	traspaso.AlmacenDestino = alm;
	//	traspaso.UbicacionDestino = ubi;
	//	traspaso.UsuarioFinalizacionId = dto.UsuarioFinalizacionId;
	//	traspaso.FechaFinalizacion = DateTime.Now;
	//	traspaso.CodigoEstado = dto.CodigoEstado; // PENDIENTE_ERP o COMPLETADO

	//	await _context.SaveChangesAsync();
	//	return Ok(new
	//	{
	//		message = "Traspaso de palet finalizado correctamente",
	//		traspaso.Id,
	//		traspaso.CodigoEstado
	//	});
	//}

	/// <summary>
	/// Finaliza TODOS los traspasos (en "PENDIENTE" o "EN_TRANSITO") que pertenezcan
	/// al mismo palet que el traspaso indicado (por traspasoId) o directamente por paletId.
	/// </summary>
	[HttpPut("palet/{paletId}/finalizar")]
	public async Task<IActionResult> FinalizarTraspasoPaletPorPaletId(
		Guid paletId,
		[FromBody] FinalizarTraspasoPaletDto dto)
	{
		return await FinalizarTraspasosDePalet(paletId, dto);
	}

	private async Task<IActionResult> FinalizarTraspasosDePalet(Guid paletId, FinalizarTraspasoPaletDto dto)
	{
		var traspasosPalet = await _context.Traspasos
			.Where(t => t.PaletId == paletId && (t.CodigoEstado == "PENDIENTE" || t.CodigoEstado == "EN_TRANSITO"))
			.ToListAsync();

		if (traspasosPalet.Count == 0)
			return BadRequest("No hay traspasos pendientes para este palet.");

		foreach (var t in traspasosPalet)
		{
			t.AlmacenDestino = dto.AlmacenDestino;
			t.UbicacionDestino = dto.UbicacionDestino;
			t.UsuarioFinalizacionId = dto.UsuarioFinalizacionId;
			t.FechaFinalizacion = DateTime.Now; // Siempre usar la hora del servidor/API
			t.CodigoEstado = "PENDIENTE_ERP";
			_context.Entry(t).State = EntityState.Modified;
		}

		await _context.SaveChangesAsync();

		return Ok(new
		{
			message = "Traspasos de palet finalizados correctamente",
			traspasoIds = traspasosPalet.Select(t => t.Id).ToList(),
			nuevoEstado = "PENDIENTE_ERP"
		});
	}


	[HttpGet("palets-cerrados-movibles")]
	public async Task<IActionResult> GetPaletsCerradosMovibles()
	{
		// 1. Buscar palets cerrados
		var paletsCerrados = await _context.Palets
			.Where(p => p.Estado == "CERRADO")
			.ToListAsync();

		// 2. Buscar traspasos completados agrupados por palet
		var traspasosCompletados = await _context.Traspasos
			.Where(t => t.CodigoEstado == "COMPLETADO" && t.TipoTraspaso == "PALET")
			.OrderByDescending(t => t.FechaFinalizacion)
			.ToListAsync();

		var ultimosTraspasosPorPalet = traspasosCompletados
			.GroupBy(t => t.PaletId)
			.Select(g => g.First())
			.ToDictionary(t => t.PaletId, t => t);

		// 3. Solo palets que tengan al menos un traspaso completado
		var resultado = paletsCerrados
			.Where(p => ultimosTraspasosPorPalet.ContainsKey(p.Id))
			.Select(p => new
			{
				p.Id,
				p.Codigo,
				p.Estado,
				AlmacenOrigen = ultimosTraspasosPorPalet[p.Id].AlmacenDestino ?? "",
				UbicacionOrigen = ultimosTraspasosPorPalet[p.Id].UbicacionDestino ?? "",
				FechaUltimoTraspaso = ultimosTraspasosPorPalet[p.Id].FechaFinalizacion,
				UsuarioUltimoTraspaso = ultimosTraspasosPorPalet[p.Id].UsuarioFinalizacionId
			})
			.ToList();

		return Ok(resultado);
	}

	//[HttpGet("palets-movibles")]
	//public async Task<IActionResult> GetPaletsMovibles()
	//{
	//	// 1. Buscar palets abiertos o cerrados
	//	var paletsMovibles = await _context.Palets
	//		.Where(p => p.Estado == "CERRADO" || p.Estado == "ABIERTO")
	//		.ToListAsync();

	//	// 2. Buscar traspasos completados agrupados por palet
	//	var traspasosCompletados = await _context.Traspasos
	//		.Where(t => t.CodigoEstado == "COMPLETADO")
	//		.OrderByDescending(t => t.FechaFinalizacion)
	//		.ToListAsync();

	//	var ultimosTraspasosPorPalet = traspasosCompletados
	//		.GroupBy(t => t.PaletId)
	//		.Select(g => g.First())
	//		.ToDictionary(t => t.PaletId, t => t);

	//	// 3. Solo palets que tengan al menos un traspaso completado
	//	var resultado = paletsMovibles
	//		.Where(p => ultimosTraspasosPorPalet.ContainsKey(p.Id))
	//		.Select(p => new
	//		{
	//			p.Id,
	//			p.Codigo,
	//			p.Estado,
	//			AlmacenOrigen = ultimosTraspasosPorPalet[p.Id].AlmacenDestino ?? "",
	//			UbicacionOrigen = ultimosTraspasosPorPalet[p.Id].UbicacionDestino ?? "",
	//			FechaUltimoTraspaso = ultimosTraspasosPorPalet[p.Id].FechaFinalizacion,
	//			UsuarioUltimoTraspaso = ultimosTraspasosPorPalet[p.Id].UsuarioFinalizacionId
	//		})
	//		.ToList();

	//	return Ok(resultado);
	//}
	[HttpGet("palets-movibles")]
	public async Task<IActionResult> GetPaletsMovibles()
	{
		// 1. Buscar palets abiertos o cerrados
		var paletsMovibles = await _context.Palets
			.Where(p => p.Estado == "CERRADO" || p.Estado == "ABIERTO")
			.ToListAsync();

		// 2. Buscar traspasos completados agrupados por palet
		var traspasosCompletados = await _context.Traspasos
			.Where(t => t.CodigoEstado == "COMPLETADO" && t.TipoTraspaso == "PALET")
			.OrderByDescending(t => t.FechaFinalizacion)
			.ToListAsync();

		var ultimosTraspasosPorPalet = traspasosCompletados
			.GroupBy(t => t.PaletId)
			.Select(g => g.First())
			.ToDictionary(t => t.PaletId, t => t);

		// 3. Solo palets que tengan al menos un traspaso completado
		var resultado = paletsMovibles
			.Where(p => ultimosTraspasosPorPalet.ContainsKey(p.Id))
			.Select(p => new
			{
				p.Id,
				p.Codigo,
				p.Estado,
				AlmacenOrigen = ultimosTraspasosPorPalet[p.Id].AlmacenDestino ?? "",
				UbicacionOrigen = ultimosTraspasosPorPalet[p.Id].UbicacionDestino ?? "",
				FechaUltimoTraspaso = ultimosTraspasosPorPalet[p.Id].FechaFinalizacion,
				UsuarioUltimoTraspaso = ultimosTraspasosPorPalet[p.Id].UsuarioFinalizacionId
			})
			.ToList();

		return Ok(resultado);
	}

	[HttpGet("estado-usuario")]
	public async Task<IActionResult> GetEstadosTraspasosPorUsuario([FromQuery] int usuarioId)
	{
		var traspasos = await _context.Traspasos
			.Where(t =>
				t.UsuarioInicioId == usuarioId &&
				(t.CodigoEstado == "COMPLETADO" || t.CodigoEstado == "ERROR_ERP") &&
				t.EsNotificado == false
			)
			.ToListAsync();

		// Mapeo a DTO para devolver al cliente
		var resultado = traspasos.Select(t => new
		{
			t.Id,
			t.CodigoEstado,
			t.CodigoPalet,
			t.CodigoArticulo,
			t.Comentario
		}).ToList();

		if (resultado.Any())
		{
			// Marcamos como notificados
			foreach (var t in traspasos)
			{
				t.EsNotificado = true;
			}
			await _context.SaveChangesAsync();
		}

		return Ok(resultado);
	}



	[HttpGet("pendiente-usuario")]
	public async Task<IActionResult> GetTraspasosPendientesPorUsuario([FromQuery] int usuarioId)
	{
		var traspasos = await _context.Traspasos
			.Where(t => t.UsuarioInicioId == usuarioId && t.CodigoEstado == "PENDIENTE")
			.Select(t => new
			{
				t.Id,
				t.CodigoEstado,
				t.TipoTraspaso,
				PaletCerrado = t.TipoTraspaso == "PALET" && t.UbicacionDestino != null,
				PaletId = t.PaletId,
				t.CodigoPalet,
				// NUEVO: Buscar IdLineaOrden usando CodigoPalet → PaletDestino
				IdLineaOrden = _context.OrdenTraspasoLinea
					.Where(otl => otl.PaletDestino == t.CodigoPalet)
					.Select(otl => otl.IdLineaOrdenTraspaso)
					.FirstOrDefault()
			})
			.ToListAsync();

		if (!traspasos.Any())
			return NotFound("No hay traspasos pendientes para este usuario.");

		return Ok(traspasos);
	}
}