namespace SGA_Api.Models.PackingList
{
    /// <summary>
    /// Entidad que mapea la tabla OrdenesTrabajo de SAGE (AURORA).
    /// Permite obtener CodigoAlmacen donde se crea el producto fabricado.
    /// Clave típica: CodigoEmpresa, EjercicioFabricacion, SerieFabricacion, NumeroFabricacion, EjercicioTrabajo, NumeroTrabajo.
    /// </summary>
    public class OrdenTrabajo
    {
        public short CodigoEmpresa { get; set; }
        public short EjercicioFabricacion { get; set; }
        public string SerieFabricacion { get; set; } = null!;
        public int NumeroFabricacion { get; set; }
        public short EjercicioTrabajo { get; set; }
        public int NumeroTrabajo { get; set; }

        public string CodigoArticulo { get; set; } = null!;
        public string CodigoAlmacen { get; set; } = null!;
    }
}
