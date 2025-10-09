namespace SGA_Api.Models.RolesSga
{
    public class RolSga
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public int NivelJerarquico { get; set; }
        public bool Activo { get; set; } = true;
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public DateTime? FechaModificacion { get; set; }
        public int? UsuarioCreacion { get; set; }
        public int? UsuarioModificacion { get; set; }
    }
}
