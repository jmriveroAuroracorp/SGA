using SGA_Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGA_Api.Models.Notificaciones;
using SGA_Api.Models.UsuarioConf;

namespace SGA_Api.Services
{
    /// <summary>
    /// Clase para almacenar nombres de usuarios
    /// </summary>
    public class NombresUsuarios
    {
        public string CreadoPor { get; set; } = string.Empty;
        public string Supervisor { get; set; } = string.Empty;
        public string Operario { get; set; } = string.Empty;
    }

    /// <summary>
    /// Servicio para gestionar notificaciones específicas de conteos
    /// </summary>
    public class NotificacionesConteosService : INotificacionesConteosService
    {
        private readonly INotificacionesUnificadasService _notificacionesUnificadas;
        private readonly AuroraSgaDbContext _context;
        private readonly ILogger<NotificacionesConteosService> _logger;

        public NotificacionesConteosService(
            INotificacionesUnificadasService notificacionesUnificadas,
            AuroraSgaDbContext context,
            ILogger<NotificacionesConteosService> logger)
        {
            _notificacionesUnificadas = notificacionesUnificadas;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Notifica cuando se crea una nueva orden de conteo
        /// </summary>
        public async Task NotificarOrdenCreadaAsync(Guid ordenId, string titulo, string creadoPorCodigo, string? supervisorCodigo = null, 
            string? codigoAlmacen = null, string? alcance = null, string? codigoOperario = null, string? codigoUbicacion = null, string? codigoArticulo = null, byte prioridad = 3)
        {
            try
            {
                // Obtener nombres reales de usuarios
                var nombresUsuarios = await ObtenerNombresUsuariosAsync(creadoPorCodigo, supervisorCodigo, codigoOperario);
                
                // Mensaje enriquecido con información detallada
                var mensaje = $"Se ha creado una nueva orden de conteo: \"{titulo}\"\n" +
                            $"Creada por: {nombresUsuarios.CreadoPor}";
                
            // Información del almacén
            if (!string.IsNullOrEmpty(codigoAlmacen))
            {
                mensaje += $"\nAlmacén: {codigoAlmacen}";
            }
                
                // Tipo de conteo
                if (!string.IsNullOrEmpty(alcance))
                {
                    var tipoConteo = alcance switch
                    {
                        "ALMACEN" => "Conteo por Almacén",
                        "UBICACION" => "Conteo por Ubicación",
                        "ARTICULO" => "Conteo por Artículo",
                        "MULTIARTICULO" => "Conteo por Múltiples Artículos",
                        "PASILLO" => "Conteo por Pasillo",
                        "ZONA" => "Conteo por Zona",
                        _ => $"Conteo por {alcance}"
                    };
                    mensaje += $"\nTipo: {tipoConteo}";
                }
                
            // Información específica según el tipo de conteo
            if (!string.IsNullOrEmpty(alcance))
            {
                switch (alcance.ToUpper())
                {
                    case "PASILLO":
                        if (!string.IsNullOrEmpty(codigoUbicacion))
                        {
                            mensaje += $"\nPasillo: {codigoUbicacion}";
                        }
                        break;
                    case "UBICACION":
                        if (!string.IsNullOrEmpty(codigoUbicacion))
                        {
                            mensaje += $"\nUbicación: {codigoUbicacion}";
                        }
                        break;
                    case "ARTICULO":
                        if (!string.IsNullOrEmpty(codigoArticulo))
                        {
                            mensaje += $"\nArtículo: {codigoArticulo}";
                        }
                        break;
                    case "MULTIARTICULO":
                        // Obtener FiltrosJson de la orden para extraer múltiples artículos
                        var orden = await _context.OrdenesConteo
                            .FirstOrDefaultAsync(o => o.GuidID == ordenId);
                        if (orden != null && !string.IsNullOrEmpty(orden.FiltrosJson))
                        {
                            var codigosArticulos = ExtraerArticulosDelFiltro(orden.FiltrosJson);
                            if (codigosArticulos != null && codigosArticulos.Any())
                            {
                                mensaje += $"\nArtículos ({codigosArticulos.Count}): {string.Join(", ", codigosArticulos)}";
                            }
                        }
                        else if (!string.IsNullOrEmpty(codigoArticulo))
                        {
                            mensaje += $"\nArtículo: {codigoArticulo}";
                        }
                        break;
                    case "ZONA":
                        if (!string.IsNullOrEmpty(codigoUbicacion))
                        {
                            mensaje += $"\nZona: {codigoUbicacion}";
                        }
                        break;
                    default:
                        // Para otros tipos, mostrar ubicación si está disponible
                        if (!string.IsNullOrEmpty(codigoUbicacion))
                        {
                            mensaje += $"\nUbicación: {codigoUbicacion}";
                        }
                        if (!string.IsNullOrEmpty(codigoArticulo))
                        {
                            mensaje += $"\nArtículo: {codigoArticulo}";
                        }
                        break;
                }
            }
                
                // Operario asignado
                if (!string.IsNullOrEmpty(codigoOperario))
                {
                    mensaje += $"\nAsignado a: {nombresUsuarios.Operario}";
                }
                
            // Supervisor
            if (!string.IsNullOrEmpty(supervisorCodigo))
            {
                mensaje += $"\nSupervisor: {nombresUsuarios.Supervisor}";
            }
            
            // Prioridad
            var nivelPrioridad = prioridad switch
            {
                1 => "Muy Baja",
                2 => "Baja", 
                3 => "Normal",
                4 => "Alta",
                5 => "Crítica",
                _ => "Normal"
            };
            mensaje += $"\nPrioridad: {nivelPrioridad} ({prioridad}/5)";

                // Crear y enviar notificación unificada (BD + SignalR)
                await _notificacionesUnificadas.CrearYEnviarNotificacionRolesAsync(
                    new[] { "OPERARIO", "SUPERVISOR", "ADMIN" },
                    "ORDEN_CREADA",
                    "Nueva Orden de Conteo",
                    mensaje,
                    ordenId,
                    null,
                    null,
                    "info");

                _logger.LogInformation("Notificación de orden creada enviada para orden {OrdenId}", ordenId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de orden creada para {OrdenId}", ordenId);
            }
        }

        /// <summary>
        /// Notifica cuando se asigna un operario a una orden de conteo
        /// </summary>
        public async Task NotificarOperarioAsignadoAsync(Guid ordenId, string codigoOperario, string? supervisorCodigo = null)
        {
            try
            {
                var titulo = "Operario Asignado";
                var mensaje = $"El operario {codigoOperario} ha sido asignado a una orden de conteo";
                
                if (!string.IsNullOrEmpty(supervisorCodigo))
                {
                    mensaje += $"\nSupervisor responsable: {supervisorCodigo}";
                }

                // Crear y enviar notificación unificada (BD + SignalR)
                await _notificacionesUnificadas.CrearYEnviarNotificacionRolesAsync(
                    new[] { "SUPERVISOR", "ADMIN" },
                    "OPERARIO_ASIGNADO",
                    titulo,
                    mensaje,
                    ordenId,
                    null,
                    null,
                    "info");

                _logger.LogInformation("Notificación de operario asignado enviada para orden {OrdenId}", ordenId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de operario asignado para {OrdenId}", ordenId);
            }
        }

        /// <summary>
        /// Notifica cuando se inicia una orden de conteo
        /// </summary>
        public async Task NotificarOrdenIniciadaAsync(Guid ordenId, string codigoOperario, string? supervisorCodigo = null)
        {
            try
            {
                var titulo = "Conteo Iniciado";
                var mensaje = $"El operario {codigoOperario} ha comenzado a realizar el conteo";
                
                if (!string.IsNullOrEmpty(supervisorCodigo))
                {
                    mensaje += $"\nSupervisor responsable: {supervisorCodigo}";
                }

                // Crear y enviar notificación unificada (BD + SignalR)
                await _notificacionesUnificadas.CrearYEnviarNotificacionRolesAsync(
                    new[] { "SUPERVISOR", "ADMIN" },
                    "ORDEN_INICIADA",
                    titulo,
                    mensaje,
                    ordenId,
                    null,
                    null,
                    "info");

                _logger.LogInformation("Notificación de orden iniciada enviada para orden {OrdenId}", ordenId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de orden iniciada para {OrdenId}", ordenId);
            }
        }

        /// <summary>
        /// Notifica cuando se completa una orden de conteo
        /// </summary>
        public async Task NotificarOrdenCompletadaAsync(Guid ordenId, string codigoOperario, int totalLecturas, string? supervisorCodigo = null)
        {
            try
            {
                var titulo = "Conteo Completado";
                var mensaje = $"Operario {codigoOperario} ha completado la orden de conteo {ordenId} con {totalLecturas} lecturas";
                
                if (!string.IsNullOrEmpty(supervisorCodigo))
                {
                    mensaje += $" - Supervisor: {supervisorCodigo}";
                }

                // Crear y enviar notificación unificada (BD + SignalR)
                await _notificacionesUnificadas.CrearYEnviarNotificacionRolesAsync(
                    new[] { "SUPERVISOR", "ADMIN" },
                    "ORDEN_COMPLETADA",
                    titulo,
                    mensaje,
                    ordenId,
                    null,
                    null,
                    "success");

                _logger.LogInformation("Notificación de orden completada enviada para orden {OrdenId}", ordenId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de orden completada para {OrdenId}", ordenId);
            }
        }

        /// <summary>
        /// Notifica cuando se cierra una orden de conteo
        /// </summary>
        public async Task NotificarOrdenCerradaAsync(Guid ordenId, string? supervisorCodigo = null, int? totalResultados = null)
        {
            try
            {
                var titulo = "Conteo Cerrado";
                var mensaje = $"Orden de conteo {ordenId} ha sido cerrada";
                
                if (totalResultados.HasValue)
                {
                    mensaje += $" con {totalResultados} resultados generados";
                }
                
                if (!string.IsNullOrEmpty(supervisorCodigo))
                {
                    mensaje += $" - Supervisor: {supervisorCodigo}";
                }

                // Crear y enviar notificación unificada (BD + SignalR)
                await _notificacionesUnificadas.CrearYEnviarNotificacionRolesAsync(
                    new[] { "SUPERVISOR", "ADMIN" },
                    "ORDEN_CERRADA",
                    titulo,
                    mensaje,
                    ordenId,
                    null,
                    null,
                    "info");

                _logger.LogInformation("Notificación de orden cerrada enviada para orden {OrdenId}", ordenId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de orden cerrada para {OrdenId}", ordenId);
            }
        }

        /// <summary>
        /// Notifica cuando se crea una nueva lectura de conteo
        /// </summary>
        public async Task NotificarLecturaCreadaAsync(Guid ordenId, string codigoOperario, string codigoArticulo, decimal cantidad, string? supervisorCodigo = null)
        {
            try
            {
                var titulo = "Nueva Lectura de Conteo";
                var mensaje = $"Operario {codigoOperario} registró {cantidad} unidades del artículo {codigoArticulo}";
                
                if (!string.IsNullOrEmpty(supervisorCodigo))
                {
                    mensaje += $" - Supervisor: {supervisorCodigo}";
                }

                // Crear y enviar notificación unificada (BD + SignalR)
                await _notificacionesUnificadas.CrearYEnviarNotificacionRolesAsync(
                    new[] { "SUPERVISOR", "ADMIN" },
                    "LECTURA_CREADA",
                    titulo,
                    mensaje,
                    ordenId,
                    null,
                    null,
                    "info");

                _logger.LogInformation("Notificación de lectura creada enviada para orden {OrdenId}", ordenId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de lectura creada para {OrdenId}", ordenId);
            }
        }

        /// <summary>
        /// Notifica cuando se reasigna una línea de conteo
        /// </summary>
        public async Task NotificarLineaReasignadaAsync(Guid ordenId, string codigoArticulo, string nuevoOperario, string? supervisorCodigo = null)
        {
            try
            {
                var titulo = "Línea de Conteo Reasignada";
                var mensaje = $"Línea del artículo {codigoArticulo} reasignada al operario {nuevoOperario}";
                
                if (!string.IsNullOrEmpty(supervisorCodigo))
                {
                    mensaje += $" - Supervisor: {supervisorCodigo}";
                }

                // Crear y enviar notificación unificada (BD + SignalR)
                await _notificacionesUnificadas.CrearYEnviarNotificacionRolesAsync(
                    new[] { "SUPERVISOR", "ADMIN" },
                    "LINEA_REASIGNADA",
                    titulo,
                    mensaje,
                    ordenId,
                    null,
                    null,
                    "warning");

                _logger.LogInformation("Notificación de línea reasignada enviada para orden {OrdenId}", ordenId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de línea reasignada para {OrdenId}", ordenId);
            }
        }

        /// <summary>
        /// Notifica cuando se actualiza un aprobador de resultado de conteo
        /// </summary>
        public async Task NotificarAprobadorActualizadoAsync(Guid resultadoId, string codigoAprobador, string? supervisorCodigo = null)
        {
            try
            {
                var titulo = "Aprobador Actualizado";
                var mensaje = $"Aprobador {codigoAprobador} asignado al resultado de conteo";
                
                if (!string.IsNullOrEmpty(supervisorCodigo))
                {
                    mensaje += $" - Supervisor: {supervisorCodigo}";
                }

                // Crear y enviar notificación unificada (BD + SignalR)
                await _notificacionesUnificadas.CrearYEnviarNotificacionRolesAsync(
                    new[] { "SUPERVISOR", "ADMIN" },
                    "APROBADOR_ACTUALIZADO",
                    titulo,
                    mensaje,
                    resultadoId,
                    null,
                    null,
                    "info");

                _logger.LogInformation("Notificación de aprobador actualizado enviada para resultado {ResultadoId}", resultadoId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de aprobador actualizado para {ResultadoId}", resultadoId);
            }
        }

        /// <summary>
        /// Notifica eventos críticos que requieren atención inmediata
        /// </summary>
        public async Task NotificarEventoCriticoAsync(string tipoEvento, string titulo, string mensaje, object? datosAdicionales = null)
        {
            try
            {
                var roles = new List<string> { "ADMIN" };
                
                // También notificar a supervisores si es relevante
                if (tipoEvento.Contains("CONTEOS") || tipoEvento.Contains("INVENTARIO"))
                {
                    roles.Add("SUPERVISOR");
                }

                // Crear y enviar notificación unificada (BD + SignalR)
                await _notificacionesUnificadas.CrearYEnviarNotificacionRolesAsync(
                    roles.ToArray(),
                    tipoEvento,
                    titulo,
                    mensaje,
                    null,
                    null,
                    null,
                    roles.Contains("SUPERVISOR") ? "warning" : "error");

                _logger.LogInformation("Notificación de evento crítico enviada: {TipoEvento}", tipoEvento);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de evento crítico: {TipoEvento}", tipoEvento);
            }
        }

        /// <summary>
        /// Notifica cuando una orden de conteo se cancela
        /// </summary>
        public async Task NotificarOrdenCanceladaAsync(Guid ordenId, string motivo, string usuarioCodigo, string? supervisorCodigo = null)
        {
            try
            {
                var titulo = "Orden de Conteo Cancelada";
                var mensaje = $"Orden {ordenId} cancelada por {usuarioCodigo}. Motivo: {motivo}";
                
                if (!string.IsNullOrEmpty(supervisorCodigo))
                {
                    mensaje += $" - Supervisor: {supervisorCodigo}";
                }

                // Crear y enviar notificación unificada (BD + SignalR)
                await _notificacionesUnificadas.CrearYEnviarNotificacionRolesAsync(
                    new[] { "SUPERVISOR", "ADMIN" },
                    "ORDEN_CANCELADA",
                    titulo,
                    mensaje,
                    ordenId,
                    null,
                    null,
                    "warning");

                _logger.LogInformation("Notificación de orden cancelada enviada para orden {OrdenId}", ordenId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de orden cancelada para {OrdenId}", ordenId);
            }
        }

        /// <summary>
        /// Notifica cuando un conteo se envía a supervisión
        /// </summary>
        public async Task NotificarConteoSupervisionAsync(Guid resultadoGuid, string codigoArticulo, decimal cantidad, string operarioCodigo, string? supervisorCodigo = null)
        {
            try
            {
                var titulo = "Conteo Enviado a Supervisión";
                var mensaje = $"Conteo del artículo {codigoArticulo} (Cantidad: {cantidad}) enviado a supervisión por {operarioCodigo}";
                
                if (!string.IsNullOrEmpty(supervisorCodigo))
                {
                    mensaje += $" - Supervisor: {supervisorCodigo}";
                }

                // Crear y enviar notificación unificada (BD + SignalR)
                await _notificacionesUnificadas.CrearYEnviarNotificacionRolesAsync(
                    new[] { "SUPERVISOR", "ADMIN" },
                    "CONTEO_SUPERVISION",
                    titulo,
                    mensaje,
                    resultadoGuid,
                    null,
                    null,
                    "warning");

                _logger.LogInformation("Notificación de conteo en supervisión enviada para resultado {ResultadoGuid}", resultadoGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de conteo en supervisión para {ResultadoGuid}", resultadoGuid);
            }
        }

        /// <summary>
        /// Convierte IdRol a nombre de rol
        /// </summary>
        private string GetRolNombre(int? idRol)
        {
            return idRol switch
            {
                10 => "OPERARIO",
                20 => "SUPERVISOR",
                30 => "ADMIN",
                _ => "OPERARIO"
            };
        }

        /// <summary>
        /// Obtiene los nombres reales de los usuarios desde la vista vUsuariosConNombre
        /// </summary>
        private async Task<NombresUsuarios> ObtenerNombresUsuariosAsync(string creadoPorCodigo, string? supervisorCodigo, string? codigoOperario)
        {
            var nombres = new NombresUsuarios
            {
                CreadoPor = creadoPorCodigo, // Por defecto usar el código
                Supervisor = supervisorCodigo ?? "No asignado",
                Operario = codigoOperario ?? "No asignado"
            };

            try
            {
                // Obtener todos los códigos que necesitamos buscar
                var codigos = new List<string>();
                if (!string.IsNullOrEmpty(creadoPorCodigo)) codigos.Add(creadoPorCodigo);
                if (!string.IsNullOrEmpty(supervisorCodigo)) codigos.Add(supervisorCodigo);
                if (!string.IsNullOrEmpty(codigoOperario)) codigos.Add(codigoOperario);

                if (codigos.Any())
                {
                    // Buscar en la vista vUsuariosConNombre
                    var usuarios = await _context.vUsuariosConNombre
                        .Where(u => codigos.Contains(u.UsuarioId.ToString()))
                        .ToListAsync();

                    // Mapear los nombres encontrados
                    foreach (var usuario in usuarios)
                    {
                        var codigo = usuario.UsuarioId.ToString();
                        if (codigo == creadoPorCodigo)
                            nombres.CreadoPor = usuario.NombreOperario;
                        if (codigo == supervisorCodigo)
                            nombres.Supervisor = usuario.NombreOperario;
                        if (codigo == codigoOperario)
                            nombres.Operario = usuario.NombreOperario;
                    }
                }

                _logger.LogDebug("Nombres obtenidos - CreadoPor: {CreadoPor}, Supervisor: {Supervisor}, Operario: {Operario}", 
                    nombres.CreadoPor, nombres.Supervisor, nombres.Operario);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al obtener nombres de usuarios, usando códigos por defecto");
            }

            return nombres;
        }

        /// <summary>
        /// Notifica cuando cambia el estado de una orden de conteo
        /// </summary>
        public async Task NotificarCambioEstadoAsync(Guid ordenId, string estadoAnterior, string estadoNuevo, string? codigoOperario = null, string? supervisorCodigo = null)
        {
            try
            {
                // Obtener nombres reales de usuarios
                var nombresUsuarios = await ObtenerNombresUsuariosAsync(codigoOperario ?? "", supervisorCodigo, null);
                
                // Determinar el tipo de notificación y mensaje según el cambio de estado
                var (titulo, mensaje, tipoVisual) = DeterminarNotificacionCambioEstado(estadoAnterior, estadoNuevo, nombresUsuarios);
                
                // Crear y enviar notificación unificada (BD + SignalR)
                var tipoEvento = $"ESTADO_{estadoNuevo.ToUpper()}";
                await _notificacionesUnificadas.CrearYEnviarNotificacionRolesAsync(
                    new[] { "OPERARIO", "SUPERVISOR", "ADMIN" },
                    tipoEvento,
                    titulo,
                    mensaje,
                    ordenId,
                    estadoAnterior,
                    estadoNuevo,
                    tipoVisual);

                _logger.LogInformation("Notificación de cambio de estado enviada para orden {OrdenId}: {EstadoAnterior} → {EstadoNuevo}", 
                    ordenId, estadoAnterior, estadoNuevo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de cambio de estado para orden {OrdenId}", ordenId);
            }
        }

        /// <summary>
        /// Determina el tipo de notificación y mensaje según el cambio de estado
        /// </summary>
        private (string titulo, string mensaje, string tipoVisual) DeterminarNotificacionCambioEstado(string estadoAnterior, string estadoNuevo, NombresUsuarios nombresUsuarios)
        {
            var operario = !string.IsNullOrEmpty(nombresUsuarios.Operario) ? nombresUsuarios.Operario : "Sistema";
            
            return (estadoAnterior, estadoNuevo) switch
            {
                (_, "ASIGNADO") => (
                    "Orden Asignada",
                    $"La orden de conteo ha sido asignada a {operario}",
                    "info"
                ),
                (_, "EN_PROGRESO") => (
                    "Orden Iniciada", 
                    $"La orden de conteo ha sido iniciada por {operario}",
                    "info"
                ),
                (_, "COMPLETADO") => (
                    "Orden Completada",
                    $"La orden de conteo ha sido completada por {operario}",
                    "success"
                ),
                (_, "CERRADO") => (
                    "Orden Cerrada",
                    $"La orden de conteo ha sido cerrada",
                    "success"
                ),
                (_, "CANCELADO") => (
                    "Orden Cancelada",
                    $"La orden de conteo ha sido cancelada por {operario}",
                    "error"
                ),
                _ => (
                    "Estado Actualizado",
                    $"El estado de la orden ha cambiado de {estadoAnterior} a {estadoNuevo}",
                    "info"
                )
            };
        }

        /// <summary>
        /// Extrae lista de artículos del FiltrosJson (soporta formato nuevo y antiguo)
        /// </summary>
        private List<string>? ExtraerArticulosDelFiltro(string? filtrosJson)
        {
            if (string.IsNullOrEmpty(filtrosJson)) return null;
            try
            {
                var filtros = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(filtrosJson);
                
                // Priorizar formato nuevo: array de artículos
                if (filtros?.ContainsKey("articulos") == true)
                {
                    var articulos = filtros["articulos"];
                    if (articulos.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        return articulos.EnumerateArray()
                            .Select(x => x.GetString())
                            .Where(x => !string.IsNullOrEmpty(x))
                            .ToList();
                    }
                }
                
                // Compatibilidad: formato antiguo con un solo artículo
                if (filtros?.ContainsKey("articulo") == true)
                {
                    var articulo = filtros["articulo"];
                    if (articulo.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var codigoArticulo = articulo.GetString();
                        if (!string.IsNullOrEmpty(codigoArticulo))
                        {
                            return new List<string> { codigoArticulo };
                        }
                    }
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }

    }
}
