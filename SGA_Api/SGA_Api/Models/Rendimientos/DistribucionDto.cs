namespace SGA_Api.Models.Rendimientos
{
    public class DistribucionDto
    {
        public List<AlmacenDistribucionDto> TopAlmacenesOrigen { get; set; } = new List<AlmacenDistribucionDto>();
        public List<AlmacenDistribucionDto> TopAlmacenesDestino { get; set; } = new List<AlmacenDistribucionDto>();
        public List<UbicacionDistribucionDto> TopUbicacionesOrigen { get; set; } = new List<UbicacionDistribucionDto>();
        public List<UbicacionDistribucionDto> TopUbicacionesDestino { get; set; } = new List<UbicacionDistribucionDto>();
        public List<FlujoDistribucionDto> FlujosPrincipales { get; set; } = new List<FlujoDistribucionDto>();
    }
    
    public class AlmacenDistribucionDto
    {
        public string CodigoAlmacen { get; set; } = string.Empty;
        public decimal UnidadesMovidas { get; set; }
        public int CantidadTraspasos { get; set; }
        public double PorcentajeDelTotal { get; set; } // Porcentaje por unidades
        public double PorcentajePorTraspasos { get; set; } // Porcentaje por cantidad de traspasos
        public int Posicion { get; set; }
    }
    
    public class UbicacionDistribucionDto
    {
        public string CodigoAlmacen { get; set; } = string.Empty;
        public string Ubicacion { get; set; } = string.Empty;
        public decimal UnidadesMovidas { get; set; }
        public int CantidadTraspasos { get; set; }
        public double PorcentajeDelTotal { get; set; } // Porcentaje por unidades
        public double PorcentajePorTraspasos { get; set; } // Porcentaje por cantidad de traspasos
        public int Posicion { get; set; }
    }
    
    public class FlujoDistribucionDto
    {
        public string AlmacenOrigen { get; set; } = string.Empty;
        public string UbicacionOrigen { get; set; } = string.Empty;
        public string AlmacenDestino { get; set; } = string.Empty;
        public string UbicacionDestino { get; set; } = string.Empty;
        public decimal UnidadesMovidas { get; set; }
        public int CantidadTraspasos { get; set; }
        public double PorcentajeDelTotal { get; set; } // Porcentaje por unidades
        public double PorcentajePorTraspasos { get; set; } // Porcentaje por cantidad de traspasos
        public int Posicion { get; set; }
    }
}

