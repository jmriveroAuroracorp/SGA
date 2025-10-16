package com.example.sga.view.conteos

import android.util.Log
import com.example.sga.data.ApiManager
import com.example.sga.data.dto.conteos.*
import com.example.sga.data.mapper.ConteosMapper
import com.example.sga.data.model.conteos.*
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import retrofit2.Call
import retrofit2.Callback
import retrofit2.Response
import retrofit2.HttpException

class ConteoLogic(
    private val conteoViewModel: ConteoViewModel
) {

    private val apiService = ApiManager.conteosApi
    private val scope = CoroutineScope(Dispatchers.Main)

    private fun String?.clean(): String? =
        this?.trim()?.takeIf { it.isNotEmpty() }

    // Listar órdenes de conteo
    fun listarOrdenes(user: com.example.sga.data.model.user.User? = null) {
        conteoViewModel.setCargando(true)
        conteoViewModel.limpiarMensajes()
        
        // Establecer el usuario en el ViewModel
        user?.let { conteoViewModel.setUser(it) }

        scope.launch {
            try {
                Log.d("ConteoLogic", "📡 Iniciando carga de órdenes...")
                Log.d("ConteoLogic", "👤 CodigoOperario: ${user?.id}")
                
                val response = withContext(Dispatchers.IO) {
                    apiService.listarOrdenes(user?.id)
                }
                
                Log.d("ConteoLogic", "✅ Respuesta recibida: ${response.size} órdenes")
                val ordenes = response.map { ConteosMapper.fromOrdenConteoDto(it) }
                Log.d("ConteoLogic", "🔄 Mapeadas ${ordenes.size} órdenes")
                
                conteoViewModel.setOrdenes(ordenes)
                
                // Calcular conteos activos si tenemos usuario
                user?.let { usuario ->
                    val conteosActivos = ordenes.count { orden ->
                        orden.codigoOperario == usuario.id && 
                        (orden.estado == "ASIGNADO" || orden.estado == "EN_PROCESO")
                    }
                    conteoViewModel.setConteosActivos(conteosActivos)
                    Log.d("ConteoLogic", "🎯 Conteos activos calculados: $conteosActivos")
                }
                
                conteoViewModel.setError(null)
                
            } catch (e: Exception) {
                Log.e("ConteoLogic", "❌ Error al listar órdenes: ${e.message}")
                Log.e("ConteoLogic", "   - Tipo: ${e.javaClass.simpleName}")
                Log.e("ConteoLogic", "   - Stack trace: ${e.stackTraceToString()}")
                conteoViewModel.setError("Error al cargar órdenes: ${e.message}")
            } finally {
                conteoViewModel.setCargando(false)
            }
        }
    }

    // Crear nueva orden de conteo
    fun crearOrden(
        titulo: String,
        visibilidad: String,
        modoGeneracion: String,
        alcance: String,
        filtrosJson: String?,
        creadoPorCodigo: String,
        codigoOperario: String?
    ) {
        conteoViewModel.setCargando(true)
        conteoViewModel.limpiarMensajes()

        val crearOrdenDto = CrearOrdenDto(
            titulo = titulo,
            visibilidad = visibilidad,
            modoGeneracion = modoGeneracion,
            alcance = alcance,
            filtrosJson = filtrosJson,
            creadoPorCodigo = creadoPorCodigo,
            codigoOperario = codigoOperario
        )

        scope.launch {
            try {
                val response = withContext(Dispatchers.IO) {
                    apiService.crearOrden(crearOrdenDto)
                }
                
                val orden = ConteosMapper.fromOrdenConteoDto(response)
                conteoViewModel.setMensaje("Orden creada exitosamente")
                conteoViewModel.setMostrarDialogoCrearOrden(false)
                listarOrdenes(conteoViewModel.user.value) // Recargar lista
                
            } catch (e: Exception) {
                Log.e("ConteoLogic", "Error al crear orden", e)
                conteoViewModel.setError("Error al crear orden: ${e.message}")
            } finally {
                conteoViewModel.setCargando(false)
            }
        }
    }

    // Obtener orden específica
    fun obtenerOrden(guidID: String) {
        conteoViewModel.setCargando(true)
        conteoViewModel.limpiarMensajes()

        scope.launch {
            try {
                val response = withContext(Dispatchers.IO) {
                    apiService.obtenerOrden(guidID)
                }
                
                val orden = ConteosMapper.fromOrdenConteoDto(response)
                conteoViewModel.setOrdenSeleccionada(orden)
                conteoViewModel.setError(null)
                
            } catch (e: Exception) {
                Log.e("ConteoLogic", "Error al obtener orden", e)
                conteoViewModel.setError("Error al cargar orden: ${e.message}")
            } finally {
                conteoViewModel.setCargando(false)
            }
        }
    }

    // Iniciar orden
    fun iniciarOrden(guidID: String, codigoOperario: String, onSuccess: () -> Unit, onError: (String) -> Unit) {
        conteoViewModel.setCargando(true)
        conteoViewModel.limpiarMensajes()

        scope.launch {
            try {
                val response = withContext(Dispatchers.IO) {
                    apiService.iniciarOrden(guidID, codigoOperario)
                }
                
                Log.d("ConteoLogic", "✅ Orden iniciada exitosamente: $guidID")
                conteoViewModel.setMensaje("Orden iniciada exitosamente")
                onSuccess()
                
            } catch (e: Exception) {
                Log.e("ConteoLogic", "Error al iniciar orden", e)
                val errorMsg = "Error al iniciar orden: ${e.message}"
                conteoViewModel.setError(errorMsg)
                onError(errorMsg)
            } finally {
                conteoViewModel.setCargando(false)
            }
        }
    }

    // Asignar operario
    fun asignarOperario(guidID: String, codigoOperario: String, comentario: String?) {
        conteoViewModel.setCargando(true)
        conteoViewModel.limpiarMensajes()

        val asignarOperarioDto = AsignarOperarioDto(
            codigoOperario = codigoOperario,
            comentario = comentario
        )

        scope.launch {
            try {
                val response = withContext(Dispatchers.IO) {
                    apiService.asignarOperario(guidID, asignarOperarioDto)
                }
                
                val orden = ConteosMapper.fromOrdenConteoDto(response)
                conteoViewModel.setOrdenSeleccionada(orden)
                conteoViewModel.setMensaje("Operario asignado exitosamente")
                conteoViewModel.setMostrarDialogoAsignarOperario(false)
                
            } catch (e: Exception) {
                Log.e("ConteoLogic", "Error al asignar operario", e)
                conteoViewModel.setError("Error al asignar operario: ${e.message}")
            } finally {
                conteoViewModel.setCargando(false)
            }
        }
    }

    // Obtener lecturas pendientes
    fun obtenerLecturasPendientes(guidID: String, codigoOperario: String? = null) {
        conteoViewModel.setCargando(true)
        conteoViewModel.limpiarMensajes()

        scope.launch {
            try {
                val response = withContext(Dispatchers.IO) {
                    apiService.obtenerLecturasPendientes(guidID, codigoOperario)
                }
                
                                 // Convertir LecturaConteo a LecturaPendiente para el nuevo flujo
                 val lecturasPendientes = response.map { lecturaDto ->
                     val lectura = ConteosMapper.fromLecturaConteoDto(lecturaDto)
                     LecturaPendiente(
                         codigoAlmacen = lectura.codigoAlmacen,
                         codigoUbicacion = lectura.codigoUbicacion,
                         codigoArticulo = lectura.codigoArticulo,
                         descripcionArticulo = lectura.descripcionArticulo,
                         lotePartida = lectura.lotePartida,
                         cantidadStock = lectura.cantidadStock,
                         cantidadTeorica = null, // No tenemos este campo en el modelo actual
                         cantidadContada = lectura.cantidadContada,
                         fechaCaducidad = lectura.fechaCaducidad
                     )
                 }
                
                                 Log.d("ConteoLogic", "📋 Lecturas pendientes obtenidas: ${lecturasPendientes.size}")
                 
                 // Log detallado de cada lectura pendiente
                 lecturasPendientes.forEachIndexed { index, lectura ->
                     Log.d("ConteoLogic", "📝 Lectura ${index + 1}:")
                     Log.d("ConteoLogic", "   - Almacén: ${lectura.codigoAlmacen}")
                     Log.d("ConteoLogic", "   - Ubicación: '${lectura.codigoUbicacion}'")
                     Log.d("ConteoLogic", "   - Artículo: ${lectura.codigoArticulo}")
                     Log.d("ConteoLogic", "   - Descripción: ${lectura.descripcionArticulo}")
                     Log.d("ConteoLogic", "   - Lote: ${lectura.lotePartida}")
                     Log.d("ConteoLogic", "   - Stock: ${lectura.cantidadStock}")
                     Log.d("ConteoLogic", "   - Contada: ${lectura.cantidadContada}")
                     Log.d("ConteoLogic", "   - Fecha Caducidad: ${lectura.fechaCaducidad}")
                 }
                 
                 conteoViewModel.setLecturasPendientes(lecturasPendientes)
                conteoViewModel.setError(null)
                
            } catch (e: Exception) {
                Log.e("ConteoLogic", "Error al obtener lecturas", e)
                conteoViewModel.setError("Error al cargar lecturas: ${e.message}")
            } finally {
                conteoViewModel.setCargando(false)
            }
        }
    }

    // Registrar lectura
    fun registrarLectura(
        ordenGuid: String,
        codigoUbicacion: String,
        codigoArticulo: String,
        descripcionArticulo: String?,
        lotePartida: String?,
        cantidadContada: Double,
        usuarioCodigo: String,
        comentario: String?,
        fechaCaducidad: String? = null,
        paletId: String? = null,
        codigoPalet: String? = null,
        codigoGS1: String? = null
    ) {
        conteoViewModel.setCargando(true)
        conteoViewModel.limpiarMensajes()

        // Extraer el código de almacén y normalizar la ubicación
        val (codigoAlmacen, ubicacionNormalizada) = if (codigoUbicacion.contains('$')) {
            val parts = codigoUbicacion.split('$', limit = 2)
            if (parts.size == 2 && parts[0].isNotBlank()) {
                val alm = parts[0].trim()  // Primera parte es el almacén (ej: "PR")
                val ubi = parts[1].trim()  // Segunda parte es la ubicación (puede estar vacía)
                alm to ubi  // Retorna (almacén, ubicación)
            } else {
                "" to codigoUbicacion  // Si no hay almacén en la ubicación
            }
        } else {
            "" to codigoUbicacion  // Si no hay formato ALM$UB
        }

        val lecturaDto = LecturaDto(
            codigoArticulo = codigoArticulo,
            codigoUbicacion = ubicacionNormalizada,  // ← usar ubicación normalizada (sin almacén)
            codigoAlmacen = codigoAlmacen,  // ← nuevo campo
            lotePartida = lotePartida ?: "",
            cantidadContada = cantidadContada,
            usuarioCodigo = usuarioCodigo,
            comentario = comentario,
            ordenGuid = ordenGuid,
            fechaCaducidad = fechaCaducidad,
            paletId = paletId,
            codigoPalet = codigoPalet,
            codigoGS1 = codigoGS1
        )
        
        scope.launch {
            try {
                Log.d("ConteoLogic", "📡 Llamando API: POST /api/conteos/$ordenGuid/lecturas")
                Log.d("ConteoLogic", "📤 DTO enviado:")
                Log.d("ConteoLogic", "   - codigoArticulo: '${lecturaDto.codigoArticulo}'")
                Log.d("ConteoLogic", "   - codigoUbicacion: '${lecturaDto.codigoUbicacion}'")
                Log.d("ConteoLogic", "   - codigoAlmacen: '${lecturaDto.codigoAlmacen}'")  // ← nuevo log
                Log.d("ConteoLogic", "   - lotePartida: '${lecturaDto.lotePartida}'")
                Log.d("ConteoLogic", "   - cantidadContada: ${lecturaDto.cantidadContada}")
                Log.d("ConteoLogic", "   - fechaCaducidad: '${lecturaDto.fechaCaducidad}'")
                Log.d("ConteoLogic", "   - usuarioCodigo: '${lecturaDto.usuarioCodigo}'")
                Log.d("ConteoLogic", "   - comentario: '${lecturaDto.comentario}'")
                Log.d("ConteoLogic", "   - paletId: '${lecturaDto.paletId}'")
                Log.d("ConteoLogic", "   - codigoPalet: '${lecturaDto.codigoPalet}'")
                Log.d("ConteoLogic", "   - codigoGS1: '${lecturaDto.codigoGS1}'")
                
                val response = withContext(Dispatchers.IO) {
                    apiService.registrarLectura(ordenGuid, lecturaDto)
                }
                
                val lectura = ConteosMapper.fromLecturaConteoDto(response)
                conteoViewModel.setMensaje("Lectura registrada exitosamente")
                conteoViewModel.limpiarFormulario()
                
                // Recargar lecturas pendientes SOLO si el registro fue exitoso
                obtenerLecturasPendientes(ordenGuid, usuarioCodigo)
                
                // Verificar si no hay más lecturas pendientes después de recargar
                scope.launch {
                    delay(500) // Pequeño delay para asegurar que se han recargado las lecturas
                    val lecturasActuales = conteoViewModel.lecturasPendientes.value
                    if (lecturasActuales.isEmpty()) {
                        conteoViewModel.setMensaje("¡Conteo completado! No hay más lecturas pendientes.")
                        // Indicar que debe volver a la pantalla de conteos
                        conteoViewModel.setConteoCompletado(true)
                    }
                }
                
            } catch (e: Exception) {
                Log.e("ConteoLogic", "❌ Error al registrar lectura", e)
                Log.e("ConteoLogic", "❌ Error message: ${e.message}")
                Log.e("ConteoLogic", "❌ Error cause: ${e.cause}")
                
                // Si es un error HTTP, intentar obtener más detalles
                if (e is retrofit2.HttpException) {
                    try {
                        val errorBody = e.response()?.errorBody()?.string()
                        Log.e("ConteoLogic", "❌ HTTP Error Body: $errorBody")
                        conteoViewModel.setError("Error ${e.code()}: ${errorBody ?: e.message()}")
                    } catch (ex: Exception) {
                        Log.e("ConteoLogic", "❌ Error al leer error body", ex)
                        conteoViewModel.setError("Error ${e.code()}: ${e.message()}")
                    }
                } else {
                    conteoViewModel.setError("Error al registrar lectura: ${e.message}")
                }
            } finally {
                conteoViewModel.setCargando(false)
            }
        }
    }

    // Cerrar orden
    fun cerrarOrden(guidID: String) {
        conteoViewModel.setCargando(true)
        conteoViewModel.limpiarMensajes()

        scope.launch {
            try {
                val response = withContext(Dispatchers.IO) {
                    apiService.cerrarOrden(guidID)
                }
                
                val respuesta = ConteosMapper.fromCerrarOrdenResponseDto(response)
                conteoViewModel.setMensaje("Orden cerrada. ${respuesta.resultadosCreados} resultados generados.")
                // Recargar orden para actualizar estado
                obtenerOrden(guidID)
                
            } catch (e: Exception) {
                Log.e("ConteoLogic", "Error al cerrar orden", e)
                conteoViewModel.setError("Error al cerrar orden: ${e.message}")
            } finally {
                conteoViewModel.setCargando(false)
            }
        }
    }

    // Obtener resultados
    fun obtenerResultados(guidID: String) {
        conteoViewModel.setCargando(true)
        conteoViewModel.limpiarMensajes()

        scope.launch {
            try {
                val response = withContext(Dispatchers.IO) {
                    apiService.obtenerResultados(guidID)
                }
                
                val resultados = response.map { ConteosMapper.fromResultadoConteoDto(it) }
                conteoViewModel.setResultados(resultados)
                conteoViewModel.setError(null)
                
            } catch (e: Exception) {
                Log.e("ConteoLogic", "Error al obtener resultados", e)
                conteoViewModel.setError("Error al cargar resultados: ${e.message}")
            } finally {
                conteoViewModel.setCargando(false)
            }
        }
    }

    // Verificar si el usuario tiene conteos activos asignados
    fun verificarConteosActivos(codigoOperario: String, onResult: (Int) -> Unit) {
        Log.d("ConteoLogic", "🔍 Verificando conteos activos para operario: $codigoOperario")
        
        scope.launch {
            try {
                val response = withContext(Dispatchers.IO) {
                    apiService.listarOrdenes(codigoOperario)
                }
                
                Log.d("ConteoLogic", "✅ Órdenes obtenidas: ${response.size}")
                val ordenes = response.map { ConteosMapper.fromOrdenConteoDto(it) }
                
                // Contar órdenes activas de forma optimizada
                val conteosActivos = ordenes.count { orden ->
                    orden.codigoOperario == codigoOperario && 
                    (orden.estado == "ASIGNADO" || orden.estado == "EN_PROCESO")
                }
                
                Log.d("ConteoLogic", "🎯 Conteos activos: $conteosActivos")
                
                // Almacenar las órdenes en el ViewModel para reutilizarlas
                conteoViewModel.setOrdenes(ordenes)
                conteoViewModel.setConteosActivos(conteosActivos)
                
                onResult(conteosActivos)
                
            } catch (e: Exception) {
                Log.e("ConteoLogic", "❌ Error: ${e.message}")
                onResult(0)
            }
        }
    }

    // Procesar código escaneado para conteos (reutilizando lógica de TraspasosLogic)
    fun procesarCodigoEscaneado(
        code: String,
        ordenGuid: String,
        codigoOperario: String,
        modoManual: Boolean = false,
        onUbicacionDetectada: (String) -> Unit,
        onArticuloDetectado: (LecturaPendiente) -> Unit,
        onMultipleArticulos: (List<com.example.sga.data.dto.stock.ArticuloDto>) -> Unit,
        onError: (String) -> Unit,
        onArticuloManual: (String, String, String, String?, String?) -> Unit = { _, _, _, _, _ -> } // codAlm, codUbi, codArt, partida, fechaCaducidad
    ) {
        Log.d("ConteoLogic", "📥 Código recibido: $code")
        val trimmed = code.trim()

        // Obtener empresaId del usuario actual
        val empresaId = conteoViewModel.user.value?.empresas?.firstOrNull()?.codigo?.toShort() ?: 1.toShort()

        // Regex para detectar códigos GS1 (EAN13 + AI) - mismo que TraspasosLogic
        val gtinRegex = Regex("""^01(?:0(\d{13})|(\d{14}))""")
        
        // Regex para detectar SSCC (palets) - mismo que TraspasosLogic
        val ssccRegex = Regex("""^00(\d{18})""")

                 // 0) UBICACIÓN incompleta tipo "ALM$" (solo almacén)
         val almSoloRegex = Regex("""^([^$]+)\$$""")
         almSoloRegex.matchEntire(trimmed)?.let { m ->
             val codAlm = m.groupValues[1].trim()
             Log.d("ConteoLogic", "🏬 Almacén detectado sin ubicación: $codAlm")
             
             if (modoManual) {
                 onUbicacionDetectada("$codAlm$")   // ← ubicación vacía; la UI puede pedirla después
             } else {
                 // Validar que hay lecturas pendientes en ese almacén con ubicación vacía
                 val lecturasPendientes = conteoViewModel.lecturasPendientes.value
                 val ubicacionValida = lecturasPendientes.any { lectura ->
                     lectura.codigoAlmacen == codAlm && lectura.codigoUbicacion.isEmpty()
                 }
                 
                 if (ubicacionValida) {
                     onUbicacionDetectada("$codAlm$")   // ← ubicación vacía; la UI puede pedirla después
                 } else {
                     onError("No hay lecturas pendientes en el almacén $codAlm sin ubicación específica")
                 }
             }
             return
         }

        // 1) UBICACIÓN explícita CODALM$UBIC
        val ubicRegex = Regex("""^([^$]+)\$([^$]+)$""")   // ej. 201$UB001
        ubicRegex.matchEntire(trimmed)?.let { m ->
            val codAlm  = m.groupValues[1].trim()
            val codUbi  = m.groupValues[2].trim()
            Log.d("ConteoLogic", "📍 Ubicación detectada: $codAlm – $codUbi")
            
            if (modoManual) {
                onUbicacionDetectada("$codAlm$$codUbi")
            } else {
                // Validar que la ubicación existe en las lecturas pendientes de la orden
                val lecturasPendientes = conteoViewModel.lecturasPendientes.value
                val ubicacionValida = lecturasPendientes.any { lectura ->
                    lectura.codigoAlmacen == codAlm && lectura.codigoUbicacion == codUbi
                }
                
                if (ubicacionValida) {
                    onUbicacionDetectada("$codAlm$$codUbi")
                } else {
                    onError("La ubicación $codAlm-$codUbi no está en las lecturas pendientes de esta orden")
                }
            }
            return
        }

        // 2) Códigos GS1 (EAN13 + AI) - misma lógica que TraspasosLogic
        gtinRegex.find(code)?.let { m ->
            val ean13 = m.groupValues.drop(1).first { it.isNotEmpty() }.takeLast(13)
            Log.d("ConteoLogic", "🛒 EAN13 detectado: $ean13")

            val ai10Index = code.indexOf("10", startIndex = 16)
            val partida = if (ai10Index != -1 && ai10Index + 2 < code.length) {
                code.substring(ai10Index + 2)
            } else null

            // Extraer fecha de caducidad (AI 15, formato AAMMDD)
            val ai15Index = code.indexOf("15", startIndex = 16)
            val fechaCaducidad = if (ai15Index != -1 && ai15Index + 8 <= code.length) {
                val raw = code.substring(ai15Index + 2, ai15Index + 8)
                if (raw.length == 6) {
                    val aa = raw.substring(0, 2)
                    val mm = raw.substring(2, 4)
                    val dd = raw.substring(4, 6)
                    "20$aa-$mm-$dd" // <-- FORMATO CORRECTO PARA .NET
                } else null
            } else null

            Log.d("ConteoLogic", "🔖 Partida extraída: $partida")
            Log.d("ConteoLogic", "📅 Fecha caducidad extraída: $fechaCaducidad")

            // Verificar que se haya escaneado una ubicación primero
            val ubicacionEscaneada = conteoViewModel.ubicacionEscaneada.value
            if (ubicacionEscaneada == null) {
                onError("Debe escanear primero una ubicación")
                return
            }

            val ubicacionParts = ubicacionEscaneada.split("$")
            if (ubicacionParts.size != 2) {
                onError("Formato de ubicación inválido")
                return
            }
            val codAlm = ubicacionParts[0]
            val codUbi = ubicacionParts[1]

            // Usar la API de etiquetas para convertir EAN13 a código de artículo (igual que en traspasos)
            ApiManager.etiquetasApiService.buscarArticulo(
                codigoEmpresa = empresaId,
                codigoAlternativo = ean13,
                codigoAlmacen = codAlm,
                codigoCentro = null,
                almacen = codAlm,
                partida = partida
            ).enqueue(object : Callback<List<com.example.sga.data.dto.stock.ArticuloDto>> {
                override fun onResponse(
                    call: Call<List<com.example.sga.data.dto.stock.ArticuloDto>>,
                    response: Response<List<com.example.sga.data.dto.stock.ArticuloDto>>
                ) {
                    if (response.isSuccessful) {
                        val lista = response.body().orEmpty()
                        Log.d("ConteoLogic", "🎯 Artículos encontrados: ${lista.size}")

                        // Asegura que todos los DTO llevan la partida y fecha de caducidad extraídas si no la tienen
                        val listaConDatos = lista.map {
                            it.copy(
                                partida = it.partida ?: partida,
                                fechaCaducidad = it.fechaCaducidad ?: fechaCaducidad
                            )
                        }

                        // Filtrar por partida si existe (misma lógica que TraspasosLogic)
                        val listaFiltrada = partida?.let { p ->
                            val pNorm = p.filter { it.isDigit() }
                            listaConDatos.filter { art ->
                                val artNorm = art.partida?.filter { it.isDigit() }
                                artNorm == pNorm
                            }
                        } ?: listaConDatos

                        val candidatos = listaFiltrada.distinctBy {
                            "${it.codigoArticulo}-${it.descripcion}-${it.partida}"
                        }

                        when (candidatos.size) {
                            0 -> onError("No se encontró ningún artículo con ese código y partida.")
                            1 -> {
                                val articuloDto = candidatos.first()
                                Log.d("ConteoLogic", "✅ Artículo encontrado: ${articuloDto.codigoArticulo}")
                                
                                if (modoManual) {
                                    // En modo manual, ir directo al diálogo de cantidad
                                    // Pasar también la fecha de caducidad extraída del código
                                    onArticuloManual(codAlm, codUbi, articuloDto.codigoArticulo, articuloDto.partida, fechaCaducidad)
                                } else {
                                    // Buscar el artículo en las lecturas pendientes usando el código del artículo
                                    val lecturasPendientes = conteoViewModel.lecturasPendientes.value
                                    val articuloEncontrado = lecturasPendientes.find { lectura ->
                                        lectura.codigoAlmacen == codAlm && 
                                        lectura.codigoUbicacion == codUbi &&
                                        lectura.codigoArticulo == articuloDto.codigoArticulo
                                    }
                                    
                                    if (articuloEncontrado != null) {
                                        // Crear una copia del artículo con la fecha de caducidad extraída del código
                                        val articuloConFechaCaducidad = articuloEncontrado.copy(
                                            fechaCaducidad = fechaCaducidad
                                        )
                                        onArticuloDetectado(articuloConFechaCaducidad)
                                    } else {
                                        // Buscar si el artículo existe en otra ubicación
                                        val articuloEnOtraUbicacion = lecturasPendientes.find { lectura ->
                                            lectura.codigoArticulo == articuloDto.codigoArticulo
                                        }
                                        
                                        if (articuloEnOtraUbicacion != null) {
                                            onError("El artículo ${articuloDto.codigoArticulo} está en la ubicación ${articuloEnOtraUbicacion.codigoAlmacen}-${articuloEnOtraUbicacion.codigoUbicacion}, no en $codAlm-$codUbi")
                                        } else {
                                            onError("El artículo ${articuloDto.codigoArticulo} no está en las lecturas pendientes de esta orden")
                                        }
                                    }
                                }
                            }
                            else -> {
                                // Múltiples artículos encontrados - mostrar diálogo de selección
                                Log.d("ConteoLogic", "🔍 Múltiples artículos encontrados: ${candidatos.size}")
                                onMultipleArticulos(candidatos)
                            }
                        }
                    } else {
                        onError("Error HTTP ${response.code()}")
                    }
                }

                override fun onFailure(call: Call<List<com.example.sga.data.dto.stock.ArticuloDto>>, t: Throwable) {
                    onError("Error de red: ${t.message}")
                }
            })
            return
        }

        // 3) SSCC (palets) - para conteos, verificar si estamos esperando un palet
        ssccRegex.find(code)?.let { m ->
            val gs1 = m.groupValues[1]
            Log.d("ConteoLogic", "📦 SSCC detectado: $gs1")
            
            // Verificar si estamos en estado de esperar palet
            val estadoActual = conteoViewModel.estadoEscaneo.value
            if (estadoActual == EstadoEscaneoConteo.EsperandoPalet) {
                // Buscar el palet por GS1 en la lista de disponibles
                val paletsDisponibles = conteoViewModel.paletsDisponibles.value
                val paletEncontrado = paletsDisponibles.find { it.codigoGS1 == gs1 }
                
                if (paletEncontrado != null) {
                    // Palet válido, continuar con el conteo
                    Log.d("ConteoLogic", "✅ Palet válido encontrado: ${paletEncontrado.codigoPalet}")
                    
                    // Buscar el artículo correspondiente a este palet
                    val ubicacionEscaneada = conteoViewModel.ubicacionEscaneada.value
                    val articuloEscaneado = conteoViewModel.articuloEscaneado.value
                    
                    if (ubicacionEscaneada != null && articuloEscaneado != null) {
                        // Crear un artículo con la cantidad específica del palet seleccionado
                        val articuloConCantidadPalet = articuloEscaneado.copy(
                            cantidadStock = paletEncontrado.cantidad, // Cantidad específica del palet
                            paletId = paletEncontrado.paletId,
                            codigoPalet = paletEncontrado.codigoPalet,
                            codigoGS1 = paletEncontrado.codigoGS1
                        )
                        
                        // Actualizar el artículo escaneado con la información del palet
                        conteoViewModel.setArticuloEscaneado(articuloConCantidadPalet)
                    }
                    
                    conteoViewModel.setPaletSeleccionado(paletEncontrado)
                    conteoViewModel.setEstadoEscaneo(EstadoEscaneoConteo.EsperandoCantidad)
                    conteoViewModel.setMensaje("Palet ${paletEncontrado.codigoPalet} confirmado. Introduzca la cantidad.")
                } else {
                    // Palet no válido
                    Log.e("ConteoLogic", "❌ Palet no válido: $gs1")
                    onError("El palet con GS1 $gs1 no está disponible en esta ubicación para este artículo.")
                }
            } else {
                onError("Los códigos SSCC (palets) no son válidos para conteos en este momento")
            }
            return
        }

        // 4) Búsqueda por código de artículo directo (si no es ubicación, EAN, ni SSCC)
        val codArt = trimmed.takeIf { it.length in 4..25 && it.all { ch -> ch.isLetterOrDigit() } }
        if (codArt != null) {
            Log.d("ConteoLogic", "🔍 Posible código de artículo: $codArt")
            
            // Verificar que se haya escaneado una ubicación primero
            val ubicacionEscaneada = conteoViewModel.ubicacionEscaneada.value
            if (ubicacionEscaneada == null) {
                onError("Debe escanear primero una ubicación")
                return
            }
            
            val ubicacionParts = ubicacionEscaneada.split("$")
            if (ubicacionParts.size != 2) {
                onError("Formato de ubicación inválido")
                return
            }
            val codAlm = ubicacionParts[0]
            val codUbi = ubicacionParts[1]
            
            if (modoManual) {
                // En modo manual, ir directo al diálogo de cantidad
                onArticuloManual(codAlm, codUbi, codArt, null, null) // fechaCaducidad = null para códigos de artículo simples
            } else {
                // Buscar el artículo en las lecturas pendientes
                val lecturasPendientes = conteoViewModel.lecturasPendientes.value
                val articulosEncontrados = lecturasPendientes.filter { lectura ->
                    lectura.codigoAlmacen == codAlm && 
                    lectura.codigoUbicacion == codUbi &&
                    lectura.codigoArticulo == codArt
                }
                
                if (articulosEncontrados.isNotEmpty()) {
                    // Verificar si hay múltiples palets para este artículo
                    val paletsConInfo = articulosEncontrados.filter { it.paletId != null }
                    val articulosSinPalet = articulosEncontrados.filter { it.paletId == null }
                    
                    when {
                        // Caso 1: Solo hay un palet
                        paletsConInfo.size == 1 -> {
                            val articulo = paletsConInfo.first()
                            Log.d("ConteoLogic", "✅ Un palet encontrado: ${articulo.codigoPalet}")
                            
                            // Crear un artículo con la cantidad específica del palet
                            val articuloConCantidadPalet = articulo.copy(
                                cantidadStock = articulo.cantidadStock ?: 0.0 // Usar la cantidad específica del palet
                            )
                            
                            conteoViewModel.setPaletSeleccionado(PaletDisponible(
                                paletId = articulo.paletId!!,
                                codigoPalet = articulo.codigoPalet ?: "",
                                codigoGS1 = articulo.codigoGS1 ?: "",
                                cantidad = articulo.cantidadStock ?: 0.0, // Cantidad específica del palet
                                estado = ""
                            ))
                            onArticuloDetectado(articuloConCantidadPalet)
                        }
                        
                        // Caso 2: Múltiples palets
                        paletsConInfo.size > 1 -> {
                            Log.d("ConteoLogic", "📦 Múltiples palets encontrados: ${paletsConInfo.size}")
                            val paletsDisponibles = paletsConInfo.map { lectura ->
                                PaletDisponible(
                                    paletId = lectura.paletId!!,
                                    codigoPalet = lectura.codigoPalet ?: "",
                                    codigoGS1 = lectura.codigoGS1 ?: "",
                                    cantidad = lectura.cantidadStock ?: 0.0, // Cantidad específica de cada palet
                                    estado = ""
                                )
                            }
                            conteoViewModel.setPaletsDisponibles(paletsDisponibles)
                            conteoViewModel.setEstadoEscaneo(EstadoEscaneoConteo.EsperandoPalet)
                            conteoViewModel.setMensaje("Múltiples palets encontrados. Escanee la etiqueta GS1 del palet específico.")
                        }
                        
                        // Caso 3: Sin palets (conteo normal)
                        articulosSinPalet.isNotEmpty() -> {
                            val articulo = articulosSinPalet.first()
                            Log.d("ConteoLogic", "📦 Sin palets - conteo normal")
                            onArticuloDetectado(articulo)
                        }
                        
                        // Caso 4: No se encontró el artículo
                        else -> {
                            // Buscar si el artículo existe en otra ubicación
                            val articuloEnOtraUbicacion = lecturasPendientes.find { lectura ->
                                lectura.codigoArticulo == codArt
                            }
                            
                            if (articuloEnOtraUbicacion != null) {
                                onError("El artículo $codArt está en la ubicación ${articuloEnOtraUbicacion.codigoAlmacen}-${articuloEnOtraUbicacion.codigoUbicacion}, no en $codAlm-$codUbi")
                            } else {
                                onError("El artículo $codArt no está en las lecturas pendientes de esta orden")
                            }
                        }
                    }
                } else {
                    // Buscar si el artículo existe en otra ubicación
                    val articuloEnOtraUbicacion = lecturasPendientes.find { lectura ->
                        lectura.codigoArticulo == codArt
                    }
                    
                    if (articuloEnOtraUbicacion != null) {
                        onError("El artículo $codArt está en la ubicación ${articuloEnOtraUbicacion.codigoAlmacen}-${articuloEnOtraUbicacion.codigoUbicacion}, no en $codAlm-$codUbi")
                    } else {
                        onError("El artículo $codArt no está en las lecturas pendientes de esta orden")
                    }
                }
            }
            return
        }

        // 5) Si no coincide con ningún patrón
        onError("Código no reconocido: $trimmed")
    }

    // Obtener palets disponibles para una ubicación y artículo
    fun obtenerPaletsDisponibles(
        codigoAlmacen: String,
        ubicacion: String?,
        codigoArticulo: String?,
        lote: String? = null,
        fechaCaducidad: String? = null,
        onSuccess: (List<PaletDisponible>) -> Unit,
        onError: (String) -> Unit
    ) {
        conteoViewModel.setCargando(true)
        conteoViewModel.limpiarMensajes()

        scope.launch {
            try {
                val response = withContext(Dispatchers.IO) {
                    apiService.obtenerPaletsDisponibles(
                        codigoAlmacen = codigoAlmacen,
                        ubicacion = ubicacion,
                        codigoArticulo = codigoArticulo,
                        lote = lote,
                        fechaCaducidad = fechaCaducidad
                    )
                }
                
                val palets = response.map { ConteosMapper.fromPaletDisponibleDto(it) }
                conteoViewModel.setPaletsDisponibles(palets)
                onSuccess(palets)
                
            } catch (e: Exception) {
                Log.e("ConteoLogic", "Error al obtener palets disponibles", e)
                val errorMsg = "Error al obtener palets disponibles: ${e.message}"
                conteoViewModel.setError(errorMsg)
                onError(errorMsg)
            } finally {
                conteoViewModel.setCargando(false)
            }
        }
    }

    // Procesar selección de artículo cuando hay múltiples candidatos
    fun procesarSeleccionArticulo(
        articuloSeleccionado: com.example.sga.data.dto.stock.ArticuloDto,
        onArticuloDetectado: (LecturaPendiente) -> Unit,
        onError: (String) -> Unit
    ) {
        // Verificar que se haya escaneado una ubicación primero
        val ubicacionEscaneada = conteoViewModel.ubicacionEscaneada.value
        if (ubicacionEscaneada == null) {
            onError("Debe escanear primero una ubicación")
            return
        }
        
        val ubicacionParts = ubicacionEscaneada.split("$")
        if (ubicacionParts.size != 2) {
            onError("Formato de ubicación inválido")
            return
        }
        val codAlm = ubicacionParts[0]
        val codUbi = ubicacionParts[1]
        
        // Buscar el artículo en las lecturas pendientes usando el código del artículo
        val lecturasPendientes = conteoViewModel.lecturasPendientes.value
        val articuloEncontrado = lecturasPendientes.find { lectura ->
            lectura.codigoAlmacen == codAlm && 
            lectura.codigoUbicacion == codUbi &&
            lectura.codigoArticulo == articuloSeleccionado.codigoArticulo
        }
        
        if (articuloEncontrado != null) {
            onArticuloDetectado(articuloEncontrado)
        } else {
            // Buscar si el artículo existe en otra ubicación
            val articuloEnOtraUbicacion = lecturasPendientes.find { lectura ->
                lectura.codigoArticulo == articuloSeleccionado.codigoArticulo
            }
            
            if (articuloEnOtraUbicacion != null) {
                onError("El artículo ${articuloSeleccionado.codigoArticulo} está en la ubicación ${articuloEnOtraUbicacion.codigoAlmacen}-${articuloEnOtraUbicacion.codigoUbicacion}, no en $codAlm-$codUbi")
            } else {
                onError("El artículo ${articuloSeleccionado.codigoArticulo} no está en las lecturas pendientes de esta orden")
            }
        }
    }

}
