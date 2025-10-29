using System.Text.Json.Serialization;

namespace SGA_Desktop.Models
{
    /// <summary>
    /// Request para validar traspaso de artículo individual
    /// </summary>
    public class ValidacionTraspasoRequest
    {
        [JsonPropertyName("codigoArticulo")]
        public string CodigoArticulo { get; set; } = string.Empty;
        
        [JsonPropertyName("almacenDestino")]
        public string AlmacenDestino { get; set; } = string.Empty;
        
        [JsonPropertyName("ubicacionDestino")]
        public string UbicacionDestino { get; set; } = string.Empty;
        
        [JsonPropertyName("codigoEmpresa")]
        public short CodigoEmpresa { get; set; }
    }
}
