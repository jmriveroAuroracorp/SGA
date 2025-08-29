using System;

namespace SGA_Desktop.Models
{
    public class ResultadoConteoDto
    {
        public long OrdenId { get; set; }
        public decimal Diferencia { get; set; }
        public string AccionFinal { get; set; } = string.Empty;
        public string? AprobadoPorCodigo { get; set; }
        public DateTime FechaEvaluacion { get; set; }
        public bool AjusteAplicado { get; set; }

        // Propiedades adicionales para UI
        public string AccionFormateada
        {
            get
            {
                return AccionFinal switch
                {
                    "SUPERVISION" => "Requiere Supervisión",
                    "APROBADO" => "Aprobado",
                    "RECHAZADO" => "Rechazado",
                    "AJUSTE_APLICADO" => "Ajuste Aplicado",
                    _ => AccionFinal
                };
            }
        }

        public string DiferenciaFormateada
        {
            get
            {
                if (Diferencia == 0) return "Sin diferencia";
                return Diferencia > 0 ? $"+{Diferencia}" : Diferencia.ToString();
            }
        }

        public string EstadoTexto => AjusteAplicado ? "Completado" : "Pendiente";
    }
} 