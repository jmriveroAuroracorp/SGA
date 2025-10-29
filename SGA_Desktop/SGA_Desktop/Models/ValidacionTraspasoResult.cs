using System.Text.Json.Serialization;

namespace SGA_Desktop.Models
{
    /// <summary>
    /// Resultado de validación de traspaso
    /// </summary>
    public class ValidacionTraspasoResult
    {
        [JsonPropertyName("esValido")]
        public bool EsValido { get; set; }
        
        [JsonPropertyName("motivoBloqueo")]
        public string MotivoBloqueo { get; set; } = string.Empty;
        
        [JsonPropertyName("codigoArticulo")]
        public string CodigoArticulo { get; set; } = string.Empty;
        
        [JsonPropertyName("ubicacionDestino")]
        public string UbicacionDestino { get; set; } = string.Empty;

        // 🔷 NUEVO: Métodos estáticos para crear instancias
        public static ValidacionTraspasoResult Valido() => new ValidacionTraspasoResult { EsValido = true };
        public static ValidacionTraspasoResult Bloqueado(string motivo) => new ValidacionTraspasoResult { EsValido = false, MotivoBloqueo = motivo };
    }
}
