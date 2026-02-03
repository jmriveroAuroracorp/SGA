using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Almacen;
using SGA_Api.Models.Palet;
using SGA_Api.Models.Stock;
using SGA_Api.Models.Registro;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SGA_Api.Controllers.Stock
{
	[ApiController]
	[Route("api/[controller]")]
	public class StockController : ControllerBase
	{
		private readonly SageDbContext _sageContext;
		private readonly StorageControlDbContext _storageContext;
		private readonly AuroraSgaDbContext _auroraSgaContext;
		private readonly IServiceProvider _serviceProvider;
		private readonly ILogger<StockController> _logger;

		public StockController(SageDbContext sageContext, StorageControlDbContext storageContext, AuroraSgaDbContext auroraSgaContext, IServiceProvider serviceProvider, ILogger<StockController> logger)
		{
			_sageContext = sageContext;
			_storageContext = storageContext;
			_auroraSgaContext = auroraSgaContext;
			_serviceProvider = serviceProvider;
			_logger = logger;
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
			var resultado = await ProjectToDtoConBloqueosAsync(datos, codigoEmpresa);
			
			// Registrar evento de consulta (después de preparar la respuesta para no bloquear)
			var detalleConsulta = $"Empresa={codigoEmpresa}";
			if (!string.IsNullOrWhiteSpace(codigoArticulo)) detalleConsulta += $", Articulo={codigoArticulo}";
			if (!string.IsNullOrWhiteSpace(descripcion)) detalleConsulta += $", Descripcion={descripcion}";
			if (!string.IsNullOrWhiteSpace(partida)) detalleConsulta += $", Partida={partida}";
			if (!string.IsNullOrWhiteSpace(codigoAlmacen)) detalleConsulta += $", Almacen={codigoAlmacen}";
			if (!string.IsNullOrWhiteSpace(codigoUbicacion)) detalleConsulta += $", Ubicacion={codigoUbicacion}";
			detalleConsulta += $", Resultados={resultado.Count}";
			
			RegistrarEventoConsultaStockAsync(
				"StockController/PorArticulo",
				$"Consulta de stock por artículo{(string.IsNullOrWhiteSpace(codigoArticulo) ? " (por descripción)" : "")}",
				detalleConsulta);
			
			return Ok(resultado);
		}



		// 1.b) Buscar por ubicación (almacén obligatorio + ubicación opcional)
		[HttpGet("ubicacion")]
		public async Task<IActionResult> PorUbicacion(
			[FromQuery] short codigoEmpresa,
			[FromQuery] string codigoAlmacen,                  // obligatorio
			[FromQuery] string? codigoUbicacion = null,        // 🔷 MODIFICADO: Ahora es opcional
			[FromQuery] bool incluirStockCero = false          // 🔷 NUEVO: incluir artículos con stock 0
		)
		{
			var ejercicio = await _sageContext.Periodos
				.Where(p => p.CodigoEmpresa == codigoEmpresa && p.Fechainicio <= DateTime.Now)
				.OrderByDescending(p => p.Fechainicio)
				.Select(p => p.Ejercicio)
				.FirstOrDefaultAsync();
			if (ejercicio == 0)
				return BadRequest("Sin ejercicio");

			var query = _storageContext.AcumuladoStockUbicacion
				.Where(a =>
					a.CodigoEmpresa == codigoEmpresa &&
					a.Ejercicio == ejercicio &&
					a.CodigoAlmacen == codigoAlmacen &&
					(incluirStockCero || a.UnidadSaldo != 0)   // 🔷 MODIFICADO: filtro condicional
				);

			// 🔷 LÓGICA FINAL: Diferenciar entre todo el almacén, sin ubicación y ubicación específica
			var queryString = Request.QueryString.ToString();
			var tieneParametroUbicacion = queryString.Contains("codigoUbicacion=");

			if (!tieneParametroUbicacion)
			{
				// No se envió el parámetro → Todo el almacén (sin filtro de ubicación)
				// No aplicamos ningún filtro adicional
			}
			else if (codigoUbicacion == null || codigoUbicacion == "")
			{
				// Se envió el parámetro pero es null o vacío → Solo artículos sin ubicar
				query = query.Where(a => string.IsNullOrEmpty(a.Ubicacion));
			}
			else
			{
				// Se envió el parámetro con valor → Ubicación específica
				query = query.Where(a => a.Ubicacion == codigoUbicacion);
			}

			var datos = await query.ToListAsync();
			var resultado = await ProjectToDtoConBloqueosAsync(datos, codigoEmpresa);

			// Registrar evento de consulta
			var detalleConsulta = $"Empresa={codigoEmpresa}, Almacen={codigoAlmacen}";
			if (tieneParametroUbicacion)
			{
				if (string.IsNullOrWhiteSpace(codigoUbicacion))
					detalleConsulta += ", Ubicacion=(sin ubicar)";
				else
					detalleConsulta += $", Ubicacion={codigoUbicacion}";
			}
			else
			{
				detalleConsulta += ", Ubicacion=(todo el almacén)";
			}
			detalleConsulta += $", IncluirStockCero={incluirStockCero}, Resultados={datos.Count}";
			
			RegistrarEventoConsultaStockAsync(
				"StockController/PorUbicacion",
				"Consulta de stock por ubicación",
				detalleConsulta);

			return Ok(resultado);
		}

		//// 1.c) Buscar por artículo (nuevo endpoint)
		//[HttpGet("consulta-stock")]
		//public async Task<IActionResult> ConsultarStock(
		//	[FromQuery] short codigoEmpresa,
		//	[FromQuery] string? codigoUbicacion,
		//	[FromQuery] string? codigoAlmacen,
		//	[FromQuery] string? codigoArticulo,
		//	[FromQuery] string? codigoCentro,
		//	[FromQuery] string? almacen,
		//	[FromQuery] string? partida)
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

		//	var stockData = await _storageContext.AcumuladoStockUbicacion
		//		.Where(a =>
		//			a.CodigoEmpresa == codigoEmpresa &&
		//			a.Ejercicio == ejercicioActual &&
		//			a.UnidadSaldo != 0 &&
		//			(
		//				(flujoUbicacion && a.Ubicacion == codigoUbicacion && a.CodigoAlmacen == codigoAlmacen) ||
		//				(flujoArticulo && a.CodigoArticulo == codigoArticulo)
		//			) &&
		//			(partida == null || a.Partida == partida))
		//		.ToListAsync();

		//	var almacenes = await _sageContext.Almacenes
		//		.Where(a =>
		//			a.CodigoEmpresa == codigoEmpresa &&
		//			(codigoCentro == null || a.CodigoCentro == codigoCentro) &&
		//			(almacen == null || a.Almacen == almacen))
		//		.ToListAsync();

		//	var resultado = stockData
		//		.Select(s =>
		//		{
		//			var alm = almacenes.FirstOrDefault(a =>
		//				a.CodigoEmpresa == s.CodigoEmpresa &&
		//				a.CodigoAlmacen == s.CodigoAlmacen);

		//			return new StockUbicacionDto
		//			{
		//				CodigoEmpresa = s.CodigoEmpresa.ToString(),
		//				CodigoArticulo = s.CodigoArticulo,
		//				CodigoCentro = alm?.CodigoCentro?.ToString(),
		//				CodigoAlmacen = s.CodigoAlmacen.ToString(),
		//				Almacen = alm?.Almacen,
		//				Ubicacion = s.Ubicacion,
		//				Partida = s.Partida,
		//				FechaCaducidad = s.FechaCaducidad,
		//				UnidadSaldo = s.UnidadSaldo
		//			};
		//		})
		//		.ToList();

		//	return Ok(resultado);
		//}
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
			var flujoUbicacion = !string.IsNullOrWhiteSpace(codigoAlmacen);
			var flujoArticulo = !string.IsNullOrWhiteSpace(codigoArticulo);

			if (!flujoUbicacion && !flujoArticulo)
			{
				return BadRequest("Debe indicar ubicación + código de almacén, o un código de artículo.");
			}

			var q = _auroraSgaContext.StockDisponible
				.Where(a => a.CodigoEmpresa == codigoEmpresa);

		if (flujoUbicacion)
		{
			q = q.Where(a => a.CodigoAlmacen == codigoAlmacen);

			// 🔷 LÓGICA MEJORADA: Diferenciar entre todo el almacén, sin ubicación y ubicación específica
			var queryString = Request.QueryString.ToString();
			var tieneParametroUbicacion = queryString.Contains("codigoUbicacion=");

			if (tieneParametroUbicacion)
			{
				if (codigoUbicacion == null || codigoUbicacion == "")
				{
					// Se envió el parámetro pero es null o vacío → Solo artículos sin ubicar
					q = q.Where(a => string.IsNullOrEmpty(a.Ubicacion));
				}
				else
				{
					// Se envió el parámetro con valor → Ubicación específica
					q = q.Where(a => a.Ubicacion == codigoUbicacion);
				}
			}
			// Si no se envió el parámetro, no aplicamos filtro de ubicación (todo el almacén)

			if (flujoArticulo)
				q = q.Where(a => a.CodigoArticulo == codigoArticulo);
		}
			else if (flujoArticulo)
			{
				q = q.Where(a => a.CodigoArticulo == codigoArticulo);
			}

			if (!string.IsNullOrWhiteSpace(partida))
				q = q.Where(a => a.Partida == partida);

			q = q.Where(a => a.Disponible > 0);

			var datos = await q.ToListAsync();

			var resultado = new List<object>();

			foreach (var item in datos)
			{
				var stockPaletizado = await _auroraSgaContext.PaletLineas
					.Where(pl => pl.CodigoEmpresa == item.CodigoEmpresa &&
								pl.CodigoArticulo == item.CodigoArticulo &&
								pl.CodigoAlmacen == item.CodigoAlmacen &&
								pl.Ubicacion == item.Ubicacion &&
								pl.Lote == item.Partida)
					.Include(pl => pl.Palet)
					.Where(pl => pl.Palet.Estado.ToUpper() == "ABIERTO" || pl.Palet.Estado.ToUpper() == "CERRADO")
					.Select(pl => new
					{
						PaletId = pl.PaletId,
						CodigoPalet = pl.Palet.Codigo,
						EstadoPalet = pl.Palet.Estado,
						CantidadEnPalet = pl.Cantidad,
						TraspasoId = pl.TraspasoId  // 🔷 NUEVO: Incluir TraspasoId de la línea
					})
					.ToListAsync();

				// 🔷 CORREGIDO: Asegurar que el cálculo se hace con precisión completa
				// Convertir explícitamente a decimal con precisión completa antes de la resta
				var totalPaletizado = stockPaletizado.Select(p => (decimal)p.CantidadEnPalet).Sum();
				var stockSuelto = (decimal)item.Disponible - totalPaletizado;

				// 🔷 NUEVO: Verificar bloqueo de calidad específico para esta ubicación
				var queryBloqueo = _auroraSgaContext.BloqueosCalidad
					.Where(b => b.CodigoEmpresa == codigoEmpresa &&
							   b.CodigoArticulo == item.CodigoArticulo &&
							   b.LotePartida == item.Partida &&
							   b.CodigoAlmacen == item.CodigoAlmacen &&
							   b.Bloqueado == true);

				// Filtrar por ubicación específica
				if (!string.IsNullOrWhiteSpace(item.Ubicacion))
				{
					queryBloqueo = queryBloqueo.Where(b => b.Ubicacion == item.Ubicacion);
				}
				else
				{
					queryBloqueo = queryBloqueo.Where(b => string.IsNullOrEmpty(b.Ubicacion));
				}

				var bloqueoEspecifico = await queryBloqueo
					.OrderByDescending(b => b.FechaBloqueo)
					.FirstOrDefaultAsync();

				// 🔷 NUEVO: Obtener fecha del último traspaso para stock suelto
				DateTime? fechaUltimoTraspasoSuelto = null;
				if (stockSuelto > 0)
				{
					var ultimoTraspasoSuelto = await _auroraSgaContext.Traspasos
						.Where(t => t.TipoTraspaso == "ARTICULO" &&
								   t.CodigoEstado == "COMPLETADO" &&
								   t.FechaFinalizacion != null &&
								   t.CodigoArticulo == item.CodigoArticulo &&
								   t.CodigoEmpresa == item.CodigoEmpresa &&
								   t.AlmacenDestino == item.CodigoAlmacen &&
								   (string.IsNullOrWhiteSpace(item.Ubicacion) ? string.IsNullOrWhiteSpace(t.UbicacionDestino) : t.UbicacionDestino == item.Ubicacion) &&
								   (string.IsNullOrWhiteSpace(item.Partida) ? string.IsNullOrWhiteSpace(t.Partida) : t.Partida == item.Partida))
						.OrderByDescending(t => t.FechaFinalizacion)
						.Select(t => t.FechaFinalizacion)
						.FirstOrDefaultAsync();
					
					fechaUltimoTraspasoSuelto = ultimoTraspasoSuelto;
				}

				if (stockSuelto > 0)
				{
					resultado.Add(new
					{
						CodigoEmpresa = item.CodigoEmpresa.ToString(),
						CodigoArticulo = item.CodigoArticulo,
						DescripcionArticulo = item.DescripcionArticulo,
						CodigoAlmacen = item.CodigoAlmacen,
						Almacen = item.Almacen,
						Ubicacion = item.Ubicacion,
						Partida = item.Partida,
						FechaCaducidad = item.FechaCaducidad,
						UnidadSaldo = item.UnidadSaldo,
						Reservado = item.Reservado,
						Disponible = stockSuelto,
						TipoStock = "Suelto",
						PaletId = (Guid?)null,
						CodigoPalet = (string?)null,
						EstadoPalet = (string?)null,
						// 🔷 NUEVO: Fecha del último traspaso
						FechaUltimoTraspaso = fechaUltimoTraspasoSuelto,
						// 🔷 NUEVO: Información de bloqueo por calidad
						IsBloqueadoCalidad = bloqueoEspecifico != null,
						MotivoBloqueoCalidad = bloqueoEspecifico?.ComentarioBloqueo,
						FechaBloqueoCalidad = bloqueoEspecifico?.FechaBloqueo,
						TipoBloqueoCalidad = bloqueoEspecifico?.TipoBloqueo ?? "TOTAL"
					});
				}

				foreach (var palet in stockPaletizado)
				{
					// 🔷 CORREGIDO: Buscar siempre el traspaso MÁS RECIENTE para este palet + artículo + partida
					// No usar solo el TraspasoId de la línea porque puede ser un traspaso antiguo
					// Hay que buscar TODOS los traspasos y tomar el más reciente
					var fechaUltimoTraspasoPalet = await _auroraSgaContext.Traspasos
						.Where(t => t.TipoTraspaso == "PALET" &&
								   t.FechaFinalizacion != null &&
								   t.CodigoPalet == palet.CodigoPalet &&
								   t.CodigoArticulo == item.CodigoArticulo &&
								   (string.IsNullOrWhiteSpace(item.Partida) ? string.IsNullOrWhiteSpace(t.Partida) : t.Partida == item.Partida))
						.OrderByDescending(t => t.FechaFinalizacion)
						.Select(t => t.FechaFinalizacion)
						.FirstOrDefaultAsync();

					resultado.Add(new
					{
						CodigoEmpresa = item.CodigoEmpresa.ToString(),
						CodigoArticulo = item.CodigoArticulo,
						DescripcionArticulo = item.DescripcionArticulo,
						CodigoAlmacen = item.CodigoAlmacen,
						Almacen = item.Almacen,
						Ubicacion = item.Ubicacion,
						Partida = item.Partida,
						FechaCaducidad = item.FechaCaducidad,
						UnidadSaldo = item.UnidadSaldo,
						Reservado = item.Reservado,
						Disponible = palet.CantidadEnPalet,
						TipoStock = "Paletizado",
						PaletId = palet.PaletId,
						CodigoPalet = palet.CodigoPalet,
						EstadoPalet = palet.EstadoPalet,
						// 🔷 NUEVO: Fecha del último traspaso
						FechaUltimoTraspaso = fechaUltimoTraspasoPalet,
						// 🔷 NUEVO: Información de bloqueo por calidad
						IsBloqueadoCalidad = bloqueoEspecifico != null,
						MotivoBloqueoCalidad = bloqueoEspecifico?.ComentarioBloqueo,
						FechaBloqueoCalidad = bloqueoEspecifico?.FechaBloqueo,
						TipoBloqueoCalidad = bloqueoEspecifico?.TipoBloqueo ?? "TOTAL"
					});
				}
			}

			// Registrar evento de consulta
			var detalleConsulta = $"Empresa={codigoEmpresa}";
			if (flujoUbicacion) detalleConsulta += $", Almacen={codigoAlmacen}";
			if (flujoArticulo) detalleConsulta += $", Articulo={codigoArticulo}";
			if (!string.IsNullOrWhiteSpace(codigoUbicacion)) detalleConsulta += $", Ubicacion={codigoUbicacion}";
			if (!string.IsNullOrWhiteSpace(partida)) detalleConsulta += $", Partida={partida}";
			detalleConsulta += $", Resultados={resultado.Count}";
			
			RegistrarEventoConsultaStockAsync(
				"StockController/ConsultarStock",
				"Consulta de stock (avanzada)",
				detalleConsulta);

			return Ok(resultado);
		}





		// 1.d) Buscar artículo (nuevo endpoint)
		[HttpGet("buscar-articulo")]
		public async Task<IActionResult> BuscarArticulo(
[FromQuery] short codigoEmpresa,
[FromQuery] string? descripcion,
[FromQuery] string? codigoAlternativo,
[FromQuery] string? codigoArticulo,
// mismos filtros opcionales
[FromQuery] string? codigoUbicacion,
[FromQuery] string? codigoAlmacen,
[FromQuery] string? partida)
		{
			// 1. búsquedas directas (sin cambios) ------------
			if (!string.IsNullOrWhiteSpace(codigoAlternativo))
			{
				var lista = await BuscarPorAlternativo(codigoAlternativo);
				
				// 🔷 ELIMINADO: No registrar eventos en búsquedas intermedias, solo en consultas de stock reales
				// BuscarArticulo es una búsqueda intermedia para encontrar el artículo, no la acción final del usuario
				
				return Ok(lista);
			}

			if (!string.IsNullOrWhiteSpace(codigoArticulo))
			{
				var lista = await BuscarPorCodigo(codigoArticulo);
				
				// 🔷 ELIMINADO: No registrar eventos en búsquedas intermedias, solo en consultas de stock reales
				// BuscarArticulo es una búsqueda intermedia para encontrar el artículo, no la acción final del usuario
				
				return Ok(lista);
			}

			// 2. descripción + stock -------------------------
			if (!string.IsNullOrWhiteSpace(descripcion))
			{
				var ejercicioActual = await _sageContext.Periodos
					.Where(p => p.CodigoEmpresa == codigoEmpresa &&
								p.Fechainicio <= DateTime.Now)
					.OrderByDescending(p => p.Fechainicio)
					.Select(p => p.Ejercicio)
					.FirstOrDefaultAsync();

				if (ejercicioActual == 0)
					return BadRequest("Ejercicio no encontrado");

				// 2.1 códigos CON stock (mismos filtros que consulta-stock)
				var codigosConStock = await _storageContext.AcumuladoStockUbicacion
					.Where(s =>
						s.CodigoEmpresa == codigoEmpresa &&
						s.Ejercicio == ejercicioActual &&
						s.UnidadSaldo > 0 &&
						(partida == null || s.Partida == partida) &&
						(codigoAlmacen == null || s.CodigoAlmacen == codigoAlmacen) &&
						(codigoUbicacion == null || s.Ubicacion == codigoUbicacion))
					.Select(s => s.CodigoArticulo)
					.Distinct()
					.ToListAsync();

				if (codigosConStock.Count == 0)
					return Ok(new List<ArticuloDto>());

				var codigosSet = codigosConStock.ToHashSet();

				// 2.2 Artículos cuya descripción contenga el texto
				var articulos = await _sageContext.Articulos
					.Where(a =>
						a.CodigoEmpresa == codigoEmpresa &&
						a.DescripcionArticulo.Contains(descripcion))
					.Select(a => new ArticuloDto
					{
						CodigoArticulo = a.CodigoArticulo,
						Descripcion = a.DescripcionArticulo,
						CodigoAlternativo = a.CodigoAlternativo
					})
					.ToListAsync();

				// 2.3 Filtrado en memoria (evita OPENJSON)
				var resultado = articulos
					.Where(a => codigosSet.Contains(a.CodigoArticulo))
					.ToList();

				// 🔷 ELIMINADO: No registrar eventos en búsquedas intermedias, solo en consultas de stock reales
				// BuscarArticulo es una búsqueda intermedia para encontrar el artículo, no la acción final del usuario

				return Ok(resultado);
			}

			return BadRequest("Debe especificar algún criterio de búsqueda.");

			// --- helpers privados ---------------------------
			async Task<List<ArticuloDto>> BuscarPorAlternativo(string alt)
			{
				var lista = await _sageContext.Articulos
					.Where(a =>
						a.CodigoAlternativo == alt ||
						a.CodigoAlternativo2 == alt ||
						a.ReferenciaEdi_ == alt ||
						a.MRHCodigoAlternativo3 == alt ||
						a.VCodigoDUN14 == alt)
					.Select(a => new ArticuloDto
					{
						CodigoArticulo = a.CodigoArticulo,
						Descripcion = a.DescripcionArticulo,
						CodigoAlternativo = a.CodigoAlternativo
					})
					.ToListAsync();

				// No registrar log aquí, se registra en el método público BuscarArticulo
				return lista;
			}

			async Task<List<ArticuloDto>> BuscarPorCodigo(string cod)
			{
				var lista = await _sageContext.Articulos
					.Where(a => a.CodigoArticulo == cod)
					.Select(a => new ArticuloDto
					{
						CodigoArticulo = a.CodigoArticulo,
						Descripcion = a.DescripcionArticulo,
						CodigoAlternativo = a.CodigoAlternativo
					})
					.ToListAsync();

				// No registrar log aquí, se registra en el método público BuscarArticulo
				return lista;
			}
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

		/// <summary>
		/// GET api/Stock/articulo/lotes-activos
		/// Obtiene los lotes activos de un artículo (incluso sin stock) con su fecha de caducidad
		/// </summary>
		[HttpGet("articulo/lotes-activos")]
		public async Task<IActionResult> ObtenerLotesActivosArticulo(
			[FromQuery] short codigoEmpresa,
			[FromQuery] string codigoArticulo,
			[FromQuery] bool incluirHistoricos = false)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(codigoArticulo))
					return BadRequest("El código de artículo es obligatorio");

				IQueryable<AcumuladoStockUbicacion> query = _storageContext.AcumuladoStockUbicacion
					.Where(s => s.CodigoEmpresa == codigoEmpresa &&
							   s.CodigoArticulo == codigoArticulo &&
							   !string.IsNullOrEmpty(s.Partida));

				// Si no se solicitan históricos, filtrar solo por el ejercicio actual
				if (!incluirHistoricos)
				{
					var ejercicio = await _sageContext.Periodos
						.Where(p => p.CodigoEmpresa == codigoEmpresa && p.Fechainicio <= DateTime.Now)
						.OrderByDescending(p => p.Fechainicio)
						.Select(p => p.Ejercicio)
						.FirstOrDefaultAsync();
					
					if (ejercicio == 0)
						return BadRequest("Sin ejercicio válido");

					query = query.Where(s => s.Ejercicio == ejercicio);
				}

				// Obtener todos los lotes activos del artículo con todas sus fechas de caducidad posibles (incluso con stock 0)
				var registros = await query.ToListAsync();

				// Agrupar por partida y obtener todas las fechas únicas
				var lotesConFechas = registros
					.GroupBy(s => s.Partida)
					.Select(g => new
					{
						Partida = g.Key,
						FechasCaducidad = g.Where(s => s.FechaCaducidad.HasValue)
										   .Select(s => s.FechaCaducidad.Value.Date)
										   .Distinct()
										   .OrderBy(f => f)
										   .ToList()
					})
					.OrderBy(x => x.Partida)
					.ToList();

				// Convertir a formato de respuesta: devolver un objeto por cada combinación partida-fecha
				var resultado = new List<object>();
				foreach (var lote in lotesConFechas)
				{
					if (lote.FechasCaducidad.Any())
					{
						// Si hay fechas, devolver una entrada por cada fecha
						foreach (var fecha in lote.FechasCaducidad)
						{
							resultado.Add(new
							{
								partida = lote.Partida,
								fechaCaducidad = fecha
							});
						}
					}
					else
					{
						// Si no hay fechas, devolver una entrada con fecha null
						resultado.Add(new
						{
							partida = lote.Partida,
							fechaCaducidad = (DateTime?)null
						});
					}
				}

				return Ok(resultado);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error al obtener lotes activos para artículo {CodigoArticulo}", codigoArticulo);
				return StatusCode(500, "Error interno del servidor");
			}
		}

		/// <summary>
		/// GET api/Stock/articulo/disponible
		/// Devuelve el stock disponible con la reserva de stock de los palets
		/// </summary>
		[HttpGet("articulo/disponible")]
		public async Task<IActionResult> PorArticuloDisponible(
    [FromQuery] short codigoEmpresa,
    [FromQuery] string? codigoArticulo = null,
    [FromQuery] string? descripcion = null,
    [FromQuery] string? partida = null,
    [FromQuery] string? codigoAlmacen = null,
    [FromQuery] string? codigoUbicacion = null)
{
    if (string.IsNullOrWhiteSpace(codigoArticulo) && string.IsNullOrWhiteSpace(descripcion))
        return BadRequest("Falta codigoArticulo o descripción");

    var q = _auroraSgaContext.StockDisponible
        .Where(a => a.CodigoEmpresa == codigoEmpresa);

    // Filtro por código o descripción
    if (!string.IsNullOrWhiteSpace(codigoArticulo))
    {
        q = q.Where(a => a.CodigoArticulo == codigoArticulo);
    }
    else if (!string.IsNullOrWhiteSpace(descripcion))
    {
        var codigosArticulos = await ObtenerCodigosArticulosPorDescripcion(descripcion, codigoEmpresa);
        if (!codigosArticulos.Any())
            return Ok(new List<object>());
        q = q.Where(a => codigosArticulos.Contains(a.CodigoArticulo));
    }

    // Filtros adicionales opcionales
    if (!string.IsNullOrWhiteSpace(partida))
        q = q.Where(a => a.Partida == partida);

    if (!string.IsNullOrWhiteSpace(codigoAlmacen))
        q = q.Where(a => a.CodigoAlmacen == codigoAlmacen);

    // 🔷 LÓGICA FINAL: Diferenciar entre todo el almacén, sin ubicación y ubicación específica
    var queryString = Request.QueryString.ToString();
    var tieneParametroUbicacion = queryString.Contains("codigoUbicacion=");

    if (!tieneParametroUbicacion)
    {
        // No se envió el parámetro → Sin filtro de ubicación (todas las ubicaciones)
        // No aplicamos ningún filtro adicional
    }
    else if (codigoUbicacion == null || codigoUbicacion == "")
    {
        // Se envió el parámetro pero es null o vacío → Solo artículos sin ubicar
        q = q.Where(a => string.IsNullOrEmpty(a.Ubicacion));
    }
    else
    {
        // Se envió el parámetro con valor → Ubicación específica
        q = q.Where(a => a.Ubicacion == codigoUbicacion);
    }

    // 🔷 aquí filtramos solo los registros con disponible > 0
    q = q.Where(a => a.Disponible > 0);

    var datos = await q.ToListAsync();
    
    // 🔷 NUEVO: Validación batch de sincronización de stock
    // Agrupar artículos únicos por (CodigoArticulo, Partida, CodigoAlmacen)
    var articulosUnicos = datos
        .GroupBy(d => new { d.CodigoArticulo, d.Partida, d.CodigoAlmacen })
        .Select(g => (g.Key.CodigoArticulo, g.Key.Partida ?? "", g.Key.CodigoAlmacen))
        .Distinct()
        .ToList();

    // Validar todos en batch (una sola llamada)
    var validacionesSincronizacion = await ValidarSincronizacionStockBatchAsync(
        codigoEmpresa,
        articulosUnicos);
    
    // 🔷 NUEVO: Consultar alérgenos de cada artículo individualmente (con caché para evitar consultas duplicadas)
    var alergenosCache = new Dictionary<string, string>();
    
    // 🔷 ACTUALIZADO: Verificar bloqueos por ubicación específica para cada registro
    // 🔷 NUEVA LÓGICA: Crear opciones separadas para stock suelto y paletizado
    var resultado = new List<object>();
    
    foreach (var item in datos)
    {
        // Buscar stock paletizado para este artículo/ubicación
        var stockPaletizado = await _auroraSgaContext.PaletLineas
            .Where(pl => pl.CodigoEmpresa == item.CodigoEmpresa &&
                        pl.CodigoArticulo == item.CodigoArticulo &&
                        pl.CodigoAlmacen == item.CodigoAlmacen &&
                        pl.Ubicacion == item.Ubicacion &&
                        pl.Lote == item.Partida)
            .Include(pl => pl.Palet)
            .Where(pl => pl.Palet.Estado.ToUpper() == "ABIERTO" || pl.Palet.Estado.ToUpper() == "CERRADO")
            .Select(pl => new
            {
                PaletId = pl.PaletId,
                CodigoPalet = pl.Palet.Codigo,
                EstadoPalet = pl.Palet.Estado,
                CantidadEnPalet = pl.Cantidad,
                TraspasoId = pl.TraspasoId  // 🔷 NUEVO: Incluir TraspasoId de la línea
            })
            .ToListAsync();

        // 🔷 CORREGIDO: Asegurar que el cálculo se hace con precisión completa
        // Convertir explícitamente a decimal con precisión completa antes de la resta
        var totalPaletizado = stockPaletizado.Select(p => (decimal)p.CantidadEnPalet).Sum();
        var stockSuelto = (decimal)item.Disponible - totalPaletizado;

        // 🔷 ACTUALIZADO: Verificar bloqueo específico para esta ubicación
        var queryBloqueo = _auroraSgaContext.BloqueosCalidad
            .Where(b => b.CodigoEmpresa == codigoEmpresa &&
                       b.CodigoArticulo == item.CodigoArticulo &&
                       b.LotePartida == item.Partida &&
                       b.CodigoAlmacen == item.CodigoAlmacen &&
                       b.Bloqueado == true);

        // Filtrar por ubicación específica
        if (!string.IsNullOrWhiteSpace(item.Ubicacion))
        {
            queryBloqueo = queryBloqueo.Where(b => b.Ubicacion == item.Ubicacion);
        }
        else
        {
            queryBloqueo = queryBloqueo.Where(b => string.IsNullOrEmpty(b.Ubicacion));
        }

        var bloqueoEspecifico = await queryBloqueo
            .OrderByDescending(b => b.FechaBloqueo)
            .FirstOrDefaultAsync();

        object? bloqueoInfo = null;
        if (bloqueoEspecifico != null)
        {
            bloqueoInfo = new
            {
                isBloqueado = true,
                motivoBloqueo = bloqueoEspecifico.ComentarioBloqueo,
                fechaBloqueo = bloqueoEspecifico.FechaBloqueo,
                usuarioBloqueo = bloqueoEspecifico.UsuarioBloqueoId.ToString(),
                idBloqueo = bloqueoEspecifico.Id,
                tipoBloqueo = bloqueoEspecifico.TipoBloqueo ?? "TOTAL" // 🔷 NUEVO
            };
        }

        // 🔷 NUEVO: Obtener fecha del último traspaso para stock suelto
        DateTime? fechaUltimoTraspasoSuelto = null;
        if (stockSuelto > 0)
        {
            var ultimoTraspasoSuelto = await _auroraSgaContext.Traspasos
                .Where(t => t.TipoTraspaso == "ARTICULO" &&
                           t.CodigoEstado == "COMPLETADO" &&
                           t.FechaFinalizacion != null &&
                           t.CodigoArticulo == item.CodigoArticulo &&
                           t.CodigoEmpresa == item.CodigoEmpresa &&
                           t.AlmacenDestino == item.CodigoAlmacen &&
                           (string.IsNullOrWhiteSpace(item.Ubicacion) ? string.IsNullOrWhiteSpace(t.UbicacionDestino) : t.UbicacionDestino == item.Ubicacion) &&
                           (string.IsNullOrWhiteSpace(item.Partida) ? string.IsNullOrWhiteSpace(t.Partida) : t.Partida == item.Partida))
                .OrderByDescending(t => t.FechaFinalizacion)
                .Select(t => t.FechaFinalizacion)
                .FirstOrDefaultAsync();
            
            fechaUltimoTraspasoSuelto = ultimoTraspasoSuelto;
        }

        // 🔷 NUEVO: Obtener alérgenos del artículo (con caché para evitar consultas duplicadas)
        if (!alergenosCache.TryGetValue(item.CodigoArticulo, out var alergenos))
        {
            var articuloInfo = await _sageContext.VisArticulos
                .AsNoTracking()
                .Where(v => v.CodigoEmpresa == codigoEmpresa && v.CodigoArticulo == item.CodigoArticulo)
                .Select(v => v.VNEWAlergenos)
                .FirstOrDefaultAsync();
            
            alergenos = articuloInfo ?? string.Empty;
            alergenosCache[item.CodigoArticulo] = alergenos;
        }

        // 🔷 NUEVO: Obtener validación de sincronización desde diccionario
        var keySincronizacion = (item.CodigoArticulo, item.Partida ?? "", item.CodigoAlmacen);
        var (tieneDesincronizacion, stockSage, stockStorageControl) = validacionesSincronizacion.GetValueOrDefault(
            keySincronizacion, 
            (false, 0m, 0m));

        // 🔷 Opción 1: Stock suelto (si hay)
        if (stockSuelto > 0)
        {
            resultado.Add(new
            {
                // Campos originales
                item.DescripcionArticulo,
                item.CodigoArticulo,
                item.CodigoAlmacen,
                item.Ubicacion,
                item.Partida,
                item.FechaCaducidad,
                item.UnidadSaldo,
                item.Reservado,
                Disponible = stockSuelto,
                
                // 🔷 NUEVOS CAMPOS
                TipoStock = "Suelto",
                PaletId = (Guid?)null,
                CodigoPalet = (string?)null,
                EstadoPalet = (string?)null,
                
                // 🔷 ACTUALIZADO: Información de bloqueo por calidad (verificado por ubicación específica)
                IsBloqueadoCalidad = bloqueoInfo != null,
                MotivoBloqueoCalidad = bloqueoInfo?.GetType().GetProperty("motivoBloqueo")?.GetValue(bloqueoInfo)?.ToString(),
                FechaBloqueoCalidad = bloqueoInfo?.GetType().GetProperty("fechaBloqueo")?.GetValue(bloqueoInfo) as DateTime?,
                TipoBloqueoCalidad = bloqueoInfo?.GetType().GetProperty("tipoBloqueo")?.GetValue(bloqueoInfo)?.ToString() ?? "TOTAL", // 🔷 NUEVO
                
                // 🔷 NUEVO: Fecha del último traspaso
                FechaUltimoTraspaso = fechaUltimoTraspasoSuelto,
                
                // 🔷 NUEVO: Alérgenos del artículo (consultado individualmente con caché)
                Alergenos = alergenos,
                
                // 🔷 NUEVO: Información de desincronización de stock
                TieneDesincronizacion = tieneDesincronizacion ? true : (bool?)null,
                StockSage = tieneDesincronizacion ? stockSage : (decimal?)null,
                StockStorageControl = tieneDesincronizacion ? stockStorageControl : (decimal?)null
            });
        }

        // 🔷 Opción 2: Stock paletizado (por cada palet)
        foreach (var palet in stockPaletizado)
        {
            // 🔷 CORREGIDO: Buscar siempre el traspaso MÁS RECIENTE para este palet + artículo + partida
            // No usar solo el TraspasoId de la línea porque puede ser un traspaso antiguo
            // Hay que buscar TODOS los traspasos y tomar el más reciente
            var fechaUltimoTraspasoPalet = await _auroraSgaContext.Traspasos
                .Where(t => t.TipoTraspaso == "PALET" &&
                           t.FechaFinalizacion != null &&
                           t.CodigoPalet == palet.CodigoPalet &&
                           t.CodigoArticulo == item.CodigoArticulo &&
                           (string.IsNullOrWhiteSpace(item.Partida) ? string.IsNullOrWhiteSpace(t.Partida) : t.Partida == item.Partida))
                .OrderByDescending(t => t.FechaFinalizacion)
                .Select(t => t.FechaFinalizacion)
                .FirstOrDefaultAsync();

            resultado.Add(new
            {
                // Campos originales
                item.DescripcionArticulo,
                item.CodigoArticulo,
                item.CodigoAlmacen,
                item.Ubicacion,
                item.Partida,
                item.FechaCaducidad,
                item.UnidadSaldo,
                item.Reservado,
                Disponible = palet.CantidadEnPalet,
                
                // 🔷 NUEVOS CAMPOS
                TipoStock = "Paletizado",
                PaletId = palet.PaletId,
                CodigoPalet = palet.CodigoPalet,
                EstadoPalet = palet.EstadoPalet,
                
                // 🔷 ACTUALIZADO: Información de bloqueo por calidad (verificado por ubicación específica)
                IsBloqueadoCalidad = bloqueoInfo != null,
                MotivoBloqueoCalidad = bloqueoInfo?.GetType().GetProperty("motivoBloqueo")?.GetValue(bloqueoInfo)?.ToString(),
                FechaBloqueoCalidad = bloqueoInfo?.GetType().GetProperty("fechaBloqueo")?.GetValue(bloqueoInfo) as DateTime?,
                TipoBloqueoCalidad = bloqueoInfo?.GetType().GetProperty("tipoBloqueo")?.GetValue(bloqueoInfo)?.ToString() ?? "TOTAL", // 🔷 NUEVO
                
                // 🔷 NUEVO: Fecha del último traspaso
                FechaUltimoTraspaso = fechaUltimoTraspasoPalet,
                
                // 🔷 NUEVO: Alérgenos del artículo (consultado individualmente con caché)
                Alergenos = alergenos,
                
                // 🔷 NUEVO: Información de desincronización de stock
                TieneDesincronizacion = tieneDesincronizacion ? true : (bool?)null,
                StockSage = tieneDesincronizacion ? stockSage : (decimal?)null,
                StockStorageControl = tieneDesincronizacion ? stockStorageControl : (decimal?)null
            });
			}
		}

		// Registrar evento de consulta
		var detalleConsulta = $"Empresa={codigoEmpresa}";
		if (!string.IsNullOrWhiteSpace(codigoArticulo)) detalleConsulta += $", Articulo={codigoArticulo}";
		if (!string.IsNullOrWhiteSpace(descripcion)) detalleConsulta += $", Descripcion={descripcion}";
		if (!string.IsNullOrWhiteSpace(partida)) detalleConsulta += $", Partida={partida}";
		if (!string.IsNullOrWhiteSpace(codigoAlmacen)) detalleConsulta += $", Almacen={codigoAlmacen}";
		if (tieneParametroUbicacion)
		{
			if (string.IsNullOrWhiteSpace(codigoUbicacion))
				detalleConsulta += ", Ubicacion=(sin ubicar)";
			else
				detalleConsulta += $", Ubicacion={codigoUbicacion}";
		}
		detalleConsulta += $", Resultados={resultado.Count}";
		
		RegistrarEventoConsultaStockAsync(
			"StockController/PorArticuloDisponible",
			"Consulta de stock disponible",
			detalleConsulta);

		return Ok(resultado);
	}


		/// <summary>
		/// GET api/Stock/precio-medio
		/// Obtiene el precio medio de un artículo desde la tabla AcumuladoStock en Sage
		/// </summary>
		[HttpGet("precio-medio")]
		public async Task<IActionResult> ObtenerPrecioMedio(
			[FromQuery] int codigoEmpresa,
			[FromQuery] string codigoArticulo,
			[FromQuery] string? codigoAlmacen = null)
		{
			try
			{
				// Obtener el ejercicio actual
				var ejercicio = await _sageContext.Periodos
					.Where(p => p.CodigoEmpresa == codigoEmpresa && p.Fechainicio <= DateTime.Now)
					.OrderByDescending(p => p.Fechainicio)
					.Select(p => p.Ejercicio)
					.FirstOrDefaultAsync();

				if (ejercicio == 0)
					return BadRequest("No se encontró ejercicio válido");

				// Buscar en AcumuladoStock
				var query = _sageContext.AcumuladoStock
					.Where(a => a.CodigoEmpresa == codigoEmpresa &&
								a.Ejercicio == ejercicio &&
								a.CodigoArticulo == codigoArticulo);

				// Si se especifica almacén, filtrar por él
				if (!string.IsNullOrWhiteSpace(codigoAlmacen))
					query = query.Where(a => a.CodigoAlmacen == codigoAlmacen);

				var acumuladoStock = await query.FirstOrDefaultAsync();

				if (acumuladoStock?.PrecioMedio != null)
				{
					return Ok(acumuladoStock.PrecioMedio);
				}

				// Si no se encuentra precio medio, devolver 0
				return Ok(0m);
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Error obteniendo precio medio: {ex.Message}");
			}
		}


		private async Task<List<string>> ObtenerCodigosArticulosPorDescripcion(string descripcion, short codigoEmpresa)
		{
			var articulos = await _sageContext.Articulos
				.Where(a => a.CodigoEmpresa == codigoEmpresa && a.DescripcionArticulo.Contains(descripcion))
				.Select(a => a.CodigoArticulo)
				.ToListAsync();

			return articulos;
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
							var logger = scope.ServiceProvider.GetRequiredService<ILogger<StockController>>();

							var dispositivo = await dbContext.Dispositivos
								.FirstOrDefaultAsync(d => d.SessionToken == tokenCapturado && d.Activo == -1);

							if (dispositivo == null)
							{
								logger.LogWarning("⚠️ Dispositivo no encontrado para token al registrar evento: {TipoConsulta}", tipoConsultaCapturado);
								return; // Dispositivo no encontrado, no registramos evento
							}

							// 🔷 DEDUPLICACIÓN: Verificar si ya existe una consulta de stock del mismo usuario/dispositivo/artículo en los últimos 5 segundos
							// Esto evita múltiples registros cuando la PDA hace varias llamadas API casi simultáneamente
							var fechaLimite = DateTime.Now.AddSeconds(-5);
							var articuloActual = ExtraerArticuloDelDetalle(detalleCapturado);
							
							var eventoReciente = await dbContext.LogEventos
								.Where(e => e.IdUsuario == dispositivo.IdUsuario &&
										   e.IdDispositivo == dispositivo.Id &&
										   e.Tipo == "CONSULTA_STOCK" &&
										   e.Fecha >= fechaLimite)
								.OrderByDescending(e => e.Fecha)
								.FirstOrDefaultAsync();

							if (eventoReciente != null)
							{
								// Extraer artículo del detalle para comparar si es la misma consulta
								var articuloReciente = ExtraerArticuloDelDetalle(eventoReciente.Detalle);
								
								// Si es el mismo artículo (o ambos están vacíos), es probablemente la misma acción del usuario
								if (articuloActual == articuloReciente || (string.IsNullOrEmpty(articuloActual) && string.IsNullOrEmpty(articuloReciente)))
								{
									logger.LogInformation("⏭️ Evento duplicado detectado y omitido: {TipoConsulta}, Usuario: {UsuarioId}, Dispositivo: {DispositivoId}, Último evento: {FechaUltimo} ({OrigenUltimo})", 
										tipoConsultaCapturado, dispositivo.IdUsuario, dispositivo.Id, eventoReciente.Fecha, eventoReciente.Origen);
									return; // Ya existe un evento similar reciente, no registrar duplicado
								}
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
								var logger = scope.ServiceProvider.GetRequiredService<ILogger<StockController>>();
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

		// Helper para obtener almacenes autorizados (individuales + centro logístico)
		private async Task<List<string>> ObtenerAlmacenesAutorizadosAsync(int operarioId, int codigoEmpresa)
		{
			try
			{
				// 1. Obtener almacenes individuales del operario
				var almacenesIndividuales = await _sageContext.OperariosAlmacenes
					.Where(a => a.Operario == operarioId && a.CodigoEmpresa == codigoEmpresa)
					.Select(a => a.CodigoAlmacen!)
					.Where(a => a != null) // Filtrar nulls
					.ToListAsync();

				// 2. Obtener el centro logístico del operario
				var operario = await _sageContext.Operarios
					.Where(o => o.Id == operarioId)
					.Select(o => o.CodigoCentro)
					.FirstOrDefaultAsync();

				var todosLosAlmacenes = new List<string>(almacenesIndividuales);

				// 3. Si el operario tiene centro logístico, obtener sus almacenes
				if (!string.IsNullOrEmpty(operario))
				{
					var almacenesCentro = await _sageContext.Almacenes
						.Where(a => a.CodigoCentro == operario && a.CodigoEmpresa == codigoEmpresa)
						.Select(a => a.CodigoAlmacen!)
						.Where(a => a != null)
						.ToListAsync();

					todosLosAlmacenes.AddRange(almacenesCentro);
				}

				// 4. Eliminar duplicados y devolver
				return todosLosAlmacenes.Distinct().ToList();
			}
			catch (Exception ex)
			{
				// En caso de error, devolver lista vacía para no bloquear la consulta
				Console.WriteLine($"ERROR en ObtenerAlmacenesAutorizadosAsync: {ex.Message}");
				return new List<string>();
			}
		}

		/// <summary>
		/// Clase auxiliar para mapear resultados de consultas SQL de stock
		/// </summary>
		private class StockBatchResult
		{
			public string CodigoArticulo { get; set; } = string.Empty;
			public string Partida { get; set; } = string.Empty;
			public string CodigoAlmacen { get; set; } = string.Empty;
			public decimal Stock { get; set; }
		}

		/// <summary>
		/// Valida sincronización de stock en batch para múltiples artículos (optimizado)
		/// Retorna diccionario con (CodigoArticulo, Partida, CodigoAlmacen) -> (TieneDesincronizacion, StockSage, StockStorageControl)
		/// </summary>
		private async Task<Dictionary<(string CodigoArticulo, string Partida, string CodigoAlmacen), (bool TieneDesincronizacion, decimal StockSage, decimal StockStorageControl)>> 
			ValidarSincronizacionStockBatchAsync(
				short codigoEmpresa,
				List<(string CodigoArticulo, string Partida, string CodigoAlmacen)> articulos)
		{
			var resultado = new Dictionary<(string, string, string), (bool, decimal, decimal)>();
			
			if (!articulos.Any())
				return resultado;

			try
			{
				// Obtener el ejercicio actual (una sola vez)
				var ejercicio = await _sageContext.Periodos
					.Where(p => p.CodigoEmpresa == codigoEmpresa && p.Fechainicio <= DateTime.Now)
					.OrderByDescending(p => p.Fechainicio)
					.Select(p => p.Ejercicio)
					.FirstOrDefaultAsync();

				if (ejercicio == 0)
				{
					_logger.LogWarning("No se encontró ejercicio válido para validación batch de sincronización");
					// Retornar todos como null (no verificado)
					foreach (var art in articulos)
					{
						resultado[(art.CodigoArticulo, art.Partida ?? "", art.CodigoAlmacen)] = (false, 0m, 0m);
					}
					return resultado;
				}

				// Construir listas de valores únicos para filtrar
				var codigosArticulos = articulos.Select(a => a.CodigoArticulo).Distinct().ToList();
				var partidas = articulos.Select(a => a.Partida ?? "").Distinct().ToList();
				var codigosAlmacenes = articulos.Select(a => a.CodigoAlmacen).Distinct().ToList();

				// UNA consulta batch a SAGE - filtrar por listas y luego filtrar en memoria por combinaciones exactas
				var stocksSageRaw = await _sageContext.Database
					.SqlQueryRaw<StockBatchResult>(
						$@"SELECT CodigoArticulo, ISNULL(Partida, '') AS Partida, CodigoAlmacen, SUM(UnidadSaldo) AS Stock
                           FROM AURORA.dbo.AcumuladoStock 
                           WHERE Ejercicio = {ejercicio} 
                             AND Periodo = 99 
                             AND CodigoEmpresa = {codigoEmpresa}
                             AND CodigoArticulo IN ({string.Join(", ", codigosArticulos.Select((a, i) => $"'{a.Replace("'", "''")}'"))})
                             AND CodigoAlmacen IN ({string.Join(", ", codigosAlmacenes.Select((a, i) => $"'{a.Replace("'", "''")}'"))})
                           GROUP BY CodigoArticulo, ISNULL(Partida, ''), CodigoAlmacen")
					.ToListAsync();

				// Filtrar en memoria por combinaciones exactas de (CodigoArticulo, Partida, CodigoAlmacen)
				var combinacionesValidas = articulos.Select(a => (a.CodigoArticulo, a.Partida ?? "", a.CodigoAlmacen)).ToHashSet();
				var stocksSage = stocksSageRaw
					.Where(s => combinacionesValidas.Contains((s.CodigoArticulo, s.Partida ?? "", s.CodigoAlmacen)))
					.ToList();

				// UNA consulta batch a StorageControl - mismo enfoque
				var stocksStorageRaw = await _storageContext.Database
					.SqlQueryRaw<StockBatchResult>(
						$@"SELECT CodigoArticulo, ISNULL(Partida, '') AS Partida, CodigoAlmacen, SUM(UnidadSaldo) AS Stock
                           FROM StorageControl.dbo.AcumuladoStockUbicacion 
                           WHERE Ejercicio = {ejercicio} 
                             AND CodigoEmpresa = {codigoEmpresa}
                             AND CodigoArticulo IN ({string.Join(", ", codigosArticulos.Select((a, i) => $"'{a.Replace("'", "''")}'"))})
                             AND CodigoAlmacen IN ({string.Join(", ", codigosAlmacenes.Select((a, i) => $"'{a.Replace("'", "''")}'"))})
                           GROUP BY CodigoArticulo, ISNULL(Partida, ''), CodigoAlmacen")
					.ToListAsync();

				// Filtrar en memoria por combinaciones exactas
				var stocksStorage = stocksStorageRaw
					.Where(s => combinacionesValidas.Contains((s.CodigoArticulo, s.Partida ?? "", s.CodigoAlmacen)))
					.ToList();

				// Crear diccionarios para lookup rápido
				var dictSage = stocksSage.ToDictionary(
					s => (s.CodigoArticulo, s.Partida ?? "", s.CodigoAlmacen), 
					s => s.Stock);
				var dictStorage = stocksStorage.ToDictionary(
					s => (s.CodigoArticulo, s.Partida ?? "", s.CodigoAlmacen), 
					s => s.Stock);

				// Comparar y construir resultado
				foreach (var art in articulos)
				{
					var key = (art.CodigoArticulo, art.Partida ?? "", art.CodigoAlmacen);
					var stockSage = dictSage.GetValueOrDefault(key, 0m);
					var stockStorage = dictStorage.GetValueOrDefault(key, 0m);
					var tieneDesincronizacion = stockSage != stockStorage;
					
					if (tieneDesincronizacion)
					{
						_logger.LogWarning("⚠️ Desincronización detectada - Artículo: {CodigoArticulo}, Partida: {Partida}, Almacén: {CodigoAlmacen}, SAGE: {StockSage:N6}, StorageControl: {StockStorage:N6}",
							art.CodigoArticulo, art.Partida ?? "(sin partida)", art.CodigoAlmacen, stockSage, stockStorage);
					}
					
					resultado[key] = (tieneDesincronizacion, stockSage, stockStorage);
				}

				_logger.LogInformation("Validación batch de sincronización completada - {Count} artículos validados", articulos.Count);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error al validar sincronización de stock en batch - {Count} artículos", articulos.Count);
				// Si falla, retornar todos como no verificados (false)
				foreach (var art in articulos)
				{
					resultado[(art.CodigoArticulo, art.Partida ?? "", art.CodigoAlmacen)] = (false, 0m, 0m);
				}
			}

			return resultado;
		}

		// helper para proyectar
		private async Task<List<StockUbicacionDto>> ProjectToDtoConBloqueosAsync(List<AcumuladoStockUbicacion> datos, short codigoEmpresa)
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

			// 🔹 precargo todas las líneas de palet para esos artículos/lotes
			var codigosArticulos = datos.Select(d => d.CodigoArticulo).Distinct().ToList();
			var partidas = datos.Select(d => d.Partida).Distinct().ToList();
			var ubicaciones = datos.Select(d => d.Ubicacion).Distinct().ToList();

			// 🔷 NUEVO: Consultar alérgenos de cada artículo individualmente (con caché para evitar consultas duplicadas)
			var alergenosCache = new Dictionary<string, string>();

			var lineasPalets = _auroraSgaContext.PaletLineas
				.Include(l => l.Palet)
				.Where(l => l.CodigoEmpresa == empresa &&
							codigosArticulos.Contains(l.CodigoArticulo) &&
							partidas.Contains(l.Lote) &&
							ubicaciones.Contains(l.Ubicacion))
				.ToList();

			// 🔹 total global por artículo (en toda la empresa, independiente del filtro)
			var totalesGlobales = _auroraSgaContext.StockDisponible
				.Where(x => x.CodigoEmpresa == empresa &&
							codigosArticulos.Contains(x.CodigoArticulo) &&
							x.Partida != null)
				.AsEnumerable() // 🔑 ejecución en memoria
				.GroupBy(x => new { x.CodigoArticulo, x.Partida })
				.ToDictionary(
					g => (g.Key.CodigoArticulo, g.Key.Partida),
					g => g.Sum(x => x.UnidadSaldo)
				);

			// 🔹 total por artículo+almacén (usar StockDisponible entero, no datos filtrados)
			var totalesPorArticuloAlmacen = _auroraSgaContext.StockDisponible
				.Where(x => x.CodigoEmpresa == empresa &&
							codigosArticulos.Contains(x.CodigoArticulo) &&
							x.Partida != null)
				.AsEnumerable()
				.GroupBy(x => new { x.CodigoArticulo, x.Partida, x.CodigoAlmacen })
				.ToDictionary(
					g => (g.Key.CodigoArticulo, g.Key.Partida, g.Key.CodigoAlmacen),
					g => g.Sum(x => x.UnidadSaldo)
				);

			// 🔷 NUEVO: Validación batch de sincronización de stock
			// Agrupar artículos únicos por (CodigoArticulo, Partida, CodigoAlmacen)
			var articulosUnicos = datos
				.GroupBy(d => new { d.CodigoArticulo, d.Partida, d.CodigoAlmacen })
				.Select(g => (g.Key.CodigoArticulo, g.Key.Partida ?? "", g.Key.CodigoAlmacen))
				.Distinct()
				.ToList();

			// Validar todos en batch (una sola llamada)
			var validacionesSincronizacion = await ValidarSincronizacionStockBatchAsync(
				codigoEmpresa,
				articulosUnicos);

			var resultado = new List<StockUbicacionDto>();
			
			foreach (var s in datos)
			{
				var alm = almacenes.FirstOrDefault(x =>
					x.CodigoEmpresa == s.CodigoEmpresa &&
					x.CodigoAlmacen == s.CodigoAlmacen);
				
				// 🔷 CORREGIDO: Comparación normalizada para evitar problemas con espacios o mayúsculas/minúsculas
				var art = articulos.FirstOrDefault(x =>
					x.CodigoEmpresa == s.CodigoEmpresa &&
					string.Equals((x.CodigoArticulo ?? "").Trim(), (s.CodigoArticulo ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
				
				// 🔷 DEBUG: Log si no encuentra el artículo
				if (art == null)
				{
					_logger.LogWarning("⚠️ No se encontró artículo en Articulos: CodigoEmpresa={CodigoEmpresa}, CodigoArticulo='{CodigoArticulo}' (Length={Length})", 
						s.CodigoEmpresa, s.CodigoArticulo, s.CodigoArticulo?.Length ?? 0);
				}

				var palets = lineasPalets
					.Where(l =>
						l.CodigoEmpresa == s.CodigoEmpresa &&
						l.CodigoArticulo == s.CodigoArticulo &&
						l.Lote == s.Partida &&
						l.Ubicacion == s.Ubicacion &&
						l.CodigoAlmacen == s.CodigoAlmacen &&   // 👈 Filtro de almacén
						(l.Palet.Estado.ToUpper() == "ABIERTO" || l.Palet.Estado.ToUpper() == "CERRADO"))
					.Select(l => new PaletDetalleDto
					{
						PaletId = l.PaletId,
						CodigoPalet = l.Palet.Codigo,
						EstadoPalet = l.Palet.Estado,
						Cantidad = l.Cantidad,
						Ubicacion = l.Ubicacion,
						Partida = l.Lote,
						FechaApertura = l.Palet.FechaApertura,
						FechaCierre = l.Palet.FechaCierre
					})
					.ToList();

				// totales
				totalesGlobales.TryGetValue((s.CodigoArticulo, s.Partida), out var totalArticuloGlobal);
				totalesPorArticuloAlmacen.TryGetValue(
					(s.CodigoArticulo, s.Partida, s.CodigoAlmacen),
					out var totalArticuloAlmacen
				);

				// 🔷 ACTUALIZADO: Verificar bloqueo específico para esta ubicación
				var queryBloqueo = _auroraSgaContext.BloqueosCalidad
					.Where(b => b.CodigoEmpresa == codigoEmpresa &&
							   b.CodigoArticulo == s.CodigoArticulo &&
							   b.LotePartida == s.Partida &&
							   b.CodigoAlmacen == s.CodigoAlmacen &&
							   b.Bloqueado == true);

				// Filtrar por ubicación específica
				if (!string.IsNullOrWhiteSpace(s.Ubicacion))
				{
					queryBloqueo = queryBloqueo.Where(b => b.Ubicacion == s.Ubicacion);
				}
				else
				{
					queryBloqueo = queryBloqueo.Where(b => string.IsNullOrEmpty(b.Ubicacion));
				}

				var bloqueoEspecifico = await queryBloqueo
					.OrderByDescending(b => b.FechaBloqueo)
					.FirstOrDefaultAsync();

				// 🔷 NUEVO: Obtener alérgenos del artículo (con caché para evitar consultas duplicadas)
				if (!alergenosCache.TryGetValue(s.CodigoArticulo, out var alergenos))
				{
					var articuloInfo = await _sageContext.VisArticulos
						.AsNoTracking()
						.Where(v => v.CodigoEmpresa == empresa && v.CodigoArticulo == s.CodigoArticulo)
						.Select(v => v.VNEWAlergenos)
						.FirstOrDefaultAsync();
					
					alergenos = articuloInfo ?? string.Empty;
					alergenosCache[s.CodigoArticulo] = alergenos;
				}

				// 🔷 CORREGIDO: Obtener fecha del último traspaso
				// Si hay palets, usar TraspasoId de las líneas directamente
				DateTime? fechaUltimoTraspaso = null;
				
				if (palets != null && palets.Any())
				{
					// Obtener las líneas de palet que corresponden a este stock
					var lineasStock = lineasPalets
						.Where(l => l.CodigoEmpresa == s.CodigoEmpresa &&
									l.CodigoArticulo == s.CodigoArticulo &&
									l.Lote == s.Partida &&
									l.Ubicacion == s.Ubicacion &&
									l.CodigoAlmacen == s.CodigoAlmacen)
						.ToList();
					
					// 🔷 CORREGIDO: Buscar siempre el traspaso MÁS RECIENTE para este palet + artículo + partida
					// No usar solo los TraspasoId de las líneas porque pueden ser traspasos antiguos
					// Hay que buscar TODOS los traspasos de estos palets y tomar el más reciente
					var codigosPalet = palets.Select(p => p.CodigoPalet).Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
					var ultimoTraspasoPalet = await _auroraSgaContext.Traspasos
						.Where(t => t.TipoTraspaso == "PALET" &&
								   t.FechaFinalizacion != null &&
								   codigosPalet.Contains(t.CodigoPalet) &&
								   t.CodigoArticulo == s.CodigoArticulo &&
								   (string.IsNullOrWhiteSpace(s.Partida) ? string.IsNullOrWhiteSpace(t.Partida) : t.Partida == s.Partida))
						.OrderByDescending(t => t.FechaFinalizacion)
						.Select(t => t.FechaFinalizacion)
						.FirstOrDefaultAsync();
					
					fechaUltimoTraspaso = ultimoTraspasoPalet;
				}
				else
				{
					// Para stock suelto, buscar el último traspaso de artículo
					var ultimoTraspasoSuelto = await _auroraSgaContext.Traspasos
						.Where(t => t.TipoTraspaso == "ARTICULO" &&
								   t.CodigoEstado == "COMPLETADO" &&
								   t.FechaFinalizacion != null &&
								   t.CodigoArticulo == s.CodigoArticulo &&
								   t.CodigoEmpresa == s.CodigoEmpresa &&
								   t.AlmacenDestino == s.CodigoAlmacen &&
								   (string.IsNullOrWhiteSpace(s.Ubicacion) ? string.IsNullOrWhiteSpace(t.UbicacionDestino) : t.UbicacionDestino == s.Ubicacion) &&
								   (string.IsNullOrWhiteSpace(s.Partida) ? string.IsNullOrWhiteSpace(t.Partida) : t.Partida == s.Partida))
						.OrderByDescending(t => t.FechaFinalizacion)
						.Select(t => t.FechaFinalizacion)
						.FirstOrDefaultAsync();
					
					fechaUltimoTraspaso = ultimoTraspasoSuelto;
				}

				// 🔷 NUEVO: Obtener validación de sincronización desde diccionario ANTES de crear el DTO
				var keySincronizacion = (s.CodigoArticulo, s.Partida ?? "", s.CodigoAlmacen);
				var (tieneDesincronizacion, stockSage, stockStorageControl) = validacionesSincronizacion.GetValueOrDefault(
					keySincronizacion, 
					(false, 0m, 0m));
				
				// 🔷 DEBUG: Log si hay desincronización para este registro específico
				if (tieneDesincronizacion)
				{
					_logger.LogInformation("🔍 Asignando desincronización a DTO - Artículo: {CodigoArticulo}, Partida: {Partida}, Almacén: {CodigoAlmacen}, Ubicación: {Ubicacion}, SAGE: {StockSage:N6}, StorageControl: {StockStorageControl:N6}",
						s.CodigoArticulo, s.Partida ?? "(sin partida)", s.CodigoAlmacen, s.Ubicacion ?? "(sin ubicación)", stockSage, stockStorageControl);
				}

				resultado.Add(new StockUbicacionDto
				{
					CodigoEmpresa = s.CodigoEmpresa.ToString(),
					CodigoArticulo = s.CodigoArticulo,
					DescripcionArticulo = art?.DescripcionArticulo?.Trim() ?? string.Empty,
					CodigoAlternativo = art?.CodigoAlternativo ?? "",
					CodigoAlmacen = s.CodigoAlmacen,
					Almacen = alm?.Almacen ?? "",
					Ubicacion = s.Ubicacion,
					Partida = s.Partida,
					FechaCaducidad = s.FechaCaducidad,

					// cantidad individual
					UnidadSaldo = s.UnidadSaldo,

					Palets = palets,

					// 🔹 nuevos campos
					TotalArticuloGlobal = totalArticuloGlobal,          // total global
					TotalArticuloAlmacen = totalArticuloAlmacen,   // total en este almacén
					
					// 🔷 NUEVO: Información de bloqueo por calidad (verificado por ubicación específica)
					IsBloqueadoCalidad = bloqueoEspecifico != null,
					MotivoBloqueoCalidad = bloqueoEspecifico?.ComentarioBloqueo,
					FechaBloqueoCalidad = bloqueoEspecifico?.FechaBloqueo,
					TipoBloqueoCalidad = bloqueoEspecifico?.TipoBloqueo ?? "TOTAL", // 🔷 NUEVO
					
					// 🔷 NUEVO: Fecha del último traspaso
					FechaUltimoTraspaso = fechaUltimoTraspaso,
					
					// 🔷 NUEVO: Alérgenos del artículo (consultado individualmente con caché)
					Alergenos = alergenos,
					
					// 🔷 NUEVO: Información de desincronización de stock
					TieneDesincronizacion = tieneDesincronizacion ? true : (bool?)null,
					StockSage = tieneDesincronizacion ? stockSage : (decimal?)null,
					StockStorageControl = tieneDesincronizacion ? stockStorageControl : (decimal?)null
				});
			}
			
			return resultado;
		}

		/// <summary>
		/// Extrae el código de artículo del detalle de un evento para comparación de duplicados
		/// </summary>
		private string? ExtraerArticuloDelDetalle(string? detalle)
		{
			if (string.IsNullOrWhiteSpace(detalle))
				return null;

			// Buscar patrones como "Articulo=13286" o "CodigoArticulo=13286"
			var patrones = new[] { "Articulo=", "CodigoArticulo=" };
			foreach (var patron in patrones)
			{
				var indice = detalle.IndexOf(patron, StringComparison.OrdinalIgnoreCase);
				if (indice >= 0)
				{
					var inicio = indice + patron.Length;
					var fin = detalle.IndexOf(',', inicio);
					if (fin < 0) fin = detalle.Length;
					return detalle.Substring(inicio, fin - inicio).Trim();
				}
			}
			return null;
		}
	}

}
