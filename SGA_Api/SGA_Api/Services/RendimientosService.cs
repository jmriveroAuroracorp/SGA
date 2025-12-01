using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Rendimientos;
using SGA_Api.Models.Traspasos;
using SGA_Api.Models.Inventario;
using SGA_Api.Models.Conteos;
using SGA_Api.Models.Palet;
using SGA_Api.Models.Registro;

namespace SGA_Api.Services
{
    public class RendimientosService
    {
        private readonly AuroraSgaDbContext _context;
        private readonly SageDbContext _sageContext;
        private readonly ILogger<RendimientosService> _logger;

        public RendimientosService(
            AuroraSgaDbContext context,
            SageDbContext sageContext,
            ILogger<RendimientosService> logger)
        {
            _context = context;
            _sageContext = sageContext;
            _logger = logger;
        }

        public async Task<List<RendimientoOperarioDto>> ObtenerRendimientoOperariosAsync(FiltroRendimientosDto filtros)
        {
            try
            {
                _logger.LogInformation("Calculando rendimiento de operarios con filtros: {Filtros}", 
                    System.Text.Json.JsonSerializer.Serialize(filtros));

                var query = _context.Traspasos.AsQueryable();
                
                // Aplicar filtros de fecha (incluir todo el día)
                if (filtros.FechaDesde.HasValue)
                {
                    var fechaDesdeInicio = filtros.FechaDesde.Value.Date; // 00:00:00
                    _logger.LogInformation("Filtro FechaDesde: {FechaOriginal} -> {FechaAplicada}", 
                        filtros.FechaDesde.Value, fechaDesdeInicio);
                    query = query.Where(t => t.FechaInicio >= fechaDesdeInicio);
                }
                if (filtros.FechaHasta.HasValue)
                {
                    var fechaHastaFin = filtros.FechaHasta.Value.Date.AddDays(1).AddTicks(-1); // 23:59:59.9999999
                    _logger.LogInformation("Filtro FechaHasta: {FechaOriginal} -> {FechaAplicada} (hasta las 23:59:59.9999999)", 
                        filtros.FechaHasta.Value, fechaHastaFin);
                    query = query.Where(t => t.FechaInicio <= fechaHastaFin);
                }
                if (filtros.CodigoEmpresa.HasValue)
                    query = query.Where(t => t.CodigoEmpresa == filtros.CodigoEmpresa.Value);

                // Contar total de traspasos después de aplicar filtros (para debugging)
                var totalTraspasosFiltrados = await query.CountAsync();
                _logger.LogInformation("Total de traspasos después de aplicar filtros: {Total}", totalTraspasosFiltrados);

                // Obtener traspasos completados con información de palet para agrupar
                var traspasosCompletadosRaw = await query
                    .Where(t => t.FechaFinalizacion.HasValue && t.UsuarioInicioId > 0)
                    .Select(t => new
                    {
                        OperarioId = t.UsuarioInicioId,
                        FechaInicio = t.FechaInicio,
                        FechaFinalizacion = t.FechaFinalizacion!.Value,
                        TipoTraspaso = t.TipoTraspaso ?? "ARTICULO",
                        PaletId = t.PaletId,
                        CodigoPalet = t.CodigoPalet,
                        AlmacenOrigen = t.AlmacenOrigen,
                        AlmacenDestino = t.AlmacenDestino,
                        CodigoArticulo = t.CodigoArticulo
                    })
                    .ToListAsync();
                
                // Obtener también traspasos iniciados (para calcular tasa de finalización)
                var traspasosIniciadosRaw = await query
                    .Where(t => t.UsuarioInicioId > 0)
                    .Select(t => new
                    {
                        OperarioId = t.UsuarioInicioId,
                        FechaInicio = t.FechaInicio,
                        FechaFinalizacion = t.FechaFinalizacion,
                        CodigoEstado = t.CodigoEstado
                    })
                    .ToListAsync();

                // Separar traspasos de palet y de artículo individual
                var traspasosPalet = traspasosCompletadosRaw
                    .Where(t => (t.TipoTraspaso?.ToUpper() ?? "ARTICULO") == "PALET" && t.PaletId != Guid.Empty)
                    .OrderBy(t => t.FechaInicio)
                    .ToList();

                var traspasosArticulo = traspasosCompletadosRaw
                    .Where(t => (t.TipoTraspaso?.ToUpper() ?? "ARTICULO") != "PALET" || t.PaletId == Guid.Empty)
                    .ToList();

                // Agrupar traspasos de palet que ocurren casi simultáneamente
                // Un lote es un grupo de traspasos del mismo PaletId, mismo operario, 
                // donde todos ocurren dentro de un rango de tiempo corto (2 minutos)
                var lotesPalet = new List<(int OperarioId, string TipoTraspaso, double TiempoMinutos, bool EsLote, int CantidadEnLote)>();
                var traspasosPaletProcesados = new HashSet<int>();

                for (int i = 0; i < traspasosPalet.Count; i++)
                {
                    if (traspasosPaletProcesados.Contains(i))
                        continue;

                    var traspasoInicial = traspasosPalet[i];
                    var lote = new List<(int Index, DateTime FechaInicio, DateTime FechaFinalizacion)>
                    {
                        (i, traspasoInicial.FechaInicio, traspasoInicial.FechaFinalizacion)
                    };
                    traspasosPaletProcesados.Add(i);

                    // Buscar traspasos del mismo palet y operario que ocurren casi simultáneamente
                    for (int j = i + 1; j < traspasosPalet.Count; j++)
                    {
                        if (traspasosPaletProcesados.Contains(j))
                            continue;

                        var traspasoActual = traspasosPalet[j];
                        
                        // Mismo palet y mismo operario
                        if (traspasoActual.PaletId == traspasoInicial.PaletId && 
                            traspasoActual.OperarioId == traspasoInicial.OperarioId)
                        {
                            // Verificar si ocurre dentro de 2 minutos del inicio del lote
                            // O si tienen exactamente la misma fecha de inicio (mismo segundo)
                            var tiempoDesdeInicioLote = (traspasoActual.FechaInicio - traspasoInicial.FechaInicio).TotalMinutes;
                            var mismoSegundo = traspasoActual.FechaInicio.Date == traspasoInicial.FechaInicio.Date &&
                                             traspasoActual.FechaInicio.Hour == traspasoInicial.FechaInicio.Hour &&
                                             traspasoActual.FechaInicio.Minute == traspasoInicial.FechaInicio.Minute &&
                                             traspasoActual.FechaInicio.Second == traspasoInicial.FechaInicio.Second;
                            
                            if (tiempoDesdeInicioLote <= 2.0 || mismoSegundo)
                            {
                                lote.Add((j, traspasoActual.FechaInicio, traspasoActual.FechaFinalizacion));
                                traspasosPaletProcesados.Add(j);
                            }
                        }
                    }

                    // Si el lote tiene más de 1 traspaso, crear un lote agrupado
                    if (lote.Count > 1)
                    {
                        // Calcular el tiempo del lote como el tiempo total desde el inicio del primer traspaso
                        // hasta el final del último traspaso
                        var fechaInicioLote = lote.Min(l => l.FechaInicio);
                        var fechaFinalizacionLote = lote.Max(l => l.FechaFinalizacion);
                        var tiempoLote = (fechaFinalizacionLote - fechaInicioLote).TotalMinutes;

                        // Si el tiempo del lote es muy pequeño (todos tienen casi la misma fecha),
                        // significa que es un traspaso de palet completo que se hace muy rápido
                        // Aplicar un tiempo mínimo razonable: 5 segundos (0.083 minutos) por lote de palet
                        if (tiempoLote < 0.083) // Menos de 5 segundos
                        {
                            tiempoLote = 0.083; // 5 segundos mínimo para un traspaso de palet completo
                        }

                        // El lote siempre se agrega (ya tiene tiempo mínimo garantizado)
                        lotesPalet.Add((traspasoInicial.OperarioId, "PALET", tiempoLote, true, lote.Count));
                    }
                    else
                    {
                        // Si solo hay 1 traspaso, tratarlo como individual
                        var tiempoIndividual = (traspasoInicial.FechaFinalizacion - traspasoInicial.FechaInicio).TotalMinutes;
                        if (tiempoIndividual >= 0.01)
                        {
                            lotesPalet.Add((traspasoInicial.OperarioId, "PALET", tiempoIndividual, false, 1));
                        }
                    }
                }

                // Convertir lotesPalet a tipo anónimo compatible con traspasosArticuloIndividual
                var lotesPaletTipados = lotesPalet
                    .Select(l => new
                    {
                        OperarioId = l.OperarioId,
                        TipoTraspaso = l.TipoTraspaso,
                        TiempoMinutos = l.TiempoMinutos,
                        EsLote = l.EsLote,
                        CantidadEnLote = l.CantidadEnLote
                    })
                    .ToList();

                // Procesar traspasos de artículo individual
                var traspasosArticuloIndividual = traspasosArticulo
                    .Select(t => new
                    {
                        t.OperarioId,
                        TipoTraspaso = t.TipoTraspaso?.ToUpper() ?? "ARTICULO",
                        TiempoMinutos = (t.FechaFinalizacion - t.FechaInicio).TotalMinutes,
                        EsLote = false,
                        CantidadEnLote = 1
                    })
                    .Where(t => t.TiempoMinutos >= 0.01)
                    .ToList();

                // Combinar lotes de palet y traspasos individuales
                var todosLosTraspasos = lotesPaletTipados.Concat(traspasosArticuloIndividual).ToList();

                var traspasosPorOperario = todosLosTraspasos
                    .GroupBy(t => t.OperarioId)
                    .Select(g => new
                    {
                        OperarioId = g.Key,
                        TotalTraspasos = g.Sum(t => t.CantidadEnLote), // Suma de todos los traspasos (incluyendo los del lote)
                        TraspasosPalet = g.Where(t => t.TipoTraspaso == "PALET").Sum(t => t.CantidadEnLote),
                        TraspasosArticulo = g.Where(t => t.TipoTraspaso == "ARTICULO" || t.TipoTraspaso == "ARTÍCULO").Sum(t => t.CantidadEnLote),
                        // Tiempo promedio: promedio ponderado por operación (cada lote cuenta como 1 operación, no por cantidad de traspasos)
                        // Esto significa que un lote de 8 traspasos cuenta igual que un traspaso individual
                        TiempoPromedioMinutos = g.Any() ? (double?)g.Average(t => t.TiempoMinutos) : null,
                        // También calcular el tiempo promedio ponderado por cantidad de traspasos (para referencia)
                        TiempoTotalMinutos = g.Sum(t => t.TiempoMinutos * t.CantidadEnLote),
                        TotalOperaciones = g.Count() // Número de operaciones (lotes + traspasos individuales)
                    })
                    .ToList();

                // Obtener líneas de inventario por operario
                var inventarioQuery = _context.InventarioLineasTemp.AsQueryable();
                if (filtros.FechaDesde.HasValue)
                {
                    var fechaDesdeInicio = filtros.FechaDesde.Value.Date; // 00:00:00
                    inventarioQuery = inventarioQuery.Where(i => i.FechaConteo >= fechaDesdeInicio);
                }
                if (filtros.FechaHasta.HasValue)
                {
                    var fechaHastaFin = filtros.FechaHasta.Value.Date.AddDays(1).AddTicks(-1); // 23:59:59.9999999
                    inventarioQuery = inventarioQuery.Where(i => i.FechaConteo <= fechaHastaFin);
                }

                var inventarioDataRaw = await inventarioQuery
                    .Where(i => i.UsuarioConteoId > 0)
                    .Select(i => new
                    {
                        OperarioId = i.UsuarioConteoId,
                        Consolidado = i.Consolidado,
                        FechaConteo = i.FechaConteo,
                        CantidadContada = i.CantidadContada,
                        StockActual = i.StockActual
                    })
                    .ToListAsync();

                // Agrupar por operario y calcular tiempo total de trabajo y precisión
                // Tiempo total = diferencia entre la última y primera línea contada
                // Precisión = porcentaje de líneas donde CantidadContada coincide con StockActual (diferencia < 0.01)
                var inventarioPorOperario = inventarioDataRaw
                    .GroupBy(i => i.OperarioId)
                    .Select(g => new
                    {
                        OperarioId = g.Key,
                        TotalLineas = g.Count(),
                        LineasConsolidadas = g.Count(i => i.Consolidado),
                        PrimeraLinea = g.Min(i => i.FechaConteo),
                        UltimaLinea = g.Max(i => i.FechaConteo),
                        // Calcular líneas con conteo (CantidadContada tiene valor)
                        LineasConConteo = g.Where(i => i.CantidadContada.HasValue).ToList(),
                        TodasLasLineas = g.ToList()
                    })
                    .ToList()
                    .Select(g => new
                    {
                        g.OperarioId,
                        g.TotalLineas,
                        g.LineasConsolidadas,
                        // Calcular tiempo total en minutos: desde la primera línea hasta la última
                        TiempoTotalMinutos = (g.UltimaLinea - g.PrimeraLinea).TotalMinutes >= 0.01
                            ? (double?)(g.UltimaLinea - g.PrimeraLinea).TotalMinutes
                            : (double?)null // Si todas las líneas fueron contadas en el mismo momento, tiempo mínimo
                    })
                    .ToList();

                // Obtener información de conteos por operario
                var conteoQuery = _context.OrdenesConteo.AsQueryable();
                if (filtros.FechaDesde.HasValue)
                {
                    var fechaDesdeInicio = filtros.FechaDesde.Value.Date; // 00:00:00
                    conteoQuery = conteoQuery.Where(c => c.FechaInicio >= fechaDesdeInicio);
                }
                if (filtros.FechaHasta.HasValue)
                {
                    var fechaHastaFin = filtros.FechaHasta.Value.Date.AddDays(1).AddTicks(-1); // 23:59:59.9999999
                    conteoQuery = conteoQuery.Where(c => c.FechaInicio <= fechaHastaFin);
                }

                // Obtener órdenes de conteo completadas por operario
                var ordenesCompletadasRaw = await conteoQuery
                    .Where(c => c.FechaCierre.HasValue && c.FechaInicio.HasValue && !string.IsNullOrEmpty(c.CodigoOperario))
                    .Select(c => new
                    {
                        CodigoOperario = c.CodigoOperario!,
                        FechaInicio = c.FechaInicio!.Value,
                        FechaCierre = c.FechaCierre!.Value,
                        Estado = c.Estado
                    })
                    .ToListAsync();

                var ordenesCompletadas = ordenesCompletadasRaw
                    .Select(c => new
                    {
                        OperarioId = int.TryParse(c.CodigoOperario, out var id) ? id : 0,
                        TiempoMinutos = (c.FechaCierre - c.FechaInicio).TotalMinutes,
                        Estado = c.Estado
                    })
                    .Where(c => c.OperarioId > 0 && c.TiempoMinutos >= 0.01)
                    .ToList();

                var conteosPorOperario = ordenesCompletadas
                    .GroupBy(c => c.OperarioId)
                    .Select(g => new
                    {
                        OperarioId = g.Key,
                        ConteosCompletados = g.Count(c => c.Estado == "CERRADO"),
                        TiempoPromedioMinutos = g.Any() ? (double?)g.Average(c => c.TiempoMinutos) : null
                    })
                    .ToList();

                // Obtener lecturas de conteo con información de precisión
                var lecturasDataRaw = await _context.LecturasConteo
                    .Where(l => conteoQuery.Any(c => c.GuidID == l.OrdenGuid) && !string.IsNullOrEmpty(l.UsuarioCodigo))
                    .Select(l => new
                    {
                        CodigoOperario = l.UsuarioCodigo,
                        CantidadContada = l.CantidadContada,
                        CantidadStock = l.CantidadStock,
                        Fecha = l.Fecha
                    })
                    .ToListAsync();

                var lecturasData = lecturasDataRaw
                    .Select(l => new
                    {
                        OperarioId = int.TryParse(l.CodigoOperario, out var id) ? id : 0,
                        CantidadContada = l.CantidadContada,
                        CantidadStock = l.CantidadStock,
                        Fecha = l.Fecha
                    })
                    .Where(l => l.OperarioId > 0)
                    .ToList();

                var lecturasPorOperario = lecturasData
                    .GroupBy(l => l.OperarioId)
                    .Select(g => new
                    {
                        OperarioId = g.Key,
                        TotalLecturas = g.Count(),
                        LecturasConConteo = g.Where(l => l.CantidadContada.HasValue).ToList(),
                        PrimeraLectura = g.Min(l => l.Fecha),
                        UltimaLectura = g.Max(l => l.Fecha)
                    })
                    .ToList()
                    .Select(g => new
                    {
                        g.OperarioId,
                        g.TotalLecturas,
                        // Calcular tiempo total de trabajo: desde la primera lectura hasta la última
                        TiempoTotalMinutos = (g.UltimaLectura - g.PrimeraLectura).TotalMinutes >= 0.01
                            ? (double?)(g.UltimaLectura - g.PrimeraLectura).TotalMinutes
                            : (double?)null
                    })
                    .ToList();

                // Calcular estadísticas adicionales de traspasos (tiempos min/max/mediano)
                var tiemposTraspasosPorOperario = traspasosCompletadosRaw
                    .Select(t => new
                    {
                        t.OperarioId,
                        TiempoMinutos = (t.FechaFinalizacion - t.FechaInicio).TotalMinutes
                    })
                    .Where(t => t.TiempoMinutos >= 0.01)
                    .GroupBy(t => t.OperarioId)
                    .Select(g => new
                    {
                        OperarioId = g.Key,
                        Tiempos = g.Select(t => t.TiempoMinutos).OrderBy(t => t).ToList(),
                        TiempoMinimo = g.Min(t => t.TiempoMinutos),
                        TiempoMaximo = g.Max(t => t.TiempoMinutos)
                    })
                    .ToList()
                    .Select(g => new
                    {
                        g.OperarioId,
                        g.Tiempos,
                        g.TiempoMinimo,
                        g.TiempoMaximo,
                        TiempoMediano = g.Tiempos.Any() 
                            ? (g.Tiempos.Count % 2 == 0 
                                ? (g.Tiempos[g.Tiempos.Count / 2 - 1] + g.Tiempos[g.Tiempos.Count / 2]) / 2.0
                                : g.Tiempos[g.Tiempos.Count / 2])
                            : (double?)null
                    })
                    .ToList();

                // Calcular tasa de finalización por operario
                var tasaFinalizacionPorOperario = traspasosIniciadosRaw
                    .GroupBy(t => t.OperarioId)
                    .Select(g => new
                    {
                        OperarioId = g.Key,
                        Iniciados = g.Count(),
                        Completados = g.Count(t => t.FechaFinalizacion.HasValue),
                        TasaFinalizacion = g.Count() > 0 ? (double)g.Count(t => t.FechaFinalizacion.HasValue) / g.Count() * 100 : 0.0
                    })
                    .ToList();

                // Calcular días activos y última actividad
                var actividadPorOperario = traspasosCompletadosRaw
                    .Concat(traspasosIniciadosRaw.Where(t => t.FechaFinalizacion.HasValue).Select(t => new
                    {
                        t.OperarioId,
                        FechaInicio = t.FechaInicio,
                        FechaFinalizacion = t.FechaFinalizacion!.Value,
                        TipoTraspaso = (string?)null,
                        PaletId = Guid.Empty,
                        CodigoPalet = (string?)null,
                        AlmacenOrigen = (string?)null,
                        AlmacenDestino = (string?)null,
                        CodigoArticulo = (string?)null
                    }))
                    .GroupBy(t => t.OperarioId)
                    .Select(g => new
                    {
                        OperarioId = g.Key,
                        DiasActivos = g.Select(t => t.FechaInicio.Date).Distinct().Count(),
                        UltimaActividad = g.Max(t => t.FechaFinalizacion)
                    })
                    .ToList();

                // Calcular almacenes y artículos diferentes
                var distribucionPorOperario = traspasosCompletadosRaw
                    .GroupBy(t => t.OperarioId)
                    .Select(g => new
                    {
                        OperarioId = g.Key,
                        AlmacenesDiferentes = g.SelectMany(t => new[] { t.AlmacenOrigen, t.AlmacenDestino })
                            .Where(a => !string.IsNullOrEmpty(a))
                            .Distinct()
                            .Count(),
                        ArticulosDiferentes = g.Where(t => !string.IsNullOrEmpty(t.CodigoArticulo))
                            .Select(t => t.CodigoArticulo!)
                            .Distinct()
                            .Count()
                    })
                    .ToList();

                // Combinar resultados
                var operariosIds = traspasosPorOperario.Select(t => t.OperarioId)
                    .Union(inventarioPorOperario.Select(i => i.OperarioId))
                    .Union(lecturasPorOperario.Select(l => l.OperarioId))
                    .Union(conteosPorOperario.Select(c => c.OperarioId))
                    .Distinct()
                    .ToList();

                var resultados = new List<RendimientoOperarioDto>();

                foreach (var operarioId in operariosIds)
                {
                    var traspaso = traspasosPorOperario.FirstOrDefault(t => t.OperarioId == operarioId);
                    var inventario = inventarioPorOperario.FirstOrDefault(i => i.OperarioId == operarioId);
                    var lecturas = lecturasPorOperario.FirstOrDefault(l => l.OperarioId == operarioId);
                    var conteos = conteosPorOperario.FirstOrDefault(c => c.OperarioId == operarioId);

                    // Obtener nombre del operario
                    var operario = await _sageContext.Operarios
                        .FirstOrDefaultAsync(o => o.Id == operarioId);

                    var rendimiento = new RendimientoOperarioDto
                    {
                        OperarioId = operarioId,
                        NombreOperario = operario?.Nombre ?? $"Operario {operarioId}",
                        TraspasosCompletados = traspaso?.TotalTraspasos ?? 0,
                        TraspasosPalet = traspaso?.TraspasosPalet ?? 0,
                        TraspasosArticulo = traspaso?.TraspasosArticulo ?? 0,
                        TiempoPromedioTraspasosMinutos = traspaso?.TiempoPromedioMinutos,
                        LineasInventarioContadas = inventario?.TotalLineas ?? 0,
                        TiempoPromedioInventarioMinutos = inventario?.TiempoTotalMinutos, // Tiempo total de trabajo (primera línea a última línea)
                        LecturasConteo = lecturas?.TotalLecturas ?? 0,
                        ConteosCompletados = conteos?.ConteosCompletados ?? 0,
                        TiempoPromedioConteoMinutos = conteos?.TiempoPromedioMinutos, // Tiempo promedio por orden de conteo
                        TotalOperaciones = (traspaso?.TotalTraspasos ?? 0) + 
                                         (inventario?.TotalLineas ?? 0) + 
                                         (lecturas?.TotalLecturas ?? 0)
                    };

                    // Calcular líneas por hora basado en tiempo total de trabajo (inventarios)
                    // TiempoTotalMinutos = tiempo desde la primera línea contada hasta la última
                    if (inventario?.TotalLineas > 0 && inventario.TiempoTotalMinutos.HasValue && inventario.TiempoTotalMinutos.Value > 0)
                    {
                        // Convertir minutos a horas y calcular líneas por hora
                        var horasTrabajadas = inventario.TiempoTotalMinutos.Value / 60.0;
                        rendimiento.LineasPorHora = inventario.TotalLineas / horasTrabajadas;
                    }
                    else if (inventario?.TotalLineas > 0)
                    {
                        // Si todas las líneas fueron contadas en el mismo momento (tiempo = 0),
                        // asumir un tiempo mínimo de 1 minuto para evitar división por cero
                        rendimiento.LineasPorHora = inventario.TotalLineas * 60; // 60 líneas por minuto = todas en el mismo momento
                    }

                    // Calcular lecturas por hora basado en tiempo total de trabajo (conteos)
                    // TiempoTotalMinutos = tiempo desde la primera lectura hasta la última
                    if (lecturas?.TotalLecturas > 0 && lecturas.TiempoTotalMinutos.HasValue && lecturas.TiempoTotalMinutos.Value > 0)
                    {
                        // Convertir minutos a horas y calcular lecturas por hora
                        var horasTrabajadas = lecturas.TiempoTotalMinutos.Value / 60.0;
                        rendimiento.LecturasPorHora = lecturas.TotalLecturas / horasTrabajadas;
                    }
                    else if (lecturas?.TotalLecturas > 0)
                    {
                        // Si todas las lecturas fueron en el mismo momento (tiempo = 0),
                        // asumir un tiempo mínimo de 1 minuto para evitar división por cero
                        rendimiento.LecturasPorHora = lecturas.TotalLecturas * 60; // 60 lecturas por minuto = todas en el mismo momento
                    }

                    // Calcular traspasos por día
                    if (filtros.FechaDesde.HasValue && filtros.FechaHasta.HasValue)
                    {
                        var dias = (filtros.FechaHasta.Value - filtros.FechaDesde.Value).TotalDays + 1;
                        if (dias > 0)
                        {
                            rendimiento.TraspasosPorDia = rendimiento.TraspasosCompletados / dias;
                        }
                    }

                    // Obtener estadísticas de tiempos
                    var tiemposStats = tiemposTraspasosPorOperario.FirstOrDefault(t => t.OperarioId == operarioId);
                    if (tiemposStats != null)
                    {
                        rendimiento.TiempoMinimoTraspasosMinutos = tiemposStats.TiempoMinimo;
                        rendimiento.TiempoMaximoTraspasosMinutos = tiemposStats.TiempoMaximo;
                        if (tiemposStats.Tiempos.Any())
                        {
                            var tiemposOrdenados = tiemposStats.Tiempos;
                            var indiceMediano = tiemposOrdenados.Count / 2;
                            rendimiento.TiempoMedianoTraspasosMinutos = tiemposOrdenados[indiceMediano];
                        }
                    }

                    // Obtener tasa de finalización
                    var tasaFinalizacion = tasaFinalizacionPorOperario.FirstOrDefault(t => t.OperarioId == operarioId);
                    if (tasaFinalizacion != null)
                    {
                        rendimiento.TasaFinalizacion = tasaFinalizacion.TasaFinalizacion;
                    }

                    // Obtener actividad
                    var actividad = actividadPorOperario.FirstOrDefault(a => a.OperarioId == operarioId);
                    if (actividad != null)
                    {
                        rendimiento.DiasActivos = actividad.DiasActivos;
                        rendimiento.UltimaActividad = actividad.UltimaActividad;
                    }

                    // Obtener distribución
                    var distribucion = distribucionPorOperario.FirstOrDefault(d => d.OperarioId == operarioId);
                    if (distribucion != null)
                    {
                        rendimiento.AlmacenesDiferentes = distribucion.AlmacenesDiferentes;
                        rendimiento.ArticulosDiferentes = distribucion.ArticulosDiferentes;
                    }

                    // Calcular tiempo total trabajado (suma de todos los tiempos de traspasos)
                    if (traspaso != null && traspaso.TiempoTotalMinutos > 0)
                    {
                        rendimiento.TiempoTotalTrabajadoMinutos = traspaso.TiempoTotalMinutos;
                    }

                    resultados.Add(rendimiento);
                }

                // Calcular totales y porcentajes después de tener todos los resultados
                var totalTraspasosGlobal = resultados.Sum(r => r.TraspasosCompletados);
                var promedioTiempoGlobal = resultados.Where(r => r.TiempoPromedioTraspasosMinutos.HasValue).Any()
                    ? resultados.Where(r => r.TiempoPromedioTraspasosMinutos.HasValue).Average(r => r.TiempoPromedioTraspasosMinutos!.Value)
                    : 0.0;

                // Asignar porcentajes y rankings
                foreach (var rendimiento in resultados)
                {
                    // Porcentaje del total
                    if (totalTraspasosGlobal > 0)
                    {
                        rendimiento.PorcentajeDelTotal = (rendimiento.TraspasosCompletados / (double)totalTraspasosGlobal) * 100;
                    }

                    // Variación porcentual vs promedio
                    if (rendimiento.TiempoPromedioTraspasosMinutos.HasValue && promedioTiempoGlobal > 0)
                    {
                        rendimiento.VariacionPorcentual = ((rendimiento.TiempoPromedioTraspasosMinutos.Value - promedioTiempoGlobal) / promedioTiempoGlobal) * 100;
                    }
                }

                // Asignar rankings
                var resultadosOrdenados = resultados.OrderByDescending(r => r.TotalOperaciones).ToList();
                for (int i = 0; i < resultadosOrdenados.Count; i++)
                {
                    resultadosOrdenados[i].Ranking = i + 1;
                }

                return resultadosOrdenados;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculando rendimiento de operarios");
                throw;
            }
        }

