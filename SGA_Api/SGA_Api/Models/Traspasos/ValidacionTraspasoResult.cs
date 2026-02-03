namespace SGA_Api.Models.Traspasos
{
    /// <summary>
    /// Resultado de validación de traspaso
    /// </summary>
    public class ValidacionTraspasoResult
    {
        public bool EsValido { get; set; }
        public string MotivoBloqueo { get; set; } = string.Empty;
        public string CodigoArticulo { get; set; } = string.Empty;
        public string UbicacionDestino { get; set; } = string.Empty;
        
        // 🔷 NUEVO: Tipo de bloqueo para distinguir entre calidad y alérgenos
        public string TipoBloqueo { get; set; } = "CALIDAD"; // Por defecto "CALIDAD" para mantener compatibilidad
        
        public static ValidacionTraspasoResult Valido()
        {
            return new ValidacionTraspasoResult { EsValido = true };
        }
        
        public static ValidacionTraspasoResult Bloqueado(string motivo, string tipoBloqueo = "CALIDAD")
        {
            return new ValidacionTraspasoResult 
            { 
                EsValido = false, 
                MotivoBloqueo = motivo,
                TipoBloqueo = tipoBloqueo
            };
        }
    }
}
