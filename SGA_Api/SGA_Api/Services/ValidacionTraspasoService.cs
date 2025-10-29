using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Traspasos;
using SGA_Api.Models.Calidad;

namespace SGA_Api.Services
{
    public class ValidacionTraspasoService : IValidacionTraspasoService
    {
        private readonly AuroraSgaDbContext _auroraSgaContext;
        private readonly ILogger<ValidacionTraspasoService> _logger;

        public ValidacionTraspasoService(
            AuroraSgaDbContext auroraSgaContext,
            ILogger<ValidacionTraspasoService> logger)
        {
            _auroraSgaContext = auroraSgaContext;
            _logger = logger;
        }

        /// <summary>
        /// 🔷 NUEVO: Validar traspaso de artículo individual
        /// </summary>
        public async Task<ValidacionTraspasoResult> ValidarTraspasoArticuloAsync(
            string codigoArticulo, 
            string almacenDestino,
            string ubicacionDestino,
            short codigoEmpresa)
        {
            try
            {
                // 1. Verificar si artículo está bloqueado por calidad
                var bloqueo = await _auroraSgaContext.BloqueosCalidad
                    .Where(b => b.CodigoEmpresa == codigoEmpresa && 
                               b.CodigoArticulo == codigoArticulo && 
                               b.Bloqueado)
                    .OrderByDescending(b => b.FechaBloqueo)
                    .FirstOrDefaultAsync();

                if (bloqueo == null)
                {
                    // Artículo no está bloqueado, permitir traspaso
                    return ValidacionTraspasoResult.Valido();
                }

                // 2. Verificar si destino es Pulmón
                // 🔷 OPTIMIZADO: Una sola consulta con JOIN para mejor rendimiento
                var tipoUbicacion = await _auroraSgaContext.Ubicaciones_Configuracion
                    .Where(u => u.Ubicacion == ubicacionDestino && u.CodigoAlmacen == almacenDestino)
                    .Join(_auroraSgaContext.TipoUbicaciones,
                        u => u.TipoUbicacionId,
                        t => t.TipoUbicacionId,
                        (u, t) => t.Descripcion)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrEmpty(tipoUbicacion))
                {
                    _logger.LogWarning($"Ubicación no encontrada en configuración: {almacenDestino}-{ubicacionDestino}");
                    return ValidacionTraspasoResult.Valido(); // Permitir si no se encuentra la ubicación
                }

                _logger.LogInformation($"Ubicación {almacenDestino}-{ubicacionDestino} -> Descripción: {tipoUbicacion}");

                var esPulmon = tipoUbicacion?.ToUpper() == "PULMON";
                
                if (esPulmon)
                {
                    return ValidacionTraspasoResult.Bloqueado(
                        $"No se puede traspasar el artículo {codigoArticulo} a ubicación PULMÓN. El artículo está bloqueado por calidad.");
                }

                // Artículo bloqueado pero destino no es Pulmón, permitir traspaso
                return ValidacionTraspasoResult.Valido();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validando traspaso del artículo {codigoArticulo} a {almacenDestino}-{ubicacionDestino}");
                // En caso de error, permitir traspaso para no bloquear operaciones
                return ValidacionTraspasoResult.Valido();
            }
        }
    }
}
