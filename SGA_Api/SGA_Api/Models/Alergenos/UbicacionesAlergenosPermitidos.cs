namespace SGA_Api.Models.Alergenos
{
    public class UbicacionesAlergenosPermitidos
    {
        public short CodigoEmpresa { get; set; }
        public string CodigoAlmacen { get; set; } = "";
        public string Ubicacion { get; set; } = "";
        public short VCodigoAlergeno { get; set; }
        public string VDescripcionAlergeno { get; set; } = "";
    }
}
