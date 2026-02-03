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
        public int? IdOperarioAsignado { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFinalizacion { get; set; }
        public Guid? IdTraspaso { get; set; }
        public string? UbicacionDestino { get; set; }
        public string? PaletDestino { get; set; }
        public string? PaletOrigen { get; set; }
        public string? CodigoAlmacenDestino { get; set; }
        public string? Partida { get; set; }
        public string? UbicacionOrigen { get; set; }
        public string? CodigoAlmacenOrigen { get; set; }
    }

    public class RegistrarMovimientoDto
    {
        public Guid IdLineaOrden { get; set; }
        public Guid? IdTraspaso { get; set; }
        public int IdOperario { get; set; }
        public string? Comentarios { get; set; }
    }

    public class StockLineaTraspasoDto
    {
        public string CodigoArticulo { get; set; }
        public string DescripcionArticulo { get; set; }
        public string CodigoAlmacen { get; set; }
        public string Ubicacion { get; set; }
        public string Partida { get; set; }
        public DateTime? FechaCaducidad { get; set; }
        public decimal StockDisponible { get; set; }
        public decimal StockReservado { get; set; }
        public string TipoStock { get; set; }
        public Guid? PaletId { get; set; }
        public string CodigoPalet { get; set; }
        public string EstadoPalet { get; set; }
    }

    public class ActualizarEstadoLineaDto
    {
        public string Estado { get; set; }
    }

    public class AjusteLineaOrdenTraspasoDto
    {
        public decimal CantidadAjuste { get; set; }
        public decimal CantidadStock { get; set; }  // Stock real en la ubicación/palet
        public int IdOperario { get; set; }
        public Guid? PaletId { get; set; }          // Si es un palet
        public string? CodigoPalet { get; set; }
        public string? CodigoGS1 { get; set; }
    }

    public class AjusteLineaResponseDto
    {
        public bool Success { get; set; }
        public string Mensaje { get; set; }
        public bool RequiereSupervision { get; set; }
        public bool LineaBloqueada { get; set; }
        public decimal DiferenciaStock { get; set; }
    }

    public class ActualizarIdTraspasoDto
    {
        public Guid? IdTraspaso { get; set; }
    }

    public class StockDisponibleDto
    {
        public string CodigoArticulo { get; set; }
        public string DescripcionArticulo { get; set; }
        public string CodigoAlmacen { get; set; }
        public string Ubicacion { get; set; }
        public string Partida { get; set; }
        public DateTime? FechaCaducidad { get; set; }
        public decimal StockDisponible { get; set; }
        public decimal StockReservado { get; set; }
        public string TipoStock { get; set; }
        public Guid? PaletId { get; set; }
        public string CodigoPalet { get; set; }
        public string? CodigoGS1 { get; set; }
        public string EstadoPalet { get; set; }
    }

    public class PaletPendienteDto
    {
        public string PaletDestino { get; set; }
        public string CodigoGS1 { get; set; }
        public int LineasCompletas { get; set; }
        public decimal CantidadTotal { get; set; }
        public bool ListoParaUbicar { get; set; }
        public string EstadoPalet { get; set; }
    }

    public class UbicarPaletDto
    {
        public string UbicacionDestino { get; set; }
        public int IdOperario { get; set; }
    }

    public class UbicarPaletResponseDto
    {
        public bool Success { get; set; }
        public string Mensaje { get; set; }
        public Guid? PaletId { get; set; }
        public string CodigoPalet { get; set; }
        public string EstadoActualizado { get; set; }
        public DateTime? FechaUbicacion { get; set; }
        public string UbicacionDestino { get; set; }
        public bool TraspasoCompletado { get; set; }
    }
} 