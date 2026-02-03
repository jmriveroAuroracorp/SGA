using SGA_Api.Models.Palet;

namespace SGA_Api.Services
{
    public interface IValidacionAlergenosPaletService
    {
        /// <summary>
        /// Valida que el artículo a añadir tenga los mismos alérgenos que el primer artículo del palet
        /// </summary>
        /// <param name="paletId">ID del palet</param>
        /// <param name="codigoArticulo">Código del artículo a añadir</param>
        /// <param name="codigoEmpresa">Código de empresa</param>
        /// <returns>Resultado de la validación</returns>
        Task<ValidacionAlergenosPaletResult> ValidarAlergenosPaletAsync(
            Guid paletId,
            string codigoArticulo,
            short codigoEmpresa);
    }
}
