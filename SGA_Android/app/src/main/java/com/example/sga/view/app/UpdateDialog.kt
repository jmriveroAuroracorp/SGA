package com.example.sga.view.app

import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable

@Composable
fun UpdateDialog(onUpdate: () -> Unit, onCancel: () -> Unit) {
    AlertDialog(
        onDismissRequest = { /* No cerrar */ },
        title = { Text("Actualización requerida") },
        text = { Text("Hay una nueva versión disponible. Debes actualizar para continuar.") },
        confirmButton = {
            Button(onClick = onUpdate) { Text("Actualizar") }
        },
        dismissButton = {
            Button(onClick = onCancel) { Text("Salir") }
        }
    )
}
