package com.example.sga.view.ordenes

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.focusable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.*
import androidx.compose.material3.Switch
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.input.key.Key
import androidx.compose.ui.input.key.KeyEventType
import androidx.compose.ui.input.key.key
import androidx.compose.ui.input.key.onPreviewKeyEvent
import androidx.compose.ui.input.key.type
import androidx.compose.ui.layout.layout
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.zIndex
import androidx.compose.ui.draw.scale
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.compose.runtime.collectAsState
import com.example.sga.data.dto.ordenes.*
import com.example.sga.data.dto.etiquetas.LogImpresionDto
import com.example.sga.data.dto.stock.ArticuloDto
import com.example.sga.data.dto.traspasos.*
import com.example.sga.data.model.stock.Stock
import com.example.sga.data.model.user.User
import com.example.sga.service.lector.DeviceUtils
import com.example.sga.service.scanner.QRScannerView
import com.example.sga.view.traspasos.TraspasosLogic
import com.example.sga.view.traspasos.TraspasosViewModel
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import java.text.SimpleDateFormat
import java.time.LocalDateTime
import java.util.*

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun OrdenTraspasoProcesoScreen(
    ordenId: String,
    sessionViewModel: com.example.sga.view.app.SessionViewModel,
    navController: androidx.navigation.NavHostController
) {
    val user by sessionViewModel.user.collectAsState()
    val viewModel: OrdenTraspasoViewModel = viewModel()
    
    // Inicializar el ViewModel con SessionViewModel
    LaunchedEffect(Unit) {
        viewModel.init(sessionViewModel)
    }
    
    val ordenSeleccionada by viewModel.ordenSeleccionada.collectAsStateWithLifecycle()
    val cargando by viewModel.cargando.collectAsStateWithLifecycle()
    val error by viewModel.error.collectAsStateWithLifecycle()
    val mensaje by viewModel.mensaje.collectAsStateWithLifecycle()
    val stockDisponible by viewModel.stockDisponible.collectAsStateWithLifecycle()
    
    val cantidadMovida by viewModel.cantidadMovida.collectAsStateWithLifecycle()
    val ubicacionOrigenSeleccionada by viewModel.ubicacionOrigenSeleccionada.collectAsStateWithLifecycle()
    val ubicacionDestino by viewModel.ubicacionDestino.collectAsStateWithLifecycle()
    val paletDestino by viewModel.paletDestino.collectAsStateWithLifecycle()
    val paletListoParaUbicar by viewModel.paletListoParaUbicar.collectAsStateWithLifecycle()
    val paletsPendientes by viewModel.paletsPendientes.collectAsStateWithLifecycle()
    val almacenDestinoPalet by viewModel.almacenDestinoPalet.collectAsStateWithLifecycle()
    val ubicacionDestinoPalet by viewModel.ubicacionDestinoPalet.collectAsStateWithLifecycle()
    val paletSeleccionado by viewModel.paletSeleccionado.collectAsStateWithLifecycle()
    
    // Estados mejorados para manejo de errores
    val estadoUI by viewModel.estadoUI.collectAsStateWithLifecycle()
    val mostrarDialogoErrorCrearMateria by viewModel.mostrarDialogoErrorCrearMateria.collectAsStateWithLifecycle()
    val detallesErrorCrearMateria by viewModel.detallesErrorCrearMateria.collectAsStateWithLifecycle()
    val mostrarAdvertenciaCantidad by viewModel.mostrarAdvertenciaCantidad.collectAsStateWithLifecycle()
    val mensajeAdvertenciaCantidad by viewModel.mensajeAdvertenciaCantidad.collectAsStateWithLifecycle()
    
    // Estados de impresión desde ViewModel
    val mostrarDialogoImpresionVM by viewModel.mostrarDialogoImpresion.collectAsStateWithLifecycle()
    val paletParaImprimirVM by viewModel.paletParaImprimir.collectAsStateWithLifecycle()
    val articuloParaLinea by viewModel.articuloParaLinea.collectAsStateWithLifecycle()
    val ubicacionParaLinea by viewModel.ubicacionParaLinea.collectAsStateWithLifecycle()
    val lineaParaAnadir by viewModel.lineaParaAnadir.collectAsStateWithLifecycle()
    val mostrarDialogoCantidadVM by viewModel.mostrarDialogoCantidad.collectAsStateWithLifecycle()
    
    // Estados para diálogo de ajuste de inventario
    val mostrarDialogoAjusteInventario by viewModel.mostrarDialogoAjusteInventario.collectAsStateWithLifecycle()
    val cantidadEncontrada by viewModel.cantidadEncontrada.collectAsStateWithLifecycle()
    val lineaParaAjuste by viewModel.lineaParaAjuste.collectAsStateWithLifecycle()
    
    // Estado local para diálogo de cantidad
    var cantidadSeleccionada by remember { mutableStateOf("") }
    
    val lineaSeleccionada by viewModel.lineaSeleccionada.collectAsStateWithLifecycle()
    var mostrarEjecutar by remember { mutableStateOf(false) }
    var mostrarUbicarPalet by remember { mutableStateOf(false) }
    var mostrarPaletsPendientes by remember { mutableStateOf(false) }
    
    // Estados para cerrar palet
    var mostrarDialogoCerrarPalet by remember { mutableStateOf(false) }
    var idPaletParaCerrar by remember { mutableStateOf<String?>(null) }
    var esperandoUbicacionDestino by remember { mutableStateOf(false) }
    var traspasoPendienteId by remember { mutableStateOf<String?>(null) }
    
    // Cargar e iniciar orden al entrar en la pantalla (una sola vez)
    LaunchedEffect(ordenId, user?.id) {
        user?.let { usuario ->
            // Primero cargar lista de órdenes, luego iniciar la específica
            viewModel.cargarOrdenes(usuario)
            // Iniciar la orden específica (esto carga las líneas completas)
            viewModel.iniciarOrden(ordenId, usuario)
            // Cargar palets pendientes para mostrar el botón solo si los hay
            android.util.Log.d("UI_PALETS", "🔍 Cargando palets pendientes para orden: $ordenId")
            viewModel.verificarPaletsPendientes(ordenId, usuario)
        }
    }
    
    // Mostrar mensajes (limpiar automáticamente según el tipo de mensaje)
    LaunchedEffect(mensaje) {
        mensaje?.let { msg ->
            when {
                // Mensajes de supervisión se limpian manualmente desde el ViewModel
                msg.contains("supervisión") || msg.contains("Supervisión") -> {
                    // No limpiar automáticamente
                }
                // Mensajes de éxito detallados se muestran por más tiempo
                msg.contains("✅ Artículo añadido al palet exitosamente") -> {
                    delay(4000) // Mostrar por 4 segundos
                    viewModel.limpiarMensajes()
                }
                // Mensajes simples se limpian automáticamente
                else -> {
                    viewModel.limpiarMensajes()
                }
            }
        }
    }
    
    // Detectar cuando hay palet listo para ubicar
    LaunchedEffect(paletListoParaUbicar) {
        paletListoParaUbicar?.let {
            mostrarUbicarPalet = true
        }
    }
    
    if (mostrarUbicarPalet && paletListoParaUbicar != null && ordenSeleccionada != null) {
        // Pantalla de ubicar palet
        val codigoGS1Palet by viewModel.codigoGS1Palet.collectAsStateWithLifecycle()
        UbicarPaletScreen(
            orden = ordenSeleccionada!!,
            paletDestino = paletListoParaUbicar!!,
            codigoGS1 = codigoGS1Palet,
            user = user!!,
            viewModel = viewModel,
            sessionViewModel = sessionViewModel,
            onNavigateBack = {
                mostrarUbicarPalet = false
                viewModel.setPaletListoParaUbicar(null)
                viewModel.setCodigoGS1Palet(null)
            }
        )
    } else if (mostrarPaletsPendientes && ordenSeleccionada != null) {
        // Pantalla de palets pendientes
        PaletsPendientesScreen(
            orden = ordenSeleccionada!!,
            paletsPendientes = paletsPendientes,
            user = user!!,
            viewModel = viewModel,
            onNavigateBack = { mostrarPaletsPendientes = false }
        )
    } else {
        // Pantalla principal de proceso
        Scaffold(
            topBar = {
                com.example.sga.view.components.AppTopBar(
                    sessionViewModel = sessionViewModel,
                    navController = navController,
                    title = "Órdenes de traspasos",
                    showBackButton = false,
                    customNavigationIcon = {
                        IconButton(onClick = {
                            // Si hay una línea seleccionada, volver a selección de líneas
                            if (lineaSeleccionada != null) {
                                // Solo limpiar la línea seleccionada para volver a la lista
                                viewModel.setLineaSeleccionada(null)
                            } else {
                                // Si no hay línea seleccionada, volver a órdenes
                                navController.popBackStack()
                            }
                        }) {
                            Icon(
                                imageVector = Icons.AutoMirrored.Filled.ArrowBack,
                                contentDescription = if (lineaSeleccionada != null) "Volver a Líneas" else "Volver a Órdenes"
                            )
                        }
                    }
                )
            }
        ) { paddingValues ->
            if (cargando) {
                Box(
                    modifier = Modifier.fillMaxSize(),
                    contentAlignment = Alignment.Center
                ) {
                    CircularProgressIndicator()
                }
            } else {
                Box(
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(paddingValues)
                ) {
                    // CONTENIDO PRINCIPAL CON SCROLL
                    Column(
                        modifier = Modifier
                            .fillMaxSize()
                            .verticalScroll(rememberScrollState())
                    ) {
                        // HEADER COMPACTO
                        ordenSeleccionada?.let { orden ->
                        val currentUser = user
                        HeaderCompacto(
                            orden = orden,
                            user = currentUser,
                            paletActivo = paletSeleccionado?.codigoPalet,
                            paletsPendientes = paletsPendientes,
                            onVerPaletsPendientes = {
                                currentUser?.let { u ->
                                    viewModel.verificarPaletsPendientes(orden.idOrdenTraspaso, u)
                                    mostrarPaletsPendientes = true
                                }
                            },
                            paletSeleccionado = paletSeleccionado,
                            onCerrarPalet = { paletId ->
                                android.util.Log.d("TOGGLE_PALET", "🔒 onCerrarPalet llamado con paletId: $paletId")
                                idPaletParaCerrar = paletId
                                mostrarDialogoCerrarPalet = true
                                android.util.Log.d("TOGGLE_PALET", "📋 Diálogo activado: mostrarDialogoCerrarPalet=$mostrarDialogoCerrarPalet")
                            },
                            onAbrirPalet = { paletId ->
                                android.util.Log.d("TOGGLE_PALET", "🔓 onAbrirPalet llamado con paletId: $paletId")
                                user?.let { u ->
                                    viewModel.abrirPalet(
                                        id = paletId,
                                        usuarioId = u.id.toInt(),
                                        onSuccess = {
                                            viewModel.setMensaje("Palet abierto correctamente")
                                        },
                                        onError = { error ->
                                            viewModel.setMensaje("Error al abrir palet: $error")
                                        }
                                    )
                                }
                            },
                            viewModel = viewModel,
                            modifier = Modifier.wrapContentHeight()
                        )
                    }
                    
                    // Mensajes de estado (compactos)
                error?.let { errorMsg ->
                    Card(
                        modifier = Modifier
                            .fillMaxWidth()
                                .padding(horizontal = 16.dp, vertical = 4.dp),
                        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.errorContainer)
                    ) {
                        Text(
                            text = errorMsg,
                                modifier = Modifier.padding(12.dp),
                                color = MaterialTheme.colorScheme.onErrorContainer,
                                style = MaterialTheme.typography.bodySmall
                        )
                    }
                }

                mensaje?.let { msg ->
                    Card(
                        modifier = Modifier
                            .fillMaxWidth()
                                .padding(horizontal = 16.dp, vertical = 4.dp),
                        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.primaryContainer)
                    ) {
                        Text(
                            text = msg,
                                modifier = Modifier.padding(12.dp),
                                color = MaterialTheme.colorScheme.onPrimaryContainer,
                                style = MaterialTheme.typography.bodySmall
                            )
                        }
                    }
                    
                    // CONTENIDO PRINCIPAL
                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(16.dp)
                    ) {
                        ordenSeleccionada?.let { orden ->
                                            val currentUser = user
                            currentUser?.let { u ->
                                AccionPrincipal(
                                    orden = orden,
                                    user = u,
                                    lineaSeleccionada = lineaSeleccionada,
                                    stockDisponible = stockDisponible,
                                    ubicacionOrigenSeleccionada = ubicacionOrigenSeleccionada,
                                    viewModel = viewModel,
                                    sessionViewModel = sessionViewModel,
                                    onLineaSeleccionada = { linea ->
                                        // Seleccionar línea y esperar por si el backend la subdivide
                                        viewModel.seleccionarLineaConVerificacion(linea, u)
                                    }
                                )
                            }
                        }
                        }
                        
                        // Espacio para que la información de apoyo no tape el contenido
                        Spacer(modifier = Modifier.height(200.dp))
                    }
                    
                    // INFORMACIÓN DE APOYO FIJA EN LA PARTE INFERIOR
                    InformacionApoyo(
                        modifier = Modifier
                            .fillMaxWidth()
                            .align(Alignment.BottomCenter),
                        lineaSeleccionada = lineaSeleccionada,
                        stockDisponible = stockDisponible,
                        paletSeleccionado = paletSeleccionado,
                        user = user,
                        viewModel = viewModel
                    )
                }
            }
        }
    }
    
    // DIÁLOGO DE IMPRESIÓN OBLIGATORIA COMPLETO - EN FUNCIÓN PRINCIPAL
    if (mostrarDialogoImpresionVM && paletParaImprimirVM != null) {
        android.util.Log.d("DIALOGO_PRINCIPAL", "✅ DIÁLOGO ACTIVO EN FUNCIÓN PRINCIPAL - Palet: ${paletParaImprimirVM!!.codigoPalet}")
        
        // Estados locales para el diálogo
        var copias by remember { mutableIntStateOf(1) }
        var dropOpenImpresora by remember { mutableStateOf(false) }
        
        // Estados del ViewModel
        val impresoras by viewModel.impresoras.collectAsStateWithLifecycle()
        val impresoraSeleccionada by sessionViewModel.impresoraSeleccionada.collectAsState()
        val impresoraNombre = impresoraSeleccionada ?: ""
        
        AlertDialog(
            onDismissRequest = { }, // ¡OBLIGATORIO! No se puede cerrar
            title = { Text("⚠️ Impresión obligatoria") },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                    Text(
                        text = "Debe imprimir la etiqueta del palet antes de continuar:",
                        fontWeight = FontWeight.Bold
                    )
                    Text("📦 Palet: ${paletParaImprimirVM!!.codigoPalet}")
                    Text("🏷️ GS1: ${paletParaImprimirVM!!.codigoGS1}")
                    
                    Spacer(modifier = Modifier.height(8.dp))
                    
                    Text("Impresora:", style = MaterialTheme.typography.bodyMedium)
                    Box {
                        OutlinedTextField(
                            readOnly = true,
                            value = impresoraNombre,
                            onValueChange = {},
                            label = { Text("Impresora") },
                            modifier = Modifier.fillMaxWidth(),
                            trailingIcon = {
                                IconButton(onClick = { dropOpenImpresora = !dropOpenImpresora }) {
                                    Icon(Icons.Default.ArrowDropDown, contentDescription = null)
                                }
                            }
                        )
                        DropdownMenu(
                            expanded = dropOpenImpresora,
                            onDismissRequest = { dropOpenImpresora = false }
                        ) {
                            impresoras.forEach { imp ->
                                DropdownMenuItem(
                                    text = { Text(imp.nombre) },
                                    onClick = {
                                        dropOpenImpresora = false
                                        sessionViewModel.actualizarImpresora(imp.nombre)
                                        viewModel.actualizarImpresoraSeleccionadaEnBD(imp.nombre, sessionViewModel)
                                    }
                                )
                            }
                        }
                    }

                    Text("Número de copias:", style = MaterialTheme.typography.bodyMedium)
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        IconButton(onClick = { if (copias > 1) copias-- }) {
                            Icon(Icons.Default.Remove, contentDescription = "Menos")
                        }
                        Text(copias.toString(), modifier = Modifier.padding(8.dp))
                        IconButton(onClick = { copias++ }) {
                            Icon(Icons.Default.Add, contentDescription = "Más")
                        }
                    }
                }
            },
            confirmButton = {
                TextButton(
                    onClick = {
                        android.util.Log.d("DIALOGO_PRINCIPAL", "🖨️ Botón imprimir pulsado")
                        
                        // Validar usuario
                        val usuario = sessionViewModel.user.value?.name
                        if (usuario == null) {
                            android.util.Log.e("IMPRESION_PALET", "❌ Usuario es null - ABORTANDO")
                            viewModel.setError("❌ Error: Usuario no identificado. Por favor, inicie sesión nuevamente.")
                            viewModel.cerrarDialogoImpresion()
                            return@TextButton
                        }
                        android.util.Log.d("IMPRESION_PALET", "✅ Usuario validado: $usuario")
                        
                        // Validar dispositivo
                        val dispositivo = sessionViewModel.dispositivo.value?.id
                        if (dispositivo == null) {
                            android.util.Log.e("IMPRESION_PALET", "❌ Dispositivo es null - ABORTANDO")
                            viewModel.setError("❌ Error: Dispositivo no identificado. Por favor, configure el dispositivo.")
                            viewModel.cerrarDialogoImpresion()
                            return@TextButton
                        }
                        android.util.Log.d("IMPRESION_PALET", "✅ Dispositivo validado: $dispositivo")
                        
                        // Validar impresora
                        android.util.Log.d("IMPRESION_PALET", "🔍 Buscando impresora con nombre: '$impresoraNombre'")
                        android.util.Log.d("IMPRESION_PALET", "📋 Impresoras disponibles: ${impresoras.map { it.nombre }}")
                        val impresora = impresoras.find { it.nombre == impresoraNombre }
                        if (impresora == null) {
                            android.util.Log.e("IMPRESION_PALET", "❌ Impresora '$impresoraNombre' NO ENCONTRADA - ABORTANDO")
                            viewModel.setError("❌ Error: Impresora '$impresoraNombre' no encontrada. Por favor, seleccione una impresora válida.")
                            viewModel.cerrarDialogoImpresion()
                            return@TextButton
                        }
                        android.util.Log.d("IMPRESION_PALET", "✅ Impresora encontrada: ${impresora.nombre} (ID: ${impresora.id})")

                        val dto = LogImpresionDto(
                            usuario = usuario,
                            dispositivo = dispositivo,
                            idImpresora = impresora.id,
                            etiquetaImpresa = 0,
                            tipoEtiqueta = 2,
                            copias = copias,
                            pathEtiqueta = "\\\\Sage200\\mrh\\Servicios\\PrintCenter\\ETIQUETAS\\PALET.nlbl",
                            codigoGS1 = paletParaImprimirVM!!.codigoGS1,
                            codigoPalet = paletParaImprimirVM!!.codigoPalet
                        )
                        
                        // Log del DTO de impresión
                        android.util.Log.d("IMPRESION_PALET", "🖨️ DTO de impresión enviado:")
                        android.util.Log.d("IMPRESION_PALET", "  📝 Usuario: $usuario")
                        android.util.Log.d("IMPRESION_PALET", "  📱 Dispositivo: $dispositivo")
                        android.util.Log.d("IMPRESION_PALET", "  🖨️ ID Impresora: ${impresora.id}")
                        android.util.Log.d("IMPRESION_PALET", "  📊 Etiqueta Impresa: 0")
                        android.util.Log.d("IMPRESION_PALET", "  🏷️ Tipo Etiqueta: 2")
                        android.util.Log.d("IMPRESION_PALET", "  📄 Copias: $copias")
                        android.util.Log.d("IMPRESION_PALET", "  📁 Path Etiqueta: \\\\Sage200\\mrh\\Servicios\\PrintCenter\\ETIQUETAS\\PALET.nlbl")
                        android.util.Log.d("IMPRESION_PALET", "  🏷️ Código GS1: ${paletParaImprimirVM!!.codigoGS1}")
                        android.util.Log.d("IMPRESION_PALET", "  📦 Código Palet: ${paletParaImprimirVM!!.codigoPalet}")
                        
                        viewModel.imprimirEtiquetaPalet(dto)
                        
                        // PASO 3: Cerrar diálogo de impresión y activar diálogo de cantidad
                        viewModel.cerrarDialogoImpresion()
                        
                        // Activar diálogo de selección de cantidad
                        if (lineaParaAnadir != null) {
                            // Usar la cantidad menor entre stock disponible y cantidad planificada
                            val cantidadDisponible = if (stockDisponible.isNotEmpty()) {
                                stockDisponible.first().cantidadDisponible
                            } else {
                                lineaParaAnadir!!.cantidadPlan
                            }
                            val cantidadPlanificada = lineaParaAnadir!!.cantidadPlan
                            val cantidadInicial = minOf(cantidadDisponible, cantidadPlanificada)
                            cantidadSeleccionada = cantidadInicial.toString()
                        }
                    },
                    enabled = impresoraNombre.isNotBlank()
                ) { 
                    Text("🖨️ Imprimir") 
                }
            }
            // NO hay dismissButton - Es obligatorio imprimir
        )
    }
    
    // DIÁLOGO DE SELECCIÓN DE CANTIDAD (replicado de TraspasosScreen.kt)
    if (mostrarDialogoCantidadVM && paletParaImprimirVM != null && articuloParaLinea != null && lineaParaAnadir != null) {
        // Inicializar cantidad cuando se active el diálogo
        LaunchedEffect(mostrarDialogoCantidadVM) {
            if (mostrarDialogoCantidadVM && lineaParaAnadir != null) {
                // Usar la cantidad menor entre stock disponible y cantidad planificada
                val cantidadDisponible = if (stockDisponible.isNotEmpty()) {
                    stockDisponible.first().cantidadDisponible
                } else {
                    lineaParaAnadir!!.cantidadPlan
                }
                val cantidadPlanificada = lineaParaAnadir!!.cantidadPlan
                val cantidadInicial = minOf(cantidadDisponible, cantidadPlanificada)
                
                cantidadSeleccionada = cantidadInicial.toString()
                android.util.Log.d("DIALOGO_CANTIDAD", "📊 Inicializando cantidad: $cantidadInicial (disponible: $cantidadDisponible, planificada: $cantidadPlanificada)")
            }
        }
        
        AlertDialog(
            onDismissRequest = { /* bloqueamos para forzar acción */ },
            title = { Text("Cantidad a añadir al palet") },
            text = {
                Column {
                    Text("📦 Artículo: ${articuloParaLinea!!.codigoArticulo}")
                    articuloParaLinea!!.descripcion?.let { desc ->
                        Text("📝 $desc", style = MaterialTheme.typography.bodySmall)
                    }
                    Text("📊 Cantidad planificada: ${lineaParaAnadir!!.cantidadPlan}")
                    Text("📦 Stock disponible: ${stockDisponible.firstOrNull()?.cantidadDisponible ?: 0.0}")
                    
                    Spacer(modifier = Modifier.height(8.dp))
                    
                    OutlinedTextField(
                        value = cantidadSeleccionada,
                        onValueChange = {
                            val limpio = it.filter { c -> c.isDigit() || c == '.' }
                            cantidadSeleccionada = limpio
                        },
                        label = { Text("Cantidad Cogida") },
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth(),
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal)
                    )
                    
                    Spacer(modifier = Modifier.height(8.dp))
                    
                    // Botón para ajustar inventario (solo visible si la línea está EN_PROCESO)
                    lineaParaAnadir?.let { linea ->
                        if (viewModel.puedeAjustarInventario(linea)) {
                            Button(
                                onClick = {
                                    android.util.Log.d("DIALOGO_CANTIDAD", "🔧 Activando ajuste de inventario para línea: ${linea.idLineaOrdenTraspaso}")
                                    viewModel.activarDialogoAjusteInventario(linea)
                                },
                                modifier = Modifier.fillMaxWidth(),
                                colors = ButtonDefaults.buttonColors(
                                    containerColor = MaterialTheme.colorScheme.secondary
                                )
                            ) {
                                Text("🔧 Ajustar Inventario")
                            }
                        }
                    }
                }
            },
            confirmButton = {
                TextButton(onClick = {
                    android.util.Log.d("DIALOGO_CANTIDAD", "🔘 Click en Añadir al palet")
                    
                    val qty = cantidadSeleccionada.toDoubleOrNull() ?: 0.0
                    val stockMax = stockDisponible.firstOrNull()?.cantidadDisponible ?: 0.0
                    
                    android.util.Log.d("DIALOGO_CANTIDAD", "📊 Cantidad: $qty, Stock máximo: $stockMax")
                    
                    // Validación con mensaje de error
                    if (qty <= 0.0) {
                        android.util.Log.d("DIALOGO_CANTIDAD", "❌ Cantidad inválida: $qty")
                        viewModel.setError("❌ La cantidad debe ser mayor que 0")
                        return@TextButton
                    }
                    
                    if (qty > stockMax) {
                        android.util.Log.d("DIALOGO_CANTIDAD", "❌ Cantidad excede stock: $qty > $stockMax")
                        viewModel.setError("❌ No puedes reservar más de lo disponible: $stockMax unidades")
                        return@TextButton
                    }

                    // Verificar datos requeridos
                    val empresaCodigo = sessionViewModel.empresaSeleccionada.value?.codigo?.toShort()
                    val usuarioIdInt = user?.id?.toIntOrNull()
                    
                    android.util.Log.d("DIALOGO_CANTIDAD", "🏢 Empresa: $empresaCodigo, Usuario: $usuarioIdInt")
                    android.util.Log.d("DIALOGO_CANTIDAD", "📦 Palet: ${paletParaImprimirVM?.id}, Artículo: ${articuloParaLinea?.codigoArticulo}")
                    
                    if (empresaCodigo == null) {
                        android.util.Log.e("DIALOGO_CANTIDAD", "❌ Empresa no seleccionada")
                        viewModel.setError("❌ Error: Empresa no seleccionada")
                        return@TextButton
                    }
                    
                    if (usuarioIdInt == null) {
                        android.util.Log.e("DIALOGO_CANTIDAD", "❌ Usuario ID inválido")
                        viewModel.setError("❌ Error: Usuario no válido")
                        return@TextButton
                    }
                    
                    if (paletParaImprimirVM == null) {
                        android.util.Log.e("DIALOGO_CANTIDAD", "❌ Palet no disponible")
                        viewModel.setError("❌ Error: Palet no disponible")
                        return@TextButton
                    }
                    
                    if (articuloParaLinea == null) {
                        android.util.Log.e("DIALOGO_CANTIDAD", "❌ Artículo no disponible")
                        viewModel.setError("❌ Error: Artículo no disponible")
                        return@TextButton
                    }
                    
                    if (ubicacionParaLinea == null) {
                        android.util.Log.e("DIALOGO_CANTIDAD", "❌ Ubicación no disponible")
                        viewModel.setError("❌ Error: Ubicación no disponible")
                        return@TextButton
                    }
                    
                    // Añadir línea con cantidad seleccionada
                    val lineaDto = LineaPaletCrearDto(
                        codigoEmpresa = empresaCodigo,
                        usuarioId = usuarioIdInt,
                        codigoArticulo = articuloParaLinea!!.codigoArticulo,
                        descripcion = articuloParaLinea!!.descripcion,
                        lote = articuloParaLinea!!.partida,
                        fechaCaducidad = articuloParaLinea!!.fechaCaducidad,
                        cantidad = qty,
                        codigoAlmacen = ubicacionParaLinea!!.first,
                        ubicacion = ubicacionParaLinea!!.second
                    )
                    
                    val paletCodigo = paletParaImprimirVM!!.codigoPalet
                    val paletId = paletParaImprimirVM!!.id
                    
                    android.util.Log.d("DIALOGO_CANTIDAD", "🚀 Llamando anadirLinea con palet: $paletId")
                    
                    viewModel.anadirLinea(
                        idPalet = paletId,
                        dto = lineaDto
                    ) {
                        // Línea añadida exitosamente
                        android.util.Log.d("DIALOGO_CANTIDAD", "✅ Línea añadida exitosamente")
                        
                        // Mostrar mensaje de éxito más detallado
                        val mensajeExito = """
                            ✅ Artículo añadido al palet exitosamente
                            
                            📦 Palet: $paletCodigo
                            🏷️ Artículo: ${articuloParaLinea?.codigoArticulo}
                            📊 Cantidad: $qty unidades
                            
                            Continuando con la siguiente tarea...
                        """.trimIndent()
                        viewModel.setMensaje(mensajeExito)
                        
                        // Limpiar estados y cerrar diálogo
                        cantidadSeleccionada = ""
                        android.util.Log.d("DIALOGO_CANTIDAD", "🔒 Llamando cerrarDialogoCantidad()")
                        viewModel.cerrarDialogoCantidad()
                        android.util.Log.d("DIALOGO_CANTIDAD", "✅ cerrarDialogoCantidad() llamado")
                    }
                }) {
                    Text("Añadir al palet")
                }
            },
            dismissButton = {
                TextButton(onClick = {
                    android.util.Log.d("DIALOGO_CANTIDAD", "🔘 Click en Cancelar")
                    try {
                        cantidadSeleccionada = ""
                        android.util.Log.d("DIALOGO_CANTIDAD", "🧹 Cantidad limpiada")
                        viewModel.cerrarDialogoCantidad()
                        android.util.Log.d("DIALOGO_CANTIDAD", "✅ Diálogo cerrado exitosamente")
                    } catch (e: Exception) {
                        android.util.Log.e("DIALOGO_CANTIDAD", "❌ Error al cancelar: ${e.message}", e)
                        viewModel.setError("Error al cancelar: ${e.message}")
                    }
                }) {
                    Text("Cancelar")
                }
            }
        )
    }
    
    // DIÁLOGO DE AJUSTE DE INVENTARIO
    if (mostrarDialogoAjusteInventario && lineaParaAjuste != null) {
        AlertDialog(
            onDismissRequest = { /* bloqueamos para forzar acción */ },
            title = { Text("🔧 Ajustar Inventario") },
            text = {
                Column {
                    Text("📦 Artículo: ${lineaParaAjuste!!.codigoArticulo}")
                    lineaParaAjuste!!.descripcionArticulo?.let { desc ->
                        Text("📝 $desc", style = MaterialTheme.typography.bodySmall)
                    }
                    Text("📊 Cantidad planificada: ${lineaParaAjuste!!.cantidadPlan}")
                    Text("📦 Stock disponible: ${stockDisponible.firstOrNull()?.cantidadDisponible ?: 0.0}")
                    
                    Spacer(modifier = Modifier.height(8.dp))
                    
                    OutlinedTextField(
                        value = cantidadEncontrada,
                        onValueChange = {
                            val limpio = it.filter { c -> c.isDigit() || c == '.' }
                            viewModel.setCantidadEncontrada(limpio)
                        },
                        label = { Text("Cantidad Encontrada") },
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth(),
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal)
                    )
                    
                    Spacer(modifier = Modifier.height(8.dp))
                    
                    Text(
                        "⚠️ Introduce la cantidad real que has encontrado físicamente en la ubicación.",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            },
            confirmButton = {
                TextButton(
                    onClick = {
                        android.util.Log.d("DIALOGO_AJUSTE", "🔘 Click en Confirmar ajuste")
                        
                        val cantidad = cantidadEncontrada.toDoubleOrNull() ?: 0.0
                        
                        if (cantidad < 0) {
                            viewModel.setError("❌ La cantidad encontrada debe ser mayor o igual a 0")
                            return@TextButton
                        }
                        
                        viewModel.confirmarAjusteInventario(
                            onSuccess = {
                                android.util.Log.d("DIALOGO_AJUSTE", "✅ Ajuste confirmado exitosamente")
                                // Cerrar el diálogo después del éxito
                                viewModel.cerrarDialogoAjusteInventario()
                            },
                            onError = { error ->
                                android.util.Log.e("DIALOGO_AJUSTE", "❌ Error en ajuste: $error")
                                // Cerrar el diálogo también en caso de error para que el usuario pueda reintentar
                                viewModel.cerrarDialogoAjusteInventario()
                            }
                        )
                    },
                    enabled = cantidadEncontrada.isNotBlank()
                ) {
                    Text("Confirmar ajuste")
                }
            },
            dismissButton = {
                TextButton(onClick = {
                    android.util.Log.d("DIALOGO_AJUSTE", "🔘 Click en Cancelar")
                    viewModel.cerrarDialogoAjusteInventario()
                }) {
                    Text("Cancelar")
                }
            }
        )
    }
    
    // DIÁLOGO DE ERROR CREAR MATERIA - Manejo específico para errores de stock
    DialogoErrorCrearMateria(
        mostrar = mostrarDialogoErrorCrearMateria,
        detalles = detallesErrorCrearMateria,
        onDismiss = {
            viewModel.cerrarDialogoErrorCrearMateria()
        }
    )
    
    // DIÁLOGO DE ADVERTENCIA DE CANTIDAD - Validación previa opcional
    DialogoAdvertenciaCantidad(
        mostrar = mostrarAdvertenciaCantidad,
        mensaje = mensajeAdvertenciaCantidad,
        onConfirmar = {
            // Obtener datos necesarios para continuar
            val lineaActual = lineaSeleccionada
            val cantidad = cantidadMovida.toDoubleOrNull()
            if (lineaActual != null && cantidad != null) {
                viewModel.confirmarAdvertenciaCantidad(
                    idLinea = lineaActual.idLineaOrdenTraspaso,
                    cantidadMovida = cantidad,
                    paletDestino = paletDestino.ifEmpty { null }
                )
            }
        },
        onCancelar = {
            viewModel.cerrarDialogoAdvertenciaCantidad()
        }
    )
    
    // DIÁLOGO DE CONFIRMACIÓN PARA CERRAR PALET
    if (mostrarDialogoCerrarPalet && idPaletParaCerrar != null) {
        AlertDialog(
            onDismissRequest = { mostrarDialogoCerrarPalet = false },
            title = { Text("Cerrar palet") },
            text = {
                Column {
                    Text(
                        text = "¿Está seguro de que desea cerrar el palet?",
                        fontWeight = FontWeight.Bold
                    )
                    Spacer(modifier = Modifier.height(8.dp))
                    Text("Palet: ${paletSeleccionado?.codigoPalet}")
                    Text("Esta acción creará un traspaso en estado PENDIENTE")
                    Text("Deberá escanear la ubicación destino para completarlo")
                }
            },
            confirmButton = {
                TextButton(
                    onClick = {
                        mostrarDialogoCerrarPalet = false
                        
                        user?.let { u ->
                            val empresa = sessionViewModel.empresaSeleccionada.value?.codigo?.toShort()
                            if (empresa != null) {
                                viewModel.cerrarPalet(
                                    id = idPaletParaCerrar!!,
                                    usuarioId = u.id.toInt(),
                                    codigoAlmacen = ubicacionOrigenSeleccionada?.codigoAlmacen,
                                    codigoEmpresa = empresa,
                                    onSuccess = { traspasoId ->
                                        // Palet cerrado exitosamente - activar bloqueo
                                        android.util.Log.d("CERRAR_PALET", "✅ Palet cerrado exitosamente")
                                        android.util.Log.d("CERRAR_PALET", "📝 Traspaso ID recibido: '$traspasoId'")
                                        android.util.Log.d("CERRAR_PALET", "   Tipo: ${traspasoId?.javaClass?.simpleName}")
                                        android.util.Log.d("CERRAR_PALET", "   Longitud: ${traspasoId?.length}")
                                        android.util.Log.d("CERRAR_PALET", "   ¿Es vacío?: ${traspasoId?.isEmpty()}")
                                        
                                        esperandoUbicacionDestino = true
                                        traspasoPendienteId = traspasoId
                                        idPaletParaCerrar = null
                                        viewModel.setMensaje("Palet cerrado. Traspaso creado: $traspasoId")
                                        viewModel.setMensaje("Escanee la ubicación destino para completar el traspaso")
                                    },
                                    onError = { error ->
                                        viewModel.setError("Error al cerrar palet: $error")
                                    }
                                )
                            } else {
                                viewModel.setError("Error: Empresa no seleccionada")
                            }
                        }
                    }
                ) { 
                    Text("Sí, cerrar palet") 
                }
            },
            dismissButton = {
                TextButton(onClick = { 
                    mostrarDialogoCerrarPalet = false
                    idPaletParaCerrar = null
                }) { 
                    Text("Cancelar") 
                }
            }
        )
    }
    
    // MANEJO DE UBICACIÓN DESTINO DESPUÉS DE CERRAR PALET
    if (esperandoUbicacionDestino) {
        Box(
            modifier = Modifier
                .fillMaxSize()
                .zIndex(2f),
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
        if (DeviceUtils.hasHardwareScanner(LocalContext.current)) {
            val focusRequester = remember { FocusRequester() }
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .focusRequester(focusRequester)
                    .focusable()
                    .onPreviewKeyEvent { event ->
                        if (event.nativeKeyEvent?.action == android.view.KeyEvent.ACTION_MULTIPLE) {
                            event.nativeKeyEvent.characters?.let { code ->
                                android.util.Log.d("UBICAR_PALET", "📍 Ubicación escaneada (PDA): $code")
                                
                                // Parsear ubicación (formato: ALMACEN$UBICACION)
                                val (almacen, ubicacion) = if (code.contains('$')) {
                                    val parts = code.split('$')
                                    if (parts.size == 2) parts[0] to parts[1] else {
                                        android.util.Log.e("UBICAR_PALET", "❌ Formato de ubicación incorrecto: $code")
                                        return@onPreviewKeyEvent false
                                    }
                                } else {
                                    android.util.Log.e("UBICAR_PALET", "❌ Formato de ubicación incorrecto: $code")
                                    return@onPreviewKeyEvent false
                                }
                                
                                // COMPLETAR TRASPASO
                                android.util.Log.d("COMPLETAR_TRASPASO", "🚀 Intentando completar traspaso")
                                android.util.Log.d("COMPLETAR_TRASPASO", "📝 Traspaso ID a usar: '$traspasoPendienteId'")
                                android.util.Log.d("COMPLETAR_TRASPASO", "   Tipo: ${traspasoPendienteId?.javaClass?.simpleName}")
                                android.util.Log.d("COMPLETAR_TRASPASO", "   Longitud: ${traspasoPendienteId?.length}")
                                android.util.Log.d("COMPLETAR_TRASPASO", "📦 Palet seleccionado ID: ${paletSeleccionado?.id}")
                                android.util.Log.d("COMPLETAR_TRASPASO", "📍 Almacén destino: $almacen")
                                android.util.Log.d("COMPLETAR_TRASPASO", "📍 Ubicación destino: $ubicacion")
                                
                                val traspasosLogic = com.example.sga.view.traspasos.TraspasosLogic()
                                val completarDto = com.example.sga.data.dto.traspasos.CompletarTraspasoDto(
                                    codigoAlmacenDestino = almacen,
                                    ubicacionDestino = ubicacion,
                                    fechaFinalizacion = java.time.LocalDateTime.now().toString(),
                                    usuarioFinalizacionId = user?.id?.toInt() ?: 0
                                )
                                
                                android.util.Log.d("COMPLETAR_TRASPASO", "📤 Llamando a completarTraspaso con ID: '$traspasoPendienteId'")
                                
                                traspasosLogic.completarTraspaso(
                                    idTraspaso = traspasoPendienteId!!,
                                    dto = completarDto,
                                    paletId = paletSeleccionado?.id,
                                    onSuccess = {
                                        android.util.Log.d("UBICAR_PALET", "✅ Traspaso completado correctamente")
                                        
                                        // PASO 3: Actualizar línea de orden con el ID del traspaso
                                        val idLinea = lineaSeleccionada?.idLineaOrdenTraspaso
                                        if (idLinea != null) {
                                            viewModel.actualizarLineaConIdTraspaso(
                                                idLinea = idLinea,
                                                idTraspaso = traspasoPendienteId!!,
                                                onSuccess = {
                                                    android.util.Log.d("UBICAR_PALET", "✅ Línea de orden actualizada con IdTraspaso")
                                                    esperandoUbicacionDestino = false
                                                    traspasoPendienteId = null
                                                    viewModel.limpiarMensajes()
                                                    viewModel.setMensaje("Traspaso completado correctamente")
                                                },
                                                onError = { error ->
                                                    android.util.Log.e("UBICAR_PALET", "❌ Error al actualizar línea de orden: $error")
                                                    viewModel.setError("Traspaso completado pero error al actualizar línea: $error")
                                                }
                                            )
                                        } else {
                                            android.util.Log.w("UBICAR_PALET", "⚠️ No hay línea seleccionada para actualizar")
                                            esperandoUbicacionDestino = false
                                            traspasoPendienteId = null
                                        }
                                    },
                                    onError = { error ->
                                        android.util.Log.e("UBICAR_PALET", "❌ Error al completar traspaso: $error")
                                        viewModel.setError("Error al completar traspaso: $error")
                                    }
                                )
                                return@onPreviewKeyEvent true
                            }
                            return@onPreviewKeyEvent false
                        }
                        return@onPreviewKeyEvent false
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
        if (!DeviceUtils.hasHardwareScanner(LocalContext.current)) {
            QRScannerView(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(250.dp),
                onCodeScanned = { code ->
                    android.util.Log.d("UBICAR_PALET", "📍 Ubicación escaneada (Cámara): $code")
                    
                    // Parsear ubicación (formato: ALMACEN$UBICACION)
                    val (almacen, ubicacion) = if (code.contains('$')) {
                        val parts = code.split('$')
                        if (parts.size == 2) parts[0] to parts[1] else {
                            android.util.Log.e("UBICAR_PALET", "❌ Formato de ubicación incorrecto: $code")
                            return@QRScannerView
                        }
                    } else {
                        android.util.Log.e("UBICAR_PALET", "❌ Formato de ubicación incorrecto: $code")
                        return@QRScannerView
                    }
                    
                    // COMPLETAR TRASPASO
                    val traspasosLogic = com.example.sga.view.traspasos.TraspasosLogic()
                    val completarDto = com.example.sga.data.dto.traspasos.CompletarTraspasoDto(
                        codigoAlmacenDestino = almacen,
                        ubicacionDestino = ubicacion,
                        fechaFinalizacion = java.time.LocalDateTime.now().toString(),
                        usuarioFinalizacionId = user?.id?.toInt() ?: 0
                    )
                    
                    traspasosLogic.completarTraspaso(
                        idTraspaso = traspasoPendienteId!!,
                        dto = completarDto,
                        paletId = paletSeleccionado?.id,
                        onSuccess = {
                            android.util.Log.d("UBICAR_PALET", "✅ Traspaso completado correctamente")
                            
                            // PASO 3: Actualizar línea de orden con el ID del traspaso
                            val idLinea = lineaSeleccionada?.idLineaOrdenTraspaso
                            if (idLinea != null) {
                                viewModel.actualizarLineaConIdTraspaso(
                                    idLinea = idLinea,
                                    idTraspaso = traspasoPendienteId!!,
                                    onSuccess = {
                                        android.util.Log.d("UBICAR_PALET", "✅ Línea de orden actualizada con IdTraspaso")
                                        esperandoUbicacionDestino = false
                                        traspasoPendienteId = null
                                        viewModel.limpiarMensajes()
                                        viewModel.setMensaje("Traspaso completado correctamente")
                                    },
                                    onError = { error ->
                                        android.util.Log.e("UBICAR_PALET", "❌ Error al actualizar línea de orden: $error")
                                        viewModel.setError("Traspaso completado pero error al actualizar línea: $error")
                                    }
                                )
                            } else {
                                android.util.Log.w("UBICAR_PALET", "⚠️ No hay línea seleccionada para actualizar")
                                esperandoUbicacionDestino = false
                                traspasoPendienteId = null
                            }
                        },
                        onError = { error ->
                            android.util.Log.e("UBICAR_PALET", "❌ Error al completar traspaso: $error")
                            viewModel.setError("Error al completar traspaso: $error")
                        }
                    )
                }
            )
        }
    }
    
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun OrdenInfoCard(
    orden: OrdenTraspasoDto
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        elevation = CardDefaults.cardElevation(defaultElevation = 4.dp)
    ) {
        Column(
            modifier = Modifier.padding(16.dp)
        ) {
            // Header con código y estado
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Column(modifier = Modifier.weight(1f)) {
                    Text(
                        text = orden.codigoOrden ?: "Sin código",
                        style = MaterialTheme.typography.titleLarge,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                }
                EstadoOrdenChip(estado = orden.estado)
            }

            Spacer(modifier = Modifier.height(16.dp))

            // Información de la orden
            orden.fechaPlan?.let { fechaPlan ->
                InfoRowOrden(
                    icon = Icons.Default.Schedule,
                    label = "Fecha Plan",
                    value = formatearSoloFecha(fechaPlan)
                )
            }
        }
    }
}

@Composable
fun LineaOrdenCard(
    linea: LineaOrdenTraspasoDetalleDto,
    onClick: () -> Unit
) {
    val estadoColor = when (linea.estado) {
        "PENDIENTE" -> MaterialTheme.colorScheme.secondary
        "EN_PROGRESO" -> MaterialTheme.colorScheme.tertiary
        "SUBDIVIDIDO" -> MaterialTheme.colorScheme.tertiary
        "COMPLETADA" -> MaterialTheme.colorScheme.primary
        "BLOQUEADA" -> MaterialTheme.colorScheme.error
        else -> MaterialTheme.colorScheme.outline
    }
    
    val estadoTexto = when (linea.estado) {
        "PENDIENTE" -> "Pendiente"
        "EN_PROCESO" -> "En Proceso"
        "SUBDIVIDIDO" -> "Subdividido"
        "COMPLETADA" -> "Completada"
        "BLOQUEADA" -> "Bloqueada"
        else -> linea.estado
    }
    
    Card(
        onClick = if (linea.estado == "SUBDIVIDIDO" || linea.estado == "BLOQUEADA") { {} } else onClick,
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = when (linea.estado) {
                "PENDIENTE", "EN_PROCESO" -> MaterialTheme.colorScheme.primaryContainer
                "SUBDIVIDIDO" -> MaterialTheme.colorScheme.surfaceVariant
                "BLOQUEADA" -> MaterialTheme.colorScheme.errorContainer
                else -> MaterialTheme.colorScheme.surface
            }
        )
    ) {
        Column(
            modifier = Modifier.padding(16.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    Text(
                        text = "Artículo: ${linea.codigoArticulo}",
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.Bold
                    )
                }
                
                Surface(
                    color = estadoColor,
                    shape = MaterialTheme.shapes.small
                ) {
                    Text(
                        text = estadoTexto,
                        modifier = Modifier.padding(horizontal = 8.dp, vertical = 4.dp),
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onPrimary
                    )
                }
            }
            
            Spacer(modifier = Modifier.height(8.dp))
            
            
            linea.descripcionArticulo?.let { descripcion ->
                Text(
                    text = descripcion,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
            
            Spacer(modifier = Modifier.height(4.dp))
            
            Text(
                text = "Cantidad Necesaria: ${linea.cantidadPlan}",
                style = MaterialTheme.typography.bodyMedium
            )
            
            if (linea.cantidadMovida > 0) {
                Text(
                    text = "Cantidad movida: ${linea.cantidadMovida}",
                    style = MaterialTheme.typography.bodyMedium
                )
            }
                       
            
            linea.fechaFinalizacion?.let { fechaFinalizacion ->
                Text(
                    text = "Finalizado: ${formatearFecha(fechaFinalizacion)}",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
        }
    }
}

@Composable
fun StockDisponibleCard(
    stock: StockDisponibleDto,
    isSelected: Boolean,
    onClick: () -> Unit
) {
    Card(
        onClick = onClick,
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = if (isSelected) 
                MaterialTheme.colorScheme.primaryContainer 
            else 
                MaterialTheme.colorScheme.surface
        )
    ) {
        Column(
            modifier = Modifier.padding(12.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = "${stock.codigoAlmacen} - ${stock.ubicacion ?: "Sin ubicación"}",
                    style = MaterialTheme.typography.bodyMedium,
                    fontWeight = FontWeight.Medium
                )
                
                Text(
                    text = "Stock: ${stock.cantidadDisponible}",
                    style = MaterialTheme.typography.bodyMedium,
                    fontWeight = FontWeight.Bold
                )
            }
            
            stock.partida?.let { partida ->
                Text(
                    text = "Partida: $partida",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
            
            stock.fechaCaducidad?.let { fechaCaducidad ->
                Text(
                    text = "Caducidad: ${formatearFecha(fechaCaducidad)}",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
        }
    }
}

@Composable
fun InfoRow(
    label: String,
    value: String
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.bodyMedium,
            fontWeight = FontWeight.Medium
        )
        Text(
            text = value,
            style = MaterialTheme.typography.bodyMedium
        )
    }
}

@Composable
fun UbicarPaletScreen(
    orden: OrdenTraspasoDto,
    paletDestino: String,
    codigoGS1: String?,
    user: User,
    viewModel: OrdenTraspasoViewModel,
    sessionViewModel: com.example.sga.view.app.SessionViewModel,
    onNavigateBack: () -> Unit
) {
    val cargando by viewModel.cargando.collectAsStateWithLifecycle()
    val error by viewModel.error.collectAsStateWithLifecycle()
    val almacenDestinoPalet by viewModel.almacenDestinoPalet.collectAsStateWithLifecycle()
    val ubicacionDestinoPalet by viewModel.ubicacionDestinoPalet.collectAsStateWithLifecycle()
    
    // Obtener usuario y empresa como en el resto del componente
    val usuarioId = user.id.toIntOrNull() ?: return
    val empresa = sessionViewModel.empresaSeleccionada.collectAsState().value?.codigo?.toShort() ?: return
    
    // Estados para el escaneo
    var paletEscaneado by remember { mutableStateOf(false) }
    var ubicacionEscaneada by remember { mutableStateOf<Pair<String, String>?>(null) }
    var escaneoProcesado by remember { mutableStateOf(false) }
    
    // Estados para el flujo de ubicación
    var esperandoUbicacionDestino by remember { mutableStateOf(false) }
    var traspasoPendienteId by remember { mutableStateOf<String?>(null) }
    var paletIdInterno by remember { mutableStateOf<String?>(null) }
    
    val context = LocalContext.current
    val focusRequester = remember { FocusRequester() }
    
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp)
    ) {
        // Header
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            IconButton(onClick = onNavigateBack) {
                Icon(
                    imageVector = Icons.AutoMirrored.Filled.ArrowBack,
                    contentDescription = "Volver"
                )
            }
            
            Text(
                text = "Ubicar Palet",
                style = MaterialTheme.typography.titleLarge,
                fontWeight = FontWeight.Bold
            )
            
            // Espacio vacío para centrar el título
            Spacer(modifier = Modifier.width(48.dp))
        }
        
        Spacer(modifier = Modifier.height(16.dp))
        
        
        // Información del palet
        Card(
            modifier = Modifier.fillMaxWidth(),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.primaryContainer)
        ) {
            Column(
                modifier = Modifier.padding(16.dp)
            ) {
                Text(
                    text = "Palet listo para ubicar",
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold
                )
                Spacer(modifier = Modifier.height(8.dp))
                Text("Palet: $paletDestino")
                Text("Orden: ${orden.codigoOrden ?: "Sin código"}")
            }
        }
        
        Spacer(modifier = Modifier.height(16.dp))
        
        // Flujo de escaneo
        if (!paletEscaneado) {
            // Paso 1: Escanear palet
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.primaryContainer)
            ) {
                Column(modifier = Modifier.padding(16.dp)) {
                    Text(
                        text = if (DeviceUtils.hasHardwareScanner(context)) "📱 Escanee el palet" else "📱 Escanee el palet",
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.onPrimaryContainer
                    )
                }
            }
            
            Spacer(modifier = Modifier.height(16.dp))
            
            // Scanner de palet - PDA (hardware scanner)
            if (DeviceUtils.hasHardwareScanner(context)) {
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(100.dp)
                        .focusRequester(focusRequester)
                        .focusable()
                        .onPreviewKeyEvent { event ->
                            if (event.nativeKeyEvent?.action == android.view.KeyEvent.ACTION_MULTIPLE) {
                                if (escaneoProcesado) return@onPreviewKeyEvent true
                                escaneoProcesado = true
                                
                                event.nativeKeyEvent.characters?.let { code ->
                                    android.util.Log.d("UBICAR_PALET", "📱 Código GS1 escaneado (PDA): $code")
                                    
                                    // Validar que el código GS1 coincida con el palet
                                    val codigoLimpio = code.trim().removePrefix("00") // Quitar ceros extra del principio
                                    if (codigoLimpio == codigoGS1) {
                                        android.util.Log.d("UBICAR_PALET", "✅ Palet validado correctamente")
                                        
                                        // OBTENER ID INTERNO DEL PALET POR GS1
                                        val traspasosLogic = TraspasosLogic()
                                        traspasosLogic.obtenerPaletPorGS1(
                                            gs1 = codigoLimpio,
                                            onSuccess = { palet ->
                                                android.util.Log.d("UBICAR_PALET", "✅ Palet obtenido: ${palet.id}")
                                                paletIdInterno = palet.id // Guardar ID interno
                                                
                                                // CERRAR PALET (genera traspaso "PENDIENTE")
                                                viewModel.cerrarPalet(
                                                    id = palet.id, // Usar ID interno, no código
                                                    usuarioId = usuarioId,
                                                    codigoAlmacen = almacenDestinoPalet,
                                                    codigoEmpresa = empresa,
                                                    onSuccess = { paletId ->
                                                        android.util.Log.d("UBICAR_PALET", "✅ Palet cerrado correctamente. Palet ID: $paletId")
                                                        // Buscar traspasos pendientes generados por este palet
                                                        traspasosLogic.comprobarTraspasoPendiente(
                                                            usuarioId = usuarioId,
                                                            onSuccess = { traspasos ->
                                                                val traspasosDelPalet = traspasos.filter { it.paletId == paletId }
                                                                if (traspasosDelPalet.isNotEmpty()) {
                                                                    traspasoPendienteId = traspasosDelPalet.first().id
                                                                    android.util.Log.d("UBICAR_PALET", "🔍 Traspasos del palet encontrados: ${traspasosDelPalet.size}, usando ID: $traspasoPendienteId")
                                                                    paletEscaneado = true
                                                                    esperandoUbicacionDestino = true
                                                                    viewModel.limpiarMensajes()
                                                                } else {
                                                                    android.util.Log.e("UBICAR_PALET", "❌ No se encontraron traspasos para el palet: $paletId")
                                                                    viewModel.setError("No se encontraron traspasos pendientes para el palet")
                                                                }
                                                            },
                                                            onError = { error ->
                                                                android.util.Log.e("UBICAR_PALET", "❌ Error al obtener traspasos pendientes: $error")
                                                                viewModel.setError("Error al obtener traspasos pendientes: $error")
                                                            }
                                                        )
                                                    },
                                                    onError = { error ->
                                                        android.util.Log.e("UBICAR_PALET", "❌ Error al cerrar palet: $error")
                                                        viewModel.setError("Error al cerrar palet: $error")
                                                    }
                                                )
                                            },
                                            onError = { error ->
                                                android.util.Log.e("UBICAR_PALET", "❌ Error al obtener palet: $error")
                                                viewModel.setError("Error al obtener palet: $error")
                                            }
                                        )
                                        escaneoProcesado = false
                                    } else {
                                        android.util.Log.e("UBICAR_PALET", "❌ Palet no coincide: esperado=$codigoGS1, escaneado=$code (limpio=$codigoLimpio)")
                                        viewModel.setError("❌ El palet escaneado no coincide con el esperado. Por favor, escanee el palet correcto.")
                                        escaneoProcesado = false
                                    }
                                }
                                return@onPreviewKeyEvent true
                            }
                            false
                        }
                )
                
                LaunchedEffect(Unit) {
                    focusRequester.requestFocus()
                }
            } else {
                // Scanner de palet - Móvil/Tablet (cámara)
                QRScannerView(
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(250.dp),
                    onCodeScanned = { code ->
                        if (escaneoProcesado) return@QRScannerView
                        escaneoProcesado = true
                        
                        android.util.Log.d("UBICAR_PALET", "📱 Código GS1 escaneado (Cámara): $code")
                        
                        // Validar que el código GS1 coincida con el palet
                        val codigoLimpio = code.trim().removePrefix("00") // Quitar ceros extra del principio
                        if (codigoLimpio == codigoGS1) {
                            android.util.Log.d("UBICAR_PALET", "✅ Palet validado correctamente")
                            
                            // OBTENER ID INTERNO DEL PALET POR GS1
                            val traspasosLogic = TraspasosLogic()
                            traspasosLogic.obtenerPaletPorGS1(
                                gs1 = codigoLimpio,
                                onSuccess = { palet ->
                                    android.util.Log.d("UBICAR_PALET", "✅ Palet obtenido: ${palet.id}")
                                    paletIdInterno = palet.id // Guardar ID interno
                                    
                                    // CERRAR PALET (genera traspaso "PENDIENTE")
                                    viewModel.cerrarPalet(
                                        id = palet.id, // Usar ID interno, no código
                                        usuarioId = usuarioId,
                                        codigoAlmacen = almacenDestinoPalet,
                                        codigoEmpresa = empresa,
                                        onSuccess = { paletId ->
                                            android.util.Log.d("UBICAR_PALET", "✅ Palet cerrado correctamente. Palet ID: $paletId")
                                            // Buscar traspasos pendientes generados por este palet
                                            traspasosLogic.comprobarTraspasoPendiente(
                                                usuarioId = usuarioId,
                                                onSuccess = { traspasos ->
                                                    val traspasosDelPalet = traspasos.filter { it.paletId == paletId }
                                                    if (traspasosDelPalet.isNotEmpty()) {
                                                        traspasoPendienteId = traspasosDelPalet.first().id
                                                        android.util.Log.d("UBICAR_PALET", "🔍 Traspasos del palet encontrados: ${traspasosDelPalet.size}, usando ID: $traspasoPendienteId")
                                                        paletEscaneado = true
                                                        esperandoUbicacionDestino = true
                                                        viewModel.limpiarMensajes()
                                                    } else {
                                                        android.util.Log.e("UBICAR_PALET", "❌ No se encontraron traspasos para el palet: $paletId")
                                                        viewModel.setError("No se encontraron traspasos pendientes para el palet")
                                                    }
                                                },
                                                onError = { error ->
                                                    android.util.Log.e("UBICAR_PALET", "❌ Error al obtener traspasos pendientes: $error")
                                                    viewModel.setError("Error al obtener traspasos pendientes: $error")
                                                }
                                            )
                                        },
                                        onError = { error ->
                                            android.util.Log.e("UBICAR_PALET", "❌ Error al cerrar palet: $error")
                                            viewModel.setError("Error al cerrar palet: $error")
                                        }
                                    )
                                },
                                onError = { error ->
                                    android.util.Log.e("UBICAR_PALET", "❌ Error al obtener palet: $error")
                                    viewModel.setError("Error al obtener palet: $error")
                                }
                            )
                            escaneoProcesado = false
                        } else {
                            android.util.Log.e("UBICAR_PALET", "❌ Palet no coincide: esperado=$codigoGS1, escaneado=$code (limpio=$codigoLimpio)")
                            viewModel.setError("❌ El palet escaneado no coincide con el esperado. Por favor, escanee el palet correcto.")
                            escaneoProcesado = false
                        }
                    }
                )
            }
        }
        
        // Bloque de ubicación destino (igual que en TraspasosScreen)
        if (esperandoUbicacionDestino) {
            Box(
                modifier = Modifier
                    .fillMaxSize()
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
        }
        
        // Escaneo de ubicación destino (PDA)
        if (esperandoUbicacionDestino && DeviceUtils.hasHardwareScanner(context)) {
                Box(
                    modifier = Modifier
                    .fillMaxSize()
                        .focusRequester(focusRequester)
                        .focusable()
                        .onPreviewKeyEvent { event ->
                            if (event.nativeKeyEvent?.action == android.view.KeyEvent.ACTION_MULTIPLE) {
                                event.nativeKeyEvent.characters?.let { code ->
                                    android.util.Log.d("UBICAR_PALET", "📍 Ubicación escaneada (PDA): $code")
                                    
                                    // Parsear ubicación (formato: ALMACEN$UBICACION)
                                    val (almacen, ubicacion) = if (code.contains('$')) {
                                        val parts = code.split('$')
                                        if (parts.size == 2) parts[0] to parts[1] else {
                                            android.util.Log.e("UBICAR_PALET", "❌ Formato de ubicación incorrecto: $code")
                                            return@onPreviewKeyEvent false
                                        }
                                    } else {
                                        // Si no tiene formato ALMACEN$UBICACION, mostrar error
                                        android.util.Log.e("UBICAR_PALET", "❌ Formato de ubicación incorrecto: $code")
                                        return@onPreviewKeyEvent false
                                    }
                                    
                                // COMPLETAR TRASPASO (igual que en TraspasosScreen.kt)
                                val traspasosLogic = TraspasosLogic()
                                val completarDto = CompletarTraspasoDto(
                                    codigoAlmacenDestino = almacen,
                                    ubicacionDestino = ubicacion,
                                    fechaFinalizacion = java.time.LocalDateTime.now().toString(),
                                    usuarioFinalizacionId = usuarioId
                                )
                                
                                traspasosLogic.completarTraspaso(
                                    idTraspaso = traspasoPendienteId!!,
                                    dto = completarDto,
                                    paletId = paletIdInterno,
                                    onSuccess = {
                                        android.util.Log.d("UBICAR_PALET", "✅ Traspaso completado correctamente")
                                        esperandoUbicacionDestino = false
                                        viewModel.limpiarMensajes()
                                        onNavigateBack()
                                    },
                                    onError = { error ->
                                        android.util.Log.e("UBICAR_PALET", "❌ Error al completar traspaso: $error")
                                        viewModel.setError("Error al completar traspaso: $error")
                                    }
                                )
                                return@onPreviewKeyEvent true
                            }
                            return@onPreviewKeyEvent false
                        }
                        return@onPreviewKeyEvent false
                    }
            ) {

            }
            
            LaunchedEffect(esperandoUbicacionDestino) {
                if (esperandoUbicacionDestino) {
                    delay(200)
                    focusRequester.requestFocus()
                }
            }
        }
        
        // Escaneo de ubicación destino (Cámara)
        if (esperandoUbicacionDestino && !DeviceUtils.hasHardwareScanner(context)) {
                QRScannerView(
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(250.dp),
                    onCodeScanned = { code ->
                        android.util.Log.d("UBICAR_PALET", "📍 Ubicación escaneada (Cámara): $code")
                        
                        // Parsear ubicación (formato: ALMACEN$UBICACION)
                        val (almacen, ubicacion) = if (code.contains('$')) {
                            val parts = code.split('$')
                            if (parts.size == 2) parts[0] to parts[1] else {
                            android.util.Log.e("UBICAR_PALET", "❌ Formato de ubicación incorrecto: $code")
                                return@QRScannerView
                            }
                        } else {
                            // Si no tiene formato ALMACEN$UBICACION, mostrar error
                            android.util.Log.e("UBICAR_PALET", "❌ Formato de ubicación incorrecto: $code")
                            return@QRScannerView
                        }
                        
                    // COMPLETAR TRASPASO (igual que en TraspasosScreen.kt)
                    val traspasosLogic = TraspasosLogic()
                    val completarDto = CompletarTraspasoDto(
                        codigoAlmacenDestino = almacen,
                        ubicacionDestino = ubicacion,
                        fechaFinalizacion = java.time.LocalDateTime.now().toString(),
                        usuarioFinalizacionId = usuarioId
                    )
                    
                    traspasosLogic.completarTraspaso(
                        idTraspaso = traspasoPendienteId!!,
                        dto = completarDto,
                        paletId = paletIdInterno,
                        onSuccess = {
                            android.util.Log.d("UBICAR_PALET", "✅ Traspaso completado correctamente")
                            esperandoUbicacionDestino = false
                            viewModel.limpiarMensajes()
                            onNavigateBack()
                        },
                        onError = { error ->
                            android.util.Log.e("UBICAR_PALET", "❌ Error al completar traspaso: $error")
                            viewModel.setError("Error al completar traspaso: $error")
                        }
                    )
                }
            )
        }
        
        // Error
        error?.let { errorMessage ->
            Spacer(modifier = Modifier.height(16.dp))
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.errorContainer)
            ) {
                Text(
                    text = errorMessage,
                    modifier = Modifier.padding(16.dp),
                    color = MaterialTheme.colorScheme.onErrorContainer
                )
            }
        }
    }
}

