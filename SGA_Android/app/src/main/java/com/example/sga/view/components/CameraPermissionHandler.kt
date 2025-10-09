package com.example.sga.view.components

import android.Manifest
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.google.accompanist.permissions.*

/**
 * Componente reutilizable para manejar permisos de cámara
 * Muestra la UI apropiada según el estado del permiso
 * 
 * NOTA: Los permisos se solicitan automáticamente en HomeScreen,
 * este componente es para casos específicos donde se necesite verificar el estado
 */
@OptIn(ExperimentalPermissionsApi::class)
@Composable
fun CameraPermissionHandler(
    modifier: Modifier = Modifier,
    onPermissionGranted: @Composable () -> Unit,
    onPermissionDenied: @Composable () -> Unit = {
        CameraPermissionDeniedUI()
    }
) {
    val cameraPermissionState = rememberPermissionState(Manifest.permission.CAMERA)

    when {
        cameraPermissionState.status.isGranted -> {
            onPermissionGranted()
        }
        cameraPermissionState.status.shouldShowRationale -> {
            CameraPermissionRationaleUI(
                onRequestPermission = { cameraPermissionState.launchPermissionRequest() }
            )
        }
        else -> {
            onPermissionDenied()
        }
    }
}

@Composable
private fun CameraPermissionDeniedUI() {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(16.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Text(
            text = "Permiso de cámara denegado",
            style = MaterialTheme.typography.titleMedium,
            color = MaterialTheme.colorScheme.error
        )
        Spacer(modifier = Modifier.height(8.dp))
        Text(
            text = "Para escanear códigos QR necesitas otorgar el permiso de cámara en la configuración de la aplicación.",
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
    }
}

@Composable
private fun CameraPermissionRationaleUI(
    onRequestPermission: () -> Unit
) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(16.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Text(
            text = "Se requiere permiso de cámara",
            style = MaterialTheme.typography.titleMedium
        )
        Spacer(modifier = Modifier.height(8.dp))
        Text(
            text = "Para escanear códigos QR necesitamos acceso a la cámara.",
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
        Spacer(modifier = Modifier.height(16.dp))
        Button(onClick = onRequestPermission) {
            Text("Solicitar permiso")
        }
    }
}
