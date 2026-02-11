using System;

namespace SGA_Api.Models.Inventario
{
    public class CambioArticuloDto
    {
        public short CodigoEmpresa { get; set; }

        // Datos del artículo ORIGEN
        public string CodigoArticuloOrigen { get; set; } = string.Empty;
        public string CodigoAlmacen { get; set; } = string.Empty;
        public string? Ubicacion { get; set; }
        public string? Partida { get; set; }
        public DateTime? FechaCaducidadOrigen { get; set; }
        public decimal Cantidad { get; set; }
        public Guid? PaletId { get; set; }

        // Datos del artículo DESTINO
        // Para cambio de código: se especifica CodigoArticuloDestino
        // Para cambio de fecha: se especifica FechaCaducidadDestino
        public string? CodigoArticuloDestino { get; set; }
        public DateTime? FechaCaducidadDestino { get; set; }
        public string? PartidaDestino { get; set; }

        // Información adicional
        public int UsuarioId { get; set; }
        public string? Comentario { get; set; }
    }

    /// <summary>
    /// DTO para planificar un cambio de artículo/asignación a operario sin ejecutar aún los ajustes.
    /// Reutiliza todos los campos de CambioArticuloDto y añade el operario asignado.
    /// </summary>
    public class PlanificarCambioArticuloDto : CambioArticuloDto
    {
        public int OperarioAsignadoId { get; set; }
    }

    /// <summary>
    /// DTO para ejecutar un cambio de artículo ya planificado.
    /// Indica el usuario/operario que realiza físicamente el ajuste.
    /// </summary>
    public class EjecutarCambioArticuloDto
    {
        public int UsuarioId { get; set; }
    }

    /// <summary>
    /// DTO para listar cambios de artículo pendientes por operario.
    /// </summary>
    public class CambioArticuloPendienteDto
    {
        public Guid IdCambioArticulo { get; set; }
        public short CodigoEmpresa { get; set; }
        public string CodigoArticuloOrigen { get; set; } = string.Empty;
        public string? CodigoArticuloDestino { get; set; }
        public string CodigoAlmacen { get; set; } = string.Empty;
        public string? Ubicacion { get; set; }
        public string? PartidaOrigen { get; set; }
        public string? PartidaDestino { get; set; }
        public DateTime? FechaCaducidadOrigen { get; set; }
        public DateTime? FechaCaducidadDestino { get; set; }
        public decimal Cantidad { get; set; }
        public Guid? PaletId { get; set; }
        public string? CodigoPalet { get; set; }
        public string TipoCambio { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string? Comentario { get; set; }
        public int? OperarioAsignadoId { get; set; }
        public DateTime Fecha { get; set; }
    }
}

