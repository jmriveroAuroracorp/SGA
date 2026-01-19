namespace SGA_Api.Models.Rendimientos
{
    public class RendimientoArticuloDto
    {
        public string CodigoFamilia { get; set; } = string.Empty;
        public int CantidadArticulosUnicos { get; set; }
        
        // Frecuencia de movimiento
        public int CantidadTraspasos { get; set; }
        public decimal UnidadesTotalesMovidas { get; set; }
        public decimal PromedioUnidadesPorTraspaso { get; set; }
        
        // Eficiencia de manejo
        public double? TiempoPromedioMinutos { get; set; }
        public double TiempoTotalMinutos { get; set; }
        public double? EficienciaUnidadesPorMinuto { get; set; }
        
        // Distribución y alcance
        public int AlmacenesUnicos { get; set; }
        public int UbicacionesUnicas { get; set; }
        public int OperariosUnicos { get; set; }
        
        // Rankings y porcentajes
        public int Posicion { get; set; }
        public double PorcentajeDelTotalTraspasos { get; set; }
    }
}

