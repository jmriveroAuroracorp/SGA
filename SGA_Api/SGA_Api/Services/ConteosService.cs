using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Conteos;
using SGA_Api.Models.Stock;
using SGA_Api.Models.Almacen;

namespace SGA_Api.Services
{
    public class ConteosService : IConteosService
    {
        private readonly AuroraSgaDbContext _context;
        private readonly SageDbContext _sageDbContext;
        private readonly StorageControlDbContext _storageControlContext;
        private readonly ILogger<ConteosService> _logger;

        public ConteosService(
            AuroraSgaDbContext context, 
            SageDbContext sageDbContext,
            StorageControlDbContext storageControlContext,
            ILogger<ConteosService> logger)
        {
            _context = context;
            _sageDbContext = sageDbContext;
            _storageControlContext = storageControlContext;
            _logger = logger;
        }

        public async Task<OrdenDto> CrearOrdenAsync(CrearOrdenDto dto)
        {
            try
            {
                _logger.LogInformation("Iniciando creación de orden de conteo: {Titulo}", dto.Titulo);
                
                // Extraer valores del FiltrosJson
                var codigoAlmacen = ExtraerAlmacenDelFiltro(dto.FiltrosJson);
                var codigoArticulo = ExtraerArticuloDelFiltro(dto.FiltrosJson);
                
                // Primero intentar extraer ubicación directa del filtro (para ubicaciones especiales)
                var codigoUbicacion = ExtraerUbicacionDelFiltro(dto.FiltrosJson);
                
                // Extraer componentes para determinar el alcance automáticamente
                var pasillo = ExtraerPasilloDelFiltro(dto.FiltrosJson);
                var estanteria = ExtraerEstanteriaDelFiltro(dto.FiltrosJson);
                var altura = ExtraerAlturaDelFiltro(dto.FiltrosJson);
                var posicion = ExtraerPosicionDelFiltro(dto.FiltrosJson);
                
                                 // Determinar el alcance automáticamente según los componentes disponibles
                 // IGNORAR el alcance enviado por el cliente y determinarlo automáticamente
                 string alcanceDeterminado = "ALMACEN"; // Default
                
                                 if (!string.IsNullOrEmpty(codigoUbicacion) || codigoUbicacion == "")
                 {
                     // Si hay ubicación directa (incluyendo ubicación vacía ""), el alcance es UBICACION
                     alcanceDeterminado = "UBICACION";
                 }
                else if (!string.IsNullOrEmpty(pasillo) && !string.IsNullOrEmpty(estanteria) && 
                         !string.IsNullOrEmpty(altura) && !string.IsNullOrEmpty(posicion))
                {
                    // Si están todos los componentes, es UBICACION
                    alcanceDeterminado = "UBICACION";
                    // Construir ubicación en formato UB + pasillo + estanteria + altura + posicion
                    var pasilloFormateado = pasillo.PadLeft(3, '0');
                    var estanteriaFormateada = estanteria.PadLeft(3, '0');
                    var alturaFormateada = altura.PadLeft(3, '0');
                    var posicionFormateada = posicion.PadLeft(3, '0');
                    
                    codigoUbicacion = $"UB{pasilloFormateado}{estanteriaFormateada}{alturaFormateada}{posicionFormateada}";
                }
                else if (!string.IsNullOrEmpty(pasillo) && !string.IsNullOrEmpty(estanteria))
                {
                    // Si hay pasillo y estantería, es ESTANTERIA
                    alcanceDeterminado = "ESTANTERIA";
                }
                else if (!string.IsNullOrEmpty(pasillo))
                {
                    // Si solo hay pasillo, es PASILLO
                    alcanceDeterminado = "PASILLO";
                }
                // Si solo hay almacén, el alcance es ALMACEN (default)
                
                                 _logger.LogInformation("ALCANCE_AUTO: Alcance determinado automáticamente: '{AlcanceDeterminado}' para filtros: almacen='{Almacen}', pasillo='{Pasillo}', estanteria='{Estanteria}', altura='{Altura}', posicion='{Posicion}', ubicacion='{Ubicacion}'", 
                     alcanceDeterminado, codigoAlmacen, pasillo, estanteria, altura, posicion, codigoUbicacion);
                 
                 _logger.LogInformation("ALCANCE_AUTO: Detalles de determinación - codigoUbicacion='{CodigoUbicacion}', esVacio={EsVacio}, esNull={EsNull}", 
                     codigoUbicacion, codigoUbicacion == "", codigoUbicacion == null);

                var orden = new OrdenConteo
                {
                    CodigoEmpresa = dto.CodigoEmpresa,
                    Titulo = dto.Titulo,
                    Visibilidad = dto.Visibilidad,
                    ModoGeneracion = dto.ModoGeneracion,
                    Alcance = alcanceDeterminado, // Usar el alcance determinado automáticamente
                    FiltrosJson = dto.FiltrosJson,
                    FechaPlan = dto.FechaPlan?.ToUniversalTime(),
                    FechaEjecucion = dto.FechaEjecucion?.ToUniversalTime(),
                    SupervisorCodigo = dto.SupervisorCodigo,
                    CreadoPorCodigo = dto.CreadoPorCodigo,
                    Estado = !string.IsNullOrEmpty(dto.CodigoOperario) ? "ASIGNADO" : "PLANIFICADO",
                    Prioridad = dto.Prioridad,
                    FechaCreacion = DateTime.UtcNow,
                    CodigoOperario = dto.CodigoOperario,
                    FechaAsignacion = !string.IsNullOrEmpty(dto.CodigoOperario) ? DateTime.UtcNow : null,
                    CodigoAlmacen = codigoAlmacen,
                    CodigoUbicacion = codigoUbicacion,
                    CodigoArticulo = codigoArticulo
                };

                _context.OrdenesConteo.Add(orden);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Orden guardada con ID: {Id}", orden.Id);

                return MapToOrdenDto(orden);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CrearOrdenAsync: {Message}", ex.Message);
                throw;
            }
        }

