using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Calidad;

namespace SGA_Api.Services
{
    public class CalidadService : ICalidadService
    {
        private readonly AuroraSgaDbContext _auroraSgaContext;
        private readonly SageDbContext _sageContext;
        private readonly ILogger<CalidadService> _logger;
        private readonly INotificacionesUnificadasService _notificacionesUnificadas;

        public CalidadService(
            AuroraSgaDbContext auroraSgaContext,
            SageDbContext sageContext,
            ILogger<CalidadService> logger,
            INotificacionesUnificadasService notificacionesUnificadas)
        {
            _auroraSgaContext = auroraSgaContext;
            _sageContext = sageContext;
            _logger = logger;
            _notificacionesUnificadas = notificacionesUnificadas;
        }

        public async Task<bool> VerificarPermisoCalidadAsync(int usuarioId)
        {
            try
            {
                var tienePermiso = await _sageContext.AccesosOperarios
                    .AnyAsync(a => a.Operario == usuarioId && a.MRH_CodigoAplicacion == 16);

                _logger.LogInformation("Verificación permiso Calidad para usuario {UsuarioId}: {TienePermiso}", 
                    usuarioId, tienePermiso);

                return tienePermiso;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar permiso Calidad para usuario {UsuarioId}", usuarioId);
                return false;
            }
        }

        public async Task<bool> VerificarAccesoEmpresaAsync(int usuarioId, short codigoEmpresa)
        {
            try
            {
                var tieneAcceso = await _sageContext.OperariosEmpresas
                    .AnyAsync(oe => oe.Operario == usuarioId && oe.CodigoEmpresa == codigoEmpresa);

                _logger.LogInformation("Verificación acceso empresa {CodigoEmpresa} para usuario {UsuarioId}: {TieneAcceso}", 
                    codigoEmpresa, usuarioId, tieneAcceso);

                return tieneAcceso;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar acceso empresa {CodigoEmpresa} para usuario {UsuarioId}", 
                    codigoEmpresa, usuarioId);
                return false;
            }
        }

        public async Task<object> BloquearStockAsync(BloquearStockDto dto)
        {
            try
            {
                // 🔷 BLOQUEO GLOBAL: Si EsBloqueoGlobal es true, bloquear en todas las ubicaciones
                if (dto.EsBloqueoGlobal)
                {
                    _logger.LogInformation("Iniciando BLOQUEO GLOBAL para empresa {CodigoEmpresa}, artículo {CodigoArticulo}, partida {Partida}",
                        dto.CodigoEmpresa, dto.CodigoArticulo, dto.LotePartida);

                    // Buscar todas las ubicaciones donde existe stock del artículo/lote
                    var todasLasUbicaciones = await _auroraSgaContext.StockDisponible
                        .Where(s => s.CodigoEmpresa == dto.CodigoEmpresa &&
                                   s.CodigoArticulo == dto.CodigoArticulo &&
                                   s.Partida == dto.LotePartida &&
                                   s.Disponible > 0)
                        .Select(s => new { s.CodigoAlmacen, s.Ubicacion })
                        .Distinct()
                        .ToListAsync();

                    if (!todasLasUbicaciones.Any())
                    {
                        _logger.LogWarning("No se encontró stock disponible para bloqueo global - Empresa: {CodigoEmpresa}, Artículo: {CodigoArticulo}, Partida: {Partida}",
                            dto.CodigoEmpresa, dto.CodigoArticulo, dto.LotePartida);
                        return new { 
                            message = "No se encontró stock disponible para este artículo y lote en ninguna ubicación",
                            codigoEmpresa = dto.CodigoEmpresa,
                            codigoArticulo = dto.CodigoArticulo,
                            lotePartida = dto.LotePartida
                        };
                    }

                    var bloqueosCreados = new List<Guid>();
                    var bloqueosDuplicados = 0;

                    // Validar tipo de bloqueo
                    var tipoBloqueo = !string.IsNullOrWhiteSpace(dto.TipoBloqueo) 
                        ? dto.TipoBloqueo.ToUpper() 
                        : "TOTAL";
                    
                    if (tipoBloqueo != "TOTAL" && tipoBloqueo != "SOLO_PULMON")
                    {
                        _logger.LogWarning("Tipo de bloqueo inválido: {TipoBloqueo}. Se usará TOTAL por defecto.", dto.TipoBloqueo);
                        tipoBloqueo = "TOTAL";
                    }

                    // Crear bloqueo para cada ubicación
                    foreach (var ubicacion in todasLasUbicaciones)
                    {
                        // Verificar si ya existe bloqueo en esta ubicación
                        var queryBloqueo = _auroraSgaContext.BloqueosCalidad
                            .Where(b => b.CodigoEmpresa == dto.CodigoEmpresa &&
                                       b.CodigoArticulo == dto.CodigoArticulo &&
                                       b.LotePartida == dto.LotePartida &&
                                       b.CodigoAlmacen == ubicacion.CodigoAlmacen &&
                                       b.Bloqueado == true);

                        if (!string.IsNullOrWhiteSpace(ubicacion.Ubicacion))
                        {
                            queryBloqueo = queryBloqueo.Where(b => b.Ubicacion == ubicacion.Ubicacion);
                        }
                        else
                        {
                            queryBloqueo = queryBloqueo.Where(b => string.IsNullOrEmpty(b.Ubicacion));
                        }

                        if (await queryBloqueo.AnyAsync())
                        {
                            bloqueosDuplicados++;
                            _logger.LogInformation("Bloqueo ya existe en {Almacen}-{Ubicacion}, se omite", 
                                ubicacion.CodigoAlmacen, ubicacion.Ubicacion ?? "(sin ubicación)");
                            continue;
                        }

                        // Crear bloqueo para esta ubicación
                        var bloqueo = new BloqueoCalidad
                        {
                            Id = Guid.NewGuid(),
                            CodigoEmpresa = dto.CodigoEmpresa,
                            CodigoArticulo = dto.CodigoArticulo,
                            LotePartida = dto.LotePartida,
                            CodigoAlmacen = ubicacion.CodigoAlmacen,
                            Ubicacion = ubicacion.Ubicacion,
                            Bloqueado = true,
                            TipoBloqueo = tipoBloqueo,
                            UsuarioBloqueoId = dto.UsuarioId,
                            FechaBloqueo = DateTime.Now,
                            ComentarioBloqueo = $"[BLOQUEO GLOBAL] {dto.ComentarioBloqueo}",
                            FechaCreacion = DateTime.Now,
                            FechaModificacion = DateTime.Now
                        };

                        _auroraSgaContext.BloqueosCalidad.Add(bloqueo);
                        bloqueosCreados.Add(bloqueo.Id);
                    }

                    await _auroraSgaContext.SaveChangesAsync();

                    _logger.LogInformation("Bloqueo global completado: {Creados} bloqueos creados, {Duplicados} ya existían", 
                        bloqueosCreados.Count, bloqueosDuplicados);

                    // Notificar a administradores y supervisores
                    try
                    {
                        var nombreUsuario = await ObtenerNombreUsuarioAsync(dto.UsuarioId);
                        var tipoBloqueoTexto = tipoBloqueo == "TOTAL" ? "TOTAL" : "SOLO PULMÓN";
                        var almacenesUnicos = todasLasUbicaciones.Select(u => u.CodigoAlmacen).Distinct().ToList();
                        var titulo = $"Bloqueo Global Aplicado - {tipoBloqueoTexto}";
                        var mensaje = $"Bloqueo Global Aplicado - {tipoBloqueoTexto}\n" +
                            $"Artículo: {dto.CodigoArticulo}\n" +
                            $"Lote: {dto.LotePartida}\n" +
                            $"Ubicaciones bloqueadas: {bloqueosCreados.Count}\n" +
                            $"Almacenes: {string.Join(", ", almacenesUnicos)}\n" +
                            $"Motivo: {dto.ComentarioBloqueo}\n" +
                            $"Usuario: {nombreUsuario}\n" +
                            $"Fecha: {DateTime.Now:dd/MM/yyyy HH:mm}";

                    // 1. Notificar a supervisores con acceso a los almacenes afectados
                    var supervisoresNotificados = new HashSet<int>();
                    foreach (var almacen in almacenesUnicos)
                    {
                        var supervisoresIds = await ObtenerSupervisoresConAccesoAlmacenAsync(almacen, dto.CodigoEmpresa, dto.UsuarioId);
                        foreach (var supervisorId in supervisoresIds)
                        {
                            if (!supervisoresNotificados.Contains(supervisorId))
                            {
                                supervisoresNotificados.Add(supervisorId);
                                try
                                {
                                    await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                                        supervisorId,
                                        "BLOQUEO_CALIDAD",
                                        titulo,
                                        mensaje,
                                        bloqueosCreados.FirstOrDefault(),
                                        null,
                                        null,
                                        "warning");
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error al notificar supervisor {SupervisorId} sobre bloqueo global", supervisorId);
                                }
                            }
                        }
                    }

                    // 2. Notificar a todos los administradores (IdRol == 3)
                    var administradoresIds = await _auroraSgaContext.Usuarios
                        .Where(u => u.IdRol == 3)
                        .Select(u => u.IdUsuario)
                        .ToListAsync();

                    // Excluir al usuario si es admin
                    var usuarioEsAdmin = await _auroraSgaContext.Usuarios
                        .Where(u => u.IdUsuario == dto.UsuarioId && u.IdRol == 3)
                        .AnyAsync();
                    
                    if (usuarioEsAdmin)
                    {
                        administradoresIds = administradoresIds.Where(id => id != dto.UsuarioId).ToList();
                    }

                    foreach (var adminId in administradoresIds)
                    {
                        try
                        {
                            await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                                adminId,
                                "BLOQUEO_CALIDAD",
                                titulo,
                                mensaje,
                                bloqueosCreados.FirstOrDefault(),
                                null,
                                null,
                                "warning");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error al notificar administrador {AdminId} sobre bloqueo global", adminId);
                        }
                    }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al enviar notificación de bloqueo global");
                    }

                    return new { 
                        Mensaje = $"Bloqueo global aplicado: {bloqueosCreados.Count} ubicaciones bloqueadas",
                        UbicacionesBloqueadas = bloqueosCreados.Count,
                        UbicacionesYaBloqueadas = bloqueosDuplicados,
                        IdsBloqueos = bloqueosCreados,
                        TipoBloqueo = tipoBloqueo
                    };
                }

