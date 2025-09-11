using System;

namespace SGA_Desktop.Models
{
    public class LecturaResponseDto
    {
        public Guid GuidID { get; set; }
        public Guid OrdenGuid { get; set; }
        public string CodigoAlmacen { get; set; } = string.Empty;
        public string? CodigoUbicacion { get; set; }
        public string? CodigoArticulo { get; set; }
        public string? DescripcionArticulo { get; set; }
        public string? LotePartida { get; set; }
        public decimal? CantidadContada { get; set; }
        public decimal? CantidadStock { get; set; }
        public string UsuarioCodigo { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string? Comentario { get; set; }
        
        // Campos calculados
        public decimal? Diferencia => CantidadContada.HasValue && CantidadStock.HasValue 
            ? CantidadContada.Value - CantidadStock.Value 
            : null;
        public bool TieneDiferencia => Diferencia.HasValue && Diferencia.Value != 0;

        // Propiedades adicionales para UI
        public string EstadoTexto
        {
            get
            {
                if (!CantidadContada.HasValue) return "Pendiente";
                if (!TieneDiferencia) return "Correcto";
                return Diferencia > 0 ? "Exceso" : "Faltante";
            }
        }

        public string DiferenciaFormateada
        {
            get
            {
                if (!Diferencia.HasValue) return "";
                if (Diferencia.Value == 0) return "Sin diferencia";
                return Diferencia.Value > 0 ? $"+{Diferencia.Value}" : Diferencia.Value.ToString();
            }
        }
    }
} 