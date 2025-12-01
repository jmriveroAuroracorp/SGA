using SGA_Api.Models.Traspasos;

namespace SGA_Api.Services
{
    public interface IValidacionTraspasoService
    {
        /// <summary>
        /// 🔷 ACTUALIZADO: Validar traspaso de artículo individual verificando bloqueos en ubicación origen específica
        /// </summary>
        /// <param name="codigoArticulo">Código del artículo</param>
        /// <param name="almacenDestino">Almacén destino</param>
        /// <param name="ubicacionDestino">Ubicación destino</param>
        /// <param name="codigoEmpresa">Código de empresa</param>
        /// <param name="partida">Partida/Lote del artículo (opcional, para validar bloqueo específico)</param>
        /// <param name="almacenOrigen">Almacén origen (opcional, para verificar bloqueo en ubicación origen específica)</param>
        /// <param name="ubicacionOrigen">Ubicación origen (opcional, para verificar bloqueo en ubicación origen específica)</param>
        /// <returns>Resultado de la validación</returns>
        Task<ValidacionTraspasoResult> ValidarTraspasoArticuloAsync(
            string codigoArticulo, 
            string almacenDestino,
            string ubicacionDestino,
            short codigoEmpresa,
            string? partida = null,
            string? almacenOrigen = null,
            string? ubicacionOrigen = null);
    }
}
