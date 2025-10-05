using System;
using System.Collections.Generic;

namespace SGA_Desktop.Models
{
    /// <summary>
    /// DTO para representar una notificación completa con información de lectura (coincide con la API)
    /// </summary>
    public class NotificacionApiDto
    {
        /// <summary>
        /// Identificador único de la notificación
        /// </summary>
        public Guid IdNotificacion { get; set; }

        /// <summary>
        /// Código de la empresa
        /// </summary>
        public int CodigoEmpresa { get; set; }

        /// <summary>
        /// Tipo de notificación
        /// </summary>
        public string TipoNotificacion { get; set; } = string.Empty;

        /// <summary>
        /// ID del proceso relacionado
        /// </summary>
        public Guid? ProcesoId { get; set; }

        /// <summary>
        /// Título de la notificación
        /// </summary>
        public string Titulo { get; set; } = string.Empty;

        /// <summary>
        /// Mensaje detallado de la notificación
        /// </summary>
        public string Mensaje { get; set; } = string.Empty;

        /// <summary>
        /// Estado anterior del proceso
        /// </summary>
        public string? EstadoAnterior { get; set; }

        /// <summary>
        /// Estado actual del proceso
        /// </summary>
        public string? EstadoActual { get; set; }

        /// <summary>
        /// Fecha de creación de la notificación
        /// </summary>
        public DateTime FechaCreacion { get; set; }

        /// <summary>
        /// Indica si la notificación está activa
        /// </summary>
        public bool EsActiva { get; set; }

        /// <summary>
        /// Indica si la notificación es para un grupo de usuarios
        /// </summary>
        public bool EsGrupal { get; set; }

        /// <summary>
        /// Grupo destino para notificaciones grupales
        /// </summary>
        public string? GrupoDestino { get; set; }

        /// <summary>
        /// Comentario adicional de la notificación
        /// </summary>
        public string? Comentario { get; set; }

        /// <summary>
        /// Indica si el usuario actual ha leído esta notificación
        /// </summary>
        public bool Leida { get; set; }

        /// <summary>
        /// Fecha en que el usuario actual leyó la notificación (si la ha leído)
        /// </summary>
        public DateTime? FechaLeida { get; set; }

        /// <summary>
        /// Lista de destinatarios de la notificación
        /// </summary>
        public List<NotificacionDestinatarioApiDto> Destinatarios { get; set; } = new List<NotificacionDestinatarioApiDto>();

        /// <summary>
        /// Información adicional para el tipo de notificación (ej: datos del traspaso, inventario, etc.)
        /// </summary>
        public object? DatosAdicionales { get; set; }

        // Propiedades de conveniencia para el frontend
        /// <summary>
        /// Obtiene el tipo de icono para el frontend
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

