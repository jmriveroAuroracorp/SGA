namespace SGA_Api.Models.Notificaciones
{
    /// <summary>
    /// Enum para los tipos de notificación disponibles en el sistema
    /// </summary>
    public enum TipoNotificacion
    {
        /// <summary>
        /// Notificación relacionada con traspasos
        /// </summary>
        TRASPASO = 1,

        /// <summary>
        /// Notificación relacionada con inventarios
        /// </summary>
        INVENTARIO = 2,

        /// <summary>
        /// Notificación relacionada con órdenes de traspaso
        /// </summary>
        ORDEN_TRASPASO = 3,

        /// <summary>
        /// Notificación relacionada con conteos
        /// </summary>
        CONTEO = 4,

        /// <summary>
        /// Notificación general del sistema
        /// </summary>
        AVISO_GENERAL = 5
    }

    /// <summary>
    /// Extensión para el enum TipoNotificacion
    /// </summary>
    public static class TipoNotificacionExtensions
    {
        /// <summary>
        /// Convierte el enum a string para almacenamiento en BD
        /// </summary>
        public static string ToStringValue(this TipoNotificacion tipo)
        {
            return tipo switch
            {
                TipoNotificacion.TRASPASO => "TRASPASO",
                TipoNotificacion.INVENTARIO => "INVENTARIO",
                TipoNotificacion.ORDEN_TRASPASO => "ORDEN_TRASPASO",
                TipoNotificacion.CONTEO => "CONTEO",
                TipoNotificacion.AVISO_GENERAL => "AVISO_GENERAL",
                _ => "AVISO_GENERAL"
            };
        }

        /// <summary>
        /// Convierte string de BD a enum
        /// </summary>
        public static TipoNotificacion FromString(string value)
        {
            return value?.ToUpper() switch
            {
                "TRASPASO" => TipoNotificacion.TRASPASO,
                "INVENTARIO" => TipoNotificacion.INVENTARIO,
                "ORDEN_TRASPASO" => TipoNotificacion.ORDEN_TRASPASO,
                "CONTEO" => TipoNotificacion.CONTEO,
                "AVISO_GENERAL" => TipoNotificacion.AVISO_GENERAL,
                _ => TipoNotificacion.AVISO_GENERAL
            };
        }

        /// <summary>
        /// Obtiene el tipo de icono para el frontend
        /// </summary>
        public static string GetTipoIcono(this TipoNotificacion tipo)
        {
            return tipo switch
            {
                TipoNotificacion.TRASPASO => "info",
                TipoNotificacion.INVENTARIO => "warning",
                TipoNotificacion.ORDEN_TRASPASO => "info",
                TipoNotificacion.CONTEO => "warning",
                TipoNotificacion.AVISO_GENERAL => "info",
                _ => "info"
            };
        }

        /// <summary>
        /// Obtiene el emoji para el frontend
        /// </summary>
        public static string GetEmoji(this TipoNotificacion tipo)
        {
            return tipo switch
            {
                TipoNotificacion.TRASPASO => "📦",
                TipoNotificacion.INVENTARIO => "📊",
                TipoNotificacion.ORDEN_TRASPASO => "📋",
                TipoNotificacion.CONTEO => "🔢",
                TipoNotificacion.AVISO_GENERAL => "🔔",
                _ => "🔔"
            };
        }
    }
}
