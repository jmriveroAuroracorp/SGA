using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Stock;

namespace SGA_Api.Controllers.Stock
{
	[ApiController]
	[Route("api/[controller]")]
	public class StockController : ControllerBase
	{
		private readonly SageDbContext _sageContext;
		private readonly StorageControlDbContext _storageContext;

		public StockController(SageDbContext sageContext, StorageControlDbContext storageContext)
		{
			_sageContext = sageContext;
			_storageContext = storageContext;
		}

		[HttpGet("consulta-stock")]
		public async Task<IActionResult> ConsultarStock(
			[FromQuery] short codigoEmpresa,
			[FromQuery] string? codigoUbicacion,
			[FromQuery] string? codigoAlmacen,   
			[FromQuery] string? codigoArticulo,
			[FromQuery] string? codigoCentro,
			[FromQuery] string? partida)
		{
			var flujoUbicacion = !string.IsNullOrWhiteSpace(codigoUbicacion) && !string.IsNullOrWhiteSpace(codigoAlmacen);
			var flujoArticulo = !string.IsNullOrWhiteSpace(codigoArticulo);

			if (!flujoUbicacion && !flujoArticulo)
				return BadRequest("Debe indicar ubicación + código de almacén, o un código de artículo.");

			var ejercicioActual = await _sageContext.Periodos
				.Where(p => p.CodigoEmpresa == codigoEmpresa && p.Fechainicio <= DateTime.Now)
				.OrderByDescending(p => p.Fechainicio)
				.Select(p => p.Ejercicio)
				.FirstOrDefaultAsync();

			if (ejercicioActual == 0)
				return BadRequest("No se encontró un ejercicio válido para la empresa.");

			var stockData = await _storageContext.AcumuladoStockUbicacion
				.Where(a =>
					a.CodigoEmpresa == codigoEmpresa &&
					a.Ejercicio == ejercicioActual &&
					a.UnidadSaldo != 0 &&
					(
						(flujoUbicacion && a.Ubicacion == codigoUbicacion && a.CodigoAlmacen == codigoAlmacen) ||
						(flujoArticulo && a.CodigoArticulo == codigoArticulo)
					) &&
					(partida == null || a.Partida == partida))
				.ToListAsync();


			// MODIFICADO POR SER MUY RESTRICTIVO, SOLO PODÍAMOS SACAR LOS NOMBRES DE ALMACENES ASOCIADOS AL CODIGO CENTRO, PERO NO TIENE EN CUENTA QUE HAY USUARIOS QUE TIENEN MÁS ALMACENES ADEMÁS DE LOS DEL CENTRO
			//	var almacenes = await _sageContext.Almacenes
			//.Where(a =>
			//	a.CodigoEmpresa == codigoEmpresa &&
			//	(codigoCentro == null || a.CodigoCentro == codigoCentro) &&
			//	(codigoAlmacen == null || a.CodigoAlmacen == codigoAlmacen)  // <––
			//)
			//.ToListAsync();

			var almacenes = await _sageContext.Almacenes
				.Where(a => a.CodigoEmpresa == codigoEmpresa)
				.ToListAsync();

			var articulos = await _sageContext.Articulos
				.Where(a => a.CodigoEmpresa == codigoEmpresa)
				.ToListAsync();

			var resultado = stockData
			   .Select(s =>
			   {
				   var alm = almacenes.FirstOrDefault(a =>
					   a.CodigoEmpresa == s.CodigoEmpresa &&
					   a.CodigoAlmacen == s.CodigoAlmacen);

				   var art = articulos.FirstOrDefault(a =>
					   a.CodigoEmpresa == s.CodigoEmpresa &&
					   a.CodigoArticulo == s.CodigoArticulo);

				   return new StockUbicacionDto
				   {
					   CodigoEmpresa = s.CodigoEmpresa.ToString(),
					   CodigoArticulo = s.CodigoArticulo,
					   DescripcionArticulo = art?.DescripcionArticulo,  // <-- nuevo
					   CodigoAlternativo = art?.CodigoAlternativo,
					   CodigoAlternativo2 = art?.CodigoAlternativo2,
					   ReferenciaEdi_ = art?.ReferenciaEdi_,
					   MRHCodigoAlternativo3 = art?.MRHCodigoAlternativo3,
					   VCodigoDUN14 = art?.VCodigoDUN14,
					   CodigoCentro = alm?.CodigoCentro?.ToString(),
					   CodigoAlmacen = s.CodigoAlmacen.ToString(),
					   Almacen = alm?.Almacen,
					   Ubicacion = s.Ubicacion,
					   Partida = s.Partida,
					   FechaCaducidad = s.FechaCaducidad,
					   UnidadSaldo = s.UnidadSaldo
				   };
			   })
			   .ToList();

			return Ok(resultado);
		}

	}
}
