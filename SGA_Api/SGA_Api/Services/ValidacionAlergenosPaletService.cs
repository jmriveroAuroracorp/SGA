using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Palet;
using SGA_Api.Models.Stock;

namespace SGA_Api.Services
{
    public class ValidacionAlergenosPaletService : IValidacionAlergenosPaletService
    {
        private readonly AuroraSgaDbContext _auroraSgaContext;
        private readonly SageDbContext _sageContext;
        private readonly ILogger<ValidacionAlergenosPaletService> _logger;

        public ValidacionAlergenosPaletService(
            AuroraSgaDbContext auroraSgaContext,
            SageDbContext sageContext,
            ILogger<ValidacionAlergenosPaletService> logger)
        {
            _auroraSgaContext = auroraSgaContext;
            _sageContext = sageContext;
            _logger = logger;
        }

        public async Task<ValidacionAlergenosPaletResult> ValidarAlergenosPaletAsync(
            Guid paletId,
            string codigoArticulo,
            short codigoEmpresa)
        {
            try
            {
                // 1. Obtener información del palet
                var palet = await _auroraSgaContext.Palets
                    .Where(p => p.Id == paletId)
                    .Select(p => new { p.Id, p.Codigo, p.CodigoEmpresa })
                    .FirstOrDefaultAsync();

                if (palet == null)
                {
                    _logger.LogWarning($"⚠️ VALIDACIÓN ALÉRGENOS PALET - Palet {paletId} no encontrado");
                    return ValidacionAlergenosPaletResult.Bloqueado(
                        $"No se encontró el palet especificado.",
                        codigoArticulo);
                }

                // 2. Obtener todas las líneas del palet (definitivas y temporales con cantidad positiva)
                var lineasDefinitivas = await _auroraSgaContext.PaletLineas
                    .Where(pl => pl.PaletId == paletId && pl.Cantidad > 0)
                    .Select(pl => new { pl.CodigoArticulo, pl.FechaAgregado })
                    .ToListAsync();

                var lineasTemporales = await _auroraSgaContext.TempPaletLineas
                    .Where(tpl => tpl.PaletId == paletId && tpl.Cantidad > 0 && !tpl.Procesada)
                    .Select(tpl => new { tpl.CodigoArticulo, tpl.FechaAgregado })
                    .ToListAsync();

                // 3. Combinar y ordenar por FechaAgregado para obtener el primer artículo
                var todasLasLineas = lineasDefinitivas
                    .Select(l => new { l.CodigoArticulo, l.FechaAgregado })
                    .Concat(lineasTemporales.Select(l => new { l.CodigoArticulo, l.FechaAgregado }))
                    .OrderBy(l => l.FechaAgregado)
                    .ToList();

                // 4. Si el palet está vacío, permitir cualquier artículo (establece el patrón)
                if (!todasLasLineas.Any())
                {
                    _logger.LogInformation($"ℹ️ VALIDACIÓN ALÉRGENOS PALET - Palet {palet.Codigo} está vacío. Se permite añadir artículo {codigoArticulo} (establece el patrón de alérgenos).");
                    return ValidacionAlergenosPaletResult.Valido();
                }

                // 5. Obtener el primer artículo (el más antiguo)
                var primerArticulo = todasLasLineas.First();
                var codigoPrimerArticulo = primerArticulo.CodigoArticulo;

                // 6. Obtener alérgenos del primer artículo
                var primerArticuloInfo = await _sageContext.VisArticulos
                    .AsNoTracking()
                    .Where(a => a.CodigoEmpresa == codigoEmpresa && a.CodigoArticulo == codigoPrimerArticulo)
                    .FirstOrDefaultAsync();

                var alergenosPrimerArticulo = primerArticuloInfo?.VNEWAlergenos ?? string.Empty;

                // 7. Obtener alérgenos del nuevo artículo
                var nuevoArticuloInfo = await _sageContext.VisArticulos
                    .AsNoTracking()
                    .Where(a => a.CodigoEmpresa == codigoEmpresa && a.CodigoArticulo == codigoArticulo)
                    .FirstOrDefaultAsync();

                var alergenosNuevoArticulo = nuevoArticuloInfo?.VNEWAlergenos ?? string.Empty;

                // 8. Parsear y normalizar alérgenos
                var alergenosPrimerArticuloNormalizados = await NormalizarAlergenos(alergenosPrimerArticulo, codigoEmpresa);
                var alergenosNuevoArticuloNormalizados = await NormalizarAlergenos(alergenosNuevoArticulo, codigoEmpresa);

                // 9. Comparar alérgenos
                if (!SonAlergenosIguales(alergenosPrimerArticuloNormalizados, alergenosNuevoArticuloNormalizados))
                {
                    var descripcionPrimerArticulo = primerArticuloInfo?.DescripcionArticulo ?? codigoPrimerArticulo;
                    var descripcionNuevoArticulo = nuevoArticuloInfo?.DescripcionArticulo ?? codigoArticulo;

                    var alergenosPrimerStr = string.IsNullOrWhiteSpace(alergenosPrimerArticulo) 
                        ? "sin alérgenos" 
                        : alergenosPrimerArticulo;
                    var alergenosNuevoStr = string.IsNullOrWhiteSpace(alergenosNuevoArticulo) 
                        ? "sin alérgenos" 
                        : alergenosNuevoArticulo;

                    _logger.LogWarning($"🚫 VALIDACIÓN ALÉRGENOS PALET - Palet {palet.Codigo}: El artículo {codigoArticulo} ({descripcionNuevoArticulo}) tiene alérgenos diferentes al primer artículo del palet. " +
                        $"Primer artículo: {codigoPrimerArticulo} ({descripcionPrimerArticulo}) con alérgenos: {alergenosPrimerStr}. " +
                        $"Nuevo artículo: {codigoArticulo} ({descripcionNuevoArticulo}) con alérgenos: {alergenosNuevoStr}.");

                    return ValidacionAlergenosPaletResult.Bloqueado(
                        $"No se puede añadir el artículo {codigoArticulo} al palet {palet.Codigo}. " +
                        $"El palet ya contiene artículos con alérgenos diferentes. " +
                        $"El primer artículo del palet ({codigoPrimerArticulo}) tiene los siguientes alérgenos: {alergenosPrimerStr}. " +
                        $"El artículo que intenta añadir ({codigoArticulo}) tiene: {alergenosNuevoStr}. " +
                        $"Para prevenir contaminación cruzada, todos los artículos de un palet deben tener exactamente los mismos alérgenos.",
                        codigoArticulo,
                        palet.Codigo);
                }

                _logger.LogInformation($"✅ VALIDACIÓN ALÉRGENOS PALET - Palet {palet.Codigo}: El artículo {codigoArticulo} tiene los mismos alérgenos que el primer artículo del palet. Validación exitosa.");
                return ValidacionAlergenosPaletResult.Valido();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ ERROR validando alérgenos en palet {paletId} para artículo {codigoArticulo}");
                // Fail-secure: bloquear por seguridad alimentaria
                return ValidacionAlergenosPaletResult.Bloqueado(
                    $"No se puede validar los alérgenos del artículo {codigoArticulo} para el palet. Por seguridad, la operación ha sido bloqueada. Contacte con el administrador del sistema.",
                    codigoArticulo);
            }
        }

