using SGA_Api.Data;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Models.Palet;
using SGA_Api.Models.Notificaciones;
using SGA_Api.Models.Traspasos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace SGA_Api.Services
{
	public class TraspasoFinalizacionBackgroundService : BackgroundService
	{
		private readonly IServiceProvider _serviceProvider;
		private readonly TimeSpan _intervalo = TimeSpan.FromSeconds(5); // Se ejecuta cada 0.5 segundos para detectar cambios muy r�pidos
		private bool _enEjecucion = false;
		private readonly bool _soloProcesarPendientes;
		private DateTime _ultimaLimpiezaDiariaSinLineas = DateTime.MinValue;
		
		// Diccionario para almacenar estados anteriores de traspasos (para detectar cambios)
		private readonly Dictionary<Guid, string> _estadosAnterioresTraspasos = new();

		public TraspasoFinalizacionBackgroundService(IServiceProvider serviceProvider, IConfiguration configuration)
		{
			_serviceProvider = serviceProvider;
			_soloProcesarPendientes = configuration.GetValue<bool>("BackgroundService:SoloProcesarPendientes", true);
		}

        /// <summary>
        /// Procesa InventarioAjustes asociados a un Palet (PaletId no nulo) cuando el ajuste está COMPLETADO por el integrador ERP.
        /// Aplica el delta en PaletLineas y registra LogPalet. Evita crear "suelto" cuando el ajuste era de palet.
        /// </summary>
        private async Task ProcesarAjustesInventarioPorPaletAsync(AuroraSgaDbContext dbContext, SageDbContext sageDbContext, ILogger<TraspasoFinalizacionBackgroundService> logger)
        {
            try
            {
                var ajustes = await dbContext.InventarioAjustes
                    .Where(a => a.PaletId != null && a.Estado == "COMPLETADO" && a.ProcesadoPalet == false)
                    .OrderBy(a => a.Fecha)
                    .ThenByDescending(a => a.Diferencia) // Procesar positivos antes que negativos para cambios de artículo
                    .ThenBy(a => a.IdCambioArticulo.HasValue ? 0 : 1) // Agrupar cambios de artículo juntos
                    .Take(200)
                    .ToListAsync();

                if (!ajustes.Any()) return;

                foreach (var aj in ajustes)
                {
                    using var tx = await dbContext.Database.BeginTransactionAsync();
                    try
                    {
                        var paletId = aj.PaletId!.Value;
                        var delta = aj.Diferencia; // puede ser +/-
                        var art = aj.CodigoArticulo;
                        var alm = aj.CodigoAlmacen;
                        var ubi = aj.CodigoUbicacion;
                        var lote = aj.Partida;
                        var cad = aj.FechaCaducidad;

                        // Buscar líneas del palet para esa clave
                        var lineasClave = await dbContext.PaletLineas
                            .Where(pl => pl.PaletId == paletId && pl.CodigoArticulo == art)
                            .ToListAsync();

                        var coincidentes = lineasClave.Where(pl =>
                                (pl.CodigoAlmacen ?? "").Trim().ToUpper() == (alm ?? "").Trim().ToUpper() &&
                                (pl.Ubicacion ?? "").Trim().ToUpper() == (ubi ?? "").Trim().ToUpper() &&
                                (pl.Lote ?? "") == (lote ?? "") &&
                                pl.FechaCaducidad == cad)
                            .ToList();

                        if (delta > 0)
                        {
                            if (coincidentes.Any())
                            {
                                var linea = coincidentes.First();
                                linea.Cantidad += delta;
                                dbContext.PaletLineas.Update(linea);
                            }
                            else
                            {
                                // Obtener descripción desde TempPaletLineas (igual que inventarios y conteos)
                                string? descripcionArticulo = null;
                                
                                if (aj.IdInventario.HasValue && aj.IdInventario.Value != Guid.Empty)
                                {
                                    var tempLinea = await dbContext.TempPaletLineas
                                        .Where(tpl => tpl.PaletId == paletId && 
                                                     tpl.InventarioId == aj.IdInventario.Value &&
                                                     (tpl.CodigoArticulo ?? "").Trim().ToUpper() == (art ?? "").Trim().ToUpper())
                                        .FirstOrDefaultAsync();
                                    
                                    if (tempLinea != null && !string.IsNullOrWhiteSpace(tempLinea.DescripcionArticulo))
                                    {
                                        descripcionArticulo = tempLinea.DescripcionArticulo.Trim();
                                    }
                                }
                                else if (aj.IdConteo.HasValue && aj.IdConteo.Value != Guid.Empty)
                                {
                                    var tempLinea = await dbContext.TempPaletLineas
                                        .Where(tpl => tpl.PaletId == paletId && 
                                                     tpl.ConteoId == aj.IdConteo.Value &&
                                                     (tpl.CodigoArticulo ?? "").Trim().ToUpper() == (art ?? "").Trim().ToUpper())
                                        .FirstOrDefaultAsync();
                                    
                                    if (tempLinea != null && !string.IsNullOrWhiteSpace(tempLinea.DescripcionArticulo))
                                    {
                                        descripcionArticulo = tempLinea.DescripcionArticulo.Trim();
                                    }
                                }
                                else if (aj.IdCambioArticulo.HasValue && aj.IdCambioArticulo.Value != Guid.Empty)
                                {
                                    // Para cambio de artículo, buscar la TempPaletLinea que coincida con el signo del ajuste
                                    var tempLinea = await dbContext.TempPaletLineas
                                        .Where(tpl => tpl.PaletId == paletId && 
                                                     tpl.CambioArticuloId == aj.IdCambioArticulo.Value &&
                                                     (tpl.CodigoArticulo ?? "").Trim().ToUpper() == (art ?? "").Trim().ToUpper() &&
                                                     ((aj.Diferencia > 0 && tpl.Cantidad > 0) || (aj.Diferencia < 0 && tpl.Cantidad < 0)))
                                        .FirstOrDefaultAsync();
                                    
                                    if (tempLinea != null && !string.IsNullOrWhiteSpace(tempLinea.DescripcionArticulo))
                                    {
                                        descripcionArticulo = tempLinea.DescripcionArticulo.Trim();
                                    }
                                }
                                
                                // Si no se encontró en TempPaletLineas, intentar desde Sage u otras fuentes
                                if (string.IsNullOrWhiteSpace(descripcionArticulo))
                                {
                                    descripcionArticulo = await ObtenerDescripcionArticuloAsync(
                                        dbContext, sageDbContext, paletId, art, aj.CodigoEmpresa, logger);
                                }
                                
                                dbContext.PaletLineas.Add(new PaletLinea
                                {
                                    Id = Guid.NewGuid(),
                                    PaletId = paletId,
                                    CodigoEmpresa = aj.CodigoEmpresa,
                                    CodigoArticulo = art,
                                    DescripcionArticulo = descripcionArticulo,
                                    Cantidad = delta,
                                    UnidadMedida = null,
                                    Lote = lote,
                                    FechaCaducidad = cad,
                                    CodigoAlmacen = alm,
                                    Ubicacion = ubi,
                                    UsuarioId = aj.UsuarioId,
                                    FechaAgregado = DateTime.Now,
                                    Observaciones = "AjusteInventario",
                                    TraspasoId = null
                                });
                            }
                        }
                        else if (delta < 0)
                        {
                            var restante = Math.Abs(delta);
                            foreach (var l in coincidentes.OrderByDescending(x => x.FechaAgregado))
                            {
                                if (restante <= 0) break;
                                var quitar = Math.Min(restante, l.Cantidad);
                                l.Cantidad -= quitar;
                                restante -= quitar;
                                if (l.Cantidad <= 0m || Math.Abs(l.Cantidad) < 0.0001m)
                                    dbContext.PaletLineas.Remove(l);
                                else
                                    dbContext.PaletLineas.Update(l);
                            }
                        }

                        // LogPalet
                        dbContext.LogPalet.Add(new LogPalet
                        {
                            PaletId = paletId,
                            Fecha = DateTime.Now,
                            IdUsuario = aj.UsuarioId,
                            Accion = "AjusteInventario",
                            Detalle = $"Aplicado ajuste por palet ±{aj.Diferencia:F4} Art={art} Ubi={alm}-{ubi}"
                        });

                        // 🔷 NUEVO: Marcar TempPaletLineas de conteo como procesadas cuando el ajuste se completa
                        // Si el ajuste tiene IdConteo, buscar y marcar las TempPaletLineas correspondientes
                        if (aj.IdConteo.HasValue && aj.IdConteo.Value != Guid.Empty)
                        {
                            var tempLineasConteo = await dbContext.TempPaletLineas
                                .Where(tpl => tpl.PaletId == paletId && 
                                             tpl.ConteoId == aj.IdConteo.Value && 
                                             tpl.Procesada == false)
                                .ToListAsync();

                            foreach (var tempLinea in tempLineasConteo)
                            {
                                tempLinea.Procesada = true;
                                dbContext.TempPaletLineas.Update(tempLinea);
                                logger.LogInformation("✅ TempPaletLinea {TempId} marcada como procesada tras completar ajuste de conteo {ConteoId}", 
                                    tempLinea.Id, aj.IdConteo);
                            }
                        }

                        // 🔷 NUEVO: Marcar TempPaletLineas de inventario como procesadas cuando el ajuste se completa
                        // Si el ajuste tiene IdInventario, buscar y marcar las TempPaletLineas correspondientes
                        if (aj.IdInventario.HasValue && aj.IdInventario.Value != Guid.Empty)
                        {
                            logger.LogInformation("🔍 Buscando TempPaletLineas de inventario: PaletId={PaletId}, InventarioId={InventarioId}, Articulo={Articulo}", 
                                paletId, aj.IdInventario.Value, art);
                            
                            // Normalizar código de artículo para comparación (trim y mayúsculas)
                            var artNormalizado = (art ?? "").Trim().ToUpper();
                            
                            var tempLineasInventario = await dbContext.TempPaletLineas
                                .Where(tpl => tpl.PaletId == paletId && 
                                             tpl.InventarioId == aj.IdInventario.Value && 
                                             (tpl.CodigoArticulo ?? "").Trim().ToUpper() == artNormalizado &&
                                             tpl.Procesada == false)
                                .ToListAsync();

                            logger.LogInformation("🔍 Encontradas {Cantidad} TempPaletLineas de inventario pendientes", tempLineasInventario.Count);

                            if (!tempLineasInventario.Any())
                            {
                                // 🔷 FALLBACK: Intentar buscar sin filtrar por artículo (por si hay discrepancias)
                                var tempLineasInventarioFallback = await dbContext.TempPaletLineas
                                    .Where(tpl => tpl.PaletId == paletId && 
                                                 tpl.InventarioId == aj.IdInventario.Value && 
                                                 tpl.Procesada == false)
                                    .ToListAsync();
                                
                                if (tempLineasInventarioFallback.Any())
                                {
                                    logger.LogWarning("⚠️ Encontradas {Cantidad} TempPaletLineas de inventario sin filtrar por artículo. Artículos: {Articulos}", 
                                        tempLineasInventarioFallback.Count, 
                                        string.Join(", ", tempLineasInventarioFallback.Select(t => t.CodigoArticulo).Distinct()));
                                    
                                    // Usar las encontradas sin filtrar por artículo
                                    tempLineasInventario = tempLineasInventarioFallback;
                                }
                            }

                            foreach (var tempLinea in tempLineasInventario)
                            {
                                tempLinea.Procesada = true;
                                dbContext.TempPaletLineas.Update(tempLinea);
                                logger.LogInformation("✅ TempPaletLinea {TempId} marcada como procesada tras completar ajuste de inventario {InventarioId}", 
                                    tempLinea.Id, aj.IdInventario);
                            }
                        }
                        else
                        {
                            logger.LogWarning("⚠️ Ajuste {AjusteId} no tiene IdInventario, no se pueden marcar TempPaletLineas como procesadas", aj.IdAjuste);
                        }

                        // 🔷 NUEVO: Marcar TempPaletLineas de cambio de artículo como procesadas cuando el ajuste se completa
                        // Si el ajuste tiene IdCambioArticulo, buscar y marcar las TempPaletLineas correspondientes
                        if (aj.IdCambioArticulo.HasValue && aj.IdCambioArticulo.Value != Guid.Empty)
                        {
                            logger.LogInformation("🔍 Buscando TempPaletLineas de cambio de artículo: PaletId={PaletId}, CambioArticuloId={CambioArticuloId}, Articulo={Articulo}, Diferencia={Diferencia}", 
                                paletId, aj.IdCambioArticulo.Value, art, aj.Diferencia);
                            
                            // Normalizar código de artículo para comparación (trim y mayúsculas)
                            var artNormalizado = (art ?? "").Trim().ToUpper();
                            
                            // Buscar TempPaletLineas que coincidan con el CambioArticuloId y el código de artículo
                            // También filtrar por el signo de la cantidad (positivo para entrada, negativo para salida)
                            var tempLineasCambio = await dbContext.TempPaletLineas
                                .Where(tpl => tpl.PaletId == paletId && 
                                             tpl.CambioArticuloId == aj.IdCambioArticulo.Value && 
                                             (tpl.CodigoArticulo ?? "").Trim().ToUpper() == artNormalizado &&
                                             // Filtrar por signo: si el ajuste es positivo, buscar TempPaletLinea positiva; si es negativo, buscar negativa
                                             ((aj.Diferencia > 0 && tpl.Cantidad > 0) || (aj.Diferencia < 0 && tpl.Cantidad < 0)) &&
                                             tpl.Procesada == false)
                                .ToListAsync();

                            logger.LogInformation("🔍 Encontradas {Cantidad} TempPaletLineas de cambio de artículo pendientes (filtradas por artículo y signo)", tempLineasCambio.Count);

                            if (!tempLineasCambio.Any())
                            {
                                // 🔷 FALLBACK 1: Intentar buscar sin filtrar por signo (por si hay discrepancias)
                                var tempLineasCambioFallback = await dbContext.TempPaletLineas
                                    .Where(tpl => tpl.PaletId == paletId && 
                                                 tpl.CambioArticuloId == aj.IdCambioArticulo.Value && 
                                                 (tpl.CodigoArticulo ?? "").Trim().ToUpper() == artNormalizado &&
                                                 tpl.Procesada == false)
                                    .ToListAsync();
                                
                                if (tempLineasCambioFallback.Any())
                                {
                                    logger.LogWarning("⚠️ Encontradas {Cantidad} TempPaletLineas de cambio de artículo sin filtrar por signo. Artículos: {Articulos}, Cantidades: {Cantidades}", 
                                        tempLineasCambioFallback.Count, 
                                        string.Join(", ", tempLineasCambioFallback.Select(t => t.CodigoArticulo).Distinct()),
                                        string.Join(", ", tempLineasCambioFallback.Select(t => t.Cantidad.ToString("F6"))));
                                    
                                    tempLineasCambio = tempLineasCambioFallback;
                                }
                                else
                                {
                                    // 🔷 FALLBACK 2: Intentar buscar sin filtrar por artículo ni signo (último recurso)
                                    var tempLineasCambioFallback2 = await dbContext.TempPaletLineas
                                        .Where(tpl => tpl.PaletId == paletId && 
                                                     tpl.CambioArticuloId == aj.IdCambioArticulo.Value && 
                                                     tpl.Procesada == false)
                                        .ToListAsync();
                                    
                                    if (tempLineasCambioFallback2.Any())
                                    {
                                        logger.LogWarning("⚠️ Encontradas {Cantidad} TempPaletLineas de cambio de artículo sin filtrar por artículo ni signo. Artículos: {Articulos}, Cantidades: {Cantidades}", 
                                            tempLineasCambioFallback2.Count, 
                                            string.Join(", ", tempLineasCambioFallback2.Select(t => t.CodigoArticulo).Distinct()),
                                            string.Join(", ", tempLineasCambioFallback2.Select(t => t.Cantidad.ToString("F6"))));
                                        
                                        // Si hay múltiples, intentar encontrar la que coincida mejor con el ajuste
                                        // Priorizar: mismo código de artículo > mismo signo
                                        var mejorCoincidencia = tempLineasCambioFallback2
                                            .OrderByDescending(t => (t.CodigoArticulo ?? "").Trim().ToUpper() == artNormalizado ? 1 : 0)
                                            .ThenByDescending(t => ((aj.Diferencia > 0 && t.Cantidad > 0) || (aj.Diferencia < 0 && t.Cantidad < 0)) ? 1 : 0)
                                            .FirstOrDefault();
                                        
                                        if (mejorCoincidencia != null)
                                        {
                                            tempLineasCambio = new List<TempPaletLinea> { mejorCoincidencia };
                                            logger.LogInformation("✅ Seleccionada mejor coincidencia: TempId={TempId}, Articulo={Articulo}, Cantidad={Cantidad}", 
                                                mejorCoincidencia.Id, mejorCoincidencia.CodigoArticulo, mejorCoincidencia.Cantidad);
                                        }
                                    }
                                }
                            }

                            foreach (var tempLinea in tempLineasCambio)
                            {
                                tempLinea.Procesada = true;
                                dbContext.TempPaletLineas.Update(tempLinea);
                                logger.LogInformation("✅ TempPaletLinea {TempId} marcada como procesada tras completar ajuste de cambio de artículo {CambioArticuloId}. Articulo={Articulo}, Cantidad={Cantidad}", 
                                    tempLinea.Id, aj.IdCambioArticulo, tempLinea.CodigoArticulo, tempLinea.Cantidad);
                            }
                        }

                        // Marcar como procesado
                        aj.ProcesadoPalet = true;
                        dbContext.InventarioAjustes.Update(aj);

                        await dbContext.SaveChangesAsync();
                        await tx.CommitAsync();
                    }
                    catch (Exception ex)
                    {
                        await tx.RollbackAsync();
                        logger.LogError(ex, "Error al procesar InventarioAjuste por palet {AjusteId}", aj.IdAjuste);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error en ProcesarAjustesInventarioPorPaletAsync");
            }
        }
		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				if (_enEjecucion)
				{
					await Task.Delay(_intervalo, stoppingToken);
					continue;
				}
				_enEjecucion = true;
				try
				{
					var permitirLimpiezaDiariaSinLineas = DebeEjecutarLimpiezaDiariaSinLineas();
					var seRealizoLimpiezaDiariaSinLineas = false;

					IServiceScope? scope = null;
					try
					{
						scope = _serviceProvider.CreateScope();
					}
					catch (ObjectDisposedException)
					{
						// El contenedor de DI se está liberando, salir del bucle
						break;
					}
					catch (InvalidOperationException)
					{
						// El contenedor de DI ya no está disponible, salir del bucle
						break;
					}

					if (scope == null)
						break;

					using (scope)
				{
					var dbContext = scope.ServiceProvider.GetRequiredService<AuroraSgaDbContext>();
					var sageDbContext = scope.ServiceProvider.GetRequiredService<SageDbContext>();
					var notificacionesUnificadas = scope.ServiceProvider.GetRequiredService<INotificacionesUnificadasService>();
					var logger = scope.ServiceProvider.GetRequiredService<ILogger<TraspasoFinalizacionBackgroundService>>();

                        // 1. DETECCIÓN DE NOTIFICACIONES - Para TODOS los traspasos (ARTICULO y PALET)
						await DetectarYNotificarCambiosEstadoAsync(dbContext, sageDbContext, notificacionesUnificadas, logger);

                        // 1.1. PROCESAR AJUSTES DE INVENTARIO POR PALET (COMPLETADO)
                        await ProcesarAjustesInventarioPorPaletAsync(dbContext, sageDbContext, logger);

                        // 1.2. DETECCIÓN DE ERRORES EN AJUSTES DE INVENTARIO
                        await DetectarYNotificarErroresInventariosAsync(dbContext, sageDbContext, notificacionesUnificadas, logger);

						// 2. CONSOLIDACI�N DE PALETS - Solo para traspasos que afectan palets
						// Obtener todos los paletId con l�neas temporales
					var query = dbContext.TempPaletLineas.AsQueryable();
					
					// Filtro configurable: solo procesar líneas pendientes si está habilitado
					if (_soloProcesarPendientes)
					{
						query = query.Where(l => l.Procesada == false);
					}
					
				var paletIdsConTempLineas = await query
					.Select(l => l.PaletId)
					.Distinct()
					.ToListAsync();
				
				// NUEVO: También procesar palets con traspasos completados recientes (PALET y ARTICULO)
				var hace1Hora = DateTime.Now.AddHours(-1);
				var hace24Horas = DateTime.Now.AddHours(-24);

				var paletIdsConTraspasos = await dbContext.Traspasos
					.Where(t => (t.TipoTraspaso == "PALET" || t.TipoTraspaso == "ARTICULO") && 
								t.CodigoEstado == "COMPLETADO" && 
								t.FechaFinalizacion >= hace1Hora &&
								t.PaletId != Guid.Empty)
					.Select(t => t.PaletId)
					.Distinct()
					.ToListAsync();
				
				// 🔷 NUEVO: También procesar palets que deberían estar vaciados pero no lo están
				// (tienen todas las temporales procesadas, no tienen definitivas, pero no están vaciados)
				// Buscar palets que tienen temporales pero todas están procesadas
				var paletsConTemporales = await dbContext.TempPaletLineas
					.Select(tpl => tpl.PaletId)
					.Distinct()
					.ToListAsync();
				
				// De esos, buscar los que NO tienen temporales pendientes (todas procesadas)
				var paletsSinTemporalesPendientes = await dbContext.TempPaletLineas
					.Where(tpl => paletsConTemporales.Contains(tpl.PaletId) && tpl.Procesada == false)
					.Select(tpl => tpl.PaletId)
					.Distinct()
					.ToListAsync();
				
				var paletIdsConTodasTemporalesProcesadas = paletsConTemporales
					.Where(id => !paletsSinTemporalesPendientes.Contains(id))
					.ToList();
				
				// Filtrar: solo los que NO tienen definitivas y NO están vaciados
				var paletsCandidatosVaciado = await dbContext.Palets
					.Where(p => paletIdsConTodasTemporalesProcesadas.Contains(p.Id) &&
							   p.Estado != "Vaciado" && p.Estado.ToUpper() != "VACIADO")
					.Select(p => p.Id)
					.ToListAsync();
				
				var paletsConDefinitivas = await dbContext.PaletLineas
					.Where(pl => paletsCandidatosVaciado.Contains(pl.PaletId))
					.Select(pl => pl.PaletId)
					.Distinct()
					.ToListAsync();
				
				var paletIdsParaVaciado = paletsCandidatosVaciado
					.Where(id => !paletsConDefinitivas.Contains(id))
					.ToList();
				
				// 🔷 NUEVO: Buscar palets sin ninguna línea (ni temporales ni definitivas) - eliminados manualmente
				// Estos palets deberían estar vaciados automáticamente
				var todosPaletIdsConTemporales = await dbContext.TempPaletLineas
					.Select(tpl => tpl.PaletId)
					.Distinct()
					.ToListAsync();
				
				var todosPaletIdsConDefinitivas = await dbContext.PaletLineas
					.Select(pl => pl.PaletId)
					.Distinct()
					.ToListAsync();
				
				// 🔷 PROTECCIÓN: Excluir palets recién creados (menos de 1 hora) que aún no tienen líneas
				// Solo procesar palets sin líneas si están cerrados o tienen más de 1 hora de antigüedad
				var paletsSinNingunaLinea = await dbContext.Palets
					.Where(p => p.Id != Guid.Empty &&
							   !todosPaletIdsConTemporales.Contains(p.Id) &&
							   !todosPaletIdsConDefinitivas.Contains(p.Id) &&
							   p.Estado != "Vaciado" && p.Estado.ToUpper() != "VACIADO" &&
							   (
								   (p.FechaCierre != null && p.FechaCierre >= hace24Horas) ||
								   (p.FechaVaciado != null && p.FechaVaciado >= hace24Horas) ||
								   p.FechaApertura >= hace24Horas
							   ) &&
							   // NO procesar palets recién creados (abiertos con menos de 1 hora)
							   !(p.Estado == "Abierto" && p.FechaApertura >= hace1Hora))
					.Select(p => p.Id)
					.ToListAsync();
				
				// Unir todas las listas
				var paletIdsAProcesar = paletIdsConTempLineas
					.Union(paletIdsConTraspasos)
					.Union(paletIdsParaVaciado)
					.Union(paletsSinNingunaLinea)
					.Distinct()
					.ToList();

				foreach (var paletId in paletIdsAProcesar)
					{
						// Usar transacción para garantizar consistencia
						using var transaction = await dbContext.Database.BeginTransactionAsync();
						try
						{
							// 1) Todas las temporales pendientes de este palet (en orden)
							var tempsPendientes = await dbContext.TempPaletLineas
								.Where(l => l.PaletId == paletId && l.Procesada == false)
								.OrderBy(l => l.FechaAgregado)
								.ToListAsync();
							var paletTuvoTemporalesPendientes = tempsPendientes.Count > 0;

							// 🔷 HashSet para rastrear traspasos con ERROR_ERP ya procesados en esta iteración
							// Evita eliminar las mismas temporales múltiples veces
							var traspasosErrorErpProcesados = new HashSet<Guid>();

					foreach (var temp in tempsPendientes)
					{
						// 🔷 PROTECCIÓN: Las líneas de conteo, inventario y cambio de artículo NO se consolidan aquí
						// Solo se procesan cuando el InventarioAjustes asociado está COMPLETADO
						// y se aplican a través de ProcesarAjustesInventarioPorPaletAsync
						if (temp.ConteoId != null && temp.ConteoId != Guid.Empty)
						{
							// Línea de conteo - no procesar aquí, esperar a que el ajuste esté COMPLETADO
							continue;
						}
						
						if (temp.InventarioId != null && temp.InventarioId != Guid.Empty)
						{
							// Línea de inventario - no procesar aquí, esperar a que el ajuste esté COMPLETADO
							continue;
						}
						
						if (temp.CambioArticuloId != null && temp.CambioArticuloId != Guid.Empty)
						{
							// Línea de cambio de artículo - no procesar aquí, esperar a que el ajuste esté COMPLETADO
							continue;
						}

						// 2) Busca el traspaso de la temporal
						var traspaso = await dbContext.Traspasos.FindAsync(temp.TraspasoId);
						if (traspaso == null) continue;
						
					// 2.2) Si el traspaso tiene ERROR_ERP, ELIMINAR TODAS las temporales asociadas (no procesarlas)
					// Esto limpia las líneas (positivas y negativas) cuando el traspaso falla
                    if (traspaso.CodigoEstado == "ERROR_ERP")
                    {
                        // 🔷 PROTECCIÓN: No eliminar si el ERROR_ERP es reciente (menos de 4 horas)
                        // Esto permite que el servicio externo haga retry y complete el traspaso
                        var fechaError = traspaso.FechaFinalizacion ?? traspaso.FechaInicio;
                        var horasDesdeError = (DateTime.Now - fechaError).TotalHours;
                        
                        if (horasDesdeError < 4)
                        {
                            logger.LogInformation("⏳ ERROR_ERP reciente ({Horas:F1} horas) en traspaso {TraspasoId} (Tipo: {TipoTraspaso}, Articulo: {Articulo}). Esperando retry antes de eliminar líneas temporales.",
                                horasDesdeError, traspaso.Id, traspaso.TipoTraspaso, traspaso.CodigoArticulo);
                            continue; // NO eliminar, esperar retry
                        }
                        
                        // Si el ERROR_ERP es antiguo (>4 horas), proceder con la eliminación normal
                        
                        // 🔷 Evitar procesar el mismo traspaso múltiples veces en esta iteración
                        if (traspasosErrorErpProcesados.Contains(traspaso.Id))
                        {
                            // Este traspaso ya fue procesado, solo continuar (la temporal ya fue eliminada)
                            continue;
                        }
                        
                        // Marcar este traspaso como procesado
                        traspasosErrorErpProcesados.Add(traspaso.Id);
                        
                        // 🔷 CRÍTICO: Buscar SOLO las TempPaletLinea asociadas a ESTE traspaso específico (por TraspasoId)
                        // NO eliminar líneas de otros traspasos aunque sean del mismo artículo
                        // Esto es crítico porque un palet puede tener múltiples traspasos del mismo artículo
                        var todasLasTemporalesDelTraspaso = await dbContext.TempPaletLineas
                            .Where(tpl => tpl.TraspasoId == traspaso.Id && tpl.Procesada == false)
                            .ToListAsync();
                        
                        if (todasLasTemporalesDelTraspaso.Any())
                        {
                            // 🔷 VALIDACIÓN ADICIONAL: Verificar que todas las líneas realmente pertenecen a este traspaso
                            var lineasConTraspasoIdIncorrecto = todasLasTemporalesDelTraspaso
                                .Where(t => t.TraspasoId != traspaso.Id)
                                .ToList();
                            
                            if (lineasConTraspasoIdIncorrecto.Any())
                            {
                                logger.LogError("❌ ERROR CRÍTICO: Se encontraron líneas temporales con TraspasoId incorrecto. Esto no debería pasar. TraspasoId esperado: {TraspasoId}, Líneas encontradas: {Cantidad}",
                                    traspaso.Id, lineasConTraspasoIdIncorrecto.Count);
                                // NO eliminar estas líneas, solo las que realmente pertenecen al traspaso
                                todasLasTemporalesDelTraspaso = todasLasTemporalesDelTraspaso
                                    .Where(t => t.TraspasoId == traspaso.Id)
                                    .ToList();
                            }
                            
                            logger.LogWarning("🚨 ERROR_ERP detectado en traspaso {TraspasoId} (Tipo: {TipoTraspaso}, Articulo: {Articulo}). Eliminando SOLO {Cantidad} líneas temporales de ESTE traspaso específico. NO se eliminan líneas de otros traspasos.",
                                traspaso.Id, traspaso.TipoTraspaso, traspaso.CodigoArticulo, todasLasTemporalesDelTraspaso.Count);
                            
                            foreach (var tempDelTraspaso in todasLasTemporalesDelTraspaso)
                            {
                                // 🔷 VALIDACIÓN FINAL: Verificar una vez más que el TraspasoId coincide
                                if (tempDelTraspaso.TraspasoId != traspaso.Id)
                                {
                                    logger.LogError("❌ ERROR: TempPaletLinea {TempId} tiene TraspasoId {TraspasoIdTemp} pero se esperaba {TraspasoIdEsperado}. NO se eliminará.",
                                        tempDelTraspaso.Id, tempDelTraspaso.TraspasoId, traspaso.Id);
                                    continue; // NO eliminar esta línea
                                }
                                
                                logger.LogInformation("🗑️ Eliminando TempPaletLinea {TempId} del palet {PaletId} (Cantidad: {Cantidad}, Articulo: {Articulo}) por ERROR_ERP del traspaso {TraspasoId}. Esta línea pertenece SOLO a este traspaso.",
                                    tempDelTraspaso.Id, tempDelTraspaso.PaletId, tempDelTraspaso.Cantidad, tempDelTraspaso.CodigoArticulo, traspaso.Id);
                                
                                dbContext.TempPaletLineas.Remove(tempDelTraspaso);
                            }
                            
                            // Guardar cambios inmediatamente para limpiar las líneas
                            await dbContext.SaveChangesAsync();
                            
                            // Registrar en LogPalet para cada palet afectado
                            var paletsAfectados = todasLasTemporalesDelTraspaso.Select(t => t.PaletId).Distinct().ToList();
                            foreach (var paletAfectadoId in paletsAfectados)
                            {
                                dbContext.LogPalet.Add(new LogPalet
                                {
                                    PaletId = paletAfectadoId,
                                    Fecha = DateTime.Now,
                                    IdUsuario = traspaso.UsuarioInicioId,
                                    Accion = "EliminarLineasPorErrorERP",
                                    Detalle = $"Líneas temporales eliminadas por ERROR_ERP del traspaso {traspaso.Id}. Traspaso falló en ERP."
                                });
                            }
                            
                            await dbContext.SaveChangesAsync();
                        }
                        
                        // Continuar con la siguiente temporal (esta ya fue eliminada si estaba en la lista)
                        continue;
                    }
							
						// 2.1) Si falta DescripcionArticulo, intentar recuperarla
						if (string.IsNullOrWhiteSpace(temp.DescripcionArticulo))
						{
							var descripcionRecuperada = await ObtenerDescripcionArticuloAsync(
								dbContext, sageDbContext, temp.PaletId, temp.CodigoArticulo, 
								temp.CodigoEmpresa, logger);
							
							if (!string.IsNullOrWhiteSpace(descripcionRecuperada))
							{
								temp.DescripcionArticulo = descripcionRecuperada;
								logger.LogInformation("✅ Recuperada DescripcionArticulo para {CodigoArticulo}: {Descripcion}", 
									temp.CodigoArticulo, temp.DescripcionArticulo);
							}
							else
							{
								logger.LogWarning("⚠️ No se pudo recuperar DescripcionArticulo para TempLinea {TempId}, Articulo={CodigoArticulo}, PaletId={PaletId}", 
									temp.Id, temp.CodigoArticulo, temp.PaletId);
							}
						}

								// Si quieres exigir �completado�, acepta lo que ponga el controller
								// 🔷 CRÍTICO: Solo consolidar líneas cuando el traspaso esté COMPLETADO
								// NO consolidar en PENDIENTE_ERP porque el traspaso aún puede fallar (ERROR_ERP)
								// Si se consolida antes de que el ERP confirme, las líneas quedarán en el palet aunque el traspaso falle
								var esCompletado = string.Equals(traspaso.CodigoEstado, "COMPLETADO", StringComparison.OrdinalIgnoreCase);

								if (!esCompletado)
								{
									// Si está en PENDIENTE_ERP, esperar a que el ERP confirme (COMPLETADO o ERROR_ERP)
									logger.LogDebug("⏳ TempLinea {TempId} esperando confirmación del ERP. Traspaso {TraspasoId} en estado {Estado}",
										temp.Id, traspaso.Id, traspaso.CodigoEstado);
									continue;
								}

						// 3) Busca la línea definitiva (misma clave)
						// Normalizar valores para comparación
						var tempCodigoAlmacen = (temp.CodigoAlmacen ?? "").Trim().ToUpper();
						var tempUbicacion = (temp.Ubicacion ?? "").Trim().ToUpper();
					var tempLote = (temp.Lote ?? "").Trim();
					
					var lineasCandidatas = await dbContext.PaletLineas
						.Where(l => l.PaletId == temp.PaletId && 
									l.CodigoArticulo == temp.CodigoArticulo)
						.ToListAsync();
					
					// Para líneas heredadas: buscar SIN incluir ubicación (puede estar en cualquier ubicación)
					// Para líneas normales: buscar CON ubicación (puede haber múltiples en diferentes ubicaciones)
					var existente = temp.EsHeredada
						? lineasCandidatas
							.Where(l => 
								(l.Lote ?? "").Trim() == tempLote &&
								l.FechaCaducidad == temp.FechaCaducidad)
							.FirstOrDefault()
						: lineasCandidatas
							.Where(l => 
								(l.Lote ?? "").Trim() == tempLote &&
								l.FechaCaducidad == temp.FechaCaducidad &&
								(l.CodigoAlmacen ?? "").Trim().ToUpper() == tempCodigoAlmacen &&
								(l.Ubicacion ?? "").Trim().ToUpper() == tempUbicacion
							)
							.FirstOrDefault();

						if (existente != null)
						{
							// 🔷 DEBUG: Log para verificar precisión antes de consolidar
							logger.LogInformation("🔍 DEBUG Consolidación: TempLinea Cantidad={TempCantidad} (F6={TempCantidadF6}), Existente Cantidad={ExistenteCantidad} (F6={ExistenteCantidadF6})", 
								temp.Cantidad, temp.Cantidad.ToString("F6"), 
								existente.Cantidad, existente.Cantidad.ToString("F6"));
							
							if (!temp.EsHeredada)
								existente.Cantidad += temp.Cantidad;  // DELTA (+/-)
							
							// Para líneas heredadas, actualizar la ubicación (mover el palet)
							if (temp.EsHeredada)
							{
								existente.CodigoAlmacen = temp.CodigoAlmacen?.Trim();
								existente.Ubicacion = (temp.Ubicacion ?? "").Trim();
								logger.LogInformation("📍 Línea heredada actualiza ubicación: PaletId={PaletId}, Articulo={Articulo}, NuevaUbicacion={Almacen}-{Ubicacion}", 
									temp.PaletId, temp.CodigoArticulo, temp.CodigoAlmacen, temp.Ubicacion);
							}
							
							// 🔷 DEBUG: Log después de sumar
							logger.LogInformation("🔍 DEBUG Consolidación: Cantidad después de sumar={CantidadFinal} (F6={CantidadFinalF6})", 
								existente.Cantidad, existente.Cantidad.ToString("F6"));

							// Usar comparación más robusta para evitar problemas de precisión decimal
							if (existente.Cantidad <= 0m || Math.Abs(existente.Cantidad) < 0.0001m)
							{
								dbContext.PaletLineas.Remove(existente);
								logger.LogInformation("🗑️ Línea eliminada por cantidad <= 0: PaletId={PaletId}, Articulo={Articulo}, CantidadFinal={Cantidad}", 
									existente.PaletId, existente.CodigoArticulo, existente.Cantidad);
							}
							else
							{
								existente.UsuarioId = temp.UsuarioId;
								existente.Observaciones = temp.Observaciones?.Trim();
								existente.TraspasoId = traspaso.Id;
								
								// Propagar/Completar DescripcionArticulo
								if (!string.IsNullOrWhiteSpace(temp.DescripcionArticulo))
								{
									// Si temp tiene descripción, usarla
									existente.DescripcionArticulo = temp.DescripcionArticulo.Trim();
								}
								else if (string.IsNullOrWhiteSpace(existente.DescripcionArticulo))
								{
									// Si ni temp ni existente tienen descripción, intentar recuperarla
									var descripcionRecuperada = await ObtenerDescripcionArticuloAsync(
										dbContext, sageDbContext, temp.PaletId, temp.CodigoArticulo, 
										temp.CodigoEmpresa, logger);
									
									if (!string.IsNullOrWhiteSpace(descripcionRecuperada))
									{
										existente.DescripcionArticulo = descripcionRecuperada;
										logger.LogInformation("✅ Completada DescripcionArticulo en línea existente para {CodigoArticulo}: {Descripcion}", 
											temp.CodigoArticulo, existente.DescripcionArticulo);
									}
								}
								
								dbContext.PaletLineas.Update(existente);
							}
						}
					else
					{
						// No existe línea en la ubicación de la temporal
						// Las líneas heredadas NO deben crear nuevas líneas definitivas
						// Solo actualizan las existentes. Si no existe, solo se marca como procesada.
						if (temp.EsHeredada)
						{
							logger.LogWarning("⚠️ Línea heredada {TempId} no encontró línea definitiva para actualizar. Solo se marca como procesada. PaletId={PaletId}, Articulo={Articulo}, Ubicacion={Almacen}-{Ubicacion}", 
								temp.Id, temp.PaletId, temp.CodigoArticulo, temp.CodigoAlmacen, temp.Ubicacion);
							// No crear línea definitiva, solo marcar como procesada más abajo
						}
						// IMPORTANTE: Solo crear nueva línea si temp.Cantidad es POSITIVO
						// Si es NEGATIVO, significa que se intenta restar de una línea que no existe → error de datos
						// Si es 0, no crear línea (no tiene sentido)
						else if (temp.Cantidad > 0m)
						{
							// Validar solo CodigoAlmacen (Ubicacion puede estar vacía = "sin ubicar")
							if (string.IsNullOrWhiteSpace(temp.CodigoAlmacen))
							{
								logger.LogWarning("⚠️ TempLinea {TempId} tiene CodigoAlmacen nulo/vacío. PaletId={PaletId}, Articulo={Articulo}. Se marca como procesada SIN consolidar.", 
									temp.Id, temp.PaletId, temp.CodigoArticulo);
							}
							else
							{
								// 🔷 DEBUG: Log para verificar precisión antes de crear nueva línea
								logger.LogInformation("🔍 DEBUG Creando nueva PaletLinea: TempLinea Cantidad={TempCantidad} (F6={TempCantidadF6})", 
									temp.Cantidad, temp.Cantidad.ToString("F6"));
								
								// Crear nueva línea SOLO si es cantidad positiva (agregar a palet)
								var nuevaLinea = new SGA_Api.Models.Palet.PaletLinea
								{
									Id = Guid.NewGuid(),
									PaletId = temp.PaletId,
									CodigoEmpresa = temp.CodigoEmpresa,
									CodigoArticulo = temp.CodigoArticulo?.Trim() ?? "",
									DescripcionArticulo = temp.DescripcionArticulo?.Trim(),
									Cantidad = temp.Cantidad,
									UnidadMedida = temp.UnidadMedida?.Trim(),
									Lote = temp.Lote?.Trim(),
									FechaCaducidad = temp.FechaCaducidad,
									CodigoAlmacen = temp.CodigoAlmacen.Trim(),
									Ubicacion = (temp.Ubicacion ?? "").Trim(),
									UsuarioId = temp.UsuarioId,
									FechaAgregado = temp.FechaAgregado,
									Observaciones = temp.Observaciones?.Trim(),
									TraspasoId = traspaso.Id
								};
								
								// 🔷 DEBUG: Log después de crear el objeto
								logger.LogInformation("🔍 DEBUG Nueva PaletLinea creada: Cantidad={Cantidad} (F6={CantidadF6})", 
									nuevaLinea.Cantidad, nuevaLinea.Cantidad.ToString("F6"));
								
								dbContext.PaletLineas.Add(nuevaLinea);
								
								logger.LogInformation("✅ Creada nueva línea definitiva: PaletId={PaletId}, Articulo={Articulo}, Cantidad={Cantidad}, Ubicacion={Almacen}-{Ubicacion}", 
									temp.PaletId, temp.CodigoArticulo, temp.Cantidad, temp.CodigoAlmacen, temp.Ubicacion);
							}
						}
						else if (temp.Cantidad < 0m)
						{
							// Línea temporal negativa pero no existe línea definitiva para restar
							// Esto puede pasar cuando se mueve stock de un palet y no hay línea en destino
							logger.LogWarning("⚠️ Línea temporal NEGATIVA {TempId} sin línea definitiva correspondiente. PaletId={PaletId}, Articulo={Articulo}, Cantidad={Cantidad}, Ubicacion={Almacen}-{Ubicacion}. Se ignora.", 
								temp.Id, temp.PaletId, temp.CodigoArticulo, temp.Cantidad, temp.CodigoAlmacen, temp.Ubicacion);
						}
						else if (temp.Cantidad == 0m)
						{
							// Línea temporal con cantidad 0 - no tiene sentido procesarla
							logger.LogInformation("ℹ️ Línea temporal con cantidad 0 ignorada: {TempId}, PaletId={PaletId}, Articulo={Articulo}", 
								temp.Id, temp.PaletId, temp.CodigoArticulo);
						}
					}

						temp.Procesada = true;
						dbContext.TempPaletLineas.Update(temp);
					}

					// 4) Solo mover l�neas por TRASPASO DE PALET (no por art�culo)
					// Mover TODAS las l�neas del palet al destino del �LTIMO traspaso completado
					var ultimoTraspasoPalet = await dbContext.Traspasos
						.Where(t => t.TipoTraspaso == "PALET" &&
									(t.CodigoEstado == "COMPLETADO" || t.CodigoEstado == "PENDIENTE_ERP") &&
									t.PaletId == paletId &&
									t.FechaFinalizacion != null &&
									t.FechaFinalizacion >= DateTime.Now.AddHours(-24)) // Extendido a 24 horas para cubrir más casos
						.OrderByDescending(t => t.FechaFinalizacion)
						.FirstOrDefaultAsync();

					if (ultimoTraspasoPalet != null)
					{
						// Verificar si las l�neas ya est�n en la ubicaci�n destino (evitar reprocesamiento)
						// 🔷 CORREGIDO: Verificar si HAY ALGUNA línea que no esté en el destino
						// En lugar de verificar solo una línea de muestra, verificar si hay líneas en diferentes almacenes
						// Esto corrige el bug donde algunas líneas quedaban sin actualizar
						var hayLineasEnDiferenteAlmacen = await dbContext.PaletLineas
							.Where(l => l.PaletId == paletId && 
										(l.CodigoAlmacen != ultimoTraspasoPalet.AlmacenDestino || 
										 l.Ubicacion != ultimoTraspasoPalet.UbicacionDestino))
							.AnyAsync();
						
						if (hayLineasEnDiferenteAlmacen)
						{
							var todasLasLineas = await dbContext.PaletLineas
								.Where(l => l.PaletId == paletId)
								.ToListAsync();

							logger.LogInformation("🔧 Corrigiendo líneas del palet {PaletId}: {Cantidad} líneas detectadas. Actualizando todas al destino {AlmacenDestino}-{UbicacionDestino}", 
								paletId, todasLasLineas.Count, ultimoTraspasoPalet.AlmacenDestino, ultimoTraspasoPalet.UbicacionDestino);

							foreach (var linea in todasLasLineas)
							{
								linea.CodigoAlmacen = ultimoTraspasoPalet.AlmacenDestino;
								linea.Ubicacion = ultimoTraspasoPalet.UbicacionDestino;
								dbContext.PaletLineas.Update(linea);
							}
						}
						else
						{
							logger.LogDebug("ℹ️ Todas las líneas del palet {PaletId} ya están en el destino {AlmacenDestino}-{UbicacionDestino}", 
								paletId, ultimoTraspasoPalet.AlmacenDestino, ultimoTraspasoPalet.UbicacionDestino);
						}

					var esDestinoPulmon = await EsUbicacionPulmonAsync(
						dbContext,
						ultimoTraspasoPalet.AlmacenDestino,
						ultimoTraspasoPalet.UbicacionDestino);

					// 🔷 CORRECCIÓN: Solo vaciar si el traspaso está COMPLETADO
					// No vaciar si está PENDIENTE_ERP porque puede fallar después y pasar a ERROR_ERP
					if (esDestinoPulmon && ultimoTraspasoPalet.CodigoEstado == "COMPLETADO")
					{
						await VaciarPaletPorDestinoPulmonAsync(dbContext, paletId, ultimoTraspasoPalet, logger);
					}
					}

							// 5) Marcar VAC�ADO SOLO cuando:
							//    - no quedan temporales pendientes de ese palet
							//    - el stock total definitivo del palet es 0 (o <= 0 por si hay redondeos)
							//    - y no hay ninguna l�nea con cantidad > 0
							var quedanTemporales = await dbContext.TempPaletLineas
								.AnyAsync(l => l.PaletId == paletId && l.Procesada == false);

							// 🔷 MEJORADO: También verificar si no quedan líneas definitivas (sin líneas en absoluto)
							var quedanDefinitivas = await dbContext.PaletLineas
								.AnyAsync(l => l.PaletId == paletId);

							// Si no quedan temporales ni definitivas, marcar como vaciado directamente
							if (!quedanTemporales && !quedanDefinitivas)
							{
							// 🔷 PROTECCIÓN: No vaciar si hay traspasos ERROR_ERP recientes (menos de 4 horas)
							// Esto permite que el servicio externo haga retry y complete el traspaso antes de vaciar
							var hace4Horas = DateTime.Now.AddHours(-4);
							var hayTraspasosErrorErpRecientes3 = await dbContext.Traspasos
								.AnyAsync(t => t.PaletId == paletId && 
											   t.CodigoEstado == "ERROR_ERP" &&
											   t.FechaFinalizacion.HasValue &&
											   t.FechaFinalizacion.Value >= hace4Horas);

								if (hayTraspasosErrorErpRecientes3)
								{
									logger.LogInformation("⏳ Palet {PaletId} tiene traspasos ERROR_ERP recientes. Esperando retry antes de vaciar (sin temporales ni definitivas).", paletId);
								}

								var paletConActividadReciente =
									paletTuvoTemporalesPendientes ||
									paletIdsConTraspasos.Contains(paletId) ||
									paletIdsParaVaciado.Contains(paletId);

								var puedeAplicarLimpiezaSinLineas = paletConActividadReciente || permitirLimpiezaDiariaSinLineas;

								if (puedeAplicarLimpiezaSinLineas && !hayTraspasosErrorErpRecientes3)
								{
									var palet = await dbContext.Palets.FindAsync(paletId);
									if (palet != null && palet.Estado != "Vaciado" && palet.Estado.ToUpper() != "VACIADO")
									{
										var fechaReferencia = palet.FechaCierre
											?? palet.FechaVaciado
											?? palet.FechaApertura;

										if (fechaReferencia < hace24Horas)
										{
											puedeAplicarLimpiezaSinLineas = false;
											logger.LogDebug("ℹ️ Palet {PaletId} sin líneas excluido de la limpieza diaria por antigüedad (última actividad {FechaReferencia}).", paletId, fechaReferencia);
										}

										if (paletConActividadReciente || puedeAplicarLimpiezaSinLineas)
										{
											if (!paletConActividadReciente)
											{
												seRealizoLimpiezaDiariaSinLineas = true;
											}
											palet.Estado = "Vaciado";
											palet.FechaVaciado = DateTime.Now;

											// intenta registrar el usuario delltimo delta negativo o del último traspaso
											var ultNeg = await dbContext.TempPaletLineas
												.Where(x => x.PaletId == paletId && x.Cantidad < 0 && x.Procesada == true)
												.OrderByDescending(x => x.FechaAgregado)
												.FirstOrDefaultAsync();

											// Si no hay temporal negativa, buscar el usuario del último traspaso completado
											if (ultNeg == null)
											{
												var ultimoTraspaso = await dbContext.Traspasos
													.Where(t => t.PaletId == paletId && t.CodigoEstado == "COMPLETADO")
													.OrderByDescending(t => t.FechaFinalizacion)
													.FirstOrDefaultAsync();
												
												if (ultimoTraspaso != null)
												{
													palet.UsuarioVaciadoId = ultimoTraspaso.UsuarioFinalizacionId ?? palet.UsuarioVaciadoId;
												}
											}
											else
											{
												palet.UsuarioVaciadoId = ultNeg.UsuarioId;
											}

											// opcional: cierra tambin
											palet.FechaCierre = palet.FechaCierre ?? DateTime.Now;
											palet.UsuarioCierreId = palet.UsuarioCierreId ?? palet.UsuarioVaciadoId;

											dbContext.Palets.Update(palet);

											dbContext.LogPalet.Add(new LogPalet
											{
												PaletId = palet.Id,
												Fecha = DateTime.Now,
												IdUsuario = palet.UsuarioVaciadoId ?? 0,
												Accion = "Vaciado",
												Detalle = "Marcado vaciado tras consolidación: sin líneas temporales ni definitivas."
											});
										}
									}
								}
								else
								{
									logger.LogDebug("ℹ️ Limpieza diaria de palets sin líneas ya ejecutada hoy. PaletId={PaletId} se omite hasta la próxima ventana diaria.", paletId);
								}
							}
							// 🔷 NUEVO: Si hay temporales PENDIENTES (procesadas o no) que suman 0 y no hay definitivas, también vaciar
							// Esto incluye líneas de conteo que no se procesan por el BackgroundService
							// IMPORTANTE: Solo vaciar si NO hay ajustes de conteo pendientes (PENDIENTE_ERP)
							else if (!quedanDefinitivas)
							{
								// Sumar solo las temporales PENDIENTES del palet (no procesadas)
								// Las procesadas ya están reflejadas o no afectan el cálculo
								var totalTemporalesPendientes = await dbContext.TempPaletLineas
									.Where(l => l.PaletId == paletId && l.Procesada == false)
									.SumAsync(l => (decimal?)l.Cantidad) ?? 0m;

								// Verificar que no haya ninguna temporal pendiente positiva
								var hayTemporalesPendientesPositivas = await dbContext.TempPaletLineas
									.AnyAsync(l => l.PaletId == paletId && l.Procesada == false && l.Cantidad > 0m);

								// 🔷 PROTECCIÓN: Verificar que NO haya ajustes de conteo o inventario PENDIENTE_ERP asociados
								// Si hay ajustes pendientes, no vaciar porque el ajuste puede cambiar las cantidades
								var conteoIdsPendientes = await dbContext.TempPaletLineas
									.Where(l => l.PaletId == paletId && l.Procesada == false && l.ConteoId != null && l.ConteoId != Guid.Empty)
									.Select(l => l.ConteoId)
									.Distinct()
									.ToListAsync();

								var inventarioIdsPendientes = await dbContext.TempPaletLineas
									.Where(l => l.PaletId == paletId && l.Procesada == false && l.InventarioId != null && l.InventarioId != Guid.Empty)
									.Select(l => l.InventarioId)
									.Distinct()
									.ToListAsync();

								var hayAjustesPendientes = false;
								if (conteoIdsPendientes.Any())
								{
									hayAjustesPendientes = await dbContext.InventarioAjustes
										.AnyAsync(a => conteoIdsPendientes.Contains(a.IdConteo.Value) && 
													  a.Estado == "PENDIENTE_ERP" && 
													  a.ProcesadoPalet == false);
								}

								if (!hayAjustesPendientes && inventarioIdsPendientes.Any())
								{
									hayAjustesPendientes = await dbContext.InventarioAjustes
										.AnyAsync(a => inventarioIdsPendientes.Contains(a.IdInventario.Value) && 
													  a.Estado == "PENDIENTE_ERP" && 
													  a.ProcesadoPalet == false);
								}

							// 🔷 PROTECCIÓN: No vaciar si hay traspasos ERROR_ERP recientes (menos de 4 horas)
							// Esto permite que el servicio externo haga retry y complete el traspaso antes de vaciar
							var hace4Horas = DateTime.Now.AddHours(-4);
							var hayTraspasosErrorErpRecientes = await dbContext.Traspasos
								.AnyAsync(t => t.PaletId == paletId && 
											   t.CodigoEstado == "ERROR_ERP" &&
											   t.FechaFinalizacion.HasValue &&
											   t.FechaFinalizacion.Value >= hace4Horas);

								if (hayTraspasosErrorErpRecientes)
								{
									logger.LogInformation("⏳ Palet {PaletId} tiene traspasos ERROR_ERP recientes. Esperando retry antes de vaciar.", paletId);
								}
								
								// Si el total de temporales pendientes es 0 o negativo, no hay positivas, 
								// NO hay ajustes pendientes, y NO hay ERROR_ERP recientes, entonces vaciar el palet
								if (totalTemporalesPendientes <= 0m && !hayTemporalesPendientesPositivas && !hayAjustesPendientes && !hayTraspasosErrorErpRecientes)
								{
									var palet = await dbContext.Palets.FindAsync(paletId);
									if (palet != null && !string.Equals(palet.Estado, "Vaciado", StringComparison.OrdinalIgnoreCase))
									{
										palet.Estado = "Vaciado";
										palet.FechaVaciado = DateTime.Now;

										// intenta registrar el usuario del último delta negativo
										var ultNeg = await dbContext.TempPaletLineas
											.Where(x => x.PaletId == paletId && x.Cantidad < 0)
											.OrderByDescending(x => x.FechaAgregado)
											.FirstOrDefaultAsync();

										// Si no hay temporal negativa, buscar el usuario del último traspaso completado
										if (ultNeg == null)
										{
											var ultimoTraspaso = await dbContext.Traspasos
												.Where(t => t.PaletId == paletId && t.CodigoEstado == "COMPLETADO")
												.OrderByDescending(t => t.FechaFinalizacion)
												.FirstOrDefaultAsync();
											
											if (ultimoTraspaso != null)
											{
												palet.UsuarioVaciadoId = ultimoTraspaso.UsuarioFinalizacionId ?? palet.UsuarioVaciadoId;
											}
										}
										else
										{
											palet.UsuarioVaciadoId = ultNeg.UsuarioId;
										}

										// opcional: cierra también
										palet.FechaCierre = palet.FechaCierre ?? DateTime.Now;
										palet.UsuarioCierreId = palet.UsuarioCierreId ?? palet.UsuarioVaciadoId;

										dbContext.Palets.Update(palet);

										dbContext.LogPalet.Add(new LogPalet
										{
											PaletId = palet.Id,
											Fecha = DateTime.Now,
											IdUsuario = palet.UsuarioVaciadoId ?? 0,
											Accion = "Vaciado",
											Detalle = $"Marcado vaciado tras consolidación: total de temporales pendientes={totalTemporalesPendientes}, sin líneas definitivas y sin ajustes de conteo/inventario pendientes."
										});
									}
								}
							}
							else if (!quedanTemporales)
							{
								// suma total de cantidades del palet
								var totalPalet = await dbContext.PaletLineas
									.Where(l => l.PaletId == paletId)
									.SumAsync(l => (decimal?)l.Cantidad) ?? 0m;

								// tambi�n comprobamos expl�citamente que NO exista ninguna l�nea positiva
								var hayPositivas = await dbContext.PaletLineas
									.AnyAsync(l => l.PaletId == paletId && l.Cantidad > 0m);

							// 🔷 PROTECCIÓN: No vaciar si hay traspasos ERROR_ERP recientes (menos de 4 horas)
							// Esto permite que el servicio externo haga retry y complete el traspaso antes de vaciar
							var hace4Horas = DateTime.Now.AddHours(-4);
							var hayTraspasosErrorErpRecientes2 = await dbContext.Traspasos
								.AnyAsync(t => t.PaletId == paletId && 
											   t.CodigoEstado == "ERROR_ERP" &&
											   t.FechaFinalizacion.HasValue &&
											   t.FechaFinalizacion.Value >= hace4Horas);

								if (hayTraspasosErrorErpRecientes2)
								{
									logger.LogInformation("⏳ Palet {PaletId} tiene traspasos ERROR_ERP recientes. Esperando retry antes de vaciar (sin temporales, total={Total}).", paletId, totalPalet);
								}

								if (totalPalet <= 0m && !hayPositivas && !hayTraspasosErrorErpRecientes2)
								{
									var palet = await dbContext.Palets.FindAsync(paletId);
									if (palet != null && !string.Equals(palet.Estado, "Vaciado", StringComparison.OrdinalIgnoreCase))
									{
										palet.Estado = "Vaciado";
										palet.FechaVaciado = DateTime.Now;

										// intenta registrar el usuario del �ltimo delta negativo
										var ultNeg = await dbContext.TempPaletLineas
											.Where(x => x.PaletId == paletId && x.Cantidad < 0 && x.Procesada == true)
											.OrderByDescending(x => x.FechaAgregado)
											.FirstOrDefaultAsync();

										palet.UsuarioVaciadoId = ultNeg?.UsuarioId ?? palet.UsuarioVaciadoId;

										// opcional: cierra tambi�n
										palet.FechaCierre = palet.FechaCierre ?? DateTime.Now;
										palet.UsuarioCierreId = palet.UsuarioCierreId ?? ultNeg?.UsuarioId;

										dbContext.Palets.Update(palet);

									dbContext.LogPalet.Add(new LogPalet
									{
										PaletId = palet.Id,
										Fecha = DateTime.Now,
										IdUsuario = palet.UsuarioVaciadoId ?? 0,
										Accion = "Vaciado",
										Detalle = "Marcado vaciado tras consolidación: total=0 y sin temporales pendientes."
									});
								}
							}
						}

					// 6) CONSOLIDACIÓN INTELIGENTE - Solo si hay líneas duplicadas
					// 🔷 CORREGIDO: Incluir CodigoAlmacen y Ubicacion en el GROUP BY
					// Solo unificar líneas que estén en la misma ubicación y almacén
					// Esto evita mezclar líneas de diferentes ubicaciones antes de que el palet se mueva a su destino final
					// 
					// FORMA ANTIGUA (comentada):
					// var tieneDuplicadas = await dbContext.PaletLineas
					// 	.Where(l => l.PaletId == paletId)
					// 	.GroupBy(l => new { 
					// 		l.CodigoArticulo, 
					// 		Lote = l.Lote ?? "", 
					// 		FechaCaducidad = l.FechaCaducidad 
					// 	})
					// 	.AnyAsync(g => g.Count() > 1);
					
					// 🔷 NUEVA FORMA: Incluye CodigoAlmacen y Ubicacion para evitar unificar líneas de diferentes ubicaciones
					var tieneDuplicadas = await dbContext.PaletLineas
						.Where(l => l.PaletId == paletId)
						.GroupBy(l => new { 
							l.CodigoArticulo, 
							Lote = l.Lote ?? "", 
							FechaCaducidad = l.FechaCaducidad,
							l.CodigoAlmacen,  // 🔷 AÑADIDO: Solo unificar si están en el mismo almacén
							Ubicacion = l.Ubicacion ?? ""  // 🔷 AÑADIDO: Solo unificar si están en la misma ubicación
						})
						.AnyAsync(g => g.Count() > 1);
					
					if (tieneDuplicadas)
					{
						logger.LogInformation("🔍 Palet {PaletId} tiene líneas duplicadas, ejecutando consolidación inteligente", paletId);
						
						// Obtener líneas del palet para consolidar
						var lineasDelPalet = await dbContext.PaletLineas
							.Where(l => l.PaletId == paletId)
							.ToListAsync();
						
						// 🔷 CORREGIDO: Agrupar incluyendo CodigoAlmacen y Ubicacion
						// FORMA ANTIGUA (comentada):
						// var lineasDuplicadas = lineasDelPalet
						// 	.GroupBy(l => new { 
						// 		l.CodigoArticulo, 
						// 		Lote = l.Lote ?? "", 
						// 		FechaCaducidad = l.FechaCaducidad 
						// 	})
						// 	.Where(g => g.Count() > 1)
						// 	.ToList();
						
						// 🔷 NUEVA FORMA: Solo unificar líneas que estén en la misma ubicación
						var lineasDuplicadas = lineasDelPalet
							.GroupBy(l => new { 
								l.CodigoArticulo, 
								Lote = l.Lote ?? "", 
								FechaCaducidad = l.FechaCaducidad,
								l.CodigoAlmacen,  // 🔷 AÑADIDO
								Ubicacion = l.Ubicacion ?? ""  // 🔷 AÑADIDO
							})
							.Where(g => g.Count() > 1)
							.ToList();

					foreach (var grupo in lineasDuplicadas)
					{
						var lineas = grupo.ToList();
						var lineaPrincipal = lineas.First();
						
						// Sumar todas las cantidades
						var cantidadTotal = lineas.Sum(l => l.Cantidad);
						
						// Actualizar la línea principal con la cantidad total
						lineaPrincipal.Cantidad = cantidadTotal;
						
						// 🔷 CORREGIDO: Ya no es necesario actualizar CodigoAlmacen y Ubicacion
						// porque todas las líneas del grupo tienen los mismos valores (están agrupadas por estos campos)
						// Usar la información más reciente (última línea por fecha)
						var ultimaLinea = lineas.OrderByDescending(l => l.FechaAgregado).First();
						lineaPrincipal.UsuarioId = ultimaLinea.UsuarioId;
						lineaPrincipal.TraspasoId = ultimaLinea.TraspasoId;
						// 🔷 Ya no necesario: lineaPrincipal.CodigoAlmacen = ultimaLinea.CodigoAlmacen;
						// 🔷 Ya no necesario: lineaPrincipal.Ubicacion = ultimaLinea.Ubicacion;
						// Todas las líneas del grupo ya tienen el mismo CodigoAlmacen y Ubicacion
						
						// Mantener la descripción más completa
						if (string.IsNullOrWhiteSpace(lineaPrincipal.DescripcionArticulo) && 
							!string.IsNullOrWhiteSpace(ultimaLinea.DescripcionArticulo))
						{
							lineaPrincipal.DescripcionArticulo = ultimaLinea.DescripcionArticulo;
						}
						
						// Eliminar las líneas duplicadas
						foreach (var duplicada in lineas.Skip(1))
						{
							dbContext.PaletLineas.Remove(duplicada);
						}
						
						// Actualizar la línea principal
						dbContext.PaletLineas.Update(lineaPrincipal);
						
						// 🔷 ACTUALIZADO: Log incluye almacén y ubicación para mejor trazabilidad
						logger.LogInformation("🔄 Líneas consolidadas: {CantidadLineas} líneas del artículo {CodigoArticulo} (lote: {Lote}, almacen: {Almacen}, ubicacion: {Ubicacion}) → cantidad total: {CantidadTotal}", 
							lineas.Count, grupo.Key.CodigoArticulo, grupo.Key.Lote, grupo.Key.CodigoAlmacen, grupo.Key.Ubicacion, cantidadTotal);
					}
					}
					else
					{
						logger.LogDebug("ℹ️ Palet {PaletId} no tiene líneas duplicadas, omitiendo consolidación", paletId);
					}

						// Guardar todos los cambios de este palet en una sola operación
						await dbContext.SaveChangesAsync();
						
						// Si todo salió bien, confirmar la transacción
						await transaction.CommitAsync();
					}
					catch (Exception ex)
					{
						// Si algo falló, revertir todos los cambios
						await transaction.RollbackAsync();
						logger.LogError(ex, "❌ Error al consolidar palet {PaletId}. Se revirtieron todos los cambios.", paletId);
						// Continuar con el siguiente palet
					}

						//	Buscar todos los traspasos COMPLETADOS para ese palet
							//	var traspasosCompletados = await dbContext.Traspasos
							//		.Where(t => t.PaletId == paletId && t.CodigoEstado == "COMPLETADO")
							//		.OrderBy(t => t.FechaFinalizacion)
							//		.ToListAsync();

							//	foreach (var traspaso in traspasosCompletados)
							//	{
							//		Solo procesar l�neas temporales con TraspasoId igual al traspaso completado y no procesadas
							//	   var tempLineas = await dbContext.TempPaletLineas
							//		   .Where(l => l.PaletId == paletId && l.TraspasoId == traspaso.Id && l.Procesada == false)
							//		   .ToListAsync();

							//		if (tempLineas.Any())
							//		{
							//			foreach (var tempLinea in tempLineas)
							//			{
							//				Buscar si ya existe una l�nea definitiva para este art�culo/ lote / fecha(comparando nulls correctamente)
							//				var existente = await dbContext.PaletLineas.FirstOrDefaultAsync(l =>
							//					l.PaletId == tempLinea.PaletId &&
							//					l.CodigoArticulo == tempLinea.CodigoArticulo &&
							//					l.Lote == tempLinea.Lote &&
							//					((l.FechaCaducidad == null && tempLinea.FechaCaducidad == null) || (l.FechaCaducidad != null && tempLinea.FechaCaducidad != null && l.FechaCaducidad == tempLinea.FechaCaducidad))
							//				);

							//				if (existente != null)
							//				{
							//					// Recalcular la cantidad definitiva como la suma de todas las l�neas temporales procesadas NO heredadas (m�s la actual si tampoco es heredada)
							//					var sumaCantidad = await dbContext.TempPaletLineas
							//						.Where(l =>
							//							l.PaletId == tempLinea.PaletId &&
							//							l.CodigoArticulo == tempLinea.CodigoArticulo &&
							//							l.Lote == tempLinea.Lote &&
							//							((l.FechaCaducidad == null && tempLinea.FechaCaducidad == null) ||
							//							 (l.FechaCaducidad != null && tempLinea.FechaCaducidad != null && l.FechaCaducidad == tempLinea.FechaCaducidad)) &&
							//							l.Procesada == true &&
							//							l.EsHeredada == false)
							//						.SumAsync(l => l.Cantidad);
							//					if (!tempLinea.EsHeredada)
							//						sumaCantidad += tempLinea.Cantidad;
							//					existente.Cantidad = sumaCantidad;
							//					existente.CodigoAlmacen = traspaso.AlmacenDestino;
							//					existente.Ubicacion = traspaso.UbicacionDestino;
							//					existente.UsuarioId = tempLinea.UsuarioId;
							//					existente.Observaciones = tempLinea.Observaciones;
							//					existente.TraspasoId = traspaso.Id;
							//					dbContext.PaletLineas.Update(existente);
							//				}
							//				else
							//				{
							//					var nuevaLinea = new SGA_Api.Models.Palet.PaletLinea
							//					{
							//						Id = Guid.NewGuid(),
							//						PaletId = tempLinea.PaletId,
							//						CodigoEmpresa = tempLinea.CodigoEmpresa,
							//						CodigoArticulo = tempLinea.CodigoArticulo,
							//						DescripcionArticulo = tempLinea.DescripcionArticulo,
							//						Cantidad = tempLinea.Cantidad,
							//						UnidadMedida = tempLinea.UnidadMedida,
							//						Lote = tempLinea.Lote,
							//						FechaCaducidad = tempLinea.FechaCaducidad,
							//						CodigoAlmacen = traspaso.AlmacenDestino,
							//						Ubicacion = traspaso.UbicacionDestino,
							//						UsuarioId = tempLinea.UsuarioId,
							//						FechaAgregado = tempLinea.FechaAgregado,
							//						Observaciones = tempLinea.Observaciones,
							//						TraspasoId = traspaso.Id
							//					};
							//					dbContext.PaletLineas.Add(nuevaLinea);
							//				}
							//				if (existente != null)
							//				{
							//					Solo aplicamos el DELTA de la temporal actual(si usas EsHeredada, resp�talo)
							//					if (!tempLinea.EsHeredada)
							//						existente.Cantidad += tempLinea.Cantidad;

							//					La ubicaci�n final viene de la temporal(ya trae el destino del traspaso)
							//					existente.CodigoAlmacen = tempLinea.CodigoAlmacen;
							//					existente.Ubicacion = tempLinea.Ubicacion;
							//					existente.UsuarioId = tempLinea.UsuarioId;
							//					existente.Observaciones = tempLinea.Observaciones;
							//					existente.TraspasoId = traspaso.Id;

							//					dbContext.PaletLineas.Update(existente);
							//				}
							//				else
							//				{
							//					dbContext.PaletLineas.Add(new SGA_Api.Models.Palet.PaletLinea
							//					{
							//						Id = Guid.NewGuid(),
							//						PaletId = tempLinea.PaletId,
							//						CodigoEmpresa = tempLinea.CodigoEmpresa,
							//						CodigoArticulo = tempLinea.CodigoArticulo,
							//						DescripcionArticulo = tempLinea.DescripcionArticulo,
							//						Cantidad = tempLinea.Cantidad,           // << DELTA
							//						UnidadMedida = tempLinea.UnidadMedida,
							//						Lote = tempLinea.Lote,
							//						FechaCaducidad = tempLinea.FechaCaducidad,
							//						CodigoAlmacen = tempLinea.CodigoAlmacen,
							//						Ubicacion = tempLinea.Ubicacion,
							//						UsuarioId = tempLinea.UsuarioId,
							//						FechaAgregado = tempLinea.FechaAgregado,
							//						Observaciones = tempLinea.Observaciones,
							//						TraspasoId = traspaso.Id
							//					});
							//				}
							//				tempLinea.Procesada = true;
							//				dbContext.TempPaletLineas.Update(tempLinea);
							//			}
							//		}
							//	}

							//ACTUALIZACI�N: Mover todas las l�neas definitivas del palet a la �ltima ubicaci�n destino
							//	if (traspasosCompletados.Any())
							//	{
							//		var ultimoTraspaso = traspasosCompletados.Last();
							//		var lineasDefinitivas = await dbContext.PaletLineas
							//			.Where(l => l.PaletId == paletId)
							//			.ToListAsync();
							//		foreach (var linea in lineasDefinitivas)
							//		{
							//			linea.CodigoAlmacen = ultimoTraspaso.AlmacenDestino;
							//			linea.Ubicacion = ultimoTraspaso.UbicacionDestino;
							//			dbContext.PaletLineas.Update(linea);
							//		}
							//	}
							//	var traspasosMoverPalet = traspasosCompletados
							//		.Where(t => t.TipoTraspaso == "PALET")
							//		.ToList();

							//	foreach (var t in traspasosMoverPalet)
							//	{
							//		var lineasDelTraspaso = await dbContext.PaletLineas
							//			.Where(l => l.PaletId == paletId && l.TraspasoId == t.Id)
							//			.ToListAsync();

							//		foreach (var linea in lineasDelTraspaso)
							//		{
							//			linea.CodigoAlmacen = t.AlmacenDestino;
							//			linea.Ubicacion = t.UbicacionDestino;   // si aqu� quieres permitir �sin ubicar�, se queda as�
							//			dbContext.PaletLineas.Update(linea);
							//		}
							//	}
							//}
							//await dbContext.SaveChangesAsync();

							//// Unificaci�n de l�neas definitivas duplicadas en PaletLineas (en memoria y normalizando)
							//var lineasPalet = await dbContext.PaletLineas.ToListAsync();

							//var grupos = lineasPalet
							//    .GroupBy(l => new {
							//	    l.PaletId,
							//	    l.CodigoArticulo,
							//	    Lote = l.Lote?.Trim() ?? "",
							//		FechaCad = l.FechaCaducidad,
							//		CodigoAlmacen = l.CodigoAlmacen?.Trim().ToUpper() ?? "",
							//	    Ubicacion = l.Ubicacion?.Trim().ToUpper() ?? ""
							//    })
							//    .Where(g => g.Count() > 1)
							//    .ToList();

							//foreach (var grupo in grupos)
							//{
							//	var lineas = grupo.ToList();
							//	var principal = lineas.First();
							//	principal.Cantidad = lineas.Sum(l => l.Cantidad);
							//	foreach (var duplicada in lineas.Skip(1))
							//	{
							//		dbContext.PaletLineas.Remove(duplicada);
							//	}
							//	dbContext.PaletLineas.Update(principal);
							//}
							//await dbContext.SaveChangesAsync(); // <-- Guardar los cambios de la unificaci�n
						}
					} // Cierra el bloque using (scope)

					if (permitirLimpiezaDiariaSinLineas && seRealizoLimpiezaDiariaSinLineas)
					{
						RegistrarLimpiezaDiariaSinLineas();
					}
				}
				catch (ObjectDisposedException)
				{
					// El contenedor de DI se está liberando, salir del bucle
					break;
				}
				catch (InvalidOperationException)
				{
					// El contenedor de DI ya no está disponible, salir del bucle
					break;
				}
				finally
				{
					_enEjecucion = false;
				}
				
				// Verificar si se canceló antes de esperar
				if (stoppingToken.IsCancellationRequested)
					break;
					
				try
				{
					await Task.Delay(_intervalo, stoppingToken);
				}
				catch (OperationCanceledException)
				{
					// El token fue cancelado, salir del bucle
					break;
				}
			}
		}

		/// <summary>
		/// Detecta cambios de estado en traspasos y env�a notificaciones popup correspondientes
		/// </summary>
		private async Task DetectarYNotificarCambiosEstadoAsync(AuroraSgaDbContext dbContext, SageDbContext sageDbContext, INotificacionesUnificadasService notificacionesUnificadas, ILogger<TraspasoFinalizacionBackgroundService> logger)
		{
			try
			{
                // Obtener traspasos que pueden cambiar de estado
                // - PENDIENTE_ERP: Puede cambiar a COMPLETADO o ERROR_ERP
                // - PENDIENTE: Puede cambiar a PENDIENTE_ERP, COMPLETADO o ERROR_ERP
                // - COMPLETADO: Estado final exitoso (se incluye para detectar la transici�n)
                // - ERROR_ERP: Estado final con error (se incluye para detectar la transici�n)
                // - Excluir solo CANCELADO (estado final que no interesa notificar)
                /*var traspasosActivos = await dbContext.Traspasos
					.Where(t => t.CodigoEstado != "CANCELADO" && 
							   t.UsuarioInicioId > 0 &&
							   (t.TipoTraspaso == "ARTICULO" || t.TipoTraspaso == "PALET")) // Solo traspasos v�lidos
					.Select(t => new { t.Id, t.CodigoEstado, t.TipoTraspaso, t.UsuarioInicioId, t.CodigoPalet, t.CodigoArticulo })
					.ToListAsync();*/

                var hace5Minutos = DateTime.Now.AddMinutes(-5);

                var traspasosActivos = await dbContext.Traspasos
                    .Where(t => t.CodigoEstado != "CANCELADO" &&
                               t.UsuarioInicioId > 0 &&
                               (t.TipoTraspaso == "ARTICULO" || t.TipoTraspaso == "PALET") &&
                               // Incluir traspasos en estados transitorios (siempre pueden cambiar)
                               ((t.CodigoEstado == "PENDIENTE" || t.CodigoEstado == "PENDIENTE_ERP") ||
                                // O traspasos COMPLETADO/ERROR_ERP muy recientes (últimos 5 minutos) para detectar transiciones
                                ((t.CodigoEstado == "COMPLETADO" || t.CodigoEstado == "ERROR_ERP") &&
                                 t.FechaFinalizacion.HasValue && t.FechaFinalizacion.Value >= hace5Minutos)))
                    .Select(t => new { t.Id, t.CodigoEstado, t.TipoTraspaso, t.UsuarioInicioId, t.CodigoPalet, t.CodigoArticulo })
                    .Take(2000) // Límite máximo para evitar sobrecarga
                    .ToListAsync();

                // Solo log cuando hay muchos traspasos activos (para evitar spam)
                if (traspasosActivos.Count > 10)
                {
                    logger.LogDebug("?? BackgroundService: Revisando {Cantidad} traspasos activos", traspasosActivos.Count);
                }

				foreach (var traspaso in traspasosActivos)
				{
					try
				{
					var estadoAnterior = _estadosAnterioresTraspasos.GetValueOrDefault(traspaso.Id, "");
					var estadoActual = traspaso.CodigoEstado ?? "";

						// Log solo traspasos nuevos con estados importantes
						if (string.IsNullOrEmpty(estadoAnterior) && (estadoActual == "PENDIENTE_ERP" || estadoActual == "ERROR_ERP"))
						{
							logger.LogInformation("?? Nuevo traspaso detectado: {TraspasoId} - Estado inicial: {EstadoActual} (Usuario: {UsuarioId}, Tipo: {TipoTraspaso})", 
								traspaso.Id, estadoActual, traspaso.UsuarioInicioId, traspaso.TipoTraspaso);
						}

					// Solo log cuando hay cambio de estado
					if (estadoAnterior != estadoActual && !string.IsNullOrEmpty(estadoAnterior))
					{
						logger.LogDebug("?? Traspaso {TraspasoId}: {EstadoAnterior} -> {EstadoActual} (Usuario: {UsuarioId})", 
							traspaso.Id, estadoAnterior, estadoActual, traspaso.UsuarioInicioId);
					}

						// Solo notificar si el estado realmente cambi� y es un estado que nos interesa notificar
						if (estadoAnterior != estadoActual && !string.IsNullOrEmpty(estadoAnterior) && 
							(estadoActual == "COMPLETADO" || estadoActual == "PENDIENTE_ERP" || estadoActual == "ERROR_ERP"))
					{
						logger.LogInformation("?? CAMBIO DETECTADO en traspaso {TraspasoId}: {EstadoAnterior} -> {EstadoActual}", 
							traspaso.Id, estadoAnterior, estadoActual);
							
							// Verificar que tenemos los datos necesarios antes de enviar
							var codigoIdentificador = traspaso.TipoTraspaso == "PALET" ? traspaso.CodigoPalet : traspaso.CodigoArticulo;
							
							if (!string.IsNullOrEmpty(codigoIdentificador) && traspaso.UsuarioInicioId > 0)
							{
								await EnviarNotificacionCambioEstadoAsync(traspaso, estadoAnterior, estadoActual, notificacionesUnificadas, dbContext, sageDbContext, logger);
							}
							else
							{
								logger.LogWarning("?? No se puede enviar notificación para traspaso {TraspasoId}: CodigoIdentificador={CodigoIdentificador}, UsuarioId={UsuarioId}", 
									traspaso.Id, codigoIdentificador, traspaso.UsuarioInicioId);
							}
					}

					// Actualizar el estado anterior
					_estadosAnterioresTraspasos[traspaso.Id] = estadoActual;
					}
					catch (Exception ex)
					{
						logger.LogError(ex, "Error al procesar traspaso individual {TraspasoId}", traspaso.Id);
						// Continuar con el siguiente traspaso aunque falle uno
					}
				}

				// Limpiar estados de traspasos que ya no existen (cancelados, eliminados, etc.)
				var traspasosExistentes = traspasosActivos.Select(t => t.Id).ToHashSet();
				var clavesAEliminar = _estadosAnterioresTraspasos.Keys.Where(id => !traspasosExistentes.Contains(id)).ToList();
				foreach (var clave in clavesAEliminar)
				{
					_estadosAnterioresTraspasos.Remove(clave);
				}
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error al detectar cambios de estado en traspasos");
			}
		}

		/// <summary>
		/// Env�a notificaci�n popup espec�fica seg�n el tipo de traspaso y estado (sistema h�brido: BD + SignalR)
		/// </summary>
		private async Task EnviarNotificacionCambioEstadoAsync(object traspaso, string estadoAnterior, string estadoActual, INotificacionesUnificadasService notificacionesUnificadas, AuroraSgaDbContext dbContext, SageDbContext sageDbContext, ILogger<TraspasoFinalizacionBackgroundService> logger)
		{
			try
			{
				// Convertir el objeto an�nimo a propiedades espec�ficas
				var traspasoId = (Guid)traspaso.GetType().GetProperty("Id")!.GetValue(traspaso)!;
				var usuarioId = (int)traspaso.GetType().GetProperty("UsuarioInicioId")!.GetValue(traspaso)!;
				var tipoTraspaso = (string?)traspaso.GetType().GetProperty("TipoTraspaso")!.GetValue(traspaso);
				var codigoPalet = (string?)traspaso.GetType().GetProperty("CodigoPalet")!.GetValue(traspaso);
				var codigoArticulo = (string?)traspaso.GetType().GetProperty("CodigoArticulo")!.GetValue(traspaso);

				var codigoIdentificador = tipoTraspaso == "PALET" ? codigoPalet : codigoArticulo;


				if (usuarioId <= 0 || string.IsNullOrEmpty(codigoIdentificador))
				{
					logger.LogWarning("?? No se puede enviar notificación: UsuarioId={UsuarioId}, CodigoIdentificador={CodigoIdentificador}", usuarioId, codigoIdentificador);
					return;
				}

				// Obtener informaci�n adicional del traspaso desde la base de datos
				var informacionAdicional = await ObtenerInformacionAdicionalTraspasoAsync(traspasoId, tipoTraspaso, logger);
				

				string titulo, mensaje, tipoNotificacion;

				// Determinar el contenido de la notificaci�n seg�n el estado y tipo de traspaso
				switch (estadoActual.ToUpper())
				{
					case "COMPLETADO":
						titulo = "Traspaso Completado";
						if (tipoTraspaso == "PALET")
							mensaje = $"Traspaso de palet {codigoIdentificador} completado exitosamente";
						else
							mensaje = $"Traspaso de artículo {codigoIdentificador} completado exitosamente";
						tipoNotificacion = "success";
						break;

					case "PENDIENTE_ERP":
						titulo = "Traspaso en Proceso";
						if (tipoTraspaso == "PALET")
							mensaje = $"Traspaso de palet {codigoIdentificador} procesándose";
						else
							mensaje = $"Traspaso de artículo {codigoIdentificador} procesándose";
						tipoNotificacion = "info";
						break;

					case "ERROR_ERP":
						titulo = "Error en Traspaso";
						if (tipoTraspaso == "PALET")
							mensaje = $"Traspaso de palet {codigoIdentificador} falló";
						else
							mensaje = $"Traspaso de artículo {codigoIdentificador} falló";
						tipoNotificacion = "error";
						break;

					default:
						// Para otros estados, no enviar notificaci�n espec�fica
						return;
				}

				// PASO 1: Guardar notificaci�n en base de datos para persistencia
				var mensajeCompleto = $"{mensaje}\n{informacionAdicional}".Trim();
				try
				{
					await notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
						usuarioId,
						"TRASPASO",
						titulo,
						mensajeCompleto,
						traspasoId,
						estadoAnterior,
						estadoActual,
						tipoNotificacion);
					
					logger.LogInformation("Notificación enviada para traspaso {TraspasoId}: {EstadoAnterior} -> {EstadoActual}", 
						traspasoId, estadoAnterior, estadoActual);
				}
				catch (Exception ex)
				{
					logger.LogError(ex, "Error al crear y enviar notificación unificada para traspaso {TraspasoId}", traspasoId);
				}

			// PASO 2: Si es ERROR_ERP, notificar también a supervisores con acceso al almacén
			if (estadoActual == "ERROR_ERP")
			{
				var almacenesTraspaso = new List<string>();
				
				try
				{
					// Obtener el traspaso completo para tener AlmacenOrigen y AlmacenDestino
					var traspasoCompleto = await dbContext.Traspasos
						.Where(t => t.Id == traspasoId)
						.Select(t => new { t.AlmacenOrigen, t.AlmacenDestino })
						.FirstOrDefaultAsync();

					if (traspasoCompleto != null && (!string.IsNullOrEmpty(traspasoCompleto.AlmacenOrigen) || !string.IsNullOrEmpty(traspasoCompleto.AlmacenDestino)))
					{
						// Recopilar almacenes únicos del traspaso
						if (!string.IsNullOrEmpty(traspasoCompleto.AlmacenOrigen))
							almacenesTraspaso.Add(traspasoCompleto.AlmacenOrigen.Trim());
						if (!string.IsNullOrEmpty(traspasoCompleto.AlmacenDestino) && 
							traspasoCompleto.AlmacenDestino.Trim() != traspasoCompleto.AlmacenOrigen?.Trim())
							almacenesTraspaso.Add(traspasoCompleto.AlmacenDestino.Trim());

						if (almacenesTraspaso.Any())
						{
							// Buscar supervisores (IdRol = 2) que tengan acceso a los almacenes del traspaso
							// Primero obtener IDs de operarios con acceso a los almacenes desde Sage
							var operariosConAcceso = await sageDbContext.OperariosAlmacenes
								.Where(oa => almacenesTraspaso.Contains(oa.CodigoAlmacen ?? ""))
								.Select(oa => oa.Operario)
								.Distinct()
								.ToListAsync();

							if (operariosConAcceso.Any())
							{
								// Luego buscar usuarios que sean supervisores (IdRol = 2) y tengan acceso
								// Excluir al usuario que inició el traspaso si es supervisor (para evitar duplicados)
								var supervisoresIds = await dbContext.Usuarios
									.Where(u => u.IdRol == 2 && 
												operariosConAcceso.Contains(u.IdUsuario) &&
												u.IdUsuario != usuarioId)
									.Select(u => u.IdUsuario)
									.ToListAsync();

								if (supervisoresIds.Any())
								{
									logger.LogInformation("Notificando a {Cantidad} supervisores con acceso a almacenes {Almacenes} del traspaso {TraspasoId}",
										supervisoresIds.Count, string.Join(", ", almacenesTraspaso), traspasoId);

									// Notificar a cada supervisor
									foreach (var supervisorId in supervisoresIds)
									{
										try
										{
											await notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
												supervisorId,
												"TRASPASO",
												$"Error en Traspaso - {string.Join(", ", almacenesTraspaso)}",
												$"Supervisión requerida: {mensajeCompleto}",
												traspasoId,
												estadoAnterior,
												estadoActual,
												"error");

											logger.LogInformation("Notificación enviada a supervisor {SupervisorId} para traspaso {TraspasoId}", supervisorId, traspasoId);
										}
										catch (Exception ex)
										{
											logger.LogError(ex, "Error al notificar supervisor {SupervisorId} para traspaso {TraspasoId}", supervisorId, traspasoId);
										}
									}
								}
								else
								{
									logger.LogDebug("No se encontraron supervisores con acceso a almacenes {Almacenes} para traspaso {TraspasoId}",
										string.Join(", ", almacenesTraspaso), traspasoId);
								}
							}
							else
							{
								logger.LogDebug("No se encontraron operarios con acceso a almacenes {Almacenes} para traspaso {TraspasoId}",
									string.Join(", ", almacenesTraspaso), traspasoId);
							}
						}
					}
				}
				catch (Exception ex)
				{
					logger.LogError(ex, "Error al notificar supervisores para traspaso {TraspasoId}", traspasoId);
					// No fallar la notificación principal si falla la de supervisores
				}

				// Notificar a TODOS los ADMIN (sin filtro de almacén), excluyendo al usuario que inició el traspaso si es admin
				// Este bloque se ejecuta siempre cuando es ERROR_ERP, independientemente de si hay almacenes o no
				try
				{
					var adminIds = await dbContext.Usuarios
						.Where(u => u.IdRol == 3 && u.IdUsuario != usuarioId)
						.Select(u => u.IdUsuario)
						.ToListAsync();

					if (adminIds.Any())
					{
						logger.LogInformation("Notificando a {Cantidad} administradores para traspaso {TraspasoId}",
							adminIds.Count, traspasoId);

						var tituloAdmin = almacenesTraspaso.Any() 
							? $"Error en Traspaso - {string.Join(", ", almacenesTraspaso)}"
							: "Error en Traspaso";

						foreach (var adminId in adminIds)
						{
							try
							{
								await notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
									adminId,
									"TRASPASO",
									tituloAdmin,
									$"Supervisión requerida: {mensajeCompleto}",
									traspasoId,
									estadoAnterior,
									estadoActual,
									"error");

								logger.LogInformation("Notificación enviada a administrador {AdminId} para traspaso {TraspasoId}", adminId, traspasoId);
							}
							catch (Exception ex)
							{
								logger.LogError(ex, "Error al notificar administrador {AdminId} para traspaso {TraspasoId}", adminId, traspasoId);
							}
						}
					}
				}
				catch (Exception ex)
				{
					logger.LogError(ex, "Error al notificar administradores para traspaso {TraspasoId}", traspasoId);
					// No fallar si falla la notificación a administradores
				}
			}

				// PASO 3: Agregar a cola de notificaciones Teams si es ERROR_ERP
				if (estadoActual == "ERROR_ERP")
				{
					try
					{
						// Obtener el traspaso completo para insertar en cola
						var traspasoCompleto = await dbContext.Traspasos
							.FirstOrDefaultAsync(t => t.Id == traspasoId);

						if (traspasoCompleto != null)
						{
							var registroCola = new NotificacionTeamsCola
							{
								Id = Guid.NewGuid(),
								TraspasoId = traspasoId,
								Estado = "Pendiente",
								Intentos = 0,
								FechaCreacion = DateTime.Now,
								MensajeError = traspasoCompleto.EstadoErp ?? traspasoCompleto.Comentario ?? "Error en traspaso ERP"
							};

							dbContext.NotificacionesTeamsCola.Add(registroCola);
							await dbContext.SaveChangesAsync();

							logger.LogInformation("Registro agregado a cola de notificaciones Teams para traspaso {TraspasoId}", traspasoId);
						}
					}
					catch (Exception ex)
					{
						logger.LogError(ex, "Error al agregar registro a cola de notificaciones Teams para traspaso {TraspasoId}", traspasoId);
						// No fallar el flujo principal si falla la inserción en cola
					}
				}

				//// PASO 2: Enviar notificaci�n por SignalR (mantener funcionalidad existente)
				//var maxIntentos = 3;
				//var intento = 0;
				//var enviadoSignalR = false;

				//while (intento < maxIntentos && !enviadoSignalR)
				//{
				//	try
				//	{
				//		intento++;

				//		await notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(usuarioId, "TRASPASO", titulo, mensajeCompleto, traspasoId, estadoAnterior, estadoActual, tipoNotificacion);
				//		enviadoSignalR = true;
						
				//							logger.LogInformation("Notificación enviada para traspaso {TraspasoId}: {EstadoAnterior} -> {EstadoActual}", 
				//								traspasoId, estadoAnterior, estadoActual);
				//	}
				//	catch (Exception ex)
				//	{
				//		logger.LogWarning(ex, "?? Error al enviar notificación SignalR (intento {Intento}/{MaxIntentos}) para traspaso {TraspasoId}", 
				//			intento, maxIntentos, traspasoId);
						
				//		if (intento < maxIntentos)
				//		{
				//			await Task.Delay(1000 * intento); // Espera progresiva: 1s, 2s, 3s
				//		}
				//	}
				//}

				//if (!enviadoSignalR)
				//{
				//	logger.LogError("? No se pudo enviar notificación SignalR después de {MaxIntentos} intentos para traspaso {TraspasoId}", 
				//		maxIntentos, traspasoId);
				//}
			}
			catch (Exception ex)
			{
				var traspasoId = traspaso.GetType().GetProperty("Id")?.GetValue(traspaso)?.ToString() ?? "desconocido";
				logger.LogError(ex, "? Error crítico al enviar notificación para traspaso {TraspasoId}", traspasoId);
			}
		}

		/// <summary>
		/// Obtiene informaci�n adicional del traspaso para enriquecer la notificaci�n
		/// </summary>
		private async Task<string> ObtenerInformacionAdicionalTraspasoAsync(Guid traspasoId, string? tipoTraspaso, ILogger<TraspasoFinalizacionBackgroundService> logger)
		{
			try
			{
				using var scope = _serviceProvider.CreateScope();
				var dbContext = scope.ServiceProvider.GetRequiredService<AuroraSgaDbContext>();

				// Obtener el traspaso completo
				var traspaso = await dbContext.Traspasos.FindAsync(traspasoId);
				if (traspaso == null) return "";

				var informacion = new List<string>();

				// Formatear ubicaci�n origen
				string ubicacionOrigen = "";
				if (!string.IsNullOrEmpty(traspaso.AlmacenOrigen) && !string.IsNullOrEmpty(traspaso.UbicacionOrigen) && traspaso.UbicacionOrigen.Trim() != "")
				{
					ubicacionOrigen = $"{traspaso.AlmacenOrigen}-{traspaso.UbicacionOrigen}";
				}
				else if (!string.IsNullOrEmpty(traspaso.UbicacionOrigen) && traspaso.UbicacionOrigen.Trim() != "")
				{
					ubicacionOrigen = traspaso.UbicacionOrigen;
				}
				else if (!string.IsNullOrEmpty(traspaso.AlmacenOrigen))
				{
					ubicacionOrigen = $"{traspaso.AlmacenOrigen}-SinUbicar";
				}

				// Formatear ubicaci�n destino
				string ubicacionDestino = "";
				if (!string.IsNullOrEmpty(traspaso.AlmacenDestino) && !string.IsNullOrEmpty(traspaso.UbicacionDestino) && traspaso.UbicacionDestino.Trim() != "")
				{
					ubicacionDestino = $"{traspaso.AlmacenDestino}-{traspaso.UbicacionDestino}";
				}
				else if (!string.IsNullOrEmpty(traspaso.UbicacionDestino) && traspaso.UbicacionDestino.Trim() != "")
				{
					ubicacionDestino = traspaso.UbicacionDestino;
				}
				else if (!string.IsNullOrEmpty(traspaso.AlmacenDestino))
				{
					ubicacionDestino = $"{traspaso.AlmacenDestino}-SinUbicar";
				}

				// Agregar ubicaci�n formateada
				if (!string.IsNullOrEmpty(ubicacionOrigen) || !string.IsNullOrEmpty(ubicacionDestino))
				{
					informacion.Add($"Ubicación: {ubicacionOrigen} - {ubicacionDestino}");
				}

				// Para traspasos de art�culo, obtener cantidad y descripci�n
				if (tipoTraspaso == "ARTICULO" && !string.IsNullOrEmpty(traspaso.CodigoArticulo))
				{

					var cantidadEncontrada = false;

					// 1. PRIMERO: Buscar en la tabla Traspasos directamente (para art�culos sueltos)
					if (traspaso.Cantidad != null && traspaso.Cantidad != 0)
					{
						informacion.Add($"Cantidad: {Math.Abs(traspaso.Cantidad.Value):F4}");
						cantidadEncontrada = true;
					}

					// 2. SEGUNDO: Buscar en TempPaletLineas (para art�culos en palets)
					if (!cantidadEncontrada)
					{
						var tempLinea = await dbContext.TempPaletLineas
							.Where(tl => tl.TraspasoId == traspasoId && tl.CodigoArticulo == traspaso.CodigoArticulo)
							.FirstOrDefaultAsync();

						if (tempLinea != null)
						{
							
							if (tempLinea.Cantidad != 0)
							{
								informacion.Add($"Cantidad: {Math.Abs(tempLinea.Cantidad):F4}");
								cantidadEncontrada = true;
							}
						}
					}

					// 3. TERCERO: Buscar en PaletLineas (para l�neas ya consolidadas)
					if (!cantidadEncontrada)
					{
						
						var paletLinea = await dbContext.PaletLineas
							.Where(pl => pl.TraspasoId == traspasoId && pl.CodigoArticulo == traspaso.CodigoArticulo)
							.FirstOrDefaultAsync();

						if (paletLinea != null)
						{
							
							if (paletLinea.Cantidad != 0)
							{
								informacion.Add($"Cantidad: {Math.Abs(paletLinea.Cantidad):F4}");
								cantidadEncontrada = true;
							}
						}
					}

					// 4. Si no encontramos cantidad en ning�n lado, log de debug
					if (!cantidadEncontrada)
					{
						logger.LogDebug("No se encontró información de cantidad para TraspasoId={TraspasoId}, CodigoArticulo={CodigoArticulo}", 
							traspasoId, traspaso.CodigoArticulo);
					}
				}

				var resultado = string.Join("\n", informacion);
				return resultado;
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "Error al obtener información adicional del traspaso {TraspasoId}", traspasoId);
				return "";
			}
		}

		/// <summary>
		/// Guarda la notificaci�n en la base de datos para persistencia
		/// </summary>
		private async Task<bool> GuardarNotificacionEnBDAsync(
			AuroraSgaDbContext dbContext,
			int usuarioId,
			string titulo,
			string mensaje,
			string tipoNotificacion,
			Guid procesoId,
			string? estadoAnterior,
			string estadoActual,
			ILogger<TraspasoFinalizacionBackgroundService> logger)
		{
			try
			{
				using var transaction = await dbContext.Database.BeginTransactionAsync();
				
				try
				{
					// Crear la notificaci�n principal
					var notificacion = new Notificacion
					{
						IdNotificacion = Guid.NewGuid(),
						CodigoEmpresa = 1,
						TipoNotificacion = "TRASPASO",
						ProcesoId = procesoId,
						Titulo = titulo,
						Mensaje = mensaje,
						EstadoAnterior = estadoAnterior,
						EstadoActual = estadoActual,
						FechaCreacion = DateTime.Now,
						EsActiva = true,
						EsGrupal = false,
						GrupoDestino = null,
						Comentario = null
					};

					dbContext.Notificaciones.Add(notificacion);

					// Crear el destinatario
					var destinatario = new NotificacionDestinatario
					{
						IdDestinatario = Guid.NewGuid(),
						IdNotificacion = notificacion.IdNotificacion,
						UsuarioId = usuarioId,
						FechaCreacion = DateTime.Now,
						EsActiva = true
					};

					dbContext.NotificacionesDestinatarios.Add(destinatario);

					// Guardar cambios
					await dbContext.SaveChangesAsync();
					await transaction.CommitAsync();


					return true;
				}
				catch (Exception ex)
				{
					await transaction.RollbackAsync();
					logger.LogError(ex, "? Error al guardar notificación en BD para traspaso {ProcesoId}, usuario {UsuarioId}", 
						procesoId, usuarioId);
					return false;
				}
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "? Error crítico al guardar notificación en BD para traspaso {ProcesoId}, usuario {UsuarioId}", 
					procesoId, usuarioId);
			return false;
		}
	}
	
	private async Task<bool> EsUbicacionPulmonAsync(
		AuroraSgaDbContext dbContext,
		string? almacenDestino,
		string? ubicacionDestino)
	{
		if (string.IsNullOrWhiteSpace(almacenDestino))
		{
			return false;
		}

		var almacenNormalizado = almacenDestino.Trim().ToUpper();
		var ubicacionNormalizada = (ubicacionDestino ?? string.Empty).Trim().ToUpper();

		var descripcionTipo = await dbContext.Ubicaciones_Configuracion
			.Where(u =>
				(u.CodigoAlmacen ?? string.Empty).Trim().ToUpper() == almacenNormalizado &&
				(u.Ubicacion ?? string.Empty).Trim().ToUpper() == ubicacionNormalizada)
			.Join(
				dbContext.TipoUbicaciones,
				u => u.TipoUbicacionId,
				t => t.TipoUbicacionId,
				(u, t) => t.Descripcion)
			.FirstOrDefaultAsync();

		return string.Equals(descripcionTipo?.Trim(), "PULMON", StringComparison.OrdinalIgnoreCase);
	}

	private async Task VaciarPaletPorDestinoPulmonAsync(
		AuroraSgaDbContext dbContext,
		Guid paletId,
		Traspaso ultimoTraspasoPalet,
		ILogger<TraspasoFinalizacionBackgroundService> logger)
	{
		var palet = await dbContext.Palets.FindAsync(paletId);
		if (palet == null)
		{
			logger.LogWarning("⚠️ No se encontró el palet {PaletId} para vaciar tras destino Pulmón.", paletId);
			return;
		}

		if (string.Equals(palet.Estado, "Vaciado", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		var lineasDefinitivas = await dbContext.PaletLineas
			.Where(l => l.PaletId == paletId)
			.ToListAsync();

		if (lineasDefinitivas.Any())
		{
			dbContext.PaletLineas.RemoveRange(lineasDefinitivas);
			logger.LogInformation("🗑️ Eliminadas {Cantidad} líneas definitivas de palet {PaletId} por llegada a Pulmón.", lineasDefinitivas.Count, paletId);
		}

		var temporalesPendientes = await dbContext.TempPaletLineas
			.Where(l => l.PaletId == paletId && l.Procesada == false)
			.ToListAsync();

		foreach (var temporal in temporalesPendientes)
		{
			temporal.Procesada = true;
			dbContext.TempPaletLineas.Update(temporal);
		}

		if (temporalesPendientes.Any())
		{
			logger.LogInformation("✅ Marcadas {Cantidad} temporales como procesadas para palet {PaletId} tras vaciado en Pulmón.", temporalesPendientes.Count, paletId);
		}

		var usuarioVaciadoId = ultimoTraspasoPalet.UsuarioFinalizacionId ?? ultimoTraspasoPalet.UsuarioInicioId;

		palet.Estado = "Vaciado";
		palet.IsVaciado = true;
		palet.FechaVaciado = DateTime.Now;
		palet.UsuarioVaciadoId = usuarioVaciadoId > 0 ? usuarioVaciadoId : palet.UsuarioVaciadoId;

		if (!palet.FechaCierre.HasValue)
		{
			palet.FechaCierre = DateTime.Now;
		}

		if (palet.UsuarioVaciadoId.HasValue)
		{
			palet.UsuarioCierreId = palet.UsuarioCierreId ?? palet.UsuarioVaciadoId;
		}

		dbContext.Palets.Update(palet);

		dbContext.LogPalet.Add(new LogPalet
		{
			PaletId = palet.Id,
			Fecha = DateTime.Now,
			IdUsuario = palet.UsuarioVaciadoId ?? 0,
			Accion = "Vaciado",
			Detalle = $"Vaciado automático por llegada a ubicación Pulmón {ultimoTraspasoPalet.AlmacenDestino}-{ultimoTraspasoPalet.UbicacionDestino}."
		});
	}

	/// <summary>
	/// Intenta obtener la descripción de un artículo desde múltiples fuentes
	/// </summary>
	private async Task<string?> ObtenerDescripcionArticuloAsync(
		AuroraSgaDbContext dbContext,
		SageDbContext sageDbContext,
		Guid paletId,
		string codigoArticulo,
		short codigoEmpresa,
		ILogger<TraspasoFinalizacionBackgroundService> logger)
	{
		try
		{
			// 1. Buscar en PaletLineas del mismo palet
			var descripcion = await dbContext.PaletLineas
				.Where(l => l.PaletId == paletId && 
							l.CodigoArticulo == codigoArticulo && 
							!string.IsNullOrWhiteSpace(l.DescripcionArticulo))
				.Select(l => l.DescripcionArticulo)
				.FirstOrDefaultAsync();
			
			if (!string.IsNullOrWhiteSpace(descripcion))
				return descripcion.Trim();
			
			// 2. Buscar en TempPaletLineas del mismo palet
			descripcion = await dbContext.TempPaletLineas
				.Where(l => l.PaletId == paletId && 
							l.CodigoArticulo == codigoArticulo && 
							!string.IsNullOrWhiteSpace(l.DescripcionArticulo))
				.Select(l => l.DescripcionArticulo)
				.FirstOrDefaultAsync();
			
			if (!string.IsNullOrWhiteSpace(descripcion))
				return descripcion.Trim();
			
			// 3. ÚLTIMO RECURSO: Buscar en la tabla maestra de Articulos
			descripcion = await sageDbContext.Articulos
				.Where(a => a.CodigoEmpresa == codigoEmpresa && 
							a.CodigoArticulo == codigoArticulo &&
							!string.IsNullOrWhiteSpace(a.DescripcionArticulo))
				.Select(a => a.DescripcionArticulo)
				.FirstOrDefaultAsync();
			
			if (!string.IsNullOrWhiteSpace(descripcion))
			{
				logger.LogInformation("✅ Descripción recuperada desde tabla Articulos para {CodigoArticulo}: {Descripcion}", 
					codigoArticulo, descripcion);
				return descripcion.Trim();
			}
			
			return null;
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "⚠️ Error al intentar recuperar descripción para artículo {CodigoArticulo}", codigoArticulo);
			return null;
		}
	}

	private bool DebeEjecutarLimpiezaDiariaSinLineas()
	{
		return _ultimaLimpiezaDiariaSinLineas.Date < DateTime.Now.Date;
	}

	private void RegistrarLimpiezaDiariaSinLineas()
	{
		_ultimaLimpiezaDiariaSinLineas = DateTime.Now;
	}

	/// <summary>
	/// Detecta ajustes de inventario con ERROR_ERP y notifica al usuario que creó el inventario
	/// </summary>
	private async Task DetectarYNotificarErroresInventariosAsync(
		AuroraSgaDbContext dbContext,
		SageDbContext sageDbContext,
		INotificacionesUnificadasService notificacionesUnificadas, 
		ILogger<TraspasoFinalizacionBackgroundService> logger)
	{
		try
		{
			logger.LogDebug("🔍 Iniciando detección de errores en ajustes de inventario...");
			
			// Obtener solo ajustes con ERROR_ERP que tienen IdInventario y NO han sido notificados
			var ajustesConError = await dbContext.InventarioAjustes
				.Where(a => a.IdInventario != null && 
						   a.IdInventario != Guid.Empty &&
						   a.Estado == "ERROR_ERP" &&
						   !a.ErrorNotificado)
				.Select(a => new 
				{ 
					a.IdAjuste, 
					a.IdInventario, 
					a.CodigoArticulo,
					a.CodigoUbicacion,
					a.EstadoErp
				})
				.ToListAsync();

			logger.LogInformation("📊 Ajustes con ERROR_ERP encontrados: {Cantidad}", ajustesConError.Count);

			if (!ajustesConError.Any())
			{
				logger.LogDebug("✅ No hay ajustes con ERROR_ERP pendientes de notificar");
				return;
			}

			// Agrupar por IdInventario para notificar una vez por inventario
			var erroresPorInventario = ajustesConError
				.GroupBy(a => a.IdInventario!.Value)
				.ToList();

			foreach (var grupoInventario in erroresPorInventario)
			{
				try
				{
					var inventarioId = grupoInventario.Key;
					var ajustesError = grupoInventario.ToList();

					// Obtener información del inventario
					var inventario = await dbContext.InventarioCabecera
						.Where(i => i.IdInventario == inventarioId)
						.Select(i => new { i.UsuarioCreacionId, i.CodigoInventario, i.CodigoAlmacen, i.CodigoEmpresa })
						.FirstOrDefaultAsync();
					
					// Obtener todos los almacenes del inventario (puede ser multialmacén)
					var almacenesInventario = await dbContext.InventarioAlmacenes
						.Where(ia => ia.IdInventario == inventarioId)
						.Select(ia => ia.CodigoAlmacen)
						.ToListAsync();
					
					// Si no hay almacenes en InventarioAlmacenes, usar el de la cabecera
					if (!almacenesInventario.Any() && !string.IsNullOrEmpty(inventario?.CodigoAlmacen))
					{
						almacenesInventario.Add(inventario.CodigoAlmacen);
					}

					if (inventario == null || inventario.UsuarioCreacionId <= 0)
					{
						logger.LogWarning("⚠️ No se puede notificar inventario {InventarioId}: Inventario no encontrado o sin usuario", inventarioId);
						// Marcar como notificados para no volver a intentar
						await MarcarAjustesComoNotificadosAsync(dbContext, ajustesError.Select(a => a.IdAjuste).ToList(), logger);
						continue;
					}

					// Obtener total de ajustes del inventario para contexto
					var totalAjustes = await dbContext.InventarioAjustes
						.Where(a => a.IdInventario == inventarioId)
						.CountAsync();

					var completados = await dbContext.InventarioAjustes
						.Where(a => a.IdInventario == inventarioId && a.Estado == "COMPLETADO")
						.CountAsync();

					logger.LogInformation("❌ Error detectado en inventario {InventarioId}: {Errores} error(es) de {Total} ajustes",
						inventarioId, ajustesError.Count, totalAjustes);

					// Preparar detalles de errores (máximo 3 para no saturar)
					var erroresDetalle = ajustesError.Take(3).Select(a => 
						$"Artículo {a.CodigoArticulo} en {a.CodigoUbicacion}: {a.EstadoErp ?? "Error desconocido"}").ToList();

					var mensaje = $"Inventario {inventario.CodigoInventario} tiene errores";
					var informacionAdicional = $"Se encontraron {ajustesError.Count} error(es) de {totalAjustes} ajustes. " +
						$"Completados: {completados}/{totalAjustes}. " +
						(erroresDetalle.Any() ? $"Errores: {string.Join("; ", erroresDetalle)}" : "");

					var mensajeCompleto = $"{mensaje}\n{informacionAdicional}".Trim();

					// Limitar el mensaje a 500 caracteres (límite de BD)
					if (mensajeCompleto.Length > 500)
					{
						mensajeCompleto = mensajeCompleto.Substring(0, 497) + "...";
					}

					logger.LogInformation("📤 Intentando crear notificación para inventario {InventarioId}, usuario {UsuarioId}, mensaje length: {Length}",
						inventarioId, inventario.UsuarioCreacionId, mensajeCompleto.Length);

					// PASO 1: Notificar al creador (siempre)
					var notificacionCreador = false;
					try
					{
						var notificacion = await notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
							inventario.UsuarioCreacionId,
							"INVENTARIO",
							"Error en Inventario",
							mensajeCompleto,
							inventarioId,
							null,
							"ERROR_ERP",
							"error");

						if (notificacion != null)
						{
							notificacionCreador = true;
							logger.LogInformation("✅ Notificación creada para el creador del inventario {InventarioId}", inventarioId);
						}
					}
					catch (Exception ex)
					{
						logger.LogError(ex, "Error al notificar al creador del inventario {InventarioId}", inventarioId);
					}

					// PASO 2: Notificar a supervisores con acceso a los almacenes del inventario
					if (almacenesInventario.Any())
					{
						try
						{
							// Obtener IDs de operarios con acceso a los almacenes desde OperariosAlmacenes
							var operariosConAcceso = new List<int>();
							foreach (var codigoAlmacen in almacenesInventario.Distinct())
							{
								var operarios = await sageDbContext.OperariosAlmacenes
									.Where(oa => oa.CodigoAlmacen == codigoAlmacen && 
											     oa.CodigoEmpresa == inventario.CodigoEmpresa)
									.Select(oa => oa.Operario)
									.Distinct()
									.ToListAsync();
								
								operariosConAcceso.AddRange(operarios);
							}

							operariosConAcceso = operariosConAcceso.Distinct().ToList();

							if (operariosConAcceso.Any())
							{
								// Filtrar solo supervisores (IdRol = 2) y excluir al creador directamente en la consulta
								var supervisoresIds = await dbContext.Usuarios
									.Where(u => u.IdRol == 2 && 
											   operariosConAcceso.Contains(u.IdUsuario) &&
											   u.IdUsuario != inventario.UsuarioCreacionId)
									.Select(u => u.IdUsuario)
									.ToListAsync();

								if (supervisoresIds.Any())
								{
									logger.LogInformation("Notificando a {Cantidad} supervisores con acceso a almacenes del inventario {InventarioId}", 
										supervisoresIds.Count, inventarioId);

									foreach (var supervisorId in supervisoresIds)
									{
										try
										{
											await notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
												supervisorId,
												"INVENTARIO",
												"Error en Inventario",
												mensajeCompleto,
												inventarioId,
												null,
												"ERROR_ERP",
												"error");
										}
										catch (Exception ex)
										{
											logger.LogError(ex, "Error al notificar supervisor {SupervisorId} para inventario {InventarioId}", 
												supervisorId, inventarioId);
										}
									}
								}
							}
						}
						catch (Exception ex)
						{
							logger.LogError(ex, "Error al notificar supervisores para inventario {InventarioId}", inventarioId);
						}
					}

					// PASO 3: Notificar a todos los administradores (IdRol == 3)
					try
					{
						// Excluir al creador directamente en la consulta (igual que en traspasos ERROR_ERP)
						var administradoresIds = await dbContext.Usuarios
							.Where(u => u.IdRol == 3 && u.IdUsuario != inventario.UsuarioCreacionId)
							.Select(u => u.IdUsuario)
							.ToListAsync();

						if (administradoresIds.Any())
						{
							logger.LogInformation("Notificando a {Cantidad} administradores sobre error en inventario {InventarioId}", 
								administradoresIds.Count, inventarioId);

							foreach (var adminId in administradoresIds)
							{
								try
								{
									await notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
										adminId,
										"INVENTARIO",
										"Error en Inventario",
										mensajeCompleto,
										inventarioId,
										null,
										"ERROR_ERP",
										"error");
								}
								catch (Exception ex)
								{
									logger.LogError(ex, "Error al notificar administrador {AdminId} para inventario {InventarioId}", 
										adminId, inventarioId);
								}
							}
						}
					}
					catch (Exception ex)
					{
						logger.LogError(ex, "Error al notificar administradores para inventario {InventarioId}", inventarioId);
					}

					// SOLO marcar como notificados si la notificación al creador se guardó correctamente en BD
					if (notificacionCreador)
					{
						try
						{
							// Ahora sí, marcar como notificados usando SQL directo para evitar problemas de tracking/concurrencia
							var ajusteIds = ajustesError.Select(a => a.IdAjuste).ToList();
							var idsString = string.Join(",", ajusteIds.Select(id => $"'{id}'"));
							var filasActualizadas = await dbContext.Database.ExecuteSqlRawAsync(
								$"UPDATE InventarioAjustes SET ErrorNotificado = 1 WHERE IdAjuste IN ({idsString}) AND ErrorNotificado = 0");

							logger.LogInformation("✅ Marcados {Cantidad} ajustes como notificados para inventario {InventarioId}", 
								filasActualizadas, inventarioId);

							logger.LogInformation("✅ Notificación de error enviada para inventario {InventarioId}",
								inventarioId);
						}
						catch (Exception ex)
						{
							logger.LogError(ex, "Error al marcar ajustes como notificados para inventario {InventarioId}", inventarioId);
						}
					}
					else
					{
						logger.LogWarning("⚠️ Notificación de inventario {InventarioId} al creador retornó NULL - NO se guardó en BD, NO se marca como notificado. Se reintentará en el siguiente ciclo.", inventarioId);
					}
				}
				catch (Exception ex)
				{
					logger.LogError(ex, "Error al procesar inventario individual {InventarioId}", grupoInventario.Key);
				}
			}
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error al detectar errores en ajustes de inventario");
		}
	}

	/// <summary>
	/// Marca los ajustes como notificados para evitar notificaciones duplicadas
	/// </summary>
	private async Task MarcarAjustesComoNotificadosAsync(
		AuroraSgaDbContext dbContext, 
		List<Guid> ajusteIds, 
		ILogger<TraspasoFinalizacionBackgroundService> logger)
	{
		try
		{
			var ajustes = await dbContext.InventarioAjustes
				.Where(a => ajusteIds.Contains(a.IdAjuste))
				.ToListAsync();

			foreach (var ajuste in ajustes)
			{
				ajuste.ErrorNotificado = true;
			}

			dbContext.InventarioAjustes.UpdateRange(ajustes);
			await dbContext.SaveChangesAsync();
			
			logger.LogDebug("✅ Marcados {Cantidad} ajustes como notificados", ajustes.Count);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error al marcar ajustes como notificados");
		}
	}
}
}
