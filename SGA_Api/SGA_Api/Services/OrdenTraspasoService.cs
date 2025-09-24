using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.OrdenTraspaso;
using Microsoft.Data.SqlClient;
using System.Data;

namespace SGA_Api.Services
{
    public class OrdenTraspasoService : IOrdenTraspasoService
    {
        private readonly AuroraSgaDbContext _context;

        public OrdenTraspasoService(AuroraSgaDbContext context)
        {
            _context = context;
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
            var orden = await _context.OrdenTraspasoCabecera
                .Include(o => o.Lineas)
                .FirstOrDefaultAsync(o => o.IdOrdenTraspaso == id);

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

            if (dto.IdOperarioAsignado > 0)
                linea.IdOperarioAsignado = dto.IdOperarioAsignado;

            if (dto.FechaInicio.HasValue)
                linea.FechaInicio = dto.FechaInicio.Value;

            if (dto.FechaFinalizacion.HasValue)
                linea.FechaFinalizacion = dto.FechaFinalizacion.Value;

            if (dto.IdTraspaso.HasValue)
                linea.IdTraspaso = dto.IdTraspaso.Value;


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
            var orden = await _context.OrdenTraspasoCabecera
                .Include(o => o.Lineas)
                .FirstOrDefaultAsync(o => o.IdOrdenTraspaso == id);
            
            if (orden == null) 
                return false;

            // Validación: Solo se puede cancelar si está PENDIENTE o EN_PROCESO
            if (orden.Estado != "PENDIENTE" && orden.Estado != "EN_PROCESO")
                return false;

            // Validación: No se puede cancelar si ya hay movimientos realizados
            var tieneMovimientos = orden.Lineas.Any(l => l.CantidadMovida > 0);
            if (tieneMovimientos)
                return false;

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
    }
} 