using SGA_Api.Data;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Models.Palet;

namespace SGA_Api.Services
{
	public class TraspasoFinalizacionBackgroundService : BackgroundService
	{
		private readonly IServiceProvider _serviceProvider;
		private readonly TimeSpan _intervalo = TimeSpan.FromSeconds(5); // Ahora se ejecuta cada 5 segundos
		private bool _enEjecucion = false;

		public TraspasoFinalizacionBackgroundService(IServiceProvider serviceProvider)
		{
			_serviceProvider = serviceProvider;
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
							// 1) Todas las temporales pendientes de este palet (en orden)
							var tempsPendientes = await dbContext.TempPaletLineas
								.Where(l => l.PaletId == paletId && l.Procesada == false)
								.OrderBy(l => l.FechaAgregado)
								.ToListAsync();

							foreach (var temp in tempsPendientes)
							{
								// 2) Busca el traspaso de la temporal
								var traspaso = await dbContext.Traspasos.FindAsync(temp.TraspasoId);
								if (traspaso == null) continue;

								// Si quieres exigir “completado”, acepta lo que ponga el controller
								var esCompletado =
									string.Equals(traspaso.CodigoEstado, "COMPLETADO", StringComparison.OrdinalIgnoreCase) ||
									string.Equals(traspaso.CodigoEstado, "PENDIENTE_ERP", StringComparison.OrdinalIgnoreCase); // opcional

								if (!esCompletado) continue;

								// 3) Busca la línea definitiva (misma clave)
								var existente = await dbContext.PaletLineas.FirstOrDefaultAsync(l =>
									l.PaletId == temp.PaletId &&
									l.CodigoArticulo == temp.CodigoArticulo &&
									l.Lote == temp.Lote &&
									l.FechaCaducidad == temp.FechaCaducidad &&
									(l.CodigoAlmacen ?? "") == (temp.CodigoAlmacen ?? "") &&
									(l.Ubicacion ?? "") == (temp.Ubicacion ?? "")
								);

								if (existente != null)
								{
									if (!temp.EsHeredada)
										existente.Cantidad += temp.Cantidad;  // ← DELTA (+/-)

									if (existente.Cantidad <= 0m)  // CAMBIO: <= en lugar de ==
									{
										dbContext.PaletLineas.Remove(existente);
									}
									else
									{
										existente.UsuarioId = temp.UsuarioId;
										existente.Observaciones = temp.Observaciones;
										existente.TraspasoId = traspaso.Id;
										existente.DescripcionArticulo = temp.DescripcionArticulo; // Propaga DescripcionArticulo
										
										// Debug: Log para verificar la propagación de descripción
										Console.WriteLine($"DEBUG: Propagando descripción '{temp.DescripcionArticulo}' para artículo {temp.CodigoArticulo}");
										
										dbContext.PaletLineas.Update(existente);
									}
								}
								else
								{
									if (temp.Cantidad != 0m) // evita crear líneas 0
									{
										dbContext.PaletLineas.Add(new SGA_Api.Models.Palet.PaletLinea
										{
											Id = Guid.NewGuid(),
											PaletId = temp.PaletId,
											CodigoEmpresa = temp.CodigoEmpresa,
											CodigoArticulo = temp.CodigoArticulo,
											DescripcionArticulo = temp.DescripcionArticulo,
											Cantidad = temp.Cantidad,      // ← DELTA (+)
											UnidadMedida = temp.UnidadMedida,
											Lote = temp.Lote,
											FechaCaducidad = temp.FechaCaducidad,
											CodigoAlmacen = temp.CodigoAlmacen,
											Ubicacion = temp.Ubicacion,
											UsuarioId = temp.UsuarioId,
											FechaAgregado = temp.FechaAgregado,
											Observaciones = temp.Observaciones,
											TraspasoId = traspaso.Id
										});
									}
								}

								temp.Procesada = true;
								dbContext.TempPaletLineas.Update(temp);
							}

							// 4) Solo mover líneas por TRASPASO DE PALET (no por artículo)
							var traspasosMoverPalet = await dbContext.Traspasos
								.Where(t => t.TipoTraspaso == "PALET" &&
											(t.CodigoEstado == "COMPLETADO") &&
											t.PaletId == paletId)
								.OrderBy(t => t.FechaFinalizacion)
								.ToListAsync();

							foreach (var t in traspasosMoverPalet)
							{
								var lineasDelTraspaso = await dbContext.PaletLineas
									.Where(l => l.PaletId == paletId && l.TraspasoId == t.Id)
									.ToListAsync();

								foreach (var linea in lineasDelTraspaso)
								{
									linea.CodigoAlmacen = t.AlmacenDestino;
									linea.Ubicacion = t.UbicacionDestino;
									dbContext.PaletLineas.Update(linea);
								}
							}

							// 5) Marcar VACÍADO SOLO cuando:
							//    - no quedan temporales pendientes de ese palet
							//    - el stock total definitivo del palet es 0 (o <= 0 por si hay redondeos)
							//    - y no hay ninguna línea con cantidad > 0
							var quedanTemporales = await dbContext.TempPaletLineas
								.AnyAsync(l => l.PaletId == paletId && l.Procesada == false);

							if (!quedanTemporales)
							{
								// suma total de cantidades del palet
								var totalPalet = await dbContext.PaletLineas
									.Where(l => l.PaletId == paletId)
									.SumAsync(l => (decimal?)l.Cantidad) ?? 0m;

								// también comprobamos explícitamente que NO exista ninguna línea positiva
								var hayPositivas = await dbContext.PaletLineas
									.AnyAsync(l => l.PaletId == paletId && l.Cantidad > 0m);

								if (totalPalet <= 0m && !hayPositivas)
								{
									var palet = await dbContext.Palets.FindAsync(paletId);
									if (palet != null && !string.Equals(palet.Estado, "Vaciado", StringComparison.OrdinalIgnoreCase))
									{
										palet.Estado = "Vaciado";
										palet.FechaVaciado = DateTime.Now;

										// intenta registrar el usuario del último delta negativo
										var ultNeg = await dbContext.TempPaletLineas
											.Where(x => x.PaletId == paletId && x.Cantidad < 0 && x.Procesada == true)
											.OrderByDescending(x => x.FechaAgregado)
											.FirstOrDefaultAsync();

										palet.UsuarioVaciadoId = ultNeg?.UsuarioId ?? palet.UsuarioVaciadoId;

										// opcional: cierra también
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

										await dbContext.SaveChangesAsync();
									}
								}
							}

							await dbContext.SaveChangesAsync();

							//	Buscar todos los traspasos COMPLETADOS para ese palet
							//	var traspasosCompletados = await dbContext.Traspasos
							//		.Where(t => t.PaletId == paletId && t.CodigoEstado == "COMPLETADO")
							//		.OrderBy(t => t.FechaFinalizacion)
							//		.ToListAsync();

							//	foreach (var traspaso in traspasosCompletados)
							//	{
							//		Solo procesar líneas temporales con TraspasoId igual al traspaso completado y no procesadas
							//	   var tempLineas = await dbContext.TempPaletLineas
							//		   .Where(l => l.PaletId == paletId && l.TraspasoId == traspaso.Id && l.Procesada == false)
							//		   .ToListAsync();

							//		if (tempLineas.Any())
							//		{
							//			foreach (var tempLinea in tempLineas)
							//			{
							//				Buscar si ya existe una línea definitiva para este artículo/ lote / fecha(comparando nulls correctamente)
							//				var existente = await dbContext.PaletLineas.FirstOrDefaultAsync(l =>
							//					l.PaletId == tempLinea.PaletId &&
							//					l.CodigoArticulo == tempLinea.CodigoArticulo &&
							//					l.Lote == tempLinea.Lote &&
							//					((l.FechaCaducidad == null && tempLinea.FechaCaducidad == null) || (l.FechaCaducidad != null && tempLinea.FechaCaducidad != null && l.FechaCaducidad == tempLinea.FechaCaducidad))
							//				);

							//				if (existente != null)
							//				{
							//					// Recalcular la cantidad definitiva como la suma de todas las líneas temporales procesadas NO heredadas (más la actual si tampoco es heredada)
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
							//					Solo aplicamos el DELTA de la temporal actual(si usas EsHeredada, respétalo)
							//					if (!tempLinea.EsHeredada)
							//						existente.Cantidad += tempLinea.Cantidad;

							//					La ubicación final viene de la temporal(ya trae el destino del traspaso)
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

							//ACTUALIZACIÓN: Mover todas las líneas definitivas del palet a la última ubicación destino
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
							//			linea.Ubicacion = t.UbicacionDestino;   // si aquí quieres permitir “sin ubicar”, se queda así
							//			dbContext.PaletLineas.Update(linea);
							//		}
							//	}
							//}
							//await dbContext.SaveChangesAsync();

							//// Unificación de líneas definitivas duplicadas en PaletLineas (en memoria y normalizando)
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
							//await dbContext.SaveChangesAsync(); // <-- Guardar los cambios de la unificación
						}
					}
				}
				finally
				{
					_enEjecucion = false;
				}
				await Task.Delay(_intervalo, stoppingToken);
			}
		}
	}
}


