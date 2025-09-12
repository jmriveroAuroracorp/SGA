using System.ComponentModel.DataAnnotations;

namespace SGA_Api.Models.Conteos
{
    public class OrdenConteo
    {
        public int CodigoEmpresa { get; set; } = 1;
        public string Titulo { get; set; } = string.Empty;
        public string Visibilidad { get; set; } = string.Empty;
        public string ModoGeneracion { get; set; } = string.Empty;
        public string Alcance { get; set; } = string.Empty;
        public string? FiltrosJson { get; set; }
        public DateTime? FechaPlan { get; set; }
        public DateTime? FechaEjecucion { get; set; }
        public string? SupervisorCodigo { get; set; }
        public string CreadoPorCodigo { get; set; } = string.Empty;
        public string Estado { get; set; } = "PLANIFICADO";
        public byte Prioridad { get; set; } = 3;
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
        public Guid GuidID { get; set; } = Guid.NewGuid();

        // Navigation properties
        public ICollection<LecturaConteo> Lecturas { get; set; } = new List<LecturaConteo>();
        public ICollection<ResultadoConteo> Resultados { get; set; } = new List<ResultadoConteo>();
    }
} 