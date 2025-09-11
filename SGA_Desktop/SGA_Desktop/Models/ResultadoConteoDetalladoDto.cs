using System;
using System.Text.Json.Serialization;

namespace SGA_Desktop.Models
{
    public class ResultadoConteoDetalladoDto
    {
        // Campos de ResultadoConteo
        [JsonPropertyName("guidID")]
        public Guid GuidID { get; set; }
        
        [JsonPropertyName("ordenGuid")]
        public Guid OrdenGuid { get; set; }
        
        [JsonPropertyName("codigoAlmacen")]
        public string CodigoAlmacen { get; set; } = string.Empty;
        
        [JsonPropertyName("codigoUbicacion")]
        public string? CodigoUbicacion { get; set; }
        
        [JsonPropertyName("codigoArticulo")]
        public string? CodigoArticulo { get; set; }
        
        [JsonPropertyName("descripcionArticulo")]
        public string? DescripcionArticulo { get; set; }
        
        [JsonPropertyName("lotePartida")]
        public string? LotePartida { get; set; }
        
        [JsonPropertyName("cantidadContada")]
        public decimal? CantidadContada { get; set; }
        
        [JsonPropertyName("cantidadStock")]
        public decimal? CantidadStock { get; set; }
        
        [JsonPropertyName("usuarioCodigo")]
        public string? UsuarioCodigo { get; set; }
        
        [JsonPropertyName("diferencia")]
        public decimal Diferencia { get; set; }
        
        [JsonPropertyName("accionFinal")]
        public string AccionFinal { get; set; } = string.Empty;
        
        [JsonPropertyName("aprobadoPorCodigo")]
        public string? AprobadoPorCodigo { get; set; }
        
        [JsonPropertyName("fechaEvaluacion")]
        public DateTime FechaEvaluacion { get; set; }
        
        [JsonPropertyName("ajusteAplicado")]
        public bool AjusteAplicado { get; set; }

        // Campos adicionales de OrdenConteo
        [JsonPropertyName("codigoEmpresa")]
        public int CodigoEmpresa { get; set; }
        
        [JsonPropertyName("titulo")]
        public string Titulo { get; set; } = string.Empty;
        
        [JsonPropertyName("visibilidad")]
        public string Visibilidad { get; set; } = string.Empty;

        // Propiedades calculadas para UI
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
                return Diferencia > 0 ? $"+{Diferencia:N2}" : $"{Diferencia:N2}";
            }
        }

        public string EstadoTexto => AjusteAplicado ? "Completado" : "Pendiente";
        
        public string VisibilidadFormateada => Visibilidad switch
        {
            "VISIBLE" => "Conteo Visible",
            "CIEGO" => "Conteo Ciego",
            _ => Visibilidad
        };

        public bool RequiereAprobacion => AccionFinal == "SUPERVISION" && string.IsNullOrEmpty(AprobadoPorCodigo);
    }
} 