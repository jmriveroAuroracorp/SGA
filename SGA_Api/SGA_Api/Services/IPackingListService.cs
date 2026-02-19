using SGA_Api.Models.PackingList;

namespace SGA_Api.Services
{
    public interface IPackingListService
    {
        /// <summary>
        /// Obtiene el packing list para una orden de fabricación (OF).
        /// Combina datos de OrdenesFabricacion, CabeceraPedidoCliente y OrdenesTrabajo.
        /// </summary>
        /// <param name="codigoEmpresa">Código de empresa (por defecto 1 si no se indica).</param>
        /// <param name="ejercicio">Ejercicio de la OF.</param>
        /// <param name="serie">Serie de la OF (ej. "OR").</param>
        /// <param name="numero">Número de la OF.</param>
        Task<PackingListDto?> GetPackingListAsync(short codigoEmpresa, int ejercicio, string serie, int numero);
    }
}
