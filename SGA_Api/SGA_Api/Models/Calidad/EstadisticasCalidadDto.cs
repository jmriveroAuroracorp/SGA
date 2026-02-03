namespace SGA_Api.Models.Calidad
{
    public class EstadisticasCalidadDto
    {
        public int TotalBloqueados { get; set; }
        public int TotalDesbloqueados { get; set; }
        public int BloqueosTotales { get; set; }
        public int BloqueosSoloPulmon { get; set; }
        public int BloqueosGlobales { get; set; }
        public int BloqueosIndividuales { get; set; }
        
        // Nuevas secciones
        public List<TopArticuloBloqueadoDto> TopArticulosBloqueados { get; set; } = new();
        public List<DistribucionAlmacenDto> DistribucionPorAlmacen { get; set; } = new();
        public List<BloqueoRecienteDto> BloqueosRecientes { get; set; } = new();
    }
    
    public class TopArticuloBloqueadoDto
    {
        public string CodigoArticulo { get; set; } = string.Empty;
        public string? DescripcionArticulo { get; set; }
        public int CantidadBloqueos { get; set; }
        public int Posicion { get; set; }
    }
    
    public class DistribucionAlmacenDto
    {
        public string CodigoAlmacen { get; set; } = string.Empty;
        public string? NombreAlmacen { get; set; }
        public int CantidadBloqueos { get; set; }
        public int Posicion { get; set; }
    }
    
    public class BloqueoRecienteDto
    {
        public Guid Id { get; set; }
        public string CodigoArticulo { get; set; } = string.Empty;
        public string? DescripcionArticulo { get; set; }
        public string LotePartida { get; set; } = string.Empty;
        public string CodigoAlmacen { get; set; } = string.Empty;
        public string? Ubicacion { get; set; }
        public string TipoBloqueo { get; set; } = string.Empty;
        public string ComentarioBloqueo { get; set; } = string.Empty;
        public string UsuarioBloqueo { get; set; } = string.Empty;
        public DateTime FechaBloqueo { get; set; }
        public bool EsGlobal { get; set; }
    }
}
