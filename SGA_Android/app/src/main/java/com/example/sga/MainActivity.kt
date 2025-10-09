package com.example.sga

import android.Manifest
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import android.view.MotionEvent
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.core.content.ContextCompat
import com.example.sga.service.Inactivity.InactivityTracker
import com.example.sga.view.app.SGAApp
import com.example.sga.view.theme.SGATheme

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            SGATheme {
                SGAApp()
            }
        }
    }
    private val requestNotifPerm = registerForActivityResult(
        ActivityResultContracts.RequestPermission()
    ) { granted ->
        // Puedes hacer un log si quieres
        // Log.d("MainActivity", "Permiso POST_NOTIFICATIONS concedido: $granted")
    }
    private fun ensureNotifPermission() {
        if (Build.VERSION.SDK_INT >= 33) {
            val perm = Manifest.permission.POST_NOTIFICATIONS
            val granted = ContextCompat.checkSelfPermission(this, perm) == PackageManager.PERMISSION_GRANTED
            if (!granted) {
                requestNotifPerm.launch(perm)
            }
        }
    }

    override fun dispatchTouchEvent(ev: MotionEvent): Boolean {
        InactivityTracker.resetTimer()
        return super.dispatchTouchEvent(ev)
    }
}
