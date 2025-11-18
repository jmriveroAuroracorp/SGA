using SGA_Api.Models.Traspasos;

namespace SGA_Api.Services
{
    public interface IValidacionTraspasoService
    {
        /// <summary>
        /// 🔷 NUEVO: Validar traspaso de artículo individual
        /// </summary>
        /// <param name="codigoArticulo">Código del artículo</param>
        /// <param name="almacenDestino">Almacén destino</param>
        /// <param name="ubicacionDestino">Ubicación destino</param>
        /// <param name="codigoEmpresa">Código de empresa</param>
        /// <param name="partida">Partida/Lote del artículo (opcional, para validar bloqueo específico)</param>
        /// <returns>Resultado de la validación</returns>
        Task<ValidacionTraspasoResult> ValidarTraspasoArticuloAsync(
            string codigoArticulo, 
            string almacenDestino,
            string ubicacionDestino,
            short codigoEmpresa,
            string? partida = null);
    }
}
