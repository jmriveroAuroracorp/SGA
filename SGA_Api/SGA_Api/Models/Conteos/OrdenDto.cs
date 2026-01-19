namespace SGA_Api.Models.Conteos
{
    public class OrdenDto
    {
        public Guid GuidID { get; set; }
        public int CodigoEmpresa { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Visibilidad { get; set; } = string.Empty;
        public string ModoGeneracion { get; set; } = string.Empty;
        public string Alcance { get; set; } = string.Empty;
        public string? FiltrosJson { get; set; }
        public DateTime? FechaPlan { get; set; }
        public DateTime? FechaEjecucion { get; set; }
        public string? SupervisorCodigo { get; set; }
        public string CreadoPorCodigo { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public byte Prioridad { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string? CodigoOperario { get; set; }
        public string? CodigoAlmacen { get; set; }
        public string? CodigoUbicacion { get; set; }
        public string? CodigoArticulo { get; set; }
        public string? DescripcionArticulo { get; set; }
        public string? LotePartida { get; set; }
        public decimal? CantidadTeorica { get; set; }
        public string? Comentario { get; set; }
        public DateTime? FechaAsignacion { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaCierre { get; set; }
        
        // Total de lecturas registradas para esta orden
        public int? TotalLecturas { get; set; }
        
        // Información adicional para la respuesta
        public List<LecturaResponseDto> Lecturas { get; set; } = new List<LecturaResponseDto>();
        public ResultadoConteoDto? Resultado { get; set; }
        
        // Propiedades para conteos periódicos
        public bool EsPeriodico { get; set; } = false;
        public bool Activo { get; set; } = true;
        public DateTime? FechaProximaRenovacion { get; set; }
        public int? FrecuenciaDias { get; set; }
        
        // Si esta orden es una renovación de un conteo periódico, aquí está el Guid del conteo periódico original
        public Guid? OrdenPadreGuid { get; set; }
    }
} 