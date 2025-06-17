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

	// Inyecta los dos contextos aquí
	public AlmacenController(
		SageDbContext sageDbContext,
		StorageControlDbContext storageControlContext)
	{
		_sageDBContext = sageDbContext;
		_storageControlContext = storageControlContext;
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
	public async Task<IActionResult> GetUbicaciones([FromQuery] string codigoAlmacen)
	{
		// Asegúrate de que storageControlContext ya no sea null
		var query = _storageControlContext.Ubicaciones
					   .Where(u => u.CodigoAlmacen == codigoAlmacen);

		// Opcional: log de la SQL para depuración
		Console.WriteLine(query.ToQueryString());

		var lista = await query.ToListAsync();
		if (!lista.Any())
			return NotFound();

		// Si quieres devolver sólo strings:
		// return Ok(lista.Select(u => u.Ubicacion).ToList());

		return Ok(lista);
	}
}


