@file:OptIn(ExperimentalMaterial3Api::class)
package com.example.sga.view.conversion

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.focusable
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Autorenew
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.input.key.onPreviewKeyEvent
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.foundation.text.KeyboardActions
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.navigation.NavHostController
import com.example.sga.view.app.SessionViewModel
import com.example.sga.view.components.AppTopBar
import com.example.sga.service.scanner.QRScannerView
import com.example.sga.service.lector.DeviceUtils
import com.example.sga.data.dto.traspasos.LineaPaletDto
import com.example.sga.utils.FormatUtils
import com.example.sga.utils.SoundUtils
import android.view.KeyEvent
import kotlinx.coroutines.delay

@Composable
fun ConversionScreen(
    navController: NavHostController,
    sessionViewModel: SessionViewModel,
    viewModel: ConversionViewModel = viewModel()
) {
    val context = LocalContext.current
    val user by sessionViewModel.user.collectAsState()
    val empresaSeleccionada by sessionViewModel.empresaSeleccionada.collectAsState()
    val almacenesPermitidos by viewModel.almacenesPermitidos.collectAsState()
    
    val cargando by viewModel.cargando.collectAsState()
    val error by viewModel.error.collectAsState()
    val ubicacionOrigen by viewModel.ubicacionOrigen.collectAsState()
    // No necesario para conversión
    // val articuloSeleccionado by viewModel.articuloSeleccionado.collectAsState()
    val paletSeleccionado by viewModel.paletSeleccionado.collectAsState()
    val lineasPalet by viewModel.lineasPalet.collectAsState()
    val mostrarDialogoConversion by viewModel.mostrarDialogoConversion.collectAsState()
    val lineaAConvertir by viewModel.lineaAConvertir.collectAsState()
    
    // Estados locales
    var mostrarScanner by remember { mutableStateOf(false) }
    val focusRequester = remember { FocusRequester() }
    val esLectorFisico = remember { DeviceUtils.hasHardwareScanner(context) }
    
    // Auto-focus al cargar
    LaunchedEffect(Unit) {
        delay(300)
        focusRequester.requestFocus()
    }

    // Cargar almacenes permitidos cuando el usuario esté disponible
    LaunchedEffect(user, empresaSeleccionada) {
        user?.let { usuario ->
            empresaSeleccionada?.let { empresa ->
                viewModel.cargarAlmacenesPermitidos(usuario, empresa.codigo.toInt())
            }
        }
    }
    
    // Función para procesar código - igual que TraspasosScreen
    fun procesarCodigo(codigo: String) {
        if (codigo.isBlank()) return

        viewModel.procesarCodigoEscaneado(
            code = codigo,
            empresaId = (empresaSeleccionada?.codigo ?: 1).toShort(),
            codigoAlmacen = user?.codigosAlmacen?.firstOrNull(),
            codigoCentro = user?.codigoCentro,
            almacen = null,
            onUbicacionDetectada = { alm, ubi ->
                // Validar que el usuario tenga permisos para el almacén de origen
                if (almacenesPermitidos.contains(alm)) {
                    viewModel.setUbicacionOrigen(alm, ubi)
                    SoundUtils.getInstance().playSuccessSound()
                } else {
                    viewModel.setError("No tienes permisos para operar en el almacén '$alm'. Ubicación no permitida.")
                    SoundUtils.getInstance().playErrorSound()
                }
            },
            onPaletDetectado = { palet ->
                // Validar que el palet esté en la ubicación escaneada ANTES de mostrarlo
                ubicacionOrigen?.let { ubicacion ->
                    viewModel.validarUbicacionDePalet(
                        palet = palet,
                        ubicacionEscaneada = ubicacion,
                        onValidado = {
                            // Solo si la validación es exitosa, mostrar el palet
                            viewModel.setPaletSeleccionadoYValidado(palet)
                            SoundUtils.getInstance().playSuccessSound()
                        },
                        onError = { errorMsg ->
                            // Si la validación falla, limpiar palet y mostrar error
                            viewModel.clearPaletSeleccionado()
                            viewModel.setError("El palet escaneado no se encuentra en la ubicación.")
                            SoundUtils.getInstance().playErrorSound()
                        }
                    )
                } ?: run {
                    viewModel.setError("Debe escanear primero una ubicación antes del palet.")
                    SoundUtils.getInstance().playErrorSound()
                }
            },
            onError = { errorMsg ->
                viewModel.setError(errorMsg)
                SoundUtils.getInstance().playErrorSound()
            }
        )
        focusRequester.requestFocus()
    }
    
    Scaffold(
        topBar = {
            AppTopBar(
                title = "Conversión",
                sessionViewModel = sessionViewModel,
                navController = navController,
                showBackButton = true
            )
        }
    ) { padding ->
        Box(modifier = Modifier.padding(padding).fillMaxSize()) {
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(16.dp)
                    .verticalScroll(rememberScrollState()),
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                // Captura invisible del escaneo por hardware (como en Traspasos)
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(1.dp)
                        .focusRequester(focusRequester)
                        .focusable()
                        .onPreviewKeyEvent { event ->
                            if (event.nativeKeyEvent?.action == android.view.KeyEvent.ACTION_MULTIPLE) {
                                event.nativeKeyEvent.characters?.let { code ->
                                    procesarCodigo(code.trim())
                                    return@onPreviewKeyEvent true
                                }
                            }
                            false
                        }
                )

                // Botón para escanear por cámara si no hay lector físico
                if (esLectorFisico.not()) {
                    Button(
                        onClick = { mostrarScanner = true },
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Text("Escanear")
                    }
                    Spacer(Modifier.height(12.dp))
                }

                // Estado del flujo - EXACTO como TraspasosScreen
                when (ubicacionOrigen) {
                    null -> {
                        // Aún no hay ubicación
                        Text(
                            "Escanee una etiqueta de ubicación",
                            style = MaterialTheme.typography.titleMedium
                        )
                    }
                    else -> {
                        // Ya hay ubicación → muéstrala y cambia la instrucción
                        val (almacen, ubi) = ubicacionOrigen!!
                        Text(
                            "Ubicación seleccionada: $almacen - $ubi",
                            style = MaterialTheme.typography.titleMedium
                        )
                        Spacer(Modifier.height(2.dp))
                        Text(
                            "Ahora escanee un palet",
                            style = MaterialTheme.typography.bodyMedium
                        )
                    }
                }

                // Mostrar palet seleccionado si existe - EXACTO como TraspasosScreen
                paletSeleccionado?.let { palet ->
                    Card(
                        modifier = Modifier.fillMaxWidth(),
                        colors = CardDefaults.cardColors(
                            containerColor = MaterialTheme.colorScheme.primaryContainer
                        )
                    ) {
                        Column(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(12.dp)
                        ) {
                            Text(
                                "Palet seleccionado:",
                                fontWeight = FontWeight.Bold,
                                style = MaterialTheme.typography.bodyMedium
                            )
                            Text("Código: ${palet.codigoPalet ?: "N/A"}")
                            
                            // Switch para reabrir el palet
                            Row(
                                verticalAlignment = Alignment.CenterVertically,
                                modifier = Modifier.fillMaxWidth()
                            ) {
                                Text(
                                    "Estado del palet:",
                                    style = MaterialTheme.typography.bodyMedium
                                )
                                Spacer(Modifier.width(8.dp))
                                Switch(
                                    checked = palet.estado.equals("Abierto", ignoreCase = true),
                                    onCheckedChange = { nuevoEstado ->
                                        if (nuevoEstado) {
                                            // REABRIR
                                            user?.let { usuario ->
                                                viewModel.reabrirPalet(palet.id, usuario.id.toInt()) {
                                                    // Actualizar el palet después de reabrir
                                                    viewModel.obtenerPalet(palet.id) { paletActualizado ->
                                                        viewModel.setPaletSeleccionadoYValidado(paletActualizado)
                                                    }
                                                }
                                            }
                                        } else {
                                            // CERRAR - no implementado en conversión
                                            viewModel.setError("No se puede cerrar palet desde conversión")
                                        }
                                    }
                                )
                                Spacer(Modifier.width(8.dp))
                                Text(
                                    if (palet.estado.equals("Abierto", ignoreCase = true)) "Abierto" else "Cerrado",
                                    style = MaterialTheme.typography.bodySmall
                                )
                            }
                            
                            if (palet.ordenTrabajoId != null) {
                                Text("Orden de Trabajo: ${palet.ordenTrabajoId}")
                            }
                        }
                    }

                    // Mostrar líneas del palet
                    Card(
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Column(modifier = Modifier.padding(16.dp)) {
                            Text(
                                text = "Líneas del palet ${palet.codigoPalet}",
                                style = MaterialTheme.typography.titleMedium,
                                fontWeight = FontWeight.Bold
                            )
                            Spacer(modifier = Modifier.height(4.dp))
                            
                            if (lineasPalet.isEmpty()) {
                                Text(
                                    "No hay líneas en este palet",
                                    style = MaterialTheme.typography.bodyMedium,
                                    color = Color.Gray,
                                    modifier = Modifier.padding(vertical = 8.dp)
                                )
                            } else {
                                Column(
                                    modifier = Modifier.fillMaxWidth(),
                                    verticalArrangement = Arrangement.spacedBy(4.dp)
                                ) {
                                    lineasPalet.forEach { linea ->
                                        LineaPaletCard(
                                            linea = linea,
                                            paletAbierto = palet.estado.equals("Abierto", ignoreCase = true),
                                            onConvertir = { viewModel.abrirDialogoConversion(linea) }
                                        )
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            // Loading overlay
            if (cargando) {
                Box(
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(padding),
                    contentAlignment = Alignment.Center
                ) {
                    CircularProgressIndicator()
                }
            }
        }
        
        // Scanner QR
        if (mostrarScanner) {
            QRScannerView(
                onCodeScanned = { codigo ->
                    mostrarScanner = false
                    procesarCodigo(codigo)
                }
            )
        }
        
        // Diálogo de conversión
        if (mostrarDialogoConversion && lineaAConvertir != null) {
            DialogoConversion(
                linea = lineaAConvertir!!,
                onDismiss = { viewModel.cerrarDialogoConversion() },
                onConfirm = { nuevoArticulo, cantidad ->
                    user?.let { usuario ->
                        empresaSeleccionada?.let { empresa ->
                            viewModel.convertirLinea(
                                lineaOriginal = lineaAConvertir!!,
                                nuevoCodigoArticulo = nuevoArticulo,
                                cantidadAConvertir = cantidad,
                                empresaId = empresa.codigo.toShort(),
                                usuarioId = usuario.id.toInt(),
                                onSuccess = {
                                    // Éxito
                                },
                                onError = { error ->
                                    viewModel.setError(error)
                                }
                            )
                        }
                    }
                }
            )
        }
    }

    // Diálogo de error
    if (error != null) {
        AlertDialog(
            onDismissRequest = { viewModel.setError(null) },
            title = { Text("Error") },
            text = { Text(error!!) },
            confirmButton = {
                TextButton(onClick = { viewModel.setError(null) }) {
                    Text("OK")
                }
            }
        )
    }
}

@Composable
fun LineaPaletCard(
    linea: LineaPaletDto,
    paletAbierto: Boolean,
    onConvertir: () -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White)
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(12.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = linea.codigoArticulo,
                    style = MaterialTheme.typography.titleSmall,
                    fontWeight = FontWeight.Bold
                )
                linea.descripcion?.let {
                    Text(
                        text = it,
                        style = MaterialTheme.typography.bodySmall,
                        color = Color.Gray
                    )
                }
                Spacer(modifier = Modifier.height(4.dp))
                Row(
                    horizontalArrangement = Arrangement.spacedBy(16.dp)
                ) {
                    Text(
                        text = "Cantidad: ${FormatUtils.formatearCantidad(linea.cantidad)}",
                        style = MaterialTheme.typography.bodySmall
                    )
                    linea.lote?.let {
                        Text(
                            text = "Lote: $it",
                            style = MaterialTheme.typography.bodySmall
                        )
                    }
                }
                linea.fechaCaducidad?.let {
                    Text(
                        text = "Caducidad: ${it.take(10)}",
                        style = MaterialTheme.typography.bodySmall
                    )
                }
            }
            
            // Solo mostrar el icono de conversión si el palet está abierto
            if (paletAbierto) {
                IconButton(onClick = onConvertir) {
                    androidx.compose.material3.Surface(
                        modifier = Modifier.size(56.dp),
                        shape = RoundedCornerShape(12.dp),
                        color = MaterialTheme.colorScheme.primary,
                        shadowElevation = 4.dp
                    ) {
                        Box(contentAlignment = Alignment.Center, modifier = Modifier.fillMaxSize()) {
                            Icon(
                                imageVector = Icons.Default.Autorenew,
                                contentDescription = "Convertir",
                                tint = Color.White,
                                modifier = Modifier.size(28.dp)
                            )
                        }
                    }
                }
            }
        }
    }
}

@Composable
fun DialogoConversion(
    linea: LineaPaletDto,
    onDismiss: () -> Unit,
    onConfirm: (nuevoArticulo: String, cantidad: Double) -> Unit
) {
    var nuevoCodigoArticulo by remember { mutableStateOf("") }
    var cantidadTexto by remember { mutableStateOf(FormatUtils.formatearCantidad(linea.cantidad)) }
    var error by remember { mutableStateOf<String?>(null) }
    
    AlertDialog(
        onDismissRequest = onDismiss,
        title = {
            Text("Convertir artículo")
        },
        text = {
            Column {
                Text(
                    text = "Artículo actual: ${linea.codigoArticulo}",
                    style = MaterialTheme.typography.bodyMedium,
                    fontWeight = FontWeight.Bold
                )
                Text(
                    text = "Cantidad disponible: ${FormatUtils.formatearCantidad(linea.cantidad)}",
                    style = MaterialTheme.typography.bodySmall,
                    color = Color.Gray
                )
                
                Spacer(modifier = Modifier.height(16.dp))
                
                OutlinedTextField(
                    value = nuevoCodigoArticulo,
                    onValueChange = { nuevoCodigoArticulo = it },
                    label = { Text("Nuevo código de artículo") },
                    modifier = Modifier.fillMaxWidth(),
                    isError = error != null
                )
                
                Spacer(modifier = Modifier.height(8.dp))
                
                OutlinedTextField(
                    value = cantidadTexto,
                    onValueChange = { cantidadTexto = it },
                    label = { Text("Cantidad a convertir") },
                    modifier = Modifier.fillMaxWidth(),
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
                    isError = error != null
                )
                
                error?.let {
                    Spacer(modifier = Modifier.height(8.dp))
                    Text(
                        text = it,
                        color = MaterialTheme.colorScheme.error,
                        style = MaterialTheme.typography.bodySmall
                    )
                }
                
                Spacer(modifier = Modifier.height(8.dp))
                
                Text(
                    text = "Nota: Se mantendrá el mismo lote y fecha de caducidad",
                    style = MaterialTheme.typography.bodySmall,
                    color = Color.Gray,
                    fontStyle = androidx.compose.ui.text.font.FontStyle.Italic
                )
            }
        },
        confirmButton = {
            Button(
                onClick = {
                    if (nuevoCodigoArticulo.isBlank()) {
                        error = "Debe introducir un código de artículo"
                        return@Button
                    }
                    
                    val cantidad = cantidadTexto.replace(",", ".").toDoubleOrNull()
                    if (cantidad == null || cantidad <= 0) {
                        error = "La cantidad debe ser mayor a 0"
                        return@Button
                    }
                    
                    if (cantidad > linea.cantidad) {
                        error = "La cantidad no puede ser mayor a ${FormatUtils.formatearCantidad(linea.cantidad)}"
                        return@Button
                    }
                    
                    onConfirm(nuevoCodigoArticulo, cantidad)
                }
            ) {
                Text("Convertir")
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) {
                Text("Cancelar")
            }
        }
    )
}
