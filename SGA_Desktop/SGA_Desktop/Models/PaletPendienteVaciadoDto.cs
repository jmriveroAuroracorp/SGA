using System;
using System.Collections.Generic;

namespace SGA_Desktop.Models;

public class PaletPendienteVaciadoDto
{
	public Guid PaletId { get; set; }
	public short CodigoEmpresa { get; set; }
	public string CodigoPalet { get; set; } = string.Empty;
	public string? Observacion { get; set; }
	public List<LineaPendienteVaciadoDto> Lineas { get; set; } = new();
}

public class LineaPendienteVaciadoDto
{
	public Guid LineaId { get; set; }
	public string CodigoArticulo { get; set; } = string.Empty;
	public string? DescripcionArticulo { get; set; }
	public decimal CantidadRegistrada { get; set; }
	public decimal CantidadDisponible { get; set; }
	public decimal CantidadFaltante { get; set; }
	public string CodigoAlmacen { get; set; } = string.Empty;
	public string Ubicacion { get; set; } = string.Empty;
	public string? Lote { get; set; }
	public DateTime? FechaCaducidad { get; set; }
}

