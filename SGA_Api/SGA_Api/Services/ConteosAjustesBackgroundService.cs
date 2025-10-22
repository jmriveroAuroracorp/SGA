using Microsoft.EntityFrameworkCore;

namespace SGA_Api.Services
{
    public class ConteosAjustesBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ConteosAjustesBackgroundService> _logger;
        private readonly SemaphoreSlim _semaphore;
        private bool _enEjecucion = false;

        public ConteosAjustesBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<ConteosAjustesBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _semaphore = new SemaphoreSlim(1, 1); // Solo permite una ejecución a la vez
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ConteosAjustesBackgroundService iniciado");

            while (!stoppingToken.IsCancellationRequested)
            {
                if (_enEjecucion)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    continue;
                }
                _enEjecucion = true;
                
                try
                {
                    // Intentar adquirir el semáforo (no bloquea si ya hay una ejecución en curso)
                    if (await _semaphore.WaitAsync(0, stoppingToken))
                    {
                        try
                        {
                            using var scope = _serviceProvider.CreateScope();
                            var conteosService = scope.ServiceProvider.GetRequiredService<IConteosService>();
                            
                            await conteosService.ProcesarAjustesCompletadosAsync();
                        }
                        finally
                        {
                            _semaphore.Release();
                        }
                    }
                    else
                    {
                        _logger.LogDebug("ConteosAjustesBackgroundService: Ya hay una ejecución en curso, saltando esta iteración");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en ConteosAjustesBackgroundService");
                }
                finally
                {
                    _enEjecucion = false;
                }

                // Esperar 5 segundos antes de la siguiente iteración
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ConteosAjustesBackgroundService detenido");
            _semaphore?.Dispose();
            await base.StopAsync(stoppingToken);
        }
    }
}
