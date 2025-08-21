using System;

namespace SGA_Desktop.Models
{
    public class ArticuloInventarioDto
    {
        public string CodigoArticulo { get; set; } = string.Empty;
        public string DescripcionArticulo { get; set; } = string.Empty;
        public string CodigoAlmacen { get; set; } = string.Empty;
        public string CodigoUbicacion { get; set; } = string.Empty;
        public string Partida { get; set; } = string.Empty;
        public DateTime? FechaCaducidad { get; set; }
        public string? Color { get; set; }
        public string? Talla { get; set; }
        public decimal StockActual { get; set; }
        public decimal? CantidadInventario { get; set; }
        public string? UsuarioConteo { get; set; }
        public DateTime? FechaConteo { get; set; }
    }
} 