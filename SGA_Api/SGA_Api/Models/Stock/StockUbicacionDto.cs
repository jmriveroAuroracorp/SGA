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
    }

}
