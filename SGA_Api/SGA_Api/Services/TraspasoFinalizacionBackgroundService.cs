using SGA_Api.Data;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Models.Palet;
using SGA_Api.Models.Notificaciones;
using Microsoft.Extensions.Logging;

namespace SGA_Api.Services
{
	public class TraspasoFinalizacionBackgroundService : BackgroundService
	{
		private readonly IServiceProvider _serviceProvider;
		private readonly TimeSpan _intervalo = TimeSpan.FromMilliseconds(500); // Se ejecuta cada 0.5 segundos para detectar cambios muy r�pidos
		private bool _enEjecucion = false;
		
		// Diccionario para almacenar estados anteriores de traspasos (para detectar cambios)
		private readonly Dictionary<Guid, string> _estadosAnterioresTraspasos = new();

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
						var notificacionesService = scope.ServiceProvider.GetRequiredService<INotificacionesTraspasosService>();
						var logger = scope.ServiceProvider.GetRequiredService<ILogger<TraspasoFinalizacionBackgroundService>>();

						// 1. DETECCI�N DE NOTIFICACIONES - Para TODOS los traspasos (ARTICULO y PALET)
						await DetectarYNotificarCambiosEstadoAsync(dbContext, notificacionesService, logger);

						// 2. CONSOLIDACI�N DE PALETS - Solo para traspasos que afectan palets
						// Obtener todos los paletId con l�neas temporales
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

								// Si quieres exigir �completado�, acepta lo que ponga el controller
								var esCompletado =
									string.Equals(traspaso.CodigoEstado, "COMPLETADO", StringComparison.OrdinalIgnoreCase) ||
									string.Equals(traspaso.CodigoEstado, "PENDIENTE_ERP", StringComparison.OrdinalIgnoreCase); // opcional

								if (!esCompletado) continue;

								// 3) Busca la l�nea definitiva (misma clave)
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
										existente.Cantidad += temp.Cantidad;  // ? DELTA (+/-)

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
										
										// Debug: Log para verificar la propagaci�n de descripci�n
										logger.LogDebug("Propagando descripci�n '{DescripcionArticulo}' para art�culo {CodigoArticulo}", temp.DescripcionArticulo, temp.CodigoArticulo);
										
