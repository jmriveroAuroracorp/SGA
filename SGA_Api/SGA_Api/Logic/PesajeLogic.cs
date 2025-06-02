using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SGA_Api.Services;
using SGA_Api.Models.Pesaje;
using System.Data;


namespace SGA_Api.Logic
{
    public class PesajeLogic : IPesajeService
    {
        private readonly IConfiguration _configuration;

        public PesajeLogic(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<PesajeResponseDto?> GetPesajeAsync(int ejercicio, string serie, int numero)
        {
            var connectionString = _configuration.GetConnectionString("Sage");
            var query = @"
                SELECT 
                    OrdenesFabricacion.EjercicioFabricacion,
                    OrdenesFabricacion.SerieFabricacion,
                    OrdenesFabricacion.NumeroFabricacion,
                    OrdenesTrabajo.EjercicioTrabajo,
                    OrdenesTrabajo.NumeroTrabajo,
                    OrdenesTrabajo.CodigoArticulo AS CodigoArticuloOT,
                    OrdenesTrabajo.DescripcionArticulo AS DescripcionArticuloOT,
                    MRH_PesosPesadores.ArticuloComponente,
                    MRH_PesosPesadores.IdAmasijo,
                    OrdenesTrabajo.CodigoArticulo,
                    MRH_AmasijosPesado.NumeroAmasijo,
                    OrdenesFabricacion.VNumeroAmasijos,
                    MRH_PesosPesadores.DescripcionArticulo,
                    MRH_PesosPesadores.Partida,
                    FORMAT(MRH_PesosPesadores.FechaCaduca,'dd-MM-yyyy') AS FechaCaduca,
                    MRH_PesosPesadores.UnidadesComponente,
                    MRH_PesosPesadores.MRH_AgrupacionAmasijo
                FROM OrdenesFabricacion
                LEFT OUTER JOIN OrdenesTrabajo WITH (NOLOCK) ON 
                    OrdenesFabricacion.CodigoEmpresa = OrdenesTrabajo.CodigoEmpresa AND 
                    OrdenesFabricacion.EjercicioFabricacion = OrdenesTrabajo.EjercicioFabricacion AND 
                    OrdenesFabricacion.SerieFabricacion = OrdenesTrabajo.SerieFabricacion AND 
                    OrdenesFabricacion.NumeroFabricacion = OrdenesTrabajo.NumeroFabricacion
                LEFT OUTER JOIN MRH_PesosPesadores WITH (NOLOCK) ON 
                    OrdenesTrabajo.CodigoEmpresa = MRH_PesosPesadores.CodigoEmpresa AND 
                    OrdenesTrabajo.EjercicioFabricacion = MRH_PesosPesadores.EjercicioFabricacion AND 
                    OrdenesTrabajo.SerieFabricacion = MRH_PesosPesadores.SerieFabricacion AND 
                    OrdenesTrabajo.NumeroFabricacion = MRH_PesosPesadores.NumeroFabricacion AND 
                    OrdenesTrabajo.EjercicioTrabajo = MRH_PesosPesadores.EjercicioTrabajo AND 
                    OrdenesTrabajo.NumeroTrabajo = MRH_PesosPesadores.NumeroTrabajo 
                LEFT OUTER JOIN MRH_AmasijosPesado WITH (NOLOCK) ON 
                    MRH_AmasijosPesado.CodigoEmpresa = MRH_PesosPesadores.CodigoEmpresa AND 
                    MRH_AmasijosPesado.EjercicioFabricacion = MRH_PesosPesadores.EjercicioFabricacion AND 
                    MRH_AmasijosPesado.SerieFabricacion = MRH_PesosPesadores.SerieFabricacion AND 
                    MRH_AmasijosPesado.NumeroFabricacion = MRH_PesosPesadores.NumeroFabricacion AND 
                    MRH_AmasijosPesado.EjercicioTrabajo = MRH_PesosPesadores.EjercicioTrabajo AND 
                    MRH_AmasijosPesado.NumeroTrabajo = MRH_PesosPesadores.NumeroTrabajo AND
                    MRH_AmasijosPesado.IdAmasijo = MRH_PesosPesadores.IdAmasijo
                WHERE
                    OrdenesFabricacion.EjercicioFabricacion = @EJ AND
                    OrdenesFabricacion.SerieFabricacion = @SER AND
                    OrdenesFabricacion.NumeroFabricacion = @NUM
                ORDER BY 
                    OrdenesTrabajo.NivelCompuesto DESC,
                    MRH_PesosPesadores.EjercicioFabricacion,
                    MRH_PesosPesadores.SerieFabricacion,
                    MRH_PesosPesadores.NumeroFabricacion,
                    MRH_PesosPesadores.EjercicioTrabajo,
                    MRH_PesosPesadores.NumeroTrabajo";

            var datos = new List<PesajeSqlRawDto>();

            using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@EJ", ejercicio);
                cmd.Parameters.AddWithValue("@SER", serie);
                cmd.Parameters.AddWithValue("@NUM", numero);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    datos.Add(new PesajeSqlRawDto
                    {
                        EjercicioFabricacion = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0)),
                        SerieFabricacion = reader.GetString(1),
                        NumeroFabricacion = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2)),
                        CodigoArticuloOT = reader.IsDBNull(5) ? null : reader.GetString(5),
                        DescripcionArticuloOT = reader.IsDBNull(6) ? null : reader.GetString(6),
                        ArticuloComponente = reader.IsDBNull(7) ? null : reader.GetString(7),
                        IdAmasijo = reader.IsDBNull(8) ? null : reader.GetValue(8) as Guid?,
                        NumeroAmasijo = reader.IsDBNull(10) ? null : reader.GetString(10),
                        VNumeroAmasijos = reader.IsDBNull(11) ? 0 : Convert.ToDecimal(reader.GetValue(11)),
                        DescripcionArticulo = reader.IsDBNull(12) ? null : reader.GetString(12),
                        Partida = reader.IsDBNull(13) ? null : reader.GetString(13),
                        FechaCaduca = reader.IsDBNull(14) ? null : reader.GetString(14),
                        UnidadesComponente = reader.IsDBNull(15) ? 0 : Convert.ToDecimal(reader.GetValue(15))
                    });
                }
            }

            if (!datos.Any())
                return null;

            var primeraFila = datos.First();
            var response = new PesajeResponseDto
            {
                EjercicioFabricacion = primeraFila.EjercicioFabricacion,
                SerieFabricacion = primeraFila.SerieFabricacion!,
                NumeroFabricacion = primeraFila.NumeroFabricacion,
                VNumeroAmasijos = primeraFila.VNumeroAmasijos
            };

            var agrupadoOTs = datos.GroupBy(d => new { d.CodigoArticuloOT, d.DescripcionArticuloOT });
            foreach (var ot in agrupadoOTs)
            {
                var pesajeOt = new PesajeOtDto
                {
                    CodigoArticuloOT = ot.Key.CodigoArticuloOT,
                    DescripcionArticuloOT = ot.Key.DescripcionArticuloOT
                };

                var agrupadoAmasijos = ot.GroupBy(d => d.IdAmasijo ?? Guid.Empty);
                foreach (var amasijo in agrupadoAmasijos)
                {
                    var amasijoDto = new PesajeAmasijoDto
                    {
                        Amasijo = amasijo.Key == Guid.Empty ? "Sin amasijo" : amasijo.First().NumeroAmasijo ?? "Desconocido",
                        TotalPesado = amasijo.Sum(x => x.UnidadesComponente)
                    };

                    foreach (var linea in amasijo.Where(x => x.ArticuloComponente != null))
                    {
                        amasijoDto.Componentes.Add(new PesajeComponenteDto
                        {
                            ArticuloComponente = linea.ArticuloComponente,
                            DescripcionArticulo = linea.DescripcionArticulo,
                            Partida = linea.Partida,
                            FechaCaduca = linea.FechaCaduca,
                            UnidadesComponente = linea.UnidadesComponente
                        });
                    }

                    pesajeOt.Amasijos.Add(amasijoDto);
                }

                response.OrdenesTrabajo.Add(pesajeOt);
            }

            return response;
        }
    }
}
