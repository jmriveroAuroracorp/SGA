package com.example.sga.view.stock

import android.os.Build
import android.util.Log
import androidx.compose.runtime.mutableStateOf
import com.example.sga.data.ApiManager
import com.example.sga.data.dto.etiquetas.AlergenosDto
import com.example.sga.data.dto.etiquetas.ImpresoraDto
import com.example.sga.data.dto.etiquetas.LogImpresionDto
import com.example.sga.data.dto.stock.ArticuloDto
import com.example.sga.data.dto.traspasos.PaletDto
import com.example.sga.data.model.stock.Stock
import retrofit2.Call
import retrofit2.Callback
import retrofit2.Response
import java.time.LocalDate

class ImpresionLogic(private val onToast: (String) -> Unit) {

    /** Registra la impresión de la etiqueta del [stock]. */
    fun imprimirStock(
        empresaId   : Short,
        stock       : Stock,
        usuario     : String,
        dispositivoId: String,
        idImpresora : Int,
        idEtiqueta  : Int = 0,
        copias      : Int = 1
    ) {

        /* ---------- envío definitivo ---------- */
        fun registrar(ean13: String?, alergenos: String?) {

            val dto = LogImpresionDto(
                usuario             = usuario,
                dispositivo = dispositivoId,
                idImpresora         = idImpresora,
                etiquetaImpresa     = 0,
                codigoArticulo      = stock.codigoArticulo,
                descripcionArticulo = stock.descripcionArticulo ?: "",
                copias              = copias,
                codigoAlternativo   = ean13,
                fechaCaducidad      = stock.fechaCaducidad?.take(10),
                partida             = stock.partida?.takeIf { it.isNotBlank() },
                alergenos           = alergenos?.takeIf { it.isNotBlank() } ?: "",
                pathEtiqueta        = "\\\\Sage200\\mrh\\Servicios\\PrintCenter\\ETIQUETAS\\MMPP_MES.nlbl",
                tipoEtiqueta = 1
            )

            Log.i("IMPRESION", "Payload → $dto")

            ApiManager.etiquetasApiService.insertarLogImpresion(dto)
                .enqueue(object : Callback<LogImpresionDto> {
                    override fun onResponse(
                        call: Call<LogImpresionDto>,
                        response: Response<LogImpresionDto>
                    ) {
                        if (response.isSuccessful) {
                            onToast("✅ Impresión registrada")
                        } else {
                            val errorBody = response.errorBody()?.string()
                            val mensajeError = if (errorBody != null) {
                                // Limpiar comillas si viene entre comillas
                                errorBody.trim().removeSurrounding("\"")
                            } else {
                                "Error ${response.code()} al imprimir"
                            }
                            onToast("❌ $mensajeError")
                        }
                    }

                    override fun onFailure(call: Call<LogImpresionDto>, t: Throwable) {
                        onToast("💥 Fallo de red: ${t.message}")
                    }
                })
        }

        /* ---------- con EAN, pedimos alérgenos ---------- */
        fun conEan(ean13: String?) {
            // Validar que el artículo tenga EAN antes de imprimir
            if (ean13.isNullOrBlank()) {
                onToast("❌ El artículo no tiene EAN. No se puede imprimir sin EAN.")
                return
            }
            
            ApiManager.etiquetasApiService.getAlergenos(
                codigoEmpresa  = empresaId,
                codigoArticulo = stock.codigoArticulo
            ).enqueue(object : Callback<AlergenosDto> {
                override fun onResponse(
                    call: Call<AlergenosDto>,
                    response: Response<AlergenosDto>
                ) {
                    val txt = response.body()?.alergenos       // ← ❶ un único string
                    registrar(ean13, txt)                      // ← ❷ lo pasamos tal cual
                }

                override fun onFailure(call: Call<AlergenosDto>, t: Throwable) {
                    Log.w("IMPRESION", "Alergenos no disponibles: ${t.message}")
                    registrar(ean13, null)
                }
            })
        }

        /* ---------- 1º – comprobar/buscar EAN ---------- */
        stock.codigoArticulo
            .takeIf { it.length == 13 && it.all(Char::isDigit) }
            ?.let { conEan(it); return }

        ApiManager.stockApi.buscarArticulo(
            codigoEmpresa  = empresaId,
            codigoArticulo = stock.codigoArticulo
        ).enqueue(object : Callback<List<ArticuloDto>> {
            override fun onResponse(
                call: Call<List<ArticuloDto>>,
                response: Response<List<ArticuloDto>>
            ) {
                val ean13 = response.body()?.firstOrNull()?.codigoAlternativo
                conEan(ean13)
            }

            override fun onFailure(call: Call<List<ArticuloDto>>, t: Throwable) {
                Log.e("IMPRESION", "No se pudo recuperar el EAN: ${t.message}")
                conEan(null)
            }
        })
    }

