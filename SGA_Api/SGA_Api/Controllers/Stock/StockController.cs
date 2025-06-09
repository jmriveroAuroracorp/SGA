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

    }
}
