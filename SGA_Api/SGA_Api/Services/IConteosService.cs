using SGA_Api.Models.Conteos;

namespace SGA_Api.Services
{
    public interface IConteosService
    {
        Task<OrdenDto> CrearOrdenAsync(CrearOrdenDto dto);
        Task<OrdenDto?> ObtenerOrdenAsync(long id);
        Task<IEnumerable<OrdenDto>> ListarOrdenesAsync(string? codigoOperario = null, string? estado = null);
        Task<OrdenDto> IniciarOrdenAsync(long id, string codigoOperario);
        Task<OrdenDto> AsignarOperarioAsync(long id, AsignarOperarioDto dto);
        Task<LecturaResponseDto> CrearLecturaAsync(long ordenId, LecturaDto dto);
        Task<CerrarOrdenResponseDto> CerrarOrdenAsync(long id);
        Task<IEnumerable<LecturaResponseDto>> ObtenerLecturasPendientesAsync(long ordenId, string? codigoOperario = null);
    }
} 