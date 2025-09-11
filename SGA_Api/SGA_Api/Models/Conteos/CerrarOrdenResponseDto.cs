namespace SGA_Api.Models.Conteos
{
    public class CerrarOrdenResponseDto
    {
        public Guid OrdenGuid { get; set; }
        public int TotalLecturas { get; set; }
        public int ResultadosCreados { get; set; }
        public DateTime FechaCierre { get; set; }
    }
} 