using System.Collections.Generic;
using SGA_Api.Models.Palet;

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
        public Guid? PaletId { get; set; }
        public decimal StockAlCrearInventario { get; set; }
        public decimal StockActual { get; set; }
        public decimal CantidadContada { get; set; }
        
        /// <summary>
        /// Información de los palets que contienen este artículo en esta ubicación
        /// </summary>
        public List<PaletDetalleDto> Palets { get; set; } = new();
        
        /// <summary>
        /// Stock total actual en la ubicación (suelto + paletizado)
        /// </summary>
        public decimal StockTotalActual { get; set; }
        
        /// <summary>
        /// Stock paletizado actual en la ubicación
        /// </summary>
        public decimal StockPaletizadoActual { get; set; }
    }
} 