using System;

namespace SGA_Desktop.Models;

public class TraspasoErrorDto
{
	public Guid TraspasoId { get; set; }
	public Guid PaletId { get; set; }
	public string? CodigoPalet { get; set; }
	public string? CodigoArticulo { get; set; }
	public decimal Cantidad { get; set; }
	public string? AlmacenOrigen { get; set; }
	public string? UbicacionOrigen { get; set; }
	public string? AlmacenDestino { get; set; }
	public string? UbicacionDestino { get; set; }
	public DateTime FechaInicio { get; set; }
	public DateTime? FechaFinalizacion { get; set; }
	public string CodigoEstado { get; set; } = string.Empty;
	public string? Comentario { get; set; }
	public string? EstadoErp { get; set; }
	public int UsuarioInicioId { get; set; }
	public string? UsuarioInicioNombre { get; set; }
	public int? UsuarioFinalizacionId { get; set; }
	public string? UsuarioFinalizacionNombre { get; set; }
	public short CodigoEmpresa { get; set; }
	public DateTime? FechaCaducidad { get; set; }
	public string? Partida { get; set; }
}

