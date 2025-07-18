namespace SGA_Api.Models.Traspasos
{
    public class CompletarTraspasoDto
    {
        public string CodigoAlmacenDestino { get; set; } = "";
        public string UbicacionDestino { get; set; } = "";
        public DateTime FechaFinalizacion { get; set; }
        public int UsuarioFinalizacionId { get; set; }
    }
} 