using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SGA_Desktop.Models
{
    public class DistribucionDto
    {
        [JsonPropertyName("topAlmacenesOrigen")]
        public List<AlmacenDistribucionDto> TopAlmacenesOrigen { get; set; } = new List<AlmacenDistribucionDto>();
        
        [JsonPropertyName("topAlmacenesDestino")]
        public List<AlmacenDistribucionDto> TopAlmacenesDestino { get; set; } = new List<AlmacenDistribucionDto>();
        
        [JsonPropertyName("topUbicacionesOrigen")]
        public List<UbicacionDistribucionDto> TopUbicacionesOrigen { get; set; } = new List<UbicacionDistribucionDto>();
        
        [JsonPropertyName("topUbicacionesDestino")]
        public List<UbicacionDistribucionDto> TopUbicacionesDestino { get; set; } = new List<UbicacionDistribucionDto>();
        
        [JsonPropertyName("flujosPrincipales")]
        public List<FlujoDistribucionDto> FlujosPrincipales { get; set; } = new List<FlujoDistribucionDto>();
    }
    
    public class AlmacenDistribucionDto
    {
        [JsonPropertyName("codigoAlmacen")]
        public string CodigoAlmacen { get; set; } = string.Empty;
        
        [JsonPropertyName("unidadesMovidas")]
        public decimal UnidadesMovidas { get; set; }
        
        [JsonPropertyName("cantidadTraspasos")]
        public int CantidadTraspasos { get; set; }
        
        [JsonPropertyName("porcentajeDelTotal")]
        public double PorcentajeDelTotal { get; set; }
        
        [JsonPropertyName("porcentajePorTraspasos")]
        public double PorcentajePorTraspasos { get; set; }
        
        [JsonPropertyName("posicion")]
        public int Posicion { get; set; }
    }
    
    public class UbicacionDistribucionDto
    {
        [JsonPropertyName("codigoAlmacen")]
        public string CodigoAlmacen { get; set; } = string.Empty;
        
        [JsonPropertyName("ubicacion")]
        public string Ubicacion { get; set; } = string.Empty;
        
        [JsonPropertyName("unidadesMovidas")]
        public decimal UnidadesMovidas { get; set; }
        
        [JsonPropertyName("cantidadTraspasos")]
        public int CantidadTraspasos { get; set; }
        
        [JsonPropertyName("porcentajeDelTotal")]
        public double PorcentajeDelTotal { get; set; }
        
        [JsonPropertyName("porcentajePorTraspasos")]
        public double PorcentajePorTraspasos { get; set; }
        
        [JsonPropertyName("posicion")]
        public int Posicion { get; set; }
    }
    
    public class FlujoDistribucionDto
    {
        [JsonPropertyName("almacenOrigen")]
        public string AlmacenOrigen { get; set; } = string.Empty;
        
        [JsonPropertyName("ubicacionOrigen")]
        public string UbicacionOrigen { get; set; } = string.Empty;
        
        [JsonPropertyName("almacenDestino")]
        public string AlmacenDestino { get; set; } = string.Empty;
        
        [JsonPropertyName("ubicacionDestino")]
        public string UbicacionDestino { get; set; } = string.Empty;
        
        [JsonPropertyName("unidadesMovidas")]
        public decimal UnidadesMovidas { get; set; }
        
        [JsonPropertyName("cantidadTraspasos")]
        public int CantidadTraspasos { get; set; }
        
        [JsonPropertyName("porcentajeDelTotal")]
        public double PorcentajeDelTotal { get; set; }
        
        [JsonPropertyName("porcentajePorTraspasos")]
        public double PorcentajePorTraspasos { get; set; }
        
        [JsonPropertyName("posicion")]
        public int Posicion { get; set; }
    }
}