@Composable
fun PaletsPendientesScreen(
    orden: OrdenTraspasoDto,
    paletsPendientes: List<PaletPendienteDto>,
    user: User,
    viewModel: OrdenTraspasoViewModel,
    onNavigateBack: () -> Unit
) {
    // Estados para el diálogo de confirmación
    var mostrarDialogoCerrarPalet by remember { mutableStateOf(false) }
    var idPaletParaCerrar by remember { mutableStateOf<String?>(null) }
    var paletDestinoParaCerrar by remember { mutableStateOf<String?>(null) }
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp)
    ) {
        // Header
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            IconButton(onClick = onNavigateBack) {
                Icon(
                    imageVector = Icons.AutoMirrored.Filled.ArrowBack,
                    contentDescription = "Volver"
                )
            }
            
            Text(
                text = "Palets Pendientes",
                style = MaterialTheme.typography.titleLarge,
                fontWeight = FontWeight.Bold
            )
            
            // Espacio vacío para centrar el título
            Spacer(modifier = Modifier.width(48.dp))
        }
        
        Spacer(modifier = Modifier.height(16.dp))
        
        if (paletsPendientes.isEmpty()) {
            Card(
                modifier = Modifier.fillMaxWidth()
            ) {
                Text(
                    text = "No hay palets pendientes de ubicar",
                    modifier = Modifier.padding(16.dp),
                    style = MaterialTheme.typography.bodyMedium
                )
            }
        } else {
            LazyColumn(
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                items(paletsPendientes) { palet ->
                    PaletPendienteCard(
                        palet = palet,
                        onUbicar = {
                            viewModel.setPaletListoParaUbicar(palet.paletDestino)
                            viewModel.setCodigoGS1Palet(palet.codigoGS1)
                            onNavigateBack()
                        },
                        onToggleCerrar = { paletDestino ->
                            idPaletParaCerrar = paletDestino
                            paletDestinoParaCerrar = paletDestino
                            // También establecer el código GS1 del palet que se va a cerrar
                            viewModel.setCodigoGS1Palet(palet.codigoGS1)
                            mostrarDialogoCerrarPalet = true
                        }
                    )
                }
            }
        }
        
        // Diálogo de confirmación para cerrar palet
        if (mostrarDialogoCerrarPalet) {
            AlertDialog(
                onDismissRequest = {
                    mostrarDialogoCerrarPalet = false
                    idPaletParaCerrar = null
                    paletDestinoParaCerrar = null
                },
                title = { Text("Cerrar Palet") },
                text = {
                    Column {
                        Text("¿Está seguro de que desea cerrar el palet?")
                        Spacer(modifier = Modifier.height(8.dp))
                        Text(
                            text = "Palet: ${paletDestinoParaCerrar ?: "N/A"}",
                            style = MaterialTheme.typography.bodyMedium,
                            fontWeight = FontWeight.Bold
                        )
                        Spacer(modifier = Modifier.height(8.dp))
                        Text(
                            text = "Una vez cerrado, deberá ubicar el palet en su destino final.",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                },
                confirmButton = {
                    TextButton(
                        onClick = {
                            mostrarDialogoCerrarPalet = false
                            // Lanzar flujo de ubicación
                            viewModel.setPaletListoParaUbicar(paletDestinoParaCerrar!!)
                            onNavigateBack()
                        }
                    ) {
                        Text("Sí, cerrar")
                    }
                },
                dismissButton = {
                    TextButton(
                        onClick = {
                            mostrarDialogoCerrarPalet = false
                            idPaletParaCerrar = null
                            paletDestinoParaCerrar = null
                        }
                    ) {
                        Text("Cancelar")
                    }
                }
            )
        }
    }
}

