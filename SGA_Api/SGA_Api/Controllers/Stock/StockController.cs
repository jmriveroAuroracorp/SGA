using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Almacen;
using SGA_Api.Models.Stock;

namespace SGA_Api.Controllers.Stock
{


	[ApiController]
	[Route("api/[controller]")]
	public class StockController : ControllerBase
	{
		private readonly SageDbContext _sageContext;
		private readonly StorageControlDbContext _storageContext;

		public StockController(SageDbContext sageContext,
							   StorageControlDbContext storageContext)
		{
			_sageContext = sageContext;
			_storageContext = storageContext;
		}

		// 1.a) Buscar por artículo (+ opcional partida + opcional almacén + opcional ubicación)
		// GET api/stock/articulo?codigoEmpresa=1&codigoArticulo=10000&partida=...&codigoAlmacen=...&codigoUbicacion=...
		[HttpGet("articulo")]
		public async Task<IActionResult> PorArticulo(
			[FromQuery] short codigoEmpresa,
			[FromQuery] string codigoArticulo,
			[FromQuery] string? partida = null,
			[FromQuery] string? codigoAlmacen = null,
			[FromQuery] string? codigoUbicacion = null)
		{
			if (string.IsNullOrWhiteSpace(codigoArticulo))
				return BadRequest("Falta codigoArticulo");

			var ejercicio = await _sageContext.Periodos
				.Where(p => p.CodigoEmpresa == codigoEmpresa && p.Fechainicio <= DateTime.Now)
				.OrderByDescending(p => p.Fechainicio)
				.Select(p => p.Ejercicio)
				.FirstOrDefaultAsync();
			if (ejercicio == 0) return BadRequest("Sin ejercicio");

			var q = _storageContext.AcumuladoStockUbicacion
				.Where(a =>
					a.CodigoEmpresa == codigoEmpresa &&
					a.Ejercicio == ejercicio &&
					a.CodigoArticulo == codigoArticulo &&
					a.UnidadSaldo != 0
				);

			if (!string.IsNullOrWhiteSpace(partida))
				q = q.Where(a => a.Partida == partida);

			if (!string.IsNullOrWhiteSpace(codigoAlmacen))
				q = q.Where(a => a.CodigoAlmacen == codigoAlmacen);

			if (Request.Query.ContainsKey("codigoUbicacion"))
			{
				var buscada = codigoUbicacion ?? string.Empty;
				q = q.Where(a => a.Ubicacion == buscada);
			}

			var datos = await q.ToListAsync();

			// <-- Aquí comprobamos vacío
			if (!datos.Any())
				return Ok(new List<StockUbicacionDto>());

			return Ok(ProjectToDto(datos));
		}

		/// <summary>
		/// Devuelve únicamente la cadena de alérgenos para un artículo específico.
		/// GET /api/Stock/articulo/alergenos?codigoEmpresa=1&codigoArticulo=10000
		/// </summary>
		[HttpGet("articulo/alergenos")]
		public async Task<IActionResult> ObtenerAlergenosArticulo(
			[FromQuery] short codigoEmpresa,
			[FromQuery] string codigoArticulo)
		{
			// 1) Extrae el valor (puede ser null si no hay fila)
			var alergenos = await _sageContext.VisArticulos
				.Where(a =>
					a.CodigoEmpresa == codigoEmpresa &&
					a.CodigoArticulo == codigoArticulo)
				.Select(a => a.VNEWAlergenos)
				.FirstOrDefaultAsync();

			// 2) Si es null, forzamos cadena vacía
			alergenos ??= string.Empty;

			// 3) Devolvemos siempre Ok con el objeto JSON
			return Ok(new { alergenos });
		}



		// 1.b) Buscar por ubicación (almacén obligatorio + ubicación obligatoria, que puede ser "")
		// GET api/stock/ubicacion?codigoEmpresa=1&codigoAlmacen=101&codigoUbicacion=
		// GET api/stock/ubicacion?codigoEmpresa=1&codigoAlmacen=101&codigoUbicacion=
		[HttpGet("ubicacion")]
		public async Task<IActionResult> PorUbicacion(
			[FromQuery] short codigoEmpresa,
			[FromQuery] string codigoAlmacen,                  // obligatorio
			[FromQuery] string codigoUbicacion = ""            // default="" acepta sin parámetro
		)
		{
			// 1) ejercicio
			var ejercicio = await _sageContext.Periodos
				.Where(p => p.CodigoEmpresa == codigoEmpresa && p.Fechainicio <= DateTime.Now)
				.OrderByDescending(p => p.Fechainicio)
				.Select(p => p.Ejercicio)
				.FirstOrDefaultAsync();
			if (ejercicio == 0)
				return BadRequest("Sin ejercicio");

			// 2) query (nota: Ubicacion=="" devolverá las filas SIN ubicación)
			var datos = await _storageContext.AcumuladoStockUbicacion
				.Where(a =>
					a.CodigoEmpresa == codigoEmpresa &&
					a.Ejercicio == ejercicio &&
					a.CodigoAlmacen == codigoAlmacen &&
					a.Ubicacion == codigoUbicacion &&
					a.UnidadSaldo != 0
				)
				.ToListAsync();

			// 3) proyecta y devuelves (si datos está vacío → Ok([]))
			return Ok(ProjectToDto(datos));
		}



