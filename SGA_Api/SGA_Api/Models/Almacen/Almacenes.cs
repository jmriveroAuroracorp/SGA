using Microsoft.EntityFrameworkCore;

namespace SGA_Api.Models.Almacen
{
    [Keyless]
    public class Almacenes
    {
        public short? CodigoEmpresa { get; set; }
        public string? CodigoCentro { get; set; }
        public string? CodigoAlmacen { get; set; }
        public string? Almacen { get; set; }
    }
}
