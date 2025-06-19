using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using System;

namespace SGA_Api.Controllers.Ubicacion
{
	[Route("api/ubicaciones")]
	[ApiController]
	public class UbicacionesController : ControllerBase
	{
		private readonly AuroraSgaDbContext _auroraSgaContext;

		public UbicacionesController(AuroraSgaDbContext context)
		{
			_auroraSgaContext = context;
		}

		[HttpGet]
		public async Task<IActionResult> GetUbicacionesDetalladas(
			[FromQuery] short codigoEmpresa,
			[FromQuery] string codigoAlmacen)
		{
			if (string.IsNullOrWhiteSpace(codigoAlmacen))
				return BadRequest("Debes especificar un código de almacén.");

			var data = await _auroraSgaContext.vUbicacionesDetalladas
				.Where(u => u.CodigoEmpresa == codigoEmpresa
						 && u.CodigoAlmacen == codigoAlmacen)
				.ToListAsync();

			return Ok(data);
		}

	}
}