		// helper para proyectar
		private List<StockUbicacionDto> ProjectToDto(List<AcumuladoStockUbicacion> datos)
		{
			if (datos.Count == 0)
				return new List<StockUbicacionDto>();

			var empresa = datos.First().CodigoEmpresa;

			// Trae toda la tabla de almacenes y artículos en memoria para las búsquedas
			var almacenes = _sageContext.Almacenes
				.Where(a => a.CodigoEmpresa == empresa)
				.ToList();
			var articulos = _sageContext.Articulos
				.Where(a => a.CodigoEmpresa == empresa)
				.ToList();

			return datos.Select(s =>
			{
				var alm = almacenes.FirstOrDefault(x =>
					x.CodigoEmpresa == s.CodigoEmpresa &&
					x.CodigoAlmacen == s.CodigoAlmacen);
				var art = articulos.FirstOrDefault(x =>
					x.CodigoEmpresa == s.CodigoEmpresa &&
					x.CodigoArticulo == s.CodigoArticulo);

				return new StockUbicacionDto
				{
					CodigoEmpresa = s.CodigoEmpresa.ToString(),
					CodigoArticulo = s.CodigoArticulo,
					DescripcionArticulo = art?.DescripcionArticulo,
					CodigoAlternativo = art?.CodigoAlternativo,
					CodigoAlternativo2 = art?.CodigoAlternativo2,
					ReferenciaEdi_ = art?.ReferenciaEdi_,
					MRHCodigoAlternativo3 = art?.MRHCodigoAlternativo3,
					VCodigoDUN14 = art?.VCodigoDUN14,

					// aquí concatenamos código + guión largo + nombre de almacén
					CodigoAlmacen = s.CodigoAlmacen,
					Almacen = alm?.Almacen,    // ya trae "100 – ANDALUCIA FABRICA"

					Ubicacion = s.Ubicacion,
					Partida = s.Partida,
					FechaCaducidad = s.FechaCaducidad,
					UnidadSaldo = s.UnidadSaldo
				};
			}).ToList();
		}
	}
}
//	[HttpGet("consulta-stock")]
//	public async Task<IActionResult> ConsultarStock(
//		[FromQuery] short codigoEmpresa,
//		[FromQuery] string? codigoUbicacion,
//		[FromQuery] string? codigoAlmacen,
//		[FromQuery] string? codigoArticulo,
//		[FromQuery] string? codigoCentro,
//		[FromQuery] string? partida)
//	{
//		// Detectar si realmente envían el parámetro "codigoUbicacion"
//		var hasUbicacionParam = Request.Query.ContainsKey("codigoUbicacion");
//		// Y si además viene un almacén no vacío
//		var hasAlmacenParam = !string.IsNullOrWhiteSpace(codigoAlmacen);

//		// Determinar si hay flujo de ubicación
//		var flujoUbicacion = hasUbicacionParam && hasAlmacenParam;
//		var flujoArticulo = !string.IsNullOrWhiteSpace(codigoArticulo);

//		if (!flujoUbicacion && !flujoArticulo)
//			return BadRequest("Debe indicar ubicación + almacén, o un código de artículo.");

//		var ejercicioActual = await _sageContext.Periodos
//			.Where(p => p.CodigoEmpresa == codigoEmpresa && p.Fechainicio <= DateTime.Now)
//			.OrderByDescending(p => p.Fechainicio)
//			.Select(p => p.Ejercicio)
//			.FirstOrDefaultAsync();

//		if (ejercicioActual == 0)
//			return BadRequest("No se encontró un ejercicio válido para la empresa.");

