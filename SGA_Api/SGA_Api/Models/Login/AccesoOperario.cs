namespace SGA_Api.Models.Login
{
    public class AccesoOperario
    {
        public short CodigoEmpresa { get; set; }         // Código de la empresa
        public int Operario { get; set; }                // Clave foránea al operario
        public short MRH_CodigoAplicacion { get; set; }    // Código de la aplicación (ej. 7 para tu app actual)
    }

}