                // 🔷 BLOQUEO ESPECÍFICO: Lógica existente para bloqueo en ubicación específica
                _logger.LogInformation("Iniciando bloqueo de stock para empresa {CodigoEmpresa}, artículo {CodigoArticulo}, partida {Partida}, almacén {Almacen}, ubicación {Ubicacion}",
                    dto.CodigoEmpresa, dto.CodigoArticulo, dto.LotePartida, dto.CodigoAlmacen, dto.Ubicacion ?? "(sin ubicación)");

                // 1. Verificar que no esté ya bloqueado en esta ubicación específica
                var queryBloqueoEspecifico = _auroraSgaContext.BloqueosCalidad
                    .Where(b => b.CodigoEmpresa == dto.CodigoEmpresa &&
                               b.CodigoArticulo == dto.CodigoArticulo &&
                               b.LotePartida == dto.LotePartida &&
                               b.CodigoAlmacen == dto.CodigoAlmacen &&
                               b.Bloqueado == true);

                // Verificar por ubicación específica
                if (!string.IsNullOrWhiteSpace(dto.Ubicacion))
                {
                    queryBloqueoEspecifico = queryBloqueoEspecifico.Where(b => b.Ubicacion == dto.Ubicacion);
                }
                else
                {
                    queryBloqueoEspecifico = queryBloqueoEspecifico.Where(b => string.IsNullOrEmpty(b.Ubicacion));
                }

                var yaBloqueado = await queryBloqueoEspecifico.AnyAsync();

                if (yaBloqueado)
                {
                    _logger.LogWarning("Stock ya está bloqueado para empresa {CodigoEmpresa}, artículo {CodigoArticulo}, partida {Partida}, almacén {Almacen}, ubicación {Ubicacion}",
                        dto.CodigoEmpresa, dto.CodigoArticulo, dto.LotePartida, dto.CodigoAlmacen, dto.Ubicacion ?? "(sin ubicación)");
                    return new { 
                        message = "El stock ya está bloqueado en esta ubicación",
                        codigoEmpresa = dto.CodigoEmpresa,
                        codigoArticulo = dto.CodigoArticulo,
                        lotePartida = dto.LotePartida,
                        codigoAlmacen = dto.CodigoAlmacen,
                        ubicacion = dto.Ubicacion
                    };
                }

                // 2. Verificar que el stock existe en la ubicación especificada
                var queryStock = _auroraSgaContext.StockDisponible
                    .Where(s => s.CodigoEmpresa == dto.CodigoEmpresa &&
                               s.CodigoArticulo == dto.CodigoArticulo &&
                               s.Partida == dto.LotePartida &&
                               s.CodigoAlmacen == dto.CodigoAlmacen &&
                               s.Disponible > 0);

                // Filtrar por ubicación si se especifica
                if (!string.IsNullOrWhiteSpace(dto.Ubicacion))
                {
                    queryStock = queryStock.Where(s => s.Ubicacion == dto.Ubicacion);
                }
                else
                {
                    queryStock = queryStock.Where(s => string.IsNullOrEmpty(s.Ubicacion));
                }

                var stockExiste = await queryStock.AnyAsync();

                if (!stockExiste)
                {
                    _logger.LogWarning("Stock no encontrado para empresa {CodigoEmpresa}, artículo {CodigoArticulo}, partida {Partida}, almacén {Almacen}, ubicación {Ubicacion}",
                        dto.CodigoEmpresa, dto.CodigoArticulo, dto.LotePartida, dto.CodigoAlmacen, dto.Ubicacion ?? "(sin ubicación)");
                    return new { 
                        message = "No se encontró stock disponible para los parámetros especificados en esta ubicación",
                        codigoEmpresa = dto.CodigoEmpresa,
                        codigoArticulo = dto.CodigoArticulo,
                        lotePartida = dto.LotePartida,
                        codigoAlmacen = dto.CodigoAlmacen,
                        ubicacion = dto.Ubicacion
                    };
                }

                // 3. Crear bloqueo
                var tipoBloqueoEspecifico = !string.IsNullOrWhiteSpace(dto.TipoBloqueo) 
                    ? dto.TipoBloqueo.ToUpper() 
                    : "TOTAL";
                
                // Validar que el tipo sea válido
                if (tipoBloqueoEspecifico != "TOTAL" && tipoBloqueoEspecifico != "SOLO_PULMON")
                {
                    _logger.LogWarning("Tipo de bloqueo inválido: {TipoBloqueo}. Se usará TOTAL por defecto.", dto.TipoBloqueo);
                    tipoBloqueoEspecifico = "TOTAL";
                }