										dbContext.PaletLineas.Update(existente);
									}
								}
								else
								{
									if (temp.Cantidad != 0m) // evita crear l�neas 0
									{
										dbContext.PaletLineas.Add(new SGA_Api.Models.Palet.PaletLinea
										{
											Id = Guid.NewGuid(),
											PaletId = temp.PaletId,
											CodigoEmpresa = temp.CodigoEmpresa,
											CodigoArticulo = temp.CodigoArticulo,
											DescripcionArticulo = temp.DescripcionArticulo,
											Cantidad = temp.Cantidad,      // ? DELTA (+)
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

							// 4) Solo mover l�neas por TRASPASO DE PALET (no por art�culo)
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

							// 5) Marcar VAC�ADO SOLO cuando:
							//    - no quedan temporales pendientes de ese palet
							//    - el stock total definitivo del palet es 0 (o <= 0 por si hay redondeos)
							//    - y no hay ninguna l�nea con cantidad > 0
							var quedanTemporales = await dbContext.TempPaletLineas
								.AnyAsync(l => l.PaletId == paletId && l.Procesada == false);

							if (!quedanTemporales)
							{
								// suma total de cantidades del palet
								var totalPalet = await dbContext.PaletLineas
									.Where(l => l.PaletId == paletId)
									.SumAsync(l => (decimal?)l.Cantidad) ?? 0m;

								// tambi�n comprobamos expl�citamente que NO exista ninguna l�nea positiva
								var hayPositivas = await dbContext.PaletLineas
									.AnyAsync(l => l.PaletId == paletId && l.Cantidad > 0m);

								if (totalPalet <= 0m && !hayPositivas)
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
											Detalle = "Marcado vaciado tras consolidaci�n: total=0 y sin temporales pendientes."
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
					}
				}
				finally
				{
					_enEjecucion = false;
				}
				await Task.Delay(_intervalo, stoppingToken);
			}
		}

		/// <summary>
		/// Detecta cambios de estado en traspasos y env�a notificaciones popup correspondientes
		/// </summary>
		private async Task DetectarYNotificarCambiosEstadoAsync(AuroraSgaDbContext dbContext, INotificacionesTraspasosService notificacionesService, ILogger<TraspasoFinalizacionBackgroundService> logger)
		{
			try
			{
				// Obtener traspasos que pueden cambiar de estado
				// - PENDIENTE_ERP: Puede cambiar a COMPLETADO o ERROR_ERP
				// - PENDIENTE: Puede cambiar a PENDIENTE_ERP, COMPLETADO o ERROR_ERP
				// - COMPLETADO: Estado final exitoso (se incluye para detectar la transici�n)
				// - ERROR_ERP: Estado final con error (se incluye para detectar la transici�n)
				// - Excluir solo CANCELADO (estado final que no interesa notificar)
				var traspasosActivos = await dbContext.Traspasos
					.Where(t => t.CodigoEstado != "CANCELADO" && 
							   t.UsuarioInicioId > 0 &&
							   (t.TipoTraspaso == "ARTICULO" || t.TipoTraspaso == "PALET")) // Solo traspasos v�lidos
					.Select(t => new { t.Id, t.CodigoEstado, t.TipoTraspaso, t.UsuarioInicioId, t.CodigoPalet, t.CodigoArticulo })
					.ToListAsync();

				// Solo log cuando hay traspasos activos para revisar
				if (traspasosActivos.Count > 0)
				{
					logger.LogDebug("?? BackgroundService: Revisando {Cantidad} traspasos activos", traspasosActivos.Count);
				}

				foreach (var traspaso in traspasosActivos)
				{
					try
				{
					var estadoAnterior = _estadosAnterioresTraspasos.GetValueOrDefault(traspaso.Id, "");
					var estadoActual = traspaso.CodigoEstado ?? "";

						// Log todos los traspasos para debug
						if (string.IsNullOrEmpty(estadoAnterior))
						{
							logger.LogDebug("?? Nuevo traspaso detectado: {TraspasoId} - Estado inicial: {EstadoActual} (Usuario: {UsuarioId}, Tipo: {TipoTraspaso})", 
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
								await EnviarNotificacionCambioEstadoAsync(traspaso, estadoAnterior, estadoActual, notificacionesService, dbContext, logger);
							}
							else
							{
								logger.LogWarning("?? No se puede enviar notificaci�n para traspaso {TraspasoId}: CodigoIdentificador={CodigoIdentificador}, UsuarioId={UsuarioId}", 
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
		private async Task EnviarNotificacionCambioEstadoAsync(object traspaso, string estadoAnterior, string estadoActual, INotificacionesTraspasosService notificacionesService, AuroraSgaDbContext dbContext, ILogger<TraspasoFinalizacionBackgroundService> logger)
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

				logger.LogDebug("?? Preparando notificaci�n: TraspasoId={TraspasoId}, UsuarioId={UsuarioId}, Tipo={TipoTraspaso}, Codigo={CodigoIdentificador}, Estado={EstadoActual}", 
					traspasoId, usuarioId, tipoTraspaso, codigoIdentificador, estadoActual);

				if (usuarioId <= 0 || string.IsNullOrEmpty(codigoIdentificador))
				{
					logger.LogWarning("?? No se puede enviar notificaci�n: UsuarioId={UsuarioId}, CodigoIdentificador={CodigoIdentificador}", usuarioId, codigoIdentificador);
					return;
				}

				// Obtener informaci�n adicional del traspaso desde la base de datos
				var informacionAdicional = await ObtenerInformacionAdicionalTraspasoAsync(traspasoId, tipoTraspaso, logger);
				
				logger.LogDebug("?? Informaci�n adicional obtenida: '{InformacionAdicional}'", informacionAdicional);

				string titulo, mensaje, tipoNotificacion;

				// Determinar el contenido de la notificaci�n seg�n el estado y tipo de traspaso
				switch (estadoActual.ToUpper())
				{
					case "COMPLETADO":
						titulo = "Traspaso Completado";
						if (tipoTraspaso == "PALET")
							mensaje = $"Traspaso de palet {codigoIdentificador} completado exitosamente";
						else
							mensaje = $"Traspaso de art�culo {codigoIdentificador} completado exitosamente";
						tipoNotificacion = "success";
						break;

					case "PENDIENTE_ERP":
						titulo = "Traspaso en Proceso";
						if (tipoTraspaso == "PALET")
							mensaje = $"Traspaso de palet {codigoIdentificador} proces�ndose";
						else
							mensaje = $"Traspaso de art�culo {codigoIdentificador} proces�ndose";
						tipoNotificacion = "info";
						break;

					case "ERROR_ERP":
						titulo = "Error en Traspaso";
						if (tipoTraspaso == "PALET")
							mensaje = $"Traspaso de palet {codigoIdentificador} fall�";
						else
							mensaje = $"Traspaso de art�culo {codigoIdentificador} fall�";
						tipoNotificacion = "error";
						break;

					default:
						// Para otros estados, no enviar notificaci�n espec�fica
						logger.LogDebug("?? Estado no notificable: {EstadoActual}", estadoActual);
						return;
				}

				// PASO 1: Guardar notificaci�n en base de datos para persistencia
				var mensajeCompleto = $"{mensaje}\n{informacionAdicional}".Trim();
				var guardadoEnBD = false;
				
				try
				{
					guardadoEnBD = await GuardarNotificacionEnBDAsync(
						dbContext, 
						usuarioId, 
						titulo, 
						mensajeCompleto, 
						tipoNotificacion, 
						traspasoId, 
						estadoAnterior, 
						estadoActual, 
						logger);
				}
				catch (Exception ex)
				{
					logger.LogError(ex, "? Error cr�tico al guardar notificaci�n en BD para traspaso {TraspasoId}", traspasoId);
					// Continuar con SignalR aunque falle la BD
				}

				// PASO 2: Enviar notificaci�n por SignalR (mantener funcionalidad existente)
				var maxIntentos = 3;
				var intento = 0;
				var enviadoSignalR = false;

				while (intento < maxIntentos && !enviadoSignalR)
				{
					try
					{
						intento++;
						logger.LogDebug("?? Enviando notificaci�n SignalR (intento {Intento}/{MaxIntentos}): {Titulo} - {Mensaje}", 
							intento, maxIntentos, titulo, mensaje);

						await notificacionesService.NotificarPopupUsuarioAsync(usuarioId, titulo, mensajeCompleto, tipoNotificacion);
						enviadoSignalR = true;
						
						logger.LogInformation("? Notificaci�n SignalR enviada exitosamente para traspaso {TraspasoId}: {EstadoAnterior} -> {EstadoActual} | BD: {GuardadoEnBD}", 
							traspasoId, estadoAnterior, estadoActual, guardadoEnBD ? "?" : "?");
					}
					catch (Exception ex)
					{
						logger.LogWarning(ex, "?? Error al enviar notificaci�n SignalR (intento {Intento}/{MaxIntentos}) para traspaso {TraspasoId}", 
							intento, maxIntentos, traspasoId);
						
						if (intento < maxIntentos)
						{
							await Task.Delay(1000 * intento); // Espera progresiva: 1s, 2s, 3s
						}
					}
				}

				if (!enviadoSignalR)
				{
					logger.LogError("? No se pudo enviar notificaci�n SignalR despu�s de {MaxIntentos} intentos para traspaso {TraspasoId} | BD: {GuardadoEnBD}", 
						maxIntentos, traspasoId, guardadoEnBD ? "?" : "?");
				}
			}
			catch (Exception ex)
			{
				var traspasoId = traspaso.GetType().GetProperty("Id")?.GetValue(traspaso)?.ToString() ?? "desconocido";
				logger.LogError(ex, "? Error cr�tico al enviar notificaci�n para traspaso {TraspasoId}", traspasoId);
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
					informacion.Add($"Ubicaci�n: {ubicacionOrigen} ? {ubicacionDestino}");
				}

				// Para traspasos de art�culo, obtener cantidad y descripci�n
				if (tipoTraspaso == "ARTICULO" && !string.IsNullOrEmpty(traspaso.CodigoArticulo))
				{
					logger.LogDebug("?? Buscando informaci�n de cantidad para TraspasoId={TraspasoId}, CodigoArticulo={CodigoArticulo}", 
						traspasoId, traspaso.CodigoArticulo);

					var cantidadEncontrada = false;

					// 1. PRIMERO: Buscar en la tabla Traspasos directamente (para art�culos sueltos)
					if (traspaso.Cantidad != null && traspaso.Cantidad != 0)
					{
						informacion.Add($"Cantidad: {Math.Abs(traspaso.Cantidad.Value):F4}");
						logger.LogDebug("? Cantidad encontrada en Traspasos: {Cantidad}", Math.Abs(traspaso.Cantidad.Value));
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
							logger.LogDebug("?? TempLinea encontrada: TraspasoId={TraspasoId}, CodigoArticulo={CodigoArticulo}, Cantidad={Cantidad}, UnidadMedida={UnidadMedida}", 
								traspasoId, traspaso.CodigoArticulo, tempLinea.Cantidad, tempLinea.UnidadMedida);
							
							if (tempLinea.Cantidad != 0)
							{
								informacion.Add($"Cantidad: {Math.Abs(tempLinea.Cantidad):F4}");
								logger.LogDebug("? Cantidad encontrada en TempPaletLineas: {Cantidad}", Math.Abs(tempLinea.Cantidad));
								cantidadEncontrada = true;
							}
						}
					}

					// 3. TERCERO: Buscar en PaletLineas (para l�neas ya consolidadas)
					if (!cantidadEncontrada)
					{
						logger.LogDebug("?? TempLinea NO encontrada para TraspasoId={TraspasoId}, CodigoArticulo={CodigoArticulo}, buscando en PaletLineas", 
							traspasoId, traspaso.CodigoArticulo);
						
						var paletLinea = await dbContext.PaletLineas
							.Where(pl => pl.TraspasoId == traspasoId && pl.CodigoArticulo == traspaso.CodigoArticulo)
							.FirstOrDefaultAsync();

						if (paletLinea != null)
						{
							logger.LogDebug("?? PaletLinea encontrada: TraspasoId={TraspasoId}, CodigoArticulo={CodigoArticulo}, Cantidad={Cantidad}, UnidadMedida={UnidadMedida}", 
								traspasoId, traspaso.CodigoArticulo, paletLinea.Cantidad, paletLinea.UnidadMedida);
							
							if (paletLinea.Cantidad != 0)
							{
								informacion.Add($"Cantidad: {Math.Abs(paletLinea.Cantidad):F4}");
								logger.LogDebug("? Cantidad encontrada en PaletLineas: {Cantidad}", Math.Abs(paletLinea.Cantidad));
								cantidadEncontrada = true;
							}
						}
					}

					// 4. Si no encontramos cantidad en ning�n lado, log de debug
					if (!cantidadEncontrada)
					{
						logger.LogWarning("?? No se encontr� informaci�n de cantidad para TraspasoId={TraspasoId}, CodigoArticulo={CodigoArticulo}", 
							traspasoId, traspaso.CodigoArticulo);
						
						// Debug: mostrar todos los datos del traspaso
						logger.LogWarning("?? Datos del traspaso: Cantidad={Cantidad}, AlmacenOrigen={AlmacenOrigen}, AlmacenDestino={AlmacenDestino}, UbicacionOrigen={UbicacionOrigen}, UbicacionDestino={UbicacionDestino}", 
							traspaso.Cantidad, traspaso.AlmacenOrigen, traspaso.AlmacenDestino, traspaso.UbicacionOrigen, traspaso.UbicacionDestino);
					}
				}

				var resultado = string.Join("\n", informacion);
				logger.LogDebug("?? Resultado final de informaci�n adicional: '{Resultado}'", resultado);
				return resultado;
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "Error al obtener informaci�n adicional del traspaso {TraspasoId}", traspasoId);
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
						FechaCreacion = DateTime.UtcNow,
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
						FechaCreacion = DateTime.UtcNow,
						EsActiva = true
					};

					dbContext.NotificacionesDestinatarios.Add(destinatario);

					// Guardar cambios
					await dbContext.SaveChangesAsync();
					await transaction.CommitAsync();

					logger.LogDebug("?? Notificaci�n guardada en BD: {IdNotificacion} para usuario {UsuarioId}", 
						notificacion.IdNotificacion, usuarioId);

					return true;
				}
				catch (Exception ex)
				{
					await transaction.RollbackAsync();
					logger.LogError(ex, "? Error al guardar notificaci�n en BD para traspaso {ProcesoId}, usuario {UsuarioId}", 
						procesoId, usuarioId);
					return false;
				}
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "? Error cr�tico al guardar notificaci�n en BD para traspaso {ProcesoId}, usuario {UsuarioId}", 
					procesoId, usuarioId);
				return false;
			}
		}
	}
}