//using Microsoft.EntityFrameworkCore;
//using SGA_Api.Data;
//using SGA_Api.Models.Palet;
//using SGA_Api.Models.Traspasos;
//using System.Linq;

//namespace SGA_Api.Services
//{
//	public class TraspasoFinalizacionBackgroundService : BackgroundService
//	{
//		private readonly IServiceProvider _serviceProvider;
//		private readonly TimeSpan _intervalo = TimeSpan.FromSeconds(5);
//		private bool _enEjecucion;

//		public TraspasoFinalizacionBackgroundService(IServiceProvider serviceProvider)
//		{
//			_serviceProvider = serviceProvider;
//		}

//		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//		{
//			while (!stoppingToken.IsCancellationRequested)
//			{
//				if (_enEjecucion)
//				{
//					await Task.Delay(_intervalo, stoppingToken);
//					continue;
//				}

//				_enEjecucion = true;
//				try
//				{
//					using var scope = _serviceProvider.CreateScope();
//					var db = scope.ServiceProvider.GetRequiredService<AuroraSgaDbContext>();

//					// 0) Primero, tratar traspasos CANCELADOS (reversa) para que no se mezclen con aplicados
//					await RevertirTraspasosCancelados(db);

//					// 1) Palets con temporales (pendientes o no) — procesaremos pendiente->aplicar y ya cancelados fueron revertidos arriba
//					var paletIds = await db.TempPaletLineas
//						.Select(l => l.PaletId)
//						.Distinct()
//						.ToListAsync(stoppingToken);

