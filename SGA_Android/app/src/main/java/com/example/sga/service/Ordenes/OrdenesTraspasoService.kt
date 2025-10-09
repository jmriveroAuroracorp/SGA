package com.example.sga.service.Ordenes

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

object OrdenesTraspasoService {

    private var checkerJob: Job? = null
    private var ordenesConocidas = mutableSetOf<String>() // IDs de órdenes que ya conocíamos
    private var usuarioId: Int = 0
    private var codigoEmpresa: Int = 1

    fun iniciar(usuarioId: Int, codigoEmpresa: Int, context: Context) {
        if (checkerJob?.isActive == true) {
            Log.d("OrdenesTraspasoService", "🔄 Reiniciando servicio con nuevo contexto")
            detener()
        }

        this.usuarioId = usuarioId
        this.codigoEmpresa = codigoEmpresa

        Log.d("OrdenesTraspasoService", "▶️ Iniciando servicio de comprobación de órdenes para usuario $usuarioId")

        checkerJob = CoroutineScope(Dispatchers.IO).launch {
            while (isActive) {
                delay(5 * 60 * 1000L) // cada 5 minutos

                verificarOrdenesActivas(context)
            }
        }
    }

    private suspend fun verificarOrdenesActivas(context: Context) {
        try {
            Log.d("OrdenesTraspasoService", "🔍 Verificando órdenes activas...")
            
            val response = ApiManager.ordenTraspasoApi.getOrdenesPorOperario(usuarioId, codigoEmpresa)
            
            if (response.isSuccessful) {
                val ordenes = response.body() ?: emptyList()
                val ordenesActivas = ordenes.filter { orden ->
                    orden.estado == "PENDIENTE" || orden.estado == "EN_PROCESO"
                }
                
                // Obtener IDs de las órdenes activas actuales
                val ordenesActuales = ordenesActivas.map { it.idOrdenTraspaso }.toSet()
                
                Log.d("OrdenesTraspasoService", "📊 Órdenes activas encontradas: ${ordenesActuales.size}")
                Log.d("OrdenesTraspasoService", "📋 Órdenes conocidas: ${ordenesConocidas.size}")
                
                // Detectar nuevas órdenes (que no estaban en la lista anterior)
                val ordenesNuevas = ordenesActuales - ordenesConocidas
                
                if (ordenesNuevas.isNotEmpty()) {
                    Log.d("OrdenesTraspasoService", "🆕 Nuevas órdenes detectadas: ${ordenesNuevas.size}")
                    
                    val mensaje = if (ordenesNuevas.size == 1) {
                        "Tienes 1 nueva orden de traspaso"
                    } else {
                        "Tienes ${ordenesNuevas.size} nuevas órdenes de traspaso"
                    }
                    
                    mostrarNotificacion(
                        context = context,
                        titulo = "Nueva Orden de Traspaso",
                        mensaje = mensaje,
                        id = "nueva_orden_traspaso".hashCode()
                    )
                }
                
                // Actualizar la lista de órdenes conocidas
                ordenesConocidas = ordenesActuales.toMutableSet()
                
            } else {
                Log.w("OrdenesTraspasoService", "❌ Error al verificar órdenes: ${response.code()}")
            }
        } catch (e: Exception) {
            Log.e("OrdenesTraspasoService", "❌ Error al verificar órdenes: ${e.message}")
        }
    }

    fun detener() {
        checkerJob?.cancel()
        checkerJob = null
        ordenesConocidas.clear()
        Log.d("OrdenesTraspasoService", "⏹️ Servicio de órdenes detenido")
    }

    private fun mostrarNotificacion(context: Context, titulo: String, mensaje: String, id: Int) {
        crearCanalNotificaciones(context)

        val builder = NotificationCompat.Builder(context, "ordenes_traspaso")
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
                "ordenes_traspaso",
                "Notificaciones de Órdenes",
                NotificationManager.IMPORTANCE_DEFAULT
            ).apply {
                description = "Avisos cuando hay nuevas órdenes de traspaso"
                enableLights(false)
                enableVibration(false)
                setSound(null, null)
            }

            val manager = context.getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
            manager.createNotificationChannel(canal)
        }
    }
}
