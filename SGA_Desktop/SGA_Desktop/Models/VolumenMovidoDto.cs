using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SGA_Desktop.Models
{
    public class VolumenMovidoDto
    {
        [JsonPropertyName("totalUnidadesMovidas")]
        public decimal TotalUnidadesMovidas { get; set; }
        
        [JsonPropertyName("totalValorEconomico")]
        public decimal TotalValorEconomico { get; set; }
        
        [JsonPropertyName("totalPaletsUnicos")]
        public int TotalPaletsUnicos { get; set; }
        
        [JsonPropertyName("totalKilosMovidos")]
        public decimal? TotalKilosMovidos { get; set; }
        
        [JsonPropertyName("desglosePorTipo")]
        public DesgloseVolumenDto DesglosePorTipo { get; set; } = new DesgloseVolumenDto();
        
        [JsonPropertyName("evolucionTemporal")]
        public List<PuntoEvolucionDto> EvolucionTemporal { get; set; } = new List<PuntoEvolucionDto>();
        
        [JsonPropertyName("topArticulos")]
        public List<TopArticuloVolumenDto> TopArticulos { get; set; } = new List<TopArticuloVolumenDto>();
        
        [JsonPropertyName("comparativaPeriodoAnterior")]
        public ComparativaVolumenDto? ComparativaPeriodoAnterior { get; set; }
    }
    
    public class DesgloseVolumenDto
    {
        [JsonPropertyName("unidadesPalet")]
        public decimal UnidadesPalet { get; set; }
        
        [JsonPropertyName("valorPalet")]
        public decimal ValorPalet { get; set; }
        
        [JsonPropertyName("cantidadTraspasosPalet")]
        public int CantidadTraspasosPalet { get; set; }
        
        [JsonPropertyName("unidadesArticulo")]
        public decimal UnidadesArticulo { get; set; }
        
        [JsonPropertyName("valorArticulo")]
        public decimal ValorArticulo { get; set; }
        
        [JsonPropertyName("cantidadTraspasosArticulo")]
        public int CantidadTraspasosArticulo { get; set; }
    }
    
    public class PuntoEvolucionDto
    {
        [JsonPropertyName("fecha")]
        public DateTime Fecha { get; set; }
        
        [JsonPropertyName("periodo")]
        public string Periodo { get; set; } = string.Empty;
        
        [JsonPropertyName("unidades")]
        public decimal Unidades { get; set; }
        
        [JsonPropertyName("unidadesPalet")]
        public decimal UnidadesPalet { get; set; }
        
        [JsonPropertyName("unidadesArticulo")]
        public decimal UnidadesArticulo { get; set; }
        
        [JsonPropertyName("valor")]
        public decimal Valor { get; set; }
        
        [JsonPropertyName("palets")]
        public int Palets { get; set; }
    }
    
    public class TopArticuloVolumenDto
    {
        [JsonPropertyName("codigoArticulo")]
        public string CodigoArticulo { get; set; } = string.Empty;
        
        [JsonPropertyName("descripcionArticulo")]
        public string? DescripcionArticulo { get; set; }
        
        [JsonPropertyName("unidadesMovidas")]
        public decimal UnidadesMovidas { get; set; }
        
        [JsonPropertyName("porcentajeDelTotal")]
        public double PorcentajeDelTotal { get; set; }
        
        [JsonPropertyName("posicion")]
        public int Posicion { get; set; }
    }
    
    public class ComparativaVolumenDto
    {
        [JsonPropertyName("variacionUnidades")]
        public double? VariacionUnidades { get; set; }
        
        [JsonPropertyName("variacionValor")]
        public double? VariacionValor { get; set; }
        
        [JsonPropertyName("variacionPalets")]
        public double? VariacionPalets { get; set; }
        
        [JsonPropertyName("variacionKilos")]
        public double? VariacionKilos { get; set; }
    }
}

