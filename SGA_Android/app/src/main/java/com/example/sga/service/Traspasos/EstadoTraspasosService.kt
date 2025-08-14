package com.example.sga.service.Traspasos

import android.content.Context
import android.os.Handler
import android.os.Looper
import android.util.Log
import android.widget.Toast
import com.example.sga.data.ApiManager
import kotlinx.coroutines.*
import retrofit2.Call
import retrofit2.Callback
import retrofit2.Response

object EstadoTraspasosService {

    private var checkerJob: Job? = null
    private val estadosPrevios = mutableMapOf<String, String>()

    fun iniciar(usuarioId: Int, context: Context) {
        if (checkerJob?.isActive == true) return

        checkerJob = CoroutineScope(Dispatchers.IO).launch {
            while (isActive) {
                delay(60_000L) // cada 60 segundos

                ApiManager.traspasosApi.obtenerEstadosTraspasosPorUsuario(usuarioId)
                    .enqueue(object : Callback<List<TraspasoEstadoDto>> {
                        override fun onResponse(
                            call: Call<List<TraspasoEstadoDto>>,
                            response: Response<List<TraspasoEstadoDto>>
                        ) {
                            if (!response.isSuccessful) return

                            val lista = response.body().orEmpty()

                            // Agrupar por codigoPalet si existe, si no por id
                            val agrupados = lista.groupBy {
                                it.codigoPalet?.takeIf { it.isNotBlank() } ?: it.id
                            }

                            for ((clave, traspasos) in agrupados) {
                                val estadoPrevio = traspasos.minOfOrNull { estadosPrevios[it.id] ?: "" } ?: ""
                                val estadoActual = traspasos.minOfOrNull { it.codigoEstado } ?: ""

                                if (estadoPrevio == "PENDIENTE_ERP" && estadoActual == "COMPLETADO") {
                                    val codigo = traspasos.firstNotNullOfOrNull {
                                        it.codigoPalet?.takeIf { it.isNotBlank() }
                                    } ?: traspasos.firstNotNullOfOrNull {
                                        it.codigoArticulo?.takeIf { it.isNotBlank() }
                                    } ?: "desconocido"

                                    mostrarNotificacion(context, "✅ Traspaso $codigo completado correctamente", clave.hashCode())

                                }

                                if (estadoPrevio == "PENDIENTE_ERP" && estadoActual == "ERROR_ERP") {
                                    val codigo = traspasos.firstNotNullOfOrNull {
                                        it.codigoPalet?.takeIf { it.isNotBlank() }
                                    } ?: traspasos.firstNotNullOfOrNull {
                                        it.codigoArticulo?.takeIf { it.isNotBlank() }
                                    } ?: "desconocido"

                                    mostrarNotificacion(context, "❌ Traspaso $codigo ha fallado. Revisa el stock.", clave.hashCode())
                                }

                                // Guardamos estado actual para todos los IDs del grupo
                                for (traspaso in traspasos) {
                                    estadosPrevios[traspaso.id] = traspaso.codigoEstado
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
        estadosPrevios.clear()
    }

    private fun mostrarNotificacion(context: Context, mensaje: String, id: Int) {
        crearCanalNotificaciones(context)

        val builder = androidx.core.app.NotificationCompat.Builder(context, "traspasos_estado")
            .setSmallIcon(android.R.drawable.stat_notify_sync)
            .setContentTitle("Estado del traspaso")
            .setContentText(mensaje)
            .setPriority(androidx.core.app.NotificationCompat.PRIORITY_DEFAULT)
            .setAutoCancel(true)

        val notificationManager =
            context.getSystemService(Context.NOTIFICATION_SERVICE) as android.app.NotificationManager
        notificationManager.notify(id, builder.build())

        // 🟡 También mostramos un Toast si la app está abierta
        Handler(Looper.getMainLooper()).post {
            Toast.makeText(context, mensaje, Toast.LENGTH_LONG).show()
        }
    }

    private fun crearCanalNotificaciones(context: Context) {
        if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.O) {
            val canal = android.app.NotificationChannel(
                "traspasos_estado",
                "Notificaciones de Traspasos",
                android.app.NotificationManager.IMPORTANCE_DEFAULT
            ).apply {
                description = "Avisos cuando un traspaso es completado o falla"
                enableLights(false)
                enableVibration(false)
                setSound(null, null)
            }

            val manager = context.getSystemService(Context.NOTIFICATION_SERVICE) as android.app.NotificationManager
            manager.createNotificationChannel(canal)
        }
    }

    data class TraspasoEstadoDto(
        val id: String,
        val codigoEstado: String,
        val codigoPalet: String?,
        val codigoArticulo: String?
    )
}
