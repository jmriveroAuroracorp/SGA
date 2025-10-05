using System;

namespace SGA_Desktop.Models
{
    /// <summary>
    /// DTO para el resumen de notificaciones (lista principal) (coincide con la API)
    /// </summary>
    public class NotificacionResumenApiDto
    {
        /// <summary>
        /// Identificador único de la notificación
        /// </summary>
        public Guid IdNotificacion { get; set; }

        /// <summary>
        /// Tipo de notificación
        /// </summary>
        public string TipoNotificacion { get; set; } = string.Empty;

        /// <summary>
        /// Título de la notificación
        /// </summary>
        public string Titulo { get; set; } = string.Empty;

        /// <summary>
        /// Mensaje resumido (primeros caracteres)
        /// </summary>
        public string MensajeResumido { get; set; } = string.Empty;

        /// <summary>
        /// Fecha de creación de la notificación
        /// </summary>
        public DateTime FechaCreacion { get; set; }

        /// <summary>
        /// Indica si el usuario actual ha leído esta notificación
        /// </summary>
        public bool Leida { get; set; }

        /// <summary>
        /// Fecha en que el usuario actual leyó la notificación
        /// </summary>
        public DateTime? FechaLeida { get; set; }

        /// <summary>
        /// Estado actual del proceso relacionado
        /// </summary>
        public string? EstadoActual { get; set; }

        /// <summary>
        /// ID del proceso relacionado
        /// </summary>
        public Guid? ProcesoId { get; set; }

        /// <summary>
        /// Tipo de notificación para iconos (success, error, warning, info)
        /// </summary>
        public string TipoIcono => EstadoActual switch
        {
            "COMPLETADO" => "success",
            "ERROR_ERP" => "error",
            "PENDIENTE_ERP" => "warning",
            "PENDIENTE" => "info",
            _ => "info"
        };

        /// <summary>
        /// Obtiene el emoji correspondiente al tipo de notificación
        /// </summary>
        public string Emoji => TipoNotificacion switch
        {
            "TRASPASO" => "📦",
            "INVENTARIO" => "📊",
            "ORDEN_TRASPASO" => "📋",
            "CONTEO" => "🔢",
            "AVISO_GENERAL" => "🔔",
            _ => "🔔"
        };

        /// <summary>
        /// Obtiene el color correspondiente al tipo de notificación
        /// </summary>
        public string Color => TipoIcono switch
        {
            "success" => "#4CAF50", // Verde
            "error" => "#F44336",   // Rojo
            "warning" => "#FFC107", // Ámbar
            "info" => "#2196F3",    // Azul
            _ => "#9E9E9E"          // Gris
        };

        /// <summary>
        /// Obtiene el color de fondo correspondiente al tipo de notificación
        /// </summary>
        public string ColorFondo => TipoIcono switch
        {
            "success" => "#E8F5E8", // Verde claro
            "error" => "#FFEBEE",   // Rojo claro
            "warning" => "#FFF3E0", // Ámbar claro
            "info" => "#E3F2FD",    // Azul claro
            _ => "#F5F5F5"          // Gris claro
        };

        /// <summary>
        /// Indica si la notificación es positiva (success) o negativa (error, warning)
        /// </summary>
        public bool EsPositiva => TipoIcono == "success";

        /// <summary>
        /// Indica si la notificación es negativa (error, warning)
        /// </summary>
        public bool EsNegativa => TipoIcono == "error" || TipoIcono == "warning";

        /// <summary>
        /// Obtiene el tiempo transcurrido desde la creación
        /// </summary>
        public string TiempoTranscurrido
        {
            get
            {
                var tiempo = DateTime.UtcNow - FechaCreacion;
                if (tiempo.TotalMinutes < 1)
                    return "Ahora mismo";
                if (tiempo.TotalMinutes < 60)
                    return $"Hace {(int)tiempo.TotalMinutes} min";
                if (tiempo.TotalHours < 24)
                    return $"Hace {(int)tiempo.TotalHours} h";
                return $"Hace {(int)tiempo.TotalDays} días";
            }
        }
    }
}

