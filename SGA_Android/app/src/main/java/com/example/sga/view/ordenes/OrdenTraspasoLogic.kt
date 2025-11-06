package com.example.sga.view.ordenes

import android.util.Log
import com.example.sga.data.ApiManager
import com.example.sga.data.dto.ordenes.*
import com.example.sga.data.dto.stock.ArticuloDto
import com.example.sga.data.model.user.User
import com.example.sga.view.app.SessionViewModel
import com.example.sga.view.traspasos.TraspasosLogic
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.text.SimpleDateFormat
import java.util.*

class OrdenTraspasoLogic(
    private val ordenTraspasoViewModel: OrdenTraspasoViewModel,
    private val sessionViewModel: SessionViewModel
) {
    
    private val apiService = ApiManager.ordenTraspasoApi
    private val scope = CoroutineScope(Dispatchers.Main)
    private val traspasosLogic = TraspasosLogic()
    
    // Listar órdenes de traspaso del operario
    fun listarOrdenes(user: User) {
        ordenTraspasoViewModel.setCargando(true)
        ordenTraspasoViewModel.limpiarMensajes()
        
        scope.launch {
            try {
                val codigoEmpresa = getCodigoEmpresa(sessionViewModel)
                Log.d("OrdenTraspasoLogic", "📡 Iniciando carga de órdenes...")
                Log.d("OrdenTraspasoLogic", "👤 CodigoOperario: ${user.id}")
                Log.d("OrdenTraspasoLogic", "🏢 CodigoEmpresa: $codigoEmpresa")
                
                val response = withContext(Dispatchers.IO) {
                    apiService.getOrdenesPorOperario(user.id.toInt(), codigoEmpresa)
                }
                
                if (response.isSuccessful) {
                    val ordenes = response.body() ?: emptyList()
                    Log.d("OrdenTraspasoLogic", "✅ Respuesta recibida: ${ordenes.size} órdenes")
                    ordenTraspasoViewModel.setOrdenes(ordenes)
                    ordenTraspasoViewModel.setError(null)
                } else {
                    val errorMsg = "Error: ${response.code()} - ${response.message()}"
                    Log.e("OrdenTraspasoLogic", "❌ $errorMsg")
                    ordenTraspasoViewModel.setError(errorMsg)
                }
                
            } catch (e: Exception) {
                Log.e("OrdenTraspasoLogic", "❌ Error al cargar órdenes: ${e.message}")
                ordenTraspasoViewModel.setError("Error al cargar órdenes: ${e.message}")
            } finally {
                ordenTraspasoViewModel.setCargando(false)
            }
        }
    }
    
    // Obtener orden sin actualizar el ViewModel (para verificaciones silenciosas)
    suspend fun obtenerOrdenSilenciosa(idOrden: String): OrdenTraspasoDto? {
        return try {
            val response = withContext(Dispatchers.IO) {
                apiService.getOrdenTraspaso(idOrden)
            }
            
            if (response.isSuccessful) {
                response.body()
            } else {
                null
            }
        } catch (e: Exception) {
            null
        }
    }
    
    // Cancelar línea de orden
    suspend fun cancelarLineaOrdenTraspaso(idLinea: String): Boolean {
        return try {
            val response = withContext(Dispatchers.IO) {
                apiService.cancelarLineaOrdenTraspaso(idLinea)
            }
            
            if (response.isSuccessful) {
                response.body() ?: false
            } else {
                false
            }
        } catch (e: Exception) {
            false
        }
    }
    
    // Cargar orden específica con detalles completos
    fun cargarOrdenDetallada(idOrden: String) {
        ordenTraspasoViewModel.setCargando(true)
        ordenTraspasoViewModel.limpiarMensajes()
        
        scope.launch {
            try {
                Log.d("OrdenTraspasoLogic", "📋 Cargando orden detallada: $idOrden")
                
                val response = withContext(Dispatchers.IO) {
                    apiService.getOrdenTraspaso(idOrden)
                }
                
                if (response.isSuccessful) {
                    val ordenDetallada = response.body()
                    if (ordenDetallada != null) {
                        Log.d("OrdenTraspasoLogic", "✅ Orden detallada cargada: ${ordenDetallada.lineas.size} líneas")
                        ordenTraspasoViewModel.setOrdenSeleccionada(ordenDetallada)
                        ordenTraspasoViewModel.setError(null)
                    } else {
                        Log.e("OrdenTraspasoLogic", "❌ Orden detallada vacía")
                        ordenTraspasoViewModel.setError("Orden no encontrada")
                    }
                } else {
                    val errorMsg = "Error: ${response.code()} - ${response.message()}"
                    Log.e("OrdenTraspasoLogic", "❌ $errorMsg")
                    ordenTraspasoViewModel.setError(errorMsg)
                }
                
            } catch (e: Exception) {
                Log.e("OrdenTraspasoLogic", "❌ Error al cargar orden detallada: ${e.message}")
                ordenTraspasoViewModel.setError("Error al cargar orden: ${e.message}")
            } finally {
                ordenTraspasoViewModel.setCargando(false)
            }
        }
    }
    
    // Iniciar orden (cambiar estado a EN_PROCESO y cargar orden completa)
    fun iniciarOrden(idOrden: String, user: User) {
        ordenTraspasoViewModel.setCargando(true)
        ordenTraspasoViewModel.limpiarMensajes()
        
        scope.launch {
            try {
                Log.d("OrdenTraspasoLogic", "🚀 Iniciando orden: $idOrden")
                
                val response = withContext(Dispatchers.IO) {
                    apiService.iniciarOrden(idOrden, user.id.toInt())
                }
                
                if (response.isSuccessful) {
                    val ordenCompleta = response.body()
                    if (ordenCompleta != null) {
                        Log.d("OrdenTraspasoLogic", "✅ Orden iniciada correctamente")
                        ordenTraspasoViewModel.setOrdenSeleccionada(ordenCompleta)
                        ordenTraspasoViewModel.setMensaje("Orden iniciada correctamente")
                        // Recargar las órdenes para reflejar el cambio
                        listarOrdenes(user)
                    } else {
                        ordenTraspasoViewModel.setError("Error: Respuesta vacía del servidor")
                    }
                } else {
                    val errorMsg = "Error: ${response.code()} - ${response.message()}"
                    Log.e("OrdenTraspasoLogic", "❌ $errorMsg")
                    ordenTraspasoViewModel.setError(errorMsg)
                }
                
            } catch (e: Exception) {
                Log.e("OrdenTraspasoLogic", "❌ Error al iniciar orden: ${e.message}")
                ordenTraspasoViewModel.setError("Error al iniciar orden: ${e.message}")
            } finally {
                ordenTraspasoViewModel.setCargando(false)
            }
        }
    }
    
    // Consultar stock de línea con lógica de subdivisión
    fun consultarStockLinea(idLinea: String, user: User) {
        ordenTraspasoViewModel.setCargando(true)
        ordenTraspasoViewModel.limpiarMensajes()
        
        scope.launch {
            try {
                Log.d("OrdenTraspasoLogic", "📦 Consultando stock para línea: $idLinea")
                
                val response = withContext(Dispatchers.IO) {
                    apiService.consultarStockLinea(idLinea)
                }
                
                if (response.isSuccessful) {
                    val stockList = response.body()
                    if (stockList != null && stockList.isNotEmpty()) {
                        Log.d("OrdenTraspasoLogic", "✅ Stock consultado correctamente: ${stockList.size} ubicaciones")
                        // Convertir de StockLineaTraspasoDto a StockDisponibleDto para compatibilidad con la UI
                        val stockDisponibleList = stockList.map { stock ->
                            StockDisponibleDto(
                                codigoAlmacen = stock.codigoAlmacen,
                                ubicacion = stock.ubicacion,
                                codigoArticulo = stock.codigoArticulo,
                                descripcionArticulo = stock.descripcionArticulo,
                                partida = stock.partida,
                                cantidadDisponible = stock.cantidadDisponible,
                                fechaCaducidad = stock.fechaCaducidad
                            )
                        }
                        ordenTraspasoViewModel.setStockDisponible(stockDisponibleList)
                        ordenTraspasoViewModel.setError(null)
                        // Recargar órdenes para reflejar cambios de estado
                        listarOrdenes(user)
                    } else {
                        ordenTraspasoViewModel.setError("Error: No hay stock disponible")
                    }
                } else {
                    val errorMsg = "Error: ${response.code()} - ${response.message()}"
                    Log.e("OrdenTraspasoLogic", "❌ $errorMsg")
                    ordenTraspasoViewModel.setError(errorMsg)
                }
                
            } catch (e: Exception) {
                Log.e("OrdenTraspasoLogic", "❌ Error al consultar stock: ${e.message}")
                ordenTraspasoViewModel.setError("Error al consultar stock: ${e.message}")
            } finally {
                ordenTraspasoViewModel.setCargando(false)
            }
        }
    }
    
    // Iniciar línea (cambiar estado a EN_PROGRESO) - Método de compatibilidad
    fun iniciarLinea(idLinea: String, user: User) {
        ordenTraspasoViewModel.setCargando(true)
        ordenTraspasoViewModel.limpiarMensajes()
        
        scope.launch {
            try {
                Log.d("OrdenTraspasoLogic", "🚀 Iniciando línea: $idLinea")
                
                val response = withContext(Dispatchers.IO) {
                    apiService.actualizarEstadoLinea(idLinea, ActualizarEstadoLineaDto("EN_PROGRESO"))
                }
                
                if (response.isSuccessful) {
                    Log.d("OrdenTraspasoLogic", "✅ Línea iniciada correctamente")
                    ordenTraspasoViewModel.setMensaje("Línea iniciada correctamente")
                    // Recargar las órdenes para reflejar el cambio
                    listarOrdenes(user)
                } else {
                    val errorMsg = "Error: ${response.code()} - ${response.message()}"
                    Log.e("OrdenTraspasoLogic", "❌ $errorMsg")
                    ordenTraspasoViewModel.setError(errorMsg)
                }
                
            } catch (e: Exception) {
                Log.e("OrdenTraspasoLogic", "❌ Error al iniciar línea: ${e.message}")
                ordenTraspasoViewModel.setError("Error al iniciar línea: ${e.message}")
            } finally {
                ordenTraspasoViewModel.setCargando(false)
            }
        }
    }
    
    // Cargar stock disponible
    fun cargarStockDisponible(codigoEmpresa: Int, codigoArticulo: String, user: User) {
        ordenTraspasoViewModel.setCargando(true)
        ordenTraspasoViewModel.limpiarMensajes()
        
        scope.launch {
            try {
                Log.d("OrdenTraspasoLogic", "📦 Cargando stock para artículo: $codigoArticulo")
                
                val response = withContext(Dispatchers.IO) {
                    apiService.getStockDisponible(codigoEmpresa, codigoArticulo, user.id.toInt())
                }
                
                if (response.isSuccessful) {
                    val stock = response.body() ?: emptyList()
                    // Ordenar por fecha de caducidad (FIFO)
                    val stockOrdenado = stock.sortedWith { a, b ->
                        when {
                            a.fechaCaducidad == null && b.fechaCaducidad == null -> 0
                            a.fechaCaducidad == null -> 1
                            b.fechaCaducidad == null -> -1
                            else -> a.fechaCaducidad.compareTo(b.fechaCaducidad)
                        }
                    }
                    Log.d("OrdenTraspasoLogic", "✅ Stock cargado: ${stockOrdenado.size} ubicaciones")
                    ordenTraspasoViewModel.setStockDisponible(stockOrdenado)
                    ordenTraspasoViewModel.setError(null)
                } else {
                    val errorMsg = "Error: ${response.code()} - ${response.message()}"
                    Log.e("OrdenTraspasoLogic", "❌ $errorMsg")
                    ordenTraspasoViewModel.setError(errorMsg)
                }
                
            } catch (e: Exception) {
                Log.e("OrdenTraspasoLogic", "❌ Error al cargar stock: ${e.message}")
                ordenTraspasoViewModel.setError("Error al cargar stock: ${e.message}")
            } finally {
                ordenTraspasoViewModel.setCargando(false)
            }
        }
    }
    
    // Completar traspaso con respuesta especial
    fun completarTraspaso(idLinea: String, user: User, dto: ActualizarLineaOrdenTraspasoDto) {
        ordenTraspasoViewModel.setCargando(true)
        ordenTraspasoViewModel.limpiarMensajes()
        
        scope.launch {
            try {
                Log.d("OrdenTraspasoLogic", "✅ Completando traspaso para línea: $idLinea")
                
                val response = withContext(Dispatchers.IO) {
                    apiService.actualizarLinea(idLinea, dto)
                }
                
                if (response.isSuccessful) {
                    val responseBody = response.body()
                    if (responseBody != null) {
                        Log.d("OrdenTraspasoLogic", "✅ Traspaso completado correctamente")
                        
                        // Verificar si hay palet listo para ubicar
                        if (responseBody.paletListoParaUbicar != null) {
                            val mensaje = responseBody.mensaje ?: "Palet ${responseBody.paletListoParaUbicar} listo para ubicar"
                            ordenTraspasoViewModel.setMensaje(mensaje)
                            ordenTraspasoViewModel.setPaletListoParaUbicar(responseBody.paletListoParaUbicar)
                            Log.d("OrdenTraspasoLogic", "📦 Palet listo para ubicar: ${responseBody.paletListoParaUbicar}")
                        } else {
                            ordenTraspasoViewModel.setMensaje("Traspaso completado correctamente")
                        }
                        
                        // Limpiar formulario
                        ordenTraspasoViewModel.limpiarFormulario()
                        // Recargar las órdenes
                        listarOrdenes(user)
                    } else {
                        ordenTraspasoViewModel.setError("Error: Respuesta vacía del servidor")
                    }
                } else {
                    val errorMsg = "Error: ${response.code()} - ${response.message()}"
                    Log.e("OrdenTraspasoLogic", "❌ $errorMsg")
                    ordenTraspasoViewModel.setError(errorMsg)
                }
                
            } catch (e: Exception) {
                Log.e("OrdenTraspasoLogic", "❌ Error al completar traspaso: ${e.message}")
                ordenTraspasoViewModel.setError("Error al completar traspaso: ${e.message}")
            } finally {
                ordenTraspasoViewModel.setCargando(false)
            }
        }
    }
    
    // Verificar palets pendientes
    fun verificarPaletsPendientes(ordenId: String, user: User) {
        ordenTraspasoViewModel.setCargando(true)
        ordenTraspasoViewModel.limpiarMensajes()
        
        scope.launch {
            try {
                Log.d("OrdenTraspasoLogic", "📦 Verificando palets pendientes para orden: $ordenId")
                
                val response = withContext(Dispatchers.IO) {
                    apiService.verificarPaletsPendientes(ordenId)
                }
                
                if (response.isSuccessful) {
                    val paletsPendientes = response.body() ?: emptyList()
                    Log.d("OrdenTraspasoLogic", "✅ Palets pendientes encontrados: ${paletsPendientes.size}")
                    ordenTraspasoViewModel.setPaletsPendientes(paletsPendientes)
                    ordenTraspasoViewModel.setError(null)
                } else {
                    val errorMsg = "Error: ${response.code()} - ${response.message()}"
                    Log.e("OrdenTraspasoLogic", "❌ $errorMsg")
                    ordenTraspasoViewModel.setError(errorMsg)
                }
                
            } catch (e: Exception) {
                Log.e("OrdenTraspasoLogic", "❌ Error al verificar palets pendientes: ${e.message}")
                ordenTraspasoViewModel.setError("Error al verificar palets pendientes: ${e.message}")
            } finally {
                ordenTraspasoViewModel.setCargando(false)
            }
        }
    }
    
    // Ubicar palet
    fun ubicarPalet(ordenId: String, paletDestino: String, dto: UbicarPaletDto, user: User) {
        ordenTraspasoViewModel.setCargando(true)
        ordenTraspasoViewModel.limpiarMensajes()
        
        scope.launch {
            try {
                Log.d("OrdenTraspasoLogic", "📍 Ubicando palet: $paletDestino en ${dto.codigoAlmacenDestino}/${dto.ubicacionDestino}")
                
                val response = withContext(Dispatchers.IO) {
                    apiService.ubicarPalet(ordenId, paletDestino, dto)
                }
                
                if (response.isSuccessful) {
                    Log.d("OrdenTraspasoLogic", "✅ Palet ubicado correctamente")
                    ordenTraspasoViewModel.setMensaje("Palet $paletDestino ubicado correctamente")
                    // Limpiar palet listo para ubicar
                    ordenTraspasoViewModel.setPaletListoParaUbicar(null)
                    // Recargar las órdenes
                    listarOrdenes(user)
                    // Verificar si hay más palets pendientes
                    verificarPaletsPendientes(ordenId, user)
                } else {
                    val errorMsg = "Error: ${response.code()} - ${response.message()}"
                    Log.e("OrdenTraspasoLogic", "❌ $errorMsg")
                    ordenTraspasoViewModel.setError(errorMsg)
                }
                
            } catch (e: Exception) {
                Log.e("OrdenTraspasoLogic", "❌ Error al ubicar palet: ${e.message}")
                ordenTraspasoViewModel.setError("Error al ubicar palet: ${e.message}")
            } finally {
                ordenTraspasoViewModel.setCargando(false)
            }
        }
    }
    
    // Obtener código de empresa seleccionada
    fun getCodigoEmpresa(sessionViewModel: SessionViewModel): Int {
        return sessionViewModel.empresaSeleccionada.value?.codigo?.toInt() ?: 1
    }
    
    // Verificar órdenes activas para el operario
    fun verificarOrdenesActivas(user: User, onResult: (Int) -> Unit) {
        Log.d("OrdenTraspasoLogic", "🔍 Verificando órdenes activas para operario: ${user.id}")
        
        scope.launch {
            try {
                val codigoEmpresa = getCodigoEmpresa(sessionViewModel)
                val response = withContext(Dispatchers.IO) {
                    apiService.getOrdenesPorOperario(user.id.toInt(), codigoEmpresa)
                }
                
                if (response.isSuccessful) {
                    val ordenes = response.body() ?: emptyList()
                    Log.d("OrdenTraspasoLogic", "✅ Órdenes obtenidas: ${ordenes.size}")
                    
                    // Contar órdenes activas
                    val ordenesActivas = ordenes.count { orden ->
                        orden.estado == "PENDIENTE" || orden.estado == "EN_PROCESO"
                    }
                    
                    Log.d("OrdenTraspasoLogic", "🎯 Órdenes activas: $ordenesActivas")
                    ordenTraspasoViewModel.setOrdenesActivas(ordenesActivas)
                    onResult(ordenesActivas)
                } else {
                    Log.e("OrdenTraspasoLogic", "❌ Error al obtener órdenes: ${response.code()}")
                    onResult(0)
                }
            } catch (e: Exception) {
                Log.e("OrdenTraspasoLogic", "❌ Error: ${e.message}")
                onResult(0)
            }
        }
    }
    
    // Crear DTO de actualización de línea
    fun crearActualizarLineaDto(
        linea: LineaOrdenTraspasoDetalleDto,
        stockSeleccionado: StockDisponibleDto,
        cantidadMovida: Double,
        ubicacionDestino: String,
        paletDestino: String,
        user: User
    ): ActualizarLineaOrdenTraspasoDto {
        val fechaActual = SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss", Locale.getDefault()).format(Date())
        
        return ActualizarLineaOrdenTraspasoDto(
            estado = "COMPLETADA",
            cantidadMovida = cantidadMovida,
            completada = true,
            idOperarioAsignado = user.id.toInt(),
            fechaInicio = linea.fechaInicio,
            fechaFinalizacion = fechaActual,
            idTraspaso = null, // Se generará en el backend
            fechaCaducidad = stockSeleccionado.fechaCaducidad,
            codigoAlmacenOrigen = stockSeleccionado.codigoAlmacen,
            ubicacionOrigen = stockSeleccionado.ubicacion,
            partida = stockSeleccionado.partida,
            paletOrigen = null, // Se puede agregar si es necesario
            codigoAlmacenDestino = linea.codigoAlmacenDestino,
            ubicacionDestino = ubicacionDestino.ifEmpty { null },
            paletDestino = paletDestino.ifEmpty { null }
        )
    }
    
    // Función para procesar escaneo específico para órdenes
    fun procesarEscaneoParaOrden(
        stockEsperado: StockDisponibleDto,
        lineaSeleccionada: LineaOrdenTraspasoDetalleDto,
        code: String,
        empresaId: Short,
        onUbicacionCorrecta: () -> Unit,
        onUbicacionIncorrecta: (esperada: String, escaneada: String) -> Unit,
        onArticuloCorrecto: (ArticuloDto) -> Unit,
        onArticuloIncorrecto: (esperado: String, escaneado: String) -> Unit,
        onError: (String) -> Unit
    ) {
        Log.d("OrdenEscaneo", "📥 Procesando código: $code")
        Log.d("OrdenEscaneo", "📍 Ubicación esperada: ${stockEsperado.codigoAlmacen}/${stockEsperado.ubicacion}")
        Log.d("OrdenEscaneo", "📦 Artículo esperado: ${lineaSeleccionada.codigoArticulo}")
        
        traspasosLogic.procesarCodigoEscaneado(
            code = code,
            empresaId = empresaId,
            codigoAlmacen = stockEsperado.codigoAlmacen,
            onUbicacionDetectada = { almacenEscaneado, ubicacionEscaneada ->
                val ubicacionEsperada = "${stockEsperado.codigoAlmacen}/${stockEsperado.ubicacion}"
                val ubicacionEscaneadaCompleta = "$almacenEscaneado/$ubicacionEscaneada"
                
                Log.d("OrdenEscaneo", "🔍 Validando ubicación:")
                Log.d("OrdenEscaneo", "  Esperada: $ubicacionEsperada")
                Log.d("OrdenEscaneo", "  Escaneada: $ubicacionEscaneadaCompleta")
                
                val almacenCorrecto = almacenEscaneado.trim().uppercase() == stockEsperado.codigoAlmacen?.trim()?.uppercase()
                val ubicacionCorrecta = ubicacionEscaneada.trim().uppercase() == stockEsperado.ubicacion?.trim()?.uppercase()
                
                if (almacenCorrecto && ubicacionCorrecta) {
                    Log.d("OrdenEscaneo", "✅ Ubicación correcta")
                    onUbicacionCorrecta()
                } else {
                    Log.d("OrdenEscaneo", "❌ Ubicación incorrecta")
                    onUbicacionIncorrecta(ubicacionEsperada, ubicacionEscaneadaCompleta)
                }
            },
            onArticuloDetectado = { articuloDto ->
                Log.d("OrdenEscaneo", "🔍 Validando artículo:")
                Log.d("OrdenEscaneo", "  Esperado: ${lineaSeleccionada.codigoArticulo}")
                Log.d("OrdenEscaneo", "  Escaneado: ${articuloDto.codigoArticulo}")
                
                val articuloCorrecto = articuloDto.codigoArticulo.trim().uppercase() == 
                                     lineaSeleccionada.codigoArticulo.trim().uppercase()
                
                if (articuloCorrecto) {
                    Log.d("OrdenEscaneo", "✅ Artículo correcto")
                    onArticuloCorrecto(articuloDto)
                } else {
                    Log.d("OrdenEscaneo", "❌ Artículo incorrecto")
                    onArticuloIncorrecto(lineaSeleccionada.codigoArticulo, articuloDto.codigoArticulo)
                }
            },
            onMultipleArticulos = { articulos ->
                Log.d("OrdenEscaneo", "📋 Múltiples artículos encontrados: ${articulos.size}")
                // Buscar el artículo correcto en la lista
                val articuloCorrecto = articulos.find { 
                    it.codigoArticulo.trim().uppercase() == lineaSeleccionada.codigoArticulo.trim().uppercase() 
                }
                
                if (articuloCorrecto != null) {
                    Log.d("OrdenEscaneo", "✅ Artículo correcto encontrado en lista")
                    onArticuloCorrecto(articuloCorrecto)
                } else {
                    Log.d("OrdenEscaneo", "❌ Artículo esperado no encontrado en lista")
                    onArticuloIncorrecto(
                        lineaSeleccionada.codigoArticulo, 
                        articulos.firstOrNull()?.codigoArticulo ?: "desconocido"
                    )
                }
            },
            onPaletDetectado = { palet ->
                Log.d("OrdenEscaneo", "📦 Palet detectado: ${palet.codigoPalet}")
                if (stockEsperado.ubicacion != null) {
                    onError("❌ Ha escaneado un palet.\nDebe escanear la ubicación: ${stockEsperado.codigoAlmacen}/${stockEsperado.ubicacion}")
                } else {
                    onError("❌ Ha escaneado un palet.\nDebe escanear el artículo: ${lineaSeleccionada.codigoArticulo}")
                }
            },
            onError = { error ->
                Log.e("OrdenEscaneo", "❌ Error: $error")
                onError(error)
            }
        )
    }
    
    fun ubicarPaletEnOrden(
        ordenId: String,
        paletDestino: String,
        codigoAlmacenDestino: String,
        ubicacionDestino: String,
        onSuccess: () -> Unit,
        onError: (String) -> Unit
    ) {
        val dto = UbicarPaletDto(
            codigoAlmacenDestino = codigoAlmacenDestino,
            ubicacionDestino = ubicacionDestino
        )
        
        CoroutineScope(Dispatchers.IO).launch {
            try {
                val response = apiService.ubicarPalet(
                    ordenId = ordenId,
                    paletDestino = paletDestino,
                    dto = dto
                )
                
                if (response.isSuccessful) {
                    Log.d("OrdenUbicarPalet", "✅ Palet $paletDestino ubicado correctamente en $codigoAlmacenDestino/$ubicacionDestino")
                    onSuccess()
                } else {
                    val error = response.errorBody()?.string() ?: "Error ${response.code()}"
                    Log.e("OrdenUbicarPalet", "❌ Error al ubicar palet: $error")
                    onError("Error al ubicar palet: $error")
                }
            } catch (e: Exception) {
                Log.e("OrdenUbicarPalet", "❌ Excepción: ${e.message}")
                onError("Error de red: ${e.message}")
            }
        }
    }
    
    fun actualizarLineaConCantidad(
        idLinea: String,
        cantidadMovida: Double,
        paletDestino: String? = null,
        codigoAlmacenOrigen: String,
        ubicacionOrigen: String,
        onSuccess: () -> Unit,
        onError: (String) -> Unit
    ) {
        scope.launch {
            try {
                android.util.Log.d("LOGIC_ACTUALIZAR", "📝 actualizarLineaConCantidad recibió:")
                android.util.Log.d("LOGIC_ACTUALIZAR", "   idLinea: $idLinea")
                android.util.Log.d("LOGIC_ACTUALIZAR", "   cantidadMovida: $cantidadMovida")
                android.util.Log.d("LOGIC_ACTUALIZAR", "   paletDestino: $paletDestino")
                android.util.Log.d("LOGIC_ACTUALIZAR", "   codigoAlmacenOrigen: '$codigoAlmacenOrigen'")
                android.util.Log.d("LOGIC_ACTUALIZAR", "   ubicacionOrigen: '$ubicacionOrigen'")
                
                val dto = ActualizarLineaOrdenTraspasoDto(
                    estado = "COMPLETADA",
                    cantidadMovida = cantidadMovida,
                    completada = true,
                    idOperarioAsignado = null,
                    fechaInicio = null,
                    fechaFinalizacion = null,
                    idTraspaso = null,
                    fechaCaducidad = null,
                    codigoAlmacenOrigen = codigoAlmacenOrigen,
                    ubicacionOrigen = ubicacionOrigen,
                    partida = null,
                    paletOrigen = null,
                    codigoAlmacenDestino = null,
                    ubicacionDestino = null,
                    paletDestino = paletDestino
                )
                
                android.util.Log.d("LOGIC_ACTUALIZAR", "📤 DTO creado con CodigoAlmacenOrigen='${dto.codigoAlmacenOrigen}', UbicacionOrigen='${dto.ubicacionOrigen}'")
                
                val response = withContext(Dispatchers.IO) {
                    apiService.actualizarLinea(idLinea, dto)
                }
                
                if (response.isSuccessful) {
                    val responseBody = response.body()
                    if (responseBody?.success == true) {
                        Log.d("OrdenTraspasoLogic", "✅ Línea actualizada correctamente")
                        onSuccess()
                    } else {
                        // El servidor devolvió success = false
                        val mensajeError = responseBody?.mensaje ?: "Error desconocido del servidor"
                        Log.e("OrdenTraspasoLogic", "❌ Error del servidor: $mensajeError")
                        onError(mensajeError)
                    }
                } else {
                    val errorBody = response.errorBody()?.string()
                    Log.e("OrdenTraspasoLogic", "❌ Error HTTP al actualizar línea ${response.code()}: $errorBody")
                    onError("Error ${response.code()}: $errorBody")
                }
            } catch (e: Exception) {
                Log.e("OrdenTraspasoLogic", "💥 Excepción al actualizar línea", e)
                onError("Error de conexión: ${e.message}")
            }
        }
    }
    
    /**
     * Actualiza una línea de orden con el IdTraspaso
     */
    fun actualizarLineaConIdTraspaso(
        dto: ActualizarLineaOrdenTraspasoDto,
        idLinea: String,
        onSuccess: () -> Unit,
        onError: (String) -> Unit
    ) {
        scope.launch {
            try {
                Log.d("OrdenTraspasoLogic", "📝 Actualizando línea $idLinea con IdTraspaso: ${dto.idTraspaso}")
                
                val response = withContext(Dispatchers.IO) {
                    apiService.actualizarLinea(idLinea, dto)
                }
                
                if (response.isSuccessful) {
                    val responseBody = response.body()
                    if (responseBody?.success == true) {
                        Log.d("OrdenTraspasoLogic", "✅ Línea $idLinea actualizada con IdTraspaso correctamente")
                        onSuccess()
                    } else {
                        val mensajeError = responseBody?.mensaje ?: "Error desconocido del servidor"
                        Log.e("OrdenTraspasoLogic", "❌ Error del servidor: $mensajeError")
                        onError(mensajeError)
                    }
                } else {
                    val errorBody = response.errorBody()?.string()
                    Log.e("OrdenTraspasoLogic", "❌ Error HTTP al actualizar línea ${response.code()}: $errorBody")
                    onError("Error ${response.code()}: $errorBody")
                }
            } catch (e: Exception) {
                Log.e("OrdenTraspasoLogic", "💥 Excepción al actualizar línea con IdTraspaso", e)
                onError("Error de conexión: ${e.message}")
            }
        }
    }
    
    /**
     * Función mejorada para actualizar línea de orden de traspaso con manejo robusto de errores
     * Maneja específicamente el caso cuando el stock físico > stock del sistema
     */
    suspend fun actualizarLineaOrdenTraspaso(
        idLinea: String,
        cantidadMovida: Double,
        paletDestino: String? = null
    ): Result<ActualizarLineaResponseDto> {
        return try {
            Log.d("OrdenTraspasoLogic", "📝 [MEJORADO] Actualizando línea $idLinea con cantidad: $cantidadMovida, palet: $paletDestino")
            
            // Obtener información de ubicación origen desde el ViewModel
            val stockOrigen = ordenTraspasoViewModel.ubicacionOrigenSeleccionada.value
            val codigoAlmacenOrigen = stockOrigen?.codigoAlmacen
            val ubicacionOrigen = stockOrigen?.ubicacion
            
            Log.d("OrdenTraspasoLogic", "📍 [MEJORADO] Ubicación origen: $codigoAlmacenOrigen/$ubicacionOrigen")
            
            val dto = ActualizarLineaOrdenTraspasoDto(
                estado = "COMPLETADA",
                cantidadMovida = cantidadMovida,
                completada = true,
                idOperarioAsignado = null,
                fechaInicio = null,
                fechaFinalizacion = null,
                idTraspaso = null,
                fechaCaducidad = null,
                codigoAlmacenOrigen = codigoAlmacenOrigen,
                ubicacionOrigen = ubicacionOrigen,
                partida = null,
                paletOrigen = null,
                codigoAlmacenDestino = null,
                ubicacionDestino = null,
                paletDestino = paletDestino
            )
            
            val response = withContext(Dispatchers.IO) {
                apiService.actualizarLinea(idLinea, dto)
            }
            
            if (response.isSuccessful) {
                val responseBody = response.body()
                if (responseBody?.success == true) {
                    Log.d("OrdenTraspasoLogic", "✅ [MEJORADO] Línea actualizada correctamente")
                    Result.success(responseBody)
                } else {
                    // El servidor devolvió success = false
                    val mensajeError = responseBody?.mensaje ?: "Error desconocido del servidor"
                    Log.e("OrdenTraspasoLogic", "❌ [MEJORADO] Error del servidor: $mensajeError")
                    Result.success(responseBody ?: ActualizarLineaResponseDto(false, null, mensajeError, null))
                }
            } else {
                val errorBody = response.errorBody()?.string()
                Log.e("OrdenTraspasoLogic", "❌ [MEJORADO] Error HTTP al actualizar línea ${response.code()}: $errorBody")
                Result.failure(Exception("Error ${response.code()}: $errorBody"))
            }
        } catch (e: Exception) {
            Log.e("OrdenTraspasoLogic", "💥 [MEJORADO] Excepción al actualizar línea", e)
            Result.failure(e)
        }
    }
    
    /**
     * Validación previa opcional para verificar cantidades antes de enviar
     */
    fun validarCantidadAntesDeEnviar(
        cantidadIngresada: Double,
        stockSistema: Double,
        onAdvertencia: (String) -> Unit
    ): Boolean {
        if (cantidadIngresada > stockSistema) {
            val mensajeAdvertencia = "La cantidad ingresada (${String.format("%.2f", cantidadIngresada)}) es mayor que el stock del sistema (${String.format("%.2f", stockSistema)}). ¿Estás seguro de continuar?"
            Log.w("OrdenTraspasoLogic", "⚠️ Validación: $mensajeAdvertencia")
            onAdvertencia(mensajeAdvertencia)
            return false
        }
        Log.d("OrdenTraspasoLogic", "✅ Validación: Cantidad válida")
        return true
    }
    
    /**
     * Desbloquear línea (solo supervisores)
     */
    suspend fun desbloquearLinea(idLinea: String): Result<Unit> {
        return try {
            Log.d("OrdenTraspasoLogic", "🔓 [DESBLOQUEO] Desbloqueando línea: $idLinea")
            
            val response = withContext(Dispatchers.IO) {
                apiService.desbloquearLinea(idLinea)
            }
            
            if (response.isSuccessful) {
                Log.d("OrdenTraspasoLogic", "✅ [DESBLOQUEO] Línea desbloqueada correctamente")
                Result.success(Unit)
            } else {
                val errorBody = response.errorBody()?.string()
                Log.e("OrdenTraspasoLogic", "❌ [DESBLOQUEO] Error HTTP al desbloquear línea ${response.code()}: $errorBody")
                Result.failure(Exception("Error ${response.code()}: $errorBody"))
            }
        } catch (e: Exception) {
            Log.e("OrdenTraspasoLogic", "💥 [DESBLOQUEO] Excepción al desbloquear línea", e)
            Result.failure(e)
        }
    }
    
    /**
     * Ajustar inventario de línea de orden de traspaso
     * Nuevo endpoint para manejar diferencias de stock
     */
    suspend fun ajustarLineaOrdenTraspaso(
        idLinea: String,
        cantidadEncontrada: Double
    ): Result<AjusteLineaResponseDto> {
        return try {
            Log.d("OrdenTraspasoLogic", "🔧 [AJUSTE] Ajustando inventario línea $idLinea con cantidad encontrada: $cantidadEncontrada")
            
            val dto = AjusteLineaOrdenTraspasoDto(
                cantidadEncontrada = cantidadEncontrada
            )
            
            Log.d("OrdenTraspasoLogic", "📤 [AJUSTE] DTO creado:")
            Log.d("OrdenTraspasoLogic", "   - idLinea: $idLinea")
            Log.d("OrdenTraspasoLogic", "   - cantidadEncontrada: ${dto.cantidadEncontrada}")
            Log.d("OrdenTraspasoLogic", "   - DTO completo: $dto")
            
            Log.d("OrdenTraspasoLogic", "🚀 [AJUSTE] Enviando petición al servidor...")
            val response = withContext(Dispatchers.IO) {
                apiService.ajustarLineaOrdenTraspaso(idLinea, dto)
            }
            
            Log.d("OrdenTraspasoLogic", "📡 [AJUSTE] Respuesta recibida - Código: ${response.code()}, Exitoso: ${response.isSuccessful}")
            
            if (response.isSuccessful) {
                val responseBody = response.body()
                if (responseBody != null) {
                    Log.d("OrdenTraspasoLogic", "✅ [AJUSTE] Ajuste procesado correctamente: success=${responseBody.success}, mensaje=${responseBody.mensaje}, requiereSupervision=${responseBody.requiereSupervision}")
                    Result.success(responseBody)
                } else {
                    Log.e("OrdenTraspasoLogic", "❌ [AJUSTE] Respuesta vacía del servidor")
                    Result.failure(Exception("Respuesta vacía del servidor"))
                }
            } else {
                val errorBody = response.errorBody()?.string()
                Log.e("OrdenTraspasoLogic", "❌ [AJUSTE] Error HTTP al ajustar línea ${response.code()}: $errorBody")
                
                // Si es un HTTP 400, intentar parsear el errorBody como AjusteLineaResponseDto
                if (response.code() == 400 && errorBody != null) {
                    try {
                        val gson = com.google.gson.Gson()
                        val ajusteResponse = gson.fromJson(errorBody, AjusteLineaResponseDto::class.java)
                        Log.d("OrdenTraspasoLogic", "🔄 [AJUSTE] Parseado errorBody como AjusteLineaResponseDto: success=${ajusteResponse.success}, mensaje=${ajusteResponse.mensaje}")
                        Result.success(ajusteResponse)
                    } catch (e: Exception) {
                        Log.e("OrdenTraspasoLogic", "❌ [AJUSTE] Error al parsear errorBody: ${e.message}")
                        Result.failure(Exception("Error ${response.code()}: $errorBody"))
                    }
                } else {
                    Result.failure(Exception("Error ${response.code()}: $errorBody"))
                }
            }
        } catch (e: Exception) {
            Log.e("OrdenTraspasoLogic", "💥 [AJUSTE] Excepción al ajustar línea", e)
            Result.failure(e)
        }
    }
    
    /**
     * Actualizar línea de orden con el ID del traspaso
     */
    fun actualizarLineaConTraspaso(
        idLinea: String,
        idTraspaso: String,
        onSuccess: () -> Unit,
        onError: (String) -> Unit
    ) {
        android.util.Log.d("ACTUALIZAR_LINEA_TRASPASO", "📝 Actualizando línea '$idLinea' con traspaso '$idTraspaso'")
        
        val dto = ActualizarIdTraspasoDto(idTraspaso = idTraspaso)
        
        CoroutineScope(Dispatchers.IO).launch {
            try {
                android.util.Log.d("ACTUALIZAR_LINEA_TRASPASO", "📤 Llamando API actualizarIdTraspaso...")
                val response = apiService.actualizarIdTraspaso(idLinea, dto)
                
                if (response.isSuccessful) {
                    android.util.Log.d("ACTUALIZAR_LINEA_TRASPASO", "✅ Línea actualizada correctamente")
                    withContext(Dispatchers.Main) {
                        onSuccess()
                    }
                } else {
                    val error = response.errorBody()?.string() ?: "Error ${response.code()}"
                    android.util.Log.e("ACTUALIZAR_LINEA_TRASPASO", "❌ Error ${response.code()}: $error")
                    withContext(Dispatchers.Main) {
                        onError("Error al actualizar línea: $error")
                    }
                }
            } catch (e: Exception) {
                android.util.Log.e("ACTUALIZAR_LINEA_TRASPASO", "❌ Excepción: ${e.message}")
                withContext(Dispatchers.Main) {
                    onError("Error de red: ${e.message}")
                }
            }
        }
    }
}
