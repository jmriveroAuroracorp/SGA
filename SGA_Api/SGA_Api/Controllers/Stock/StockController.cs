using Microsoft.AspNetCore.Mvc;
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

		public StockController(SageDbContext sageContext, StorageControlDbContext storageContext)
		{
			_sageContext = sageContext;
			_storageContext = storageContext;
		}

		//// 1.a) Buscar por artículo (+ opcional partida + opcional almacén + opcional ubicación)
		//[HttpGet("articulo")]
		//public async Task<IActionResult> PorArticulo(
		//	[FromQuery] short codigoEmpresa,
		//	[FromQuery] string codigoArticulo,
		//	[FromQuery] string? partida = null,
		//	[FromQuery] string? codigoAlmacen = null,
		//	[FromQuery] string? codigoUbicacion = null)
		//{
		//	if (string.IsNullOrWhiteSpace(codigoArticulo))
		//		return BadRequest("Falta codigoArticulo");

		//	var ejercicio = await _sageContext.Periodos
		//		.Where(p => p.CodigoEmpresa == codigoEmpresa && p.Fechainicio <= DateTime.Now)
		//		.OrderByDescending(p => p.Fechainicio)
		//		.Select(p => p.Ejercicio)
		//		.FirstOrDefaultAsync();
		//	if (ejercicio == 0) return BadRequest("Sin ejercicio");

		//	var q = _storageContext.AcumuladoStockUbicacion
		//		.Where(a =>
		//			a.CodigoEmpresa == codigoEmpresa &&
		//			a.Ejercicio == ejercicio &&
		//			a.CodigoArticulo == codigoArticulo &&
		//			a.UnidadSaldo != 0
		//		);

		//	if (!string.IsNullOrWhiteSpace(partida))
		//		q = q.Where(a => a.Partida == partida);

		//	if (!string.IsNullOrWhiteSpace(codigoAlmacen))
		//		q = q.Where(a => a.CodigoAlmacen == codigoAlmacen);

		//	if (Request.Query.ContainsKey("codigoUbicacion"))
		//	{
		//		var buscada = codigoUbicacion ?? string.Empty;
		//		q = q.Where(a => a.Ubicacion == buscada);
		//	}

		//	var datos = await q.ToListAsync();

		//	if (!datos.Any())
		//		return Ok(new List<StockUbicacionDto>());

		//	return Ok(ProjectToDto(datos));
		//}
		//	[HttpGet("articulo")]
		//	public async Task<IActionResult> PorArticulo(
		//[FromQuery] short codigoEmpresa,
		//[FromQuery] string? codigoArticulo = null,
		//[FromQuery] string? descripcion = null)
		//	{
		//		if (string.IsNullOrWhiteSpace(codigoArticulo) && string.IsNullOrWhiteSpace(descripcion))
		//			return BadRequest("Falta codigoArticulo o descripción");

		//		var ejercicio = await _sageContext.Periodos
		//			.Where(p => p.CodigoEmpresa == codigoEmpresa && p.Fechainicio <= DateTime.Now)
		//			.OrderByDescending(p => p.Fechainicio)
		//			.Select(p => p.Ejercicio)
		//			.FirstOrDefaultAsync();

		//		if (ejercicio == 0)
		//			return BadRequest("Sin ejercicio");

		//		var q = _storageContext.AcumuladoStockUbicacion
		//			.Where(a => a.CodigoEmpresa == codigoEmpresa && a.Ejercicio == ejercicio && a.UnidadSaldo != 0);

		//		if (!string.IsNullOrWhiteSpace(codigoArticulo))
		//		{
		//			q = q.Where(a => a.CodigoArticulo == codigoArticulo);
		//		}
		//		else if (!string.IsNullOrWhiteSpace(descripcion))
		//		{
		//			var codigosArticulos = await ObtenerCodigosArticulosPorDescripcion(descripcion, codigoEmpresa);

		//			if (!codigosArticulos.Any())
		//				return Ok(new List<StockUbicacionDto>());

		//			q = q.Where(a => codigosArticulos.Contains(a.CodigoArticulo));
		//		}

		//		var datos = await q.ToListAsync();

		//		return Ok(ProjectToDto(datos));
		//	}

		[HttpGet("articulo")]
		public async Task<IActionResult> PorArticulo(
			[FromQuery] short codigoEmpresa,
			[FromQuery] string? codigoArticulo = null,
			[FromQuery] string? descripcion = null,
			[FromQuery] string? partida = null,
			[FromQuery] string? codigoAlmacen = null,
			[FromQuery] string? codigoUbicacion = null)
		{
			if (string.IsNullOrWhiteSpace(codigoArticulo) && string.IsNullOrWhiteSpace(descripcion))
				return BadRequest("Falta codigoArticulo o descripción");

			var ejercicio = await _sageContext.Periodos
				.Where(p => p.CodigoEmpresa == codigoEmpresa && p.Fechainicio <= DateTime.Now)
				.OrderByDescending(p => p.Fechainicio)
				.Select(p => p.Ejercicio)
				.FirstOrDefaultAsync();

			if (ejercicio == 0)
				return BadRequest("Sin ejercicio");

			var q = _storageContext.AcumuladoStockUbicacion
				.Where(a => a.CodigoEmpresa == codigoEmpresa &&
							a.Ejercicio == ejercicio &&
							a.UnidadSaldo != 0);

			// Filtro por código o descripción
			if (!string.IsNullOrWhiteSpace(codigoArticulo))
			{
				q = q.Where(a => a.CodigoArticulo == codigoArticulo);
			}
			else if (!string.IsNullOrWhiteSpace(descripcion))
			{
				var codigosArticulos = await ObtenerCodigosArticulosPorDescripcion(descripcion, codigoEmpresa);
				if (!codigosArticulos.Any())
					return Ok(new List<StockUbicacionDto>());
				q = q.Where(a => codigosArticulos.Contains(a.CodigoArticulo));
			}

			// Filtros adicionales opcionales
			if (!string.IsNullOrWhiteSpace(partida))
				q = q.Where(a => a.Partida == partida);

			if (!string.IsNullOrWhiteSpace(codigoAlmacen))
				q = q.Where(a => a.CodigoAlmacen == codigoAlmacen);

			if (!string.IsNullOrWhiteSpace(codigoUbicacion))
				q = q.Where(a => a.Ubicacion == codigoUbicacion);

			var datos = await q.ToListAsync();
			return Ok(ProjectToDto(datos));
		}



		// 1.b) Buscar por ubicación (almacén obligatorio + ubicación obligatoria, que puede ser "")
		[HttpGet("ubicacion")]
		public async Task<IActionResult> PorUbicacion(
			[FromQuery] short codigoEmpresa,
			[FromQuery] string codigoAlmacen,                  // obligatorio
			[FromQuery] string codigoUbicacion = ""            // default="" acepta sin parámetro
		)
		{
			var ejercicio = await _sageContext.Periodos
				.Where(p => p.CodigoEmpresa == codigoEmpresa && p.Fechainicio <= DateTime.Now)
				.OrderByDescending(p => p.Fechainicio)
				.Select(p => p.Ejercicio)
				.FirstOrDefaultAsync();
			if (ejercicio == 0)
				return BadRequest("Sin ejercicio");

			var datos = await _storageContext.AcumuladoStockUbicacion
				.Where(a =>
					a.CodigoEmpresa == codigoEmpresa &&
					a.Ejercicio == ejercicio &&
					a.CodigoAlmacen == codigoAlmacen &&
					a.Ubicacion == codigoUbicacion &&
					a.UnidadSaldo != 0
				)
				.ToListAsync();

			return Ok(ProjectToDto(datos));
		}

		// 1.c) Buscar por artículo (nuevo endpoint)
		[HttpGet("consulta-stock")]
		public async Task<IActionResult> ConsultarStock(
			[FromQuery] short codigoEmpresa,
			[FromQuery] string? codigoUbicacion,
			[FromQuery] string? codigoAlmacen,
			[FromQuery] string? codigoArticulo,
			[FromQuery] string? codigoCentro,
			[FromQuery] string? almacen,
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

			var almacenes = await _sageContext.Almacenes
				.Where(a =>
					a.CodigoEmpresa == codigoEmpresa &&
					(codigoCentro == null || a.CodigoCentro == codigoCentro) &&
					(almacen == null || a.Almacen == almacen))
				.ToListAsync();

			var resultado = stockData
				.Select(s =>
				{
					var alm = almacenes.FirstOrDefault(a =>
						a.CodigoEmpresa == s.CodigoEmpresa &&
						a.CodigoAlmacen == s.CodigoAlmacen);

					return new StockUbicacionDto
					{
						CodigoEmpresa = s.CodigoEmpresa.ToString(),
						CodigoArticulo = s.CodigoArticulo,
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

		// 1.d) Buscar artículo (nuevo endpoint)
		[HttpGet("buscar-articulo")]
		public async Task<IActionResult> BuscarArticulo(
			[FromQuery] string? codigoAlternativo,
			[FromQuery] string? codigoArticulo,
			[FromQuery] string? descripcion)
		{
			var query = _sageContext.Articulos.AsQueryable();

			if (!string.IsNullOrWhiteSpace(codigoAlternativo))
			{
				query = query.Where(a =>
					a.CodigoAlternativo == codigoAlternativo ||
					a.CodigoAlternativo2 == codigoAlternativo ||
					a.ReferenciaEdi_ == codigoAlternativo ||
					a.MRHCodigoAlternativo3 == codigoAlternativo ||
					a.VCodigoDUN14 == codigoAlternativo
				);
			}
			else if (!string.IsNullOrWhiteSpace(codigoArticulo))
			{
				query = query.Where(a => a.CodigoArticulo == codigoArticulo);
			}
			else if (!string.IsNullOrWhiteSpace(descripcion))
			{
				query = query.Where(a => a.DescripcionArticulo.Contains(descripcion));
			}
			else
			{
				return BadRequest("Debe especificar algún criterio de búsqueda.");
			}

			var resultado = await query
				.Select(a => new ArticuloDto
				{
					CodigoArticulo = a.CodigoArticulo,
					Descripcion = a.DescripcionArticulo,
					CodigoAlternativo = a.CodigoAlternativo
				})
				.ToListAsync();

			return Ok(resultado);
		}

		/// <summary>
		/// GET api/Stock/articulo/alergenos
		/// Devuelve los alérgenos de un artículo, leyendo Vis_Articulos.VNEWALERGENOS
		/// </summary>
		// GET api/Stock/articulo/alergenos?codigoEmpresa=1&codigoArticulo=096124
		[HttpGet("articulo/alergenos")]
		public async Task<IActionResult> GetAlergenos(
			[FromQuery] short codigoEmpresa,
			[FromQuery] string codigoArticulo)
		{
			var art = await _sageContext.VisArticulos
				.AsNoTracking()
				.FirstOrDefaultAsync(x =>
					x.CodigoEmpresa == codigoEmpresa &&
					x.CodigoArticulo.Equals(codigoArticulo));

			if (art == null)
				return NotFound();

			// Ahora sí leerá bien la propiedad mapeada
			var alerg = art.VNEWAlergenos ?? string.Empty;

			return Ok(new { alergenos = alerg });
		}



		// helper para proyectar
		private List<StockUbicacionDto> ProjectToDto(List<AcumuladoStockUbicacion> datos)
		{
			if (datos.Count == 0)
				return new List<StockUbicacionDto>();

			var empresa = datos.First().CodigoEmpresa;

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
					CodigoAlmacen = s.CodigoAlmacen,
					Almacen = alm?.Almacen,
					Ubicacion = s.Ubicacion,
					Partida = s.Partida,
					FechaCaducidad = s.FechaCaducidad,
					UnidadSaldo = s.UnidadSaldo
				};
			}).ToList();
		}
		private async Task<List<string>> ObtenerCodigosArticulosPorDescripcion(string descripcion, short codigoEmpresa)
		{
			var articulos = await _sageContext.Articulos
				.Where(a => a.CodigoEmpresa == codigoEmpresa && a.DescripcionArticulo.Contains(descripcion))
				.Select(a => a.CodigoArticulo)
				.ToListAsync();

			return articulos;
		}

	}

}
