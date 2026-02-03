using System;
using System.Text.Json.Serialization;

namespace SGA_Desktop.Models
{
    /// <summary>
    /// DTO para representar un ajuste de inventario
    /// </summary>
    public class AjusteDto
    {
        [JsonPropertyName("idAjuste")]
        public Guid IdAjuste { get; set; }

        [JsonPropertyName("idInventario")]
        public Guid? IdInventario { get; set; }

        [JsonPropertyName("codigoInventario")]
        public string? CodigoInventario { get; set; }

        [JsonPropertyName("codigoArticulo")]
        public string CodigoArticulo { get; set; } = string.Empty;

        [JsonPropertyName("descripcionArticulo")]
        public string? DescripcionArticulo { get; set; }

        [JsonPropertyName("codigoUbicacion")]
        public string CodigoUbicacion { get; set; } = string.Empty;

        [JsonPropertyName("codigoAlmacen")]
        public string CodigoAlmacen { get; set; } = string.Empty;

        [JsonPropertyName("diferencia")]
        public decimal Diferencia { get; set; }

        [JsonPropertyName("usuarioId")]
        public int UsuarioId { get; set; }

        [JsonPropertyName("usuarioNombre")]
        public string UsuarioNombre { get; set; } = string.Empty;

        [JsonPropertyName("fecha")]
        public DateTime Fecha { get; set; }

        [JsonPropertyName("estado")]
        public string Estado { get; set; } = "PENDIENTE_ERP";

        [JsonPropertyName("estadoErp")]
        public string? EstadoErp { get; set; }

        [JsonPropertyName("partida")]
        public string? Partida { get; set; }

        [JsonPropertyName("fechaCaducidad")]
        public DateTime? FechaCaducidad { get; set; }

        [JsonPropertyName("paletId")]
        public Guid? PaletId { get; set; }

        [JsonPropertyName("codigoPalet")]
        public string? CodigoPalet { get; set; }

        [JsonPropertyName("codigoGS1")]
        public string? CodigoGS1 { get; set; }

        [JsonPropertyName("codigoEmpresa")]
        public short CodigoEmpresa { get; set; }

        [JsonPropertyName("idConteo")]
        public Guid? IdConteo { get; set; }

        [JsonPropertyName("codigoConteo")]
        public string? CodigoConteo { get; set; }

        [JsonPropertyName("creadorConteoCodigo")]
        public string? CreadorConteoCodigo { get; set; }

        [JsonPropertyName("creadorConteoNombre")]
        public string? CreadorConteoNombre { get; set; }

        [JsonPropertyName("idCambioArticulo")]
        public Guid? IdCambioArticulo { get; set; }

        [JsonPropertyName("tipoCambioArticulo")]
        public string? TipoCambioArticulo { get; set; } // "CAMBIO_CODIGO" o "AMPLIACION"

        [JsonPropertyName("origen")]
        public string Origen { get; set; } = "";

        // Propiedades calculadas para UI
        public string DiferenciaFormateada => Diferencia >= 0 
            ? $"+{Diferencia:0.######}" 
            : Diferencia.ToString("0.######");

        public bool EsDiferenciaPositiva => Diferencia >= 0;

        // Propiedades calculadas adicionales para origen
        public string OrigenTexto => Origen switch
        {
            "INVENTARIO" => "Inventario",
            "CONTEO" => "Conteo",
            "CAMBIO_ARTICULO" => TipoCambioArticulo switch
            {
                "CAMBIO_CODIGO" => "Cambio de Código",
                "AMPLIACION" => "Ampliación",
                _ => "Cambio de Artículo"
            },
            _ => ""
        };

        public bool EsDeInventario => Origen == "INVENTARIO";
        public bool EsDeConteo => Origen == "CONTEO";
        public bool EsDeCambioArticulo => Origen == "CAMBIO_ARTICULO";

        // Propiedad calculada para mostrar el creador del conteo
        public string CreadorConteoDisplay
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(CreadorConteoNombre))
                    return CreadorConteoNombre;
                if (!string.IsNullOrWhiteSpace(CreadorConteoCodigo))
                    return CreadorConteoCodigo;
                return "N/A";
            }
        }
    }
}

