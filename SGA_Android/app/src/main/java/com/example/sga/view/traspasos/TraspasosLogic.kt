package com.example.sga.view.traspasos

import android.util.Log
import com.example.sga.data.ApiManager
import com.example.sga.data.dto.almacenes.AlmacenDto
import com.example.sga.data.dto.almacenes.AlmacenesAutorizadosDto
import com.example.sga.data.dto.etiquetas.ImpresoraDto
import com.example.sga.data.dto.etiquetas.LogImpresionDto
import com.example.sga.data.dto.login.ConfiguracionUsuarioPatchDto
import com.example.sga.data.dto.stock.ArticuloDto
import com.example.sga.data.dto.stock.StockDisponibleDto
import com.example.sga.data.dto.traspasos.CerrarPaletMobilityDto
import com.example.sga.data.dto.traspasos.CompletarTraspasoDto
import com.example.sga.data.dto.traspasos.LineaPaletCrearDto
import com.example.sga.data.dto.traspasos.LineaPaletDto
import com.example.sga.data.dto.traspasos.PaletCrearDto
import com.example.sga.data.dto.traspasos.PaletDto
import com.example.sga.data.dto.traspasos.TipoPaletDto
import com.example.sga.data.dto.traspasos.CrearTraspasoArticuloDto
import com.example.sga.data.dto.traspasos.TraspasoArticuloDto
import com.example.sga.utils.SoundUtils
import com.example.sga.data.dto.traspasos.FinalizarTraspasoArticuloDto
import com.example.sga.data.dto.traspasos.FinalizarTraspasoPaletDto
import com.example.sga.data.dto.traspasos.MoverPaletDto
import com.example.sga.data.dto.traspasos.MoverPaletResponse
import com.example.sga.data.dto.traspasos.PaletMovibleDto
import com.example.sga.data.dto.traspasos.PrecheckResp
import com.example.sga.data.dto.traspasos.TraspasoCreadoResponse
import com.example.sga.data.dto.traspasos.TraspasoPendienteDto
import com.example.sga.data.dto.traspasos.ValidarUbicacionResponse
import com.example.sga.data.mapper.StockDisponibleMapper
import com.example.sga.data.model.stock.Stock
import com.example.sga.data.model.user.User
import com.example.sga.view.app.SessionViewModel
import retrofit2.Call
import retrofit2.Callback
import retrofit2.Response

class TraspasosLogic {

    fun cargarAlmacenesPermitidos(
        user: User,
        codigoEmpresa: Int,
        onSuccess: (List<AlmacenDto>) -> Unit,
        onError: (String) -> Unit
    ) {
        val dto = AlmacenesAutorizadosDto(
            codigoEmpresa = codigoEmpresa,
            codigoCentro = user.codigoCentro,
            codigosAlmacen = user.codigosAlmacen
        )

        ApiManager.almacenApi
            .obtenerAlmacenesAutorizados(dto)
            .enqueue(object : Callback<List<AlmacenDto>> {
                override fun onResponse(
                    call: Call<List<AlmacenDto>>,
                    response: Response<List<AlmacenDto>>
                ) {
                    if (response.isSuccessful) {
                        onSuccess(response.body().orEmpty())
                    } else {
                        val error = response.errorBody()?.string() ?: "Error desconocido"
                        onError("Error ${response.code()}: $error")
                    }
                }

                override fun onFailure(call: Call<List<AlmacenDto>>, t: Throwable) {
                    onError("Fallo de red: ${t.message}")
                }
            })
    }

    fun obtenerTiposPalet(
        onSuccess: (List<TipoPaletDto>) -> Unit,
        onError: (String) -> Unit
    ) {
        ApiManager.traspasosApi.obtenerTiposPalet()
            .enqueue(object : Callback<List<TipoPaletDto>> {
                override fun onResponse(
                    call: Call<List<TipoPaletDto>>,
                    response: Response<List<TipoPaletDto>>
                ) {
                    if (response.isSuccessful) {
                        onSuccess(response.body().orEmpty())
                    } else {
                        onError("Error ${response.code()}: ${response.errorBody()?.string().orEmpty()}")
                    }
                }

                override fun onFailure(call: Call<List<TipoPaletDto>>, t: Throwable) {
                    onError("Error de red: ${t.message}")
                }
            })
    }

    fun crearPalet(
        dto: PaletCrearDto,
        onSuccess: (PaletDto) -> Unit,
        onError: (String) -> Unit
    ) {
        ApiManager.traspasosApi.crearPalet(dto)
            .enqueue(object : Callback<PaletDto> {
                override fun onResponse(call: Call<PaletDto>, response: Response<PaletDto>) {
                    Log.d("CREAR_PALET", "📡 HTTP ${response.code()}")
                    if (response.isSuccessful) {
                        val body = response.body()
                        if (body != null) {
                            Log.d("CREAR_PALET", "✅ Palet creado: ${body.codigoPalet}")
                            onSuccess(body)
                        } else {
                            Log.e("CREAR_PALET", "⚠️ Respuesta vacía")
                            onError("Error: Respuesta vacía del servidor.")
                        }
                    } else {
                        val error = response.errorBody()?.string().orEmpty()
                        Log.e("CREAR_PALET", "❌ Error HTTP ${response.code()}: $error")
                        onError("Error ${response.code()}: $error")
                    }
                }

                override fun onFailure(call: Call<PaletDto>, t: Throwable) {
                    Log.e("CREAR_PALET", "💥 Fallo de red: ${t.message}")
                    onError("Error de red: ${t.message}")
                }
            })
    }

