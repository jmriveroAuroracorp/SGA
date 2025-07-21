package com.example.sga.view.pesaje

import com.example.sga.data.ApiManager
import com.example.sga.data.PesajeApiService
import com.example.sga.data.mapper.*
import com.example.sga.data.dto.pesajedto.Pesajedto
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import retrofit2.Call
import retrofit2.Callback
import retrofit2.Response
class PesajeLogic(
    private val viewModel: PesajeViewModel
) {
    fun onCodeScanned(code: String) {
        if (esGuid(code)) {
            buscarPesajePorAmasijo(code)
        } else {
            val partes = code.split("/")
            if (partes.size == 3) {
                buscarPesaje(
                    ejercicio = partes[0],
                    serie = partes[1],
                    numero = partes[2]
                )
            } else {
                viewModel.mostrarError("Formato QR no válido: $code")
            }
        }
    }

    fun onScanStart() {
        viewModel.empezarEscaneo()
    }

    private fun esGuid(code: String): Boolean {
        val guidRegex = Regex("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")
        return guidRegex.matches(code)
    }

    private fun buscarPesaje(ejercicio: String, serie: String, numero: String) {
        viewModel.empezarEscaneo(false)
        viewModel.mostrarError(null)

        ApiManager.pesajeApi.getPesaje(ejercicio, serie, numero)
            .enqueue(object : Callback<Pesajedto> {
                override fun onResponse(call: Call<Pesajedto>, response: Response<Pesajedto>) {
                    if (response.isSuccessful) {
                        val dto = response.body()
                        if (dto != null) {
                            val modelo = PesajeMapper.fromDto(
                                dto,
                                { ordenDto ->
                                    OrdenTrabajoMapper.fromDto(
                                        ordenDto,
                                        { amasijoDto ->
                                            AmasijoMapper.fromDto(
                                                amasijoDto,
                                                ComponenteMapper::fromDto
                                            )
                                        }
                                    )
                                }
                            )
                            viewModel.setResultado(modelo)
                        } else {
                            viewModel.mostrarError("Respuesta vacía del servidor")
                        }
                    } else {
                        viewModel.mostrarError("No se encontró información para ese código")
                    }
                }

                override fun onFailure(call: Call<Pesajedto>, t: Throwable) {
                    viewModel.mostrarError("Error de conexión: ${t.localizedMessage}")
                }
            })
    }
    private fun buscarPesajePorAmasijo(idAmasijo: String) {
        viewModel.empezarEscaneo(false)
        viewModel.mostrarError(null)

        ApiManager.pesajeApi.getPesajePorAmasijo(idAmasijo)
            .enqueue(object : Callback<Pesajedto> {
                override fun onResponse(call: Call<Pesajedto>, response: Response<Pesajedto>) {
                    if (response.isSuccessful) {
                        val dto = response.body()
                        if (dto != null) {
                            val modelo = PesajeMapper.fromDto(
                                dto,
                                { ordenDto ->
                                    OrdenTrabajoMapper.fromDto(
                                        ordenDto,
                                        { amasijoDto ->
                                            AmasijoMapper.fromDto(
                                                amasijoDto,
                                                ComponenteMapper::fromDto
                                            )
                                        }
                                    )
                                }
                            )
                            viewModel.setResultado(modelo)
                        } else {
                            viewModel.mostrarError("Respuesta vacía del servidor")
                        }
                    } else {
                        viewModel.mostrarError("No se encontró información para ese amasijo")
                    }
                }

                override fun onFailure(call: Call<Pesajedto>, t: Throwable) {
                    viewModel.mostrarError("Error de conexión: ${t.localizedMessage}")
                }
            })
    }
}

