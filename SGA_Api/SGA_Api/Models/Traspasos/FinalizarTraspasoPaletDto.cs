namespace SGA_Api.Models.Traspasos
{
    public class FinalizarTraspasoPaletDto
    {
        public string AlmacenDestino { get; set; }
        public string UbicacionDestino { get; set; }
        public int UsuarioFinalizacionId { get; set; }
        public string CodigoEstado { get; set; }
    }
} 