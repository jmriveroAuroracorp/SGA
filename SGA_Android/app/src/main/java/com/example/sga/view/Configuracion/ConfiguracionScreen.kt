package com.example.sga.view.configuracion

import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Check
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.navigation.NavHostController
import com.example.sga.view.app.SessionViewModel
import com.example.sga.view.components.AppTopBar

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ConfiguracionScreen(
    navController: NavHostController,
    sessionViewModel: SessionViewModel
) {
    val user by sessionViewModel.user.collectAsState()
    if (user == null) {
        Text("Usuario no disponible")
        return
    }
    val empresas = user?.empresas ?: emptyList()
    val empresaActual = sessionViewModel.empresaSeleccionada.collectAsState().value
    val logic = remember { ConfiguracionLogic(sessionViewModel) }
    LaunchedEffect(Unit) {
        logic.cargarImpresoras()
    }

    Scaffold(
        topBar = {
            AppTopBar(
                sessionViewModel = sessionViewModel,
                navController = navController
            )
        }
    ) { padding ->
        Column(modifier = Modifier
            .padding(padding)
            .padding(16.dp)) {

            Text("Empresa activa:", style = MaterialTheme.typography.titleMedium)
            Spacer(Modifier.height(8.dp))

            var expanded by remember { mutableStateOf(false) }

            ExposedDropdownMenuBox(
                expanded = expanded,
                onExpandedChange = { expanded = !expanded }
            ) {
                OutlinedTextField(
                    readOnly = true,
                    value = empresaActual?.nombre ?: "",
                    onValueChange = {},
                    label = { Text("Seleccionar empresa") },
                    trailingIcon = {
                        ExposedDropdownMenuDefaults.TrailingIcon(expanded = expanded)
                    },
                    modifier = Modifier
                        .menuAnchor()
                        .fillMaxWidth()
                )

                ExposedDropdownMenu(
                    expanded = expanded,
                    onDismissRequest = { expanded = false }
                ) {
                    empresas.forEach { empresa ->
                        DropdownMenuItem(
                            text = {
                                Row {
                                    if (empresa == empresaActual) {
                                        Icon(Icons.Default.Check, contentDescription = null)
                                        Spacer(Modifier.width(4.dp))
                                    }
                                    Text(empresa.nombre)
                                }
                            },
                            onClick = {
                                expanded = false
                                logic.cambiarEmpresa(empresa.codigo.toString())
                            }
                        )
                    }
                }
            }

            Spacer(Modifier.height(32.dp))
            Text("Impresora:", style = MaterialTheme.typography.titleMedium)
            Spacer(Modifier.height(8.dp))

            var impExpanded by remember { mutableStateOf(false) }
            val impresoras by logic.impresoras
            val impresoraSel by logic.impresoraSeleccionada

            ExposedDropdownMenuBox(
                expanded = impExpanded,
                onExpandedChange = { impExpanded = !impExpanded }
            ) {
                OutlinedTextField(
                    readOnly = true,
                    value = impresoraSel?.nombre ?: "",
                    onValueChange = {},
                    label = { Text("Seleccionar impresora") },
                    trailingIcon = {
                        ExposedDropdownMenuDefaults.TrailingIcon(expanded = impExpanded)
                    },
                    modifier = Modifier
                        .menuAnchor()
                        .fillMaxWidth()
                )

                ExposedDropdownMenu(
                    expanded = impExpanded,
                    onDismissRequest = { impExpanded = false }
                ) {
                    impresoras.forEach { impresora ->
                        DropdownMenuItem(
                            text = {
                                Row {
                                    if (impresora == impresoraSel) {
                                        Icon(Icons.Default.Check, contentDescription = null)
                                        Spacer(Modifier.width(4.dp))
                                    }
                                    Text(impresora.nombre)
                                }
                            },
                            onClick = {
                                impExpanded = false
                                logic.cambiarImpresora(impresora)
                            }
                        )
                    }
                }
            }

        }
    }
}


