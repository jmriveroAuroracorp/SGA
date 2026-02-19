using System;

namespace SGA_Api.Models.Inventario
{
    /// <summary>
    /// DTO para crear una orden de conversión (cabecera). Desktop crea la orden, operario la ejecuta desde Android.
    /// Sin Ubicacion ni PaletId en cabecera - eso lo registra el operario en cada línea.
    /// </summary>
    public class CrearOrdenConversionDto
    {
        public short CodigoEmpresa { get; set; }
        public int UsuarioId { get; set; }
        public int? OperarioAsignadoId { get; set; }

        // Datos del artículo ORIGEN
        public string CodigoArticuloOrigen { get; set; } = string.Empty;
        public string CodigoAlmacen { get; set; } = string.Empty;
        public string? PartidaOrigen { get; set; }
        public DateTime? FechaCaducidadOrigen { get; set; }
        public decimal Cantidad { get; set; }
        /// <summary>
        /// Cantidad en unidades de destino (ej. 600 pastillas). Si no se envía, se considera 1:1 con Cantidad.
        /// </summary>
        public decimal? CantidadFinal { get; set; }

        // Datos del artículo DESTINO
        public string? CodigoArticuloDestino { get; set; }
        public string? PartidaDestino { get; set; }
        public DateTime? FechaCaducidadDestino { get; set; }

        public string TipoCambio { get; set; } = string.Empty; // "CAMBIO_CODIGO" o "AMPLIACION"
        public string? Comentario { get; set; }
    }

    /// <summary>
    /// DTO para registrar una línea de conversión (lo que el operario ejecuta en Android).
    /// </summary>
    public class RegistrarLineaConversionDto
    {
        public int UsuarioEjecucionId { get; set; }
        public string CodigoAlmacen { get; set; } = string.Empty;
        public string Ubicacion { get; set; } = string.Empty;
        public Guid? PaletId { get; set; }
        public string? Partida { get; set; }
        public DateTime? FechaCaducidad { get; set; }
        public decimal Cantidad { get; set; }
    }

    /// <summary>
    /// DTO para listar órdenes de conversión pendientes por operario.
    /// </summary>
    public class OrdenConversionPendienteDto
    {
        public Guid IdOrdenConversion { get; set; }
        public short CodigoEmpresa { get; set; }
        public int UsuarioId { get; set; }
        public int? OperarioAsignadoId { get; set; }
        public DateTime Fecha { get; set; }
        public string CodigoArticuloOrigen { get; set; } = string.Empty;
        public string CodigoAlmacen { get; set; } = string.Empty;
        public string? PartidaOrigen { get; set; }
        public DateTime? FechaCaducidadOrigen { get; set; }
        public decimal Cantidad { get; set; }
        public decimal? CantidadFinal { get; set; }
        public string? CodigoArticuloDestino { get; set; }
        public string? PartidaDestino { get; set; }
        public DateTime? FechaCaducidadDestino { get; set; }
        public string TipoCambio { get; set; } = string.Empty;
        public string? Comentario { get; set; }
        public string Estado { get; set; } = string.Empty;
        public decimal CantidadEjecutada { get; set; }
        public decimal CantidadPendiente { get; set; }
    }

    /// <summary>
    /// DTO para detalle de una orden de conversión con sus líneas.
    /// </summary>
    public class OrdenConversionDetalleDto
    {
        public Guid IdOrdenConversion { get; set; }
        public short CodigoEmpresa { get; set; }
        public int UsuarioId { get; set; }
        public int? OperarioAsignadoId { get; set; }
        public DateTime Fecha { get; set; }
        public string CodigoArticuloOrigen { get; set; } = string.Empty;
        public string CodigoAlmacen { get; set; } = string.Empty;
        public string? PartidaOrigen { get; set; }
        public DateTime? FechaCaducidadOrigen { get; set; }
        public decimal Cantidad { get; set; }
        public decimal? CantidadFinal { get; set; }
        public string? CodigoArticuloDestino { get; set; }
        public string? PartidaDestino { get; set; }
        public DateTime? FechaCaducidadDestino { get; set; }
        public string TipoCambio { get; set; } = string.Empty;
        public string? Comentario { get; set; }
        public string Estado { get; set; } = string.Empty;
        public decimal CantidadEjecutada { get; set; }
        public decimal CantidadPendiente { get; set; }
        public List<OrdenConversionLineaDto> Lineas { get; set; } = new();
    }

    public class OrdenConversionLineaDto
    {
        public Guid IdLinea { get; set; }
        public int NumeroLinea { get; set; }
        public string CodigoAlmacen { get; set; } = string.Empty;
        public string Ubicacion { get; set; } = string.Empty;
        public Guid? PaletId { get; set; }
        public string? Partida { get; set; }
        public DateTime? FechaCaducidad { get; set; }
        public decimal Cantidad { get; set; }
        public decimal CantidadFinal { get; set; }
        public int UsuarioEjecucionId { get; set; }
        public DateTime FechaEjecucion { get; set; }
    }

    /// <summary>
    /// DTO para ubicaciones con stock disponible del artículo origen (replica Conteos/lecturas-pendientes).
    /// El operario ve dónde puede tomar stock para ejecutar la conversión.
    /// </summary>
    public class UbicacionDisponibleConversionDto
    {
        public string CodigoAlmacen { get; set; } = string.Empty;
        public string Ubicacion { get; set; } = string.Empty;
        public string? Partida { get; set; }
        public DateTime? FechaCaducidad { get; set; }
        public decimal CantidadDisponible { get; set; }
        public Guid? PaletId { get; set; }
        public string? CodigoPalet { get; set; }
    }
}
