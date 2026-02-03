using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Traspasos;
using SGA_Api.Models.Calidad;
using SGA_Api.Models.Stock;

namespace SGA_Api.Services
{
    public class ValidacionTraspasoService : IValidacionTraspasoService
    {
        private readonly AuroraSgaDbContext _auroraSgaContext;
        private readonly SageDbContext _sageContext;
        private readonly ILogger<ValidacionTraspasoService> _logger;

        public ValidacionTraspasoService(
            AuroraSgaDbContext auroraSgaContext,
            SageDbContext sageContext,
            ILogger<ValidacionTraspasoService> logger)
        {
            _auroraSgaContext = auroraSgaContext;
            _sageContext = sageContext;
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
                if (!string.IsNullOrWhiteSpace(partida))
                {

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

                    if (bloqueo != null)
                    {
                        var tipoBloqueo = bloqueo.TipoBloqueo?.ToUpper() ?? "TOTAL";
                        _logger.LogInformation($"🚫 Bloqueo encontrado en ubicación origen - ID: {bloqueo.Id}, Almacén: {bloqueo.CodigoAlmacen}, Ubicación: {bloqueo.Ubicacion ?? "(sin ubicación)"}, Tipo: {tipoBloqueo}, Fecha: {bloqueo.FechaBloqueo}, Comentario: {bloqueo.ComentarioBloqueo}");

                        // 🔷 Verificar tipo de bloqueo - Si bloquea, retornar inmediatamente sin validar alérgenos
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
                                _logger.LogInformation($"✅ BLOQUEO TOTAL - Artículo {codigoArticulo} (partida {partida}) bloqueado pero destino es igual al origen ({almacenDestinoNormalizado}-{ubicacionDestinoNormalizada}). Continuando con validación de alérgenos.");
                            }
                            else
                            {
                                // Bloqueo total: No se puede traspasar a ninguna otra ubicación
                                _logger.LogWarning($"🚫 BLOQUEO TOTAL ACTIVADO - Artículo {codigoArticulo} (partida {partida}) bloqueado por calidad en {bloqueo.CodigoAlmacen}-{bloqueo.Ubicacion ?? "(sin ubicación)"}. No se puede traspasar a ninguna ubicación.");
                                return ValidacionTraspasoResult.Bloqueado(
                                    $"No se puede traspasar el artículo {codigoArticulo} (partida {partida}). El artículo está bloqueado por calidad en la ubicación origen {bloqueo.CodigoAlmacen}-{bloqueo.Ubicacion ?? "(sin ubicación)"}. Debe desbloquearse antes de poder traspasarlo.");
                            }
                        }
                        else if (tipoBloqueo == "SOLO_PULMON")
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
                                _logger.LogWarning($"⚠️ Ubicación no encontrada en configuración: {almacenDestino}-{ubicacionDestinoDisplay}. Se permite el traspaso (no se puede validar tipo). Continuando con validación de alérgenos.");
                            }
                            else
                            {
                                _logger.LogInformation($"📍 Ubicación {almacenDestino}-{ubicacionDestinoDisplay} -> Tipo: {tipoUbicacion}");

                                var esPulmon = tipoUbicacion?.ToUpper() == "PULMON";

                                if (esPulmon)
                                {
                                    // Bloqueo solo a PULMÓN y destino es PULMÓN: bloquear traspaso
                                    _logger.LogWarning($"🚫 BLOQUEO A PULMÓN ACTIVADO - Artículo {codigoArticulo} (partida {partida}) bloqueado por calidad intentando moverse a PULMÓN {almacenDestino}-{ubicacionDestinoDisplay}");
                                    return ValidacionTraspasoResult.Bloqueado(
                                        $"No se puede traspasar el artículo {codigoArticulo} (partida {partida}) a ubicación PULMÓN. El artículo está bloqueado por calidad.");
                                }

                                // Bloqueo solo a PULMÓN pero destino no es PULMÓN, permitir traspaso
                                _logger.LogInformation($"✅ Artículo {codigoArticulo} (partida {partida}) tiene bloqueo solo a PULMÓN pero destino NO es PULMÓN ({tipoUbicacion}). Continuando con validación de alérgenos.");
                            }
                        }
                        else
                        {
                            // Tipo desconocido, por seguridad bloquear
                            _logger.LogWarning($"⚠️ Tipo de bloqueo desconocido: {tipoBloqueo}. Por seguridad, se bloquea el traspaso.");
                            return ValidacionTraspasoResult.Bloqueado(
                                $"No se puede traspasar el artículo {codigoArticulo} (partida {partida}). Tipo de bloqueo desconocido: {tipoBloqueo}.");
                        }
                    }
                    else
                    {
                        _logger.LogInformation($"✅ No se encontró bloqueo para artículo {codigoArticulo}, partida {partida} en la ubicación origen especificada. Continuando con validación de alérgenos.");
                    }
                }
                else
                {
                    // Si no hay partida, no validamos bloqueos de calidad pero sí validamos alérgenos
                    _logger.LogInformation($"ℹ️ No hay partida para artículo {codigoArticulo}. No se validan bloqueos de calidad, pero se validarán alérgenos.");
                }

                // 🔷 NUEVO: Validar alérgenos del artículo vs alérgenos permitidos en ubicación destino
                // Esta validación se ejecuta siempre, independientemente de si hay partida o no
                // 🔷 OPTIMIZADO: Consultar primero la ubicación para evitar consultas innecesarias a artículos
                
                // ⚠️ TEMPORALMENTE DESACTIVADO: La validación de alérgenos está desactivada hasta que calidad complete la segmentación por alérgenos
                // TODO: Reactivar esta validación cuando calidad haya completado la segmentación
                // Para reactivar, cambiar el siguiente 'if (false)' por 'if (true)'
                if (false) // 🔷 TEMPORALMENTE DESACTIVADO
                {
                    try
                    {
                    // 1. PRIMERO: Obtener alérgenos permitidos en la ubicación destino
                    // Si la ubicación no tiene restricciones, no necesitamos consultar los alérgenos del artículo
                    var ubicacionDestinoParaConsulta = string.IsNullOrWhiteSpace(ubicacionDestino) ? "" : ubicacionDestino;
                    var ubicacionDestinoDisplay = string.IsNullOrWhiteSpace(ubicacionDestino) ? "(sin ubicación)" : ubicacionDestino;

                    var codigosAlergenosPermitidos = await _auroraSgaContext.Ubicaciones_AlergenosPermitidos
                        .Where(up => up.CodigoEmpresa == codigoEmpresa &&
                                   up.CodigoAlmacen == almacenDestino &&
                                   up.Ubicacion == ubicacionDestinoParaConsulta)
                        .Select(up => up.VCodigoAlergeno)
                        .ToListAsync();

                    // 2. Si la ubicación no tiene alérgenos permitidos configurados, permitir traspaso (sin restricciones)
                    if (!codigosAlergenosPermitidos.Any())
                    {
                        _logger.LogInformation($"ℹ️ VALIDACIÓN ALÉRGENOS - Ubicación {almacenDestino}-{ubicacionDestinoDisplay} no tiene alérgenos permitidos configurados. Se permite el traspaso sin restricciones.");
                    }
                    else
                    {
                        // 3. SOLO si la ubicación tiene restricciones, consultar los alérgenos del artículo
                        var articulo = await _sageContext.VisArticulos
                            .AsNoTracking()
                            .Where(a => a.CodigoEmpresa == codigoEmpresa && a.CodigoArticulo == codigoArticulo)
                            .FirstOrDefaultAsync();
                        
                        // Convertir NULL a string vacío después de obtener el resultado
                        var alergenosArticulo = articulo?.VNEWAlergenos ?? string.Empty;

                        if (articulo == null)
                        {
                            // 🔷 NUEVO: Si la ubicación requiere alérgenos específicos, el artículo debe tenerlos TODOS
                            _logger.LogWarning($"🚫 VALIDACIÓN ALÉRGENOS - Artículo {codigoArticulo} no encontrado en Vis_Articulos. La ubicación {almacenDestino}-{ubicacionDestinoDisplay} requiere alérgenos específicos. Se bloquea el traspaso.");
                            return ValidacionTraspasoResult.Bloqueado(
                                $"No se puede traspasar el artículo {codigoArticulo} a la ubicación {almacenDestino}-{ubicacionDestinoDisplay}. " +
                                $"Esta ubicación solo permite artículos con alérgenos específicos y el artículo no tiene alérgenos configurados.",
                                "ALERGENOS");
                        }
                        else if (string.IsNullOrWhiteSpace(alergenosArticulo))
                        {
                            // 🔷 NUEVO: Si la ubicación requiere alérgenos específicos, el artículo debe tenerlos TODOS
                            _logger.LogWarning($"🚫 VALIDACIÓN ALÉRGENOS - Artículo {codigoArticulo} no tiene alérgenos configurados (VNEWAlergenos es NULL o vacío). La ubicación {almacenDestino}-{ubicacionDestinoDisplay} requiere alérgenos específicos. Se bloquea el traspaso.");
                            return ValidacionTraspasoResult.Bloqueado(
                                $"No se puede traspasar el artículo {codigoArticulo} a la ubicación {almacenDestino}-{ubicacionDestinoDisplay}. " +
                                $"Esta ubicación solo permite artículos con alérgenos específicos y el artículo no tiene alérgenos configurados.",
                                "ALERGENOS");
                        }
                        else
                        {
                            var alergenosArticuloStr = alergenosArticulo.Trim();

                            // 4. Parsear alérgenos del artículo (formato: "GLUTEN,HUEVO,SOJA,LECHE")
                            var descripcionesAlergenos = alergenosArticuloStr
                                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(d => d.Trim().ToUpper())
                                .Where(d => !string.IsNullOrWhiteSpace(d))
                                .ToList();

                            if (descripcionesAlergenos.Any())
                            {
                                // 5. Obtener códigos de alérgenos del artículo desde el maestro
                                var codigosAlergenosArticulo = await _auroraSgaContext.AlergenoMaestros
                                    .Where(am => am.CodigoEmpresa == codigoEmpresa &&
                                               descripcionesAlergenos.Contains(am.VDescripcionAlergeno.ToUpper()))
                                    .Select(am => am.VCodigoAlergeno)
                                    .ToListAsync();

                                if (codigosAlergenosArticulo.Any())
                                {
                                    // 🔷 NUEVO: Verificar que el artículo tenga TODOS los alérgenos permitidos
                                    // Convertir codigosAlergenosArticulo (List<short>) a int para comparar con codigosAlergenosPermitidos (List<int>)
                                    var codigosAlergenosArticuloInt = codigosAlergenosArticulo.Select(c => (int)c).ToList();
                                    var alergenosFaltantes = codigosAlergenosPermitidos
                                        .Where(codPermitido => !codigosAlergenosArticuloInt.Contains(codPermitido))
                                        .ToList();

                                    if (alergenosFaltantes.Any())
                                    {
                                        // Obtener descripciones de los alérgenos faltantes para el mensaje
                                        // Convertir alergenosFaltantes (List<int>) a short para comparar con VCodigoAlergeno (short)
                                        var alergenosFaltantesShort = alergenosFaltantes.Select(c => (short)c).ToList();
                                        var descripcionesFaltantes = await _auroraSgaContext.AlergenoMaestros
                                            .Where(am => am.CodigoEmpresa == codigoEmpresa &&
                                                       alergenosFaltantesShort.Contains(am.VCodigoAlergeno))
                                            .Select(am => am.VDescripcionAlergeno)
                                            .ToListAsync();

                                        var descripcionesFaltantesStr = string.Join(", ", descripcionesFaltantes);

                                        _logger.LogWarning($"🚫 VALIDACIÓN ALÉRGENOS - Artículo {codigoArticulo} no tiene todos los alérgenos requeridos en ubicación destino {almacenDestino}-{ubicacionDestinoDisplay}. Alérgenos faltantes: {descripcionesFaltantesStr}");

                                        return ValidacionTraspasoResult.Bloqueado(
                                            $"No se puede traspasar el artículo {codigoArticulo} a la ubicación {almacenDestino}-{ubicacionDestinoDisplay}. " +
                                            $"Esta ubicación requiere que los artículos contengan todos los siguientes alérgenos: {descripcionesFaltantesStr}. " +
                                            $"El artículo no contiene todos los alérgenos requeridos.",
                                            "ALERGENOS");
                                    }

                                    // 6. Verificar que TODOS los alérgenos del artículo estén permitidos (no puede tener otros)
                                    // Reutilizar codigosAlergenosArticuloInt ya declarado arriba
                                    var alergenosNoPermitidos = codigosAlergenosArticuloInt
                                        .Where(codArt => !codigosAlergenosPermitidos.Contains(codArt))
                                        .ToList();

                                    if (alergenosNoPermitidos.Any())
                                    {
                                        // Obtener descripciones de los alérgenos no permitidos para el mensaje
                                        // Convertir alergenosNoPermitidos (List<int>) a short para comparar con VCodigoAlergeno (short)
                                        var alergenosNoPermitidosShort = alergenosNoPermitidos.Select(c => (short)c).ToList();
                                        var descripcionesNoPermitidas = await _auroraSgaContext.AlergenoMaestros
                                            .Where(am => am.CodigoEmpresa == codigoEmpresa &&
                                                       alergenosNoPermitidosShort.Contains(am.VCodigoAlergeno))
                                            .Select(am => am.VDescripcionAlergeno)
                                            .ToListAsync();

                                        var descripcionesStr = string.Join(", ", descripcionesNoPermitidas);

                                        _logger.LogWarning($"🚫 VALIDACIÓN ALÉRGENOS - Artículo {codigoArticulo} tiene alérgenos no permitidos en ubicación destino {almacenDestino}-{ubicacionDestinoDisplay}. Alérgenos no permitidos: {descripcionesStr}");

                                        return ValidacionTraspasoResult.Bloqueado(
                                            $"No se puede traspasar el artículo {codigoArticulo} a la ubicación {almacenDestino}-{ubicacionDestinoDisplay}. " +
                                            $"El artículo contiene los siguientes alérgenos que no están permitidos en esta ubicación: {descripcionesStr}.",
                                            "ALERGENOS");
                                    }

                                    _logger.LogInformation($"✅ VALIDACIÓN ALÉRGENOS - El artículo {codigoArticulo} tiene todos los alérgenos requeridos y no tiene alérgenos no permitidos en la ubicación destino.");
                                }
                                else
                                {
                                    // Si no se encontraron códigos para los alérgenos del artículo, bloquear por seguridad
                                    _logger.LogWarning($"⚠️ VALIDACIÓN ALÉRGENOS - No se encontraron códigos en el maestro para los alérgenos del artículo {codigoArticulo}: {alergenosArticuloStr}. La ubicación requiere alérgenos específicos. Se bloquea el traspaso.");
                                    return ValidacionTraspasoResult.Bloqueado(
                                        $"No se puede traspasar el artículo {codigoArticulo} a la ubicación {almacenDestino}-{ubicacionDestinoDisplay}. " +
                                        $"No se pudieron validar los alérgenos del artículo. Contacte con el administrador del sistema.",
                                        "ALERGENOS");
                                }
                            }
                            else
                            {
                                // Si después de parsear no hay alérgenos válidos, bloquear porque la ubicación requiere alérgenos
                                _logger.LogWarning($"🚫 VALIDACIÓN ALÉRGENOS - Artículo {codigoArticulo} tiene campo VNEWAlergenos pero no contiene alérgenos válidos después de parsear. La ubicación {almacenDestino}-{ubicacionDestinoDisplay} requiere alérgenos específicos. Se bloquea el traspaso.");
                                return ValidacionTraspasoResult.Bloqueado(
                                    $"No se puede traspasar el artículo {codigoArticulo} a la ubicación {almacenDestino}-{ubicacionDestinoDisplay}. " +
                                    $"Esta ubicación solo permite artículos con alérgenos específicos y el artículo no tiene alérgenos válidos configurados.",
                                    "ALERGENOS");
                            }
                        }
                    }
                }
                    catch (Exception ex)
                    {
                        // 🔷 FAIL-SECURE: En caso de error, bloquear traspaso por seguridad alimentaria
                        _logger.LogError(ex, $"❌ ERROR CRÍTICO validando alérgenos para artículo {codigoArticulo} en ubicación {almacenDestino}-{ubicacionDestino}. Se bloquea el traspaso por seguridad.");
                        return ValidacionTraspasoResult.Bloqueado(
                            $"No se puede validar los alérgenos del artículo {codigoArticulo}. Por seguridad, el traspaso ha sido bloqueado. Contacte con el administrador del sistema.",
                            "ALERGENOS"); // 🔷 Marcar como bloqueo de alérgenos
                    }
                }

                // Si llegamos aquí, todas las validaciones pasaron
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
