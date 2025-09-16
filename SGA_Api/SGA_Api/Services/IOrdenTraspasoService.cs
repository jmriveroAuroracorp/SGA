using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.OrdenTraspaso;

namespace SGA_Api.Services
{
    public interface IOrdenTraspasoService
    {
        Task<IEnumerable<OrdenTraspasoDto>> GetOrdenesTraspasoAsync(short? codigoEmpresa = null, string? estado = null);
        Task<OrdenTraspasoDto?> GetOrdenTraspasoAsync(Guid id);
        Task<OrdenTraspasoDto> CrearOrdenTraspasoAsync(CrearOrdenTraspasoDto dto);
        Task<bool> ActualizarOrdenTraspasoAsync(Guid id, ActualizarOrdenTraspasoDto dto);
        Task<bool> ActualizarLineaOrdenTraspasoAsync(Guid id, ActualizarLineaOrdenTraspasoDto dto);
        Task<bool> CompletarOrdenTraspasoAsync(Guid id);
        Task<bool> CancelarOrdenTraspasoAsync(Guid id);
        Task<bool> EliminarOrdenTraspasoAsync(Guid id);
    }
}