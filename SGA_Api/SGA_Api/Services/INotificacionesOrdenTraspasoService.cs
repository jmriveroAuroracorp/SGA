namespace SGA_Api.Services
{
    /// <summary>
    /// Interfaz para el servicio de notificaciones específicas de órdenes de traspaso
    /// </summary>
    public interface INotificacionesOrdenTraspasoService
    {
        /// <summary>
        /// Notifica cuando se crea una nueva orden de traspaso
        /// </summary>
        Task NotificarOrdenCreadaAsync(Guid ordenId, int usuarioCreacion, short codigoEmpresa, string codigoOrden, string? codigoAlmacenDestino = null);

        /// <summary>
        /// Notifica cuando se completa una orden de traspaso
        /// </summary>
        Task NotificarOrdenCompletadaAsync(Guid ordenId, int usuarioCreacion, short codigoEmpresa, string codigoOrden);

        /// <summary>
        /// Notifica cuando se asigna un operario a una línea de orden de traspaso
        /// </summary>
        Task NotificarLineaAsignadaAsync(Guid lineaId, Guid ordenId, int operarioAsignado, string codigoArticulo, string codigoAlmacenOrigen, short codigoEmpresa);
    }
}
