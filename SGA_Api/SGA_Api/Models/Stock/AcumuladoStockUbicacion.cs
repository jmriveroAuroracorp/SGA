using Microsoft.EntityFrameworkCore;

namespace SGA_Api.Models.Stock
{
    [Keyless]
    public class AcumuladoStockUbicacion
    {
        public short CodigoEmpresa { get; set; }            // smallint
        public string? CodigoArticulo { get; set; }         // varchar
        public string? CodigoAlmacen { get; set; }          // varchar
        public string? Ubicacion { get; set; }              // varchar
        public string? Partida { get; set; }                // varchar
        //public string? CodigoCentro { get; set; }
        public DateTime? FechaCaducidad { get; set; }
        public decimal? UnidadSaldo { get; set; }
        public short Ejercicio { get; set; }                // smallint
    }
}