        public async Task<List<RendimientoProcesoDto>> ObtenerRendimientoProcesosAsync(FiltroRendimientosDto filtros)
        {
            try
            {
                _logger.LogInformation("Calculando rendimiento de procesos con filtros: {Filtros}", 
                    System.Text.Json.JsonSerializer.Serialize(filtros));

                var resultados = new List<RendimientoProcesoDto>();

                // Proceso: TRASPASOS
                var traspasosQuery = _context.Traspasos.AsQueryable();
                if (filtros.FechaDesde.HasValue)
                {
                    var fechaDesdeInicio = filtros.FechaDesde.Value.Date; // 00:00:00
                    traspasosQuery = traspasosQuery.Where(t => t.FechaInicio >= fechaDesdeInicio);
                }
                if (filtros.FechaHasta.HasValue)
                {
                    var fechaHastaFin = filtros.FechaHasta.Value.Date.AddDays(1).AddTicks(-1); // 23:59:59.9999999
                    traspasosQuery = traspasosQuery.Where(t => t.FechaInicio <= fechaHastaFin);
                }
                if (filtros.CodigoEmpresa.HasValue)
                    traspasosQuery = traspasosQuery.Where(t => t.CodigoEmpresa == filtros.CodigoEmpresa.Value);

                var traspasosDataRaw = await traspasosQuery
                    .Where(t => t.FechaFinalizacion.HasValue)
                    .Select(t => new
                    {
                        FechaInicio = t.FechaInicio,
                        FechaFinalizacion = t.FechaFinalizacion!.Value
                    })
                    .ToListAsync();

                var traspasosData = traspasosDataRaw
                    .Select(t => new
                    {
                        TiempoMinutos = (t.FechaFinalizacion - t.FechaInicio).TotalMinutes
                    })
                    .Where(t => t.TiempoMinutos >= 0.01) // Filtrar tiempos menores a 0.6 segundos (0.01 minutos)
                    .ToList();

                var traspasosStats = new
                {
                    Total = await traspasosQuery.CountAsync(),
                    Completados = traspasosData.Count,
                    TiempoPromedio = traspasosData.Any() ? traspasosData.Average(t => t.TiempoMinutos) : (double?)null,
                    TiempoMinimo = traspasosData.Any() ? traspasosData.Min(t => t.TiempoMinutos) : (double?)null,
                    TiempoMaximo = traspasosData.Any() ? traspasosData.Max(t => t.TiempoMinutos) : (double?)null
                };

                if (traspasosStats != null)
                {
                    resultados.Add(new RendimientoProcesoDto
                    {
                        TipoProceso = "TRASPASOS",
                        TotalProcesos = traspasosStats.Total,
                        ProcesosCompletados = traspasosStats.Completados,
                        ProcesosPendientes = traspasosStats.Total - traspasosStats.Completados,
                        TasaFinalizacion = traspasosStats.Total > 0 
                            ? (double)traspasosStats.Completados / traspasosStats.Total * 100 
                            : 0,
                        TiempoPromedioMinutos = traspasosStats.TiempoPromedio,
                        TiempoMinimoMinutos = traspasosStats.TiempoMinimo,
                        TiempoMaximoMinutos = traspasosStats.TiempoMaximo
                    });
                }

                // Proceso: INVENTARIOS
                var inventarioQuery = _context.InventarioLineasTemp.AsQueryable();
                if (filtros.FechaDesde.HasValue)
                {
                    var fechaDesdeInicio = filtros.FechaDesde.Value.Date; // 00:00:00
                    inventarioQuery = inventarioQuery.Where(i => i.FechaConteo >= fechaDesdeInicio);
                }
                if (filtros.FechaHasta.HasValue)
                {
                    var fechaHastaFin = filtros.FechaHasta.Value.Date.AddDays(1).AddTicks(-1); // 23:59:59.9999999
                    inventarioQuery = inventarioQuery.Where(i => i.FechaConteo <= fechaHastaFin);
                }

                var inventarioDataStatsRaw = await inventarioQuery
                    .Select(i => new
                    {
                        Consolidado = i.Consolidado,
                        FechaConteo = i.FechaConteo
                    })
                    .ToListAsync();

                // Calcular tiempo total de trabajo: desde la primera línea hasta la última
                var primeraLinea = inventarioDataStatsRaw.Any() 
                    ? inventarioDataStatsRaw.Min(i => i.FechaConteo) 
                    : (DateTime?)null;
                var ultimaLinea = inventarioDataStatsRaw.Any() 
                    ? inventarioDataStatsRaw.Max(i => i.FechaConteo) 
                    : (DateTime?)null;

                var tiempoTotalMinutos = primeraLinea.HasValue && ultimaLinea.HasValue
                    ? (ultimaLinea.Value - primeraLinea.Value).TotalMinutes
                    : (double?)null;

                var totalLineas = inventarioDataStatsRaw.Count;
                var consolidados = inventarioDataStatsRaw.Count(i => i.Consolidado);
                
                var inventarioStats = new
                {
                    Total = totalLineas,
                    Consolidados = consolidados,
                    TiempoTotalMinutos = tiempoTotalMinutos
                };

                if (inventarioStats != null)
                {
                    var tiempoTotalHoras = inventarioStats.TiempoTotalMinutos.HasValue && inventarioStats.TiempoTotalMinutos.Value > 0
                        ? inventarioStats.TiempoTotalMinutos.Value / 60.0 
                        : 0;
                    var lineasPorHora = tiempoTotalHoras > 0 
                        ? inventarioStats.Total / tiempoTotalHoras 
                        : 0;

                    resultados.Add(new RendimientoProcesoDto
                    {
                        TipoProceso = "INVENTARIOS",
                        TotalProcesos = inventarioStats.Total,
                        ProcesosCompletados = inventarioStats.Consolidados,
                        ProcesosPendientes = inventarioStats.Total - inventarioStats.Consolidados,
                        TasaFinalizacion = inventarioStats.Total > 0 
                            ? (double)inventarioStats.Consolidados / inventarioStats.Total * 100 
                            : 0,
                        TiempoPromedioMinutos = inventarioStats.TiempoTotalMinutos, // Tiempo total de trabajo (primera línea a última línea)
                        LineasPorHora = lineasPorHora
                    });
                }

                // Proceso: CONTEO
                var conteoQuery = _context.OrdenesConteo.AsQueryable();
                if (filtros.FechaDesde.HasValue)
                {
                    var fechaDesdeInicio = filtros.FechaDesde.Value.Date; // 00:00:00
                    conteoQuery = conteoQuery.Where(c => c.FechaInicio >= fechaDesdeInicio);
                }
                if (filtros.FechaHasta.HasValue)
                {
                    var fechaHastaFin = filtros.FechaHasta.Value.Date.AddDays(1).AddTicks(-1); // 23:59:59.9999999
                    conteoQuery = conteoQuery.Where(c => c.FechaInicio <= fechaHastaFin);
                }

                var conteoDataRaw = await conteoQuery
                    .Where(c => c.FechaCierre.HasValue && c.FechaInicio.HasValue)
                    .Select(c => new
                    {
                        Estado = c.Estado,
                        FechaInicio = c.FechaInicio!.Value,
                        FechaCierre = c.FechaCierre!.Value
                    })
                    .ToListAsync();

                var conteoData = conteoDataRaw
                    .Select(c => new
                    {
                        c.Estado,
                        TiempoMinutos = (c.FechaCierre - c.FechaInicio).TotalMinutes
                    })
                    .Where(c => c.TiempoMinutos >= 0.01) // Filtrar tiempos menores a 0.6 segundos
                    .ToList();

                var conteoStats = new
                {
                    Total = await conteoQuery.CountAsync(),
                    Cerrados = conteoData.Count(c => c.Estado == "CERRADO"),
                    TiempoPromedio = conteoData.Any() ? conteoData.Average(c => c.TiempoMinutos) : (double?)null
                };

                if (conteoStats != null)
                {
                    var totalLecturas = await _context.LecturasConteo
                        .Where(l => conteoQuery.Any(c => c.GuidID == l.OrdenGuid))
                        .CountAsync();

                    var tiempoTotalHoras = conteoStats.TiempoPromedio.HasValue 
                        ? conteoStats.TiempoPromedio.Value / 60.0 
                        : 0;
                    var lecturasPorHora = tiempoTotalHoras > 0 
                        ? totalLecturas / tiempoTotalHoras 
                        : 0;

                    resultados.Add(new RendimientoProcesoDto
                    {
                        TipoProceso = "CONTEO",
                        TotalProcesos = conteoStats.Total,
                        ProcesosCompletados = conteoStats.Cerrados,
                        ProcesosPendientes = conteoStats.Total - conteoStats.Cerrados,
                        TasaFinalizacion = conteoStats.Total > 0 
                            ? (double)conteoStats.Cerrados / conteoStats.Total * 100 
                            : 0,
                        TiempoPromedioMinutos = conteoStats.TiempoPromedio,
                        LineasPorHora = lecturasPorHora
                    });
                }

                return resultados;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculando rendimiento de procesos");
                throw;
            }
        }

