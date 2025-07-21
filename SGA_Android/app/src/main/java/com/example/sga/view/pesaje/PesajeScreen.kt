package com.example.sga.view.pesaje

import android.Manifest
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.expandVertically
import androidx.compose.animation.shrinkVertically
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.KeyboardArrowDown
import androidx.compose.material.icons.filled.KeyboardArrowRight
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.navigation.NavHostController
import com.example.sga.service.scanner.QRScannerView
import com.example.sga.view.app.SessionViewModel
import com.example.sga.view.components.AppTopBar
import com.example.sga.view.components.EmpresaActivaDisplay
import com.google.accompanist.permissions.*


@OptIn(ExperimentalMaterial3Api::class, ExperimentalPermissionsApi::class)
@Composable
fun PesajeScreen(
    navController: NavHostController,
    sessionViewModel: SessionViewModel,
    viewModel: PesajeViewModel = viewModel()
) {
    val logic = remember { PesajeLogic(viewModel) }

    val scanning by viewModel.scanning.collectAsState()
    val resultadoPesaje by viewModel.resultado.collectAsState()
    val error by viewModel.error.collectAsState()

    val cameraPermissionState = rememberPermissionState(Manifest.permission.CAMERA)

    LaunchedEffect(Unit) {
        if (!cameraPermissionState.status.isGranted) {
            cameraPermissionState.launchPermissionRequest()
        }
    }

    Scaffold(
        topBar = {
            AppTopBar(
                sessionViewModel = sessionViewModel,
                navController = navController
            )
        }

    ) { padding ->
        Column(
            modifier = Modifier
                .padding(padding)
                .padding(16.dp)
                .fillMaxSize()
                .verticalScroll(rememberScrollState())
        ) {
            when {
                !cameraPermissionState.status.isGranted -> {
                    Text("Se requiere permiso de cámara para escanear etiquetas.")
                    Spacer(modifier = Modifier.height(16.dp))
                    Button(onClick = { cameraPermissionState.launchPermissionRequest() }) {
                        Text("Solicitar permiso")
                    }
                }

                scanning -> {
                    QRScannerView(
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(400.dp),
                        onCodeScanned = { code -> logic.onCodeScanned(code) }
                    )
                    Spacer(modifier = Modifier.height(16.dp))
                    Button(onClick = { viewModel.mostrarError("Escaneo cancelado") }) {
                        Text("Cancelar")
                    }
                }

                else -> {
                    Button(onClick = { logic.onScanStart() }) {
                        Text("Escanear etiqueta")
                    }

                    Spacer(modifier = Modifier.height(24.dp))

                    resultadoPesaje?.let { pesaje ->
                        if (pesaje.ejercicio != 0 && pesaje.serie.isNotBlank() && pesaje.numero != 0) {
                            Text(
                                text = "OF: ${pesaje.ejercicio}/${pesaje.serie}/${pesaje.numero}",
                                style = MaterialTheme.typography.titleLarge
                            )
                        }
                        Spacer(modifier = Modifier.height(8.dp))

                        Text(
                            text = "Total de amasijos: ${pesaje.numeroAmasijos}",
                            style = MaterialTheme.typography.bodyLarge
                        )

                        Spacer(modifier = Modifier.height(16.dp))

                        pesaje.ordenesTrabajo.forEach { ot ->
                            var expandedOT by remember { mutableStateOf(false) }

                            Card(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .padding(vertical = 8.dp),
                                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant),
                            ) {
                                Column(
                                    modifier = Modifier
                                        .clickable { expandedOT = !expandedOT }
                                        .padding(20.dp)
                                ) {
                                    Row(
                                        modifier = Modifier.fillMaxWidth(),
                                        verticalAlignment = Alignment.CenterVertically
                                    ) {
                                        Text(
                                            text = "OT: ${ot.codigoArticuloOT}",
                                            style = MaterialTheme.typography.titleLarge,
                                            modifier = Modifier.weight(1f)
                                        )
                                        Icon(
                                            imageVector = if (expandedOT) Icons.Default.KeyboardArrowDown else Icons.Default.KeyboardArrowRight,
                                            contentDescription = null
                                        )
                                    }

                                    Text(
                                        text = ot.descripcionArticuloOT,
                                        style = MaterialTheme.typography.bodyMedium
                                    )

                                    AnimatedVisibility(
                                        visible = expandedOT,
                                        enter = expandVertically(),
                                        exit = shrinkVertically()
                                    ) {
                                        Column {
                                            Spacer(modifier = Modifier.height(8.dp))
                                            ot.amasijos.forEach { amasijo ->
                                                var expandedAmasijo by remember { mutableStateOf(false) }

                                                Card(
                                                    modifier = Modifier
                                                        .fillMaxWidth()
                                                        .padding(vertical = 4.dp),
                                                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
                                                ) {
                                                    Column(
                                                        modifier = Modifier
                                                            .clickable { expandedAmasijo = !expandedAmasijo }
                                                            .padding(16.dp)
                                                    ) {
                                                        Row(
                                                            modifier = Modifier.fillMaxWidth(),
                                                            verticalAlignment = Alignment.CenterVertically
                                                        ) {
                                                            Text(
                                                                text = "Amasijo: ${amasijo.amasijo}",
                                                                style = MaterialTheme.typography.titleSmall,
                                                                modifier = Modifier.weight(1f)
                                                            )
                                                            Icon(
                                                                imageVector = if (expandedAmasijo) Icons.Default.KeyboardArrowDown else Icons.Default.KeyboardArrowRight,
                                                                contentDescription = null
                                                            )
                                                        }

                                                        Text(
                                                            text = "Total pesado: ${amasijo.totalPesado} kg",
                                                            style = MaterialTheme.typography.bodyMedium
                                                        )

                                                        AnimatedVisibility(
                                                            visible = expandedAmasijo,
                                                            enter = expandVertically(),
                                                            exit = shrinkVertically()
                                                        ) {
                                                            Column(
                                                                modifier = Modifier.padding(top = 8.dp)
                                                            ) {
                                                                amasijo.componentes.forEach { comp ->
                                                                    Column(
                                                                        modifier = Modifier
                                                                            .padding(vertical = 4.dp)
                                                                            .fillMaxWidth()
                                                                    ) {
                                                                        Text(text = "Artículo: ${comp.articuloComponente}")
                                                                        Text(text = "Descripción: ${comp.descripcionArticulo}")
                                                                        Text(text = "Partida: ${comp.partida}")
                                                                        Text(text = "Caducidad: ${comp.fechaCaduca}")
                                                                        Text(text = "Peso: ${comp.unidadesComponente} kg")
                                                                    }
                                                                    Divider()
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    error?.let {
                        Spacer(modifier = Modifier.height(16.dp))
                        Text(text = it, color = MaterialTheme.colorScheme.error)
                    }
                }
            }
        }
    }
}

