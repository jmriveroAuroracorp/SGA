package com.example.sga.view.app

import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Build
import android.provider.Settings
import android.util.Log
import android.widget.Toast
import androidx.core.content.FileProvider
import com.example.sga.data.VersionApiService
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlin.system.exitProcess

class UpdateLogic(private val sessionViewModel: SessionViewModel) {

    private var reintentarDesdeAjustes: (() -> Unit)? = null

    fun setReintentoLanzador(callback: () -> Unit) {
        reintentarDesdeAjustes = callback
    }

    fun tienePermisoInstalacion(context: Context): Boolean {
        return if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            context.packageManager.canRequestPackageInstalls()
        } else {
            true
        }
    }

    fun pedirPermisoInstalacion(context: Context) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val intent = Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES).apply {
                data = Uri.parse("package:${context.packageName}")
                flags = Intent.FLAG_ACTIVITY_NEW_TASK
            }
            context.startActivity(intent)
        }
    }

    suspend fun comprobarYActualizar(context: Context, api: VersionApiService): Boolean {
        Log.d("SGA_UPDATE", "Entrando en comprobarYActualizar()")

        return try {
            val local = context.packageManager
                .getPackageInfo(context.packageName, 0).versionName ?: "0.0.0"
            Log.d("SGA_UPDATE", "Versión instalada: $local")

            val versionDto = api.getUltimaVersion()
            val remota = versionDto.version.trim()
            Log.d("SGA_UPDATE", "Versión en servidor: $remota")

            if (esVersionNueva(remota, local)) {
                if (!tienePermisoInstalacion(context)) {
                    Log.d("SGA_UPDATE", "Permiso de instalación no concedido. Pidiendo al usuario...")
                    Toast.makeText(context, "Debes permitir la instalación...", Toast.LENGTH_LONG).show()
                    reintentarDesdeAjustes?.invoke()
                    return false
                }

                Log.d("SGA_UPDATE", "Versión nueva detectada. Descargando APK...")
                val apkResponse = api.descargarAPK()
                val apkBytes = withContext(Dispatchers.IO) { apkResponse.bytes() }
                Log.d("SGA_UPDATE", "APK descargada. Lanzando instalador...")

                guardarYLanzarAPK(context, apkBytes, "SGA.apk")
                Log.d("SGA_UPDATE", "APK lanzada. Cerrando app.")
                exitProcess(0)
            } else {
                Log.d("SGA_UPDATE", "Ya está actualizada.")
            }

            true // puede continuar
        } catch (e: Exception) {
            Log.e("SGA_UPDATE", "Error durante la comprobación o descarga", e)
            true // seguimos aunque haya fallo, para no bloquear el login
        }
    }

    private fun esVersionNueva(remota: String, local: String): Boolean {
        val r = remota.split('.').map { it.toIntOrNull() ?: 0 }
        val l = local.split('.').map { it.toIntOrNull() ?: 0 }
        val max = maxOf(r.size, l.size)
        repeat(max) { i ->
            val rv = r.getOrElse(i) { 0 }
            val lv = l.getOrElse(i) { 0 }
            if (rv != lv) return rv > lv
        }
        return false
    }

    private fun guardarYLanzarAPK(context: Context, bytes: ByteArray, nombreArchivo: String) {
        Log.d("SGA_UPDATE", "Iniciando instalación. Android Version: ${Build.VERSION.SDK_INT}")
        
        // Usar siempre el método con Intent que abre el diálogo del instalador
        instalarConIntent(context, bytes, nombreArchivo)
    }
    
    private fun instalarConIntent(context: Context, bytes: ByteArray, nombreArchivo: String) {
        // Guardar el APK en disco
        val apkFile = java.io.File(context.getExternalFilesDir(null), nombreArchivo)
        
        if (apkFile.exists()) {
            apkFile.delete()
            Log.d("SGA_UPDATE", "APK antiguo eliminado")
        }

        try {
            apkFile.outputStream().use { outputStream ->
                outputStream.write(bytes)
                outputStream.flush()
                outputStream.fd.sync()
            }
            
            Log.d("SGA_UPDATE", "APK guardado: ${apkFile.absolutePath}")
            Log.d("SGA_UPDATE", "Tamaño: ${apkFile.length()} bytes")
            
            // Verificar integridad
            if (apkFile.length() != bytes.size.toLong()) {
                Log.e("SGA_UPDATE", "Error: archivo incompleto")
                Toast.makeText(context, "Error al guardar APK", Toast.LENGTH_LONG).show()
                return
            }
        } catch (e: Exception) {
            Log.e("SGA_UPDATE", "Error al escribir APK", e)
            Toast.makeText(context, "Error al guardar APK: ${e.message}", Toast.LENGTH_LONG).show()
            return
        }

        // Pequeño delay para Android 10+ asegurando que el archivo esté disponible
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            Thread.sleep(300)
            Log.d("SGA_UPDATE", "Delay aplicado para Android 10+")
        }

        val apkUri = try {
            FileProvider.getUriForFile(
                context,
                "${context.packageName}.provider",
                apkFile
            )
        } catch (e: Exception) {
            Log.e("SGA_UPDATE", "Error al crear URI", e)
            Toast.makeText(context, "Error al crear URI: ${e.message}", Toast.LENGTH_LONG).show()
            return
        }

        Log.d("SGA_UPDATE", "URI generado: $apkUri")

        // Otorgar permisos explícitos para Android 10+
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            try {
                context.grantUriPermission(
                    "com.google.android.packageinstaller",
                    apkUri,
                    Intent.FLAG_GRANT_READ_URI_PERMISSION
                )
                Log.d("SGA_UPDATE", "Permisos otorgados al instalador")
            } catch (e: Exception) {
                Log.w("SGA_UPDATE", "No se pudo otorgar permiso explícito: ${e.message}")
            }
        }

        val intent = Intent(Intent.ACTION_VIEW).apply {
            setDataAndType(apkUri, "application/vnd.android.package-archive")
            addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
            addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
        }

        try {
            context.startActivity(intent)
            Log.d("SGA_UPDATE", "Instalador lanzado con Intent")
        } catch (e: Exception) {
            Log.e("SGA_UPDATE", "Error al lanzar instalador", e)
            Toast.makeText(context, "Error al abrir instalador: ${e.message}", Toast.LENGTH_LONG).show()
        }
    }
}

