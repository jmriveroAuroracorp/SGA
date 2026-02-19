namespace SGA_Api.Models.PackingList
{
    /// <summary>
    /// DTO de respuesta del packing list para una orden de fabricación.
    /// Agrupa datos de OrdenesFabricacion, CabeceraPedidoCliente y OrdenesTrabajo (almacén).
    /// </summary>
    public class PackingListDto
    {
        // Orden de fabricación (OF)
        public short EjercicioFabricacion { get; set; }
        public string SerieFabricacion { get; set; } = null!;
        public int NumeroFabricacion { get; set; }
        public string OrdenFabricacionTexto => $"{EjercicioFabricacion}/{SerieFabricacion}/{NumeroFabricacion}";

        // Artículo fabricado
        public string CodigoArticulo { get; set; } = null!;

        // Pedido
        public short EjercicioPedido { get; set; }
        public string SeriePedido { get; set; } = null!;
        public int NumeroPedido { get; set; }
        public DateTime FechaRegistro { get; set; }

        // Cliente (desde CabeceraPedidoCliente)
        public string CodigoCliente { get; set; } = null!;
        public string RazonSocial { get; set; } = null!;

        // Almacén y trabajo donde se crea el producto (desde OrdenesTrabajo)
        public string CodigoAlmacen { get; set; } = null!;
        public short CodigoEmpresa { get; set; }
        public short EjercicioTrabajo { get; set; }
        public int NumeroTrabajo { get; set; }

        // Partidas desde Incidencias (puede ser una o más)
        public List<string> Partidas { get; set; } = new();
    }
}
