package com.example.sga.data.dto.traspasos.components

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.example.sga.data.dto.stock.ArticuloDto

@Composable
fun DialogSeleccionArticulo(
    lista: List<ArticuloDto>,
    onDismiss: () -> Unit,
    onSeleccion: (ArticuloDto) -> Unit
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        confirmButton = {},
        title = { Text("Selecciona un artículo") },
        text = {
            LazyColumn {
                items(lista) { articulo ->
                    Column(
                        modifier = Modifier
                            .fillMaxWidth()
                            .clickable { onSeleccion(articulo) }
                            .padding(vertical = 8.dp)
                    ) {
                        Text(
                            text = "${articulo.codigoArticulo} - ${articulo.descripcion}",
                            style = MaterialTheme.typography.bodyMedium
                        )
                        if (!articulo.codigoAlternativo.isNullOrBlank()) {
                            Text(
                                text = "EAN: ${articulo.codigoAlternativo}",
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.secondary
                            )
                        }
                    }
                }
            }
        }
    )
}
