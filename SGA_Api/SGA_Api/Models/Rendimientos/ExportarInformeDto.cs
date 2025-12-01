namespace SGA_Api.Models.Rendimientos
{
    public class ExportarInformeDto
    {
        public FiltroRendimientosDto Filtros { get; set; } = new FiltroRendimientosDto();
        public string TipoInforme { get; set; } = string.Empty; // "OPERARIOS", "PROCESOS", "COMPARATIVA", "TENDENCIAS"
        public string Formato { get; set; } = "PDF"; // "PDF", "EXCEL"
        public List<string> SeccionesIncluidas { get; set; } = new List<string>();
    }
}

