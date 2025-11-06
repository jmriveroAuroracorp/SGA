package com.example.sga.view.conteos

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import android.util.Log
import com.example.sga.data.model.conteos.OrdenConteo
import com.example.sga.data.model.conteos.LecturaConteo
import com.example.sga.view.components.AppTopBar
import com.example.sga.utils.FormatUtils
import androidx.compose.foundation.clickable
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.style.TextAlign
import com.example.sga.view.conteos.EstadoChip
import com.example.sga.view.conteos.InfoRow

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ConteoScreen(
    conteoViewModel: ConteoViewModel,
    conteoLogic: ConteoLogic,
    sessionViewModel: com.example.sga.view.app.SessionViewModel,
    navController: androidx.navigation.NavHostController,
    onNavigateToDetail: (String) -> Unit
) {
    val ordenes by conteoViewModel.ordenes.collectAsState()
    val cargando by conteoViewModel.cargando.collectAsState()
    val error by conteoViewModel.error.collectAsState()
    val mensaje by conteoViewModel.mensaje.collectAsState()
    val mostrarDialogoCrearOrden by conteoViewModel.mostrarDialogoCrearOrden.collectAsState()
    val mostrarDialogoAsignarOperario by conteoViewModel.mostrarDialogoAsignarOperario.collectAsState()
    val ordenParaAsignar by conteoViewModel.ordenParaAsignar.collectAsState()
    val user by sessionViewModel.user.collectAsState()

    // Mostrar todas las órdenes ordenadas por prioridad (5 = máxima prioridad)
    val ordenesActivas = ordenes.sortedByDescending { it.prioridad }

    // Log resumido para debugging
    if (ordenes.isNotEmpty()) {
        Log.d("ConteoScreen", "📊 Total de órdenes: ${ordenes.size}")
    }

    // Cargar órdenes si no están ya cargadas
    LaunchedEffect(user?.id) {
        user?.let { usuario ->
            if (ordenes.isEmpty()) {
                Log.d("ConteoScreen", "🚀 Cargando órdenes para usuario: ${usuario.id}")
                conteoLogic.listarOrdenes(usuario)
            } else {
                Log.d("ConteoScreen", "✅ Usando órdenes ya cargadas: ${ordenes.size}")
            }
        }
    }

    // Función para manejar la navegación a una orden
    fun handleNavigateToOrden(orden: OrdenConteo) {
        when (orden.estado) {
            "PLANIFICADO" -> {
                // Si la orden está planificada, mostrar diálogo de asignación
                Log.d("ConteoScreen", "🤔 Mostrando diálogo de asignación para orden PLANIFICADO: ${orden.guidID}")
                conteoViewModel.setOrdenParaAsignar(orden)
                conteoViewModel.setMostrarDialogoAsignarOperario(true)
            }
            "ASIGNADO" -> {
                // Si la orden está asignada, primero iniciarla
                Log.d("ConteoScreen", "🚀 Iniciando orden ASIGNADO: ${orden.guidID}")
                conteoLogic.iniciarOrden(
                    guidID = orden.guidID,
                    codigoOperario = user?.id ?: "",
                    onSuccess = {
                        // Recargar órdenes para actualizar el estado
                        user?.let { usuario ->
                            conteoLogic.listarOrdenes(usuario)
                        }
                        // Navegar después de iniciar exitosamente
                        onNavigateToDetail(orden.guidID)
                    },
                    onError = { errorMsg ->
                        Log.e("ConteoScreen", "❌ Error al iniciar orden: $errorMsg")
                        // No navegar si hay error
                    }
                )
            }
            else -> {
                // Si la orden ya está iniciada o en otro estado, navegar directamente
                Log.d("ConteoScreen", "➡️ Navegando a orden ${orden.estado}: ${orden.guidID}")
                onNavigateToDetail(orden.guidID)
            }
        }
    }

    Scaffold(
        topBar = {
            AppTopBar(
                sessionViewModel = sessionViewModel,
                navController = navController,
                title = "Órdenes de Conteo",
                showBackButton = true
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
            } else if (ordenesActivas.isEmpty()) {
                Box(
                    modifier = Modifier.fillMaxSize(),
                    contentAlignment = Alignment.Center
                ) {
                    Column(horizontalAlignment = Alignment.CenterHorizontally) {
                        Icon(
                            Icons.Default.Inventory,
                            contentDescription = null,
                            modifier = Modifier.size(64.dp),
                            tint = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        Spacer(modifier = Modifier.height(20.dp))
                        Text(
                            text = "No tienes órdenes de conteo",
                            style = MaterialTheme.typography.bodyLarge,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        Spacer(modifier = Modifier.height(8.dp))
                        Text(
                            text = "Las órdenes aparecerán aquí",
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
                    items(ordenesActivas) { orden ->
                        OrdenConteoCard(
                            orden = orden,
                            conteoViewModel = conteoViewModel,
                                                    onNavigateToProceso = { _ ->
                            handleNavigateToOrden(orden)
                        }
                        )
                    }
                }
            }
        }

        // Dialogo para crear orden
        if (mostrarDialogoCrearOrden) {
            CrearOrdenDialog(
                onDismiss = { conteoViewModel.setMostrarDialogoCrearOrden(false) },
                onConfirm = { titulo, visibilidad, modoGeneracion, alcance, filtrosJson, creadoPorCodigo, codigoOperario ->
                    conteoLogic.crearOrden(titulo, visibilidad, modoGeneracion, alcance, filtrosJson, creadoPorCodigo, codigoOperario)
                }
            )
        }

        // Dialogo para asignar operario
        if (mostrarDialogoAsignarOperario && ordenParaAsignar != null) {
            AsignarOperarioDialog(
                orden = ordenParaAsignar!!,
                onDismiss = {
                    conteoViewModel.setMostrarDialogoAsignarOperario(false)
                    conteoViewModel.setOrdenParaAsignar(null)
                },
                onConfirm = { orden ->
                                    conteoLogic.asignarOperario(
                    guidID = orden.guidID,
                    codigoOperario = user?.id ?: "",
                        comentario = null
                    )
                    // Recargar órdenes después de asignar
                    user?.let { usuario ->
                        conteoLogic.listarOrdenes(usuario)
                    }
                    conteoViewModel.setMostrarDialogoAsignarOperario(false)
                    conteoViewModel.setOrdenParaAsignar(null)
                }
            )
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun OrdenConteoCard(
    orden: OrdenConteo,
    conteoViewModel: ConteoViewModel,
    onNavigateToProceso: (String) -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        elevation = CardDefaults.cardElevation(defaultElevation = 4.dp)
    ) {
        Column(
            modifier = Modifier
                .padding(16.dp)
                .clickable { onNavigateToProceso(orden.guidID) }
        ) {
            // Header de la orden
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Column(modifier = Modifier.weight(1f)) {
                    Text(
                        text = orden.titulo,
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    Spacer(modifier = Modifier.height(8.dp))
                    Row(
                        horizontalArrangement = Arrangement.spacedBy(8.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        EstadoChip(estado = orden.estado)
                        PrioridadChip(prioridad = orden.prioridad)
                    }
                }
                Spacer(modifier = Modifier.width(8.dp))
                Icon(
                    Icons.Default.ArrowForward,
                    contentDescription = "Ir al proceso",
                    tint = MaterialTheme.colorScheme.primary
                )
            }

            Spacer(modifier = Modifier.height(16.dp))

            // Información básica de la orden
            Column(
                modifier = Modifier.fillMaxWidth(),
                verticalArrangement = Arrangement.spacedBy(6.dp)
            ) {
                InfoRow(
                    icon = Icons.Default.Storage,
                    label = "Alcance",
                    value = orden.alcance.replace("_", " ")
                )

                InfoRow(
                    icon = Icons.Default.Visibility,
                    label = "Visibilidad",
                    value = orden.visibilidad.replace("_", " ")
                )

                orden.codigoAlmacen?.let { almacen ->
                    InfoRow(
                        icon = Icons.Default.Business,
                        label = "Almacén",
                        value = almacen
                    )
                }

                InfoRow(
                    icon = Icons.Default.Schedule,
                    label = "Asignado",
                    value = orden.fechaAsignacion?.let { FormatUtils.formatearFecha(it) } ?: "No asignado"
                )

                orden.fechaInicio?.let { fechaInicio ->
                    InfoRow(
                        icon = Icons.Default.PlayArrow,
                        label = "Iniciado",
                        value = FormatUtils.formatearFecha(fechaInicio) ?: ""
                    )
                }
            }

            Spacer(modifier = Modifier.height(12.dp))

            // Botón de acción
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.End
            ) {
                                                 TextButton(
                    onClick = { onNavigateToProceso(orden.guidID) },
                     colors = ButtonDefaults.textButtonColors(
                         contentColor = MaterialTheme.colorScheme.primary
                     )
                 ) {
                    Text(
                        text = when (orden.estado) {
                            "ASIGNADO" -> "Iniciar Conteo"
                            "EN_PROCESO" -> "Continuar Conteo"
                            else -> "Ver Detalles"
                        },
                        fontWeight = FontWeight.SemiBold
                    )
                    Spacer(modifier = Modifier.width(4.dp))
                    Icon(
                        Icons.Default.PlayArrow,
                        contentDescription = null,
                        modifier = Modifier.size(16.dp)
                    )
                }
            }
        }
    }
}





@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun CrearOrdenDialog(
    onDismiss: () -> Unit,
    onConfirm: (String, String, String, String, String?, String, String?) -> Unit
) {
    var titulo by remember { mutableStateOf("") }
    var visibilidad by remember { mutableStateOf("VISUAL") }
    var modoGeneracion by remember { mutableStateOf("AUTO") }
    var alcance by remember { mutableStateOf("ALMACEN") }
    var filtrosJson by remember { mutableStateOf("") }
    var creadoPorCodigo by remember { mutableStateOf("") }
    var codigoOperario by remember { mutableStateOf("") }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Crear Orden de Conteo") },
        text = {
            Column(
                modifier = Modifier.fillMaxWidth(),
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                OutlinedTextField(
                    value = titulo,
                    onValueChange = { titulo = it },
                    label = { Text("Título") },
                    modifier = Modifier.fillMaxWidth()
                )
                
                OutlinedTextField(
                    value = creadoPorCodigo,
                    onValueChange = { creadoPorCodigo = it },
                    label = { Text("Creado por") },
                    modifier = Modifier.fillMaxWidth()
                )
                
                OutlinedTextField(
                    value = codigoOperario,
                    onValueChange = { codigoOperario = it },
                    label = { Text("Código Operario (opcional)") },
                    modifier = Modifier.fillMaxWidth()
                )
                
                OutlinedTextField(
                    value = filtrosJson,
                    onValueChange = { filtrosJson = it },
                    label = { Text("Filtros JSON (opcional)") },
                    modifier = Modifier.fillMaxWidth()
                )
            }
        },
        confirmButton = {
            TextButton(
                onClick = {
                    if (titulo.isNotBlank() && creadoPorCodigo.isNotBlank()) {
                        onConfirm(
                            titulo,
                            visibilidad,
                            modoGeneracion,
                            alcance,
                            filtrosJson.takeIf { it.isNotBlank() },
                            creadoPorCodigo,
                            codigoOperario.takeIf { it.isNotBlank() }
                        )
                    }
                }
            ) {
                Text("Crear")
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) {
                Text("Cancelar")
            }
        }
    )
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AsignarOperarioDialog(
    orden: OrdenConteo,
    onDismiss: () -> Unit,
    onConfirm: (OrdenConteo) -> Unit
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Asignar Orden de Conteo") },
        text = { 
                            Text("¿Desea asignarse esta orden de conteo?\n\n${orden.titulo}\nID: ${orden.guidID}")
        },
        confirmButton = {
            Button(onClick = { onConfirm(orden) }) {
                Text("Sí, asignarme")
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) {
                Text("No, cancelar")
            }
        }
    )
}

@Composable
fun PrioridadChip(prioridad: Int) {
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
