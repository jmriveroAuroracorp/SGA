using Microsoft.AspNetCore.Mvc;
using SGA_Api.Models.Calidad;

namespace SGA_Api.Services
{
    public interface ICalidadService
    {
        /// <summary>
        /// Verifica si el usuario tiene permiso 16 (Calidad)
        /// </summary>
        /// <param name="usuarioId">ID del usuario</param>
        /// <returns>True si tiene permiso, false en caso contrario</returns>
        Task<bool> VerificarPermisoCalidadAsync(int usuarioId);

        /// <summary>
        /// Verifica si el usuario tiene acceso a la empresa especificada
        /// </summary>
        /// <param name="usuarioId">ID del usuario</param>
        /// <param name="codigoEmpresa">Código de la empresa</param>
        /// <returns>True si tiene acceso, false en caso contrario</returns>
        Task<bool> VerificarAccesoEmpresaAsync(int usuarioId, short codigoEmpresa);

        /// <summary>
        /// Busca stock por artículo y lote con filtros obligatorios
        /// </summary>
        /// <param name="codigoEmpresa">Código de empresa (obligatorio)</param>
        /// <param name="codigoArticulo">Código de artículo (obligatorio)</param>
        /// <param name="partida">Lote/partida (obligatorio)</param>
        /// <param name="codigoAlmacen">Código de almacén (opcional)</param>
        /// <param name="codigoUbicacion">Código de ubicación (opcional)</param>
        /// <returns>Lista de stock encontrado</returns>
        Task<List<StockCalidadDto>> BuscarStockPorArticuloYLoteAsync(
            short codigoEmpresa, 
            string codigoArticulo, 
            string partida, 
            string? codigoAlmacen = null, 
            string? codigoUbicacion = null);

        /// <summary>
        /// Bloquea stock específico
        /// </summary>
        /// <param name="dto">Datos del bloqueo</param>
        /// <returns>Resultado del bloqueo</returns>
        Task<object> BloquearStockAsync(BloquearStockDto dto);

        /// <summary>
        /// Verifica si el stock está bloqueado
        /// </summary>
        /// <param name="codigoEmpresa">Código de empresa</param>
        /// <param name="codigoArticulo">Código de artículo</param>
        /// <param name="lotePartida">Lote/partida</param>
        /// <returns>True si está bloqueado, false en caso contrario</returns>
        Task<bool> EstaStockBloqueadoAsync(short codigoEmpresa, string codigoArticulo, string lotePartida);

        /// <summary>
        /// Desbloquea stock específico
        /// </summary>
        /// <param name="dto">Datos del desbloqueo</param>
        /// <returns>Resultado del desbloqueo</returns>
        Task<object> DesbloquearStockAsync(DesbloquearStockDto dto);

        /// <summary>
        /// Obtiene lista de bloqueos actuales
        /// </summary>
        /// <param name="codigoEmpresa">Código de empresa</param>
        /// <param name="soloBloqueados">Si true, solo muestra bloqueos activos</param>
        /// <returns>Lista de bloqueos</returns>
        Task<List<BloqueoCalidadDto>> ObtenerBloqueosAsync(short codigoEmpresa, bool? soloBloqueados = null);
    }
}