        public async Task<ComparativaRendimientoDto> ObtenerComparativaAsync(FiltroRendimientosDto filtros, string tipoComparativa)
        {
            try
            {
                _logger.LogInformation("Calculando comparativa de tipo: {Tipo}", tipoComparativa);

                var comparativa = new ComparativaRendimientoDto
                {
                    TipoComparativa = tipoComparativa
                };

                if (tipoComparativa == "OPERARIOS")
                {
                    var operarios = await ObtenerRendimientoOperariosAsync(filtros);
                    var promedio = operarios.Any() ? operarios.Average(o => o.TotalOperaciones) : 0;

                    comparativa.Items = operarios.Select(o => new ItemComparativaDto
                    {
                        Etiqueta = o.NombreOperario ?? $"Operario {o.OperarioId}",
                        Valor = o.TotalOperaciones,
                        Unidad = "operaciones",
                        Variacion = promedio > 0 ? ((o.TotalOperaciones - promedio) / promedio) * 100 : null
                    }).ToList();
                }
                else if (tipoComparativa == "PERIODOS")
                {
                    // Comparar período actual vs período anterior
                    if (filtros.FechaDesde.HasValue && filtros.FechaHasta.HasValue)
                    {
                        var periodoActual = await ObtenerRendimientoProcesosAsync(filtros);
                        var totalActual = periodoActual.Sum(p => p.TotalProcesos);

                        var diasPeriodo = (filtros.FechaHasta.Value - filtros.FechaDesde.Value).TotalDays + 1;
                        var filtrosAnterior = new FiltroRendimientosDto
                        {
                            FechaDesde = filtros.FechaDesde.Value.AddDays(-diasPeriodo),
                            FechaHasta = filtros.FechaDesde.Value.AddDays(-1),
                            CodigoEmpresa = filtros.CodigoEmpresa,
                            CodigoAlmacen = filtros.CodigoAlmacen
                        };
                        var periodoAnterior = await ObtenerRendimientoProcesosAsync(filtrosAnterior);
                        var totalAnterior = periodoAnterior.Sum(p => p.TotalProcesos);

                        comparativa.Items = new List<ItemComparativaDto>
                        {
                            new ItemComparativaDto
                            {
                                Etiqueta = "Período Actual",
                                Valor = totalActual,
                                Unidad = "procesos"
                            },
                            new ItemComparativaDto
                            {
                                Etiqueta = "Período Anterior",
                                Valor = totalAnterior,
                                Unidad = "procesos",
                                Variacion = totalAnterior > 0 ? ((totalActual - totalAnterior) / totalAnterior) * 100 : null
                            }
                        };
                    }
                }

                return comparativa;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculando comparativa");
                throw;
            }
        }

