namespace SGA_Api.Models.Registro
{
    public class LogEvento
    {
        public int Id { get; set; }
        public DateTime? Fecha { get; set; }
        public int? IdUsuario { get; set; }
        public string? Tipo { get; set; }
        public string? Origen { get; set; }
        public string? Descripcion { get; set; }
        public string? Detalle { get; set; }

        public string DispositivoId { get; set; } = null!; // Obligatorio

        public Dispositivo Dispositivo { get; set; } = null!; // Navegación obligatoria
    }

}