    fun obtenerPalet(
        idPalet: String,
        onSuccess: (PaletDto) -> Unit,
        onError: (String) -> Unit
    ) {
        ApiManager.traspasosApi.obtenerPalet(idPalet)
            .enqueue(object : Callback<PaletDto> {
                override fun onResponse(call: Call<PaletDto>, response: Response<PaletDto>) {
                    if (response.isSuccessful) {
                        response.body()?.let { 
                            onSuccess(it)
                            SoundUtils.getInstance().playSuccessSound()
                        } ?: {
                            onError("Palet vacío")
                            SoundUtils.getInstance().playErrorSound()
                        }()
                    } else {
                        onError("Error ${response.code()}")
                        SoundUtils.getInstance().playErrorSound()
                    }
                }

                override fun onFailure(call: Call<PaletDto>, t: Throwable) {
                    onError("Error: ${t.message}")
                    SoundUtils.getInstance().playErrorSound()
                }
            })
    }

    fun obtenerLineasPalet(
        idPalet: String,
        onSuccess: (List<LineaPaletDto>) -> Unit,
        onError: (String) -> Unit
    ) {
        ApiManager.traspasosApi.obtenerLineasPalet(idPalet)
            .enqueue(object : Callback<List<LineaPaletDto>> {
                override fun onResponse(
                    call: Call<List<LineaPaletDto>>,
                    response: Response<List<LineaPaletDto>>
                ) {
                    if (response.isSuccessful) {
                        onSuccess(response.body().orEmpty())
                    } else {
                        onError("Error ${response.code()}")
                    }
                }

                override fun onFailure(call: Call<List<LineaPaletDto>>, t: Throwable) {
                    onError("Error: ${t.message}")
                }
            })
    }
    
    fun obtenerPaletPorGS1(
        gs1: String,
        onSuccess: (PaletDto) -> Unit,
        onError: (String) -> Unit
    ) {
        ApiManager.traspasosApi.obtenerPaletPorGS1(gs1)
            .enqueue(object : Callback<PaletDto> {
                override fun onResponse(call: Call<PaletDto>, response: Response<PaletDto>) {
                    if (response.isSuccessful) {
                        val palet = response.body()
                        if (palet != null) {
                            onSuccess(palet)
                        } else {
                            onError("No se encontró ningún palet con ese GS1.")
                            SoundUtils.getInstance().playErrorSound()
                        }
                    } else {
                        onError("Error HTTP al obtener palet: ${response.code()}")
                    }
                }

                override fun onFailure(call: Call<PaletDto>, t: Throwable) {
                    onError("Error de red al obtener palet: ${t.message}")
                }
            })
    }
    
    fun cerrarPalet(
        idPalet: String,
        usuarioId: Int,
        codigoAlmacen: String?,
        codigoEmpresa: Short,
        //ubicacionOrigen: String?, // ✅ añadido como parámetro
        onSuccess: (String) -> Unit,
        onError: (String) -> Unit
    ) {
        val dto = CerrarPaletMobilityDto(
            usuarioId = usuarioId,
            codigoAlmacen = codigoAlmacen,
            codigoEmpresa = codigoEmpresa,
            //ubicacionOrigen = ubicacionOrigen // ✅ se asigna si viene
        )

        ApiManager.traspasosApi.cerrarPalet(idPalet, dto)
            .enqueue(object : Callback<TraspasoCreadoResponse> {
                override fun onResponse(
                    call: Call<TraspasoCreadoResponse>,
                    response: Response<TraspasoCreadoResponse>
                ) {
                    if (response.isSuccessful) {
                        val respuesta = response.body()
                        android.util.Log.d("CERRAR_PALET_API", "✅ Respuesta exitosa del servidor")
                        android.util.Log.d("CERRAR_PALET_API", "📝 Mensaje: ${respuesta?.message}")
                        android.util.Log.d("CERRAR_PALET_API", "📦 Palet ID: ${respuesta?.paletId}")
                        android.util.Log.d("CERRAR_PALET_API", "🔄 Traspasos ID: ${respuesta?.traspasosId}")
                        
                        // Obtener el ID del traspaso creado (no el paletId)
                        val traspasoId = respuesta?.traspasosId?.firstOrNull()

                        if (traspasoId != null) {
                            android.util.Log.d("CERRAR_PALET_API", "✅ Devolviendo Traspaso ID: '$traspasoId'")
                            onSuccess(traspasoId)
                        } else {
                            android.util.Log.e("CERRAR_PALET_API", "❌ No se recibió ID de traspaso")
                            onError("Respuesta inválida del servidor - No se creó el traspaso")
                        }

                    } else {
                        val errorMsg = "Código ${response.code()}: ${response.errorBody()?.string()}"
                        android.util.Log.e("CERRAR_PALET_API", "❌ Error: $errorMsg")
                        onError(errorMsg)

                    }
                }

                override fun onFailure(call: Call<TraspasoCreadoResponse>, t: Throwable) {
                    onError(t.message ?: "Error desconocido")
                }
            })
    }