@Composable
fun PaletPendienteCard(
    palet: PaletPendienteDto,
    onUbicar: () -> Unit,
    onToggleCerrar: (String) -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = if (palet.listoParaUbicar) 
                MaterialTheme.colorScheme.primaryContainer 
            else 
                MaterialTheme.colorScheme.surface
        )
    ) {
        Column(
            modifier = Modifier.padding(16.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = palet.paletDestino,
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold
                )
                
                // Toggle para cambiar estado del palet (Abierto/Cerrado)
                Row(
                    verticalAlignment = Alignment.CenterVertically
                ) {
   
                    Spacer(modifier = Modifier.width(8.dp))
                    Switch(
                        checked = true, // Palets pendientes están "Abiertos" (sin ubicar) - mostrar como activado
                        onCheckedChange = { nuevoEstado ->
                            if (!nuevoEstado) {
                                // Cambiar a "Cerrado" - Mostrar diálogo de confirmación
                                onToggleCerrar(palet.paletDestino)
                            } else {
                                // Ya está "Abierto" - No hacer nada
                            }
                        }
                    )
                    Spacer(modifier = Modifier.width(8.dp))
                    Text(
                        text = "Abierto", // Palets pendientes están abiertos
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            }
            
            Spacer(modifier = Modifier.height(8.dp))
            
            Text("Líneas completas: ${palet.lineasCompletas}")
            Text("Cantidad total: ${palet.cantidadTotal}")
            Text(
                text = if (palet.listoParaUbicar) "✅ Listo para ubicar" else "⏳ Pendiente",
                color = if (palet.listoParaUbicar) 
                    MaterialTheme.colorScheme.primary 
                else 
                    MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
    }
}

private fun formatearFecha(fecha: String): String {
    return try {
        val inputFormat = SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss", Locale.getDefault())
        val outputFormat = SimpleDateFormat("dd-MM-yyyy HH:mm", Locale.getDefault())
        val date = inputFormat.parse(fecha)
        outputFormat.format(date ?: Date())
    } catch (e: Exception) {
        fecha
    }
}

private fun formatearSoloFecha(fecha: String): String {
    return try {
        val inputFormat = SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss", Locale.getDefault())
        val outputFormat = SimpleDateFormat("dd-MM-yyyy", Locale.getDefault())
        val date = inputFormat.parse(fecha)
        outputFormat.format(date ?: Date())
    } catch (e: Exception) {
        fecha
    }
}

@Composable
fun EscaneoBasicoOrden(
    stockDisponible: List<StockDisponibleDto>,
    lineaSeleccionada: LineaOrdenTraspasoDetalleDto,
    ordenSeleccionada: OrdenTraspasoDto?,
    viewModel: OrdenTraspasoViewModel,
    sessionViewModel: com.example.sga.view.app.SessionViewModel,
    user: User
) {
    // TraspasosLogic para reutilizar la lógica de escaneo
    val traspasosLogic = TraspasosLogic()
    
    val empresa = sessionViewModel.empresaSeleccionada.collectAsState().value?.codigo?.toShort() ?: return
    val context = LocalContext.current
    val focusRequester = remember { FocusRequester() }
    
    // Estados básicos para detección inteligente
    var ubicacionEscaneada by remember { mutableStateOf<Pair<String,String>?>(null) }
    var articuloEscaneado by remember { mutableStateOf<ArticuloDto?>(null) }
    var paletEscaneado by remember { mutableStateOf<com.example.sga.data.dto.traspasos.PaletDto?>(null) }
    var escaneoProcesado by remember { mutableStateOf(false) }
    var mostrarDialogoError by remember { mutableStateOf<String?>(null) }
    
    // Sincronizar variables de la UI con el ViewModel
    val articuloValidadoVM by viewModel.articuloValidado.collectAsStateWithLifecycle()
    LaunchedEffect(articuloValidadoVM) {
        if (articuloValidadoVM == null) {
            articuloEscaneado = null
        }
    }
    
    // Información del proceso (inteligente)
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = when {
                paletEscaneado != null && ubicacionEscaneada != null && articuloEscaneado != null -> MaterialTheme.colorScheme.primaryContainer
                paletEscaneado != null -> MaterialTheme.colorScheme.secondaryContainer
                ubicacionEscaneada != null || articuloEscaneado != null -> MaterialTheme.colorScheme.tertiaryContainer
                else -> MaterialTheme.colorScheme.surfaceVariant
            }
        )
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            when {
                paletEscaneado == null && ubicacionEscaneada == null && articuloEscaneado == null -> {
                    android.util.Log.d("UI_PALET_CENTRO", "🔍 Mostrando sección: ESCANEE Palet o Ubicación")
                    Text(
                        text = "ESCANEE Palet o Ubicación",
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.primary
                    )
                    lineaSeleccionada?.let { linea ->
                        if (stockDisponible.isNotEmpty()) {
                            val stock = stockDisponible.first()
                            Text(
                                text = "Escanee palet existente o ubicación: ${stock.codigoAlmacen}/${stock.ubicacion}",
                                style = MaterialTheme.typography.bodyMedium
                            )
                        } else {
                            Text(
                                text = "Escanee palet existente o ubicación para empezar",
                                style = MaterialTheme.typography.bodyMedium
                            )
                        }
                    }
                }
                paletEscaneado != null && ubicacionEscaneada == null -> {
                    android.util.Log.d("UI_PALET_CENTRO", "🔍 Mostrando sección: Palet seleccionado + X")
                    Column {
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Text(
                                text = "Palet seleccionado: ${paletEscaneado!!.codigoPalet}",
                                style = MaterialTheme.typography.titleMedium,
                                fontWeight = FontWeight.Bold,
                                color = MaterialTheme.colorScheme.primary,
                                modifier = Modifier.weight(1f)
                            )
                            
                            // Botón X al lado del código del palet
                            Text(
                                text = "✕",
                                color = MaterialTheme.colorScheme.error,
                                fontSize = 16.sp,
                                fontWeight = FontWeight.Bold,
                                modifier = Modifier.clickable {
                                    android.util.Log.d("UI_PALET_CENTRO", "🗑️ Click en X para deseleccionar palet desde centro")
                                    paletEscaneado = null  // Limpia variable local
                                    viewModel.deseleccionarPalet() // Limpia ViewModel
                                }
                            )
                        }
                    }
                    Text(
                        text = "Ahora escanee la ubicación:",
                        style = MaterialTheme.typography.bodyMedium
                    )
                    Text(
                        text = "${stockDisponible.first().codigoAlmacen} - ${stockDisponible.first().ubicacion}",
                        style = MaterialTheme.typography.headlineSmall,
                        fontWeight = FontWeight.Bold
                    )
                }
                paletEscaneado != null && ubicacionEscaneada != null && articuloEscaneado == null -> {
                    android.util.Log.d("UI_PALET_CENTRO", "🔍 Mostrando sección: Palet + Ubicación OK + X")
                    Column {
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Text(
                                text = "Palet: ${paletEscaneado!!.codigoPalet} | Ubicación: OK",
                                style = MaterialTheme.typography.titleMedium,
                                fontWeight = FontWeight.Bold,
                                color = MaterialTheme.colorScheme.primary,
                                modifier = Modifier.weight(1f)
                            )
                            
                            // Botón X al lado del código del palet
                            Text(
                                text = "✕",
                                color = MaterialTheme.colorScheme.error,
                                fontSize = 16.sp,
                                fontWeight = FontWeight.Bold,
                                modifier = Modifier.clickable {
                                    android.util.Log.d("UI_PALET_CENTRO", "🗑️ Click en X para deseleccionar palet desde centro (ubicación OK)")
                                    paletEscaneado = null  // Limpia variable local
                                    viewModel.deseleccionarPalet() // Limpia ViewModel
                                }
                            )
                        }
                    }
                    Text(
                        text = "Ahora escanee el artículo:",
                        style = MaterialTheme.typography.bodyMedium
                    )
                    Text(
                        text = lineaSeleccionada.codigoArticulo,
                        style = MaterialTheme.typography.headlineSmall,
                        fontWeight = FontWeight.Bold
                    )
                }
                ubicacionEscaneada != null && articuloEscaneado == null && paletEscaneado == null -> {
                    android.util.Log.d("UI_PALET_CENTRO", "🔍 Mostrando sección: Solo ubicación")
                    Text(
                        text = "Ubicación: ${ubicacionEscaneada!!.first}-${ubicacionEscaneada!!.second}",
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.primary
                    )
                    Text(
                        text = "Ahora escanee el artículo:",
                        style = MaterialTheme.typography.bodyMedium
                    )
                    Text(
                        text = lineaSeleccionada.codigoArticulo,
                        style = MaterialTheme.typography.headlineSmall,
                        fontWeight = FontWeight.Bold
                    )
                }
            }
        }
    }
    
    // PASO 2: Gestionar palet (cuando todo esté escaneado O ya tengamos palet)
    when {
        // Caso 1: Ya tiene palet escaneado + ubicación + artículo = Listo para añadir
        paletEscaneado != null && ubicacionEscaneada != null && articuloEscaneado != null -> {
            android.util.Log.d("UI_PALET_CENTRO", "🔍 Mostrando sección: TODO ESCANEADO - PaletExistenteSeccion")
            Spacer(modifier = Modifier.height(16.dp))
            
            // Mostrar palet y botón añadir
            PaletExistenteSeccion(
                palet = paletEscaneado!!,
                articulo = articuloEscaneado!!,
                ubicacion = ubicacionEscaneada!!,
                lineaSeleccionada = lineaSeleccionada,
                viewModel = viewModel
            )
        }
        
        // Caso 2: Ubicación + artículo (flujo tradicional) = Crear o escanear palet
        ubicacionEscaneada != null && articuloEscaneado != null && paletEscaneado == null -> {
            android.util.Log.d("UI_PALET_CENTRO", "🔍 Mostrando sección: UBICACIÓN + ARTÍCULO - GestionarPaletSeccion")
        Spacer(modifier = Modifier.height(16.dp))
        
        GestionarPaletSeccion(
            articuloEscaneado = articuloEscaneado!!,
            ubicacionEscaneada = ubicacionEscaneada!!,
            lineaSeleccionada = lineaSeleccionada,
            ordenSeleccionada = ordenSeleccionada,
            viewModel = viewModel,
            sessionViewModel = sessionViewModel,
            user = user,
            traspasosLogic = traspasosLogic,
            focusRequester = focusRequester,
            escaneoProcesado = escaneoProcesado,
            onEscaneoProcesado = { escaneoProcesado = it }
        )
        }
    }
    
    // Captura de escaneos - PDA
    if (DeviceUtils.hasHardwareScanner(context)) {
        Box(
            modifier = Modifier
                .focusRequester(focusRequester)
                .focusable()
                .onPreviewKeyEvent { event ->
                    if (event.nativeKeyEvent?.action == android.view.KeyEvent.ACTION_MULTIPLE) {
                        if (escaneoProcesado) return@onPreviewKeyEvent true
                        escaneoProcesado = true

                        event.nativeKeyEvent.characters?.let { code ->
                            traspasosLogic.procesarCodigoEscaneado(
                                code = code.trim(),
                                empresaId = empresa,
                                onUbicacionDetectada = { codAlm, codUbi ->
                                    val stockEsperado = stockDisponible.first()
                                    if (codAlm.trim().uppercase() == stockEsperado.codigoAlmacen?.trim()?.uppercase() &&
                                        codUbi.trim().uppercase() == stockEsperado.ubicacion?.trim()?.uppercase()) {
                                        ubicacionEscaneada = codAlm to codUbi
                                        viewModel.setMensaje("✅ Ubicación validada correctamente")
                                    } else {
                                        mostrarDialogoError = "❌ Ubicación incorrecta\n\nDebe ir a: ${stockEsperado.codigoAlmacen}/${stockEsperado.ubicacion}\nHa escaneado: $codAlm/$codUbi"
                                    }
                                    escaneoProcesado = false
                                },
                                onArticuloDetectado = { articuloDto ->
                                    // Verificar si estamos en la fase inicial (debe escanear palet o ubicación)
                                    if (paletEscaneado == null && ubicacionEscaneada == null && articuloEscaneado == null) {
                                        // Fase inicial: mostrar error porque debe escanear palet o ubicación
                                        mostrarDialogoError = "❌ Escaneo incorrecto\n\nDebe escanear un palet (SSCC) o una ubicación.\nHa escaneado un artículo: ${articuloDto.codigoArticulo}"
                                    } else if (articuloDto.codigoArticulo.trim().uppercase() == lineaSeleccionada.codigoArticulo.trim().uppercase()) {
                                        // Fase correcta: validar artículo
                                        articuloEscaneado = articuloDto
                                        viewModel.setMensaje("✅ Artículo validado correctamente")
                                    } else {
                                        // Artículo incorrecto
                                        val articuloEsperado = lineaSeleccionada.codigoArticulo
                                        mostrarDialogoError = "❌ Artículo incorrecto\n\nDebe escanear: $articuloEsperado\nHa escaneado: ${articuloDto.codigoArticulo}"
                                    }
                                    escaneoProcesado = false
                                },
                                onMultipleArticulos = { articulos ->
                                    val articuloCorrecto = articulos.find { 
                                        it.codigoArticulo.trim().uppercase() == lineaSeleccionada.codigoArticulo.trim().uppercase() 
                                    }
                                    if (articuloCorrecto != null) {
                                        articuloEscaneado = articuloCorrecto
                                        viewModel.setMensaje("✅ Artículo validado correctamente")
                                    } else {
                                        mostrarDialogoError = "❌ Artículo no encontrado\n\nDebe escanear específicamente: ${lineaSeleccionada.codigoArticulo}"
                                    }
                                    escaneoProcesado = false
                                },
                                onPaletDetectado = { palet ->
                                    // DETECCIÓN INTELIGENTE: Palet se puede escanear en cualquier momento
                                    if (palet.estado.equals("Abierto", ignoreCase = true)) {
                                        paletEscaneado = palet
                                        viewModel.setPaletSeleccionado(palet)
                                        viewModel.obtenerLineasPalet(palet.id)
                                        viewModel.setMensaje("✅ Palet ${palet.codigoPalet} seleccionado correctamente")
                                    } else {
                                        mostrarDialogoError = "❌ Palet cerrado\n\nEl palet ${palet.codigoPalet} está cerrado.\nDebe escanear un palet abierto."
                                }
                                    escaneoProcesado = false
                                },
                                onError = { error ->
                                    mostrarDialogoError = "❌ Error de escaneo\n\n$error"
                                    escaneoProcesado = false
                                }
                            )
                        }
                        true
                    } else false
                }
                .layout { measurable, constraints ->
                    val placeable = measurable.measure(constraints)
                    layout(0, 0) { placeable.place(0, 0) }
                }
        )
        
        LaunchedEffect(Unit) {
            focusRequester.requestFocus()
        }
    }
    
    // Captura de escaneos - Móvil/Tablet
    if (!DeviceUtils.hasHardwareScanner(context)) {
        Spacer(modifier = Modifier.height(16.dp))
        
        QRScannerView(
            modifier = Modifier.fillMaxWidth().height(250.dp),
            onCodeScanned = { code ->
                if (escaneoProcesado) return@QRScannerView
                escaneoProcesado = true
                
                traspasosLogic.procesarCodigoEscaneado(
                    code = code.trim(),
                    empresaId = empresa,
                    onUbicacionDetectada = { codAlm, codUbi ->
                        val stockEsperado = stockDisponible.first()
                        if (codAlm.trim().uppercase() == stockEsperado.codigoAlmacen?.trim()?.uppercase() &&
                            codUbi.trim().uppercase() == stockEsperado.ubicacion?.trim()?.uppercase()) {
                            ubicacionEscaneada = codAlm to codUbi
                            viewModel.setMensaje("✅ Ubicación validada correctamente")
                        } else {
                            mostrarDialogoError = "❌ Ubicación incorrecta\n\nDebe ir a: ${stockEsperado.codigoAlmacen}/${stockEsperado.ubicacion}\nHa escaneado: $codAlm/$codUbi"
                        }
                        escaneoProcesado = false
                    },
                    onArticuloDetectado = { articuloDto ->
                        // Verificar si estamos en la fase inicial (debe escanear palet o ubicación)
                        if (paletEscaneado == null && ubicacionEscaneada == null && articuloEscaneado == null) {
                            // Fase inicial: mostrar error porque debe escanear palet o ubicación
                            mostrarDialogoError = "❌ Escaneo incorrecto\n\nDebe escanear un palet (SSCC) o una ubicación.\nHa escaneado un artículo: ${articuloDto.codigoArticulo}"
                        } else if (articuloDto.codigoArticulo.trim().uppercase() == lineaSeleccionada.codigoArticulo.trim().uppercase()) {
                            // Fase correcta: validar artículo
                            articuloEscaneado = articuloDto
                            viewModel.setMensaje("✅ Artículo validado correctamente")
                        } else {
                            // Artículo incorrecto
                            mostrarDialogoError = "❌ Artículo incorrecto\n\nDebe escanear: ${lineaSeleccionada.codigoArticulo}\nHa escaneado: ${articuloDto.codigoArticulo}"
                        }
                        escaneoProcesado = false
                    },
                    onMultipleArticulos = { articulos ->
                        val articuloCorrecto = articulos.find { 
                            it.codigoArticulo.trim().uppercase() == lineaSeleccionada.codigoArticulo.trim().uppercase() 
                        }
                        if (articuloCorrecto != null) {
                            articuloEscaneado = articuloCorrecto
                            viewModel.setMensaje("✅ Artículo validado correctamente")
                        } else {
                            mostrarDialogoError = "❌ Artículo no encontrado\n\nDebe escanear específicamente: ${lineaSeleccionada.codigoArticulo}"
                        }
                        escaneoProcesado = false
                    },
                    onPaletDetectado = { palet ->
                        // DETECCIÓN INTELIGENTE: Palet se puede escanear en cualquier momento
                        if (palet.estado.equals("Abierto", ignoreCase = true)) {
                            paletEscaneado = palet
                            viewModel.setPaletSeleccionado(palet)
                            viewModel.obtenerLineasPalet(palet.id)
                            viewModel.setMensaje("✅ Palet ${palet.codigoPalet} seleccionado correctamente")
                        } else {
                            mostrarDialogoError = "❌ Palet cerrado\n\nEl palet ${palet.codigoPalet} está cerrado.\nDebe escanear un palet abierto."
                        }
                        escaneoProcesado = false
                    },
                    onError = { error ->
                        mostrarDialogoError = "❌ Error de escaneo\n\n$error"
                        escaneoProcesado = false
                    }
                )
                
                // Reset después de un tiempo
                kotlinx.coroutines.GlobalScope.launch {
                    kotlinx.coroutines.delay(1000)
                    escaneoProcesado = false
                }
            }
        )
    }
    
    // DIÁLOGO DE ERROR - Simple y claro
    mostrarDialogoError?.let { mensaje ->
        AlertDialog(
            onDismissRequest = { mostrarDialogoError = null },
            title = { Text("Error de escaneo") },
            text = { Text(mensaje) },
            confirmButton = {
                TextButton(onClick = { mostrarDialogoError = null }) { 
                    Text("Reintentar") 
                }
            }
        )
    }
}

