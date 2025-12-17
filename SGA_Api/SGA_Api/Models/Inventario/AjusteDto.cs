using System;
using System.Text.Json.Serialization;

namespace SGA_Api.Models.Inventario
{
    /// <summary>
    /// DTO para representar un ajuste de inventario con información adicional
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

        // Propiedad calculada para el origen
        [JsonPropertyName("origen")]
        public string Origen
        {
            get
            {
                if (IdInventario.HasValue)
                    return "INVENTARIO";
                if (IdConteo.HasValue && IdConteo.Value != Guid.Empty)
                    return "CONTEO";
                return "";
            }
        }
    }
}

