namespace SGA_Api.Models.Conteos
{
    public class LecturaResponseDto
    {
        public Guid GuidID { get; set; }
        public Guid OrdenGuid { get; set; }
        public string CodigoAlmacen { get; set; } = string.Empty;
        public string? CodigoUbicacion { get; set; }
        public string? CodigoArticulo { get; set; }
        public string? DescripcionArticulo { get; set; }
        public string? LotePartida { get; set; }
        public decimal? CantidadContada { get; set; }
        public decimal? CantidadStock { get; set; }
        public string UsuarioCodigo { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string? Comentario { get; set; }
        
        // Campos calculados
        public decimal? Diferencia => CantidadContada.HasValue && CantidadStock.HasValue 
            ? CantidadContada.Value - CantidadStock.Value 
            : null;
        public bool TieneDiferencia => Diferencia.HasValue && Diferencia.Value != 0;
    }
} 