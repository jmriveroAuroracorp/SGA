package com.example.sga.service.scanner

import android.annotation.SuppressLint
import android.util.Log
import androidx.camera.core.ImageAnalysis
import androidx.camera.core.ImageProxy
import com.google.mlkit.vision.barcode.BarcodeScanning
import com.google.mlkit.vision.common.InputImage

class QRCodeAnalyzer(
    private val onQRCodeScanned: (String) -> Unit,
    private val scanDelayMillis: Long = 1000L // Tiempo mínimo entre escaneos (1 segundo por defecto)
) : ImageAnalysis.Analyzer {

    private val scanner = BarcodeScanning.getClient()
    private var lastScanTime: Long = 0L
    private var lastScannedCode: String? = null

    @SuppressLint("UnsafeOptInUsageError")
    override fun analyze(imageProxy: ImageProxy) {
        val mediaImage = imageProxy.image ?: run {
            imageProxy.close()
            return
        }

        val image = InputImage.fromMediaImage(mediaImage, imageProxy.imageInfo.rotationDegrees)

        scanner.process(image)
            .addOnSuccessListener { barcodes ->
                val currentTime = System.currentTimeMillis()
                val timeSinceLastScan = currentTime - lastScanTime

                for (barcode in barcodes) {
                    val value = barcode.rawValue
                    if (value != null) {
                        // Solo procesar si ha pasado suficiente tiempo desde el último escaneo
                        // o si es un código diferente
                        if (timeSinceLastScan >= scanDelayMillis || value != lastScannedCode) {
                            Log.d("QR_DEBUG", "Código leído: $value")
                            lastScanTime = currentTime
                            lastScannedCode = value
                            onQRCodeScanned(value)
                        } else {
                            Log.d("QR_DEBUG", "Escaneo ignorado (demasiado rápido): $value")
                        }
                        break
                    }
                }
            }
            .addOnFailureListener {
                // Puedes loguear el error si quieres
            }
            .addOnCompleteListener {
                imageProxy.close()
            }
    }
}