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
}

