namespace SGA_Desktop.Models.Calidad
{
    public class BloqueoCalidadDto
    {
        public Guid Id { get; set; }
        public string CodigoArticulo { get; set; } = string.Empty;
        public string DescripcionArticulo { get; set; } = string.Empty;
        public string LotePartida { get; set; } = string.Empty;
        public string CodigoAlmacen { get; set; } = string.Empty;
        public string Almacen { get; set; } = string.Empty;
        public string? Ubicacion { get; set; }
        public bool Bloqueado { get; set; }
        public string UsuarioBloqueo { get; set; } = string.Empty;
        public DateTime FechaBloqueo { get; set; }
        public string ComentarioBloqueo { get; set; } = string.Empty;
        public string? UsuarioDesbloqueo { get; set; }
        public DateTime? FechaDesbloqueo { get; set; }
        public string? ComentarioDesbloqueo { get; set; }
    }
}
