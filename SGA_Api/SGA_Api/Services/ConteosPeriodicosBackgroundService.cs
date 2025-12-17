using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGA_Api.Data;
using SGA_Api.Models.Conteos;

namespace SGA_Api.Services
{
    public class ConteosPeriodicosBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private const int HORA_EJECUCION = 4; // Se ejecuta a las 04:00

        public ConteosPeriodicosBackgroundService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var logger = _serviceProvider.CreateScope().ServiceProvider.GetRequiredService<ILogger<ConteosPeriodicosBackgroundService>>();
            logger.LogInformation("ConteosPeriodicosBackgroundService iniciado. Se ejecutará diariamente a las {Hora}:00", HORA_EJECUCION);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Calcular tiempo hasta las 04:00 del día siguiente (o del día actual si aún no han pasado las 04:00)
                    var ahora = DateTime.Now;
                    var proximaEjecucion = new DateTime(ahora.Year, ahora.Month, ahora.Day, HORA_EJECUCION, 0, 0);
                    
                    // Si ya pasaron las 04:00 de hoy, programar para mañana
                    if (ahora >= proximaEjecucion)
                    {
                        proximaEjecucion = proximaEjecucion.AddDays(1);
                    }

                    var tiempoHastaEjecucion = proximaEjecucion - ahora;
                    
                    logger.LogInformation("Próxima ejecución programada para: {FechaHora} (en {Horas} horas y {Minutos} minutos)", 
                        proximaEjecucion, tiempoHastaEjecucion.Hours, tiempoHastaEjecucion.Minutes);

                    // Esperar hasta la hora programada
                    await Task.Delay(tiempoHastaEjecucion, stoppingToken);

                    // Si se canceló durante la espera, salir
                    if (stoppingToken.IsCancellationRequested)
                        break;

                    // Ejecutar el procesamiento
                    using var scope = _serviceProvider.CreateScope();
                    var auroraSgaDbContext = scope.ServiceProvider.GetRequiredService<AuroraSgaDbContext>();
                    var loggerEjecucion = scope.ServiceProvider.GetRequiredService<ILogger<ConteosPeriodicosBackgroundService>>();

