using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Conteos;
using SGA_Api.Models.Inventario;
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
               
                if (!string.IsNullOrEmpty(codigoArticulo) &&
                    string.IsNullOrEmpty(codigoUbicacion) &&
                    string.IsNullOrEmpty(pasillo) &&
                    string.IsNullOrEmpty(estanteria))
                {
                    // Si solo se especifica artículo (sin ubicación, pasillo, estantería), es ARTICULO
                    alcanceDeterminado = "ARTICULO";
                }
                else if (!string.IsNullOrEmpty(codigoUbicacion) || codigoUbicacion == "")
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
                else if (!string.IsNullOrEmpty(pasillo) && !string.IsNullOrEmpty(estanteria) &&
                         !string.IsNullOrEmpty(altura))
                {
                    // Si hay pasillo, estantería y altura (sin posición), es ALTURA
                    alcanceDeterminado = "ALTURA";
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
                    FechaCreacion = DateTime.Now,
                    CodigoOperario = dto.CodigoOperario,
                    FechaAsignacion = !string.IsNullOrEmpty(dto.CodigoOperario) ? DateTime.Now : null,
                    CodigoAlmacen = codigoAlmacen,
                    CodigoUbicacion = codigoUbicacion,
                    CodigoArticulo = codigoArticulo
                };
 
                _context.OrdenesConteo.Add(orden);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Orden guardada con Guid: {Guid}", orden.GuidID);
 
                // NO generar lecturas automáticas - se generan dinámicamente cuando se solicitan
                _logger.LogInformation("Orden creada sin generar lecturas automáticas. Se generarán dinámicamente cuando se soliciten.");
 
                return MapToOrdenDto(orden);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CrearOrdenAsync: {Message}", ex.Message);
                throw;
            }
        }

        public async Task<OrdenDto> ActualizarOrdenAsync(Guid guid, CrearOrdenConteoDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _logger.LogInformation("Iniciando actualización de orden de conteo: {Guid}", guid);

                var orden = await _context.OrdenesConteo.FirstOrDefaultAsync(o => o.GuidID == guid);
                if (orden == null)
                    throw new InvalidOperationException($"No se encontró la orden con Guid {guid}");

                // Solo se puede editar si está en estado PLANIFICADO o ASIGNADO
                if (orden.Estado != "PLANIFICADO" && orden.Estado != "ASIGNADO")
                    throw new InvalidOperationException($"No se puede editar una orden en estado {orden.Estado}");

                // Actualizar campos básicos
                orden.Titulo = dto.Titulo;
                orden.Prioridad = dto.Prioridad;
                orden.FechaPlan = dto.FechaPlan;
                orden.Comentario = dto.Comentario;

                // Actualizar filtros
                orden.FiltrosJson = dto.FiltrosJson;

                // Extraer valores del FiltrosJson para actualizar campos específicos
                var codigoAlmacen = ExtraerAlmacenDelFiltro(dto.FiltrosJson);
                var codigoArticulo = ExtraerArticuloDelFiltro(dto.FiltrosJson);
                var codigoUbicacion = ExtraerUbicacionDelFiltro(dto.FiltrosJson);

                // Actualizar campos específicos
                orden.CodigoAlmacen = codigoAlmacen;
                orden.CodigoArticulo = codigoArticulo;
                orden.CodigoUbicacion = codigoUbicacion;

                // Determinar el alcance automáticamente
                var pasillo = ExtraerPasilloDelFiltro(dto.FiltrosJson);
                var estanteria = ExtraerEstanteriaDelFiltro(dto.FiltrosJson);
                var altura = ExtraerAlturaDelFiltro(dto.FiltrosJson);
                var posicion = ExtraerPosicionDelFiltro(dto.FiltrosJson);

                // Determinar el alcance automáticamente según los componentes disponibles
                // IGNORAR el alcance enviado por el cliente y determinarlo automáticamente
                string alcanceDeterminado = "ALMACEN"; // Default
               
                if (!string.IsNullOrEmpty(codigoArticulo) &&
                    string.IsNullOrEmpty(codigoUbicacion) &&
                    string.IsNullOrEmpty(pasillo) &&
                    string.IsNullOrEmpty(estanteria))
                {
                    // Si solo se especifica artículo (sin ubicación, pasillo, estantería), es ARTICULO
                    alcanceDeterminado = "ARTICULO";
                }
                else if (!string.IsNullOrEmpty(codigoUbicacion) || codigoUbicacion == "")
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
                else if (!string.IsNullOrEmpty(pasillo) && !string.IsNullOrEmpty(estanteria) &&
                         !string.IsNullOrEmpty(altura))
                {
                    // Si hay pasillo, estantería y altura (sin posición), es ALTURA
                    alcanceDeterminado = "ALTURA";
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

                _logger.LogInformation("ALCANCE_AUTO_UPDATE: Alcance determinado automáticamente: '{AlcanceDeterminado}' para filtros: almacen='{Almacen}', pasillo='{Pasillo}', estanteria='{Estanteria}', altura='{Altura}', posicion='{Posicion}', ubicacion='{Ubicacion}'",
                     alcanceDeterminado, codigoAlmacen, pasillo, estanteria, altura, posicion, codigoUbicacion);

                orden.Alcance = alcanceDeterminado;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Orden {Guid} actualizada correctamente", guid);
                return MapToOrdenDto(orden);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error en ActualizarOrdenAsync: {Message}", ex.Message);
                throw;
            }
        }

        public async Task<OrdenDto?> ObtenerOrdenAsync(Guid guid)
        {
            var orden = await _context.OrdenesConteo
                .Include(o => o.Lecturas)
                .Include(o => o.Resultados)
                .FirstOrDefaultAsync(o => o.GuidID == guid);

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

        /// <summary>
        /// Listar todas las órdenes de conteo sin restricciones de usuario (para Desktop)
        /// </summary>
        public async Task<IEnumerable<OrdenDto>> ListarTodasLasOrdenesAsync(string? estado = null, string? codigoOperario = null)
        {
            try
            {
                _logger.LogInformation("Iniciando ListarTodasLasOrdenesAsync con estado: {Estado}, operario: {Operario}", estado, codigoOperario);
                
                var query = _context.OrdenesConteo.AsQueryable();

                // Aplicar filtro de estado si se especifica
                if (!string.IsNullOrEmpty(estado))
                {
                    query = query.Where(o => o.Estado == estado);
                }

                // Aplicar filtro de operario si se especifica
                if (!string.IsNullOrEmpty(codigoOperario))
                {
                    query = query.Where(o => o.CodigoOperario == codigoOperario);
                }

                var ordenes = await query
                    .OrderByDescending(o => o.FechaCreacion)
                    .ToListAsync();

                _logger.LogInformation("Se encontraron {Count} órdenes de conteo (todas)", ordenes.Count);
                return ordenes.Select(MapToOrdenDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ListarTodasLasOrdenesAsync: {Message}", ex.Message);
                throw;
            }
        }

        public async Task<OrdenDto> IniciarOrdenAsync(Guid guid, string codigoOperario)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                var orden = await _context.OrdenesConteo
                    .FirstOrDefaultAsync(o => o.GuidID == guid);

                if (orden == null)
                {
                    throw new InvalidOperationException($"No se encontró la orden con Guid {guid}");
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
                orden.FechaInicio = DateTime.Now;
                orden.FechaAsignacion = orden.FechaAsignacion ?? DateTime.Now;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Orden {Guid} iniciada por operario {Operario}", guid, codigoOperario);
                return MapToOrdenDto(orden);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<OrdenDto> AsignarOperarioAsync(Guid guid, AsignarOperarioDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                var orden = await _context.OrdenesConteo
                    .FirstOrDefaultAsync(o => o.GuidID == guid);

                if (orden == null)
                {
                    throw new InvalidOperationException($"No se encontró la orden con Guid {guid}");
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
                orden.FechaAsignacion = DateTime.Now;
                
                if (!string.IsNullOrEmpty(dto.Comentario))
                {
                    orden.Comentario = dto.Comentario;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Orden {Guid} asignada al operario {Operario}", guid, dto.CodigoOperario);
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
                    _logger.LogWarning("No se pudo determinar el almacén para la orden {Guid}", orden.GuidID);
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
                    _logger.LogInformation("Generadas {Count} lecturas automáticas para orden {Guid}", 
                        lecturasGeneradas.Count, orden.GuidID);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generando lecturas automáticas para orden {Guid}", orden.GuidID);
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
                // Para conteos por artículo, NO filtrar por almacén - buscar en TODOS los almacenes
                var stockArticulo = await _storageControlContext.AcumuladoStockUbicacion
                    .Where(x => x.CodigoEmpresa == orden.CodigoEmpresa &&
                               x.Ejercicio == ejercicio &&
                               x.CodigoArticulo == codigoArticulo &&
                               x.UnidadSaldo > 0)
                    .ToListAsync();

                _logger.LogInformation("ARTICULO_DEBUG: Encontrados {Count} registros de stock para artículo {CodigoArticulo} en todos los almacenes", 
                    stockArticulo.Count, codigoArticulo);

                var descripcionArticulo = await ObtenerDescripcionArticuloAsync(orden.CodigoEmpresa, codigoArticulo);

                foreach (var stock in stockArticulo)
                {
                    // Solo incluir ubicaciones válidas para conteo
                    if (EsUbicacionValidaParaConteo(stock.Ubicacion))
                    {
                        lecturasGeneradas.Add(new LecturaConteo
                        {
                            OrdenGuid = orden.GuidID,
                            CodigoAlmacen = stock.CodigoAlmacen, // Usar el almacén real del stock
                            CodigoUbicacion = stock.Ubicacion,
                            CodigoArticulo = stock.CodigoArticulo,
                            DescripcionArticulo = descripcionArticulo,
                            LotePartida = stock.Partida,
                            CantidadContada = null,
                            CantidadStock = stock.UnidadSaldo,
                            UsuarioCodigo = orden.CodigoOperario ?? "",
                            Fecha = DateTime.Now,
                            FechaCaducidad = stock.FechaCaducidad
                        });
                        
                        _logger.LogInformation("ARTICULO_DEBUG: Lectura generada para artículo {CodigoArticulo} en almacén {CodigoAlmacen}, ubicación '{Ubicacion}'", 
                            stock.CodigoArticulo, stock.CodigoAlmacen, stock.Ubicacion);
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
                        OrdenGuid = orden.GuidID,
                        CodigoAlmacen = codigoAlmacen,
                        CodigoUbicacion = stock.Ubicacion,
                        CodigoArticulo = stock.CodigoArticulo,
                        DescripcionArticulo = descripcionArticulo,
                        LotePartida = stock.Partida,
                        CantidadContada = null,
                        CantidadStock = stock.UnidadSaldo,
                        UsuarioCodigo = orden.CodigoOperario ?? "",
                        Fecha = DateTime.Now,
                        FechaCaducidad = stock.FechaCaducidad
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
                            OrdenGuid = orden.GuidID,
                            CodigoAlmacen = codigoAlmacen,
                            CodigoUbicacion = stock.Ubicacion,
                            CodigoArticulo = stock.CodigoArticulo,
                            DescripcionArticulo = descripcionArticulo,
                            LotePartida = stock.Partida,
                            CantidadContada = null,
                            CantidadStock = stock.UnidadSaldo,
                            UsuarioCodigo = orden.CodigoOperario ?? "",
                            Fecha = DateTime.Now
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

            // Para alcance UBICACION, usar directamente la ubicación que ya se construyó en la orden
            string? ubicacionEspecifica = orden.Alcance == "UBICACION" ? orden.CodigoUbicacion : null;

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
                        OrdenGuid = orden.GuidID,
                        CodigoAlmacen = codigoAlmacen,
                        CodigoUbicacion = stock.Ubicacion,
                        CodigoArticulo = stock.CodigoArticulo,
                        DescripcionArticulo = descripcionArticulo,
                        LotePartida = stock.Partida,
                        CantidadContada = null,
                        CantidadStock = stock.UnidadSaldo,
                        UsuarioCodigo = orden.CodigoOperario ?? "",
                        Fecha = DateTime.Now,
                        FechaCaducidad = stock.FechaCaducidad
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
                            OrdenGuid = orden.GuidID,
                            CodigoAlmacen = codigoAlmacen,
                            CodigoUbicacion = stock.Ubicacion,
                            CodigoArticulo = stock.CodigoArticulo,
                            DescripcionArticulo = descripcionArticulo,
                            LotePartida = stock.Partida,
                            CantidadContada = null,
                            CantidadStock = stock.UnidadSaldo,
                            UsuarioCodigo = orden.CodigoOperario ?? "",
                            Fecha = DateTime.Now
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
                        OrdenGuid = orden.GuidID,
                        CodigoAlmacen = codigoAlmacen,
                        CodigoUbicacion = stock.Ubicacion,
                        CodigoArticulo = stock.CodigoArticulo,
                        DescripcionArticulo = descripcionArticulo,
                        LotePartida = stock.Partida,
                        CantidadContada = null,
                        CantidadStock = stock.UnidadSaldo,
                        UsuarioCodigo = orden.CodigoOperario ?? "",
                        Fecha = DateTime.Now,
                        FechaCaducidad = stock.FechaCaducidad
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
                GuidID = orden.GuidID,
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
        public async Task<LecturaResponseDto> CrearLecturaAsync(Guid ordenGuid, LecturaDto dto)
        {
            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var orden = await _context.OrdenesConteo.FirstOrDefaultAsync(o => o.GuidID == ordenGuid);
                if (orden is null)
                    throw new InvalidOperationException($"No se encontró la orden con Guid {ordenGuid}");
                if (orden.Estado != "EN_PROCESO")
                    throw new InvalidOperationException($"No se puede crear lecturas para una orden en estado {orden.Estado}");

                // Obtener el almacén de la orden (la ubicación viene del frontend)
                var almacenOrden = orden.CodigoAlmacen ?? ExtraerAlmacenDelFiltro(orden.FiltrosJson);

                // Obtener el stock actual del artículo
                var ejercicio = await _sageDbContext.Periodos
                    .Where(p => p.CodigoEmpresa == orden.CodigoEmpresa && p.Fechainicio <= DateTime.Now)
                    .OrderByDescending(p => p.Fechainicio)
                    .Select(p => p.Ejercicio)
                    .FirstOrDefaultAsync();

                if (ejercicio == 0)
                    throw new InvalidOperationException("No se encontró ejercicio válido");

                var stockActual = await _storageControlContext.AcumuladoStockUbicacion
                    .Where(x => x.CodigoEmpresa == orden.CodigoEmpresa &&
                               x.Ejercicio == ejercicio &&
                               x.CodigoAlmacen == almacenOrden &&
                               x.Ubicacion == dto.CodigoUbicacion &&
                               x.CodigoArticulo == dto.CodigoArticulo &&
                               (string.IsNullOrEmpty(dto.LotePartida) || x.Partida == dto.LotePartida))
                    .Select(x => x.UnidadSaldo ?? 0m)
                    .FirstOrDefaultAsync();

                // Operario (para límites)
                var operarioCodigo = !string.IsNullOrEmpty(orden.CodigoOperario) ? orden.CodigoOperario : dto.UsuarioCodigo;
                var operario = await _sageDbContext.Operarios.AsNoTracking().FirstOrDefaultAsync(o => o.Id.ToString() == operarioCodigo);
                var limUnidades = operario?.MRH_LimiteInventarioUnidades ?? 0m;
                var limEuros    = operario?.MRH_LimiteInventarioEuros    ?? 0m;

                // Descripción del artículo (siempre obtenerla del servicio)
                var descripcionArticulo = !string.IsNullOrEmpty(dto.CodigoArticulo)
                    ? await ObtenerDescripcionArticuloAsync(orden.CodigoEmpresa, dto.CodigoArticulo)
                    : "";

                // Crear SIEMPRE una lectura nueva (no actualizar "pendientes")
                var lectura = new LecturaConteo
                {
                    OrdenGuid = orden.GuidID,
                    CodigoAlmacen = almacenOrden,
                    CodigoUbicacion = dto.CodigoUbicacion,
                    CodigoArticulo = dto.CodigoArticulo,
                    DescripcionArticulo = descripcionArticulo,
                    LotePartida = dto.LotePartida,
                    CantidadContada = dto.CantidadContada,
                    CantidadStock = stockActual,
                    UsuarioCodigo = dto.UsuarioCodigo,
                    Comentario = dto.Comentario,
                    Fecha = DateTime.Now,
                    FechaCaducidad = dto.FechaCaducidad
                };
                _context.LecturasConteo.Add(lectura);
                await _context.SaveChangesAsync();

                // Diferencia y acción
                var diferencia = (dto.CantidadContada ?? 0m) - stockActual;
                if (Math.Abs(diferencia) >= 0.0001m)
                {
                    // Calcular acción considerando límites por unidades y por euros (precio medio)
                    var diferenciaAbs = Math.Abs(diferencia);
                    decimal? precioMedio = null;
                    try
                    {
                        precioMedio = await _sageDbContext.AcumuladoStock
                            .Where(a => a.CodigoEmpresa == orden.CodigoEmpresa
                                    && a.Ejercicio == ejercicio
                                    && a.CodigoArticulo == dto.CodigoArticulo)
                            .Select(a => a.PrecioMedio)
                            .FirstOrDefaultAsync();
                    }
                    catch { /* si falla el precio, tratamos como 0 */ }

                    var superaUnidades = diferenciaAbs > limUnidades;
                    var superaEuros = false;
                    if (limEuros > 0m && precioMedio.HasValue)
                    {
                        superaEuros = diferenciaAbs * precioMedio.Value > limEuros;
                    }

                    var accion = (superaUnidades || superaEuros) ? "SUPERVISION" : "AJUSTE";

                    // Crear un nuevo ResultadoConteo para cada lectura
                    var resultado = new ResultadoConteo
                        {
                            OrdenGuid = orden.GuidID,
                            CodigoAlmacen = lectura.CodigoAlmacen,
                            CodigoUbicacion = lectura.CodigoUbicacion,
                            CodigoArticulo = lectura.CodigoArticulo,
                            DescripcionArticulo = lectura.DescripcionArticulo,
                            LotePartida = lectura.LotePartida,
                            CantidadContada = lectura.CantidadContada,
                            CantidadStock = lectura.CantidadStock,
                            UsuarioCodigo = lectura.UsuarioCodigo,
                            Diferencia = diferencia,
                            AccionFinal = accion,
                            FechaEvaluacion = DateTime.Now,
                            AjusteAplicado = false,
                            FechaCaducidad = lectura.FechaCaducidad
                        };
                        _context.ResultadosConteo.Add(resultado);

                    await _context.SaveChangesAsync();

                    // Si la acción es AJUSTE, crear registro en InventarioAjustes
                    if (accion == "AJUSTE")
                    {
                        var inventarioAjuste = new InventarioAjustes
                        {
                            IdInventario = null, // Para ajustes de conteo no necesitamos InventarioCabecera
                            CodigoArticulo = resultado.CodigoArticulo,
                            CodigoUbicacion = resultado.CodigoUbicacion,
                            Diferencia = resultado.Diferencia,
                            UsuarioId = int.Parse(resultado.UsuarioCodigo), // Convertir string a int
                            Fecha = DateTime.Now,
                            IdConteo = resultado.OrdenGuid,
                            CodigoEmpresa = (short)orden.CodigoEmpresa, // Convertir int a short
                            CodigoAlmacen = resultado.CodigoAlmacen,
                            Estado = "PENDIENTE_ERP",
                            FechaCaducidad = resultado.FechaCaducidad,
                            Partida = resultado.LotePartida
                        };

                        _context.InventarioAjustes.Add(inventarioAjuste);
                        await _context.SaveChangesAsync();
                        
                        _logger.LogInformation("InventarioAjustes creado para resultado {ResultadoGuid} con diferencia {Diferencia}", resultado.GuidID, diferencia);
                    }
                }

                // TEMPORAL: Comentar verificación de lecturas pendientes para debug
                
                var lecturasPendientes = await ObtenerLecturasPendientesAsync(orden.GuidID, dto.UsuarioCodigo);
                
                if (!lecturasPendientes.Any())
                {
                    // No quedan lecturas pendientes, cerrar la orden automáticamente
                    orden.Estado = "CERRADO";
                    orden.FechaCierre = DateTime.Now;
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Orden {OrdenGuid} cerrada automáticamente al completar todas las lecturas", orden.GuidID);
                }
                

                await tx.CommitAsync();
                return MapToLecturaResponseDto(lectura);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task<CerrarOrdenResponseDto> CerrarOrdenAsync(Guid guid)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                var orden = await _context.OrdenesConteo
                    .Include(o => o.Lecturas)
                    .FirstOrDefaultAsync(o => o.GuidID == guid);

                if (orden == null)
                {
                    throw new InvalidOperationException($"No se encontró la orden con Guid {guid}");
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
                
                // Contar los resultados ya creados durante las lecturas
                var resultadosCreados = await _context.ResultadosConteo
                    .Where(r => r.OrdenGuid == orden.GuidID)
                    .CountAsync();

                // Actualizar la orden
                orden.Estado = "CERRADO";
                orden.FechaCierre = DateTime.Now;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Orden {Guid} cerrada. Total lecturas: {TotalLecturas}, Resultados creados: {ResultadosCreados}", 
                    guid, lecturasCompletadas.Count, resultadosCreados);

                return new CerrarOrdenResponseDto
                {
                    OrdenGuid = orden.GuidID,
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

        public async Task<ResultadoConteoDetalladoDto> ActualizarAprobadorAsync(Guid resultadoGuid, ActualizarAprobadorDto dto)
        {
            try
            {
                _logger.LogInformation("Actualizando aprobador para resultado {ResultadoGuid} con operario {Aprobador}", resultadoGuid, dto.AprobadoPorCodigo);
                
                // Buscar el ResultadoConteo por GuidID
                var resultado = await _context.ResultadosConteo
                    .Include(r => r.Orden)
                    .FirstOrDefaultAsync(r => r.GuidID == resultadoGuid);

                if (resultado == null)
                {
                    throw new InvalidOperationException($"No se encontró el resultado de conteo con Guid {resultadoGuid}");
                }

                // Verificar que la acción sea SUPERVISION
                if (resultado.AccionFinal != "SUPERVISION")
                {
                    throw new InvalidOperationException($"Solo se puede actualizar el aprobador para resultados con AccionFinal = SUPERVISION. El resultado actual tiene AccionFinal = {resultado.AccionFinal}");
                }

                // Verificar que el resultado no tenga ya un aprobador asignado
                if (!string.IsNullOrEmpty(resultado.AprobadoPorCodigo))
                {
                    throw new InvalidOperationException($"El resultado de conteo ya tiene un aprobador asignado: {resultado.AprobadoPorCodigo}");
                }

                // Actualizar el campo AprobadoPorCodigo
                resultado.AprobadoPorCodigo = dto.AprobadoPorCodigo;
                
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Aprobador actualizado correctamente para resultado {ResultadoGuid}", resultadoGuid);
                
                return MapToResultadoConteoDetalladoDto(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ActualizarAprobadorAsync: {Message}", ex.Message);
                throw;
            }
        }

        public async Task<IEnumerable<ResultadoConteoDetalladoDto>> ObtenerResultadosConteoAsync(string? accion = null)
        {
            try
            {
                _logger.LogInformation("Obteniendo resultados de conteo con filtro de acción: {Accion}", accion ?? "TODOS");
                
                var query = _context.ResultadosConteo
                    .Include(r => r.Orden)
                    .AsQueryable();

                // Aplicar filtro por acción si se especifica
                if (!string.IsNullOrEmpty(accion))
                {
                    query = query.Where(r => r.AccionFinal == accion);
                }

                var resultados = await query
                    .OrderByDescending(r => r.FechaEvaluacion)
                    .ToListAsync();

                _logger.LogInformation("Se encontraron {Count} resultados de conteo", resultados.Count);
                
                return resultados.Select(MapToResultadoConteoDetalladoDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtenerResultadosConteoAsync: {Message}", ex.Message);
                throw;
            }
        }

        public async Task<IEnumerable<LecturaResponseDto>> ObtenerLecturasPendientesAsync(Guid ordenGuid, string? codigoOperario = null)
        {
            try
            {
                _logger.LogInformation("Generando lecturas pendientes dinámicamente para orden {OrdenGuid} con operario {Operario}", ordenGuid, codigoOperario);
                
                // Obtener la orden
                var orden = await _context.OrdenesConteo
                    .FirstOrDefaultAsync(o => o.GuidID == ordenGuid);
                
                if (orden == null)
                    throw new InvalidOperationException($"No se encontró la orden con Guid {ordenGuid}");

                if (orden.Estado != "EN_PROCESO" && orden.Estado != "CERRADO")
                    throw new InvalidOperationException($"No se pueden obtener lecturas para una orden en estado {orden.Estado}");

                // Si la orden está cerrada, no hay lecturas pendientes
                if (orden.Estado == "CERRADO")
                {
                    _logger.LogInformation("Orden {OrdenGuid} está cerrada, no hay lecturas pendientes", ordenGuid);
                    return new List<LecturaResponseDto>();
                }

                // Obtener ejercicio actual
                var ejercicio = await _sageDbContext.Periodos
                    .Where(p => p.CodigoEmpresa == orden.CodigoEmpresa && p.Fechainicio <= DateTime.Now)
                    .OrderByDescending(p => p.Fechainicio)
                    .Select(p => p.Ejercicio)
                    .FirstOrDefaultAsync();

                if (ejercicio == 0)
                    throw new InvalidOperationException("No se encontró ejercicio válido");

                // Generar lecturas dinámicamente según el alcance
                var lecturasGeneradas = new List<LecturaResponseDto>();
                var codigoAlmacen = orden.CodigoAlmacen;
                
                if (string.IsNullOrEmpty(codigoAlmacen))
                {
                    codigoAlmacen = ExtraerAlmacenDelFiltro(orden.FiltrosJson);
                }

                // Obtener almacenes autorizados para el operario
                List<string> almacenesAutorizados = new List<string>();
                if (!string.IsNullOrEmpty(codigoOperario) && int.TryParse(codigoOperario, out int operarioId))
                {
                    almacenesAutorizados = await ObtenerAlmacenesAutorizadosAsync(operarioId, orden.CodigoEmpresa);
                    
                    // Si el operario no tiene almacenes autorizados, no mostrar lecturas
                    if (!almacenesAutorizados.Any())
                    {
                        _logger.LogWarning("Operario {Operario} no tiene almacenes autorizados", codigoOperario);
                        return new List<LecturaResponseDto>();
                    }
                }

                // Construir query base según alcance
                var query = _storageControlContext.AcumuladoStockUbicacion
                    .Where(x => x.CodigoEmpresa == orden.CodigoEmpresa &&
                               x.Ejercicio == ejercicio &&
                               x.UnidadSaldo > 0);

                // Aplicar filtros según alcance
                switch (orden.Alcance?.ToUpper())
                {
                    case "ARTICULO":
                        var codigoArticulo = orden.CodigoArticulo ?? ExtraerArticuloDelFiltro(orden.FiltrosJson);
                        if (!string.IsNullOrEmpty(codigoArticulo))
                        {
                            query = query.Where(x => x.CodigoArticulo == codigoArticulo);
                            
                            // Si se especifica almacén específico
                            if (!string.IsNullOrEmpty(codigoAlmacen))
                            {
                                // Verificar que el operario tenga acceso al almacén específico
                                if (!almacenesAutorizados.Contains(codigoAlmacen))
                                {
                                    _logger.LogWarning("Operario {Operario} no tiene acceso al almacén {Almacen}", codigoOperario, codigoAlmacen);
                                    return new List<LecturaResponseDto>();
                                }
                                
                                query = query.Where(x => x.CodigoAlmacen == codigoAlmacen);
                                _logger.LogInformation("Pendientes (ARTICULO): filtrando artículo '{Articulo}' en almacén '{Almacen}'", codigoArticulo, codigoAlmacen);
                            }
                            else
                            {
                                // Si no se especifica almacén, filtrar por almacenes autorizados
                                if (almacenesAutorizados.Any())
                                {
                                    query = query.Where(x => almacenesAutorizados.Contains(x.CodigoAlmacen));
                                    _logger.LogInformation("Pendientes (ARTICULO): filtrando artículo '{Articulo}' en almacenes autorizados: {Almacenes}", 
                                        codigoArticulo, string.Join(", ", almacenesAutorizados));
                                }
                                else
                                {
                                    _logger.LogInformation("Pendientes (ARTICULO): filtrando artículo '{Articulo}' en TODOS los almacenes (sin restricciones)", codigoArticulo);
                                }
                            }
                        }
                        break;
                    case "UBICACION":
                    {
                        // Para ubicación, siempre necesitamos un almacén específico
                        if (string.IsNullOrEmpty(codigoAlmacen))
                            throw new InvalidOperationException("Para alcance UBICACION se requiere especificar un almacén");

                        // Verificar que el operario tenga acceso al almacén
                        if (!almacenesAutorizados.Contains(codigoAlmacen))
                        {
                            _logger.LogWarning("Operario {Operario} no tiene acceso al almacén {Almacen} para ubicación específica", codigoOperario, codigoAlmacen);
                            return new List<LecturaResponseDto>();
                        }

                        query = query.Where(x => x.CodigoAlmacen == codigoAlmacen);
                        
                        // 1) Prioriza la ubicación guardada en la orden (incluye "" como válida)
                        string? ubicacion = null;
                        if (orden.CodigoUbicacion != null || orden.CodigoUbicacion == "")
                        {
                            ubicacion = orden.CodigoUbicacion;
                        }
                        else
                        {
                            // 2) Intenta extraer "ubicacion" directa del filtro
                            ubicacion = ExtraerUbicacionDelFiltro(orden.FiltrosJson);

                            // 3) Si no hay, reconstruye desde pasillo/estanteria/altura/posicion
                            if (ubicacion == null)
                            {
                                var ubicacionPasillo    = ExtraerPasilloDelFiltro(orden.FiltrosJson);
                                var ubicacionEstanteria = ExtraerEstanteriaDelFiltro(orden.FiltrosJson);
                                var altura     = ExtraerAlturaDelFiltro(orden.FiltrosJson);
                                var posicion   = ExtraerPosicionDelFiltro(orden.FiltrosJson);

                                if (!string.IsNullOrEmpty(ubicacionPasillo) &&
                                    !string.IsNullOrEmpty(ubicacionEstanteria) &&
                                    !string.IsNullOrEmpty(altura) &&
                                    !string.IsNullOrEmpty(posicion))
                                {
                                    ubicacion = $"UB{ubicacionPasillo.PadLeft(3,'0')}{ubicacionEstanteria.PadLeft(3,'0')}{altura.PadLeft(3,'0')}{posicion.PadLeft(3,'0')}";
                                }
                            }
                        }

                        if (ubicacion != null)
                        {
                            query = query.Where(x => x.Ubicacion == ubicacion);
                            _logger.LogInformation("Pendientes (UBICACION): filtrando por almacén '{CodigoAlmacen}' y ubicación '{Ubicacion}'", codigoAlmacen, ubicacion);
                        }
                        else
                        {
                            _logger.LogWarning("Pendientes (UBICACION): no se pudo resolver la ubicación; el resultado podría ser muy grande.");
                            // Si quieres devolver vacío en este caso, descomenta la siguiente línea y ajusta el tipo de retorno:
                            // return new List<LecturaResponseDto>();
                        }
                        break;
                    }
                    case "ESTANTERIA":
                        if (string.IsNullOrEmpty(codigoAlmacen))
                            throw new InvalidOperationException("Para alcance ESTANTERIA se requiere especificar un almacén");

                        // Verificar que el operario tenga acceso al almacén
                        if (!almacenesAutorizados.Contains(codigoAlmacen))
                        {
                            _logger.LogWarning("Operario {Operario} no tiene acceso al almacén {Almacen} para estantería", codigoOperario, codigoAlmacen);
                            return new List<LecturaResponseDto>();
                        }

                        query = query.Where(x => x.CodigoAlmacen == codigoAlmacen);
                        
                        var pasillo = ExtraerPasilloDelFiltro(orden.FiltrosJson);
                        var estanteria = ExtraerEstanteriaDelFiltro(orden.FiltrosJson);
                        if (!string.IsNullOrEmpty(pasillo) && !string.IsNullOrEmpty(estanteria))
                        {
                            var pasilloFormateado = pasillo.PadLeft(3, '0');
                            var estanteriaFormateada = estanteria.PadLeft(3, '0');
                            var prefijoEstanteria = $"UB{pasilloFormateado}{estanteriaFormateada}";
                            query = query.Where(x => x.Ubicacion != null && x.Ubicacion.StartsWith(prefijoEstanteria));
                        }
                        break;
                    case "PASILLO":
                        if (string.IsNullOrEmpty(codigoAlmacen))
                            throw new InvalidOperationException("Para alcance PASILLO se requiere especificar un almacén");

                        // Verificar que el operario tenga acceso al almacén
                        if (!almacenesAutorizados.Contains(codigoAlmacen))
                        {
                            _logger.LogWarning("Operario {Operario} no tiene acceso al almacén {Almacen} para pasillo", codigoOperario, codigoAlmacen);
                            return new List<LecturaResponseDto>();
                        }

                        query = query.Where(x => x.CodigoAlmacen == codigoAlmacen);
                        
                        var pasilloFiltro = ExtraerPasilloDelFiltro(orden.FiltrosJson);
                        if (!string.IsNullOrEmpty(pasilloFiltro))
                        {
                            var prefijoPasillo = $"UB{pasilloFiltro.PadLeft(3, '0')}";
                            query = query.Where(x => x.Ubicacion != null && x.Ubicacion.StartsWith(prefijoPasillo));
                        }
                        break;
                    case "ALMACEN":
                    case "PALET":
                    default:
                        if (string.IsNullOrEmpty(codigoAlmacen))
                            throw new InvalidOperationException("Para alcance ALMACEN se requiere especificar un almacén");

                        // Verificar que el operario tenga acceso al almacén
                        if (!almacenesAutorizados.Contains(codigoAlmacen))
                        {
                            _logger.LogWarning("Operario {Operario} no tiene acceso al almacén {Almacen}", codigoOperario, codigoAlmacen);
                            return new List<LecturaResponseDto>();
                        }

                        query = query.Where(x => x.CodigoAlmacen == codigoAlmacen);
                        _logger.LogInformation("Pendientes (ALMACEN): filtrando almacén '{Almacen}'", codigoAlmacen);
                        break;
                }

                // Obtener stock y generar lecturas
                var stockData = await query.ToListAsync();
                
                // Obtener lecturas ya creadas para excluirlas
                var lecturasCreadas = await _context.LecturasConteo
                    .Where(l => l.OrdenGuid == orden.GuidID)
                    .Select(l => new { l.CodigoAlmacen, l.CodigoUbicacion, l.CodigoArticulo, l.LotePartida })
                    .ToListAsync();

                foreach (var stock in stockData)
                {
                    if (EsUbicacionValidaParaConteo(stock.Ubicacion))
                    {
                        // Verificar si ya existe una lectura para esta combinación
                        var yaExiste = lecturasCreadas.Any(l => 
                            l.CodigoAlmacen == stock.CodigoAlmacen &&
                            l.CodigoUbicacion == stock.Ubicacion &&
                            l.CodigoArticulo == stock.CodigoArticulo &&
                            (string.IsNullOrEmpty(stock.Partida) || l.LotePartida == stock.Partida));
                        
                        if (!yaExiste)
                        {
                            var descripcionArticulo = await ObtenerDescripcionArticuloAsync(orden.CodigoEmpresa, stock.CodigoArticulo);
                            
                            lecturasGeneradas.Add(new LecturaResponseDto
                            {
                                GuidID = Guid.Empty, // No se persiste, es dinámico
                                OrdenGuid = orden.GuidID,
                                CodigoAlmacen = stock.CodigoAlmacen, // Usar el almacén real del stock
                                CodigoUbicacion = stock.Ubicacion,
                                CodigoArticulo = stock.CodigoArticulo,
                                DescripcionArticulo = descripcionArticulo,
                                LotePartida = stock.Partida,
                                CantidadContada = null, // Pendiente de conteo
                                CantidadStock = stock.UnidadSaldo,
                                UsuarioCodigo = codigoOperario ?? "",
                                Fecha = DateTime.Now,
                                Comentario = null
                            });
                        }
                    }
                }

                _logger.LogInformation("Generadas {Count} lecturas dinámicas para orden {OrdenGuid}", lecturasGeneradas.Count, ordenGuid);
                return lecturasGeneradas;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generando lecturas dinámicas para orden {OrdenGuid}: {Message}", ordenGuid, ex.Message);
                throw;
            }
        }

        private static LecturaResponseDto MapToLecturaResponseDto(LecturaConteo lectura)
        {
            return new LecturaResponseDto
            {
                GuidID = lectura.GuidID,
                OrdenGuid = lectura.OrdenGuid,
                CodigoAlmacen = lectura.CodigoAlmacen,
                CodigoUbicacion = lectura.CodigoUbicacion,
                CodigoArticulo = lectura.CodigoArticulo,
                DescripcionArticulo = lectura.DescripcionArticulo,
                LotePartida = lectura.LotePartida,
                CantidadContada = lectura.CantidadContada,
                CantidadStock = lectura.CantidadStock,
                UsuarioCodigo = lectura.UsuarioCodigo,
                Fecha = lectura.Fecha,
                Comentario = lectura.Comentario,
                FechaCaducidad = lectura.FechaCaducidad
            };
        }

        /// <summary>
        /// Parsea una ubicación escaneada en formato "ALM$UBIC" y retorna (almacen, ubicacion)
        /// </summary>
        private (string almacen, string ubicacion) ParsearUbicacionEscaneada(string? ubicacionEscaneada)
        {
            if (string.IsNullOrEmpty(ubicacionEscaneada))
                return ("", "");

            var partes = ubicacionEscaneada.Split('$');
            if (partes.Length != 2)
                throw new InvalidOperationException($"Formato de ubicación inválido: {ubicacionEscaneada}. Debe ser 'ALMACEN$UBICACION'");

            return (partes[0], partes[1]);
        }

		private static ResultadoConteoDetalladoDto MapToResultadoConteoDetalladoDto(ResultadoConteo resultado)
		{
			return new ResultadoConteoDetalladoDto
			{
				// Campos de ResultadoConteo
				GuidID = resultado.GuidID,
				OrdenGuid = resultado.OrdenGuid,
				CodigoAlmacen = resultado.CodigoAlmacen,
				CodigoUbicacion = resultado.CodigoUbicacion,
				CodigoArticulo = resultado.CodigoArticulo,
				DescripcionArticulo = resultado.DescripcionArticulo,
				LotePartida = resultado.LotePartida,
				CantidadContada = resultado.CantidadContada,
				CantidadStock = resultado.CantidadStock,
				UsuarioCodigo = resultado.UsuarioCodigo,
				Diferencia = resultado.Diferencia,
				AccionFinal = resultado.AccionFinal,
				AprobadoPorCodigo = resultado.AprobadoPorCodigo,
				FechaEvaluacion = resultado.FechaEvaluacion,
				AjusteAplicado = resultado.AjusteAplicado,
				FechaCaducidad = resultado.FechaCaducidad,
				// Campos de OrdenConteo
				CodigoEmpresa = resultado.Orden?.CodigoEmpresa ?? 0,
				Titulo = resultado.Orden?.Titulo ?? string.Empty,
				Visibilidad = resultado.Orden?.Visibilidad ?? string.Empty
			};
		}

		public async Task<OrdenDto> ReasignarLineaAsync(Guid resultadoGuid, ReasignarLineaDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                _logger.LogInformation("Iniciando reasignación de línea para resultado {ResultadoGuid} al operario {Operario}", resultadoGuid, dto.CodigoOperario);
                
                // Buscar el ResultadoConteo por GuidID
                var resultado = await _context.ResultadosConteo
                    .Include(r => r.Orden)
                    .FirstOrDefaultAsync(r => r.GuidID == resultadoGuid);

                if (resultado == null)
                {
                    throw new InvalidOperationException($"No se encontró el resultado de conteo con Guid {resultadoGuid}");
                }

                // Verificar que la acción sea SUPERVISION
                if (resultado.AccionFinal != "SUPERVISION")
                {
                    throw new InvalidOperationException($"Solo se puede reasignar resultados con AccionFinal = SUPERVISION. El resultado actual tiene AccionFinal = {resultado.AccionFinal}");
                }

                // Crear nueva orden basada en el resultado original
                var nuevaOrden = new OrdenConteo
                {
                    CodigoEmpresa = resultado.Orden.CodigoEmpresa,
                    Titulo = $"REASIGNACIÓN - {resultado.Orden.Titulo}",
                    Visibilidad = resultado.Orden.Visibilidad,
                    ModoGeneracion = "REASIGNA", // Solo 10 caracteres máximo
                    Alcance = "UBICACION",
                    FiltrosJson = GenerarFiltrosJsonParaReasignacion(resultado),
                    FechaPlan = DateTime.UtcNow,
                    SupervisorCodigo = dto.SupervisorCodigo,
                    CreadoPorCodigo = dto.SupervisorCodigo ?? "SISTEMA",
                    Estado = "ASIGNADO",
                    Prioridad = 1,
                    FechaCreacion = DateTime.UtcNow,
                    CodigoOperario = dto.CodigoOperario,
                    FechaAsignacion = DateTime.UtcNow,
                    CodigoAlmacen = resultado.CodigoAlmacen,
                    CodigoUbicacion = resultado.CodigoUbicacion,
                    CodigoArticulo = resultado.CodigoArticulo
                };

                _context.OrdenesConteo.Add(nuevaOrden);
                await _context.SaveChangesAsync();

                // Marcar el resultado original como reasignado
                resultado.AprobadoPorCodigo = dto.CodigoOperario;
                resultado.AccionFinal = "REASIGNADO";
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                
                _logger.LogInformation("Línea reasignada exitosamente. Nueva orden creada con Guid: {NuevaOrdenGuid}", nuevaOrden.GuidID);
                
                return MapToOrdenDto(nuevaOrden);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error en ReasignarLineaAsync: {Message}", ex.Message);
                throw;
            }
        }

        private string GenerarFiltrosJsonParaReasignacion(ResultadoConteo resultado)
        {
            var filtros = new Dictionary<string, object>
            {
                ["almacen"] = resultado.CodigoAlmacen
            };

            if (!string.IsNullOrEmpty(resultado.CodigoUbicacion))
            {
                filtros["ubicacion"] = resultado.CodigoUbicacion;
            }

            if (!string.IsNullOrEmpty(resultado.CodigoArticulo))
            {
                filtros["articulo"] = resultado.CodigoArticulo;
            }

            return System.Text.Json.JsonSerializer.Serialize(filtros);
        }

        private string TruncarTitulo(string titulo, int maxLength)
        {
            if (string.IsNullOrEmpty(titulo))
                return "REASIGNACIÓN";
                
            return titulo.Length <= maxLength 
                ? titulo 
                : titulo.Substring(0, maxLength - 3) + "...";
        }

        private string TruncarComentario(string comentario, int maxLength)
        {
            if (string.IsNullOrEmpty(comentario))
                return string.Empty;
                
            return comentario.Length <= maxLength 
                ? comentario 
                : comentario.Substring(0, maxLength - 3) + "...";
        }

        private async Task<List<string>> ObtenerAlmacenesAutorizadosAsync(int operarioId, int codigoEmpresa)
        {
            try
            {
                // 1. Obtener almacenes individuales del operario
                var almacenesIndividuales = await _sageDbContext.OperariosAlmacenes
                    .Where(a => a.Operario == operarioId && a.CodigoEmpresa == codigoEmpresa)
                    .Select(a => a.CodigoAlmacen!)
                    .Where(a => a != null) // Filtrar nulls
                    .ToListAsync();

                // 2. Obtener el centro logístico del operario
                var operario = await _sageDbContext.Operarios
                    .Where(o => o.Id == operarioId)
                    .Select(o => o.CodigoCentro)
                    .FirstOrDefaultAsync();

                var todosLosAlmacenes = new List<string>(almacenesIndividuales);

                // 3. Si el operario tiene centro logístico, obtener sus almacenes
                if (!string.IsNullOrEmpty(operario))
                {
                    var almacenesCentro = await _sageDbContext.Almacenes
                        .Where(a => a.CodigoCentro == operario && a.CodigoEmpresa == codigoEmpresa)
                        .Select(a => a.CodigoAlmacen!)
                        .Where(a => a != null)
                        .ToListAsync();

                    todosLosAlmacenes.AddRange(almacenesCentro);
                }

                // 4. Eliminar duplicados y devolver
                var resultado = todosLosAlmacenes.Distinct().ToList();

                _logger.LogInformation("Operario {Operario} tiene acceso a {Count} almacenes (individuales: {Individuales}, centro: {Centro}): {Almacenes}", 
                    operarioId, resultado.Count, almacenesIndividuales.Count, 
                    !string.IsNullOrEmpty(operario) ? "SÍ" : "NO", string.Join(", ", resultado));

                return resultado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo almacenes autorizados para operario {Operario}", operarioId);
                return new List<string>();
            }
        }
    }
} 
