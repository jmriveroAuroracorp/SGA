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
using SGA_Api.Models.Registro;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace SGA_Api.Controllers.Traspasos;

[ApiController]
[Route("api/[controller]")]
public class TraspasosController : ControllerBase
{
	private readonly AuroraSgaDbContext _context;
	private readonly StorageControlDbContext _storageContext;
	private readonly SageDbContext _sageContext;
	private readonly ILogger<TraspasosController> _logger;
	private readonly IValidacionTraspasoService _validacionService;
	private readonly ICalidadService _calidadService;
	private readonly IServiceProvider _serviceProvider;

	public TraspasosController(
		AuroraSgaDbContext context,
		StorageControlDbContext storageContext,
		SageDbContext sageContext,
		ILogger<TraspasosController> logger,
		IValidacionTraspasoService validacionService,
		ICalidadService calidadService,
		IServiceProvider serviceProvider)
	{
		_context = context;
		_storageContext = storageContext;
		_sageContext = sageContext;
		_logger = logger;
		_validacionService = validacionService;
		_calidadService = calidadService;
		_serviceProvider = serviceProvider;
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
				EsNotificado = false,
				OrigenTraspaso = "AuroraSGA"
			};

			_context.Traspasos.Add(traspaso);
			await _context.SaveChangesAsync();

			var detalleCreacion = $"TraspasoId={traspaso.Id}, PaletId={traspaso.PaletId}, UsuarioInicio={traspaso.UsuarioInicioId}, AlmacenOrigen={traspaso.AlmacenOrigen}, UbicacionOrigen={traspaso.UbicacionOrigen}";
			RegistrarEventoTraspasoAsync(
				"TRASPASO_CREACION",
				"TraspasosController/CrearTraspaso",
				"Traspaso de palet creado",
				detalleCreacion);

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

