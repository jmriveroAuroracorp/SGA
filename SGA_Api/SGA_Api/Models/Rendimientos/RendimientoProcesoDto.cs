namespace SGA_Api.Models.Rendimientos
{
    public class RendimientoProcesoDto
    {
        public string TipoProceso { get; set; } = string.Empty; // "TRASPASOS", "INVENTARIOS", "CONTEO", "PALETS"
        
        // Métricas generales
        public int TotalProcesos { get; set; }
        public int ProcesosCompletados { get; set; }
        public int ProcesosPendientes { get; set; }
        public double TasaFinalizacion { get; set; } // Porcentaje
        
        // Métricas de tiempo
        public double? TiempoPromedioMinutos { get; set; }
        public double? TiempoMinimoMinutos { get; set; }
        public double? TiempoMaximoMinutos { get; set; }
        
        // Métricas específicas por tipo
        public double? LineasPorHora { get; set; } // Para inventarios y conteos
        public double? ProcesosPorDia { get; set; }
    }
}

