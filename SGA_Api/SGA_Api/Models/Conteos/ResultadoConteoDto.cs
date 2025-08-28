namespace SGA_Api.Models.Conteos
{
    public class ResultadoConteoDto
    {
        public long OrdenId { get; set; }
        public decimal Diferencia { get; set; }
        public string AccionFinal { get; set; } = string.Empty;
        public string? AprobadoPorCodigo { get; set; }
        public DateTime FechaEvaluacion { get; set; }
        public bool AjusteAplicado { get; set; }
    }
} 