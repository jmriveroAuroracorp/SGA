package com.example.sga.service.Inactivity

import android.os.Handler
import android.os.Looper

object InactivityTracker {
    private var timeoutMillis: Long = 60 * 60 * 1000 // 60 minutos
    //private var timeoutMillis: Long = 5 * 1000
    private var handler: Handler? = null
    private var logoutCallback: (() -> Unit)? = null
    private val runnable = Runnable {
        logoutCallback?.invoke()
    }

    fun initialize(timeout: Long = timeoutMillis, onTimeout: () -> Unit) {
        handler = Handler(Looper.getMainLooper())
        timeoutMillis = timeout
        logoutCallback = onTimeout
        resetTimer()
    }

    fun resetTimer() {
        handler?.removeCallbacks(runnable)
        handler?.postDelayed(runnable, timeoutMillis)
    }

    fun stop() {
        handler?.removeCallbacks(runnable)
    }
}
