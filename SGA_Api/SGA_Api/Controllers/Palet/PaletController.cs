using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Mysqlx.Cursor;
using SGA_Api.Data;
using SGA_Api.Models.Palet;
using SGA_Api.Models.Traspasos;
using SGA_Api.Models.UsuarioConf;
using System;
using System.Data;

namespace SGA_Api.Controllers.Palet;

[ApiController]
[Route("api/[controller]")]
public class PaletController : ControllerBase
{
	private readonly AuroraSgaDbContext _auroraSgaContext;
	private readonly SageDbContext _sageContext;
	private readonly StorageControlDbContext _storageContext;

	public PaletController(
		AuroraSgaDbContext auroraSgaContext,
		SageDbContext sageContext,
		StorageControlDbContext storageContext)
	{
		_auroraSgaContext = auroraSgaContext;
		_sageContext = sageContext;
		_storageContext = storageContext;
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
		[FromQuery] bool sinCierre = false)
	{
		var nombreDict = await _auroraSgaContext.vUsuariosConNombre
			.ToDictionaryAsync(x => x.UsuarioId, x => x.NombreOperario);

		var q = _auroraSgaContext.Palets
			.Where(p => p.CodigoEmpresa == codigoEmpresa && p.FechaVaciado == null);

		if (!string.IsNullOrWhiteSpace(codigo))
			q = q.Where(p => p.Codigo == codigo);

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

		var lista = await q.OrderBy(p => p.Codigo)
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

		foreach (var palet in lista)
		{
			if (palet.UsuarioAperturaId.HasValue && nombreDict.TryGetValue(palet.UsuarioAperturaId.Value, out var nombreA))
				palet.UsuarioAperturaNombre = nombreA;

			if (palet.UsuarioCierreId.HasValue && nombreDict.TryGetValue(palet.UsuarioCierreId.Value, out var nombreC))
				palet.UsuarioCierreNombre = nombreC;
		}

		return Ok(lista);
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

	//#region POST: Crear palet
	//[HttpPost]
	//public async Task<ActionResult<PaletDto>> CrearPalet([FromBody] PaletCrearDto dto)
	//{
	//	try
	//	{
	//		var pCanal = new SqlParameter("@Canal", SqlDbType.VarChar, 10) { Value = "" };
	//		var pSerie = new SqlParameter("@Serie", SqlDbType.Int) { Value = 0 };
	//		var pCodigoEmpresa = new SqlParameter("@CodigoEmpresa", SqlDbType.SmallInt) { Value = dto.CodigoEmpresa };
	//		var pEstado = new SqlParameter("@Estado", SqlDbType.NVarChar, 50) { Value = "Abierto" };
	//		var pTipoPaletCodigo = new SqlParameter("@TipoPaletCodigo", SqlDbType.NVarChar, 10) { Value = (object)dto.TipoPaletCodigo ?? DBNull.Value };
	//		var pUsuarioAperturaId = new SqlParameter("@UsuarioAperturaId", SqlDbType.Int) { Value = dto.UsuarioAperturaId };
	//		var pOrdenTrabajoId = new SqlParameter("@OrdenTrabajoId", SqlDbType.VarChar, 50) { Value = string.IsNullOrWhiteSpace(dto.OrdenTrabajoId) ? "" : dto.OrdenTrabajoId! };
	//		var pNuevoCodigo = new SqlParameter("@NuevoCodigo", SqlDbType.VarChar, 20) { Direction = ParameterDirection.Output };

	//		await _auroraSgaContext.Database.ExecuteSqlRawAsync(
	//			"EXEC dbo.CrearPalet @Canal, @Serie, @CodigoEmpresa, @Estado, @TipoPaletCodigo, @UsuarioAperturaId, @OrdenTrabajoId, @NuevoCodigo OUTPUT",
	//			pCanal, pSerie, pCodigoEmpresa, pEstado, pTipoPaletCodigo, pUsuarioAperturaId, pOrdenTrabajoId, pNuevoCodigo);

	//		var codigoGenerado = (string)pNuevoCodigo.Value!;
	//		var palet = await _auroraSgaContext.Palets.SingleAsync(x => x.Codigo == codigoGenerado);
	//		_auroraSgaContext.LogPalet.Add(new LogPalet
	//		{
	//			PaletId = palet.Id,
	//			Fecha = DateTime.Now,
	//			IdUsuario = dto.UsuarioAperturaId,
	//			Accion = "Crear",
	//			Detalle = $"Palet creado por el usuario: {dto.UsuarioAperturaId}"
	//		});

	//		var resultado = new PaletDto
	//		{
	//			Id = palet.Id,
	//			CodigoEmpresa = palet.CodigoEmpresa,
	//			Codigo = palet.Codigo,
	//			Estado = palet.Estado,
	//			TipoPaletCodigo = palet.TipoPaletCodigo,
	//			FechaApertura = palet.FechaApertura,
	//			FechaCierre = palet.FechaCierre,
	//			UsuarioAperturaId = palet.UsuarioAperturaId,
	//			UsuarioCierreId = palet.UsuarioCierreId,
	//			OrdenTrabajoId = palet.OrdenTrabajoId,
	//			Altura = palet.Altura,
	//			Peso = palet.Peso,
	//			EtiquetaGenerada = palet.EtiquetaGenerada,
	//			IsVaciado = palet.IsVaciado,
	//			FechaVaciado = palet.FechaVaciado
	//		};

	//		await _auroraSgaContext.SaveChangesAsync();
	//		return CreatedAtRoute("GetPaletById", new { id = palet.Id }, resultado);
	//	}
	//	catch (Exception ex)
	//	{
	//		return Problem(detail: ex.ToString(), statusCode: 500, title: "Error creando palet");
	//	}
	//}
	//#endregion

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

			// 🔷 Crear la línea temporal
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
				FechaAgregado = DateTime.Now
			};

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
		// Primero busca en PaletLineas (definitivas)
		var lineas = await _auroraSgaContext.PaletLineas
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

