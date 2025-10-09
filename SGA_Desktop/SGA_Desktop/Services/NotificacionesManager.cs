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
        private static ApiService? _apiService;
        private static readonly object _lock = new object();
        
        private static readonly string _apiUrl = "http://localhost:5234"; // URL del Hub según especificaciones

        // Lista de notificaciones gestionada por el manager
        private static List<NotificacionDto> _notificaciones = new();
        private static int _contadorPendientes = 0;
        private static bool _inicializado = false;

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
                if (_apiService == null)
                {
                    _apiService = new ApiService();
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
                if (_servicio == null || _apiService == null)
                {
                    Initialize(); // Asegurarse de que las instancias estén creadas
                }
            }

            if (SessionManager.UsuarioActual != null && SessionManager.UsuarioActual.operario > 0)
            {
                try
                {
                    // 1. Cargar notificaciones pendientes desde la base de datos
                    await CargarNotificacionesPendientesAsync();
                    
                    // 2. Conectar SignalR para notificaciones en tiempo real
                    if (_servicio != null)
                    {
                        await _servicio.ConectarAsync();
                    }
                    
                    _inicializado = true;
                    System.Diagnostics.Debug.WriteLine($"NotificacionesManager inicializado completamente para usuario {SessionManager.UsuarioActual.operario}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error al inicializar NotificacionesManager: {ex.Message}");
                    _logger?.LogError(ex, "Error al inicializar NotificacionesManager");
                    // No lanzar excepción para mantener funcionalidad básica
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"❌ NotificacionesManager: Usuario no logueado. UsuarioActual={SessionManager.UsuarioActual?.operario}");
                _logger?.LogWarning("No se pudo inicializar NotificacionesManager: Usuario no logueado o ID de operario inválido.");
            }
        }

        public static async Task DesconectarAsync()
        {
            if (_servicio != null)
            {
                await _servicio.DesconectarAsync();
                await ((IAsyncDisposable)_servicio).DisposeAsync();
                _servicio = null;
            }
            
            _notificaciones.Clear();
            ContadorPendientes = 0;
            _inicializado = false;
        }

        /// <summary>
        /// Carga las notificaciones pendientes desde la base de datos
        /// </summary>
        private static async Task CargarNotificacionesPendientesAsync()
        {
            if (_apiService == null || SessionManager.UsuarioActual == null)
            {
                System.Diagnostics.Debug.WriteLine("No se puede cargar notificaciones: ApiService o UsuarioActual es null");
                return;
            }

            try
            {
                var notificacionesApi = await _apiService.ObtenerNotificacionesPendientesAsync(SessionManager.UsuarioActual.operario);
                
                // Limpiar notificaciones existentes
                _notificaciones.Clear();
                
                // Convertir notificaciones de API a DTOs internos
                foreach (var notificacionApi in notificacionesApi)
                {
                    var notificacionDto = NotificacionConverter.ConvertirADesktopDto(notificacionApi);
                    
                    // Asegurar que el UsuarioId esté asignado correctamente
                    if (notificacionDto.UsuarioId == 0)
                    {
                        notificacionDto.UsuarioId = SessionManager.UsuarioActual.operario;
                    }
                    
                    _notificaciones.Add(notificacionDto);
                }
                
                // Actualizar contador
                ContadorPendientes = _notificaciones.Count(n => !n.Leida);
                
                System.Diagnostics.Debug.WriteLine($"Cargadas {notificacionesApi.Count} notificaciones desde BD. Pendientes: {ContadorPendientes}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar notificaciones desde BD: {ex.Message}");
                _logger?.LogError(ex, "Error al cargar notificaciones pendientes desde la base de datos");
                // Mantener funcionalidad básica aunque falle la carga
            }
        }

        /// <summary>
        /// Actualiza el contador de notificaciones desde la base de datos
        /// </summary>
        public static async Task ActualizarContadorAsync()
        {
            if (_apiService == null || SessionManager.UsuarioActual == null || !_inicializado)
            {
                return;
            }

            try
            {
                var contadorReal = await _apiService.ObtenerContadorPendientesAsync(SessionManager.UsuarioActual.operario);
                ContadorPendientes = contadorReal;
                System.Diagnostics.Debug.WriteLine($"🔄 Contador actualizado desde BD: {contadorReal}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error al actualizar contador desde BD: {ex.Message}");
                // Mantener contador local si falla la consulta
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
            // Las notificaciones que vienen de la API ya están filtradas por usuario en el servidor
            // Solo filtrar por no leídas
            return _notificaciones
                .Where(n => !n.Leida)
                .OrderByDescending(n => n.FechaCreacion)
                .ToList();
        }

        /// <summary>
        /// Marca una notificación específica como leída (local y en BD)
        /// </summary>
        public static async Task<bool> MarcarComoLeidaAsync(Guid idNotificacion)
        {
            var notificacion = _notificaciones.FirstOrDefault(n => n.Id == idNotificacion);
            if (notificacion == null || notificacion.Leida)
            {
                return false;
            }

            // Marcar como leída localmente primero
            notificacion.Leida = true;
            ContadorPendientes = _notificaciones.Count(n => !n.Leida);

            // Actualizar en la base de datos
            if (_apiService != null && SessionManager.UsuarioActual != null && _inicializado)
            {
                try
                {
                    var exito = await _apiService.MarcarComoLeidaAsync(idNotificacion, SessionManager.UsuarioActual.operario);
                    if (exito)
                    {
                        System.Diagnostics.Debug.WriteLine($"✅ Notificación {idNotificacion} marcada como leída en BD");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ No se pudo marcar notificación {idNotificacion} como leída en BD");
                    }
                    return exito;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Error al marcar notificación como leída en BD: {ex.Message}");
                    _logger?.LogError(ex, "Error al marcar notificación como leída en la base de datos");
                    // Mantener estado local aunque falle la actualización en BD
                    return false;
                }
            }

            return true; // Si no hay API service, al menos se marcó localmente
        }

        /// <summary>
        /// Marca una notificación específica como leída (método síncrono para compatibilidad)
        /// </summary>
        public static void MarcarComoLeida(Guid idNotificacion)
        {
            // Llamar al método asíncrono de forma síncrona
            Task.Run(async () => await MarcarComoLeidaAsync(idNotificacion));
        }

        /// <summary>
        /// Marca todas las notificaciones pendientes como leídas (local y en BD)
        /// </summary>
        public static async Task<int> MarcarTodasComoLeidasAsync()
        {
            var notificacionesPendientes = _notificaciones.Where(n => !n.Leida).ToList();
            if (!notificacionesPendientes.Any())
            {
                return 0;
            }

            // Marcar como leídas localmente primero
            foreach (var notificacion in notificacionesPendientes)
            {
                notificacion.Leida = true;
            }
            ContadorPendientes = 0;

            // Actualizar en la base de datos
            if (_apiService != null && SessionManager.UsuarioActual != null && _inicializado)
            {
                try
                {
                    var cantidadMarcadas = await _apiService.MarcarTodasComoLeidasAsync(SessionManager.UsuarioActual.operario);
                    System.Diagnostics.Debug.WriteLine($"✅ {cantidadMarcadas} notificaciones marcadas como leídas en BD");
                    return cantidadMarcadas;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Error al marcar todas las notificaciones como leídas en BD: {ex.Message}");
                    _logger?.LogError(ex, "Error al marcar todas las notificaciones como leídas en la base de datos");
                    // Mantener estado local aunque falle la actualización en BD
                    return notificacionesPendientes.Count;
                }
            }

            return notificacionesPendientes.Count; // Si no hay API service, al menos se marcaron localmente
        }

        /// <summary>
        /// Marca todas las notificaciones pendientes como leídas (método síncrono para compatibilidad)
        /// </summary>
        public static void MarcarTodasComoLeidas()
        {
            // Llamar al método asíncrono de forma síncrona
            Task.Run(async () => await MarcarTodasComoLeidasAsync());
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