@Composable
fun PaletExistenteSeccion(
    palet: com.example.sga.data.dto.traspasos.PaletDto,
    articulo: ArticuloDto,
    ubicacion: Pair<String, String>,
    lineaSeleccionada: LineaOrdenTraspasoDetalleDto,
    viewModel: OrdenTraspasoViewModel
) {
    val lineasPalet by viewModel.lineasPalet.collectAsStateWithLifecycle()
    val lineasDelPalet = lineasPalet[palet.id] ?: emptyList()
    
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.primaryContainer)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Text(
                text = "PALET SELECCIONADO",
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.Bold,
                color = MaterialTheme.colorScheme.primary
            )
            
            Spacer(modifier = Modifier.height(12.dp))
            
            // Información del palet
            Text(
                text = "Palet: ${palet.codigoPalet}",
                style = MaterialTheme.typography.bodyLarge,
                fontWeight = FontWeight.Bold
            )
            Text(
                text = "Tipo: ${palet.tipoPaletCodigo}",
                style = MaterialTheme.typography.bodyMedium
            )
            Text(
                text = "Estado: ${palet.estado}",
                style = MaterialTheme.typography.bodyMedium
            )
            
            // Líneas existentes en el palet
            if (lineasDelPalet.isNotEmpty()) {
                Spacer(modifier = Modifier.height(8.dp))
                Text(
                    text = "Artículos actuales:",
                    style = MaterialTheme.typography.bodySmall,
                    fontWeight = FontWeight.Bold
                )
                lineasDelPalet.forEach { linea ->
                    Text(
                        text = "• ${linea.codigoArticulo}: ${linea.cantidad} uds",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            }
            
            Spacer(modifier = Modifier.height(16.dp))
            
            // Botón para añadir artículo
            Button(
                onClick = {
                    viewModel.activarDialogoCantidadDirecto(
                        palet = palet,
                        articulo = articulo,
                        ubicacion = ubicacion,
                        linea = lineaSeleccionada
                    )
                },
                modifier = Modifier.fillMaxWidth(),
                colors = ButtonDefaults.buttonColors(
                    containerColor = MaterialTheme.colorScheme.primary
                )
            ) {
                Text("➕ Añadir ${articulo.codigoArticulo} al palet")
            }
        }
    }
}