                var bloqueoEspecifico = new BloqueoCalidad
                {
                    Id = Guid.NewGuid(),
                    CodigoEmpresa = dto.CodigoEmpresa,
                    CodigoArticulo = dto.CodigoArticulo,
                    LotePartida = dto.LotePartida,
                    CodigoAlmacen = dto.CodigoAlmacen,
                    Ubicacion = dto.Ubicacion,
                    Bloqueado = true,
                    TipoBloqueo = tipoBloqueoEspecifico, // 🔷 NUEVO: Tipo de bloqueo
                    UsuarioBloqueoId = dto.UsuarioId,
                    FechaBloqueo = DateTime.Now,
                    ComentarioBloqueo = dto.ComentarioBloqueo,
                    FechaCreacion = DateTime.Now,
                    FechaModificacion = DateTime.Now
                };

                _auroraSgaContext.BloqueosCalidad.Add(bloqueoEspecifico);
                await _auroraSgaContext.SaveChangesAsync();

                _logger.LogInformation("Bloqueo creado exitosamente con ID {BloqueoId} para empresa {CodigoEmpresa}, artículo {CodigoArticulo}, partida {Partida}, almacén {Almacen}, ubicación {Ubicacion}",
                    bloqueoEspecifico.Id, dto.CodigoEmpresa, dto.CodigoArticulo, dto.LotePartida, dto.CodigoAlmacen, dto.Ubicacion ?? "(sin ubicación)");

                // Notificar a administradores y supervisores
                try
                {
                    var nombreUsuario = await ObtenerNombreUsuarioAsync(dto.UsuarioId);
                    var tipoBloqueoTexto = tipoBloqueoEspecifico == "TOTAL" ? "TOTAL" : "SOLO PULMÓN";
                    var titulo = $"Stock Bloqueado - {tipoBloqueoTexto}";
                    var mensaje = $"Stock Bloqueado - {tipoBloqueoTexto}\n" +
                        $"Artículo: {dto.CodigoArticulo}\n" +
                        $"Lote: {dto.LotePartida}\n" +
                        $"Almacén: {dto.CodigoAlmacen}\n" +
                        $"Ubicación: {dto.Ubicacion ?? "(sin ubicación)"}\n" +
                        $"Motivo: {dto.ComentarioBloqueo}\n" +
                        $"Usuario: {nombreUsuario}\n" +
                        $"Fecha: {bloqueoEspecifico.FechaBloqueo:dd/MM/yyyy HH:mm}";

                    await NotificarBloqueoDesbloqueoAsync(
                        "BLOQUEO_CALIDAD",
                        titulo,
                        mensaje,
                        bloqueoEspecifico.Id,
                        dto.CodigoAlmacen,
                        dto.CodigoEmpresa,
                        dto.UsuarioId,
                        "warning");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al enviar notificación de bloqueo específico");
                }

