package com.example.sga.view.stock

import android.content.Context
import android.os.Handler
import android.os.Looper
import android.util.Log
import androidx.compose.ui.text.TextRange
import androidx.compose.ui.text.input.TextFieldValue
import com.example.sga.data.ApiManager
import com.example.sga.data.dto.stock.ArticuloDto
import com.example.sga.data.dto.stock.StockDto
import com.example.sga.data.mapper.StockMapper
import com.example.sga.utils.SoundUtils
import retrofit2.Call
import retrofit2.Callback
import retrofit2.Response
import java.time.LocalDate

class StockLogic(
    private val stockViewModel: StockViewModel,
    private val context: Context
) {

    private fun String?.clean(): String? =
        this?.trim()?.uppercase()?.takeIf { it.isNotEmpty() }

    fun consultarStock(
        codigoEmpresa : Short,
        codigoUbicacion: String? = null,
        codigoAlmacen : String? = null,
        codigoArticulo: String? = null,
        codigoCentro  : String? = null,
        almacen       : String? = null,
        partida       : String? = null,
        onFinally     : (() -> Unit)? = null,   // limpieza UI (campo y foco)
    ) {
        stockViewModel.setCargando(true)

        fun finish() {
            // Ejecuta SIEMPRE en Main: modifica estados Compose sin riesgos
            Handler(Looper.getMainLooper()).post {
                stockViewModel.setCargando(false)
                try { onFinally?.invoke() } catch (_: Exception) {}
            }
        }

        // 🔍 LOG DE DEBUG - Parámetros enviados al backend
        Log.d("STOCK_PARAMS", """
            📡 Parámetros enviados al endpoint Stock/consulta-stock:
            🏢 codigoEmpresa: $codigoEmpresa
            📦 codigoArticulo: ${codigoArticulo.clean()}
            📍 codigoUbicacion: ${codigoUbicacion.clean()}
            🏬 codigoAlmacen: ${codigoAlmacen.clean()}
            🎯 codigoCentro: ${codigoCentro.clean()}
            🏭 almacen: ${almacen.clean()}
            📋 partida: ${partida.clean()}
            ═══════════════════════════════
        """.trimIndent())

        ApiManager.stockApi.consultarStock(
            codigoEmpresa   = codigoEmpresa,
            codigoUbicacion = codigoUbicacion.clean(),
            codigoAlmacen   = codigoAlmacen.clean(),
            codigoArticulo  = codigoArticulo.clean(),
            codigoCentro    = codigoCentro.clean(),
            almacen         = almacen.clean(),
            partida         = partida.clean()
        ).enqueue(object : Callback<List<StockDto>> {
            override fun onResponse(
                call: Call<List<StockDto>>,
                response: Response<List<StockDto>>
            ) {
                if (!response.isSuccessful) {
                    val msg = response.errorBody()?.string() ?: "Error desconocido"
                    stockViewModel.setError("Error ${response.code()}: $msg")
                    // 🔊 Sonido de error
                    SoundUtils.getInstance().playErrorSound()
                    finish(); return
                }

                val listaInicial = response.body().orEmpty()

                // 🔍 LOGS DE DEBUG - Stock recibido del backend
                Log.d("STOCK_CONSULTA", """
                    📡 Respuesta del backend:
                    🎯 Total registros: ${listaInicial.size}
                    📦 Almacenes encontrados: ${listaInicial.map { it.codigoAlmacen }.distinct()}
                    
                    📋 DETALLE DE STOCK:
                    ${listaInicial.take(5).joinToString("\n") { stock ->
                        "  • Art: ${stock.codigoArticulo} | Alm: ${stock.codigoAlmacen} | Disp: ${stock.disponible} | Ubi: ${stock.ubicacion}"
                    }}${if (listaInicial.size > 5) "\n  ... y ${listaInicial.size - 5} más" else ""}
                    ═══════════════════════════════
                """.trimIndent())

                if (listaInicial.isNotEmpty()) {
                    // Obtener descripciones para cada artículo individualmente
                    obtenerDescripcionesArticulos(
                        listaStock = listaInicial,
                        codigoEmpresa = codigoEmpresa.toShort(),
                        codigoAlmacen = codigoAlmacen.clean(),
                        codigoCentro = codigoCentro.clean(),
                        almacen = almacen.clean(),
                        partida = partida.clean(),
                        onSuccess = { listaConDescripciones ->
                            stockViewModel.setResultado(listaConDescripciones)
                            stockViewModel.setError(null)
                            // 🔊 Sonido de éxito
                            SoundUtils.getInstance().playSuccessSound()
                            finish()
                        },
                        onError = { errorMsg ->
                            // Si falla la obtención de descripciones, mostrar stock sin descripciones
                            val listaSinDesc = listaInicial.map { StockMapper.fromDto(it) }
                            stockViewModel.setResultado(listaSinDesc)
                            stockViewModel.setError("Stock obtenido pero descripciones no disponibles: $errorMsg")
                            // 🔊 Sonido de éxito (parcial) - tenemos stock aunque sin descripciones
                            SoundUtils.getInstance().playSuccessSound()
                            finish()
                        }
                    )
                } else {
                    stockViewModel.setResultado(emptyList())
                    stockViewModel.setError("No se encontraron datos de stock.")
                    // 🔊 Sonido de error - no hay stock
                    SoundUtils.getInstance().playErrorSound()
                    finish()
                }
            }

            override fun onFailure(call: Call<List<StockDto>>, t: Throwable) {
                stockViewModel.setError("Error de conexión: ${t.localizedMessage}")
                // 🔊 Sonido de error - fallo de conexión
                SoundUtils.getInstance().playErrorSound()
                finish()
            }
        })
    }

    private fun obtenerDescripcionesArticulos(
        listaStock: List<StockDto>,
        codigoEmpresa: Short,
        codigoAlmacen: String?,
        codigoCentro: String?,
        almacen: String?,
        partida: String?,
        onSuccess: (List<com.example.sga.data.model.stock.Stock>) -> Unit,
        onError: (String) -> Unit
    ) {
        val articulosUnicos = listaStock.map { it.codigoArticulo }.distinct()
        val descripcionesMap = mutableMapOf<String, String>()
        
        if (articulosUnicos.isEmpty()) {
            onSuccess(listaStock.map { StockMapper.fromDto(it) })
            return
        }

        // Función recursiva para procesar artículos secuencialmente
        fun procesarArticulo(index: Int) {
            if (index >= articulosUnicos.size) {
                // Todos los artículos procesados, mapear resultados
                val listaConDescripciones = listaStock.map { stockDto ->
                    val descripcion = descripcionesMap[stockDto.codigoArticulo] ?: "Sin descripción"
                    StockMapper.fromDto(stockDto.copy(descripcionArticulo = descripcion))
                }
                onSuccess(listaConDescripciones)
                return
            }

            val codigoArticulo = articulosUnicos[index]
            
            ApiManager.stockApi.buscarArticulo(
                codigoEmpresa = codigoEmpresa,
                codigoArticulo = codigoArticulo,
                codigoAlmacen = codigoAlmacen,
                codigoCentro = codigoCentro,
                almacen = almacen,
                partida = partida
            ).enqueue(object : Callback<List<ArticuloDto>> {
                override fun onResponse(
                    call: Call<List<ArticuloDto>>,
                    response: Response<List<ArticuloDto>>
                ) {
                    val descripcion = if (response.isSuccessful) {
                        response.body()?.firstOrNull()?.descripcion ?: "Sin descripción"
                    } else {
                        "Sin descripción"
                    }
                    descripcionesMap[codigoArticulo] = descripcion
                    
                    // Procesar el siguiente artículo
                    procesarArticulo(index + 1)
                }

                override fun onFailure(call: Call<List<ArticuloDto>>, t: Throwable) {
                    // En caso de error, usar descripción por defecto y continuar
                    descripcionesMap[codigoArticulo] = "Sin descripción"
                    Log.w("STOCK_DESC", "Error obteniendo descripción para $codigoArticulo: ${t.message}")
                    
                    // Procesar el siguiente artículo
                    procesarArticulo(index + 1)
                }
            })
        }

        // Iniciar el procesamiento secuencial
        procesarArticulo(0)
    }

    fun buscarArticuloPorEAN(
        codigoEmpresa : Short,
        ean13         : String,
        codigoAlmacen : String? = null,
        codigoCentro  : String? = null,
        almacen       : String? = null,
        partida       : String? = null,
        onUnico       : (String) -> Unit,
        onMultiple    : (List<ArticuloDto>) -> Unit,
        onError       : (String)  -> Unit
    ) {
        ApiManager.stockApi.buscarArticulo(
            codigoEmpresa    = codigoEmpresa,
            codigoAlternativo = ean13,
            codigoAlmacen    = codigoAlmacen,
            codigoCentro     = codigoCentro,
            almacen          = almacen,
            partida          = partida
        ).enqueue(object : Callback<List<ArticuloDto>> {
            override fun onResponse(
                call: Call<List<ArticuloDto>>,
                response: Response<List<ArticuloDto>>
            ) {
                Log.d("BUSQUEDA_ARTICULO", "📡 HTTP ${response.code()}")

                if (response.isSuccessful) {
                    val lista = response.body().orEmpty()
                    Log.d("BUSQUEDA_ARTICULO", "✅ Artículos encontrados (sin filtrar): ${lista.size}")
                    lista.forEach {
                        Log.d("BUSQUEDA_ARTICULO", "   → ${it.codigoArticulo} - ${it.descripcion}")
                    }

                    // 🔍 Filtrado manual por partida (normalizado sin guiones ni espacios)
                    val listaFiltrada = partida?.let { p ->
                        val pNorm = p.filter { it.isDigit() }  // “54-2403701” → “542403701”
                        lista.filter { art ->
                            art.partida?.filter { it.isDigit() } == pNorm
                        }
                    } ?: lista

                    //val candidatos = if (listaFiltrada.isNotEmpty()) listaFiltrada else lista
                    val candidatos = (if (listaFiltrada.isNotEmpty()) listaFiltrada else lista)
                        .distinctBy { "${it.codigoArticulo}-${it.descripcion}-${it.partida}" }
                    Log.d("BUSQUEDA_ARTICULO", "🎯 Candidatos tras filtro: ${candidatos.size}")

                    when (candidatos.size) {
                        0 -> {
                            Log.w("BUSQUEDA_ARTICULO", "⚠️ Sin artículos para ese EAN")
                            onError("No se encontró ningún artículo con ese código.")
                        }
                        1 -> onUnico(candidatos.first().codigoArticulo)
                        else -> onMultiple(candidatos)
                    }
                } else {
                    val errorBody = response.errorBody()?.string()
                    Log.e("BUSQUEDA_ARTICULO", "❌ Error HTTP ${response.code()}: $errorBody")
                    onError("Error ${response.code()}: $errorBody")
                }
            }

            override fun onFailure(call: Call<List<ArticuloDto>>, t: Throwable) {
                Log.e("BUSQUEDA_ARTICULO", "💥 Fallo de red: ${t.message}")
                onError("Error al buscar artículo: ${t.message}")
            }
        })
    }

    fun buscarArticuloPorDescripcion(
        codigoEmpresa: Short,
        descripcion: String,
        codigoAlmacen: String? = null,
        codigoCentro:  String? = null,
        almacen:       String? = null,
        partida:       String? = null,
        onUnico:      (String) -> Unit,
        onMultiple:   (List<ArticuloDto>) -> Unit,
        onError:      (String) -> Unit
    ) {
        ApiManager.stockApi.buscarArticulo(
            codigoEmpresa  = codigoEmpresa,
            descripcion    = descripcion,
            codigoAlmacen  = codigoAlmacen,
            codigoCentro   = codigoCentro,
            almacen        = almacen,
            partida        = partida
        ).enqueue(object : Callback<List<ArticuloDto>> {
            override fun onResponse(
                call: Call<List<ArticuloDto>>,
                response: Response<List<ArticuloDto>>
            ) {
                val lista = response.body().orEmpty()
                when (lista.size) {
                    0    -> onError("No se encontraron artículos con esa descripción.")
                    1    -> onUnico(lista.first().codigoArticulo)
                    else -> onMultiple(lista)
                }
            }

            override fun onFailure(call: Call<List<ArticuloDto>>, t: Throwable) {
                onError("Error al buscar artículos: ${t.message}")
            }
        })
    }

/*fun procesarCodigoEscaneado(
    code: String,
    almacenSel: String?,
    empresaId: Short,
    onCodigoArticuloDetectado: (TextFieldValue) -> Unit,
    onUbicacionDetectada: (TextFieldValue) -> Unit,
    onError: (String) -> Unit,
    lanzarConsulta: () -> Unit,
    onMultipleArticulos: (List<ArticuloDto>) -> Unit
) {
    Log.d("ESCANEO", "📷 Código recibido: $code")
    val trimmed = code.trim()

    // ——— 1) Código EAN13 ———
    if (trimmed.startsWith("010") && trimmed.length >= 20) {
        val ean13 = trimmed.substring(3, 16)
        Log.d("ESCANEO", "📦 EAN extraído: $ean13")

        val ai15Index = trimmed.indexOf("15", startIndex = 16)
        val ai10Index = trimmed.indexOf("10", startIndex = 16)

        val fechaCaducidad = if (ai15Index != -1 && ai15Index + 8 <= trimmed.length) {
            val fechaStr = trimmed.substring(ai15Index + 2, ai15Index + 8)
            try {
                LocalDate.parse("20${fechaStr.substring(0, 2)}-${fechaStr.substring(2, 4)}-${fechaStr.substring(4, 6)}")
            } catch (_: Exception) { null }
        } else null

        val partida = if (ai10Index != -1 && ai10Index + 2 < trimmed.length) {
            trimmed.substring(ai10Index + 2)
        } else null

        Log.d("ESCANEO", "📅 Fecha caducidad: $fechaCaducidad")
        Log.d("ESCANEO", "🔖 Partida: $partida")

        buscarArticuloPorEAN(
            codigoEmpresa = empresaId,
            ean13 = ean13,
            codigoAlmacen = almacenSel.takeIf { it != "Todos" },
            partida = partida,
            onUnico = {
                onCodigoArticuloDetectado(TextFieldValue(it))
                lanzarConsulta()
            },
            onMultiple = onMultipleArticulos,
            onError = { onError("❌ $it") }
        )
        return
    }
    // ——— 2) Ubicación con $ (p.ej. "PR$UB001013003004" o "213$UB001013003004") ———
    if (trimmed.contains('$')) {
        val parts = trimmed.split('$', limit = 2)
        val (almacenEscaneado, ubicacionEscaneada) = when {
            parts.size == 2 && parts[0].isNotBlank() -> parts[0] to parts[1]   // acepta PR, ALM01, 213, etc.
            parts.size == 2 -> null to parts[1]
            else -> null to trimmed.removePrefix("$")
        }

        Log.d("ESCANEO", "📍 Ubicación escaneada: $ubicacionEscaneada")
        almacenEscaneado?.let { Log.d("ESCANEO", "🏷️ Almacén escaneado: $it") }

        // Mantenemos el contrato: pasamos lo escaneado tal cual para que la UI lo procese
        onUbicacionDetectada(TextFieldValue(trimmed))
        lanzarConsulta()
        return
    }

    // ——— ) Para el resto de casos, exigir almacén seleccionado ———
    // Validar almacén solo si vamos a buscar por ubicación
    val esArticulo = trimmed.length in 4..25 && trimmed.all { it.isLetterOrDigit() }
    if (!esArticulo && (almacenSel.isNullOrBlank() || almacenSel == "Todos")) {
        onError("⚠️ Selecciona un almacén para consultar por ubicación.")
        return
    }

    // ——— 3) Código artículo directo ———
    if (trimmed.length in 4..25 && trimmed.all { it.isLetterOrDigit() }) {
        Log.d("ESCANEO", "🔍 Posible código de artículo: $trimmed")

        ApiManager.stockApi.buscarArticulo(
            codigoEmpresa = empresaId,
            codigoArticulo = trimmed,
            codigoAlmacen = almacenSel.takeIf { it != "Todos" }
        ).enqueue(object : Callback<List<ArticuloDto>> {
            override fun onResponse(call: Call<List<ArticuloDto>>, response: Response<List<ArticuloDto>>) {
                val lista = response.body().orEmpty()

                when (lista.size) {
                    0 -> onError("No se encontró ningún artículo con ese código.")
                    1 -> {
                        onCodigoArticuloDetectado(TextFieldValue(lista.first().codigoArticulo))
                        lanzarConsulta()
                    }
                    else -> onMultipleArticulos(lista)
                }
            }

            override fun onFailure(call: Call<List<ArticuloDto>>, t: Throwable) {
                onError("Error de red: ${t.message}")
            }
        })
    } else {
        onError("❌ Código no válido o formato no reconocido.")
    }
}*/
fun procesarCodigoEscaneado(
    code: String,
    almacenSel: String?,
    empresaId: Short,
    onCodigoArticuloDetectado: (TextFieldValue) -> Unit,
    onUbicacionDetectada: (TextFieldValue) -> Unit,
    onError: (String) -> Unit,
    lanzarConsulta: () -> Unit,
    onMultipleArticulos: (List<ArticuloDto>) -> Unit
) {
    Log.d("ESCANEO", "📷 Código recibido: $code")
    val trimmed = code.trim()
    if (trimmed.isEmpty()) {
        onError("❌ Código vacío.")
        return
    }

    // ——— 1) GS1 con EAN13 (AI 01) + opcionales AI 15/10 ———
    if (trimmed.startsWith("010") && trimmed.length >= 20) {
        val ean13 = trimmed.substring(3, 16)
        Log.d("ESCANEO", "📦 EAN extraído: $ean13")

        val ai15Index = trimmed.indexOf("15", startIndex = 16)
        val ai10Index = trimmed.indexOf("10", startIndex = 16)

        val fechaCaducidad = if (ai15Index != -1 && ai15Index + 8 <= trimmed.length) {
            val fechaStr = trimmed.substring(ai15Index + 2, ai15Index + 8)
            try {
                LocalDate.parse("20${fechaStr.substring(0, 2)}-${fechaStr.substring(2, 4)}-${fechaStr.substring(4, 6)}")
            } catch (_: Exception) { null }
        } else null

        val partida = if (ai10Index != -1 && ai10Index + 2 < trimmed.length) {
            trimmed.substring(ai10Index + 2)
        } else null

        Log.d("ESCANEO", "📅 Fecha caducidad: $fechaCaducidad")
        Log.d("ESCANEO", "🔖 Partida: $partida")

        buscarArticuloPorEAN(
            codigoEmpresa = empresaId,
            ean13 = ean13,
            codigoAlmacen = almacenSel.takeIf { it != "Todos" },
            partida = partida,
            onUnico = { cod ->
                // SUSTITUYE el campo artículo (no concatenar) y deja el cursor al final
                onCodigoArticuloDetectado(
                    TextFieldValue(text = cod, selection = TextRange(cod.length))
                )
                lanzarConsulta()
            },
            onMultiple = onMultipleArticulos,
            onError = { onError("❌ $it") }
        )
        return
    }

/*    // ——— 2) Ubicación con $ (p.ej. "PR$UB001013003004" o "$UB001...") ———
    if (trimmed.contains('$')) {
        // Antes de tocar ubicación, VACIAMOS el artículo para evitar acumulaciones/foco pegado
        onCodigoArticuloDetectado(TextFieldValue(""))

        // Enviamos tal cual para que la UI lo procese (mantengo tu contrato actual)
        onUbicacionDetectada(TextFieldValue(text = trimmed, selection = TextRange(trimmed.length)))
        lanzarConsulta()
        return
    }*/
    // ——— 2) Ubicación con $ (ALM$UB…, ALM$, o $UB…) ———
    if (trimmed.contains('$')) {
        // Antes de tocar ubicación, VACIAMOS el artículo para evitar acumulaciones/foco pegado
        onCodigoArticuloDetectado(TextFieldValue(""))

        val almUbRegex   = Regex("""^([^$]+)\$([^$]+)$""") // ALM$UB…  (incluye ALM$suelo)
        val soloAlmRegex = Regex("""^([^$]+)\$$""")        // ALM$     (sin ubicar → ubi = "")
        val soloUbRegex  = Regex("""^\$([^$]+)$""")        // $UB…

        // ✅ Caso normal: "ALM$UB..." o "ALM$suelo"
        if (almUbRegex.matchEntire(trimmed) != null) {
            onUbicacionDetectada(TextFieldValue(text = trimmed, selection = TextRange(trimmed.length)))
            lanzarConsulta()
            return
        }

        // ✅ Caso "ALM$" → ubicación SIN UBICAR (cadena vacía) ⇒ CONSULTAR
        if (soloAlmRegex.matchEntire(trimmed) != null) {
            onUbicacionDetectada(TextFieldValue(text = trimmed, selection = TextRange(trimmed.length)))
            lanzarConsulta()
            return
        }

        // ⚠️ Caso "$UB..." → compón con almacén seleccionado si existe; si no, avisar
        if (soloUbRegex.matchEntire(trimmed) != null) {
            val ub = soloUbRegex.matchEntire(trimmed)!!.groupValues[1].trim()
            val almSelVal = almacenSel?.takeIf { it.isNotBlank() && it != "Todos" }
            if (almSelVal == null) {
                onError("Selecciona un almacén o escanea en formato 'ALM\$UB...'.")
                return
            }
            val compuesto = "$almSelVal$$ub"
            onUbicacionDetectada(TextFieldValue(text = compuesto, selection = TextRange(compuesto.length)))
            lanzarConsulta()
            return
        }

        // 🟥 Cualquier otro patrón raro con '$'
        onError("❌ Formato de ubicación no válido.")
        return
    }

    // ——— 3) Ubicación simple "UB..." (requiere almacén seleccionado distinto de "Todos") ———
    val esUbicacionSimple = trimmed.startsWith("UB", ignoreCase = true) && trimmed.length >= 6
    if (esUbicacionSimple) {
        if (almacenSel.isNullOrBlank() || almacenSel == "Todos") {
            onError("⚠️ Selecciona un almacén para consultar por ubicación.")
            return
        }
        // Limpiamos el artículo y construimos el formato ALM$UB... para mantener homogeneidad
        onCodigoArticuloDetectado(TextFieldValue(""))
        val compuesto = "$almacenSel$$trimmed"
        onUbicacionDetectada(TextFieldValue(text = compuesto, selection = TextRange(compuesto.length)))
        lanzarConsulta()
        return
    }

    // ——— 4) Código artículo directo ———
    val esArticulo = trimmed.length in 4..25 && trimmed.all { it.isLetterOrDigit() }
    if (esArticulo) {
        Log.d("ESCANEO", "🔍 Posible código de artículo: $trimmed")

        ApiManager.stockApi.buscarArticulo(
            codigoEmpresa = empresaId,
            codigoArticulo = trimmed,
            codigoAlmacen = almacenSel.takeIf { it != "Todos" }
        ).enqueue(object : Callback<List<ArticuloDto>> {
            override fun onResponse(
                call: Call<List<ArticuloDto>>,
                response: Response<List<ArticuloDto>>
            ) {
                val lista = response.body().orEmpty()
                when (lista.size) {
                    0 -> onError("No se encontró ningún artículo con ese código.")
                    1 -> {
                        val cod = lista.first().codigoArticulo
                        onCodigoArticuloDetectado(TextFieldValue(text = cod, selection = TextRange(cod.length)))
                        lanzarConsulta()
                    }
                    else -> onMultipleArticulos(lista)
                }
            }

            override fun onFailure(call: Call<List<ArticuloDto>>, t: Throwable) {
                onError("Error de red: ${t.message}")
            }
        })
        return
    }

    // ——— 5) Formato no reconocido ———
    onError("❌ Código no válido o formato no reconocido.")
}

}

