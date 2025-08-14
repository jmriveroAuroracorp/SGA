namespace SGA_Api.Models.Impresion
{
	public class LogImpresionDto
	{
		public string Usuario { get; set; }
		public string Dispositivo { get; set; }
		public int IdImpresora { get; set; }
		public int EtiquetaImpresa { get; set; }
		public int? Copias { get; set; } // Opcional, backend controla que por defecto sea 1
		public string? CodigoArticulo { get; set; }
		public string? DescripcionArticulo { get; set; }
		public string? CodigoAlternativo { get; set; }
		public DateTime? FechaCaducidad { get; set; }
		public string? Partida { get; set; }
		public string? Alergenos { get; set; }
		public string PathEtiqueta { get; set; }

		public int TipoEtiqueta { get; set; }
		public string? CodigoGS1 { get; set; }
		public string? CodigoPalet { get; set; }

		// Campos de ubicación
		public string? CodAlmacen { get; set; }
		public string? CodUbicacion { get; set; }
		public int? Altura { get; set; }
		public int? Estanteria { get; set; }
		public int? Pasillo { get; set; }
		public int? Posicion { get; set; }
	}
}
