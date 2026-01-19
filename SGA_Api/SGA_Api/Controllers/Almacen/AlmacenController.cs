using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Models.Stock;
using SGA_Api.Data;
using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGA_Api.Models.Almacen;

[ApiController]
[Route("api/[controller]")]
public class AlmacenController : ControllerBase
{
	private readonly SageDbContext _sageDBContext;
	private readonly StorageControlDbContext _storageControlContext;
	private readonly AuroraSgaDbContext _auroraSgaContext;

	// Inyecta los tres contextos aquí
	public AlmacenController(
		SageDbContext sageDbContext,
		StorageControlDbContext storageControlContext,
		AuroraSgaDbContext auroraSgaContext)
	{
		_sageDBContext = sageDbContext;
		_storageControlContext = storageControlContext;
		_auroraSgaContext = auroraSgaContext;
	}

	// GET api/Almacen?codigoCentro=1
	[HttpGet]
	public async Task<ActionResult<List<string>>> GetAlmacenes([FromQuery] string codigoCentro)
	{
		var lista = await _sageDBContext.Almacenes
						  .Where(a => a.CodigoCentro == codigoCentro)
						  .Select(a => a.CodigoAlmacen!)
						  .ToListAsync();

		if (lista.Count == 0)
			return NotFound();

		return Ok(lista);
	}

	/// <summary>
	/// Devuelve todas las columnas de Ubicaciones para un almacén dado.
	/// </summary>
	[HttpGet("Ubicaciones")]
	public async Task<IActionResult> GetUbicaciones(
		[FromQuery] string codigoAlmacen,
		[FromQuery] short? codigoEmpresa = null,
		[FromQuery] bool soloConStock = false)
	{
		if (soloConStock && codigoEmpresa == null)
			return BadRequest("Se requiere 'codigoEmpresa' si se usa 'soloConStock=true'");

		if (soloConStock)
		{
			var ejercicio = await _sageDBContext.Periodos
				.Where(p => p.CodigoEmpresa == codigoEmpresa && p.Fechainicio <= DateTime.Now)
				.OrderByDescending(p => p.Fechainicio)
				.Select(p => p.Ejercicio)
				.FirstOrDefaultAsync();

			if (ejercicio == 0)
				return BadRequest("Ejercicio no encontrado");

			var ubicacionesConStock = await _storageControlContext.AcumuladoStockUbicacion
				.Where(x =>
					x.CodigoEmpresa == codigoEmpresa &&
					x.CodigoAlmacen == codigoAlmacen &&
					x.Ejercicio == ejercicio &&
					x.UnidadSaldo != 0)
				.Select(x => new { x.CodigoEmpresa, x.CodigoAlmacen, x.Ubicacion })
				.Distinct()
				.ToListAsync();

			return Ok(ubicacionesConStock.Select(u => new UbicacionDto
			{
				CodigoEmpresa = u.CodigoEmpresa,
				CodigoAlmacen = u.CodigoAlmacen,
				Ubicacion = u.Ubicacion
			}).ToList());
		}

		// Caso por defecto: devolvemos todas las ubicaciones desde AuroraSGA
		var query = _auroraSgaContext.Ubicaciones
			.Where(u => u.CodigoAlmacen == codigoAlmacen && u.Obsoleta == 0);
		
		// Si se proporciona codigoEmpresa, filtrar también por empresa
		if (codigoEmpresa.HasValue)
		{
			query = query.Where(u => u.CodigoEmpresa == codigoEmpresa.Value);
		}
		
		var lista = await query
			.Select(u => new UbicacionDto
			{
				CodigoEmpresa = u.CodigoEmpresa, 
				CodigoAlmacen = u.CodigoAlmacen ?? "",
				Ubicacion = u.CodigoUbicacion ?? ""
			})
			.ToListAsync();

		return Ok(lista);
	}



	[HttpPost("Combos/Autorizados")]
	public async Task<IActionResult> GetAlmacenesAutorizados([FromBody] AlmacenesAutorizadosDto request)
	{
		try
		{
			request.CodigosAlmacen ??= new List<string>();
			var codigosPermitidos = request.CodigosAlmacen.ToHashSet();

			// 1. Almacenes del centro
			var delCentro = await _sageDBContext.Almacenes
				.Where(a => a.CodigoCentro == request.CodigoCentro &&
							a.CodigoEmpresa == request.CodigoEmpresa)
				.Select(a => new AlmacenDto
				{
					CodigoAlmacen = a.CodigoAlmacen!,
					NombreAlmacen = a.Almacen!,
					CodigoEmpresa = a.CodigoEmpresa ?? 0,
					EsDelCentro = true
				})
				.ToListAsync();

		// 2. Almacenes individuales del operario
		List<AlmacenDto> individuales;
		
		if (request.OperarioId.HasValue)
		{
			// Si viene OperarioId, consultar OperariosAlmacenes directamente (más preciso)
			// Hacer un join directo para evitar problemas con Contains y HashSet
			var posibles = await (from oa in _sageDBContext.OperariosAlmacenes
			                     join a in _sageDBContext.Almacenes 
			                         on new { oa.CodigoEmpresa, oa.CodigoAlmacen } 
			                         equals new { CodigoEmpresa = (short)a.CodigoEmpresa, a.CodigoAlmacen }
			                     where oa.Operario == request.OperarioId.Value && 
			                           oa.CodigoEmpresa == request.CodigoEmpresa
			                     select new AlmacenDto
			                     {
			                         CodigoAlmacen = a.CodigoAlmacen!,
			                         NombreAlmacen = a.Almacen!,
			                         CodigoEmpresa = a.CodigoEmpresa ?? 0,
			                         EsDelCentro = false
			                     })
			                    .ToListAsync();

			individuales = posibles;
		}
		else
		{
			// Comportamiento anterior: filtrar por códigos (retrocompatibilidad)
			var posibles = await _sageDBContext.Almacenes
				.Where(a => a.CodigoEmpresa == request.CodigoEmpresa)
				.ToListAsync();

			individuales = posibles
				.Where(a => codigosPermitidos.Contains(a.CodigoAlmacen!))
				.Select(a => new AlmacenDto
				{
					CodigoAlmacen = a.CodigoAlmacen!,
					NombreAlmacen = a.Almacen!,
					CodigoEmpresa = a.CodigoEmpresa ?? 0,
					EsDelCentro = false
				})
				.ToList();
		}

			// Fusionar
			var resultado = delCentro
				.Concat(individuales)
				.GroupBy(x => x.CodigoAlmacen)
				.Select(g => g.First())
				.OrderBy(x => x.CodigoAlmacen)
				.ToList();

			return Ok(resultado);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error en GetAlmacenesAutorizados: {ex}");
			return StatusCode(500, $"Error interno en la API: {ex.Message}");
		}
	}



}