@Composable
fun GestionarPaletSeccion(
    articuloEscaneado: ArticuloDto,
    ubicacionEscaneada: Pair<String, String>,
    lineaSeleccionada: LineaOrdenTraspasoDetalleDto,
    ordenSeleccionada: OrdenTraspasoDto?,
    viewModel: OrdenTraspasoViewModel,
    sessionViewModel: com.example.sga.view.app.SessionViewModel,
    user: User,
    traspasosLogic: TraspasosLogic,
    focusRequester: FocusRequester,
    escaneoProcesado: Boolean,
    onEscaneoProcesado: (Boolean) -> Unit
) {
    val empresa = sessionViewModel.empresaSeleccionada.collectAsState().value?.codigo?.toShort() ?: return
    val usuarioId = user.id.toIntOrNull() ?: return
    val context = LocalContext.current
    
    // Estados del ViewModel para palets
    val tiposPalet by viewModel.tiposPalet.collectAsStateWithLifecycle()
    val paletSeleccionado by viewModel.paletSeleccionado.collectAsStateWithLifecycle()
    val cargando by viewModel.cargando.collectAsStateWithLifecycle()
    
    // Estados para impresión
    val impresoras by viewModel.impresoras.collectAsStateWithLifecycle()
    val impresoraSeleccionada by sessionViewModel.impresoraSeleccionada.collectAsState()
    val impresoraNombre = impresoraSeleccionada ?: ""
    
    // Estados locales
    var tipoSeleccionado by remember { mutableStateOf<String?>(null) }
    var dropOpen by remember { mutableStateOf(false) }
    var mostrarDialogoConfirmar by remember { mutableStateOf(false) }
    var mostrarDialogoError by remember { mutableStateOf<String?>(null) }
    
    // Estados para impresión (ya no se usan aquí - se manejan en función principal)
    var copias by remember { mutableIntStateOf(1) }
    var dropOpenImpresora by remember { mutableStateOf(false) }
    
    // Estados para cerrar palet
    var mostrarDialogoCerrarPalet by remember { mutableStateOf(false) }
    var idPaletParaCerrar by remember { mutableStateOf<String?>(null) }
    var esperandoUbicacionDestino by remember { mutableStateOf(false) }
    var traspasoPendienteId by remember { mutableStateOf<String?>(null) }
    var mostrarDialogoExito by remember { mutableStateOf(false) }
    
    // Cargar tipos de palet e impresoras
    LaunchedEffect(Unit) {
        viewModel.cargarTiposPalet()
        viewModel.cargarImpresoras()
    }
    
    // NOTA: Ya no se llama a ubicarPaletEnOrden - el flujo ahora es:
    // 1. Completar traspaso (devuelve ID del traspaso)
    // 2. Actualizar línea de orden con ese ID
    
    // Verificar si hay palet seleccionado y está abierto
    val hayPaletAbierto = paletSeleccionado != null && 
                         paletSeleccionado!!.estado.equals("Abierto", ignoreCase = true)
    
    // BLOQUEO: Si estamos esperando ubicación destino, solo mostrar instrucciones
    if (esperandoUbicacionDestino) {
        Card(
            modifier = Modifier.fillMaxWidth(),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.errorContainer)
        ) {
            Column(modifier = Modifier.padding(16.dp)) {
                Text(
                    text = "TRASPASO PENDIENTE",
                    style = MaterialTheme.typography.titleLarge,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.error
                )
                Spacer(modifier = Modifier.height(8.dp))
                Text(
                    text = "Escanee la ubicación DESTINO para completar el traspaso:",
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold
                )
                Text(
                    text = "ID Traspaso: $traspasoPendienteId",
                    style = MaterialTheme.typography.bodyMedium
                )
            }
        }
        
        // Captura de escaneos para ubicación destino - PDA
        if (DeviceUtils.hasHardwareScanner(context)) {
            Box(
                modifier = Modifier
                    .focusRequester(focusRequester)
                    .focusable()
                    .onPreviewKeyEvent { event ->
                        if (event.nativeKeyEvent?.action == android.view.KeyEvent.ACTION_MULTIPLE) {
                            event.nativeKeyEvent.characters?.let { code ->
                                manejarEscaneoDestino(
                                    code = code.trim(),
                                    empresa = empresa,
                                    traspasosLogic = traspasosLogic,
                                    onUbicacionDestino = { almacenDestino, ubicacionDestino ->
                                        // Completar traspaso
                                        viewModel.completarTraspaso(
                                            id = traspasoPendienteId!!,
                                            codigoAlmacenDestino = almacenDestino,
                                            ubicacionDestino = ubicacionDestino,
                                            usuarioId = usuarioId,
                                            paletId = paletSeleccionado?.id,
                                            onSuccess = {
                                                // Traspaso completado exitosamente
                                                esperandoUbicacionDestino = false
                                                traspasoPendienteId = null
                                                mostrarDialogoExito = true
                                            },
                                            onError = { error ->
                                                mostrarDialogoError = "❌ Error al completar traspaso\n\n$error"
                                            }
                                        )
                                    },
                                    onError = { error ->
                                        mostrarDialogoError = "❌ Error de escaneo\n\n$error"
                                    }
                                )
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
                    focusRequester.requestFocus()
                }
            }
        }
        
        // Captura de escaneos para ubicación destino - Móvil/Tablet
        if (!DeviceUtils.hasHardwareScanner(context)) {
            Spacer(modifier = Modifier.height(16.dp))
            
            QRScannerView(
                modifier = Modifier.fillMaxWidth().height(250.dp),
                onCodeScanned = { code ->
                    manejarEscaneoDestino(
                        code = code.trim(),
                        empresa = empresa,
                        traspasosLogic = traspasosLogic,
                        onUbicacionDestino = { almacenDestino, ubicacionDestino ->
                            // Completar traspaso
                            viewModel.completarTraspaso(
                                id = traspasoPendienteId!!,
                                codigoAlmacenDestino = almacenDestino,
                                ubicacionDestino = ubicacionDestino,
                                usuarioId = usuarioId,
                                paletId = paletSeleccionado?.id,
                                onSuccess = {
                                    // Traspaso completado exitosamente
                                    esperandoUbicacionDestino = false
                                    traspasoPendienteId = null
                                    mostrarDialogoExito = true
                                },
                                onError = { error ->
                                    mostrarDialogoError = "❌ Error al completar traspaso\n\n$error"
                                }
                            )
                        },
                        onError = { error ->
                            mostrarDialogoError = "❌ Error de escaneo\n\n$error"
                        }
                    )
                }
            )
        }
        
        return // No mostrar el resto de la UI cuando estamos esperando destino
    }
    
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = if (hayPaletAbierto) 
                MaterialTheme.colorScheme.secondaryContainer 
            else 
                MaterialTheme.colorScheme.primaryContainer
        )
    ) {
        LazyColumn(
            modifier = Modifier
                .padding(16.dp)
                .heightIn(max = 600.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            if (hayPaletAbierto) {
                // CASO: Ya hay un palet abierto
                item {
                    Text(
                        text = "Usar palet existente",
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.primary
                    )
                }
                
                item { Spacer(modifier = Modifier.height(12.dp)) }
                
                item {
                    Card(
                        modifier = Modifier.fillMaxWidth(),
                        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
                    ) {
                    Column(modifier = Modifier.padding(12.dp)) {
                        // Header con información del palet y botón de deselección
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Column(modifier = Modifier.weight(1f)) {
                                Text(
                                    text = "Palet actual: ${paletSeleccionado!!.codigoPalet}",
                                    style = MaterialTheme.typography.bodyLarge,
                                    fontWeight = FontWeight.Bold
                                )
                                Text(
                                    text = "Tipo: ${paletSeleccionado!!.tipoPaletCodigo}",
                                    style = MaterialTheme.typography.bodyMedium
                                )
                                paletSeleccionado!!.ordenTrabajoId?.let { orden ->
                                    Text(
                                        text = "Orden: $orden",
                                        style = MaterialTheme.typography.bodySmall
                                    )
                                }
                            }
                            
                            // Botón X para deseleccionar palet
                            IconButton(
                                onClick = {
                                    android.util.Log.d("UI_PALET", "🗑️ Click en deseleccionar palet")
                                    viewModel.deseleccionarPalet() // Limpia ViewModel
                                },
                                modifier = Modifier
                                    .size(32.dp)
                                    .background(
                                        MaterialTheme.colorScheme.errorContainer,
                                        CircleShape
                                    )
                            ) {
                                Icon(
                                    imageVector = Icons.Default.Close,
                                    contentDescription = "Deseleccionar palet",
                                    tint = MaterialTheme.colorScheme.onErrorContainer,
                                    modifier = Modifier.size(20.dp)
                                )
                            }
                        }
                        
                        
                        // Mostrar líneas del palet con scroll
                        val lineasPalet by viewModel.lineasPalet.collectAsStateWithLifecycle()
                        val lineasDelPalet = lineasPalet[paletSeleccionado!!.id] ?: emptyList()
                        
                        if (lineasDelPalet.isNotEmpty()) {
                            Spacer(modifier = Modifier.height(8.dp))
                            Text(
                                text = "Artículos en el palet:",
                                style = MaterialTheme.typography.bodySmall,
                                fontWeight = FontWeight.Bold
                            )
                            
                            // Lista con scroll para las líneas del palet
                            LazyColumn(
                                modifier = Modifier
                                    .heightIn(max = 300.dp)
                                    .fillMaxWidth(),
                                verticalArrangement = Arrangement.spacedBy(4.dp)
                            ) {
                                items(lineasDelPalet) { linea ->
                                    Column {
                                        Text(
                                            text = "• ${linea.codigoArticulo}: ${linea.cantidad} uds",
                                            style = MaterialTheme.typography.bodySmall,
                                            color = MaterialTheme.colorScheme.onSurfaceVariant
                                        )
                                        // Mostrar lote/partida si está disponible
                                        linea.lote?.let { lote ->
                                            Text(
                                                text = "  Lote: $lote",
                                                style = MaterialTheme.typography.bodySmall,
                                                color = MaterialTheme.colorScheme.onSurfaceVariant
                                            )
                                        }
                                    }
                                }
                            }
                        }
                    }
                    }
                }
                
                item { Spacer(modifier = Modifier.height(12.dp)) }
                
                // Botón para añadir al palet existente (FUERA del Card)
                item {
                    Button(
                        onClick = {
                            // Activar diálogo de cantidad directamente (sin impresión)
                            viewModel.activarDialogoCantidadDirecto(
                                palet = paletSeleccionado!!,
                                articulo = articuloEscaneado!!,
                                ubicacion = ubicacionEscaneada!!,
                                linea = lineaSeleccionada
                            )
                        },
                        modifier = Modifier.fillMaxWidth(),
                        colors = ButtonDefaults.buttonColors(
                            containerColor = MaterialTheme.colorScheme.primary
                        )
                    ) {
                        Text("➕ Añadir artículo al palet")
                    }
                }
                
            } else {
                // CASO: No hay palet o está cerrado - Crear nuevo
                item {
                    Text(
                        text = "Crear palet o escanear uno existente",
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.primary
                    )
                }
                
                item { Spacer(modifier = Modifier.height(12.dp)) }
                                
                // Selector de tipo de palet
                item {
                    Box {
                    OutlinedTextField(
                        readOnly = true,
                        value = tiposPalet
                            .firstOrNull { it.codigoPalet == tipoSeleccionado }
                            ?.let { "${it.codigoPalet} - ${it.descripcion}" }
                            ?: "",
                        onValueChange = {},
                        label = { Text("Tipo de palet") },
                        modifier = Modifier.fillMaxWidth(),
                        trailingIcon = {
                            IconButton(onClick = { dropOpen = !dropOpen }) {
                                Icon(Icons.Default.ArrowDropDown, contentDescription = null)
                            }
                        }
                    )

                    DropdownMenu(
                        expanded = dropOpen,
                        onDismissRequest = { dropOpen = false }
                    ) {
                        tiposPalet.forEach { tipo ->
                            DropdownMenuItem(
                                text = { Text("${tipo.codigoPalet} - ${tipo.descripcion}") },
                                onClick = {
                                    tipoSeleccionado = tipo.codigoPalet
                                    dropOpen = false
                                }
                            )
                        }
                    }
                    }
                }

                item { Spacer(modifier = Modifier.height(16.dp)) }

                // Orden de trabajo (automática)
                item {
                    OutlinedTextField(
                    value = ordenSeleccionada?.codigoOrden ?: "",
                    onValueChange = { },
                    readOnly = true,
                    label = { Text("Orden de trabajo") },
                    modifier = Modifier.fillMaxWidth(),
                    colors = OutlinedTextFieldDefaults.colors(
                        disabledTextColor = MaterialTheme.colorScheme.onSurface,
                        disabledBorderColor = MaterialTheme.colorScheme.outline,
                        disabledLabelColor = MaterialTheme.colorScheme.onSurfaceVariant
                    ),
                    enabled = false
                    )
                }

                item { Spacer(modifier = Modifier.height(16.dp)) }

                // Botón crear palet
                item {
                    Button(
                    onClick = { mostrarDialogoConfirmar = true },
                    enabled = tipoSeleccionado != null && !cargando,
                    modifier = Modifier.fillMaxWidth()
                ) {
                    if (cargando) {
                        CircularProgressIndicator(
                            modifier = Modifier.size(16.dp),
                            strokeWidth = 2.dp,
                            color = MaterialTheme.colorScheme.onPrimary
                        )
                        Spacer(modifier = Modifier.width(8.dp))
                    }
                    Text("Crear palet nuevo")
                    }
                }
            }
            
            item { Spacer(modifier = Modifier.height(16.dp)) }
            
            // Información del artículo a añadir (solo cuando hay palet creado)
            if (hayPaletAbierto) {
                item {
                    Card(
                    modifier = Modifier.fillMaxWidth(),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
                ) {
                    Column(modifier = Modifier.padding(12.dp)) {
                        Text(
                            text = "Artículo a añadir:",
                            style = MaterialTheme.typography.labelMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        Text(
                            text = "📦 ${articuloEscaneado.codigoArticulo}",
                            style = MaterialTheme.typography.bodyLarge,
                            fontWeight = FontWeight.Bold
                        )
                        articuloEscaneado.descripcion?.let { desc ->
                            Text(
                                text = desc,
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                        Text(
                            text = "Cantidad: ${lineaSeleccionada.cantidadPlan}",
                            style = MaterialTheme.typography.bodyMedium
                        )
                        articuloEscaneado.partida?.let { partida ->
                            Text(
                                text = "Partida: $partida",
                                style = MaterialTheme.typography.bodySmall
                            )
                        }
                    }
                    }
                }
            }
        }
    }
    
    // Escaneo de palets (solo si no hay hardware scanner activo en el componente padre)
    if (!hayPaletAbierto && !DeviceUtils.hasHardwareScanner(context)) {
        Spacer(modifier = Modifier.height(16.dp))
        
        QRScannerView(
            modifier = Modifier.fillMaxWidth().height(200.dp),
            onCodeScanned = { code ->
                if (escaneoProcesado) return@QRScannerView
                onEscaneoProcesado(true)
                
                traspasosLogic.procesarCodigoEscaneado(
                    code = code.trim(),
                    empresaId = empresa,
                    onUbicacionDetectada = { _, _ ->
                        mostrarDialogoError = "❌ Ha escaneado una ubicación\n\nDebe escanear un palet (SSCC) o crear uno nuevo."
                        onEscaneoProcesado(false)
                    },
                    onArticuloDetectado = { _ ->
                        mostrarDialogoError = "❌ Ha escaneado un artículo\n\nDebe escanear un palet (SSCC) o crear uno nuevo."
                        onEscaneoProcesado(false)
                    },
                    onMultipleArticulos = { _ ->
                        mostrarDialogoError = "❌ Ha escaneado un artículo\n\nDebe escanear un palet (SSCC) o crear uno nuevo."
                        onEscaneoProcesado(false)
                    },
                    onPaletDetectado = { palet ->
                        if (palet.estado.equals("Abierto", ignoreCase = true)) {
                            viewModel.setPaletSeleccionado(palet)
                            viewModel.obtenerLineasPalet(palet.id) // ✅ OBTENER LÍNEAS
                            viewModel.setMensaje("Palet ${palet.codigoPalet} seleccionado correctamente")
                        } else {
                            mostrarDialogoError = "❌ Palet cerrado\n\nEl palet ${palet.codigoPalet} está cerrado.\nDebe escanear un palet abierto o crear uno nuevo."
                        }
                        onEscaneoProcesado(false)
                    },
                    onError = { error ->
                        mostrarDialogoError = "❌ Error de escaneo\n\n$error"
                        onEscaneoProcesado(false)
                    }
                )
            }
        )
    }
    
    // Diálogo de confirmación para crear palet
    if (mostrarDialogoConfirmar) {
        AlertDialog(
            onDismissRequest = { mostrarDialogoConfirmar = false },
            title = { Text("Confirmar creación de palet") },
            text = {
                Column {
                    Text("Se creará un palet nuevo con:")
                    Spacer(modifier = Modifier.height(8.dp))
                    Text("• Tipo: ${tiposPalet.find { it.codigoPalet == tipoSeleccionado }?.descripcion}")
                    Text("• Orden: ${ordenSeleccionada?.codigoOrden ?: ""}")
                    Text("• Ubicación origen: ${ubicacionEscaneada.first}/${ubicacionEscaneada.second}")
                }
            },
            confirmButton = {
                TextButton(
                    onClick = {
                        mostrarDialogoConfirmar = false
                        viewModel.crearPalet(
                            PaletCrearDto(
                                codigoEmpresa = empresa,
                                usuarioAperturaId = usuarioId,
                                tipoPaletCodigo = tipoSeleccionado!!,
                                ordenTrabajoId = ordenSeleccionada?.codigoOrden
                            )
                        ) { nuevoPalet ->
                            // Palet creado exitosamente
                            android.util.Log.d("CREAR_PALET", "Palet creado: ${nuevoPalet.codigoPalet}")
                            viewModel.setPaletSeleccionado(nuevoPalet)
                            viewModel.setMensaje("Palet ${nuevoPalet.codigoPalet} creado correctamente")
                            // Activar impresión obligatoria usando ViewModel
                            android.util.Log.d("CREAR_PALET", "🖨️ Activando diálogo vía ViewModel")
                            viewModel.activarDialogoImpresion(
                                palet = nuevoPalet,
                                articulo = articuloEscaneado,
                                ubicacion = ubicacionEscaneada,
                                linea = lineaSeleccionada
                            )
                        }
                    }
                ) { Text("Crear") }
            },
            dismissButton = {
                TextButton(onClick = { mostrarDialogoConfirmar = false }) { 
                    Text("Cancelar") 
                }
            }
        )
    }
    
    // Diálogo de error
    mostrarDialogoError?.let { mensaje ->
        AlertDialog(
            onDismissRequest = { mostrarDialogoError = null },
            title = { Text("Error de escaneo") },
            text = { Text(mensaje) },
            confirmButton = {
                TextButton(onClick = { mostrarDialogoError = null }) { 
                    Text("Reintentar") 
                }
            }
        )
    }
    
    // DIÁLOGO DE CONFIRMACIÓN PARA CERRAR PALET
    if (mostrarDialogoCerrarPalet && idPaletParaCerrar != null) {
        AlertDialog(
            onDismissRequest = { mostrarDialogoCerrarPalet = false },
            title = { Text("Cerrar palet") },
            text = {
                Column {
                    Text(
                        text = "¿Está seguro de que desea cerrar el palet?",
                        fontWeight = FontWeight.Bold
                    )
                    Spacer(modifier = Modifier.height(8.dp))
                    Text("Palet: ${paletSeleccionado?.codigoPalet}")
                    Text("Esta acción creará un traspaso en estado PENDIENTE")
                    Text("Deberá escanear la ubicación destino para completarlo")
                }
            },
            confirmButton = {
                TextButton(
                    onClick = {
                        mostrarDialogoCerrarPalet = false
                        
                        viewModel.cerrarPalet(
                            id = idPaletParaCerrar!!,
                            usuarioId = usuarioId,
                            codigoAlmacen = ubicacionEscaneada?.first,
                            codigoEmpresa = empresa,
                            onSuccess = { traspasoId ->
                                // Palet cerrado exitosamente - activar bloqueo
                                esperandoUbicacionDestino = true
                                traspasoPendienteId = traspasoId
                                idPaletParaCerrar = null
                                viewModel.setMensaje("Palet cerrado. Traspaso creado: $traspasoId")
                                viewModel.setMensaje("Escanee la ubicación destino para completar el traspaso")
                            },
                            onError = { error ->
                                mostrarDialogoError = "❌ Error al cerrar palet\n\n$error"
                            }
                        )
                    }
                ) { 
                    Text("Sí, cerrar palet") 
                }
            },
            dismissButton = {
                TextButton(onClick = { 
                    mostrarDialogoCerrarPalet = false
                    idPaletParaCerrar = null
                }) { 
                    Text("Cancelar") 
                }
            }
        )
    }
    
    // DIÁLOGO DE ÉXITO (solo mostrar brevemente y luego continuar automáticamente)
    if (mostrarDialogoExito) {
        AlertDialog(
            onDismissRequest = {
                mostrarDialogoExito = false
            },
            title = { Text("Trabajo completado") },
            text = {
                Column {
                    Text("Artículo completado exitosamente")
                    Spacer(modifier = Modifier.height(8.dp))
                    Text(
                        text = "Continuando automáticamente...",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.primary
                    )
                }
            },
            confirmButton = {
                TextButton(onClick = { 
                    mostrarDialogoExito = false
                }) { 
                    Text("Continuar") 
                }
            }
        )
        
        // Auto-continuar después de 2 segundos
        LaunchedEffect(mostrarDialogoExito) {
            if (mostrarDialogoExito) {
                kotlinx.coroutines.delay(2000) // 2 segundos
                mostrarDialogoExito = false
            }
        }
    }
    
}

@Composable
fun HeaderCompacto(
    orden: OrdenTraspasoDto,
    user: User?,
    paletActivo: String?,
    paletsPendientes: List<PaletPendienteDto>,
    onVerPaletsPendientes: () -> Unit,
    paletSeleccionado: com.example.sga.data.dto.traspasos.PaletDto?,
    onCerrarPalet: (String) -> Unit,
    onAbrirPalet: (String) -> Unit,
    viewModel: OrdenTraspasoViewModel,
    modifier: Modifier = Modifier
) {
    Card(
        modifier = modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant)
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(8.dp)
        ) {
            // FILA 1: Información básica de la orden
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                // INFORMACIÓN DE LA ORDEN
                Column(modifier = Modifier.weight(0.6f)) {
                    Text(
                        text = orden.codigoOrden ?: "Sin código",
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.Bold
                    )
                    
                    Row(
                        horizontalArrangement = Arrangement.spacedBy(6.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        EstadoOrdenChip(estado = orden.estado)
                    }
                }
                
                // FECHA Y PALET ACTIVO
                Column(
                    modifier = Modifier.weight(0.4f),
                    horizontalAlignment = Alignment.End
                ) {
                    orden.fechaPlan?.let { fechaPlan ->
                        Text(
                            text = formatearSoloFecha(fechaPlan),
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                    
                    android.util.Log.d("UI_PALET_HEADER", "🔍 paletActivo: $paletActivo")
                    if (paletActivo == null) {
                        android.util.Log.d("UI_PALET_HEADER", "❌ paletActivo es null, no se muestra la X")
                    }
                    paletActivo?.let { palet ->
                        android.util.Log.d("UI_PALET_HEADER", "🔍 Mostrando paletActivo: $palet")
                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(4.dp)
                        ) {
                            Text(
                                text = "📦 $palet",
                                style = MaterialTheme.typography.bodySmall,
                                fontWeight = FontWeight.Bold,
                                color = MaterialTheme.colorScheme.primary
                            )
                            
                            // Botón X para deseleccionar palet
                            android.util.Log.d("UI_PALET_HEADER", "🔍 Mostrando botón X para palet: $palet")
                            IconButton(
                                onClick = {
                                    android.util.Log.d("UI_PALET_HEADER", "🗑️ Click en X para deseleccionar palet: $palet")
                                    viewModel.deseleccionarPalet()
                                },
                                modifier = Modifier.size(20.dp)
                            ) {
                                Icon(
                                    imageVector = Icons.Default.Close,
                                    contentDescription = "Deseleccionar palet",
                                    tint = MaterialTheme.colorScheme.error,
                                    modifier = Modifier.size(14.dp)
                                )
                            }
                        }
                        
                        // Switch para abrir/cerrar palet (dentro del header)
                        paletSeleccionado?.let { paletSeleccionado ->
                            val estaAbierto = paletSeleccionado.estado.equals("Abierto", ignoreCase = true)
                            Row(
                                modifier = Modifier.padding(top = 2.dp),
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                Text(
                                    text = "Estado del palet:",
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                                    fontSize = 10.sp
                                )
                                Spacer(Modifier.width(4.dp))
                                Switch(
                                    checked = estaAbierto,
                                    onCheckedChange = { nuevoEstado ->
                                        android.util.Log.d("TOGGLE_PALET", "🔄 Toggle cambiado: nuevoEstado=$nuevoEstado, estaAbierto=$estaAbierto")
                                        if (nuevoEstado && !estaAbierto) {
                                            // ABRIR palet
                                            android.util.Log.d("TOGGLE_PALET", "🔓 Abriendo palet: ${paletSeleccionado.id}")
                                            onAbrirPalet(paletSeleccionado.id)
                                        } else if (!nuevoEstado && estaAbierto) {
                                            // CERRAR palet
                                            android.util.Log.d("TOGGLE_PALET", "🔒 Cerrando palet: ${paletSeleccionado.id}")
                                            onCerrarPalet(paletSeleccionado.id)
                                        }
                                    },
                                    modifier = Modifier.scale(0.7f)
                                )
                                Spacer(Modifier.width(4.dp))
                                Text(
                                    text = if (estaAbierto) "Abierto" else "Cerrado",
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                                    fontSize = 10.sp
                                )
                            }
                        }
                    }
                }
            }
            
            Spacer(modifier = Modifier.height(8.dp))
            
            // FILA 2: Progreso y acciones
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
            
                // ESPACIO LIBRE
                Spacer(modifier = Modifier.weight(0.5f))
                
                // ACCIONES
                Column(
                    modifier = Modifier.weight(0.5f),
                    horizontalAlignment = Alignment.End
                ) {
                    android.util.Log.d("UI_PALETS", "🔍 HeaderCompacto - paletsPendientes.size: ${paletsPendientes.size}")
                    if (paletsPendientes.isNotEmpty()) {
                        android.util.Log.d("UI_PALETS", "✅ Mostrando botón Ver Palets")
                        Button(
                            onClick = onVerPaletsPendientes,
                            modifier = Modifier.height(32.dp),
                            contentPadding = PaddingValues(horizontal = 8.dp, vertical = 4.dp)
                        ) {
                            Text(
                                text = "Ver Palets (${paletsPendientes.size})",
                                style = MaterialTheme.typography.labelSmall
                            )
                        }
                    } else {
                        android.util.Log.d("UI_PALETS", "❌ No se muestra botón Ver Palets - lista vacía")
                    }
                }
            }
        }
    }
}