		// Si no hay líneas definitivas, busca en temporales
		if (lineas.Count == 0)
		{
			lineas = await _auroraSgaContext.TempPaletLineas
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
		}

		return Ok(lineas);
	}
	#endregion

	#region DELETE: Eliminar línea de palet
	[HttpDelete("lineas/{lineaId}")]
	public async Task<IActionResult> EliminarLineaPalet(Guid lineaId, [FromQuery] int usuarioId)
	{
		var linea = await _auroraSgaContext.TempPaletLineas.FindAsync(lineaId);
		if (linea == null)
			return NotFound();

		// Primero obtenemos el palet asociado
		var palet = await _auroraSgaContext.Palets.FindAsync(linea.PaletId);
		if (palet == null)
			return NotFound("Palet no encontrado");

		// Si está cerrado, no se puede eliminar la línea
		if (palet.Estado == "Cerrado")
			return BadRequest("No se pueden eliminar líneas de un palet cerrado.");

		// Eliminamos la línea
		_auroraSgaContext.TempPaletLineas.Remove(linea);

		// Log de la eliminación
		_auroraSgaContext.LogPalet.Add(new LogPalet
		{
			PaletId = palet.Id,
			Fecha = DateTime.Now,
			IdUsuario = usuarioId,
			Accion = "EliminarLinea",
			Detalle = $"Línea eliminada: Artículo={linea.CodigoArticulo}, Cantidad={linea.Cantidad}, Ubicación={linea.Ubicacion}"
		});

		await _auroraSgaContext.SaveChangesAsync();

		return Ok(new { message = "Línea eliminada correctamente" });
	}

	#endregion


	//#region POST: Cerrar palet
	//[HttpPost("{id}/cerrar")]
	//public async Task<IActionResult> CerrarPalet(Guid id, [FromQuery] int usuarioId)
	//{
	//	var palet = await _auroraSgaContext.Palets.FindAsync(id);
	//	if (palet == null)
	//		return NotFound("Palet no encontrado");

	//	if (palet.Estado == "Cerrado")
	//		return BadRequest("El palet ya está cerrado.");

	//	palet.Estado = "Cerrado";
	//	palet.FechaCierre = DateTime.Now;
	//	palet.UsuarioCierreId = usuarioId;

	//	_auroraSgaContext.LogPalet.Add(new LogPalet
	//	{
	//		PaletId = palet.Id,
	//		Fecha = DateTime.Now,
	//		IdUsuario = usuarioId,
	//		Accion = "Cerrar",
	//		Detalle = "Palet Cerrado por el usuario: " + usuarioId
	//	});


	//	_auroraSgaContext.Palets.Update(palet);
	//	await _auroraSgaContext.SaveChangesAsync();

	//	return Ok(new { message = $"Palet {palet.Codigo} cerrado correctamente." });
	//}
	//#endregion
	//#region POST: Cerrar palet
	//[HttpPost("{id}/cerrar")]
	//public async Task<IActionResult> CerrarPalet(Guid id, [FromQuery] int usuarioId)
	//{
	//	var palet = await _auroraSgaContext.Palets.FindAsync(id);
	//	if (palet == null)
	//		return NotFound("Palet no encontrado");

	//	if (palet.Estado == "Cerrado")
	//		return BadRequest("El palet ya está cerrado.");

	//	// 🔷 Validamos que tenga al menos una línea
	//	bool tieneLineas = await _auroraSgaContext.TempPaletLineas
	//		.AnyAsync(l => l.PaletId == id);

	//	if (!tieneLineas)
	//		return BadRequest("No se puede cerrar un palet vacío. Debe tener al menos una línea.");

	//	// 🔷 Cerramos el palet
	//	palet.Estado = "Cerrado";
	//	palet.FechaCierre = DateTime.Now;
	//	palet.UsuarioCierreId = usuarioId;

	//	_auroraSgaContext.LogPalet.Add(new LogPalet
	//	{
	//		PaletId = palet.Id,
	//		Fecha = DateTime.Now,
	//		IdUsuario = usuarioId,
	//		Accion = "Cerrar",
	//		Detalle = "Palet Cerrado por el usuario: " + usuarioId
	//	});

	//	// 🔷 Creamos el traspaso mínimo
	//	var traspaso = new Traspaso
	//	{
	//		PaletId = palet.Id,
	//		CodigoEstado = "PENDIENTE"
	//	};

	//	_auroraSgaContext.Traspasos.Add(traspaso);

	//	await _auroraSgaContext.SaveChangesAsync();

	//	return Ok(new
	//	{
	//		message = $"Palet {palet.Codigo} cerrado correctamente y traspaso creado.",
	//		traspasoId = traspaso.Id
	//	});
	//}
	//#endregion



	//#region POST: Reabrir palet
	//[HttpPost("{id}/reabrir")]
	//public async Task<IActionResult> ReabrirPalet(Guid id, [FromQuery] int usuarioId)
	//{
	//	var palet = await _auroraSgaContext.Palets.FindAsync(id);
	//	if (palet == null)
	//		return NotFound("Palet no encontrado");

	//	if (palet.Estado == "Abierto")
	//		return BadRequest("El palet ya está abierto.");

	//	palet.Estado = "Abierto";
	//	palet.FechaApertura = DateTime.Now;  // 👈 nueva fecha de apertura
	//	palet.UsuarioAperturaId = usuarioId;
	//	palet.FechaCierre = null;
	//	palet.UsuarioCierreId = null;

	//	_auroraSgaContext.LogPalet.Add(new LogPalet
	//	{
	//		PaletId = palet.Id,
	//		Fecha = DateTime.Now,
	//		IdUsuario = usuarioId,
	//		Accion = "Reabrir",
	//		Detalle = "Palet reabierto por el usuario: " + usuarioId
	//	});

	//	_auroraSgaContext.Palets.Update(palet);
	//	await _auroraSgaContext.SaveChangesAsync();

	//	return Ok(new { message = $"Palet {palet.Codigo} reabierto correctamente." });
	//}
	//#endregion

	//#region POST: Cerrar palet
	//[HttpPost("{id}/cerrar")]
	//public async Task<IActionResult> CerrarPalet(Guid id, [FromQuery] int usuarioId)
	//{
	//	var palet = await _auroraSgaContext.Palets.FindAsync(id);
	//	if (palet == null)
	//		return NotFound("Palet no encontrado");

	//	if (palet.Estado == "Cerrado")
	//		return BadRequest("El palet ya está cerrado.");

	//	// 🔷 Validamos que tenga al menos una línea
	//	bool tieneLineas = await _auroraSgaContext.TempPaletLineas
	//		.AnyAsync(l => l.PaletId == id);

	//	if (!tieneLineas)
	//		return BadRequest("No se puede cerrar un palet vacío. Debe tener al menos una línea.");

	//	// 🔷 Cerramos el palet
	//	palet.Estado = "Cerrado";
	//	palet.FechaCierre = DateTime.Now;
	//	palet.UsuarioCierreId = usuarioId;

	//	_auroraSgaContext.LogPalet.Add(new LogPalet
	//	{
	//		PaletId = palet.Id,
	//		Fecha = DateTime.Now,
	//		IdUsuario = usuarioId,
	//		Accion = "Cerrar",
	//		Detalle = "Palet Cerrado por el usuario: " + usuarioId
	//	});

	//	// 🔷 Creamos el traspaso mínimo
	//	var traspaso = new Traspaso
	//	{
	//		PaletId = palet.Id,
	//		CodigoEstado = "PENDIENTE",
	//		FechaInicio = DateTime.Now,
	//		UsuarioInicioId = usuarioId,
	//		 AlmacenOrigen = "N/A",
	//		AlmacenDestino = "N/A"
	//	};

	//	_auroraSgaContext.Traspasos.Add(traspaso);

	//	await _auroraSgaContext.SaveChangesAsync();

	//	return Ok(new
	//	{
	//		message = $"Palet {palet.Codigo} cerrado correctamente y traspaso creado.",
	//		traspasoId = traspaso.Id
	//	});
	//}
	//#endregion

	[HttpPost("{id}/cerrar")]
	public async Task<IActionResult> CerrarPalet(Guid id, [FromBody] CerrarPaletDto dto)
	{
		var palet = await _auroraSgaContext.Palets.FindAsync(id);
		if (palet == null)
			return NotFound("Palet no encontrado");

		if (palet.Estado == "Cerrado")
			return BadRequest("El palet ya está cerrado.");

		// Verifica que tenga al menos una línea
		bool tieneLineas = await _auroraSgaContext.TempPaletLineas
			.AnyAsync(l => l.PaletId == id);

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

		// Obtener todas las líneas del palet
		var lineas = await _auroraSgaContext.TempPaletLineas
			.Where(l => l.PaletId == palet.Id)
			.ToListAsync();

		var traspasosCreados = new List<Guid>();

		foreach (var linea in lineas)
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
				AlmacenOrigen = dto.CodigoAlmacen,
				AlmacenDestino = dto.CodigoAlmacenDestino,
				UbicacionOrigen = linea.Ubicacion,
				UbicacionDestino = dto.UbicacionDestino, // Siempre se asigna
				FechaFinalizacion = dto.FechaFinalizacion, // Siempre se asigna
				UsuarioFinalizacionId = dto.UsuarioFinalizacionId, // Siempre se asigna
				CodigoEmpresa = dto.CodigoEmpresa,
				CodigoArticulo = linea.CodigoArticulo,
				Cantidad = linea.Cantidad,
				Partida = linea.Lote,
				FechaCaducidad = linea.FechaCaducidad,
				Comentario = dto.Comentario,
			};
			_auroraSgaContext.Traspasos.Add(traspasoArticulo);
			traspasosCreados.Add(traspasoArticulo.Id);
		}

		await _auroraSgaContext.SaveChangesAsync();

		return Ok(new
		{
			message = $"Palet {palet.Codigo} cerrado correctamente y traspasos de artículos creados.",
			traspasosIds = traspasosCreados
		});
	}



	#region POST: Reabrir palet
	[HttpPost("{id}/reabrir")]
	public async Task<IActionResult> ReabrirPalet(Guid id, [FromQuery] int usuarioId)
	{
		var palet = await _auroraSgaContext.Palets.FindAsync(id);
		if (palet == null)
			return NotFound("Palet no encontrado");

		if (palet.Estado == "Abierto")
			return BadRequest("El palet ya está abierto.");

		palet.Estado = "Abierto";
		palet.FechaApertura = DateTime.Now;  // 👈 nueva fecha de apertura
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

		// 🔷 Busca el traspaso pendiente y márcalo como CANCELADO
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

    [HttpPost("{id}/cerrar-mobility")]
    public async Task<IActionResult> CerrarPaletMobility(Guid id, [FromBody] CerrarPaletMobilityDto dto)
    {
        var palet = await _auroraSgaContext.Palets.FindAsync(id);
        if (palet == null)
            return NotFound("Palet no encontrado");

        if (palet.Estado == "Cerrado")
            return BadRequest("El palet ya está cerrado.");

        // Verifica que tenga al menos una línea
        bool tieneLineas = await _auroraSgaContext.TempPaletLineas
            .AnyAsync(l => l.PaletId == id);

        if (!tieneLineas)
            return BadRequest("No se puede cerrar un palet vacío. Debe tener al menos una línea.");

        // Cierra el palet
        palet.Estado = "Cerrado";
        palet.FechaCierre = DateTime.Now;
        palet.UsuarioCierreId = dto.UsuarioId;

        _auroraSgaContext.LogPalet.Add(new LogPalet
        {
            PaletId = palet.Id,
            Fecha = DateTime.Now,
            IdUsuario = dto.UsuarioId,
            Accion = "CerrarMobility",
            Detalle = $"Palet cerrado por usuario {dto.UsuarioId} desde Mobility"
        });

        // Crea el traspaso en estado PENDIENTE, sin datos de destino ni finalización
        var traspaso = new Traspaso
        {
            PaletId = palet.Id,
            CodigoPalet = palet.Codigo,
            TipoTraspaso = "PALET",
            CodigoEstado = "PENDIENTE",
            FechaInicio = DateTime.Now,
            UsuarioInicioId = dto.UsuarioId,
            AlmacenOrigen = dto.CodigoAlmacen,
            CodigoEmpresa = dto.CodigoEmpresa,
            Comentario = dto.Comentario // Nuevo campo
        };
        _auroraSgaContext.Traspasos.Add(traspaso);

        await _auroraSgaContext.SaveChangesAsync();

        return Ok(new
        {
            message = $"Palet {palet.Codigo} cerrado correctamente y traspaso pendiente creado.",
            traspasoId = traspaso.Id
        });
    }

    [HttpPost("{id}/completar-traspaso")]
    public async Task<IActionResult> CompletarTraspaso(Guid id, [FromBody] CompletarTraspasoDto dto)
    {
        var traspaso = await _auroraSgaContext.Traspasos.FindAsync(id);
        if (traspaso == null)
            return NotFound("Traspaso no encontrado");

        if (traspaso.CodigoEstado != "PENDIENTE")
            return BadRequest("Solo se pueden completar traspasos en estado PENDIENTE.");

        // Actualiza los datos de destino y finalización
        traspaso.AlmacenDestino = dto.CodigoAlmacenDestino;
        traspaso.UbicacionDestino = dto.UbicacionDestino;
        traspaso.FechaFinalizacion = dto.FechaFinalizacion;
        traspaso.UsuarioFinalizacionId = dto.UsuarioFinalizacionId;
        traspaso.CodigoEstado = "PENDIENTE_ERP";

        _auroraSgaContext.Traspasos.Update(traspaso);
        await _auroraSgaContext.SaveChangesAsync();

        return Ok(new { message = "Traspaso completado correctamente." });
    }

}