//					foreach (var paletId in paletIds)
//					{
//						// 2) Aplicar SOLO las temporales NO procesadas cuyos traspasos estén en PENDIENTE_ERP o COMPLETADO
//						var temps = await db.TempPaletLineas
//							.Where(t => t.PaletId == paletId && t.Procesada == false)
//							.OrderBy(t => t.FechaAgregado)
//							.ToListAsync(stoppingToken);

//						foreach (var temp in temps)
//						{
//							var tr = await db.Traspasos.FindAsync(new object?[] { temp.TraspasoId }, cancellationToken: stoppingToken);
//							if (tr == null)
//							{
//								// No hay traspaso: marca como procesada para que no bloquee el bucle
//								temp.Procesada = true;
//								db.TempPaletLineas.Update(temp);
//								continue;
//							}

//							var estado = (tr.CodigoEstado ?? "").ToUpperInvariant();

//							// PENDIENTE: no tocar aún, se quedará sin procesar
//							if (estado == "PENDIENTE")
//								continue;

//							// CANCELADO: por seguridad, no aplicar (la reversión ya la hace RevertirTraspasosCancelados)
//							if (estado == "CANCELADO")
//							{
//								temp.Procesada = true;
//								temp.Observaciones = AgregarMarca(temp.Observaciones, "CANCELADO (no aplicado)");
//								db.TempPaletLineas.Update(temp);
//								continue;
//							}