		var detalleFinalizacion = $"TraspasoId={traspaso.Id}, PaletId={traspaso.PaletId}, UsuarioFinalizacion={traspaso.UsuarioFinalizacionId}, AlmacenDestino={traspaso.AlmacenDestino}, UbicacionDestino={traspaso.UbicacionDestino}";
		RegistrarEventoTraspasoAsync(
			"TRASPASO_FINALIZACION",
			"TraspasosController/FinalizarTraspaso",
			"Traspaso de palet finalizado",
			detalleFinalizacion);

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
			Comentarios = traspaso.Comentario,
			Partida = traspaso.Partida,
			FechaCaducidad = traspaso.FechaCaducidad
		};

		if (string.Equals(dto.TipoTraspaso, "PALET", StringComparison.OrdinalIgnoreCase))
		{
			var paletInfo = await _context.Palets
				.Where(p => p.Id == traspaso.PaletId)
				.Select(p => new { p.OrdenTrabajoId })
				.FirstOrDefaultAsync();

			if (paletInfo != null)
			{
				dto.OrdenTrabajoId = paletInfo.OrdenTrabajoId;

				if (!string.IsNullOrWhiteSpace(paletInfo.OrdenTrabajoId))
				{
					dto.Comentarios = paletInfo.OrdenTrabajoId;
				}
			}
		}

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
		[FromQuery] string? almacenDestino = null,
		[FromQuery] int? limite = null) // Si es null, usar límite dinámico basado en filtros
	{
		var q = _context.Traspasos.AsQueryable();
		
		// 🚀 OPTIMIZACIÓN: Aplicar filtro de usuario PRIMERO si existe
		// Esto permite que SQL Server use índices de usuario y reduzca significativamente el conjunto de datos
		// antes de aplicar otros filtros más costosos
		if (usuarioId.HasValue)
		{
			q = q.Where(t => t.UsuarioInicioId == usuarioId.Value || t.UsuarioFinalizacionId == usuarioId.Value);
		}
		
		// Filtros de fecha (aplicar después del usuario para optimizar)
		if (fechaDesde.HasValue)
			q = q.Where(t => t.FechaInicio >= fechaDesde.Value);
		if (fechaHasta.HasValue)
			q = q.Where(t => t.FechaInicio <= fechaHasta.Value.AddDays(1).AddSeconds(-1)); // Incluir todo el día hasta 23:59:59
		
		// Otros filtros
		if (paletId.HasValue)
			q = q.Where(t => t.PaletId == paletId.Value);
		if (!string.IsNullOrWhiteSpace(codigoEstado))
			q = q.Where(t => t.CodigoEstado == codigoEstado);
		if (!string.IsNullOrWhiteSpace(codigoPalet))
			q = q.Where(t => t.CodigoPalet.Contains(codigoPalet));
		if (!string.IsNullOrWhiteSpace(almacenOrigen))
			q = q.Where(t => t.AlmacenOrigen == almacenOrigen);
		if (!string.IsNullOrWhiteSpace(almacenDestino))
			q = q.Where(t => t.AlmacenDestino == almacenDestino);

		// 🚀 Calcular límite dinámico si no se especificó
		// Si hay filtro de usuario, aumentar el límite porque el filtro reduce significativamente los resultados
		// Si hay un rango de fechas amplio, también aumentar el límite
		int limiteFinal = limite ?? 5000; // Límite más alto por defecto
		if (usuarioId.HasValue)
		{
			// Cuando se filtra por usuario, los resultados ya están filtrados, así que podemos permitir más
			limiteFinal = Math.Max(limiteFinal, 10000);
		}
		else if (fechaDesde.HasValue && fechaHasta.HasValue)
		{
			var diasRango = (fechaHasta.Value.Date - fechaDesde.Value.Date).Days + 1;
			if (diasRango > 7)
			{
				limiteFinal = Math.Max(limiteFinal, 10000); // 10,000 para rangos grandes sin filtro de usuario
			}
			else if (diasRango > 3)
			{
				limiteFinal = Math.Max(limiteFinal, 5000); // 5,000 para rangos medianos
			}
		}

		var nombreDict = await _context.vUsuariosConNombre
			.ToDictionaryAsync(x => x.UsuarioId, x => x.NombreOperario);

		// 🚀 OPTIMIZACIÓN: Obtener todos los datos en una sola consulta sin subconsultas N+1
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
				Partida = t.Partida,
				FechaCaducidad = t.FechaCaducidad
			})
			.Take(limiteFinal) // 🚀 Límite dinámico basado en filtros
			.ToListAsync();

		// 🚀 OPTIMIZACIÓN: Cargar descripciones de artículos desde la tabla Articulos (más eficiente que vStockDisponible)
		var codigosArticulos = lista
			.Where(t => !string.IsNullOrWhiteSpace(t.CodigoArticulo))
			.Select(t => t.CodigoArticulo!)
			.Distinct()
			.ToList();

		var descripcionesDict = new Dictionary<string, string>();
		if (codigosArticulos.Any())
		{
			// Obtener empresas únicas de los traspasos originales (antes del mapeo a DTO)
			var empresas = await q
				.Where(t => t.CodigoEmpresa > 0)
				.Select(t => t.CodigoEmpresa)
				.Distinct()
				.ToListAsync();

			// Crear HashSet para búsqueda eficiente O(1)
			var codigosArticulosSet = codigosArticulos.ToHashSet();

			// Consultar Articulos por empresa (evita OPENJSON y es mucho más rápido)
			foreach (var empresa in empresas)
			{
				// Cargar todos los artículos de la empresa y filtrar en memoria
				var articulosEmpresa = await _sageContext.Articulos
					.Where(a => a.CodigoEmpresa == empresa)
					.Select(a => new { a.CodigoArticulo, a.DescripcionArticulo })
					.ToListAsync();

				// Filtrar en memoria usando HashSet (muy rápido)
				foreach (var art in articulosEmpresa)
				{
					if (codigosArticulosSet.Contains(art.CodigoArticulo) && 
						!string.IsNullOrWhiteSpace(art.DescripcionArticulo) && 
						!descripcionesDict.ContainsKey(art.CodigoArticulo))
					{
						descripcionesDict[art.CodigoArticulo] = art.DescripcionArticulo;
					}
				}
			}
		}

		// 🚀 OPTIMIZACIÓN: Resolver nombres de usuarios y descripciones en memoria (ya cargados)
		foreach (var traspaso in lista)
		{
			if (traspaso.UsuarioInicioId > 0 && nombreDict.TryGetValue(traspaso.UsuarioInicioId, out var nombreInicio))
				traspaso.UsuarioInicioNombre = nombreInicio;

			if (traspaso.UsuarioFinalizacionId.HasValue && nombreDict.TryGetValue(traspaso.UsuarioFinalizacionId.Value, out var nombreFinalizacion))
				traspaso.UsuarioFinalizacionNombre = nombreFinalizacion;

			if (!string.IsNullOrWhiteSpace(traspaso.CodigoArticulo) && descripcionesDict.TryGetValue(traspaso.CodigoArticulo, out var descripcion))
				traspaso.DescripcionArticulo = descripcion;
		}

		// 🚀 OPTIMIZACIÓN: Cargar datos de palets en una sola consulta (solo si hay palets)
		var paletIds = lista.Where(t => t.PaletId != Guid.Empty).Select(t => t.PaletId).Distinct().ToList();
		if (paletIds.Any())
		{
			var paletOrdenDict = await _context.Palets
				.Where(p => paletIds.Contains(p.Id))
				.Select(p => new { p.Id, p.OrdenTrabajoId })
				.ToDictionaryAsync(p => p.Id, p => p.OrdenTrabajoId);

			foreach (var traspaso in lista.Where(t => string.Equals(t.TipoTraspaso, "PALET", StringComparison.OrdinalIgnoreCase)))
			{
				if (paletOrdenDict.TryGetValue(traspaso.PaletId, out var ordenTrabajoId))
				{
					traspaso.OrdenTrabajoId = ordenTrabajoId;
					if (!string.IsNullOrWhiteSpace(ordenTrabajoId))
					{
						traspaso.Comentarios = ordenTrabajoId;
					}
				}
			}

			var lineasPalets = await _context.PaletLineas
				.Where(pl => paletIds.Contains(pl.PaletId))
				.Select(pl => new LineaPaletDto
				{
					Id = pl.Id,
					PaletId = pl.PaletId,
					CodigoArticulo = pl.CodigoArticulo,
					DescripcionArticulo = pl.DescripcionArticulo,
					Cantidad = pl.Cantidad,
					CodigoAlmacen = pl.CodigoAlmacen,
					Ubicacion = pl.Ubicacion,
					Lote = pl.Lote,
					FechaCaducidad = pl.FechaCaducidad
				})
				.ToListAsync();

			// Agrupar líneas por PaletId
			var lineasPorPalet = lineasPalets.GroupBy(l => l.PaletId).ToDictionary(g => g.Key, g => g.ToList());

			// Asignar líneas a cada traspaso
			foreach (var traspaso in lista.Where(t => t.PaletId != Guid.Empty))
			{
				if (lineasPorPalet.TryGetValue(traspaso.PaletId, out var lineas))
				{
					traspaso.LineasPalet = lineas;
				}
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
				Comentarios = t.Comentario,
				Partida = t.Partida,
				FechaCaducidad = t.FechaCaducidad
			})
			.ToListAsync();

		var paletIds = lista.Where(t => t.PaletId != Guid.Empty).Select(t => t.PaletId).Distinct().ToList();
		if (paletIds.Any())
		{
			var paletOrdenDict = await _context.Palets
				.Where(p => paletIds.Contains(p.Id))
				.Select(p => new { p.Id, p.OrdenTrabajoId })
				.ToDictionaryAsync(p => p.Id, p => p.OrdenTrabajoId);

			foreach (var traspaso in lista.Where(t => string.Equals(t.TipoTraspaso, "PALET", StringComparison.OrdinalIgnoreCase)))
			{
				if (paletOrdenDict.TryGetValue(traspaso.PaletId, out var ordenTrabajoId))
				{
					traspaso.OrdenTrabajoId = ordenTrabajoId;
					if (!string.IsNullOrWhiteSpace(ordenTrabajoId))
					{
						traspaso.Comentarios = ordenTrabajoId;
					}
				}
			}
		}

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
			decimal cantidadDelPalet = 0; // 🔷 Declarado fuera para que esté disponible en ambos casos
			DateTime? fechaCaducidadOrigen = null; // 🔷 CORREGIDO: Guardar FechaCaducidad de la línea del palet encontrada

			var loteDto = dto.Partida?.Trim() ?? "";

			// 🔷 PRIORIDAD 1: Si el usuario especificó un PaletIdOrigen (seleccionó stock desde un palet específico)
			// Usar ese palet directamente sin buscar - esto respeta la selección del usuario
			if (dto.PaletIdOrigen.HasValue && dto.PaletIdOrigen.Value != Guid.Empty)
			{
				paletIdOrigen = dto.PaletIdOrigen.Value;
				var palet = await _context.Palets.FindAsync(paletIdOrigen.Value);
				codigoPaletOrigen = palet?.Codigo;

				// Verificar que el palet tenga stock del artículo especificado y obtener su FechaCaducidad
				var lineaPaletOrigen = await _context.PaletLineas
					.Where(pl =>
						pl.PaletId == paletIdOrigen.Value &&
						pl.CodigoArticulo == dto.CodigoArticulo &&
						pl.CodigoAlmacen.Trim().ToUpper() == dto.AlmacenOrigen.Trim().ToUpper() &&
						pl.Ubicacion.Trim().ToUpper() == dto.UbicacionOrigen.Trim().ToUpper() &&
						(pl.Lote ?? "") == loteDto &&
						pl.Cantidad >= dto.Cantidad)
					.OrderByDescending(pl => pl.Cantidad)
					.ThenByDescending(pl => pl.FechaAgregado)
					.FirstOrDefaultAsync();

				if (lineaPaletOrigen == null)
				{
					return BadRequest($"El palet {codigoPaletOrigen} no tiene suficiente stock del artículo {dto.CodigoArticulo} en la ubicación especificada.");
				}

				// 🔷 CORREGIDO: Guardar la FechaCaducidad de la línea del palet encontrada
				fechaCaducidadOrigen = lineaPaletOrigen.FechaCaducidad;

				// Verificar estado del palet
				if (palet != null && palet.Estado != null && palet.Estado.ToUpper() == "CERRADO")
				{
					if (dto.ReabrirSiCerradoOrigen == true)
					{
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
							Detalle = "Reapertura de palet en ORIGEN desde traspaso de artículo (palet seleccionado por usuario)"
						});
						await _context.SaveChangesAsync();
					}
					else
					{
						return BadRequest($"El palet {codigoPaletOrigen} está cerrado. Debe abrirlo o habilitar la reapertura automática.");
					}
				}

				cantidadDelPalet = dto.Cantidad ?? 0; // Toda la cantidad viene del palet seleccionado por el usuario
				_logger.LogInformation($"✅ Usando palet origen seleccionado por usuario: PaletId={paletIdOrigen}, Codigo={codigoPaletOrigen}, Cantidad={cantidadDelPalet}");
			}
			// 🔷 PRIORIDAD 2: Si no hay PaletIdOrigen especificado, usar lógica automática
			if (!dto.PaletIdOrigen.HasValue || dto.PaletIdOrigen.Value == Guid.Empty)
			{
				// 🔷 LÓGICA MEJORADA: Priorizar stock suelto, usar palets solo si es necesario
				// Esta lógica evita la mezcla confusa de stock suelto y paletizado en la misma ubicación

				var stockPaletizado = await _context.PaletLineas
					.Where(pl =>
						pl.CodigoArticulo == dto.CodigoArticulo &&
						pl.CodigoAlmacen.Trim().ToUpper() == dto.AlmacenOrigen.Trim().ToUpper() &&
						pl.Ubicacion.Trim().ToUpper() == dto.UbicacionOrigen.Trim().ToUpper() &&
						(pl.Lote ?? "") == loteDto)
					.SumAsync(pl => pl.Cantidad);

				var stockTotal = stock.Disponible; // Stock total disponible (suelto + paletizado)
				var stockSuelto = stockTotal - stockPaletizado; // Stock suelto calculado

				_logger.LogInformation($"📊 Stock disponible - Total: {stockTotal}, Paletizado: {stockPaletizado}, Suelto: {stockSuelto}, Cantidad a traspasar: {dto.Cantidad}");

				// PRIORIDAD 1: Si hay suficiente stock suelto, usarlo y NO tocar palets
				if (stockSuelto >= dto.Cantidad)
				{
					// Hay suficiente stock suelto, NO crear línea negativa en palets
					cantidadDelPalet = 0; // No se toma nada del palet
					_logger.LogInformation($"✅ Usando stock SUELTO únicamente: {stockSuelto} >= {dto.Cantidad}");
					// No asignamos paletIdOrigen, el traspaso se hace desde stock suelto
				}
				// PRIORIDAD 2: Si no hay suficiente stock suelto, usar todo el suelto + palets necesarios
				else if (stockSuelto > 0 && stockSuelto < dto.Cantidad)
				{
					// Hay algo de stock suelto pero no suficiente, necesitamos usar palets también
					cantidadDelPalet = (dto.Cantidad ?? 0) - stockSuelto; // Solo esta cantidad viene del palet
					_logger.LogInformation($"⚠️ Stock suelto insuficiente. Usando {stockSuelto} suelto + {cantidadDelPalet} de palets");

					// Buscar palet con cantidad suficiente para cubrir lo faltante
					var lineaPalet = await _context.PaletLineas
						.Where(pl =>
							pl.CodigoArticulo == dto.CodigoArticulo &&
							pl.CodigoAlmacen.Trim().ToUpper() == dto.AlmacenOrigen.Trim().ToUpper() &&
							pl.Ubicacion.Trim().ToUpper() == dto.UbicacionOrigen.Trim().ToUpper() &&
							(pl.Lote ?? "") == loteDto &&
							pl.Cantidad >= cantidadDelPalet) // El palet debe tener al menos lo que falta
						.OrderByDescending(pl => pl.Cantidad)
						.ThenByDescending(pl => pl.FechaAgregado)
						.FirstOrDefaultAsync();

					if (lineaPalet != null)
					{
						paletIdOrigen = lineaPalet.PaletId;
						var palet = await _context.Palets.FindAsync(lineaPalet.PaletId);
						codigoPaletOrigen = palet?.Codigo;
						// 🔷 CORREGIDO: Guardar la FechaCaducidad de la línea del palet encontrada
						fechaCaducidadOrigen = lineaPalet.FechaCaducidad;

						if (palet != null && palet.Estado != null && palet.Estado.ToUpper() == "CERRADO")
						{
							if (dto.ReabrirSiCerradoOrigen == true)
							{
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
									Detalle = "Reapertura de palet en ORIGEN desde traspaso de artículo (complementando stock suelto)"
								});
								await _context.SaveChangesAsync();
							}
							else
							{
								return BadRequest("No se puede extraer stock de un palet cerrado. Debe abrirlo o habilitar la reapertura automática.");
							}
						}

						_logger.LogInformation($"✅ Usando stock mixto: {stockSuelto} suelto + {cantidadDelPalet} del palet {codigoPaletOrigen}");
					}
					else
					{
						_logger.LogWarning($"⚠️ No se encontró palet con cantidad suficiente para complementar: Faltante={cantidadDelPalet}, StockPaletizado={stockPaletizado}");
					}
				}
				// PRIORIDAD 3: Si NO hay stock suelto, usar solo palets
				else if (stockSuelto == 0 && stockPaletizado > 0)
				{
					// Solo hay stock paletizado, buscar palet
					cantidadDelPalet = dto.Cantidad ?? 0; // Toda la cantidad viene del palet
					var lineaPalet = await _context.PaletLineas
						.Where(pl =>
							pl.CodigoArticulo == dto.CodigoArticulo &&
							pl.CodigoAlmacen.Trim().ToUpper() == dto.AlmacenOrigen.Trim().ToUpper() &&
							pl.Ubicacion.Trim().ToUpper() == dto.UbicacionOrigen.Trim().ToUpper() &&
							(pl.Lote ?? "") == loteDto &&
							pl.Cantidad >= dto.Cantidad)
						.OrderByDescending(pl => pl.Cantidad)
						.ThenByDescending(pl => pl.FechaAgregado)
						.FirstOrDefaultAsync();

					if (lineaPalet != null)
					{
						paletIdOrigen = lineaPalet.PaletId;
						var palet = await _context.Palets.FindAsync(lineaPalet.PaletId);
						codigoPaletOrigen = palet?.Codigo;
						// 🔷 CORREGIDO: Guardar la FechaCaducidad de la línea del palet encontrada
						fechaCaducidadOrigen = lineaPalet.FechaCaducidad;

						if (palet != null && palet.Estado != null && palet.Estado.ToUpper() == "CERRADO")
						{
							if (dto.ReabrirSiCerradoOrigen == true)
							{
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

						_logger.LogInformation($"✅ Usando solo stock PALETIZADO del palet {codigoPaletOrigen} (cantidad: {cantidadDelPalet})");
					}
					else
					{
						_logger.LogWarning($"⚠️ No se encontró palet origen con cantidad suficiente: CantidadNecesaria={dto.Cantidad}, StockPaletizado={stockPaletizado}");
					}
				}
			}

			// Determinar palet destino: manual (especificado por usuario) o automático (búsqueda)
			Guid? paletIdDestino = null;
			string codigoPaletDestino = null;

			// 🔷 CORREGIDO: Verificar primero si el usuario eligió dejar suelto
			// Si DejarSuelto == true, NO buscar palets y NO agregar al palet
			if (dto.DejarSuelto == true)
			{
				// Dejar suelto: no hacer nada, paletIdDestino queda null
				_logger.LogInformation($"✅ Usuario eligió dejar suelto. No se buscará palet destino.");
			}
			// OPCIÓN 1: Usuario especificó manualmente el palet destino (y confirmó agregar a palet)
			else if (dto.ConfirmarAgregarAPalet == true && dto.PaletIdDestino.HasValue)
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
			// OPCIÓN 2: Búsqueda automática (lógica original) - solo si NO se eligió dejar suelto
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

			// 🔷 VALIDACIÓN DE BLOQUEOS DE CALIDAD: Si se finaliza inmediatamente, validar antes de crear
			if (dto.Finalizar ?? true)
			{
				if (!string.IsNullOrWhiteSpace(dto.CodigoArticulo) && !string.IsNullOrWhiteSpace(dto.AlmacenDestino))
				{
					var ubicacionDestino = string.IsNullOrWhiteSpace(dto.UbicacionDestino) ? "" : dto.UbicacionDestino.Trim();
					
					_logger.LogInformation("🔍 Validando bloqueo de calidad en CrearTraspasoArticulo - Artículo: {CodigoArticulo}, Partida: {Partida}, Origen: {AlmacenOrigen}-{UbicacionOrigen}, Destino: {AlmacenDestino}-{UbicacionDestino}, Empresa: {CodigoEmpresa}",
						dto.CodigoArticulo, dto.Partida ?? "(sin partida)", dto.AlmacenOrigen ?? "(null)", dto.UbicacionOrigen ?? "(null)", dto.AlmacenDestino, ubicacionDestino, dto.CodigoEmpresa);

					var resultadoValidacion = await _validacionService.ValidarTraspasoArticuloAsync(
						dto.CodigoArticulo,
						dto.AlmacenDestino,
						ubicacionDestino,
						dto.CodigoEmpresa,
						dto.Partida,
						dto.AlmacenOrigen,
						dto.UbicacionOrigen);

					if (!resultadoValidacion.EsValido)
					{
						_logger.LogWarning("🚫 Traspaso bloqueado por calidad en CrearTraspasoArticulo - Artículo: {CodigoArticulo}, Partida: {Partida}, Destino: {AlmacenDestino}-{UbicacionDestino}, Motivo: {MotivoBloqueo}",
							dto.CodigoArticulo, dto.Partida ?? "(sin partida)", dto.AlmacenDestino, ubicacionDestino, resultadoValidacion.MotivoBloqueo);
						return BadRequest(resultadoValidacion.MotivoBloqueo ?? "No se puede realizar el traspaso debido a un bloqueo de calidad.");
					}
				}
			}

		// 🔷 MEJORADO: Usar transacción para garantizar integridad atómica
		// Si algo falla después de crear la línea negativa, todo se revierte automáticamente
		await using var transaction = await _context.Database.BeginTransactionAsync();
		
		try
		{
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
				Comentario = dto.Comentario,
				OrigenTraspaso = "AuroraSGA"
			};

			_context.Traspasos.Add(traspaso);
			await _context.SaveChangesAsync();

			// 🔷 DEBUG: Log después de guardar para verificar que se guardó correctamente (usando Warning para que siempre se vea)
			_logger.LogWarning("✅ Traspaso creado - ID={Id}, Tipo={TipoTraspaso}, Estado={CodigoEstado}, Articulo={CodigoArticulo}, Partida={Partida}, Empresa={CodigoEmpresa}, Cantidad={Cantidad}, Origen={AlmacenOrigen}-{UbicacionOrigen}, DTO.Partida={DtoPartida}",
				traspaso.Id, traspaso.TipoTraspaso, traspaso.CodigoEstado, traspaso.CodigoArticulo ?? "(null)", traspaso.Partida ?? "(null)", traspaso.CodigoEmpresa, traspaso.Cantidad, traspaso.AlmacenOrigen, traspaso.UbicacionOrigen, dto.Partida ?? "(null)");
			
			// 🔷 VALIDACIÓN: Verificar que la partida se guardó correctamente
			if (string.IsNullOrWhiteSpace(traspaso.Partida) && !string.IsNullOrWhiteSpace(dto.Partida))
			{
				_logger.LogError("❌ CRÍTICO: La partida del DTO ({DtoPartida}) no se guardó en el traspaso {Id}", dto.Partida, traspaso.Id);
			}

			// === US-002: Crear línea temporal NEGATIVA para palet origen si existe ===
			_logger.LogInformation($"🔍 DEBUG CrearTraspasoArticulo: paletIdOrigen.HasValue={paletIdOrigen.HasValue}, paletIdOrigen={paletIdOrigen}");
			_logger.LogInformation($"🔍 DEBUG CrearTraspasoArticulo: Cantidad={dto.Cantidad}, AlmacenOrigen={dto.AlmacenOrigen}, UbicacionOrigen={dto.UbicacionOrigen}");

			if (paletIdOrigen.HasValue && cantidadDelPalet > 0)
			{
				_logger.LogInformation($"✅ DEBUG CrearTraspasoArticulo: ENTRANDO en bloque para crear línea temporal NEGATIVA");
				_logger.LogInformation($"✅ DEBUG: Cantidad total traspaso={dto.Cantidad}, Cantidad del palet={cantidadDelPalet}");

				var tempLineaOrigen = new TempPaletLinea
				{
					PaletId = paletIdOrigen.Value,
					CodigoEmpresa = dto.CodigoEmpresa,
					CodigoArticulo = dto.CodigoArticulo,
					DescripcionArticulo = dto.DescripcionArticulo,
					Cantidad = -cantidadDelPalet, // 🔷 CORREGIDO: Solo la cantidad que viene del palet (no toda la del traspaso)
					UnidadMedida = dto.UnidadMedida,
					Lote = dto.Partida,
					FechaCaducidad = fechaCaducidadOrigen ?? dto.FechaCaducidad, // 🔷 CORREGIDO: Usar FechaCaducidad del palet encontrado, si no existe usar la del DTO
					CodigoAlmacen = dto.AlmacenOrigen, // UBICACIÓN ORIGEN
					Ubicacion = dto.UbicacionOrigen,   // UBICACIÓN ORIGEN
					UsuarioId = dto.UsuarioId,
					FechaAgregado = DateTime.Now,
					Observaciones = cantidadDelPalet == dto.Cantidad
						? "Delta negativo origen (traspaso de artículo - solo palet)"
						: $"Delta negativo origen (traspaso de artículo - mixto: {cantidadDelPalet} del palet, {dto.Cantidad - cantidadDelPalet} suelto)",
					Procesada = false,
					TraspasoId = traspaso.Id, // Asociar al mismo traspaso
					EsHeredada = false
				};
			_context.TempPaletLineas.Add(tempLineaOrigen);
			_logger.LogInformation($"✅ Creada línea temporal NEGATIVA para palet origen: PaletId={paletIdOrigen.Value}, Cantidad={tempLineaOrigen.Cantidad} (de {dto.Cantidad} total), Articulo={dto.CodigoArticulo}, FechaCaducidad={tempLineaOrigen.FechaCaducidad?.ToString("yyyy-MM-dd") ?? "null"} (del palet: {fechaCaducidadOrigen?.ToString("yyyy-MM-dd") ?? "null"}, del DTO: {dto.FechaCaducidad?.ToString("yyyy-MM-dd") ?? "null"})");
			// NO guardamos aquí, lo haremos al final de la transacción
			}
			else
			{
				_logger.LogWarning($"⚠️ DEBUG CrearTraspasoArticulo: NO se detectó palet origen, NO se creará línea temporal NEGATIVA");
			}

			// Si hay palet destino, agregar línea temporal POSITIVA
			if (paletIdDestino != null)
			{
				var tempLinea = new TempPaletLinea
				{
					PaletId = paletIdDestino.Value,
					CodigoEmpresa = dto.CodigoEmpresa,
					CodigoArticulo = dto.CodigoArticulo,
					DescripcionArticulo = dto.DescripcionArticulo,
					Cantidad = dto.Cantidad ?? 0, // CANTIDAD POSITIVA para agregar stock
					UnidadMedida = dto.UnidadMedida,
					Lote = dto.Partida,
					FechaCaducidad = dto.FechaCaducidad,
					CodigoAlmacen = dto.AlmacenDestino,
					Ubicacion = dto.UbicacionDestino,
					UsuarioId = dto.UsuarioId,
					FechaAgregado = DateTime.Now, // Siempre usar la hora del servidor/API
					Observaciones = "Delta positivo destino (traspaso de artículo)",
					Procesada = false,
					TraspasoId = traspaso.Id, // Asociar el Guid del traspaso
					EsHeredada = false
				};
			_context.TempPaletLineas.Add(tempLinea);
			// NO guardamos aquí, lo haremos al final de la transacción

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
				// 🔷 CORREGIDO: Mensaje específico cuando el usuario eligió dejar suelto
				if (dto.DejarSuelto == true)
				{
					paletInfo = "El artículo se ha dejado suelto en la ubicación destino (sin paletizar).";
				}
				else
				{
					paletInfo = "No se ha detectado ningún palet en la ubicación destino. El stock queda sin asociar a palet.";
				}
			}

			// 🔷 Guardar todos los cambios juntos al final de la transacción
			await _context.SaveChangesAsync();
			await transaction.CommitAsync();
			
			_logger.LogInformation($"✅ Traspaso {traspaso.Id} creado correctamente con todas sus líneas temporales");

			// 🔷 NUEVO: Copiar bloqueo de calidad si el traspaso se finaliza inmediatamente
			if ((dto.Finalizar ?? true) && !string.IsNullOrWhiteSpace(dto.AlmacenDestino) && !string.IsNullOrWhiteSpace(dto.CodigoArticulo) && !string.IsNullOrWhiteSpace(dto.Partida))
			{
				try
				{
					var copiado = await _calidadService.CopiarBloqueoCalidadAsync(
						dto.CodigoEmpresa,
						dto.CodigoArticulo,
						dto.Partida,
						dto.AlmacenOrigen,
						dto.UbicacionOrigen,
						dto.AlmacenDestino,
						dto.UbicacionDestino);
					
					if (copiado)
					{
						_logger.LogInformation($"✅ Bloqueo de calidad copiado desde {dto.AlmacenOrigen}-{dto.UbicacionOrigen ?? "(sin ubicación)"} a {dto.AlmacenDestino}-{dto.UbicacionDestino ?? "(sin ubicación)"}");
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, $"Error al copiar bloqueo de calidad en CrearTraspasoArticulo - TraspasoId: {traspaso.Id}");
					// No fallar el traspaso si falla la copia del bloqueo
				}
			}
			
			var detalleArticuloCreacion = $"TraspasoId={traspaso.Id}, Articulo={dto.CodigoArticulo}, Cantidad={dto.Cantidad}, AlmacenOrigen={dto.AlmacenOrigen}, UbicacionOrigen={dto.UbicacionOrigen}, AlmacenDestino={(dto.Finalizar ?? true ? dto.AlmacenDestino : "Pendiente")}, PaletOrigen={(paletIdOrigen?.ToString() ?? "Suelto")}, PaletDestino={(paletIdDestino?.ToString() ?? "SinPalet")}, Finalizar={(dto.Finalizar ?? true)}";
			RegistrarEventoTraspasoAsync(
				"TRASPASO_ARTICULO_CREACION",
				"TraspasosController/CrearTraspasoArticulo",
				"Traspaso de artículo creado",
				detalleArticuloCreacion);
			
			return Ok(new { message = "Traspaso de artículo creado correctamente", traspaso.Id, traspaso.CodigoEstado, paletInfo });
		}
		catch (Exception ex)
		{
			// 🔷 Si algo falla, hacer rollback de toda la transacción
			await transaction.RollbackAsync();
			_logger.LogError(ex, "❌ Error creando traspaso de artículo - Se hizo rollback de la transacción");
			return Problem(detail: ex.ToString(), statusCode: 500, title: "Error creando traspaso de artículo");
		}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "❌ Error general creando traspaso de artículo");
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
		_logger.LogInformation("🔍 FinalizarTraspasoArticulo - ID={Id}, DejarSuelto={DejarSuelto}, ConfirmarAgregarAPalet={ConfirmarAgregarAPalet}, PaletIdConfirmado={PaletId}",
			id, dto.DejarSuelto, dto.ConfirmarAgregarAPalet, dto.PaletIdConfirmado);

		// 1) Validaciones básicas
		var traspaso = await _context.Traspasos.FindAsync(id);
		if (traspaso == null)
			return NotFound();

		// 🔷 DEBUG: Log de toda la información del traspaso al finalizar (usando Warning para que siempre se vea)
		_logger.LogWarning("📋 FinalizarTraspasoArticulo - Información del traspaso encontrado: ID={Id}, Tipo={TipoTraspaso}, Estado={CodigoEstado}, Articulo={CodigoArticulo}, Partida={Partida}, Empresa={CodigoEmpresa}, Cantidad={Cantidad}, Origen={AlmacenOrigen}-{UbicacionOrigen}",
			traspaso.Id, traspaso.TipoTraspaso, traspaso.CodigoEstado, traspaso.CodigoArticulo ?? "(null)", traspaso.Partida ?? "(null)", traspaso.CodigoEmpresa, traspaso.Cantidad, traspaso.AlmacenOrigen, traspaso.UbicacionOrigen);
		
		// 🔷 VALIDACIÓN CRÍTICA: Verificar que el traspaso tenga información necesaria
		if (string.IsNullOrWhiteSpace(traspaso.CodigoArticulo))
		{
			_logger.LogError("❌ CRÍTICO: Traspaso {Id} no tiene código de artículo. No se puede validar bloqueo de calidad.", traspaso.Id);
		}
		if (string.IsNullOrWhiteSpace(traspaso.Partida))
		{
			_logger.LogWarning("⚠️ ADVERTENCIA: Traspaso {Id} no tiene partida. La validación de bloqueo de calidad no funcionará correctamente.", traspaso.Id);
		}

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

		// 🔷 VALIDACIÓN DE BLOQUEOS DE CALIDAD: Verificar si el artículo está bloqueado y el destino es PULMÓN
		// IMPORTANTE: Validar siempre que haya código de artículo y ubicación destino (requerido para finalizar)
		if (!string.IsNullOrWhiteSpace(traspaso.CodigoArticulo))
		{
			if (string.IsNullOrWhiteSpace(ubiDestino))
			{
				_logger.LogWarning("⚠️ FinalizarTraspasoArticulo: Ubicación destino vacía para artículo {CodigoArticulo}. No se puede validar bloqueo de calidad.", 
					traspaso.CodigoArticulo);
			}
			else
			{
				_logger.LogWarning("🔍 Validando bloqueo de calidad - Artículo: {CodigoArticulo}, Partida: {Partida}, Origen: {AlmacenOrigen}-{UbicacionOrigen}, Destino: {AlmacenDestino}-{UbicacionDestino}, Empresa: {CodigoEmpresa}",
					traspaso.CodigoArticulo, traspaso.Partida ?? "(sin partida)", traspaso.AlmacenOrigen ?? "(null)", traspaso.UbicacionOrigen ?? "(null)", almDestino, ubiDestino, traspaso.CodigoEmpresa);

				var resultadoValidacion = await _validacionService.ValidarTraspasoArticuloAsync(
					traspaso.CodigoArticulo,
					almDestino,
					ubiDestino,
					traspaso.CodigoEmpresa,
					traspaso.Partida,
					traspaso.AlmacenOrigen,
					traspaso.UbicacionOrigen);

				_logger.LogWarning("🔍 Resultado validación - EsValido: {EsValido}, Motivo: {MotivoBloqueo}",
					resultadoValidacion.EsValido, resultadoValidacion.MotivoBloqueo ?? "(sin motivo)");

				if (!resultadoValidacion.EsValido)
				{
					_logger.LogWarning("🚫 Traspaso bloqueado por calidad - Artículo: {CodigoArticulo}, Partida: {Partida}, Destino: {AlmacenDestino}-{UbicacionDestino}, Motivo: {MotivoBloqueo}",
						traspaso.CodigoArticulo, traspaso.Partida ?? "(sin partida)", almDestino, ubiDestino, resultadoValidacion.MotivoBloqueo);
					return BadRequest(resultadoValidacion.MotivoBloqueo ?? "No se puede realizar el traspaso debido a un bloqueo de calidad.");
				}
			}
		}
		else
		{
			_logger.LogWarning("⚠️ FinalizarTraspasoArticulo: Código de artículo vacío. No se puede validar bloqueo de calidad.");
		}

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
			// Solo buscar palets si el usuario NO decidió dejar suelto
			if (dto.DejarSuelto != true)
			{
				// Búsqueda automática: buscar palets que ACTUALMENTE tienen líneas en esa ubicación
				var paletsEnDestino = await (
					from l in _context.PaletLineas.AsNoTracking()
					join p in _context.Palets.AsNoTracking() on l.PaletId equals p.Id
					where p.CodigoEmpresa == traspaso.CodigoEmpresa
					   && p.Estado != "Vaciado"
					   && (l.CodigoAlmacen ?? "").Trim().ToUpper() == almKey
					   && (l.Ubicacion ?? "").Trim().ToUpper() == ubiKey
					   && l.Cantidad > 0
					group l by l.PaletId into g
					select g.Key
				).ToListAsync();

				// Si hay múltiples palets, devolver error para que el usuario elija
				if (paletsEnDestino.Count > 1)
				{
					return StatusCode(StatusCodes.Status409Conflict, new
					{
						message = $"Hay {paletsEnDestino.Count} palets en {almDestino}-{ubiDestino}. Debe especificar cuál usar.",
						requiereConfirmacion = true,
						paletDetectado = true,
						cantidadPalets = paletsEnDestino.Count,
						palets = paletsEnDestino.Select(p => new { paletId = p }).ToList(),
						almacen = almDestino,
						ubicacion = ubiDestino,
						opciones = new[]
						{
						new {
							tipo = "paletizar",
							descripcion = "Escanear palet para paletizar",
							accion = "EscanearPalet"
						},
						new {
							tipo = "suelto",
							descripcion = "Dejar material suelto en la ubicación (sin paletizar)",
							accion = "DejarSuelto"
						},
						new {
							tipo = "cancelar",
							descripcion = "Cancelar y buscar otra ubicación",
							accion = "Cancelar"
						}
					}
					});
				}

				paletDestinoIdPre = paletsEnDestino.FirstOrDefault();
			}
		}

		bool hayPaletEnDestino = paletDestinoIdPre.HasValue && paletDestinoIdPre.Value != Guid.Empty;
		if (hayPaletEnDestino)
		{
			var paletPre = await _context.Palets.AsNoTracking()
							.FirstOrDefaultAsync(p => p.Id == paletDestinoIdPre.Value);

			if (paletPre != null && dto.ConfirmarAgregarAPalet != true && dto.DejarSuelto != true)
			{
				bool cerrado = string.Equals(paletPre.Estado ?? "", "CERRADO", StringComparison.OrdinalIgnoreCase);
				string estadoTxt = cerrado ? "CERRADO" : "ABIERTO";

				return StatusCode(StatusCodes.Status409Conflict, new
				{
					message = $"Hay un palet {estadoTxt} en {almDestino}-{ubiDestino} (Código: {paletPre.Codigo}). " +
							  $"Elige una opción:",
					requiereConfirmacion = true,
					paletDetectado = true,
					paletCerrado = cerrado,
					paletId = paletPre.Id,
					codigoPalet = paletPre.Codigo,
					almacen = almDestino,
					ubicacion = ubiDestino,
					opciones = new[]
					{
						new {
							tipo = "paletizar",
							descripcion = $"Agregar al palet {paletPre.Codigo}" + (cerrado ? " (se reabrirá)" : ""),
							accion = "ConfirmarAgregarAPalet"
						},
						new {
							tipo = "suelto",
							descripcion = "Dejar material suelto en la ubicación (sin paletizar)",
							accion = "DejarSuelto"
						},
						new {
							tipo = "cancelar",
							descripcion = "Cancelar y buscar otra ubicación",
							accion = "Cancelar"
						}
					}
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

		Guid? paletDestinoId = null;
		string paletInfo;

		// Solo paletizar si el usuario confirmó explícitamente
		if (dto.ConfirmarAgregarAPalet == true && paletDestinoIdPre.HasValue && paletDestinoIdPre.Value != Guid.Empty)
		{
			paletDestinoId = paletDestinoIdPre.Value;
		}

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
		else if (dto.DejarSuelto == true)
		{
			paletInfo = "El artículo se ha dejado suelto en la ubicación (sin paletizar).";
		}
		else
		{
			paletInfo = "No hay palet en destino. El artículo queda sin asociar a palet.";
		}

		await _context.SaveChangesAsync();
		await tx.CommitAsync();

		// 🔷 NUEVO: Copiar bloqueo de calidad al finalizar traspaso
		if (!string.IsNullOrWhiteSpace(traspaso.CodigoArticulo) && !string.IsNullOrWhiteSpace(traspaso.Partida) && !string.IsNullOrWhiteSpace(almDestino))
		{
			try
			{
				var copiado = await _calidadService.CopiarBloqueoCalidadAsync(
					traspaso.CodigoEmpresa,
					traspaso.CodigoArticulo,
					traspaso.Partida,
					traspaso.AlmacenOrigen ?? "",
					traspaso.UbicacionOrigen,
					almDestino,
					ubiDestino);
				
				if (copiado)
				{
					_logger.LogInformation($"✅ Bloqueo de calidad copiado desde {traspaso.AlmacenOrigen}-{traspaso.UbicacionOrigen ?? "(sin ubicación)"} a {almDestino}-{ubiDestino ?? "(sin ubicación)"} en FinalizarTraspasoArticulo");
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error al copiar bloqueo de calidad en FinalizarTraspasoArticulo - TraspasoId: {traspaso.Id}");
				// No fallar el traspaso si falla la copia del bloqueo
			}
		}

		var detalleFinalArticulo = $"TraspasoId={traspaso.Id}, Articulo={traspaso.CodigoArticulo}, Cantidad={traspaso.Cantidad}, AlmacenDestino={almDestino}, UbicacionDestino={ubiDestino}, PaletDestino={(paletDestinoId?.ToString() ?? (dto.DejarSuelto == true ? "Suelto" : "SinPalet"))}, DejarSuelto={dto.DejarSuelto}, ConfirmarPalet={dto.ConfirmarAgregarAPalet}";
		RegistrarEventoTraspasoAsync(
			"TRASPASO_ARTICULO_FINALIZACION",
			"TraspasosController/FinalizarTraspasoArticulo",
			"Traspaso de artículo finalizado",
			detalleFinalArticulo);

		// Enviar notificación cuando el traspaso pasa a PENDIENTE_ERP
		try
		{
			using var scope = _serviceProvider.CreateScope();
			var notificacionesUnificadas = scope.ServiceProvider.GetRequiredService<INotificacionesUnificadasService>();
			
			var estadoAnterior = "PENDIENTE"; // Estado antes de finalizar
			var estadoActual = traspaso.CodigoEstado; // PENDIENTE_ERP
			var usuarioId = traspaso.UsuarioInicioId > 0 ? traspaso.UsuarioInicioId : dto.UsuarioId;
			
			if (usuarioId > 0)
			{
				var ubicacionOrigen = traspaso.UbicacionOrigen ?? "SinUbicar";
				var ubicacionDestinoFinal = ubiDestino ?? "SinUbicar";
				var informacionAdicional = $"Ubicación: {traspaso.AlmacenOrigen}-{ubicacionOrigen} - {almDestino}-{ubicacionDestinoFinal}\nCantidad: {traspaso.Cantidad:F4}";
				
				await notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
					usuarioId,
					"TRASPASO",
					"Traspaso en Proceso",
					$"Traspaso de artículo {traspaso.CodigoArticulo} procesándose\n{informacionAdicional}",
					traspaso.Id,
					estadoAnterior,
					estadoActual,
					"info");
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error al enviar notificación de traspaso finalizado {TraspasoId}", traspaso.Id);
			// No fallar la operación si falla la notificación
		}

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
		try
		{
			_logger.LogInformation($"🚨 DEBUG: EJECUTANDO MoverPalet - PaletId={dto.PaletId}, AlmacenDestino={dto.AlmacenDestino}, UbicacionDestino={dto.UbicacionDestino}, CodigoEstado={dto.CodigoEstado}");

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

			// 2. Buscar el último traspaso COMPLETADO de tipo PALET para ese palet
			var ultimoTraspaso = await _context.Traspasos
				.Where(t => t.PaletId == dto.PaletId
					&& t.CodigoEstado == "COMPLETADO"
					&& t.TipoTraspaso == "PALET")
				.OrderByDescending(t => t.FechaFinalizacion)
				.FirstOrDefaultAsync();

			if (ultimoTraspaso == null)
				return BadRequest("No hay traspasos de palet completados para este palet.");

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

			// 🔷 VALIDACIÓN DE BLOQUEOS DE CALIDAD: Validar cada línea del palet antes de crear traspasos
			var ubicacionDestino = string.IsNullOrWhiteSpace(dto.UbicacionDestino) ? "" : dto.UbicacionDestino.Trim();
			var almacenOrigen = ultimoTraspaso.AlmacenDestino ?? "";
			var ubicacionOrigen = ultimoTraspaso.UbicacionDestino ?? "";

			foreach (var linea in lineas)
			{
				if (!string.IsNullOrWhiteSpace(linea.CodigoArticulo) && !string.IsNullOrWhiteSpace(dto.AlmacenDestino))
				{
					_logger.LogInformation("🔍 Validando bloqueo de calidad en MoverPalet - Artículo: {CodigoArticulo}, Partida: {Partida}, Origen: {AlmacenOrigen}-{UbicacionOrigen}, Destino: {AlmacenDestino}-{UbicacionDestino}, Empresa: {CodigoEmpresa}",
						linea.CodigoArticulo, linea.Lote ?? "(sin partida)", almacenOrigen, ubicacionOrigen, dto.AlmacenDestino, ubicacionDestino, dto.CodigoEmpresa);

					var resultadoValidacion = await _validacionService.ValidarTraspasoArticuloAsync(
						linea.CodigoArticulo,
						dto.AlmacenDestino,
						ubicacionDestino,
						dto.CodigoEmpresa,
						linea.Lote,
						almacenOrigen,
						ubicacionOrigen);

					if (!resultadoValidacion.EsValido)
					{
						_logger.LogWarning("🚫 Traspaso de palet bloqueado por calidad - Artículo: {CodigoArticulo}, Partida: {Partida}, Destino: {AlmacenDestino}-{UbicacionDestino}, Motivo: {MotivoBloqueo}",
							linea.CodigoArticulo, linea.Lote ?? "(sin partida)", dto.AlmacenDestino, ubicacionDestino, resultadoValidacion.MotivoBloqueo);
						return BadRequest($"No se puede mover el palet. {resultadoValidacion.MotivoBloqueo}");
					}
				}
			}

			var traspasosCreados = new List<Guid>();

			var comentarioOrden = !string.IsNullOrWhiteSpace(palet.OrdenTrabajoId)
				? palet.OrdenTrabajoId
				: dto.Comentario;

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
					Comentario = comentarioOrden, // Incluir OrdenTrabajoId del palet o comentario del usuario
					EsNotificado = false, // SIEMPRE false para que el BackgroundService lo procese
					OrigenTraspaso = "AuroraSGA"
				};
				_context.Traspasos.Add(traspasoArticulo);
				traspasosCreados.Add(traspasoArticulo.Id);

				// Log temporal para depuración
				_logger.LogInformation($"DEBUG: Traspaso creado - ID: {traspasoArticulo.Id}, Estado: {traspasoArticulo.CodigoEstado}");

				// === NO CREAR TEMPORALES para movimiento de palet completo ===
				// El BackgroundService moverá las líneas definitivas existentes cambiando su ubicación
				// Las temporales solo se usan para agregar/quitar artículos individuales, NO para mover el palet completo
			}

			await _context.SaveChangesAsync();

			var detalleMoverPalet = $"PaletId={dto.PaletId}, CodigoPalet={dto.CodigoPalet}, AlmacenOrigen={ultimoTraspaso.AlmacenDestino}, UbicacionOrigen={ultimoTraspaso.UbicacionDestino}, AlmacenDestino={dto.AlmacenDestino}, UbicacionDestino={dto.UbicacionDestino}, TraspasosCreados={traspasosCreados.Count}, EstadoFinal={dto.CodigoEstado}, UsuarioId={dto.UsuarioId}";
			RegistrarEventoTraspasoAsync(
				"TRASPASO_PALET_MOVIMIENTO",
				"TraspasosController/MoverPalet",
				esFinalizado ? "Movimiento de palet completado" : "Movimiento de palet iniciado",
				detalleMoverPalet);

			return Ok(new { message = esFinalizado ? "Traspasos de palet creados y finalizados correctamente" : "Traspasos de palet creados correctamente", traspasosIds = traspasosCreados });
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, $"❌ ERROR en MoverPalet: PaletId={dto.PaletId}, UbicacionDestino={dto.UbicacionDestino}");
			return StatusCode(500, $"Error al mover el palet: {ex.Message}");
		}
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

		// 🔷 VALIDACIÓN DE BLOQUEOS DE CALIDAD: Validar cada traspaso antes de finalizarlo
		var ubicacionDestino = string.IsNullOrWhiteSpace(dto.UbicacionDestino) ? "" : dto.UbicacionDestino.Trim();

		foreach (var t in traspasosPalet)
		{
			if (!string.IsNullOrWhiteSpace(t.CodigoArticulo) && !string.IsNullOrWhiteSpace(dto.AlmacenDestino))
			{
				_logger.LogInformation("🔍 Validando bloqueo de calidad en FinalizarTraspasosDePalet - Artículo: {CodigoArticulo}, Partida: {Partida}, Origen: {AlmacenOrigen}-{UbicacionOrigen}, Destino: {AlmacenDestino}-{UbicacionDestino}, Empresa: {CodigoEmpresa}",
					t.CodigoArticulo, t.Partida ?? "(sin partida)", t.AlmacenOrigen ?? "(null)", t.UbicacionOrigen ?? "(null)", dto.AlmacenDestino, ubicacionDestino, t.CodigoEmpresa);

				var resultadoValidacion = await _validacionService.ValidarTraspasoArticuloAsync(
					t.CodigoArticulo,
					dto.AlmacenDestino,
					ubicacionDestino,
					t.CodigoEmpresa,
					t.Partida,
					t.AlmacenOrigen,
					t.UbicacionOrigen);

				if (!resultadoValidacion.EsValido)
				{
					_logger.LogWarning("🚫 Finalización de traspaso de palet bloqueada por calidad - Artículo: {CodigoArticulo}, Partida: {Partida}, Destino: {AlmacenDestino}-{UbicacionDestino}, Motivo: {MotivoBloqueo}",
						t.CodigoArticulo, t.Partida ?? "(sin partida)", dto.AlmacenDestino, ubicacionDestino, resultadoValidacion.MotivoBloqueo);
					return BadRequest($"No se puede finalizar el traspaso del palet. {resultadoValidacion.MotivoBloqueo}");
				}
			}
		}

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

		// 🔷 NUEVO: Copiar bloqueos de calidad para cada traspaso del palet
		foreach (var t in traspasosPalet)
		{
			if (!string.IsNullOrWhiteSpace(t.CodigoArticulo) && !string.IsNullOrWhiteSpace(t.Partida) && !string.IsNullOrWhiteSpace(dto.AlmacenDestino))
			{
				try
				{
					var copiado = await _calidadService.CopiarBloqueoCalidadAsync(
						t.CodigoEmpresa,
						t.CodigoArticulo,
						t.Partida,
						t.AlmacenOrigen ?? "",
						t.UbicacionOrigen,
						dto.AlmacenDestino,
						ubicacionDestino);
					
					if (copiado)
					{
						_logger.LogInformation($"✅ Bloqueo de calidad copiado para artículo {t.CodigoArticulo} (partida {t.Partida}) desde {t.AlmacenOrigen}-{t.UbicacionOrigen ?? "(sin ubicación)"} a {dto.AlmacenDestino}-{ubicacionDestino ?? "(sin ubicación)"} en FinalizarTraspasosDePalet");
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, $"Error al copiar bloqueo de calidad en FinalizarTraspasosDePalet - TraspasoId: {t.Id}, Articulo: {t.CodigoArticulo}");
					// No fallar el traspaso si falla la copia del bloqueo
				}
			}
		}

		var detalleFinalPalet = $"PaletId={paletId}, TraspasosActualizados={traspasosPalet.Count}, UsuarioFinalizacion={dto.UsuarioFinalizacionId}, Destino={dto.AlmacenDestino}-{dto.UbicacionDestino}";
		RegistrarEventoTraspasoAsync(
			"TRASPASO_PALET_FINALIZACION",
			"TraspasosController/FinalizarTraspasoPaletPorPaletId",
			"Finalización masiva de traspasos de palet",
			detalleFinalPalet);

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

	[HttpGet("palets-con-ubicacion")]
	public async Task<IActionResult> GetPaletsConUbicacion([FromQuery] string? paletIds = null)
	{
		// 🔷 OPTIMIZADO: Si se pasan IDs, solo cargar esos (mucho más rápido)
		if (!string.IsNullOrWhiteSpace(paletIds))
		{
			var ids = paletIds.Split(',')
				.Where(id => Guid.TryParse(id.Trim(), out _))
				.Select(id => Guid.Parse(id.Trim()))
				.Distinct()
				.ToList();

			if (ids.Any())
			{
				// Verificar que los palets existen y están en estado válido
				var paletsEspecificos = await _context.Palets
					.Where(p => ids.Contains(p.Id) && (p.Estado == "CERRADO" || p.Estado == "ABIERTO"))
					.Select(p => p.Id)
					.ToListAsync();

				if (!paletsEspecificos.Any())
					return Ok(new List<object>());

				// Buscar solo traspasos de estos palets
				var traspasosCompletadosFiltrados = await _context.Traspasos
					.Where(t => t.CodigoEstado == "COMPLETADO" && 
							   t.TipoTraspaso == "PALET" && 
							   paletsEspecificos.Contains(t.PaletId))
					.OrderByDescending(t => t.FechaFinalizacion)
					.ToListAsync();

				var ultimosTraspasosPorPaletFiltrado = traspasosCompletadosFiltrados
					.GroupBy(t => t.PaletId)
					.Select(g => g.First())
					.ToDictionary(t => t.PaletId, t => t);

				var resultadoFiltrado = paletsEspecificos.Select(id => new
				{
					Id = id,
					AlmacenOrigen = ultimosTraspasosPorPaletFiltrado.ContainsKey(id)
						? ultimosTraspasosPorPaletFiltrado[id].AlmacenDestino ?? ""
						: "",
					UbicacionOrigen = ultimosTraspasosPorPaletFiltrado.ContainsKey(id)
						? ultimosTraspasosPorPaletFiltrado[id].UbicacionDestino ?? ""
						: "",
					FechaUltimoTraspaso = ultimosTraspasosPorPaletFiltrado.ContainsKey(id)
						? ultimosTraspasosPorPaletFiltrado[id].FechaFinalizacion
						: (DateTime?)null,
					UsuarioUltimoTraspaso = ultimosTraspasosPorPaletFiltrado.ContainsKey(id)
						? ultimosTraspasosPorPaletFiltrado[id].UsuarioFinalizacionId
						: (int?)null
				}).ToList();

				return Ok(resultadoFiltrado);
			}
		}

		// 🔷 COMPATIBLE HACIA ATRÁS: Si no se pasan IDs, comportamiento original
		// 1. Buscar todos los palets (abiertos y cerrados)
		var todosLosPalets = await _context.Palets
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

		// 3. Crear resultado con información de ubicación para todos los palets
		var resultado = todosLosPalets
			.Select(p => new
			{
				p.Id,
				p.Codigo,
				p.Estado,
				AlmacenOrigen = ultimosTraspasosPorPalet.ContainsKey(p.Id)
					? ultimosTraspasosPorPalet[p.Id].AlmacenDestino ?? ""
					: "",
				UbicacionOrigen = ultimosTraspasosPorPalet.ContainsKey(p.Id)
					? ultimosTraspasosPorPalet[p.Id].UbicacionDestino ?? ""
					: "",
				FechaUltimoTraspaso = ultimosTraspasosPorPalet.ContainsKey(p.Id)
					? ultimosTraspasosPorPalet[p.Id].FechaFinalizacion
					: (DateTime?)null,
				UsuarioUltimoTraspaso = ultimosTraspasosPorPalet.ContainsKey(p.Id)
					? ultimosTraspasosPorPalet[p.Id].UsuarioFinalizacionId
					: (int?)null
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

	/// <summary>
	/// 🔷 NUEVO: Validar traspaso de artículo individual
	/// </summary>
	/// <param name="request">Datos del traspaso a validar</param>
	/// <returns>Resultado de la validación</returns>
	[HttpGet("test")]
	public IActionResult Test()
	{
		_logger.LogInformation("🔍 Test endpoint llamado");
		return Ok("Test endpoint funcionando");
	}

	[HttpPost("validar-articulo")]
	[ProducesResponseType(typeof(ValidacionTraspasoResult), 200)]
	[ProducesResponseType(typeof(ProblemDetails), 400)]
	[ProducesResponseType(typeof(ProblemDetails), 500)]
	public async Task<IActionResult> ValidarTraspasoArticulo([FromBody] ValidacionTraspasoRequest request)
	{
		try
		{
			_logger.LogInformation("🔍 ValidarTraspasoArticulo recibido - Artículo: {CodigoArticulo}, Almacén: '{AlmacenDestino}', Ubicación: '{UbicacionDestino}', Empresa: {CodigoEmpresa}",
				request.CodigoArticulo, request.AlmacenDestino, request.UbicacionDestino, request.CodigoEmpresa);

			if (string.IsNullOrWhiteSpace(request.CodigoArticulo))
			{
				_logger.LogWarning("❌ Código de artículo vacío");
				return BadRequest("Código de artículo es requerido");
			}

			// 🔷 CORREGIDO: Permitir ubicación vacía (SIN UBICAR)
			// if (string.IsNullOrWhiteSpace(request.UbicacionDestino))
			//     return BadRequest("Ubicación destino es requerida");

			_logger.LogInformation("🔍 Llamando ValidacionTraspasoService...");
			var resultado = await _validacionService.ValidarTraspasoArticuloAsync(
				request.CodigoArticulo,
				request.AlmacenDestino,
				request.UbicacionDestino,
				request.CodigoEmpresa,
				request.Partida,
				request.AlmacenOrigen,
				request.UbicacionOrigen);

			_logger.LogInformation("🔍 Resultado validación - EsValido: {EsValido}, Motivo: {MotivoBloqueo}",
				resultado.EsValido, resultado.MotivoBloqueo);

			return Ok(resultado);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error validando traspaso de artículo {CodigoArticulo} a {AlmacenDestino}-{UbicacionDestino}",
				request.CodigoArticulo, request.AlmacenDestino, request.UbicacionDestino);

			return StatusCode(500, new ProblemDetails
			{
				Title = "Error interno del servidor",
				Detail = "Error validando traspaso de artículo"
			});
		}
	}

	/// <summary>
	/// Obtener traspasos históricos de AURORA (tabla MovimientoStock) agrupados por MovTraspaso
	/// </summary>
	[HttpGet("storagecontrol")]
	public async Task<IActionResult> GetTraspasosStorageControl(
		[FromQuery] DateTime? fechaDesde = null,
		[FromQuery] DateTime? fechaHasta = null,
		[FromQuery] string? almacenOrigen = null,
		[FromQuery] string? almacenDestino = null,
		[FromQuery] string? codigoArticulo = null,
		[FromQuery] string? partida = null)
	{
		try
		{
			var guidEmpty = Guid.Empty;

			_logger.LogInformation($"🔍 Buscando traspasos AURORA (MovimientoStock) - FechaDesde: {fechaDesde}, FechaHasta: {fechaHasta}, AlmacenOrigen: {almacenOrigen}, AlmacenDestino: {almacenDestino}");

			// 1. Obtener TODOS los movimientos de tipo 1 (Entrada) y 2 (Salida) en el rango de fechas
			// NOTA: En AURORA, MovTraspaso puede ser Guid.Empty, así que relacionamos por lógica de negocio
			// IMPORTANTE: MovimientoStock está en la base de datos AURORA, no en AURORA_SGA, así que usamos _sageContext
			// Aumentar timeout para consultas grandes
			_sageContext.Database.SetCommandTimeout(120); // 2 minutos
			
			var query = _sageContext.MovimientoStock
				.Where(m => m.TipoMovimiento == 1 || m.TipoMovimiento == 2);
				// NOTA: Se quitó el filtro CodigoCanal == "0" para mostrar todos los traspasos
				// Esto puede incluir movimientos que no forman traspasos completos (1 salida = 1 entrada)
			
			// Aplicar filtros de fecha (siempre requeridos para evitar cargar toda la tabla)
			// Por defecto solo hoy si no se especifica
			var fechaDesdeFiltro = fechaDesde ?? DateTime.Today;
			var fechaHastaFiltro = fechaHasta ?? DateTime.Today.AddDays(1).AddSeconds(-1);
			
			query = query.Where(m => m.Fecha >= fechaDesdeFiltro && m.Fecha <= fechaHastaFiltro);
			
			// Aplicar filtros opcionales
			if (!string.IsNullOrWhiteSpace(codigoArticulo))
				query = query.Where(m => m.CodigoArticulo == codigoArticulo);
			
			if (!string.IsNullOrWhiteSpace(partida))
				query = query.Where(m => m.Partida == partida);
			
			var movimientos = await query.ToListAsync();

			_logger.LogInformation($"📊 Movimientos encontrados: {movimientos.Count}");

			// 2. Separar movimientos de salida y entrada
			var movimientosSalida = movimientos.Where(m => m.TipoMovimiento == 2).ToList();
			var movimientosEntrada = movimientos.Where(m => m.TipoMovimiento == 1).ToList();
			
			_logger.LogInformation($"📊 Movimientos Salida: {movimientosSalida.Count}, Entrada: {movimientosEntrada.Count}");

			// 3. Relacionar movimientos por lógica de negocio: mismo artículo, misma cantidad, misma partida, fechas cercanas
			// Usar AlmacenContrapartida si está disponible, sino relacionar por almacenes diferentes
			var traspasosCompletos = new List<(Guid MovTraspaso, MovimientoStock Salida, MovimientoStock Entrada)>();
			var movimientosProcesados = new HashSet<Guid>();
			
			foreach (var salida in movimientosSalida)
			{
				if (movimientosProcesados.Contains(salida.MovPosicion))
					continue;
				
				// Aplicar filtro de almacén origen si se especifica
				if (!string.IsNullOrWhiteSpace(almacenOrigen) && salida.CodigoAlmacen != almacenOrigen)
					continue;
				
				// Buscar entrada que coincida
				MovimientoStock? entrada = null;
				
				// Si AlmacenContrapartida está lleno, usarlo para encontrar la entrada
				if (!string.IsNullOrWhiteSpace(salida.AlmacenContrapartida))
				{
					entrada = movimientosEntrada.FirstOrDefault(m => 
						!movimientosProcesados.Contains(m.MovPosicion) &&
						m.CodigoAlmacen == salida.AlmacenContrapartida &&
						m.CodigoArticulo == salida.CodigoArticulo &&
						m.Unidades == salida.Unidades &&
						(m.Partida == salida.Partida || (string.IsNullOrWhiteSpace(m.Partida) && string.IsNullOrWhiteSpace(salida.Partida))) &&
						Math.Abs((m.Fecha - salida.Fecha).TotalHours) <= 24);
				}
				else
				{
					// Si no hay AlmacenContrapartida, relacionar por lógica: mismo artículo, cantidad, partida, almacenes diferentes
					entrada = movimientosEntrada.FirstOrDefault(m => 
						!movimientosProcesados.Contains(m.MovPosicion) &&
						m.CodigoAlmacen != salida.CodigoAlmacen && // Almacenes diferentes
						m.CodigoArticulo == salida.CodigoArticulo &&
						m.Unidades == salida.Unidades &&
						(m.Partida == salida.Partida || (string.IsNullOrWhiteSpace(m.Partida) && string.IsNullOrWhiteSpace(salida.Partida))) &&
						Math.Abs((m.Fecha - salida.Fecha).TotalHours) <= 24);
				}
				
				// Aplicar filtro de almacén destino si se especifica
				if (entrada != null && !string.IsNullOrWhiteSpace(almacenDestino))
				{
					if (entrada.CodigoAlmacen != almacenDestino)
						entrada = null;
				}
				
				if (entrada != null)
				{
					// Usar MovTraspaso de la salida si existe y no es Guid.Empty, sino generar uno nuevo
					var movTraspaso = salida.MovTraspaso != guidEmpty ? salida.MovTraspaso : Guid.NewGuid();
					traspasosCompletos.Add((movTraspaso, salida, entrada));
					movimientosProcesados.Add(salida.MovPosicion);
					movimientosProcesados.Add(entrada.MovPosicion);
				}
			}

			_logger.LogInformation($"📦 Traspasos completos encontrados: {traspasosCompletos.Count}");

			// 4. Identificar movimientos sin pareja (los que no están en movimientosProcesados)
			var salidasSinPareja = movimientosSalida
				.Where(s => !movimientosProcesados.Contains(s.MovPosicion))
				.ToList();
			
			var entradasSinPareja = movimientosEntrada
				.Where(e => !movimientosProcesados.Contains(e.MovPosicion))
				.ToList();
			
			_logger.LogInformation($"📊 Movimientos sin pareja: {salidasSinPareja.Count} salidas, {entradasSinPareja.Count} entradas");

			// 5. Obtener todas las descripciones de artículos en una sola consulta (optimización)
			// Incluir también los movimientos sin pareja para obtener sus descripciones
			var codigosArticulos = traspasosCompletos
				.SelectMany(t => new[] { t.Salida, t.Entrada })
				.Concat(salidasSinPareja)
				.Concat(entradasSinPareja)
				.Where(m => !string.IsNullOrWhiteSpace(m.CodigoArticulo))
				.Select(m => new { m.CodigoEmpresa, m.CodigoArticulo })
				.Distinct()
				.ToList();

			var descripcionesDict = new Dictionary<(short, string), string?>();
			if (codigosArticulos.Any())
			{
				// Crear HashSet de tuplas para búsqueda eficiente O(1)
				var codigosArticulosSet = codigosArticulos
					.Select(c => (c.CodigoEmpresa, c.CodigoArticulo))
					.ToHashSet();

				// Obtener empresas únicas
				var empresas = codigosArticulos.Select(c => c.CodigoEmpresa).Distinct().ToList();

				// Cargar artículos haciendo consultas individuales por empresa (evita completamente OPENJSON)
				var todosArticulos = new List<(short CodigoEmpresa, string CodigoArticulo, string? DescripcionArticulo)>();
				
				foreach (var empresa in empresas)
				{
					// Consulta simple con == (sin Contains, sin OPENJSON)
					var articulosEmpresa = await _sageContext.Articulos
						.Where(a => a.CodigoEmpresa == empresa)
						.Select(a => new { a.CodigoEmpresa, a.CodigoArticulo, a.DescripcionArticulo })
						.ToListAsync();
					
					todosArticulos.AddRange(articulosEmpresa.Select(a => (a.CodigoEmpresa, a.CodigoArticulo, a.DescripcionArticulo)));
				}

				// Filtrar en memoria usando HashSet para búsqueda eficiente O(1)
				foreach (var art in todosArticulos)
				{
					if (codigosArticulosSet.Contains((art.CodigoEmpresa, art.CodigoArticulo)))
					{
						descripcionesDict[(art.CodigoEmpresa, art.CodigoArticulo)] = art.DescripcionArticulo;
					}
				}
			}

			// 6. Construir DTOs para traspasos completos
			var resultado = new List<TraspasoStorageControlDto>();

			foreach (var traspasoCompleto in traspasosCompletos)
			{
				var salida = traspasoCompleto.Salida;
				var entrada = traspasoCompleto.Entrada;

				// Obtener descripción del artículo del diccionario
				string? descripcionArticulo = null;
				if (!string.IsNullOrWhiteSpace(salida.CodigoArticulo))
				{
					var key = (salida.CodigoEmpresa, salida.CodigoArticulo);
					descripcionesDict.TryGetValue(key, out descripcionArticulo);
				}

				var dto = new TraspasoStorageControlDto
				{
					MovTraspaso = traspasoCompleto.MovTraspaso,
					CodigoArticulo = salida.CodigoArticulo,
					DescripcionArticulo = descripcionArticulo,
					Partida = salida.Partida,
					FechaCaducidad = salida.FechaCaduca,
					AlmacenOrigen = salida.CodigoAlmacen, // Origen: almacén de la salida
					UbicacionOrigen = salida.Ubicacion, // Origen: ubicación de la salida
					AlmacenDestino = entrada.CodigoAlmacen, // Destino: almacén de la entrada
					UbicacionDestino = entrada.Ubicacion, // Destino: ubicación de la entrada
					Cantidad = salida.Unidades,
					Fecha = salida.Fecha,
					FechaRegistro = salida.FechaRegistro,
					Comentario = salida.Comentario,
					CodigoEmpresa = salida.CodigoEmpresa,
					Ejercicio = salida.Ejercicio,
					EstadoMovimiento = null // Traspaso completo
				};

				resultado.Add(dto);
			}

			// 7. Agregar salidas sin pareja como registros individuales
			foreach (var salida in salidasSinPareja)
			{
				// Aplicar filtro de almacén origen si se especifica
				if (!string.IsNullOrWhiteSpace(almacenOrigen) && salida.CodigoAlmacen != almacenOrigen)
					continue;

				// Obtener descripción del artículo del diccionario
				string? descripcionArticulo = null;
				if (!string.IsNullOrWhiteSpace(salida.CodigoArticulo))
				{
					var key = (salida.CodigoEmpresa, salida.CodigoArticulo);
					descripcionesDict.TryGetValue(key, out descripcionArticulo);
				}

				var dto = new TraspasoStorageControlDto
				{
					MovTraspaso = salida.MovTraspaso != guidEmpty ? salida.MovTraspaso : Guid.NewGuid(),
					CodigoArticulo = salida.CodigoArticulo,
					DescripcionArticulo = descripcionArticulo,
					Partida = salida.Partida,
					FechaCaducidad = salida.FechaCaduca,
					AlmacenOrigen = salida.CodigoAlmacen,
					UbicacionOrigen = salida.Ubicacion,
					AlmacenDestino = null, // Sin destino porque no hay entrada
					UbicacionDestino = null, // Sin destino porque no hay entrada
					Cantidad = salida.Unidades,
					Fecha = salida.Fecha,
					FechaRegistro = salida.FechaRegistro,
					Comentario = salida.Comentario,
					CodigoEmpresa = salida.CodigoEmpresa,
					Ejercicio = salida.Ejercicio,
					EstadoMovimiento = "SIN_ENTRADA" // Salida sin entrada correspondiente
				};

				resultado.Add(dto);
			}

			// 8. Agregar entradas sin pareja como registros individuales
			foreach (var entrada in entradasSinPareja)
			{
				// Aplicar filtro de almacén destino si se especifica
				if (!string.IsNullOrWhiteSpace(almacenDestino) && entrada.CodigoAlmacen != almacenDestino)
					continue;

				// Obtener descripción del artículo del diccionario
				string? descripcionArticulo = null;
				if (!string.IsNullOrWhiteSpace(entrada.CodigoArticulo))
				{
					var key = (entrada.CodigoEmpresa, entrada.CodigoArticulo);
					descripcionesDict.TryGetValue(key, out descripcionArticulo);
				}

				var dto = new TraspasoStorageControlDto
				{
					MovTraspaso = entrada.MovTraspaso != guidEmpty ? entrada.MovTraspaso : Guid.NewGuid(),
					CodigoArticulo = entrada.CodigoArticulo,
					DescripcionArticulo = descripcionArticulo,
					Partida = entrada.Partida,
					FechaCaducidad = entrada.FechaCaduca,
					AlmacenOrigen = null, // Sin origen porque no hay salida
					UbicacionOrigen = null, // Sin origen porque no hay salida
					AlmacenDestino = entrada.CodigoAlmacen,
					UbicacionDestino = entrada.Ubicacion,
					Cantidad = entrada.Unidades,
					Fecha = entrada.Fecha,
					FechaRegistro = entrada.FechaRegistro,
					Comentario = entrada.Comentario,
					CodigoEmpresa = entrada.CodigoEmpresa,
					Ejercicio = entrada.Ejercicio,
					EstadoMovimiento = "SIN_SALIDA" // Entrada sin salida correspondiente
				};

				resultado.Add(dto);
			}

			// Ordenar por fecha descendente (usando FechaRegistro si está disponible, sino Fecha)
			resultado = resultado.OrderByDescending(t => t.FechaRegistro ?? t.Fecha).ToList();

			_logger.LogInformation($"✅ Traspasos finales: {resultado.Count}");

			return Ok(resultado);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error obteniendo traspasos de StorageControl");
			return StatusCode(500, new ProblemDetails
			{
				Title = "Error interno del servidor",
				Detail = "Error obteniendo traspasos de StorageControl"
			});
		}
	}

	private void RegistrarEventoTraspasoAsync(string tipoEvento, string origen, string descripcion, string? detalle = null)
	{
		try
		{
			string? token = null;
			try
			{
				if (Request?.Headers != null &&
					Request.Headers.TryGetValue("Authorization", out var authHeader) &&
					authHeader.ToString().StartsWith("Bearer "))
				{
					token = authHeader.ToString().Substring("Bearer ".Length).Trim();
					_logger.LogInformation("✅ Token capturado para evento de traspaso: {Origen}", origen);
				}
				else
				{
					_logger.LogWarning("⚠️ No se encontró header Authorization para evento de traspaso: {Origen}", origen);
				}
			}
			catch (ObjectDisposedException)
			{
				_logger.LogWarning("⚠️ Request ya fue liberado, no se puede registrar evento de traspaso: {Origen}", origen);
				return;
			}

			if (string.IsNullOrWhiteSpace(token))
			{
				_logger.LogWarning("⚠️ No se pudo obtener el token para registrar evento de traspaso: {Origen}", origen);
				return;
			}

			var tokenCapturado = token;
			var tipoEventoCapturado = tipoEvento;
			var origenCapturado = origen;
			var descripcionCapturada = descripcion;
			var detalleCapturado = detalle;

			_ = Task.Run(async () =>
			{
				try
				{
					if (_serviceProvider == null)
						return;

					IServiceScope? scope = null;
					try
					{
						scope = _serviceProvider.CreateScope();
					}
					catch (ObjectDisposedException)
					{
						return;
					}

					using (scope)
					{
						var dbContext = scope.ServiceProvider.GetRequiredService<AuroraSgaDbContext>();
						var logger = scope.ServiceProvider.GetRequiredService<ILogger<TraspasosController>>();

						var dispositivo = await dbContext.Dispositivos
							.FirstOrDefaultAsync(d => d.SessionToken == tokenCapturado && d.Activo == -1);

						if (dispositivo == null)
						{
							logger.LogWarning("⚠️ Dispositivo no encontrado para token al registrar evento de traspaso: {Origen}", origenCapturado);
							return;
						}

						var logEvento = new LogEvento
						{
							Fecha = DateTime.Now,
							IdUsuario = dispositivo.IdUsuario,
							IdDispositivo = dispositivo.Id,
							Tipo = tipoEventoCapturado,
							Origen = origenCapturado,
							Descripcion = descripcionCapturada,
							Detalle = detalleCapturado
						};

						dbContext.LogEventos.Add(logEvento);
						await dbContext.SaveChangesAsync();

						logger.LogInformation("✅ Evento de traspaso registrado: {Origen}, Usuario: {UsuarioId}, Dispositivo: {DispositivoId}",
							origenCapturado, dispositivo.IdUsuario, dispositivo.Id);
					}
				}
				catch (ObjectDisposedException)
				{
					return;
				}
				catch (Exception ex)
				{
					try
					{
						if (_serviceProvider != null)
						{
							using var scope = _serviceProvider.CreateScope();
							var logger = scope.ServiceProvider.GetRequiredService<ILogger<TraspasosController>>();
							logger.LogError(ex, "❌ Error al registrar evento de traspaso: {Origen}", origenCapturado);
						}
					}
					catch
					{
						// Ignorar errores secundarios de logging
					}
				}
			});
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "❌ Error al capturar token para evento de traspaso: {Origen}", origen);
		}
	}
}
