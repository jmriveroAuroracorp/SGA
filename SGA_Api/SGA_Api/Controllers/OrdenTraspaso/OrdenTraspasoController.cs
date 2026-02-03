using Microsoft.AspNetCore.Mvc;
using SGA_Api.Models.OrdenTraspaso;
using SGA_Api.Services;

namespace SGA_Api.Controllers.OrdenTraspaso
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdenTraspasoController : ControllerBase
    {
        private readonly IOrdenTraspasoService _ordenTraspasoService;

        public OrdenTraspasoController(IOrdenTraspasoService ordenTraspasoService)
        {
            _ordenTraspasoService = ordenTraspasoService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<OrdenTraspasoDto>>> GetOrdenesTraspaso(
            [FromQuery] short? codigoEmpresa = null,
            [FromQuery] string? estado = null)
        {
            var ordenes = await _ordenTraspasoService.GetOrdenesTraspasoAsync(codigoEmpresa, estado);
            return Ok(ordenes);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<OrdenTraspasoDto>> GetOrdenTraspaso(Guid id)
        {
            var orden = await _ordenTraspasoService.GetOrdenTraspasoAsync(id);
            if (orden == null)
                return NotFound();

            return Ok(orden);
        }

        [HttpPost]
        public async Task<ActionResult<OrdenTraspasoDto>> CrearOrdenTraspaso(CrearOrdenTraspasoDto dto)
        {
            var orden = await _ordenTraspasoService.CrearOrdenTraspasoAsync(dto);
            return CreatedAtAction(nameof(GetOrdenTraspaso), new { id = orden.IdOrdenTraspaso }, orden);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> ActualizarOrdenTraspaso(Guid id, ActualizarOrdenTraspasoDto dto)
        {
            var result = await _ordenTraspasoService.ActualizarOrdenTraspasoAsync(id, dto);
            if (!result)
                return NotFound();

            return NoContent();
        }

        [HttpPut("linea/{id}")]
        public async Task<ActionResult<OrdenTraspasoDto>> ActualizarLineaOrdenTraspaso(Guid id, ActualizarLineaOrdenTraspasoDto dto)
        {
            var orden = await _ordenTraspasoService.ActualizarLineaOrdenTraspasoAsync(id, dto);
            if (orden == null)
                return NotFound();

            return Ok(orden);
        }

        [HttpPost("{idOrden}/linea")]
        public async Task<ActionResult<LineaOrdenTraspasoDetalleDto>> CrearLineaOrdenTraspaso(Guid idOrden, CrearLineaOrdenTraspasoDto dto)
        {
            var linea = await _ordenTraspasoService.CrearLineaOrdenTraspasoAsync(idOrden, dto);
            if (linea == null)
                return NotFound();

            return CreatedAtAction(nameof(GetOrdenTraspaso), new { id = idOrden }, linea);
        }

        [HttpPost("{id}/completar")]
        public async Task<IActionResult> CompletarOrdenTraspaso(Guid id)
        {
            var result = await _ordenTraspasoService.CompletarOrdenTraspasoAsync(id);
            if (!result)
                return NotFound();

            return NoContent();
        }

        [HttpPost("{id}/cancelar")]
        public async Task<IActionResult> CancelarOrdenTraspaso(Guid id)
        {
            var result = await _ordenTraspasoService.CancelarOrdenTraspasoAsync(id);
            if (!result)
                return BadRequest("No se puede cancelar la orden. Verifique que esté en estado PENDIENTE o SIN_ASIGNAR y sin movimientos realizados.");

            return Ok(new { mensaje = "Orden cancelada exitosamente" });
        }

        [HttpPost("{id}/cancelar-lineas-pendientes")]
        public async Task<IActionResult> CancelarLineasPendientes(Guid id)
        {
            var result = await _ordenTraspasoService.CancelarLineasPendientesAsync(id);
            if (!result)
                return BadRequest("No se pueden cancelar las líneas pendientes. Verifique que la orden esté en estado EN_PROCESO y tenga líneas pendientes.");

            return Ok(new { mensaje = "Líneas pendientes canceladas exitosamente" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> EliminarOrdenTraspaso(Guid id)
        {
            var result = await _ordenTraspasoService.EliminarOrdenTraspasoAsync(id);
            if (!result)
                return NotFound();

            return NoContent();
        }

        [HttpGet("operario/{idOperario}")]
        public async Task<ActionResult<IEnumerable<OrdenTraspasoDto>>> GetOrdenesPorOperario(
            int idOperario,
            [FromQuery] short codigoEmpresa)
        {
            var ordenes = await _ordenTraspasoService.GetOrdenesPorOperarioAsync(idOperario, codigoEmpresa);
            return Ok(ordenes);
        }

        [HttpPost("{id}/iniciar/{idOperario}")]
        public async Task<ActionResult<OrdenTraspasoDto>> IniciarOrden(Guid id, int idOperario)
        {
            var orden = await _ordenTraspasoService.IniciarOrdenAsync(id, idOperario);
            if (orden == null)
                return BadRequest("No se puede iniciar la orden. Verifique que esté asignada al operario y en estado PENDIENTE.");

            return Ok(orden);
        }

        [HttpGet("linea/{idLinea}/stock")]
        public async Task<ActionResult<IEnumerable<StockLineaTraspasoDto>>> GetStockLinea(Guid idLinea)
        {
            var stock = await _ordenTraspasoService.GetStockLineaAsync(idLinea);
            if (stock == null)
                return NotFound();

            return Ok(stock);
        }

        [HttpPut("linea/{idLinea}/estado")]
        public async Task<ActionResult<OrdenTraspasoDto>> ActualizarEstadoLinea(Guid idLinea, ActualizarEstadoLineaDto dto)
        {
            var orden = await _ordenTraspasoService.ActualizarEstadoLineaAsync(idLinea, dto);
            if (orden == null)
                return NotFound();

            return Ok(orden);
        }

        [HttpGet("{ordenId}/palets-pendientes")]
        public async Task<ActionResult<IEnumerable<PaletPendienteDto>>> GetPaletsPendientes(Guid ordenId)
        {
            var palets = await _ordenTraspasoService.GetPaletsPendientesAsync(ordenId);
            return Ok(palets);
        }

        [HttpPut("{ordenId}/palet/{paletDestino}/ubicar")]
        public async Task<IActionResult> UbicarPalet(Guid ordenId, string paletDestino, UbicarPaletDto dto)
        {
            var result = await _ordenTraspasoService.UbicarPaletAsync(ordenId, paletDestino, dto);
            return Ok(result);
        }

        [HttpGet("stock/{codigoEmpresa}/{codigoArticulo}/{idOperario}")]
        public async Task<ActionResult<IEnumerable<StockDisponibleDto>>> GetStockDisponible(
            short codigoEmpresa,
            string codigoArticulo,
            int idOperario)
        {
            var stock = await _ordenTraspasoService.GetStockDisponibleAsync(codigoEmpresa, codigoArticulo, idOperario);
            return Ok(stock);
        }

        [HttpPut("linea/{idLinea}/completa")]
        public async Task<ActionResult<OrdenTraspasoDto>> ActualizarLineaCompleta(Guid idLinea, ActualizarLineaOrdenTraspasoDto dto)
        {
            var orden = await _ordenTraspasoService.ActualizarLineaOrdenTraspasoAsync(idLinea, dto);
            if (orden == null)
                return NotFound();

            return Ok(orden);
        }

        [HttpPost("linea/{idLinea}/desbloquear")]
        public async Task<IActionResult> DesbloquearLinea(Guid idLinea)
        {
            var result = await _ordenTraspasoService.DesbloquearLineaAsync(idLinea);
            if (!result)
                return NotFound();

            return NoContent();
        }

        [HttpPost("linea/{idLinea}/ajuste")]
        public async Task<ActionResult<AjusteLineaResponseDto>> AjustarLineaOrdenTraspaso(Guid idLinea, AjusteLineaOrdenTraspasoDto dto)
        {
            var result = await _ordenTraspasoService.AjustarLineaAsync(idLinea, dto);
            if (result == null)
                return NotFound();

            return Ok(result);
        }

        [HttpPut("linea/{idLinea}/traspaso")]
        public async Task<IActionResult> ActualizarIdTraspaso(Guid idLinea, ActualizarIdTraspasoDto dto)
        {
            var result = await _ordenTraspasoService.ActualizarIdTraspasoAsync(idLinea, dto);
            if (!result)
                return NotFound();

            return NoContent();
        }

        [HttpPost("linea/{idLinea}/cancelar")]
        public async Task<ActionResult<OrdenTraspasoDto>> CancelarLineaOrdenTraspaso(Guid idLinea)
        {
            var orden = await _ordenTraspasoService.CancelarLineaAsync(idLinea);
            if (orden == null)
                return NotFound();

            return Ok(orden);
        }

    }
} 