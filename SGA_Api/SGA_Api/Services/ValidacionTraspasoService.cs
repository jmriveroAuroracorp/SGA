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
        /// 🔷 ACTUALIZADO: Validar traspaso de artículo individual verificando bloqueos en ubicación origen específica
        /// </summary>
        public async Task<ValidacionTraspasoResult> ValidarTraspasoArticuloAsync(
            string codigoArticulo, 
            string almacenDestino,
            string ubicacionDestino,
            short codigoEmpresa,
            string? partida = null,
            string? almacenOrigen = null,
            string? ubicacionOrigen = null)
        {
            try
            {
                _logger.LogInformation($"🔍 ValidacionTraspasoService - Artículo: {codigoArticulo}, Partida: {partida ?? "(null)"}, Origen: {almacenOrigen ?? "(null)"}-{ubicacionOrigen ?? "(null)"}, Destino: {almacenDestino}-{ubicacionDestino}, Empresa: {codigoEmpresa}");

                // 🔷 CORREGIDO: Los bloqueos de calidad se guardan por ARTÍCULO + PARTIDA + ALMACÉN + UBICACIÓN
                // Si no hay partida, no podemos validar bloqueos específicos (no bloqueamos todo el artículo)
                if (string.IsNullOrWhiteSpace(partida))
                {
                    _logger.LogInformation($"⚠️ No se proporcionó partida para validar bloqueo de calidad del artículo {codigoArticulo}. Se permite el traspaso.");
                    return ValidacionTraspasoResult.Valido();
                }

                // 🔷 ACTUALIZADO: Verificar bloqueo SOLO en la ubicación origen específica (si se proporciona)
                // Si no se proporciona ubicación origen, mantener lógica anterior por compatibilidad
                BloqueoCalidad? bloqueo = null;
                
                if (!string.IsNullOrWhiteSpace(almacenOrigen))
                {
                    // Verificar bloqueo en ubicación origen específica
                    _logger.LogInformation($"🔍 Buscando bloqueos para artículo {codigoArticulo}, partida {partida}, almacén origen {almacenOrigen}, ubicación origen {ubicacionOrigen ?? "(sin ubicación)"}, empresa {codigoEmpresa}");
                    
                    var queryBloqueo = _auroraSgaContext.BloqueosCalidad
                        .Where(b => b.CodigoEmpresa == codigoEmpresa && 
                                   b.CodigoArticulo == codigoArticulo && 
                                   b.LotePartida == partida &&
                                   b.CodigoAlmacen == almacenOrigen &&
                                   b.Bloqueado);

                    // Filtrar por ubicación origen específica
                    if (!string.IsNullOrWhiteSpace(ubicacionOrigen))
                    {
                        queryBloqueo = queryBloqueo.Where(b => b.Ubicacion == ubicacionOrigen);
                    }
                    else
                    {
                        queryBloqueo = queryBloqueo.Where(b => string.IsNullOrEmpty(b.Ubicacion));
                    }

                    bloqueo = await queryBloqueo
                        .OrderByDescending(b => b.FechaBloqueo)
                        .FirstOrDefaultAsync();
                }
                else
                {
                    // Fallback: Si no hay ubicación origen, verificar en cualquier ubicación (compatibilidad)
                    _logger.LogInformation($"⚠️ No se proporcionó almacén origen. Verificando bloqueos en cualquier ubicación (modo compatibilidad)");
                    _logger.LogInformation($"🔍 Buscando bloqueos para artículo {codigoArticulo}, partida {partida}, empresa {codigoEmpresa}");
                    
                    bloqueo = await _auroraSgaContext.BloqueosCalidad
                        .Where(b => b.CodigoEmpresa == codigoEmpresa && 
                                   b.CodigoArticulo == codigoArticulo && 
                                   b.LotePartida == partida &&
                                   b.Bloqueado)
                        .OrderByDescending(b => b.FechaBloqueo)
                        .FirstOrDefaultAsync();
                }

                if (bloqueo == null)
                {
                    _logger.LogInformation($"✅ No se encontró bloqueo para artículo {codigoArticulo}, partida {partida} en la ubicación origen especificada. Se permite el traspaso.");
                    return ValidacionTraspasoResult.Valido();
                }

                var tipoBloqueo = bloqueo.TipoBloqueo?.ToUpper() ?? "TOTAL";
                _logger.LogInformation($"🚫 Bloqueo encontrado en ubicación origen - ID: {bloqueo.Id}, Almacén: {bloqueo.CodigoAlmacen}, Ubicación: {bloqueo.Ubicacion ?? "(sin ubicación)"}, Tipo: {tipoBloqueo}, Fecha: {bloqueo.FechaBloqueo}, Comentario: {bloqueo.ComentarioBloqueo}");

                // 🔷 NUEVO: Verificar tipo de bloqueo
                if (tipoBloqueo == "TOTAL")
                {
                    // 🔷 CORREGIDO: Permitir traspaso si el destino es igual al origen (mismo almacén y ubicación)
                    // Normalizar ubicaciones para comparación
                    var almacenOrigenNormalizado = almacenOrigen?.Trim() ?? "";
                    var almacenDestinoNormalizado = almacenDestino?.Trim() ?? "";
                    var ubicacionOrigenNormalizada = string.IsNullOrWhiteSpace(ubicacionOrigen) ? "" : ubicacionOrigen.Trim();
                    var ubicacionDestinoNormalizada = string.IsNullOrWhiteSpace(ubicacionDestino) ? "" : ubicacionDestino.Trim();
                    
                    // Verificar si origen y destino son iguales
                    var esMismoAlmacen = string.Equals(almacenOrigenNormalizado, almacenDestinoNormalizado, StringComparison.OrdinalIgnoreCase);
                    var esMismaUbicacion = string.Equals(ubicacionOrigenNormalizada, ubicacionDestinoNormalizada, StringComparison.OrdinalIgnoreCase);
                    
                    if (esMismoAlmacen && esMismaUbicacion)
                    {
                        // Destino igual al origen: permitir traspaso (no hay movimiento real)
                        _logger.LogInformation($"✅ BLOQUEO TOTAL - Artículo {codigoArticulo} (partida {partida}) bloqueado pero destino es igual al origen ({almacenDestinoNormalizado}-{ubicacionDestinoNormalizada}). Se permite el traspaso.");
                        return ValidacionTraspasoResult.Valido();
                    }
                    
                    // Bloqueo total: No se puede traspasar a ninguna otra ubicación
                    _logger.LogWarning($"🚫 BLOQUEO TOTAL ACTIVADO - Artículo {codigoArticulo} (partida {partida}) bloqueado por calidad en {bloqueo.CodigoAlmacen}-{bloqueo.Ubicacion ?? "(sin ubicación)"}. No se puede traspasar a ninguna ubicación.");
                    return ValidacionTraspasoResult.Bloqueado(
                        $"No se puede traspasar el artículo {codigoArticulo} (partida {partida}). El artículo está bloqueado por calidad en la ubicación origen {bloqueo.CodigoAlmacen}-{bloqueo.Ubicacion ?? "(sin ubicación)"}. Debe desbloquearse antes de poder traspasarlo.");
                }

                // Si es "SOLO_PULMON", verificar si el destino es PULMÓN
                if (tipoBloqueo == "SOLO_PULMON")
                {
                    // 🔷 CORREGIDO: Manejar ubicaciones vacías correctamente (NULL o cadena vacía)
                    var ubicacionDestinoParaConsulta = string.IsNullOrWhiteSpace(ubicacionDestino) ? null : ubicacionDestino;
                    var ubicacionDestinoDisplay = string.IsNullOrWhiteSpace(ubicacionDestino) ? "(sin ubicación)" : ubicacionDestino;
                    
                    // 🔷 OPTIMIZADO: Una sola consulta con JOIN para mejor rendimiento
                    // Manejar tanto NULL como cadena vacía en la base de datos
                    var tipoUbicacion = await _auroraSgaContext.Ubicaciones_Configuracion
                        .Where(u => u.CodigoAlmacen == almacenDestino &&
                                   (ubicacionDestinoParaConsulta == null 
                                    ? (u.Ubicacion == null || u.Ubicacion == "")
                                    : u.Ubicacion == ubicacionDestinoParaConsulta))
                        .Join(_auroraSgaContext.TipoUbicaciones,
                            u => u.TipoUbicacionId,
                            t => t.TipoUbicacionId,
                            (u, t) => t.Descripcion)
                        .FirstOrDefaultAsync();

                    if (string.IsNullOrEmpty(tipoUbicacion))
                    {
                        _logger.LogWarning($"⚠️ Ubicación no encontrada en configuración: {almacenDestino}-{ubicacionDestinoDisplay}. Se permite el traspaso (no se puede validar tipo).");
                        return ValidacionTraspasoResult.Valido(); // Permitir si no se encuentra la ubicación
                    }

                    _logger.LogInformation($"📍 Ubicación {almacenDestino}-{ubicacionDestinoDisplay} -> Tipo: {tipoUbicacion}");

                    var esPulmon = tipoUbicacion?.ToUpper() == "PULMON";
                    
                    if (esPulmon)
                    {
                        _logger.LogWarning($"🚫 BLOQUEO A PULMÓN ACTIVADO - Artículo {codigoArticulo} (partida {partida}) bloqueado por calidad intentando moverse a PULMÓN {almacenDestino}-{ubicacionDestinoDisplay}");
                        return ValidacionTraspasoResult.Bloqueado(
                            $"No se puede traspasar el artículo {codigoArticulo} (partida {partida}) a ubicación PULMÓN. El artículo está bloqueado por calidad.");
                    }

                    // Bloqueo solo a PULMÓN pero destino no es PULMÓN, permitir traspaso
                    _logger.LogInformation($"✅ Artículo {codigoArticulo} (partida {partida}) tiene bloqueo solo a PULMÓN pero destino NO es PULMÓN ({tipoUbicacion}). Se permite el traspaso.");
                    return ValidacionTraspasoResult.Valido();
                }

                // Tipo desconocido, por seguridad bloquear
                _logger.LogWarning($"⚠️ Tipo de bloqueo desconocido: {tipoBloqueo}. Por seguridad, se bloquea el traspaso.");
                return ValidacionTraspasoResult.Bloqueado(
                    $"No se puede traspasar el artículo {codigoArticulo} (partida {partida}). Tipo de bloqueo desconocido: {tipoBloqueo}.");
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
