package com.example.sga.view.components

import androidx.compose.foundation.layout.padding
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.example.sga.view.app.SessionViewModel

@Composable
fun EmpresaActivaDisplay(sessionViewModel: SessionViewModel) {
    val empresa by sessionViewModel.empresaSeleccionada.collectAsState()

    empresa?.let {
        Text(
            text = "Empresa: ${it.nombre}",
            style = MaterialTheme.typography.bodyLarge, // antes era bodySmall
            modifier = Modifier.padding(top = 2.dp)
        )
    }
}

