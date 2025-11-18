using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Mysqlx.Cursor;
using SGA_Api.Data;
using SGA_Api.Models.Palet;
using SGA_Api.Models.Traspasos;
using SGA_Api.Models.UsuarioConf;
using SGA_Api.Models.Registro;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace SGA_Api.Controllers.Palet;

[ApiController]
[Route("api/[controller]")]
public class PaletController : ControllerBase
{
	private readonly AuroraSgaDbContext _auroraSgaContext;
	private readonly SageDbContext _sageContext;
	private readonly StorageControlDbContext _storageContext;
	private readonly ILogger<PaletController> _logger;
	private readonly IServiceProvider _serviceProvider;

	public PaletController(
		AuroraSgaDbContext auroraSgaContext,
		SageDbContext sageContext,
		StorageControlDbContext storageContext,
		ILogger<PaletController> logger,
		IServiceProvider serviceProvider)
	{
		_auroraSgaContext = auroraSgaContext;
		_sageContext = sageContext;
		_storageContext = storageContext;
		_logger = logger;
		_serviceProvider = serviceProvider;
	}

	#region GET: Catálogo de tipos
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
	#endregion

	#region GET: Estados posibles de un palet
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
	#endregion

	#region GET: Listado filtrado
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
		[FromQuery] bool sinCierre = false,
		[FromQuery] string? almacen = null,
		[FromQuery] string? tipoUltimaActividad = null,
		[FromQuery] int? usuarioUltimaActividad = null,
		[FromQuery] int limite = 50)
	{
		var nombreDict = await _auroraSgaContext.vUsuariosConNombre
			.ToDictionaryAsync(x => x.UsuarioId, x => x.NombreOperario);

		var q = _auroraSgaContext.Palets
			.Where(p => p.CodigoEmpresa == codigoEmpresa && p.FechaVaciado == null);

		if (!string.IsNullOrWhiteSpace(codigo) && codigo.Length >= 3)
			q = q.Where(p => p.Codigo.Contains(codigo));

		if (!string.IsNullOrWhiteSpace(estado))
			q = q.Where(p => p.Estado == estado);

		if (!string.IsNullOrWhiteSpace(tipoPaletCodigo))
			q = q.Where(p => p.TipoPaletCodigo == tipoPaletCodigo);

		if (fechaApertura.HasValue)
			q = q.Where(p => p.FechaApertura.Date == fechaApertura.Value.Date);

		if (fechaCierre.HasValue)
			q = q.Where(p => p.FechaCierre.HasValue && p.FechaCierre.Value.Date == fechaCierre.Value.Date);

		if (sinCierre)
			q = q.Where(p => p.FechaCierre == null);

		if (fechaDesde.HasValue && fechaHasta.HasValue)
			q = q.Where(p => p.FechaApertura >= fechaDesde && p.FechaCierre <= fechaHasta);

		if (usuarioApertura.HasValue)
			q = q.Where(p => p.UsuarioAperturaId == usuarioApertura);

		if (usuarioCierre.HasValue)
			q = q.Where(p => p.UsuarioCierreId == usuarioCierre);

		// Filtro por almacén: buscar palets que tengan líneas en ese almacén
		if (!string.IsNullOrWhiteSpace(almacen))
		{
			// Buscar IDs de palets que tengan líneas en el almacén especificado
			var paletIdsEnAlmacen = await _auroraSgaContext.PaletLineas
				.Where(pl => pl.CodigoAlmacen == almacen && pl.Cantidad > 0)
				.Select(pl => pl.PaletId)
				.Distinct()
				.ToListAsync();

			// Filtrar solo los palets que están en el almacén especificado
			q = q.Where(p => paletIdsEnAlmacen.Contains(p.Id));
		}

		var lista = await q
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
				FechaVaciado = p.FechaVaciado,
				OrdenTrabajoId = p.OrdenTrabajoId,
				// 👇 añade esto
				CodigoGS1 = p.CodigoGS1,
				CodigoPalet = p.Codigo // si CódigoPalet es en realidad el mismo que Codig
			})
			.ToListAsync();

		// 🔷 NUEVO: Consultar bloqueos de calidad para palets
		await ConsultarBloqueosCalidadPaletsAsync(lista, codigoEmpresa);

		// 🔷 NUEVO: Consultar información de última actividad para palets
		await ConsultarUltimaActividadPaletsAsync(lista, nombreDict);

		foreach (var palet in lista)
		{
			if (palet.UsuarioAperturaId.HasValue && nombreDict.TryGetValue(palet.UsuarioAperturaId.Value, out var nombreA))
				palet.UsuarioAperturaNombre = nombreA;

			if (palet.UsuarioCierreId.HasValue && nombreDict.TryGetValue(palet.UsuarioCierreId.Value, out var nombreC))
				palet.UsuarioCierreNombre = nombreC;
		}

		// 🔷 NUEVO: Filtrar por última actividad (después de calcularla)
		if (!string.IsNullOrWhiteSpace(tipoUltimaActividad))
		{
			lista = lista.Where(p => p.TipoUltimaActividad == tipoUltimaActividad).ToList();
		}

		if (usuarioUltimaActividad.HasValue)
		{
			lista = lista.Where(p => p.UsuarioUltimaActividadId == usuarioUltimaActividad).ToList();
		}

		// 🔷 NUEVO: Ordenar por fecha de última actividad (más reciente primero)
		var resultado = lista
			.OrderByDescending(p => p.FechaUltimaActividad ?? p.FechaApertura) // Si no hay última actividad, usar fecha de apertura
			.ThenByDescending(p => p.FechaApertura) // Segundo criterio: fecha de apertura
			.ToList();

		// 🔷 LÓGICA MEJORADA: Solo aplicar límite si NO hay filtros aplicados
		bool hayFiltrosAplicados = !string.IsNullOrWhiteSpace(codigo) ||
								   !string.IsNullOrWhiteSpace(estado) ||
								   !string.IsNullOrWhiteSpace(tipoUltimaActividad) ||
								   usuarioUltimaActividad.HasValue ||
								   !string.IsNullOrWhiteSpace(tipoPaletCodigo) ||
								   fechaApertura.HasValue ||
								   fechaCierre.HasValue ||
								   fechaDesde.HasValue ||
								   fechaHasta.HasValue ||
								   usuarioApertura.HasValue ||
								   usuarioCierre.HasValue ||
								   sinCierre ||
								   !string.IsNullOrWhiteSpace(almacen);

		// Si hay filtros aplicados, devolver todos los resultados filtrados
		// Si no hay filtros, aplicar el límite por defecto
		if (!hayFiltrosAplicados)
		{
			resultado = resultado.Take(limite).ToList();
		}

		return Ok(resultado);
	}

	// 🔷 NUEVO: Consultar bloqueos de calidad para palets (OPTIMIZADO)
	private async Task ConsultarBloqueosCalidadPaletsAsync(List<PaletDto> palets, short codigoEmpresa)
	{
		try
		{
			if (!palets.Any())
				return;

			// 🚀 OPTIMIZACIÓN: Una sola consulta para obtener todas las líneas de todos los palets
			var paletIds = palets.Select(p => p.Id).ToList();
			var todasLasLineas = await _auroraSgaContext.PaletLineas
				.Where(pl => paletIds.Contains(pl.PaletId))
				.Select(pl => new { pl.PaletId, pl.CodigoArticulo })
				.ToListAsync();

			// Agrupar por palet
			var lineasPorPalet = todasLasLineas
				.GroupBy(l => l.PaletId)
				.ToDictionary(g => g.Key, g => g.Select(x => x.CodigoArticulo).Distinct().ToList());

			// Obtener todos los artículos únicos
			var codigosArticulos = todasLasLineas
				.Select(l => l.CodigoArticulo)
				.Distinct()
				.Where(c => !string.IsNullOrEmpty(c))
				.ToList();

			if (!codigosArticulos.Any())
				return;

			// 🚀 OPTIMIZACIÓN: Una sola consulta para todos los bloqueos
			var bloqueosActivos = await _auroraSgaContext.BloqueosCalidad
				.Where(b => b.CodigoEmpresa == codigoEmpresa && 
						   codigosArticulos.Contains(b.CodigoArticulo) &&
						   b.Bloqueado)
				.GroupBy(b => b.CodigoArticulo)
				.Select(g => new
				{
					CodigoArticulo = g.Key,
					BloqueoMasReciente = g.OrderByDescending(b => b.FechaBloqueo).First()
				})
				.ToListAsync();

			var bloqueosDict = bloqueosActivos.ToDictionary(
				b => b.CodigoArticulo, 
				b => b.BloqueoMasReciente);

			// Aplicar información de bloqueos a cada palet
			foreach (var palet in palets)
			{
				var lineas = lineasPorPalet.GetValueOrDefault(palet.Id, new List<string>());
				var articulosBloqueados = lineas
					.Where(codigo => !string.IsNullOrEmpty(codigo) && bloqueosDict.ContainsKey(codigo))
					.ToHashSet();

				// Actualizar propiedades del palet
				palet.TieneArticulosBloqueadosCalidad = articulosBloqueados.Any();
				palet.CantidadArticulosBloqueados = articulosBloqueados.Count;
				
				if (palet.TieneArticulosBloqueadosCalidad)
				{
					// Obtener información del bloqueo más reciente
					var bloqueoMasReciente = articulosBloqueados
						.Select(codigo => bloqueosDict.GetValueOrDefault(codigo))
						.Where(b => b != null)
						.OrderByDescending(b => b.FechaBloqueo)
						.FirstOrDefault();
						
					if (bloqueoMasReciente != null)
					{
						palet.MotivoBloqueoCalidad = bloqueoMasReciente.ComentarioBloqueo;
						palet.FechaBloqueoCalidad = bloqueoMasReciente.FechaBloqueo;
					}
				}
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error consultando bloqueos de calidad para palets");
			// No lanzar excepción para no interrumpir la carga de palets
		}
	}

	// 🔷 NUEVO: Consultar información de última actividad para palets
	private async Task ConsultarUltimaActividadPaletsAsync(List<PaletDto> palets, Dictionary<int, string> nombreDict)
	{
		try
		{
			if (!palets.Any())
				return;

			var paletIds = palets.Select(p => p.Id).ToList();

			// Obtener traspasos completados para estos palets
			var traspasosCompletados = await _auroraSgaContext.Traspasos
				.Where(t => paletIds.Contains(t.PaletId) && 
						   t.TipoTraspaso == "PALET" && 
						   t.CodigoEstado == "COMPLETADO")
				.OrderByDescending(t => t.FechaFinalizacion)
				.ToListAsync();

			// Agrupar por palet y obtener el más reciente
			var ultimoTraspasoPorPalet = traspasosCompletados
				.GroupBy(t => t.PaletId)
				.ToDictionary(g => g.Key, g => g.First());

			foreach (var palet in palets)
			{
				// Determinar la última actividad considerando el estado del palet y comparando fechas
				var actividades = new List<(string tipo, DateTime fecha, int? usuarioId, string descripcion)>();

				// 🔷 LÓGICA CORREGIDA: Agregar todas las actividades relevantes y comparar fechas
				// Si el palet está cerrado, la última actividad debe ser CIERRE o TRASPASO (la más reciente), nunca APERTURA
				// Si el palet está abierto, la última actividad puede ser APERTURA o TRASPASO (la más reciente)

				bool esCerrado = palet.Estado?.Equals("Cerrado", StringComparison.OrdinalIgnoreCase) == true;

				// Agregar traspaso si existe
				if (ultimoTraspasoPorPalet.TryGetValue(palet.Id, out var ultimoTraspaso))
				{
					var fechaTraspaso = ultimoTraspaso.FechaFinalizacion ?? DateTime.MinValue;
					actividades.Add(("TRASPASO", fechaTraspaso, ultimoTraspaso.UsuarioFinalizacionId,
						$"Traspasado a {ultimoTraspaso.AlmacenDestino} - {ultimoTraspaso.UbicacionDestino} por {nombreDict.GetValueOrDefault(ultimoTraspaso.UsuarioFinalizacionId ?? 0, "Usuario desconocido")}"));
				}

				// Agregar cierre si el palet está cerrado
				if (esCerrado && palet.FechaCierre.HasValue)
				{
					actividades.Add(("CIERRE", palet.FechaCierre.Value, palet.UsuarioCierreId,
						$"Palet cerrado por {nombreDict.GetValueOrDefault(palet.UsuarioCierreId ?? 0, "Usuario desconocido")}"));
				}

				// Agregar apertura solo si el palet está abierto (y no hay otras actividades más recientes)
				if (!esCerrado)
				{
					actividades.Add(("APERTURA", palet.FechaApertura, palet.UsuarioAperturaId, 
						$"Palet abierto por {nombreDict.GetValueOrDefault(palet.UsuarioAperturaId ?? 0, "Usuario desconocido")}"));
				}

				// Obtener la actividad más reciente comparando todas las fechas
				if (actividades.Any())
				{
					var ultimaActividad = actividades.OrderByDescending(a => a.fecha).First();
					palet.TipoUltimaActividad = ultimaActividad.tipo;
					palet.FechaUltimaActividad = ultimaActividad.fecha;
					palet.UsuarioUltimaActividadId = ultimaActividad.usuarioId;
					palet.UsuarioUltimaActividadNombre = nombreDict.GetValueOrDefault(ultimaActividad.usuarioId ?? 0, "Usuario desconocido");
					palet.DescripcionUltimaActividad = ultimaActividad.descripcion;
				}
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error consultando última actividad para palets");
			// No lanzar excepción para no interrumpir la carga de palets
		}
	}
	#endregion

	#region GET: Por Id
	[HttpGet("{id:guid}", Name = "GetPaletById")]
	public async Task<ActionResult<PaletDto>> GetPaletById(Guid id)
	{
		var palet = await _auroraSgaContext.Palets.FindAsync(id);
		if (palet == null) return NotFound();

		var nombreDict = await _auroraSgaContext.vUsuariosConNombre
			.ToDictionaryAsync(x => x.UsuarioId, x => x.NombreOperario);

		var dto = new PaletDto
		{
			Id = palet.Id,
			CodigoEmpresa = palet.CodigoEmpresa,
			Codigo = palet.Codigo,
			CodigoGS1 = palet.CodigoGS1,
			Estado = palet.Estado,
			TipoPaletCodigo = palet.TipoPaletCodigo,
			FechaApertura = palet.FechaApertura,
			FechaCierre = palet.FechaCierre,
			UsuarioAperturaId = palet.UsuarioAperturaId,
			UsuarioCierreId = palet.UsuarioCierreId,
			Altura = palet.Altura,
			Peso = palet.Peso,
			EtiquetaGenerada = palet.EtiquetaGenerada,
			IsVaciado = palet.IsVaciado,
			FechaVaciado = palet.FechaVaciado,
			OrdenTrabajoId = palet.OrdenTrabajoId
		};

		if (dto.UsuarioAperturaId.HasValue && nombreDict.TryGetValue(dto.UsuarioAperturaId.Value, out var nombreA))
			dto.UsuarioAperturaNombre = nombreA;

		if (dto.UsuarioCierreId.HasValue && nombreDict.TryGetValue(dto.UsuarioCierreId.Value, out var nombreC))
			dto.UsuarioCierreNombre = nombreC;

		return Ok(dto);
	}

	#endregion

	#region POST: Crear palet
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
			var pOrdenTrabajoId = new SqlParameter("@OrdenTrabajoId", SqlDbType.VarChar, 50) { Value = string.IsNullOrWhiteSpace(dto.OrdenTrabajoId) ? "" : dto.OrdenTrabajoId! };
			var pNuevoCodigo = new SqlParameter("@NuevoCodigo", SqlDbType.VarChar, 20) { Direction = ParameterDirection.Output };

			await _auroraSgaContext.Database.ExecuteSqlRawAsync(
				"EXEC dbo.CrearPalet @Canal, @Serie, @CodigoEmpresa, @Estado, @TipoPaletCodigo, @UsuarioAperturaId, @OrdenTrabajoId, @NuevoCodigo OUTPUT",
				pCanal, pSerie, pCodigoEmpresa, pEstado, pTipoPaletCodigo, pUsuarioAperturaId, pOrdenTrabajoId, pNuevoCodigo);

			var codigoGenerado = (string)pNuevoCodigo.Value!;
			var palet = await _auroraSgaContext.Palets.SingleAsync(x => x.Codigo == codigoGenerado);

			// === Generación del Código GS1 (SSCC) ===
			const string digitoExtension = "1";
			const string prefijoEmpresa = "8410191"; // Asegúrate que este es el tuyo

			// Extraer número secuencial del código: PAL25-0000029 → "0000029"
			string secuencia = codigoGenerado.Substring(codigoGenerado.LastIndexOf('-') + 1).PadLeft(9, '0');

			string cuerpo = digitoExtension + prefijoEmpresa + secuencia; // 17 dígitos

			string codigoGS1 = cuerpo + CalcularDigitoControlGs1(cuerpo); // 18 dígitos

			palet.CodigoGS1 = codigoGS1;

			_auroraSgaContext.LogPalet.Add(new LogPalet
			{
				PaletId = palet.Id,
				Fecha = DateTime.Now,
				IdUsuario = dto.UsuarioAperturaId,
				Accion = "Crear",
				Detalle = $"Palet creado por el usuario: {dto.UsuarioAperturaId}"
			});

			await _auroraSgaContext.SaveChangesAsync();

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
				FechaVaciado = palet.FechaVaciado,
				CodigoGS1 = palet.CodigoGS1
			};

			return CreatedAtRoute("GetPaletById", new { id = palet.Id }, resultado);
		}
		catch (Exception ex)
		{
			return Problem(detail: ex.ToString(), statusCode: 500, title: "Error creando palet");
		}
	}
	#endregion

	private static int CalcularDigitoControlGs1(string numeroBase)
	{
		int suma = 0;
		bool multiplicarPorTres = true;

		for (int i = numeroBase.Length - 1; i >= 0; i--)
		{
			int digito = numeroBase[i] - '0';
			suma += digito * (multiplicarPorTres ? 3 : 1);
			multiplicarPorTres = !multiplicarPorTres;
		}

		int resto = suma % 10;
		return resto == 0 ? 0 : 10 - resto;
	}

	#region POST: Añadir línea a palet
	[HttpPost("{id}/lineas")]
	public async Task<IActionResult> AnhadirLineaPalet(Guid id, [FromBody] LineaPaletCrearDto dto)
	{
		// 🟥 Verificar que el palet existe y está abierto
		var palet = await _auroraSgaContext.Palets.FindAsync(id);
		if (palet == null)
			return NotFound("Palet no encontrado");

		if (palet.Estado == "Cerrado")
			return BadRequest("No se pueden añadir líneas a un palet cerrado.");

		var ejercicio = await _sageContext.Periodos
			.Where(p => p.CodigoEmpresa == dto.CodigoEmpresa && p.Fechainicio <= DateTime.Now)
			.OrderByDescending(p => p.Fechainicio)
			.Select(p => p.Ejercicio)
			.FirstOrDefaultAsync();

		if (ejercicio == 0)
			return BadRequest("No se encontró ejercicio válido");

		// 🟦 Aquí comienza la transacción
		await using var transaction = await _auroraSgaContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

		try
		{
		// 🔷 Leer stock actual dentro de la transacción
		var stock = await _auroraSgaContext.StockDisponible
			.FirstOrDefaultAsync(s =>
				s.CodigoEmpresa == dto.CodigoEmpresa &&
				s.CodigoArticulo == dto.CodigoArticulo &&
				s.CodigoAlmacen == dto.CodigoAlmacen &&
				s.Ubicacion == dto.Ubicacion &&
				s.Partida == dto.Lote);

		if (stock == null)
			return BadRequest("No se encontró stock para el artículo, almacén y ubicación especificados.");

		if (dto.Cantidad > stock.Disponible)
			return BadRequest($"No puedes reservar más de lo disponible: {stock.Disponible:N2} unidades.");

		// === US-002: SOLO crear línea negativa si Android especifica PaletIdOrigen ===
		// Si el usuario NO especifica PaletIdOrigen, significa que quiere material SUELTO
		var loteNormalizado = (dto.Lote ?? "").Trim();
		PaletLinea? paletOrigen = null;
		
		// SOLO buscar palet origen si Android lo especifica explícitamente
		if (dto.PaletIdOrigen.HasValue && dto.PaletIdOrigen.Value != Guid.Empty)
		{
			paletOrigen = await _auroraSgaContext.PaletLineas
				.Include(pl => pl.Palet)
				.Where(pl =>
					pl.PaletId == dto.PaletIdOrigen.Value &&
					pl.CodigoArticulo == dto.CodigoArticulo &&
					pl.CodigoAlmacen.Trim().ToUpper() == dto.CodigoAlmacen.Trim().ToUpper() &&
					pl.Ubicacion.Trim().ToUpper() == dto.Ubicacion.Trim().ToUpper() &&
					(pl.Lote ?? "") == loteNormalizado &&
					pl.Cantidad >= dto.Cantidad)
				.FirstOrDefaultAsync();
		}
		
		if (paletOrigen != null)
		{
			// Crear línea temporal NEGATIVA para el palet origen
			// NO asignamos TraspasoId aquí - se asignará cuando se cierre el palet destino
			var lineaNegativa = new TempPaletLinea
			{
				PaletId = paletOrigen.PaletId,
				CodigoEmpresa = dto.CodigoEmpresa,
				CodigoArticulo = dto.CodigoArticulo,
				DescripcionArticulo = dto.DescripcionArticulo,
				Cantidad = -dto.Cantidad, // CANTIDAD NEGATIVA
				Lote = dto.Lote,
				FechaCaducidad = dto.FechaCaducidad,
				CodigoAlmacen = dto.CodigoAlmacen,
				Ubicacion = dto.Ubicacion,
				UsuarioId = dto.UsuarioId,
				Observaciones = "Delta negativo por extracción de material del palet",
				FechaAgregado = DateTime.Now,
				Procesada = false,
				EsHeredada = false,
				TraspasoId = null // Sin TraspasoId - se asignará después
			};
			
			_auroraSgaContext.TempPaletLineas.Add(lineaNegativa);
		}

		// 🔷 Crear la línea temporal POSITIVA para el palet nuevo
		// 🔷 DEBUG: Log para verificar precisión de cantidad
		_logger.LogInformation("🔍 DEBUG Cantidad recibida en DTO: {Cantidad} (formato completo: {CantidadFormato})", 
			dto.Cantidad, dto.Cantidad.ToString("F6"));
		
		var linea = new TempPaletLinea
		{
			PaletId = palet.Id,
			CodigoEmpresa = dto.CodigoEmpresa,
			CodigoArticulo = dto.CodigoArticulo,
			DescripcionArticulo = dto.DescripcionArticulo,
			Cantidad = dto.Cantidad,
			Lote = dto.Lote,
			FechaCaducidad = dto.FechaCaducidad,
			CodigoAlmacen = dto.CodigoAlmacen,
			Ubicacion = dto.Ubicacion,
			UsuarioId = dto.UsuarioId,
			Observaciones = dto.Observaciones,
			FechaAgregado = DateTime.Now,
			Procesada = false,
			EsHeredada = false,
			TraspasoId = null
		};

		_logger.LogInformation("🔍 DEBUG Cantidad asignada a TempPaletLinea: {Cantidad} (formato completo: {CantidadFormato})", 
			linea.Cantidad, linea.Cantidad.ToString("F6"));

		_auroraSgaContext.TempPaletLineas.Add(linea);

			// 🔷 Registrar en log
			_auroraSgaContext.LogPalet.Add(new LogPalet
			{
				PaletId = palet.Id,
				Fecha = DateTime.Now,
				IdUsuario = dto.UsuarioId,
				Accion = "AñadirLínea",
				Detalle = $"Artículo: {dto.CodigoArticulo}, Cantidad: {dto.Cantidad}, Almacén: {dto.CodigoAlmacen}, Ubicación: {dto.Ubicacion}, Lote: {dto.Lote}"
			});

		// 🔷 Guardar cambios
		await _auroraSgaContext.SaveChangesAsync();
		
		// 🔷 DEBUG: Verificar valor después de guardar
		await _auroraSgaContext.Entry(linea).ReloadAsync();
		_logger.LogInformation("🔍 DEBUG Cantidad después de SaveChanges (reloaded): {Cantidad} (formato completo: {CantidadFormato})", 
			linea.Cantidad, linea.Cantidad.ToString("F6"));

		// 🔷 Confirmar la transacción
			await transaction.CommitAsync();

			return Ok(new { message = "Línea registrada correctamente", linea.Id });
		}
		catch (Exception ex)
		{
			// 🔷 Si falla algo, deshacer la transacción
			await transaction.RollbackAsync();

			// opcional: loggear error
			// _logger.LogError(ex, "Error al añadir línea al palet.");

			return StatusCode(500, $"Error al registrar la línea: {ex.Message}");
		}
	}
	#endregion

	#region GET: Líneas de un palet
	[HttpGet("{id}/lineas")]
	public async Task<ActionResult<List<LineaPaletDto>>> GetLineasPalet(Guid id)
	{
		// Obtener líneas definitivas
		var definitivas = await _auroraSgaContext.PaletLineas
			.Where(l => l.PaletId == id)
			.Select(l => new LineaPaletDto
			{
				Id = l.Id,
				PaletId = l.PaletId,
				CodigoEmpresa = l.CodigoEmpresa,
				CodigoArticulo = l.CodigoArticulo,
				DescripcionArticulo = l.DescripcionArticulo,
				Cantidad = l.Cantidad,
				UnidadMedida = l.UnidadMedida,
				Lote = l.Lote,
				FechaCaducidad = l.FechaCaducidad,
				CodigoAlmacen = l.CodigoAlmacen,
				Ubicacion = l.Ubicacion,
				UsuarioId = l.UsuarioId,
				FechaAgregado = l.FechaAgregado,
				Observaciones = l.Observaciones
			})
			.ToListAsync();

		// Obtener líneas temporales NO PROCESADAS
		var temporales = await _auroraSgaContext.TempPaletLineas
			.Where(l => l.PaletId == id && l.Procesada == false)
			.Select(l => new LineaPaletDto
			{
				Id = l.Id,
				PaletId = l.PaletId,
				CodigoEmpresa = l.CodigoEmpresa,
				CodigoArticulo = l.CodigoArticulo,
				DescripcionArticulo = l.DescripcionArticulo,
				Cantidad = l.Cantidad,
				UnidadMedida = l.UnidadMedida,
				Lote = l.Lote,
				FechaCaducidad = l.FechaCaducidad,
				CodigoAlmacen = l.CodigoAlmacen,
				Ubicacion = l.Ubicacion,
				UsuarioId = l.UsuarioId,
				FechaAgregado = l.FechaAgregado,
				Observaciones = l.Observaciones
			})
			.ToListAsync();

		// Obtener información del palet para el registro de eventos
		var palet = await _auroraSgaContext.Palets
			.Where(p => p.Id == id)
			.Select(p => new { p.Codigo, p.CodigoEmpresa })
			.FirstOrDefaultAsync();

		// Unir y agrupar consolidando cantidades (SOLO VISUAL - BD mantiene líneas individuales para trazabilidad)
		// Agrupa por {Artículo, Lote, Fecha} sin ubicación para mostrar total consolidado al usuario
		var lineas = definitivas.Concat(temporales)
			.GroupBy(l => new
			{
				l.CodigoArticulo,
				l.Lote,
				l.FechaCaducidad,
				l.DescripcionArticulo
			})
			.Select(g =>
			{
				var first = g.First();
				var ultimaLinea = g.OrderByDescending(x => x.FechaAgregado).First();
				return new LineaPaletDto
				{
					Id = first.Id,
					PaletId = first.PaletId,
					CodigoEmpresa = first.CodigoEmpresa,
					CodigoArticulo = first.CodigoArticulo,
					DescripcionArticulo = first.DescripcionArticulo,
					Cantidad = g.Sum(x => x.Cantidad), // Suma TODAS las cantidades (múltiples orígenes)
					UnidadMedida = first.UnidadMedida,
					Lote = first.Lote,
					FechaCaducidad = first.FechaCaducidad,
					CodigoAlmacen = ultimaLinea.CodigoAlmacen, // Ubicación de la línea más reciente
					Ubicacion = ultimaLinea.Ubicacion,
					UsuarioId = ultimaLinea.UsuarioId,
					FechaAgregado = ultimaLinea.FechaAgregado,
					Observaciones = ultimaLinea.Observaciones
				};
		})
		.ToList();

		// 🔷 NUEVO: Consultar bloqueos de calidad para las líneas
		await ConsultarBloqueosCalidadLineasAsync(lineas);

		// Registrar evento de consulta de stock (consulta de líneas de palet)
		if (palet != null)
		{
			var detalleConsulta = $"Empresa={palet.CodigoEmpresa}, PaletId={id}, CodigoPalet={palet.Codigo}, Lineas={lineas.Count}";
			RegistrarEventoConsultaStockAsync(
				"PaletController/GetLineasPalet",
				$"Consulta de líneas de palet {palet.Codigo}",
				detalleConsulta);
		}

		return Ok(lineas);
	}

	// 🔷 NUEVO: Consultar bloqueos de calidad para líneas de palet
	private async Task ConsultarBloqueosCalidadLineasAsync(List<LineaPaletDto> lineas)
	{
		try
		{
			if (!lineas.Any())
				return;

			// Obtener códigos de artículos únicos
			var codigosArticulos = lineas.Select(l => l.CodigoArticulo).Distinct().ToList();

			// Consultar bloqueos activos
			var bloqueosActivos = await _auroraSgaContext.BloqueosCalidad
				.Where(b => codigosArticulos.Contains(b.CodigoArticulo) && b.Bloqueado)
				.GroupBy(b => b.CodigoArticulo)
				.Select(g => new
				{
					CodigoArticulo = g.Key,
					BloqueoMasReciente = g.OrderByDescending(b => b.FechaBloqueo).First()
				})
				.ToListAsync();

			var bloqueosDict = bloqueosActivos.ToDictionary(
				b => b.CodigoArticulo, 
				b => b.BloqueoMasReciente);

			// Aplicar información de bloqueos a cada línea
			foreach (var linea in lineas)
			{
				if (bloqueosDict.TryGetValue(linea.CodigoArticulo, out var bloqueo))
				{
					linea.IsBloqueadoCalidad = true;
					linea.MotivoBloqueoCalidad = bloqueo.ComentarioBloqueo;
					linea.FechaBloqueoCalidad = bloqueo.FechaBloqueo;
				}
				else
				{
					linea.IsBloqueadoCalidad = false;
					linea.MotivoBloqueoCalidad = null;
					linea.FechaBloqueoCalidad = null;
				}
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error consultando bloqueos de calidad para líneas de palet");
			// No lanzar excepción para no interrumpir la carga de líneas
		}
	}
	#endregion

	#region DELETE: Eliminar línea de palet
	[HttpDelete("lineas/{lineaId}")]
	public async Task<IActionResult> EliminarLineaPalet(Guid lineaId, [FromQuery] int usuarioId)
	{
		// 🟦 Iniciar transacción para garantizar consistencia
		await using var transaction = await _auroraSgaContext.Database.BeginTransactionAsync();
		
		try
		{
			var linea = await _auroraSgaContext.TempPaletLineas.FindAsync(lineaId);
			if (linea == null)
			{
				return NotFound();
			}

			// Primero obtenemos el palet asociado
			var palet = await _auroraSgaContext.Palets.FindAsync(linea.PaletId);
			if (palet == null)
				return NotFound("Palet no encontrado");

			// Si está cerrado, no se puede eliminar la línea
			if (palet.Estado == "Cerrado")
				return BadRequest("No se pueden eliminar líneas de un palet cerrado.");

			// 🔷 FUNCIONALIDAD MEJORADA: Manejar tanto líneas POSITIVAS como NEGATIVAS
			TempPaletLinea? lineaCorrespondiente = null;
			if (linea.Cantidad > 0)
			{
				// Buscar línea NEGATIVA correspondiente para línea POSITIVA
				// Ordenar por fecha para encontrar la más reciente que coincida
				lineaCorrespondiente = await _auroraSgaContext.TempPaletLineas
					.Where(l => 
						l.CodigoArticulo == linea.CodigoArticulo &&
						l.Lote == linea.Lote &&
						l.CodigoAlmacen == linea.CodigoAlmacen &&
						l.Cantidad == -linea.Cantidad && // Cantidad opuesta
						l.Procesada == false &&
						(l.TraspasoId == linea.TraspasoId || (l.TraspasoId == null && linea.TraspasoId == null)) && // Mismo traspaso o ambos null
						l.Id != lineaId) // No la misma línea
					.OrderByDescending(l => l.FechaAgregado) // Más reciente primero
					.FirstOrDefaultAsync();
			}
			else if (linea.Cantidad < 0)
			{
				// Buscar línea POSITIVA correspondiente para línea NEGATIVA
				// Ordenar por fecha para encontrar la más reciente que coincida
				lineaCorrespondiente = await _auroraSgaContext.TempPaletLineas
					.Where(l => 
						l.CodigoArticulo == linea.CodigoArticulo &&
						l.Lote == linea.Lote &&
						l.CodigoAlmacen == linea.CodigoAlmacen &&
						l.Cantidad == -linea.Cantidad && // Cantidad opuesta
						l.Procesada == false &&
						(l.TraspasoId == linea.TraspasoId || (l.TraspasoId == null && linea.TraspasoId == null)) && // Mismo traspaso o ambos null
						l.Id != lineaId) // No la misma línea
					.OrderByDescending(l => l.FechaAgregado) // Más reciente primero
					.FirstOrDefaultAsync();
			}
					
			if (lineaCorrespondiente != null)
			{
				// Obtener el palet correspondiente para el log
				var paletCorrespondiente = await _auroraSgaContext.Palets.FindAsync(lineaCorrespondiente.PaletId);
				
				// Eliminar la línea correspondiente (esto mantiene el balance)
				_auroraSgaContext.TempPaletLineas.Remove(lineaCorrespondiente);
				
				// Log de la eliminación en cascada
				_auroraSgaContext.LogPalet.Add(new LogPalet
				{
					PaletId = lineaCorrespondiente.PaletId,
					Fecha = DateTime.Now,
					IdUsuario = usuarioId,
					Accion = "EliminarLineaCorrespondiente",
					Detalle = $"Eliminada línea correspondiente automáticamente. Artículo: {linea.CodigoArticulo}, Cantidad: {lineaCorrespondiente.Cantidad}, Palet: {paletCorrespondiente?.Codigo ?? "N/A"}"
				});
			}
			else
			{
				// 🔷 FIX: Solo crear líneas compensatorias si hay un traspaso asociado (movimiento entre palets)
				// Para líneas normales (sin traspaso), solo eliminar sin crear compensaciones
				
				if (linea.TraspasoId.HasValue)
				{
					// Solo crear compensación si hay traspaso (movimiento entre palets)
					// Buscar el palet origen basado en el traspaso
					var traspaso = await _auroraSgaContext.Traspasos.FindAsync(linea.TraspasoId);
					Guid paletOrigenId = Guid.Empty;
					
					if (traspaso != null && traspaso.PaletId != Guid.Empty)
					{
						paletOrigenId = traspaso.PaletId;
					}
					else
					{
						// Si no hay traspaso o palet origen, buscar palets con el mismo artículo en ubicación origen
						var paletOrigen = await _auroraSgaContext.Palets
							.Join(_auroraSgaContext.PaletLineas, p => p.Id, pl => pl.PaletId, (p, pl) => new { p, pl })
							.Where(x => x.pl.CodigoArticulo == linea.CodigoArticulo && 
										x.pl.Lote == linea.Lote &&
										x.pl.CodigoAlmacen == linea.CodigoAlmacen &&
										x.p.Estado == "Abierto")
							.Select(x => x.p.Id)
							.FirstOrDefaultAsync();
						
						if (paletOrigen != Guid.Empty)
							paletOrigenId = paletOrigen;
					}
					
					if (paletOrigenId != Guid.Empty)
					{
						// Crear línea compensatoria en el palet origen
						// Para líneas POSITIVAS: devolver stock (cantidad positiva)
						// Para líneas NEGATIVAS: compensar la eliminación (cantidad negativa)
						var lineaCompensatoria = new TempPaletLinea
						{
							PaletId = paletOrigenId, // Palet origen
							CodigoEmpresa = linea.CodigoEmpresa,
							CodigoArticulo = linea.CodigoArticulo,
							DescripcionArticulo = linea.DescripcionArticulo,
							Cantidad = linea.Cantidad, // Mantener el mismo signo para compensar
							Lote = linea.Lote,
							FechaCaducidad = linea.FechaCaducidad,
							CodigoAlmacen = linea.CodigoAlmacen,
							Ubicacion = linea.Ubicacion,
							UsuarioId = linea.UsuarioId,
							Observaciones = linea.Cantidad > 0 
								? "Devolución de stock al palet origen por cancelación de línea"
								: "Compensación de eliminación de línea negativa",
							FechaAgregado = DateTime.Now,
							Procesada = false,
							EsHeredada = false,
							TraspasoId = linea.TraspasoId
						};
						
						_auroraSgaContext.TempPaletLineas.Add(lineaCompensatoria);
						
						// Log de la línea compensatoria
						_auroraSgaContext.LogPalet.Add(new LogPalet
						{
							PaletId = paletOrigenId,
							Fecha = DateTime.Now,
							IdUsuario = usuarioId,
							Accion = "CompensarEliminacion",
							Detalle = $"Línea compensatoria creada en palet origen por cancelación. Artículo: {linea.CodigoArticulo}, Cantidad: {lineaCompensatoria.Cantidad}"
						});
					}
					else
					{
						// Si no se puede encontrar palet origen, crear línea compensatoria negativa en destino
						// (comportamiento anterior como fallback)
						var lineaCompensatoria = new TempPaletLinea
						{
							PaletId = linea.PaletId, // Mismo palet destino
							CodigoEmpresa = linea.CodigoEmpresa,
							CodigoArticulo = linea.CodigoArticulo,
							DescripcionArticulo = linea.DescripcionArticulo,
							Cantidad = -linea.Cantidad, // Cantidad negativa para compensar
							Lote = linea.Lote,
							FechaCaducidad = linea.FechaCaducidad,
							CodigoAlmacen = linea.CodigoAlmacen,
							Ubicacion = linea.Ubicacion,
							UsuarioId = linea.UsuarioId,
							Observaciones = "Línea compensatoria por eliminación (no se encontró palet origen)",
							FechaAgregado = DateTime.Now,
							Procesada = false,
							EsHeredada = false,
							TraspasoId = linea.TraspasoId
						};
						
						_auroraSgaContext.TempPaletLineas.Add(lineaCompensatoria);
						
						// Log de la línea compensatoria
						_auroraSgaContext.LogPalet.Add(new LogPalet
						{
							PaletId = linea.PaletId,
							Fecha = DateTime.Now,
							IdUsuario = usuarioId,
							Accion = "CrearLineaCompensatoria",
							Detalle = $"Creada línea compensatoria al eliminar línea sin palet origen identificado. Artículo: {linea.CodigoArticulo}, Cantidad: {lineaCompensatoria.Cantidad}"
						});
					}
				}
				else
				{
					// Para líneas normales (sin traspaso), solo eliminar sin crear compensaciones
					// Esto evita el bug de crear líneas negativas innecesarias
					_auroraSgaContext.LogPalet.Add(new LogPalet
					{
						PaletId = linea.PaletId,
						Fecha = DateTime.Now,
						IdUsuario = usuarioId,
						Accion = "EliminarLineaNormal",
						Detalle = $"Línea normal eliminada sin compensación (sin traspaso asociado). Artículo: {linea.CodigoArticulo}, Cantidad: {linea.Cantidad}"
					});
				}
			}

			// Eliminar la línea original
			_auroraSgaContext.TempPaletLineas.Remove(linea);

			// Log de la eliminación principal
			_auroraSgaContext.LogPalet.Add(new LogPalet
			{
				PaletId = palet.Id,
				Fecha = DateTime.Now,
				IdUsuario = usuarioId,
				Accion = "EliminarLinea",
				Detalle = $"Línea eliminada: Artículo={linea.CodigoArticulo}, Cantidad={linea.Cantidad}, Ubicación={linea.Ubicacion}" +
					(lineaCorrespondiente != null ? " (Incluye eliminación automática de línea correspondiente)" : "")
			});

			await _auroraSgaContext.SaveChangesAsync();

			// 🔷 NUEVO: Verificar si el palet quedó sin líneas y marcarlo como vaciado automáticamente
			// NOTA: Esta verificación es segura y compatible con TraspasoFinalizacionBackgroundService porque:
			// 1. Ambos verifican palet.Estado != "Vaciado" antes de actualizar (evita duplicados)
			// 2. Ambos usan transacciones para garantizar consistencia
			// 3. El BackgroundService procesa traspasos, mientras esto procesa eliminaciones manuales
			// 4. Si el BackgroundService ya marcó el palet como vaciado, esta verificación no lo vuelve a marcar
			
			var quedanTemporales = await _auroraSgaContext.TempPaletLineas
				.AnyAsync(l => l.PaletId == palet.Id && l.Procesada == false);
			
			var quedanDefinitivas = await _auroraSgaContext.PaletLineas
				.AnyAsync(l => l.PaletId == palet.Id);

			// Verificar condición: solo marcar como vaciado si NO está ya vaciado (evita condiciones de carrera)
			// Esta verificación es compatible con el BackgroundService que también verifica el estado
			if (!quedanTemporales && !quedanDefinitivas && palet.Estado != "Vaciado")
			{
				palet.Estado = "Vaciado";
				palet.FechaVaciado = DateTime.Now;
				palet.UsuarioVaciadoId = usuarioId;
				
				// Si no tiene fecha de cierre, establecerla
				if (!palet.FechaCierre.HasValue)
				{
					palet.FechaCierre = DateTime.Now;
					palet.UsuarioCierreId = usuarioId;
				}

				_auroraSgaContext.Palets.Update(palet);

				_auroraSgaContext.LogPalet.Add(new LogPalet
				{
					PaletId = palet.Id,
					Fecha = DateTime.Now,
					IdUsuario = usuarioId,
					Accion = "Vaciado",
					Detalle = "Marcado automáticamente como vaciado al eliminar todas las líneas."
				});

				await _auroraSgaContext.SaveChangesAsync();
			}

			// 🔷 NUEVO: También verificar el palet de la línea correspondiente si se eliminó una
			if (lineaCorrespondiente != null && lineaCorrespondiente.PaletId != palet.Id)
			{
				var paletCorrespondiente = await _auroraSgaContext.Palets.FindAsync(lineaCorrespondiente.PaletId);
				if (paletCorrespondiente != null && paletCorrespondiente.Estado != "Vaciado")
				{
					var quedanTemporalesCorrespondiente = await _auroraSgaContext.TempPaletLineas
						.AnyAsync(l => l.PaletId == paletCorrespondiente.Id && l.Procesada == false);
					
					var quedanDefinitivasCorrespondiente = await _auroraSgaContext.PaletLineas
						.AnyAsync(l => l.PaletId == paletCorrespondiente.Id);

					if (!quedanTemporalesCorrespondiente && !quedanDefinitivasCorrespondiente)
					{
						paletCorrespondiente.Estado = "Vaciado";
						paletCorrespondiente.FechaVaciado = DateTime.Now;
						paletCorrespondiente.UsuarioVaciadoId = usuarioId;
						
						// Si no tiene fecha de cierre, establecerla
						if (!paletCorrespondiente.FechaCierre.HasValue)
						{
							paletCorrespondiente.FechaCierre = DateTime.Now;
							paletCorrespondiente.UsuarioCierreId = usuarioId;
						}

						_auroraSgaContext.Palets.Update(paletCorrespondiente);

						_auroraSgaContext.LogPalet.Add(new LogPalet
						{
							PaletId = paletCorrespondiente.Id,
							Fecha = DateTime.Now,
							IdUsuario = usuarioId,
							Accion = "Vaciado",
							Detalle = "Marcado automáticamente como vaciado al eliminar todas las líneas (incluyendo línea correspondiente eliminada)."
						});

						await _auroraSgaContext.SaveChangesAsync();
					}
				}
			}

			await transaction.CommitAsync();

			var mensaje = lineaCorrespondiente != null 
				? "Línea eliminada correctamente. Se eliminó automáticamente la línea correspondiente para mantener el balance."
				: "Línea eliminada correctamente. Se creó una línea compensatoria para mantener la integridad del inventario.";

			return Ok(new { message = mensaje });
		}
		catch (Exception ex)
		{
			// 🔷 Si falla algo, deshacer la transacción
			await transaction.RollbackAsync();
			_logger.LogError(ex, "Error al eliminar línea de palet. LineaId: {LineaId}, UsuarioId: {UsuarioId}", lineaId, usuarioId);
			return StatusCode(500, $"Error al eliminar línea: {ex.Message}");
		}
	}

	#endregion

	[HttpPost("{id}/cerrar")]
	public async Task<IActionResult> CerrarPalet(Guid id, [FromBody] CerrarPaletDto dto)
	{
		var palet = await _auroraSgaContext.Palets.FindAsync(id);
		if (palet == null)
			return NotFound("Palet no encontrado");

		if (palet.Estado == "Cerrado")
			return BadRequest("El palet ya está cerrado.");

		// Verifica que tenga al menos una línea
		bool tieneLineas = await _auroraSgaContext.TempPaletLineas.AnyAsync(l => l.PaletId == id)
			|| await _auroraSgaContext.PaletLineas.AnyAsync(l => l.PaletId == id);

		if (!tieneLineas)
			return BadRequest("No se puede cerrar un palet vacío. Debe tener al menos una línea.");

		// Valida que la ubicación destino exista en ese almacén destino
		var ubicacionDestino = await _auroraSgaContext.Ubicaciones
			.FirstOrDefaultAsync(u =>
				u.CodigoAlmacen == dto.CodigoAlmacenDestino &&
				u.CodigoUbicacion == dto.UbicacionDestino);

		if (ubicacionDestino == null)
			return BadRequest($"La ubicación '{dto.UbicacionDestino}' no existe en el almacén destino '{dto.CodigoAlmacenDestino}'.");

		// Cierra el palet
		palet.Estado = "Cerrado";
		palet.FechaCierre = DateTime.Now;
		palet.UsuarioCierreId = dto.UsuarioId;
		if (dto.Altura.HasValue) palet.Altura = dto.Altura;
		if (dto.Peso.HasValue) palet.Peso = dto.Peso;

		_auroraSgaContext.LogPalet.Add(new LogPalet
		{
			PaletId = palet.Id,
			Fecha = DateTime.Now,
			IdUsuario = dto.UsuarioId,
			Accion = "Cerrar",
			Detalle = $"Palet cerrado en almacén destino {dto.CodigoAlmacenDestino} - ubicación destino {dto.UbicacionDestino} por usuario {dto.UsuarioId}"
		});

		// Determina el estado del traspaso por defecto
		var estadoTraspaso = (dto.CodigoAlmacen == dto.CodigoAlmacenDestino) ? "COMPLETADO" : "PENDIENTE";

		// 1. Obtén las definitivas
		var lineasDefinitivas = await _auroraSgaContext.PaletLineas
			.Where(l => l.PaletId == palet.Id)
			.ToListAsync();

		// 2. Compara con la ubicación/almacén destino
		bool ubicacionCambiada = lineasDefinitivas.Any() &&
			(lineasDefinitivas.Any(l => l.CodigoAlmacen != dto.CodigoAlmacenDestino || l.Ubicacion != dto.UbicacionDestino));

		List<TempPaletLinea> lineasParaTraspaso;

		if (ubicacionCambiada)
		{
			// Traspasar todas: definitivas (convertidas a temporales) + nuevas temporales no procesadas
			foreach (var def in lineasDefinitivas)
			{
				var yaExiste = await _auroraSgaContext.TempPaletLineas
					.AnyAsync(t => t.PaletId == palet.Id && t.CodigoArticulo == def.CodigoArticulo && t.Lote == def.Lote && t.Procesada == false);
				if (!yaExiste)
				{
					var temp = new TempPaletLinea
					{
						PaletId = def.PaletId,
						CodigoEmpresa = def.CodigoEmpresa,
						CodigoArticulo = def.CodigoArticulo,
						DescripcionArticulo = def.DescripcionArticulo,
						Cantidad = def.Cantidad,
						UnidadMedida = def.UnidadMedida,
						Lote = def.Lote,
						FechaCaducidad = def.FechaCaducidad,
						CodigoAlmacen = def.CodigoAlmacen,
						Ubicacion = def.Ubicacion,
						UsuarioId = def.UsuarioId,
						FechaAgregado = DateTime.Now,
						Observaciones = def.Observaciones,
						Procesada = false,
						EsHeredada = true // Marcar como heredada
					};
					_auroraSgaContext.TempPaletLineas.Add(temp);
				}
			}
			await _auroraSgaContext.SaveChangesAsync();

			// Selecciona todas las temporales no procesadas
			lineasParaTraspaso = await _auroraSgaContext.TempPaletLineas
				.Where(l => l.PaletId == palet.Id && l.Procesada == false)
				.ToListAsync();
		}
		else
		{
			// Solo las nuevas temporales no procesadas
			lineasParaTraspaso = await _auroraSgaContext.TempPaletLineas
				.Where(l => l.PaletId == palet.Id && l.Procesada == false)
				.ToListAsync();
		}

		var traspasosCreados = new List<Guid>();

		foreach (var linea in lineasParaTraspaso)
		{
			var traspasoArticulo = new Traspaso
			{
				Id = Guid.NewGuid(),
				PaletId = palet.Id,
				CodigoPalet = palet.Codigo,
				TipoTraspaso = "PALET", // Siempre PALET
				CodigoEstado = dto.CodigoEstado ?? estadoTraspaso,
				FechaInicio = DateTime.Now,
				UsuarioInicioId = dto.UsuarioId,
				AlmacenOrigen = linea.CodigoAlmacen,
				AlmacenDestino = dto.CodigoAlmacenDestino,
				UbicacionOrigen = linea.Ubicacion,
				UbicacionDestino = dto.UbicacionDestino, // Siempre se asigna
				FechaFinalizacion = DateTime.Now, // Siempre se asigna
				UsuarioFinalizacionId = dto.UsuarioFinalizacionId, // Siempre se asigna
				CodigoEmpresa = dto.CodigoEmpresa,
				CodigoArticulo = linea.CodigoArticulo,
				Cantidad = linea.Cantidad,
				Partida = linea.Lote,
				FechaCaducidad = linea.FechaCaducidad,
				Comentario = dto.Comentario,
				OrigenTraspaso = "AuroraSGA"
			};
			_auroraSgaContext.Traspasos.Add(traspasoArticulo);
			traspasosCreados.Add(traspasoArticulo.Id);

			// Asociar el TraspasoId a la línea temporal correspondiente
			linea.TraspasoId = traspasoArticulo.Id;
			_auroraSgaContext.TempPaletLineas.Update(linea);
		}

		// === NUEVO: Aplicar lógica de inventario cuando se cierra el palet ===
		await AplicarLogicaInventarioAlCerrarPaletAsync(palet.Id, dto.CodigoEmpresa);

		await _auroraSgaContext.SaveChangesAsync();

		return Ok(new
		{
			message = $"Palet {palet.Codigo} cerrado correctamente y traspasos de artículos creados.",
			traspasosIds = traspasosCreados
		});
	}

	/// <summary>
	/// Aplica la lógica de inventario cuando se cierra un palet
	/// </summary>
		private async Task AplicarLogicaInventarioAlCerrarPaletAsync(Guid paletId, short codigoEmpresa)
	{
		try
		{
			// Obtener todas las líneas del palet (definitivas y temporales)
			var lineasPalet = await _auroraSgaContext.PaletLineas
				.Where(pl => pl.PaletId == paletId)
				.ToListAsync();

			var lineasTempPalet = await _auroraSgaContext.TempPaletLineas
				.Where(tpl => tpl.PaletId == paletId && !tpl.Procesada)
				.ToListAsync();

			// Crear una lista unificada con información común
			var todasLasLineas = lineasPalet.Select(pl => new
			{
				pl.CodigoArticulo,
				pl.CodigoAlmacen,
				Ubicacion = NormalizarUbicacion(pl.Ubicacion),
				pl.Cantidad,
				pl.Lote
			}).ToList();

			todasLasLineas.AddRange(lineasTempPalet.Select(tpl => new
			{
				tpl.CodigoArticulo,
				tpl.CodigoAlmacen,
				Ubicacion = NormalizarUbicacion(tpl.Ubicacion),
				tpl.Cantidad,
				tpl.Lote
			}));

			// Agrupar por ubicación
			var lineasPorUbicacion = todasLasLineas
				.GroupBy(l => new { l.CodigoAlmacen, l.Ubicacion })
				.ToList();

			foreach (var grupo in lineasPorUbicacion)
			{
				var ubicacion = grupo.Key.Ubicacion ?? string.Empty;
				var esUbicacionNormal = !string.IsNullOrEmpty(ubicacion) && ubicacion.StartsWith("UB", StringComparison.OrdinalIgnoreCase);

				// Buscar líneas de inventario temporales para esta ubicación
				var lineasInventario = await _auroraSgaContext.InventarioLineasTemp
					.Where(ilt => !ilt.Consolidado && 
								  ilt.CodigoUbicacion == ubicacion &&
								  grupo.Any(l => l.CodigoArticulo == ilt.CodigoArticulo))
					.ToListAsync();

				foreach (var lineaInventario in lineasInventario)
				{
					var diferencia = (lineaInventario.CantidadContada ?? 0) - lineaInventario.StockActual;

					if (Math.Abs(diferencia) > 0.001m) // Hay diferencia significativa
					{
						if (esUbicacionNormal)
						{
							// Ubicación normal: modificar visualmente el palet existente
							var lineaPalet = grupo.FirstOrDefault(l => l.CodigoArticulo == lineaInventario.CodigoArticulo);
							if (lineaPalet != null)
							{
								// Buscar la línea real del palet para modificarla
								var lineaPaletReal = lineasPalet.FirstOrDefault(pl => 
									pl.CodigoArticulo == lineaInventario.CodigoArticulo && 
										NormalizarUbicacion(pl.Ubicacion) == ubicacion);
								
								if (lineaPaletReal != null)
								{
									lineaPaletReal.Cantidad = lineaInventario.CantidadContada ?? 0;
									_logger.LogInformation($"Palet modificado visualmente: {lineaPaletReal.CodigoArticulo} en {ubicacion}, nueva cantidad: {lineaPaletReal.Cantidad}");
								}
							}
						}
						else
						{
							// Ubicación especial: NO modificamos palets, el servicio externo se encarga
							_logger.LogInformation($"Stock sin paletizar: {lineaInventario.CodigoArticulo} en {ubicacion}, diferencia: {diferencia} - El servicio externo se encargará");
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error al aplicar lógica de inventario al cerrar palet {PaletId}", paletId);
		}
	}

	#region POST: Reabrir palet
	[HttpPost("{id}/reabrir")]
	public async Task<IActionResult> ReabrirPalet(Guid id, [FromQuery] int usuarioId)
	{
		var palet = await _auroraSgaContext.Palets.FindAsync(id);
		if (palet == null)
			return NotFound("Palet no encontrado");

		// 🚫 Control de vaciado
		if (string.Equals(palet.Estado, "Vaciado", StringComparison.OrdinalIgnoreCase))
			return BadRequest("El palet está Vaciado y no puede reabrirse.");

		if (string.Equals(palet.Estado, "Abierto", StringComparison.OrdinalIgnoreCase))
			return BadRequest("El palet ya está abierto.");

		palet.Estado = "Abierto";
		palet.FechaApertura = DateTime.Now;
		palet.UsuarioAperturaId = usuarioId;
		palet.FechaCierre = null;
		palet.UsuarioCierreId = null;

		_auroraSgaContext.LogPalet.Add(new LogPalet
		{
			PaletId = palet.Id,
			Fecha = DateTime.Now,
			IdUsuario = usuarioId,
			Accion = "Reabrir",
			Detalle = "Palet reabierto por el usuario: " + usuarioId
		});

		// Cancela traspaso pendiente si lo hay
		var traspaso = await _auroraSgaContext.Traspasos
			.Where(t => t.PaletId == id && t.CodigoEstado == "PENDIENTE")
			.FirstOrDefaultAsync();

		if (traspaso != null)
		{
			traspaso.CodigoEstado = "CANCELADO";
			traspaso.FechaFinalizacion = DateTime.Now;
			traspaso.UsuarioFinalizacionId = usuarioId;
			traspaso.UbicacionDestino = "N/A";
			_auroraSgaContext.Traspasos.Update(traspaso);

			_auroraSgaContext.LogPalet.Add(new LogPalet
			{
				PaletId = palet.Id,
				Fecha = DateTime.Now,
				IdUsuario = usuarioId,
				Accion = "CancelarTraspaso",
				Detalle = $"Traspaso {traspaso.Id} cancelado al reabrir el palet"
			});
		}

		_auroraSgaContext.Palets.Update(palet);
		await _auroraSgaContext.SaveChangesAsync();

		return Ok(new { message = $"Palet {palet.Codigo} reabierto correctamente. Traspaso pendiente cancelado." });
	}

	#endregion

	#region GET: Por CódigoGS1
	[HttpGet("by-gs1/{codigoGS1}", Name = "GetPaletByGS1")]
	public async Task<ActionResult<PaletDto>> GetPaletByCodigoGS1(string codigoGS1)
	{
		var palet = await _auroraSgaContext.Palets
			.FirstOrDefaultAsync(p => p.CodigoGS1 == codigoGS1);

		if (palet == null)
			return NotFound();

		var nombreDict = await _auroraSgaContext.vUsuariosConNombre
			.ToDictionaryAsync(x => x.UsuarioId, x => x.NombreOperario);

		var dto = new PaletDto
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
			Altura = palet.Altura,
			Peso = palet.Peso,
			EtiquetaGenerada = palet.EtiquetaGenerada,
			IsVaciado = palet.IsVaciado,
			FechaVaciado = palet.FechaVaciado,
			OrdenTrabajoId = palet.OrdenTrabajoId,
			CodigoGS1 = palet.CodigoGS1
		};

		if (dto.UsuarioAperturaId.HasValue && nombreDict.TryGetValue(dto.UsuarioAperturaId.Value, out var nombreA))
			dto.UsuarioAperturaNombre = nombreA;

		if (dto.UsuarioCierreId.HasValue && nombreDict.TryGetValue(dto.UsuarioCierreId.Value, out var nombreC))
			dto.UsuarioCierreNombre = nombreC;

		return Ok(dto);
	}
	#endregion

	// Consolidar líneas temporales no procesadas por artículo, lote, fecha, almacén, ubicación, unidad de medida
	private List<TempPaletLinea> ConsolidarLineas(List<TempPaletLinea> lineas)
	{
		return lineas
			.GroupBy(l => new
			{
				l.CodigoArticulo,
				l.Lote,
				l.FechaCaducidad,
				l.CodigoAlmacen,
				l.Ubicacion,
				l.UnidadMedida
			})
			.Select(g => new TempPaletLinea
			{
				PaletId = g.First().PaletId,
				CodigoEmpresa = g.First().CodigoEmpresa,
				CodigoArticulo = g.Key.CodigoArticulo,
				DescripcionArticulo = g.First().DescripcionArticulo,
				Cantidad = g.Sum(x => x.Cantidad),
				UnidadMedida = g.Key.UnidadMedida,
				Lote = g.Key.Lote,
				FechaCaducidad = g.Key.FechaCaducidad,
				CodigoAlmacen = g.Key.CodigoAlmacen,
				Ubicacion = g.Key.Ubicacion,
				UsuarioId = g.First().UsuarioId,
				FechaAgregado = DateTime.Now,
				Observaciones = g.Select(x => x.Observaciones).FirstOrDefault(x => !string.IsNullOrEmpty(x)),
				Procesada = false,
				EsHeredada = false
			})
			.ToList();
	}

	[HttpPost("{id}/cerrar-mobility")]
	public async Task<IActionResult> CerrarPaletMobility(Guid id, [FromBody] CerrarPaletMobilityDto dto)
	{
		var palet = await _auroraSgaContext.Palets.FindAsync(id);
		if (palet == null)
			return NotFound("Palet no encontrado");

		if (palet.Estado == "Cerrado")
			return BadRequest("El palet ya está cerrado.");

		// Verifica que tenga al menos una línea
		bool tieneLineas = await _auroraSgaContext.TempPaletLineas.AnyAsync(l => l.PaletId == id)
			|| await _auroraSgaContext.PaletLineas.AnyAsync(l => l.PaletId == id);

		if (!tieneLineas)
			return BadRequest("No se puede cerrar un palet vacío. Debe tener al menos una línea.");

	// === LÓGICA MEJORADA: Detectar si estamos moviendo material de un palet existente ===
	var lineasDefinitivas = await _auroraSgaContext.PaletLineas
		.Where(l => l.PaletId == id)
		.ToListAsync();
	
	// Obtener líneas temporales existentes (creadas por Android al escanear artículos)
	var lineasTemporalesExistentes = await _auroraSgaContext.TempPaletLineas
		.Where(l => l.PaletId == id && l.Procesada == false && l.EsHeredada == false)
		.ToListAsync();
	
	foreach (var def in lineasDefinitivas)
	{
		var ubicacionDef = NormalizarUbicacion(def.Ubicacion);

		// Buscar si hay una línea temporal para este mismo artículo/lote
		var tempExistente = lineasTemporalesExistentes.FirstOrDefault(t =>
			t.CodigoArticulo == def.CodigoArticulo &&
			t.Lote == def.Lote &&
			string.Equals((t.CodigoAlmacen ?? string.Empty).Trim(), (def.CodigoAlmacen ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase) &&
			NormalizarUbicacion(t.Ubicacion) == ubicacionDef);
		
		if (tempExistente != null)
		{
			// Si la temporal es MENOR que la definitiva, puede ser:
			// 1. Movimiento parcial REAL: se está sacando material del palet (cambió ubicación o está en ubicación vacía vs ubicación real)
			// 2. Solo añadiendo material: se añadió material nuevo pero la temporal tiene menos porque solo registra lo nuevo
			// Solo generamos negativo si la temporal tiene una ubicación REAL diferente a la definitiva, 
			// lo que indica un movimiento parcial real, no solo añadir material
			var ubicacionTemp = NormalizarUbicacion(tempExistente.Ubicacion);
			var esMovimientoParcialReal = !string.IsNullOrEmpty(ubicacionDef) && 
				!string.IsNullOrEmpty(ubicacionTemp) && 
				ubicacionDef != ubicacionTemp &&
				tempExistente.Cantidad < def.Cantidad;
			
			if (esMovimientoParcialReal)
			{
				var diferencia = def.Cantidad - tempExistente.Cantidad;
				
				// Crear línea temporal NEGATIVA para reducir el stock del palet origen
				var tempNegativa = new TempPaletLinea
				{
					PaletId = def.PaletId,
					CodigoEmpresa = def.CodigoEmpresa,
					CodigoArticulo = def.CodigoArticulo,
					DescripcionArticulo = def.DescripcionArticulo,
					Cantidad = -diferencia, // CANTIDAD NEGATIVA
					UnidadMedida = def.UnidadMedida,
					Lote = def.Lote,
					FechaCaducidad = def.FechaCaducidad,
					CodigoAlmacen = def.CodigoAlmacen, // UBICACIÓN ORIGEN
					Ubicacion = ubicacionDef, // UBICACIÓN ORIGEN
					UsuarioId = dto.UsuarioId,
					FechaAgregado = DateTime.Now,
					Observaciones = "Delta negativo por movimiento parcial de palet",
					Procesada = false,
					EsHeredada = false,
					TraspasoId = null // Se asignará después
				};
				_auroraSgaContext.TempPaletLineas.Add(tempNegativa);
				_logger.LogInformation($"✅ Creada línea temporal NEGATIVA: Articulo={def.CodigoArticulo}, Cantidad={tempNegativa.Cantidad}, Ubicacion={def.CodigoAlmacen}-{def.Ubicacion}");
			}
			else if (tempExistente.Cantidad >= def.Cantidad)
			{
				// La temporal tiene más o igual cantidad: se añadió material, usar la temporal (ya incluye todo)
				_logger.LogInformation($"ℹ️ CerrarPaletMobility: Temporal >= Definitiva, usando temporal (añadido material). Articulo={def.CodigoArticulo}, Temporal={tempExistente.Cantidad}, Definitiva={def.Cantidad}");
			}
			else
			{
				// La temporal es menor pero están en la misma ubicación (o ambas vacías): 
				// probablemente solo añadió material nuevo, heredar la definitiva y la temporal se sumará después
				_logger.LogInformation($"ℹ️ CerrarPaletMobility: Temporal < Definitiva pero misma ubicación, heredando definitiva. Articulo={def.CodigoArticulo}, Temporal={tempExistente.Cantidad}, Definitiva={def.Cantidad}");
				var temp = new TempPaletLinea
				{
					PaletId = def.PaletId,
					CodigoEmpresa = def.CodigoEmpresa,
					CodigoArticulo = def.CodigoArticulo,
					DescripcionArticulo = def.DescripcionArticulo,
					Cantidad = def.Cantidad,
					UnidadMedida = def.UnidadMedida,
					Lote = def.Lote,
					FechaCaducidad = def.FechaCaducidad,
					CodigoAlmacen = def.CodigoAlmacen,
					Ubicacion = ubicacionDef,
					UsuarioId = def.UsuarioId,
					FechaAgregado = DateTime.Now,
					Observaciones = def.Observaciones,
					Procesada = false,
					EsHeredada = true // Marcar como heredada
				};
				_auroraSgaContext.TempPaletLineas.Add(temp);
			}
		}
		else
		{
			// No hay línea temporal para este artículo, copiar la definitiva como heredada
			_logger.LogInformation($"ℹ️ DEBUG CerrarPaletMobility: No hay línea temporal, copiando definitiva como heredada");
			
			var temp = new TempPaletLinea
			{
				PaletId = def.PaletId,
				CodigoEmpresa = def.CodigoEmpresa,
				CodigoArticulo = def.CodigoArticulo,
				DescripcionArticulo = def.DescripcionArticulo,
				Cantidad = def.Cantidad,
				UnidadMedida = def.UnidadMedida,
				Lote = def.Lote,
				FechaCaducidad = def.FechaCaducidad,
				CodigoAlmacen = def.CodigoAlmacen,
					Ubicacion = ubicacionDef,
				UsuarioId = def.UsuarioId,
				FechaAgregado = DateTime.Now,
				Observaciones = def.Observaciones,
				Procesada = false,
				EsHeredada = true // Marcar como heredada
			};
			_auroraSgaContext.TempPaletLineas.Add(temp);
		}
	}
	await _auroraSgaContext.SaveChangesAsync();

		// Recarga las líneas temporales después de guardar
		var lineasTemporales = await _auroraSgaContext.TempPaletLineas
			.Where(l => l.PaletId == id && l.Procesada == false)
			.ToListAsync();

		// Cierra el palet
		palet.Estado = "Cerrado";
		palet.FechaCierre = DateTime.Now;
		palet.UsuarioCierreId = dto.UsuarioId;
		if (dto.Altura.HasValue) palet.Altura = dto.Altura;
		if (dto.Peso.HasValue) palet.Peso = dto.Peso;

		_auroraSgaContext.LogPalet.Add(new LogPalet
		{
			PaletId = palet.Id,
			Fecha = DateTime.Now,
			IdUsuario = dto.UsuarioId,
			Accion = "CerrarMobility",
			Detalle = $"Palet cerrado por usuario {dto.UsuarioId} desde Mobility"
		});

	var traspasosCreados = new List<Guid>();
	foreach (var linea in lineasTemporales)
	{
		var codigoAlmacenLinea = (linea.CodigoAlmacen ?? string.Empty).Trim();
		var ubicacionLinea = NormalizarUbicacion(linea.Ubicacion);
		linea.CodigoAlmacen = codigoAlmacenLinea;
		linea.Ubicacion = ubicacionLinea;

		var traspaso = new Traspaso
		{
			Id = Guid.NewGuid(),
			PaletId = palet.Id,
			CodigoPalet = palet.Codigo,
			TipoTraspaso = "PALET",
			CodigoEstado = "PENDIENTE",
			FechaInicio = DateTime.Now,
			UsuarioInicioId = dto.UsuarioId,
			AlmacenOrigen = codigoAlmacenLinea,
			CodigoEmpresa = linea.CodigoEmpresa,
			CodigoArticulo = linea.CodigoArticulo,
			UbicacionOrigen = ubicacionLinea,
			Cantidad = linea.Cantidad,
			Partida = linea.Lote,
			FechaCaducidad = linea.FechaCaducidad,
			Comentario = dto.Comentario,
			EsNotificado = false,
			OrigenTraspaso = "AuroraSGA"
		};
		_auroraSgaContext.Traspasos.Add(traspaso);
		traspasosCreados.Add(traspaso.Id);

		// Asociar el TraspasoId a la línea temporal correspondiente (palet destino)
		linea.TraspasoId = traspaso.Id;
		_auroraSgaContext.TempPaletLineas.Update(linea);
		
		// === US-002: Buscar y asociar líneas temporales NEGATIVAS en otros palets ===
		// Buscar líneas negativas que se crearon para este mismo artículo/lote/ubicación
		var lineasNegativasRelacionadas = await _auroraSgaContext.TempPaletLineas
			.Where(tpl => 
				tpl.PaletId != id && // Diferente palet (el origen)
				tpl.CodigoArticulo == linea.CodigoArticulo &&
				tpl.Lote == linea.Lote &&
				tpl.CodigoAlmacen == codigoAlmacenLinea &&
				tpl.Ubicacion == ubicacionLinea &&
				tpl.Procesada == false &&
				tpl.TraspasoId == null && // Sin traspaso asignado aún
				tpl.Cantidad < 0 && // Solo líneas NEGATIVAS
				tpl.Observaciones == "Delta negativo por extracción de material del palet")
			.ToListAsync();
		
		foreach (var lineaNegativa in lineasNegativasRelacionadas)
		{
			lineaNegativa.TraspasoId = traspaso.Id;
			_auroraSgaContext.TempPaletLineas.Update(lineaNegativa);
			_logger.LogInformation($"✅ Asignado TraspasoId={traspaso.Id} a línea NEGATIVA en palet origen: PaletId={lineaNegativa.PaletId}, Cantidad={lineaNegativa.Cantidad}");
		}
	}
	await _auroraSgaContext.SaveChangesAsync();

		return Ok(new
		{
			message = $"Palet {palet.Codigo} cerrado correctamente y traspasos pendientes creados.",
			paletId = palet.Id,
			traspasosIds = traspasosCreados
		});
	}

	[HttpPost("{id}/completar-traspaso")]
	public async Task<IActionResult> CompletarTraspaso(Guid id, [FromBody] CompletarTraspasoDto dto)
	{
		_logger.LogInformation($"🚨 DEBUG: EJECUTANDO CompletarTraspaso - TraspasoId={id}, UsuarioId={dto.UsuarioFinalizacionId}");
		var traspaso = await _auroraSgaContext.Traspasos.FindAsync(id);
		if (traspaso == null)
			return NotFound("Traspaso no encontrado");

		if (traspaso.CodigoEstado != "PENDIENTE")
			return BadRequest("Solo se pueden completar traspasos en estado PENDIENTE.");

		// Actualiza los datos de destino y finalización
		traspaso.AlmacenDestino = dto.CodigoAlmacenDestino;
		traspaso.UbicacionDestino = dto.UbicacionDestino;
		traspaso.FechaFinalizacion = DateTime.Now;
		traspaso.UsuarioFinalizacionId = dto.UsuarioFinalizacionId;
		traspaso.CodigoEstado = "PENDIENTE_ERP";

		// === CORRECCIÓN: NO crear líneas temporales automáticamente ===
		// Las líneas temporales deben crearse cuando se hace el traspaso real,
		// no cuando se completa el traspaso. El CompletarTraspasoDto no tiene
		// información sobre la cantidad específica que se está moviendo.
		_logger.LogInformation($"ℹ️ CompletarTraspaso: Solo actualizando traspaso, NO creando líneas temporales. PaletId={traspaso.PaletId}, Articulo={traspaso.CodigoArticulo}");

		_auroraSgaContext.Traspasos.Update(traspaso);
		await _auroraSgaContext.SaveChangesAsync();

		return Ok(new { message = "Traspaso completado correctamente." });
	}
	[HttpGet("estado-en-ubicacion")]
	public async Task<IActionResult> GetEstadoPaletEnUbicacion(
[FromQuery] int codigoEmpresa,
[FromQuery] string codigoAlmacen,
[FromQuery] string? ubicacion = null)
{
    var codigoAlmacenNorm = codigoAlmacen.Trim().ToUpper();
    
    // Buscar palet Abierto en la ubicación destino
    var paletAbierto = string.IsNullOrWhiteSpace(ubicacion) 
        ? await (
            from p in _auroraSgaContext.Palets
            join l in _auroraSgaContext.PaletLineas on p.Id equals l.PaletId
            where p.CodigoEmpresa == codigoEmpresa
                && l.CodigoAlmacen.Trim().ToUpper() == codigoAlmacenNorm
                && (l.Ubicacion == null || l.Ubicacion == "" || l.Ubicacion.Trim() == "")
                && p.Estado == "Abierto"
            orderby p.FechaApertura descending
            select new { p.Id, p.Codigo, p.Estado }
        ).FirstOrDefaultAsync()
        : await (
            from p in _auroraSgaContext.Palets
            join l in _auroraSgaContext.PaletLineas on p.Id equals l.PaletId
            where p.CodigoEmpresa == codigoEmpresa
                && l.CodigoAlmacen.Trim().ToUpper() == codigoAlmacenNorm
                && l.Ubicacion.Trim().ToUpper() == ubicacion.Trim().ToUpper()
                && p.Estado == "Abierto"
            orderby p.FechaApertura descending
            select new { p.Id, p.Codigo, p.Estado }
        ).FirstOrDefaultAsync();

    if (paletAbierto != null)
    {
        return Ok(new
        {
            estado = "Abierto",
            paletId = paletAbierto.Id,
            codigo = paletAbierto.Codigo
        });
    }

    // Buscar palet Cerrado en la ubicación destino
    var paletCerrado = string.IsNullOrWhiteSpace(ubicacion) 
        ? await (
            from p in _auroraSgaContext.Palets
            join l in _auroraSgaContext.PaletLineas on p.Id equals l.PaletId
            where p.CodigoEmpresa == codigoEmpresa
                && l.CodigoAlmacen.Trim().ToUpper() == codigoAlmacenNorm
                && (l.Ubicacion == null || l.Ubicacion == "" || l.Ubicacion.Trim() == "")
                && p.Estado == "Cerrado"
            orderby p.FechaCierre descending
            select new { p.Id, p.Codigo, p.Estado }
        ).FirstOrDefaultAsync()
        : await (
            from p in _auroraSgaContext.Palets
            join l in _auroraSgaContext.PaletLineas on p.Id equals l.PaletId
            where p.CodigoEmpresa == codigoEmpresa
                && l.CodigoAlmacen.Trim().ToUpper() == codigoAlmacenNorm
                && l.Ubicacion.Trim().ToUpper() == ubicacion.Trim().ToUpper()
                && p.Estado == "Cerrado"
            orderby p.FechaCierre descending
            select new { p.Id, p.Codigo, p.Estado }
        ).FirstOrDefaultAsync();

    if (paletCerrado != null)
    {
        return Ok(new
        {
            estado = "Cerrado",
            paletId = paletCerrado.Id,
            codigo = paletCerrado.Codigo
        });
    }

    // No hay palet
    return Ok(new { estado = "NINGUNO" });
}

public class TraspasoErrorDto
{
	public Guid TraspasoId { get; set; }
	public Guid PaletId { get; set; }
	public string? CodigoPalet { get; set; }
	public string? CodigoArticulo { get; set; }
	public decimal Cantidad { get; set; }
	public string? AlmacenOrigen { get; set; }
	public string? UbicacionOrigen { get; set; }
	public string? AlmacenDestino { get; set; }
	public string? UbicacionDestino { get; set; }
	public DateTime FechaInicio { get; set; }
	public DateTime? FechaFinalizacion { get; set; }
	public string CodigoEstado { get; set; } = string.Empty;
	public string? Comentario { get; set; }
	public string? EstadoErp { get; set; }
	public int UsuarioInicioId { get; set; }
	public string? UsuarioInicioNombre { get; set; }
	public int? UsuarioFinalizacionId { get; set; }
	public string? UsuarioFinalizacionNombre { get; set; }
	public short CodigoEmpresa { get; set; }
	public DateTime? FechaCaducidad { get; set; }
	public string? Partida { get; set; }
}

public class RelanzarTraspasoDto
{
	public int UsuarioId { get; set; }
	public string? Comentario { get; set; }
}

	[HttpPost("{id}/marcar-vaciado")]
	public async Task<IActionResult> MarcarVaciado(Guid id, [FromQuery] int usuarioId, [FromQuery] bool forzar = false)
	{
		var palet = await _auroraSgaContext.Palets.FindAsync(id);
		if (palet == null) return NotFound("Palet no encontrado.");

		if (string.Equals(palet.Estado, "Vaciado", StringComparison.OrdinalIgnoreCase))
			return BadRequest("El palet ya está marcado como Vaciado.");

		// si no es "forzar", comprobamos que no queden líneas
		var quedanLineas = await _auroraSgaContext.PaletLineas.AnyAsync(l => l.PaletId == id);
		if (quedanLineas && !forzar)
			return BadRequest("El palet aún tiene líneas. No se puede marcar Vaciado.");

		palet.Estado = "Vaciado";
		palet.FechaVaciado = DateTime.Now;
		palet.UsuarioVaciadoId = usuarioId;

		// si quieres, también cierra
		palet.FechaCierre = DateTime.Now;
		palet.UsuarioCierreId = usuarioId;

		_auroraSgaContext.Palets.Update(palet);
		_auroraSgaContext.LogPalet.Add(new LogPalet
		{
			PaletId = palet.Id,
			Fecha = DateTime.Now,
			IdUsuario = usuarioId,
			Accion = "Vaciado",
			Detalle = "Marcado como palet vaciado (desmontado)."
		});

		await _auroraSgaContext.SaveChangesAsync();
		return Ok(new { message = $"Palet {palet.Codigo} marcado como Vaciado." });
	}

	[HttpPost("marcar-vaciados-sin-lineas")]
	public async Task<IActionResult> MarcarVaciadosSinLineas([FromQuery] int? usuarioId = null)
	{
		var fechaActual = DateTime.Now;

		var paletsSinLineas = await (
			from p in _auroraSgaContext.Palets
			where !_auroraSgaContext.TempPaletLineas.Any(tpl => tpl.PaletId == p.Id)
				&& !_auroraSgaContext.PaletLineas.Any(pl => pl.PaletId == p.Id)
				&& p.Estado != "Vaciado"
			select p
		).ToListAsync();

		if (!paletsSinLineas.Any())
		{
			return Ok(new { message = "No se encontraron palets sin líneas para marcar como vaciados.", cantidad = 0 });
		}

		var cantidadActualizados = 0;
		foreach (var palet in paletsSinLineas)
		{
			palet.Estado = "Vaciado";
			palet.FechaVaciado = fechaActual;
			palet.IsVaciado = true;

			if (usuarioId.HasValue)
			{
				palet.UsuarioVaciadoId = usuarioId.Value;
				palet.UsuarioCierreId = usuarioId.Value;
			}

			if (!palet.FechaCierre.HasValue)
			{
				palet.FechaCierre = fechaActual;
			}

			_auroraSgaContext.Palets.Update(palet);

			if (usuarioId.HasValue)
			{
				_auroraSgaContext.LogPalet.Add(new LogPalet
				{
					PaletId = palet.Id,
					Fecha = fechaActual,
					IdUsuario = usuarioId.Value,
					Accion = "Vaciado",
					Detalle = "Marcado automáticamente como vaciado por no tener líneas temporales ni definitivas."
				});
			}

			cantidadActualizados++;
		}

		await _auroraSgaContext.SaveChangesAsync();

		_logger.LogInformation($"Se marcaron {cantidadActualizados} palets como vaciados automáticamente.");

		return Ok(new
		{
			message = $"Se marcaron {cantidadActualizados} palet(s) como vaciados.",
			cantidad = cantidadActualizados,
			palets = paletsSinLineas.Select(p => new { p.Id, p.Codigo, p.Estado })
		});
	}

	#region GET: Palets pendientes de vaciado
	[HttpGet("pendientes-vaciado")]
	public async Task<ActionResult<List<PaletPendienteVaciadoDto>>> GetPaletsPendientesVaciado([FromQuery] short? codigoEmpresa = null)
	{
		var lineasQuery =
			from p in _auroraSgaContext.Palets.AsNoTracking()
			join pl in _auroraSgaContext.PaletLineas.AsNoTracking() on p.Id equals pl.PaletId
			where !p.IsVaciado
				  && p.Estado != "Vaciado"
				  && pl.Cantidad > 0
			select new LineaPendienteRaw
			{
				PaletId = p.Id,
				CodigoPalet = p.Codigo,
				CodigoEmpresa = p.CodigoEmpresa,
				LineaId = pl.Id,
				CodigoArticulo = pl.CodigoArticulo,
				DescripcionArticulo = pl.DescripcionArticulo,
				Cantidad = pl.Cantidad,
				CodigoAlmacen = pl.CodigoAlmacen,
				Ubicacion = pl.Ubicacion,
				Lote = pl.Lote,
				FechaCaducidad = pl.FechaCaducidad
			};

		if (codigoEmpresa.HasValue && codigoEmpresa.Value > 0)
		{
			lineasQuery = lineasQuery.Where(x => x.CodigoEmpresa == codigoEmpresa.Value);
		}

		var lineas = await lineasQuery.ToListAsync();

		if (!lineas.Any())
		{
			return Ok(new List<PaletPendienteVaciadoDto>());
		}

		var empresas = lineas.Select(l => l.CodigoEmpresa).Distinct().ToList();
		var articulos = lineas.Select(l => l.CodigoArticulo).Distinct().ToList();
		var almacenes = lineas.Select(l => l.CodigoAlmacen).Distinct().ToList();

		var stock = await _auroraSgaContext.StockDisponible.AsNoTracking()
			.Where(sd => empresas.Contains(sd.CodigoEmpresa)
						 && articulos.Contains(sd.CodigoArticulo)
						 && almacenes.Contains(sd.CodigoAlmacen))
			.ToListAsync();

		var stockLookup = stock
			.GroupBy(sd => BuildStockKey(sd.CodigoEmpresa, sd.CodigoArticulo, sd.CodigoAlmacen, sd.Ubicacion, sd.Partida))
			.ToDictionary(g => g.Key, g => g.Sum(x => x.Disponible));

		const decimal tolerance = 0.000001m;
		var lineasAfectadas = new List<LineaPendienteRaw>();

		foreach (var linea in lineas)
		{
			var key = BuildStockKey(linea.CodigoEmpresa, linea.CodigoArticulo, linea.CodigoAlmacen, linea.Ubicacion, linea.Lote);
			stockLookup.TryGetValue(key, out var disponible);

			if (disponible + tolerance < linea.Cantidad)
			{
				linea.StockDisponible = disponible;
				linea.Faltante = Math.Max(linea.Cantidad - disponible, 0m);
				lineasAfectadas.Add(linea);
			}
		}

		if (!lineasAfectadas.Any())
		{
			return Ok(new List<PaletPendienteVaciadoDto>());
		}

		var resultado = lineasAfectadas
			.GroupBy(l => new { l.PaletId, l.CodigoPalet, l.CodigoEmpresa })
			.Select(g => new PaletPendienteVaciadoDto
			{
				PaletId = g.Key.PaletId,
				CodigoPalet = g.Key.CodigoPalet,
				CodigoEmpresa = g.Key.CodigoEmpresa,
				Observacion = "Stock no encontrado en la ubicación registrada.",
				Lineas = g.Select(l => new LineaPendienteVaciadoDto
				{
					LineaId = l.LineaId,
					CodigoArticulo = l.CodigoArticulo,
					DescripcionArticulo = l.DescripcionArticulo,
					CantidadRegistrada = l.Cantidad,
					CantidadDisponible = l.StockDisponible,
					CantidadFaltante = l.Faltante,
					CodigoAlmacen = l.CodigoAlmacen,
					Ubicacion = l.Ubicacion ?? string.Empty,
					Lote = l.Lote,
					FechaCaducidad = l.FechaCaducidad
				}).ToList()
			})
			.OrderByDescending(p => p.Lineas.Count)
			.ThenBy(p => p.CodigoPalet)
			.ToList();

		// Registrar evento de consulta de stock
		var detalleConsulta = codigoEmpresa.HasValue ? $"Empresa={codigoEmpresa.Value}" : "Empresa=(todas)";
		detalleConsulta += $", PaletsConsultados={lineasAfectadas.Select(l => l.PaletId).Distinct().Count()}, LineasAfectadas={lineasAfectadas.Count}, Resultados={resultado.Count}";
		
		RegistrarEventoConsultaStockAsync(
			"PaletController/GetPaletsPendientesVaciado",
			"Consulta de stock para palets pendientes de vaciado",
			detalleConsulta);

		return Ok(resultado);
	}

	/// <summary>
	/// Registra un evento de consulta de stock en log_eventos
	/// </summary>
	private void RegistrarEventoConsultaStockAsync(string tipoConsulta, string descripcion, string? detalle = null)
	{
		try
		{
			// Capturar el token ANTES de cualquier operación asíncrona (Request se libera después de la respuesta)
			string? token = null;
			try
			{
				if (Request?.Headers != null && Request.Headers.TryGetValue("Authorization", out var authHeader) &&
					authHeader.ToString().StartsWith("Bearer "))
				{
					token = authHeader.ToString().Substring("Bearer ".Length).Trim();
					_logger.LogInformation("✅ Token capturado para evento: {TipoConsulta}", tipoConsulta);
				}
				else
				{
					_logger.LogWarning("⚠️ No se encontró header Authorization para evento: {TipoConsulta}", tipoConsulta);
				}
			}
			catch (ObjectDisposedException)
			{
				_logger.LogWarning("⚠️ Request ya fue liberado, no se puede registrar evento: {TipoConsulta}", tipoConsulta);
				return;
			}

			if (string.IsNullOrWhiteSpace(token))
			{
				_logger.LogWarning("⚠️ No se pudo obtener el token para registrar evento de consulta stock: {TipoConsulta}", tipoConsulta);
				return; // No hay token, no registramos evento
			}

			// Capturar variables locales para usar en el Task.Run
			var tokenCapturado = token;
			var tipoConsultaCapturado = tipoConsulta;
			var descripcionCapturada = descripcion;
			var detalleCapturado = detalle;

			// Ejecutar en background sin bloquear la respuesta
			_ = Task.Run(async () =>
			{
				try
				{
					// Verificar si el servicio provider está disponible (puede estar liberado durante el cierre)
					if (_serviceProvider == null)
						return;

					// Crear un scope para obtener un nuevo DbContext (thread-safe)
					IServiceScope? scope = null;
					try
					{
						scope = _serviceProvider.CreateScope();
					}
					catch (ObjectDisposedException)
					{
						// La aplicación se está cerrando, ignorar silenciosamente
						return;
					}

					using (scope)
					{
						var dbContext = scope.ServiceProvider.GetRequiredService<AuroraSgaDbContext>();
						var logger = scope.ServiceProvider.GetRequiredService<ILogger<PaletController>>();

						var dispositivo = await dbContext.Dispositivos
							.FirstOrDefaultAsync(d => d.SessionToken == tokenCapturado && d.Activo == -1);

						if (dispositivo == null)
						{
							logger.LogWarning("⚠️ Dispositivo no encontrado para token al registrar evento: {TipoConsulta}", tipoConsultaCapturado);
							return; // Dispositivo no encontrado, no registramos evento
						}

						var logEvento = new LogEvento
						{
							Fecha = DateTime.Now,
							IdUsuario = dispositivo.IdUsuario,
							IdDispositivo = dispositivo.Id,
							Tipo = "CONSULTA_STOCK",
							Origen = tipoConsultaCapturado,
							Descripcion = descripcionCapturada,
							Detalle = detalleCapturado
						};

						dbContext.LogEventos.Add(logEvento);
						await dbContext.SaveChangesAsync();
						
						logger.LogInformation("✅ Evento de consulta stock registrado: {TipoConsulta}, Usuario: {UsuarioId}, Dispositivo: {DispositivoId}", 
							tipoConsultaCapturado, dispositivo.IdUsuario, dispositivo.Id);
					}
				}
				catch (ObjectDisposedException)
				{
					// La aplicación se está cerrando, ignorar silenciosamente
					return;
				}
				catch (Exception ex)
				{
					// Loggear el error solo si el logger está disponible
					try
					{
						if (_serviceProvider != null)
						{
							using var scope = _serviceProvider.CreateScope();
							var logger = scope.ServiceProvider.GetRequiredService<ILogger<PaletController>>();
							logger.LogError(ex, "❌ Error al registrar evento de consulta stock: {TipoConsulta}", tipoConsultaCapturado);
						}
					}
					catch
					{
						// Si no se puede loggear, ignorar silenciosamente
					}
				}
			});
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "❌ Error al capturar token para evento de consulta stock: {TipoConsulta}", tipoConsulta);
		}
	}

	private static string BuildStockKey(short codigoEmpresa, string codigoArticulo, string codigoAlmacen, string? ubicacion, string? partida)
	{
		return $"{codigoEmpresa}|{Normalize(codigoArticulo)}|{Normalize(codigoAlmacen)}|{Normalize(ubicacion)}|{Normalize(partida)}";
	}

	private static string Normalize(string? value)
	{
		return string.IsNullOrWhiteSpace(value)
			? string.Empty
			: value.Trim().ToUpperInvariant();
	}

	private class LineaPendienteRaw
	{
		public Guid PaletId { get; set; }
		public string CodigoPalet { get; set; } = string.Empty;
		public short CodigoEmpresa { get; set; }
		public Guid LineaId { get; set; }
		public string CodigoArticulo { get; set; } = string.Empty;
		public string? DescripcionArticulo { get; set; }
		public decimal Cantidad { get; set; }
		public string CodigoAlmacen { get; set; } = string.Empty;
		public string? Ubicacion { get; set; }
		public string? Lote { get; set; }
		public DateTime? FechaCaducidad { get; set; }
		public decimal StockDisponible { get; set; }
		public decimal Faltante { get; set; }
	}
	#endregion

	#region GET: Traspasos PALET con ERROR_ERP
	[HttpGet("traspasos/error-erp")]
	public async Task<ActionResult<List<TraspasoErrorDto>>> GetTraspasosErrorErp([FromQuery] short? codigoEmpresa = null)
	{
		var usuariosDict = await _auroraSgaContext.vUsuariosConNombre
			.ToDictionaryAsync(u => u.UsuarioId, u => u.NombreOperario);

		var query = _auroraSgaContext.Traspasos
			.Where(t => t.TipoTraspaso == "PALET" && t.CodigoEstado == "ERROR_ERP");

		if (codigoEmpresa.HasValue && codigoEmpresa.Value > 0)
		{
			query = query.Where(t => t.CodigoEmpresa == codigoEmpresa.Value);
		}

		var traspasos = await query
			.OrderByDescending(t => t.FechaInicio)
			.ToListAsync();

		var resultado = traspasos.Select(t =>
		{
			usuariosDict.TryGetValue(t.UsuarioInicioId, out var usuarioInicioNombre);
			string? usuarioFinNombre = null;
			if (t.UsuarioFinalizacionId.HasValue)
			{
				usuariosDict.TryGetValue(t.UsuarioFinalizacionId.Value, out usuarioFinNombre);
			}

			return new TraspasoErrorDto
			{
				TraspasoId = t.Id,
				PaletId = t.PaletId,
				CodigoPalet = t.CodigoPalet,
				CodigoArticulo = t.CodigoArticulo,
				Cantidad = t.Cantidad ?? 0m,
				AlmacenOrigen = t.AlmacenOrigen,
				UbicacionOrigen = t.UbicacionOrigen,
				AlmacenDestino = t.AlmacenDestino,
				UbicacionDestino = t.UbicacionDestino,
				FechaInicio = t.FechaInicio,
				FechaFinalizacion = t.FechaFinalizacion,
				CodigoEstado = t.CodigoEstado,
				Comentario = t.Comentario,
				EstadoErp = t.EstadoErp,
				UsuarioInicioId = t.UsuarioInicioId,
				UsuarioInicioNombre = usuarioInicioNombre,
				UsuarioFinalizacionId = t.UsuarioFinalizacionId,
				UsuarioFinalizacionNombre = usuarioFinNombre,
				CodigoEmpresa = t.CodigoEmpresa,
				FechaCaducidad = t.FechaCaducidad,
				Partida = t.Partida
			};
		}).ToList();

		return Ok(resultado);
	}
	#endregion

	#region POST: Relanzar traspaso ERROR_ERP
	[HttpPost("traspasos/{traspasoId:guid}/relanzar")]
	public async Task<IActionResult> RelanzarTraspaso(Guid traspasoId, [FromBody] RelanzarTraspasoDto dto)
	{
		if (dto == null || dto.UsuarioId <= 0)
		{
			return BadRequest("Debe indicar el usuario que relanza el traspaso.");
		}

		await using var transaction = await _auroraSgaContext.Database.BeginTransactionAsync();

		var traspasoError = await _auroraSgaContext.Traspasos
			.FirstOrDefaultAsync(t => t.Id == traspasoId && t.CodigoEstado == "ERROR_ERP" && t.TipoTraspaso == "PALET");

		if (traspasoError == null)
		{
			return NotFound("El traspaso no existe o no está en estado ERROR_ERP.");
		}

		var traspasosDelPalet = await _auroraSgaContext.Traspasos
			.Where(t => t.PaletId == traspasoError.PaletId &&
						 t.CodigoEstado == "ERROR_ERP" &&
						 t.TipoTraspaso == "PALET")
			.OrderBy(t => t.FechaInicio)
			.ToListAsync();

		var relanzados = new List<Guid>();
		var advertencias = new List<string>();

		foreach (var traspaso in traspasosDelPalet)
		{
			try
			{
				var nuevoId = await RelanzarTraspasoIndividualAsync(traspaso, dto);
				if (nuevoId.HasValue)
					relanzados.Add(nuevoId.Value);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error al relanzar traspaso {TraspasoId}", traspaso.Id);
				advertencias.Add($"Traspaso {traspaso.Id}: {ex.Message}");
			}
		}

		await transaction.CommitAsync();

		return Ok(new
		{
			message = $"Relanzados {relanzados.Count} traspasos para el palet {traspasoError.CodigoPalet}.",
			traspasosRelanzados = relanzados,
			advertencias
		});
	}
	#endregion

	private async Task<Guid?> RelanzarTraspasoIndividualAsync(Traspaso traspasoError, RelanzarTraspasoDto dto)
	{
		// Buscar línea definitiva del palet (estado real actual)
		var paletLineaDefinitiva = await _auroraSgaContext.PaletLineas
			.Where(pl => pl.PaletId == traspasoError.PaletId &&
						 pl.CodigoArticulo == traspasoError.CodigoArticulo &&
						 (pl.Lote == traspasoError.Partida || (pl.Lote == null && traspasoError.Partida == null)))
			.FirstOrDefaultAsync();

		if (paletLineaDefinitiva == null)
		{
			throw new InvalidOperationException($"No se encontró línea definitiva para el artículo {traspasoError.CodigoArticulo} del traspaso en error {traspasoError.Id}.");
		}

		// Siempre crear una nueva línea temporal basada en la línea definitiva
		// Esto evita problemas de concurrencia con líneas temporales que puedan haber sido procesadas
		var tempLinea = new TempPaletLinea
		{
			Id = Guid.NewGuid(),
			PaletId = paletLineaDefinitiva.PaletId,
			CodigoEmpresa = paletLineaDefinitiva.CodigoEmpresa,
			CodigoArticulo = paletLineaDefinitiva.CodigoArticulo,
			DescripcionArticulo = paletLineaDefinitiva.DescripcionArticulo,
			Cantidad = paletLineaDefinitiva.Cantidad,
			UnidadMedida = paletLineaDefinitiva.UnidadMedida,
			Lote = paletLineaDefinitiva.Lote,
			FechaCaducidad = paletLineaDefinitiva.FechaCaducidad,
			CodigoAlmacen = traspasoError.AlmacenOrigen, // Usar el almacén origen del traspaso en error
			Ubicacion = traspasoError.UbicacionOrigen ?? paletLineaDefinitiva.Ubicacion ?? "", // Usar ubicación origen o la de la línea definitiva
			UsuarioId = dto.UsuarioId,
			FechaAgregado = DateTime.Now,
			Observaciones = $"Creada para relanzar traspaso {traspasoError.Id}",
			Procesada = false,
			TraspasoId = null, // Se asignará después
			EsHeredada = true
		};
		_auroraSgaContext.TempPaletLineas.Add(tempLinea);

		var marcaRelanzado = $"Relanzado: {DateTime.Now:yyyy-MM-dd HH:mm}";
		var comentarioFinal = marcaRelanzado.Length > 500 ? marcaRelanzado.Substring(0, 500) : marcaRelanzado;

		var nuevoTraspaso = new Traspaso
		{
			Id = Guid.NewGuid(),
			PaletId = traspasoError.PaletId,
			CodigoPalet = traspasoError.CodigoPalet,
			TipoTraspaso = traspasoError.TipoTraspaso,
			CodigoEstado = "PENDIENTE_ERP",
			FechaInicio = DateTime.Now,
			UsuarioInicioId = dto.UsuarioId,
			AlmacenOrigen = traspasoError.AlmacenOrigen,
			UbicacionOrigen = traspasoError.UbicacionOrigen,
			AlmacenDestino = traspasoError.AlmacenDestino,
			UbicacionDestino = traspasoError.UbicacionDestino,
			FechaFinalizacion = null,
			UsuarioFinalizacionId = null,
			CodigoArticulo = traspasoError.CodigoArticulo,
			Cantidad = traspasoError.Cantidad,
			Partida = traspasoError.Partida,
			FechaCaducidad = traspasoError.FechaCaducidad,
			CodigoEmpresa = traspasoError.CodigoEmpresa,
			Comentario = comentarioFinal,
			EstadoErp = null,
			EsNotificado = false,
			MovPosicionOrigen = traspasoError.MovPosicionOrigen,
			MovPosicionDestino = traspasoError.MovPosicionDestino,
			OrigenTraspaso = "AuroraSGA"
		};

		_auroraSgaContext.Traspasos.Add(nuevoTraspaso);

		var paletLineasAsociadas = await _auroraSgaContext.PaletLineas
			.Where(pl => pl.TraspasoId == traspasoError.Id)
			.ToListAsync();

		if (paletLineasAsociadas.Any())
		{
			_auroraSgaContext.PaletLineas.RemoveRange(paletLineasAsociadas);
		}

		// Asociar la línea temporal al nuevo traspaso (ya está en el contexto como Added)
		tempLinea.TraspasoId = nuevoTraspaso.Id;
		tempLinea.Procesada = false;

		var detalleRelanzado = $"Traspaso relanzado. Original: {traspasoError.Id}, Nuevo: {nuevoTraspaso.Id}";

		_auroraSgaContext.LogPalet.Add(new LogPalet
		{
			PaletId = traspasoError.PaletId,
			Fecha = DateTime.Now,
			IdUsuario = dto.UsuarioId,
			Accion = "RelanzarTraspaso",
			Detalle = $"{detalleRelanzado} | {marcaRelanzado}"
		});

		await _auroraSgaContext.SaveChangesAsync();

		return nuevoTraspaso.Id;
	}

	private static string NormalizarUbicacion(string? ubicacion)
	{
		return string.IsNullOrWhiteSpace(ubicacion) ? string.Empty : ubicacion.Trim();
	}

	#region POST: Vaciar palet pendiente
	[HttpPost("{id}/vaciar-pendiente")]
	public async Task<IActionResult> VaciarPaletPendiente(Guid id, [FromBody] ForzarVaciadoPaletDto dto)
	{
		if (dto == null || dto.UsuarioId <= 0)
		{
			return BadRequest("Debe indicar el usuario que realiza el vaciado.");
		}

		await using var transaction = await _auroraSgaContext.Database.BeginTransactionAsync();
		try
		{
			var palet = await _auroraSgaContext.Palets
				.FirstOrDefaultAsync(p => p.Id == id);

			if (palet == null)
				return NotFound("Palet no encontrado.");

			if (string.Equals(palet.Estado, "Vaciado", StringComparison.OrdinalIgnoreCase))
				return BadRequest("El palet ya está vaciado.");

			var fechaActual = DateTime.Now;

			var lineas = await _auroraSgaContext.PaletLineas
				.Where(pl => pl.PaletId == id)
				.ToListAsync();

			if (lineas.Any())
			{
				_auroraSgaContext.PaletLineas.RemoveRange(lineas);
			}

			palet.Estado = "Vaciado";
			palet.IsVaciado = true;
			palet.FechaVaciado = fechaActual;
			palet.UsuarioVaciadoId = dto.UsuarioId;

			if (!palet.FechaCierre.HasValue)
			{
				palet.FechaCierre = fechaActual;
				palet.UsuarioCierreId = dto.UsuarioId;
			}

			_auroraSgaContext.Palets.Update(palet);

			_auroraSgaContext.LogPalet.Add(new LogPalet
			{
				PaletId = palet.Id,
				Fecha = fechaActual,
				IdUsuario = dto.UsuarioId,
				Accion = "VaciarPendiente",
				Detalle = "Palet vaciado manualmente desde la pantalla de pendientes de vaciar."
			});

			await _auroraSgaContext.SaveChangesAsync();
			await transaction.CommitAsync();

			return Ok(new
			{
				message = $"Palet {palet.Codigo} vaciado correctamente.",
				paletId = palet.Id,
				codigoPalet = palet.Codigo,
				estado = palet.Estado,
				fechaVaciado = palet.FechaVaciado
			});
		}
		catch (Exception ex)
		{
			await transaction.RollbackAsync();
			_logger.LogError(ex, "Error al vaciar manualmente el palet {PaletId}", id);
			return StatusCode(500, $"Error al vaciar el palet: {ex.Message}");
		}
	}
	#endregion

	#region POST: Forzar vaciado de palet
	[HttpPost("{id}/forzar-vaciado")]
	public async Task<IActionResult> ForzarVaciadoPalet(Guid id, [FromBody] ForzarVaciadoPaletDto dto)
	{
		using var transaction = await _auroraSgaContext.Database.BeginTransactionAsync();
		try
		{
			// Buscar el palet
			var palet = await _auroraSgaContext.Palets.FindAsync(id);
			if (palet == null)
				return NotFound("Palet no encontrado");

			// Verificar que el palet no esté ya vaciado
			if (string.Equals(palet.Estado, "Vaciado", StringComparison.OrdinalIgnoreCase))
				return BadRequest("El palet ya está vaciado");

			// Actualizar el estado del palet
			palet.Estado = "Vaciado";
			palet.FechaVaciado = DateTime.Now;
			palet.UsuarioVaciadoId = dto.UsuarioId;
			
			// Si no tiene fecha de cierre, establecerla
			if (palet.FechaCierre == null)
			{
				palet.FechaCierre = DateTime.Now;
				palet.UsuarioCierreId = dto.UsuarioId;
			}


			_auroraSgaContext.Palets.Update(palet);

			// Registrar en el log
			_auroraSgaContext.LogPalet.Add(new LogPalet
			{
				PaletId = palet.Id,
				Fecha = DateTime.Now,
				IdUsuario = dto.UsuarioId,
				Accion = "ForzarVaciado",
				Detalle = $"Palet vaciado forzadamente por usuario {dto.UsuarioId}"
			});

			await _auroraSgaContext.SaveChangesAsync();
			await transaction.CommitAsync();

			return Ok(new
			{
				message = $"Palet {palet.Codigo} vaciado correctamente",
				paletId = palet.Id,
				codigoPalet = palet.Codigo,
				estado = palet.Estado,
				fechaVaciado = palet.FechaVaciado,
				usuarioVaciado = palet.UsuarioVaciadoId
			});
		}
		catch (Exception ex)
		{
			await transaction.RollbackAsync();
			_logger.LogError(ex, "Error al forzar vaciado del palet {PaletId}", id);
			return StatusCode(500, $"Error al vaciar el palet: {ex.Message}");
		}
	}
	#endregion

}