                return new { 
                    Id = bloqueoEspecifico.Id, 
                    Mensaje = "Stock bloqueado exitosamente",
                    FechaBloqueo = bloqueoEspecifico.FechaBloqueo
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al bloquear stock para empresa {CodigoEmpresa}, artículo {CodigoArticulo}, partida {Partida}, almacén {Almacen}, ubicación {Ubicacion}",
                    dto.CodigoEmpresa, dto.CodigoArticulo, dto.LotePartida, dto.CodigoAlmacen, dto.Ubicacion ?? "(sin ubicación)");
                throw;
            }
        }

        public async Task<bool> EstaStockBloqueadoAsync(short codigoEmpresa, string codigoArticulo, string lotePartida, string codigoAlmacen, string? ubicacion = null)
        {
            try
            {
                var query = _auroraSgaContext.BloqueosCalidad
                    .Where(b => b.CodigoEmpresa == codigoEmpresa &&
                               b.CodigoArticulo == codigoArticulo &&
                               b.LotePartida == lotePartida &&
                               b.CodigoAlmacen == codigoAlmacen &&
                               b.Bloqueado == true);

                // Verificar por ubicación específica
                if (!string.IsNullOrWhiteSpace(ubicacion))
                {
                    query = query.Where(b => b.Ubicacion == ubicacion);
                }
                else
                {
                    // Si no se especifica ubicación, buscar bloqueos sin ubicación
                    query = query.Where(b => string.IsNullOrEmpty(b.Ubicacion));
                }

                var estaBloqueado = await query.AnyAsync();

                _logger.LogInformation("Verificación de bloqueo para empresa {CodigoEmpresa}, artículo {CodigoArticulo}, partida {Partida}, almacén {Almacen}, ubicación {Ubicacion}: {EstaBloqueado}",
                    codigoEmpresa, codigoArticulo, lotePartida, codigoAlmacen, ubicacion ?? "(sin ubicación)", estaBloqueado);

                return estaBloqueado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar bloqueo para empresa {CodigoEmpresa}, artículo {CodigoArticulo}, partida {Partida}, almacén {Almacen}, ubicación {Ubicacion}",
                    codigoEmpresa, codigoArticulo, lotePartida, codigoAlmacen, ubicacion ?? "(sin ubicación)");
                return false;
            }
        }

        public async Task<object> DesbloquearStockAsync(DesbloquearStockDto dto)
        {
            try
            {
                // 🔷 DESBLOQUEO GLOBAL: Si EsDesbloqueoGlobal es true, desbloquear en todas las ubicaciones
                if (dto.EsDesbloqueoGlobal && dto.CodigoEmpresa.HasValue && 
                    !string.IsNullOrWhiteSpace(dto.CodigoArticulo) && 
                    !string.IsNullOrWhiteSpace(dto.LotePartida))
                {
                    _logger.LogInformation("Iniciando DESBLOQUEO GLOBAL para empresa {CodigoEmpresa}, artículo {CodigoArticulo}, partida {Partida}",
                        dto.CodigoEmpresa, dto.CodigoArticulo, dto.LotePartida);

                    // Buscar todos los bloqueos activos del artículo y lote
                    var bloqueosGlobales = await _auroraSgaContext.BloqueosCalidad
                        .Where(b => b.CodigoEmpresa == dto.CodigoEmpresa.Value &&
                                   b.CodigoArticulo == dto.CodigoArticulo &&
                                   b.LotePartida == dto.LotePartida &&
                                   b.Bloqueado == true)
                        .ToListAsync();

                    if (!bloqueosGlobales.Any())
                    {
                        _logger.LogWarning("No se encontraron bloqueos activos para desbloqueo global - Empresa: {CodigoEmpresa}, Artículo: {CodigoArticulo}, Partida: {Partida}",
                            dto.CodigoEmpresa, dto.CodigoArticulo, dto.LotePartida);
                        return new { 
                            message = "No se encontraron bloqueos activos para este artículo y lote",
                            codigoEmpresa = dto.CodigoEmpresa,
                            codigoArticulo = dto.CodigoArticulo,
                            lotePartida = dto.LotePartida
                        };
                    }

                    var bloqueosDesbloqueados = new List<Guid>();
                    var fechaDesbloqueo = DateTime.Now;

                    // Desbloquear todos los bloqueos encontrados
                    foreach (var bloqueoGlobal in bloqueosGlobales)
                    {
                        bloqueoGlobal.Bloqueado = false;
                        bloqueoGlobal.UsuarioDesbloqueoId = dto.UsuarioId;
                        bloqueoGlobal.FechaDesbloqueo = fechaDesbloqueo;
                        bloqueoGlobal.ComentarioDesbloqueo = $"[DESBLOQUEO GLOBAL] {dto.ComentarioDesbloqueo}";
                        bloqueoGlobal.FechaModificacion = DateTime.Now;
                        bloqueosDesbloqueados.Add(bloqueoGlobal.Id);
                    }

                    await _auroraSgaContext.SaveChangesAsync();

                    _logger.LogInformation("Desbloqueo global completado: {Desbloqueados} bloqueos desbloqueados", 
                        bloqueosDesbloqueados.Count);

                    // Notificar a administradores y supervisores
                    try
                    {
                        var nombreUsuario = await ObtenerNombreUsuarioAsync(dto.UsuarioId);
                        var almacenesUnicos = bloqueosGlobales.Select(b => b.CodigoAlmacen).Distinct().ToList();
                        var titulo = "Desbloqueo Global Aplicado";
                        var mensaje = $"Desbloqueo Global Aplicado\n" +
                            $"Artículo: {dto.CodigoArticulo}\n" +
                            $"Lote: {dto.LotePartida}\n" +
                            $"Ubicaciones desbloqueadas: {bloqueosDesbloqueados.Count}\n" +
                            $"Almacenes: {string.Join(", ", almacenesUnicos)}\n" +
                            $"Motivo: {dto.ComentarioDesbloqueo}\n" +
                            $"Usuario: {nombreUsuario}\n" +
                            $"Fecha: {fechaDesbloqueo:dd/MM/yyyy HH:mm}";

                        // 1. Notificar a supervisores con acceso a los almacenes afectados
                        var supervisoresNotificados = new HashSet<int>();
                        foreach (var almacen in almacenesUnicos)
                        {
                            var supervisoresIds = await ObtenerSupervisoresConAccesoAlmacenAsync(almacen, dto.CodigoEmpresa.Value, dto.UsuarioId);
                            foreach (var supervisorId in supervisoresIds)
                            {
                                if (!supervisoresNotificados.Contains(supervisorId))
                                {
                                    supervisoresNotificados.Add(supervisorId);
                                    try
                                    {
                                        await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                                            supervisorId,
                                            "DESBLOQUEO_CALIDAD",
                                            titulo,
                                            mensaje,
                                            bloqueosDesbloqueados.FirstOrDefault(),
                                            null,
                                            null,
                                            "success");
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Error al notificar supervisor {SupervisorId} sobre desbloqueo global", supervisorId);
                                    }
                                }
                            }
                        }

                        // 2. Notificar a todos los administradores (IdRol == 3)
                        var administradoresIds = await _auroraSgaContext.Usuarios
                            .Where(u => u.IdRol == 3)
                            .Select(u => u.IdUsuario)
                            .ToListAsync();

                        // Excluir al usuario si es admin
                        var usuarioEsAdmin = await _auroraSgaContext.Usuarios
                            .Where(u => u.IdUsuario == dto.UsuarioId && u.IdRol == 3)
                            .AnyAsync();
                        
                        if (usuarioEsAdmin)
                        {
                            administradoresIds = administradoresIds.Where(id => id != dto.UsuarioId).ToList();
                        }

                        foreach (var adminId in administradoresIds)
                        {
                            try
                            {
                                await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                                    adminId,
                                    "DESBLOQUEO_CALIDAD",
                                    titulo,
                                    mensaje,
                                    bloqueosDesbloqueados.FirstOrDefault(),
                                    null,
                                    null,
                                    "success");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error al notificar administrador {AdminId} sobre desbloqueo global", adminId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al enviar notificación de desbloqueo global");
                    }

                    return new { 
                        Mensaje = $"Desbloqueo global aplicado: {bloqueosDesbloqueados.Count} ubicaciones desbloqueadas",
                        UbicacionesDesbloqueadas = bloqueosDesbloqueados.Count,
                        IdsBloqueos = bloqueosDesbloqueados,
                        FechaDesbloqueo = fechaDesbloqueo
                    };
                }

                // 🔷 DESBLOQUEO ESPECÍFICO: Lógica existente para desbloqueo de un bloqueo específico
                if (!dto.IdBloqueo.HasValue)
                {
                    _logger.LogWarning("Desbloqueo específico requiere IdBloqueo");
                    return new { 
                        message = "Se requiere IdBloqueo para desbloqueo específico o campos para desbloqueo global"
                    };
                }

                _logger.LogInformation("Iniciando desbloqueo de stock para bloqueo ID {BloqueoId}",
                    dto.IdBloqueo);

                // 1. Buscar el bloqueo
                var bloqueo = await _auroraSgaContext.BloqueosCalidad
                    .FirstOrDefaultAsync(b => b.Id == dto.IdBloqueo.Value);

                if (bloqueo == null)
                {
                    _logger.LogWarning("Bloqueo no encontrado con ID {BloqueoId}", dto.IdBloqueo);
                    return new { 
                        message = "No se encontró el bloqueo especificado",
                        idBloqueo = dto.IdBloqueo
                    };
                }

                // 2. Verificar que esté bloqueado
                if (!bloqueo.Bloqueado)
                {
                    _logger.LogWarning("El bloqueo ID {BloqueoId} ya está desbloqueado", dto.IdBloqueo);
                    return new { 
                        message = "El bloqueo especificado ya está desbloqueado",
                        idBloqueo = dto.IdBloqueo
                    };
                }

                // 3. Actualizar bloqueo
                bloqueo.Bloqueado = false;
                bloqueo.UsuarioDesbloqueoId = dto.UsuarioId;
                bloqueo.FechaDesbloqueo = DateTime.Now;
                bloqueo.ComentarioDesbloqueo = dto.ComentarioDesbloqueo;
                bloqueo.FechaModificacion = DateTime.Now;

                await _auroraSgaContext.SaveChangesAsync();

                _logger.LogInformation("Desbloqueo ejecutado exitosamente para bloqueo ID {BloqueoId}",
                    dto.IdBloqueo);

                // Notificar a administradores y supervisores
                try
                {
                    var nombreUsuario = await ObtenerNombreUsuarioAsync(dto.UsuarioId);
                    var titulo = "Stock Desbloqueado";
                    var mensaje = $"Stock Desbloqueado\n" +
                        $"Artículo: {bloqueo.CodigoArticulo}\n" +
                        $"Lote: {bloqueo.LotePartida}\n" +
                        $"Almacén: {bloqueo.CodigoAlmacen}\n" +
                        $"Ubicación: {bloqueo.Ubicacion ?? "(sin ubicación)"}\n" +
                        $"Motivo: {dto.ComentarioDesbloqueo}\n" +
                        $"Usuario: {nombreUsuario}\n" +
                        $"Fecha: {bloqueo.FechaDesbloqueo:dd/MM/yyyy HH:mm}";

                    await NotificarBloqueoDesbloqueoAsync(
                        "DESBLOQUEO_CALIDAD",
                        titulo,
                        mensaje,
                        bloqueo.Id,
                        bloqueo.CodigoAlmacen,
                        bloqueo.CodigoEmpresa,
                        dto.UsuarioId,
                        "success");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al enviar notificación de desbloqueo específico");
                }

                return new { 
                    Id = bloqueo.Id, 
                    Mensaje = "Stock desbloqueado exitosamente",
                    FechaDesbloqueo = bloqueo.FechaDesbloqueo
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al desbloquear stock");
                throw;
            }
        }

        public async Task<List<BloqueoCalidadDto>> ObtenerBloqueosAsync(short codigoEmpresa, bool? soloBloqueados = null)
        {
            try
            {
                _logger.LogInformation("Obteniendo bloqueos para empresa {CodigoEmpresa}, soloBloqueados: {SoloBloqueados}",
                    codigoEmpresa, soloBloqueados);

                var query = _auroraSgaContext.BloqueosCalidad
                    .Where(b => b.CodigoEmpresa == codigoEmpresa);

                // Filtro opcional por estado
                if (soloBloqueados.HasValue)
                {
                    query = query.Where(b => b.Bloqueado == soloBloqueados.Value);
                }

                var bloqueos = await query
                    .OrderByDescending(b => b.FechaBloqueo)
                    .ToListAsync();

                var resultado = new List<BloqueoCalidadDto>();

                foreach (var bloqueo in bloqueos)
                {
                    // Obtener información del artículo desde StockDisponible
                    var stockInfo = await _auroraSgaContext.StockDisponible
                        .FirstOrDefaultAsync(s => s.CodigoEmpresa == bloqueo.CodigoEmpresa &&
                                                 s.CodigoArticulo == bloqueo.CodigoArticulo &&
                                                 s.Partida == bloqueo.LotePartida);

                    var bloqueoDto = new BloqueoCalidadDto
                    {
                        Id = bloqueo.Id,
                        CodigoArticulo = bloqueo.CodigoArticulo,
                        DescripcionArticulo = stockInfo?.DescripcionArticulo ?? "N/A",
                        LotePartida = bloqueo.LotePartida,
                        CodigoAlmacen = bloqueo.CodigoAlmacen,
                        Almacen = stockInfo?.Almacen ?? "N/A",
                        Ubicacion = bloqueo.Ubicacion,
                        Bloqueado = bloqueo.Bloqueado,
                        TipoBloqueo = bloqueo.TipoBloqueo ?? "TOTAL", // 🔷 NUEVO: Tipo de bloqueo
                        UsuarioBloqueo = bloqueo.UsuarioBloqueoId.ToString(),
                        FechaBloqueo = bloqueo.FechaBloqueo,
                        ComentarioBloqueo = bloqueo.ComentarioBloqueo,
                        UsuarioDesbloqueo = bloqueo.UsuarioDesbloqueoId?.ToString(),
                        FechaDesbloqueo = bloqueo.FechaDesbloqueo,
                        ComentarioDesbloqueo = bloqueo.ComentarioDesbloqueo
                    };

                    resultado.Add(bloqueoDto);
                }

                _logger.LogInformation("Encontrados {Count} bloqueos para empresa {CodigoEmpresa}",
                    resultado.Count, codigoEmpresa);

                return resultado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener bloqueos para empresa {CodigoEmpresa}",
                    codigoEmpresa);
                throw;
            }
        }

        /// <summary>
        /// 🔷 NUEVO: Obtiene información de bloqueos de calidad para una lista de artículos
        /// </summary>
        public async Task<Dictionary<string, object>> ObtenerBloqueosPorArticulosAsync(
            short codigoEmpresa, 
            List<string> codigosArticulos)
        {
            try
            {
                _logger.LogInformation("Obteniendo bloqueos para empresa {CodigoEmpresa}, artículos: {Count}",
                    codigoEmpresa, codigosArticulos.Count);

                var resultado = new Dictionary<string, object>();

                foreach (var codigoArticulo in codigosArticulos)
                {
                    // Buscar bloqueos activos para este artículo
                    var bloqueos = await _auroraSgaContext.BloqueosCalidad
                        .Where(b => b.CodigoEmpresa == codigoEmpresa &&
                                   b.CodigoArticulo == codigoArticulo &&
                                   b.Bloqueado == true)
                        .OrderByDescending(b => b.FechaBloqueo)
                        .ToListAsync();

                    if (bloqueos.Any())
                    {
                        // Si hay bloqueos, tomar el más reciente
                        var bloqueoMasReciente = bloqueos.First();
                         resultado[codigoArticulo] = new
                         {
                             isBloqueado = true,
                             motivoBloqueo = bloqueoMasReciente.ComentarioBloqueo,
                             fechaBloqueo = bloqueoMasReciente.FechaBloqueo,
                             usuarioBloqueo = bloqueoMasReciente.UsuarioBloqueoId.ToString(),
                             idBloqueo = bloqueoMasReciente.Id
                         };
                    }
                    else
                    {
                        // Si no hay bloqueos, indicar que no está bloqueado
                         resultado[codigoArticulo] = new
                         {
                             isBloqueado = false,
                             motivoBloqueo = (string?)null,
                             fechaBloqueo = (DateTime?)null,
                             usuarioBloqueo = (string?)null,
                             idBloqueo = (Guid?)null
                         };
                    }
                }

                _logger.LogInformation("Procesados {Count} artículos para empresa {CodigoEmpresa}",
                    resultado.Count, codigoEmpresa);

                return resultado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener bloqueos por artículos para empresa {CodigoEmpresa}",
                    codigoEmpresa);
                throw;
            }
        }

        /// <summary>
        /// 🔷 NUEVO: Copia un bloqueo de calidad desde una ubicación origen a una ubicación destino
        /// </summary>
        public async Task<bool> CopiarBloqueoCalidadAsync(
            short codigoEmpresa,
            string codigoArticulo,
            string lotePartida,
            string almacenOrigen,
            string? ubicacionOrigen,
            string almacenDestino,
            string? ubicacionDestino)
        {
            try
            {
                // Normalizar ubicaciones (null o vacío = "sin ubicación")
                var ubicacionOrigenNormalizada = string.IsNullOrWhiteSpace(ubicacionOrigen) ? "" : ubicacionOrigen.Trim();
                var ubicacionDestinoNormalizada = string.IsNullOrWhiteSpace(ubicacionDestino) ? "" : ubicacionDestino.Trim();

                _logger.LogInformation($"🔍 CopiarBloqueoCalidad - Artículo: {codigoArticulo}, Partida: {lotePartida}, Origen: {almacenOrigen}-{ubicacionOrigenNormalizada}, Destino: {almacenDestino}-{ubicacionDestinoNormalizada}");

                // 1. Buscar bloqueo en origen
                var queryBloqueoOrigen = _auroraSgaContext.BloqueosCalidad
                    .Where(b => b.CodigoEmpresa == codigoEmpresa &&
                               b.CodigoArticulo == codigoArticulo &&
                               b.LotePartida == lotePartida &&
                               b.CodigoAlmacen == almacenOrigen &&
                               b.Bloqueado == true);

                // Filtrar por ubicación origen
                if (!string.IsNullOrWhiteSpace(ubicacionOrigenNormalizada))
                {
                    queryBloqueoOrigen = queryBloqueoOrigen.Where(b => b.Ubicacion == ubicacionOrigenNormalizada);
                }
                else
                {
                    queryBloqueoOrigen = queryBloqueoOrigen.Where(b => string.IsNullOrEmpty(b.Ubicacion));
                }

                var bloqueoOrigen = await queryBloqueoOrigen
                    .OrderByDescending(b => b.FechaBloqueo)
                    .FirstOrDefaultAsync();

                if (bloqueoOrigen == null)
                {
                    _logger.LogInformation($"✅ No hay bloqueo en origen para copiar - Artículo: {codigoArticulo}, Partida: {lotePartida}, Origen: {almacenOrigen}-{ubicacionOrigenNormalizada}");
                    return false; // No hay bloqueo en origen, no hay nada que copiar
                }

                // 2. Verificar si ya existe bloqueo en destino (evitar duplicados)
                var queryBloqueoDestino = _auroraSgaContext.BloqueosCalidad
                    .Where(b => b.CodigoEmpresa == codigoEmpresa &&
                               b.CodigoArticulo == codigoArticulo &&
                               b.LotePartida == lotePartida &&
                               b.CodigoAlmacen == almacenDestino &&
                               b.Bloqueado == true);

                // Filtrar por ubicación destino
                if (!string.IsNullOrWhiteSpace(ubicacionDestinoNormalizada))
                {
                    queryBloqueoDestino = queryBloqueoDestino.Where(b => b.Ubicacion == ubicacionDestinoNormalizada);
                }
                else
                {
                    queryBloqueoDestino = queryBloqueoDestino.Where(b => string.IsNullOrEmpty(b.Ubicacion));
                }

                var bloqueoDestinoExistente = await queryBloqueoDestino.AnyAsync();

                if (bloqueoDestinoExistente)
                {
                    _logger.LogInformation($"⚠️ Ya existe bloqueo en destino - Artículo: {codigoArticulo}, Partida: {lotePartida}, Destino: {almacenDestino}-{ubicacionDestinoNormalizada}. No se copia para evitar duplicados.");
                    return false; // Ya existe bloqueo en destino, no copiar
                }

                // 3. Crear bloqueo en destino copiando datos del origen
                var bloqueoDestino = new BloqueoCalidad
                {
                    Id = Guid.NewGuid(),
                    CodigoEmpresa = bloqueoOrigen.CodigoEmpresa,
                    CodigoArticulo = bloqueoOrigen.CodigoArticulo,
                    LotePartida = bloqueoOrigen.LotePartida,
                    CodigoAlmacen = almacenDestino,
                    Ubicacion = ubicacionDestinoNormalizada,
                    Bloqueado = true,
                    TipoBloqueo = bloqueoOrigen.TipoBloqueo ?? "TOTAL", // Copiar el tipo de bloqueo
                    UsuarioBloqueoId = bloqueoOrigen.UsuarioBloqueoId,
                    FechaBloqueo = DateTime.Now, // Nueva fecha de bloqueo
                    ComentarioBloqueo = ConstruirComentarioBloqueoCopiado(bloqueoOrigen.ComentarioBloqueo, almacenOrigen, ubicacionOrigenNormalizada),
                    FechaCreacion = DateTime.Now,
                    FechaModificacion = DateTime.Now
                };

                _auroraSgaContext.BloqueosCalidad.Add(bloqueoDestino);
                await _auroraSgaContext.SaveChangesAsync();

                _logger.LogInformation($"✅ Bloqueo copiado exitosamente - ID Origen: {bloqueoOrigen.Id}, ID Destino: {bloqueoDestino.Id}, Tipo: {bloqueoDestino.TipoBloqueo}");

                // 4. Desbloquear automáticamente el bloqueo origen si es SOLO_PULMON y el stock llega a 0
                try
                {
                    // Verificar tipo de bloqueo
                    var tipoBloqueo = bloqueoOrigen.TipoBloqueo?.ToUpper() ?? "TOTAL";
                    
                    // Solo desbloquear automáticamente bloqueos SOLO_PULMON
                    // Los bloqueos TOTAL no permiten traspasos, por lo que nunca se moverán
                    if (tipoBloqueo != "SOLO_PULMON")
                    {
                        _logger.LogInformation($"ℹ️ Bloqueo origen es {tipoBloqueo} (ID: {bloqueoOrigen.Id}). No se desbloqueará automáticamente (solo SOLO_PULMON permite traspasos).");
                    }
                    else
                    {
                        // Verificar stock disponible en origen
                        var queryStockOrigen = _auroraSgaContext.StockDisponible
                            .Where(s => s.CodigoEmpresa == codigoEmpresa &&
                                       s.CodigoArticulo == codigoArticulo &&
                                       s.Partida == lotePartida &&
                                       s.CodigoAlmacen == almacenOrigen);
                        
                        // Filtrar por ubicación origen
                        if (!string.IsNullOrWhiteSpace(ubicacionOrigenNormalizada))
                        {
                            queryStockOrigen = queryStockOrigen.Where(s => s.Ubicacion == ubicacionOrigenNormalizada);
                        }
                        else
                        {
                            queryStockOrigen = queryStockOrigen.Where(s => string.IsNullOrEmpty(s.Ubicacion));
                        }
                        
                        var stockRestante = await queryStockOrigen.SumAsync(s => (decimal?)s.Disponible) ?? 0m;
                        
                        _logger.LogInformation($"📊 Stock restante en origen después de traspaso: {stockRestante} (Bloqueo Tipo: {tipoBloqueo})");
                        
                        // Si no queda stock, desbloquear automáticamente
                        if (stockRestante <= 0)
                        {
                            bloqueoOrigen.Bloqueado = false;
                            bloqueoOrigen.FechaDesbloqueo = DateTime.Now;
                            bloqueoOrigen.ComentarioDesbloqueo = $"Desbloqueo automático: stock agotado en {almacenOrigen}-{ubicacionOrigenNormalizada ?? "(sin ubicar)"} después de traspaso";
                            bloqueoOrigen.FechaModificacion = DateTime.Now;
                            // UsuarioDesbloqueoId queda null para indicar que fue automático
                            
                            await _auroraSgaContext.SaveChangesAsync();
                            
                            _logger.LogInformation($"✅ Bloqueo {tipoBloqueo} desbloqueado automáticamente - ID: {bloqueoOrigen.Id}, Motivo original: {bloqueoOrigen.ComentarioBloqueo}");

                            // Notificar a administradores y supervisores sobre el traspaso
                            try
                            {
                                var nombreUsuarioBloqueo = await ObtenerNombreUsuarioAsync(bloqueoOrigen.UsuarioBloqueoId);
                                var tipoBloqueoTexto = tipoBloqueo == "TOTAL" ? "TOTAL" : "SOLO PULMÓN";
                                var titulo = "Material Traspasado - Bloqueo Desbloqueado";
                                var mensaje = $"Material Traspasado - Bloqueo Desbloqueado\n" +
                                    $"Artículo: {codigoArticulo}\n" +
                                    $"Lote: {lotePartida}\n" +
                                    $"Traspasado desde: {almacenOrigen} - {ubicacionOrigenNormalizada ?? "(sin ubicar)"}\n" +
                                    $"Traspasado a: {almacenDestino} - {ubicacionDestinoNormalizada ?? "(sin ubicar)"}\n" +
                                    $"Motivo bloqueo original: {bloqueoOrigen.ComentarioBloqueo}\n" +
                                    $"Tipo bloqueo: {tipoBloqueoTexto}\n" +
                                    $"Usuario bloqueo original: {nombreUsuarioBloqueo}\n" +
                                    $"Fecha traspaso: {DateTime.Now:dd/MM/yyyy HH:mm}";

                                await NotificarBloqueoDesbloqueoAsync(
                                    "DESBLOQUEO_CALIDAD",
                                    titulo,
                                    mensaje,
                                    bloqueoOrigen.Id,
                                    almacenOrigen,
                                    codigoEmpresa,
                                    null,
                                    "info");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error al enviar notificación de desbloqueo automático por traspaso");
                            }
                        }
                        else
                        {
                            _logger.LogInformation($"ℹ️ Stock aún disponible ({stockRestante}), bloqueo {tipoBloqueo} permanece activo");
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Best effort - no afectar el traspaso si falla el desbloqueo
                    _logger.LogError(ex, "❌ Error al intentar desbloquear automáticamente. El traspaso continúa.");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al copiar bloqueo de calidad - Artículo: {codigoArticulo}, Partida: {lotePartida}, Origen: {almacenOrigen}-{ubicacionOrigen}, Destino: {almacenDestino}-{ubicacionDestino}");
                // En caso de error, no fallar el traspaso, solo loguear
                return false;
            }
        }

        /// <summary>
        /// Obtiene el nombre de un usuario desde la vista vUsuariosConNombre
        /// </summary>
        private async Task<string> ObtenerNombreUsuarioAsync(int usuarioId)
        {
            try
            {
                var usuario = await _auroraSgaContext.vUsuariosConNombre
                    .Where(u => u.UsuarioId == usuarioId)
                    .Select(u => u.NombreOperario)
                    .FirstOrDefaultAsync();

                return usuario ?? usuarioId.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al obtener nombre del usuario {UsuarioId}, usando ID", usuarioId);
                return usuarioId.ToString();
            }
        }

        /// <summary>
        /// Obtiene supervisores con acceso al almacén especificado
        /// </summary>
        private async Task<List<int>> ObtenerSupervisoresConAccesoAlmacenAsync(
            string codigoAlmacen,
            short codigoEmpresa,
            int? usuarioExcluir = null)
        {
            try
            {
                if (string.IsNullOrEmpty(codigoAlmacen))
                    return new List<int>();

                // Obtener IDs de operarios con acceso al almacén desde OperariosAlmacenes
                var operariosConAcceso = await _sageContext.OperariosAlmacenes
                    .Where(oa => oa.CodigoAlmacen == codigoAlmacen && oa.CodigoEmpresa == codigoEmpresa)
                    .Select(oa => oa.Operario)
                    .Distinct()
                    .ToListAsync();

                if (!operariosConAcceso.Any())
                    return new List<int>();

                // Filtrar solo supervisores (IdRol == 2) con acceso al almacén
                var query = _auroraSgaContext.Usuarios
                    .Where(u => u.IdRol == 2 && operariosConAcceso.Contains(u.IdUsuario));

                // Excluir usuario si se especifica
                if (usuarioExcluir.HasValue)
                {
                    query = query.Where(u => u.IdUsuario != usuarioExcluir.Value);
                }

                var supervisoresIds = await query
                    .Select(u => u.IdUsuario)
                    .ToListAsync();

                return supervisoresIds;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al obtener supervisores con acceso al almacén {Almacen}", codigoAlmacen);
                return new List<int>();
            }
        }

        /// <summary>
        /// Construye el comentario de bloqueo copiado, extrayendo solo el mensaje original
        /// para evitar acumulación de información de múltiples traspasos
        /// </summary>
        private string ConstruirComentarioBloqueoCopiado(string comentarioOrigen, string almacenOrigen, string? ubicacionOrigen)
        {
            // Extraer el mensaje original (antes del primer "Traspasado desde")
            string mensajeOriginal = comentarioOrigen ?? string.Empty;
            
            // Si el comentario ya contiene "Traspasado desde", extraer solo la parte original
            const string marcadorTraspaso = " - Traspasado desde";
            int indiceTraspaso = mensajeOriginal.IndexOf(marcadorTraspaso, StringComparison.OrdinalIgnoreCase);
            
            if (indiceTraspaso >= 0)
            {
                // Extraer solo el mensaje original (antes del primer "Traspasado desde")
                mensajeOriginal = mensajeOriginal.Substring(0, indiceTraspaso).Trim();
            }
            
            // Construir el nuevo comentario con el mensaje original + información del traspaso actual
            if (string.IsNullOrWhiteSpace(ubicacionOrigen))
            {
                return $"{mensajeOriginal} - Traspasado desde {almacenOrigen}";
            }
            else
            {
                return $"{mensajeOriginal} - Traspasado desde {almacenOrigen} - {ubicacionOrigen}";
            }
        }

        /// <summary>
        /// Notifica a administradores y supervisores sobre bloqueo/desbloqueo
        /// </summary>
        private async Task NotificarBloqueoDesbloqueoAsync(
            string tipoNotificacion,
            string titulo,
            string mensaje,
            Guid bloqueoId,
            string codigoAlmacen,
            short codigoEmpresa,
            int? usuarioExcluir = null,
            string tipoVisual = "info")
        {
            try
            {
                // 1. Notificar a supervisores con acceso al almacén
                var supervisoresIds = await ObtenerSupervisoresConAccesoAlmacenAsync(codigoAlmacen, codigoEmpresa, usuarioExcluir);

                if (supervisoresIds.Any())
                {
                    foreach (var supervisorId in supervisoresIds)
                    {
                        try
                        {
                            await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                                supervisorId,
                                tipoNotificacion,
                                titulo,
                                mensaje,
                                bloqueoId,
                                null,
                                null,
                                tipoVisual);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error al notificar supervisor {SupervisorId} sobre {TipoNotificacion}", supervisorId, tipoNotificacion);
                        }
                    }
                }

                // 2. Notificar a todos los administradores (IdRol == 3)
                var administradoresIds = await _auroraSgaContext.Usuarios
                    .Where(u => u.IdRol == 3)
                    .Select(u => u.IdUsuario)
                    .ToListAsync();

                // Excluir al usuario si es admin
                if (usuarioExcluir.HasValue)
                {
                    var usuarioEsAdmin = await _auroraSgaContext.Usuarios
                        .Where(u => u.IdUsuario == usuarioExcluir.Value && u.IdRol == 3)
                        .AnyAsync();
                    
                    if (usuarioEsAdmin)
                    {
                        administradoresIds = administradoresIds.Where(id => id != usuarioExcluir.Value).ToList();
                    }
                }

                if (administradoresIds.Any())
                {
                    foreach (var adminId in administradoresIds)
                    {
                        try
                        {
                            await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                                adminId,
                                tipoNotificacion,
                                titulo,
                                mensaje,
                                bloqueoId,
                                null,
                                null,
                                tipoVisual);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error al notificar administrador {AdminId} sobre {TipoNotificacion}", adminId, tipoNotificacion);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificaciones de {TipoNotificacion}", tipoNotificacion);
            }
        }

        public async Task<EstadisticasCalidadDto> ObtenerEstadisticasAsync(short codigoEmpresa)
        {
            try
            {
                _logger.LogInformation("Obteniendo estadísticas de calidad para empresa {CodigoEmpresa}", codigoEmpresa);

                // Obtener todos los bloqueos de la empresa
                var bloqueos = await _auroraSgaContext.BloqueosCalidad
                    .Where(b => b.CodigoEmpresa == codigoEmpresa)
                    .ToListAsync();

                _logger.LogInformation("Total de bloqueos encontrados en BD para empresa {CodigoEmpresa}: {Total}", codigoEmpresa, bloqueos.Count);

                // Contar bloqueados y desbloqueados
                var totalBloqueados = bloqueos.Count(b => b.Bloqueado);
                var totalDesbloqueados = bloqueos.Count(b => !b.Bloqueado);

                // Contar por tipo de bloqueo
                var bloqueosTotales = bloqueos.Count(b => b.TipoBloqueo == "TOTAL");
                var bloqueosSoloPulmon = bloqueos.Count(b => b.TipoBloqueo == "SOLO_PULMON");

                // Identificar bloqueos globales (contienen "[BLOQUEO GLOBAL]" en el comentario)
                var bloqueosGlobales = bloqueos.Count(b => 
                    !string.IsNullOrEmpty(b.ComentarioBloqueo) && 
                    b.ComentarioBloqueo.Contains("[BLOQUEO GLOBAL]", StringComparison.OrdinalIgnoreCase));

                // Bloqueos individuales = total - globales
                var bloqueosIndividuales = bloqueos.Count - bloqueosGlobales;

                // 1. Top artículos bloqueados (solo bloqueos activos)
                var bloqueosActivos = bloqueos.Where(b => b.Bloqueado).ToList();
                var topArticulos = bloqueosActivos
                    .GroupBy(b => b.CodigoArticulo)
                    .Select(g => new { CodigoArticulo = g.Key, Cantidad = g.Count() })
                    .OrderByDescending(x => x.Cantidad)
                    .Take(10)
                    .ToList();

                // Obtener descripciones de artículos (cargar todos los artículos de la empresa y filtrar en memoria)
                var codigosArticulos = topArticulos.Select(a => a.CodigoArticulo).Distinct().ToList();
                var descripcionesArticulos = new Dictionary<string, string?>();
                if (codigosArticulos.Any())
                {
                    var codigosArticulosSet = codigosArticulos.ToHashSet();
                    var articulosEmpresa = await _sageContext.Articulos
                        .Where(a => a.CodigoEmpresa == codigoEmpresa)
                        .Select(a => new { a.CodigoArticulo, a.DescripcionArticulo })
                        .ToListAsync();
                    
                    foreach (var art in articulosEmpresa)
                    {
                        if (codigosArticulosSet.Contains(art.CodigoArticulo) && 
                            !string.IsNullOrWhiteSpace(art.DescripcionArticulo))
                        {
                            descripcionesArticulos[art.CodigoArticulo] = art.DescripcionArticulo;
                        }
                    }
                }

                var topArticulosDto = topArticulos
                    .Select((a, index) => new TopArticuloBloqueadoDto
                    {
                        CodigoArticulo = a.CodigoArticulo,
                        DescripcionArticulo = descripcionesArticulos.GetValueOrDefault(a.CodigoArticulo),
                        CantidadBloqueos = a.Cantidad,
                        Posicion = index + 1
                    })
                    .ToList();

                // 2. Distribución por almacén (solo bloqueos activos)
                var distribucionAlmacenes = bloqueosActivos
                    .GroupBy(b => b.CodigoAlmacen)
                    .Select(g => new { CodigoAlmacen = g.Key, Cantidad = g.Count() })
                    .OrderByDescending(x => x.Cantidad)
                    .Take(10)
                    .ToList();

                // Obtener nombres de almacenes (cargar todos los almacenes de la empresa y filtrar en memoria)
                var codigosAlmacenes = distribucionAlmacenes.Select(a => a.CodigoAlmacen).Distinct().ToList();
                var nombresAlmacenes = new Dictionary<string, string?>();
                if (codigosAlmacenes.Any())
                {
                    var codigosAlmacenesSet = codigosAlmacenes.ToHashSet();
                    var almacenesEmpresa = await _sageContext.Almacenes
                        .Where(a => a.CodigoEmpresa == codigoEmpresa)
                        .Select(a => new { CodigoAlmacen = a.CodigoAlmacen!, Nombre = a.Almacen })
                        .ToListAsync();
                    
                    foreach (var alm in almacenesEmpresa)
                    {
                        if (codigosAlmacenesSet.Contains(alm.CodigoAlmacen))
                        {
                            nombresAlmacenes[alm.CodigoAlmacen] = alm.Nombre;
                        }
                    }
                }

                var distribucionAlmacenesDto = distribucionAlmacenes
                    .Select((a, index) => new DistribucionAlmacenDto
                    {
                        CodigoAlmacen = a.CodigoAlmacen,
                        NombreAlmacen = nombresAlmacenes.GetValueOrDefault(a.CodigoAlmacen),
                        CantidadBloqueos = a.Cantidad,
                        Posicion = index + 1
                    })
                    .ToList();

                // 3. Bloqueos recientes (últimos 10, ordenados por fecha descendente)
                var bloqueosRecientes = bloqueos
                    .OrderByDescending(b => b.FechaBloqueo)
                    .Take(10)
                    .ToList();

                // Obtener nombres de usuarios (cargar todos los usuarios de una vez)
                var usuariosIds = bloqueosRecientes.Select(b => b.UsuarioBloqueoId).Distinct().ToList();
                var nombresUsuarios = new Dictionary<int, string>();
                if (usuariosIds.Any())
                {
                    var nombreDict = await _auroraSgaContext.vUsuariosConNombre
                        .ToDictionaryAsync(x => x.UsuarioId, x => x.NombreOperario);
                    
                    foreach (var usuarioId in usuariosIds)
                    {
                        if (nombreDict.TryGetValue(usuarioId, out var nombreOperario))
                        {
                            nombresUsuarios[usuarioId] = nombreOperario ?? $"Usuario {usuarioId}";
                        }
                        else
                        {
                            nombresUsuarios[usuarioId] = $"Usuario {usuarioId}";
                        }
                    }
                }

                // Obtener descripciones para bloqueos recientes (reutilizar descripcionesArticulos si ya se cargaron)
                var codigosArticulosRecientes = bloqueosRecientes.Select(b => b.CodigoArticulo).Distinct().ToList();
                var descripcionesRecientes = new Dictionary<string, string?>();
                
                // Si ya tenemos las descripciones cargadas, reutilizarlas
                var codigosArticulosTodos = codigosArticulos.Concat(codigosArticulosRecientes).Distinct().ToList();
                if (codigosArticulosTodos.Any())
                {
                    var codigosArticulosSet = codigosArticulosTodos.ToHashSet();
                    var articulosEmpresa = await _sageContext.Articulos
                        .Where(a => a.CodigoEmpresa == codigoEmpresa)
                        .Select(a => new { a.CodigoArticulo, a.DescripcionArticulo })
                        .ToListAsync();
                    
                    foreach (var art in articulosEmpresa)
                    {
                        if (codigosArticulosSet.Contains(art.CodigoArticulo) && 
                            !string.IsNullOrWhiteSpace(art.DescripcionArticulo))
                        {
                            if (codigosArticulos.Contains(art.CodigoArticulo))
                            {
                                descripcionesArticulos[art.CodigoArticulo] = art.DescripcionArticulo;
                            }
                            if (codigosArticulosRecientes.Contains(art.CodigoArticulo))
                            {
                                descripcionesRecientes[art.CodigoArticulo] = art.DescripcionArticulo;
                            }
                        }
                    }
                }

                var bloqueosRecientesDto = bloqueosRecientes
                    .Select(b => new BloqueoRecienteDto
                    {
                        Id = b.Id,
                        CodigoArticulo = b.CodigoArticulo,
                        DescripcionArticulo = descripcionesRecientes.GetValueOrDefault(b.CodigoArticulo),
                        LotePartida = b.LotePartida,
                        CodigoAlmacen = b.CodigoAlmacen,
                        Ubicacion = b.Ubicacion,
                        TipoBloqueo = b.TipoBloqueo,
                        ComentarioBloqueo = b.ComentarioBloqueo,
                        UsuarioBloqueo = nombresUsuarios.GetValueOrDefault(b.UsuarioBloqueoId, $"Usuario {b.UsuarioBloqueoId}"),
                        FechaBloqueo = b.FechaBloqueo,
                        EsGlobal = !string.IsNullOrEmpty(b.ComentarioBloqueo) && 
                                   b.ComentarioBloqueo.Contains("[BLOQUEO GLOBAL]", StringComparison.OrdinalIgnoreCase)
                    })
                    .ToList();

                var estadisticas = new EstadisticasCalidadDto
                {
                    TotalBloqueados = totalBloqueados,
                    TotalDesbloqueados = totalDesbloqueados,
                    BloqueosTotales = bloqueosTotales,
                    BloqueosSoloPulmon = bloqueosSoloPulmon,
                    BloqueosGlobales = bloqueosGlobales,
                    BloqueosIndividuales = bloqueosIndividuales,
                    TopArticulosBloqueados = topArticulosDto,
                    DistribucionPorAlmacen = distribucionAlmacenesDto,
                    BloqueosRecientes = bloqueosRecientesDto
                };

                _logger.LogInformation("Estadísticas calculadas - Bloqueados: {Bloqueados}, Desbloqueados: {Desbloqueados}, TOTAL: {Totales}, SOLO_PULMON: {SoloPulmon}, Globales: {Globales}, Individuales: {Individuales}",
                    estadisticas.TotalBloqueados, estadisticas.TotalDesbloqueados, estadisticas.BloqueosTotales, 
                    estadisticas.BloqueosSoloPulmon, estadisticas.BloqueosGlobales, estadisticas.BloqueosIndividuales);

                return estadisticas;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener estadísticas de calidad para empresa {CodigoEmpresa}", codigoEmpresa);
                // Retornar estadísticas en cero en caso de error
                return new EstadisticasCalidadDto();
            }
        }
    }
}
