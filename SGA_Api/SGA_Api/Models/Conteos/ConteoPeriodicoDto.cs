namespace SGA_Api.Models.Conteos
{
    public class ConteoPeriodicoDto
    {
        public Guid GuidID { get; set; }
        public int CodigoEmpresa { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public int? FrecuenciaDias { get; set; }
        public DateTime? FechaUltimaRenovacion { get; set; }
        public DateTime? FechaProximaRenovacion { get; set; }
        public bool Activo { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string? CodigoOperario { get; set; }
        public string? CodigoAlmacen { get; set; }
        public string Alcance { get; set; } = string.Empty;
        public string CreadoPorCodigo { get; set; } = string.Empty;
        public byte Prioridad { get; set; }
        public DateTime FechaCreacion { get; set; }
        public int TotalRenovaciones { get; set; } // Contador de órdenes hijas
    }
}

