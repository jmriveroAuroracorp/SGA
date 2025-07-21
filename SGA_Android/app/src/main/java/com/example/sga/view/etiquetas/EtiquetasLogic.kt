package com.example.sga.view.etiquetas

import android.util.Log
import androidx.compose.ui.text.input.TextFieldValue
import com.example.sga.data.ApiManager
import com.example.sga.data.dto.etiquetas.AlergenosDto
import com.example.sga.data.dto.etiquetas.ImpresoraDto
import com.example.sga.data.dto.etiquetas.LogImpresionDto
import com.example.sga.data.dto.login.ConfiguracionUsuarioPatchDto
import com.example.sga.data.dto.stock.ArticuloDto
import com.example.sga.data.dto.stock.StockDto
import com.example.sga.data.mapper.StockMapper
import com.example.sga.data.model.stock.Stock

import com.example.sga.view.app.SessionViewModel
import retrofit2.Call
import retrofit2.Callback
import retrofit2.Response

class EtiquetasLogic(
    private val viewModel: EtiquetasViewModel,
    private val sessionViewModel: SessionViewModel
) {

    private fun String?.clean(): String? =
        this?.trim()?.uppercase()?.takeIf { it.isNotEmpty() }

    fun buscarArticuloPorDescripcion(
        descripcion: String,
        codigoEmpresa: Short,
        onUnico: (ArticuloDto) -> Unit,
        onMultiple: (List<ArticuloDto>) -> Unit,
        onError: (String) -> Unit
    ) {
        ApiManager.etiquetasApiService.buscarArticulo(
            codigoEmpresa = codigoEmpresa,
            descripcion = descripcion.clean()
        ).enqueue(object : Callback<List<ArticuloDto>> {
            override fun onResponse(
                call: Call<List<ArticuloDto>>,
                response: Response<List<ArticuloDto>>
            ) {
                if (response.isSuccessful) {
                    val lista = response.body().orEmpty()
                    when {
                        lista.isEmpty() -> onError("No se encontraron artículos")
                        lista.size == 1 -> onUnico(lista.first())
                        else -> onMultiple(lista)
                    }
                } else {
                    onError("Error al buscar artículos: ${response.code()}")
                }
            }

            override fun onFailure(call: Call<List<ArticuloDto>>, t: Throwable) {
                onError("Error al conectar con el servidor: ${t.message}")
            }
        })
    }
    fun buscarArticuloPorCodigo(
        codigoArticulo: String,
        codigoEmpresa: Short,
        onUnico: (ArticuloDto) -> Unit,
        onError: (String) -> Unit
    ) {
        ApiManager.etiquetasApiService.buscarArticulo(
            codigoEmpresa = codigoEmpresa,
            codigoArticulo = codigoArticulo
        ).enqueue(object : Callback<List<ArticuloDto>> {
            override fun onResponse(call: Call<List<ArticuloDto>>, response: Response<List<ArticuloDto>>) {
                if (response.isSuccessful) {
                    val lista = response.body().orEmpty()
                    when {
                        lista.isEmpty() -> onError("No se encontró ningún artículo")
                        lista.size == 1 -> onUnico(lista.first())
                        else -> onError("Múltiples resultados no soportados aún")
                    }
                } else {
                    onError("Error al buscar artículo: ${response.code()}")
                }
            }

            override fun onFailure(call: Call<List<ArticuloDto>>, t: Throwable) {
                onError("Fallo al conectar con el servidor: ${t.message}")
            }
        })
    }
    fun procesarCodigoEscaneado(
        code: String,
        empresaId: Short,
        onCodigoDetectado: (TextFieldValue) -> Unit,
        onMultipleArticulos: (List<ArticuloDto>) -> Unit,
        onError: (String) -> Unit
    ) {
        Log.d("ESCANEO", "📷 Código recibido: $code")

        if (code.startsWith("01") && code.length >= 15) {
            val ean13 = code.substring(3, 16)
            Log.d("ESCANEO", "📦 EAN extraído: $ean13")

            ApiManager.etiquetasApiService.buscarArticulo(
                codigoEmpresa = empresaId,
                codigoAlternativo = ean13
            ).enqueue(object : Callback<List<ArticuloDto>> {
                override fun onResponse(call: Call<List<ArticuloDto>>, response: Response<List<ArticuloDto>>) {
                    if (response.isSuccessful) {
                        val lista = response.body().orEmpty()
                        when {
                            lista.isEmpty() -> onError("No se encontró ningún artículo con el código escaneado.")
                            lista.size == 1 -> onCodigoDetectado(TextFieldValue(lista.first().codigoArticulo))
                            else -> onMultipleArticulos(lista)
                        }
                    } else {
                        onError("Error HTTP ${response.code()}")
                    }
                }

                override fun onFailure(call: Call<List<ArticuloDto>>, t: Throwable) {
                    onError("Fallo al buscar artículo: ${t.message}")
                }
            })
        } else {
            onError("El código escaneado no es un EAN válido")
        }
    }

    fun consultarStock(
        codigoEmpresa: Short,
        codigoArticulo: String,
        onSuccess: (List<Stock>) -> Unit,
        onError  : (String) -> Unit
    ) {

        ApiManager.stockApi.consultarStock(
            codigoEmpresa = codigoEmpresa.toInt(),   // ← el backend espera Int
            codigoArticulo = codigoArticulo
        ).enqueue(object : retrofit2.Callback<List<StockDto>> {

            override fun onResponse(
                call: retrofit2.Call<List<StockDto>>,
                response: retrofit2.Response<List<StockDto>>
            ) {
                if (response.isSuccessful) {
                    val lista = response.body().orEmpty()
                        .map(StockMapper::fromDto)      // ← convierte a modelo
                    onSuccess(lista)
                } else {
                    onError("Error al consultar stock: ${response.code()}")
                }
            }

            override fun onFailure(
                call: retrofit2.Call<List<StockDto>>,
                t: Throwable
            ) {
                onError("Fallo al consultar stock: ${t.message}")
            }
        })
    }
    fun obtenerAlergenos(
        codigoEmpresa: Short,
        codigoArticulo: String,
        onResult: (AlergenosDto?) -> Unit
    ) {
        ApiManager.etiquetasApiService.getAlergenos(
            codigoEmpresa,
            codigoArticulo
        ).enqueue(object : Callback<AlergenosDto> {
            override fun onResponse(
                call: Call<AlergenosDto>,
                response: Response<AlergenosDto>
            ) {
                onResult(response.body())
            }

            override fun onFailure(call: Call<AlergenosDto>, t: Throwable) {
                Log.e("ETIQ_API", "Error al obtener alérgenos", t)
                onResult(null)
            }
        })
    }

    fun obtenerImpresoras(
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

    fun enviarImpresion(dto: LogImpresionDto, onResult: (LogImpresionDto?) -> Unit) {
        Log.d("ETIQ_API", "📝 Enviando impresión: $dto")
        ApiManager.etiquetasApiService.insertarLogImpresion(dto)
            .enqueue(object : Callback<LogImpresionDto> {
                override fun onResponse(
                    call: Call<LogImpresionDto>,
                    response: Response<LogImpresionDto>
                ) {
                    if (response.isSuccessful) {
                        onResult(response.body())
                    } else {
                        Log.e("ETIQ_API", "Error HTTP: ${response.code()}")
                        onResult(null)
                    }
                }

                override fun onFailure(call: Call<LogImpresionDto>, t: Throwable) {
                    Log.e("ETIQ_API", "Fallo al imprimir", t)
                    onResult(null)
                }
            })
        onResult(dto)
    }
    fun actualizarImpresoraSeleccionadaEnBD(nombre: String) {
        val userId = sessionViewModel.user.value?.id?.toIntOrNull() ?: return
        val empresaId = sessionViewModel.empresaSeleccionada.value?.codigo?.toString() ?: return

        val dto = ConfiguracionUsuarioPatchDto(
            idEmpresa = empresaId,
            impresora = nombre
        )

        ApiManager.userApi.actualizarConfiguracionUsuario(userId, dto)
            .enqueue(object : Callback<Void> {
                override fun onResponse(call: Call<Void>, response: Response<Void>) {
                    Log.d("ETIQUETAS", "✅ Impresora actualizada en BD: $nombre")
                    sessionViewModel.actualizarImpresora(nombre)
                }

                override fun onFailure(call: Call<Void>, t: Throwable) {
                    Log.e("ETIQUETAS", "❌ Error al actualizar impresora: ${t.message}")
                }
            })
    }
}
