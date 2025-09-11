using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.OrdenTraspaso;

namespace SGA_Api.Services
{
    public class OrdenTraspasoService : IOrdenTraspasoService
    {
        private readonly AuroraSgaDbContext _context;

        public OrdenTraspasoService(AuroraSgaDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<OrdenTraspasoDto>> GetOrdenesTraspasoAsync(short? codigoEmpresa = null, string? estado = null, int? usuarioAsignado = null)
        {
            var query = _context.OrdenTraspasoCabecera
                .Include(o => o.Lineas)
                .AsQueryable();

            if (codigoEmpresa.HasValue)
                query = query.Where(o => o.CodigoEmpresa == codigoEmpresa.Value);

            if (!string.IsNullOrEmpty(estado))
                query = query.Where(o => o.Estado == estado);

            if (usuarioAsignado.HasValue)
                query = query.Where(o => o.UsuarioAsignado == usuarioAsignado.Value);

            var ordenes = await query.OrderByDescending(o => o.FechaCreacion).ToListAsync();

            return ordenes.Select(MapToDto);
        }

        public async Task<OrdenTraspasoDto?> GetOrdenTraspasoAsync(Guid id)
        {
            var orden = await _context.OrdenTraspasoCabecera
                .Include(o => o.Lineas)
                .FirstOrDefaultAsync(o => o.IdOrdenTrabajo == id);

            return orden != null ? MapToDto(orden) : null;
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
                IdOrigen = dto.IdOrigen,
                UsuarioCreacion = dto.UsuarioCreacion,
                UsuarioAsignado = dto.UsuarioAsignado,
                Comentarios = dto.Comentarios,
                FechaCreacion = DateTime.Now
            };

            _context.OrdenTraspasoCabecera.Add(orden);

            // Agregar líneas
            foreach (var lineaDto in dto.Lineas)
            {
                var linea = new OrdenTraspasoLinea
                {
                    IdOrdenTrabajo = orden.IdOrdenTrabajo,
                    Orden = lineaDto.Orden,
                    CodigoArticulo = lineaDto.CodigoArticulo,
                    DescripcionArticulo = lineaDto.DescripcionArticulo,
                    UM = lineaDto.UM,
                    CantidadPlan = lineaDto.CantidadPlan,
                    CodigoAlmacenOrigen = lineaDto.CodigoAlmacenOrigen,
                    UbicacionOrigen = lineaDto.UbicacionOrigen,
                    PartidaOrigen = lineaDto.PartidaOrigen,
                    PaletOrigen = lineaDto.PaletOrigen,
                    CodigoAlmacenDestino = lineaDto.CodigoAlmacenDestino,
                    UbicacionDestino = lineaDto.UbicacionDestino,
                    PartidaDestino = lineaDto.PartidaDestino,
                    PaletDestino = lineaDto.PaletDestino,
                    Estado = "PENDIENTE",
                    CantidadMovida = 0,
                    Completada = false
                };

                _context.OrdenTraspasoLinea.Add(linea);
            }

            await _context.SaveChangesAsync();

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

            if (dto.UsuarioAsignado.HasValue)
                orden.UsuarioAsignado = dto.UsuarioAsignado.Value;

            if (dto.Comentarios != null)
                orden.Comentarios = dto.Comentarios;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ActualizarLineaOrdenTraspasoAsync(Guid id, ActualizarLineaOrdenTraspasoDto dto)
        {
            var linea = await _context.OrdenTraspasoLinea.FindAsync(id);
            if (linea == null) return false;

            if (!string.IsNullOrEmpty(dto.Estado))
                linea.Estado = dto.Estado;

            if (dto.CantidadMovida.HasValue)
                linea.CantidadMovida = dto.CantidadMovida.Value;

            if (dto.Completada.HasValue)
                linea.Completada = dto.Completada.Value;

            if (dto.IdOperarioAsignado.HasValue)
                linea.IdOperarioAsignado = dto.IdOperarioAsignado.Value;

            if (dto.FechaInicio.HasValue)
                linea.FechaInicio = dto.FechaInicio.Value;

            if (dto.FechaFinalizacion.HasValue)
                linea.FechaFinalizacion = dto.FechaFinalizacion.Value;

            if (dto.IdTraspaso.HasValue)
                linea.IdTraspaso = dto.IdTraspaso.Value;

            if (dto.IdLineaTraspaso.HasValue)
                linea.IdLineaTraspaso = dto.IdLineaTraspaso.Value;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> CompletarOrdenTraspasoAsync(Guid id)
        {
            var orden = await _context.OrdenTraspasoCabecera.FindAsync(id);
            if (orden == null) return false;

            orden.Estado = "COMPLETADA";
            orden.FechaFinalizacion = DateTime.Now;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> CancelarOrdenTraspasoAsync(Guid id)
        {
            var orden = await _context.OrdenTraspasoCabecera.FindAsync(id);
            if (orden == null) return false;

            orden.Estado = "CANCELADA";
            orden.FechaFinalizacion = DateTime.Now;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> EliminarOrdenTraspasoAsync(Guid id)
        {
            var orden = await _context.OrdenTraspasoCabecera.FindAsync(id);
            if (orden == null) return false;

            _context.OrdenTraspasoCabecera.Remove(orden);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RegistrarMovimientoAsync(RegistrarMovimientoDto dto)
        {
            var movimiento = new OrdenTraspasoMovimiento
            {
                IdOrdenTrabajo = dto.IdLineaOrden, // TODO: Obtener el ID de la orden desde la línea
                IdLineaOrden = dto.IdLineaOrden,
                IdTraspaso = dto.IdTraspaso,
                IdLineaTraspaso = dto.IdLineaTraspaso,
                FechaMovimiento = DateTime.Now,
                IdOperario = dto.IdOperario,
                Comentarios = dto.Comentarios
            };

            _context.OrdenTraspasoMovimiento.Add(movimiento);
            await _context.SaveChangesAsync();
            return true;
        }

        private static OrdenTraspasoDto MapToDto(OrdenTraspasoCabecera orden)
        {
            return new OrdenTraspasoDto
            {
                IdOrdenTrabajo = orden.IdOrdenTrabajo,
                CodigoEmpresa = orden.CodigoEmpresa,
                Estado = orden.Estado,
                Prioridad = orden.Prioridad,
                FechaPlan = orden.FechaPlan,
                FechaInicio = orden.FechaInicio,
                FechaFinalizacion = orden.FechaFinalizacion,
                TipoOrigen = orden.TipoOrigen,
                IdOrigen = orden.IdOrigen,
                UsuarioCreacion = orden.UsuarioCreacion,
                UsuarioAsignado = orden.UsuarioAsignado,
                Comentarios = orden.Comentarios,
                FechaCreacion = orden.FechaCreacion,
                Lineas = orden.Lineas.Select(MapLineaToDto).ToList()
            };
        }

        private static LineaOrdenTraspasoDetalleDto MapLineaToDto(OrdenTraspasoLinea linea)
        {
            return new LineaOrdenTraspasoDetalleDto
            {
                IdLineaOrden = linea.IdLineaOrden,
                IdOrdenTrabajo = linea.IdOrdenTrabajo,
                Orden = linea.Orden,
                CodigoArticulo = linea.CodigoArticulo,
                DescripcionArticulo = linea.DescripcionArticulo,
                UM = linea.UM,
                CantidadPlan = linea.CantidadPlan,
                CodigoAlmacenOrigen = linea.CodigoAlmacenOrigen,
                UbicacionOrigen = linea.UbicacionOrigen,
                PartidaOrigen = linea.PartidaOrigen,
                PaletOrigen = linea.PaletOrigen,
                CodigoAlmacenDestino = linea.CodigoAlmacenDestino,
                UbicacionDestino = linea.UbicacionDestino,
                PartidaDestino = linea.PartidaDestino,
                PaletDestino = linea.PaletDestino,
                Estado = linea.Estado,
                CantidadMovida = linea.CantidadMovida,
                Completada = linea.Completada,
                IdOperarioAsignado = linea.IdOperarioAsignado,
                FechaInicio = linea.FechaInicio,
                FechaFinalizacion = linea.FechaFinalizacion,
                IdTraspaso = linea.IdTraspaso,
                IdLineaTraspaso = linea.IdLineaTraspaso
            };
        }
    }
} 