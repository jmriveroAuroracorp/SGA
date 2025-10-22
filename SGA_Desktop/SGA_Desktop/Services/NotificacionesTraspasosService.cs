using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using SGA_Desktop.Helpers;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace SGA_Desktop.Services
{
    /// <summary>
    /// Servicio para manejar notificaciones de traspasos en tiempo real a través de SignalR
    /// </summary>
    public class NotificacionesTraspasosService : IAsyncDisposable
    {
        private HubConnection? _connection;
        private readonly string _apiUrl;
        private readonly ILogger<NotificacionesTraspasosService>? _logger;
        private bool _estaConectando = false;
        private bool _debeReconectar = true;

        // Eventos públicos
        public event EventHandler<NotificacionUsuarioEventArgs>? NotificacionRecibida;
        public event EventHandler<string>? EstadoConexionCambiado;
        public event EventHandler<Exception>? ErrorConexion;

        public bool EstaConectado => _connection?.State == HubConnectionState.Connected;
        public bool EstaConectando => _estaConectando;

        public NotificacionesTraspasosService(string apiUrl, ILogger<NotificacionesTraspasosService>? logger = null)
        {
            _apiUrl = apiUrl.TrimEnd('/');
            _logger = logger;
        }

        /// <summary>
        /// Inicia la conexión con el Hub de notificaciones
        /// </summary>
        public async Task ConectarAsync()
        {
            // Si la aplicación se está cerrando, no conectar
            if (SessionManager.IsClosing)
            {
                _logger?.LogInformation("Aplicación cerrándose, no conectar SignalR");
                return;
            }

            if (_estaConectando || EstaConectado)
            {
                _logger?.LogInformation("Ya está conectado o conectándose. Estado: {Estado}", _connection?.State);
                return;
            }

            try
            {
                _estaConectando = true;
                _debeReconectar = true;
                
                _logger?.LogInformation("Iniciando conexión a SignalR Hub: {Url}", $"{_apiUrl}/notificacionesTraspasosHub");

                // Obtener token de autenticación
                var token = SessionManager.Token;
                if (string.IsNullOrEmpty(token))
                {
                    _logger?.LogWarning("No hay token de sesión disponible. No se puede conectar a SignalR.");
                    EstadoConexionCambiado?.Invoke(this, "Sin token de sesión");
                    return;
                }

                // Obtener ID del usuario actual
                var usuarioId = SessionManager.UsuarioActual?.operario ?? 0;
                if (usuarioId <= 0)
                {
                    _logger?.LogWarning("No hay usuario actual válido. No se puede unir al grupo de notificaciones.");
                    EstadoConexionCambiado?.Invoke(this, "Usuario no válido");
                    return;
                }

                // Configurar conexión
                _connection = new HubConnectionBuilder()
                    .WithUrl($"{_apiUrl}/notificacionesTraspasosHub", options =>
                    {
                        // Agregar token de autenticación
                        options.Headers.Add("Authorization", $"Bearer {token}");
                    })
                    .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) })
                    .Build();

                // Configurar eventos
                _connection.On<object>("NotificacionUsuario", OnNotificacionRecibida);
                _connection.On<object>("NotificacionRol", OnNotificacionRecibida);
                _connection.On<object>("NotificacionNivelJerarquico", OnNotificacionRecibida);
                
                _connection.Closed += OnConexionCerrada;
                _connection.Reconnecting += OnReconectando;
                _connection.Reconnected += OnReconectado;

                // Conectar
                await _connection.StartAsync();
                
                _logger?.LogInformation("✅ Conectado exitosamente a SignalR Hub");

                // Unirse al grupo del usuario
                await UnirseAGrupoUsuarioAsync(usuarioId);
                
                // Unirse automáticamente a grupos de rol (el servidor lo hace automáticamente)
                // pero podemos agregar lógica adicional aquí si es necesario
                
                EstadoConexionCambiado?.Invoke(this, "Conectado");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ Error al conectar con SignalR Hub");
                ErrorConexion?.Invoke(this, ex);
                EstadoConexionCambiado?.Invoke(this, $"Error: {ex.Message}");
            }
            finally
            {
                _estaConectando = false;
            }
        }

        /// <summary>
        /// Desconecta del Hub de notificaciones
        /// </summary>
        public async Task DesconectarAsync()
        {
            try
            {
                _debeReconectar = false;
                
                if (_connection != null)
                {
                    _logger?.LogInformation("Desconectando de SignalR Hub");
                    
                    // Salir del grupo del usuario antes de desconectar
                    var usuarioId = SessionManager.UsuarioActual?.operario ?? 0;
                    if (usuarioId > 0)
                    {
                        try
                        {
                            await SalirDeGrupoUsuarioAsync(usuarioId);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Error al salir del grupo de usuario durante desconexión");
                        }
                    }
                    
                    try
                    {
                        await _connection.DisposeAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Error al dispose de la conexión SignalR");
                    }
                    finally
                    {
                        _connection = null;
                    }
                    
                    _logger?.LogInformation("✅ Desconectado de SignalR Hub");
                    EstadoConexionCambiado?.Invoke(this, "Desconectado");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error al desconectar de SignalR Hub");
                // No invocar ErrorConexion durante el cierre para evitar excepciones adicionales
                if (!SessionManager.IsClosing)
                {
                    ErrorConexion?.Invoke(this, ex);
                }
            }
        }

        /// <summary>
        /// Se une al grupo unipersonal del usuario para recibir notificaciones específicas
        /// </summary>
        private async Task UnirseAGrupoUsuarioAsync(int usuarioId)
        {
            if (_connection?.State != HubConnectionState.Connected)
            {
                _logger?.LogWarning("No se puede unir al grupo: conexión no disponible. Estado: {Estado}", _connection?.State);
                return;
            }

            try
            {
                await _connection.InvokeAsync("UnirseAGrupoUsuario", usuarioId);
                _logger?.LogInformation("✅ Unido al grupo de usuario: Usuario_{UsuarioId}", usuarioId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error al unirse al grupo de usuario: Usuario_{UsuarioId}", usuarioId);
                ErrorConexion?.Invoke(this, ex);
            }
        }

        /// <summary>
        /// Sale del grupo unipersonal del usuario
        /// </summary>
        private async Task SalirDeGrupoUsuarioAsync(int usuarioId)
        {
            if (_connection?.State != HubConnectionState.Connected)
            {
                return;
            }

            try
            {
                await _connection.InvokeAsync("SalirDeGrupoUsuario", usuarioId);
                _logger?.LogInformation("✅ Salido del grupo de usuario: Usuario_{UsuarioId}", usuarioId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error al salir del grupo de usuario: Usuario_{UsuarioId}", usuarioId);
            }
        }

        /// <summary>
        /// Maneja las notificaciones recibidas del Hub
        /// </summary>
        private void OnNotificacionRecibida(object notificacion)
        {
            try
            {
                _logger?.LogInformation("📨 Notificación recibida: {Notificacion}", notificacion);

                // Deserializar la notificación
                var notificacionData = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(notificacion.ToString() ?? "");
                
                var args = new NotificacionUsuarioEventArgs
                {
                    TipoNotificacion = notificacionData?.tipoNotificacion?.ToString() ?? "",
                    Titulo = notificacionData?.titulo?.ToString(),
                    Mensaje = notificacionData?.mensaje?.ToString(),
                    TipoPopup = notificacionData?.tipoPopup?.ToString(),
                    Timestamp = DateTime.UtcNow,
                    DatosCompletos = notificacion
                };

                _logger?.LogInformation("📋 Notificación procesada - Tipo: {Tipo}, Título: {Titulo}", 
                    args.TipoNotificacion, args.Titulo);

                // Invocar en el hilo de UI
                Application.Current.Dispatcher.Invoke(() =>
                {
                    NotificacionRecibida?.Invoke(this, args);
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error al procesar notificación recibida");
                ErrorConexion?.Invoke(this, ex);
            }
        }

        /// <summary>
        /// Maneja el cierre de la conexión
        /// </summary>
        private async Task OnConexionCerrada(Exception? exception)
        {
            if (exception != null)
            {
                _logger?.LogWarning("Conexión SignalR cerrada con error: {Error}", exception.Message);
                // Solo invocar ErrorConexion si la aplicación no se está cerrando
                if (!SessionManager.IsClosing)
                {
                    ErrorConexion?.Invoke(this, exception);
                }
            }
            else
            {
                _logger?.LogInformation("Conexión SignalR cerrada correctamente");
            }

            EstadoConexionCambiado?.Invoke(this, "Desconectado");

            // Intentar reconectar automáticamente solo si la aplicación no se está cerrando
            if (_debeReconectar && !_estaConectando && !SessionManager.IsClosing)
            {
                _logger?.LogInformation("Intentando reconectar en 5 segundos...");
                await Task.Delay(5000);
                
                // Verificar nuevamente si la aplicación se está cerrando antes de reconectar
                if (_debeReconectar && !SessionManager.IsClosing)
                {
                    await ConectarAsync();
                }
            }
        }

        /// <summary>
        /// Maneja la reconexión automática
        /// </summary>
        private async Task OnReconectando(Exception? exception)
        {
            // No intentar reconectar si la aplicación se está cerrando
            if (SessionManager.IsClosing)
            {
                _logger?.LogInformation("Aplicación cerrándose, cancelando reconexión SignalR");
                return;
            }

            _logger?.LogWarning("Reconectando a SignalR Hub: {Error}", exception?.Message);
            EstadoConexionCambiado?.Invoke(this, "Reconectando...");
            
            // Volver a unirse al grupo del usuario después de reconectar
            await Task.Delay(1000); // Pequeño delay para asegurar la reconexión
            
            // Verificar nuevamente si la aplicación se está cerrando antes de unirse al grupo
            if (!SessionManager.IsClosing)
            {
                var usuarioId = SessionManager.UsuarioActual?.operario ?? 0;
                if (usuarioId > 0)
                {
                    await UnirseAGrupoUsuarioAsync(usuarioId);
                }
            }
        }

        /// <summary>
        /// Maneja la reconexión exitosa
        /// </summary>
        private async Task OnReconectado(string? connectionId)
        {
            // No procesar reconexión si la aplicación se está cerrando
            if (SessionManager.IsClosing)
            {
                _logger?.LogInformation("Aplicación cerrándose, cancelando procesamiento de reconexión SignalR");
                return;
            }

            _logger?.LogInformation("✅ Reconectado exitosamente a SignalR Hub. ConnectionId: {ConnectionId}", connectionId);
            EstadoConexionCambiado?.Invoke(this, "Reconectado");
            
            // Volver a unirse al grupo del usuario solo si la aplicación no se está cerrando
            if (!SessionManager.IsClosing)
            {
                var usuarioId = SessionManager.UsuarioActual?.operario ?? 0;
                if (usuarioId > 0)
                {
                    await UnirseAGrupoUsuarioAsync(usuarioId);
                }
            }
        }

        /// <summary>
        /// Reinicia la conexión (útil para cambios de usuario)
        /// </summary>
        public async Task ReiniciarConexionAsync()
        {
            _logger?.LogInformation("🔄 Reiniciando conexión SignalR");
            await DesconectarAsync();
            await Task.Delay(1000);
            await ConectarAsync();
        }

        /// <summary>
        /// Libera recursos
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await DesconectarAsync();
        }
    }
}