@Composable
fun AccionPrincipal(
    orden: OrdenTraspasoDto,
    user: User,
    lineaSeleccionada: LineaOrdenTraspasoDetalleDto?,
    stockDisponible: List<StockDisponibleDto>,
    ubicacionOrigenSeleccionada: StockDisponibleDto?,
    viewModel: OrdenTraspasoViewModel,
    sessionViewModel: com.example.sga.view.app.SessionViewModel,
    onLineaSeleccionada: (LineaOrdenTraspasoDetalleDto) -> Unit
) {
    when {
        // PASO 1: Seleccionar línea
        lineaSeleccionada == null -> {
            AccionSeleccionarLinea(
                orden = orden,
                user = user,
                onLineaSeleccionada = onLineaSeleccionada
            )
        }
        
        // PASO 2: Mostrar stock y permitir escaneo
        stockDisponible.isEmpty() -> {
            AccionCargandoStock()
        }
        
        // PASO 3: Escaneo y gestión de palet
        else -> {
            AccionEscaneoYPalet(
                stockDisponible = stockDisponible,
                lineaSeleccionada = lineaSeleccionada,
                ordenSeleccionada = orden,
                viewModel = viewModel,
                sessionViewModel = sessionViewModel,
                user = user
            )
        }
    }
}

@Composable
fun AccionSeleccionarLinea(
    orden: OrdenTraspasoDto,
    user: User,
    onLineaSeleccionada: (LineaOrdenTraspasoDetalleDto) -> Unit
) {
    val todasLasLineas = orden.lineas
    val lineasDelOperario = orden.lineas.filter { linea ->
        linea.idOperarioAsignado == user.id.toInt()
    }
    val lineasOperario = lineasDelOperario.filter { linea ->
        !linea.completada
    }.sortedWith(compareBy { linea ->
        when (linea.estado) {
            "EN_PROCESO" -> 1  // Primero las que ya están empezadas
            "PENDIENTE" -> 2   // Luego las pendientes
            "SUBDIVIDIDO" -> 3 // Las subdivididas después
            "COMPLETADA" -> 4
            else -> 5
        }
    })
    
    // Log de debug
    android.util.Log.d("AccionSeleccionarLinea", "📊 DEBUG - Total líneas en orden: ${todasLasLineas.size}")
    android.util.Log.d("AccionSeleccionarLinea", "👤 User ID: ${user.id.toInt()}")
    android.util.Log.d("AccionSeleccionarLinea", "📋 Líneas del operario: ${lineasDelOperario.size}")
    android.util.Log.d("AccionSeleccionarLinea", "⏳ Líneas pendientes: ${lineasOperario.size}")
    
    lineasDelOperario.forEachIndexed { index, linea ->
        android.util.Log.d("AccionSeleccionarLinea", "📝 Línea $index: ID=${linea.idLineaOrdenTraspaso}, Operario=${linea.idOperarioAsignado}, Completada=${linea.completada}, Estado=${linea.estado}")
    }
    
    Column(
        modifier = Modifier.fillMaxSize()
    ) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(16.dp),
            verticalArrangement = Arrangement.Top
        ) {
            if (lineasOperario.isNotEmpty()) {
                // Mostrar líneas directamente, sin texto grande innecesario
                Column(
                    modifier = Modifier.fillMaxWidth(),
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    lineasOperario.forEach { linea ->
                        LineaOrdenCard(
                            linea = linea,
                            onClick = { onLineaSeleccionada(linea) }
                        )
                    }
                }
            } else {
                // Mostrar información de debug
                Icon(
                    if (lineasDelOperario.isEmpty()) Icons.Default.Info else Icons.Default.CheckCircle,
                    contentDescription = null,
                    modifier = Modifier.size(64.dp),
                    tint = if (lineasDelOperario.isEmpty()) MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.primary
                )
                
                Text(
                    text = if (lineasDelOperario.isEmpty()) "SIN ARTÍCULOS ASIGNADOS" else "COMPLETADO",
                    style = MaterialTheme.typography.headlineMedium,
                    fontWeight = FontWeight.Bold,
                    textAlign = TextAlign.Center,
                    color = if (lineasDelOperario.isEmpty()) MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.primary
                )
                
                if (lineasDelOperario.isEmpty()) {
                    Text(
                        text = "No tienes artículos asignados en esta orden",
                        style = MaterialTheme.typography.bodyLarge,
                        textAlign = TextAlign.Center
                    )
                    
                    Spacer(modifier = Modifier.height(8.dp))
                    
                    // Información de debug
                    Card(
                        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.errorContainer)
                    ) {
                        Column(modifier = Modifier.padding(12.dp)) {
                            Text(
                                text = "Debug Info:",
                                style = MaterialTheme.typography.labelMedium,
                                fontWeight = FontWeight.Bold
                            )
                            Text("Total líneas: ${todasLasLineas.size}")
                            Text("Tu ID: ${user.id.toInt()}")
                            if (todasLasLineas.isNotEmpty()) {
                                Text("IDs de operarios en líneas:")
                                todasLasLineas.take(3).forEach { linea ->
                                    Text("• Línea ${linea.idLineaOrdenTraspaso}: Operario ${linea.idOperarioAsignado}")
                                }
                            }
                        }
                    }
                } else {
                    Text(
                        text = "Has completado todos los artículos asignados (${lineasDelOperario.count { it.completada }}/${lineasDelOperario.size})",
                        style = MaterialTheme.typography.bodyLarge,
                        textAlign = TextAlign.Center
                    )
                }
            }
        }
    }
}

