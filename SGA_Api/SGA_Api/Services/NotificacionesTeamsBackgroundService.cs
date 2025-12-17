using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGA_Api.Data;
using SGA_Api.Models.Notificaciones;
using SGA_Api.Models.Traspasos;

namespace SGA_Api.Services
{
    public class NotificacionesTeamsBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _intervalo = TimeSpan.FromSeconds(20); // Se ejecuta cada 20 segundos
        private const int MAX_REGISTROS_POR_CICLO = 10; // Límite de registros a procesar por ciclo
        private const int MAX_INTENTOS = 3; // Máximo de intentos antes de marcar como Error

        public NotificacionesTeamsBackgroundService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var auroraSgaDbContext = scope.ServiceProvider.GetRequiredService<AuroraSgaDbContext>();
                    var sageDbContext = scope.ServiceProvider.GetRequiredService<SageDbContext>();
                    var notificacionesTeamsService = scope.ServiceProvider.GetRequiredService<INotificacionesTeamsService>();
                    var logger = scope.ServiceProvider.GetRequiredService<ILogger<NotificacionesTeamsBackgroundService>>();

                    await ProcesarColaAsync(auroraSgaDbContext, sageDbContext, notificacionesTeamsService, logger);
                }
                catch (Exception ex)
                {
                    var logger = _serviceProvider.CreateScope().ServiceProvider.GetRequiredService<ILogger<NotificacionesTeamsBackgroundService>>();
                    logger.LogError(ex, "Error en ciclo de procesamiento de cola de notificaciones Teams");
                }

                await Task.Delay(_intervalo, stoppingToken);
            }
        }

        private async Task ProcesarColaAsync(
            AuroraSgaDbContext auroraSgaDbContext,
            SageDbContext sageDbContext,
            INotificacionesTeamsService notificacionesTeamsService,
            ILogger<NotificacionesTeamsBackgroundService> logger)
        {
            try
            {
                // Leer registros pendientes de la cola
                var registrosPendientes = await auroraSgaDbContext.NotificacionesTeamsCola
                    .Where(n => n.Estado == "Pendiente")
                    .OrderBy(n => n.FechaCreacion)
                    .Take(MAX_REGISTROS_POR_CICLO)
                    .ToListAsync();

                if (!registrosPendientes.Any())
                {
                    return; // No hay registros pendientes
                }

                logger.LogInformation("Procesando {Cantidad} registros pendientes de notificaciones Teams", registrosPendientes.Count);

                foreach (var registro in registrosPendientes)
                {
                    try
                    {
                        await ProcesarRegistroAsync(registro, auroraSgaDbContext, sageDbContext, notificacionesTeamsService, logger);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error al procesar registro {RegistroId} de la cola", registro.Id);
                        
                        // Incrementar intentos y actualizar estado
                        registro.Intentos++;
                        if (registro.Intentos >= MAX_INTENTOS)
                        {
                            registro.Estado = "Error";
                            registro.ErrorMensaje = $"Error después de {MAX_INTENTOS} intentos: {ex.Message}";
                            logger.LogWarning("Registro {RegistroId} marcado como Error después de {Intentos} intentos", registro.Id, registro.Intentos);
                        }
                        // Si no alcanzó el máximo, se mantiene como "Pendiente" para reintento
                        
                        await auroraSgaDbContext.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error al leer o procesar registros de la cola");
            }
        }

        private async Task ProcesarRegistroAsync(
            NotificacionTeamsCola registro,
            AuroraSgaDbContext auroraSgaDbContext,
            SageDbContext sageDbContext,
            INotificacionesTeamsService notificacionesTeamsService,
            ILogger<NotificacionesTeamsBackgroundService> logger)
        {
            // Obtener traspaso completo
            var traspaso = await auroraSgaDbContext.Traspasos
                .FirstOrDefaultAsync(t => t.Id == registro.TraspasoId);

            if (traspaso == null)
            {
                logger.LogWarning("Traspaso {TraspasoId} no encontrado para registro {RegistroId}. Marcando como Enviado", registro.TraspasoId, registro.Id);
                registro.Estado = "Enviado";
                registro.FechaProcesado = DateTime.Now;
                await auroraSgaDbContext.SaveChangesAsync();
                return;
            }

            // Verificar que el traspaso sigue en ERROR_ERP (puede haber sido relanzado)
            if (traspaso.CodigoEstado != "ERROR_ERP")
            {
                logger.LogInformation("Traspaso {TraspasoId} ya no está en ERROR_ERP (estado actual: {Estado}). Marcando registro como Enviado", 
                    traspaso.Id, traspaso.CodigoEstado);
                registro.Estado = "Enviado";
                registro.FechaProcesado = DateTime.Now;
                await auroraSgaDbContext.SaveChangesAsync();
                return;
            }

            // Determinar tipo de notificación según almacén DESTINO
            // Reglas: 002 → Canela (95), 100 → Andalucía (94), 300 → Oceania (97), resto → General (96)
            var almacenDestino = traspaso.AlmacenDestino;

            var tipoNotificacionId = await notificacionesTeamsService.DeterminarTipoNotificacionAsync(sageDbContext, almacenDestino);

            // Obtener datos del tipo de notificación (incluyendo CanalTeams)
            var tipoNotificacion = await notificacionesTeamsService.ObtenerTipoNotificacionAsync(sageDbContext, tipoNotificacionId);

            // Si no se encuentra el tipo determinado, intentar con fallback (tipo 98 - Pendientes supervisión)
            if (tipoNotificacion == null && tipoNotificacionId != 98)
            {
                logger.LogWarning("No se encontró tipo de notificación {TipoNotificacionId}, intentando con fallback 98", tipoNotificacionId);
                tipoNotificacion = await notificacionesTeamsService.ObtenerTipoNotificacionAsync(sageDbContext, 98);
                if (tipoNotificacion != null)
                {
                    tipoNotificacionId = 98;
                }
            }

            if (tipoNotificacion == null)
            {
                throw new Exception($"No se pudo obtener tipo de notificación {tipoNotificacionId} ni el fallback 98 desde MRH_TipoNotificacion");
            }

            // Construir mensaje
            var mensajeError = registro.MensajeError ?? traspaso.EstadoErp ?? traspaso.Comentario ?? "Error en traspaso ERP";
            var mensajeCompleto = notificacionesTeamsService.ConstruirMensajeTraspaso(traspaso, mensajeError);

            // Insertar en MRH_Notificaciones en AURORA
            // MovPosicion se genera automáticamente con NEWID() en la inserción
            await notificacionesTeamsService.InsertarMrhNotificacionAsync(
                sageDbContext,
                traspaso,
                tipoNotificacion,
                mensajeCompleto);

            // Marcar como procesado exitosamente
            registro.Estado = "Enviado";
            registro.FechaProcesado = DateTime.Now;
            await auroraSgaDbContext.SaveChangesAsync();

            logger.LogInformation("Registro {RegistroId} procesado exitosamente. Traspaso {TraspasoId} insertado en MRH_Notificaciones con tipo {TipoNotificacion}",
                registro.Id, traspaso.Id, tipoNotificacionId);
        }
    }
}