    fun reabrirPalet(
        idPalet: String,
        usuarioId: Int,
        onSuccess: () -> Unit,
        onError: (String) -> Unit
    ) {
        ApiManager.traspasosApi.reabrirPalet(idPalet, usuarioId)
            .enqueue(object : Callback<Void> {
                override fun onResponse(call: Call<Void>, response: Response<Void>) {
                    if (response.isSuccessful) {
                        onSuccess()
                        SoundUtils.getInstance().playSuccessSound()
                    } else {
                        onError("Código ${response.code()}")
                        SoundUtils.getInstance().playErrorSound()
                    }
                }

                override fun onFailure(call: Call<Void>, t: Throwable) {
                    onError(t.message ?: "Error desconocido")
                    SoundUtils.getInstance().playErrorSound()
                }
            })
    }
    // TraspasosLogic.kt
    fun anadirLineaPalet(
        idPalet: String,
        dto: LineaPaletCrearDto,
        onSuccess: () -> Unit,
        onError: (String) -> Unit
    ) {
        ApiManager.traspasosApi.añadirLineaPalet(idPalet, dto)
            .enqueue(object : Callback<LineaPaletDto> {

                override fun onResponse(
                    call: Call<LineaPaletDto>,
                    response: Response<LineaPaletDto>
                ) {
                    if (response.isSuccessful) {
                        Log.d("LINEA_PALET", "✅ Línea añadida al palet $idPalet")
                        onSuccess()
                        SoundUtils.getInstance().playSuccessSound()
                    } else {
                        val errorBody = response.errorBody()?.string().orEmpty()
                        Log.e("LINEA_PALET", "❌ Error ${response.code()}: $errorBody")
                        onError("Error ${response.code()}: $errorBody")
                        SoundUtils.getInstance().playErrorSound()
                    }
                }

                override fun onFailure(call: Call<LineaPaletDto>, t: Throwable) {
                    Log.e("LINEA_PALET", "💥 Fallo de red: ${t.message}")
                    onError("Error de red: ${t.message}")
                    SoundUtils.getInstance().playErrorSound()
                }
            })
    }
    fun eliminarLineaPalet(
        idLinea: String,
        usuarioId: Int,
        onSuccess: () -> Unit,
        onError: (String) -> Unit
    ) {
        ApiManager.traspasosApi.eliminarLineaPalet(idLinea, usuarioId)
            .enqueue(object : Callback<Void> {
                override fun onResponse(call: Call<Void>, response: Response<Void>) {
                    if (response.isSuccessful) {
                        onSuccess()
                        SoundUtils.getInstance().playSuccessSound()
                    } else {
                        val msg = response.errorBody()?.string() ?: "Error ${response.code()}"
                        onError("No se pudo eliminar la línea: $msg")
                        SoundUtils.getInstance().playErrorSound()
                    }
                }

                override fun onFailure(call: Call<Void>, t: Throwable) {
                    onError("Error de red: ${t.message}")
                    SoundUtils.getInstance().playErrorSound()
                }
            })
    }
    fun cargarImpresoras(
        onResult: (List<ImpresoraDto>) -> Unit,
        onError: (String) -> Unit
    ) {
        ApiManager.etiquetasApiService.getImpresoras()
            .enqueue(object : Callback<List<ImpresoraDto>> {
                override fun onResponse(
                    call: Call<List<ImpresoraDto>>,
                    response: Response<List<ImpresoraDto>>
                ) {
                    if (response.isSuccessful) {
                        onResult(response.body().orEmpty())
                    } else {
                        onError("Error al obtener impresoras")
                    }
                }

                override fun onFailure(call: Call<List<ImpresoraDto>>, t: Throwable) {
                    onError("Error de conexión: ${t.message}")
                }
            })
    }

    // ——— patrones ———
    private val ssccRegex = Regex("""^00(\d{18})""")              // 00 + 18 dígitos, SOLO al inicio
    private val gtinRegex = Regex("""^01(?:0(\d{13})|(\d{14}))""")// 01 + ( opcional 0 + 13 ó 14 dígitos )

    fun procesarCodigoEscaneado(
        code: String,
        empresaId: Short,
        codigoAlmacen: String? = null,
        codigoCentro: String? = null,
        almacen: String? = null,
        onUbicacionDetectada: (almacen: String, ubicacion: String) -> Unit,
        onArticuloDetectado: (ArticuloDto) -> Unit,
        onMultipleArticulos: (List<ArticuloDto>) -> Unit,
        onPaletDetectado: (PaletDto) -> Unit,
        onError: (String) -> Unit
    ) {
        Log.d("ESCANEO", "📥 Código recibido: $code")
        val trimmed = code.trim()

        // 0) UBICACIÓN incompleta tipo "ALM$" (solo almacén)
        val almSoloRegex = Regex("""^([^$]+)\$$""")
        almSoloRegex.matchEntire(trimmed)?.let { m ->
            val codAlm = m.groupValues[1].trim()
            Log.d("ESCANEO", "🏬 Almacén detectado sin ubicación: $codAlm")
            onUbicacionDetectada(codAlm, "")   // ← ubicación vacía; la UI puede pedirla después
            SoundUtils.getInstance().playSuccessSound()
            return
        }
        /* ───────────────────────────────────────────────
           1) UBICACIÓN explícita  CODALM$UBIC
           ─────────────────────────────────────────────*/
        val ubicRegex = Regex("""^([^$]+)\$([^$]+)$""")   // ej. 201$UB001
        ubicRegex.matchEntire(trimmed)?.let { m ->
            val codAlm  = m.groupValues[1].trim()
            val codUbi  = m.groupValues[2].trim()
            Log.d("ESCANEO", "📍 Ubicación detectada: $codAlm – $codUbi")
            onUbicacionDetectada(codAlm, codUbi)
            SoundUtils.getInstance().playSuccessSound()
            return
        }

        gtinRegex.find(code)?.let { m ->
            val ean13 = m.groupValues.drop(1).first { it.isNotEmpty() }.takeLast(13)
            Log.d("ESCANEO", "🛒 EAN13 detectado: $ean13")

            // CÓDIGO ORIGINAL (comentado por si hay que volver atrás):
            // val ai10Index = code.indexOf("10", startIndex = 16)
            // val partida = if (ai10Index != -1 && ai10Index + 2 < code.length) {
            //     code.substring(ai10Index + 2)
            // } else null

            // Extraer fecha de caducidad (AI 15, formato AAMMDD) PRIMERO
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

            // Buscar AI 10 (lote) DESPUÉS de la fecha de caducidad para evitar conflictos
            val startSearchIndex = if (ai15Index != -1) ai15Index + 8 else 16
            val ai10Index = code.indexOf("10", startIndex = startSearchIndex)
            val partida = if (ai10Index != -1 && ai10Index + 2 < code.length) {
                code.substring(ai10Index + 2)
            } else null

            Log.d("ESCANEO", "🔖 Partida extraída: $partida")
            Log.d("ESCANEO", "📅 Fecha caducidad extraída: $fechaCaducidad")

            ApiManager.etiquetasApiService.buscarArticulo(
                codigoEmpresa = empresaId,
                codigoAlternativo = ean13,
                codigoAlmacen = codigoAlmacen,
                codigoCentro = codigoCentro,
                almacen = almacen,
                partida = partida
            ).enqueue(object : Callback<List<ArticuloDto>> {
                override fun onResponse(
                    call: Call<List<ArticuloDto>>,
                    response: Response<List<ArticuloDto>>
                ) {
                    if (response.isSuccessful) {
                        val lista = response.body().orEmpty()
                        Log.d("ESCANEO", "🎯 Artículos encontrados: "+lista.size)

                        // Asegura que todos los DTO llevan la partida y fecha de caducidad extraídas si no la tienen
                        val listaConDatos = lista.map {
                            it.copy(
                                partida = it.partida ?: partida,
                                fechaCaducidad = it.fechaCaducidad ?: fechaCaducidad
                            )
                        }

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
                            0 -> {
                                onError("No se encontró ningún artículo con ese código y partida.")
                                SoundUtils.getInstance().playErrorSound()
                            }
                            1 -> {
                                onArticuloDetectado(candidatos.first())
                                SoundUtils.getInstance().playSuccessSound()
                            }
                            else -> {
                                onMultipleArticulos(candidatos)
                                SoundUtils.getInstance().playSuccessSound()
                            }
                        }
                    } else {
                        onError("HTTP ${response.code()}")
                    }
                }

                override fun onFailure(call: Call<List<ArticuloDto>>, t: Throwable) {
                    onError("Red: ${t.message}")
                }
            })
            return
        }

