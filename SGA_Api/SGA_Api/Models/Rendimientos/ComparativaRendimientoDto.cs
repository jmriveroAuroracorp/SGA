namespace SGA_Api.Models.Rendimientos
{
    public class ComparativaRendimientoDto
    {
        public string TipoComparativa { get; set; } = string.Empty; // "OPERARIOS", "PERIODOS", "PROCESOS"
        public List<ItemComparativaDto> Items { get; set; } = new List<ItemComparativaDto>();
    }
    
    public class ItemComparativaDto
    {
        public string Etiqueta { get; set; } = string.Empty; // Nombre del operario, período, etc.
        public double Valor { get; set; }
        public string Unidad { get; set; } = string.Empty; // "operaciones", "minutos", "porcentaje"
        public double? Variacion { get; set; } // Variación respecto al promedio o período anterior
    }
}

