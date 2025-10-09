package com.example.sga.view.app

import android.content.Context
import android.provider.Settings
import android.util.Log
import com.example.sga.data.ApiManager
import com.example.sga.data.dto.login.DispositivoDto
import com.example.sga.data.dto.login.LogEventoDto
import com.example.sga.data.model.user.User
import retrofit2.Call
import retrofit2.Callback
import retrofit2.Response

class SessionLogic(private val sessionViewModel: SessionViewModel) {

    fun cerrarSesion(context: Context, onFinalizado: () -> Unit) {
        val user = sessionViewModel.user.value
        if (user == null) {
            sessionViewModel.clearSession()
            sessionViewModel.ocultarMensajeCaducidad()
            onFinalizado()
            return
        }
        // 👇 REGISTRAMOS EL LOG ANTES DE LIMPIAR LA SESIÓN
        registrarLogout(context, user)

        val idDispositivo = Settings.Secure.getString(
            context.contentResolver,
            Settings.Secure.ANDROID_ID
        )

        val dto = DispositivoDto(
            id = idDispositivo,
            tipo = "Android",
            idUsuario = user.id.toInt()
        )

        ApiManager.userApi.desactivarDispositivo(dto).enqueue(object : Callback<Void> {
            override fun onResponse(call: Call<Void>, response: Response<Void>) {
                Log.d("SGA_SESSION", "✅ Dispositivo desactivado correctamente.")
                sessionViewModel.resetVigilancia()
                sessionViewModel.clearSession()
                sessionViewModel.ocultarMensajeCaducidad()
                onFinalizado()
            }

            override fun onFailure(call: Call<Void>, t: Throwable) {
                Log.e("SGA_SESSION", "❌ Error al desactivar dispositivo: ${t.localizedMessage}")
                sessionViewModel.resetVigilancia()
                sessionViewModel.clearSession()
                sessionViewModel.ocultarMensajeCaducidad()
                onFinalizado()
            }
        })
    }

    fun registrarLogout(context: Context, user: User) {
        val idDispositivo = Settings.Secure.getString(
            context.contentResolver,
            Settings.Secure.ANDROID_ID
        )

        val log = LogEventoDto(
            IdUsuario = user.id.toInt(),
            IdDispositivo = idDispositivo,
            Tipo = "LOGOUT",
            Origen = "Logout",
            Descripcion = "Sesión cerrada",
            Detalle = "El usuario cerró sesión"
        )

        ApiManager.userApi.crearLogEvento(log).enqueue(object : Callback<Void> {
            override fun onResponse(call: Call<Void>, response: Response<Void>) {
                Log.d("SesionLogic", "✅ LOGOUT registrado: ${response.code()}")
            }

            override fun onFailure(call: Call<Void>, t: Throwable) {
                Log.e("SesionLogic", "❌ Error registrando LOGOUT: ${t.localizedMessage}")
            }
        })
    }

}