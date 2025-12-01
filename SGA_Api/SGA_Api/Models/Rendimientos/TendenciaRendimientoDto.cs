namespace SGA_Api.Models.Rendimientos
{
    public class TendenciaRendimientoDto
    {
        public string TipoMetrica { get; set; } = string.Empty; // "PRODUCTIVIDAD", "TIEMPO", "PRECISION"
        public List<PuntoTendenciaDto> Puntos { get; set; } = new List<PuntoTendenciaDto>();
    }
    
    public class PuntoTendenciaDto
    {
        public DateTime Fecha { get; set; }
        public string Periodo { get; set; } = string.Empty; // "2024-01", "2024-01-15", etc.
        public double Valor { get; set; }
        public string Unidad { get; set; } = string.Empty;
    }
}

