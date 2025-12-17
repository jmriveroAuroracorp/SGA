namespace SGA_Api.Models.Rendimientos
{
    public class VolumenMovidoDto
    {
        public decimal TotalUnidadesMovidas { get; set; }
        public decimal TotalValorEconomico { get; set; }
        public int TotalPaletsUnicos { get; set; }
        public decimal? TotalKilosMovidos { get; set; } // Opcional si hay datos de peso
        
        public DesgloseVolumenDto DesglosePorTipo { get; set; } = new DesgloseVolumenDto();
        public List<PuntoEvolucionDto> EvolucionTemporal { get; set; } = new List<PuntoEvolucionDto>();
        public List<TopArticuloVolumenDto> TopArticulos { get; set; } = new List<TopArticuloVolumenDto>();
        public ComparativaVolumenDto? ComparativaPeriodoAnterior { get; set; }
    }
    
    public class DesgloseVolumenDto
    {
        public decimal UnidadesPalet { get; set; }
        public decimal ValorPalet { get; set; }
        public int CantidadTraspasosPalet { get; set; }
        
        public decimal UnidadesArticulo { get; set; }
        public decimal ValorArticulo { get; set; }
        public int CantidadTraspasosArticulo { get; set; }
    }
    
    public class PuntoEvolucionDto
    {
        public DateTime Fecha { get; set; }
        public string Periodo { get; set; } = string.Empty; // "2024-01-15", "2024-S01" (semana), etc.
        public decimal Unidades { get; set; }
        public decimal UnidadesPalet { get; set; }
        public decimal UnidadesArticulo { get; set; }
        public decimal Valor { get; set; }
        public int Palets { get; set; }
    }
    
    public class TopArticuloVolumenDto
    {
        public string CodigoArticulo { get; set; } = string.Empty;
        public string? DescripcionArticulo { get; set; }
        public decimal UnidadesMovidas { get; set; }
        public double PorcentajeDelTotal { get; set; }
        public int Posicion { get; set; }
    }
    
    public class ComparativaVolumenDto
    {
        public double? VariacionUnidades { get; set; } // % de variación
        public double? VariacionValor { get; set; }
        public double? VariacionPalets { get; set; }
        public double? VariacionKilos { get; set; }
    }
}

