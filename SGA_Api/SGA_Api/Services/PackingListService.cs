using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.PackingList;

namespace SGA_Api.Services
{
    public class PackingListService : IPackingListService
    {
        private readonly SageDbContext _sage;

        public PackingListService(SageDbContext sage)
        {
            _sage = sage;
        }

        public async Task<PackingListDto?> GetPackingListAsync(short codigoEmpresa, int ejercicio, string serie, int numero)
        {
            var ejercicioShort = (short)ejercicio;

            var baseQuery = from ofab in _sage.OrdenesFabricacion.AsNoTracking()
                            join cpc in _sage.CabeceraPedidoCliente.AsNoTracking()
                                on new { ofab.CodigoEmpresa, ofab.EjercicioPedido, ofab.SeriePedido, ofab.NumeroPedido }
                                equals new { cpc.CodigoEmpresa, cpc.EjercicioPedido, cpc.SeriePedido, cpc.NumeroPedido } into cpcJoin
                            from cpc in cpcJoin.DefaultIfEmpty()
                            join ot in _sage.OrdenesTrabajo.AsNoTracking()
                                on new { ofab.CodigoEmpresa, ofab.EjercicioFabricacion, ofab.SerieFabricacion, ofab.NumeroFabricacion, ofab.CodigoArticulo }
                                equals new { ot.CodigoEmpresa, ot.EjercicioFabricacion, ot.SerieFabricacion, ot.NumeroFabricacion, ot.CodigoArticulo } into otJoin
                            from ot in otJoin.DefaultIfEmpty()
                            where ofab.CodigoEmpresa == codigoEmpresa
                                  && ofab.EjercicioFabricacion == ejercicioShort
                                  && ofab.SerieFabricacion == serie
                                  && ofab.NumeroFabricacion == numero
                            select new { Ofab = ofab, Cpc = cpc, Ot = ot };

            var row = await baseQuery.FirstOrDefaultAsync();
            if (row == null)
                return null;

            var partidas = new List<string>();
            if (row.Ot != null)
            {
                partidas = await _sage.Incidencias
                    .AsNoTracking()
                    .Where(i => i.CodigoEmpresa == row.Ot.CodigoEmpresa
                                && i.EjercicioTrabajo == row.Ot.EjercicioTrabajo
                                && i.NumeroTrabajo == row.Ot.NumeroTrabajo)
                    .Select(i => i.Partida)
                    .Distinct()
                    .ToListAsync();
            }

            return new PackingListDto
            {
                EjercicioFabricacion = row.Ofab.EjercicioFabricacion,
                SerieFabricacion = row.Ofab.SerieFabricacion,
                NumeroFabricacion = row.Ofab.NumeroFabricacion,
                CodigoArticulo = row.Ofab.CodigoArticulo,
                EjercicioPedido = row.Ofab.EjercicioPedido,
                SeriePedido = row.Ofab.SeriePedido,
                NumeroPedido = row.Ofab.NumeroPedido,
                FechaRegistro = row.Ofab.FechaRegistro,
                CodigoCliente = row.Cpc?.CodigoCliente ?? "",
                RazonSocial = row.Cpc?.RazonSocial ?? "",
                CodigoAlmacen = row.Ot?.CodigoAlmacen ?? "",
                CodigoEmpresa = row.Ot?.CodigoEmpresa ?? row.Ofab.CodigoEmpresa,
                EjercicioTrabajo = row.Ot?.EjercicioTrabajo ?? (short)0,
                NumeroTrabajo = row.Ot?.NumeroTrabajo ?? 0,
                Partidas = partidas
            };
        }
    }
}