        ssccRegex.find(code)?.let { m ->
            val gs1 = m.groupValues[1]
            Log.d("ESCANEO", "📦 SSCC detectado: $gs1")

            ApiManager.traspasosApi.obtenerPaletPorGS1(gs1)
                .enqueue(object : Callback<PaletDto> {
                    override fun onResponse(call: Call<PaletDto>, response: Response<PaletDto>) {
                        if (response.isSuccessful) {
                            val palet = response.body()
                            if (palet != null) {
                                onPaletDetectado(palet)
                                SoundUtils.getInstance().playSuccessSound()
                            } else {
                                onError("No se encontró ningún palet con ese GS1.")
                            }
                        } else {
                            onError("Error HTTP al obtener palet: ${response.code()}")
                        }
                    }

                    override fun onFailure(call: Call<PaletDto>, t: Throwable) {
                        onError("Error de red al obtener palet: ${t.message}")
                    }
                })
            return
        }
        // ——— Búsqueda por código de artículo directo (si no es ubicación, EAN, ni SSCC) ———
        val codArt = trimmed.takeIf { it.length in 4..25 && it.all { ch -> ch.isLetterOrDigit() } }
        if (codArt != null) {
            Log.d("ESCANEO", "🔍 Posible código de artículo: $codArt")

            ApiManager.etiquetasApiService.buscarArticulo(
                codigoEmpresa = empresaId,
                codigoArticulo = codArt,
                codigoAlmacen = codigoAlmacen,
                codigoCentro = codigoCentro,
                almacen = almacen
            ).enqueue(object : Callback<List<ArticuloDto>> {
                override fun onResponse(call: Call<List<ArticuloDto>>, response: Response<List<ArticuloDto>>) {
                    if (response.isSuccessful) {
                        val lista = response.body().orEmpty()
                        Log.d("ESCANEO", "🎯 Artículos encontrados por código directo: ${lista.size}")
                        when (lista.size) {
                            0 -> {
                                onError("No se encontró ningún artículo con ese código.")
                                SoundUtils.getInstance().playErrorSound()
                            }
                            1 -> {
                                onArticuloDetectado(lista.first())
                                SoundUtils.getInstance().playSuccessSound()
                            }
                            else -> {
                                onMultipleArticulos(lista)
                                SoundUtils.getInstance().playSuccessSound()
                            }
                        }
                    } else {
                        onError("HTTP ${response.code()}")
                    }
                }

                override fun onFailure(call: Call<List<ArticuloDto>>, t: Throwable) {
                    onError("Red: ${t.message}")
                }
            })
        }

    }

    fun obtenerUbicacionDePalet(
        idPalet: String,
        onResult: (almacen: String, ubicacion: String) -> Unit,
        onError: (String) -> Unit
    ) {
        ApiManager.traspasosApi.obtenerPaletsMovibles()
            .enqueue(object : Callback<List<PaletMovibleDto>> {
                override fun onResponse(
                    call: Call<List<PaletMovibleDto>>,
                    response: Response<List<PaletMovibleDto>>
                ) {
                    if (response.isSuccessful) {
                        val palet = response.body()?.find { it.id.equals(idPalet, ignoreCase = true) }
                        if (palet != null) {
                            onResult(palet.almacenOrigen.trim().uppercase(), palet.ubicacionOrigen.trim().uppercase())
                        } else {
                            onError("Palet no encontrado en el sistema.")
                        }
                    } else {
                        onError("Error al obtener ubicación: código ${response.code()}")
                    }
                }

                override fun onFailure(call: Call<List<PaletMovibleDto>>, t: Throwable) {
                    onError("Fallo de red: ${t.message}")
                }
            })
    }


    /*fun consultarStockConDescripcion(
        codigoEmpresa: Short,
        codigoArticulo: String,
        codigoAlmacen: String? = null,
        codigoUbicacion: String? = null,
        codigoCentro: String? = null,
        almacen: String? = null,
        partida: String? = null,
        almacenesPermitidos: List<String>? = null,
        onSuccess: (List<Stock>) -> Unit,
        onError: (String) -> Unit
    ) {
        Log.d("STOCK", "🔍 Consultando stock para $codigoArticulo")
        ApiManager.traspasosApi.obtenerStockDisponible(
            empresaId       = codigoEmpresa,
            codigoArticulo  = codigoArticulo.trim().uppercase(),
            descripcion     = null,
            partida         = partida?.trim()?.uppercase(),
            codigoAlmacen   = codigoAlmacen?.trim()?.uppercase(),
            codigoUbicacion = codigoUbicacion?.trim()?.uppercase()
        ).enqueue(object : Callback<List<StockDisponibleDto>> {
            override fun onResponse(call: Call<List<StockDisponibleDto>>, response: Response<List<StockDisponibleDto>>) {
                if (!response.isSuccessful) {
                    val msg = response.errorBody()?.string() ?: "Error desconocido"
                    Log.e("STOCK", "❌ Error ${response.code()}: $msg")
                    when (response.code()) {
                        403 -> onError("No tienes permisos para acceder a este artículo en esta ubicación.")
                        404 -> onError("El artículo no está disponible en esta ubicación.")
                        else -> onError("Error al consultar stock: ${response.code()}")
                    }
                    return
                }

                val listaInicial = response.body().orEmpty()
                Log.d("STOCK", "📦 StockDisponible recibido: ${listaInicial.size} registros")

                if (listaInicial.isEmpty()) {
                    onSuccess(emptyList())
                    onError("El artículo no está disponible en esta ubicación o no tienes permisos para acceder a él.")
                    return
                }

                val codigoPrimero = listaInicial.first().codigoArticulo

                ApiManager.stockApi.buscarArticulo(
                    codigoEmpresa   = codigoEmpresa,
                    codigoArticulo  = codigoPrimero,
                    codigoAlmacen   = codigoAlmacen,
                    codigoCentro    = codigoCentro,
                    almacen         = almacen,
                    partida         = partida
                ).enqueue(object : Callback<List<ArticuloDto>> {
                    override fun onResponse(call: Call<List<ArticuloDto>>, response: Response<List<ArticuloDto>>) {
                        val descripcion = response.body()
                            ?.firstOrNull()
                            ?.descripcion ?: "Sin descripción"

                        Log.d("STOCK", "📝 Descripción encontrada: $descripcion")

                        val listaMapeada = listaInicial.map {
                            StockDisponibleMapper.fromDisponibleDto(it.copy(descripcion = descripcion), codigoEmpresa.toString())
                        }

                        val listaFiltrada = if (almacenesPermitidos.isNullOrEmpty()) {
                            listaMapeada                                  // ⬅️ sin filtro
                        } else {
                            val permitidosNorm = almacenesPermitidos
                                .map { it.trim().uppercase().trimStart('0') }   // “002” → “2”
                                .toSet()

                            listaMapeada.filter { stock ->
                                stock.codigoAlmacen.trim().uppercase().trimStart('0') in permitidosNorm
                            }
                        }
                        Log.d("STOCK", "Permitidos: $almacenesPermitidos  ⇒ ${listaFiltrada.size}")
                        onSuccess(listaFiltrada)
                    }

                    override fun onFailure(call: Call<List<ArticuloDto>>, t: Throwable) {
                        Log.w("STOCK", "⚠️ No se pudo obtener la descripción: ${t.message}")
                        val listaMapeada = listaInicial.map { StockDisponibleMapper.fromDisponibleDto(it, codigoEmpresa.toString()) }
                        val listaFiltrada = almacenesPermitidos?.let { permitidos ->
                            listaMapeada.filter { it.codigoAlmacen in permitidos }
                        } ?: listaMapeada

                        onSuccess(listaFiltrada)
                        onError("Descripción no disponible.")
                    }
                })
            }

            override fun onFailure(call: Call<List<StockDisponibleDto>>, t: Throwable) {
                Log.e("STOCK", "💥 Error de red: ${t.localizedMessage}")
                onError("Error de conexión: ${t.localizedMessage}")
            }
        })
    }
*/
fun consultarStockConDescripcion(
    codigoEmpresa: Short,
    codigoArticulo: String,
    codigoAlmacen: String? = null,
    codigoUbicacion: String? = null,
    codigoCentro: String? = null,
    almacen: String? = null,
    partida: String? = null,
    almacenesPermitidos: List<String>? = null,
    onSuccess: (List<Stock>) -> Unit,
    onError: (String) -> Unit
) {
    Log.d("STOCK", "🔍 Consultando stock para $codigoArticulo")
    
    // Primero verificar permisos si tenemos almacén y lista de almacenes permitidos
    if (codigoAlmacen != null && !almacenesPermitidos.isNullOrEmpty()) {
        val almacenNormalizado = codigoAlmacen.trim().uppercase().trimStart('0')
        val permitidosNorm = almacenesPermitidos
            .map { it.trim().uppercase().trimStart('0') }
            .toSet()
        
        if (almacenNormalizado !in permitidosNorm) {
            onError("No tienes permisos para operar en el almacén '$codigoAlmacen'.")
            return
        }
    }
    
    ApiManager.traspasosApi.obtenerStockDisponible(
        empresaId       = codigoEmpresa,
        codigoArticulo  = codigoArticulo.trim().uppercase(),
        descripcion     = null,
        partida         = partida?.trim()?.uppercase(),
        codigoAlmacen   = codigoAlmacen?.trim()?.uppercase(),
        codigoUbicacion = codigoUbicacion?.trim()?.uppercase()
    ).enqueue(object : Callback<List<StockDisponibleDto>> {
        override fun onResponse(call: Call<List<StockDisponibleDto>>, response: Response<List<StockDisponibleDto>>) {
            if (!response.isSuccessful) {
                val msg = response.errorBody()?.string() ?: "Error desconocido"
                Log.e("STOCK", "❌ Error ${response.code()}: $msg")
                when (response.code()) {
                    403 -> onError("No tienes permisos para acceder a este artículo en esta ubicación.")
                    404 -> onError("El artículo no está disponible en esta ubicación.")
                    else -> onError("Error al consultar stock: ${response.code()}")
                }
                return
            }

            val listaInicial = response.body().orEmpty()
            Log.d("STOCK", "📦 StockDisponible recibido: ${listaInicial.size} registros")

            if (listaInicial.isEmpty()) {
                onSuccess(emptyList())
                // Obtener descripción del artículo para el mensaje de error más específico
                ApiManager.stockApi.buscarArticulo(
                    codigoEmpresa   = codigoEmpresa,
                    codigoArticulo  = codigoArticulo.trim().uppercase(),
                    codigoAlmacen   = codigoAlmacen,
                    codigoCentro    = codigoCentro,
                    almacen         = almacen,
                    partida         = partida
                ).enqueue(object : Callback<List<ArticuloDto>> {
                    override fun onResponse(call: Call<List<ArticuloDto>>, response: Response<List<ArticuloDto>>) {
                        val descripcion = response.body()
                            ?.firstOrNull()
                            ?.descripcion ?: "Sin descripción"
                        
                        onError("No hay stock disponible del artículo $codigoArticulo - $descripcion.")
                    }
                    
                    override fun onFailure(call: Call<List<ArticuloDto>>, t: Throwable) {
                        onError("No hay stock disponible del artículo $codigoArticulo.")
                    }
                })
                return
            }

            val codigoPrimero = listaInicial.first().codigoArticulo

            ApiManager.stockApi.buscarArticulo(
                codigoEmpresa   = codigoEmpresa,
                codigoArticulo  = codigoPrimero,
                codigoAlmacen   = codigoAlmacen,
                codigoCentro    = codigoCentro,
                almacen         = almacen,
                partida         = partida
            ).enqueue(object : Callback<List<ArticuloDto>> {
                override fun onResponse(call: Call<List<ArticuloDto>>, response: Response<List<ArticuloDto>>) {
                    val descripcion = response.body()
                        ?.firstOrNull()
                        ?.descripcion ?: "Sin descripción"

                    Log.d("STOCK", "📝 Descripción encontrada: $descripcion")

                    val listaMapeada = listaInicial.map {
                        StockDisponibleMapper.fromDisponibleDto(it.copy(descripcion = descripcion), codigoEmpresa.toString())
                    }

                    val listaFiltrada = if (almacenesPermitidos.isNullOrEmpty()) {
                        listaMapeada                                  // ⬅️ sin filtro
                    } else {
                        val permitidosNorm = almacenesPermitidos
                            .map { it.trim().uppercase().trimStart('0') }   // "002" → "2"
                            .toSet()

                        listaMapeada.filter { stock ->
                            stock.codigoAlmacen.trim().uppercase().trimStart('0') in permitidosNorm
                        }
                    }
                    
                    // Verificar si después del filtrado por permisos queda algún resultado
                    if (listaFiltrada.isEmpty() && !almacenesPermitidos.isNullOrEmpty()) {
                        // Si había stock pero se filtró por permisos, mostrar mensaje específico
                        val almacenesEncontrados = listaMapeada.map { it.codigoAlmacen }.distinct()
                        if (almacenesEncontrados.isNotEmpty()) {
                            val almacenEncontrado = almacenesEncontrados.first()
                            onError("No tienes permisos para operar en el almacén '$almacenEncontrado'.")
                        } else {
                            onError("No hay stock disponible del artículo $codigoArticulo - $descripcion.")
                        }
                    } else {
                        Log.d("STOCK", "Permitidos: $almacenesPermitidos  ⇒ ${listaFiltrada.size}")
                        onSuccess(listaFiltrada)
                    }
                }

                override fun onFailure(call: Call<List<ArticuloDto>>, t: Throwable) {
                    Log.w("STOCK", "⚠️ No se pudo obtener la descripción: ${t.message}")
                    val listaMapeada = listaInicial.map { StockDisponibleMapper.fromDisponibleDto(it, codigoEmpresa.toString()) }
                    val listaFiltrada = if (almacenesPermitidos.isNullOrEmpty()) {
                        listaMapeada
                    } else {
                        val permitidosNorm = almacenesPermitidos
                            .map { it.trim().uppercase().trimStart('0') }
                            .toSet()

                        listaMapeada.filter { stock ->
                            stock.codigoAlmacen.trim().uppercase().trimStart('0') in permitidosNorm
                        }
                    }

                    // Verificar si después del filtrado por permisos queda algún resultado
                    if (listaFiltrada.isEmpty() && !almacenesPermitidos.isNullOrEmpty()) {
                        val almacenesEncontrados = listaMapeada.map { it.codigoAlmacen }.distinct()
                        if (almacenesEncontrados.isNotEmpty()) {
                            val almacenEncontrado = almacenesEncontrados.first()
                            onError("No tienes permisos para operar en el almacén '$almacenEncontrado'.")
                        } else {
                            onError("No hay stock disponible del artículo $codigoArticulo.")
                        }
                    } else {
                        onSuccess(listaFiltrada)
                    }
                }
            })
        }

        override fun onFailure(call: Call<List<StockDisponibleDto>>, t: Throwable) {
            Log.e("STOCK", "💥 Error de red: ${t.localizedMessage}")
            onError("Error de conexión: ${t.localizedMessage}")
        }
    })
}
    fun completarTraspaso(
        idTraspaso: String,
        dto: CompletarTraspasoDto,
        paletId: String? = null, // ← opcional (solo valida si es palet)
        onSuccess: () -> Unit,
        onError: (String) -> Unit
    ) {
        val ubic = dto.ubicacionDestino?.trim().orEmpty()
        val alm  = dto.codigoAlmacenDestino?.trim().orEmpty()
        val debeValidar = paletId != null && ubic.startsWith("UB", ignoreCase = true)

        fun llamarCompletar() {
            ApiManager.traspasosApi.completarTraspaso(idTraspaso, dto)
                .enqueue(object : Callback<Void> {
                    override fun onResponse(call: Call<Void>, response: Response<Void>) {
                        if (response.isSuccessful) onSuccess()
                        else onError("Error ${response.code()}: ${response.errorBody()?.string()}")
                    }
                    override fun onFailure(call: Call<Void>, t: Throwable) {
                        onError(t.message ?: "Error desconocido")
                    }
                })
        }

        if (!debeValidar) {
            // → Como antes (no aplica exclusividad de palet)
            llamarCompletar()
            return
        }

        // ✅ Reutiliza el helper de validación ANTES de completar
        validarUbicacionDestino(
            paletId = paletId!!,
            codigoAlmacen = alm,
            ubicacion = ubic,
            onOk = { llamarCompletar() },
            onBloqueada = { motivo -> onError(motivo) },
            onError = { err -> onError(err) }
        )
    }

    fun imprimirEtiquetaPalet(
        dto: LogImpresionDto,
        onSuccess: () -> Unit,
        onError: (String) -> Unit
    ) {
        ApiManager.etiquetasApiService.insertarLogImpresion(dto)
            .enqueue(object : Callback<LogImpresionDto> {
                override fun onResponse(
                    call: Call<LogImpresionDto>,
                    response: Response<LogImpresionDto>
                ) {
                    if (response.isSuccessful) {
                        onSuccess()
                    } else {
                        val error = response.errorBody()?.string() ?: "Error ${response.code()}"
                        onError("Error al imprimir: $error")
                    }
                }

                override fun onFailure(call: Call<LogImpresionDto>, t: Throwable) {
                    onError("Error de red: ${t.message}")
                }
            })
    }

    fun crearTraspasoArticulo(
        dto: CrearTraspasoArticuloDto,
        onSuccess: (TraspasoArticuloDto) -> Unit,
        onError: (String) -> Unit
    ) {
        Log.d("TRASPASOS_API", "📤 Enviando crearTraspasoArticulo con DTO: $dto")

        ApiManager.traspasosApi.crearTraspasoArticulo(dto)
            .enqueue(object : retrofit2.Callback<TraspasoArticuloDto> {
                override fun onResponse(
                    call: retrofit2.Call<TraspasoArticuloDto>,
                    response: retrofit2.Response<TraspasoArticuloDto>
                ) {
                    if (response.isSuccessful) {
                        Log.d("TRASPASOS_API", "✅ Respuesta exitosa: ${response.body()}")
                        response.body()?.let { onSuccess(it) }
                            ?: onError("Respuesta vacía")
                    } else {
                        val errorMsg = response.errorBody()?.string()
                        Log.e("TRASPASOS_API", "❌ Error ${response.code()}: $errorMsg")
                        onError("Error ${response.code()}: $errorMsg")
                    }
                }

                override fun onFailure(call: retrofit2.Call<TraspasoArticuloDto>, t: Throwable) {
                    Log.e("TRASPASOS_API", "❌ Error de red: ${t.message}")
                    onError("Error de red: ${t.message}")
                }
            })
    }

    fun finalizarTraspasoArticulo(
        id: String,
        dto: FinalizarTraspasoArticuloDto,
        onSuccess: () -> Unit,
        onError: (String) -> Unit
    ) {
        ApiManager.traspasosApi.finalizarTraspasoArticulo(id, dto)
            .enqueue(object : retrofit2.Callback<Void> {
                override fun onResponse(
                    call: retrofit2.Call<Void>,
                    response: retrofit2.Response<Void>
                ) {
                    if (response.isSuccessful) {
                        onSuccess()
                    } else {
                        onError("Error ${response.code()}: ${response.errorBody()?.string()}")
                    }
                }
                override fun onFailure(call: retrofit2.Call<Void>, t: Throwable) {
                    onError("Error de red: ${t.message}")
                }
            })
    }
    fun comprobarTraspasoPendiente(
        usuarioId: Int,
        onSuccess: (List<TraspasoPendienteDto>) -> Unit,
        onError: (String) -> Unit
    ) {
        ApiManager.traspasosApi.comprobarTraspasoPendiente(usuarioId)
            .enqueue(object : Callback<List<TraspasoPendienteDto>> {
                override fun onResponse(
                    call: Call<List<TraspasoPendienteDto>>,
                    response: Response<List<TraspasoPendienteDto>>
                ) {
                    if (response.isSuccessful) {
                        val lista = response.body().orEmpty()
                        onSuccess(lista)
                    } else if (response.code() == 404) {
                        onSuccess(emptyList()) // No hay traspasos pendientes
                    } else {
                        onError("Error ${response.code()}: ${response.errorBody()?.string()}")
                    }
                }

                override fun onFailure(call: Call<List<TraspasoPendienteDto>>, t: Throwable) {
                    onError("Error de red: ${t.message}")
                }
            })
    }

    fun actualizarImpresoraSeleccionadaEnBD(
        nombre: String,
        sessionViewModel: SessionViewModel
    ) {
        val userId = sessionViewModel.user.value?.id?.toIntOrNull() ?: return
        val empresaId = sessionViewModel.empresaSeleccionada.value?.codigo?.toString() ?: return

        val dto = ConfiguracionUsuarioPatchDto(
            idEmpresa = empresaId,
            impresora = nombre
        )

        ApiManager.userApi.actualizarConfiguracionUsuario(userId, dto)
            .enqueue(object : Callback<Void> {
                override fun onResponse(call: Call<Void>, response: Response<Void>) {
                    Log.d("TRASPASOS", "✅ Impresora actualizada en BD: $nombre")
                    sessionViewModel.actualizarImpresora(nombre)
                }

                override fun onFailure(call: Call<Void>, t: Throwable) {
                    Log.e("TRASPASOS", "❌ Error al actualizar impresora: ${t.message}")
                }
            })
    }

    fun moverPalet(
        dto: MoverPaletDto,
        onSuccess: (String) -> Unit, // ahora devuelve el ID del traspaso
        onError: (String) -> Unit
    ) {
        Log.d("MOVER_PALET", "📡 Llamando a API moverPalet con: $dto")
        ApiManager.traspasosApi.moverPalet(dto)
            .enqueue(object : Callback<MoverPaletResponse> {
                override fun onResponse(call: Call<MoverPaletResponse>, response: Response<MoverPaletResponse>) {
                    Log.d("MOVER_PALET", "📬 Código HTTP: ${response.code()}")
                    if (response.isSuccessful) {
                        val id = response.body()?.traspasosIds?.firstOrNull()
                        if (id != null) {
                            Log.d("MOVER_PALET", "✅ Traspaso creado con ID: $id")
                            onSuccess(id)
                            SoundUtils.getInstance().playSuccessSound()
                        } else {
                            onError("Traspaso creado pero no se recibió ID")
                            SoundUtils.getInstance().playErrorSound()
                        }
                    } else {
                        val msg = response.errorBody()?.string() ?: "Error ${response.code()}"
                        Log.e("MOVER_PALET", "❌ Error moverPalet: $msg")
                        onError("Error ${response.code()}: $msg")
                        SoundUtils.getInstance().playErrorSound()
                    }
                }

                override fun onFailure(call: Call<MoverPaletResponse>, t: Throwable) {
                    Log.e("MOVER_PALET", "❌ Error de red moverPalet: ${t.message}")
                    onError("Error de red: ${t.message}")
                    SoundUtils.getInstance().playErrorSound()
                }
            })
    }

    fun finalizarTraspasoPalet(
        traspasoId: String,
        dto: FinalizarTraspasoPaletDto,
        paletId: String?,                         // puede venir null en flujos sin palet
        onSuccess: () -> Unit,
        onError: (String) -> Unit
    ) {
        val alm = dto.almacenDestino.trim()
        val ubi = dto.ubicacionDestino.trim()
        val debeValidar = paletId != null && ubi.startsWith("UB", ignoreCase = true)

        val continuarFinalizacion = {
            ApiManager.traspasosApi.finalizarTraspasoPalet(
                traspasoId = traspasoId,
                dto = dto
            ).enqueue(object : retrofit2.Callback<Void> {
                override fun onResponse(call: retrofit2.Call<Void>, response: retrofit2.Response<Void>) {
                    if (response.isSuccessful) {
                        onSuccess()
                    } else {
                        onError("Error ${response.code()} al finalizar traspaso.")
                    }
                }
                override fun onFailure(call: retrofit2.Call<Void>, t: Throwable) {
                    onError("Fallo de red al finalizar traspaso: ${t.message}")
                }
            })
        }

        if (!debeValidar) {
            // No aplica validación de ubicación (p. ej. artículo o ubicación no UB…)
            continuarFinalizacion()
            return
        }

        // ✅ Reutilizamos el helper de validación ANTES de finalizar
        validarUbicacionDestino(
            paletId = paletId!!,
            codigoAlmacen = alm,
            ubicacion = ubi,
            onOk = { continuarFinalizacion() },
            onBloqueada = { motivo -> onError(motivo) },
            onError = { err -> onError(err) }
        )
    }

    private fun validarUbicacionDestino(
        paletId: String,
        codigoAlmacen: String,
        ubicacion: String,
        onOk: () -> Unit,
        onBloqueada: (String) -> Unit,
        onError: (String) -> Unit
    ) {
        ApiManager.traspasosApi.validarUbicacionDestino(
            paletId = paletId,
            codigoAlmacen = codigoAlmacen,
            ubicacion = ubicacion
        ).enqueue(object : Callback<ValidarUbicacionResponse> {
            override fun onResponse(
                call: Call<ValidarUbicacionResponse>,
                response: Response<ValidarUbicacionResponse>
            ) {
                if (response.isSuccessful) {
                    val body = response.body()
                    if (body?.ok == true) onOk()
                    else onBloqueada(body?.motivo ?: "Ubicación no válida.")
                    return
                }
                if (response.code() == 409) {
                    Log.e("HTTP409", "⚠️ 409 desde ${call.request().url}")
                    onBloqueada("Ubicación ocupada por otro palet.")
                    return
                }
                onError("Error ${response.code()} al validar ubicación.")
            }

            override fun onFailure(call: Call<ValidarUbicacionResponse>, t: Throwable) {
                onError("Fallo de red al validar ubicación: ${t.message}")
            }
        })
    }

    fun buscarArticuloPorCodigo(
        codigoEmpresa: Short,
        codigoArticulo: String,
        codigoAlmacen: String? = null,
        codigoCentro: String? = null,
        almacen: String? = null,
        onSuccess: (ArticuloDto) -> Unit,
        onError: (String) -> Unit
    ) {
        ApiManager.etiquetasApiService.buscarArticulo(
            codigoEmpresa = codigoEmpresa,
            codigoArticulo = codigoArticulo,
            codigoAlmacen = codigoAlmacen,
            codigoCentro = codigoCentro,
            almacen = almacen
        ).enqueue(object: retrofit2.Callback<List<ArticuloDto>> {
            override fun onResponse(call: retrofit2.Call<List<ArticuloDto>>, resp: retrofit2.Response<List<ArticuloDto>>) {
                if (!resp.isSuccessful) { onError("HTTP ${resp.code()}"); return }
                val art = resp.body()?.firstOrNull()
                if (art != null) onSuccess(art) else onError("Artículo no encontrado")
            }
            override fun onFailure(call: retrofit2.Call<List<ArticuloDto>>, t: Throwable) {
                onError("Red: ${t.message}")
            }
        })
    }
    fun precheckFinalizarArticulo(
        codigoEmpresa: Short,
        almacenDestino: String,
        ubicacionDestino: String,
        onResult: (existe: Boolean, paletId: String?, cerrado: Boolean, aviso: String?) -> Unit,
        onError: (String) -> Unit
    ) {
        ApiManager.traspasosApi
            .precheckFinalizarArticulo(codigoEmpresa, almacenDestino, ubicacionDestino)
            .enqueue(object : retrofit2.Callback<PrecheckResp> {
                override fun onResponse(
                    call: retrofit2.Call<PrecheckResp>,
                    resp: retrofit2.Response<PrecheckResp>
                ) {
                    if (!resp.isSuccessful) {
                        onError("HTTP ${resp.code()}: ${resp.errorBody()?.string().orEmpty()}"); return
                    }
                    val b = resp.body() ?: PrecheckResp(false)
                    onResult(b.existe, b.paletId, b.cerrado == true, b.aviso)
                }
                override fun onFailure(call: retrofit2.Call<PrecheckResp>, t: Throwable) {
                    onError("Red: ${t.message}")
                }
            })
    }
}
