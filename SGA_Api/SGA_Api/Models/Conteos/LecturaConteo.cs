namespace SGA_Api.Models.Conteos
{
    public class LecturaConteo
    {
        public long Id { get; set; }
        public long OrdenId { get; set; }
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

        // Navigation property
        public OrdenConteo Orden { get; set; } = null!;
    }
} 