@Composable
fun AccionCargandoStock() {
    Card(
        modifier = Modifier.fillMaxSize(),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant)
    ) {
        Box(
            modifier = Modifier.fillMaxSize(),
            contentAlignment = Alignment.Center
        ) {
            Column(horizontalAlignment = Alignment.CenterHorizontally) {
                CircularProgressIndicator(modifier = Modifier.size(48.dp))
                Spacer(modifier = Modifier.height(16.dp))
                Text(
                    text = "Consultando stock disponible...",
                    style = MaterialTheme.typography.bodyLarge,
                    textAlign = TextAlign.Center
                )
            }
        }
    }
}

@Composable
fun AccionEscaneoYPalet(
    stockDisponible: List<StockDisponibleDto>,
    lineaSeleccionada: LineaOrdenTraspasoDetalleDto,
    ordenSeleccionada: OrdenTraspasoDto,
    viewModel: OrdenTraspasoViewModel,
    sessionViewModel: com.example.sga.view.app.SessionViewModel,
    user: User
) {
    // Reutilizar el componente existente EscaneoBasicoOrden
    EscaneoBasicoOrden(
        stockDisponible = stockDisponible,
        lineaSeleccionada = lineaSeleccionada,
        ordenSeleccionada = ordenSeleccionada,
        viewModel = viewModel,
        sessionViewModel = sessionViewModel,
        user = user
    )
}

