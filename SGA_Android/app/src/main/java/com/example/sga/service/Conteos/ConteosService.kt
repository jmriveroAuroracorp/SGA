package com.example.sga.service.Conteos

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

object ConteosService {

    private var checkerJob: Job? = null
    private var ordenesConocidas = mutableSetOf<String>() // IDs de órdenes de conteo que ya conocíamos
    private var codigoOperario: String = ""

    fun iniciar(codigoOperario: String, context: Context) {
        if (checkerJob?.isActive == true) {
            Log.d("ConteosService", "🔄 Reiniciando servicio con nuevo contexto")
            detener()
        }

        this.codigoOperario = codigoOperario

        Log.d("ConteosService", "▶️ Iniciando servicio de comprobación de conteos para operario $codigoOperario")

        checkerJob = CoroutineScope(Dispatchers.IO).launch {
            // Inicializar la lista de órdenes conocidas antes de empezar las verificaciones
            inicializarOrdenesConocidas()
            
            while (isActive) {
                //delay(5 * 60 * 1000L) // cada 5 minutos
                delay(1 * 15 * 1000L)
                verificarConteosActivos(context)
            }
        }
    }

    private suspend fun inicializarOrdenesConocidas() {
        try {
            Log.d("ConteosService", "🔧 Inicializando lista de órdenes conocidas...")
            
            val response = withContext(Dispatchers.IO) {
                ApiManager.conteosApi.listarOrdenes(codigoOperario)
            }
            
            val ordenesActivas = response.filter { orden ->
                orden.codigoOperario == codigoOperario && 
                (orden.estado == "ASIGNADO" || orden.estado == "EN_PROCESO")
            }
            
            // Inicializar la lista con las órdenes existentes sin notificar
            ordenesConocidas = ordenesActivas.map { it.guidID }.toMutableSet()
            
            Log.d("ConteosService", "✅ Inicialización completa. Órdenes conocidas: ${ordenesConocidas.size}")
            
        } catch (e: Exception) {
            Log.e("ConteosService", "❌ Error al inicializar órdenes conocidas: ${e.message}")
        }
    }

    private suspend fun verificarConteosActivos(context: Context) {
        try {
            Log.d("ConteosService", "🔍 Verificando conteos activos...")
            
            val response = withContext(Dispatchers.IO) {
                ApiManager.conteosApi.listarOrdenes(codigoOperario)
            }
            
            val ordenesActivas = response.filter { orden ->
                orden.codigoOperario == codigoOperario && 
                (orden.estado == "ASIGNADO" || orden.estado == "EN_PROCESO")
            }
            
            // Obtener IDs de las órdenes activas actuales
            val ordenesActuales = ordenesActivas.map { it.guidID }.toSet()
            
            Log.d("ConteosService", "📊 Órdenes de conteo activas encontradas: ${ordenesActuales.size}")
            Log.d("ConteosService", "📋 Órdenes conocidas: ${ordenesConocidas.size}")
            
            // Detectar nuevas órdenes (que no estaban en la lista anterior)
            val ordenesNuevas = ordenesActuales - ordenesConocidas
            
            if (ordenesNuevas.isNotEmpty()) {
                Log.d("ConteosService", "🆕 Nuevas órdenes de conteo detectadas: ${ordenesNuevas.size}")
                
                val mensaje = if (ordenesNuevas.size == 1) {
                    "Tienes 1 nueva orden de conteo"
                } else {
                    "Tienes ${ordenesNuevas.size} nuevas órdenes de conteo"
                }
                
                mostrarNotificacion(
                    context = context,
                    titulo = "Nueva Orden de Conteo",
                    mensaje = mensaje,
                    id = "nueva_orden_conteo".hashCode()
                )
            }
            
            // Actualizar la lista de órdenes conocidas
            ordenesConocidas = ordenesActuales.toMutableSet()
            
        } catch (e: Exception) {
            Log.e("ConteosService", "❌ Error al verificar conteos: ${e.message}")
        }
    }

    fun detener() {
        checkerJob?.cancel()
        checkerJob = null
        ordenesConocidas.clear()
        Log.d("ConteosService", "⏹️ Servicio de conteos detenido")
    }

    private fun mostrarNotificacion(context: Context, titulo: String, mensaje: String, id: Int) {
        crearCanalNotificaciones(context)

        val builder = NotificationCompat.Builder(context, "conteos_activos")
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
                "conteos_activos",
                "Notificaciones de Conteos",
                NotificationManager.IMPORTANCE_DEFAULT
            ).apply {
                description = "Avisos cuando hay nuevas órdenes de conteo"
                enableLights(false)
                enableVibration(false)
                setSound(null, null)
            }

            val manager = context.getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
            manager.createNotificationChannel(canal)
        }
    }
}
