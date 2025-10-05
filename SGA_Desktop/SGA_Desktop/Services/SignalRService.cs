using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System.Windows;

namespace SGA_Desktop.Services
{
    /// <summary>
    /// Servicio para manejar la conexión SignalR desde la aplicación desktop
    /// </summary>
    public class SignalRService
    {
        private HubConnection? _connection;
        private readonly string _baseUrl;
        private readonly ILogger<SignalRService>? _logger;
        private string? _sessionToken;

        public event EventHandler<NotificacionTraspasoEventArgs>? NotificacionRecibida;
        public event EventHandler<NotificacionUsuarioEventArgs>? NotificacionUsuarioRecibida;
        public event EventHandler<string>? ConexionCambiada;

        public bool EstaConectado => _connection?.State == HubConnectionState.Connected;

        public SignalRService(string baseUrl, ILogger<SignalRService>? logger = null)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _logger = logger;
        }

        /// <summary>
        /// Establece el token de sesión para autenticación
        /// </summary>
        public void SetSessionToken(string token)
        {
            _sessionToken = token;
        }

        /// <summary>
        /// Inicia la conexión con el Hub de SignalR
        /// </summary>
        public async Task ConectarAsync()
        {
            try
            {
                if (_connection != null)
                {
                    await DesconectarAsync();
                }

                _connection = new HubConnectionBuilder()
                    .WithUrl($"{_baseUrl}/notificacionesTraspasosHub", options =>
                    {
                        // Usar el mismo token que el resto de la aplicación
                        if (!string.IsNullOrEmpty(_sessionToken))
                        {
                            options.Headers.Add("Authorization", $"Bearer {_sessionToken}");
                        }
                    })
                    .WithAutomaticReconnect()
                    .Build();

                // Configurar eventos
                _connection.On<object>("NotificacionTraspaso", OnNotificacionRecibida);
                _connection.On<object>("NotificacionUsuario", OnNotificacionUsuarioRecibida);
                
                _connection.Closed += OnConexionCerrada;
                _connection.Reconnected += OnReconectado;
                _connection.Reconnecting += OnReconectando;

                await _connection.StartAsync();
                
                _logger?.LogInformation("Conectado al Hub SignalR: {Url}", _baseUrl);
                ConexionCambiada?.Invoke(this, "Conectado");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error al conectar con SignalR");
                ConexionCambiada?.Invoke(this, $"Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Desconecta del Hub SignalR
        /// </summary>
        public async Task DesconectarAsync()
        {
            if (_connection != null)
            {
                await _connection.DisposeAsync();
                _connection = null;
                _logger?.LogInformation("Desconectado del Hub SignalR");
                ConexionCambiada?.Invoke(this, "Desconectado");
            }
        }

        /// <summary>
        /// Se une a un grupo de traspaso específico
        /// </summary>
        public async Task UnirseAGrupoTraspasoAsync(int traspasoId)
        {
            if (_connection?.State == HubConnectionState.Connected)
            {
                try
                {
                    await _connection.InvokeAsync("UnirseAGrupoTraspaso", traspasoId.ToString());
                    _logger?.LogInformation("Unido al grupo de traspaso: {TraspasoId}", traspasoId);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error al unirse al grupo de traspaso: {TraspasoId}", traspasoId);
                    throw;
                }
            }
            else
            {
                throw new InvalidOperationException("No hay conexión activa con SignalR");
            }
        }

        /// <summary>
        /// Sale de un grupo de traspaso específico
        /// </summary>
        public async Task SalirDeGrupoTraspasoAsync(int traspasoId)
        {
            if (_connection?.State == HubConnectionState.Connected)
            {
                try
                {
                    await _connection.InvokeAsync("SalirDeGrupoTraspaso", traspasoId.ToString());
                    _logger?.LogInformation("Salido del grupo de traspaso: {TraspasoId}", traspasoId);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error al salir del grupo de traspaso: {TraspasoId}", traspasoId);
                    throw;
                }
            }
        }

        /// <summary>
        /// Se une a un grupo de usuario específico (para notificaciones unipersonales)
        /// </summary>
        public async Task UnirseAGrupoUsuarioAsync(int usuarioId)
        {
            if (_connection?.State == HubConnectionState.Connected)
            {
                try
                {
                    await _connection.InvokeAsync("UnirseAGrupoUsuario", usuarioId);
                    _logger?.LogInformation("Unido al grupo de usuario: {UsuarioId}", usuarioId);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error al unirse al grupo de usuario: {UsuarioId}", usuarioId);
                    throw;
                }
            }
        }

        /// <summary>
        /// Sale de un grupo de usuario específico
        /// </summary>
        public async Task SalirDeGrupoUsuarioAsync(int usuarioId)
        {
            if (_connection?.State == HubConnectionState.Connected)
            {
                try
                {
                    await _connection.InvokeAsync("SalirDeGrupoUsuario", usuarioId);
                    _logger?.LogInformation("Salido del grupo de usuario: {UsuarioId}", usuarioId);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error al salir del grupo de usuario: {UsuarioId}", usuarioId);
                    throw;
                }
            }
        }

        private void OnNotificacionRecibida(object notificacion)
        {
            try
            {
                // Convertir el objeto recibido a un tipo más manejable
                var notificacionData = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(notificacion.ToString() ?? "");
                
                var args = new NotificacionTraspasoEventArgs
                {
                    TraspasoId = notificacionData?.traspasoId ?? 0,
                    TipoNotificacion = notificacionData?.tipoNotificacion?.ToString() ?? "",
                    NuevoEstado = notificacionData?.nuevoEstado?.ToString(),
                    TipoActualizacion = notificacionData?.tipoActualizacion?.ToString(),
                    Mensaje = notificacionData?.mensaje?.ToString(),
                    Timestamp = DateTime.UtcNow,
                    DatosCompletos = notificacion
                };

                _logger?.LogInformation("Notificación recibida para traspaso {TraspasoId}: {Tipo}", 
                    args.TraspasoId, args.TipoNotificacion);

                // Invocar en el hilo de UI
                Application.Current.Dispatcher.Invoke(() =>
                {
                    NotificacionRecibida?.Invoke(this, args);
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error al procesar notificación SignalR");
            }
        }

        private void OnNotificacionUsuarioRecibida(object notificacion)
        {
            try
            {
                // Convertir el objeto recibido a un tipo más manejable
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

                _logger?.LogInformation("Notificación de usuario recibida: {Tipo} - {Titulo}", 
                    args.TipoNotificacion, args.Titulo);

                // Invocar en el hilo de UI
                Application.Current.Dispatcher.Invoke(() =>
                {
                    NotificacionUsuarioRecibida?.Invoke(this, args);
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error al procesar notificación de usuario SignalR");
            }
        }

        private async Task OnConexionCerrada(Exception? exception)
        {
            _logger?.LogWarning("Conexión SignalR cerrada: {Error}", exception?.Message);
            ConexionCambiada?.Invoke(this, "Desconectado");
            
            // Intentar reconectar después de un delay
            if (exception != null)
            {
                await Task.Delay(5000);
                try
                {
                    await ConectarAsync();
                }
                catch
                {
                    // Ignorar errores de reconexión automática
                }
            }
        }

        private async Task OnReconectado(string? connectionId)
        {
            _logger?.LogInformation("SignalR reconectado con ID: {ConnectionId}", connectionId);
            ConexionCambiada?.Invoke(this, "Reconectado");
        }

        private async Task OnReconectando(Exception? exception)
        {
            _logger?.LogWarning("Intentando reconectar SignalR: {Error}", exception?.Message);
            ConexionCambiada?.Invoke(this, "Reconectando...");
        }

        public void Dispose()
        {
            _connection?.DisposeAsync();
        }
    }

    /// <summary>
    /// Event args para notificaciones de traspasos
    /// </summary>
    public class NotificacionTraspasoEventArgs : EventArgs
    {
        public int TraspasoId { get; set; }
        public string TipoNotificacion { get; set; } = string.Empty;
        public string? NuevoEstado { get; set; }
        public string? TipoActualizacion { get; set; }
        public string? Mensaje { get; set; }
        public DateTime Timestamp { get; set; }
        public object? DatosCompletos { get; set; }
    }

    /// <summary>
    /// Event args para notificaciones de usuario (popups, mensajes personales)
    /// </summary>
    public class NotificacionUsuarioEventArgs : EventArgs
    {
        public string TipoNotificacion { get; set; } = string.Empty;
        public string? Titulo { get; set; }
        public string? Mensaje { get; set; }
        public string? TipoPopup { get; set; } // info, warning, error, success
        public DateTime Timestamp { get; set; }
        public object? DatosCompletos { get; set; }
    }
}
