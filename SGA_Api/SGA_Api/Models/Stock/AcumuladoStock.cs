using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.Stock
{
    public class AcumuladoStock
    {
        public short CodigoEmpresa { get; set; }
        public short Ejercicio { get; set; }
        public string CodigoArticulo { get; set; } = string.Empty;
        public string CodigoAlmacen { get; set; } = string.Empty;
        public decimal? UnidadEntrada { get; set; }
        public decimal? UnidadSalida { get; set; }
        public decimal? UnidadSaldo { get; set; }
        public decimal? PrecioMedio { get; set; }
        public decimal? ImporteEntrada { get; set; }
        public decimal? ImporteSalida { get; set; }
        public decimal? ImporteSaldo { get; set; }
    }
} 