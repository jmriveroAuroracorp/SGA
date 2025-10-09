package com.example.sga.service.lector

import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Build
import android.view.KeyCharacterMap
import android.view.KeyEvent
import android.util.Log

object DeviceUtils {

    // Paquetes habituales de stacks de escáner (no "marcas" en tu lógica de negocio)
    private val knownScannerPackages = listOf(
        "com.symbol.datawedge",         // Zebra DataWedge
        "com.honeywell.aidc",           // Honeywell AIDC
        "com.honeywell.decode",
        "com.intermec.datacollection",  // Intermec (Honeywell)
        "com.datalogic.device",         // Datalogic SDK
        "com.datalogic.decode",
        "com.unitech.scanservice",      // Unitech
        "co.kr.bluebird.barcode.service", // Bluebird
        "com.seuic.scanner",            // Seuic
        "com.urovo.sdk",                // Urovo
        "com.zebra.scanner",            // Zebra Scanner SDK
        "com.zebra.scannercontrol",     // Zebra Scanner Control
        "com.honeywell.scanner",        // Honeywell Scanner
        "com.datalogic.scanner",        // Datalogic Scanner
        "com.newland.scanner",          // Newland
        "com.opticon.scanner",          // Opticon
        "com.cognex.scanner"            // Cognex
    )

    // Intents/broadcasts típicos de stacks de escáner
    private val knownScannerBroadcasts = listOf(
        "com.symbol.datawedge.api.ACTION",               // Zebra API
        "com.symbol.datawedge.api.RESULT_ACTION",
        "com.honeywell.aidc.action.ACTION_BARCODE_READ", // Honeywell (varía por versión)
        "com.datalogic.decode.action.BARCODE_READ",      // Datalogic
        "unitech.scanservice.data",                      // Unitech
        "com.zebra.scanner.action.BARCODE_READ",         // Zebra Scanner
        "com.honeywell.scanner.action.BARCODE_READ",     // Honeywell Scanner
        "com.datalogic.scanner.action.BARCODE_READ",     // Datalogic Scanner
        "com.newland.scanner.action.BARCODE_READ",       // Newland
        "com.opticon.scanner.action.BARCODE_READ",       // Opticon
        "com.cognex.scanner.action.BARCODE_READ"         // Cognex
    )

    // Gatillos físicos que suelen mapear las PDAs
    private val likelyScanKeys = intArrayOf(
        KeyEvent.KEYCODE_BUTTON_L1,
        KeyEvent.KEYCODE_BUTTON_R1,
        KeyEvent.KEYCODE_STEM_PRIMARY,
        KeyEvent.KEYCODE_F9, // frecuente para "SCAN"
        KeyEvent.KEYCODE_F10,
        KeyEvent.KEYCODE_F11,
        KeyEvent.KEYCODE_F12,
        KeyEvent.KEYCODE_BUTTON_A,
        KeyEvent.KEYCODE_BUTTON_B,
        KeyEvent.KEYCODE_BUTTON_C,
        KeyEvent.KEYCODE_BUTTON_X,
        KeyEvent.KEYCODE_BUTTON_Y,
        KeyEvent.KEYCODE_BUTTON_Z
    )

    // Fabricantes conocidos de PDAs
    private val pdaManufacturers = setOf(
        "zebra", "symbol", "honeywell", "datalogic", "unitech", 
        "bluebird", "seuic", "urovo", "newland", "opticon", 
        "cognex", "intermec", "motorola", "psion", "handheld"
    )

    // Modelos conocidos de PDAs (patrones en el modelo)
    private val pdaModelPatterns = setOf(
        "tc", "mc", "et", "zt", "cc", "ce", "cn", "ct", "cx", "cz", // Zebra
        "ct", "cn", "ct", "cn", "ct", "cn", "ct", "cn", "ct", "cn", // Honeywell
        "falcon", "scorpion", "lion", "panther", "lynx", // Datalogic
        "unitech", "bluebird", "seuic", "urovo", "newland", "opticon"
    )

    /** Heurística mejorada "¿tiene escáner HW?" */
    fun hasHardwareScanner(context: Context): Boolean {
        val hasPackages = hasScannerPackagesOrReceivers(context)
        val hasTriggers = hasLikelyScanTriggers()
        val isPdaManufacturer = isPdaManufacturer()
        val hasPdaFeatures = hasPdaHardwareFeatures()
        val hasSystemProperties = hasPdaSystemProperties()
        
        val result = hasPackages || hasTriggers || isPdaManufacturer || hasPdaFeatures || hasSystemProperties
        
        Log.d("DeviceUtils", "🔍 PDA Detection: packages=$hasPackages, triggers=$hasTriggers, manufacturer=$isPdaManufacturer, features=$hasPdaFeatures, system=$hasSystemProperties -> result=$result")
        
        return result
    }

