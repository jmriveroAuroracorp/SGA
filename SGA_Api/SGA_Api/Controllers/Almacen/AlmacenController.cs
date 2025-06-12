using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Models.Stock;
using SGA_Api.Data;
using System;

[ApiController]
[Route("api/[controller]")]
public class AlmacenController : ControllerBase
{
	private readonly SageDbContext _ctx;
	public AlmacenController(SageDbContext ctx) => _ctx = ctx;

	// GET api/Almacen?codigoCentro=1
	[HttpGet]
	public async Task<ActionResult<List<string>>> GetAlmacenes([FromQuery] string codigoCentro)
	{
		// SELECT CodigoAlmacen 
		//   FROM Almacenes 
		//  WHERE CodigoCentro = {codigoCentro};
		var lista = await _ctx.Almacenes
							  .Where(a => a.CodigoCentro == codigoCentro)
							  .Select(a => a.CodigoAlmacen!)
							  .ToListAsync();

		if (lista.Count == 0)
			return NotFound();

		return Ok(lista);
	}
}