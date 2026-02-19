namespace SGA_Api.Models.PackingList
{
    /// <summary>
    /// Entidad que mapea la tabla OrdenesFabricacion de SAGE (AURORA).
    /// Clave: CodigoEmpresa, EjercicioFabricacion, SerieFabricacion, NumeroFabricacion.
    /// </summary>
    public class OrdenFabricacion
    {
        public short CodigoEmpresa { get; set; }
        public short EjercicioFabricacion { get; set; }
        public string SerieFabricacion { get; set; } = null!;
        public int NumeroFabricacion { get; set; }

        public string CodigoArticulo { get; set; } = null!;
        public short EjercicioPedido { get; set; }
        public string SeriePedido { get; set; } = null!;
        public int NumeroPedido { get; set; }
        public DateTime FechaRegistro { get; set; }
    }
}
