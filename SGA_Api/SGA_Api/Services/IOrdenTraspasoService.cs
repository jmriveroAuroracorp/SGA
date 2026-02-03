using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.OrdenTraspaso;

namespace SGA_Api.Services
{
    public interface IOrdenTraspasoService
    {
        Task<IEnumerable<OrdenTraspasoDto>> GetOrdenesTraspasoAsync(short? codigoEmpresa = null, string? estado = null);
        Task<IEnumerable<OrdenTraspasoDto>> GetOrdenesPorOperarioAsync(int idOperario, short codigoEmpresa);
        Task<OrdenTraspasoDto?> GetOrdenTraspasoAsync(Guid id);
        Task<OrdenTraspasoDto> CrearOrdenTraspasoAsync(CrearOrdenTraspasoDto dto);
        Task<bool> ActualizarOrdenTraspasoAsync(Guid id, ActualizarOrdenTraspasoDto dto);
        Task<OrdenTraspasoDto?> ActualizarLineaOrdenTraspasoAsync(Guid id, ActualizarLineaOrdenTraspasoDto dto);
        Task<LineaOrdenTraspasoDetalleDto?> CrearLineaOrdenTraspasoAsync(Guid idOrden, CrearLineaOrdenTraspasoDto dto);
        Task<bool> CompletarOrdenTraspasoAsync(Guid id);
        Task<bool> CancelarOrdenTraspasoAsync(Guid id);
        Task<bool> CancelarLineasPendientesAsync(Guid idOrden);
        Task<bool> EliminarOrdenTraspasoAsync(Guid id);
        Task<OrdenTraspasoDto?> IniciarOrdenAsync(Guid id, int idOperario);
        Task<IEnumerable<StockLineaTraspasoDto>?> GetStockLineaAsync(Guid idLinea);
        Task<OrdenTraspasoDto?> ActualizarEstadoLineaAsync(Guid idLinea, ActualizarEstadoLineaDto dto);
        Task<IEnumerable<PaletPendienteDto>> GetPaletsPendientesAsync(Guid ordenId);
        Task<UbicarPaletResponseDto> UbicarPaletAsync(Guid ordenId, string paletDestino, UbicarPaletDto dto);
        Task<IEnumerable<StockDisponibleDto>> GetStockDisponibleAsync(short codigoEmpresa, string codigoArticulo, int idOperario);
        Task<bool> DesbloquearLineaAsync(Guid idLinea);
        Task<AjusteLineaResponseDto?> AjustarLineaAsync(Guid idLinea, AjusteLineaOrdenTraspasoDto dto);
        Task<bool> ActualizarIdTraspasoAsync(Guid idLinea, ActualizarIdTraspasoDto dto);
        Task<OrdenTraspasoDto?> CancelarLineaAsync(Guid idLinea);
    }
}