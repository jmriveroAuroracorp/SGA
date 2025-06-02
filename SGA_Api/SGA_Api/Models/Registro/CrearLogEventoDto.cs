namespace SGA_Api.Models.Registro
{
    public class CrearLogEventoDto
    {
        public DateTime? Fecha { get; set; }
        public int? IdUsuario { get; set; }
        public string? Tipo { get; set; }
        public string? Origen { get; set; }
        public string? Descripcion { get; set; }
        public string? Detalle { get; set; }
        public string IdDispositivo { get; set; } = null!;
    }
}
