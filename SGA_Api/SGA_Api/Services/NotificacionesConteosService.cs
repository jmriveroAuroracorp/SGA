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
        private readonly INotificacionesTraspasosService _notificacionesService;
        private readonly INotificacionesService _notificacionesBdService;
        private readonly AuroraSgaDbContext _context;
        private readonly ILogger<NotificacionesConteosService> _logger;

        public NotificacionesConteosService(
            INotificacionesTraspasosService notificacionesService,
            INotificacionesService notificacionesBdService,
            AuroraSgaDbContext context,
            ILogger<NotificacionesConteosService> logger)
        {
            _notificacionesService = notificacionesService;
            _notificacionesBdService = notificacionesBdService;
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

                // 1. Enviar por SignalR a operarios, supervisores y administradores
                await _notificacionesService.NotificarRolAsync("OPERARIO", "Nueva Orden de Conteo", mensaje, "info");
                await _notificacionesService.NotificarRolAsync("SUPERVISOR", "Nueva Orden de Conteo", mensaje, "info");
                await _notificacionesService.NotificarRolAsync("ADMIN", "Nueva Orden de Conteo", mensaje, "info");

                // 2. Guardar en la base de datos para usuarios con rol OPERARIO, SUPERVISOR y ADMIN
                await GuardarNotificacionEnBdAsync("ORDEN_CREADA", ordenId, "Nueva Orden de Conteo", mensaje, new[] { "OPERARIO", "SUPERVISOR", "ADMIN" });

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

                // Notificar a supervisores y administradores
                await _notificacionesService.NotificarRolAsync("SUPERVISOR", titulo, mensaje, "info");
                await _notificacionesService.NotificarRolAsync("ADMIN", titulo, mensaje, "info");

                // Guardar en la base de datos
                await GuardarNotificacionEnBdAsync("OPERARIO_ASIGNADO", ordenId, titulo, mensaje, new[] { "SUPERVISOR", "ADMIN" });

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

                // Notificar a supervisores y administradores
                await _notificacionesService.NotificarRolAsync("SUPERVISOR", titulo, mensaje, "info");
                await _notificacionesService.NotificarRolAsync("ADMIN", titulo, mensaje, "info");

                // Guardar en la base de datos
                await GuardarNotificacionEnBdAsync("ORDEN_INICIADA", ordenId, titulo, mensaje, new[] { "SUPERVISOR", "ADMIN" });

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

                // Notificar a supervisores y administradores
                await _notificacionesService.NotificarRolAsync("SUPERVISOR", titulo, mensaje, "success");
                await _notificacionesService.NotificarRolAsync("ADMIN", titulo, mensaje, "success");

                // Guardar en la base de datos
                await GuardarNotificacionEnBdAsync("ORDEN_COMPLETADA", ordenId, titulo, mensaje, new[] { "SUPERVISOR", "ADMIN" });

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

                // Notificar a supervisores y administradores
                await _notificacionesService.NotificarRolAsync("SUPERVISOR", titulo, mensaje, "info");
                await _notificacionesService.NotificarRolAsync("ADMIN", titulo, mensaje, "info");

                // Guardar en la base de datos
                await GuardarNotificacionEnBdAsync("ORDEN_CERRADA", ordenId, titulo, mensaje, new[] { "SUPERVISOR", "ADMIN" });

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

                // Notificar a supervisores y administradores
                await _notificacionesService.NotificarRolAsync("SUPERVISOR", titulo, mensaje, "info");
                await _notificacionesService.NotificarRolAsync("ADMIN", titulo, mensaje, "info");

                // Guardar en la base de datos
                await GuardarNotificacionEnBdAsync("LECTURA_CREADA", ordenId, titulo, mensaje, new[] { "SUPERVISOR", "ADMIN" });

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

                // Notificar a supervisores y administradores
                await _notificacionesService.NotificarRolAsync("SUPERVISOR", titulo, mensaje, "warning");
                await _notificacionesService.NotificarRolAsync("ADMIN", titulo, mensaje, "warning");

                // Guardar en la base de datos
                await GuardarNotificacionEnBdAsync("LINEA_REASIGNADA", ordenId, titulo, mensaje, new[] { "SUPERVISOR", "ADMIN" });

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

                // Notificar a supervisores y administradores
                await _notificacionesService.NotificarRolAsync("SUPERVISOR", titulo, mensaje, "info");
                await _notificacionesService.NotificarRolAsync("ADMIN", titulo, mensaje, "info");

                // Guardar en la base de datos
                await GuardarNotificacionEnBdAsync("APROBADOR_ACTUALIZADO", resultadoId, titulo, mensaje, new[] { "SUPERVISOR", "ADMIN" });

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
                // Notificar a administradores con prioridad alta
                await _notificacionesService.NotificarRolAsync("ADMIN", titulo, mensaje, "error");
                
                // También notificar a supervisores si es relevante
                if (tipoEvento.Contains("CONTEOS") || tipoEvento.Contains("INVENTARIO"))
                {
                    await _notificacionesService.NotificarRolAsync("SUPERVISOR", titulo, mensaje, "warning");
                }

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

                // Notificar a supervisores y administradores
                await _notificacionesService.NotificarRolAsync("SUPERVISOR", titulo, mensaje, "warning");
                await _notificacionesService.NotificarRolAsync("ADMIN", titulo, mensaje, "warning");

                // Guardar en la base de datos
                await GuardarNotificacionEnBdAsync("ORDEN_CANCELADA", ordenId, titulo, mensaje, new[] { "SUPERVISOR", "ADMIN" });

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

                // Notificar a supervisores y administradores con prioridad alta
                await _notificacionesService.NotificarRolAsync("SUPERVISOR", titulo, mensaje, "warning");
                await _notificacionesService.NotificarRolAsync("ADMIN", titulo, mensaje, "info");

                // Guardar en la base de datos
                await GuardarNotificacionEnBdAsync("CONTEO_SUPERVISION", resultadoGuid, titulo, mensaje, new[] { "SUPERVISOR", "ADMIN" });

                _logger.LogInformation("Notificación de conteo en supervisión enviada para resultado {ResultadoGuid}", resultadoGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de conteo en supervisión para {ResultadoGuid}", resultadoGuid);
            }
        }

        /// <summary>
        /// Guarda una notificación en la base de datos para usuarios con roles específicos
        /// </summary>
        private async Task GuardarNotificacionEnBdAsync(string tipoEvento, Guid procesoId, string titulo, string mensaje, string[] roles)
        {
            try
            {
                // Obtener IDs de usuarios con los roles especificados
                var usuarioIds = await _context.Usuarios
                    .Where(u => u.IdRol.HasValue && (
                        (u.IdRol == 3 && roles.Contains("OPERARIO")) ||
                        (u.IdRol == 10 && roles.Contains("OPERARIO")) ||
                        (u.IdRol == 20 && roles.Contains("SUPERVISOR")) ||
                        (u.IdRol == 30 && roles.Contains("ADMIN"))
                    ))
                    .Select(u => u.IdUsuario)
                    .ToListAsync();

                if (usuarioIds.Any())
                {
                    var crearDto = new CrearNotificacionDto
                    {
                        CodigoEmpresa = 1, // Por defecto
                        TipoNotificacion = tipoEvento,
                        ProcesoId = procesoId,
                        Titulo = titulo,
                        Mensaje = mensaje,
                        EsGrupal = true,
                        GrupoDestino = string.Join(",", roles),
                        UsuarioIds = usuarioIds
                    };

                    await _notificacionesBdService.CrearNotificacionAsync(crearDto);
                    _logger.LogInformation("✅ Notificación guardada en BD para {Cantidad} usuarios con roles {Roles}", usuarioIds.Count, string.Join(",", roles));
                }
                else
                {
                    _logger.LogWarning("⚠️ No se encontraron usuarios con roles {Roles}", string.Join(",", roles));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al guardar notificación en BD: {TipoEvento}", tipoEvento);
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
                
                // 1. Enviar por SignalR a operarios, supervisores y administradores
                await _notificacionesService.NotificarRolAsync("OPERARIO", titulo, mensaje, tipoVisual);
                await _notificacionesService.NotificarRolAsync("SUPERVISOR", titulo, mensaje, tipoVisual);
                await _notificacionesService.NotificarRolAsync("ADMIN", titulo, mensaje, tipoVisual);

                // 2. Guardar en la base de datos
                var tipoEvento = $"ESTADO_{estadoNuevo.ToUpper()}";
                await GuardarNotificacionEnBdAsync(tipoEvento, ordenId, titulo, mensaje, new[] { "OPERARIO", "SUPERVISOR", "ADMIN" });

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

    }
}
