namespace SGA_Desktop.Models
{
    public class MoverPaletDto
    {
        public Guid PaletId { get; set; }
        public string CodigoPalet { get; set; }
        public int UsuarioId { get; set; }
        public string AlmacenDestino { get; set; }
        public string UbicacionDestino { get; set; }
        public string CodigoEstado { get; set; }
    }
} 