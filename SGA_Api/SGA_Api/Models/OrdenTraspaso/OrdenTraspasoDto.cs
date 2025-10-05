using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.OrdenTraspaso
{
    public class OrdenTraspasoDto
    {
        public Guid IdOrdenTraspaso { get; set; }
        public short CodigoEmpresa { get; set; }
        public string Estado { get; set; }
        public short Prioridad { get; set; }
        public DateTime? FechaPlan { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFinalizacion { get; set; }
        public string TipoOrigen { get; set; }
        public int UsuarioCreacion { get; set; }
        public string? Comentarios { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string CodigoOrden { get; set; }
        public string? CodigoAlmacenDestino { get; set; }
        public List<LineaOrdenTraspasoDetalleDto> Lineas { get; set; } = new();
    }

    public class LineaOrdenTraspasoDetalleDto
    {
        public Guid IdLineaOrden { get; set; }
        public Guid IdOrdenTraspaso { get; set; }
        public int Orden { get; set; }
        public string CodigoArticulo { get; set; }
        public string? DescripcionArticulo { get; set; }
        public DateTime? FechaCaducidad { get; set; }
        public decimal CantidadPlan { get; set; }
        public string CodigoAlmacenOrigen { get; set; }
        public string? UbicacionOrigen { get; set; }
        public string? Partida { get; set; }
        public string? PaletOrigen { get; set; }
        public string CodigoAlmacenDestino { get; set; }
        public string? UbicacionDestino { get; set; }
        public string? PaletDestino { get; set; }
        public string Estado { get; set; }
        public decimal CantidadMovida { get; set; }
        public bool Completada { get; set; }
        public int IdOperarioAsignado { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFinalizacion { get; set; }
        public Guid? IdTraspaso { get; set; }
    }

    public class CrearOrdenTraspasoDto
    {
        public short CodigoEmpresa { get; set; }
        public short Prioridad { get; set; } = 10;
        public DateTime? FechaPlan { get; set; }
        public string TipoOrigen { get; set; } = "SGA";
        public int UsuarioCreacion { get; set; }
        public string? Comentarios { get; set; }
        public string? CodigoAlmacenDestino { get; set; }
        public List<CrearLineaOrdenTraspasoDto> Lineas { get; set; } = new();
    }

    public class CrearLineaOrdenTraspasoDto
    {
        public int Orden { get; set; }
        public string CodigoArticulo { get; set; }
        public string? DescripcionArticulo { get; set; }
        public DateTime? FechaCaducidad { get; set; }
        public decimal CantidadPlan { get; set; }
        public string? CodigoAlmacenOrigen { get; set; }
        public string? UbicacionOrigen { get; set; }
        public string? Partida { get; set; }
        public string? PaletOrigen { get; set; }
        public string CodigoAlmacenDestino { get; set; }
        public string? UbicacionDestino { get; set; }
        public string? PaletDestino { get; set; }
        public int IdOperarioAsignado { get; set; }
        public string Estado { get; set; } = "PENDIENTE";
    }

    public class ActualizarOrdenTraspasoDto
    {
        public string? Estado { get; set; }
        public short? Prioridad { get; set; }
        public DateTime? FechaPlan { get; set; }
        public int? UsuarioAsignado { get; set; }
        public string? Comentarios { get; set; }
    }

    public class ActualizarLineaOrdenTraspasoDto
    {
        public string? Estado { get; set; }
        public decimal? CantidadMovida { get; set; }
        public bool? Completada { get; set; }
        public int IdOperarioAsignado { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFinalizacion { get; set; }
        public Guid? IdTraspaso { get; set; }
    }

    public class RegistrarMovimientoDto
    {
        public Guid IdLineaOrden { get; set; }
        public Guid? IdTraspaso { get; set; }
        public int IdOperario { get; set; }
        public string? Comentarios { get; set; }
    }
} 