//		var stockData = await _storageContext.AcumuladoStockUbicacion
//			.Where(a =>
//				a.CodigoEmpresa == codigoEmpresa &&
//				a.Ejercicio == ejercicioActual &&
//				a.UnidadSaldo != 0 &&
//				(
//					// Búsqueda por artículo en ubicación y almacén
//           (flujoUbicacion && flujoArticulo && a.Ubicacion == codigoUbicacion && a.CodigoAlmacen == codigoAlmacen && a.CodigoArticulo == codigoArticulo) || // Artículo específico en ubicación y almacén
//           // Búsqueda por ubicación y almacén
//           (flujoUbicacion && !flujoArticulo && a.Ubicacion == codigoUbicacion && a.CodigoAlmacen == codigoAlmacen) || // Todos los artículos en la ubicación y almacén
//           // Búsqueda solo por artículo
//           (flujoArticulo && !flujoUbicacion && a.CodigoArticulo == codigoArticulo) || // Solo buscar por artículo
//           // Búsqueda por almacén específico y ubicación vacía
//           (flujoArticulo && a.CodigoAlmacen == codigoAlmacen && string.IsNullOrEmpty(a.Ubicacion) && a.CodigoArticulo == codigoArticulo) || // Artículo específico en almacén y ubicación vacía
//           // Búsqueda por almacén específico y ubicación vacía (sin artículo)
//           (string.IsNullOrEmpty(codigoUbicacion) && a.CodigoAlmacen == codigoAlmacen && string.IsNullOrEmpty(a.Ubicacion)) // Buscar por almacén específico y ubicación vacía
//				) &&
//				(partida == null || a.Partida == partida))
//			.ToListAsync();

//		// Asegúrate de que la lógica de búsqueda por cadena vacía esté aquí
//		if (string.IsNullOrEmpty(codigoUbicacion) && hasAlmacenParam)
//		{
//			stockData = stockData.Where(a => string.IsNullOrEmpty(a.Ubicacion)).ToList();
//		}

//		var almacenes = await _sageContext.Almacenes
//			.Where(a => a.CodigoEmpresa == codigoEmpresa)
//			.ToListAsync();

//		var articulos = await _sageContext.Articulos
//			.Where(a => a.CodigoEmpresa == codigoEmpresa)
//			.ToListAsync();

//		var resultado = stockData
//		   .Select(s =>
//		   {
//			   var alm = almacenes.FirstOrDefault(a =>
//				   a.CodigoEmpresa == s.CodigoEmpresa &&
//				   a.CodigoAlmacen == s.CodigoAlmacen);

//			   var art = articulos.FirstOrDefault(a =>
//				   a.CodigoEmpresa == s.CodigoEmpresa &&
//				   a.CodigoArticulo == s.CodigoArticulo);

//			   return new StockUbicacionDto
//			   {
//				   CodigoEmpresa = s.CodigoEmpresa.ToString(),
//				   CodigoArticulo = s.CodigoArticulo,
//				   DescripcionArticulo = art?.DescripcionArticulo,
//				   CodigoAlternativo = art?.CodigoAlternativo,
//				   CodigoAlternativo2 = art?.CodigoAlternativo2,
//				   ReferenciaEdi_ = art?.ReferenciaEdi_,
//				   MRHCodigoAlternativo3 = art?.MRHCodigoAlternativo3,
//				   VCodigoDUN14 = art?.VCodigoDUN14,
//				   CodigoCentro = alm?.CodigoCentro?.ToString(),
//				   CodigoAlmacen = s.CodigoAlmacen.ToString(),
//				   Almacen = alm?.Almacen,
//				   Ubicacion = s.Ubicacion,
//				   Partida = s.Partida,
//				   FechaCaducidad = s.FechaCaducidad,
//				   UnidadSaldo = s.UnidadSaldo
//			   };
//		   })
//		   .ToList();

//		return Ok(resultado);
//	}
//}




// --- CODIGO ANTIGUO ---



//[HttpGet("consulta-stock")]
//public async Task<IActionResult> ConsultarStock(
//			[FromQuery] short codigoEmpresa,
//			[FromQuery] string? codigoUbicacion,
//			[FromQuery] string? codigoAlmacen,
//			[FromQuery] string? codigoArticulo,
//			[FromQuery] string? codigoCentro,
//			[FromQuery] string? partida)
//{
//	var flujoUbicacion = !string.IsNullOrWhiteSpace(codigoUbicacion) && !string.IsNullOrWhiteSpace(codigoAlmacen);
//	var flujoArticulo = !string.IsNullOrWhiteSpace(codigoArticulo);

//	if (!flujoUbicacion && !flujoArticulo)
//		return BadRequest("Debe indicar ubicación + código de almacén, o un código de artículo.");

//	var ejercicioActual = await _sageContext.Periodos
//		.Where(p => p.CodigoEmpresa == codigoEmpresa && p.Fechainicio <= DateTime.Now)
//		.OrderByDescending(p => p.Fechainicio)
//		.Select(p => p.Ejercicio)
//		.FirstOrDefaultAsync();

