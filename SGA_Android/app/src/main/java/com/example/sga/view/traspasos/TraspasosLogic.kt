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
import com.example.sga.data.dto.traspasos.FinalizarTraspasoArticuloDto
import com.example.sga.data.dto.traspasos.FinalizarTraspasoPaletDto
import com.example.sga.data.dto.traspasos.MoverPaletDto
import com.example.sga.data.dto.traspasos.MoverPaletResponse
import com.example.sga.data.dto.traspasos.PaletMovibleDto
import com.example.sga.data.dto.traspasos.TraspasoCreadoResponse
import com.example.sga.data.dto.traspasos.TraspasoPendienteDto
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
                        response.body()?.let { onSuccess(it) } ?: onError("Palet vacío")
                    } else {
                        onError("Error ${response.code()}")
                    }
                }

                override fun onFailure(call: Call<PaletDto>, t: Throwable) {
                    onError("Error: ${t.message}")
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
    fun cerrarPalet(
        idPalet: String,
        usuarioId: Int,
        codigoAlmacen: String,
        codigoEmpresa: Short,
        ubicacionOrigen: String?, // ✅ añadido como parámetro
        onSuccess: (String) -> Unit,
        onError: (String) -> Unit
    ) {
        val dto = CerrarPaletMobilityDto(
            usuarioId = usuarioId,
            codigoAlmacen = codigoAlmacen,
            codigoEmpresa = codigoEmpresa,
            ubicacionOrigen = ubicacionOrigen // ✅ se asigna si viene
        )

        ApiManager.traspasosApi.cerrarPalet(idPalet, dto)
            .enqueue(object : Callback<TraspasoCreadoResponse> {
                override fun onResponse(
                    call: Call<TraspasoCreadoResponse>,
                    response: Response<TraspasoCreadoResponse>
                ) {
                    if (response.isSuccessful) {

                        val paletId = response.body()?.paletId

                        if (paletId != null) onSuccess(paletId)

                        else onError("Respuesta inválida del servidor")

                    } else {

                        onError("Código ${response.code()}: ${response.errorBody()?.string()}")

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
                    if (response.isSuccessful) onSuccess() else onError("Código ${response.code()}")
                }

                override fun onFailure(call: Call<Void>, t: Throwable) {
                    onError(t.message ?: "Error desconocido")
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
                    } else {
                        val errorBody = response.errorBody()?.string().orEmpty()
                        Log.e("LINEA_PALET", "❌ Error ${response.code()}: $errorBody")
                        onError("Error ${response.code()}: $errorBody")
                    }
                }

                override fun onFailure(call: Call<LineaPaletDto>, t: Throwable) {
                    Log.e("LINEA_PALET", "💥 Fallo de red: ${t.message}")
                    onError("Error de red: ${t.message}")
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
                    } else {
                        val msg = response.errorBody()?.string() ?: "Error ${response.code()}"
                        onError("No se pudo eliminar la línea: $msg")
                    }
                }

                override fun onFailure(call: Call<Void>, t: Throwable) {
                    onError("Error de red: ${t.message}")
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

        /* ───────────────────────────────────────────────
           1) UBICACIÓN explícita  CODALM$UBIC
           ─────────────────────────────────────────────
        val ubicRegex = Regex("""^([^$]+)\$([^$]+)$""")   // ej. 201$UB001
        ubicRegex.matchEntire(trimmed)?.let { m ->
            val codAlm  = m.groupValues[1].trim()
            val codUbi  = m.groupValues[2].trim()
            Log.d("ESCANEO", "📍 Ubicación detectada: $codAlm – $codUbi")
            onUbicacionDetectada(codAlm, codUbi)
            return
        }*/

        gtinRegex.find(code)?.let { m ->
            val ean13 = m.groupValues.drop(1).first { it.isNotEmpty() }.takeLast(13)
            Log.d("ESCANEO", "🛒 EAN13 detectado: $ean13")

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
                            0 -> onError("No se encontró ningún artículo con ese código y partida.")
                            1 -> onArticuloDetectado(candidatos.first())
                            else -> onMultipleArticulos(candidatos)
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
        /* ───────────────────────────────────────────────
   4) UBICACIÓN implícita  (sin '$'  y no GTIN / SSCC)
   ───────────────────────────────────────────── */
        if (!trimmed.contains('$')) {
            val codAlm  = "PR"          // almacén fijo
            val codUbi  = trimmed        // ej. UB001001002002
            Log.d("ESCANEO", "📍 Ubicación detectada (fijo PR): $codUbi")
            onUbicacionDetectada(codAlm, codUbi)
            return
        }
        onError("El código no es un SSCC ni un EAN-13/GTIN-14 válido")
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
                    onError("Error ${response.code()}: $msg")
                    return
                }

                val listaInicial = response.body().orEmpty()
                Log.d("STOCK", "📦 StockDisponible recibido: ${listaInicial.size} registros")

                if (listaInicial.isEmpty()) {
                    onSuccess(emptyList())
                    onError("No se encontraron datos de stock.")
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
                            StockDisponibleMapper.fromDisponibleDto(it.copy(descripcion = descripcion))
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
                        val listaMapeada = listaInicial.map { StockDisponibleMapper.fromDisponibleDto(it) }
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
    fun completarTraspaso(
        idTraspaso: String,
        dto: CompletarTraspasoDto,
        onSuccess: () -> Unit,
        onError: (String) -> Unit
    ) {
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
        // Debug: Log para verificar el DTO que se está enviando
        Log.d("TRASPASOS_LOGIC", "📤 Enviando DTO: descripcionArticulo=${dto.descripcionArticulo}, codigoEmpresa=${dto.codigoEmpresa}")
        
        ApiManager.traspasosApi.crearTraspasoArticulo(dto)
            .enqueue(object : retrofit2.Callback<TraspasoArticuloDto> {
                override fun onResponse(
                    call: retrofit2.Call<TraspasoArticuloDto>,
                    response: retrofit2.Response<TraspasoArticuloDto>
                ) {
                    if (response.isSuccessful) {
                        response.body()?.let { onSuccess(it) } ?: onError("Respuesta vacía")
                    } else {
                        onError("Error ${response.code()}: ${response.errorBody()?.string()}")
                    }
                }
                override fun onFailure(call: retrofit2.Call<TraspasoArticuloDto>, t: Throwable) {
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
        onSuccess: (TraspasoPendienteDto?) -> Unit,
        onError: (String) -> Unit
    ) {
        ApiManager.traspasosApi.comprobarTraspasoPendiente(usuarioId)
            .enqueue(object : Callback<TraspasoPendienteDto> {
                override fun onResponse(
                    call: Call<TraspasoPendienteDto>,
                    response: Response<TraspasoPendienteDto>
                ) {
                    if (response.isSuccessful) {
                        val dto = response.body()
                        onSuccess(dto)
                    } else if (response.code() == 404) {
                        onSuccess(null) // No hay traspaso pendiente
                    } else {
                        onError("Error ${response.code()}: ${response.errorBody()?.string()}")
                    }
                }

                override fun onFailure(call: Call<TraspasoPendienteDto>, t: Throwable) {
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
                        } else {
                            onError("Traspaso creado pero no se recibió ID")
                        }
                    } else {
                        val msg = response.errorBody()?.string() ?: "Error ${response.code()}"
                        Log.e("MOVER_PALET", "❌ Error moverPalet: $msg")
                        onError("Error ${response.code()}: $msg")
                    }
                }

                override fun onFailure(call: Call<MoverPaletResponse>, t: Throwable) {
                    Log.e("MOVER_PALET", "❌ Error de red moverPalet: ${t.message}")
                    onError("Error de red: ${t.message}")
                }
            })
    }

    fun finalizarTraspasoPalet(
        traspasoId: String,
        dto: FinalizarTraspasoPaletDto,
        onSuccess: () -> Unit,
        onError: (String) -> Unit
    ) {
        Log.d("FINALIZAR_TRASPASO_PALET", "📡 Llamando a endpoint con dto: $dto")
        ApiManager.traspasosApi.finalizarTraspasoPalet(traspasoId, dto)
            .enqueue(object : Callback<Void> {
                override fun onResponse(call: Call<Void>, response: Response<Void>) {
                    Log.d("FINALIZAR_TRASPASO_PALET", "📬 Código HTTP: ${response.code()}")
                    if (response.isSuccessful) {
                        Log.d("FINALIZAR_TRASPASO_PALET", "✅ Traspaso finalizado correctamente")
                        onSuccess()
                    } else {
                        val error = response.errorBody()?.string()
                        Log.e("FINALIZAR_TRASPASO_PALET", "❌ Error ${response.code()}: $error")
                        onError("Error ${response.code()}: ${error ?: "desconocido"}")
                    }
                }

                override fun onFailure(call: Call<Void>, t: Throwable) {
                    Log.e("FINALIZAR_TRASPASO_PALET", "❌ Error de red: ${t.message}")
                    onError("Error de red: ${t.message}")
                }
            })
    }

    fun finalizarTraspasoPaletPorPaletId(
        paletId: String,
        dto: FinalizarTraspasoPaletDto,
        onSuccess: () -> Unit,
        onError: (String) -> Unit
    ) {
        ApiManager.traspasosApi.finalizarTraspasoPaletPorPaletId(paletId, dto)
            .enqueue(object : retrofit2.Callback<Void> {
                override fun onResponse(call: retrofit2.Call<Void>, response: retrofit2.Response<Void>) {
                    if (response.isSuccessful) {
                        onSuccess()
                    } else {
                        val error = response.errorBody()?.string()
                        onError("Error ${response.code()}: ${error ?: "desconocido"}")
                    }
                }
                override fun onFailure(call: retrofit2.Call<Void>, t: Throwable) {
                    onError("Error de red: ${t.message}")
                }
            })
    }

}