@Composable
fun InformacionApoyo(
    modifier: Modifier = Modifier,
    lineaSeleccionada: LineaOrdenTraspasoDetalleDto?,
    stockDisponible: List<StockDisponibleDto>,
    paletSeleccionado: com.example.sga.data.dto.traspasos.PaletDto?,
    user: User?,
    viewModel: OrdenTraspasoViewModel
) {
    var expandido by remember { mutableStateOf(false) }
    
    Card(
        modifier = modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
    ) {
        Column(
            modifier = Modifier.padding(12.dp)
        ) {
            // Header plegable
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .clickable { expandido = !expandido },
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = "Información detallada",
                    style = MaterialTheme.typography.labelMedium,
                    fontWeight = FontWeight.Bold
                )
                Icon(
                    if (expandido) Icons.Default.ExpandLess else Icons.Default.ExpandMore,
                    contentDescription = null
                )
            }
            
            // Contenido expandible
            if (expandido) {
                Spacer(modifier = Modifier.height(8.dp))
                
                // Mostrar información contextual con explicaciones (con scroll)
                LazyColumn(
                    modifier = Modifier.heightIn(max = 400.dp),
                    verticalArrangement = Arrangement.spacedBy(6.dp)
                ) {
                    // 1. Línea de trabajo actual (PRIMERO)
                    lineaSeleccionada?.let { linea ->
                        item {
                            Text(
                                text = "ARTÍCULO SELECCIONADO:",
                                style = MaterialTheme.typography.labelSmall,
                                fontWeight = FontWeight.Bold,
                                color = MaterialTheme.colorScheme.primary
                            )
                        }
                        item {
                            Text(
                                text = "Artículo: ${linea.codigoArticulo} - ${linea.descripcionArticulo ?: "Sin descripción"}",
                                style = MaterialTheme.typography.bodySmall
                            )
                        }
                        item {
                            Text(
                                text = "Cantidad a recoger: ${linea.cantidadPlan}",
                                style = MaterialTheme.typography.bodySmall
                            )
                        }
                    }
                    
                    // 2. Ubicación de origen (donde buscar) (SEGUNDO)
                    if (stockDisponible.isNotEmpty()) {
                        val stock = stockDisponible.first()
                        item {
                            Text(
                                text = "UBICACIÓN DEL ARTÍCULO:",
                                style = MaterialTheme.typography.labelSmall,
                                fontWeight = FontWeight.Bold,
                                color = MaterialTheme.colorScheme.primary
                            )
                        }
                        item {
                            Text(
                                text = "Almacén: ${stock.codigoAlmacen}",
                                style = MaterialTheme.typography.bodySmall
                            )
                        }
                        item {
                            Text(
                                text = "Ubicación: ${stock.ubicacion}",
                                style = MaterialTheme.typography.bodySmall
                            )
                        }
                        item {
                            Text(
                                text = "Stock disponible: ${stock.cantidadDisponible}",
                                style = MaterialTheme.typography.bodySmall
                            )
                        }
                    }
                    
                    // 3. Artículos en el palet (TERCERO)
                    paletSeleccionado?.let { palet ->
                        // Mostrar solo las líneas del palet
                        item {
                            LineasPaletInfo(
                                palet = palet,
                                user = user,
                                viewModel = viewModel
                            )
                        }
                    }
                }
            }
        }
    }
}

// Función auxiliar para manejar escaneo de ubicación destino
private fun manejarEscaneoDestino(
    code: String,
    empresa: Short,
    traspasosLogic: TraspasosLogic,
    onUbicacionDestino: (String, String) -> Unit,
    onError: (String) -> Unit
) {
    traspasosLogic.procesarCodigoEscaneado(
        code = code,
        empresaId = empresa,
        onUbicacionDetectada = { almacenDestino, ubicacionDestino ->
            onUbicacionDestino(almacenDestino, ubicacionDestino)
        },
        onArticuloDetectado = { _ ->
            onError("Ha escaneado un artículo. Debe escanear una ubicación destino.")
        },
        onMultipleArticulos = { _ ->
            onError("Ha escaneado un artículo. Debe escanear una ubicación destino.")
        },
        onPaletDetectado = { _ ->
            onError("Ha escaneado un palet. Debe escanear una ubicación destino.")
        },
        onError = onError
    )
}

@Composable
fun LineasPaletInfo(
    palet: com.example.sga.data.dto.traspasos.PaletDto,
    user: User?,
    viewModel: OrdenTraspasoViewModel
) {
    val lineasPalet by viewModel.lineasPalet.collectAsStateWithLifecycle()
    val lineasDelPalet = lineasPalet[palet.id] ?: emptyList()
    
    if (lineasDelPalet.isNotEmpty()) {
        Spacer(modifier = Modifier.height(4.dp))
        Text(
            text = "ARTÍCULOS EN EL PALET:",
            style = MaterialTheme.typography.labelSmall,
            fontWeight = FontWeight.Bold,
            color = MaterialTheme.colorScheme.primary
        )
        
        lineasDelPalet.forEach { lineaPalet ->
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Column(modifier = Modifier.weight(1f)) {
                    Text(
                        text = "• ${lineaPalet.codigoArticulo}",
                        style = MaterialTheme.typography.bodySmall
                    )
                    Text(
                        text = "  ${lineaPalet.cantidad} uds",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
                
                // Botón para eliminar línea
                TextButton(
                    onClick = {
                        user?.let { u ->
                            viewModel.eliminarLineaPalet(
                                idLinea = lineaPalet.id,
                                usuarioId = u.id.toInt(),
                                paletId = palet.id
                            )
                        }
                    },
                    modifier = Modifier.size(24.dp),
                    contentPadding = PaddingValues(0.dp)
                ) {
                    Text(
                        text = "✕",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.error
                    )
                }
            }
        }
    } else {
        Text(
            text = "Palet vacío",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
    }
}

// DIÁLOGO DE ERROR CREAR MATERIA - Manejo específico para errores de stock
@Composable
fun DialogoErrorCrearMateria(
    mostrar: Boolean,
    detalles: String?,
    onDismiss: () -> Unit
) {
    if (mostrar && detalles != null) {
        AlertDialog(
            onDismissRequest = { /* No permitir cerrar con gestos */ },
            text = {
                Text(
                    text = detalles,
                    style = MaterialTheme.typography.bodyMedium,
                    lineHeight = 20.sp
                )
            },
            confirmButton = {
                TextButton(
                    onClick = onDismiss,
                    colors = ButtonDefaults.textButtonColors(
                        contentColor = MaterialTheme.colorScheme.error
                    )
                ) {
                    Text("Entendido")
                }
            }
        )
    }
}

// DIÁLOGO DE ADVERTENCIA DE CANTIDAD - Validación previa opcional
@Composable
fun DialogoAdvertenciaCantidad(
    mostrar: Boolean,
    mensaje: String?,
    onConfirmar: () -> Unit,
    onCancelar: () -> Unit
) {
    if (mostrar && mensaje != null) {
        AlertDialog(
            onDismissRequest = { /* No permitir cerrar con gestos */ },
            title = { 
                Text(
                    text = "⚠️ Advertencia de cantidad",
                    style = MaterialTheme.typography.headlineSmall,
                    color = MaterialTheme.colorScheme.tertiary
                )
            },
            text = {
                Text(
                    text = mensaje,
                    style = MaterialTheme.typography.bodyMedium
                )
            },
            confirmButton = {
                TextButton(
                    onClick = onConfirmar,
                    colors = ButtonDefaults.textButtonColors(
                        contentColor = MaterialTheme.colorScheme.primary
                    )
                ) {
                    Text("Continuar")
                }
            },
            dismissButton = {
                TextButton(
                    onClick = onCancelar,
                    colors = ButtonDefaults.textButtonColors(
                        contentColor = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                ) {
                    Text("Cancelar")
                }
            }
        )
    }
}

