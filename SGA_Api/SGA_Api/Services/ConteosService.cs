using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Conteos;
using SGA_Api.Models.Inventario;
using SGA_Api.Models.Stock;
using SGA_Api.Models.Almacen;
using SGA_Api.Models.Palet;
using System.Text.RegularExpressions;

namespace SGA_Api.Services
{
    public class ConteosService : IConteosService
    {
        private readonly AuroraSgaDbContext _context;
        private readonly SageDbContext _sageDbContext;
        private readonly StorageControlDbContext _storageControlContext;
        private readonly ILogger<ConteosService> _logger;
        private readonly INotificacionesConteosService? _notificacionesConteos;

        public ConteosService(
            AuroraSgaDbContext context, 
            SageDbContext sageDbContext,
            StorageControlDbContext storageControlContext,
            ILogger<ConteosService> logger,
            INotificacionesConteosService? notificacionesConteos = null)
        {
            _context = context;
            _sageDbContext = sageDbContext;
            _storageControlContext = storageControlContext;
            _logger = logger;
            _notificacionesConteos = notificacionesConteos;
        }

        public async Task<OrdenDto> CrearOrdenAsync(CrearOrdenDto dto)
        {
            try
            {
                _logger.LogInformation("Iniciando creación de orden de conteo: {Titulo}", dto.Titulo);
               
                // Extraer valores del FiltrosJson
                var codigoAlmacen = ExtraerAlmacenDelFiltro(dto.FiltrosJson);
                var codigoArticulo = ExtraerArticuloDelFiltro(dto.FiltrosJson);
                var codigosArticulos = ExtraerArticulosDelFiltro(dto.FiltrosJson); // Soporte para múltiples artículos
               
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
               
                // Verificar si hay artículos (formato antiguo o nuevo)
                bool tieneArticulos = (!string.IsNullOrEmpty(codigoArticulo)) || 
                                      (codigosArticulos != null && codigosArticulos.Any());
               
                if (tieneArticulos &&
                    string.IsNullOrEmpty(codigoUbicacion) &&
                    string.IsNullOrEmpty(pasillo) &&
                    string.IsNullOrEmpty(estanteria))
                {
                    // Si solo se especifica artículo(s) (sin ubicación, pasillo, estantería)
                    // Distinguir entre un artículo vs múltiples artículos
                    if (codigosArticulos != null && codigosArticulos.Count > 1)
                    {
                        alcanceDeterminado = "MULTIARTICULO";
                    }
                    else
                    {
                        alcanceDeterminado = "ARTICULO";
                    }
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
 
                // Validar periodicidad
                if (dto.EsPeriodico && (!dto.FrecuenciaDias.HasValue || dto.FrecuenciaDias.Value <= 0))
                {
                    throw new InvalidOperationException("La frecuencia en días es obligatoria y debe ser mayor a 0 para conteos periódicos");
                }

                var orden = new OrdenConteo
                {
                    CodigoEmpresa = dto.CodigoEmpresa,
                    Titulo = dto.Titulo,
                    Visibilidad = dto.Visibilidad,
                    ModoGeneracion = dto.ModoGeneracion,
                    Alcance = alcanceDeterminado, // Usar el alcance determinado automáticamente
                    FiltrosJson = dto.FiltrosJson,
                    FechaPlan = dto.FechaPlan, // Guardar tal cual viene del cliente (sin convertir a UTC)
                    FechaEjecucion = dto.FechaEjecucion, // Guardar tal cual viene del cliente (sin convertir a UTC)
                    SupervisorCodigo = dto.SupervisorCodigo,
                    CreadoPorCodigo = dto.CreadoPorCodigo,
                    Estado = !string.IsNullOrEmpty(dto.CodigoOperario) ? "ASIGNADO" : "PLANIFICADO",
                    Prioridad = dto.Prioridad,
                    FechaCreacion = DateTime.Now,
                    CodigoOperario = dto.CodigoOperario,
                    FechaAsignacion = !string.IsNullOrEmpty(dto.CodigoOperario) ? DateTime.Now : null,
                    CodigoAlmacen = codigoAlmacen,
                    CodigoUbicacion = codigoUbicacion,
                    CodigoArticulo = codigoArticulo,
                    // Propiedades de periodicidad
                    EsPeriodico = dto.EsPeriodico,
                    FrecuenciaDias = dto.FrecuenciaDias,
                    Activo = dto.EsPeriodico, // Si es periódico, activo por defecto
                    FechaProximaRenovacion = null // Se calculará después de guardar para usar FechaCreacion
                };
 
                _context.OrdenesConteo.Add(orden);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Orden guardada con Guid: {Guid}", orden.GuidID);

                // Si es periódico, calcular FechaProximaRenovacion desde la fecha de creación
                // para mantener el mismo día de la semana en las renovaciones
                if (orden.EsPeriodico && orden.FrecuenciaDias.HasValue)
                {
                    orden.FechaProximaRenovacion = orden.FechaCreacion.Date.AddDays(orden.FrecuenciaDias.Value);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Fecha de próxima renovación calculada desde fecha de creación: {FechaCreacion} + {Frecuencia} días = {FechaProximaRenovacion}", 
                        orden.FechaCreacion.Date, orden.FrecuenciaDias.Value, orden.FechaProximaRenovacion);
                }
 
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

                // Para conteos periódicos, permitir edición independientemente del estado
                // porque los cambios solo afectan a la plantilla para futuras renovaciones
                // Para conteos normales, solo se puede editar si está en estado PLANIFICADO o ASIGNADO
                if (!orden.EsPeriodico && orden.Estado != "PLANIFICADO" && orden.Estado != "ASIGNADO")
                    throw new InvalidOperationException($"No se puede editar una orden en estado {orden.Estado}");

                // Si es un conteo periódico, SIEMPRE tratarlo como plantilla
                // IMPORTANTE: Estos cambios solo afectan a la plantilla periódica. Las órdenes de conteo
                // ya creadas (renovaciones anteriores) son independientes y NO se verán afectadas.
                // Los cambios se aplicarán solo a las PRÓXIMAS renovaciones cuando se creen nuevas órdenes.
                if (orden.EsPeriodico)
                {
                    _logger.LogInformation("Editando conteo periódico (plantilla) - Solo actualizando campos permitidos: Prioridad, Operario, Comentario, FechaProximaRenovacion, FrecuenciaDias. Los cambios solo afectarán a futuras renovaciones. Estado actual: {Estado}, Activo: {Activo}", orden.Estado, orden.Activo);
                    
                    // Solo actualizar campos permitidos en la plantilla
                    // NOTA: Las órdenes de conteo ya creadas (renovaciones) son independientes y no se ven afectadas
                    orden.Prioridad = dto.Prioridad;
                    orden.Comentario = dto.Comentario;
                    orden.CodigoOperario = dto.CodigoOperario;
                    orden.FrecuenciaDias = dto.FrecuenciaDias;
                    
                    // Para conteos periódicos, NO cambiar el estado si está en CERRADO o EN_PROCESO
                    // Solo cambiar el estado si está en PLANIFICADO o ASIGNADO (estados de plantilla)
                    // Esto asegura que no editamos órdenes ya cerradas o en proceso
                    if (orden.Estado == "PLANIFICADO" || orden.Estado == "ASIGNADO")
                    {
                        // Si se asigna un operario, actualizar estado y fecha de asignación
                        if (!string.IsNullOrEmpty(dto.CodigoOperario) && string.IsNullOrEmpty(orden.CodigoOperario))
                        {
                            orden.Estado = "ASIGNADO";
                            orden.FechaAsignacion = DateTime.Now;
                        }
                        // Si se quita el operario, cambiar a PLANIFICADO
                        else if (string.IsNullOrEmpty(dto.CodigoOperario) && !string.IsNullOrEmpty(orden.CodigoOperario))
                        {
                            orden.Estado = "PLANIFICADO";
                            orden.FechaAsignacion = null;
                        }
                    }
                    // Si el estado es CERRADO, EN_PROCESO u otro, NO cambiar el estado
                    // porque podría estar editando una orden ya cerrada o en proceso
                }
                else
                {
                    // Edición normal: actualizar todos los campos
                    orden.Titulo = dto.Titulo;
                    orden.Prioridad = dto.Prioridad;
                    orden.FechaPlan = dto.FechaPlan;
                    orden.Comentario = dto.Comentario;
                    orden.Visibilidad = dto.Visibilidad ?? "VISIBLE";
                    orden.CodigoOperario = dto.CodigoOperario;
                    
                    // Si se asigna un operario, actualizar estado y fecha de asignación
                    if (!string.IsNullOrEmpty(dto.CodigoOperario) && string.IsNullOrEmpty(orden.CodigoOperario))
                    {
                        orden.Estado = "ASIGNADO";
                        orden.FechaAsignacion = DateTime.Now;
                    }
                    // Si se quita el operario, cambiar a PLANIFICADO
                    else if (string.IsNullOrEmpty(dto.CodigoOperario) && !string.IsNullOrEmpty(orden.CodigoOperario))
                    {
                        orden.Estado = "PLANIFICADO";
                        orden.FechaAsignacion = null;
                    }

                    // Actualizar filtros
                    orden.FiltrosJson = dto.FiltrosJson;

                    // Extraer valores del FiltrosJson para actualizar campos específicos
                    var codigoAlmacen = ExtraerAlmacenDelFiltro(dto.FiltrosJson);
                    var codigoArticulo = ExtraerArticuloDelFiltro(dto.FiltrosJson);
                    var codigosArticulos = ExtraerArticulosDelFiltro(dto.FiltrosJson); // Soporte para múltiples artículos
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
                   
                    // Verificar si hay artículos (formato antiguo o nuevo)
                    bool tieneArticulos = (!string.IsNullOrEmpty(codigoArticulo)) || 
                                          (codigosArticulos != null && codigosArticulos.Any());
                   
                    if (tieneArticulos &&
                        string.IsNullOrEmpty(codigoUbicacion) &&
                        string.IsNullOrEmpty(pasillo) &&
                        string.IsNullOrEmpty(estanteria))
                    {
                        // Si solo se especifica artículo(s) (sin ubicación, pasillo, estantería)
                        // Distinguir entre un artículo vs múltiples artículos
                        if (codigosArticulos != null && codigosArticulos.Count > 1)
                        {
                            alcanceDeterminado = "MULTIARTICULO";
                        }
                        else
                        {
                            alcanceDeterminado = "ARTICULO";
                        }
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
                }

                // Si es un conteo periódico, actualizar FechaProximaRenovacion y activar si estaba desactivado
                if (orden.EsPeriodico)
                {
                    if (dto.FechaProximaRenovacion.HasValue)
                    {
                        orden.FechaProximaRenovacion = dto.FechaProximaRenovacion.Value;
                        _logger.LogInformation("Fecha de próxima renovación actualizada para orden {Guid}: {Fecha}", guid, dto.FechaProximaRenovacion.Value);
                    }
                    
                    if (dto.FrecuenciaDias.HasValue)
                    {
                        orden.FrecuenciaDias = dto.FrecuenciaDias.Value;
                        _logger.LogInformation("Frecuencia de días actualizada para orden {Guid}: {Frecuencia} días", guid, dto.FrecuenciaDias.Value);
                    }
                    
                    // Si estaba desactivado y se está actualizando, activarlo
                    if (!orden.Activo)
                    {
                        orden.Activo = true;
                        _logger.LogInformation("Conteo periódico {Guid} activado mediante actualización", guid);
                    }
                }

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
        public async Task<IEnumerable<OrdenDto>> ListarTodasLasOrdenesAsync(
            string? estado = null, 
            string? codigoOperario = null,
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null,
            string? codigoOperarioSesion = null,
            string? creadoPorCodigo = null)
        {
            try
            {
                _logger.LogInformation("Iniciando ListarTodasLasOrdenesAsync con estado: {Estado}, operario: {Operario}, fechaDesde: {FechaDesde}, fechaHasta: {FechaHasta}, operarioSesion: {OperarioSesion}, creadoPorCodigo: {CreadoPorCodigo}", 
                    estado, codigoOperario, fechaDesde, fechaHasta, codigoOperarioSesion, creadoPorCodigo);
                
                var query = _context.OrdenesConteo.AsQueryable();

                // Aplicar filtro de estado si se especifica
                if (!string.IsNullOrEmpty(estado))
                {
                    query = query.Where(o => o.Estado == estado);
                }

                // Aplicar filtro de operario si se especifica (filtro visual)
                if (!string.IsNullOrEmpty(codigoOperario))
                {
                    query = query.Where(o => o.CodigoOperario == codigoOperario);
                }

                // Aplicar filtro por creador (solo propios vs ver todos)
                if (!string.IsNullOrEmpty(creadoPorCodigo))
                {
                    query = query.Where(o => o.CreadoPorCodigo == creadoPorCodigo);
                }

                // Aplicar filtro de fecha desde si se especifica
                if (fechaDesde.HasValue)
                {
                    query = query.Where(o => o.FechaCreacion.Date >= fechaDesde.Value.Date);
                }

                // Aplicar filtro de fecha hasta si se especifica
                if (fechaHasta.HasValue)
                {
                    query = query.Where(o => o.FechaCreacion.Date <= fechaHasta.Value.Date);
                }

                // Si se proporciona el código de operario de la sesión, filtrar por almacenes autorizados
                List<OrdenConteo> ordenes;
                if (!string.IsNullOrEmpty(codigoOperarioSesion) && int.TryParse(codigoOperarioSesion, out int operarioIdSesion))
                {
                    // Obtener todas las órdenes primero para obtener el código de empresa
                    var ordenesTemporales = await query.ToListAsync();
                    if (!ordenesTemporales.Any())
                    {
                        return new List<OrdenDto>();
                    }
                    
                    // Obtener el código de empresa de la primera orden (todas deberían tener el mismo)
                    var codigoEmpresa = ordenesTemporales.First().CodigoEmpresa;
                    if (codigoEmpresa == 0) codigoEmpresa = 1; // Por defecto empresa 1
                    
                    var almacenesAutorizados = await ObtenerAlmacenesAutorizadosAsync(operarioIdSesion, codigoEmpresa);
                    
                    if (almacenesAutorizados.Any())
                    {
                        // Filtrar órdenes que tengan almacén en los autorizados
                        // O que no tengan almacén específico (alcance general)
                        ordenes = ordenesTemporales.Where(o => 
                            (o.CodigoAlmacen != null && almacenesAutorizados.Contains(o.CodigoAlmacen)) ||
                            (o.CodigoAlmacen == null && o.Alcance != "ALMACEN"))
                            .OrderByDescending(o => o.FechaCreacion)
                            .ToList();
                    }
                    else
                    {
                        // Si el operario no tiene almacenes autorizados, no mostrar ninguna orden
                        _logger.LogWarning("Operario de sesión {Operario} no tiene almacenes autorizados para órdenes de conteo", codigoOperarioSesion);
                        return new List<OrdenDto>();
                    }
                }
                else
                {
                    // Si no hay filtro de operario de sesión, obtener todas las órdenes
                    ordenes = await query
                        .OrderByDescending(o => o.FechaCreacion)
                        .ToListAsync();
                }

                _logger.LogInformation("Se encontraron {Count} órdenes de conteo (todas)", ordenes.Count);
                
                // Obtener conteo de lecturas para cada orden
                var ordenGuids = ordenes.Select(o => o.GuidID).ToList();
                var conteosLecturas = await _context.LecturasConteo
                    .Where(l => ordenGuids.Contains(l.OrdenGuid))
                    .GroupBy(l => l.OrdenGuid)
                    .Select(g => new
                    {
                        OrdenGuid = g.Key,
                        TotalLecturas = g.Count()
                    })
                    .ToListAsync();
                
                var conteosDict = conteosLecturas.ToDictionary(c => c.OrdenGuid, c => c.TotalLecturas);
                
                return ordenes.Select(orden =>
                {
                    var dto = MapToOrdenDto(orden);
                    if (conteosDict.TryGetValue(orden.GuidID, out var totalLecturas))
                    {
                        dto.TotalLecturas = totalLecturas;
                    }
                    else
                    {
                        dto.TotalLecturas = 0;
                    }
                    return dto;
                });
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

                // Obtener almacenes de la orden o del filtro
                List<string>? codigosAlmacen = null;
                string? codigoAlmacen = null; // Para alcances que requieren almacén único
                
                // Para alcance ARTICULO/MULTIARTICULO, intentar extraer lista de almacenes
                if (orden.Alcance?.ToUpper() == "ARTICULO" || orden.Alcance?.ToUpper() == "MULTIARTICULO")
                {
                    codigosAlmacen = ExtraerAlmacenesDelFiltro(orden.FiltrosJson);
                    
                    // Si no hay lista, intentar almacén único (compatibilidad)
                    if (codigosAlmacen == null || !codigosAlmacen.Any())
                    {
                        codigoAlmacen = orden.CodigoAlmacen;
                        if (string.IsNullOrEmpty(codigoAlmacen))
                        {
                            codigoAlmacen = ExtraerAlmacenDelFiltro(orden.FiltrosJson);
                        }
                        if (!string.IsNullOrEmpty(codigoAlmacen))
                        {
                            codigosAlmacen = new List<string> { codigoAlmacen };
                        }
                    }
                }
                else
                {
                    // Para otros alcances, usar almacén único (comportamiento actual)
                    codigoAlmacen = orden.CodigoAlmacen;
                    if (string.IsNullOrEmpty(codigoAlmacen))
                    {
                        codigoAlmacen = ExtraerAlmacenDelFiltro(orden.FiltrosJson);
                    }

                    if (string.IsNullOrEmpty(codigoAlmacen))
                    {
                        _logger.LogWarning("No se pudo determinar el almacén para la orden {Guid}", orden.GuidID);
                        return;
                    }
                }

                _logger.LogInformation("GENERACION_DEBUG: Alcance de la orden: '{Alcance}', FiltrosJson: '{FiltrosJson}'", orden.Alcance, orden.FiltrosJson);
                
                switch (orden.Alcance?.ToUpper())
                {
                    case "ARTICULO":
                        _logger.LogInformation("GENERACION_DEBUG: Generando lecturas por ARTICULO");
                        await GenerarLecturasPorArticuloAsync(orden, codigosAlmacen, lecturasGeneradas);
                        break;
                    case "MULTIARTICULO":
                        _logger.LogInformation("GENERACION_DEBUG: Generando lecturas por MULTIARTICULO");
                        await GenerarLecturasPorArticuloAsync(orden, codigosAlmacen, lecturasGeneradas);
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

        private async Task GenerarLecturasPorArticuloAsync(OrdenConteo orden, List<string>? codigosAlmacen, List<LecturaConteo> lecturasGeneradas)
        {
            // Obtener ejercicio actual
            var ejercicio = await _sageDbContext.Periodos
                .Where(p => p.CodigoEmpresa == orden.CodigoEmpresa && p.Fechainicio <= DateTime.Now)
                .OrderByDescending(p => p.Fechainicio)
                .Select(p => p.Ejercicio)
                .FirstOrDefaultAsync();

            if (ejercicio == 0) return;

            // Obtener lista de artículos (soporta múltiples artículos)
            List<string>? codigosArticulos = null;
            
            // Primero intentar desde CodigoArticulo (compatibilidad)
            if (!string.IsNullOrEmpty(orden.CodigoArticulo))
            {
                codigosArticulos = new List<string> { orden.CodigoArticulo };
            }
            else
            {
                // Intentar extraer desde FiltrosJson (soporta formato nuevo y antiguo)
                codigosArticulos = ExtraerArticulosDelFiltro(orden.FiltrosJson);
            }

            if (codigosArticulos != null && codigosArticulos.Any())
            {
                // Construir query base
                var query = _storageControlContext.AcumuladoStockUbicacion
                    .Where(x => x.CodigoEmpresa == orden.CodigoEmpresa &&
                               x.Ejercicio == ejercicio &&
                               codigosArticulos.Contains(x.CodigoArticulo) &&
                               x.UnidadSaldo > 0);

                // Filtrar por almacenes si se especifican
                if (codigosAlmacen != null && codigosAlmacen.Any())
                {
                    query = query.Where(x => codigosAlmacen.Contains(x.CodigoAlmacen));
                    _logger.LogInformation("ARTICULO_DEBUG: Filtrando por {Count} almacenes específicos: {Almacenes}", 
                        codigosAlmacen.Count, string.Join(", ", codigosAlmacen));
                }
                else
                {
                    _logger.LogInformation("ARTICULO_DEBUG: Buscando en TODOS los almacenes (sin filtro de almacén)");
                }

                var stockArticulos = await query.ToListAsync();

                _logger.LogInformation("ARTICULO_DEBUG: Encontrados {Count} registros de stock para {ArticulosCount} artículo(s) en todos los almacenes", 
                    stockArticulos.Count, codigosArticulos.Count);

                // Agrupar por artículo para obtener descripciones
                var articulosUnicos = stockArticulos.Select(s => s.CodigoArticulo).Distinct().ToList();
                var descripcionesArticulos = new Dictionary<string, string>();
                
                foreach (var codigoArticulo in articulosUnicos)
                {
                    var descripcion = await ObtenerDescripcionArticuloAsync(orden.CodigoEmpresa, codigoArticulo);
                    descripcionesArticulos[codigoArticulo] = descripcion;
                }

                foreach (var stock in stockArticulos)
                {
                    // Solo incluir ubicaciones válidas para conteo
                    if (EsUbicacionValidaParaConteo(stock.Ubicacion))
                    {
                        var descripcionArticulo = descripcionesArticulos.GetValueOrDefault(stock.CodigoArticulo, stock.CodigoArticulo);
                        
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

            var rangoPasillo = ExtraerRangoPasilloDelFiltro(orden.FiltrosJson);
            if (rangoPasillo.HasValue)
            {
                List<AcumuladoStockUbicacion> stockPasillo;
                
                // Si es valor único [1, 1], usar lógica antigua (más rápida)
                if (rangoPasillo.Value.desde == rangoPasillo.Value.hasta)
                {
                    var prefijoPasillo = $"UB{rangoPasillo.Value.desde.ToString().PadLeft(3, '0')}";
                    
                    stockPasillo = await _storageControlContext.AcumuladoStockUbicacion
                        .Where(x => x.CodigoEmpresa == orden.CodigoEmpresa &&
                                   x.Ejercicio == ejercicio &&
                                   x.CodigoAlmacen == codigoAlmacen &&
                                   x.Ubicacion != null &&
                                   x.Ubicacion.StartsWith(prefijoPasillo) &&
                                   x.UnidadSaldo > 0)
                        .ToListAsync();
                }
                else
                {
                    // Rango real: obtener todos los pasillos y filtrar por componentes
                    var todosLosPasillos = await _storageControlContext.AcumuladoStockUbicacion
                        .Where(x => x.CodigoEmpresa == orden.CodigoEmpresa &&
                                   x.Ejercicio == ejercicio &&
                                   x.CodigoAlmacen == codigoAlmacen &&
                                   x.Ubicacion != null &&
                                   x.Ubicacion.StartsWith("UB") &&
                                   x.Ubicacion.Length >= 5 && // Al menos UB + pasillo (5 caracteres)
                                   x.UnidadSaldo > 0)
                        .ToListAsync();
                    
                    // Filtrar por rango de pasillo
                    stockPasillo = todosLosPasillos.Where(stock =>
                    {
                        var componentes = ExtraerComponentesUbicacion(stock.Ubicacion);
                        return componentes.HasValue &&
                               componentes.Value.pasillo >= rangoPasillo.Value.desde &&
                               componentes.Value.pasillo <= rangoPasillo.Value.hasta;
                    }).ToList();
                }

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

            var rangoPasillo = ExtraerRangoPasilloDelFiltro(orden.FiltrosJson);
            var rangoEstanteria = ExtraerRangoEstanteriaDelFiltro(orden.FiltrosJson);
            
            _logger.LogInformation("ESTANTERIA_DEBUG: FiltrosJson = '{FiltrosJson}'", orden.FiltrosJson);
            _logger.LogInformation("ESTANTERIA_DEBUG: Rango pasillo = {RangoPasillo}, Rango estantería = {RangoEstanteria}", 
                rangoPasillo.HasValue ? $"[{rangoPasillo.Value.desde}, {rangoPasillo.Value.hasta}]" : "null",
                rangoEstanteria.HasValue ? $"[{rangoEstanteria.Value.desde}, {rangoEstanteria.Value.hasta}]" : "null");
            
            if (rangoPasillo.HasValue && rangoEstanteria.HasValue)
            {
                List<AcumuladoStockUbicacion> stockEstanteria;
                
                // Si ambos son valores únicos [1,1] y [5,5], usar lógica antigua (más rápida)
                if (rangoPasillo.Value.desde == rangoPasillo.Value.hasta && 
                    rangoEstanteria.Value.desde == rangoEstanteria.Value.hasta)
                {
                    var pasilloFormateado = rangoPasillo.Value.desde.ToString().PadLeft(3, '0');
                    var estanteriaFormateada = rangoEstanteria.Value.desde.ToString().PadLeft(3, '0');
                    
                    _logger.LogInformation("ESTANTERIA_DEBUG: Usando lógica antigua (valores únicos). Pasillo = '{PasilloFormateado}', Estantería = '{EstanteriaFormateada}'", 
                        pasilloFormateado, estanteriaFormateada);
                    
                    stockEstanteria = await _storageControlContext.AcumuladoStockUbicacion
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
                }
                else
                {
                    // Rango real: obtener todos y filtrar por componentes
                    _logger.LogInformation("ESTANTERIA_DEBUG: Usando lógica de rangos. Pasillo [{DesdePasillo}, {HastaPasillo}], Estantería [{DesdeEstanteria}, {HastaEstanteria}]",
                        rangoPasillo.Value.desde, rangoPasillo.Value.hasta,
                        rangoEstanteria.Value.desde, rangoEstanteria.Value.hasta);
                    
                    var todosLosStock = await _storageControlContext.AcumuladoStockUbicacion
                        .Where(x => x.CodigoEmpresa == orden.CodigoEmpresa &&
                                   x.Ejercicio == ejercicio &&
                                   x.CodigoAlmacen == codigoAlmacen &&
                                   x.Ubicacion != null &&
                                   x.Ubicacion.StartsWith("UB") &&
                                   x.Ubicacion.Length >= 8 &&
                                   x.UnidadSaldo > 0)
                        .ToListAsync();
                    
                    // Filtrar por rangos de pasillo y estantería
                    stockEstanteria = todosLosStock.Where(stock =>
                    {
                        var componentes = ExtraerComponentesUbicacion(stock.Ubicacion);
                        return componentes.HasValue &&
                               componentes.Value.pasillo >= rangoPasillo.Value.desde &&
                               componentes.Value.pasillo <= rangoPasillo.Value.hasta &&
                               componentes.Value.estanteria >= rangoEstanteria.Value.desde &&
                               componentes.Value.estanteria <= rangoEstanteria.Value.hasta;
                    }).ToList();
                }

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
                FechaCierre = orden.FechaCierre,
                EsPeriodico = orden.EsPeriodico,
                Activo = orden.Activo,
                FechaProximaRenovacion = orden.FechaProximaRenovacion,
                FrecuenciaDias = orden.FrecuenciaDias,
                OrdenPadreGuid = orden.OrdenPadreGuid
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

        private List<string>? ExtraerAlmacenesDelFiltro(string? filtrosJson)
        {
            if (string.IsNullOrEmpty(filtrosJson)) return null;
            try
            {
                var filtros = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(filtrosJson);
                
                // Priorizar formato nuevo: array de almacenes
                if (filtros?.ContainsKey("almacenes") == true)
                {
                    var almacenes = filtros["almacenes"];
                    if (almacenes.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        return almacenes.EnumerateArray()
                            .Select(x => x.GetString())
                            .Where(x => !string.IsNullOrEmpty(x))
                            .ToList();
                    }
                }
                
                // Compatibilidad: formato antiguo con un solo almacén
                if (filtros?.ContainsKey("almacen") == true)
                {
                    var almacen = filtros["almacen"].GetString();
                    if (!string.IsNullOrEmpty(almacen))
                    {
                        return new List<string> { almacen };
                    }
                }
                
                return null;
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

        private List<string>? ExtraerArticulosDelFiltro(string? filtrosJson)
        {
            if (string.IsNullOrEmpty(filtrosJson)) return null;
            try
            {
                var filtros = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(filtrosJson);
                
                // Priorizar formato nuevo: array de artículos
                if (filtros?.ContainsKey("articulos") == true)
                {
                    var articulos = filtros["articulos"];
                    if (articulos.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        return articulos.EnumerateArray()
                            .Select(x => x.GetString())
                            .Where(x => !string.IsNullOrEmpty(x))
                            .ToList();
                    }
                }
                
                // Compatibilidad: formato antiguo con un solo artículo
                if (filtros?.ContainsKey("articulo") == true)
                {
                    var articulo = filtros["articulo"];
                    if (articulo.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var codigoArticulo = articulo.GetString();
                        if (!string.IsNullOrEmpty(codigoArticulo))
                        {
                            return new List<string> { codigoArticulo };
                        }
                    }
                }
                
                return null;
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

        /// <summary>
        /// Extrae los componentes numéricos de una ubicación en formato UB001005003001
        /// </summary>
        private (int pasillo, int estanteria, int altura, int posicion)? ExtraerComponentesUbicacion(string? ubicacion)
        {
            if (string.IsNullOrEmpty(ubicacion) || !ubicacion.StartsWith("UB", StringComparison.OrdinalIgnoreCase))
                return null;

            // Formato esperado: UB001005003001 (14 caracteres total)
            // UB (2) + pasillo (3) + estanteria (3) + altura (3) + posicion (3) = 14
            if (ubicacion.Length < 14)
                return null;

            try
            {
                var pasilloStr = ubicacion.Substring(2, 3);
                var estanteriaStr = ubicacion.Substring(5, 3);
                var alturaStr = ubicacion.Substring(8, 3);
                var posicionStr = ubicacion.Substring(11, 3);

                if (int.TryParse(pasilloStr, out int pasillo) &&
                    int.TryParse(estanteriaStr, out int estanteria) &&
                    int.TryParse(alturaStr, out int altura) &&
                    int.TryParse(posicionStr, out int posicion))
                {
                    return (pasillo, estanteria, altura, posicion);
                }
            }
            catch
            {
                // Si falla el parsing, retornar null
            }

            return null;
        }

        /// <summary>
        /// Extrae rango de pasillo del filtro JSON. Soporta formato antiguo (string) y nuevo (objeto con desde/hasta)
        /// </summary>
        private (int desde, int hasta)? ExtraerRangoPasilloDelFiltro(string? filtrosJson)
        {
            if (string.IsNullOrEmpty(filtrosJson)) return null;
            try
            {
                var filtros = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(filtrosJson);
                
                if (!filtros.ContainsKey("pasillo")) return null;
                
                var pasillo = filtros["pasillo"];
                
                // FORMATO ANTIGUO: "pasillo": "1" (string)
                if (pasillo.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    if (int.TryParse(pasillo.GetString(), out int valor))
                        return (valor, valor); // Rango [1, 1]
                }
                
                // FORMATO NUEVO: "pasillo": {"desde": 1, "hasta": 5} (objeto)
                if (pasillo.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    var desde = pasillo.TryGetProperty("desde", out var desdeProp) && desdeProp.ValueKind == System.Text.Json.JsonValueKind.Number
                        ? desdeProp.GetInt32() : (int?)null;
                    var hasta = pasillo.TryGetProperty("hasta", out var hastaProp) && hastaProp.ValueKind == System.Text.Json.JsonValueKind.Number
                        ? hastaProp.GetInt32() : (int?)null;
                    
                    if (desde.HasValue && hasta.HasValue)
                        return (desde.Value, hasta.Value);
                }
                
                return null;
            }
            catch
            {
                // Si falla, intentar con método antiguo como fallback
                var pasilloAntiguo = ExtraerPasilloDelFiltro(filtrosJson);
                if (!string.IsNullOrEmpty(pasilloAntiguo) && int.TryParse(pasilloAntiguo, out int valor))
                    return (valor, valor);
                return null;
            }
        }

        /// <summary>
        /// Extrae rango de estantería del filtro JSON. Soporta formato antiguo (string) y nuevo (objeto con desde/hasta)
        /// </summary>
        private (int desde, int hasta)? ExtraerRangoEstanteriaDelFiltro(string? filtrosJson)
        {
            if (string.IsNullOrEmpty(filtrosJson)) return null;
            try
            {
                var filtros = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(filtrosJson);
                
                if (!filtros.ContainsKey("estanteria")) return null;
                
                var estanteria = filtros["estanteria"];
                
                // FORMATO ANTIGUO: "estanteria": "5" (string)
                if (estanteria.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    if (int.TryParse(estanteria.GetString(), out int valor))
                        return (valor, valor); // Rango [5, 5]
                }
                
                // FORMATO NUEVO: "estanteria": {"desde": 1, "hasta": 5} (objeto)
                if (estanteria.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    var desde = estanteria.TryGetProperty("desde", out var desdeProp) && desdeProp.ValueKind == System.Text.Json.JsonValueKind.Number
                        ? desdeProp.GetInt32() : (int?)null;
                    var hasta = estanteria.TryGetProperty("hasta", out var hastaProp) && hastaProp.ValueKind == System.Text.Json.JsonValueKind.Number
                        ? hastaProp.GetInt32() : (int?)null;
                    
                    if (desde.HasValue && hasta.HasValue)
                        return (desde.Value, hasta.Value);
                }
                
                return null;
            }
            catch
            {
                // Si falla, intentar con método antiguo como fallback
                var estanteriaAntigua = ExtraerEstanteriaDelFiltro(filtrosJson);
                if (!string.IsNullOrEmpty(estanteriaAntigua) && int.TryParse(estanteriaAntigua, out int valor))
                    return (valor, valor);
                return null;
            }
        }

        /// <summary>
        /// Extrae rango de altura del filtro JSON. Soporta formato antiguo (string) y nuevo (objeto con desde/hasta)
        /// </summary>
        private (int desde, int hasta)? ExtraerRangoAlturaDelFiltro(string? filtrosJson)
        {
            if (string.IsNullOrEmpty(filtrosJson)) return null;
            try
            {
                var filtros = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(filtrosJson);
                
                if (!filtros.ContainsKey("altura")) return null;
                
                var altura = filtros["altura"];
                
                // FORMATO ANTIGUO: "altura": "3" (string)
                if (altura.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    if (int.TryParse(altura.GetString(), out int valor))
                        return (valor, valor); // Rango [3, 3]
                }
                
                // FORMATO NUEVO: "altura": {"desde": 1, "hasta": 3} (objeto)
                if (altura.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    var desde = altura.TryGetProperty("desde", out var desdeProp) && desdeProp.ValueKind == System.Text.Json.JsonValueKind.Number
                        ? desdeProp.GetInt32() : (int?)null;
                    var hasta = altura.TryGetProperty("hasta", out var hastaProp) && hastaProp.ValueKind == System.Text.Json.JsonValueKind.Number
                        ? hastaProp.GetInt32() : (int?)null;
                    
                    if (desde.HasValue && hasta.HasValue)
                        return (desde.Value, hasta.Value);
                }
                
                return null;
            }
            catch
            {
                // Si falla, intentar con método antiguo como fallback
                var alturaAntigua = ExtraerAlturaDelFiltro(filtrosJson);
                if (!string.IsNullOrEmpty(alturaAntigua) && int.TryParse(alturaAntigua, out int valor))
                    return (valor, valor);
                return null;
            }
        }

        /// <summary>
        /// Extrae rango de posición del filtro JSON. Soporta formato antiguo (string) y nuevo (objeto con desde/hasta)
        /// </summary>
        private (int desde, int hasta)? ExtraerRangoPosicionDelFiltro(string? filtrosJson)
        {
            if (string.IsNullOrEmpty(filtrosJson)) return null;
            try
            {
                var filtros = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(filtrosJson);
                
                if (!filtros.ContainsKey("posicion")) return null;
                
                var posicion = filtros["posicion"];
                
                // FORMATO ANTIGUO: "posicion": "1" (string)
                if (posicion.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    if (int.TryParse(posicion.GetString(), out int valor))
                        return (valor, valor); // Rango [1, 1]
                }
                
                // FORMATO NUEVO: "posicion": {"desde": 1, "hasta": 10} (objeto)
                if (posicion.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    var desde = posicion.TryGetProperty("desde", out var desdeProp) && desdeProp.ValueKind == System.Text.Json.JsonValueKind.Number
                        ? desdeProp.GetInt32() : (int?)null;
                    var hasta = posicion.TryGetProperty("hasta", out var hastaProp) && hastaProp.ValueKind == System.Text.Json.JsonValueKind.Number
                        ? hastaProp.GetInt32() : (int?)null;
                    
                    if (desde.HasValue && hasta.HasValue)
                        return (desde.Value, hasta.Value);
                }
                
                return null;
            }
            catch
            {
                // Si falla, intentar con método antiguo como fallback
                var posicionAntigua = ExtraerPosicionDelFiltro(filtrosJson);
                if (!string.IsNullOrEmpty(posicionAntigua) && int.TryParse(posicionAntigua, out int valor))
                    return (valor, valor);
                return null;
            }
        }




		// TODO: Implementar métodos restantes
		//public async Task<LecturaResponseDto> CrearLecturaAsync(Guid ordenGuid, LecturaDto dto)
		//{
		//    using var tx = await _context.Database.BeginTransactionAsync();
		//    try
		//    {
		//        var orden = await _context.OrdenesConteo.FirstOrDefaultAsync(o => o.GuidID == ordenGuid);
		//        if (orden is null)
		//            throw new InvalidOperationException($"No se encontró la orden con Guid {ordenGuid}");
		//        if (orden.Estado != "EN_PROCESO")
		//            throw new InvalidOperationException($"No se puede crear lecturas para una orden en estado {orden.Estado}");

		//        Obtener el almacén de la lectura(viene del frontend)
		//        var almacenOrden = dto.CodigoAlmacen;
		//        if (string.IsNullOrWhiteSpace(almacenOrden))
		//            throw new InvalidOperationException("El código de almacén es obligatorio en la lectura.");

		//        Obtener el stock actual del artículo
		//        var ejercicio = await _sageDbContext.Periodos
		//            .Where(p => p.CodigoEmpresa == orden.CodigoEmpresa && p.Fechainicio <= DateTime.Now)
		//            .OrderByDescending(p => p.Fechainicio)
		//            .Select(p => p.Ejercicio)
		//            .FirstOrDefaultAsync();

		//        if (ejercicio == 0)
		//            throw new InvalidOperationException("No se encontró ejercicio válido");

		//        var stockTotalUbicacion = await _storageControlContext.AcumuladoStockUbicacion
		//            .Where(x => x.CodigoEmpresa == orden.CodigoEmpresa &&
		//                       x.Ejercicio == ejercicio &&
		//                       x.CodigoAlmacen == almacenOrden &&
		//                       x.Ubicacion == dto.CodigoUbicacion &&
		//                       x.CodigoArticulo == dto.CodigoArticulo &&
		//                       (string.IsNullOrEmpty(dto.LotePartida) || x.Partida == dto.LotePartida))
		//            .Select(x => x.UnidadSaldo ?? 0m)
		//            .FirstOrDefaultAsync();

		//        Operario(para límites)
		//        var operarioCodigo = !string.IsNullOrEmpty(orden.CodigoOperario) ? orden.CodigoOperario : dto.UsuarioCodigo;
		//        var operario = await _sageDbContext.Operarios.AsNoTracking().FirstOrDefaultAsync(o => o.Id.ToString() == operarioCodigo);
		//        var limUnidades = operario?.MRH_LimiteInventarioUnidades ?? 0m;
		//        var limEuros = operario?.MRH_LimiteInventarioEuros ?? 0m;

		//        Descripción del artículo(siempre obtenerla del servicio)
		//        var descripcionArticulo = !string.IsNullOrEmpty(dto.CodigoArticulo)
		//            ? await ObtenerDescripcionArticuloAsync(orden.CodigoEmpresa, dto.CodigoArticulo)
		//            : "";

		//        Detectar si hay material paletizado en esta ubicación
		//        var materialPaletizado = await DetectarMaterialPaletizadoAsync(
		//            almacenOrden,
		//            dto.CodigoUbicacion,
		//            dto.CodigoArticulo,
		//            dto.LotePartida,
		//            dto.FechaCaducidad);

		//        Datos de palet proporcionados por el cliente
		//       Guid? paletIdDetectado = dto.PaletId;
		//        string? codigoPaletDetectado = dto.CodigoPalet;
		//        string? codigoGS1Detectado = dto.CodigoGS1;
		//        var dtoProporcionoPalet = paletIdDetectado.HasValue ||
		//                                   !string.IsNullOrWhiteSpace(codigoPaletDetectado) ||
		//                                   !string.IsNullOrWhiteSpace(codigoGS1Detectado);

		//        Complementar datos del palet solo si el DTO indicó alguno
		//        if (dtoProporcionoPalet && !paletIdDetectado.HasValue && materialPaletizado != null)
		//        {
		//            paletIdDetectado = materialPaletizado.PaletId;
		//            codigoPaletDetectado = materialPaletizado.CodigoPalet;
		//            codigoGS1Detectado = materialPaletizado.CodigoGS1;

		//            _logger.LogInformation("🔍 Palet detectado automáticamente para completar DTO: {CodigoPalet} (ID: {PaletId}) en ubicación {Ubicacion}",
		//                codigoPaletDetectado, paletIdDetectado, dto.CodigoUbicacion);
		//        }

		//        Determinar el stock de referencia para la lectura
		//        decimal stockReferencia = stockTotalUbicacion;

		//        Obtener información de palets y cantidades en la ubicación
		//       var paletsDisponiblesEnUbicacion = await DetectarTodosLosPaletsAsync(
		//           almacenOrden,
		//           dto.CodigoUbicacion,
		//           dto.CodigoArticulo,
		//           dto.LotePartida,
		//           dto.FechaCaducidad);

		//        var sumaPalets = paletsDisponiblesEnUbicacion.Sum(pl => pl.Cantidad);
		//        var remanente = stockTotalUbicacion - sumaPalets;
		//        if (remanente < 0m)
		//            remanente = 0m;

		//        var dtoSolicitaPalet = dtoProporcionoPalet;

		//        if (dtoSolicitaPalet)
		//        {
		//            var paletCoincidente = paletsDisponiblesEnUbicacion.FirstOrDefault(pl =>
		//                (paletIdDetectado.HasValue && pl.PaletId == paletIdDetectado.Value) ||
		//                (!string.IsNullOrWhiteSpace(codigoPaletDetectado) && pl.CodigoPalet == codigoPaletDetectado) ||
		//                (!string.IsNullOrWhiteSpace(codigoGS1Detectado) && pl.CodigoGS1 == codigoGS1Detectado))
		//                ?? (paletsDisponiblesEnUbicacion.Count == 1 ? paletsDisponiblesEnUbicacion[0] : null);

		//            if (paletCoincidente != null)
		//            {
		//                paletIdDetectado = paletCoincidente.PaletId;
		//                codigoPaletDetectado = paletCoincidente.CodigoPalet;
		//                codigoGS1Detectado = paletCoincidente.CodigoGS1;
		//                stockReferencia = paletCoincidente.Cantidad;
		//            }
		//            else
		//            {
		//                paletIdDetectado = null;
		//                codigoPaletDetectado = null;
		//                codigoGS1Detectado = null;
		//                stockReferencia = remanente;
		//            }
		//        }
		//        else
		//        {
		//            if (!dtoProporcionoPalet && paletsDisponiblesEnUbicacion.Count == 1 && remanente <= 0.0001m)
		//            {
		//                var paletUnico = paletsDisponiblesEnUbicacion[0];
		//                paletIdDetectado = paletUnico.PaletId;
		//                codigoPaletDetectado = paletUnico.CodigoPalet;
		//                codigoGS1Detectado = paletUnico.CodigoGS1;
		//                stockReferencia = paletUnico.Cantidad;
		//            }
		//            else
		//            {
		//                paletIdDetectado = null;
		//                codigoPaletDetectado = null;
		//                codigoGS1Detectado = null;
		//                stockReferencia = remanente;
		//            }
		//        }

		//        Crear SIEMPRE una lectura nueva(no actualizar "pendientes")
		//        var lectura = new LecturaConteo
		//        {
		//            OrdenGuid = orden.GuidID,
		//            CodigoAlmacen = almacenOrden,
		//            CodigoUbicacion = dto.CodigoUbicacion,
		//            CodigoArticulo = dto.CodigoArticulo,
		//            DescripcionArticulo = descripcionArticulo,
		//            LotePartida = dto.LotePartida,
		//            CantidadContada = dto.CantidadContada,
		//            CantidadStock = stockReferencia,
		//            UsuarioCodigo = dto.UsuarioCodigo,
		//            Comentario = dto.Comentario,
		//            Fecha = DateTime.Now,
		//            FechaCaducidad = dto.FechaCaducidad,
		//            Información de palet(detectado automáticamente si no se proporcionó)
		//            PaletId = paletIdDetectado,
		//            CodigoPalet = codigoPaletDetectado,
		//            CodigoGS1 = codigoGS1Detectado
		//        };
		//        _context.LecturasConteo.Add(lectura);
		//        await _context.SaveChangesAsync();

		//        Diferencia y acción
		//       var diferencia = (dto.CantidadContada ?? 0m) - stockReferencia;
		//        if (Math.Abs(diferencia) >= 0.0001m)
		//        {
		//            Calcular acción considerando límites por unidades y por euros(precio medio)
		//            var diferenciaAbs = Math.Abs(diferencia);
		//            decimal? precioMedio = null;
		//            try
		//            {
		//                precioMedio = await _sageDbContext.AcumuladoStock
		//                    .Where(a => a.CodigoEmpresa == orden.CodigoEmpresa
		//                            && a.Ejercicio == ejercicio
		//                            && a.CodigoArticulo == dto.CodigoArticulo)
		//                    .Select(a => a.PrecioMedio)
		//                    .FirstOrDefaultAsync();
		//            }
		//            catch { /* si falla el precio, tratamos como 0 */ }

		//            var superaUnidades = limUnidades > 0m && diferenciaAbs > limUnidades;
		//            var superaEuros = false;
		//            if (limEuros > 0m && precioMedio.HasValue)
		//            {
		//                superaEuros = diferenciaAbs * precioMedio.Value > limEuros;
		//            }

		//            var accion = (superaUnidades || superaEuros) ? "SUPERVISION" : "AJUSTE";

		//            Crear un nuevo ResultadoConteo para cada lectura
		//           var resultado = new ResultadoConteo
		//           {
		//               OrdenGuid = orden.GuidID,
		//               CodigoAlmacen = lectura.CodigoAlmacen,
		//               CodigoUbicacion = lectura.CodigoUbicacion,
		//               CodigoArticulo = lectura.CodigoArticulo,
		//               DescripcionArticulo = lectura.DescripcionArticulo,
		//               LotePartida = lectura.LotePartida,
		//               CantidadContada = lectura.CantidadContada,
		//               CantidadStock = lectura.CantidadStock,
		//               UsuarioCodigo = lectura.UsuarioCodigo,
		//               Diferencia = diferencia,
		//               AccionFinal = accion,
		//               FechaEvaluacion = DateTime.Now,
		//               AjusteAplicado = false,
		//               FechaCaducidad = lectura.FechaCaducidad,
		//               Información de palet(detectado automáticamente si no se proporcionó)

		//                   PaletId = lectura.PaletId,
		//               CodigoPalet = lectura.CodigoPalet,
		//               CodigoGS1 = lectura.CodigoGS1
		//           };
		//            _context.ResultadosConteo.Add(resultado);

		//            await _context.SaveChangesAsync();

		//            Si la acción es AJUSTE, crear registro en InventarioAjustes(funcionalidad básica)
		//            if (accion == "AJUSTE")
		//            {
		//                _logger.LogInformation("🔧 Creando InventarioAjustes para resultado {ResultadoGuid} con diferencia {Diferencia}", resultado.GuidID, diferencia);

		//                var inventarioAjuste = new InventarioAjustes
		//                {
		//                    IdInventario = null, // Para ajustes de conteo no necesitamos InventarioCabecera
		//                    CodigoArticulo = resultado.CodigoArticulo,
		//                    CodigoUbicacion = resultado.CodigoUbicacion,
		//                    Diferencia = resultado.Diferencia,
		//                    UsuarioId = operario?.Id ?? int.Parse(resultado.UsuarioCodigo), // Usar operario.Id o parsear UsuarioCodigo
		//                    Fecha = DateTime.Now,
		//                    IdConteo = resultado.OrdenGuid,
		//                    CodigoEmpresa = (short)orden.CodigoEmpresa, // Convertir int a short
		//                    CodigoAlmacen = resultado.CodigoAlmacen,
		//                    Estado = "PENDIENTE_ERP",
		//                    FechaCaducidad = resultado.FechaCaducidad,
		//                    Información de palet si existe
		//                    PaletId = resultado.PaletId,
		//                    CodigoPalet = resultado.CodigoPalet,
		//                    CodigoGS1 = resultado.CodigoGS1,
		//                    Partida = resultado.LotePartida
		//                };

		//                _context.InventarioAjustes.Add(inventarioAjuste);
		//                _logger.LogInformation("✅ InventarioAjustes agregado al contexto para resultado {ResultadoGuid}", resultado.GuidID);

		//            ADICIONAL: Si hay palets en la ubicación, crear TempPaletLinea para consolidación unificada
		//                if (resultado.PaletId.HasValue)
		//                {
		//                    var tempPaletLinea = new TempPaletLinea
		//                    {
		//                        Id = Guid.NewGuid(),
		//                        PaletId = resultado.PaletId.Value,
		//                        CodigoEmpresa = (short)orden.CodigoEmpresa,
		//                        CodigoArticulo = resultado.CodigoArticulo,
		//                        DescripcionArticulo = resultado.DescripcionArticulo,
		//                        Cantidad = resultado.Diferencia, // DELTA (+/-)
		//                        UnidadMedida = "UN", // Unidad por defecto
		//                        Lote = resultado.LotePartida,
		//                        FechaCaducidad = resultado.FechaCaducidad,
		//                        CodigoAlmacen = resultado.CodigoAlmacen,
		//                        Ubicacion = resultado.CodigoUbicacion,
		//                        UsuarioId = operario?.Id ?? int.Parse(resultado.UsuarioCodigo),
		//                        FechaAgregado = DateTime.Now,
		//                        Observaciones = $"Ajuste de conteo - Orden: {orden.Titulo}",
		//                        TraspasoId = null, // No es un traspaso
		//                        ConteoId = resultado.OrdenGuid, // ID del conteo
		//                        Procesada = false,
		//                        EsHeredada = false
		//                    };
		//                    _context.TempPaletLineas.Add(tempPaletLinea);

		//                    _logger.LogInformation("✅ Creada TempPaletLinea adicional para consolidación de palet: PaletId={PaletId}, Diferencia={Diferencia}, Articulo={Articulo}",
		//                        resultado.PaletId, resultado.Diferencia, resultado.CodigoArticulo);
		//                }

		//                Guardar ajustes ANTES de verificar lecturas pendientes
		//               await _context.SaveChangesAsync();
		//                _logger.LogInformation("💾 Ajustes guardados en BD para resultado {ResultadoGuid}", resultado.GuidID);
		//            }
		//        }

		//    TEMPORAL: Comentar verificación de lecturas pendientes para debug
		//        _logger.LogInformation("🔍 Verificando lecturas pendientes para orden {OrdenGuid} con operario {Operario}", orden.GuidID, dto.UsuarioCodigo);

		//        var lecturasPendientes = await ObtenerLecturasPendientesAsync(orden.GuidID, dto.UsuarioCodigo);
		//        _logger.LogInformation("📊 Lecturas pendientes encontradas: {Count}", lecturasPendientes.Count());

		//        if (!lecturasPendientes.Any())
		//        {
		//            No quedan lecturas pendientes, cerrar la orden automáticamente
		//            _logger.LogInformation("🔒 Cerrando orden {OrdenGuid} automáticamente - no quedan lecturas pendientes", orden.GuidID);
		//            orden.Estado = "CERRADO";
		//            orden.FechaCierre = DateTime.Now;
		//            await _context.SaveChangesAsync();

		//            _logger.LogInformation("✅ Orden {OrdenGuid} cerrada automáticamente al completar todas las lecturas", orden.GuidID);
		//        }
		//        else
		//        {
		//            _logger.LogInformation("⏳ Orden {OrdenGuid} mantiene estado EN_PROCESO - quedan {Count} lecturas pendientes", orden.GuidID, lecturasPendientes.Count());
		//        }

		//        _logger.LogInformation("💾 Confirmando transacción para orden {OrdenGuid}", orden.GuidID);
		//        await tx.CommitAsync();
		//        _logger.LogInformation("✅ Transacción confirmada para orden {OrdenGuid}", orden.GuidID);

		//        return MapToLecturaResponseDto(lectura);
		//    }
		//    catch
		//    {
		//        await tx.RollbackAsync();
		//        throw;
		//    }
		//}
		// TODO: Implementar métodos restantes

		#region Validación de Alcance para Lecturas

		/// <summary>
		/// Valida que una lectura esté dentro del alcance definido en la orden de conteo
		/// </summary>
		private async Task ValidarLecturaDentroAlcanceAsync(OrdenConteo orden, LecturaDto dto)
		{
			_logger.LogInformation("Validando lectura dentro del alcance. Orden: {OrdenGuid}, Alcance: {Alcance}, Artículo: {Articulo}, Ubicación: {Ubicacion}", 
				orden.GuidID, orden.Alcance, dto.CodigoArticulo, dto.CodigoUbicacion);

			switch (orden.Alcance?.ToUpper())
			{
				case "ARTICULO":
				case "MULTIARTICULO":
					await ValidarAlcanceArticuloAsync(orden, dto);
					break;
				case "UBICACION":
					ValidarAlcanceUbicacion(orden, dto);
					break;
				case "ESTANTERIA":
					ValidarAlcanceEstanteria(orden, dto);
					break;
				case "PASILLO":
					ValidarAlcancePasillo(orden, dto);
					break;
				case "ALMACEN":
					ValidarAlcanceAlmacen(orden, dto);
					break;
				case "PALET":
					ValidarAlcancePalet(orden, dto);
					break;
				default:
					// Para alcances desconocidos, validar al menos el almacén
					ValidarAlcanceAlmacen(orden, dto);
					break;
			}

			_logger.LogInformation("✅ Validación de alcance exitosa para lectura. Orden: {OrdenGuid}, Artículo: {Articulo}, Ubicación: {Ubicacion}", 
				orden.GuidID, dto.CodigoArticulo, dto.CodigoUbicacion);
		}

		/// <summary>
		/// Valida que el artículo esté dentro del alcance ARTICULO/MULTIARTICULO
		/// </summary>
		private async Task ValidarAlcanceArticuloAsync(OrdenConteo orden, LecturaDto dto)
		{
			// Obtener lista de artículos permitidos
			List<string>? codigosArticulos = null;

			// Primero intentar desde CodigoArticulo (compatibilidad)
			if (!string.IsNullOrEmpty(orden.CodigoArticulo))
			{
				codigosArticulos = new List<string> { orden.CodigoArticulo };
			}
			else
			{
				// Intentar extraer desde FiltrosJson (soporta formato nuevo y antiguo)
				codigosArticulos = ExtraerArticulosDelFiltro(orden.FiltrosJson);
			}

			if (codigosArticulos == null || !codigosArticulos.Any())
			{
				_logger.LogWarning("No se encontraron artículos en el alcance de la orden {OrdenGuid}", orden.GuidID);
				throw new InvalidOperationException("La orden de conteo no tiene artículos definidos en su alcance");
			}

			// Validar que el artículo esté en la lista
			if (!codigosArticulos.Contains(dto.CodigoArticulo))
			{
				_logger.LogWarning("❌ Lectura rechazada: El artículo {CodigoArticulo} no está incluido en el alcance de la orden {OrdenGuid}. Artículos permitidos: {ArticulosPermitidos}", 
					dto.CodigoArticulo, orden.GuidID, string.Join(", ", codigosArticulos));
				throw new InvalidOperationException($"El artículo {dto.CodigoArticulo} no está incluido en el alcance de esta orden de conteo. Artículos permitidos: {string.Join(", ", codigosArticulos)}");
			}

			// Validar almacén si está especificado en el filtro
			var codigosAlmacenFiltro = ExtraerAlmacenesDelFiltro(orden.FiltrosJson);
			if (codigosAlmacenFiltro != null && codigosAlmacenFiltro.Any())
			{
				if (!codigosAlmacenFiltro.Contains(dto.CodigoAlmacen))
				{
					_logger.LogWarning("❌ Lectura rechazada: El almacén {CodigoAlmacen} no está incluido en el alcance de la orden {OrdenGuid}. Almacenes permitidos: {AlmacenesPermitidos}", 
						dto.CodigoAlmacen, orden.GuidID, string.Join(", ", codigosAlmacenFiltro));
					throw new InvalidOperationException($"El almacén {dto.CodigoAlmacen} no está incluido en el alcance de esta orden de conteo. Almacenes permitidos: {string.Join(", ", codigosAlmacenFiltro)}");
				}
			}
			else
			{
				// Si no hay almacenes específicos, validar que el operario tenga acceso al almacén
				var operarioCodigo = !string.IsNullOrEmpty(orden.CodigoOperario) ? orden.CodigoOperario : dto.UsuarioCodigo;
				if (!string.IsNullOrEmpty(operarioCodigo) && int.TryParse(operarioCodigo, out int operarioId))
				{
					var almacenesAutorizados = await ObtenerAlmacenesAutorizadosAsync(operarioId, orden.CodigoEmpresa);
					if (almacenesAutorizados.Any() && !almacenesAutorizados.Contains(dto.CodigoAlmacen))
					{
						_logger.LogWarning("❌ Lectura rechazada: El operario {Operario} no tiene acceso al almacén {CodigoAlmacen}", 
							operarioCodigo, dto.CodigoAlmacen);
						throw new InvalidOperationException($"El operario no tiene acceso al almacén {dto.CodigoAlmacen}");
					}
				}
			}
		}

		/// <summary>
		/// Valida que la ubicación esté dentro del alcance UBICACION
		/// </summary>
		private void ValidarAlcanceUbicacion(OrdenConteo orden, LecturaDto dto)
		{
			// Obtener la ubicación esperada
			string? ubicacionEsperada = null;

			// 1) Prioriza la ubicación guardada en la orden (incluye "" como válida)
			if (orden.CodigoUbicacion != null)
			{
				ubicacionEsperada = orden.CodigoUbicacion;
			}
			else
			{
				// 2) Intenta extraer "ubicacion" directa del filtro
				ubicacionEsperada = ExtraerUbicacionDelFiltro(orden.FiltrosJson);

				// 3) Si no hay, reconstruye desde pasillo/estanteria/altura/posicion
				if (ubicacionEsperada == null)
				{
					var ubicacionPasillo = ExtraerPasilloDelFiltro(orden.FiltrosJson);
					var ubicacionEstanteria = ExtraerEstanteriaDelFiltro(orden.FiltrosJson);
					var altura = ExtraerAlturaDelFiltro(orden.FiltrosJson);
					var posicion = ExtraerPosicionDelFiltro(orden.FiltrosJson);

					if (!string.IsNullOrEmpty(ubicacionPasillo) &&
						!string.IsNullOrEmpty(ubicacionEstanteria) &&
						!string.IsNullOrEmpty(altura) &&
						!string.IsNullOrEmpty(posicion))
					{
						ubicacionEsperada = $"UB{ubicacionPasillo.PadLeft(3, '0')}{ubicacionEstanteria.PadLeft(3, '0')}{altura.PadLeft(3, '0')}{posicion.PadLeft(3, '0')}";
					}
				}
			}

			if (ubicacionEsperada == null)
			{
				_logger.LogWarning("No se pudo determinar la ubicación esperada para la orden {OrdenGuid}", orden.GuidID);
				throw new InvalidOperationException("No se pudo determinar la ubicación esperada para esta orden de conteo");
			}

			// Validar que la ubicación coincida exactamente
			if (dto.CodigoUbicacion != ubicacionEsperada)
			{
				_logger.LogWarning("❌ Lectura rechazada: La ubicación {CodigoUbicacion} no coincide con el alcance de la orden {OrdenGuid}. Ubicación esperada: {UbicacionEsperada}", 
					dto.CodigoUbicacion, orden.GuidID, ubicacionEsperada);
				throw new InvalidOperationException($"La ubicación {dto.CodigoUbicacion} no coincide con el alcance de esta orden de conteo. Ubicación esperada: {ubicacionEsperada}");
			}

			// Validar almacén
			var codigoAlmacen = orden.CodigoAlmacen;
			if (string.IsNullOrEmpty(codigoAlmacen))
			{
				codigoAlmacen = ExtraerAlmacenDelFiltro(orden.FiltrosJson);
			}

			if (!string.IsNullOrEmpty(codigoAlmacen) && dto.CodigoAlmacen != codigoAlmacen)
			{
				_logger.LogWarning("❌ Lectura rechazada: El almacén {CodigoAlmacen} no coincide con el alcance de la orden {OrdenGuid}. Almacén esperado: {AlmacenEsperado}", 
					dto.CodigoAlmacen, orden.GuidID, codigoAlmacen);
				throw new InvalidOperationException($"El almacén {dto.CodigoAlmacen} no coincide con el alcance de esta orden de conteo. Almacén esperado: {codigoAlmacen}");
			}
		}

		/// <summary>
		/// Valida que la ubicación esté dentro del alcance ESTANTERIA
		/// </summary>
		private void ValidarAlcanceEstanteria(OrdenConteo orden, LecturaDto dto)
		{
			// Validar almacén
			var codigoAlmacen = orden.CodigoAlmacen;
			if (string.IsNullOrEmpty(codigoAlmacen))
			{
				codigoAlmacen = ExtraerAlmacenDelFiltro(orden.FiltrosJson);
			}

			if (!string.IsNullOrEmpty(codigoAlmacen) && dto.CodigoAlmacen != codigoAlmacen)
			{
				_logger.LogWarning("❌ Lectura rechazada: El almacén {CodigoAlmacen} no coincide con el alcance de la orden {OrdenGuid}. Almacén esperado: {AlmacenEsperado}", 
					dto.CodigoAlmacen, orden.GuidID, codigoAlmacen);
				throw new InvalidOperationException($"El almacén {dto.CodigoAlmacen} no coincide con el alcance de esta orden de conteo. Almacén esperado: {codigoAlmacen}");
			}

			// Extraer rangos de pasillo y estantería
			var rangoPasillo = ExtraerRangoPasilloDelFiltro(orden.FiltrosJson);
			var rangoEstanteria = ExtraerRangoEstanteriaDelFiltro(orden.FiltrosJson);

			if (!rangoPasillo.HasValue || !rangoEstanteria.HasValue)
			{
				_logger.LogWarning("No se encontraron rangos de pasillo/estantería para la orden {OrdenGuid}", orden.GuidID);
				throw new InvalidOperationException("La orden de conteo no tiene rangos de pasillo/estantería definidos");
			}

			// Extraer componentes de la ubicación
			var componentes = ExtraerComponentesUbicacion(dto.CodigoUbicacion);
			if (!componentes.HasValue)
			{
				// Verificar si es una ubicación especial válida
				if (EsUbicacionValidaParaConteo(dto.CodigoUbicacion) && 
					!string.IsNullOrEmpty(dto.CodigoUbicacion) && 
					!dto.CodigoUbicacion.StartsWith("UB"))
				{
					_logger.LogWarning("❌ Lectura rechazada: La ubicación especial {CodigoUbicacion} no es válida para alcance ESTANTERIA que requiere formato UB", 
						dto.CodigoUbicacion);
					throw new InvalidOperationException($"La ubicación {dto.CodigoUbicacion} es una ubicación especial y no es válida para alcance ESTANTERIA. Se requiere formato UB (ej: UB001005003001)");
				}
				else
				{
					_logger.LogWarning("❌ Lectura rechazada: La ubicación {CodigoUbicacion} no tiene formato válido para validación de estantería", 
						dto.CodigoUbicacion);
					throw new InvalidOperationException($"La ubicación {dto.CodigoUbicacion} no tiene formato válido para validación de estantería. Se requiere formato UB (ej: UB001005003001)");
				}
			}

			// Validar que el pasillo esté en el rango
			if (componentes.Value.pasillo < rangoPasillo.Value.desde || componentes.Value.pasillo > rangoPasillo.Value.hasta)
			{
				_logger.LogWarning("❌ Lectura rechazada: El pasillo {Pasillo} está fuera del rango [{Desde}, {Hasta}] de la orden {OrdenGuid}", 
					componentes.Value.pasillo, rangoPasillo.Value.desde, rangoPasillo.Value.hasta, orden.GuidID);
				throw new InvalidOperationException($"La ubicación {dto.CodigoUbicacion} está fuera del rango de pasillos del conteo. Pasillo {componentes.Value.pasillo} no está en el rango [{rangoPasillo.Value.desde}, {rangoPasillo.Value.hasta}]");
			}

			// Validar que la estantería esté en el rango
			if (componentes.Value.estanteria < rangoEstanteria.Value.desde || componentes.Value.estanteria > rangoEstanteria.Value.hasta)
			{
				_logger.LogWarning("❌ Lectura rechazada: La estantería {Estanteria} está fuera del rango [{Desde}, {Hasta}] de la orden {OrdenGuid}", 
					componentes.Value.estanteria, rangoEstanteria.Value.desde, rangoEstanteria.Value.hasta, orden.GuidID);
				throw new InvalidOperationException($"La ubicación {dto.CodigoUbicacion} está fuera del rango de estanterías del conteo. Estantería {componentes.Value.estanteria} no está en el rango [{rangoEstanteria.Value.desde}, {rangoEstanteria.Value.hasta}]");
			}
		}

		/// <summary>
		/// Valida que la ubicación esté dentro del alcance PASILLO
		/// </summary>
		private void ValidarAlcancePasillo(OrdenConteo orden, LecturaDto dto)
		{
			// Validar almacén
			var codigoAlmacen = orden.CodigoAlmacen;
			if (string.IsNullOrEmpty(codigoAlmacen))
			{
				codigoAlmacen = ExtraerAlmacenDelFiltro(orden.FiltrosJson);
			}

			if (!string.IsNullOrEmpty(codigoAlmacen) && dto.CodigoAlmacen != codigoAlmacen)
			{
				_logger.LogWarning("❌ Lectura rechazada: El almacén {CodigoAlmacen} no coincide con el alcance de la orden {OrdenGuid}. Almacén esperado: {AlmacenEsperado}", 
					dto.CodigoAlmacen, orden.GuidID, codigoAlmacen);
				throw new InvalidOperationException($"El almacén {dto.CodigoAlmacen} no coincide con el alcance de esta orden de conteo. Almacén esperado: {codigoAlmacen}");
			}

			// Extraer rango de pasillo
			var rangoPasillo = ExtraerRangoPasilloDelFiltro(orden.FiltrosJson);

			if (!rangoPasillo.HasValue)
			{
				_logger.LogWarning("No se encontró rango de pasillo para la orden {OrdenGuid}", orden.GuidID);
				throw new InvalidOperationException("La orden de conteo no tiene rango de pasillo definido");
			}

			// Extraer componente de pasillo de la ubicación
			var componentes = ExtraerComponentesUbicacion(dto.CodigoUbicacion);
			if (!componentes.HasValue)
			{
				// Verificar si es una ubicación especial válida
				if (EsUbicacionValidaParaConteo(dto.CodigoUbicacion) && 
					!string.IsNullOrEmpty(dto.CodigoUbicacion) && 
					!dto.CodigoUbicacion.StartsWith("UB"))
				{
					_logger.LogWarning("❌ Lectura rechazada: La ubicación especial {CodigoUbicacion} no es válida para alcance PASILLO que requiere formato UB", 
						dto.CodigoUbicacion);
					throw new InvalidOperationException($"La ubicación {dto.CodigoUbicacion} es una ubicación especial y no es válida para alcance PASILLO. Se requiere formato UB (ej: UB001005003001)");
				}
				else
				{
					_logger.LogWarning("❌ Lectura rechazada: La ubicación {CodigoUbicacion} no tiene formato válido para validación de pasillo", 
						dto.CodigoUbicacion);
					throw new InvalidOperationException($"La ubicación {dto.CodigoUbicacion} no tiene formato válido para validación de pasillo. Se requiere formato UB (ej: UB001005003001)");
				}
			}

			// Validar que el pasillo esté en el rango
			if (componentes.Value.pasillo < rangoPasillo.Value.desde || componentes.Value.pasillo > rangoPasillo.Value.hasta)
			{
				_logger.LogWarning("❌ Lectura rechazada: El pasillo {Pasillo} está fuera del rango [{Desde}, {Hasta}] de la orden {OrdenGuid}", 
					componentes.Value.pasillo, rangoPasillo.Value.desde, rangoPasillo.Value.hasta, orden.GuidID);
				throw new InvalidOperationException($"La ubicación {dto.CodigoUbicacion} está fuera del rango de pasillos del conteo. Pasillo {componentes.Value.pasillo} no está en el rango [{rangoPasillo.Value.desde}, {rangoPasillo.Value.hasta}]");
			}
		}

		/// <summary>
		/// Valida que el almacén esté dentro del alcance ALMACEN
		/// </summary>
		private void ValidarAlcanceAlmacen(OrdenConteo orden, LecturaDto dto)
		{
			var codigoAlmacen = orden.CodigoAlmacen;
			if (string.IsNullOrEmpty(codigoAlmacen))
			{
				codigoAlmacen = ExtraerAlmacenDelFiltro(orden.FiltrosJson);
			}

			if (string.IsNullOrEmpty(codigoAlmacen))
			{
				_logger.LogWarning("No se encontró almacén para la orden {OrdenGuid}", orden.GuidID);
				throw new InvalidOperationException("La orden de conteo no tiene almacén definido");
			}

			if (dto.CodigoAlmacen != codigoAlmacen)
			{
				_logger.LogWarning("❌ Lectura rechazada: El almacén {CodigoAlmacen} no coincide con el alcance de la orden {OrdenGuid}. Almacén esperado: {AlmacenEsperado}", 
					dto.CodigoAlmacen, orden.GuidID, codigoAlmacen);
				throw new InvalidOperationException($"El almacén {dto.CodigoAlmacen} no coincide con el alcance de esta orden de conteo. Almacén esperado: {codigoAlmacen}");
			}
		}

		/// <summary>
		/// Valida que el palet esté dentro del alcance PALET
		/// </summary>
		private void ValidarAlcancePalet(OrdenConteo orden, LecturaDto dto)
		{
			// Para alcance PALET, validar principalmente el almacén
			// Si hay filtros específicos de palet en el futuro, se pueden agregar aquí
			ValidarAlcanceAlmacen(orden, dto);
		}

		#endregion

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

				// Obtener el almacén de la lectura (viene del frontend)
				var almacenOrden = dto.CodigoAlmacen;
				if (string.IsNullOrWhiteSpace(almacenOrden))
					throw new InvalidOperationException("El código de almacén es obligatorio en la lectura.");

				// Validar que la lectura esté dentro del alcance de la orden
				await ValidarLecturaDentroAlcanceAsync(orden, dto);

				// Obtener el stock actual del artículo
				var ejercicio = await _sageDbContext.Periodos
					.Where(p => p.CodigoEmpresa == orden.CodigoEmpresa && p.Fechainicio <= DateTime.Now)
					.OrderByDescending(p => p.Fechainicio)
					.Select(p => p.Ejercicio)
					.FirstOrDefaultAsync();

				if (ejercicio == 0)
					throw new InvalidOperationException("No se encontró ejercicio válido");

				var stockTotalUbicacion = await _storageControlContext.AcumuladoStockUbicacion
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
				var limEuros = operario?.MRH_LimiteInventarioEuros ?? 0m;

				// Descripción del artículo (siempre obtenerla del servicio)
				var descripcionArticulo = !string.IsNullOrEmpty(dto.CodigoArticulo)
					? await ObtenerDescripcionArticuloAsync(orden.CodigoEmpresa, dto.CodigoArticulo)
					: "";

				// Detectar si hay material paletizado en esta ubicación
				var materialPaletizado = await DetectarMaterialPaletizadoAsync(
					almacenOrden,
					dto.CodigoUbicacion,
					dto.CodigoArticulo,
					dto.LotePartida,
					dto.FechaCaducidad);

				// Datos de palet proporcionados por el cliente
				Guid? paletIdDetectado = dto.PaletId;
				string? codigoPaletDetectado = dto.CodigoPalet;
				string? codigoGS1Detectado = dto.CodigoGS1;
				var dtoProporcionoPalet = paletIdDetectado.HasValue ||
										   !string.IsNullOrWhiteSpace(codigoPaletDetectado) ||
										   !string.IsNullOrWhiteSpace(codigoGS1Detectado);

				// LOG: Verificar datos de palet recibidos del cliente
				_logger.LogInformation("📦 DATOS PALET RECIBIDOS - PaletId: {PaletId}, CodigoPalet: '{CodigoPalet}', CodigoGS1: '{CodigoGS1}', ProporcPalet: {ProporcPalet}",
					paletIdDetectado, codigoPaletDetectado ?? "NULL", codigoGS1Detectado ?? "NULL", dtoProporcionoPalet);

				// Complementar datos del palet solo si el DTO indicó alguno
				if (dtoProporcionoPalet && !paletIdDetectado.HasValue && materialPaletizado != null)
				{
					paletIdDetectado = materialPaletizado.PaletId;
					codigoPaletDetectado = materialPaletizado.CodigoPalet;
					codigoGS1Detectado = materialPaletizado.CodigoGS1;

					_logger.LogInformation("🔍 Palet detectado automáticamente para completar DTO: {CodigoPalet} (ID: {PaletId}) en ubicación {Ubicacion}",
						codigoPaletDetectado, paletIdDetectado, dto.CodigoUbicacion);
				}

				// Determinar el stock de referencia para la lectura
				decimal stockReferencia = stockTotalUbicacion;

				// Obtener información de palets y cantidades en la ubicación
				var paletsDisponiblesEnUbicacion = await DetectarTodosLosPaletsAsync(
					almacenOrden,
					dto.CodigoUbicacion,
					dto.CodigoArticulo,
					dto.LotePartida,
					dto.FechaCaducidad);

				var sumaPalets = paletsDisponiblesEnUbicacion.Sum(pl => pl.Cantidad);
				var remanente = stockTotalUbicacion - sumaPalets;
				if (remanente < 0m)
					remanente = 0m;

				var dtoSolicitaPalet = dtoProporcionoPalet;

				if (dtoSolicitaPalet)
				{
					var paletCoincidente = paletsDisponiblesEnUbicacion.FirstOrDefault(pl =>
						(paletIdDetectado.HasValue && pl.PaletId == paletIdDetectado.Value) ||
						(!string.IsNullOrWhiteSpace(codigoPaletDetectado) && pl.CodigoPalet == codigoPaletDetectado) ||
						(!string.IsNullOrWhiteSpace(codigoGS1Detectado) && pl.CodigoGS1 == codigoGS1Detectado))
						?? (paletsDisponiblesEnUbicacion.Count == 1 ? paletsDisponiblesEnUbicacion[0] : null);

					if (paletCoincidente != null)
					{
						// Palet existente encontrado: usar sus datos y cantidad específica
						paletIdDetectado = paletCoincidente.PaletId;
						codigoPaletDetectado = paletCoincidente.CodigoPalet;
						codigoGS1Detectado = paletCoincidente.CodigoGS1;
						stockReferencia = paletCoincidente.Cantidad;

						_logger.LogInformation("✅ Palet coincidente encontrado: {CodigoPalet} (ID: {PaletId}), Cantidad: {Cantidad}",
							codigoPaletDetectado, paletIdDetectado, stockReferencia);
					}
					else
					{
						// No hay palet existente, pero PRESERVAR los datos enviados por el cliente
						// Esto permite lecturas manuales con datos de palet que aún no existen físicamente
						stockReferencia = remanente;

						_logger.LogInformation("ℹ️ No se encontró palet coincidente en BD, pero se preservan datos del cliente: PaletId={PaletId}, Codigo={Codigo}, GS1={GS1}, StockReferencia={Stock}",
							paletIdDetectado, codigoPaletDetectado, codigoGS1Detectado, stockReferencia);
					}
				}
				else
				{
					if (!dtoProporcionoPalet && paletsDisponiblesEnUbicacion.Count == 1 && remanente <= 0.0001m)
					{
						var paletUnico = paletsDisponiblesEnUbicacion[0];
						paletIdDetectado = paletUnico.PaletId;
						codigoPaletDetectado = paletUnico.CodigoPalet;
						codigoGS1Detectado = paletUnico.CodigoGS1;
						stockReferencia = paletUnico.Cantidad;
					}
					else
					{
						paletIdDetectado = null;
						codigoPaletDetectado = null;
						codigoGS1Detectado = null;
						stockReferencia = remanente;
					}
				}

				// LOG: Datos finales que se guardarán en la lectura
				_logger.LogInformation("💾 DATOS FINALES LECTURA - PaletId: {PaletId}, CodigoPalet: '{CodigoPalet}', CodigoGS1: '{CodigoGS1}', StockRef: {StockRef}, CantContada: {CantContada}",
					paletIdDetectado, codigoPaletDetectado ?? "NULL", codigoGS1Detectado ?? "NULL", stockReferencia, dto.CantidadContada);

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
					CantidadStock = stockReferencia,
					UsuarioCodigo = dto.UsuarioCodigo,
					Comentario = dto.Comentario,
					Fecha = DateTime.Now,
					FechaCaducidad = dto.FechaCaducidad,
					// Información de palet (detectado automáticamente si no se proporcionó)
					PaletId = paletIdDetectado,
					CodigoPalet = codigoPaletDetectado,
					CodigoGS1 = codigoGS1Detectado
				};
				_context.LecturasConteo.Add(lectura);
				await _context.SaveChangesAsync();

				// Diferencia y acción
				var diferencia = (dto.CantidadContada ?? 0m) - stockReferencia;
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

					var superaUnidades = limUnidades > 0m && diferenciaAbs > limUnidades;
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
						FechaCaducidad = lectura.FechaCaducidad,
						// Información de palet (detectado automáticamente si no se proporcionó)
						PaletId = lectura.PaletId,
						CodigoPalet = lectura.CodigoPalet,
						CodigoGS1 = lectura.CodigoGS1
					};
					_context.ResultadosConteo.Add(resultado);

					await _context.SaveChangesAsync();

					// Si la acción es SUPERVISION, notificar
					if (accion == "SUPERVISION" && _notificacionesConteos != null)
					{
						// Notificar que el conteo se envió a supervisión
						try
						{
					await _notificacionesConteos.NotificarConteoSupervisionAsync(
						resultado.GuidID,
						resultado.CodigoArticulo ?? "Artículo desconocido",
						resultado.CantidadContada ?? 0m,
						resultado.UsuarioCodigo,
						orden.SupervisorCodigo
					);
						}
						catch (Exception ex)
						{
							_logger.LogError(ex, "Error al notificar supervisión para resultado {ResultadoGuid}", resultado.GuidID);
							// No fallar la operación si falla la notificación
						}
					}

					// Si la acción es AJUSTE, crear registro en InventarioAjustes (funcionalidad básica)
					if (accion == "AJUSTE")
					{
						_logger.LogInformation("🔧 Creando InventarioAjustes para resultado {ResultadoGuid} con diferencia {Diferencia}", resultado.GuidID, diferencia);

						// Normalizar FechaCaducidad para evitar problemas de conversión en SQL
						DateTime? fechaCaducidadNormalizada = resultado.FechaCaducidad.HasValue 
							? resultado.FechaCaducidad.Value.Date 
							: null;

						var inventarioAjuste = new InventarioAjustes
						{
							IdInventario = null, // Para ajustes de conteo no necesitamos InventarioCabecera
							CodigoArticulo = resultado.CodigoArticulo,
							CodigoUbicacion = resultado.CodigoUbicacion,
							Diferencia = resultado.Diferencia,
							UsuarioId = operario?.Id ?? int.Parse(resultado.UsuarioCodigo), // Usar operario.Id o parsear UsuarioCodigo
							Fecha = DateTime.Now,
							IdConteo = resultado.OrdenGuid,
							CodigoEmpresa = (short)orden.CodigoEmpresa, // Convertir int a short
							CodigoAlmacen = resultado.CodigoAlmacen,
							Estado = "PENDIENTE_ERP",
							FechaCaducidad = fechaCaducidadNormalizada,
							// Información de palet si existe
							PaletId = resultado.PaletId,
							CodigoPalet = resultado.CodigoPalet,
							CodigoGS1 = resultado.CodigoGS1,
							Partida = resultado.LotePartida
						};

						_context.InventarioAjustes.Add(inventarioAjuste);
						_logger.LogInformation("✅ InventarioAjustes agregado al contexto para resultado {ResultadoGuid}", resultado.GuidID);

						// ADICIONAL: Si hay palets en la ubicación, crear TempPaletLinea para consolidación unificada
						if (resultado.PaletId.HasValue)
						{
							var tempPaletLinea = new TempPaletLinea
							{
								Id = Guid.NewGuid(),
								PaletId = resultado.PaletId.Value,
								CodigoEmpresa = (short)orden.CodigoEmpresa,
								CodigoArticulo = resultado.CodigoArticulo,
								DescripcionArticulo = resultado.DescripcionArticulo,
								Cantidad = resultado.Diferencia, // DELTA (+/-)
								UnidadMedida = "UN", // Unidad por defecto
								Lote = resultado.LotePartida,
								FechaCaducidad = resultado.FechaCaducidad,
								CodigoAlmacen = resultado.CodigoAlmacen,
								Ubicacion = resultado.CodigoUbicacion,
								UsuarioId = operario?.Id ?? int.Parse(resultado.UsuarioCodigo),
								FechaAgregado = DateTime.Now,
								Observaciones = $"Ajuste de conteo - Orden: {orden.Titulo}",
								TraspasoId = null, // No es un traspaso
								ConteoId = resultado.OrdenGuid, // ID del conteo
								Procesada = false,
								EsHeredada = false
							};
							_context.TempPaletLineas.Add(tempPaletLinea);

							_logger.LogInformation("✅ Creada TempPaletLinea adicional para consolidación de palet: PaletId={PaletId}, Diferencia={Diferencia}, Articulo={Articulo}",
								resultado.PaletId, resultado.Diferencia, resultado.CodigoArticulo);
						}

						// Guardar ajustes ANTES de verificar lecturas pendientes
						await _context.SaveChangesAsync();
						_logger.LogInformation("💾 Ajustes guardados en BD para resultado {ResultadoGuid}", resultado.GuidID);
					}
				}

				// TEMPORAL: Comentar verificación de lecturas pendientes para debug
				_logger.LogInformation("🔍 Verificando lecturas pendientes para orden {OrdenGuid} con operario {Operario}", orden.GuidID, dto.UsuarioCodigo);

				var lecturasPendientes = await ObtenerLecturasPendientesAsync(orden.GuidID, dto.UsuarioCodigo);
				_logger.LogInformation("📊 Lecturas pendientes encontradas: {Count}", lecturasPendientes.Count());

				if (!lecturasPendientes.Any())
				{
					// No quedan lecturas pendientes, cerrar la orden automáticamente
					_logger.LogInformation("🔒 Cerrando orden {OrdenGuid} automáticamente - no quedan lecturas pendientes", orden.GuidID);
					orden.Estado = "CERRADO";
					orden.FechaCierre = DateTime.Now;
					await _context.SaveChangesAsync();

					_logger.LogInformation("✅ Orden {OrdenGuid} cerrada automáticamente al completar todas las lecturas", orden.GuidID);
				}
				else
				{
					_logger.LogInformation("⏳ Orden {OrdenGuid} mantiene estado EN_PROCESO - quedan {Count} lecturas pendientes", orden.GuidID, lecturasPendientes.Count());
				}

				_logger.LogInformation("💾 Confirmando transacción para orden {OrdenGuid}", orden.GuidID);
				await tx.CommitAsync();
				_logger.LogInformation("✅ Transacción confirmada para orden {OrdenGuid}", orden.GuidID);

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

                // Si hay lecturas, verificar que NO hay lecturas pendientes (todas deben estar contadas)
                if (orden.Lecturas.Any())
                {
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
                }

                // Obtener todas las lecturas completadas (puede ser 0 si no hay lecturas)
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

        public async Task<IEnumerable<LecturaResponseDto>> ObtenerLecturasRegistradasAsync(Guid ordenGuid, string? codigoOperario = null)
        {
            try
            {
                _logger.LogInformation("Obteniendo lecturas registradas para orden {OrdenGuid} con operario {Operario}", ordenGuid, codigoOperario);
                
                // Obtener la orden
                var orden = await _context.OrdenesConteo
                    .FirstOrDefaultAsync(o => o.GuidID == ordenGuid);
                
                if (orden == null)
                    throw new InvalidOperationException($"No se encontró la orden con Guid {ordenGuid}");

                // Obtener las lecturas registradas de la tabla LecturaConteo
                var query = _context.LecturasConteo
                    .Where(l => l.OrdenGuid == ordenGuid);

                // Aplicar filtro por operario si se especifica
                if (!string.IsNullOrEmpty(codigoOperario))
                {
                    query = query.Where(l => l.UsuarioCodigo == codigoOperario);
                }

                var lecturas = await query
                    .OrderBy(l => l.CodigoUbicacion)
                    .ThenBy(l => l.CodigoArticulo)
                    .ThenBy(l => l.LotePartida)
                    .ToListAsync();

                _logger.LogInformation("Se encontraron {Count} lecturas registradas para orden {OrdenGuid}", lecturas.Count, ordenGuid);

                return lecturas.Select(MapToLecturaResponseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtenerLecturasRegistradasAsync: {Message}", ex.Message);
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
                
                // Obtener almacén único para alcances que lo requieren (UBICACION, ESTANTERIA, etc.)
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
                    case "MULTIARTICULO":
                        // Obtener lista de artículos (soporta múltiples artículos)
                        List<string>? codigosArticulos = null;
                        
                        // Primero intentar desde CodigoArticulo (compatibilidad)
                        if (!string.IsNullOrEmpty(orden.CodigoArticulo))
                        {
                            codigosArticulos = new List<string> { orden.CodigoArticulo };
                        }
                        else
                        {
                            // Intentar extraer desde FiltrosJson (soporta formato nuevo y antiguo)
                            codigosArticulos = ExtraerArticulosDelFiltro(orden.FiltrosJson);
                        }
                        
                        if (codigosArticulos != null && codigosArticulos.Any())
                        {
                            query = query.Where(x => codigosArticulos.Contains(x.CodigoArticulo));
                            
                            // Intentar extraer lista de almacenes del filtro
                            var codigosAlmacenFiltro = ExtraerAlmacenesDelFiltro(orden.FiltrosJson);
                            
                            // Si no hay lista, intentar almacén único (compatibilidad)
                            if (codigosAlmacenFiltro == null || !codigosAlmacenFiltro.Any())
                            {
                                // Reutilizar la variable codigoAlmacen ya declarada en el ámbito del método
                                if (string.IsNullOrEmpty(codigoAlmacen))
                                {
                                    codigoAlmacen = orden.CodigoAlmacen;
                                    if (string.IsNullOrEmpty(codigoAlmacen))
                                    {
                                        codigoAlmacen = ExtraerAlmacenDelFiltro(orden.FiltrosJson);
                                    }
                                }
                                if (!string.IsNullOrEmpty(codigoAlmacen))
                                {
                                    codigosAlmacenFiltro = new List<string> { codigoAlmacen };
                                }
                            }
                            
                            // Si hay almacenes específicos en el filtro
                            if (codigosAlmacenFiltro != null && codigosAlmacenFiltro.Any())
                            {
                                // Filtrar por intersección: almacenes del filtro Y almacenes autorizados del operario
                                var almacenesFiltrados = codigosAlmacenFiltro
                                    .Where(a => almacenesAutorizados.Contains(a))
                                    .ToList();
                                
                                if (almacenesFiltrados.Any())
                                {
                                    query = query.Where(x => almacenesFiltrados.Contains(x.CodigoAlmacen));
                                    _logger.LogInformation("Pendientes ({Alcance}): filtrando {ArticulosCount} artículo(s) en {Count} almacenes específicos: {Almacenes}. Artículos: {Articulos}", 
                                        orden.Alcance, codigosArticulos.Count, almacenesFiltrados.Count, string.Join(", ", almacenesFiltrados), string.Join(", ", codigosArticulos));
                                }
                                else
                                {
                                    _logger.LogWarning("Operario {Operario} no tiene acceso a ninguno de los almacenes especificados: {Almacenes}", 
                                        codigoOperario, string.Join(", ", codigosAlmacenFiltro));
                                    return new List<LecturaResponseDto>();
                                }
                            }
                            else
                            {
                                // Si no se especifica almacén, filtrar por almacenes autorizados
                                if (almacenesAutorizados.Any())
                                {
                                    query = query.Where(x => almacenesAutorizados.Contains(x.CodigoAlmacen));
                                    _logger.LogInformation("Pendientes ({Alcance}): filtrando {ArticulosCount} artículo(s) en almacenes autorizados: {Almacenes}. Artículos: {Articulos}", 
                                        orden.Alcance, codigosArticulos.Count, string.Join(", ", almacenesAutorizados), string.Join(", ", codigosArticulos));
                                }
                                else
                                {
                                    _logger.LogInformation("Pendientes ({Alcance}): filtrando {ArticulosCount} artículo(s) en TODOS los almacenes (sin restricciones): {Articulos}", 
                                        orden.Alcance, codigosArticulos.Count, string.Join(", ", codigosArticulos));
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
                        
                        var rangoPasillo = ExtraerRangoPasilloDelFiltro(orden.FiltrosJson);
                        var rangoEstanteria = ExtraerRangoEstanteriaDelFiltro(orden.FiltrosJson);
                        
                        if (rangoPasillo.HasValue && rangoEstanteria.HasValue)
                        {
                            // Si ambos son valores únicos [1,1] y [5,5], usar lógica antigua (más rápida)
                            if (rangoPasillo.Value.desde == rangoPasillo.Value.hasta && 
                                rangoEstanteria.Value.desde == rangoEstanteria.Value.hasta)
                            {
                                var pasilloFormateado = rangoPasillo.Value.desde.ToString().PadLeft(3, '0');
                                var estanteriaFormateada = rangoEstanteria.Value.desde.ToString().PadLeft(3, '0');
                                var prefijoEstanteria = $"UB{pasilloFormateado}{estanteriaFormateada}";
                                query = query.Where(x => x.Ubicacion != null && x.Ubicacion.StartsWith(prefijoEstanteria));
                            }
                            else
                            {
                                // Rango real: marcar para filtrar después del switch
                                // No aplicar filtro aquí, se hará después de obtener los datos
                                _logger.LogInformation("Pendientes (ESTANTERIA): usando rangos. Se filtrará después de obtener datos base.");
                            }
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
                        
                        var rangoPasilloFiltro = ExtraerRangoPasilloDelFiltro(orden.FiltrosJson);
                        if (rangoPasilloFiltro.HasValue)
                        {
                            // Si es valor único [1, 1], usar lógica antigua (más rápida)
                            if (rangoPasilloFiltro.Value.desde == rangoPasilloFiltro.Value.hasta)
                            {
                                var prefijoPasillo = $"UB{rangoPasilloFiltro.Value.desde.ToString().PadLeft(3, '0')}";
                                query = query.Where(x => x.Ubicacion != null && x.Ubicacion.StartsWith(prefijoPasillo));
                            }
                            else
                            {
                                // Rango real: marcar para filtrar después del switch
                                // No aplicar filtro aquí, se hará después de obtener los datos
                                _logger.LogInformation("Pendientes (PASILLO): usando rangos. Se filtrará después de obtener datos base.");
                            }
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
                
                // Aplicar filtrado por rangos si es necesario (para casos ESTANTERIA y PASILLO con rangos reales)
                if (orden.Alcance?.ToUpper() == "ESTANTERIA" || orden.Alcance?.ToUpper() == "PASILLO")
                {
                    var rangoPasillo = ExtraerRangoPasilloDelFiltro(orden.FiltrosJson);
                    var rangoEstanteria = ExtraerRangoEstanteriaDelFiltro(orden.FiltrosJson);
                    
                    if (orden.Alcance?.ToUpper() == "ESTANTERIA" && rangoPasillo.HasValue && rangoEstanteria.HasValue)
                    {
                        // Si hay rangos reales (no valores únicos), filtrar
                        if (!(rangoPasillo.Value.desde == rangoPasillo.Value.hasta && 
                              rangoEstanteria.Value.desde == rangoEstanteria.Value.hasta))
                        {
                            stockData = stockData.Where(stock =>
                            {
                                var componentes = ExtraerComponentesUbicacion(stock.Ubicacion);
                                return componentes.HasValue &&
                                       componentes.Value.pasillo >= rangoPasillo.Value.desde &&
                                       componentes.Value.pasillo <= rangoPasillo.Value.hasta &&
                                       componentes.Value.estanteria >= rangoEstanteria.Value.desde &&
                                       componentes.Value.estanteria <= rangoEstanteria.Value.hasta;
                            }).ToList();
                            
                            _logger.LogInformation("Pendientes (ESTANTERIA): filtrado por rangos. Pasillo [{DesdePasillo}, {HastaPasillo}], Estantería [{DesdeEstanteria}, {HastaEstanteria}]. Resultados: {Count}",
                                rangoPasillo.Value.desde, rangoPasillo.Value.hasta,
                                rangoEstanteria.Value.desde, rangoEstanteria.Value.hasta,
                                stockData.Count);
                        }
                    }
                    else if (orden.Alcance?.ToUpper() == "PASILLO" && rangoPasillo.HasValue)
                    {
                        // Si hay rango real (no valor único), filtrar
                        if (rangoPasillo.Value.desde != rangoPasillo.Value.hasta)
                        {
                            stockData = stockData.Where(stock =>
                            {
                                var componentes = ExtraerComponentesUbicacion(stock.Ubicacion);
                                return componentes.HasValue &&
                                       componentes.Value.pasillo >= rangoPasillo.Value.desde &&
                                       componentes.Value.pasillo <= rangoPasillo.Value.hasta;
                            }).ToList();
                            
                            _logger.LogInformation("Pendientes (PASILLO): filtrado por rango [{DesdePasillo}, {HastaPasillo}]. Resultados: {Count}",
                                rangoPasillo.Value.desde, rangoPasillo.Value.hasta,
                                stockData.Count);
                        }
                    }
                }
                
                // Obtener lecturas ya creadas para excluirlas
                var lecturasCreadas = await _context.LecturasConteo
                    .Where(l => l.OrdenGuid == orden.GuidID)
                    .Select(l => new { l.CodigoAlmacen, l.CodigoUbicacion, l.CodigoArticulo, l.LotePartida, l.PaletId })
                    .ToListAsync();

                foreach (var stock in stockData)
                {
                    if (EsUbicacionValidaParaConteo(stock.Ubicacion))
                    {
                        // Verificar si ya existe una lectura para esta combinación (sin palet)
                        var yaExisteLecturaSinPalet = lecturasCreadas.Any(l => 
                            l.CodigoAlmacen == stock.CodigoAlmacen &&
                            l.CodigoUbicacion == stock.Ubicacion &&
                            l.CodigoArticulo == stock.CodigoArticulo &&
                            (string.IsNullOrEmpty(stock.Partida) || l.LotePartida == stock.Partida) &&
                            l.PaletId == null);

                        var descripcionArticulo = await ObtenerDescripcionArticuloAsync(orden.CodigoEmpresa, stock.CodigoArticulo);

                        // Detectar si hay material paletizado en esta ubicación
                        var paletsDisponibles = await DetectarTodosLosPaletsAsync(
                            stock.CodigoAlmacen, 
                            stock.Ubicacion, 
                            stock.CodigoArticulo, 
                            stock.Partida, 
                            stock.FechaCaducidad);

                        decimal totalCantidadPalets = 0m;

                        if (paletsDisponibles.Any())
                        {
                            // Si hay múltiples palets, crear una lectura por cada palet
                            foreach (var palet in paletsDisponibles)
                            {
                                totalCantidadPalets += palet.Cantidad;

                                // Verificar si ya existe una lectura para este palet específico
                                var yaExistePalet = lecturasCreadas.Any(l => 
                                    l.CodigoAlmacen == stock.CodigoAlmacen &&
                                    l.CodigoUbicacion == stock.Ubicacion &&
                                    l.CodigoArticulo == stock.CodigoArticulo &&
                                    (string.IsNullOrEmpty(stock.Partida) || l.LotePartida == stock.Partida) &&
                                    l.PaletId == palet.PaletId);
                                
                                if (!yaExistePalet)
                                {
                                    lecturasGeneradas.Add(new LecturaResponseDto
                                    {
                                        GuidID = Guid.Empty, // No se persiste, es dinámico
                                        OrdenGuid = orden.GuidID,
                                        CodigoAlmacen = stock.CodigoAlmacen,
                                        CodigoUbicacion = stock.Ubicacion,
                                        CodigoArticulo = stock.CodigoArticulo,
                                        DescripcionArticulo = descripcionArticulo,
                                        LotePartida = stock.Partida,
                                        CantidadContada = null, // Pendiente de conteo
                                        CantidadStock = palet.Cantidad, // Cantidad específica del palet
                                        UsuarioCodigo = codigoOperario ?? "",
                                        Fecha = DateTime.Now,
                                        Comentario = null,
                                        FechaCaducidad = stock.FechaCaducidad,
                                        // Información específica del palet
                                        PaletId = palet.PaletId,
                                        CodigoPalet = palet.CodigoPalet,
                                        CodigoGS1 = palet.CodigoGS1
                                    });
                                }
                            }

                            // Generar lectura adicional para el remanente sin palet (si existe)
                            var cantidadRestante = stock.UnidadSaldo - totalCantidadPalets;
                            if (cantidadRestante > 0.0001m && !yaExisteLecturaSinPalet)
                            {
                                lecturasGeneradas.Add(new LecturaResponseDto
                                {
                                    GuidID = Guid.Empty,
                                    OrdenGuid = orden.GuidID,
                                    CodigoAlmacen = stock.CodigoAlmacen,
                                    CodigoUbicacion = stock.Ubicacion,
                                    CodigoArticulo = stock.CodigoArticulo,
                                    DescripcionArticulo = descripcionArticulo,
                                    LotePartida = stock.Partida,
                                    CantidadContada = null,
                                    CantidadStock = cantidadRestante,
                                    UsuarioCodigo = codigoOperario ?? "",
                                    Fecha = DateTime.Now,
                                    Comentario = null,
                                    FechaCaducidad = stock.FechaCaducidad,
                                    PaletId = null,
                                    CodigoPalet = null,
                                    CodigoGS1 = null
                                });
                            }
                        }
                        else if (!yaExisteLecturaSinPalet)
                        {
                            // Si no hay palets, crear lectura normal (sin palet)
                            lecturasGeneradas.Add(new LecturaResponseDto
                            {
                                GuidID = Guid.Empty, // No se persiste, es dinámico
                                OrdenGuid = orden.GuidID,
                                CodigoAlmacen = stock.CodigoAlmacen,
                                CodigoUbicacion = stock.Ubicacion,
                                CodigoArticulo = stock.CodigoArticulo,
                                DescripcionArticulo = descripcionArticulo,
                                LotePartida = stock.Partida,
                                CantidadContada = null, // Pendiente de conteo
                                CantidadStock = stock.UnidadSaldo,
                                UsuarioCodigo = codigoOperario ?? "",
                                Fecha = DateTime.Now,
                                Comentario = null,
                                FechaCaducidad = stock.FechaCaducidad,
                                // Sin información de palet
                                PaletId = null,
                                CodigoPalet = null,
                                CodigoGS1 = null
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
				Visibilidad = resultado.Orden?.Visibilidad ?? string.Empty,
				CreadoPorCodigo = resultado.Orden?.CreadoPorCodigo
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
                    Titulo = GenerarTituloReasignacion(resultado.Orden.Titulo),
                    Visibilidad = resultado.Orden.Visibilidad,
                    ModoGeneracion = "REASIGNA", // Solo 10 caracteres máximo
                    Alcance = "UBICACION", // Mantener UBICACION pero con filtros específicos de artículo
                    FiltrosJson = GenerarFiltrosJsonParaReasignacion(resultado),
                    FechaPlan = DateTime.Now, // Usar hora del servidor local en lugar de UTC
                    SupervisorCodigo = dto.SupervisorCodigo,
                    CreadoPorCodigo = dto.SupervisorCodigo ?? "SISTEMA",
                    Estado = "ASIGNADO",
                    Prioridad = 5, // Mayor prioridad para reasignaciones
                    FechaCreacion = DateTime.Now, // Usar hora del servidor local en lugar de UTC
                    CodigoOperario = dto.CodigoOperario,
                    FechaAsignacion = DateTime.Now, // Usar hora del servidor local en lugar de UTC
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
                ["almacen"] = resultado.CodigoAlmacen,
                ["ubicacion"] = resultado.CodigoUbicacion ?? string.Empty,
                ["articulo"] = resultado.CodigoArticulo ?? string.Empty,
                ["tipo"] = "LINEA_ESPECIFICA", // Especificar que es para una línea específica
                ["modo"] = "REASIGNACION" // Indicar que es una reasignación
            };

            return System.Text.Json.JsonSerializer.Serialize(filtros);
        }

        /// <summary>
        /// Genera el título para una reasignación con formato R, R1, R2, etc.
        /// Detecta si el título ya tiene prefijo de reasignación y lo incrementa.
        /// También maneja compatibilidad con el formato antiguo "REASIGNACIÓN - ".
        /// </summary>
        private string GenerarTituloReasignacion(string tituloOriginal)
        {
            if (string.IsNullOrWhiteSpace(tituloOriginal))
            {
                return "R - Reasignación";
            }

            // Patrón para detectar "R" o "R{N}" al inicio, seguido de " - "
            // También detecta "REASIGNACIÓN - " para compatibilidad
            var patronR = new Regex(@"^(R(\d+)?|REASIGNACIÓN)\s*-\s*(.+)$", RegexOptions.IgnoreCase);
            var match = patronR.Match(tituloOriginal.Trim());

            if (match.Success)
            {
                // Ya tiene prefijo de reasignación
                var prefijo = match.Groups[1].Value.ToUpper();
                var tituloBase = match.Groups[3].Value.Trim();

                // Si es "REASIGNACIÓN", convertir a "R1"
                if (prefijo == "REASIGNACIÓN")
                {
                    return $"R1 - {tituloBase}";
                }

                // Si es "R" (sin número), convertir a "R1"
                if (prefijo == "R")
                {
                    return $"R1 - {tituloBase}";
                }

                // Si es "R{N}", extraer el número e incrementarlo
                if (prefijo.StartsWith("R") && match.Groups[2].Success)
                {
                    if (int.TryParse(match.Groups[2].Value, out int numeroActual))
                    {
                        int nuevoNumero = numeroActual + 1;
                        return $"R{nuevoNumero} - {tituloBase}";
                    }
                }
            }

            // No tiene prefijo de reasignación, primera reasignación
            return $"R - {tituloOriginal.Trim()}";
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

        /// <summary>
        /// Detecta si hay material paletizado en una ubicación específica
        /// </summary>
        private async Task<MaterialPaletizadoInfo?> DetectarMaterialPaletizadoAsync(
            string codigoAlmacen, 
            string? ubicacion, 
            string? codigoArticulo, 
            string? lote, 
            DateTime? fechaCaducidad)
        {
            try
            {
                // Buscar en PaletLineas si hay material paletizado en esa ubicación
                var paletLinea = await _context.PaletLineas
                    .Where(pl => pl.CodigoAlmacen == codigoAlmacen &&
                               pl.Ubicacion == ubicacion &&
                               (string.IsNullOrEmpty(codigoArticulo) || pl.CodigoArticulo == codigoArticulo) &&
                               (string.IsNullOrEmpty(lote) || pl.Lote == lote) &&
                               (fechaCaducidad == null || pl.FechaCaducidad == fechaCaducidad) &&
                               pl.Cantidad > 0) // Solo líneas con cantidad positiva
                    .FirstOrDefaultAsync();

                if (paletLinea != null)
                {
                    // Obtener información del palet
                    var palet = await _context.Palets
                        .Where(p => p.Id == paletLinea.PaletId)
                        .Select(p => new { p.Codigo, p.CodigoGS1 })
                        .FirstOrDefaultAsync();

                    if (palet != null)
                    {
                        return new MaterialPaletizadoInfo
                        {
                            PaletId = paletLinea.PaletId,
                            CodigoPalet = palet.Codigo,
                            CodigoGS1 = palet.CodigoGS1
                        };
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detectando material paletizado para ubicación {Ubicacion}, artículo {Articulo}", ubicacion, codigoArticulo);
                return null;
            }
        }


        /// <summary>
        /// Obtiene todos los palets disponibles en una ubicación específica (método público)
        /// </summary>
        public async Task<List<PaletDisponibleInfo>> ObtenerPaletsDisponiblesAsync(
            string codigoAlmacen, 
            string? ubicacion, 
            string? codigoArticulo, 
            string? lote, 
            DateTime? fechaCaducidad)
        {
            return await DetectarTodosLosPaletsAsync(codigoAlmacen, ubicacion, codigoArticulo, lote, fechaCaducidad);
        }

        /// <summary>
        /// Detecta TODOS los palets disponibles en una ubicación específica
        /// </summary>
        private async Task<List<PaletDisponibleInfo>> DetectarTodosLosPaletsAsync(
            string codigoAlmacen, 
            string? ubicacion, 
            string? codigoArticulo, 
            string? lote, 
            DateTime? fechaCaducidad)
        {
            try
            {
                // Buscar TODOS los palets en esa ubicación
                _logger.LogInformation("🔍 Buscando palets en ubicación: {Ubicacion}, artículo: {CodigoArticulo}, lote: {Lote}, fechaCaducidad: {FechaCaducidad}", 
                    ubicacion, codigoArticulo, lote, fechaCaducidad);
                
                var paletLineas = await _context.PaletLineas
                    .Where(pl => pl.CodigoAlmacen == codigoAlmacen &&
                               pl.Ubicacion == ubicacion &&
                               (string.IsNullOrEmpty(codigoArticulo) || pl.CodigoArticulo == codigoArticulo) &&
                               (string.IsNullOrEmpty(lote) || pl.Lote == lote) &&
                               (fechaCaducidad == null || pl.FechaCaducidad == fechaCaducidad) &&
                               pl.Cantidad > 0)
                    .ToListAsync();
                
                _logger.LogInformation("📦 Encontradas {Count} líneas de palet", paletLineas.Count);

                var palets = new List<PaletDisponibleInfo>();

                foreach (var paletLinea in paletLineas)
                {
                    // Obtener información del palet
                    var palet = await _context.Palets
                        .Where(p => p.Id == paletLinea.PaletId)
                        .Select(p => new { p.Codigo, p.CodigoGS1, p.Estado })
                        .FirstOrDefaultAsync();

                    if (palet != null)
                    {
                        _logger.LogInformation("✅ Palet encontrado: {CodigoPalet}, GS1: {CodigoGS1}, Cantidad: {Cantidad}", 
                            palet.Codigo, palet.CodigoGS1, paletLinea.Cantidad);
                        
                        palets.Add(new PaletDisponibleInfo
                        {
                            PaletId = paletLinea.PaletId,
                            CodigoPalet = palet.Codigo,
                            CodigoGS1 = palet.CodigoGS1,
                            Cantidad = paletLinea.Cantidad,
                            Estado = palet.Estado
                        });
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Palet no encontrado para PaletId: {PaletId}", paletLinea.PaletId);
                    }
                }

                return palets;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detectando todos los palets para ubicación {Ubicacion}, artículo {Articulo}", ubicacion, codigoArticulo);
                return new List<PaletDisponibleInfo>();
            }
        }

        // Métodos para conteos periódicos
        public async Task<IEnumerable<ConteoPeriodicoDto>> ListarConteosPeriodicosAsync(
            string? codigoAlmacen = null,
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null,
            bool? activo = null,
            string? codigoOperario = null,
            string? codigoOperarioSesion = null,
            string? creadoPorCodigo = null)
        {
            try
            {
                _logger.LogInformation("Iniciando ListarConteosPeriodicosAsync con codigoAlmacen: {CodigoAlmacen}, fechaDesde: {FechaDesde}, fechaHasta: {FechaHasta}, activo: {Activo}, operario: {Operario}, operarioSesion: {OperarioSesion}, creadoPorCodigo: {CreadoPorCodigo}",
                    codigoAlmacen, fechaDesde, fechaHasta, activo, codigoOperario, codigoOperarioSesion, creadoPorCodigo);

                var query = _context.OrdenesConteo
                    .Where(o => o.EsPeriodico == true)
                    .AsQueryable();

                // Aplicar filtro por almacén si se especifica
                if (!string.IsNullOrEmpty(codigoAlmacen) && codigoAlmacen != "Todas")
                {
                    query = query.Where(o => o.CodigoAlmacen == codigoAlmacen);
                }

                // Aplicar filtro de fecha desde si se especifica
                if (fechaDesde.HasValue)
                {
                    query = query.Where(o => o.FechaCreacion.Date >= fechaDesde.Value.Date);
                }

                // Aplicar filtro de fecha hasta si se especifica
                if (fechaHasta.HasValue)
                {
                    query = query.Where(o => o.FechaCreacion.Date <= fechaHasta.Value.Date);
                }

                // Aplicar filtro por activo/inactivo si se especifica
                if (activo.HasValue)
                {
                    query = query.Where(o => o.Activo == activo.Value);
                }

                // Aplicar filtro de operario si se especifica (filtro visual)
                if (!string.IsNullOrEmpty(codigoOperario))
                {
                    query = query.Where(o => o.CodigoOperario == codigoOperario);
                }

                // Aplicar filtro por creador (solo propios vs ver todos)
                if (!string.IsNullOrEmpty(creadoPorCodigo))
                {
                    query = query.Where(o => o.CreadoPorCodigo == creadoPorCodigo);
                }

                // Si se proporciona el código de operario de la sesión, filtrar por almacenes autorizados
                List<OrdenConteo> ordenesPeriodicas;
                if (!string.IsNullOrEmpty(codigoOperarioSesion) && int.TryParse(codigoOperarioSesion, out int operarioIdSesion))
                {
                    // Obtener todas las órdenes periódicas primero para obtener el código de empresa
                    var ordenesTemporales = await query.ToListAsync();
                    if (!ordenesTemporales.Any())
                    {
                        return new List<ConteoPeriodicoDto>();
                    }
                    
                    // Obtener el código de empresa de la primera orden (todas deberían tener el mismo)
                    var codigoEmpresa = ordenesTemporales.First().CodigoEmpresa;
                    if (codigoEmpresa == 0) codigoEmpresa = 1; // Por defecto empresa 1
                    
                    var almacenesAutorizados = await ObtenerAlmacenesAutorizadosAsync(operarioIdSesion, codigoEmpresa);
                    
                    if (almacenesAutorizados.Any())
                    {
                        // Filtrar conteos periódicos que tengan almacén en los autorizados
                        // O que no tengan almacén específico (alcance general)
                        ordenesPeriodicas = ordenesTemporales.Where(o => 
                            (o.CodigoAlmacen != null && almacenesAutorizados.Contains(o.CodigoAlmacen)) ||
                            (o.CodigoAlmacen == null && o.Alcance != "ALMACEN")).ToList();
                    }
                    else
                    {
                        // Si el operario no tiene almacenes autorizados, no mostrar ningún conteo
                        _logger.LogWarning("Operario {Operario} no tiene almacenes autorizados para conteos periódicos", codigoOperarioSesion);
                        return new List<ConteoPeriodicoDto>();
                    }
                }
                else
                {
                    // Si no hay filtro de operario de sesión, obtener todas las órdenes periódicas filtradas
                    ordenesPeriodicas = await query.ToListAsync();
                }

                var resultado = new List<ConteoPeriodicoDto>();

                foreach (var orden in ordenesPeriodicas)
                {
                    // Contar renovaciones (órdenes hijas)
                    var totalRenovaciones = await _context.OrdenesConteo
                        .CountAsync(o => o.OrdenPadreGuid == orden.GuidID);

                    resultado.Add(new ConteoPeriodicoDto
                    {
                        GuidID = orden.GuidID,
                        CodigoEmpresa = orden.CodigoEmpresa,
                        Titulo = orden.Titulo,
                        FrecuenciaDias = orden.FrecuenciaDias,
                        FechaUltimaRenovacion = orden.FechaUltimaRenovacion,
                        FechaProximaRenovacion = orden.FechaProximaRenovacion,
                        Activo = orden.Activo,
                        Estado = orden.Estado,
                        CodigoOperario = orden.CodigoOperario,
                        CodigoAlmacen = orden.CodigoAlmacen,
                        Alcance = orden.Alcance,
                        CreadoPorCodigo = orden.CreadoPorCodigo,
                        Prioridad = orden.Prioridad,
                        FechaCreacion = orden.FechaCreacion,
                        TotalRenovaciones = totalRenovaciones
                    });
                }

                return resultado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar conteos periódicos");
                throw;
            }
        }

        public async Task ActivarPeriodicidadAsync(Guid guid)
        {
            try
            {
                var orden = await _context.OrdenesConteo.FirstOrDefaultAsync(o => o.GuidID == guid);
                if (orden == null)
                    throw new InvalidOperationException($"No se encontró la orden con Guid {guid}");

                if (!orden.EsPeriodico)
                    throw new InvalidOperationException("La orden no es un conteo periódico");

                orden.Activo = true;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Periodicidad activada para orden {Guid}", guid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al activar periodicidad para orden {Guid}", guid);
                throw;
            }
        }

        public async Task DesactivarPeriodicidadAsync(Guid guid)
        {
            try
            {
                var orden = await _context.OrdenesConteo.FirstOrDefaultAsync(o => o.GuidID == guid);
                if (orden == null)
                    throw new InvalidOperationException($"No se encontró la orden con Guid {guid}");

                if (!orden.EsPeriodico)
                    throw new InvalidOperationException("La orden no es un conteo periódico");

                orden.Activo = false;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Periodicidad desactivada para orden {Guid}", guid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al desactivar periodicidad para orden {Guid}", guid);
                throw;
            }
        }

        public async Task<IEnumerable<OrdenDto>> ObtenerRenovacionesAsync(Guid guid)
        {
            try
            {
                var renovaciones = await _context.OrdenesConteo
                    .Where(o => o.OrdenPadreGuid == guid)
                    .OrderByDescending(o => o.FechaCreacion)
                    .ToListAsync();

                return renovaciones.Select(MapToOrdenDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener renovaciones para orden {Guid}", guid);
                throw;
            }
        }

        public async Task<IEnumerable<string>> ObtenerCreadoresConteosAsync()
        {
            try
            {
                var creadoresCodigos = await _context.OrdenesConteo
                    .Where(o => !string.IsNullOrEmpty(o.CreadoPorCodigo) && o.CreadoPorCodigo != "0")
                    .Select(o => o.CreadoPorCodigo!)
                    .Distinct()
                    .ToListAsync();

                return creadoresCodigos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener creadores de conteos");
                throw;
            }
        }

        public async Task<IEnumerable<string>> ObtenerCreadoresConteosPeriodicosAsync()
        {
            try
            {
                var creadoresCodigos = await _context.OrdenesConteo
                    .Where(o => o.EsPeriodico == true && 
                                !string.IsNullOrEmpty(o.CreadoPorCodigo) && 
                                o.CreadoPorCodigo != "0")
                    .Select(o => o.CreadoPorCodigo!)
                    .Distinct()
                    .ToListAsync();

                return creadoresCodigos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener creadores de conteos periódicos");
                throw;
            }
        }

        public async Task<OrdenDto> RenovarConteoPeriodicoAsync(Guid guid)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _logger.LogInformation("Iniciando renovación manual de conteo periódico {Guid}", guid);

                var ordenOriginal = await _context.OrdenesConteo
                    .FirstOrDefaultAsync(o => o.GuidID == guid);

                if (ordenOriginal == null)
                {
                    throw new InvalidOperationException($"No se encontró la orden de conteo con Guid {guid}");
                }

                if (!ordenOriginal.EsPeriodico)
                {
                    throw new InvalidOperationException($"La orden {guid} no es un conteo periódico");
                }

                if (!ordenOriginal.Activo)
                {
                    throw new InvalidOperationException($"El conteo periódico {guid} está desactivado y no puede ser renovado");
                }

                // Crear nueva orden basada en la original
                var fechaRenovacionStr = DateTime.Now.ToString("dd/MM/yyyy");
                var tituloConFecha = $"{ordenOriginal.Titulo} - {fechaRenovacionStr}";
                
                var nuevaOrden = new OrdenConteo
                {
                    CodigoEmpresa = ordenOriginal.CodigoEmpresa,
                    Titulo = tituloConFecha,
                    Visibilidad = ordenOriginal.Visibilidad,
                    ModoGeneracion = ordenOriginal.ModoGeneracion,
                    Alcance = ordenOriginal.Alcance,
                    FiltrosJson = ordenOriginal.FiltrosJson,
                    FechaPlan = DateTime.Now,
                    FechaEjecucion = null,
                    SupervisorCodigo = ordenOriginal.SupervisorCodigo,
                    CreadoPorCodigo = ordenOriginal.CreadoPorCodigo,
                    Estado = string.IsNullOrEmpty(ordenOriginal.CodigoOperario) ? "PLANIFICADO" : "ASIGNADO",
                    Prioridad = ordenOriginal.Prioridad,
                    FechaCreacion = DateTime.Now,
                    CodigoOperario = ordenOriginal.CodigoOperario,
                    FechaAsignacion = !string.IsNullOrEmpty(ordenOriginal.CodigoOperario) ? DateTime.Now : null,
                    CodigoAlmacen = ordenOriginal.CodigoAlmacen,
                    CodigoUbicacion = ordenOriginal.CodigoUbicacion,
                    CodigoArticulo = ordenOriginal.CodigoArticulo,
                    DescripcionArticulo = ordenOriginal.DescripcionArticulo,
                    LotePartida = ordenOriginal.LotePartida,
                    CantidadTeorica = ordenOriginal.CantidadTeorica,
                    Comentario = ordenOriginal.Comentario,
                    EsPeriodico = false,
                    FrecuenciaDias = null,
                    Activo = true,
                    OrdenPadreGuid = ordenOriginal.GuidID
                };

                _context.OrdenesConteo.Add(nuevaOrden);

                // Actualizar orden original
                var fechaRenovacion = DateTime.Now;
                // Guardar la fecha anterior antes de actualizarla para usarla en el cálculo
                var fechaUltimaRenovacionAnterior = ordenOriginal.FechaUltimaRenovacion;
                ordenOriginal.FechaUltimaRenovacion = fechaRenovacion;
                if (ordenOriginal.FrecuenciaDias.HasValue)
                {
                    // Calcular próxima renovación desde la fecha de la última renovación (o fecha de creación si es la primera)
                    // para mantener el mismo día de la semana en las renovaciones
                    var fechaBase = fechaUltimaRenovacionAnterior.HasValue 
                        ? fechaUltimaRenovacionAnterior.Value.Date 
                        : ordenOriginal.FechaCreacion.Date;
                    
                    ordenOriginal.FechaProximaRenovacion = fechaBase.AddDays(ordenOriginal.FrecuenciaDias.Value);
                    
                    _logger.LogInformation("Próxima renovación calculada desde {FechaBase} + {Frecuencia} días = {FechaProximaRenovacion}", 
                        fechaBase, ordenOriginal.FrecuenciaDias.Value, ordenOriginal.FechaProximaRenovacion);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Conteo periódico {GuidOriginal} renovado manualmente exitosamente. Nueva orden creada: {GuidNueva}", 
                    ordenOriginal.GuidID, nuevaOrden.GuidID);

                return MapToOrdenDto(nuevaOrden);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al renovar conteo periódico {Guid}", guid);
                throw;
            }
        }

    }

    /// <summary>
    /// Información de material paletizado detectado
    /// </summary>
    public class MaterialPaletizadoInfo
    {
        public Guid PaletId { get; set; }
        public string CodigoPalet { get; set; } = string.Empty;
        public string? CodigoGS1 { get; set; }
    }

    /// <summary>
    /// Información de palet con cantidad disponible
    /// </summary>
    public class PaletDisponibleInfo
    {
        public Guid PaletId { get; set; }
        public string CodigoPalet { get; set; } = string.Empty;
        public string? CodigoGS1 { get; set; }
        public decimal Cantidad { get; set; }
        public string Estado { get; set; } = string.Empty;
    }
}
