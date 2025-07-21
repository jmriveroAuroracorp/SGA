package com.example.sga.service.version

import android.content.Context
import android.content.Intent
import android.net.Uri
import androidx.core.content.FileProvider
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.ResponseBody
import okio.buffer
import okio.sink
import java.io.File

suspend fun guardarYLanzarAPK(context: Context, body: ResponseBody, nombreArchivo: String) {
    val archivo = withContext(Dispatchers.IO) {
        val file = File(context.getExternalFilesDir(null), nombreArchivo)
        val sink = file.sink().buffer()
        sink.writeAll(body.source())
        sink.close()
        file
    }

    val uri: Uri = FileProvider.getUriForFile(
        context,
        "${context.packageName}.provider",
        archivo
    )

    withContext(Dispatchers.Main) {
        val intent = Intent(Intent.ACTION_VIEW).apply {
            setDataAndType(uri, "application/vnd.android.package-archive")
            addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
            addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        }
        context.startActivity(intent)
    }
}
