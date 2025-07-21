package com.example.sga.view.configuracion

import android.util.Log
import androidx.compose.runtime.mutableStateOf
import com.example.sga.data.ApiManager
import com.example.sga.data.dto.etiquetas.ImpresoraDto
import com.example.sga.data.dto.login.ConfiguracionUsuarioPatchDto
import com.example.sga.view.app.SessionViewModel
import retrofit2.Call
import retrofit2.Callback
import retrofit2.Response

class ConfiguracionLogic(
    private val sessionViewModel: SessionViewModel
) {

    val impresoras = mutableStateOf<List<ImpresoraDto>>(emptyList())
    val impresoraSeleccionada = mutableStateOf<ImpresoraDto?>(null)

    fun cambiarEmpresa(codigoEmpresa: String) {
        val user = sessionViewModel.user.value
        val userId = user?.id?.toIntOrNull() ?: return

        sessionViewModel.setEmpresaSeleccionada(
            user.empresas.find { it.codigo.toString() == codigoEmpresa } ?: return
        )

        val dto = ConfiguracionUsuarioPatchDto(idEmpresa = codigoEmpresa)

        ApiManager.userApi.actualizarConfiguracionUsuario(userId, dto)
            .enqueue(object : Callback<Void> {
                override fun onResponse(call: Call<Void>, response: Response<Void>) {
                    Log.d("PATCH", "✅ Código de respuesta: ${response.code()}, body: ${response.body()}")
                }

                override fun onFailure(call: Call<Void>, t: Throwable) {
                    Log.e("PATCH", "❌ Error: ${t.message}")
                }
            })
    }

    fun cambiarImpresora(nueva: ImpresoraDto) {
        impresoraSeleccionada.value = nueva

        val user = sessionViewModel.user.value ?: return
        val userId = user.id.toIntOrNull() ?: return
        val empresaId = sessionViewModel.empresaSeleccionada.value?.codigo?.toString() ?: return

        val dto = ConfiguracionUsuarioPatchDto(
            idEmpresa = empresaId,
            impresora = nueva.nombre
        )

        ApiManager.userApi.actualizarConfiguracionUsuario(userId, dto)
            .enqueue(object : Callback<Void> {
                override fun onResponse(call: Call<Void>, response: Response<Void>) {
                    Log.d("PATCH", "✅ Impresora actualizada: ${nueva.nombre}")
                    sessionViewModel.actualizarImpresora(nueva.nombre)
                }

                override fun onFailure(call: Call<Void>, t: Throwable) {
                    Log.e("PATCH", "❌ Error al actualizar impresora: ${t.message}")
                }
            })
    }

    fun cargarImpresoras() {
        ApiManager.etiquetasApiService.getImpresoras()
            .enqueue(object : Callback<List<ImpresoraDto>> {
                override fun onResponse(
                    call: Call<List<ImpresoraDto>>,
                    response: Response<List<ImpresoraDto>>
                ) {
                    if (response.isSuccessful) {
                        val lista = response.body().orEmpty()
                        impresoras.value = lista

                        val actualNombre = sessionViewModel.impresoraSeleccionada.value
                        impresoraSeleccionada.value = lista.find { it.nombre == actualNombre }
                            ?: lista.firstOrNull()
                    } else {
                        Log.e("CONFIG", "❌ Error al obtener impresoras: ${response.code()}")
                    }
                }

                override fun onFailure(call: Call<List<ImpresoraDto>>, t: Throwable) {
                    Log.e("CONFIG", "❌ Error de red al obtener impresoras: ${t.message}")
                }
            })
    }

}
