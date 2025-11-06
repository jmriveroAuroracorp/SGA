package com.example.sga.view.conversion

import android.util.Log
import com.example.sga.data.ApiManager
import com.example.sga.data.dto.almacenes.AlmacenDto
import com.example.sga.data.dto.almacenes.AlmacenesAutorizadosDto
import com.example.sga.data.dto.stock.ArticuloDto
import com.example.sga.data.dto.traspasos.LineaPaletCrearDto
import com.example.sga.data.dto.traspasos.LineaPaletDto
import com.example.sga.data.dto.traspasos.PaletDto
import com.example.sga.data.dto.traspasos.ValidarUbicacionResponse
import com.example.sga.utils.SoundUtils
import retrofit2.Call
import retrofit2.Callback
import retrofit2.Response

class ConversionLogic {

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

    fun procesarCodigoEscaneado(
        code: String,
        empresaId: Short,
        codigoAlmacen: String?,
        codigoCentro: String?,
        almacen: String?,
        onUbicacionDetectada: (String, String) -> Unit,
        onPaletDetectado: (PaletDto) -> Unit,
        onError: (String) -> Unit
    ) {
        val cleanCode = code.trim()

        // 1. Verificar si es una ubicación (formato: ALM$UBIC) - igual que TraspasosLogic
        val ubicRegex = Regex("""^([^$]+)\$([^$]+)$""")   // ej. 201$UB001
        ubicRegex.matchEntire(cleanCode)?.let { m ->
            val codAlm = m.groupValues[1].trim()
            val codUbi = m.groupValues[2].trim()
            onUbicacionDetectada(codAlm, codUbi)
            SoundUtils.getInstance().playSuccessSound()
            return
        }

        // 2. Verificar si es un código GS1 de palet (formato: 00XXXXXXXXXXXXXXXXXX)
        val gs1Regex = Regex("""^00(\d{18})""")
        val gs1Match = gs1Regex.find(cleanCode)
        
        if (gs1Match != null) {
            val gs1 = gs1Match.groupValues[1]
            obtenerPaletPorGS1(
                gs1 = gs1,
                onSuccess = { palet ->
                    onPaletDetectado(palet)
                    SoundUtils.getInstance().playSuccessSound()
                },
                onError = { error ->
                    onError(error)
                    SoundUtils.getInstance().playErrorSound()
                }
            )
            return
        }

        // 3. Si no es ubicación ni GS1, error
        onError("No se encontró ubicación o palet GS1 con el código: $cleanCode")
        SoundUtils.getInstance().playErrorSound()
    }

