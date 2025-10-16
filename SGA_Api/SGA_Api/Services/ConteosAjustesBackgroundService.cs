using Microsoft.EntityFrameworkCore;

namespace SGA_Api.Services
{
    public class ConteosAjustesBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ConteosAjustesBackgroundService> _logger;

        public ConteosAjustesBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<ConteosAjustesBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ConteosAjustesBackgroundService iniciado");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var conteosService = scope.ServiceProvider.GetRequiredService<IConteosService>();
                    
                    await conteosService.ProcesarAjustesCompletadosAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en ConteosAjustesBackgroundService");
                }

                // Esperar 0.5 segundos antes de la siguiente iteración (misma frecuencia que TraspasoFinalizacionBackgroundService)
                await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ConteosAjustesBackgroundService detenido");
            await base.StopAsync(stoppingToken);
        }
    }
}
