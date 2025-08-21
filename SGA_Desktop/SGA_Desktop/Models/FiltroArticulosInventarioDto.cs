namespace SGA_Desktop.Models
{
    public class FiltroArticulosInventarioDto
    {
        public Guid IdInventario { get; set; }
        public string? CodigoAlmacen { get; set; }
        public string? CodigoUbicacion { get; set; }
        public string? CodigoArticulo { get; set; }
    }
} 