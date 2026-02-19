using Microsoft.AspNetCore.Mvc;
using SGA_Api.Models.PackingList;
using SGA_Api.Services;

namespace SGA_Api.Controllers.PackingList
{
    /// <summary>
    /// Packing list para escritorio: datos de OF, cliente y almacén a partir de una orden de fabricación.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class PackingListController : ControllerBase
    {
        private readonly IPackingListService _packingListService;

        public PackingListController(IPackingListService packingListService)
        {
            _packingListService = packingListService;
        }

        /// <summary>
        /// GET /api/PackingList?codigoEmpresa=1&amp;ejercicio=2025&amp;serie=OR&amp;numero=186
        /// O por ruta: GET /api/PackingList/2025/OR/186 (codigoEmpresa por defecto 1)
        /// </summary>
        [HttpGet("{ejercicio}/{serie}/{numero}")]
        public async Task<ActionResult<PackingListDto>> GetByOrdenFabricacion(
            int ejercicio,
            string serie,
            int numero,
            [FromQuery] short codigoEmpresa = 1)
        {
            var result = await _packingListService.GetPackingListAsync(codigoEmpresa, ejercicio, serie, numero);
            if (result == null)
                return NotFound("No se encontró la orden de fabricación o no hay datos para el packing list.");
            return Ok(result);
        }
    }
}
