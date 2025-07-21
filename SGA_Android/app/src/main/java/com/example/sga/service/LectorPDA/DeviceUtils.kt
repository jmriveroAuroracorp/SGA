package com.example.sga.service.LectorPDA

import android.os.Build
object DeviceUtils {
    /** -- true cuando el hardware es Honeywell (láser integrado) -- */
    val isHoneywell get() =
        Build.MANUFACTURER.equals("Honeywell", ignoreCase = true)
}