//							// PENDIENTE_ERP o COMPLETADO -> aplicar delta a PaletLineas
//							if (estado == "PENDIENTE_ERP" || estado == "COMPLETADO")
//							{
//								await AplicarDeltaAsync(db, temp, tr);
//								temp.Procesada = true;
//								db.TempPaletLineas.Update(temp);
//							}
//						}

//						// 3) Mover líneas por TRASPASO DE PALET (COMPLETADO) — SOLO afecta ubicación, no cantidades
//						var traspasosMoverPalet = await db.Traspasos
//							.Where(t => t.PaletId == paletId &&
//										t.TipoTraspaso == "PALET" &&
//										t.CodigoEstado == "COMPLETADO")
//							.OrderBy(t => t.FechaFinalizacion)
//							.ToListAsync(stoppingToken);

//						foreach (var t in traspasosMoverPalet)
//						{
//							var lineasDeEseTraspaso = await db.PaletLineas
//								.Where(l => l.PaletId == paletId && l.TraspasoId == t.Id)
//								.ToListAsync(stoppingToken);

//							foreach (var linea in lineasDeEseTraspaso)
//							{
//								linea.CodigoAlmacen = t.AlmacenDestino;
//								linea.Ubicacion = t.UbicacionDestino;
//								db.PaletLineas.Update(linea);
//							}
//						}



//						// 4) Vaciado: solo si realmente no queda NADA tras todo lo anterior
//						var quedanLineas = await db.PaletLineas
//							.AnyAsync(l => l.PaletId == paletId && l.Cantidad > 0m, stoppingToken);

//						if (!quedanLineas)
//						{
//							var palet = await db.Palets.FindAsync(new object?[] { paletId }, cancellationToken: stoppingToken);
//							if (palet != null && !string.Equals(palet.Estado, "Vaciado", StringComparison.OrdinalIgnoreCase))
//							{
//								palet.Estado = "Vaciado";
//								palet.FechaVaciado = DateTime.Now;
//								var ultNegativa = await db.TempPaletLineas
//									.Where(x => x.PaletId == paletId && x.Cantidad < 0 && x.Procesada)
//									.OrderByDescending(x => x.FechaAgregado)
//									.FirstOrDefaultAsync(stoppingToken);

//								palet.UsuarioVaciadoId = ultNegativa?.UsuarioId ?? palet.UsuarioVaciadoId;
//								palet.FechaCierre = palet.FechaCierre ?? DateTime.Now;
//								palet.UsuarioCierreId = palet.UsuarioCierreId ?? ultNegativa?.UsuarioId;

//								db.Palets.Update(palet);
//								db.LogPalet.Add(new LogPalet
//								{
//									PaletId = palet.Id,
//									Fecha = DateTime.Now,
//									IdUsuario = palet.UsuarioVaciadoId ?? 0,
//									Accion = "Vaciado",
//									Detalle = "Palet sin líneas tras consolidación."
//								});
//							}
//						}

//						await db.SaveChangesAsync(stoppingToken);
//					}
//				}
//				finally
//				{
//					_enEjecucion = false;
//				}

//				await Task.Delay(_intervalo, stoppingToken);
//			}
//		}

//		// ========= Helpers =========
//		private static async Task AplicarDeltaAsync(AuroraSgaDbContext db, TempPaletLinea temp, Traspaso tr)
//		{
//			var existente = await db.PaletLineas.FirstOrDefaultAsync(l =>
//				l.PaletId == temp.PaletId &&
//				l.CodigoArticulo == temp.CodigoArticulo &&
//				l.Lote == temp.Lote &&
//				l.FechaCaducidad == temp.FechaCaducidad &&
//				(l.CodigoAlmacen ?? "") == (temp.CodigoAlmacen ?? "") &&
//				(l.Ubicacion ?? "") == (temp.Ubicacion ?? "")
//			);

//			if (existente != null)
//			{
//				if (!temp.EsHeredada)
//					existente.Cantidad += temp.Cantidad;   // suma/resta tal cual (sin Round)