        public async Task<List<TendenciaRendimientoDto>> ObtenerTendenciasAsync(FiltroRendimientosDto filtros, string tipoMetrica)
        {
            try
            {
                _logger.LogInformation("Calculando tendencias de tipo: {Tipo}", tipoMetrica);

                var tendencias = new List<TendenciaRendimientoDto>();

                if (tipoMetrica == "PRODUCTIVIDAD")
                {
                    if (filtros.FechaDesde.HasValue && filtros.FechaHasta.HasValue)
                    {
                        var puntos = new List<PuntoTendenciaDto>();
                        var fechaActual = filtros.FechaDesde.Value.Date;
                        var fechaFin = filtros.FechaHasta.Value.Date;

                        while (fechaActual <= fechaFin)
                        {
                            var filtroDia = new FiltroRendimientosDto
                            {
                                FechaDesde = fechaActual.Date, // 00:00:00
                                FechaHasta = fechaActual.Date, // Incluir todo el día
                                CodigoEmpresa = filtros.CodigoEmpresa,
                                CodigoAlmacen = filtros.CodigoAlmacen
                            };

                            var procesos = await ObtenerRendimientoProcesosAsync(filtroDia);
                            var totalProcesos = procesos.Sum(p => p.TotalProcesos);

                            puntos.Add(new PuntoTendenciaDto
                            {
                                Fecha = fechaActual,
                                Periodo = fechaActual.ToString("yyyy-MM-dd"),
                                Valor = totalProcesos,
                                Unidad = "procesos"
                            });

                            fechaActual = fechaActual.AddDays(1);
                        }

                        tendencias.Add(new TendenciaRendimientoDto
                        {
                            TipoMetrica = "PRODUCTIVIDAD",
                            Puntos = puntos
                        });
                    }
                }

                return tendencias;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculando tendencias");
                throw;
            }
        }
    }
}

