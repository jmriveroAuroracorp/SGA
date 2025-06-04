namespace SGA_Api.Models.Registro
{
    public class Dispositivo
    {
        public required string Id { get; set; }
        public string? Tipo { get; set; }
        public int? Activo { get; set; }
        public int? IdUsuario { get; set; }
        public string? Impresoras { get; set; }

        // Relación: Un dispositivo tiene muchos eventos
        public ICollection<LogEvento> LogEventos { get; set; } = new List<LogEvento>();
    }
}
