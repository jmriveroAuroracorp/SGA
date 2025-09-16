namespace SGA_Api.Models.Conteos
{
	public class ResultadoConteoDetalladoDto
	{
		// Campos de ResultadoConteo
		public Guid GuidID { get; set; }
		public Guid OrdenGuid { get; set; }
		public string CodigoAlmacen { get; set; } = string.Empty;
		public string? CodigoUbicacion { get; set; }
		public string? CodigoArticulo { get; set; }
		public string? DescripcionArticulo { get; set; }
		public string? LotePartida { get; set; }
		public decimal? CantidadContada { get; set; }
		public decimal? CantidadStock { get; set; }
		public string? UsuarioCodigo { get; set; }
		public decimal Diferencia { get; set; }
		public string AccionFinal { get; set; } = string.Empty;
		public string? AprobadoPorCodigo { get; set; }
		public DateTime FechaEvaluacion { get; set; }
		public bool AjusteAplicado { get; set; }
		public DateTime? FechaCaducidad { get; set; }

		// Campos adicionales de OrdenConteo
		public int CodigoEmpresa { get; set; }
		public string Titulo { get; set; } = string.Empty;
		public string Visibilidad { get; set; } = string.Empty;
	}
}
