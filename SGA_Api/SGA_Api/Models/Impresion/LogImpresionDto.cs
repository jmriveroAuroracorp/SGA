namespace SGA_Api.Models.Impresion
{
	public class LogImpresionDto
	{
		public string Usuario { get; set; }
		public string Dispositivo { get; set; }
		public int IdImpresora { get; set; }
		public int EtiquetaImpresa { get; set; }
		public int? Copias { get; set; } // Opcional, backend controla que por defecto sea 1
		public string CodigoArticulo { get; set; }
		public string DescripcionArticulo { get; set; }
		public string CodigoAlternativo { get; set; }
		public DateTime? FechaCaducidad { get; set; }
		public string Partida { get; set; }
		public string Alergenos { get; set; }
		public string PathEtiqueta { get; set; }
	}
}
