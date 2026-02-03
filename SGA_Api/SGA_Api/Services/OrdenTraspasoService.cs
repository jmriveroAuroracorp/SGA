using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.OrdenTraspaso;
using SGA_Api.Models.Conteos;
using SGA_Api.Models.Inventario;
using SGA_Api.Models.Palet;
using SGA_Api.Models.Stock;
using SGA_Api.Services;
using Microsoft.Data.SqlClient;
using System;
using System.Data;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace SGA_Api.Services
{
    public class OrdenTraspasoService : IOrdenTraspasoService
    {
		private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> _lineaLocks = new();
		private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> _ordenLocks = new();
        private readonly AuroraSgaDbContext _context;
        private readonly SageDbContext _sageContext;
        private readonly ILogger<OrdenTraspasoService> _logger;
        private readonly INotificacionesOrdenTraspasoService _notificacionesOrdenTraspaso;

        public OrdenTraspasoService(
            AuroraSgaDbContext context,
            SageDbContext sageContext,
            ILogger<OrdenTraspasoService> logger,
            INotificacionesOrdenTraspasoService notificacionesOrdenTraspaso)
        {
            _context = context;
            _sageContext = sageContext;
            _logger = logger;
            _notificacionesOrdenTraspaso = notificacionesOrdenTraspaso;
        }

        public async Task<IEnumerable<OrdenTraspasoDto>> GetOrdenesTraspasoAsync(short? codigoEmpresa = null, string? estado = null)
        {
            var query = _context.OrdenTraspasoCabecera
                .Include(o => o.Lineas)
                .AsQueryable();

            if (codigoEmpresa.HasValue)
                query = query.Where(o => o.CodigoEmpresa == codigoEmpresa.Value);

            if (!string.IsNullOrEmpty(estado))
                query = query.Where(o => o.Estado == estado);


            var ordenes = await query.OrderByDescending(o => o.FechaCreacion).ToListAsync();

            return ordenes.Select(MapToDto);
        }

        public async Task<OrdenTraspasoDto?> GetOrdenTraspasoAsync(Guid id)
        {
            return await GetOrdenTraspasoAsync(id, prepararLineas: false);
        }

        public async Task<OrdenTraspasoDto?> GetOrdenTraspasoAsync(Guid id, bool prepararLineas = false)
        {
            var orden = await _context.OrdenTraspasoCabecera
                .Include(o => o.Lineas)
                .FirstOrDefaultAsync(o => o.IdOrdenTraspaso == id);

            if (orden == null) return null;

            if (prepararLineas || orden.Estado == "EN_PROCESO")
            {
                await PrepararLineasInicialesAsync(orden);
                // Siempre recargar líneas después de prepararlas para incluir las nuevas líneas hijas creadas
                await RecargarLineasOrdenAsync(orden);
            }

            return MapToDto(orden);
        }

        public async Task<OrdenTraspasoDto> CrearOrdenTraspasoAsync(CrearOrdenTraspasoDto dto)
        {
            var orden = new OrdenTraspasoCabecera
            {
                CodigoEmpresa = dto.CodigoEmpresa,
                Estado = "PENDIENTE",
                Prioridad = dto.Prioridad,
                FechaPlan = dto.FechaPlan,
                TipoOrigen = dto.TipoOrigen,
                UsuarioCreacion = dto.UsuarioCreacion,
                Comentarios = dto.Comentarios,
                FechaCreacion = DateTime.Now,
                CodigoOrden = await GenerarCodigoOrdenAsync(dto.CodigoEmpresa),
                CodigoAlmacenDestino = dto.CodigoAlmacenDestino
            };

            _context.OrdenTraspasoCabecera.Add(orden);

            // Agregar líneas
            foreach (var lineaDto in dto.Lineas)
            {
                // Debug: verificar IdOperarioAsignado
                System.Diagnostics.Debug.WriteLine($"API - Línea: {lineaDto.CodigoArticulo} - IdOperarioAsignado: {lineaDto.IdOperarioAsignado}");
                var linea = new OrdenTraspasoLinea
                {
                    IdOrdenTraspaso = orden.IdOrdenTraspaso,
                    NumeroLinea = lineaDto.Orden,
                    CodigoArticulo = lineaDto.CodigoArticulo,
                    DescripcionArticulo = lineaDto.DescripcionArticulo,
                    FechaCaducidad = lineaDto.FechaCaducidad,
                    CantidadPlan = lineaDto.CantidadPlan,
                    CodigoAlmacenOrigen = lineaDto.CodigoAlmacenOrigen,
                    UbicacionOrigen = lineaDto.UbicacionOrigen,
                    Partida = lineaDto.Partida,
                    PaletOrigen = lineaDto.PaletOrigen,
                    CodigoAlmacenDestino = lineaDto.CodigoAlmacenDestino,
                    UbicacionDestino = lineaDto.UbicacionDestino,
                    PaletDestino = lineaDto.PaletDestino,
                    Estado = "PENDIENTE",
                    CantidadMovida = 0,
                    Completada = false,
                    IdOperarioAsignado = lineaDto.IdOperarioAsignado
                };

                _context.OrdenTraspasoLinea.Add(linea);
            }

            await _context.SaveChangesAsync();

            // Verificar si todas las líneas tienen operarios asignados
            var todasLasLineasTienenOperario = orden.Lineas.All(l => l.IdOperarioAsignado > 0);
            if (!todasLasLineasTienenOperario)
            {
                orden.Estado = "SIN_ASIGNAR";
                await _context.SaveChangesAsync();
            }

            // Notificar creación de orden
            try
            {
                await _notificacionesOrdenTraspaso.NotificarOrdenCreadaAsync(
                    orden.IdOrdenTraspaso,
                    orden.UsuarioCreacion,
                    orden.CodigoEmpresa,
                    orden.CodigoOrden,
                    orden.CodigoAlmacenDestino);
            }
            catch (Exception ex)
            {
                // Log pero no fallar la operación
                // Las notificaciones no deben bloquear la creación de la orden
            }

            return MapToDto(orden);
        }

        public async Task<bool> ActualizarOrdenTraspasoAsync(Guid id, ActualizarOrdenTraspasoDto dto)
        {
            var orden = await _context.OrdenTraspasoCabecera.FindAsync(id);
            if (orden == null) return false;

            if (!string.IsNullOrEmpty(dto.Estado))
                orden.Estado = dto.Estado;

            if (dto.Prioridad.HasValue)
                orden.Prioridad = dto.Prioridad.Value;

            if (dto.FechaPlan.HasValue)
                orden.FechaPlan = dto.FechaPlan.Value;


            if (dto.Comentarios != null)
                orden.Comentarios = dto.Comentarios;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<OrdenTraspasoDto?> ActualizarLineaOrdenTraspasoAsync(Guid id, ActualizarLineaOrdenTraspasoDto dto)
        {
            var linea = await _context.OrdenTraspasoLinea.FindAsync(id);
            if (linea == null) return null;

            if (!string.IsNullOrEmpty(dto.Estado))
                linea.Estado = dto.Estado;

            if (dto.CantidadMovida.HasValue)
                linea.CantidadMovida = dto.CantidadMovida.Value;

            if (!string.IsNullOrWhiteSpace(dto.PaletOrigen))
            {
                var paletOrigenInput = dto.PaletOrigen.Trim();

                // Si viene un GUID, interpretamos que es el Id del palet y guardamos el CÓDIGO,
                // igual que hacemos con PaletDestino / CódigoPalet para mantener coherencia funcional.
                if (Guid.TryParse(paletOrigenInput, out var paletId))
                {
                    var palet = await _context.Palets.FindAsync(paletId);
                    linea.PaletOrigen = palet?.Codigo ?? paletOrigenInput; // fallback al texto original si no se encuentra
                }
                else
                {
                    // Si ya viene como código, lo guardamos tal cual normalizado
                    linea.PaletOrigen = paletOrigenInput;
                }
            }

            if (dto.PaletDestino != null)
                linea.PaletDestino = dto.PaletDestino.Trim();

            // 🔷 Buscar el traspaso CORRECTO para esta línea específica (no confiar en el que envía Android)
            if (dto.IdTraspaso.HasValue)
            {
                var traspasoRecibido = await _context.Traspasos.FindAsync(dto.IdTraspaso.Value);
                if (traspasoRecibido != null)
                {
                    // Buscar entre todos los traspasos del mismo palet el que coincida con esta línea
                    var traspasoCorrectoCandidatos = await _context.Traspasos
                        .Where(t => t.PaletId == traspasoRecibido.PaletId &&
                                    t.CodigoArticulo == linea.CodigoArticulo &&
                                    (t.Partida ?? "") == (linea.Partida ?? ""))
                        .ToListAsync();

                    var traspasoCorrectoId = dto.IdTraspaso.Value; // Por defecto, usar el que envía Android

                    if (traspasoCorrectoCandidatos.Count > 1)
                    {
                        // Si hay varios candidatos, buscar el que coincida exactamente por cantidad y ubicación
                        var traspasoExacto = traspasoCorrectoCandidatos
                            .FirstOrDefault(t =>
                                t.Cantidad == linea.CantidadMovida &&
                                (t.UbicacionOrigen ?? "") == (linea.UbicacionOrigen ?? ""));

                        if (traspasoExacto != null)
                        {
                            traspasoCorrectoId = traspasoExacto.Id;
                            _logger.LogInformation(
                                "ActualizarLineaOrdenTraspasoAsync: Encontrado traspaso exacto {TraspasoCorrectoId} para línea {LineaId}. " +
                                "Android envió {TraspasoRecibidoId}. Articulo={Articulo}, Cantidad={Cantidad}, UbicOrigen={UbicOrigen}",
                                traspasoCorrectoId, id, dto.IdTraspaso.Value,
                                linea.CodigoArticulo, linea.CantidadMovida, linea.UbicacionOrigen);
                        }
                        else
                        {
                            // Buscar por ubicación origen solamente
                            var traspasoPorUbicacion = traspasoCorrectoCandidatos
                                .FirstOrDefault(t => (t.UbicacionOrigen ?? "") == (linea.UbicacionOrigen ?? ""));

                            if (traspasoPorUbicacion != null)
                            {
                                traspasoCorrectoId = traspasoPorUbicacion.Id;
                                _logger.LogInformation(
                                    "ActualizarLineaOrdenTraspasoAsync: Encontrado traspaso por ubicación {TraspasoCorrectoId} para línea {LineaId}. " +
                                    "Android envió {TraspasoRecibidoId}. Articulo={Articulo}, UbicOrigen={UbicOrigen}",
                                    traspasoCorrectoId, id, dto.IdTraspaso.Value,
                                    linea.CodigoArticulo, linea.UbicacionOrigen);
                            }
                            else
                            {
                                _logger.LogWarning(
                                    "ActualizarLineaOrdenTraspasoAsync: No se encontró traspaso exacto para línea {LineaId}. " +
                                    "Usando el enviado por Android {TraspasoRecibidoId}. Candidatos={Candidatos}",
                                    id, dto.IdTraspaso.Value, traspasoCorrectoCandidatos.Count);
                            }
                        }
                    }
                    else if (traspasoCorrectoCandidatos.Count == 1)
                    {
                        traspasoCorrectoId = traspasoCorrectoCandidatos[0].Id;
                        if (traspasoCorrectoId != dto.IdTraspaso.Value)
                        {
                            _logger.LogInformation(
                                "ActualizarLineaOrdenTraspasoAsync: Un solo candidato {TraspasoCorrectoId} para línea {LineaId}. " +
                                "Android envió {TraspasoRecibidoId}",
                                traspasoCorrectoId, id, dto.IdTraspaso.Value);
                        }
                    }

                    linea.IdTraspaso = traspasoCorrectoId;
                }
                else
                {
                    // Si no se encuentra el traspaso, asignar el que envía Android (comportamiento anterior)
                    linea.IdTraspaso = dto.IdTraspaso.Value;
                    _logger.LogWarning(
                        "ActualizarLineaOrdenTraspasoAsync: No se encontró el traspaso {TraspasoId} enviado por Android. LineaId={LineaId}",
                        dto.IdTraspaso.Value, id);
                }
            }

            if (dto.FechaFinalizacion.HasValue)
                linea.FechaFinalizacion = dto.FechaFinalizacion.Value;

            // Si se marca como COMPLETADA, establecer FechaFinalizacion si no está
            if (dto.Estado == "COMPLETADA" && !linea.FechaFinalizacion.HasValue)
            {
                linea.FechaFinalizacion = DateTime.Now;
            }

            if (dto.Estado == "COMPLETADA")
            {
                linea.Completada = true;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ActualizarLineaOrdenTraspasoAsync: fallo al guardar línea {LineaId}", id);
                throw;
            }

            // 🔷 Si se está asignando IdTraspaso en esta actualización y hay PaletOrigen, actualizar la TempPaletLinea negativa del palet origen
            // El IdTraspaso se asigna cuando se completa/ubica el palet, no cuando se añade la línea al palet
            if (dto.IdTraspaso.HasValue && !string.IsNullOrWhiteSpace(linea.PaletOrigen))
            {
                // Buscar el palet origen por su código
                var paletOrigen = await _context.Palets
                    .FirstOrDefaultAsync(p => p.Codigo == linea.PaletOrigen);
                
                if (paletOrigen != null)
                {
                    // Buscar la TempPaletLinea negativa del palet origen que coincida
                    var tempPaletLineaNegativa = await _context.TempPaletLineas
                        .FirstOrDefaultAsync(t => 
                            t.PaletId == paletOrigen.Id &&
                            t.CodigoArticulo == linea.CodigoArticulo &&
                            (t.Lote ?? "") == (linea.Partida ?? "") &&
                            t.Cantidad == -linea.CantidadMovida && // Cantidad negativa
                            t.TraspasoId == null && // Aún no asignado
                            !t.Procesada);
                    
                    if (tempPaletLineaNegativa != null)
                    {
                        // Asignar el TraspasoId a la línea temporal negativa
                        tempPaletLineaNegativa.TraspasoId = linea.IdTraspaso.Value;
                        _context.TempPaletLineas.Update(tempPaletLineaNegativa);
                        await _context.SaveChangesAsync();
                        
                        _logger.LogInformation(
                            "ActualizarLineaOrdenTraspasoAsync: Actualizada TempPaletLinea negativa {TempId} del palet origen {PaletOrigen} con TraspasoId {TraspasoId}",
                            tempPaletLineaNegativa.Id, linea.PaletOrigen, linea.IdTraspaso.Value);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "ActualizarLineaOrdenTraspasoAsync: No se encontró TempPaletLinea negativa para actualizar. PaletOrigen={PaletOrigen}, Articulo={Articulo}, Partida={Partida}, Cantidad={Cantidad}",
                            linea.PaletOrigen, linea.CodigoArticulo, linea.Partida, linea.CantidadMovida);
                    }
                }
            }

            // Si se completó la línea, verificar si es línea hija de una SUBDIVIDIDA
            if (dto.Estado == "COMPLETADA")
            {
                var lineaPadre = await _context.OrdenTraspasoLinea
                    .Where(l => l.CodigoArticulo == linea.CodigoArticulo &&
                               l.IdOrdenTraspaso == linea.IdOrdenTraspaso &&
                               l.Estado == "SUBDIVIDIDA" &&
                               l.IdLineaOrdenTraspaso != id)
                    .FirstOrDefaultAsync();

                if (lineaPadre != null)
                {
                    // Verificar si la línea padre está completamente satisfecha
                    var cantidadCompletada = await _context.OrdenTraspasoLinea
                        .Where(l => l.CodigoArticulo == lineaPadre.CodigoArticulo &&
                                   l.IdOrdenTraspaso == lineaPadre.IdOrdenTraspaso &&
                                   l.Estado == "COMPLETADA" &&
                                   l.IdLineaOrdenTraspaso != lineaPadre.IdLineaOrdenTraspaso)
                        .SumAsync(l => l.CantidadMovida);

                    if (cantidadCompletada >= lineaPadre.CantidadPlan)
                    {
                        // Línea padre completamente satisfecha, marcarla como completada
                        lineaPadre.Estado = "COMPLETADA";
                        lineaPadre.FechaFinalizacion = DateTime.Now;
                        lineaPadre.Completada = true;
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        // Crear siguiente línea hija automáticamente
                        var hijaCreada = await CrearLineaHijaAsync(lineaPadre, linea);

                        // Si no se pudo crear la línea hija por falta de stock, marcar la línea padre como SIN_STOCK
                        if (!hijaCreada)
                        {
                            _logger.LogWarning("ActualizarLineaOrdenTraspasoAsync: No se pudo crear línea hija adicional para {Art} en orden {Orden} - marcando línea padre como SIN_STOCK",
                                lineaPadre.CodigoArticulo, lineaPadre.IdOrdenTraspaso);

                            lineaPadre.Estado = "SIN_STOCK";
                            lineaPadre.FechaFinalizacion = DateTime.Now;
                            lineaPadre.Completada = false;
                            await _context.SaveChangesAsync();

                            // Verificar si la orden debe cambiar de estado después de marcar esta línea como SIN_STOCK
                            await VerificarCompletitudOrdenAsync(lineaPadre.IdOrdenTraspaso);
                        }
                    }
                }

                // Verificar si la orden debe completarse
                await VerificarCompletitudOrdenAsync(linea.IdOrdenTraspaso);
            }

            // Lógica para actualizar el estado de la orden si todas las líneas tienen operario o no
            var orden = await _context.OrdenTraspasoCabecera
                .Include(o => o.Lineas)
                .FirstOrDefaultAsync(o => o.IdOrdenTraspaso == linea.IdOrdenTraspaso);
            
            if (orden != null)
            {
                var todasLasLineasTienenOperario = orden.Lineas.All(l => l.IdOperarioAsignado > 0 || l.Estado == "CANCELADA" || l.Estado == "COMPLETADA" || l.Estado == "SUBDIVIDIDA");
                var algunaLineaSinOperario = orden.Lineas.Any(l => l.IdOperarioAsignado <= 0 && l.Estado != "CANCELADA" && l.Estado != "COMPLETADA" && l.Estado != "SUBDIVIDIDA");

                if (todasLasLineasTienenOperario && orden.Estado == "SIN_ASIGNAR")
                {
                    orden.Estado = "PENDIENTE";
                    await _context.SaveChangesAsync();
                }
                else if (algunaLineaSinOperario && orden.Estado == "PENDIENTE")
                {
                    orden.Estado = "SIN_ASIGNAR";
                    await _context.SaveChangesAsync();
                }
            }

            // Recargar la orden completa con todas las líneas actualizadas
            var ordenActualizada = await _context.OrdenTraspasoCabecera
                .Include(o => o.Lineas)
                .FirstOrDefaultAsync(o => o.IdOrdenTraspaso == linea.IdOrdenTraspaso);

            return ordenActualizada != null ? MapToDto(ordenActualizada) : null;
        }

        public async Task<LineaOrdenTraspasoDetalleDto?> CrearLineaOrdenTraspasoAsync(Guid idOrden, CrearLineaOrdenTraspasoDto dto)
        {
            // Verificar que la orden existe
            var orden = await _context.OrdenTraspasoCabecera.FindAsync(idOrden);
            if (orden == null) return null;

            // Obtener el siguiente número de línea
            var siguienteNumero = await _context.OrdenTraspasoLinea
                .Where(l => l.IdOrdenTraspaso == idOrden)
                .MaxAsync(l => (int?)l.NumeroLinea) ?? 0;

            var linea = new OrdenTraspasoLinea
            {
                IdOrdenTraspaso = idOrden,
                NumeroLinea = siguienteNumero + 1,
                CodigoArticulo = dto.CodigoArticulo,
                DescripcionArticulo = dto.DescripcionArticulo,
                FechaCaducidad = dto.FechaCaducidad,
                CantidadPlan = dto.CantidadPlan,
                CodigoAlmacenOrigen = dto.CodigoAlmacenOrigen,
                UbicacionOrigen = dto.UbicacionOrigen,
                Partida = dto.Partida,
                PaletOrigen = dto.PaletOrigen,
                CodigoAlmacenDestino = dto.CodigoAlmacenDestino,
                UbicacionDestino = dto.UbicacionDestino,
                PaletDestino = dto.PaletDestino,
                Estado = dto.Estado ?? "PENDIENTE",
                CantidadMovida = 0,
                Completada = false,
                IdOperarioAsignado = dto.IdOperarioAsignado
            };

            _context.OrdenTraspasoLinea.Add(linea);
            await _context.SaveChangesAsync();

            // Verificar si la orden debe cambiar de estado
            var todasLasLineasTienenOperario = await _context.OrdenTraspasoLinea
                .Where(l => l.IdOrdenTraspaso == idOrden)
                .AllAsync(l => l.IdOperarioAsignado > 0 || l.Estado == "CANCELADA" || l.Estado == "COMPLETADA" || l.Estado == "SUBDIVIDIDO");

            if (!todasLasLineasTienenOperario && orden.Estado == "PENDIENTE")
            {
                orden.Estado = "SIN_ASIGNAR";
                await _context.SaveChangesAsync();
            }
            else if (todasLasLineasTienenOperario && orden.Estado == "SIN_ASIGNAR")
            {
                orden.Estado = "PENDIENTE";
                await _context.SaveChangesAsync();
            }

            return MapLineaToDto(linea);
        }

        public async Task<bool> CompletarOrdenTraspasoAsync(Guid id)
        {
            var orden = await _context.OrdenTraspasoCabecera.FindAsync(id);
            if (orden == null) return false;

            orden.Estado = "COMPLETADA";
            orden.FechaFinalizacion = DateTime.Now;

            await _context.SaveChangesAsync();

            // Notificar finalización de orden
            try
            {
                await _notificacionesOrdenTraspaso.NotificarOrdenCompletadaAsync(
                    id,
                    orden.UsuarioCreacion,
                    orden.CodigoEmpresa,
                    orden.CodigoOrden);
            }
            catch (Exception ex)
            {
                // Log pero no fallar la operación
            }

            return true;
        }

        public async Task<bool> CancelarOrdenTraspasoAsync(Guid id)
        {
            var orden = await _context.OrdenTraspasoCabecera
                .Include(o => o.Lineas)
                .FirstOrDefaultAsync(o => o.IdOrdenTraspaso == id);
            
            if (orden == null) 
                return false;

            // Validación: Solo se puede cancelar si está PENDIENTE o SIN_ASIGNAR
            if (orden.Estado != "PENDIENTE" && orden.Estado != "SIN_ASIGNAR")
                return false;

            // Validación: No se puede cancelar si ya hay movimientos realizados
            var tieneMovimientos = orden.Lineas.Any(l => l.CantidadMovida > 0);
            if (tieneMovimientos)
                return false;

            orden.Estado = "CANCELADA";
            orden.FechaFinalizacion = DateTime.Now;

            // Cancelar solo las líneas que están pendientes o sin asignar
            foreach (var linea in orden.Lineas)
            {
                if (linea.Estado == "PENDIENTE" || linea.Estado == "SIN_ASIGNAR")
                {
                    linea.Estado = "CANCELADA";
                    linea.Completada = false;
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> CancelarLineasPendientesAsync(Guid idOrden)
        {
            var orden = await _context.OrdenTraspasoCabecera
                .Include(o => o.Lineas)
                .FirstOrDefaultAsync(o => o.IdOrdenTraspaso == idOrden);
            
            if (orden == null) 
                return false;

            // Solo se puede cancelar líneas si la orden está EN_PROCESO
            if (orden.Estado != "EN_PROCESO")
                return false;

            // Cancelar solo las líneas que están pendientes o sin asignar
            var lineasCanceladas = 0;
            foreach (var linea in orden.Lineas)
            {
                if (linea.Estado == "PENDIENTE" || linea.Estado == "SIN_ASIGNAR")
                {
                    linea.Estado = "CANCELADA";
                    linea.Completada = false;
                    lineasCanceladas++;
                }
            }

            // Si se cancelaron líneas, verificar si todas las líneas restantes están completadas
            if (lineasCanceladas > 0)
            {
                var todasCompletadas = orden.Lineas.All(l => 
                    l.Estado == "COMPLETADA" || l.Estado == "CANCELADA" || l.Estado == "SUBDIVIDIDO");
                
                if (todasCompletadas)
                {
                    orden.Estado = "COMPLETADA";
                    orden.FechaFinalizacion = DateTime.Now;
                }
            }

            await _context.SaveChangesAsync();
            return lineasCanceladas > 0;
        }

        public async Task<bool> EliminarOrdenTraspasoAsync(Guid id)
        {
            var orden = await _context.OrdenTraspasoCabecera.FindAsync(id);
            if (orden == null) return false;

            _context.OrdenTraspasoCabecera.Remove(orden);
            await _context.SaveChangesAsync();
            return true;
        }


        private static OrdenTraspasoDto MapToDto(OrdenTraspasoCabecera orden)
        {
            return new OrdenTraspasoDto
            {
                IdOrdenTraspaso = orden.IdOrdenTraspaso,
                CodigoEmpresa = orden.CodigoEmpresa,
                Estado = orden.Estado,
                Prioridad = orden.Prioridad,
                FechaPlan = orden.FechaPlan,
                FechaInicio = orden.FechaInicio,
                FechaFinalizacion = orden.FechaFinalizacion,
                TipoOrigen = orden.TipoOrigen,
                UsuarioCreacion = orden.UsuarioCreacion,
                Comentarios = orden.Comentarios,
                FechaCreacion = orden.FechaCreacion,
                CodigoOrden = orden.CodigoOrden,
                CodigoAlmacenDestino = orden.CodigoAlmacenDestino,
                Lineas = orden.Lineas.Select(MapLineaToDto).ToList()
            };
        }

        private static LineaOrdenTraspasoDetalleDto MapLineaToDto(OrdenTraspasoLinea linea)
        {
            return new LineaOrdenTraspasoDetalleDto
            {
                IdLineaOrden = linea.IdLineaOrdenTraspaso,
                IdOrdenTraspaso = linea.IdOrdenTraspaso,
                Orden = linea.NumeroLinea,
                CodigoArticulo = linea.CodigoArticulo,
                DescripcionArticulo = linea.DescripcionArticulo,
                FechaCaducidad = linea.FechaCaducidad,
                CantidadPlan = linea.CantidadPlan,
                CodigoAlmacenOrigen = linea.CodigoAlmacenOrigen,
                UbicacionOrigen = linea.UbicacionOrigen,
                Partida = linea.Partida,
                PaletOrigen = linea.PaletOrigen,
                CodigoAlmacenDestino = linea.CodigoAlmacenDestino,
                UbicacionDestino = linea.UbicacionDestino,
                PaletDestino = linea.PaletDestino,
                Estado = linea.Estado,
                CantidadMovida = linea.CantidadMovida,
                Completada = linea.Completada,
                IdOperarioAsignado = linea.IdOperarioAsignado,
                FechaInicio = linea.FechaInicio,
                FechaFinalizacion = linea.FechaFinalizacion,
                IdTraspaso = linea.IdTraspaso
            };
        }

        private async Task<string> GenerarCodigoOrdenAsync(short codigoEmpresa)
        {
            try
            {
                var pCodigoEmpresa = new SqlParameter("@CodigoEmpresa", SqlDbType.SmallInt) { Value = codigoEmpresa };
                var pNuevoCodigo = new SqlParameter("@NuevoCodigo", SqlDbType.VarChar, 50) { Direction = ParameterDirection.Output };

                await _context.Database.ExecuteSqlRawAsync(
                    "EXEC dbo.CrearOrdenTraspaso @CodigoEmpresa, @NuevoCodigo OUTPUT",
                    pCodigoEmpresa, pNuevoCodigo);

                var codigoGenerado = (string)pNuevoCodigo.Value!;
                return codigoGenerado; // Formato: 2025/OTR/0000001
            }
            catch (Exception ex)
            {
                // Fallback: generar código manual si falla el stored procedure
                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                return $"OTR-{codigoEmpresa:D2}-{timestamp}";
            }
        }

        // IMPLEMENTACIÓN DE NUEVOS MÉTODOS PARA EL FLUJO DE ANDROID

        public async Task<IEnumerable<OrdenTraspasoDto>> GetOrdenesPorOperarioAsync(int idOperario, short codigoEmpresa)
        {
            var ordenes = await _context.OrdenTraspasoCabecera
                .Include(o => o.Lineas)
                .Where(o => o.CodigoEmpresa == codigoEmpresa && 
                           o.Estado != "CANCELADA" && // Excluir órdenes canceladas
                           o.Lineas.Any(l => l.IdOperarioAsignado == idOperario))
                .OrderByDescending(o => o.Prioridad)
                .ThenBy(o => o.FechaPlan)
                .ToListAsync();

            foreach (var orden in ordenes)
            {
                if (orden.Estado == "EN_PROCESO")
                {
                    var huboCambios = await PrepararLineasInicialesAsync(orden);
                    if (huboCambios)
                    {
                        await RecargarLineasOrdenAsync(orden);
                    }
                }
            }

            return ordenes.Select(MapToDto);
        }

        public async Task<OrdenTraspasoDto?> IniciarOrdenAsync(Guid id, int idOperario)
        {
            var orden = await _context.OrdenTraspasoCabecera
                .Include(o => o.Lineas)
                .FirstOrDefaultAsync(o => o.IdOrdenTraspaso == id);

            if (orden == null) return null;

            // Verificar que el operario tiene líneas asignadas en esta orden
            var tieneLineasAsignadas = orden.Lineas.Any(l => l.IdOperarioAsignado == idOperario);
            if (!tieneLineasAsignadas) return null;

            // Si ya está en proceso, devolver la orden actualizada (idempotencia)
            if (orden.Estado == "EN_PROCESO")
            {
                await PrepararLineasInicialesAsync(orden);
                await RecargarLineasOrdenAsync(orden);
                return MapToDto(orden);
            }

            // Solo se puede iniciar si está PENDIENTE
            if (orden.Estado != "PENDIENTE") return null;

            orden.Estado = "EN_PROCESO";
            orden.FechaInicio = DateTime.Now;

            await _context.SaveChangesAsync();

            await PrepararLineasInicialesAsync(orden);
            await RecargarLineasOrdenAsync(orden);

            return MapToDto(orden);
        }

        public async Task<IEnumerable<StockLineaTraspasoDto>?> GetStockLineaAsync(Guid idLinea)
        {
            var linea = await _context.OrdenTraspasoLinea
                .Include(l => l.OrdenTraspaso)
                .FirstOrDefaultAsync(l => l.IdLineaOrdenTraspaso == idLinea);

            if (linea == null) return null;

            // 1. VERIFICAR SI ES LÍNEA PADRE SUBDIVIDIDA
            if (linea.Estado == "SUBDIVIDIDA")
            {
                // Calcular cantidad completada por líneas hijas
                var cantidadCompletada = await _context.OrdenTraspasoLinea
                    .Where(l => l.CodigoArticulo == linea.CodigoArticulo &&
                               l.IdOrdenTraspaso == linea.IdOrdenTraspaso &&
                               l.Estado == "COMPLETADA" &&
                               l.IdLineaOrdenTraspaso != idLinea)
                    .SumAsync(l => l.CantidadMovida);

                var cantidadRestante = linea.CantidadPlan - cantidadCompletada;

                // Si todavía falta cantidad, verificar si hay línea hija activa (PENDIENTE o EN_PROCESO)
                if (cantidadRestante > 0)
                {
                    var lineaHijaActiva = await _context.OrdenTraspasoLinea
                        .Where(l => l.CodigoArticulo == linea.CodigoArticulo &&
                                   l.IdOrdenTraspaso == linea.IdOrdenTraspaso &&
                                   (l.Estado == "PENDIENTE" || l.Estado == "EN_PROCESO") &&
                                   l.IdLineaOrdenTraspaso != idLinea)
                        .FirstOrDefaultAsync();

                    // Si NO hay línea hija activa, crear una nueva
                    if (lineaHijaActiva == null)
                    {
                        await CrearLineaHijaAsync(linea);
                    }
                }
            }

            // 2. OBTENER ALMACENES PERMITIDOS AL OPERARIO
        var almacenesPermitidos = await ObtenerAlmacenesAutorizadosAsync(
            linea.IdOperarioAsignado, 
            linea.OrdenTraspaso.CodigoEmpresa);

        _logger.LogInformation("OrdenTraspasoService: Operario {Operario} tiene {Count} almacenes autorizados para empresa {Empresa}: {Almacenes}",
            linea.IdOperarioAsignado,
            almacenesPermitidos.Count,
            linea.OrdenTraspaso.CodigoEmpresa,
            string.Join(", ", almacenesPermitidos));

            var stockQuery = _context.StockDisponible
                .Where(s => s.CodigoEmpresa == linea.OrdenTraspaso.CodigoEmpresa &&
                           s.CodigoArticulo == linea.CodigoArticulo);

			var stockArticulo = await stockQuery.ToListAsync();

		var almacenDestinoNormalizado = NormalizeAlmacen(linea.OrdenTraspaso.CodigoAlmacenDestino);
		if (!string.IsNullOrEmpty(almacenDestinoNormalizado))
		{
			stockArticulo = stockArticulo
				.Where(s => !string.Equals(NormalizeAlmacen(s.CodigoAlmacen), almacenDestinoNormalizado, StringComparison.OrdinalIgnoreCase))
				.ToList();
		}

		// Excluir almacén 004 (camión de transporte entre almacenes)
		stockArticulo = stockArticulo
			.Where(s => !string.Equals(NormalizeAlmacen(s.CodigoAlmacen), "004", StringComparison.OrdinalIgnoreCase))
			.ToList();

		var ubicacionesPulmon = await ObtenerUbicacionesPulmonAsync(
			linea.OrdenTraspaso.CodigoEmpresa,
			stockArticulo.Select(s => ((string?)s.CodigoAlmacen, (string?)s.Ubicacion)));

		if (ubicacionesPulmon.Count > 0)
		{
			stockArticulo = stockArticulo
				.Where(s => !ubicacionesPulmon.Contains(NormalizeUbicacionKey(s.CodigoAlmacen, s.Ubicacion)))
				.ToList();
		}

		// Excluir artículos/partidas bloqueados por calidad
		var bloqueosCalidad = await ObtenerBloqueosCalidadAsync(
			linea.OrdenTraspaso.CodigoEmpresa,
			stockArticulo.Select(s => ((string?)s.CodigoArticulo, (string?)s.Partida, (string?)s.CodigoAlmacen, (string?)s.Ubicacion)));

		if (bloqueosCalidad.Count > 0)
		{
			stockArticulo = stockArticulo
				.Where(s => !bloqueosCalidad.Contains($"{(s.CodigoArticulo ?? string.Empty).Trim().ToUpperInvariant()}|{(s.Partida ?? string.Empty).Trim().ToUpperInvariant()}|{(s.CodigoAlmacen ?? string.Empty).Trim().ToUpperInvariant()}|{(s.Ubicacion ?? string.Empty).Trim().ToUpperInvariant()}"))
				.ToList();
			_logger.LogInformation("GetStockLineaAsync: Excluidos {Count} artículos/partidas bloqueados por calidad", bloqueosCalidad.Count);
		}

        var almacenesPermitidosNormalizados = almacenesPermitidos
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a.Trim().ToUpperInvariant())
            .ToHashSet();

            var almacenesStockNormalizados = stockArticulo
                .Select(s => new
                {
                    Original = s.CodigoAlmacen ?? string.Empty,
                    Normalizado = (s.CodigoAlmacen ?? string.Empty).Trim().ToUpperInvariant()
                })
                .Distinct()
                .ToList();

            _logger.LogInformation(
                "OrdenTraspasoService: Línea {LineaId}. Almacenes permitidos normalizados ({CountPermisos}): {Permisos}. Almacenes presentes en stock ({CountStock}): {Stock}",
                linea.IdLineaOrdenTraspaso,
                almacenesPermitidosNormalizados.Count,
                string.Join(", ", almacenesPermitidosNormalizados),
                almacenesStockNormalizados.Count,
                string.Join(", ", almacenesStockNormalizados.Select(a => $"{a.Original}->{a.Normalizado}"))
            );

            var stockDataQuery = stockArticulo
                .Where(s => almacenesPermitidosNormalizados.Contains((s.CodigoAlmacen ?? string.Empty).Trim().ToUpperInvariant()));
            var stockData = new List<StockDisponible>();

            if (!string.IsNullOrWhiteSpace(linea.Partida))
            {
                var partidaNormalizada = linea.Partida.Trim().ToUpperInvariant();
                stockData = stockDataQuery
                    // Primero priorizar la partida indicada
                    .OrderBy(s => (s.Partida ?? string.Empty).Trim().ToUpperInvariant() == partidaNormalizada ? 0 : 1)
                    .ThenBy(s => s.FechaCaducidad)  // Luego FEFO
                    .ThenBy(s => ((s.CodigoAlmacen ?? string.Empty).Trim() + "|" + (s.Ubicacion ?? string.Empty).Trim()).ToUpperInvariant())
                    .ToList();
            }
            else
            {
                stockData = stockDataQuery
                    .OrderBy(s => s.FechaCaducidad)  // PRIMERO: FEFO (fecha de caducidad)
                    .ThenBy(s => ((s.CodigoAlmacen ?? string.Empty).Trim() + "|" + (s.Ubicacion ?? string.Empty).Trim()).ToUpperInvariant())
                    .ToList();
            }

            if (!stockData.Any())
            {
                _logger.LogWarning(
                    "OrdenTraspasoService: Línea {LineaId}. StockData vacío tras filtrar. Permisos={Permisos}. Stock={Stock}",
                    linea.IdLineaOrdenTraspaso,
                    string.Join(", ", almacenesPermitidosNormalizados),
                    string.Join(", ", almacenesStockNormalizados.Select(a => $"{a.Original}->{a.Normalizado}"))
                );
            }

            // 4. CASO 1: Si la línea no tiene ubicación asignada (null), asignar la mejor disponible
            if (linea.UbicacionOrigen == null && linea.Estado == "PENDIENTE" && stockData.Any())
            {
                var mejorUbicacion = stockData
                    .Where(s => s.Disponible > 0)
                    .FirstOrDefault();
                
                if (mejorUbicacion != null)
                {
                    linea.UbicacionOrigen = mejorUbicacion.Ubicacion;
                    linea.CodigoAlmacenOrigen = mejorUbicacion.CodigoAlmacen;
                    linea.Partida = mejorUbicacion.Partida;
                    linea.FechaCaducidad = mejorUbicacion.FechaCaducidad;
                    await _context.SaveChangesAsync();
                }
            }
            // CASO: Si la línea tiene ubicación asignada (incluso si es ""), verificar si todavía tiene stock
            else if (linea.UbicacionOrigen != null && linea.Estado == "PENDIENTE")
            {
                var stockUbicacionOriginal = stockData
                    .FirstOrDefault(s => 
                        (linea.UbicacionOrigen == "" ? (s.Ubicacion == null || s.Ubicacion == "") : s.Ubicacion == linea.UbicacionOrigen) && 
                        (string.IsNullOrEmpty(linea.Partida) || s.Partida == linea.Partida));
                
                // Si la ubicación original no tiene stock suficiente o no existe, actualizar
                if (stockUbicacionOriginal == null || stockUbicacionOriginal.Disponible < linea.CantidadPlan)
                {
                    var mejorUbicacion = stockData
                        .Where(s => s.Disponible > 0)
                        .FirstOrDefault();
                    
                    if (mejorUbicacion != null)
                    {
                        linea.UbicacionOrigen = mejorUbicacion.Ubicacion;
                        linea.CodigoAlmacenOrigen = mejorUbicacion.CodigoAlmacen;
                        linea.Partida = mejorUbicacion.Partida;
                        linea.FechaCaducidad = mejorUbicacion.FechaCaducidad;
                        await _context.SaveChangesAsync();
                    }
                }
            }

            // 5. CASO 2: VERIFICAR SI NECESITA DESGLOSE (stock insuficiente)
            // Solo subdividir si NO hay ninguna ubicación con stock suficiente
            if (linea.Estado == "PENDIENTE")
            {
                // Verificar si hay alguna ubicación con stock suficiente
                var hayUbicacionConStockSuficiente = stockData
                    .Any(s => s.Disponible >= linea.CantidadPlan);

                // Si NO hay ninguna ubicación con stock suficiente, verificar stock total
                if (!hayUbicacionConStockSuficiente)
                {
                    var cantidadTotalDisponible = stockData.Sum(s => s.Disponible);
                    if (cantidadTotalDisponible < linea.CantidadPlan)
                    {
                        // Stock insuficiente en total, iniciar desglose
                        await IniciarDesgloseAsync(linea);
                    }
                }
                // Si hay ubicación con stock suficiente, NO subdividir (ya se asignó arriba)
            }

            var resultado = new List<StockLineaTraspasoDto>();

            if (!string.IsNullOrEmpty(linea.CodigoAlmacenOrigen) && linea.UbicacionOrigen != null)
            {
                // Preparar información de stock suelto y paletizado para la ubicación asignada
                var stockSeleccionado = await _context.StockDisponible.FirstOrDefaultAsync(s =>
                    s.CodigoEmpresa == linea.OrdenTraspaso.CodigoEmpresa &&
                    s.CodigoArticulo == linea.CodigoArticulo &&
                    s.CodigoAlmacen == linea.CodigoAlmacenOrigen &&
                    (linea.UbicacionOrigen == "" ? (s.Ubicacion == null || s.Ubicacion == "") : s.Ubicacion == linea.UbicacionOrigen) &&
                    (string.IsNullOrEmpty(linea.Partida) || s.Partida == linea.Partida));

                var paletsEnUbicacion = await _context.PaletLineas
                    .Include(pl => pl.Palet)
                    .Where(pl => pl.CodigoEmpresa == linea.OrdenTraspaso.CodigoEmpresa &&
                                  pl.CodigoArticulo == linea.CodigoArticulo &&
                                  pl.CodigoAlmacen == linea.CodigoAlmacenOrigen &&
                                  (linea.UbicacionOrigen == "" ? (pl.Ubicacion == null || pl.Ubicacion == "") : pl.Ubicacion == linea.UbicacionOrigen) &&
                                  (string.IsNullOrEmpty(linea.Partida) || pl.Lote == linea.Partida) &&
                                  (pl.Palet.Estado.ToUpper() == "ABIERTO" || pl.Palet.Estado.ToUpper() == "CERRADO"))
                    .ToListAsync();

                // 🔷 Excluir palets que son destino de esta misma orden (no se puede coger de un palet que estamos llenando en la orden)
                var paletsDestinoDeEstaOrden = await _context.OrdenTraspasoLinea
                    .Where(l => l.IdOrdenTraspaso == linea.IdOrdenTraspaso &&
                                !string.IsNullOrWhiteSpace(l.PaletDestino))
                    .Select(l => l.PaletDestino!.Trim().ToUpperInvariant())
                    .Distinct()
                    .ToListAsync();

                if (paletsDestinoDeEstaOrden.Count > 0)
                {
                    paletsEnUbicacion = paletsEnUbicacion
                        .Where(pl => pl.Palet != null &&
                                     !paletsDestinoDeEstaOrden.Contains((pl.Palet.Codigo ?? "").Trim().ToUpperInvariant()))
                        .ToList();
                    _logger.LogInformation(
                        "GetStockLineaAsync: Excluidos palets destino de la orden {OrdenId}: {Palets}. Línea {LineaId}",
                        linea.IdOrdenTraspaso, string.Join(", ", paletsDestinoDeEstaOrden), idLinea);
                }

                var stockPaletizadoTotal = paletsEnUbicacion.Sum(pl => pl.Cantidad);
                var disponibleBase = stockSeleccionado?.Disponible ?? 0m;
                var stockSuelto = disponibleBase - stockPaletizadoTotal;

                if (stockSuelto > 0)
                {
                    resultado.Add(new StockLineaTraspasoDto
                    {
                        CodigoArticulo = linea.CodigoArticulo,
                        DescripcionArticulo = linea.DescripcionArticulo,
                        CodigoAlmacen = linea.CodigoAlmacenOrigen,
                        Ubicacion = linea.UbicacionOrigen,
                        Partida = linea.Partida,
                        FechaCaducidad = linea.FechaCaducidad ?? stockSeleccionado?.FechaCaducidad,
                        StockDisponible = stockSuelto,
                        StockReservado = stockSeleccionado?.Reservado ?? 0m,
                        TipoStock = "Suelto",
                        PaletId = null,
                        CodigoPalet = null,
                        EstadoPalet = null
                    });
                }

                foreach (var palet in paletsEnUbicacion)
                {
                    resultado.Add(new StockLineaTraspasoDto
                    {
                        CodigoArticulo = linea.CodigoArticulo,
                        DescripcionArticulo = linea.DescripcionArticulo,
                        CodigoAlmacen = palet.CodigoAlmacen,
                        Ubicacion = palet.Ubicacion,
                        Partida = palet.Lote,
                        FechaCaducidad = palet.FechaCaducidad,
                        StockDisponible = palet.Cantidad,
                        StockReservado = 0,
                        TipoStock = "Paletizado",
                        PaletId = palet.PaletId,
                        CodigoPalet = palet.Palet?.Codigo,
                        EstadoPalet = palet.Palet?.Estado
                    });
                }
            }

            return resultado;
        }

        public async Task<OrdenTraspasoDto?> ActualizarEstadoLineaAsync(Guid idLinea, ActualizarEstadoLineaDto dto)
        {
			var linea = await _context.OrdenTraspasoLinea
				.Include(l => l.OrdenTraspaso)
				.FirstOrDefaultAsync(l => l.IdLineaOrdenTraspaso == idLinea);
            
			if (linea == null) return null;

			if (dto.Estado == "EN_PROCESO")
			{
				await GetStockLineaAsync(idLinea);
			}

			linea.Estado = dto.Estado;

			if (dto.Estado == "EN_PROCESO" && !linea.FechaInicio.HasValue)
                linea.FechaInicio = DateTime.Now;
            else if (dto.Estado == "COMPLETADA" && !linea.FechaFinalizacion.HasValue)
            {
                linea.FechaFinalizacion = DateTime.Now;
                
                // VERIFICAR SI ES LÍNEA HIJA COMPLETADA
                // Buscar línea padre subdividida con mismo artículo
                var lineaPadre = await _context.OrdenTraspasoLinea
                    .Where(l => l.CodigoArticulo == linea.CodigoArticulo &&
                               l.IdOrdenTraspaso == linea.IdOrdenTraspaso &&
                               l.Estado == "SUBDIVIDIDA" &&
                               l.IdLineaOrdenTraspaso != idLinea)
                    .FirstOrDefaultAsync();

                if (lineaPadre != null)
                {
                    // Verificar si la línea padre está completamente satisfecha
                    var cantidadCompletada = await _context.OrdenTraspasoLinea
                        .Where(l => l.CodigoArticulo == lineaPadre.CodigoArticulo &&
                                   l.IdOrdenTraspaso == lineaPadre.IdOrdenTraspaso &&
                                   l.Estado == "COMPLETADA" &&
                                   l.IdLineaOrdenTraspaso != lineaPadre.IdLineaOrdenTraspaso)
                        .SumAsync(l => l.CantidadMovida);

                    if (cantidadCompletada >= lineaPadre.CantidadPlan)
                    {
                        // Línea padre completamente satisfecha, marcarla como completada
                        lineaPadre.Estado = "COMPLETADA";
                        lineaPadre.FechaFinalizacion = DateTime.Now;
                    }
                    else
                    {
                        // Crear siguiente línea hija automáticamente
                        await CrearLineaHijaAsync(lineaPadre);
                    }
                }
            }

            // VERIFICAR SI LA ORDEN DEBE COMPLETARSE
            await VerificarCompletitudOrdenAsync(linea.IdOrdenTraspaso);

            await _context.SaveChangesAsync();

            // Recargar la orden completa con todas las líneas actualizadas
            var ordenActualizada = await _context.OrdenTraspasoCabecera
                .Include(o => o.Lineas)
                .FirstOrDefaultAsync(o => o.IdOrdenTraspaso == linea.IdOrdenTraspaso);

            return ordenActualizada != null ? MapToDto(ordenActualizada) : null;
        }

        public async Task<bool> DesbloquearLineaAsync(Guid idLinea)
        {
            var linea = await _context.OrdenTraspasoLinea.FindAsync(idLinea);
            if (linea == null) return false;

            // Solo se puede desbloquear si está BLOQUEADA
            if (linea.Estado != "BLOQUEADA") return false;

            linea.Estado = "PENDIENTE";
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<AjusteLineaResponseDto?> AjustarLineaAsync(Guid idLinea, AjusteLineaOrdenTraspasoDto dto)
        {
            var linea = await _context.OrdenTraspasoLinea.FindAsync(idLinea);
            if (linea == null) return null;

            // Obtener la orden para acceder al código de empresa
            var orden = await _context.OrdenTraspasoCabecera.FindAsync(linea.IdOrdenTraspaso);
            if (orden == null) return null;

            // Lógica de ajuste de inventario basada en límites del operario (igual que en ConteosService)
            // La diferencia se calcula respecto al stock real, no la cantidad planificada
            var diferencia = dto.CantidadAjuste - dto.CantidadStock;
            var diferenciaAbs = Math.Abs(diferencia);

            // Obtener límites del operario desde la tabla Operarios
            var operario = await _sageContext.Operarios.AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == dto.IdOperario);
            
            var limUnidades = operario?.MRH_LimiteInventarioUnidades ?? 0m;
            var limEuros = operario?.MRH_LimiteInventarioEuros ?? 0m;

            // Obtener precio medio del artículo para cálculo en euros
            decimal? precioMedio = null;
            try
            {
                // Obtener ejercicio actual
                var ejercicio = await _sageContext.Periodos
                    .Where(p => p.CodigoEmpresa == orden.CodigoEmpresa && p.Fechainicio <= DateTime.Now)
                    .OrderByDescending(p => p.Fechainicio)
                    .Select(p => p.Ejercicio)
                    .FirstOrDefaultAsync();

                if (ejercicio > 0)
                {
                    precioMedio = await _sageContext.AcumuladoStock
                        .Where(a => a.CodigoEmpresa == orden.CodigoEmpresa
                                && a.Ejercicio == ejercicio
                                && a.CodigoArticulo == linea.CodigoArticulo)
                        .Select(a => a.PrecioMedio)
                        .FirstOrDefaultAsync();
                }
            }
            catch 
            { 
                // Si falla el precio, tratamos como 0
            }

            // Determinar si se está creando o destruyendo materia
            var lineaBloqueada = diferencia > 0; // Si se crea materia (diferencia positiva), bloquear siempre
            
            // Si se CREA materia (diferencia positiva): SIEMPRE SUPERVISION, sin comprobar límites
            // Si se DESTRUYE materia (diferencia negativa): comprobar límites como en conteos
            var requiereSupervision = false;
            
            if (lineaBloqueada)
            {
                // Crear materia: SIEMPRE requiere supervisión
                requiereSupervision = true;
            }
            else
            {
                // Destruir materia: comprobar límites del operario (como en conteos)
                var superaUnidades = limUnidades > 0m && diferenciaAbs > limUnidades;
                var superaEuros = false;
                if (limEuros > 0m && precioMedio.HasValue)
                {
                    superaEuros = diferenciaAbs * precioMedio.Value > limEuros;
                }
                requiereSupervision = superaUnidades || superaEuros;
            }

            if (lineaBloqueada)
            {
                linea.Estado = "BLOQUEADA";
                
                // Guardar cambios antes de crear línea hija
                await _context.SaveChangesAsync();
                
                // Si la línea bloqueada tiene ubicación asignada, crear línea hija nueva
                // que buscará stock en otra ubicación (excluyendo la bloqueada)
                if (!string.IsNullOrEmpty(linea.CodigoAlmacenOrigen) && !string.IsNullOrEmpty(linea.UbicacionOrigen))
                {
                    // Verificar si existe una línea padre SUBDIVIDIDA para este artículo
                    var lineaPadre = await _context.OrdenTraspasoLinea
                        .Where(l => l.CodigoArticulo == linea.CodigoArticulo &&
                                   l.IdOrdenTraspaso == linea.IdOrdenTraspaso &&
                                   l.Estado == "SUBDIVIDIDA" &&
                                   l.IdLineaOrdenTraspaso != linea.IdLineaOrdenTraspaso)
                        .FirstOrDefaultAsync();
                    
                    // Si no hay línea padre SUBDIVIDIDA, crear una nueva basada en la línea bloqueada
                    if (lineaPadre == null)
                    {
                        // Calcular cantidad completada por otras líneas
                        var cantidadCompletada = await _context.OrdenTraspasoLinea
                            .Where(l => l.CodigoArticulo == linea.CodigoArticulo &&
                                       l.IdOrdenTraspaso == linea.IdOrdenTraspaso &&
                                       l.Estado == "COMPLETADA" &&
                                       l.IdLineaOrdenTraspaso != linea.IdLineaOrdenTraspaso)
                            .SumAsync(l => l.CantidadMovida);
                        
                        // Crear nueva línea padre SUBDIVIDIDA basada en la línea bloqueada
                        // La línea bloqueada se mantiene como BLOQUEADA, pero creamos un padre para las hijas
                        var nuevaLineaPadre = new OrdenTraspasoLinea
                        {
                            IdLineaOrdenTraspaso = Guid.NewGuid(),
                            IdOrdenTraspaso = linea.IdOrdenTraspaso,
                            NumeroLinea = await ObtenerSiguienteOrdenAsync(linea.IdOrdenTraspaso),
                            CodigoArticulo = linea.CodigoArticulo,
                            DescripcionArticulo = linea.DescripcionArticulo,
                            CantidadPlan = linea.CantidadPlan,
                            CantidadMovida = cantidadCompletada,
                            Estado = "SUBDIVIDIDA",
                            CodigoAlmacenOrigen = null, // Se asignará en la línea hija
                            UbicacionOrigen = null,
                            CodigoAlmacenDestino = linea.CodigoAlmacenDestino,
                            UbicacionDestino = linea.UbicacionDestino,
                            Partida = null,
                            FechaCaducidad = linea.FechaCaducidad,
                            IdOperarioAsignado = linea.IdOperarioAsignado
                        };
                        
                        _context.OrdenTraspasoLinea.Add(nuevaLineaPadre);
                        await _context.SaveChangesAsync();
                        lineaPadre = nuevaLineaPadre;
                        
                        _logger.LogInformation("✅ Línea padre SUBDIVIDIDA creada para línea bloqueada: {LineaPadreId}, Bloqueada: {LineaBloqueadaId}", 
                            nuevaLineaPadre.IdLineaOrdenTraspaso, linea.IdLineaOrdenTraspaso);
                    }
                    
                    // Crear línea hija nueva que excluirá la ubicación bloqueada
                    // Pasamos la línea bloqueada como referencia para que se excluya su ubicación
                    await CrearLineaHijaExcluyendoUbicacionAsync(lineaPadre, linea);
                }
            }

            // REPLICAR EXACTAMENTE EL FLUJO DE ConteosService.cs
            // 1. CREAR OrdenConteo para el ajuste (para que funcione la FK)
            var ordenConteo = new OrdenConteo
            {
                CodigoEmpresa = orden.CodigoEmpresa,
                Titulo = $"AJUSTE TRASPASO - {orden.CodigoOrden}",
                Visibilidad = "INTERNO",
                ModoGeneracion = "TRASPASO",
                Alcance = "UBICACION",
                FiltrosJson = $"{{\"almacen\":\"{linea.CodigoAlmacenOrigen}\",\"ubicacion\":\"{linea.UbicacionOrigen}\",\"articulo\":\"{linea.CodigoArticulo}\"}}",
                FechaPlan = DateTime.Now,
                FechaEjecucion = DateTime.Now,
                SupervisorCodigo = null,
                CreadoPorCodigo = dto.IdOperario.ToString(),
                Estado = "CERRADO",
                Prioridad = 5, // Alta prioridad para ajustes
                FechaCreacion = DateTime.Now,
                CodigoOperario = dto.IdOperario.ToString(),
                FechaAsignacion = DateTime.Now,
                FechaInicio = DateTime.Now,
                FechaCierre = DateTime.Now,
                CodigoAlmacen = linea.CodigoAlmacenOrigen,
                CodigoUbicacion = linea.UbicacionOrigen,
                CodigoArticulo = linea.CodigoArticulo,
                DescripcionArticulo = linea.DescripcionArticulo,
                LotePartida = linea.Partida,
                CantidadTeorica = linea.CantidadPlan,
                Comentario = $"Ajuste de línea de traspaso {linea.IdLineaOrdenTraspaso}",
                IdOrdenTraspaso = orden.IdOrdenTraspaso
            };
            _context.OrdenesConteo.Add(ordenConteo);
            await _context.SaveChangesAsync();

            // 2. CREAR LecturaConteo (para constancia del desajuste)
            var lectura = new LecturaConteo
            {
                OrdenGuid = ordenConteo.GuidID, // ✅ VINCULADO A OrdenConteo VÁLIDA
                CodigoAlmacen = linea.CodigoAlmacenOrigen,
                CodigoUbicacion = linea.UbicacionOrigen,
                CodigoArticulo = linea.CodigoArticulo,
                DescripcionArticulo = linea.DescripcionArticulo,
                LotePartida = linea.Partida,
                CantidadContada = dto.CantidadAjuste, // Cantidad ajustada
                CantidadStock = dto.CantidadStock, // Stock real en la ubicación/palet
                UsuarioCodigo = dto.IdOperario.ToString(),
                Fecha = DateTime.Now,
                FechaCaducidad = linea.FechaCaducidad,
                Comentario = $"Ajuste de Orden Traspaso - Línea {linea.IdLineaOrdenTraspaso}",
                PaletId = dto.PaletId,
                CodigoPalet = dto.CodigoPalet,
                CodigoGS1 = dto.CodigoGS1
            };
            _context.LecturasConteo.Add(lectura);
            await _context.SaveChangesAsync();

            // 3. CREAR ResultadoConteo SIEMPRE (si hay diferencia)
            if (Math.Abs(diferencia) >= 0.0001m)
            {
                var resultado = new ResultadoConteo
                {
                    OrdenGuid = ordenConteo.GuidID, // ✅ VINCULADO A OrdenConteo VÁLIDA
                    CodigoAlmacen = linea.CodigoAlmacenOrigen,
                    CodigoUbicacion = linea.UbicacionOrigen,
                    CodigoArticulo = linea.CodigoArticulo,
                    DescripcionArticulo = linea.DescripcionArticulo,
                    LotePartida = linea.Partida,
                    CantidadContada = dto.CantidadAjuste,
                    CantidadStock = dto.CantidadStock, // Stock real en la ubicación/palet
                    UsuarioCodigo = dto.IdOperario.ToString(),
                    Diferencia = diferencia,
                    AccionFinal = requiereSupervision ? "SUPERVISION" : "AJUSTE",
                    FechaEvaluacion = DateTime.Now,
                    AjusteAplicado = false,
                    FechaCaducidad = linea.FechaCaducidad,
                    PaletId = dto.PaletId,
                    CodigoPalet = dto.CodigoPalet,
                    CodigoGS1 = dto.CodigoGS1
                };
                _context.ResultadosConteo.Add(resultado);
                await _context.SaveChangesAsync();

                // 3. CREAR InventarioAjustes SOLO SI ACCIÓN = "AJUSTE"
                if (!requiereSupervision && !lineaBloqueada)
                {
                    var inventarioAjuste = new InventarioAjustes
                    {
                        IdInventario = null, // Para ajustes de órdenes de traspaso
                        CodigoArticulo = linea.CodigoArticulo,
                        CodigoUbicacion = linea.UbicacionOrigen,
                        Diferencia = diferencia,
                        UsuarioId = dto.IdOperario,
                        Fecha = DateTime.Now,
                        IdConteo = null, // No es un conteo
                        IdOrden = orden.IdOrdenTraspaso, // ✅ VINCULADO A ORDEN DE TRASPASO
                        CodigoEmpresa = orden.CodigoEmpresa,
                        CodigoAlmacen = linea.CodigoAlmacenOrigen,
                        Estado = "PENDIENTE_ERP", // ✅ PARA QUE BackgroundService LO PROCESE
                        FechaCaducidad = linea.FechaCaducidad,
                        Partida = linea.Partida,
                        PaletId = dto.PaletId,
                        CodigoPalet = dto.CodigoPalet,
                        CodigoGS1 = dto.CodigoGS1
                    };

                    _context.InventarioAjustes.Add(inventarioAjuste);
                    await _context.SaveChangesAsync();
                }
            }

            return new AjusteLineaResponseDto
            {
                Success = true,
                Mensaje = lineaBloqueada ? "Línea bloqueada: se está creando materia" :
                          requiereSupervision ? "Ajuste enviado a supervisión (excede límites del operario)" :
                          "Ajuste procesado correctamente",
                RequiereSupervision = requiereSupervision,
                LineaBloqueada = lineaBloqueada,
                DiferenciaStock = diferencia
            };
        }

        public async Task<bool> ActualizarIdTraspasoAsync(Guid idLinea, ActualizarIdTraspasoDto dto)
        {
            var linea = await _context.OrdenTraspasoLinea.FindAsync(idLinea);
            if (linea == null) return false;

            // 🔷 Obtener el traspaso que envía Android para identificar el PaletId
            var traspasoRecibido = await _context.Traspasos.FindAsync(dto.IdTraspaso);
            if (traspasoRecibido == null)
            {
                _logger.LogWarning(
                    "ActualizarIdTraspasoAsync: No se encontró el traspaso {TraspasoId} enviado por Android. LineaId={LineaId}",
                    dto.IdTraspaso, idLinea);
                return false;
            }

            // 🔷 Buscar el traspaso CORRECTO para esta línea específica
            // Criterios: mismo palet, mismo artículo, misma cantidad, misma ubicación origen, misma partida
            var traspasoCorrectoCandidatos = await _context.Traspasos
                .Where(t => t.PaletId == traspasoRecibido.PaletId &&
                            t.CodigoArticulo == linea.CodigoArticulo &&
                            (t.Partida ?? "") == (linea.Partida ?? ""))
                .ToListAsync();

            // Buscar el que mejor coincida por cantidad y ubicación origen
            var traspasoCorrectoId = traspasoRecibido.Id; // Por defecto, usar el que envía Android
            
            if (traspasoCorrectoCandidatos.Count > 1)
            {
                // Si hay varios candidatos, buscar el que coincida exactamente
                var traspasoExacto = traspasoCorrectoCandidatos
                    .FirstOrDefault(t => 
                        t.Cantidad == linea.CantidadMovida &&
                        (t.UbicacionOrigen ?? "") == (linea.UbicacionOrigen ?? ""));

                if (traspasoExacto != null)
                {
                    traspasoCorrectoId = traspasoExacto.Id;
                    _logger.LogInformation(
                        "ActualizarIdTraspasoAsync: Encontrado traspaso exacto {TraspasoCorrectoId} para línea {LineaId}. " +
                        "Android envió {TraspasoRecibidoId}. Articulo={Articulo}, Cantidad={Cantidad}, UbicOrigen={UbicOrigen}",
                        traspasoCorrectoId, idLinea, dto.IdTraspaso, 
                        linea.CodigoArticulo, linea.CantidadMovida, linea.UbicacionOrigen);
                }
                else
                {
                    // Buscar por ubicación origen solamente
                    var traspasoPorUbicacion = traspasoCorrectoCandidatos
                        .FirstOrDefault(t => (t.UbicacionOrigen ?? "") == (linea.UbicacionOrigen ?? ""));

                    if (traspasoPorUbicacion != null)
                    {
                        traspasoCorrectoId = traspasoPorUbicacion.Id;
                        _logger.LogInformation(
                            "ActualizarIdTraspasoAsync: Encontrado traspaso por ubicación {TraspasoCorrectoId} para línea {LineaId}. " +
                            "Android envió {TraspasoRecibidoId}. Articulo={Articulo}, UbicOrigen={UbicOrigen}",
                            traspasoCorrectoId, idLinea, dto.IdTraspaso, 
                            linea.CodigoArticulo, linea.UbicacionOrigen);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "ActualizarIdTraspasoAsync: No se encontró traspaso exacto para línea {LineaId}. " +
                            "Usando el enviado por Android {TraspasoRecibidoId}. Candidatos={Candidatos}",
                            idLinea, dto.IdTraspaso, traspasoCorrectoCandidatos.Count);
                    }
                }
            }
            else if (traspasoCorrectoCandidatos.Count == 1)
            {
                traspasoCorrectoId = traspasoCorrectoCandidatos[0].Id;
                if (traspasoCorrectoId != dto.IdTraspaso)
                {
                    _logger.LogInformation(
                        "ActualizarIdTraspasoAsync: Un solo candidato {TraspasoCorrectoId} para línea {LineaId}. " +
                        "Android envió {TraspasoRecibidoId}",
                        traspasoCorrectoId, idLinea, dto.IdTraspaso);
                }
            }

            // 🔷 Asignar el traspaso correcto (no necesariamente el que envió Android)
            linea.IdTraspaso = traspasoCorrectoId;
            await _context.SaveChangesAsync();
            
            return true;
        }

		public async Task<OrdenTraspasoDto?> CancelarLineaAsync(Guid idLinea)
		{
			var linea = await _context.OrdenTraspasoLinea.FindAsync(idLinea);
			if (linea == null) return null;

			// Solo se puede cancelar si está PENDIENTE o EN_PROCESO
			if (linea.Estado != "PENDIENTE" && linea.Estado != "EN_PROCESO") return null;

			// Cancelación MANUAL: usuario pulsa atrás
			linea.Estado = "CANCELADA";
			linea.FechaFinalizacion = DateTime.Now;
			linea.Completada = false;

			await _context.SaveChangesAsync();

			_logger.LogInformation("CancelarLineaAsync: Línea {LineaId} cancelada manualmente por el usuario", idLinea);

			// NO intentar crear nueva línea hija en cancelación manual
			// El usuario decidió no hacer esta línea ahora, puede volver más tarde

			// Verificar si la orden debe cambiar de estado
			await VerificarCompletitudOrdenAsync(linea.IdOrdenTraspaso);

			// Recargar la orden completa con todas las líneas actualizadas
			var ordenActualizada = await _context.OrdenTraspasoCabecera
				.Include(o => o.Lineas)
				.FirstOrDefaultAsync(o => o.IdOrdenTraspaso == linea.IdOrdenTraspaso);

			return ordenActualizada != null ? MapToDto(ordenActualizada) : null;
		}

        public async Task<IEnumerable<StockDisponibleDto>> GetStockDisponibleAsync(short codigoEmpresa, string codigoArticulo, int idOperario)
        {
            var stockData = await _context.StockDisponible
                .Where(s => s.CodigoEmpresa == codigoEmpresa && s.CodigoArticulo == codigoArticulo)
                .ToListAsync();

			var ubicacionesPulmon = await ObtenerUbicacionesPulmonAsync(
				codigoEmpresa,
				stockData.Select(s => ((string?)s.CodigoAlmacen, (string?)s.Ubicacion)));

			if (ubicacionesPulmon.Count > 0)
			{
				stockData = stockData
					.Where(s => !ubicacionesPulmon.Contains(NormalizeUbicacionKey(s.CodigoAlmacen, s.Ubicacion)))
					.ToList();
			}

            // 🔷 Obtener palets con este artículo para distinguir stock suelto de paletizado
            var paletsConArticulo = await _context.PaletLineas
                .Include(pl => pl.Palet)
                .Where(pl => pl.CodigoEmpresa == codigoEmpresa &&
                             pl.CodigoArticulo == codigoArticulo &&
                             pl.Palet != null &&
                             (pl.Palet.Estado.ToUpper() == "ABIERTO" || pl.Palet.Estado.ToUpper() == "CERRADO"))
                .ToListAsync();

            _logger.LogInformation(
                "GetStockDisponibleAsync: Buscando stock para Articulo={Articulo}, Empresa={Empresa}. " +
                "StockData={StockCount} ubicaciones, PaletsConArticulo={PaletCount} líneas de palet",
                codigoArticulo, codigoEmpresa, stockData.Count, paletsConArticulo.Count);
            
            // Log detallado de los palets encontrados
            foreach (var pl in paletsConArticulo)
            {
                _logger.LogInformation(
                    "  -> PaletLinea: Palet={Codigo}, Estado={Estado}, Almacen={Alm}, Ubicacion={Ubi}, Lote={Lote}, Cantidad={Qty}",
                    pl.Palet?.Codigo, pl.Palet?.Estado, pl.CodigoAlmacen, pl.Ubicacion, pl.Lote, pl.Cantidad);
            }

            var resultado = new List<StockDisponibleDto>();

            // Helper para normalizar strings en comparaciones
            static string Norm(string? val) => (val ?? "").Trim().ToUpperInvariant();

            foreach (var s in stockData)
            {
                // Buscar palets en esta ubicación/partida (con normalización para evitar diferencias de formato)
                var paletsEnUbicacion = paletsConArticulo
                    .Where(pl => Norm(pl.CodigoAlmacen) == Norm(s.CodigoAlmacen) &&
                                 Norm(pl.Ubicacion) == Norm(s.Ubicacion) &&
                                 Norm(pl.Lote) == Norm(s.Partida))
                    .ToList();
                
                _logger.LogInformation(
                    "GetStockDisponibleAsync: Articulo={Art}, Almacen={Alm}, Ubicacion={Ubi}, Partida={Part}, " +
                    "StockTotal={Total}, PaletsEncontrados={Palets}, CantidadPaletizada={PaletQty}",
                    s.CodigoArticulo, s.CodigoAlmacen, s.Ubicacion, s.Partida,
                    s.Disponible, paletsEnUbicacion.Count, paletsEnUbicacion.Sum(pl => pl.Cantidad));

                var stockPaletizadoTotal = paletsEnUbicacion.Sum(pl => pl.Cantidad);
                var stockSuelto = s.Disponible - stockPaletizadoTotal;

                // Añadir stock suelto si existe
                if (stockSuelto > 0)
                {
                    resultado.Add(new StockDisponibleDto
                    {
                        CodigoArticulo = s.CodigoArticulo,
                        DescripcionArticulo = s.DescripcionArticulo,
                        CodigoAlmacen = s.CodigoAlmacen,
                        Ubicacion = s.Ubicacion,
                        Partida = s.Partida,
                        FechaCaducidad = s.FechaCaducidad,
                        StockDisponible = stockSuelto,
                        StockReservado = s.Reservado,
                        TipoStock = "Suelto",
                        PaletId = null,
                        CodigoPalet = null,
                        CodigoGS1 = null,
                        EstadoPalet = null
                    });
                }

                // Añadir cada palet como stock paletizado
                foreach (var palet in paletsEnUbicacion)
                {
                    resultado.Add(new StockDisponibleDto
                    {
                        CodigoArticulo = s.CodigoArticulo,
                        DescripcionArticulo = s.DescripcionArticulo,
                        CodigoAlmacen = palet.CodigoAlmacen,
                        Ubicacion = palet.Ubicacion,
                        Partida = palet.Lote,
                        FechaCaducidad = palet.FechaCaducidad,
                        StockDisponible = palet.Cantidad,
                        StockReservado = 0,
                        TipoStock = "Paletizado",
                        PaletId = palet.PaletId,
                        CodigoPalet = palet.Palet?.Codigo,
                        CodigoGS1 = palet.Palet?.CodigoGS1,
                        EstadoPalet = palet.Palet?.Estado
                    });
                }
            }

            return resultado;
        }

        public async Task<IEnumerable<PaletPendienteDto>> GetPaletsPendientesAsync(Guid ordenId)
        {
            // Obtener la orden de traspaso para obtener información de destino
            var orden = await _context.OrdenTraspasoCabecera
                .FirstOrDefaultAsync(o => o.IdOrdenTraspaso == ordenId);

            if (orden == null)
                return new List<PaletPendienteDto>();

            var codigoOrden = orden.CodigoOrden ?? string.Empty;

            // Obtener palets relacionados con la orden (abiertos o cerrados)
            var palets = await _context.Palets
                .Where(p => p.OrdenTrabajoId == codigoOrden
                            && (p.Estado == "Abierto" || p.Estado == "Cerrado"))
                .ToListAsync();

            var resultado = new List<PaletPendienteDto>();

            foreach (var palet in palets)
            {
                // Cargar líneas del palet por separado
                var paletLineas = await _context.PaletLineas
                    .Where(pl => pl.PaletId == palet.Id)
                    .ToListAsync();

                // 🔷 CORREGIDO: Un palet está "listo para ubicar" si tiene traspasos con estado "PENDIENTE"
                // Cuando se cierra el palet → Se crean traspasos con CodigoEstado = "PENDIENTE"
                // Cuando se ubica el palet → Los traspasos cambian a CodigoEstado = "PENDIENTE_ERP" o "COMPLETADO"
                var tieneTraspasosPendientes = await _context.Traspasos
                    .AnyAsync(t => t.PaletId == palet.Id && 
                                   t.TipoTraspaso == "PALET" && 
                                   t.CodigoEstado == "PENDIENTE");

                // Un palet está pendiente de ubicar si:
                // - Está "Abierto" (aún no se cerró)
                // - Está "Cerrado" y tiene traspasos PENDIENTE (cerrado pero no ubicado)
                var listoParaUbicar = palet.Estado == "Cerrado" && tieneTraspasosPendientes;
                var estaPendiente = palet.Estado == "Abierto" || listoParaUbicar;

                // Solo incluir palets que realmente están pendientes
                if (estaPendiente)
                {
                    var paletDto = new PaletPendienteDto
                    {
                        PaletDestino = palet.Codigo,
                        CodigoGS1 = palet.CodigoGS1,
                        LineasCompletas = paletLineas.Count,
                        CantidadTotal = paletLineas.Sum(pl => pl.Cantidad),
                        ListoParaUbicar = listoParaUbicar,
                        EstadoPalet = palet.Estado
                    };

                    resultado.Add(paletDto);
                }
            }

            return resultado;
        }

        public async Task<UbicarPaletResponseDto> UbicarPaletAsync(Guid ordenId, string paletDestino, UbicarPaletDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. VALIDACIONES INICIALES
                // Verificar que la orden existe y está en estado válido
                var orden = await _context.OrdenTraspasoCabecera
                    .FirstOrDefaultAsync(o => o.IdOrdenTraspaso == ordenId);

                if (orden == null)
                {
                    return new UbicarPaletResponseDto
                    {
                        Success = false,
                        Mensaje = "Orden de traspaso no encontrada"
                    };
                }

                if (orden.Estado != "EN_PROCESO" && orden.Estado != "PENDIENTE")
                {
                    return new UbicarPaletResponseDto
                    {
                        Success = false,
                        Mensaje = $"La orden está en estado {orden.Estado}, no se puede ubicar palets"
                    };
                }

                // Buscar el palet por código (sin navegación a líneas)
                var palet = await _context.Palets
                    .FirstOrDefaultAsync(p => p.Codigo == paletDestino);

                if (palet == null)
                {
                    return new UbicarPaletResponseDto
                    {
                        Success = false,
                        Mensaje = "Palet no encontrado"
                    };
                }

                // 2. VALIDACIONES DEL PALET
                // Verificar que el palet está asociado a la orden
                if (palet.OrdenTrabajoId != ordenId.ToString())
                {
                    return new UbicarPaletResponseDto
                    {
                        Success = false,
                        Mensaje = "El palet no está asociado a esta orden"
                    };
                }

                // Verificar que el palet está en estado válido para ubicar
                if (palet.Estado != "Cerrado" && palet.Estado != "PENDIENTE_UBICACION")
                {
                    return new UbicarPaletResponseDto
                    {
                        Success = false,
                        Mensaje = $"El palet está en estado {palet.Estado}, no se puede ubicar"
                    };
                }

                // 3. VALIDACIONES DE UBICACIÓN
                // Verificar que la ubicación destino es válida
                if (string.IsNullOrEmpty(dto.UbicacionDestino))
                {
                    return new UbicarPaletResponseDto
                    {
                        Success = false,
                        Mensaje = "La ubicación destino es requerida"
                    };
                }

                // No validamos la ubicación destino - el operario puede dejarlo donde quiera

                // 4. ACTUALIZAR ESTADO DEL PALET
                palet.Estado = "UBICADO";
                palet.FechaCierre = DateTime.Now;
                palet.UsuarioCierreId = dto.IdOperario;

                // 5. ACTUALIZAR UBICACIONES DE LAS LÍNEAS DEL PALET
                var lineasPalet = await _context.PaletLineas
                    .Where(l => l.PaletId == palet.Id)
                    .ToListAsync();
                foreach (var linea in lineasPalet)
                {
                    linea.Ubicacion = dto.UbicacionDestino;
                }

                // 8. GUARDAR CAMBIOS
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 9. VERIFICAR COMPLETITUD DE LA ORDEN (usa la lógica centralizada que valida palets pendientes)
                // Esto verificará si todas las líneas están completadas Y si no hay palets pendientes
                await VerificarCompletitudOrdenAsync(ordenId);

                // 10. Recargar la orden para ver si quedó COMPLETADA después de la verificación
                var ordenActualizada = await _context.OrdenTraspasoCabecera
                    .FirstOrDefaultAsync(o => o.IdOrdenTraspaso == ordenId);
                var ordenCompletada = ordenActualizada?.Estado == "COMPLETADA";

                // 11. GENERAR EVENTO DE UBICACIÓN (si hay sistema de eventos)
                // TODO: Implementar notificación a sistemas relacionados

            return new UbicarPaletResponseDto
            {
                Success = true,
                Mensaje = $"Palet {paletDestino} ubicado correctamente en {dto.UbicacionDestino}",
                PaletId = palet.Id,
                CodigoPalet = palet.Codigo,
                EstadoActualizado = "UBICADO",
                FechaUbicacion = DateTime.Now,
                UbicacionDestino = dto.UbicacionDestino,
                TraspasoCompletado = ordenCompletada
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return new UbicarPaletResponseDto
            {
                Success = false,
                Mensaje = $"Error al ubicar palet: {ex.Message}"
            };
        }
    }

    // MÉTODOS AUXILIARES PARA DESGLOSE AUTOMÁTICO
    private async Task<bool> IniciarDesgloseAsync(OrdenTraspasoLinea linea)
    {
        var seModifico = false;

        // Verificar si ya existe una línea hija (evitar duplicados)
        var yaExisteHija = await _context.OrdenTraspasoLinea
            .AnyAsync(l => l.CodigoArticulo == linea.CodigoArticulo &&
                          l.IdOrdenTraspaso == linea.IdOrdenTraspaso &&
                          l.IdLineaOrdenTraspaso != linea.IdLineaOrdenTraspaso &&
                          (l.Estado == "PENDIENTE" || l.Estado == "EN_PROCESO" || l.Estado == "COMPLETADA"));

        if (yaExisteHija)
        {
            _logger.LogWarning("IniciarDesgloseAsync: Ya existe línea hija para {Art} en orden {Orden}", linea.CodigoArticulo, linea.IdOrdenTraspaso);
            // Solo cambiar a SUBDIVIDIDA si no lo está ya
            if (linea.Estado != "SUBDIVIDIDA")
            {
                linea.Estado = "SUBDIVIDIDA";
                await _context.SaveChangesAsync();
                seModifico = true;
            }
            return seModifico;
        }

        // Cambiar estado de línea padre a SUBDIVIDIDA
        if (linea.Estado != "SUBDIVIDIDA")
        {
            linea.Estado = "SUBDIVIDIDA";
            await _context.SaveChangesAsync();
            seModifico = true;
        }

        // Crear primera línea hija
        var hijaCreada = await CrearLineaHijaAsync(linea);

        // Si no se pudo crear la línea hija por falta de stock, marcar como SIN_STOCK
        if (!hijaCreada)
        {
            _logger.LogWarning("IniciarDesgloseAsync: No se pudo crear línea hija para {Art} en orden {Orden} - marcando como SIN_STOCK", linea.CodigoArticulo, linea.IdOrdenTraspaso);

            linea.Estado = "SIN_STOCK";
            await _context.SaveChangesAsync();

            // Verificar si la orden debe cambiar de estado
            await VerificarCompletitudOrdenAsync(linea.IdOrdenTraspaso);
        }

        return seModifico || hijaCreada;
    }

		private async Task<bool> CrearLineaHijaAsync(OrdenTraspasoLinea lineaPadre, OrdenTraspasoLinea? lineaRecienCompletada = null)
    {
        _logger.LogError("🔴 INICIO CrearLineaHijaAsync - Padre: {Id}, Articulo: {Art}", lineaPadre.IdLineaOrdenTraspaso, lineaPadre.CodigoArticulo);

			// 🔴 NUEVA: Clave con 4 componentes para distinguir stock suelto de cada palet
			static string NormalizeKeyConPartidaYPalet(string? almacen, string? ubicacion, string? partida, string? paletOrigen)
        {
            var almac = (almacen ?? string.Empty).Trim().ToUpperInvariant();
            var ubi = string.IsNullOrWhiteSpace(ubicacion) ? "##EMPTY##" : ubicacion.Trim().ToUpperInvariant();
            var lot = string.IsNullOrWhiteSpace(partida) ? "##EMPTY##" : partida.Trim().ToUpperInvariant();
            var palet = string.IsNullOrWhiteSpace(paletOrigen) ? "##SUELTO##" : paletOrigen.Trim().ToUpperInvariant();
            return $"{almac}|{ubi}|{lot}|{palet}";
        }

        var semaforo = _lineaLocks.GetOrAdd(lineaPadre.IdLineaOrdenTraspaso, _ => new SemaphoreSlim(1, 1));
        await semaforo.WaitAsync();

        try
        {
            // Verificar si ya existe una línea hija PENDIENTE o EN_PROCESO (evitar duplicados)
            var yaExisteHijaPendiente = await _context.OrdenTraspasoLinea
                .AnyAsync(l => l.CodigoArticulo == lineaPadre.CodigoArticulo &&
                              l.IdOrdenTraspaso == lineaPadre.IdOrdenTraspaso &&
                              l.IdLineaOrdenTraspaso != lineaPadre.IdLineaOrdenTraspaso &&
                              (l.Estado == "PENDIENTE" || l.Estado == "EN_PROCESO"));

            if (yaExisteHijaPendiente)
            {
                _logger.LogWarning("CrearLineaHijaAsync: Ya existe línea hija PENDIENTE/EN_PROCESO para {Art} en orden {Orden}, no se crea otra", lineaPadre.CodigoArticulo, lineaPadre.IdOrdenTraspaso);
                return true; // ✅ Devolver true porque hay una línea hija activa (no es un error)
            }

            var orden = await _context.OrdenTraspasoCabecera.FindAsync(lineaPadre.IdOrdenTraspaso);
            if (orden == null)
            {
                _logger.LogError("CrearLineaHijaAsync: Orden no encontrada para línea {LineaId}", lineaPadre.IdLineaOrdenTraspaso);
                return false;
            }

            var almacenesPermitidos = await ObtenerAlmacenesAutorizadosAsync(
                lineaPadre.IdOperarioAsignado,
                orden.CodigoEmpresa);

            var almacenesPermitidosNormalizados = almacenesPermitidos
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Select(a => a.Trim().ToUpperInvariant())
                .ToHashSet();

            // Obtener stock disponible ordenado por FEFO + Alfabético en almacenes permitidos
		var stockDisponible = await _context.StockDisponible
				.Where(s => s.CodigoEmpresa == orden.CodigoEmpresa &&
						   s.CodigoArticulo == lineaPadre.CodigoArticulo)
				.ToListAsync();

			var almacenDestinoNormalizado = NormalizeAlmacen(orden.CodigoAlmacenDestino);
			if (!string.IsNullOrEmpty(almacenDestinoNormalizado))
			{
				stockDisponible = stockDisponible
					.Where(s => !string.Equals(NormalizeAlmacen(s.CodigoAlmacen), almacenDestinoNormalizado, StringComparison.OrdinalIgnoreCase))
					.ToList();
			}

			// Excluir almacén 004 (camión de transporte entre almacenes)
			stockDisponible = stockDisponible
				.Where(s => !string.Equals(NormalizeAlmacen(s.CodigoAlmacen), "004", StringComparison.OrdinalIgnoreCase))
				.ToList();

			var ubicacionesPulmon = await ObtenerUbicacionesPulmonAsync(
				orden.CodigoEmpresa,
				stockDisponible.Select(s => ((string?)s.CodigoAlmacen, (string?)s.Ubicacion)));

		if (ubicacionesPulmon.Count > 0)
		{
			stockDisponible = stockDisponible
				.Where(s => !ubicacionesPulmon.Contains(NormalizeUbicacionKey(s.CodigoAlmacen, s.Ubicacion)))
				.ToList();
		}

		// Excluir artículos/partidas bloqueados por calidad
		var bloqueosCalidad = await ObtenerBloqueosCalidadAsync(
			orden.CodigoEmpresa,
			stockDisponible.Select(s => ((string?)s.CodigoArticulo, (string?)s.Partida, (string?)s.CodigoAlmacen, (string?)s.Ubicacion)));

		if (bloqueosCalidad.Count > 0)
		{
			stockDisponible = stockDisponible
				.Where(s => !bloqueosCalidad.Contains($"{(s.CodigoArticulo ?? string.Empty).Trim().ToUpperInvariant()}|{(s.Partida ?? string.Empty).Trim().ToUpperInvariant()}|{(s.CodigoAlmacen ?? string.Empty).Trim().ToUpperInvariant()}|{(s.Ubicacion ?? string.Empty).Trim().ToUpperInvariant()}"))
				.ToList();
			_logger.LogInformation("CrearLineaHijaAsync: Excluidos {Count} artículos/partidas bloqueados por calidad", bloqueosCalidad.Count);
		}

		stockDisponible = stockDisponible
            .Where(s => almacenesPermitidosNormalizados.Contains((s.CodigoAlmacen ?? string.Empty).Trim().ToUpperInvariant()) &&
                        s.Disponible > 0)
            .ToList();

		if (!string.IsNullOrWhiteSpace(lineaPadre.Partida))
		{
			var partidaNormalizada = lineaPadre.Partida.Trim().ToUpperInvariant();
			stockDisponible = stockDisponible
				.Where(s => (s.Partida ?? string.Empty).Trim().ToUpperInvariant() == partidaNormalizada)
				.ToList();

			// Si la línea padre exige partida y no hay stock de esa partida, no hay stock
			if (!stockDisponible.Any())
			{
				_logger.LogWarning("CrearLineaHijaAsync: No hay stock para la partida indicada {Partida} del artículo {Articulo}",
					lineaPadre.Partida, lineaPadre.CodigoArticulo);
				return false;
			}
		}

		stockDisponible = stockDisponible
			.OrderBy(s => s.FechaCaducidad)
			.ThenBy(s => ((s.CodigoAlmacen ?? string.Empty).Trim() + "|" + (s.Ubicacion ?? string.Empty).Trim()).ToUpperInvariant())
			.ToList();

        if (!stockDisponible.Any())
        {
            _logger.LogWarning("CrearLineaHijaAsync: No hay stock disponible para subdividir línea {LineaId} del artículo {Articulo} después de aplicar filtros", lineaPadre.IdLineaOrdenTraspaso, lineaPadre.CodigoArticulo);
            return false;
        }

		// 🔴 NUEVO: Desagregar stock en suelto vs paletizado (por cada palet individual)
		// Obtener palets con este artículo para distinguir stock suelto de paletizado
		var paletsConArticulo = await _context.PaletLineas
			.Include(pl => pl.Palet)
			.Where(pl => pl.CodigoEmpresa == orden.CodigoEmpresa &&
						 pl.CodigoArticulo == lineaPadre.CodigoArticulo &&
						 pl.Palet != null &&
						 (pl.Palet.Estado.ToUpper() == "ABIERTO" || pl.Palet.Estado.ToUpper() == "CERRADO"))
			.ToListAsync();

		// 🔷 Excluir palets que son destino de esta misma orden (no asignar línea hija a un palet que estamos llenando)
		var paletsDestinoOrden = await _context.OrdenTraspasoLinea
			.Where(l => l.IdOrdenTraspaso == lineaPadre.IdOrdenTraspaso && !string.IsNullOrWhiteSpace(l.PaletDestino))
			.Select(l => l.PaletDestino!.Trim().ToUpperInvariant())
			.Distinct()
			.ToListAsync();
		if (paletsDestinoOrden.Count > 0)
		{
			paletsConArticulo = paletsConArticulo
				.Where(pl => pl.Palet != null && !paletsDestinoOrden.Contains((pl.Palet.Codigo ?? "").Trim().ToUpperInvariant()))
				.ToList();
			_logger.LogInformation("CrearLineaHijaAsync: Excluidos palets destino de la orden: {Palets}", string.Join(", ", paletsDestinoOrden));
		}

		_logger.LogInformation("CrearLineaHijaAsync: PaletsConArticulo encontrados: {Count}", paletsConArticulo.Count);

		// Helper para normalizar strings en comparaciones
		static string Norm(string? val) => (val ?? "").Trim().ToUpperInvariant();

		var stockDesagregado = new List<StockDisponibleDto>();
		foreach (var s in stockDisponible)
		{
			// Buscar palets en esta ubicación/partida
			var paletsEnUbicacion = paletsConArticulo
				.Where(pl => Norm(pl.CodigoAlmacen) == Norm(s.CodigoAlmacen) &&
							 Norm(pl.Ubicacion) == Norm(s.Ubicacion) &&
							 Norm(pl.Lote) == Norm(s.Partida))
				.ToList();

			var stockPaletizadoTotal = paletsEnUbicacion.Sum(pl => pl.Cantidad);
			var stockSuelto = s.Disponible - stockPaletizadoTotal;

			// Añadir stock suelto si existe
			if (stockSuelto > 0)
			{
				stockDesagregado.Add(new StockDisponibleDto
				{
					CodigoArticulo = s.CodigoArticulo,
					DescripcionArticulo = s.DescripcionArticulo,
					CodigoAlmacen = s.CodigoAlmacen,
					Ubicacion = s.Ubicacion,
					Partida = s.Partida,
					FechaCaducidad = s.FechaCaducidad,
					StockDisponible = stockSuelto,
					StockReservado = s.Reservado,
					TipoStock = "Suelto",
					PaletId = null,
					CodigoPalet = null,
					CodigoGS1 = null,
					EstadoPalet = null
				});
			}

			// Añadir cada palet como stock paletizado
			foreach (var palet in paletsEnUbicacion)
			{
				stockDesagregado.Add(new StockDisponibleDto
				{
					CodigoArticulo = s.CodigoArticulo,
					DescripcionArticulo = s.DescripcionArticulo,
					CodigoAlmacen = palet.CodigoAlmacen,
					Ubicacion = palet.Ubicacion,
					Partida = palet.Lote,
					FechaCaducidad = palet.FechaCaducidad,
					StockDisponible = palet.Cantidad,
					StockReservado = 0,
					TipoStock = "Paletizado",
					PaletId = palet.PaletId,
					CodigoPalet = palet.Palet?.Codigo,
					CodigoGS1 = palet.Palet?.CodigoGS1,
					EstadoPalet = palet.Palet?.Estado
				});
			}
		}

		// Ordenar stock desagregado por FEFO
		stockDesagregado = stockDesagregado
			.OrderBy(s => s.FechaCaducidad)
			.ThenBy(s => $"{Norm(s.CodigoAlmacen)}|{Norm(s.Ubicacion)}")
			.ToList();

		_logger.LogInformation("CrearLineaHijaAsync: Stock desagregado total: {Count} registros (sueltos + palets individuales)", stockDesagregado.Count);

		if (!stockDesagregado.Any())
		{
			_logger.LogWarning("CrearLineaHijaAsync: No hay stock desagregado disponible para artículo {Articulo}", lineaPadre.CodigoArticulo);
			return false;
		}

            var lineasRelacionadas = await _context.OrdenTraspasoLinea
                .Where(l => l.IdOrdenTraspaso == lineaPadre.IdOrdenTraspaso &&
                            l.CodigoArticulo == lineaPadre.CodigoArticulo &&
                            l.IdLineaOrdenTraspaso != lineaPadre.IdLineaOrdenTraspaso)
                .Select(l => new
                {
                    l.CodigoAlmacenOrigen,
                    l.UbicacionOrigen,
                    l.Partida,
                    l.PaletOrigen, // 🔴 NUEVO: incluir PaletOrigen para distinguir suelto de paletizado
                    Estado = (l.Estado ?? string.Empty),
                    l.CantidadMovida,
                    l.CantidadPlan
                })
                .ToListAsync();

            // Solo considerar líneas COMPLETADAS (las PENDIENTE/EN_PROCESO aún no sabemos si realmente necesitaremos más)
            // 🔴 MODIFICADO: Usar clave con 4 componentes (incluye PaletOrigen)
            var lineasCompletadas = lineasRelacionadas
                .Where(l => !string.IsNullOrWhiteSpace(l.CodigoAlmacenOrigen))
                .Where(l => !string.IsNullOrWhiteSpace(l.Estado))
                .Where(l => l.Estado.Trim().ToUpperInvariant() == "COMPLETADA")
				.Select(l => new
				{
					Key = NormalizeKeyConPartidaYPalet(l.CodigoAlmacenOrigen, l.UbicacionOrigen, l.Partida, l.PaletOrigen),
                    Estado = l.Estado.Trim().ToUpperInvariant(),
                    l.CantidadMovida,
                    l.CantidadPlan
                })
                .ToList();

            // Obtener las claves de ubicaciones/palets ya usados (solo COMPLETADAS)
            var ubicacionesUsadas = lineasCompletadas.Select(l => l.Key).ToHashSet();

            _logger.LogError($"🔍 DEBUG: lineasCompletadas obtenidas: {lineasCompletadas.Count}");
            foreach (var linea in lineasCompletadas)
            {
                _logger.LogError($"  - Key: '{linea.Key}', Estado: {linea.Estado}, CantidadMovida: {linea.CantidadMovida}");
            }

            // 🔴 MODIFICADO: Incluir PaletOrigen en la clave de lineaRecienCompletada
            if (lineaRecienCompletada != null)
            {
				var keyReciente = NormalizeKeyConPartidaYPalet(
                    lineaRecienCompletada.CodigoAlmacenOrigen,
                    lineaRecienCompletada.UbicacionOrigen,
                    lineaRecienCompletada.Partida,
                    lineaRecienCompletada.PaletOrigen);

                _logger.LogError($"🔴 Línea completada - Alm: '{lineaRecienCompletada.CodigoAlmacenOrigen}', Ubi: '{lineaRecienCompletada.UbicacionOrigen}', Part: '{lineaRecienCompletada.Partida}', Palet: '{lineaRecienCompletada.PaletOrigen}' -> Key: '{keyReciente}'");

                if (!string.IsNullOrEmpty(keyReciente))
                {
                    ubicacionesUsadas.Add(keyReciente);
                }
            }

            _logger.LogError($"📍 Ubicaciones/Palets usados ({ubicacionesUsadas.Count}): {string.Join(", ", ubicacionesUsadas)}");

            // Calcular cantidad restante: SOLO CantidadMovida de hijas COMPLETADAS
            // Importante:
            // - Evitar crear líneas hijas con cantidades residuales que, al persistir (p.ej. 4 decimales), quedan en 0.0000.
            // - Evitar asignar más que el stock disponible (p.ej. 17.99999 → 18.0000). Por eso TRUNCAMOS a 4 decimales,
            //   no redondeamos.
            static decimal Truncar4(decimal valor)
            {
                const decimal factor = 10000m;
                return Math.Truncate(valor * factor) / factor;
            }
            var consumoTotal = lineasCompletadas.Sum(l => l.CantidadMovida);
            var cantidadRestante = lineaPadre.CantidadPlan - consumoTotal;
            var cantidadRestanteTruncada = Truncar4(cantidadRestante);
            _logger.LogError($"💰 Consumo total (solo COMPLETADAS): {consumoTotal}, Cantidad restante: {cantidadRestante} (Plan padre: {lineaPadre.CantidadPlan})");

            if (cantidadRestanteTruncada <= 0)
            {
                _logger.LogError($"⚠️ No hay cantidad restante, saliendo de CrearLineaHijaAsync");
                return false;
            }

            // 🔴 CRÍTICO: Ajustar stock disponible restando lo ya movido en traspasos de la misma orden
            // Cuando se ubica un palet, el stock se mueve a la ubicación destino del traspaso.
            // Ese stock aparece en StockDisponible de esa ubicación, pero ya fue contado para la orden.
            // Debemos restar esas cantidades del Disponible, no excluir la ubicación completa.
            var queryLineas = _context.OrdenTraspasoLinea
                .Where(l => l.IdOrdenTraspaso == lineaPadre.IdOrdenTraspaso &&
                           l.CodigoArticulo == lineaPadre.CodigoArticulo &&
                           l.IdTraspaso.HasValue);

            // Si la línea padre tiene partida, filtrar también por partida
            if (!string.IsNullOrWhiteSpace(lineaPadre.Partida))
            {
                var partidaNormalizada = lineaPadre.Partida.Trim().ToUpperInvariant();
                queryLineas = queryLineas.Where(l => l.Partida != null && 
                                                     l.Partida.Trim().ToUpperInvariant() == partidaNormalizada);
            }

            var lineasConTraspaso = await queryLineas
                .Select(l => l.IdTraspaso.Value)
                .Distinct()
                .ToListAsync();

            // Diccionario: Key (Almacen|Ubicacion|Partida) -> Cantidad total movida
            var cantidadesMovidasPorUbicacion = new Dictionary<string, decimal>();
            if (lineasConTraspaso.Any())
            {
                var queryTraspasos = _context.Traspasos
                    .Where(t => lineasConTraspaso.Contains(t.Id) &&
                               !string.IsNullOrEmpty(t.AlmacenDestino) &&
                               t.CodigoArticulo == lineaPadre.CodigoArticulo);

                // Si la línea padre tiene partida, filtrar también por partida en traspasos
                if (!string.IsNullOrWhiteSpace(lineaPadre.Partida))
                {
                    var partidaNormalizada = lineaPadre.Partida.Trim().ToUpperInvariant();
                    queryTraspasos = queryTraspasos.Where(t => t.Partida != null && 
                                                               t.Partida.Trim().ToUpperInvariant() == partidaNormalizada);
                }

                var traspasos = await queryTraspasos
                    .Select(t => new { t.AlmacenDestino, t.UbicacionDestino, t.Partida, t.Cantidad })
                    .ToListAsync();

                // Agrupar por ubicación destino y sumar cantidades
                // Clave de 3 componentes para destinos (no tienen palet)
                foreach (var traspaso in traspasos)
                {
                    var keyDestino = $"{Norm(traspaso.AlmacenDestino)}|{(string.IsNullOrWhiteSpace(traspaso.UbicacionDestino) ? "##EMPTY##" : Norm(traspaso.UbicacionDestino))}|{(string.IsNullOrWhiteSpace(traspaso.Partida) ? "##EMPTY##" : Norm(traspaso.Partida))}";
                    
                    if (!string.IsNullOrEmpty(keyDestino) && traspaso.Cantidad.HasValue)
                    {
                        if (!cantidadesMovidasPorUbicacion.ContainsKey(keyDestino))
                            cantidadesMovidasPorUbicacion[keyDestino] = 0;
                        
                        cantidadesMovidasPorUbicacion[keyDestino] += traspaso.Cantidad.Value;
                        _logger.LogInformation("🔴 Stock movido a ubicación destino: {Key} (Alm={Almacen}, Ubi={Ubicacion}, Part={Partida}, Cant={Cantidad})",
                            keyDestino, traspaso.AlmacenDestino, traspaso.UbicacionDestino, traspaso.Partida, traspaso.Cantidad.Value);
                    }
                }
            }

            _logger.LogError($"📦 Stock desagregado para artículo {lineaPadre.CodigoArticulo}: {stockDesagregado.Count} registros");
            foreach (var s in stockDesagregado.Take(10))
            {
                _logger.LogError($"  - Art: '{s.CodigoArticulo}', Alm: '{s.CodigoAlmacen}', Ubi: '{s.Ubicacion}', Part: '{s.Partida}', Tipo: {s.TipoStock}, Palet: '{s.CodigoPalet ?? "N/A"}', Disp: {s.StockDisponible}, FechaCad: {s.FechaCaducidad:yyyy-MM-dd}");
            }

            // 🔴 MODIFICADO: Buscar primer stock (suelto o palet) no usado en orden FEFO
			foreach (var stock in stockDesagregado)
			{
				// 🔴 NUEVO: Clave con 4 componentes (incluye palet o ##SUELTO## si es suelto)
				var key = NormalizeKeyConPartidaYPalet(stock.CodigoAlmacen, stock.Ubicacion, stock.Partida, stock.CodigoPalet);

				// Si ya se usó este stock específico (ubicación + palet o suelto), saltar
				if (ubicacionesUsadas.Contains(key)) continue;

				// Clave de 3 componentes para ajuste de cantidades movidas (destino no tiene palet)
				var keyUbicacion = $"{Norm(stock.CodigoAlmacen)}|{(string.IsNullOrWhiteSpace(stock.Ubicacion) ? "##EMPTY##" : Norm(stock.Ubicacion))}|{(string.IsNullOrWhiteSpace(stock.Partida) ? "##EMPTY##" : Norm(stock.Partida))}";

				// 🔴 CRÍTICO: Ajustar stock disponible restando lo ya movido en traspasos
				// Si esta ubicación es destino de un traspaso de la misma orden, restar esa cantidad
				var disponibleAjustado = stock.StockDisponible;
				if (cantidadesMovidasPorUbicacion.ContainsKey(keyUbicacion))
				{
					var cantidadMovida = cantidadesMovidasPorUbicacion[keyUbicacion];
					disponibleAjustado = Math.Max(0, stock.StockDisponible - cantidadMovida);
					_logger.LogInformation("🔴 Ajustando stock disponible en ubicación destino: {Key} - Disponible original: {DisponibleOriginal}, Cantidad movida: {CantidadMovida}, Disponible ajustado: {DisponibleAjustado}",
						keyUbicacion, stock.StockDisponible, cantidadMovida, disponibleAjustado);
				}

				// Si después del ajuste no hay stock disponible, saltar
				if (disponibleAjustado <= 0)
				{
					_logger.LogDebug("Stock sin disponible después de ajuste: {Key}", key);
					continue;
				}

				// Tomar el mínimo entre lo que queda por mover y lo que hay disponible (ajustado)
				var cantidadAsignada = Math.Min(cantidadRestante, disponibleAjustado);
				var cantidadAsignadaTruncada = Truncar4(cantidadAsignada);
				if (cantidadAsignadaTruncada <= 0) continue;

				_logger.LogError($"✅ Creando línea hija: Padre={lineaPadre.IdLineaOrdenTraspaso}, Art={lineaPadre.CodigoArticulo}, Stock seleccionado: Alm={stock.CodigoAlmacen}, Ubi={stock.Ubicacion}, Part={stock.Partida}, Tipo={stock.TipoStock}, Palet={stock.CodigoPalet ?? "N/A"}, Cant={cantidadAsignada}");

				var ordenLock = _ordenLocks.GetOrAdd(lineaPadre.IdOrdenTraspaso, _ => new SemaphoreSlim(1, 1));
				await ordenLock.WaitAsync();
				try
				{
					var numeroLinea = await ObtenerSiguienteOrdenAsync(lineaPadre.IdOrdenTraspaso);

					var lineaHija = new OrdenTraspasoLinea
					{
						IdLineaOrdenTraspaso = Guid.NewGuid(),
						IdOrdenTraspaso = lineaPadre.IdOrdenTraspaso,
						NumeroLinea = numeroLinea,
						CodigoArticulo = lineaPadre.CodigoArticulo,
						DescripcionArticulo = lineaPadre.DescripcionArticulo,
						CantidadPlan = cantidadAsignadaTruncada,
						CantidadMovida = 0,
						Estado = "PENDIENTE",
						CodigoAlmacenOrigen = stock.CodigoAlmacen,
						UbicacionOrigen = stock.Ubicacion ?? string.Empty,
						CodigoAlmacenDestino = lineaPadre.CodigoAlmacenDestino,
						UbicacionDestino = lineaPadre.UbicacionDestino,
						Partida = stock.Partida,
						FechaCaducidad = stock.FechaCaducidad,
						IdOperarioAsignado = lineaPadre.IdOperarioAsignado,
						// 🔴 NUEVO: Asignar PaletOrigen si el stock es paletizado
						PaletOrigen = stock.TipoStock == "Paletizado" ? stock.CodigoPalet : null
					};

					_context.OrdenTraspasoLinea.Add(lineaHija);
					await _context.SaveChangesAsync();

					_logger.LogError($"✅ LÍNEA HIJA GUARDADA: Id={lineaHija.IdLineaOrdenTraspaso}, NumLinea={lineaHija.NumeroLinea}, Art={lineaHija.CodigoArticulo}, Alm={lineaHija.CodigoAlmacenOrigen}, Ubi={lineaHija.UbicacionOrigen}, Part={lineaHija.Partida}, PaletOrigen={lineaHija.PaletOrigen ?? "N/A (Suelto)"}");
				}
				finally
				{
					ordenLock.Release();
				}

				return true;
			}

            return false;
        }
        finally
        {
            semaforo.Release();
        }
    }

    /// <summary>
    /// Crea una línea hija nueva excluyendo la ubicación de la línea bloqueada
    /// </summary>
    private async Task<bool> CrearLineaHijaExcluyendoUbicacionAsync(OrdenTraspasoLinea lineaPadre, OrdenTraspasoLinea lineaBloqueada)
    {
        _logger.LogInformation("🔴 Creando línea hija excluyendo ubicación bloqueada - Padre: {Id}, Bloqueada: {BloqueadaId}, Ubicación: {Almacen}-{Ubicacion}-{Partida}-{Palet}", 
            lineaPadre.IdLineaOrdenTraspaso, lineaBloqueada.IdLineaOrdenTraspaso, 
            lineaBloqueada.CodigoAlmacenOrigen, lineaBloqueada.UbicacionOrigen, lineaBloqueada.Partida, lineaBloqueada.PaletOrigen ?? "Suelto");

        // 🔴 NUEVO: Clave con 4 componentes para distinguir stock suelto de cada palet
        static string NormalizeKeyConPartidaYPalet(string? almacen, string? ubicacion, string? partida, string? paletOrigen)
        {
            var almac = (almacen ?? string.Empty).Trim().ToUpperInvariant();
            var ubi = string.IsNullOrWhiteSpace(ubicacion) ? "##EMPTY##" : ubicacion.Trim().ToUpperInvariant();
            var lot = string.IsNullOrWhiteSpace(partida) ? "##EMPTY##" : partida.Trim().ToUpperInvariant();
            var palet = string.IsNullOrWhiteSpace(paletOrigen) ? "##SUELTO##" : paletOrigen.Trim().ToUpperInvariant();
            return $"{almac}|{ubi}|{lot}|{palet}";
        }

        var semaforo = _lineaLocks.GetOrAdd(lineaPadre.IdLineaOrdenTraspaso, _ => new SemaphoreSlim(1, 1));
        await semaforo.WaitAsync();

        try
        {
            // Verificar si ya existe una línea hija PENDIENTE o EN_PROCESO (evitar duplicados)
            var yaExisteHijaPendiente = await _context.OrdenTraspasoLinea
                .AnyAsync(l => l.CodigoArticulo == lineaPadre.CodigoArticulo &&
                              l.IdOrdenTraspaso == lineaPadre.IdOrdenTraspaso &&
                              l.IdLineaOrdenTraspaso != lineaPadre.IdLineaOrdenTraspaso &&
                              (l.Estado == "PENDIENTE" || l.Estado == "EN_PROCESO"));

            if (yaExisteHijaPendiente)
            {
                _logger.LogWarning("CrearLineaHijaExcluyendoUbicacionAsync: Ya existe línea hija PENDIENTE/EN_PROCESO para {Art} en orden {Orden}", 
                    lineaPadre.CodigoArticulo, lineaPadre.IdOrdenTraspaso);
                return false;
            }

            var orden = await _context.OrdenTraspasoCabecera.FindAsync(lineaPadre.IdOrdenTraspaso);
            if (orden == null)
            {
                _logger.LogError("🔴 CrearLineaHijaExcluyendoUbicacionAsync: orden no encontrada");
                return false;
            }

            var almacenesPermitidos = await ObtenerAlmacenesAutorizadosAsync(
                lineaPadre.IdOperarioAsignado,
                orden.CodigoEmpresa);

            var almacenesPermitidosNormalizados = almacenesPermitidos
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Select(a => a.Trim().ToUpperInvariant())
                .ToHashSet();

            // Obtener stock disponible ordenado por FEFO + Alfabético en almacenes permitidos
            var stockDisponible = await _context.StockDisponible
                .Where(s => s.CodigoEmpresa == orden.CodigoEmpresa &&
                           s.CodigoArticulo == lineaPadre.CodigoArticulo)
                .ToListAsync();

            var almacenDestinoNormalizado = NormalizeAlmacen(orden.CodigoAlmacenDestino);
            if (!string.IsNullOrEmpty(almacenDestinoNormalizado))
            {
                stockDisponible = stockDisponible
                    .Where(s => !string.Equals(NormalizeAlmacen(s.CodigoAlmacen), almacenDestinoNormalizado, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // Excluir almacén 004 (camión de transporte entre almacenes)
            stockDisponible = stockDisponible
                .Where(s => !string.Equals(NormalizeAlmacen(s.CodigoAlmacen), "004", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var ubicacionesPulmon = await ObtenerUbicacionesPulmonAsync(
                orden.CodigoEmpresa,
                stockDisponible.Select(s => ((string?)s.CodigoAlmacen, (string?)s.Ubicacion)));

            if (ubicacionesPulmon.Count > 0)
            {
                stockDisponible = stockDisponible
                    .Where(s => !ubicacionesPulmon.Contains(NormalizeUbicacionKey(s.CodigoAlmacen, s.Ubicacion)))
                    .ToList();
            }

            // Excluir artículos/partidas bloqueados por calidad
            var bloqueosCalidad = await ObtenerBloqueosCalidadAsync(
                orden.CodigoEmpresa,
                stockDisponible.Select(s => ((string?)s.CodigoArticulo, (string?)s.Partida, (string?)s.CodigoAlmacen, (string?)s.Ubicacion)));

            if (bloqueosCalidad.Count > 0)
            {
                stockDisponible = stockDisponible
                    .Where(s => !bloqueosCalidad.Contains($"{(s.CodigoArticulo ?? string.Empty).Trim().ToUpperInvariant()}|{(s.Partida ?? string.Empty).Trim().ToUpperInvariant()}|{(s.CodigoAlmacen ?? string.Empty).Trim().ToUpperInvariant()}|{(s.Ubicacion ?? string.Empty).Trim().ToUpperInvariant()}"))
                    .ToList();
                _logger.LogInformation("CrearLineaHijaExcluyendoUbicacionAsync: Excluidos {Count} artículos/partidas bloqueados por calidad", bloqueosCalidad.Count);
            }

            stockDisponible = stockDisponible
                .Where(s => almacenesPermitidosNormalizados.Contains((s.CodigoAlmacen ?? string.Empty).Trim().ToUpperInvariant()) &&
                            s.Disponible > 0)
                .ToList();

            if (!string.IsNullOrWhiteSpace(lineaPadre.Partida))
            {
                var partidaNormalizada = lineaPadre.Partida.Trim().ToUpperInvariant();
                stockDisponible = stockDisponible
                    .Where(s => (s.Partida ?? string.Empty).Trim().ToUpperInvariant() == partidaNormalizada)
                    .ToList();

                // Si la línea padre exige partida y no hay stock de esa partida, no hay stock
                if (!stockDisponible.Any())
                {
                    _logger.LogWarning("CrearLineaHijaExcluyendoUbicacionAsync: No hay stock para la partida indicada {Partida} del artículo {Articulo}",
                        lineaPadre.Partida, lineaPadre.CodigoArticulo);
                    return false;
                }
            }

            stockDisponible = stockDisponible
                .OrderBy(s => s.FechaCaducidad)
                .ThenBy(s => ((s.CodigoAlmacen ?? string.Empty).Trim() + "|" + (s.Ubicacion ?? string.Empty).Trim()).ToUpperInvariant())
                .ToList();

            if (!stockDisponible.Any())
            {
                _logger.LogWarning("CrearLineaHijaExcluyendoUbicacionAsync: No hay stock disponible para artículo {Art}", lineaPadre.CodigoArticulo);
                return false;
            }

            // 🔴 NUEVO: Desagregar stock en suelto vs paletizado (por cada palet individual)
            var paletsConArticulo = await _context.PaletLineas
                .Include(pl => pl.Palet)
                .Where(pl => pl.CodigoEmpresa == orden.CodigoEmpresa &&
                             pl.CodigoArticulo == lineaPadre.CodigoArticulo &&
                             pl.Palet != null &&
                             (pl.Palet.Estado.ToUpper() == "ABIERTO" || pl.Palet.Estado.ToUpper() == "CERRADO"))
                .ToListAsync();

            // 🔷 Excluir palets que son destino de esta misma orden
            var paletsDestinoOrdenExcl = await _context.OrdenTraspasoLinea
                .Where(l => l.IdOrdenTraspaso == lineaPadre.IdOrdenTraspaso && !string.IsNullOrWhiteSpace(l.PaletDestino))
                .Select(l => l.PaletDestino!.Trim().ToUpperInvariant())
                .Distinct()
                .ToListAsync();
            if (paletsDestinoOrdenExcl.Count > 0)
            {
                paletsConArticulo = paletsConArticulo
                    .Where(pl => pl.Palet != null && !paletsDestinoOrdenExcl.Contains((pl.Palet.Codigo ?? "").Trim().ToUpperInvariant()))
                    .ToList();
                _logger.LogInformation("CrearLineaHijaExcluyendoUbicacionAsync: Excluidos palets destino de la orden: {Palets}", string.Join(", ", paletsDestinoOrdenExcl));
            }

            _logger.LogInformation("CrearLineaHijaExcluyendoUbicacionAsync: PaletsConArticulo encontrados: {Count}", paletsConArticulo.Count);

            static string Norm(string? val) => (val ?? "").Trim().ToUpperInvariant();

            var stockDesagregado = new List<StockDisponibleDto>();
            foreach (var s in stockDisponible)
            {
                var paletsEnUbicacion = paletsConArticulo
                    .Where(pl => Norm(pl.CodigoAlmacen) == Norm(s.CodigoAlmacen) &&
                                 Norm(pl.Ubicacion) == Norm(s.Ubicacion) &&
                                 Norm(pl.Lote) == Norm(s.Partida))
                    .ToList();

                var stockPaletizadoTotal = paletsEnUbicacion.Sum(pl => pl.Cantidad);
                var stockSuelto = s.Disponible - stockPaletizadoTotal;

                if (stockSuelto > 0)
                {
                    stockDesagregado.Add(new StockDisponibleDto
                    {
                        CodigoArticulo = s.CodigoArticulo,
                        DescripcionArticulo = s.DescripcionArticulo,
                        CodigoAlmacen = s.CodigoAlmacen,
                        Ubicacion = s.Ubicacion,
                        Partida = s.Partida,
                        FechaCaducidad = s.FechaCaducidad,
                        StockDisponible = stockSuelto,
                        StockReservado = s.Reservado,
                        TipoStock = "Suelto",
                        PaletId = null,
                        CodigoPalet = null,
                        CodigoGS1 = null,
                        EstadoPalet = null
                    });
                }

                foreach (var palet in paletsEnUbicacion)
                {
                    stockDesagregado.Add(new StockDisponibleDto
                    {
                        CodigoArticulo = s.CodigoArticulo,
                        DescripcionArticulo = s.DescripcionArticulo,
                        CodigoAlmacen = palet.CodigoAlmacen,
                        Ubicacion = palet.Ubicacion,
                        Partida = palet.Lote,
                        FechaCaducidad = palet.FechaCaducidad,
                        StockDisponible = palet.Cantidad,
                        StockReservado = 0,
                        TipoStock = "Paletizado",
                        PaletId = palet.PaletId,
                        CodigoPalet = palet.Palet?.Codigo,
                        CodigoGS1 = palet.Palet?.CodigoGS1,
                        EstadoPalet = palet.Palet?.Estado
                    });
                }
            }

            stockDesagregado = stockDesagregado
                .OrderBy(s => s.FechaCaducidad)
                .ThenBy(s => $"{Norm(s.CodigoAlmacen)}|{Norm(s.Ubicacion)}")
                .ToList();

            _logger.LogInformation("CrearLineaHijaExcluyendoUbicacionAsync: Stock desagregado total: {Count} registros", stockDesagregado.Count);

            if (!stockDesagregado.Any())
            {
                _logger.LogWarning("CrearLineaHijaExcluyendoUbicacionAsync: No hay stock desagregado disponible para artículo {Art}", lineaPadre.CodigoArticulo);
                return false;
            }

            // Obtener ubicaciones ya usadas (incluyendo la bloqueada)
            // 🔴 MODIFICADO: Incluir PaletOrigen en la consulta
            var lineasRelacionadas = await _context.OrdenTraspasoLinea
                .Where(l => l.IdOrdenTraspaso == lineaPadre.IdOrdenTraspaso &&
                            l.CodigoArticulo == lineaPadre.CodigoArticulo &&
                            l.IdLineaOrdenTraspaso != lineaPadre.IdLineaOrdenTraspaso)
                .Select(l => new
                {
                    l.CodigoAlmacenOrigen,
                    l.UbicacionOrigen,
                    l.Partida,
                    l.PaletOrigen, // 🔴 NUEVO
                    Estado = (l.Estado ?? string.Empty),
                    l.CantidadMovida,
                    l.CantidadPlan
                })
                .ToListAsync();

            // 🔴 MODIFICADO: Usar clave con 4 componentes
            var lineasCompletadas = lineasRelacionadas
                .Where(l => !string.IsNullOrWhiteSpace(l.CodigoAlmacenOrigen))
                .Where(l => !string.IsNullOrWhiteSpace(l.Estado))
                .Where(l => l.Estado.Trim().ToUpperInvariant() == "COMPLETADA")
                .Select(l => new
                {
                    Key = NormalizeKeyConPartidaYPalet(l.CodigoAlmacenOrigen, l.UbicacionOrigen, l.Partida, l.PaletOrigen),
                    Estado = l.Estado.Trim().ToUpperInvariant(),
                    l.CantidadMovida,
                    l.CantidadPlan
                })
                .ToList();

            var ubicacionesUsadas = lineasCompletadas.Select(l => l.Key).ToHashSet();

            // 🔴 CRÍTICO: Agregar la ubicación/palet de la línea bloqueada a las excluidas
            // 🔴 MODIFICADO: Usar clave con 4 componentes
            var keyBloqueada = NormalizeKeyConPartidaYPalet(
                lineaBloqueada.CodigoAlmacenOrigen,
                lineaBloqueada.UbicacionOrigen,
                lineaBloqueada.Partida,
                lineaBloqueada.PaletOrigen);
            
            if (!string.IsNullOrEmpty(keyBloqueada))
            {
                ubicacionesUsadas.Add(keyBloqueada);
                _logger.LogInformation("🔴 Ubicación/Palet bloqueado agregado a excluidas: {Key}", keyBloqueada);
            }

            // Calcular cantidad restante
            // Importante:
            // - Evitar crear líneas hijas con cantidades residuales que, al persistir (p.ej. 4 decimales), quedan en 0.0000.
            // - Evitar asignar más que el stock disponible (p.ej. 17.99999 → 18.0000). Por eso TRUNCAMOS a 4 decimales,
            //   no redondeamos.
            static decimal Truncar4(decimal valor)
            {
                const decimal factor = 10000m;
                return Math.Truncate(valor * factor) / factor;
            }
            var consumoTotal = lineasCompletadas.Sum(l => l.CantidadMovida);
            var cantidadRestante = lineaPadre.CantidadPlan - consumoTotal;
            var cantidadRestanteTruncada = Truncar4(cantidadRestante);

            if (cantidadRestanteTruncada <= 0)
            {
                _logger.LogWarning("CrearLineaHijaExcluyendoUbicacionAsync: No hay cantidad restante");
                return false;
            }

            // 🔴 CRÍTICO: Ajustar stock disponible restando lo ya movido en traspasos de la misma orden
            // Cuando se ubica un palet, el stock se mueve a la ubicación destino del traspaso.
            // Ese stock aparece en StockDisponible de esa ubicación, pero ya fue contado para la orden.
            // Debemos restar esas cantidades del Disponible, no excluir la ubicación completa.
            var queryLineas = _context.OrdenTraspasoLinea
                .Where(l => l.IdOrdenTraspaso == lineaPadre.IdOrdenTraspaso &&
                           l.CodigoArticulo == lineaPadre.CodigoArticulo &&
                           l.IdTraspaso.HasValue);

            // Si la línea padre tiene partida, filtrar también por partida
            if (!string.IsNullOrWhiteSpace(lineaPadre.Partida))
            {
                var partidaNormalizada = lineaPadre.Partida.Trim().ToUpperInvariant();
                queryLineas = queryLineas.Where(l => l.Partida != null && 
                                                     l.Partida.Trim().ToUpperInvariant() == partidaNormalizada);
            }

            var lineasConTraspaso = await queryLineas
                .Select(l => l.IdTraspaso.Value)
                .Distinct()
                .ToListAsync();

            // Diccionario: Key (Almacen|Ubicacion|Partida) -> Cantidad total movida (para ajuste de destinos)
            var cantidadesMovidasPorUbicacion = new Dictionary<string, decimal>();
            if (lineasConTraspaso.Any())
            {
                var queryTraspasos = _context.Traspasos
                    .Where(t => lineasConTraspaso.Contains(t.Id) &&
                               !string.IsNullOrEmpty(t.AlmacenDestino) &&
                               t.CodigoArticulo == lineaPadre.CodigoArticulo);

                // Si la línea padre tiene partida, filtrar también por partida en traspasos
                if (!string.IsNullOrWhiteSpace(lineaPadre.Partida))
                {
                    var partidaNormalizada = lineaPadre.Partida.Trim().ToUpperInvariant();
                    queryTraspasos = queryTraspasos.Where(t => t.Partida != null && 
                                                               t.Partida.Trim().ToUpperInvariant() == partidaNormalizada);
                }

                var traspasos = await queryTraspasos
                    .Select(t => new { t.AlmacenDestino, t.UbicacionDestino, t.Partida, t.Cantidad })
                    .ToListAsync();

                // Agrupar por ubicación destino y sumar cantidades
                foreach (var traspaso in traspasos)
                {
                    // Clave de 3 componentes para destinos (no tienen palet)
                    var keyDestino = $"{Norm(traspaso.AlmacenDestino)}|{(string.IsNullOrWhiteSpace(traspaso.UbicacionDestino) ? "##EMPTY##" : Norm(traspaso.UbicacionDestino))}|{(string.IsNullOrWhiteSpace(traspaso.Partida) ? "##EMPTY##" : Norm(traspaso.Partida))}";
                    
                    if (!string.IsNullOrEmpty(keyDestino) && traspaso.Cantidad.HasValue)
                    {
                        if (!cantidadesMovidasPorUbicacion.ContainsKey(keyDestino))
                            cantidadesMovidasPorUbicacion[keyDestino] = 0;
                        
                        cantidadesMovidasPorUbicacion[keyDestino] += traspaso.Cantidad.Value;
                        _logger.LogInformation("🔴 Stock movido a ubicación destino: {Key} (Alm={Almacen}, Ubi={Ubicacion}, Part={Partida}, Cant={Cantidad})",
                            keyDestino, traspaso.AlmacenDestino, traspaso.UbicacionDestino, traspaso.Partida, traspaso.Cantidad.Value);
                    }
                }
            }

            // 🔴 MODIFICADO: Buscar primer stock (suelto o palet) no usado (excluyendo el bloqueado) en orden FEFO
            foreach (var stock in stockDesagregado)
            {
                // 🔴 NUEVO: Clave con 4 componentes
                var key = NormalizeKeyConPartidaYPalet(stock.CodigoAlmacen, stock.Ubicacion, stock.Partida, stock.CodigoPalet);

                // Si ya se usó este stock específico (incluyendo el bloqueado), saltar
                if (ubicacionesUsadas.Contains(key))
                {
                    _logger.LogDebug("Stock excluido (usado o bloqueado): {Key}", key);
                    continue;
                }

                // Clave de 3 componentes para ajuste de cantidades movidas (destino no tiene palet)
                var keyUbicacion = $"{Norm(stock.CodigoAlmacen)}|{(string.IsNullOrWhiteSpace(stock.Ubicacion) ? "##EMPTY##" : Norm(stock.Ubicacion))}|{(string.IsNullOrWhiteSpace(stock.Partida) ? "##EMPTY##" : Norm(stock.Partida))}";

                // 🔴 CRÍTICO: Ajustar stock disponible restando lo ya movido en traspasos
                var disponibleAjustado = stock.StockDisponible;
                if (cantidadesMovidasPorUbicacion.ContainsKey(keyUbicacion))
                {
                    var cantidadMovida = cantidadesMovidasPorUbicacion[keyUbicacion];
                    disponibleAjustado = Math.Max(0, stock.StockDisponible - cantidadMovida);
                    _logger.LogInformation("🔴 Ajustando stock disponible en ubicación destino: {Key} - Disponible original: {DisponibleOriginal}, Cantidad movida: {CantidadMovida}, Disponible ajustado: {DisponibleAjustado}",
                        keyUbicacion, stock.StockDisponible, cantidadMovida, disponibleAjustado);
                }

                // Si después del ajuste no hay stock disponible, saltar
                if (disponibleAjustado <= 0)
                {
                    _logger.LogDebug("Stock sin disponible después de ajuste: {Key}", key);
                    continue;
                }

                // Tomar el mínimo entre lo que queda por mover y lo que hay disponible (ajustado)
                var cantidadAsignada = Math.Min(cantidadRestante, disponibleAjustado);
                var cantidadAsignadaTruncada = Truncar4(cantidadAsignada);
                if (cantidadAsignadaTruncada <= 0) continue;

                _logger.LogInformation("✅ Creando línea hija nueva: Padre={PadreId}, Art={Art}, Stock seleccionado: Alm={Almacen}, Ubi={Ubicacion}, Part={Partida}, Tipo={Tipo}, Palet={Palet}, Cant={Cantidad}", 
                    lineaPadre.IdLineaOrdenTraspaso, lineaPadre.CodigoArticulo, 
                    stock.CodigoAlmacen, stock.Ubicacion, stock.Partida, stock.TipoStock, stock.CodigoPalet ?? "N/A", cantidadAsignada);

                var ordenLock = _ordenLocks.GetOrAdd(lineaPadre.IdOrdenTraspaso, _ => new SemaphoreSlim(1, 1));
                await ordenLock.WaitAsync();
                try
                {
                    var numeroLinea = await ObtenerSiguienteOrdenAsync(lineaPadre.IdOrdenTraspaso);

                    var lineaHija = new OrdenTraspasoLinea
                    {
                        IdLineaOrdenTraspaso = Guid.NewGuid(),
                        IdOrdenTraspaso = lineaPadre.IdOrdenTraspaso,
                        NumeroLinea = numeroLinea,
                        CodigoArticulo = lineaPadre.CodigoArticulo,
                        DescripcionArticulo = lineaPadre.DescripcionArticulo,
                        CantidadPlan = cantidadAsignadaTruncada,
                        CantidadMovida = 0,
                        Estado = "PENDIENTE",
                        CodigoAlmacenOrigen = stock.CodigoAlmacen,
                        UbicacionOrigen = stock.Ubicacion ?? string.Empty,
                        CodigoAlmacenDestino = lineaPadre.CodigoAlmacenDestino,
                        UbicacionDestino = lineaPadre.UbicacionDestino,
                        Partida = stock.Partida,
                        FechaCaducidad = stock.FechaCaducidad,
                        IdOperarioAsignado = lineaPadre.IdOperarioAsignado,
                        // 🔴 NUEVO: Asignar PaletOrigen si el stock es paletizado
                        PaletOrigen = stock.TipoStock == "Paletizado" ? stock.CodigoPalet : null
                    };

                    _context.OrdenTraspasoLinea.Add(lineaHija);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("✅ LÍNEA HIJA CREADA (excluyendo bloqueada): Id={Id}, NumLinea={NumLinea}, Art={Art}, Alm={Almacen}, Ubi={Ubicacion}, Part={Partida}, PaletOrigen={Palet}", 
                        lineaHija.IdLineaOrdenTraspaso, lineaHija.NumeroLinea, lineaHija.CodigoArticulo, 
                        lineaHija.CodigoAlmacenOrigen, lineaHija.UbicacionOrigen, lineaHija.Partida, lineaHija.PaletOrigen ?? "N/A (Suelto)");
                }
                finally
                {
                    ordenLock.Release();
                }

                return true;
            }

            _logger.LogWarning("CrearLineaHijaExcluyendoUbicacionAsync: No se encontró stock disponible (todos usados o bloqueados)");
            return false;
        }
        finally
        {
            semaforo.Release();
        }
    }

    private async Task<int> ObtenerSiguienteOrdenAsync(Guid idOrdenTraspaso)
    {
        var maxOrden = await _context.OrdenTraspasoLinea
            .Where(l => l.IdOrdenTraspaso == idOrdenTraspaso)
            .MaxAsync(l => (int?)l.NumeroLinea) ?? 0;

        return maxOrden + 1;
    }

    private async Task VerificarCompletitudOrdenAsync(Guid idOrdenTraspaso)
    {
        var orden = await _context.OrdenTraspasoCabecera
            .Include(o => o.Lineas)
            .FirstOrDefaultAsync(o => o.IdOrdenTraspaso == idOrdenTraspaso);

        if (orden == null || orden.Estado == "COMPLETADA") return;

        _logger.LogInformation("VerificarCompletitudOrdenAsync: Verificando orden {OrdenId} - Estado actual: {Estado}", idOrdenTraspaso, orden.Estado);

        // Verificar si todas las líneas están completadas, canceladas o sin stock
        // Una línea SUBDIVIDIDA se considera completada solo si sus líneas hijas han movido la cantidad total planificada
        var todasCompletadas = true;
        foreach (var linea in orden.Lineas)
        {
            if (linea.Estado == "COMPLETADA" || linea.Estado == "CANCELADA" || linea.Estado == "SIN_STOCK")
            {
                continue; // Estas líneas están terminadas
            }
            else if (linea.Estado == "SUBDIVIDIDA")
            {
                // Verificar si la cantidad completada de las hijas es igual a la cantidad plan
                var cantidadCompletada = await _context.OrdenTraspasoLinea
                    .Where(l => l.CodigoArticulo == linea.CodigoArticulo &&
                               l.IdOrdenTraspaso == linea.IdOrdenTraspaso &&
                               l.Estado == "COMPLETADA" &&
                               l.IdLineaOrdenTraspaso != linea.IdLineaOrdenTraspaso)
                    .SumAsync(l => l.CantidadMovida);

                if (cantidadCompletada >= linea.CantidadPlan)
                {
                    continue; // Esta línea subdividida está realmente completada
                }
                else
                {
                    todasCompletadas = false;
                    _logger.LogInformation("VerificarCompletitudOrdenAsync: Línea subdividida {LineaId} no completada - Completado: {Completado}/{Planificado}",
                        linea.IdLineaOrdenTraspaso, cantidadCompletada, linea.CantidadPlan);
                    break;
                }
            }
            else
            {
                // Líneas PENDIENTE, EN_PROCESO, etc.
                todasCompletadas = false;
                break;
            }
        }

        _logger.LogInformation("VerificarCompletitudOrdenAsync: Todas completadas: {TodasCompletadas} - Líneas: {Lineas}",
            todasCompletadas,
            string.Join(", ", orden.Lineas.Select(l => $"{l.NumeroLinea}:{l.Estado}")));

        if (todasCompletadas)
        {
            // Verificar si se movió ALGO de stock
            var seMovioAlgo = orden.Lineas.Any(l => l.CantidadMovida > 0);
            
            // Verificar tipos de líneas
            var hayLineasCompletadas = orden.Lineas.Any(l => l.Estado == "COMPLETADA");
            var todasSinStock = orden.Lineas.All(l => l.Estado == "SIN_STOCK");
            var todasCanceladas = orden.Lineas.All(l => l.Estado == "CANCELADA");
            var todasSinStockOCanceladas = orden.Lineas.All(l => l.Estado == "SIN_STOCK" || l.Estado == "CANCELADA");

            if (todasSinStockOCanceladas && !seMovioAlgo)
            {
                // Si TODAS las líneas están SIN_STOCK o CANCELADAS y NO se movió nada
                if (todasSinStock)
                {
                    _logger.LogInformation("VerificarCompletitudOrdenAsync: Todas las líneas SIN_STOCK y no se movió nada - marcando orden {OrdenId} como SIN_STOCK", idOrdenTraspaso);
                    orden.Estado = "SIN_STOCK";
                    orden.Comentarios = "No hay stock disponible para completar ninguna línea de esta orden";
                }
                else
                {
                    _logger.LogInformation("VerificarCompletitudOrdenAsync: Todas las líneas CANCELADAS/SIN_STOCK y no se movió nada - marcando orden {OrdenId} como CANCELADA", idOrdenTraspaso);
                    orden.Estado = "CANCELADA";
                    orden.Comentarios = "Orden cancelada: todas las líneas fueron canceladas o sin stock disponible";
                }
                orden.FechaFinalizacion = DateTime.Now;
                await _context.SaveChangesAsync();
            }
            else if (hayLineasCompletadas)
            {
                // 🔷 NUEVA VALIDACIÓN: No completar la orden si hay palets pendientes de ubicar
                var paletsPendientes = await GetPaletsPendientesAsync(idOrdenTraspaso);
                var paletsListosParaUbicar = paletsPendientes.Where(p => p.ListoParaUbicar).ToList();

                if (paletsListosParaUbicar.Any())
                {
                    _logger.LogWarning(
                        "VerificarCompletitudOrdenAsync: Orden {OrdenId} tiene {Cantidad} palet(s) pendiente(s) de ubicar (palets: {Palets}) - se mantiene estado {EstadoActual}",
                        idOrdenTraspaso,
                        paletsListosParaUbicar.Count,
                        string.Join(", ", paletsListosParaUbicar.Select(p => p.PaletDestino)),
                        orden.Estado);
                    // No marcar COMPLETADA; la orden se queda en su estado actual (normalmente EN_PROCESO)
                }
                else
                {
                    // Si hay al menos una línea COMPLETADA y no hay palets pendientes, la orden está COMPLETADA
                    _logger.LogInformation("VerificarCompletitudOrdenAsync: Hay líneas completadas y sin palets pendientes - marcando orden {OrdenId} como COMPLETADA", idOrdenTraspaso);
                    orden.Estado = "COMPLETADA";
                    orden.FechaFinalizacion = DateTime.Now;
                    await _context.SaveChangesAsync();
                }
            }
            else if (seMovioAlgo)
            {
                // Si se movió algo pero ninguna línea está COMPLETADA (trabajo parcial)
                // Dejar la orden EN_PROCESO para que puedan esperar más stock o cerrar manualmente
                _logger.LogInformation("VerificarCompletitudOrdenAsync: Se movió stock ({Total} unidades) pero quedan líneas pendientes - orden {OrdenId} sigue EN_PROCESO",
                    orden.Lineas.Sum(l => l.CantidadMovida), idOrdenTraspaso);
            }
        }
        else
        {
            _logger.LogInformation("VerificarCompletitudOrdenAsync: Orden {OrdenId} aún tiene líneas pendientes", idOrdenTraspaso);
        }
    }

    private async Task<bool> PrepararLineasInicialesAsync(OrdenTraspasoCabecera orden)
    {
        if (orden.Lineas == null || orden.Lineas.Count == 0) return false;

			var seModifico = false;

        var lineasSinOrigen = orden.Lineas
            .Where(l => l.Estado == "PENDIENTE" && string.IsNullOrWhiteSpace(l.CodigoAlmacenOrigen))
            .ToList();

			foreach (var linea in lineasSinOrigen)
        {
            var resultado = await IniciarDesgloseAsync(linea);
            seModifico = seModifico || resultado;
        }

			var lineasSubdivididas = orden.Lineas
				.Where(l => string.Equals(l.Estado, "SUBDIVIDIDA", StringComparison.OrdinalIgnoreCase))
				.ToList();

			foreach (var lineaPadre in lineasSubdivididas)
			{
				var resultado = await AsegurarLineaHijaActivaAsync(lineaPadre);
				seModifico = seModifico || resultado;
			}

        return seModifico;
    }

    private async Task RecargarLineasOrdenAsync(OrdenTraspasoCabecera orden)
    {
        var lineasActualizadas = await _context.OrdenTraspasoLinea
            .Where(l => l.IdOrdenTraspaso == orden.IdOrdenTraspaso)
            .OrderBy(l => l.NumeroLinea)
            .ToListAsync();

        orden.Lineas = lineasActualizadas;
    }

		private async Task<bool> AsegurarLineaHijaActivaAsync(OrdenTraspasoLinea lineaPadre)
		{
			if (lineaPadre == null)
			{
				return false;
			}

			var cantidadCompletada = await _context.OrdenTraspasoLinea
				.Where(l =>
					l.IdOrdenTraspaso == lineaPadre.IdOrdenTraspaso &&
					l.CodigoArticulo == lineaPadre.CodigoArticulo &&
					l.Estado == "COMPLETADA" &&
					l.IdLineaOrdenTraspaso != lineaPadre.IdLineaOrdenTraspaso)
				.SumAsync(l => l.CantidadMovida);

			if (cantidadCompletada >= lineaPadre.CantidadPlan)
			{
				return false;
			}

		// Buscar línea hija activa (PENDIENTE o EN_PROCESO)
		var lineaHijaActiva = await _context.OrdenTraspasoLinea
			.FirstOrDefaultAsync(l =>
				l.IdOrdenTraspaso == lineaPadre.IdOrdenTraspaso &&
				l.CodigoArticulo == lineaPadre.CodigoArticulo &&
				l.IdLineaOrdenTraspaso != lineaPadre.IdLineaOrdenTraspaso &&
				l.Estado != null &&
				(l.Estado == "PENDIENTE" || l.Estado == "EN_PROCESO"));

		if (lineaHijaActiva != null)
		{
		// Verificar si todavía hay stock en esa ubicación/partida
		var orden = await _context.OrdenTraspasoCabecera.FindAsync(lineaPadre.IdOrdenTraspaso);
		if (orden != null && lineaHijaActiva.Estado == "PENDIENTE")
		{
		// Obtener bloqueos de calidad para esta combinación específica
		var bloqueosCalidad = await ObtenerBloqueosCalidadAsync(
			orden.CodigoEmpresa,
			new[] { (
				Articulo: lineaHijaActiva.CodigoArticulo,
				Partida: lineaHijaActiva.Partida,
				Almacen: lineaHijaActiva.CodigoAlmacenOrigen,
				Ubicacion: lineaHijaActiva.UbicacionOrigen
			) }
		);

		var keyStock = $"{lineaHijaActiva.CodigoArticulo?.ToUpperInvariant()}|{lineaHijaActiva.Partida?.ToUpperInvariant()}|{lineaHijaActiva.CodigoAlmacenOrigen?.ToUpperInvariant()}|{lineaHijaActiva.UbicacionOrigen?.ToUpperInvariant()}";
		var estaBloqueado = bloqueosCalidad.Contains(keyStock);
		
		_logger.LogInformation("AsegurarLineaHijaActivaAsync: Verificando bloqueo - Key={Key}, Bloqueado={Bloqueado}, Total bloqueados={Total}",
			keyStock, estaBloqueado, bloqueosCalidad.Count);

			// Normalizar claves para evitar falsos "sin stock" por espacios/mayúsculas o null/""
			static string Norm(string? s) => (s ?? string.Empty).Trim().ToUpper();
			static decimal Truncar4(decimal valor)
			{
				const decimal factor = 10000m;
				return Math.Truncate(valor * factor) / factor;
			}

			var artLinea = (lineaHijaActiva.CodigoArticulo ?? string.Empty).Trim();
			var almLinea = Norm(lineaHijaActiva.CodigoAlmacenOrigen);
			var ubiLinea = Norm(lineaHijaActiva.UbicacionOrigen);
			var parLinea = Norm(lineaHijaActiva.Partida);

			// Nota: NO filtramos por Disponible en SQL para poder comparar con la misma regla de 4 decimales
			var stockDisponible = await _context.StockDisponible
				.FirstOrDefaultAsync(s =>
					s.CodigoEmpresa == orden.CodigoEmpresa &&
					s.CodigoArticulo == artLinea &&
					((s.CodigoAlmacen ?? string.Empty).Trim().ToUpper()) == almLinea &&
					((s.CodigoAlmacen ?? string.Empty).Trim().ToUpper()) != "004" && // Excluir almacén 004 (camión)
					((s.Ubicacion ?? string.Empty).Trim().ToUpper()) == ubiLinea &&
					((s.Partida ?? string.Empty).Trim().ToUpper()) == parLinea);

			// 🔴 CRÍTICO: No contar como "disponible" el stock que esta misma ORDEN ya movió a esta ubicación (por traspasos de palet).
			// Si no lo descontamos, la orden se "roba stock a sí misma" al reentrar (porque ese stock ya está en vStockDisponible).
			decimal movidoPorOrdenAEstaUbicacion = 0m;
			var traspasoIdsOrden = await _context.OrdenTraspasoLinea
				.Where(l => l.IdOrdenTraspaso == lineaPadre.IdOrdenTraspaso &&
							l.CodigoArticulo == artLinea &&
							l.IdTraspaso.HasValue)
				.Select(l => l.IdTraspaso!.Value)
				.Distinct()
				.ToListAsync();

			if (traspasoIdsOrden.Count > 0)
			{
				var traspasosOrden = await _context.Traspasos
					.Where(t => traspasoIdsOrden.Contains(t.Id) &&
								t.CodigoArticulo == artLinea &&
								t.CodigoEstado == "COMPLETADO" &&
								t.Cantidad.HasValue)
					.Select(t => new { t.AlmacenDestino, t.UbicacionDestino, t.Partida, t.Cantidad })
					.ToListAsync();

				movidoPorOrdenAEstaUbicacion = traspasosOrden
					.Where(t => Norm(t.AlmacenDestino) == almLinea &&
								Norm(t.UbicacionDestino) == ubiLinea &&
								Norm(t.Partida) == parLinea)
					.Sum(t => t.Cantidad!.Value);
			}

			var disponibleBruto = stockDisponible?.Disponible ?? 0m;
			var disponibleAjustado = Math.Max(0m, disponibleBruto - movidoPorOrdenAEstaUbicacion);

			_logger.LogInformation(
				"AsegurarLineaHijaActivaAsync: Stock bruto={Bruto}, MovidoPorOrden={Movido}, Stock ajustado={Ajustado} para {Art} {Alm}-{Ubi} Part={Part}",
				disponibleBruto, movidoPorOrdenAEstaUbicacion, disponibleAjustado, artLinea, almLinea, ubiLinea, parLinea);

			var hayStockSuficiente = stockDisponible != null && Truncar4(disponibleAjustado) >= lineaHijaActiva.CantidadPlan;

			if (!hayStockSuficiente || estaBloqueado)
			{
				_logger.LogWarning("AsegurarLineaHijaActivaAsync: Línea hija {LineaId} PENDIENTE sin stock disponible o bloqueado - cancelando y creando nueva",
					lineaHijaActiva.IdLineaOrdenTraspaso);

				// Stock ya no disponible o está bloqueado, cancelar esta línea hija obsoleta
				lineaHijaActiva.Estado = "CANCELADA";
				lineaHijaActiva.FechaFinalizacion = DateTime.Now;
				lineaHijaActiva.Completada = false;
				await _context.SaveChangesAsync();

				// Intentar crear una nueva línea hija con stock actual
				// (continúa al código que crea nueva línea más abajo)
			}
			else
			{
				// Stock todavía disponible y no bloqueado, mantener línea hija actual
				_logger.LogInformation("AsegurarLineaHijaActivaAsync: Línea hija {LineaId} PENDIENTE con stock disponible - no se crea nueva",
					lineaHijaActiva.IdLineaOrdenTraspaso);
				return false;
			}
		}
			else
			{
				// Línea EN_PROCESO, no tocar
				return false;
			}
		}

			var hijaCreada = await CrearLineaHijaAsync(lineaPadre);

			// Si no se pudo crear la línea hija por falta de stock, marcar como SIN_STOCK
			if (!hijaCreada)
			{
            _logger.LogWarning("AsegurarLineaHijaActivaAsync: No se pudo crear línea hija para {Art} en orden {Orden} - marcando línea padre como SIN_STOCK",
                lineaPadre.CodigoArticulo, lineaPadre.IdOrdenTraspaso);

            lineaPadre.Estado = "SIN_STOCK";
            lineaPadre.FechaFinalizacion = DateTime.Now;
            await _context.SaveChangesAsync();

				// Verificar si la orden debe cambiar de estado
				await VerificarCompletitudOrdenAsync(lineaPadre.IdOrdenTraspaso);

				return false;
			}

			return true;
		}

		private static string NormalizeUbicacionKey(string? almacen, string? ubicacion)
		{
			var almacenNormalizado = (almacen ?? string.Empty).Trim().ToUpperInvariant();
			var ubicacionNormalizada = string.IsNullOrWhiteSpace(ubicacion)
				? string.Empty
				: ubicacion.Trim().ToUpperInvariant();

			return $"{almacenNormalizado}|{ubicacionNormalizada}";
		}

		private static string NormalizeAlmacen(string? almacen)
		{
			return (almacen ?? string.Empty).Trim().ToUpperInvariant();
		}

	private async Task<HashSet<string>> ObtenerBloqueosCalidadAsync(short codigoEmpresa, IEnumerable<(string? Articulo, string? Partida, string? Almacen, string? Ubicacion)> combinaciones)
	{
		var combinacionesValidas = combinaciones
			.Where(c => !string.IsNullOrWhiteSpace(c.Articulo) && !string.IsNullOrWhiteSpace(c.Partida))
			.Select(c => new
			{
				Articulo = (c.Articulo ?? string.Empty).Trim(),
				Partida = (c.Partida ?? string.Empty).Trim(),
				Almacen = (c.Almacen ?? string.Empty).Trim(),
				// Normalizar ubicación: null o whitespace → ""
				Ubicacion = string.IsNullOrWhiteSpace(c.Ubicacion) ? "" : c.Ubicacion.Trim(),
				Key = $"{(c.Articulo ?? string.Empty).Trim().ToUpperInvariant()}|{(c.Partida ?? string.Empty).Trim().ToUpperInvariant()}|{(c.Almacen ?? string.Empty).Trim().ToUpperInvariant()}|{(string.IsNullOrWhiteSpace(c.Ubicacion) ? "" : c.Ubicacion.Trim()).ToUpperInvariant()}"
			})
			.GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
			.Select(g => g.First())
			.ToList();

		if (!combinacionesValidas.Any())
		{
			return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		}

		var resultado = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		// Verificar cada combinación usando la lógica de TraspasoController
		foreach (var combinacion in combinacionesValidas)
		{
			var queryBloqueo = _context.BloqueosCalidad
				.Where(b => b.CodigoEmpresa == codigoEmpresa &&
						   b.CodigoArticulo == combinacion.Articulo &&
						   b.LotePartida == combinacion.Partida &&
						   b.CodigoAlmacen == combinacion.Almacen &&
						   b.Bloqueado);

			// Filtrar por ubicación como en TraspasoController
			if (!string.IsNullOrWhiteSpace(combinacion.Ubicacion))
			{
				queryBloqueo = queryBloqueo.Where(b => b.Ubicacion == combinacion.Ubicacion);
			}
			else
			{
				queryBloqueo = queryBloqueo.Where(b => string.IsNullOrEmpty(b.Ubicacion));
			}

			var bloqueo = await queryBloqueo.FirstOrDefaultAsync();

			if (bloqueo != null)
			{
				_logger.LogInformation("ObtenerBloqueosCalidadAsync: BLOQUEADO - Art={Art}, Partida={Partida}, Alm={Alm}, Ubi={Ubi}, BloqueoUbi={BloqueoUbi}",
					combinacion.Articulo, combinacion.Partida, combinacion.Almacen, 
					combinacion.Ubicacion == "" ? "(vacía)" : combinacion.Ubicacion, 
					string.IsNullOrEmpty(bloqueo.Ubicacion) ? "(vacía)" : bloqueo.Ubicacion);
				resultado.Add(combinacion.Key);
			}
			else
			{
				_logger.LogInformation("ObtenerBloqueosCalidadAsync: NO BLOQUEADO - Art={Art}, Partida={Partida}, Alm={Alm}, Ubi={Ubi}",
					combinacion.Articulo, combinacion.Partida, combinacion.Almacen, 
					combinacion.Ubicacion == "" ? "(vacía)" : combinacion.Ubicacion);
			}
		}

		return resultado;
	}

		private async Task<HashSet<string>> ObtenerUbicacionesPulmonAsync(short codigoEmpresa, IEnumerable<(string? Almacen, string? Ubicacion)> ubicaciones)
		{
			var combinaciones = ubicaciones
				.Select(u => new
				{
					Almacen = (u.Almacen ?? string.Empty),
					Ubicacion = u.Ubicacion ?? string.Empty,
					Key = NormalizeUbicacionKey(u.Almacen, u.Ubicacion)
				})
				.Where(x => !string.IsNullOrWhiteSpace(x.Almacen))
				.GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
				.Select(g => g.First())
				.ToList();

			if (!combinaciones.Any())
			{
				return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			}

			var clavesObjetivo = combinaciones
				.Select(c => c.Key)
				.ToHashSet(StringComparer.OrdinalIgnoreCase);

			var posiblesPulmones = await (from cfg in _context.Ubicaciones_Configuracion
										  join tipo in _context.TipoUbicaciones on cfg.TipoUbicacionId equals tipo.TipoUbicacionId
										  where cfg.CodigoEmpresa == codigoEmpresa
										  select new { cfg.CodigoAlmacen, cfg.Ubicacion, tipo.Descripcion })
										 .ToListAsync();

			var resultado = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (var item in posiblesPulmones)
			{
				if (!string.Equals(item.Descripcion?.Trim(), "PULMON", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				var key = NormalizeUbicacionKey(item.CodigoAlmacen, item.Ubicacion);
				if (clavesObjetivo.Contains(key))
				{
					resultado.Add(key);
				}
			}

			return resultado;
		}

    private async Task<List<string>> ObtenerAlmacenesAutorizadosAsync(int operarioId, short codigoEmpresa)
    {
        try
        {
            // 1. Obtener almacenes individuales del operario
            var almacenesIndividuales = await _sageContext.OperariosAlmacenes
                .Where(a => a.Operario == operarioId && a.CodigoEmpresa == codigoEmpresa)
                .Select(a => a.CodigoAlmacen!)
                .Where(a => a != null) // Filtrar nulls
                .ToListAsync();

            // 2. Obtener el centro logístico del operario
            var operario = await _sageContext.Operarios
                .Where(o => o.Id == operarioId)
                .Select(o => o.CodigoCentro)
                .FirstOrDefaultAsync();

            var todosLosAlmacenes = new List<string>(almacenesIndividuales);

            List<string> almacenesCentro = new();

            // 3. Si el operario tiene centro logístico, obtener sus almacenes
            if (!string.IsNullOrEmpty(operario))
            {
                almacenesCentro = await _sageContext.Almacenes
                    .Where(a => a.CodigoCentro == operario && a.CodigoEmpresa == codigoEmpresa)
                    .Select(a => a.CodigoAlmacen!)
                    .Where(a => a != null)
                    .ToListAsync();

                todosLosAlmacenes.AddRange(almacenesCentro);
            }

            // 4. Eliminar duplicados y devolver
            var resultado = todosLosAlmacenes.Distinct().ToList();

            _logger.LogDebug("OrdenTraspasoService: Operario {OperarioId} almacenes individuales: {Ind}, centro: {Centro}, total: {Total}",
                operarioId,
                string.Join(", ", almacenesIndividuales),
                string.IsNullOrEmpty(operario) ? "-" : string.Join(", ", almacenesCentro),
                string.Join(", ", resultado));

            return resultado;
        }
        catch (Exception)
        {
            // En caso de error, devolver lista vacía para no bloquear la consulta
            _logger.LogError("Error obteniendo almacenes autorizados para operario {OperarioId}", operarioId);
            return new List<string>();
        }
    }
}
} 