        public async Task<OrdenDto?> ObtenerOrdenAsync(long id)
        {
            var orden = await _context.OrdenesConteo
                .FirstOrDefaultAsync(o => o.Id == id);

            return orden != null ? MapToOrdenDto(orden) : null;
        }

        public async Task<IEnumerable<OrdenDto>> ListarOrdenesAsync(string? codigoOperario = null, string? estado = null)
        {
            try
            {
                _logger.LogInformation("Iniciando ListarOrdenesAsync con codigoOperario: {CodigoOperario}, estado: {Estado}", codigoOperario, estado);
                
                var query = _context.OrdenesConteo.AsQueryable();

                if (!string.IsNullOrEmpty(codigoOperario))
                {
                    query = query.Where(o => o.CodigoOperario == codigoOperario && 
                                           (o.Estado == "ASIGNADO" || o.Estado == "EN_PROCESO"));
                }

                if (!string.IsNullOrEmpty(estado))
                {
                    query = query.Where(o => o.Estado == estado);
                }

                var ordenes = await query
                    .OrderByDescending(o => o.FechaCreacion)
                    .ToListAsync();

                _logger.LogInformation("Se encontraron {Count} órdenes de conteo", ordenes.Count);
                return ordenes.Select(MapToOrdenDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ListarOrdenesAsync: {Message}", ex.Message);
                throw;
            }
        }

        public async Task<OrdenDto> IniciarOrdenAsync(long id, string codigoOperario)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                var orden = await _context.OrdenesConteo
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (orden == null)
                {
                    throw new InvalidOperationException($"No se encontró la orden con ID {id}");
                }

                if (orden.Estado != "ASIGNADO" && orden.Estado != "PLANIFICADO")
                {
                    throw new InvalidOperationException($"No se puede iniciar una orden en estado {orden.Estado}");
                }

                if (orden.Estado == "EN_PROCESO" && orden.FechaInicio.HasValue)
                {
                    await transaction.CommitAsync();
                    return MapToOrdenDto(orden);
                }

                orden.Estado = "EN_PROCESO";
                orden.CodigoOperario = codigoOperario;
                orden.FechaInicio = DateTime.UtcNow;
                orden.FechaAsignacion = orden.FechaAsignacion ?? DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await GenerarLecturasAutomaticasAsync(orden);
                await transaction.CommitAsync();

                _logger.LogInformation("Orden {Id} iniciada por operario {Operario}", id, codigoOperario);
                return MapToOrdenDto(orden);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<OrdenDto> AsignarOperarioAsync(long id, AsignarOperarioDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                var orden = await _context.OrdenesConteo
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (orden == null)
                {
                    throw new InvalidOperationException($"No se encontró la orden con ID {id}");
                }

                if (orden.Estado != "PLANIFICADO" && orden.Estado != "ASIGNADO")
                {
                    throw new InvalidOperationException($"No se puede asignar operario a una orden en estado {orden.Estado}");
                }

                if (orden.CodigoOperario == dto.CodigoOperario)
                {
                    await transaction.CommitAsync();
                    return MapToOrdenDto(orden);
                }

                orden.CodigoOperario = dto.CodigoOperario;
                orden.Estado = "ASIGNADO";
                orden.FechaAsignacion = DateTime.UtcNow;
                
                if (!string.IsNullOrEmpty(dto.Comentario))
                {
                    orden.Comentario = dto.Comentario;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Orden {Id} asignada al operario {Operario}", id, dto.CodigoOperario);
                return MapToOrdenDto(orden);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // Continuaré con el resto del servicio en la siguiente parte...
        private async Task GenerarLecturasAutomaticasAsync(OrdenConteo orden)
        {
            try
            {
                var lecturasGeneradas = new List<LecturaConteo>();

                // Obtener el almacén de la orden o del filtro
                var codigoAlmacen = orden.CodigoAlmacen;
                if (string.IsNullOrEmpty(codigoAlmacen))
                {
                    // Intentar extraer del filtro JSON
                    codigoAlmacen = ExtraerAlmacenDelFiltro(orden.FiltrosJson);
                }

                if (string.IsNullOrEmpty(codigoAlmacen))
                {
                    _logger.LogWarning("No se pudo determinar el almacén para la orden {Id}", orden.Id);
                    return;
                }

                _logger.LogInformation("GENERACION_DEBUG: Alcance de la orden: '{Alcance}', FiltrosJson: '{FiltrosJson}'", orden.Alcance, orden.FiltrosJson);
                
                switch (orden.Alcance?.ToUpper())
                {
                    case "ARTICULO":
                        _logger.LogInformation("GENERACION_DEBUG: Generando lecturas por ARTICULO");
                        await GenerarLecturasPorArticuloAsync(orden, codigoAlmacen, lecturasGeneradas);
                        break;
                    case "UBICACION":
                        _logger.LogInformation("GENERACION_DEBUG: Generando lecturas por UBICACION");
                        await GenerarLecturasPorUbicacionAsync(orden, codigoAlmacen, lecturasGeneradas);
                        break;
                    case "ESTANTERIA":
                        _logger.LogInformation("GENERACION_DEBUG: Generando lecturas por ESTANTERIA");
                        await GenerarLecturasPorEstanteriaAsync(orden, codigoAlmacen, lecturasGeneradas);
                        break;
                    case "PALET":
                        _logger.LogInformation("GENERACION_DEBUG: Generando lecturas por PALET");
                        await GenerarLecturasPorPaletAsync(orden, codigoAlmacen, lecturasGeneradas);
                        break;
                    case "PASILLO":
                        _logger.LogInformation("GENERACION_DEBUG: Generando lecturas por PASILLO");
                        await GenerarLecturasPorPasilloAsync(orden, codigoAlmacen, lecturasGeneradas);
                        break;
                    case "ALMACEN":
                    default:
                        _logger.LogInformation("GENERACION_DEBUG: Generando lecturas por ALMACEN (default)");
                        await GenerarLecturasPorAlmacenAsync(orden, codigoAlmacen, lecturasGeneradas);
                        break;
                }

                if (lecturasGeneradas.Any())
                {
                    _context.LecturasConteo.AddRange(lecturasGeneradas);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Generadas {Count} lecturas automáticas para orden {Id}", 
                        lecturasGeneradas.Count, orden.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generando lecturas automáticas para orden {Id}", orden.Id);
                throw;
            }
        }

        private async Task GenerarLecturasPorArticuloAsync(OrdenConteo orden, string codigoAlmacen, List<LecturaConteo> lecturasGeneradas)
        {
            // Obtener ejercicio actual
            var ejercicio = await _sageDbContext.Periodos
                .Where(p => p.CodigoEmpresa == orden.CodigoEmpresa && p.Fechainicio <= DateTime.Now)
                .OrderByDescending(p => p.Fechainicio)
                .Select(p => p.Ejercicio)
                .FirstOrDefaultAsync();

            if (ejercicio == 0) return;

            var codigoArticulo = orden.CodigoArticulo;
            if (string.IsNullOrEmpty(codigoArticulo))
            {
                codigoArticulo = ExtraerArticuloDelFiltro(orden.FiltrosJson);
            }

            if (!string.IsNullOrEmpty(codigoArticulo))
            {
                var stockArticulo = await _storageControlContext.AcumuladoStockUbicacion
                    .Where(x => x.CodigoEmpresa == orden.CodigoEmpresa &&
                               x.Ejercicio == ejercicio &&
                               x.CodigoAlmacen == codigoAlmacen &&
                               x.CodigoArticulo == codigoArticulo &&
                               x.UnidadSaldo > 0)
                    .ToListAsync();

                var descripcionArticulo = await ObtenerDescripcionArticuloAsync(orden.CodigoEmpresa, codigoArticulo);

                foreach (var stock in stockArticulo)
                {
                    // Solo incluir ubicaciones válidas para conteo
                    if (EsUbicacionValidaParaConteo(stock.Ubicacion))
                    {
                        lecturasGeneradas.Add(new LecturaConteo
                        {
                            OrdenId = orden.Id,
                            CodigoAlmacen = codigoAlmacen,
                            CodigoUbicacion = stock.Ubicacion,
                            CodigoArticulo = stock.CodigoArticulo,
                            DescripcionArticulo = descripcionArticulo,
                            LotePartida = stock.Partida,
                            CantidadContada = null,
                            CantidadStock = stock.UnidadSaldo,
                            UsuarioCodigo = orden.CodigoOperario ?? "",
                            Fecha = DateTime.UtcNow
                        });
                    }
                }
            }
        }

        private async Task GenerarLecturasPorAlmacenAsync(OrdenConteo orden, string codigoAlmacen, List<LecturaConteo> lecturasGeneradas)
        {
            // 🚨 OBTENER EL EJERCICIO ACTUAL (igual que en StockController)
            var ejercicio = await _sageDbContext.Periodos
                .Where(p => p.CodigoEmpresa == orden.CodigoEmpresa && p.Fechainicio <= DateTime.Now)
                .OrderByDescending(p => p.Fechainicio)
                .Select(p => p.Ejercicio)
                .FirstOrDefaultAsync();

            if (ejercicio == 0)
            {
                _logger.LogWarning("No se encontró ejercicio válido para empresa {CodigoEmpresa}", orden.CodigoEmpresa);
                return;
            }

            _logger.LogInformation("🔍 CONSULTANDO StorageControlDbContext.AcumuladoStockUbicacion");
            _logger.LogInformation("🔍 FILTROS: CodigoEmpresa={CodigoEmpresa}, CodigoAlmacen={CodigoAlmacen}, Ejercicio={Ejercicio}", orden.CodigoEmpresa, codigoAlmacen, ejercicio);
            
            var stockPorUbicacion = await _storageControlContext.AcumuladoStockUbicacion
                .Where(x => x.CodigoEmpresa == orden.CodigoEmpresa &&
                           x.Ejercicio == ejercicio &&  // 🚨 FILTRO POR EJERCICIO
                           x.CodigoAlmacen == codigoAlmacen &&
                           x.UnidadSaldo > 0)
                .ToListAsync();

            _logger.LogInformation("🔍 ENCONTRADOS {Count} registros en AcumuladoStockUbicacion", stockPorUbicacion.Count);
            
            // Mostrar TODOS los registros encontrados
            foreach (var stock in stockPorUbicacion.Take(20))
            {
                _logger.LogInformation("🔍 STOCK: Empresa={Empresa}, Almacen={Almacen}, Ubicacion='{Ubicacion}', Articulo={Articulo}, Saldo={Saldo}, Partida={Partida}", 
                    stock.CodigoEmpresa, stock.CodigoAlmacen, stock.Ubicacion ?? "NULL", stock.CodigoArticulo, stock.UnidadSaldo, stock.Partida);
            }

            foreach (var stock in stockPorUbicacion)
            {
                if (EsUbicacionValidaParaConteo(stock.Ubicacion))
                {
                    string? descripcionArticulo = null;
                    if (!string.IsNullOrEmpty(stock.CodigoArticulo))
                    {
                        descripcionArticulo = await ObtenerDescripcionArticuloAsync(orden.CodigoEmpresa, stock.CodigoArticulo);
                    }
                    
                    lecturasGeneradas.Add(new LecturaConteo
                    {
                        OrdenId = orden.Id,
                        CodigoAlmacen = codigoAlmacen,
                        CodigoUbicacion = stock.Ubicacion,
                        CodigoArticulo = stock.CodigoArticulo,
                        DescripcionArticulo = descripcionArticulo,
                        LotePartida = stock.Partida,
                        CantidadContada = null,
                        CantidadStock = stock.UnidadSaldo,
                        UsuarioCodigo = orden.CodigoOperario ?? "",
                        Fecha = DateTime.UtcNow
                    });
                }
            }
        }

        private async Task GenerarLecturasPorPasilloAsync(OrdenConteo orden, string codigoAlmacen, List<LecturaConteo> lecturasGeneradas)
        {
            // Obtener ejercicio actual
            var ejercicio = await _sageDbContext.Periodos
                .Where(p => p.CodigoEmpresa == orden.CodigoEmpresa && p.Fechainicio <= DateTime.Now)
                .OrderByDescending(p => p.Fechainicio)
                .Select(p => p.Ejercicio)
                .FirstOrDefaultAsync();

            if (ejercicio == 0) return;

            var pasillo = ExtraerPasilloDelFiltro(orden.FiltrosJson);
            if (!string.IsNullOrEmpty(pasillo))
            {
                var prefijoPasillo = $"UB{pasillo.PadLeft(3, '0')}";
                
                var stockPasillo = await _storageControlContext.AcumuladoStockUbicacion
                    .Where(x => x.CodigoEmpresa == orden.CodigoEmpresa &&
                               x.Ejercicio == ejercicio &&
                               x.CodigoAlmacen == codigoAlmacen &&
                               x.Ubicacion != null &&
                               x.Ubicacion.StartsWith(prefijoPasillo) &&
                               x.UnidadSaldo > 0)
                    .ToListAsync();

                foreach (var stock in stockPasillo)
                {
                    // Solo incluir ubicaciones válidas para conteo
                    if (EsUbicacionValidaParaConteo(stock.Ubicacion))
                    {
                        string? descripcionArticulo = null;
                        if (!string.IsNullOrEmpty(stock.CodigoArticulo))
                        {
                            descripcionArticulo = await ObtenerDescripcionArticuloAsync(orden.CodigoEmpresa, stock.CodigoArticulo);
                        }
                        
                        lecturasGeneradas.Add(new LecturaConteo
                        {
                            OrdenId = orden.Id,
                            CodigoAlmacen = codigoAlmacen,
                            CodigoUbicacion = stock.Ubicacion,
                            CodigoArticulo = stock.CodigoArticulo,
                            DescripcionArticulo = descripcionArticulo,
                            LotePartida = stock.Partida,
                            CantidadContada = null,
                            CantidadStock = stock.UnidadSaldo,
                            UsuarioCodigo = orden.CodigoOperario ?? "",
                            Fecha = DateTime.UtcNow
                        });
                    }
                }
            }
        }

        private async Task GenerarLecturasPorUbicacionAsync(OrdenConteo orden, string codigoAlmacen, List<LecturaConteo> lecturasGeneradas)
        {
            // Obtener ejercicio actual
            var ejercicio = await _sageDbContext.Periodos
                .Where(p => p.CodigoEmpresa == orden.CodigoEmpresa && p.Fechainicio <= DateTime.Now)
                .OrderByDescending(p => p.Fechainicio)
                .Select(p => p.Ejercicio)
                .FirstOrDefaultAsync();

            if (ejercicio == 0) return;

                         // Si el alcance es UBICACION, obtener la ubicación específica desde los filtros
             string? ubicacionEspecifica = null;
             if (orden.Alcance == "UBICACION")
             {
                 // Primero intentar extraer ubicación directa del filtro (para ubicaciones especiales)
                 ubicacionEspecifica = ExtraerUbicacionDelFiltro(orden.FiltrosJson);
                 
                 // Si no hay ubicación directa, construir a partir de los componentes
                 if (ubicacionEspecifica == null)
                 {
                     var pasillo = ExtraerPasilloDelFiltro(orden.FiltrosJson);
                     var estanteria = ExtraerEstanteriaDelFiltro(orden.FiltrosJson);
                     var altura = ExtraerAlturaDelFiltro(orden.FiltrosJson);
                     var posicion = ExtraerPosicionDelFiltro(orden.FiltrosJson);
                     
                     if (!string.IsNullOrEmpty(pasillo) && !string.IsNullOrEmpty(estanteria))
                     {
                         var pasilloFormateado = pasillo.PadLeft(3, '0');
                         var estanteriaFormateada = estanteria.PadLeft(3, '0');
                         var alturaFormateada = altura?.PadLeft(3, '0') ?? "001";
                         var posicionFormateada = posicion?.PadLeft(3, '0') ?? "001";
                         
                         ubicacionEspecifica = $"UB{pasilloFormateado}{estanteriaFormateada}{alturaFormateada}{posicionFormateada}";
                     }
                 }
             }

            _logger.LogInformation("UBICACION_DEBUG: Ubicación específica extraída = '{UbicacionEspecifica}'", ubicacionEspecifica);
            
            // Construir la consulta base
            var query = _storageControlContext.AcumuladoStockUbicacion
                .Where(x => x.CodigoEmpresa == orden.CodigoEmpresa &&
                           x.Ejercicio == ejercicio &&
                           x.CodigoAlmacen == codigoAlmacen &&
                           x.UnidadSaldo > 0);

            // Si hay ubicación específica, filtrar por ella
            if (ubicacionEspecifica != null)
            {
                query = query.Where(x => x.Ubicacion == ubicacionEspecifica);
                _logger.LogInformation("UBICACION_DEBUG: Aplicando filtro por ubicación específica: '{UbicacionEspecifica}'", ubicacionEspecifica);
            }

            var stockPorUbicacion = await query.ToListAsync();
            
            _logger.LogInformation("UBICACION_DEBUG: Encontrados {Count} registros de stock para la ubicación", stockPorUbicacion.Count);
            
            // Mostrar los primeros registros para debug
            foreach (var stock in stockPorUbicacion.Take(5))
            {
                _logger.LogInformation("UBICACION_DEBUG: Stock encontrado - Ubicación: '{Ubicacion}', Artículo: {Articulo}, Saldo: {Saldo}", 
                    stock.Ubicacion, stock.CodigoArticulo, stock.UnidadSaldo);
            }

            foreach (var stock in stockPorUbicacion)
            {
                // Incluir ubicaciones especiales (suelo, playa, vacía) y ubicaciones normales
                if (EsUbicacionValidaParaConteo(stock.Ubicacion))
                {
                    // Obtener la descripción del artículo individualmente
                    string? descripcionArticulo = null;
                    if (!string.IsNullOrEmpty(stock.CodigoArticulo))
                    {
                        descripcionArticulo = await ObtenerDescripcionArticuloAsync(orden.CodigoEmpresa, stock.CodigoArticulo);
                    }
                    
                    lecturasGeneradas.Add(new LecturaConteo
                    {
                        OrdenId = orden.Id,
                        CodigoAlmacen = codigoAlmacen,
                        CodigoUbicacion = stock.Ubicacion,
                        CodigoArticulo = stock.CodigoArticulo,
                        DescripcionArticulo = descripcionArticulo,
                        LotePartida = stock.Partida,
                        CantidadContada = null,
                        CantidadStock = stock.UnidadSaldo,
                        UsuarioCodigo = orden.CodigoOperario ?? "",
                        Fecha = DateTime.UtcNow
                    });
                    
                    _logger.LogInformation("UBICACION_DEBUG: Lectura generada para ubicación '{Ubicacion}', artículo {Articulo}", 
                        stock.Ubicacion, stock.CodigoArticulo);
                }
                else
                {
                    _logger.LogWarning("UBICACION_DEBUG: Ubicación '{Ubicacion}' no válida para conteo, artículo {Articulo}", 
                        stock.Ubicacion, stock.CodigoArticulo);
                }
            }
        }

        private async Task GenerarLecturasPorEstanteriaAsync(OrdenConteo orden, string codigoAlmacen, List<LecturaConteo> lecturasGeneradas)
        {
            // Obtener ejercicio actual
            var ejercicio = await _sageDbContext.Periodos
                .Where(p => p.CodigoEmpresa == orden.CodigoEmpresa && p.Fechainicio <= DateTime.Now)
                .OrderByDescending(p => p.Fechainicio)
                .Select(p => p.Ejercicio)
                .FirstOrDefaultAsync();

            if (ejercicio == 0) return;

            var pasillo = ExtraerPasilloDelFiltro(orden.FiltrosJson);
            var estanteria = ExtraerEstanteriaDelFiltro(orden.FiltrosJson);
            
            _logger.LogInformation("ESTANTERIA_DEBUG: FiltrosJson = '{FiltrosJson}'", orden.FiltrosJson);
            _logger.LogInformation("ESTANTERIA_DEBUG: Pasillo extraído = '{Pasillo}', Estantería extraída = '{Estanteria}'", pasillo, estanteria);
            
            if (!string.IsNullOrEmpty(pasillo) && !string.IsNullOrEmpty(estanteria))
            {
                var pasilloFormateado = pasillo.PadLeft(3, '0');
                var estanteriaFormateada = estanteria.PadLeft(3, '0');
                
                _logger.LogInformation("ESTANTERIA_DEBUG: Pasillo formateado = '{PasilloFormateado}', Estantería formateada = '{EstanteriaFormateada}'", pasilloFormateado, estanteriaFormateada);
                
                var stockEstanteria = await _storageControlContext.AcumuladoStockUbicacion
                    .Where(x => x.CodigoEmpresa == orden.CodigoEmpresa &&
                               x.Ejercicio == ejercicio &&
                               x.CodigoAlmacen == codigoAlmacen &&
                               x.Ubicacion != null &&
                               x.Ubicacion.StartsWith("UB") &&
                               x.Ubicacion.Length >= 8 &&
                               x.Ubicacion.Substring(2, 3) == pasilloFormateado &&
                               x.Ubicacion.Substring(5, 3) == estanteriaFormateada &&
                               x.UnidadSaldo > 0)
                    .ToListAsync();

                foreach (var stock in stockEstanteria)
                {
                    // Solo incluir ubicaciones válidas para conteo
                    if (EsUbicacionValidaParaConteo(stock.Ubicacion))
                    {
                        string? descripcionArticulo = null;
                        if (!string.IsNullOrEmpty(stock.CodigoArticulo))
                        {
                            descripcionArticulo = await ObtenerDescripcionArticuloAsync(orden.CodigoEmpresa, stock.CodigoArticulo);
                        }
                        
                        lecturasGeneradas.Add(new LecturaConteo
                        {
                            OrdenId = orden.Id,
                            CodigoAlmacen = codigoAlmacen,
                            CodigoUbicacion = stock.Ubicacion,
                            CodigoArticulo = stock.CodigoArticulo,
                            DescripcionArticulo = descripcionArticulo,
                            LotePartida = stock.Partida,
                            CantidadContada = null,
                            CantidadStock = stock.UnidadSaldo,
                            UsuarioCodigo = orden.CodigoOperario ?? "",
                            Fecha = DateTime.UtcNow
                        });
                    }
                }
            }
        }

        private async Task GenerarLecturasPorPaletAsync(OrdenConteo orden, string codigoAlmacen, List<LecturaConteo> lecturasGeneradas)
        {
            // Obtener ejercicio actual
            var ejercicio = await _sageDbContext.Periodos
                .Where(p => p.CodigoEmpresa == orden.CodigoEmpresa && p.Fechainicio <= DateTime.Now)
                .OrderByDescending(p => p.Fechainicio)
                .Select(p => p.Ejercicio)
                .FirstOrDefaultAsync();

            if (ejercicio == 0) return;

            // Para palets, generar lecturas por ubicación con filtro de ubicaciones válidas
            var stockPorUbicacion = await _storageControlContext.AcumuladoStockUbicacion
                .Where(x => x.CodigoEmpresa == orden.CodigoEmpresa &&
                           x.Ejercicio == ejercicio &&
                           x.CodigoAlmacen == codigoAlmacen &&
                           x.UnidadSaldo > 0)
                .ToListAsync();

            foreach (var stock in stockPorUbicacion)
            {
                // Solo incluir ubicaciones válidas para conteo
                if (EsUbicacionValidaParaConteo(stock.Ubicacion))
                {
                    string? descripcionArticulo = null;
                    if (!string.IsNullOrEmpty(stock.CodigoArticulo))
                    {
                        descripcionArticulo = await ObtenerDescripcionArticuloAsync(orden.CodigoEmpresa, stock.CodigoArticulo);
                    }
                    
                    lecturasGeneradas.Add(new LecturaConteo
                    {
                        OrdenId = orden.Id,
                        CodigoAlmacen = codigoAlmacen,
                        CodigoUbicacion = stock.Ubicacion,
                        CodigoArticulo = stock.CodigoArticulo,
                        DescripcionArticulo = descripcionArticulo,
                        LotePartida = stock.Partida,
                        CantidadContada = null,
                        CantidadStock = stock.UnidadSaldo,
                        UsuarioCodigo = orden.CodigoOperario ?? "",
                        Fecha = DateTime.UtcNow
                    });
                }
            }
        }

        private bool EsUbicacionValidaParaConteo(string? ubicacion)
        {
            if (ubicacion == null)
                return false;

            // Ubicación vacía es válida para conteo (ubicación especial "SIN UBICAR")
            if (ubicacion == "")
                return true;

            // Ubicaciones normales UB con formato correcto (14 caracteres)
            if (ubicacion.StartsWith("UB") && ubicacion.Length == 14)
                return true;

            // Cualquier otra ubicación que no empiece con UB es una ubicación especial válida
            if (!ubicacion.StartsWith("UB"))
                return true;

            // Otras ubicaciones no válidas
            return false;
        }

        private async Task<string?> ObtenerDescripcionArticuloAsync(int codigoEmpresa, string codigoArticulo)
        {
            try
            {
                var articulo = await _sageDbContext.Articulos
                    .Where(x => x.CodigoEmpresa == codigoEmpresa && x.CodigoArticulo == codigoArticulo)
                    .FirstOrDefaultAsync();

                return articulo?.DescripcionArticulo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo descripción del artículo {CodigoArticulo}", codigoArticulo);
                return null;
            }
        }

        private static OrdenDto MapToOrdenDto(OrdenConteo orden)
        {
            return new OrdenDto
            {
                Id = orden.Id,
                CodigoEmpresa = orden.CodigoEmpresa,
                Titulo = orden.Titulo,
                Visibilidad = orden.Visibilidad,
                ModoGeneracion = orden.ModoGeneracion,
                Alcance = orden.Alcance,
                FiltrosJson = orden.FiltrosJson,
                FechaPlan = orden.FechaPlan,
                FechaEjecucion = orden.FechaEjecucion,
                SupervisorCodigo = orden.SupervisorCodigo,
                CreadoPorCodigo = orden.CreadoPorCodigo,
                Estado = orden.Estado,
                Prioridad = orden.Prioridad,
                FechaCreacion = orden.FechaCreacion,
                CodigoOperario = orden.CodigoOperario,
                CodigoAlmacen = orden.CodigoAlmacen,
                CodigoUbicacion = orden.CodigoUbicacion,
                CodigoArticulo = orden.CodigoArticulo,
                DescripcionArticulo = orden.DescripcionArticulo,
                LotePartida = orden.LotePartida,
                CantidadTeorica = orden.CantidadTeorica,
                Comentario = orden.Comentario,
                FechaAsignacion = orden.FechaAsignacion,
                FechaInicio = orden.FechaInicio,
                FechaCierre = orden.FechaCierre
            };
        }

        // Métodos auxiliares para extraer datos del JSON
        private string? ExtraerAlmacenDelFiltro(string? filtrosJson)
        {
            if (string.IsNullOrEmpty(filtrosJson)) return null;
            try
            {
                var filtros = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(filtrosJson);
                return filtros?.GetValueOrDefault("almacen")?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private string? ExtraerArticuloDelFiltro(string? filtrosJson)
        {
            if (string.IsNullOrEmpty(filtrosJson)) return null;
            try
            {
                var filtros = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(filtrosJson);
                return filtros?.GetValueOrDefault("articulo")?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private string? ExtraerPasilloDelFiltro(string? filtrosJson)
        {
            if (string.IsNullOrEmpty(filtrosJson)) return null;
            try
            {
                var filtros = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(filtrosJson);
                return filtros?.GetValueOrDefault("pasillo")?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private string? ExtraerEstanteriaDelFiltro(string? filtrosJson)
        {
            if (string.IsNullOrEmpty(filtrosJson)) return null;
            try
            {
                var filtros = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(filtrosJson);
                return filtros?.GetValueOrDefault("estanteria")?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private string? ExtraerAlturaDelFiltro(string? filtrosJson)
        {
            if (string.IsNullOrEmpty(filtrosJson)) return null;
            try
            {
                var filtros = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(filtrosJson);
                return filtros?.GetValueOrDefault("altura")?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private string? ExtraerPosicionDelFiltro(string? filtrosJson)
        {
            if (string.IsNullOrEmpty(filtrosJson)) return null;
            try
            {
                var filtros = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(filtrosJson);
                return filtros?.GetValueOrDefault("posicion")?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private string? ExtraerUbicacionDelFiltro(string? filtrosJson)
        {
            if (string.IsNullOrEmpty(filtrosJson)) return null;
            try
            {
                var filtros = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(filtrosJson);
                return filtros?.GetValueOrDefault("ubicacion")?.ToString();
            }
            catch
            {
                return null;
            }
        }

        // TODO: Implementar métodos restantes
        public async Task<LecturaResponseDto> CrearLecturaAsync(long ordenId, LecturaDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                var orden = await _context.OrdenesConteo
                    .FirstOrDefaultAsync(o => o.Id == ordenId);

                if (orden == null)
                {
                    throw new InvalidOperationException($"No se encontró la orden con ID {ordenId}");
                }

                // Verificar que la orden esté en proceso
                if (orden.Estado != "EN_PROCESO")
                {
                    throw new InvalidOperationException($"No se puede crear lecturas para una orden en estado {orden.Estado}");
                }

                // Buscar la lectura pendiente existente
                var lecturaExistente = await _context.LecturasConteo
                    .Where(l => l.OrdenId == ordenId && 
                                l.CodigoArticulo == dto.CodigoArticulo && 
                                l.LotePartida == dto.LotePartida && 
                                l.CodigoUbicacion == dto.CodigoUbicacion &&
                                l.CantidadContada == null)
                    .FirstOrDefaultAsync();

                if (lecturaExistente == null)
                {
                    throw new InvalidOperationException($"No se encontró una lectura pendiente para el artículo {dto.CodigoArticulo}, lote {dto.LotePartida} en ubicación {dto.CodigoUbicacion}");
                }

                // Actualizar la lectura existente
                lecturaExistente.CantidadContada = dto.CantidadContada;
                lecturaExistente.UsuarioCodigo = dto.UsuarioCodigo;
                lecturaExistente.Comentario = dto.Comentario;
                lecturaExistente.Fecha = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Lectura actualizada para orden {OrdenId} por usuario {Usuario} - Artículo: {Articulo}, Lote: {Lote}, Ubicación: {Ubicacion}, Cantidad: {Cantidad}", 
                    ordenId, dto.UsuarioCodigo, dto.CodigoArticulo, dto.LotePartida, dto.CodigoUbicacion, dto.CantidadContada);

                return MapToLecturaResponseDto(lecturaExistente);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<CerrarOrdenResponseDto> CerrarOrdenAsync(long id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                var orden = await _context.OrdenesConteo
                    .Include(o => o.Lecturas)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (orden == null)
                {
                    throw new InvalidOperationException($"No se encontró la orden con ID {id}");
                }

                // Verificar que la orden esté en proceso
                if (orden.Estado != "EN_PROCESO")
                {
                    throw new InvalidOperationException($"No se puede cerrar una orden en estado {orden.Estado}");
                }

                // Verificar que haya al menos una lectura
                if (!orden.Lecturas.Any())
                {
                    throw new InvalidOperationException("No se puede cerrar una orden sin lecturas");
                }

                // Verificar que NO hay lecturas pendientes (todas deben estar contadas)
                var lecturasPendientes = orden.Lecturas.Where(l => l.CantidadContada == null).ToList();
                if (lecturasPendientes.Any())
                {
                    var articulosPendientes = lecturasPendientes
                        .Select(l => $"{l.CodigoArticulo} (Lote: {l.LotePartida}, Ubicación: {l.CodigoUbicacion})")
                        .Take(5);
                    
                    var mensaje = $"No se puede cerrar la orden. Faltan {lecturasPendientes.Count} lecturas por realizar. Artículos pendientes: {string.Join(", ", articulosPendientes)}";
                    if (lecturasPendientes.Count > 5)
                    {
                        mensaje += $" y {lecturasPendientes.Count - 5} más...";
                    }
                    
                    throw new InvalidOperationException(mensaje);
                }

                // Obtener todas las lecturas completadas
                var lecturasCompletadas = orden.Lecturas.Where(l => l.CantidadContada.HasValue).ToList();
                
                // Crear registros en ResultadoConteo solo para las lecturas que requieren acción
                var resultadosCreados = 0;
                foreach (var lectura in lecturasCompletadas)
                {
                    // Calcular la diferencia entre cantidad contada y stock
                    var diferencia = lectura.CantidadContada.Value - (lectura.CantidadStock ?? 0);
                    
                    // Solo crear resultado si hay diferencia significativa (requiere acción)
                    if (Math.Abs(diferencia) >= 0.0001m)
                    {
                        // Verificar si ya existe un resultado para esta orden
                        var resultadoExistente = await _context.ResultadosConteo
                            .FirstOrDefaultAsync(r => r.OrdenId == id);

                        if (resultadoExistente == null)
                        {
                            var resultado = new ResultadoConteo
                            {
                                OrdenId = id,
                                CodigoAlmacen = lectura.CodigoAlmacen,
                                CodigoUbicacion = lectura.CodigoUbicacion,
                                CodigoArticulo = lectura.CodigoArticulo,
                                DescripcionArticulo = lectura.DescripcionArticulo,
                                LotePartida = lectura.LotePartida,
                                CantidadContada = lectura.CantidadContada,
                                CantidadStock = lectura.CantidadStock,
                                UsuarioCodigo = lectura.UsuarioCodigo,
                                Diferencia = diferencia,
                                AccionFinal = "SUPERVISION",
                                FechaEvaluacion = DateTime.UtcNow
                            };

                            _context.ResultadosConteo.Add(resultado);
                            resultadosCreados++;
                        }
                    }
                }

                // Actualizar la orden
                orden.Estado = "CERRADO";
                orden.FechaCierre = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Orden {Id} cerrada. Total lecturas: {TotalLecturas}, Resultados creados: {ResultadosCreados}", 
                    id, lecturasCompletadas.Count, resultadosCreados);

                return new CerrarOrdenResponseDto
                {
                    OrdenId = id,
                    TotalLecturas = lecturasCompletadas.Count,
                    ResultadosCreados = resultadosCreados,
                    FechaCierre = orden.FechaCierre.Value
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<IEnumerable<LecturaResponseDto>> ObtenerLecturasPendientesAsync(long ordenId, string? codigoOperario = null)
        {
            try
            {
                _logger.LogInformation("Buscando lecturas pendientes para orden {OrdenId} con operario {Operario}", ordenId, codigoOperario);
                
                var query = _context.LecturasConteo
                    .Where(l => l.OrdenId == ordenId && l.CantidadContada == null);

                if (!string.IsNullOrEmpty(codigoOperario))
                {
                    query = query.Where(l => l.UsuarioCodigo == codigoOperario);
                }

                var lecturas = await query
                    .OrderBy(l => l.CodigoUbicacion)
                    .ThenBy(l => l.CodigoArticulo)
                    .ToListAsync();

                _logger.LogInformation("Se encontraron {Count} lecturas pendientes", lecturas.Count);
                
                return lecturas.Select(MapToLecturaResponseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtenerLecturasPendientesAsync: {Message}", ex.Message);
                throw;
            }
        }

        private static LecturaResponseDto MapToLecturaResponseDto(LecturaConteo lectura)
        {
            return new LecturaResponseDto
            {
                Id = lectura.Id,
                OrdenId = lectura.OrdenId,
                CodigoAlmacen = lectura.CodigoAlmacen,
                CodigoUbicacion = lectura.CodigoUbicacion,
                CodigoArticulo = lectura.CodigoArticulo,
                DescripcionArticulo = lectura.DescripcionArticulo,
                LotePartida = lectura.LotePartida,
                CantidadContada = lectura.CantidadContada,
                CantidadStock = lectura.CantidadStock,
                UsuarioCodigo = lectura.UsuarioCodigo,
                Fecha = lectura.Fecha,
                Comentario = lectura.Comentario
            };
        }
    }
} 