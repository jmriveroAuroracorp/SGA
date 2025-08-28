namespace SGA_Api.Models.Conteos
{
    public class ResultadoConteo
    {
        public long OrdenId { get; set; }
        public string CodigoAlmacen { get; set; } = string.Empty;
        public string? CodigoUbicacion { get; set; }
        public string? CodigoArticulo { get; set; }
        public string? DescripcionArticulo { get; set; }
        public string? LotePartida { get; set; }
        public decimal? CantidadContada { get; set; }
        public decimal? CantidadStock { get; set; }
        public string UsuarioCodigo { get; set; } = string.Empty;
        public decimal Diferencia { get; set; }
        public string AccionFinal { get; set; } = string.Empty;
        public string? AprobadoPorCodigo { get; set; }
        public DateTime FechaEvaluacion { get; set; }
        public bool AjusteAplicado { get; set; } = false;

        // Navigation property
        public OrdenConteo Orden { get; set; } = null!;
    }
} 