    private fun obtenerPaletPorGS1(
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
                        }
                    } else {
                        onError("Error ${response.code()}: Palet no encontrado")
                    }
                }

                override fun onFailure(call: Call<PaletDto>, t: Throwable) {
                    onError("Error de red: ${t.message}")
                }
            })
    }


    private fun buscarArticuloPorCodigo(
        codigoEmpresa: Short,
        codigoArticulo: String,
        codigoAlmacen: String?,
        codigoCentro: String?,
        almacen: String?,
        onSuccess: (ArticuloDto) -> Unit,
        onError: (String) -> Unit
    ) {
        ApiManager.etiquetasApiService.buscarArticulo(
            codigoEmpresa = codigoEmpresa,
            codigoArticulo = codigoArticulo,
            codigoAlmacen = codigoAlmacen,
            codigoCentro = codigoCentro,
            almacen = almacen
        ).enqueue(object : Callback<List<ArticuloDto>> {
            override fun onResponse(
                call: Call<List<ArticuloDto>>,
                response: Response<List<ArticuloDto>>
            ) {
                if (response.isSuccessful) {
                    val articulos = response.body().orEmpty()
                    if (articulos.isNotEmpty()) {
                        onSuccess(articulos.first())
                    } else {
                        onError("Artículo no encontrado")
                    }
                } else {
                    onError("Error ${response.code()}: ${response.errorBody()?.string()}")
                }
            }

            override fun onFailure(call: Call<List<ArticuloDto>>, t: Throwable) {
                onError("Error de red: ${t.message}")
            }
        })
    }

    fun convertirLinea(
        lineaOriginal: LineaPaletDto,
        nuevoCodigoArticulo: String,
        cantidadAConvertir: Double,
        empresaId: Short,
        usuarioId: Int,
        onSuccess: () -> Unit,
        onError: (String) -> Unit
    ) {
        val TAG = "CONVERSION"
        Log.d(TAG, "🔄 Iniciando conversión: ${lineaOriginal.codigoArticulo} -> $nuevoCodigoArticulo (${cantidadAConvertir}/${lineaOriginal.cantidad})")

        val cantidadRestante = lineaOriginal.cantidad - cantidadAConvertir

        // Crear nueva línea con el artículo convertido
        val nuevaLinea = LineaPaletCrearDto(
            codigoEmpresa = empresaId,
            codigoArticulo = nuevoCodigoArticulo,
            descripcion = null, // Se obtendrá del backend
            lote = lineaOriginal.lote,
            fechaCaducidad = lineaOriginal.fechaCaducidad,
            cantidad = cantidadAConvertir,
            codigoAlmacen = lineaOriginal.codigoAlmacen ?: "",
            ubicacion = lineaOriginal.ubicacion,
            usuarioId = usuarioId,
            observaciones = "Convertido desde ${lineaOriginal.codigoArticulo}",
            paletIdOrigen = null
        )

        // Añadir la nueva línea
        ApiManager.traspasosApi.añadirLineaPalet(lineaOriginal.idPalet, nuevaLinea)
            .enqueue(object : Callback<LineaPaletDto> {
                override fun onResponse(
                    call: Call<LineaPaletDto>,
                    response: Response<LineaPaletDto>
                ) {
                    if (response.isSuccessful) {
                        Log.d(TAG, "✅ Nueva línea creada con artículo: $nuevoCodigoArticulo")
                        
                        // Si la cantidad convertida es la total, eliminar línea original
                        if (cantidadRestante <= 0) {
                            Log.d(TAG, "🗑️ Eliminando línea original (conversión total)")
                            eliminarLineaPalet(
                                lineaId = lineaOriginal.id,
                                usuarioId = usuarioId,
                                onSuccess = {
                                    Log.d(TAG, "✅ Conversión completada: línea original eliminada")
                                    SoundUtils.getInstance().playSuccessSound()
                                    onSuccess()
                                },
                                onError = { error ->
                                    Log.e(TAG, "❌ Error al eliminar línea original: $error")
                                    onError("Nueva línea creada pero no se pudo eliminar la original: $error")
                                }
                            )
                        } else {
                            // Si es conversión parcial, modificar la cantidad de la línea original
                            Log.d(TAG, "📝 Modificando línea original (conversión parcial: resta $cantidadRestante)")
                            modificarCantidadLinea(
                                lineaId = lineaOriginal.id,
                                paletId = lineaOriginal.idPalet,
                                cantidadNueva = cantidadRestante,
                                empresaId = empresaId,
                                usuarioId = usuarioId,
                                lineaOriginal = lineaOriginal,
                                onSuccess = {
                                    Log.d(TAG, "✅ Conversión completada: línea original actualizada")
                                    SoundUtils.getInstance().playSuccessSound()
                                    onSuccess()
                                },
                                onError = { error ->
                                    Log.e(TAG, "❌ Error al modificar línea original: $error")
                                    onError("Nueva línea creada pero no se pudo actualizar la original: $error")
                                }
                            )
                        }
                    } else {
                        val errorMsg = "Error ${response.code()}: ${response.errorBody()?.string()}"
                        Log.e(TAG, "❌ Error al crear nueva línea: $errorMsg")
                        SoundUtils.getInstance().playErrorSound()
                        onError(errorMsg)
                    }
                }

                override fun onFailure(call: Call<LineaPaletDto>, t: Throwable) {
                    val errorMsg = "Error de red: ${t.message}"
                    Log.e(TAG, "💥 Fallo al crear nueva línea: $errorMsg")
                    SoundUtils.getInstance().playErrorSound()
                    onError(errorMsg)
                }
            })
    }

    private fun eliminarLineaPalet(
        lineaId: String,
        usuarioId: Int,
        onSuccess: () -> Unit,
        onError: (String) -> Unit
    ) {
        ApiManager.traspasosApi.eliminarLineaPalet(lineaId, usuarioId)
            .enqueue(object : Callback<Void> {
                override fun onResponse(call: Call<Void>, response: Response<Void>) {
                    if (response.isSuccessful) {
                        SoundUtils.getInstance().playSuccessSound()
                        onSuccess()
                    } else {
                        SoundUtils.getInstance().playErrorSound()
                        onError("Error ${response.code()}")
                    }
                }

                override fun onFailure(call: Call<Void>, t: Throwable) {
                    SoundUtils.getInstance().playErrorSound()
                    onError("Error: ${t.message}")
                }
            })
    }

    fun cargarAlmacenesPermitidos(
        user: com.example.sga.data.model.user.User,
        empresaId: Int,
        onSuccess: (List<String>) -> Unit,
        onError: (String) -> Unit
    ) {
        val dto = AlmacenesAutorizadosDto(
            codigoEmpresa = empresaId,
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
                        val lista = response.body().orEmpty()
                        // Incluir tanto almacenes específicos como del centro
                        val almacenesEspecificos = user.codigosAlmacen
                        val almacenesDelCentro = lista.filter { it.esDelCentro }.map { it.codigoAlmacen }
                        val almacenesPermitidos = (almacenesEspecificos + almacenesDelCentro).distinct()
                        onSuccess(almacenesPermitidos)
                    } else {
                        onError("Error al cargar almacenes: ${response.code()}")
                    }
                }

                override fun onFailure(call: Call<List<AlmacenDto>>, t: Throwable) {
                    onError("Error de red: ${t.message}")
                }
            })
    }

    fun obtenerUbicacionDePalet(
        idPalet: String,
        onResult: (almacen: String, ubicacion: String) -> Unit,
        onError: (String) -> Unit
    ) {
        Log.d("OBTENER_UBICACION", "🔍 Buscando ubicación del palet ID: $idPalet")
        ApiManager.traspasosApi.obtenerPaletsMovibles()
            .enqueue(object : Callback<List<com.example.sga.data.dto.traspasos.PaletMovibleDto>> {
                override fun onResponse(
                    call: Call<List<com.example.sga.data.dto.traspasos.PaletMovibleDto>>,
                    response: Response<List<com.example.sga.data.dto.traspasos.PaletMovibleDto>>
                ) {
                    if (response.isSuccessful) {
                        val palets = response.body() ?: emptyList()
                        Log.d("OBTENER_UBICACION", "📦 Palets movibles encontrados: ${palets.size}")
                        palets.forEach { p ->
                            Log.d("OBTENER_UBICACION", "   - ID: ${p.id}, Almacen: ${p.almacenOrigen}, Ubicacion: ${p.ubicacionOrigen}")
                        }
                        
                        val palet = palets.find { it.id.equals(idPalet, ignoreCase = true) }
                        if (palet != null) {
                            Log.d("OBTENER_UBICACION", "✅ Palet encontrado: ${palet.almacenOrigen} - ${palet.ubicacionOrigen}")
                            onResult(palet.almacenOrigen.trim().uppercase(), palet.ubicacionOrigen.trim().uppercase())
                        } else {
                            Log.e("OBTENER_UBICACION", "❌ Palet $idPalet no encontrado en la lista")
                            onError("Palet no encontrado en el sistema.")
                        }
                    } else {
                        Log.e("OBTENER_UBICACION", "❌ Error HTTP: ${response.code()}")
                        onError("Error al obtener ubicación: código ${response.code()}")
                    }
                }

                override fun onFailure(call: Call<List<com.example.sga.data.dto.traspasos.PaletMovibleDto>>, t: Throwable) {
                    Log.e("OBTENER_UBICACION", "❌ Error de red: ${t.message}")
                    onError("Error de red: ${t.message}")
                }
            })
    }

    fun validarUbicacionDePalet(
        palet: PaletDto,
        ubicacionEscaneada: Pair<String, String>,
        onValidado: () -> Unit,
        onError: (String) -> Unit
    ) {
        val TAG = "VALIDAR_PALET_CONVERSION"

        val almacenEscaneado = ubicacionEscaneada.first.trim().uppercase()
        val ubicEscaneadaNorm = ubicacionEscaneada.second.trim().uppercase()

        // 🔎 Contexto de entrada
        Log.d(TAG, "▷ validarUbicacionDePalet() id=${palet.id}, codigo=${palet.codigoPalet}")
        Log.d(TAG, "   escaneado → almacen='$almacenEscaneado', ubicacion='$ubicEscaneadaNorm' (raw='${ubicacionEscaneada.first}'|'${ubicacionEscaneada.second}')")

        obtenerUbicacionDePalet(
            idPalet = palet.id,
            onResult = { almApi, ubiApi ->
                Log.d(TAG, "   backend  → almacen='$almApi', ubicacion='$ubiApi'")

                val coincideAlm = almApi == almacenEscaneado
                val coincideUbi = ubiApi == ubicEscaneadaNorm
                Log.d(TAG, "   comparación → almacenOK=$coincideAlm, ubicOK=$coincideUbi")

                if (!coincideAlm || !coincideUbi) {
                    Log.e(
                        TAG,
                        "❌ NO COINCIDE. escaneado=[$almacenEscaneado|$ubicEscaneadaNorm] vs backend=[$almApi|$ubiApi]"
                    )
                    onError("La ubicación escaneada y el palet escaneado no coinciden.")
                } else {
                    Log.d(TAG, "✅ Ubicación de palet VALIDADA")
                    onValidado()
                }
            },
            onError = { msg ->
                Log.e(TAG, "❌ Error obtenerUbicacionDePalet(id=${palet.id}): $msg")
                onError(msg)
            }
        )
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
                    onError("Error de red: ${t.message}")
                    SoundUtils.getInstance().playErrorSound()
                }
            })
    }

    fun obtenerPalet(
        idPalet: String,
        onSuccess: (com.example.sga.data.dto.traspasos.PaletDto) -> Unit,
        onError: (String) -> Unit
    ) {
        ApiManager.traspasosApi.obtenerPalet(idPalet)
            .enqueue(object : Callback<com.example.sga.data.dto.traspasos.PaletDto> {
                override fun onResponse(
                    call: Call<com.example.sga.data.dto.traspasos.PaletDto>,
                    response: Response<com.example.sga.data.dto.traspasos.PaletDto>
                ) {
                    if (response.isSuccessful) {
                        val palet = response.body()
                        if (palet != null) {
                            onSuccess(palet)
                        } else {
                            onError("Palet no encontrado")
                        }
                    } else {
                        onError("Error ${response.code()}")
                    }
                }

                override fun onFailure(
                    call: Call<com.example.sga.data.dto.traspasos.PaletDto>,
                    t: Throwable
                ) {
                    onError("Error de red: ${t.message}")
                }
            })
    }

    private fun modificarCantidadLinea(
        lineaId: String,
        paletId: String,
        cantidadNueva: Double,
        empresaId: Short,
        usuarioId: Int,
        lineaOriginal: LineaPaletDto,
        onSuccess: () -> Unit,
        onError: (String) -> Unit
    ) {
        // Primero eliminar la línea original
        eliminarLineaPalet(
            lineaId = lineaId,
            usuarioId = usuarioId,
            onSuccess = {
                // Luego crear una nueva línea con la cantidad modificada
                val lineaModificada = LineaPaletCrearDto(
                    codigoEmpresa = empresaId,
                    codigoArticulo = lineaOriginal.codigoArticulo,
                    descripcion = lineaOriginal.descripcion,
                    lote = lineaOriginal.lote,
                    fechaCaducidad = lineaOriginal.fechaCaducidad,
                    cantidad = cantidadNueva,
                    codigoAlmacen = lineaOriginal.codigoAlmacen ?: "",
                    ubicacion = lineaOriginal.ubicacion,
                    usuarioId = usuarioId,
                    observaciones = null,
                    paletIdOrigen = null
                )

                ApiManager.traspasosApi.añadirLineaPalet(paletId, lineaModificada)
                    .enqueue(object : Callback<LineaPaletDto> {
                        override fun onResponse(
                            call: Call<LineaPaletDto>,
                            response: Response<LineaPaletDto>
                        ) {
                            if (response.isSuccessful) {
                                SoundUtils.getInstance().playSuccessSound()
                                onSuccess()
                            } else {
                                SoundUtils.getInstance().playErrorSound()
                                onError("Error al recrear línea: ${response.code()}")
                            }
                        }

                        override fun onFailure(call: Call<LineaPaletDto>, t: Throwable) {
                            SoundUtils.getInstance().playErrorSound()
                            onError("Error de red al recrear línea: ${t.message}")
                        }
                    })
            },
            onError = onError
        )
    }
}