//	if (ejercicioActual == 0)
//		return BadRequest("No se encontró un ejercicio válido para la empresa.");

//	// MODIFICACIÓN PARA BUSCAR ARTÍCULOS EN UBICACIONES CONCRETAS
//	//var stockData = await _storageContext.AcumuladoStockUbicacion
//	//	.Where(a =>
//	//		a.CodigoEmpresa == codigoEmpresa &&
//	//		a.Ejercicio == ejercicioActual &&
//	//		a.UnidadSaldo != 0 &&
//	//		(
//	//			(flujoUbicacion && a.Ubicacion == codigoUbicacion && a.CodigoAlmacen == codigoAlmacen) ||
//	//			(flujoArticulo && a.CodigoArticulo == codigoArticulo)
//	//		) &&
//	//		(partida == null || a.Partida == partida))
//	//	.ToListAsync();

//	var stockData = await _storageContext.AcumuladoStockUbicacion
//.Where(a =>
//	a.CodigoEmpresa == codigoEmpresa &&
//	a.Ejercicio == ejercicioActual &&
//	a.UnidadSaldo != 0 &&
//	(
//		(flujoUbicacion && flujoArticulo && a.Ubicacion == codigoUbicacion && a.CodigoAlmacen == codigoAlmacen && a.CodigoArticulo == codigoArticulo) || // Buscar por artículo en ubicación y almacén
//		(flujoUbicacion && !flujoArticulo && a.Ubicacion == codigoUbicacion && a.CodigoAlmacen == codigoAlmacen) || // Solo buscar por ubicación y almacén
//		(flujoArticulo && !flujoUbicacion && a.CodigoArticulo == codigoArticulo) // Solo buscar por artículo
//	) &&
//	(partida == null || a.Partida == partida))
//.ToListAsync();


//	// MODIFICADO POR SER MUY RESTRICTIVO, SOLO PODÍAMOS SACAR LOS NOMBRES DE ALMACENES ASOCIADOS AL CODIGO CENTRO, PERO NO TIENE EN CUENTA QUE HAY USUARIOS QUE TIENEN MÁS ALMACENES ADEMÁS DE LOS DEL CENTRO
//	//	var almacenes = await _sageContext.Almacenes
//	//.Where(a =>
//	//	a.CodigoEmpresa == codigoEmpresa &&
//	//	(codigoCentro == null || a.CodigoCentro == codigoCentro) &&
//	//	(codigoAlmacen == null || a.CodigoAlmacen == codigoAlmacen)  // <––
//	//)
//	//.ToListAsync();

//	var almacenes = await _sageContext.Almacenes
//		.Where(a => a.CodigoEmpresa == codigoEmpresa)
//	.ToListAsync();

//	var articulos = await _sageContext.Articulos
//		.Where(a => a.CodigoEmpresa == codigoEmpresa)
//		.ToListAsync();

//	var resultado = stockData
//	   .Select(s =>
//	   {
//		   var alm = almacenes.FirstOrDefault(a =>
//			   a.CodigoEmpresa == s.CodigoEmpresa &&
//			   a.CodigoAlmacen == s.CodigoAlmacen);

//		   var art = articulos.FirstOrDefault(a =>
//			   a.CodigoEmpresa == s.CodigoEmpresa &&
//			   a.CodigoArticulo == s.CodigoArticulo);

//		   return new StockUbicacionDto
//		   {
//			   CodigoEmpresa = s.CodigoEmpresa.ToString(),
//			   CodigoArticulo = s.CodigoArticulo,
//			   DescripcionArticulo = art?.DescripcionArticulo,  // <-- nuevo
//			   CodigoAlternativo = art?.CodigoAlternativo,
//			   CodigoAlternativo2 = art?.CodigoAlternativo2,
//			   ReferenciaEdi_ = art?.ReferenciaEdi_,
//			   MRHCodigoAlternativo3 = art?.MRHCodigoAlternativo3,
//			   VCodigoDUN14 = art?.VCodigoDUN14,
//			   CodigoCentro = alm?.CodigoCentro?.ToString(),
//			   CodigoAlmacen = s.CodigoAlmacen.ToString(),
//			   Almacen = alm?.Almacen,
//			   Ubicacion = s.Ubicacion,
//			   Partida = s.Partida,
//			   FechaCaducidad = s.FechaCaducidad,
//			   UnidadSaldo = s.UnidadSaldo
//		   };
//	   })
//	   .ToList();

//	return Ok(resultado);
//}