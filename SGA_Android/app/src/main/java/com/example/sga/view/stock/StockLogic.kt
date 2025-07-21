package com.example.sga.view.stock

import android.util.Log
import androidx.compose.ui.text.input.TextFieldValue
import com.example.sga.data.ApiManager
import com.example.sga.data.dto.stock.ArticuloDto
import com.example.sga.data.dto.stock.StockDto
import com.example.sga.data.mapper.StockMapper
import retrofit2.Call
import retrofit2.Callback
import retrofit2.Response
import java.time.LocalDate

class StockLogic(
    private val stockViewModel: StockViewModel
) {

    private fun String?.clean(): String? =
        this?.trim()?.uppercase()?.takeIf { it.isNotEmpty() }

    fun consultarStock(
        codigoEmpresa : Int,
        codigoUbicacion: String? = null,
        codigoAlmacen : String? = null,
        codigoArticulo: String? = null,
        codigoCentro  : String? = null,
        almacen       : String? = null,
        partida       : String? = null,
    ) {
        stockViewModel.setCargando(true)

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
                    stockViewModel.setCargando(false)
                    return
                }

                val listaInicial = response.body().orEmpty()
                val codigoPrimero = listaInicial.firstOrNull()?.codigoArticulo

                if (codigoPrimero != null) {
                    /* 2ª llamada para traer la descripción (filtrada con stock) */
                    ApiManager.stockApi.buscarArticulo(
                        codigoEmpresa   = codigoEmpresa.toShort(),
                        codigoArticulo  = codigoPrimero,
                        codigoAlmacen   = codigoAlmacen.clean(),
                        codigoCentro    = codigoCentro.clean(),
                        almacen         = almacen.clean(),
                        partida         = partida.clean()
                    ).enqueue(object : Callback<List<ArticuloDto>> {
                        override fun onResponse(
                            call: Call<List<ArticuloDto>>,
                            response: Response<List<ArticuloDto>>
                        ) {
                            val descripcion = response.body()
                                ?.firstOrNull()
                                ?.descripcion ?: "Sin descripción"

                            val listaConDesc = listaInicial.map {
                                StockMapper.fromDto(it.copy(descripcionArticulo = descripcion))
                            }
                            stockViewModel.setResultado(listaConDesc)
                            stockViewModel.setError(null)
                            stockViewModel.setCargando(false)
                        }

                        override fun onFailure(call: Call<List<ArticuloDto>>, t: Throwable) {
                            stockViewModel.setResultado(listaInicial.map { StockMapper.fromDto(it) })
                            stockViewModel.setError("Descripción no disponible.")
                            stockViewModel.setCargando(false)
                        }
                    })
                } else {
                    /* No hay stock */
                    stockViewModel.setResultado(emptyList())
                    stockViewModel.setError("No se encontraron datos de stock.")
                    stockViewModel.setCargando(false)
                }
            }

            override fun onFailure(call: Call<List<StockDto>>, t: Throwable) {
                stockViewModel.setError("Error de conexión: ${t.localizedMessage}")
                stockViewModel.setCargando(false)
            }
        })
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

        if (code.startsWith("010") && code.length >= 20) {
            val ean13 = code.substring(3, 16)
            Log.d("ESCANEO", "📦 EAN extraído: $ean13")

            val ai15Index = code.indexOf("15", startIndex = 16)
            val ai10Index = code.indexOf("10", startIndex = 16)

            val fechaCaducidad = if (ai15Index != -1 && ai15Index + 8 <= code.length) {
                val fechaStr = code.substring(ai15Index + 2, ai15Index + 8)
                try {
                    LocalDate.parse("20${fechaStr.substring(0, 2)}-${fechaStr.substring(2, 4)}-${fechaStr.substring(4, 6)}")
                } catch (_: Exception) { null }
            } else null

            val partida = if (ai10Index != -1 && ai10Index + 2 < code.length) {
                code.substring(ai10Index + 2)
            } else null

            Log.d("ESCANEO", "📅 Fecha caducidad: $fechaCaducidad")
            Log.d("ESCANEO", "🔖 Partida: $partida")
            //stockViewModel.setPartidaSeleccionada(partida)
            buscarArticuloPorEAN(
                codigoEmpresa = empresaId,
                ean13 = ean13,
                codigoAlmacen = almacenSel.takeIf { it != "Todos" },
                partida = partida,
                onUnico = { codArticulo ->
                    onCodigoArticuloDetectado(TextFieldValue(codArticulo))
                    lanzarConsulta()
                },
                onMultiple = onMultipleArticulos,
                onError = { onError("❌ $it") }
            )

        } else {
            if (almacenSel.isNullOrBlank() || almacenSel == "Todos") {
                onError("⚠️ Selecciona un almacén para consultar por ubicación.")
                return
            }

            Log.d("ESCANEO", "📍 Ubicación escaneada: $code")
            onUbicacionDetectada(TextFieldValue(code))
            lanzarConsulta()
        }
    }

}

