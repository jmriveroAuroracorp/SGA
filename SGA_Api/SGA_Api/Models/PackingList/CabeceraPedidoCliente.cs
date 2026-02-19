namespace SGA_Api.Models.PackingList
{
    /// <summary>
    /// Entidad que mapea la tabla CabeceraPedidoCliente de SAGE (AURORA).
    /// Clave: CodigoEmpresa, EjercicioPedido, SeriePedido, NumeroPedido (mismo criterio que OrdenesFabricacion).
    /// </summary>
    public class CabeceraPedidoCliente
    {
        public short CodigoEmpresa { get; set; }
        public short EjercicioPedido { get; set; }
        public string SeriePedido { get; set; } = null!;
        public int NumeroPedido { get; set; }

        public string CodigoCliente { get; set; } = null!;   // varchar(15)
        public string RazonSocial { get; set; } = null!;     // varchar(40)
    }
}
