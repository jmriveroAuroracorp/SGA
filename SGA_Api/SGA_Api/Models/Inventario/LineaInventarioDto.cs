using System;
using System.Collections.Generic;
using System.Linq;
using SGA_Api.Models.Palet;

namespace SGA_Api.Models.Inventario
{
    public class LineaInventarioDto
    {
        public string CodigoArticulo { get; set; } = string.Empty;
        public string DescripcionArticulo { get; set; } = string.Empty;
        public string CodigoUbicacion { get; set; } = string.Empty;
        public string Partida { get; set; } = string.Empty;
        public DateTime? FechaCaducidad { get; set; }
        public decimal StockActual { get; set; }
        public decimal StockContado { get; set; }
        public decimal StockTeorico { get; set; }
        public decimal? AjusteFinal { get; set; }
        public string Estado { get; set; } = string.Empty;
        
        // Propiedades para información de palets
        public List<PaletDetalleDto> Palets { get; set; } = new();
        
        public bool TienePalets => Palets?.Any() == true;
        public bool TieneMultiplesPalets => Palets?.Count > 1;
        
        public string PaletsResumen
        {
            get
            {
                if (Palets == null || !Palets.Any())
                    return "Sin palets";

                if (Palets.Count == 1)
                {
                    var palet = Palets.First();
                    // Mostrar el código del palet independientemente de su estado
                    return palet.CodigoPalet;
                }

                return "Múltiples palets";
            }
        }
    }
} 