using SGA_Api.Data;
using Microsoft.EntityFrameworkCore;

namespace SGA_Api.Services
{
	public class TraspasoFinalizacionBackgroundService : BackgroundService
	{
		private readonly IServiceProvider _serviceProvider;
		private readonly TimeSpan _intervalo = TimeSpan.FromSeconds(20); // Ahora se ejecuta cada 20 segundos

		public TraspasoFinalizacionBackgroundService(IServiceProvider serviceProvider)
		{
			_serviceProvider = serviceProvider;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				using (var scope = _serviceProvider.CreateScope())
				{
					var dbContext = scope.ServiceProvider.GetRequiredService<AuroraSgaDbContext>();

					// Obtener todos los paletId con líneas temporales
					var paletIdsConTempLineas = await dbContext.TempPaletLineas
						.Select(l => l.PaletId)
						.Distinct()
						.ToListAsync();

					foreach (var paletId in paletIdsConTempLineas)
					{
						// Buscar todos los traspasos COMPLETADOS para ese palet
						var traspasosCompletados = await dbContext.Traspasos
							.Where(t => t.PaletId == paletId && t.CodigoEstado == "COMPLETADO")
							.OrderBy(t => t.FechaFinalizacion)
							.ToListAsync();

						foreach (var traspaso in traspasosCompletados)
						{
							// Solo procesar líneas temporales con TraspasoId igual al traspaso completado y no procesadas
							var tempLineas = await dbContext.TempPaletLineas
								.Where(l => l.PaletId == paletId && l.TraspasoId == traspaso.Id && l.Procesada == false)
								.ToListAsync();

							if (tempLineas.Any())
							{
								foreach (var tempLinea in tempLineas)
								{
									// Buscar si ya existe una línea definitiva para este artículo/lote/fecha (comparando nulls correctamente)
									var existente = await dbContext.PaletLineas.FirstOrDefaultAsync(l =>
										l.PaletId == tempLinea.PaletId &&
										l.CodigoArticulo == tempLinea.CodigoArticulo &&
										l.Lote == tempLinea.Lote &&
										((l.FechaCaducidad == null && tempLinea.FechaCaducidad == null) || (l.FechaCaducidad != null && tempLinea.FechaCaducidad != null && l.FechaCaducidad == tempLinea.FechaCaducidad))
									);

									if (existente != null)
									{
										// Recalcular la cantidad definitiva como la suma de todas las líneas temporales procesadas NO heredadas (más la actual si tampoco es heredada)
										var sumaCantidad = await dbContext.TempPaletLineas
											.Where(l =>
												l.PaletId == tempLinea.PaletId &&
												l.CodigoArticulo == tempLinea.CodigoArticulo &&
												l.Lote == tempLinea.Lote &&
												((l.FechaCaducidad == null && tempLinea.FechaCaducidad == null) ||
												 (l.FechaCaducidad != null && tempLinea.FechaCaducidad != null && l.FechaCaducidad == tempLinea.FechaCaducidad)) &&
												l.Procesada == true &&
												l.EsHeredada == false)
											.SumAsync(l => l.Cantidad);
										if (!tempLinea.EsHeredada)
											sumaCantidad += tempLinea.Cantidad;
										existente.Cantidad = sumaCantidad;
										existente.CodigoAlmacen = traspaso.AlmacenDestino;
										existente.Ubicacion = traspaso.UbicacionDestino;
										existente.UsuarioId = tempLinea.UsuarioId;
										existente.Observaciones = tempLinea.Observaciones;
										existente.TraspasoId = traspaso.Id;
										dbContext.PaletLineas.Update(existente);
									}
									else
									{
										var nuevaLinea = new SGA_Api.Models.Palet.PaletLinea
										{
											Id = Guid.NewGuid(),
											PaletId = tempLinea.PaletId,
											CodigoEmpresa = tempLinea.CodigoEmpresa,
											CodigoArticulo = tempLinea.CodigoArticulo,
											DescripcionArticulo = tempLinea.DescripcionArticulo,
											Cantidad = tempLinea.Cantidad,
											UnidadMedida = tempLinea.UnidadMedida,
											Lote = tempLinea.Lote,
											FechaCaducidad = tempLinea.FechaCaducidad,
											CodigoAlmacen = traspaso.AlmacenDestino,
											Ubicacion = traspaso.UbicacionDestino,
											UsuarioId = tempLinea.UsuarioId,
											FechaAgregado = tempLinea.FechaAgregado,
											Observaciones = tempLinea.Observaciones,
											TraspasoId = traspaso.Id
										};
										dbContext.PaletLineas.Add(nuevaLinea);
									}
									tempLinea.Procesada = true;
									dbContext.TempPaletLineas.Update(tempLinea);
								}
							}
						}
					}
					await dbContext.SaveChangesAsync();
				}

				await Task.Delay(_intervalo, stoppingToken);
			}
		}
	}
}