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
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
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
                exitProcess(0) // <- no sigue, pero por completitud
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
        val apkFile = context.getExternalFilesDir(null)?.resolve(nombreArchivo)
        apkFile?.writeBytes(bytes)

        val apkUri = FileProvider.getUriForFile(
            context,
            "${context.packageName}.provider",
            apkFile!!
        )

        val intent = Intent(Intent.ACTION_VIEW).apply {
            setDataAndType(apkUri, "application/vnd.android.package-archive")
            addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
            addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
        }

        context.startActivity(intent)
    }
}