    /** (Opcional) Para telemetría o logs */
    fun hardwareScannerHint(context: Context): String = buildString {
        val pm = context.packageManager
        val pkgs = knownScannerPackages.filter { isInstalled(pm, it) }
        val acts = knownScannerBroadcasts.filter { hasReceiver(pm, it) }
        append("pkgs=$pkgs; acts=$acts; keys=${presentScanKeys().joinToString()}")
        append("; manufacturer=${Build.MANUFACTURER}; model=${Build.MODEL}")
        append("; brand=${Build.BRAND}; product=${Build.PRODUCT}")
    }

    // — Helpers —

    private fun hasScannerPackagesOrReceivers(context: Context): Boolean {
        val pm = context.packageManager
        val pkgHit = knownScannerPackages.any { isInstalled(pm, it) }
        val rcvHit = knownScannerBroadcasts.any { hasReceiver(pm, it) }
        return pkgHit || rcvHit
    }

    private fun isInstalled(pm: PackageManager, pkg: String): Boolean =
        try { pm.getPackageInfo(pkg, 0); true } catch (_: Exception) { false }

    private fun hasReceiver(pm: PackageManager, action: String): Boolean =
        pm.queryBroadcastReceivers(Intent(action), PackageManager.MATCH_DEFAULT_ONLY).isNotEmpty()

    private fun hasLikelyScanTriggers(): Boolean =
        presentScanKeys().size >= 2 // ≥2 gatillos suele indicar PDA

    private fun presentScanKeys(): List<Int> =
        likelyScanKeys.filter { KeyCharacterMap.deviceHasKey(it) }

    /** Detección por fabricante */
    private fun isPdaManufacturer(): Boolean {
        val manufacturer = Build.MANUFACTURER.lowercase()
        val brand = Build.BRAND.lowercase()
        val model = Build.MODEL.lowercase()
        val product = Build.PRODUCT.lowercase()
        
        // Verificar fabricante directo
        val isKnownManufacturer = pdaManufacturers.any { 
            manufacturer.contains(it) || brand.contains(it) 
        }
        
        // Verificar patrones en modelo/producto
        val hasModelPattern = pdaModelPatterns.any { pattern ->
            model.contains(pattern) || product.contains(pattern)
        }
        
        return isKnownManufacturer || hasModelPattern
    }

    /** Detección de características hardware específicas de PDAs */
    private fun hasPdaHardwareFeatures(): Boolean {
        // Verificar si tiene características típicas de PDA
        val hasPhysicalKeyboard = KeyCharacterMap.deviceHasKey(KeyEvent.KEYCODE_Q) && 
                                 KeyCharacterMap.deviceHasKey(KeyEvent.KEYCODE_W) &&
                                 KeyCharacterMap.deviceHasKey(KeyEvent.KEYCODE_E)
        
        // Verificar botones de función adicionales
        val hasFunctionKeys = KeyCharacterMap.deviceHasKey(KeyEvent.KEYCODE_F1) ||
                             KeyCharacterMap.deviceHasKey(KeyEvent.KEYCODE_F2) ||
                             KeyCharacterMap.deviceHasKey(KeyEvent.KEYCODE_F3)
        
        // Verificar si tiene teclado numérico
        val hasNumericKeypad = KeyCharacterMap.deviceHasKey(KeyEvent.KEYCODE_NUMPAD_0) ||
                              KeyCharacterMap.deviceHasKey(KeyEvent.KEYCODE_NUMPAD_1)
        
        // Verificar botones de navegación específicos
        val hasNavigationKeys = KeyCharacterMap.deviceHasKey(KeyEvent.KEYCODE_DPAD_UP) &&
                               KeyCharacterMap.deviceHasKey(KeyEvent.KEYCODE_DPAD_DOWN) &&
                               KeyCharacterMap.deviceHasKey(KeyEvent.KEYCODE_DPAD_LEFT) &&
                               KeyCharacterMap.deviceHasKey(KeyEvent.KEYCODE_DPAD_RIGHT)
        
        return hasPhysicalKeyboard || hasFunctionKeys || hasNumericKeypad || hasNavigationKeys
    }

    /** Verificación de propiedades del sistema */
    private fun hasPdaSystemProperties(): Boolean {
        return try {
            // Verificar propiedades específicas de PDAs
            val systemProperties = listOf(
                "ro.build.product",
                "ro.product.model", 
                "ro.product.manufacturer",
                "ro.product.brand"
            )
            
            systemProperties.any { property ->
                val value = getSystemProperty(property)?.lowercase() ?: ""
                pdaManufacturers.any { manufacturer -> 
                    value.contains(manufacturer) 
                } || pdaModelPatterns.any { pattern -> 
                    value.contains(pattern) 
                }
            }
        } catch (e: Exception) {
            false
        }
    }

    /** Obtener propiedad del sistema */
    private fun getSystemProperty(key: String): String? {
        return try {
            val process = Runtime.getRuntime().exec("getprop $key")
            process.inputStream.bufferedReader().readText().trim()
        } catch (e: Exception) {
            null
        }
    }

}