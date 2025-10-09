namespace SGA_Api.Models.RolesSga
{
    public class RolSgaDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public int NivelJerarquico { get; set; }
        public bool Activo { get; set; } = true;
    }

    public class AsignarRolDto
    {
        public int OperarioId { get; set; }
        public int RolId { get; set; }
    }

    public class RolSugeridoDto
    {
        public int? RolId { get; set; }
        public string? RolNombre { get; set; }
        public string? Descripcion { get; set; }
        public string? Justificacion { get; set; }
    }
}
