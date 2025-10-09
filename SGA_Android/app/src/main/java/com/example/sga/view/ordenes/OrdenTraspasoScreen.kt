package com.example.sga.view.ordenes

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import com.example.sga.data.dto.ordenes.OrdenTraspasoDto
import com.example.sga.data.model.user.User
import com.example.sga.data.ApiManager
import com.example.sga.view.components.AppTopBar
import androidx.compose.foundation.clickable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.style.TextAlign
import java.text.SimpleDateFormat
import java.util.*
import androidx.compose.foundation.background
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.ui.zIndex
import com.example.sga.view.traspasos.TraspasosLogic
import com.example.sga.data.dto.traspasos.TraspasoPendienteDto
import android.util.Log
import androidx.compose.foundation.focusable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.input.key.onPreviewKeyEvent
import androidx.compose.ui.platform.LocalContext
import com.example.sga.service.lector.DeviceUtils
import com.example.sga.service.scanner.QRScannerView
import androidx.compose.ui.layout.layout
import androidx.compose.ui.focus.focusRequester
import androidx.compose.runtime.rememberCoroutineScope
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun OrdenTraspasoScreen(
    sessionViewModel: com.example.sga.view.app.SessionViewModel,
    navController: androidx.navigation.NavHostController,
    onNavigateToDetail: (String) -> Unit
) {
    val user by sessionViewModel.user.collectAsStateWithLifecycle()
    val viewModel: OrdenTraspasoViewModel = viewModel()
    
    // Inicializar el ViewModel con SessionViewModel
    LaunchedEffect(Unit) {
        viewModel.init(sessionViewModel)
    }
    
    val ordenes by viewModel.ordenes.collectAsStateWithLifecycle()
    val cargando by viewModel.cargando.collectAsStateWithLifecycle()
    val error by viewModel.error.collectAsStateWithLifecycle()
    val mensaje by viewModel.mensaje.collectAsStateWithLifecycle()
    
    // Estados para el bloqueo de traspaso pendiente
    var esperandoUbicacionDestino by remember { mutableStateOf(false) }
    var traspasosPendientes by remember { mutableStateOf<List<TraspasoPendienteDto>>(emptyList()) }
    val traspasosLogic = remember { TraspasosLogic() }
    
    // Estados para diálogos
    var mostrarDialogoExito by remember { mutableStateOf(false) }
    var mostrarDialogoError by remember { mutableStateOf<String?>(null) }
    var escaneoProcesado by remember { mutableStateOf(false) }
    
    // Estados para el escaneo
    val context = LocalContext.current
    val focusRequester = remember { FocusRequester() }
    val scope = rememberCoroutineScope()
    
    // Función para procesar la ubicación destino escaneada
    fun procesarUbicacionDestino(codigo: String) {
        Log.d("ORDEN_TRASPASO_UI", "🔍 Procesando ubicación destino: $codigo")
        
        val empresa = sessionViewModel.empresaSeleccionada.value?.codigo?.toShort() ?: return
        
        // Parsear el código para obtener almacén y ubicación
        val (almacenDestino, ubicacionDestino) = if (codigo.contains('$')) {
            val parts = codigo.split('$')
            if (parts.size == 2) parts[0] to parts[1] else "PR" to codigo
        } else {
            "PR" to codigo
        }
        
        Log.d("ORDEN_TRASPASO_UI", "📍 Almacén: $almacenDestino, Ubicación: $ubicacionDestino")
        
        // Procesar cada traspaso pendiente
        traspasosPendientes.forEach { traspaso ->
            when (traspaso.tipoTraspaso.uppercase()) {
                "PALET" -> {
                    Log.d("ORDEN_TRASPASO_UI", "🔍 Finalizando traspaso: ${traspaso.id}, paletId: ${traspaso.paletId}")
                    Log.d("ORDEN_TRASPASO_UI", "🔍 DTO completo: almacenDestino=$almacenDestino, ubicacionDestino=$ubicacionDestino, usuarioId=${user?.id?.toInt()}")
                    traspasosLogic.completarTraspaso(
                        idTraspaso = traspaso.id,
                        dto = com.example.sga.data.dto.traspasos.CompletarTraspasoDto(
                            codigoAlmacenDestino = almacenDestino,
                            ubicacionDestino = ubicacionDestino,
                            fechaFinalizacion = java.time.LocalDateTime.now().toString(),
                            usuarioFinalizacionId = user?.id?.toInt() ?: 0
                        ),
                        paletId = traspaso.paletId,
                        onSuccess = {
                            Log.d("ORDEN_TRASPASO_UI", "✅ Traspaso completado: ${traspaso.id}")
                            // 3. Actualizar línea de orden con el ID del traspaso
                            if (traspaso.idLineaOrden != null) {
                                Log.d("ORDEN_TRASPASO_UI", "📝 Actualizando línea de orden: ${traspaso.idLineaOrden} con traspasoId: ${traspaso.id}")
                                scope.launch {
                                    try {
                                        val actualizarDto = com.example.sga.data.dto.ordenes.ActualizarIdTraspasoDto(
                                            idTraspaso = traspaso.id
                                        )
                                        val responseActualizar = ApiManager.ordenTraspasoApi.actualizarIdTraspaso(
                                            idLinea = traspaso.idLineaOrden,
                                            dto = actualizarDto
                                        )
                                        
                                        if (responseActualizar.isSuccessful) {
                                            Log.d("ORDEN_TRASPASO_UI", "✅ Línea de orden actualizada con IdTraspaso")
                                            
                                            // Todo completado exitosamente
                                            esperandoUbicacionDestino = false
                                            mostrarDialogoExito = true
                                        } else {
                                            Log.e("ORDEN_TRASPASO_UI", "❌ Error actualizando línea: ${responseActualizar.code()}")
                                            mostrarDialogoError = "Error ${responseActualizar.code()} al actualizar línea de orden"
                                        }
                                    } catch (e: Exception) {
                                        Log.e("ORDEN_TRASPASO_UI", "❌ Excepción actualizando línea: ${e.message}")
                                        mostrarDialogoError = "Error al actualizar línea: ${e.message}"
                                    }
                                }
                            } else {
                                // Traspaso directo (no viene de orden) - solo completar traspaso
                                Log.d("ORDEN_TRASPASO_UI", "ℹ️ Traspaso directo completado (no requiere actualizar línea de orden)")
                                esperandoUbicacionDestino = false
                                mostrarDialogoExito = true
                            }
                        },
                        onError = { error ->
                            Log.e("ORDEN_TRASPASO_UI", "❌ Error completando traspaso: $error")
                            mostrarDialogoError = error
                        }
                    )
                }
            }
        }
    }
    
    // Mostrar órdenes ordenadas por fechaPlan (más cercanas primero), luego prioridad (5=máxima)
    val ordenesOrdenadas = ordenes.sortedWith(
        compareBy<OrdenTraspasoDto> { it.fechaPlan }
            .thenByDescending { it.prioridad }
    )
    
    // Cargar órdenes al iniciar y al volver de pantallas de proceso
    LaunchedEffect(user?.id) {
        user?.let { usuario ->
            // Siempre cargar órdenes para obtener estados actualizados
            viewModel.cargarOrdenes(usuario)
            
            // Comprobar si hay traspasos pendientes de órdenes
            traspasosLogic.comprobarTraspasoPendiente(
                usuarioId = usuario.id.toInt(),
                onSuccess = { lista ->
                    Log.d("ORDEN_TRASPASO_UI", "🔍 Traspasos pendientes encontrados: ${lista.size}")
                    traspasosPendientes = lista
                    if (lista.isNotEmpty()) {
                        esperandoUbicacionDestino = true
                        Log.d("ORDEN_TRASPASO_UI", "🚫 Activando bloqueo - hay traspasos pendientes")
                    }
                },
                onError = { errorMsg ->
                    Log.e("ORDEN_TRASPASO_UI", "❌ Error comprobando traspasos pendientes: $errorMsg")
                }
            )
        }
    }
    
    // Función para manejar la navegación a una orden
    fun handleNavigateToOrden(orden: OrdenTraspasoDto) {
        when (orden.estado) {
            "PENDIENTE" -> {
                // Si la orden está pendiente, iniciarla primero
                viewModel.iniciarOrden(
                    idOrden = orden.idOrdenTraspaso,
                    user = user!!
                )
                // La navegación se hará desde el ViewModel después de iniciar exitosamente
                onNavigateToDetail(orden.idOrdenTraspaso)
            }
            else -> {
                // Si la orden ya está iniciada, navegar directamente
                onNavigateToDetail(orden.idOrdenTraspaso)
            }
        }
    }

    Scaffold(
        topBar = {
            AppTopBar(
                sessionViewModel = sessionViewModel,
                navController = navController,
                title = "Órdenes de Traspaso",
                showBackButton = false,
                customNavigationIcon = {
                    IconButton(onClick = {
                        navController.navigate("home") {
                            popUpTo("home") { inclusive = false }
                        }
                    }) {
                        Icon(
                            imageVector = Icons.AutoMirrored.Filled.ArrowBack,
                            contentDescription = "Volver al Home"
                        )
                    }
                }
            )
        }
    ) { paddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
        ) {
            // Mensajes de estado
            error?.let { errorMsg ->
                Card(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(16.dp),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.errorContainer)
                ) {
                    Text(
                        text = errorMsg,
                        modifier = Modifier.padding(16.dp),
                        color = MaterialTheme.colorScheme.onErrorContainer
                    )
                }
            }

            mensaje?.let { msg ->
                Card(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(16.dp),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.primaryContainer)
                ) {
                    Text(
                        text = msg,
                        modifier = Modifier.padding(16.dp),
                        color = MaterialTheme.colorScheme.onPrimaryContainer
                    )
                }
            }

            // Lista de órdenes
            if (cargando) {
                Box(
                    modifier = Modifier.fillMaxSize(),
                    contentAlignment = Alignment.Center
                ) {
                    CircularProgressIndicator()
                }
            } else if (ordenesOrdenadas.isEmpty()) {
                Box(
                    modifier = Modifier.fillMaxSize(),
                    contentAlignment = Alignment.Center
                ) {
                    Column(horizontalAlignment = Alignment.CenterHorizontally) {
                        Icon(
                            Icons.Default.Assignment,
                            contentDescription = null,
                            modifier = Modifier.size(64.dp),
                            tint = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        Spacer(modifier = Modifier.height(20.dp))
                        Text(
                            text = "No tienes órdenes de traspaso",
                            style = MaterialTheme.typography.bodyLarge,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        Spacer(modifier = Modifier.height(8.dp))
                        Text(
                            text = "Las órdenes aparecerán aquí cuando se asignen",
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }
            } else {
                LazyColumn(
                    modifier = Modifier.fillMaxSize(),
                    contentPadding = PaddingValues(16.dp),
                    verticalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    items(ordenesOrdenadas) { orden ->
                        OrdenTraspasoCard(
                            orden = orden,
                            user = user!!,
                            onNavigateToDetail = { 
                                handleNavigateToOrden(orden)
                            }
                        )
                    }
                }
            }
        }
    }
    
    // Bloqueo cuando hay traspasos pendientes (igual que en TraspasosScreen)
    if (esperandoUbicacionDestino) {
        Box(
            modifier = Modifier
                .fillMaxSize()
                .background(Color.Black.copy(alpha = 0.5f))
                .zIndex(2f), // asegura que esté por encima del resto
            contentAlignment = Alignment.Center
        ) {
            Column(
                modifier = Modifier
                    .padding(24.dp)
                    .background(Color.White, RoundedCornerShape(12.dp))
                    .padding(16.dp)
                    .zIndex(3f)
            ) {
                Text("Ubicación destino requerida", style = MaterialTheme.typography.titleLarge)
                Spacer(modifier = Modifier.height(8.dp))
                Text("Escanee una ubicación para finalizar el traspaso.")
                Spacer(modifier = Modifier.height(16.dp))
            }
        }
        
        // Escaneo de ubicación destino (PDA)
        if (DeviceUtils.hasHardwareScanner(context)) {
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .focusRequester(focusRequester)
                    .focusable()
                    .onPreviewKeyEvent { event ->
                        if (event.nativeKeyEvent?.action == android.view.KeyEvent.ACTION_MULTIPLE) {
                            if (escaneoProcesado) return@onPreviewKeyEvent true
                            escaneoProcesado = true
                            
                            event.nativeKeyEvent.characters?.let { code ->
                                Log.d("ORDEN_TRASPASO_UI", "📥 Código escaneado (PDA): '$code'")
                                procesarUbicacionDestino(code.trim())
                            }
                            true
                        } else false
                    }
                    .layout { measurable, constraints ->
                        val placeable = measurable.measure(constraints)
                        layout(0, 0) { placeable.place(0, 0) }
                    }
            )
            
            LaunchedEffect(esperandoUbicacionDestino) {
                if (esperandoUbicacionDestino) {
                    delay(200)
                    focusRequester.requestFocus()
                }
            }
        }
        
        // Escaneo de ubicación destino (Cámara)
        if (!DeviceUtils.hasHardwareScanner(context)) {
            QRScannerView(
                modifier = Modifier
                    .fillMaxSize()
                    .zIndex(1f),
                onCodeScanned = { code ->
                    if (!esperandoUbicacionDestino || escaneoProcesado) return@QRScannerView
                    escaneoProcesado = true
                    Log.d("ORDEN_TRASPASO_UI", "📥 Código escaneado (cámara): '$code'")
                    procesarUbicacionDestino(code.trim())
                }
            )
        }
    }
    
    // Diálogo de éxito
    if (mostrarDialogoExito) {
        AlertDialog(
            onDismissRequest = { mostrarDialogoExito = false },
            title = { Text("Traspaso completado") },
            text = { Text("El traspaso se ha finalizado correctamente.") },
            confirmButton = {
                TextButton(onClick = { 
                    mostrarDialogoExito = false
                    esperandoUbicacionDestino = false
                    traspasosPendientes = emptyList()
                }) { Text("Aceptar") }
            }
        )
    }
    
    // Diálogo de error
    if (mostrarDialogoError != null) {
        AlertDialog(
            onDismissRequest = { mostrarDialogoError = null },
            title = { Text("Error") },
            text = { Text(mostrarDialogoError!!) },
            confirmButton = {
                TextButton(onClick = { 
                    mostrarDialogoError = null
                    escaneoProcesado = false
                }) { Text("Aceptar") }
            }
        )
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun OrdenTraspasoCard(
    orden: OrdenTraspasoDto,
    user: User,
    onNavigateToDetail: () -> Unit
) {
    // Calcular estadísticas de líneas del operario
    val lineasOperario = orden.lineas.filter { linea ->
        linea.idOperarioAsignado == user.id.toInt()
    }
    
    val tieneLineasPendientes = lineasOperario.any { it.estado == "PENDIENTE" }
    val tieneLineasEnProgreso = lineasOperario.any { it.estado == "EN_PROCESO" }
    val todasCompletadas = lineasOperario.all { it.completada }
    
    Card(
        modifier = Modifier.fillMaxWidth(),
        elevation = CardDefaults.cardElevation(defaultElevation = 4.dp)
    ) {
        Column(
            modifier = Modifier
                .padding(16.dp)
                .clickable { onNavigateToDetail() }
        ) {
            // Header de la orden
            Column(
                modifier = Modifier.fillMaxWidth()
            ) {
                Text(
                    text = orden.codigoOrden ?: "Sin código",
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.onSurface
                )
                Spacer(modifier = Modifier.height(8.dp))
                Row(
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    EstadoOrdenChip(estado = orden.estado)
                    PrioridadOrdenChip(prioridad = orden.prioridad)
                }
            }

            Spacer(modifier = Modifier.height(16.dp))

            // Información básica de la orden
            Column(
                modifier = Modifier.fillMaxWidth(),
                verticalArrangement = Arrangement.spacedBy(6.dp)
            ) {


                orden.codigoAlmacenDestino?.let { almacenDestino ->
                    InfoRowOrden(
                        icon = Icons.Default.LocationOn,
                        label = "Destino",
                        value = almacenDestino
                    )
                }

                orden.fechaPlan?.let { fechaPlan ->
                    InfoRowOrden(
                        icon = Icons.Default.Schedule,
                        label = "Fecha Plan",
                        value = formatearFecha(fechaPlan)
                    )
                }

                orden.fechaInicio?.let { fechaInicio ->
                    InfoRowOrden(
                        icon = Icons.Default.PlayArrow,
                        label = "Iniciado",
                        value = formatearFecha(fechaInicio)
                    )
                }

                // Estadísticas de líneas
                if (lineasOperario.isNotEmpty()) {
                    InfoRowOrden(
                        icon = Icons.Default.Assignment,
                        label = "Líneas asignadas",
                        value = "${lineasOperario.size} líneas"
                    )
                }
            }

            Spacer(modifier = Modifier.height(12.dp))

            // Resumen de progreso si hay líneas asignadas
            if (lineasOperario.isNotEmpty()) {
                val pendientes = lineasOperario.count { it.estado == "PENDIENTE" }
                val enProgreso = lineasOperario.count { it.estado == "EN_PROCESO" }
                val completadas = lineasOperario.count { it.completada }
                
                Card(
                    colors = CardDefaults.cardColors(
                        containerColor = MaterialTheme.colorScheme.surfaceVariant
                    )
                ) {
                    Column(
                        modifier = Modifier.padding(12.dp)
                    ) {
                        Text(
                            text = "Progreso de líneas",
                            style = MaterialTheme.typography.labelMedium,
                            fontWeight = FontWeight.Bold
                        )
                        Spacer(modifier = Modifier.height(4.dp))
                        Text(
                            text = "Pendientes: $pendientes | En proceso: $enProgreso | Completadas: $completadas",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }
            }

        }
    }
}

@Composable
fun EstadoOrdenChip(estado: String) {
    val (texto, color) = when (estado) {
        "PENDIENTE" -> "Pendiente" to Color(0xFFFF9800)     // Naranja
        "EN_PROCESO" -> "En Proceso" to Color(0xFF2196F3)   // Azul
        "COMPLETADA" -> "Completada" to Color(0xFF4CAF50)   // Verde
        "BLOQUEADA" -> "🔒 Bloqueada" to Color(0xFFF44336)  // Rojo
        else -> estado to Color(0xFF9E9E9E)                 // Gris
    }
    
    Surface(
        color = color,
        shape = MaterialTheme.shapes.small
    ) {
        Text(
            text = texto,
            style = MaterialTheme.typography.labelSmall,
            color = Color.White,
            fontWeight = FontWeight.Bold,
            modifier = Modifier.padding(horizontal = 8.dp, vertical = 4.dp)
        )
    }
}

@Composable
fun PrioridadOrdenChip(prioridad: Int) {
    val (texto, color) = when (prioridad) {
        1 -> "Muy Baja" to Color(0xFF4CAF50) // Verde
        2 -> "Baja" to Color(0xFF8BC34A)      // Verde claro
        3 -> "Media" to Color(0xFFFF9800)     // Naranja
        4 -> "Alta" to Color(0xFFFF5722)      // Rojo naranja
        5 -> "Muy Alta" to Color(0xFFD32F2F)  // Rojo
        else -> "Sin Prioridad" to Color(0xFF9E9E9E) // Gris
    }
    
    Card(
        modifier = Modifier.size(width = 80.dp, height = 24.dp),
        colors = CardDefaults.cardColors(containerColor = color),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
    ) {
        Box(
            modifier = Modifier.fillMaxSize(),
            contentAlignment = Alignment.Center
        ) {
            Text(
                text = texto,
                style = MaterialTheme.typography.labelSmall,
                color = Color.White,
                fontWeight = FontWeight.Bold,
                textAlign = TextAlign.Center
            )
        }
    }
}

@Composable
fun InfoRowOrden(
    icon: androidx.compose.ui.graphics.vector.ImageVector,
    label: String,
    value: String
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            modifier = Modifier.size(16.dp),
            tint = MaterialTheme.colorScheme.onSurfaceVariant
        )
        Spacer(modifier = Modifier.width(8.dp))
        Text(
            text = "$label:",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.width(100.dp)
        )
        Text(
            text = value,
            style = MaterialTheme.typography.bodySmall,
            fontWeight = FontWeight.Medium
        )
    }
}

private fun formatearFecha(fecha: String): String {
    return try {
        val inputFormat = SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss", Locale.getDefault())
        val outputFormat = SimpleDateFormat("dd-MM-yyyy", Locale.getDefault())
        val date = inputFormat.parse(fecha)
        outputFormat.format(date ?: Date())
    } catch (e: Exception) {
        fecha
    }
}