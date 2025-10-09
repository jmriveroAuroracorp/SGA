package com.example.sga.service.Traspasos

import android.app.NotificationChannel
import android.app.NotificationManager
import android.content.Context
import android.os.Build
import android.os.Handler
import android.os.Looper
import android.util.Log
import android.widget.Toast
import androidx.core.app.NotificationCompat
import com.example.sga.data.ApiManager
import kotlinx.coroutines.*
import retrofit2.Call
import retrofit2.Callback
import retrofit2.Response

object EstadoTraspasosService {

    private var checkerJob: Job? = null

    fun iniciar(usuarioId: Int, context: Context) {
        if (checkerJob?.isActive == true) {
            Log.d("EstadoTraspasosService", "🔄 Reiniciando servicio con nuevo contexto")
            detener()
        }

        Log.d("EstadoTraspasosService", "▶️ Iniciando servicio de comprobación de traspasos para usuario $usuarioId")

        checkerJob = CoroutineScope(Dispatchers.IO).launch {
            while (isActive) {
                delay(5_000L) // cada 5 segundos (ajusta si quieres)

                ApiManager.traspasosApi.obtenerEstadosTraspasosPorUsuario(usuarioId)
                    .enqueue(object : Callback<List<TraspasoEstadoDto>> {
                        override fun onResponse(
                            call: Call<List<TraspasoEstadoDto>>,
                            response: Response<List<TraspasoEstadoDto>>
                        ) {
                            if (!response.isSuccessful) {
                                Log.w("EstadoTraspasosService", "Respuesta no exitosa: ${response.code()}")
                                return
                            }

                            val lista = response.body().orEmpty()
                            if (lista.isEmpty()) return

                            // El API ya devuelve solo los no notificados y los marca como notificados.
                            for (t in lista) {
                                val codigo = when {
                                    !t.codigoPalet.isNullOrBlank() -> t.codigoPalet
                                    !t.codigoArticulo.isNullOrBlank() -> t.codigoArticulo
                                    else -> "desconocido"
                                }

                                when (t.codigoEstado.uppercase()) {
                                    "COMPLETADO" -> {
                                        mostrarNotificacion(
                                            context = context,
                                            titulo = "Estado del traspaso",
                                            mensaje = "✅ Traspaso $codigo completado correctamente",
                                            id = t.id.hashCode()
                                        )
                                    }
                                    "ERROR_ERP" -> {
                                        val detalle = t.comentario?.takeIf { it.isNotBlank() } ?: "Error en ERP."
                                        mostrarNotificacion(
                                            context = context,
                                            titulo = "Estado del traspaso",
                                            mensaje = "❌ Traspaso $codigo ha fallado. $detalle",
                                            id = t.id.hashCode()
                                        )
                                    }
                                    else -> {
                                        // No notificar otros estados
                                    }
                                }
                            }
                        }

                        override fun onFailure(call: Call<List<TraspasoEstadoDto>>, t: Throwable) {
                            Log.e("EstadoTraspasosService", "Error de red: ${t.message}")
                        }
                    })
            }
        }
    }

    fun detener() {
        checkerJob?.cancel()
        checkerJob = null
        Log.d("EstadoTraspasosService", "⏹️ Servicio detenido")
    }

    private fun mostrarNotificacion(context: Context, titulo: String, mensaje: String, id: Int) {
        crearCanalNotificaciones(context)

        val builder = NotificationCompat.Builder(context, "traspasos_estado")
            .setSmallIcon(android.R.drawable.stat_notify_sync)
            .setContentTitle(titulo)
            .setContentText(mensaje)
            .setPriority(NotificationCompat.PRIORITY_DEFAULT)
            .setAutoCancel(true)

        val notificationManager =
            context.getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
        notificationManager.notify(id, builder.build())

        // También mostramos un Toast si la app está abierta
        Handler(Looper.getMainLooper()).post {
            Toast.makeText(context, mensaje, Toast.LENGTH_LONG).show()
        }
    }

    private fun crearCanalNotificaciones(context: Context) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val canal = NotificationChannel(
                "traspasos_estado",
                "Notificaciones de Traspasos",
                NotificationManager.IMPORTANCE_DEFAULT
            ).apply {
                description = "Avisos cuando un traspaso es completado o falla"
                enableLights(false)
                enableVibration(false)
                setSound(null, null)
            }

            val manager = context.getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
            manager.createNotificationChannel(canal)
        }
    }

    data class TraspasoEstadoDto(
        val id: String,
        val codigoEstado: String,
        val codigoPalet: String?,
        val codigoArticulo: String?,
        val comentario: String?
    )
}
