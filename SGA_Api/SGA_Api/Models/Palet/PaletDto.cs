using System;

namespace SGA_Api.Models.Palet
{
	public class PaletDto
	{
		public Guid Id { get; set; }
		public short CodigoEmpresa { get; set; }
		public string Codigo { get; set; } = "";
		public string Estado { get; set; } = "";
		public string? TipoPaletCodigo { get; set; }
		public DateTime FechaApertura { get; set; }
		public DateTime? FechaCierre { get; set; }
		public int? UsuarioAperturaId { get; set; }
		public string? UsuarioAperturaNombre { get; set; }
		public int? UsuarioCierreId { get; set; }
		public string? UsuarioCierreNombre { get; set; }
		public decimal? Altura { get; set; }
		public decimal? Peso { get; set; }
		public bool EtiquetaGenerada { get; set; }
		public bool IsVaciado { get; set; }
		public DateTime? FechaVaciado { get; set; }
		public string? OrdenTrabajoId { get; set; } = "";

		public string? CodigoGS1 { get; set; }
		public string? CodigoPalet { get; set; }

		// 🔷 NUEVO: Indicadores de bloqueo por calidad
		public bool TieneArticulosBloqueadosCalidad { get; set; }
		public int CantidadArticulosBloqueados { get; set; }
		public string? MotivoBloqueoCalidad { get; set; }
		public DateTime? FechaBloqueoCalidad { get; set; }

		// 🔷 NUEVO: Información de última actividad
		public string? TipoUltimaActividad { get; set; } // "APERTURA", "CIERRE", "TRASPASO"
		public DateTime? FechaUltimaActividad { get; set; }
		public int? UsuarioUltimaActividadId { get; set; }
		public string? UsuarioUltimaActividadNombre { get; set; }
		public string? DescripcionUltimaActividad { get; set; } // Descripción detallada de la actividad

	}
}
