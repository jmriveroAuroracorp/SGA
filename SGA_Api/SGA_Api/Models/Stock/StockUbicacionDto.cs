using SGA_Api.Models.Palet;
using System.Text.Json.Serialization;

namespace SGA_Api.Models.Stock
{
    public class StockUbicacionDto
    {
        public string? CodigoEmpresa { get; set; }
        public string? CodigoArticulo { get; set; }
		public string? DescripcionArticulo { get; set; }
        public string? CodigoAlternativo { get; set; }
		public string? CodigoAlternativo2 { get; set; }
		public string? ReferenciaEdi_ { get; set; }
		public string? MRHCodigoAlternativo3 { get; set; }
		public string? VCodigoDUN14 { get; set; }
		public string? CodigoCentro { get; set; }
        public string? CodigoAlmacen { get; set; }
        public string? Almacen { get; set; }
        public string? Ubicacion { get; set; }
        public string? Partida { get; set; }
        public DateTime? FechaCaducidad { get; set; }
        public decimal?  UnidadSaldo { get; set; }

		// Campos nuevos para mostrar si los artículos están paletizados en la consulta de stock
		public Guid? PaletId { get; set; }
		public string? CodigoPalet { get; set; }
		public bool EstaPaletizado => !string.IsNullOrEmpty(CodigoPalet);
		public string? EstadoPalet { get; set; }
		public List<PaletDetalleDto> Palets { get; set; } = new();
		public decimal? TotalArticuloGlobal { get; set; }
		public decimal? TotalArticuloAlmacen { get; set; }
	}

}
