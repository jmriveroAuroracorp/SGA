namespace SGA_Api.Models.Inventario
{
    public class LineaProblematicaDto
    {
        public string CodigoArticulo { get; set; } = string.Empty;
        public string DescripcionArticulo { get; set; } = string.Empty;
        public string CodigoAlmacen { get; set; } = string.Empty;
        public string CodigoUbicacion { get; set; } = string.Empty;
        public string Partida { get; set; } = string.Empty;
        public DateTime? FechaCaducidad { get; set; }
        public decimal StockAlCrearInventario { get; set; }
        public decimal StockActual { get; set; }
        public decimal CantidadContada { get; set; }
    }
} 