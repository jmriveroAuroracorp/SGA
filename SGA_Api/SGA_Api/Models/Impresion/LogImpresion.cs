namespace SGA_Api.Models.Impresion
{
	public class LogImpresion
	{
		public int Id { get; set; }
		public string Usuario { get; set; }
		public string Dispositivo { get; set; }
		public DateTime FechaRegistro { get; set; }
		public int IdImpresora { get; set; }
		public int EtiquetaImpresa { get; set; }
		public int Copias { get; set; }
		public string CodigoArticulo { get; set; }
		public string DescripcionArticulo { get; set; }
		public string CodigoAlternativo { get; set; }
		public DateTime? FechaCaducidad { get; set; }
		public string Partida { get; set; }
		public string Alergenos { get; set; }
		public string PathEtiqueta { get; set; }
		
		// 👇 NUEVO
		public int TipoEtiqueta { get; set; }
		public string CodigoGS1 { get; set; }
		public string CodigoPalet { get; set; }

		// Campos de ubicación
		public string? CodAlmacen { get; set; }
		public string? CodUbicacion { get; set; }
		public int? Altura { get; set; }
		public int? Estanteria { get; set; }
		public int? Pasillo { get; set; }
		public int? Posicion { get; set; }
	}
}