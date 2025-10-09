package com.example.sga.service.lector

import android.util.Log
import androidx.compose.foundation.focusable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.size
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.input.key.*
import androidx.compose.ui.unit.dp
import androidx.compose.ui.input.key.KeyEventType
import android.view.KeyEvent

/**
 * Captura las pulsaciones que genera el láser Honeywell (keyboard-wedge).
 * Llama a [onScan] cuando detecta ENTER / TAB.
 */
@Composable
fun HardwareScannerListener(
    modifier: Modifier = Modifier,
    onScan: (String) -> Unit
) {
    var buffer by remember { mutableStateOf(StringBuilder()) }
    val focusRequester = remember { FocusRequester() }

    Box(
        modifier
            .size(1.dp)                             // 1 dp invisible pero con foco real
            .focusRequester(focusRequester)
            .focusable()
            .onPreviewKeyEvent { event ->
                // ① Acción típica: lectura completa por ACTION_MULTIPLE
                if (event.nativeKeyEvent?.action == KeyEvent.ACTION_MULTIPLE) {
                    val chars = event.nativeKeyEvent.characters
                    Log.d("ESCANEO", "📃 ACTION_MULTIPLE  characters=$chars")
                    if (!chars.isNullOrEmpty()) {
                        onScan(chars.trim())
                        return@onPreviewKeyEvent true
                    }
                }

                // ② Acción rara: llega como KEY_UP pero con characters adjuntos
                if (event.type == KeyEventType.KeyUp) {
                    val chars = event.nativeKeyEvent?.characters
                    Log.d("ESCANEO", "⌨️  KEY_UP con chars='$chars'")
                    if (!chars.isNullOrEmpty()) {
                        onScan(chars.trim())
                        return@onPreviewKeyEvent true
                    }

                    // solo si llega como tecla real (ENTER/TAB), disparamos buffer
                    val ch = event.utf16CodePoint.toChar()
                    Log.d("ESCANEO", "⌨️  KEY_UP  key=${event.key}  ch='$ch'")
                    if (ch == '\n' || ch == '\r' || event.key == Key.Tab) {
                        if (buffer.isNotEmpty()) {
                            Log.d("ESCANEO", "✅ FIN — buffer='${buffer}'")
                            onScan(buffer.toString())
                        }
                        buffer = StringBuilder()
                        return@onPreviewKeyEvent true
                    } else {
                        buffer.append(ch)
                    }
                }

                false
            }
    )

    LaunchedEffect(Unit) { focusRequester.requestFocus() }
}
