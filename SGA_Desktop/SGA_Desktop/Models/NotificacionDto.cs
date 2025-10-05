using System;

namespace SGA_Desktop.Models
{
    /// <summary>
    /// DTO para representar una notificación del sistema
    /// </summary>
    public class NotificacionDto
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Titulo { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
        public string Tipo { get; set; } = "info"; // "success", "info", "error", "warning"
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public bool Leida { get; set; } = false;
        public int UsuarioId { get; set; }
        public string? TraspasoId { get; set; }
        public string? CodigoPalet { get; set; }
        public string? CodigoArticulo { get; set; }
        public string? TipoTraspaso { get; set; } // "PALET", "ARTICULO"
        public string? EstadoAnterior { get; set; }
        public string? EstadoActual { get; set; }
        
        // Información adicional del traspaso
        public string? AlmacenOrigen { get; set; }
        public string? UbicacionOrigen { get; set; }
        public string? AlmacenDestino { get; set; }
        public string? UbicacionDestino { get; set; }
        public decimal? Cantidad { get; set; }
        public string? Unidad { get; set; } = "UD"; // Por defecto unidades
        public string? DescripcionArticulo { get; set; }

        /// <summary>
        /// Obtiene el identificador principal del traspaso (palet o artículo)
        /// </summary>
        public string? IdentificadorPrincipal => TipoTraspaso == "PALET" ? CodigoPalet : CodigoArticulo;

        /// <summary>
        /// Obtiene el texto descriptivo del tipo de traspaso
        /// </summary>
        public string TipoTraspasoTexto => TipoTraspaso == "PALET" ? "palet" : "artículo";

        /// <summary>
        /// Obtiene el icono correspondiente al tipo de notificación
        /// </summary>
        public string Icono => Tipo switch
        {
            "success" => "✅",
            "error" => "❌",
            "warning" => "⚠️",
            "info" => "ℹ️",
            _ => "📢"
        };

        /// <summary>
        /// Obtiene el color correspondiente al tipo de notificación
        /// </summary>
        public string Color => Tipo switch
        {
            "success" => "#4CAF50", // Verde
            "error" => "#F44336",   // Rojo
            "warning" => "#FF9800", // Naranja
            "info" => "#2196F3",    // Azul
            _ => "#9E9E9E"          // Gris
        };

        /// <summary>
        /// Obtiene el color de fondo correspondiente al tipo de notificación
        /// </summary>
        public string ColorFondo => Tipo switch
        {
            "success" => "#E8F5E8", // Verde claro
            "error" => "#FFEBEE",   // Rojo claro
            "warning" => "#FFF3E0", // Naranja claro
            "info" => "#E3F2FD",    // Azul claro
            _ => "#F5F5F5"          // Gris claro
        };

        /// <summary>
        /// Indica si la notificación es positiva (success) o negativa (error, warning)
        /// </summary>
        public bool EsPositiva => Tipo == "success";
        
        /// <summary>
        /// Indica si la notificación es negativa (error, warning)
        /// </summary>
        public bool EsNegativa => Tipo == "error" || Tipo == "warning";

        /// <summary>
        /// Obtiene la información de cantidad formateada
        /// </summary>
        public string CantidadFormateada => Cantidad.HasValue ? $"{Cantidad.Value:N2} {Unidad ?? "UD"}" : "";

        /// <summary>
        /// Obtiene la información de ubicación formateada con almacenes
        /// </summary>
        public string UbicacionFormateada
        {
            get
            {
                var origen = !string.IsNullOrEmpty(AlmacenOrigen) && !string.IsNullOrEmpty(UbicacionOrigen) 
                    ? $"{AlmacenOrigen}-{UbicacionOrigen}" 
                    : UbicacionOrigen ?? "";
                
                var destino = !string.IsNullOrEmpty(AlmacenDestino) && !string.IsNullOrEmpty(UbicacionDestino) 
                    ? $"{AlmacenDestino}-{UbicacionDestino}" 
                    : UbicacionDestino ?? "";
                
                if (!string.IsNullOrEmpty(origen) && !string.IsNullOrEmpty(destino))
                    return $"{origen} → {destino}";
                else if (!string.IsNullOrEmpty(destino))
                    return $"→ {destino}";
                else if (!string.IsNullOrEmpty(origen))
                    return $"{origen} →";
                else
                    return "";
            }
        }

        /// <summary>
        /// Obtiene información adicional formateada para mostrar en la notificación
        /// </summary>
        public string InformacionAdicional
        {
            get
            {
                var partes = new List<string>();
                
                if (!string.IsNullOrEmpty(DescripcionArticulo))
                    partes.Add(DescripcionArticulo);
                
                if (!string.IsNullOrEmpty(UbicacionFormateada))
                    partes.Add($"Ubicación: {UbicacionFormateada}");
                
                if (!string.IsNullOrEmpty(CantidadFormateada))
                    partes.Add($"Cantidad: {CantidadFormateada}");
                
                return string.Join(" • ", partes);
            }
        }
    }
}