        /// <summary>
        /// Normaliza los alérgenos parseándolos y convirtiéndolos a códigos
        /// </summary>
        private async Task<List<int>> NormalizarAlergenos(string alergenosStr, short codigoEmpresa)
        {
            if (string.IsNullOrWhiteSpace(alergenosStr))
            {
                return new List<int>(); // Sin alérgenos
            }

            // Parsear alérgenos (formato: "GLUTEN,HUEVO,SOJA,LECHE")
            var descripcionesAlergenos = alergenosStr
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(d => d.Trim().ToUpper())
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .ToList();

            if (!descripcionesAlergenos.Any())
            {
                return new List<int>();
            }

            // Convertir descripciones a códigos usando el maestro
            var codigosAlergenos = await _auroraSgaContext.AlergenoMaestros
                .Where(am => am.CodigoEmpresa == codigoEmpresa &&
                           descripcionesAlergenos.Contains(am.VDescripcionAlergeno.ToUpper()))
                .Select(am => (int)am.VCodigoAlergeno)
                .OrderBy(c => c)
                .ToListAsync();

            return codigosAlergenos;
        }

        /// <summary>
        /// Compara si dos listas de códigos de alérgenos son iguales
        /// </summary>
        private bool SonAlergenosIguales(List<int> alergenos1, List<int> alergenos2)
        {
            // Comparar si tienen los mismos códigos (ordenados)
            return alergenos1.OrderBy(a => a).SequenceEqual(alergenos2.OrderBy(a => a));
        }
    }
}