                    loggerEjecucion.LogInformation("Iniciando procesamiento diario de conteos periódicos a las {Hora}", DateTime.Now);
                    await ProcesarRenovacionesAsync(auroraSgaDbContext, loggerEjecucion);
                    loggerEjecucion.LogInformation("Procesamiento diario de conteos periódicos completado");
                }
                catch (OperationCanceledException)
                {
                    // Cancelación normal, salir del bucle
                    break;
                }
                catch (Exception ex)
                {
                    var loggerError = _serviceProvider.CreateScope().ServiceProvider.GetRequiredService<ILogger<ConteosPeriodicosBackgroundService>>();
                    loggerError.LogError(ex, "Error en ciclo de procesamiento de conteos periódicos");
                    
                    // En caso de error, esperar 1 hora antes de reintentar
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
            }

            logger.LogInformation("ConteosPeriodicosBackgroundService detenido");
        }

        private async Task ProcesarRenovacionesAsync(
            AuroraSgaDbContext dbContext,
            ILogger<ConteosPeriodicosBackgroundService> logger)
        {
            try
            {
                var hoy = DateTime.Now.Date; // Solo la fecha, sin hora

                // Buscar órdenes periódicas que necesitan renovación
                // Comparar solo por fecha (día), no por hora
                // Renovamos independientemente del estado (puede estar ASIGNADO, EN_PROCESO, etc.)
                var ordenesParaRenovar = await dbContext.OrdenesConteo
                    .Where(o => o.EsPeriodico == true
                        && o.Activo == true
                        && o.FechaProximaRenovacion.HasValue
                        && o.FechaProximaRenovacion.Value.Date <= hoy)
                    .ToListAsync();

                if (!ordenesParaRenovar.Any())
                {
                    logger.LogDebug("No hay conteos periódicos pendientes de renovación");
                    return;
                }

                logger.LogInformation("Procesando {Cantidad} conteos periódicos para renovación", ordenesParaRenovar.Count);

                foreach (var ordenOriginal in ordenesParaRenovar)
                {
                    try
                    {
                        await RenovarConteoAsync(ordenOriginal, dbContext, logger);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error al renovar conteo periódico {GuidID}: {Mensaje}", 
                            ordenOriginal.GuidID, ex.Message);
                        // Continuar con el siguiente aunque falle uno
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error al procesar renovaciones de conteos periódicos");
            }
        }

        private async Task RenovarConteoAsync(
            OrdenConteo ordenOriginal,
            AuroraSgaDbContext dbContext,
            ILogger<ConteosPeriodicosBackgroundService> logger)
        {
            using var transaction = await dbContext.Database.BeginTransactionAsync();
            try
            {
                logger.LogInformation("Renovando conteo periódico {GuidID} - Título: {Titulo}", 
                    ordenOriginal.GuidID, ordenOriginal.Titulo);

                // Crear nueva orden basada en la original
                // Añadir fecha al título para diferenciar las renovaciones
                var fechaRenovacion = DateTime.Now.ToString("dd/MM/yyyy");
                var tituloConFecha = $"{ordenOriginal.Titulo} - {fechaRenovacion}";
                
                var nuevaOrden = new OrdenConteo
                {
                    CodigoEmpresa = ordenOriginal.CodigoEmpresa,
                    Titulo = tituloConFecha,
                    Visibilidad = ordenOriginal.Visibilidad,
                    ModoGeneracion = ordenOriginal.ModoGeneracion,
                    Alcance = ordenOriginal.Alcance,
                    FiltrosJson = ordenOriginal.FiltrosJson,
                    FechaPlan = DateTime.Now, // Nueva fecha plan para la renovación
                    FechaEjecucion = null,
                    SupervisorCodigo = ordenOriginal.SupervisorCodigo,
                    CreadoPorCodigo = ordenOriginal.CreadoPorCodigo,
                    Estado = string.IsNullOrEmpty(ordenOriginal.CodigoOperario) ? "PLANIFICADO" : "ASIGNADO",
                    Prioridad = ordenOriginal.Prioridad,
                    FechaCreacion = DateTime.Now,
                    CodigoOperario = ordenOriginal.CodigoOperario,
                    FechaAsignacion = !string.IsNullOrEmpty(ordenOriginal.CodigoOperario) ? DateTime.Now : null,
                    CodigoAlmacen = ordenOriginal.CodigoAlmacen,
                    CodigoUbicacion = ordenOriginal.CodigoUbicacion,
                    CodigoArticulo = ordenOriginal.CodigoArticulo,
                    DescripcionArticulo = ordenOriginal.DescripcionArticulo,
                    LotePartida = ordenOriginal.LotePartida,
                    CantidadTeorica = ordenOriginal.CantidadTeorica,
                    Comentario = ordenOriginal.Comentario,
                    // La nueva orden NO es periódica (es una instancia)
                    EsPeriodico = false,
                    FrecuenciaDias = null,
                    Activo = true, // Por defecto activa
                    // Referencia a la orden padre
                    OrdenPadreGuid = ordenOriginal.GuidID
                };

                dbContext.OrdenesConteo.Add(nuevaOrden);

                // Actualizar orden original
                ordenOriginal.FechaUltimaRenovacion = DateTime.Now;
                if (ordenOriginal.FrecuenciaDias.HasValue)
                {
                    // Calcular próxima renovación solo por fecha (día), sin hora específica
                    ordenOriginal.FechaProximaRenovacion = DateTime.Now.Date.AddDays(ordenOriginal.FrecuenciaDias.Value);
                }

                await dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                logger.LogInformation("Conteo periódico {GuidOriginal} renovado exitosamente. Nueva orden creada: {GuidNueva}", 
                    ordenOriginal.GuidID, nuevaOrden.GuidID);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}

