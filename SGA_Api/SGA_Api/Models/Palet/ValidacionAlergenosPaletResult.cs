namespace SGA_Api.Models.Palet
{
    /// <summary>
    /// Resultado de validación de alérgenos en palet
    /// </summary>
    public class ValidacionAlergenosPaletResult
    {
        public bool EsValido { get; set; }
        public string MotivoBloqueo { get; set; } = string.Empty;
        public string CodigoArticulo { get; set; } = string.Empty;
        public string CodigoPalet { get; set; } = string.Empty;
        
        public static ValidacionAlergenosPaletResult Valido()
        {
            return new ValidacionAlergenosPaletResult { EsValido = true };
        }
        
        public static ValidacionAlergenosPaletResult Bloqueado(string motivo, string codigoArticulo = "", string codigoPalet = "")
        {
            return new ValidacionAlergenosPaletResult 
            { 
                EsValido = false, 
                MotivoBloqueo = motivo,
                CodigoArticulo = codigoArticulo,
                CodigoPalet = codigoPalet
            };
        }
    }
}
