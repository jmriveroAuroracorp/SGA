namespace SGA_Api.Models.RolesSga
{
    public class RolSgaTable
    {
        public int IdRol { get; set; }
        public string CodigoRol { get; set; } = string.Empty;
        public string NombreRol { get; set; } = string.Empty;
        public int NivelJerarquico { get; set; }
    }
}
