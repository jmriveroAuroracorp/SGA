using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Rendimientos;
using SGA_Api.Models.Traspasos;
using SGA_Api.Models.Inventario;
using SGA_Api.Models.Conteos;
using SGA_Api.Models.Palet;
using SGA_Api.Models.Registro;
using SGA_Api.Models.Stock;

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
                // Consultar líneas creadas manualmente desde InventarioLineas (consolidadas)
                // Las líneas creadas se identifican por StockTeorico = 0 y StockContado > 0
                // Hacer JOIN con InventarioLineasTemp para obtener el UsuarioConteoId original
                var lineasCreadasQuery = from linea in _context.InventarioLineas
                                         join lineaTemp in _context.InventarioLineasTemp
                                         on new { 
                                             linea.IdInventario, 
                                             linea.CodigoArticulo, 
                                             linea.CodigoUbicacion
                                         } equals new { 
                                             IdInventario = lineaTemp.IdInventario, 
                                             CodigoArticulo = lineaTemp.CodigoArticulo, 
                                             CodigoUbicacion = lineaTemp.CodigoUbicacion
                                         }
                                         where linea.StockTeorico == 0 && 
                                               linea.StockContado.HasValue && 
                                               linea.StockContado.Value > 0 &&
                                               lineaTemp.UsuarioConteoId > 0 &&
                                               (linea.Partida == lineaTemp.Partida || (linea.Partida == null && lineaTemp.Partida == null)) &&
                                               (linea.PaletId == lineaTemp.PaletId || (linea.PaletId == null && lineaTemp.PaletId == null))
                                         select new
                                         {
                                             OperarioId = lineaTemp.UsuarioConteoId,
                                             FechaValidacion = linea.FechaValidacion
                                         };

                if (filtros.FechaDesde.HasValue)
                {
                    var fechaDesdeInicio = filtros.FechaDesde.Value.Date;
                    lineasCreadasQuery = lineasCreadasQuery.Where(l => l.FechaValidacion.HasValue && l.FechaValidacion.Value >= fechaDesdeInicio);
                }
                if (filtros.FechaHasta.HasValue)
                {
                    var fechaHastaFin = filtros.FechaHasta.Value.Date.AddDays(1).AddTicks(-1);
                    lineasCreadasQuery = lineasCreadasQuery.Where(l => l.FechaValidacion.HasValue && l.FechaValidacion.Value <= fechaHastaFin);
                }

                var lineasCreadasRaw = await lineasCreadasQuery.ToListAsync();

                var lineasCreadasPorOperario = lineasCreadasRaw
                    .GroupBy(l => l.OperarioId)
                    .Select(g => new
                    {
                        OperarioId = g.Key,
                        LineasCreadas = g.Count()
                    })
                    .ToList();

                var inventarioPorOperario = inventarioDataRaw
                    .GroupBy(i => i.OperarioId)
                    .Select(g => new
                    {
                        OperarioId = g.Key,
                        TotalLineas = g.Count(), // Total de líneas generadas
                        // 🔷 CORREGIDO: Solo contar líneas realmente modificadas (donde hubo un cambio)
                        // Esto evita contar líneas inicializadas a 0 que nunca se modificaron
                        LineasContadas = g.Count(i => i.CantidadContada.HasValue && i.CantidadContada.Value != i.StockActual), // Solo las que realmente contó el usuario
                        LineasConsolidadas = g.Count(i => i.Consolidado),
                        PrimeraLinea = g.Min(i => i.FechaConteo),
                        UltimaLinea = g.Max(i => i.FechaConteo),
                        // Calcular líneas con conteo (CantidadContada tiene valor y hubo cambio)
                        LineasConConteo = g.Where(i => i.CantidadContada.HasValue && i.CantidadContada.Value != i.StockActual).ToList(),
                        TodasLasLineas = g.ToList()
                    })
                    .ToList()
                    .Select(g => new
                    {
                        g.OperarioId,
                        g.TotalLineas,
                        g.LineasContadas,
                        // Obtener líneas creadas desde la consulta separada
                        LineasCreadas = lineasCreadasPorOperario.FirstOrDefault(l => l.OperarioId == g.OperarioId)?.LineasCreadas ?? 0,
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
                        LineasInventarioGeneradas = inventario?.TotalLineas ?? 0, // Total de líneas generadas
                        LineasInventarioContadas = inventario?.LineasContadas ?? 0, // Solo las que realmente contó el usuario
                        LineasInventarioCreadas = inventario?.LineasCreadas ?? 0, // Líneas creadas manualmente
                        TiempoPromedioInventarioMinutos = inventario?.TiempoTotalMinutos, // Tiempo total de trabajo (primera línea a última línea)
                        LecturasConteo = lecturas?.TotalLecturas ?? 0,
                        ConteosCompletados = conteos?.ConteosCompletados ?? 0,
                        TiempoPromedioConteoMinutos = conteos?.TiempoPromedioMinutos, // Tiempo promedio por orden de conteo
                        TotalOperaciones = (traspaso?.TotalTraspasos ?? 0) + 
                                         (inventario?.LineasContadas ?? 0) + // ← CAMBIAR: usar LineasContadas en lugar de TotalLineas
                                         (lecturas?.TotalLecturas ?? 0)
                    };

                    // Calcular líneas por hora basado en tiempo total de trabajo (inventarios)
                    // Usar LineasContadas (las que realmente contó) en lugar de TotalLineas (todas las generadas)
                    if (inventario?.LineasContadas > 0 && inventario.TiempoTotalMinutos.HasValue && inventario.TiempoTotalMinutos.Value > 0)
                    {
                        // Convertir minutos a horas y calcular líneas por hora
                        var horasTrabajadas = inventario.TiempoTotalMinutos.Value / 60.0;
                        rendimiento.LineasPorHora = inventario.LineasContadas / horasTrabajadas;
                    }
                    else if (inventario?.LineasContadas > 0)
                    {
                        // Si todas las líneas fueron contadas en el mismo momento (tiempo = 0),
                        // asumir un tiempo mínimo de 1 minuto para evitar división por cero
                        rendimiento.LineasPorHora = inventario.LineasContadas * 60; // 60 líneas por minuto = todas en el mismo momento
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
                        FechaConteo = i.FechaConteo,
                        CantidadContada = i.CantidadContada,
                        StockActual = i.StockActual // 🔷 AGREGADO: Necesario para detectar líneas realmente modificadas
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

                var totalLineas = inventarioDataStatsRaw.Count; // Total de líneas generadas
                // 🔷 CORREGIDO: Solo contar líneas realmente modificadas (donde hubo un cambio)
                // Esto evita contar líneas inicializadas a 0 que nunca se modificaron
                var lineasContadas = inventarioDataStatsRaw.Count(i => i.CantidadContada.HasValue && i.CantidadContada.Value != i.StockActual); // Solo las contadas
                var consolidados = inventarioDataStatsRaw.Count(i => i.Consolidado);
                
                var inventarioStats = new
                {
                    Total = totalLineas,
                    LineasContadas = lineasContadas,
                    Consolidados = consolidados,
                    TiempoTotalMinutos = tiempoTotalMinutos
                };

                if (inventarioStats != null)
                {
                    var tiempoTotalHoras = inventarioStats.TiempoTotalMinutos.HasValue && inventarioStats.TiempoTotalMinutos.Value > 0
                        ? inventarioStats.TiempoTotalMinutos.Value / 60.0 
                        : 0;
                    // Usar LineasContadas en lugar de Total para calcular líneas por hora
                    var lineasPorHora = tiempoTotalHoras > 0 
                        ? inventarioStats.LineasContadas / tiempoTotalHoras 
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

        public async Task<VolumenMovidoDto> ObtenerVolumenMovidoAsync(FiltroRendimientosDto filtros, bool incluirComparativa = true)
        {
            try
            {
                _logger.LogInformation("Calculando volumen movido con filtros: {Filtros}", 
                    System.Text.Json.JsonSerializer.Serialize(filtros));

                var resultado = new VolumenMovidoDto();

                // Consultar traspasos completados
                var traspasosQuery = _context.Traspasos.AsQueryable();
                
                if (filtros.FechaDesde.HasValue)
                {
                    var fechaDesdeInicio = filtros.FechaDesde.Value.Date;
                    traspasosQuery = traspasosQuery.Where(t => t.FechaInicio >= fechaDesdeInicio);
                }
                if (filtros.FechaHasta.HasValue)
                {
                    var fechaHastaFin = filtros.FechaHasta.Value.Date.AddDays(1).AddTicks(-1);
                    traspasosQuery = traspasosQuery.Where(t => t.FechaInicio <= fechaHastaFin);
                }
                if (filtros.CodigoEmpresa.HasValue)
                    traspasosQuery = traspasosQuery.Where(t => t.CodigoEmpresa == filtros.CodigoEmpresa.Value);

                var traspasosCompletados = await traspasosQuery
                    .Where(t => t.FechaFinalizacion.HasValue)
                    .Select(t => new
                    {
                        t.Cantidad,
                        t.TipoTraspaso,
                        t.PaletId,
                        t.CodigoArticulo,
                        t.MovPosicionOrigen,
                        t.MovPosicionDestino,
                        FechaFinalizacion = t.FechaFinalizacion!.Value,
                        t.CodigoEmpresa
                    })
                    .ToListAsync();

                // Obtener descripciones de artículos desde múltiples fuentes
                var codigosArticulos = traspasosCompletados
                    .Where(t => !string.IsNullOrEmpty(t.CodigoArticulo))
                    .Select(t => t.CodigoArticulo!)
                    .Distinct()
                    .ToList();

                var descripcionesArticulos = new Dictionary<string, string?>();
                if (codigosArticulos.Any() && filtros.CodigoEmpresa.HasValue)
                {
                    // 1. Intentar obtener desde PaletLineas
                    var descripcionesPalet = await _context.PaletLineas
                        .Where(pl => codigosArticulos.Contains(pl.CodigoArticulo) && 
                                     pl.CodigoEmpresa == filtros.CodigoEmpresa.Value &&
                                     !string.IsNullOrEmpty(pl.DescripcionArticulo))
                        .GroupBy(pl => pl.CodigoArticulo)
                        .Select(g => new { CodigoArticulo = g.Key, Descripcion = g.First().DescripcionArticulo })
                        .ToListAsync();

                    foreach (var desc in descripcionesPalet)
                    {
                        descripcionesArticulos[desc.CodigoArticulo] = desc.Descripcion;
                    }

                    // 2. Para los artículos que faltan, buscar en la tabla maestra Articulos de Sage
                    var codigosFaltantes = codigosArticulos
                        .Where(c => !descripcionesArticulos.ContainsKey(c))
                        .ToList();

                    if (codigosFaltantes.Any())
                    {
                        try
                        {
                            // Cargar todos los artículos de la empresa y filtrar en memoria para evitar Contains
                            var codigosFaltantesSet = codigosFaltantes.ToHashSet();
                            var descripcionesSage = await _sageContext.Articulos
                                .Where(a => a.CodigoEmpresa == filtros.CodigoEmpresa.Value &&
                                           !string.IsNullOrEmpty(a.DescripcionArticulo))
                                .Select(a => new { a.CodigoArticulo, a.DescripcionArticulo })
                                .ToListAsync();

                            // Filtrar en memoria
                            foreach (var desc in descripcionesSage)
                            {
                                if (codigosFaltantesSet.Contains(desc.CodigoArticulo))
                                {
                                    descripcionesArticulos[desc.CodigoArticulo] = desc.DescripcionArticulo;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "No se pudieron obtener algunas descripciones de artículos desde Sage");
                        }
                    }
                }

                // 1. Calcular total de unidades movidas
                resultado.TotalUnidadesMovidas = traspasosCompletados
                    .Where(t => t.Cantidad.HasValue)
                    .Sum(t => t.Cantidad!.Value);

                // 2. Calcular palets únicos
                resultado.TotalPaletsUnicos = traspasosCompletados
                    .Where(t => t.PaletId != Guid.Empty)
                    .Select(t => t.PaletId)
                    .Distinct()
                    .Count();

                // 3. Calcular valor económico desde MovimientoStock (Aurora)
                // Filtrar por fecha directamente para evitar problemas con Contains y GUIDs
                decimal totalValor = 0;
                if (filtros.FechaDesde.HasValue && filtros.FechaHasta.HasValue && filtros.CodigoEmpresa.HasValue)
                {
                    try
                    {
                        _sageContext.Database.SetCommandTimeout(60);
                        
                        // Filtrar MovimientoStock por fecha y empresa (evita usar Contains con GUIDs)
                        var fechaDesdeFiltro = filtros.FechaDesde.Value.Date;
                        var fechaHastaFiltro = filtros.FechaHasta.Value.Date.AddDays(1).AddTicks(-1);
                        
                        // Obtener movimientos de salida (TipoMovimiento = 2) en el rango de fechas
                        var movimientosSalida = await _sageContext.MovimientoStock
                            .Where(m => m.CodigoEmpresa == filtros.CodigoEmpresa.Value &&
                                       m.TipoMovimiento == 2 &&
                                       m.Fecha >= fechaDesdeFiltro &&
                                       m.Fecha <= fechaHastaFiltro)
                            .Select(m => new { m.MovPosicion, m.Importe })
                            .ToListAsync();

                        // Crear diccionario para búsqueda rápida por MovPosicion
                        var importesPorMovPosicion = movimientosSalida
                            .GroupBy(m => m.MovPosicion)
                            .ToDictionary(g => g.Key, g => g.Sum(m => (decimal)m.Importe));

                        // Sumar importes de los MovPosicionOrigen que coincidan con los traspasos
                        var movPosicionesOrigen = traspasosCompletados
                            .Where(t => t.MovPosicionOrigen != Guid.Empty)
                            .Select(t => t.MovPosicionOrigen)
                            .Distinct()
                            .ToList();

                        foreach (var movPos in movPosicionesOrigen)
                        {
                            if (importesPorMovPosicion.TryGetValue(movPos, out var importe))
                            {
                                totalValor += importe;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "No se pudo obtener valor económico desde MovimientoStock, se usará 0");
                        totalValor = 0;
                    }
                }
                resultado.TotalValorEconomico = totalValor;

                // 4. Desglose por tipo
                // IMPORTANTE: Usamos Traspaso.Cantidad, NO PaletLineas.Cantidad
                // Cuando se traspasa un palet, se crea un Traspaso por cada línea del palet
                // Cada Traspaso.Cantidad se guarda en el momento del traspaso desde TempPaletLinea.Cantidad
                // Esto es histórico y no se elimina aunque luego se eliminen las PaletLineas
                var traspasosPalet = traspasosCompletados
                    .Where(t => (t.TipoTraspaso?.ToUpper() ?? "ARTICULO") == "PALET" && t.PaletId != Guid.Empty)
                    .ToList();

                var traspasosArticulo = traspasosCompletados
                    .Where(t => (t.TipoTraspaso?.ToUpper() ?? "ARTICULO") != "PALET" || t.PaletId == Guid.Empty)
                    .ToList();

                // Para traspasos de palet: agrupar por PaletId y sumar todas las líneas del palet
                // Ejemplo: Si un palet tiene 3 líneas (10, 20, 30 unidades), se crean 3 traspasos
                // Agrupamos por PaletId y sumamos: 10+20+30 = 60 unidades movidas en ese palet
                resultado.DesglosePorTipo.UnidadesPalet = traspasosPalet
                    .Where(t => t.Cantidad.HasValue)
                    .GroupBy(t => t.PaletId)
                    .Select(g => g.Sum(t => t.Cantidad!.Value))
                    .Sum();
                
                // Contar palets únicos traspasados (no líneas de traspaso)
                resultado.DesglosePorTipo.CantidadTraspasosPalet = traspasosPalet
                    .Select(t => t.PaletId)
                    .Distinct()
                    .Count();

                resultado.DesglosePorTipo.UnidadesArticulo = traspasosArticulo
                    .Where(t => t.Cantidad.HasValue)
                    .Sum(t => t.Cantidad!.Value);
                resultado.DesglosePorTipo.CantidadTraspasosArticulo = traspasosArticulo.Count;

                // Calcular valor por tipo (simplificado: proporcional a unidades)
                if (resultado.TotalUnidadesMovidas > 0)
                {
                    resultado.DesglosePorTipo.ValorPalet = resultado.TotalValorEconomico * 
                        (resultado.DesglosePorTipo.UnidadesPalet / resultado.TotalUnidadesMovidas);
                    resultado.DesglosePorTipo.ValorArticulo = resultado.TotalValorEconomico * 
                        (resultado.DesglosePorTipo.UnidadesArticulo / resultado.TotalUnidadesMovidas);
                }

                // 5. Evolución temporal
                var diasDiferencia = filtros.FechaHasta.HasValue && filtros.FechaDesde.HasValue
                    ? (filtros.FechaHasta.Value - filtros.FechaDesde.Value).TotalDays
                    : 0;

                var agruparPorSemana = diasDiferencia > 30;

                var evolucion = traspasosCompletados
                    .GroupBy(t => agruparPorSemana 
                        ? new { Año = t.FechaFinalizacion.Year, Semana = System.Globalization.CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(t.FechaFinalizacion, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday) }
                        : (object)t.FechaFinalizacion.Date)
                    .Select(g => 
                    {
                        var traspasosPaletPeriodo = g.Where(t => (t.TipoTraspaso?.ToUpper() ?? "ARTICULO") == "PALET" && t.PaletId != Guid.Empty && t.Cantidad.HasValue).ToList();
                        var traspasosArticuloPeriodo = g.Where(t => ((t.TipoTraspaso?.ToUpper() ?? "ARTICULO") != "PALET" || t.PaletId == Guid.Empty) && t.Cantidad.HasValue).ToList();
                        
                        // Para traspasos de palet: agrupar por PaletId y sumar todas las líneas del palet
                        var unidadesPalet = traspasosPaletPeriodo
                            .GroupBy(t => t.PaletId)
                            .Select(paletGroup => paletGroup.Sum(t => t.Cantidad!.Value))
                            .Sum();
                        
                        return new PuntoEvolucionDto
                        {
                            Fecha = agruparPorSemana 
                                ? g.First().FechaFinalizacion.Date 
                                : ((DateTime)g.Key),
                            Periodo = agruparPorSemana 
                                ? $"{g.First().FechaFinalizacion.Year}-S{System.Globalization.CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(g.First().FechaFinalizacion, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday):D2}"
                                : ((DateTime)g.Key).ToString("yyyy-MM-dd"),
                            Unidades = g.Where(t => t.Cantidad.HasValue).Sum(t => t.Cantidad!.Value),
                            UnidadesPalet = unidadesPalet,
                            UnidadesArticulo = traspasosArticuloPeriodo.Sum(t => t.Cantidad!.Value),
                            Palets = g.Where(t => t.PaletId != Guid.Empty).Select(t => t.PaletId).Distinct().Count(),
                            Valor = 0 // Se calcularía igual que arriba, pero por simplicidad lo dejamos en 0
                        };
                    })
                    .OrderBy(p => p.Fecha)
                    .ToList();

                resultado.EvolucionTemporal = evolucion;

                // 6. Top artículos
                var topArticulos = traspasosCompletados
                    .Where(t => !string.IsNullOrEmpty(t.CodigoArticulo) && t.Cantidad.HasValue)
                    .GroupBy(t => t.CodigoArticulo!)
                    .Select(g => new TopArticuloVolumenDto
                    {
                        CodigoArticulo = g.Key,
                        DescripcionArticulo = descripcionesArticulos.ContainsKey(g.Key) ? descripcionesArticulos[g.Key] : null,
                        UnidadesMovidas = g.Sum(t => t.Cantidad!.Value)
                    })
                    .OrderByDescending(a => a.UnidadesMovidas)
                    .Take(10)
                    .ToList();

                if (resultado.TotalUnidadesMovidas > 0)
                {
                    for (int i = 0; i < topArticulos.Count; i++)
                    {
                        topArticulos[i].Posicion = i + 1;
                        topArticulos[i].PorcentajeDelTotal = (double)(topArticulos[i].UnidadesMovidas / resultado.TotalUnidadesMovidas * 100);
                    }
                }

                resultado.TopArticulos = topArticulos;

                // 7. Comparativa con período anterior (opcional, solo si no es llamada recursiva)
                if (incluirComparativa && filtros.FechaDesde.HasValue && filtros.FechaHasta.HasValue)
                {
                    var diasPeriodo = (filtros.FechaHasta.Value - filtros.FechaDesde.Value).TotalDays + 1;
                    var filtrosAnterior = new FiltroRendimientosDto
                    {
                        FechaDesde = filtros.FechaDesde.Value.AddDays(-diasPeriodo),
                        FechaHasta = filtros.FechaDesde.Value.AddDays(-1),
                        CodigoEmpresa = filtros.CodigoEmpresa,
                        CodigoAlmacen = filtros.CodigoAlmacen
                    };

                    try
                    {
                        // Llamada recursiva sin comparativa para evitar recursión infinita
                        var volumenAnterior = await ObtenerVolumenMovidoAsync(filtrosAnterior, incluirComparativa: false);
                        resultado.ComparativaPeriodoAnterior = new ComparativaVolumenDto
                        {
                            VariacionUnidades = volumenAnterior.TotalUnidadesMovidas > 0
                                ? (double)((resultado.TotalUnidadesMovidas - volumenAnterior.TotalUnidadesMovidas) / volumenAnterior.TotalUnidadesMovidas * 100)
                                : null,
                            VariacionValor = volumenAnterior.TotalValorEconomico > 0
                                ? (double)((resultado.TotalValorEconomico - volumenAnterior.TotalValorEconomico) / volumenAnterior.TotalValorEconomico * 100)
                                : null,
                            VariacionPalets = volumenAnterior.TotalPaletsUnicos > 0
                                ? (double)((resultado.TotalPaletsUnicos - volumenAnterior.TotalPaletsUnicos) / (double)volumenAnterior.TotalPaletsUnicos * 100)
                                : null
                        };
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "No se pudo calcular comparativa con período anterior");
                    }
                }

                return resultado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculando volumen movido");
                throw;
            }
        }

        public async Task<DistribucionDto> ObtenerDistribucionAsync(FiltroRendimientosDto filtros)
        {
            try
            {
                _logger.LogInformation("Calculando distribución con filtros: {Filtros}", 
                    System.Text.Json.JsonSerializer.Serialize(filtros));

                var resultado = new DistribucionDto();

                // Consultar traspasos completados
                var traspasosQuery = _context.Traspasos.AsQueryable();
                
                if (filtros.FechaDesde.HasValue)
                {
                    var fechaDesdeInicio = filtros.FechaDesde.Value.Date;
                    traspasosQuery = traspasosQuery.Where(t => t.FechaInicio >= fechaDesdeInicio);
                }
                if (filtros.FechaHasta.HasValue)
                {
                    var fechaHastaFin = filtros.FechaHasta.Value.Date.AddDays(1).AddTicks(-1);
                    traspasosQuery = traspasosQuery.Where(t => t.FechaInicio <= fechaHastaFin);
                }
                if (filtros.CodigoEmpresa.HasValue)
                    traspasosQuery = traspasosQuery.Where(t => t.CodigoEmpresa == filtros.CodigoEmpresa.Value);

                var traspasosCompletados = await traspasosQuery
                    .Where(t => t.FechaFinalizacion.HasValue)
                    .Select(t => new
                    {
                        t.Cantidad,
                        t.TipoTraspaso,
                        t.PaletId,
                        t.AlmacenOrigen,
                        t.AlmacenDestino,
                        t.UbicacionOrigen,
                        t.UbicacionDestino,
                        t.CodigoEmpresa
                    })
                    .ToListAsync();

                if (!traspasosCompletados.Any())
                {
                    return resultado;
                }

                // Calcular totales para porcentajes
                // Para unidades: agrupar por PaletId y sumar todas las líneas del palet
                var totalUnidades = traspasosCompletados
                    .Where(t => t.Cantidad.HasValue)
                    .GroupBy(t => t.PaletId)
                    .Select(g => g.Sum(t => t.Cantidad!.Value))
                    .Sum();
                
                // Para traspasos: contar palets únicos (no líneas de traspaso)
                var totalTraspasos = traspasosCompletados
                    .Where(t => t.PaletId != Guid.Empty)
                    .Select(t => t.PaletId)
                    .Distinct()
                    .Count();

                // 1. Top Almacenes Origen
                var almacenesOrigen = traspasosCompletados
                    .Where(t => !string.IsNullOrEmpty(t.AlmacenOrigen) && t.Cantidad.HasValue)
                    .GroupBy(t => t.AlmacenOrigen!)
                    .Select(g => new
                    {
                        CodigoAlmacen = g.Key,
                        UnidadesMovidas = g
                            .GroupBy(t => t.PaletId)
                            .Select(paletGroup => paletGroup.Sum(t => t.Cantidad!.Value))
                            .Sum(),
                        CantidadTraspasos = g.Select(t => t.PaletId).Distinct().Count()
                    })
                    .OrderByDescending(x => x.UnidadesMovidas)
                    .Take(10)
                    .ToList();

                resultado.TopAlmacenesOrigen = almacenesOrigen
                    .Select((a, index) => new AlmacenDistribucionDto
                    {
                        CodigoAlmacen = a.CodigoAlmacen,
                        UnidadesMovidas = a.UnidadesMovidas,
                        CantidadTraspasos = a.CantidadTraspasos,
                        PorcentajeDelTotal = totalUnidades > 0 ? (double)(a.UnidadesMovidas / totalUnidades * 100) : 0,
                        PorcentajePorTraspasos = totalTraspasos > 0 ? (double)(a.CantidadTraspasos / (double)totalTraspasos * 100) : 0,
                        Posicion = index + 1
                    })
                    .ToList();

                // 2. Top Almacenes Destino
                var almacenesDestino = traspasosCompletados
                    .Where(t => !string.IsNullOrEmpty(t.AlmacenDestino) && t.Cantidad.HasValue)
                    .GroupBy(t => t.AlmacenDestino!)
                    .Select(g => new
                    {
                        CodigoAlmacen = g.Key,
                        UnidadesMovidas = g
                            .GroupBy(t => t.PaletId)
                            .Select(paletGroup => paletGroup.Sum(t => t.Cantidad!.Value))
                            .Sum(),
                        CantidadTraspasos = g.Select(t => t.PaletId).Distinct().Count()
                    })
                    .OrderByDescending(x => x.UnidadesMovidas)
                    .Take(10)
                    .ToList();

                resultado.TopAlmacenesDestino = almacenesDestino
                    .Select((a, index) => new AlmacenDistribucionDto
                    {
                        CodigoAlmacen = a.CodigoAlmacen,
                        UnidadesMovidas = a.UnidadesMovidas,
                        CantidadTraspasos = a.CantidadTraspasos,
                        PorcentajeDelTotal = totalUnidades > 0 ? (double)(a.UnidadesMovidas / totalUnidades * 100) : 0,
                        PorcentajePorTraspasos = totalTraspasos > 0 ? (double)(a.CantidadTraspasos / (double)totalTraspasos * 100) : 0,
                        Posicion = index + 1
                    })
                    .ToList();

                // 3. Top Ubicaciones Origen
                var ubicacionesOrigen = traspasosCompletados
                    .Where(t => !string.IsNullOrEmpty(t.AlmacenOrigen) && !string.IsNullOrEmpty(t.UbicacionOrigen) && t.Cantidad.HasValue)
                    .GroupBy(t => new { AlmacenOrigen = t.AlmacenOrigen!, UbicacionOrigen = t.UbicacionOrigen! })
                    .Select(g => new
                    {
                        CodigoAlmacen = g.Key.AlmacenOrigen,
                        Ubicacion = g.Key.UbicacionOrigen,
                        UnidadesMovidas = g
                            .GroupBy(t => t.PaletId)
                            .Select(paletGroup => paletGroup.Sum(t => t.Cantidad!.Value))
                            .Sum(),
                        CantidadTraspasos = g.Select(t => t.PaletId).Distinct().Count()
                    })
                    .OrderByDescending(x => x.UnidadesMovidas)
                    .Take(10)
                    .ToList();

                resultado.TopUbicacionesOrigen = ubicacionesOrigen
                    .Select((u, index) => new UbicacionDistribucionDto
                    {
                        CodigoAlmacen = u.CodigoAlmacen,
                        Ubicacion = u.Ubicacion,
                        UnidadesMovidas = u.UnidadesMovidas,
                        CantidadTraspasos = u.CantidadTraspasos,
                        PorcentajeDelTotal = totalUnidades > 0 ? (double)(u.UnidadesMovidas / totalUnidades * 100) : 0,
                        PorcentajePorTraspasos = totalTraspasos > 0 ? (double)(u.CantidadTraspasos / (double)totalTraspasos * 100) : 0,
                        Posicion = index + 1
                    })
                    .ToList();

                // 4. Top Ubicaciones Destino
                var ubicacionesDestino = traspasosCompletados
                    .Where(t => !string.IsNullOrEmpty(t.AlmacenDestino) && !string.IsNullOrEmpty(t.UbicacionDestino) && t.Cantidad.HasValue)
                    .GroupBy(t => new { AlmacenDestino = t.AlmacenDestino!, UbicacionDestino = t.UbicacionDestino! })
                    .Select(g => new
                    {
                        CodigoAlmacen = g.Key.AlmacenDestino,
                        Ubicacion = g.Key.UbicacionDestino,
                        UnidadesMovidas = g
                            .GroupBy(t => t.PaletId)
                            .Select(paletGroup => paletGroup.Sum(t => t.Cantidad!.Value))
                            .Sum(),
                        CantidadTraspasos = g.Select(t => t.PaletId).Distinct().Count()
                    })
                    .OrderByDescending(x => x.UnidadesMovidas)
                    .Take(10)
                    .ToList();

                resultado.TopUbicacionesDestino = ubicacionesDestino
                    .Select((u, index) => new UbicacionDistribucionDto
                    {
                        CodigoAlmacen = u.CodigoAlmacen,
                        Ubicacion = u.Ubicacion,
                        UnidadesMovidas = u.UnidadesMovidas,
                        CantidadTraspasos = u.CantidadTraspasos,
                        PorcentajeDelTotal = totalUnidades > 0 ? (double)(u.UnidadesMovidas / totalUnidades * 100) : 0,
                        PorcentajePorTraspasos = totalTraspasos > 0 ? (double)(u.CantidadTraspasos / (double)totalTraspasos * 100) : 0,
                        Posicion = index + 1
                    })
                    .ToList();

                // 5. Flujos Principales (Origen → Destino)
                var flujos = traspasosCompletados
                    .Where(t => !string.IsNullOrEmpty(t.AlmacenOrigen) && !string.IsNullOrEmpty(t.AlmacenDestino) 
                        && !string.IsNullOrEmpty(t.UbicacionOrigen) && !string.IsNullOrEmpty(t.UbicacionDestino) 
                        && t.Cantidad.HasValue)
                    .GroupBy(t => new 
                    { 
                        AlmacenOrigen = t.AlmacenOrigen!, 
                        UbicacionOrigen = t.UbicacionOrigen!,
                        AlmacenDestino = t.AlmacenDestino!,
                        UbicacionDestino = t.UbicacionDestino!
                    })
                    .Select(g => new
                    {
                        AlmacenOrigen = g.Key.AlmacenOrigen,
                        UbicacionOrigen = g.Key.UbicacionOrigen,
                        AlmacenDestino = g.Key.AlmacenDestino,
                        UbicacionDestino = g.Key.UbicacionDestino,
                        UnidadesMovidas = g
                            .GroupBy(t => t.PaletId)
                            .Select(paletGroup => paletGroup.Sum(t => t.Cantidad!.Value))
                            .Sum(),
                        CantidadTraspasos = g.Select(t => t.PaletId).Distinct().Count()
                    })
                    .OrderByDescending(x => x.UnidadesMovidas)
                    .Take(10)
                    .ToList();

                resultado.FlujosPrincipales = flujos
                    .Select((f, index) => new FlujoDistribucionDto
                    {
                        AlmacenOrigen = f.AlmacenOrigen,
                        UbicacionOrigen = f.UbicacionOrigen,
                        AlmacenDestino = f.AlmacenDestino,
                        UbicacionDestino = f.UbicacionDestino,
                        UnidadesMovidas = f.UnidadesMovidas,
                        CantidadTraspasos = f.CantidadTraspasos,
                        PorcentajeDelTotal = totalUnidades > 0 ? (double)(f.UnidadesMovidas / totalUnidades * 100) : 0,
                        PorcentajePorTraspasos = totalTraspasos > 0 ? (double)(f.CantidadTraspasos / (double)totalTraspasos * 100) : 0,
                        Posicion = index + 1
                    })
                    .ToList();

                return resultado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculando distribución");
                throw;
            }
        }

        public async Task<List<RendimientoArticuloDto>> ObtenerRendimientoArticulosAsync(FiltroRendimientosDto filtros)
        {
            try
            {
                _logger.LogInformation("Calculando rendimiento de artículos con filtros: {Filtros}", 
                    System.Text.Json.JsonSerializer.Serialize(filtros));

                // Consultar traspasos completados
                var traspasosQuery = _context.Traspasos.AsQueryable();
                
                if (filtros.FechaDesde.HasValue)
                {
                    var fechaDesdeInicio = filtros.FechaDesde.Value.Date;
                    traspasosQuery = traspasosQuery.Where(t => t.FechaInicio >= fechaDesdeInicio);
                }
                if (filtros.FechaHasta.HasValue)
                {
                    var fechaHastaFin = filtros.FechaHasta.Value.Date.AddDays(1).AddTicks(-1);
                    traspasosQuery = traspasosQuery.Where(t => t.FechaInicio <= fechaHastaFin);
                }
                if (filtros.CodigoEmpresa.HasValue)
                    traspasosQuery = traspasosQuery.Where(t => t.CodigoEmpresa == filtros.CodigoEmpresa.Value);
                if (!string.IsNullOrEmpty(filtros.CodigoAlmacen))
                {
                    traspasosQuery = traspasosQuery.Where(t => 
                        t.AlmacenOrigen == filtros.CodigoAlmacen || 
                        t.AlmacenDestino == filtros.CodigoAlmacen);
                }

                var traspasosCompletados = await traspasosQuery
                    .Where(t => t.FechaFinalizacion.HasValue && 
                               !string.IsNullOrEmpty(t.CodigoArticulo) &&
                               t.Cantidad.HasValue)
                    .Select(t => new
                    {
                        t.CodigoArticulo,
                        t.Cantidad,
                        t.FechaInicio,
                        FechaFinalizacion = t.FechaFinalizacion!.Value,
                        t.AlmacenOrigen,
                        t.AlmacenDestino,
                        t.UbicacionOrigen,
                        t.UbicacionDestino,
                        t.UsuarioInicioId,
                        t.UsuarioFinalizacionId
                    })
                    .ToListAsync();

                if (!traspasosCompletados.Any())
                {
                    return new List<RendimientoArticuloDto>();
                }

                // Obtener códigos de artículo únicos para buscar CodigoFamilia
                var codigosArticulos = traspasosCompletados
                    .Select(t => t.CodigoArticulo!)
                    .Distinct()
                    .ToList();

                // Obtener CodigoFamilia de los artículos desde Sage
                var familiasArticulos = new Dictionary<string, string>();
                if (codigosArticulos.Any() && filtros.CodigoEmpresa.HasValue)
                {
                    try
                    {
                        // Crear HashSet para búsqueda eficiente O(1)
                        var codigosArticulosSet = codigosArticulos.ToHashSet();
                        
                        // Cargar todos los artículos de la empresa y filtrar en memoria para evitar Contains/OPENJSON
                        var articulosEmpresa = await _sageContext.Articulos
                            .Where(a => a.CodigoEmpresa == filtros.CodigoEmpresa.Value)
                            .Select(a => new { a.CodigoArticulo, a.CodigoFamilia })
                            .ToListAsync();
                        
                        // Filtrar en memoria usando HashSet (muy rápido)
                        foreach (var art in articulosEmpresa)
                        {
                            if (codigosArticulosSet.Contains(art.CodigoArticulo))
                            {
                                familiasArticulos[art.CodigoArticulo] = art.CodigoFamilia ?? "SIN_FAMILIA";
                            }
                        }
                        
                        // Para artículos que no se encontraron en Sage, asignar SIN_FAMILIA
                        foreach (var codigo in codigosArticulos)
                        {
                            if (!familiasArticulos.ContainsKey(codigo))
                            {
                                familiasArticulos[codigo] = "SIN_FAMILIA";
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "No se pudieron obtener códigos de familia desde Sage");
                        // Asignar SIN_FAMILIA a todos los artículos si falla la consulta
                        foreach (var codigo in codigosArticulos)
                        {
                            if (!familiasArticulos.ContainsKey(codigo))
                            {
                                familiasArticulos[codigo] = "SIN_FAMILIA";
                            }
                        }
                    }
                }
                else
                {
                    // Si no hay empresa, asignar SIN_FAMILIA a todos
                    foreach (var codigo in codigosArticulos)
                    {
                        familiasArticulos[codigo] = "SIN_FAMILIA";
                    }
                }

                // Calcular total de traspasos para porcentajes
                var totalTraspasos = traspasosCompletados.Count;

                // Agrupar por CodigoFamilia
                var rendimientos = traspasosCompletados
                    .GroupBy(t => familiasArticulos.ContainsKey(t.CodigoArticulo!) 
                        ? familiasArticulos[t.CodigoArticulo!] 
                        : "SIN_FAMILIA")
                    .Select(g =>
                    {
                        var codigoFamilia = g.Key;
                        var traspasosGrupo = g.ToList();
                        var unidadesTotales = traspasosGrupo.Sum(t => t.Cantidad!.Value);
                        var cantidadTraspasos = traspasosGrupo.Count;
                        var promedioUnidades = cantidadTraspasos > 0 ? unidadesTotales / cantidadTraspasos : 0;
                        
                        // Contar artículos únicos de esta familia
                        var articulosUnicos = traspasosGrupo
                            .Select(t => t.CodigoArticulo!)
                            .Distinct()
                            .Count();

                        // Calcular tiempos
                        var tiemposValidos = traspasosGrupo
                            .Where(t => t.FechaFinalizacion > t.FechaInicio)
                            .Select(t => (t.FechaFinalizacion - t.FechaInicio).TotalMinutes)
                            .Where(t => t >= 0.01)
                            .ToList();

                        var tiempoPromedio = tiemposValidos.Any() ? (double?)tiemposValidos.Average() : null;
                        var tiempoTotal = tiemposValidos.Sum();
                        var eficiencia = tiempoPromedio.HasValue && tiempoPromedio.Value > 0 
                            ? (double)((double)unidadesTotales / tiempoPromedio.Value) 
                            : (double?)null;

                        // Calcular distribución
                        var almacenes = new HashSet<string>();
                        var ubicaciones = new HashSet<string>();
                        var operarios = new HashSet<int>();

                        foreach (var t in traspasosGrupo)
                        {
                            if (!string.IsNullOrEmpty(t.AlmacenOrigen))
                                almacenes.Add(t.AlmacenOrigen);
                            if (!string.IsNullOrEmpty(t.AlmacenDestino))
                                almacenes.Add(t.AlmacenDestino);
                            if (!string.IsNullOrEmpty(t.UbicacionOrigen))
                                ubicaciones.Add(t.UbicacionOrigen);
                            if (!string.IsNullOrEmpty(t.UbicacionDestino))
                                ubicaciones.Add(t.UbicacionDestino);
                            operarios.Add(t.UsuarioInicioId);
                            if (t.UsuarioFinalizacionId.HasValue)
                                operarios.Add(t.UsuarioFinalizacionId.Value);
                        }

                        return new RendimientoArticuloDto
                        {
                            CodigoFamilia = codigoFamilia,
                            CantidadArticulosUnicos = articulosUnicos,
                            CantidadTraspasos = cantidadTraspasos,
                            UnidadesTotalesMovidas = unidadesTotales,
                            PromedioUnidadesPorTraspaso = promedioUnidades,
                            TiempoPromedioMinutos = tiempoPromedio,
                            TiempoTotalMinutos = tiempoTotal,
                            EficienciaUnidadesPorMinuto = eficiencia,
                            AlmacenesUnicos = almacenes.Count,
                            UbicacionesUnicas = ubicaciones.Count,
                            OperariosUnicos = operarios.Count,
                            PorcentajeDelTotalTraspasos = totalTraspasos > 0 
                                ? (double)(cantidadTraspasos / (double)totalTraspasos * 100) 
                                : 0
                        };
                    })
                    .OrderByDescending(r => r.CantidadTraspasos)
                    .ToList();

                // Asignar posiciones y recalcular porcentajes
                var totalTraspasosReal = traspasosCompletados.Count;
                var rendimientosList = rendimientos.ToList();
                for (int i = 0; i < rendimientosList.Count; i++)
                {
                    rendimientosList[i].Posicion = i + 1;
                    rendimientosList[i].PorcentajeDelTotalTraspasos = totalTraspasosReal > 0
                        ? (double)(rendimientosList[i].CantidadTraspasos / (double)totalTraspasosReal * 100)
                        : 0;
                }

                return rendimientosList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculando rendimiento de artículos");
                throw;
            }
        }
    }
}