    val impresoras   = mutableStateOf<List<ImpresoraDto>>(emptyList())
    val impresoraSel = mutableStateOf<ImpresoraDto?>(null)

    /** Descarga la lista de impresoras del backend y selecciona la primera. */
    fun cargarImpresoras() {
        ApiManager.etiquetasApiService.getImpresoras()      // endpoint ya existente :contentReference[oaicite:0]{index=0}
            .enqueue(object : Callback<List<ImpresoraDto>> {
                override fun onResponse(
                    call   : Call<List<ImpresoraDto>>,
                    resp   : Response<List<ImpresoraDto>>
                ) {
                    if (!resp.isSuccessful) return
                    impresoras.value   = resp.body().orEmpty()
                    impresoraSel.value = impresoraSel.value ?: impresoras.value.firstOrNull()
                }
                override fun onFailure(call: Call<List<ImpresoraDto>>, t: Throwable) {
                    Log.e("IMPRESION", "No se pudieron obtener impresoras: ${t.message}")
                    onToast("❌ Error al obtener impresoras")
                }
            })
    }

    /** Obtiene la información del palet e imprime su etiqueta. */
    fun imprimirPalet(
        paletId: String,
        usuario: String,
        dispositivoId: String,
        idImpresora: Int,
        copias: Int = 1
    ) {
        // Primero obtener el palet completo para tener el codigoGS1
        ApiManager.traspasosApi.obtenerPalet(paletId)
            .enqueue(object : Callback<PaletDto> {
                override fun onResponse(call: Call<PaletDto>, response: Response<PaletDto>) {
                    if (response.isSuccessful) {
                        response.body()?.let { palet ->
                            val dto = LogImpresionDto(
                                usuario = usuario,
                                dispositivo = dispositivoId,
                                idImpresora = idImpresora,
                                etiquetaImpresa = 0,
                                tipoEtiqueta = 2, // Tipo 2 para etiquetas de palet
                                copias = copias,
                                pathEtiqueta = "\\\\Sage200\\mrh\\Servicios\\PrintCenter\\ETIQUETAS\\PALET.nlbl",
                                codigoGS1 = palet.codigoGS1,
                                codigoPalet = palet.codigoPalet
                            )

                            Log.i("IMPRESION_PALET", "Payload → $dto")

                            ApiManager.etiquetasApiService.insertarLogImpresion(dto)
                                .enqueue(object : Callback<LogImpresionDto> {
                                    override fun onResponse(
                                        call: Call<LogImpresionDto>,
                                        response: Response<LogImpresionDto>
                                    ) {
                                        if (response.isSuccessful)
                                            onToast("✅ Etiqueta de palet enviada a impresión")
                                        else
                                            onToast("❌ Error ${response.code()} al imprimir palet")
                                    }

                                    override fun onFailure(call: Call<LogImpresionDto>, t: Throwable) {
                                        onToast("💥 Fallo de red: ${t.message}")
                                    }
                                })
                        } ?: onToast("❌ No se pudo obtener información del palet")
                    } else {
                        onToast("❌ Error ${response.code()} al obtener palet")
                    }
                }

                override fun onFailure(call: Call<PaletDto>, t: Throwable) {
                    Log.e("IMPRESION_PALET", "Error obteniendo palet: ${t.message}")
                    onToast("💥 Error al obtener palet: ${t.message}")
                }
            })
    }
}


