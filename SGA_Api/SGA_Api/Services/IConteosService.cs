using SGA_Api.Models.Conteos;

namespace SGA_Api.Services
{
    public interface IConteosService
    {
        Task<OrdenDto> CrearOrdenAsync(CrearOrdenDto dto);
        Task<OrdenDto> ActualizarOrdenAsync(Guid guid, CrearOrdenConteoDto dto);
        Task<OrdenDto?> ObtenerOrdenAsync(Guid guid);
        Task<IEnumerable<OrdenDto>> ListarOrdenesAsync(string? codigoOperario = null, string? estado = null);
        Task<IEnumerable<OrdenDto>> ListarTodasLasOrdenesAsync(string? estado = null, string? codigoOperario = null, DateTime? fechaDesde = null, DateTime? fechaHasta = null, string? codigoOperarioSesion = null, string? creadoPorCodigo = null);
        Task<OrdenDto> IniciarOrdenAsync(Guid guid, string codigoOperario);
        Task<OrdenDto> AsignarOperarioAsync(Guid guid, AsignarOperarioDto dto);
        Task<LecturaResponseDto> CrearLecturaAsync(Guid ordenGuid, LecturaDto dto);
        Task<CerrarOrdenResponseDto> CerrarOrdenAsync(Guid guid);
        Task<IEnumerable<LecturaResponseDto>> ObtenerLecturasPendientesAsync(Guid ordenGuid, string? codigoOperario = null);
        Task<IEnumerable<LecturaResponseDto>> ObtenerLecturasRegistradasAsync(Guid ordenGuid, string? codigoOperario = null);
        Task<IEnumerable<ResultadoConteoDetalladoDto>> ObtenerResultadosConteoAsync(string? accion = null);
        Task<ResultadoConteoDetalladoDto> ActualizarAprobadorAsync(Guid resultadoGuid, ActualizarAprobadorDto dto);
        Task<OrdenDto> ReasignarLineaAsync(Guid resultadoGuid, ReasignarLineaDto dto);
        Task<List<PaletDisponibleInfo>> ObtenerPaletsDisponiblesAsync(string codigoAlmacen, string? ubicacion, string? codigoArticulo, string? lote, DateTime? fechaCaducidad);
        
        // Métodos para conteos periódicos
        Task<IEnumerable<ConteoPeriodicoDto>> ListarConteosPeriodicosAsync(
            string? codigoAlmacen = null,
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null,
            bool? activo = null,
            string? codigoOperario = null,
            string? codigoOperarioSesion = null,
            string? creadoPorCodigo = null);
        Task ActivarPeriodicidadAsync(Guid guid);
        Task DesactivarPeriodicidadAsync(Guid guid);
        Task<IEnumerable<OrdenDto>> ObtenerRenovacionesAsync(Guid guid);
        Task<IEnumerable<string>> ObtenerCreadoresConteosAsync();
        Task<IEnumerable<string>> ObtenerCreadoresConteosPeriodicosAsync();
        Task<OrdenDto> RenovarConteoPeriodicoAsync(Guid guid);
    }
} 