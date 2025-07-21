using SGA_Api.Data;
using Microsoft.EntityFrameworkCore;

namespace SGA_Api.Services
{
	public class TraspasoFinalizacionBackgroundService : BackgroundService
	{
		private readonly IServiceProvider _serviceProvider;
		private readonly TimeSpan _intervalo = TimeSpan.FromMinutes(1); // Ahora se ejecuta cada 1 minuto

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
						// Buscar el último traspaso para ese palet
						var ultimoTraspaso = await dbContext.Traspasos
							.Where(t => t.PaletId == paletId)
							.OrderByDescending(t => t.FechaFinalizacion)
							.FirstOrDefaultAsync();

						// Solo mover si el último traspaso está COMPLETADO
						if (ultimoTraspaso != null && ultimoTraspaso.CodigoEstado == "COMPLETADO")
						{
							var tempLineas = await dbContext.TempPaletLineas
								.Where(l => l.PaletId == paletId)
								.ToListAsync();

							if (tempLineas.Any())
							{
								foreach (var tempLinea in tempLineas)
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
										CodigoAlmacen = ultimoTraspaso.AlmacenDestino,
										Ubicacion = ultimoTraspaso.UbicacionDestino,
										UsuarioId = tempLinea.UsuarioId,
										FechaAgregado = tempLinea.FechaAgregado,
										Observaciones = tempLinea.Observaciones
									};
									dbContext.PaletLineas.Add(nuevaLinea);
								}
								dbContext.TempPaletLineas.RemoveRange(tempLineas);
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