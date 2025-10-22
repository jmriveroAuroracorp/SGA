namespace SGA_Api.Services
{
    /// <summary>
    /// Interfaz para el servicio de notificaciones específicas de conteos
    /// </summary>
    public interface INotificacionesConteosService
    {
        /// <summary>
        /// Notifica cuando se crea una nueva orden de conteo
        /// </summary>
        Task NotificarOrdenCreadaAsync(Guid ordenId, string titulo, string creadoPorCodigo, string? supervisorCodigo = null, 
            string? codigoAlmacen = null, string? alcance = null, string? codigoOperario = null, string? codigoUbicacion = null, string? codigoArticulo = null, byte prioridad = 3);

        /// <summary>
        /// Notifica cuando se asigna un operario a una orden de conteo
        /// </summary>
        Task NotificarOperarioAsignadoAsync(Guid ordenId, string codigoOperario, string? supervisorCodigo = null);

        /// <summary>
        /// Notifica cuando se inicia una orden de conteo
        /// </summary>
        Task NotificarOrdenIniciadaAsync(Guid ordenId, string codigoOperario, string? supervisorCodigo = null);

        /// <summary>
        /// Notifica cuando se completa una orden de conteo
        /// </summary>
        Task NotificarOrdenCompletadaAsync(Guid ordenId, string codigoOperario, int totalLecturas, string? supervisorCodigo = null);

        /// <summary>
        /// Notifica cuando se cierra una orden de conteo
        /// </summary>
        Task NotificarOrdenCerradaAsync(Guid ordenId, string? supervisorCodigo = null, int? totalResultados = null);

        /// <summary>
        /// Notifica cuando se crea una nueva lectura de conteo
        /// </summary>
        Task NotificarLecturaCreadaAsync(Guid ordenId, string codigoOperario, string codigoArticulo, decimal cantidad, string? supervisorCodigo = null);

        /// <summary>
        /// Notifica cuando se reasigna una línea de conteo
        /// </summary>
        Task NotificarLineaReasignadaAsync(Guid ordenId, string codigoArticulo, string nuevoOperario, string? supervisorCodigo = null);

        /// <summary>
        /// Notifica cuando se actualiza un aprobador de resultado de conteo
        /// </summary>
        Task NotificarAprobadorActualizadoAsync(Guid resultadoId, string codigoAprobador, string? supervisorCodigo = null);

        /// <summary>
        /// Notifica cuando una orden de conteo se cancela
        /// </summary>
        Task NotificarOrdenCanceladaAsync(Guid ordenId, string motivo, string usuarioCodigo, string? supervisorCodigo = null);

        /// <summary>
        /// Notifica cuando un conteo se envía a supervisión
        /// </summary>
        Task NotificarConteoSupervisionAsync(Guid resultadoGuid, string codigoArticulo, decimal cantidad, string operarioCodigo, string? supervisorCodigo = null);

        /// <summary>
        /// Notifica eventos críticos que requieren atención inmediata
        /// </summary>
        Task NotificarEventoCriticoAsync(string tipoEvento, string titulo, string mensaje, object? datosAdicionales = null);

        /// <summary>
        /// Notifica cuando cambia el estado de una orden de conteo
        /// </summary>
        Task NotificarCambioEstadoAsync(Guid ordenId, string estadoAnterior, string estadoNuevo, string? codigoOperario = null, string? supervisorCodigo = null);
    }
}
