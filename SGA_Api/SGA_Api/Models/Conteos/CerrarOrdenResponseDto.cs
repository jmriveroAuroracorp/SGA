namespace SGA_Api.Models.Conteos
{
    public class CerrarOrdenResponseDto
    {
        public long OrdenId { get; set; }
        public int TotalLecturas { get; set; }
        public int ResultadosCreados { get; set; }
        public DateTime FechaCierre { get; set; }
    }
} 