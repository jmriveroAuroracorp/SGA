using SGA_Desktop.Models;
using SGA_Desktop.Helpers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SGA_Desktop.Services
{
    /// <summary>
    /// Manager singleton para gestionar notificaciones del usuario actual
    /// </summary>
    public static class NotificacionesManager
    {
        private static NotificacionesTraspasosService? _servicio;
        private static ILogger<NotificacionesTraspasosService>? _logger;
        private static readonly object _lock = new object();
        
        private static readonly string _apiUrl = "http://localhost:5234"; // URL del Hub según especificaciones

        // Lista de notificaciones gestionada por el manager
        private static List<NotificacionDto> _notificaciones = new();
        private static int _contadorPendientes = 0;

        // Eventos para notificar cambios en las notificaciones
        public static event Action<NotificacionDto>? OnNotificacionAgregada;
        public static event Action<int>? OnContadorCambiado;

        public static NotificacionesTraspasosService? Instancia => _servicio;

        public static int ContadorPendientes
        {
            get => _contadorPendientes;
            private set
            {
                if (_contadorPendientes != value)
                {
                    _contadorPendientes = value;
                    OnContadorCambiado?.Invoke(_contadorPendientes);
                }
            }
        }

        public static void Initialize(ILogger<NotificacionesTraspasosService>? logger = null)
        {
            lock (_lock)
            {
                _logger = logger;
                if (_servicio == null)
                {
                    _servicio = new NotificacionesTraspasosService(_apiUrl, _logger);
                }
            }
        }

        public static async Task InicializarAsync()
        {
            // Si la aplicación se está cerrando, no inicializar notificaciones
            if (SessionManager.IsClosing)
            {
                System.Diagnostics.Debug.WriteLine("🚫 NotificacionesManager: Aplicación cerrándose, no inicializar");
                return;
            }

            lock (_lock)
            {
                if (_servicio == null)
                {
                    Initialize(); // Asegurarse de que la instancia esté creada
                }
            }

            if (_servicio != null && SessionManager.UsuarioActual != null && SessionManager.UsuarioActual.operario > 0)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"🔌 NotificacionesManager: Conectando para usuario {SessionManager.UsuarioActual.operario}");
                    await _servicio.ConectarAsync();
                    System.Diagnostics.Debug.WriteLine("✅ NotificacionesManager: Conectado exitosamente");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ NotificacionesManager: Error al conectar: {ex.Message}");
                    _logger?.LogError(ex, "Error al inicializar servicio de notificaciones SignalR");
                    throw;
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"❌ NotificacionesManager: Usuario no logueado. UsuarioActual={SessionManager.UsuarioActual?.operario}");
                _logger?.LogWarning("No se pudo inicializar SignalR: Usuario no logueado o ID de operario inválido.");
            }
        }

        public static async Task DesconectarAsync()
        {
            if (_servicio != null)
            {
                await _servicio.DesconectarAsync();
                await ((IAsyncDisposable)_servicio).DisposeAsync();
                _servicio = null;
                _notificaciones.Clear();
                ContadorPendientes = 0;
            }
        }

        /// <summary>
        /// Agrega una nueva notificación a la lista y actualiza el contador
        /// </summary>
        public static void AgregarNotificacion(NotificacionDto notificacion)
        {
            _notificaciones.Add(notificacion);
            ContadorPendientes = _notificaciones.Count(n => !n.Leida);
            OnNotificacionAgregada?.Invoke(notificacion);
        }

        /// <summary>
        /// Obtiene todas las notificaciones pendientes para el usuario actual
        /// </summary>
        public static List<NotificacionDto> ObtenerNotificacionesPendientes()
        {
            return _notificaciones
                .Where(n => !n.Leida && n.UsuarioId == SessionManager.UsuarioActual?.operario)
                .OrderByDescending(n => n.FechaCreacion)
                .ToList();
        }

        /// <summary>
        /// Marca una notificación específica como leída
        /// </summary>
        public static void MarcarComoLeida(Guid idNotificacion)
        {
            var notificacion = _notificaciones.FirstOrDefault(n => n.Id == idNotificacion);
            if (notificacion != null && !notificacion.Leida)
            {
                notificacion.Leida = true;
                ContadorPendientes = _notificaciones.Count(n => !n.Leida);
            }
        }

        /// <summary>
        /// Marca todas las notificaciones pendientes como leídas
        /// </summary>
        public static void MarcarTodasComoLeidas()
        {
            foreach (var notificacion in _notificaciones.Where(n => !n.Leida))
            {
                notificacion.Leida = true;
            }
            ContadorPendientes = 0;
        }

        /// <summary>
        /// Método de ejemplo para crear notificaciones con información adicional del traspaso
        /// </summary>
        public static void CrearNotificacionTraspasoCompletado(string codigoArticulo, string descripcionArticulo, 
            string ubicacionOrigen, string ubicacionDestino, decimal cantidad, string unidad = "UD")
        {
            var notificacion = new NotificacionDto
            {
                Titulo = "Traspaso Completado",
                Mensaje = $"Traspaso de artículo {codigoArticulo} completado exitosamente",
                Tipo = "success",
                FechaCreacion = DateTime.UtcNow,
                UsuarioId = SessionManager.UsuarioActual?.operario ?? 0,
                CodigoArticulo = codigoArticulo,
                DescripcionArticulo = descripcionArticulo,
                UbicacionOrigen = ubicacionOrigen,
                UbicacionDestino = ubicacionDestino,
                Cantidad = cantidad,
                Unidad = unidad,
                TipoTraspaso = "ARTICULO"
            };

            AgregarNotificacion(notificacion);
        }

        /// <summary>
        /// Método de ejemplo para crear notificaciones de error con información adicional
        /// </summary>
        public static void CrearNotificacionTraspasoError(string codigoArticulo, string descripcionArticulo, 
            string ubicacionOrigen, string ubicacionDestino, decimal cantidad, string unidad = "UD", string motivo = "")
        {
            var notificacion = new NotificacionDto
            {
                Titulo = "Error en Traspaso",
                Mensaje = $"Error al trasladar artículo {codigoArticulo}" + (!string.IsNullOrEmpty(motivo) ? $": {motivo}" : ""),
                Tipo = "error",
                FechaCreacion = DateTime.UtcNow,
                UsuarioId = SessionManager.UsuarioActual?.operario ?? 0,
                CodigoArticulo = codigoArticulo,
                DescripcionArticulo = descripcionArticulo,
                UbicacionOrigen = ubicacionOrigen,
                UbicacionDestino = ubicacionDestino,
                Cantidad = cantidad,
                Unidad = unidad,
                TipoTraspaso = "ARTICULO"
            };

            AgregarNotificacion(notificacion);
        }
    }
}