//				if (existente.Cantidad <= 0m)
//				{
//					db.PaletLineas.Remove(existente);      // borra sólo si 0 o negativo
//				}
//				else
//				{
//					existente.UsuarioId = temp.UsuarioId;
//					existente.Observaciones = temp.Observaciones;
//					existente.TraspasoId = tr.Id;
//					db.PaletLineas.Update(existente);
//				}
//			}
//			else
//			{
//				if (temp.Cantidad != 0m) // no crear líneas con 0
//				{
//					db.PaletLineas.Add(new PaletLinea
//					{
//						Id = Guid.NewGuid(),
//						PaletId = temp.PaletId,
//						CodigoEmpresa = temp.CodigoEmpresa,
//						CodigoArticulo = temp.CodigoArticulo,
//						DescripcionArticulo = temp.DescripcionArticulo,
//						Cantidad = temp.Cantidad,          // sin redondeo
//						UnidadMedida = temp.UnidadMedida,
//						Lote = temp.Lote,
//						FechaCaducidad = temp.FechaCaducidad,
//						CodigoAlmacen = temp.CodigoAlmacen,
//						Ubicacion = temp.Ubicacion,
//						UsuarioId = temp.UsuarioId,
//						FechaAgregado = temp.FechaAgregado,
//						Observaciones = temp.Observaciones,
//						TraspasoId = tr.Id
//					});
//				}
//			}
//		}



//		/// <summary>
//		/// Revertir deltas de traspasos que fueron CANCELADOS.
//		/// Recorre tanto temporales ya procesadas (para deshacer) como no procesadas (se descartan).
//		/// </summary>
//		private static async Task RevertirTraspasosCancelados(AuroraSgaDbContext db)
//		{
//			var cancelados = await db.Traspasos
//				.Where(t => t.CodigoEstado == "CANCELADO")
//				.Select(t => t.Id)
//				.ToListAsync();

//			if (cancelados.Count == 0) return;

//			var temps = await db.TempPaletLineas
//	.Where(tp => tp.TraspasoId.HasValue && cancelados.Contains(tp.TraspasoId.Value))
//	.ToListAsync();



//			foreach (var temp in temps)
//			{
//				if (temp.Procesada)
//				{
//					// Si esa temporal ya se aplicó, revertir su delta en PaletLineas
//					var existente = await db.PaletLineas.FirstOrDefaultAsync(l =>
//						l.PaletId == temp.PaletId &&
//						l.CodigoArticulo == temp.CodigoArticulo &&
//						l.Lote == temp.Lote &&
//						l.FechaCaducidad == temp.FechaCaducidad &&
//						(l.CodigoAlmacen ?? "") == (temp.CodigoAlmacen ?? "") &&
//						(l.Ubicacion ?? "") == (temp.Ubicacion ?? "")
//					);

//					if (existente != null)
//					{
//						existente.Cantidad -= temp.Cantidad; // revertir delta
//						if (existente.Cantidad == 0m)
//							db.PaletLineas.Remove(existente);
//						else
//							db.PaletLineas.Update(existente);
//					}
//				}

//				// Marcar procesada y anotar cancelación (evita re-procesos futuros)
//				temp.Procesada = true;
//				temp.Observaciones = AgregarMarca(temp.Observaciones, "CANCELADO (revertido)");
//				db.TempPaletLineas.Update(temp);

//				// Log
//				db.LogPalet.Add(new LogPalet
//				{
//					PaletId = temp.PaletId,
//					Fecha = DateTime.Now,
//					IdUsuario = temp.UsuarioId,
//					Accion = "RevertirCancelacion",
//					Detalle = $"Revertido delta {temp.Cantidad} del art {temp.CodigoArticulo} por traspaso cancelado."
//				});
//			}

//			await db.SaveChangesAsync();
//		}

//		private static string AgregarMarca(string? original, string marca)
//		{
//			original ??= "";
//			if (original.Contains(marca)) return original;
//			return string.IsNullOrWhiteSpace(original) ? marca : $"{original} | {marca}";
//		}
//	}
//}
