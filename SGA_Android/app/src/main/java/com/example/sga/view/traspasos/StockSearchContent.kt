package com.example.sga.view.traspasos

import android.os.Build
import android.util.Log
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Search
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.TextFieldValue
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel
import com.example.sga.data.model.stock.Stock
import com.example.sga.service.scanner.QRScannerView
import com.example.sga.view.app.SessionViewModel
import com.example.sga.view.stock.StockLogic
import com.example.sga.view.stock.StockViewModel
import com.example.sga.utils.FormatUtils
import androidx.compose.foundation.focusable
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.input.key.onPreviewKeyEvent
import com.example.sga.service.lector.DeviceUtils
import android.view.KeyEvent
import androidx.compose.ui.layout.layout
import androidx.compose.ui.platform.LocalFocusManager
import com.example.sga.utils.SoundUtils
import kotlinx.coroutines.flow.*

@Composable
fun StockSearchContent(
    sessionViewModel: SessionViewModel,
    onDismiss: () -> Unit
) {
    val context = LocalContext.current
    val stockViewModel: StockViewModel = viewModel()
    val stockLogic = remember { StockLogic(stockViewModel, context) }
    
    val empresa = sessionViewModel.empresaSeleccionada.collectAsState().value
    val usuario by sessionViewModel.user.collectAsState()
    
    var codigoArticulo by remember { mutableStateOf(TextFieldValue("")) }
    var descripcionBusqueda by remember { mutableStateOf(TextFieldValue("")) }
    
    val resultado by stockViewModel.resultado.collectAsState()
    val cargando by stockViewModel.cargando.collectAsState()
    val error by stockViewModel.error.collectAsState()
    val articulosFiltrados by stockViewModel.articulosFiltrados.collectAsState()
    val mostrarDialogoSeleccion by stockViewModel.mostrarDialogoSeleccion.collectAsState()
    
    var escaneando by remember { mutableStateOf(false) }
    var escaneoProcesado by remember { mutableStateOf(false) }
    var skipNextDescripcionSearch by remember { mutableStateOf(false) }
    
    val empresaCodigo: Short? = empresa?.codigo?.toShort()
    val wedgeFocusRequester = remember { FocusRequester() }
    val focusManager = LocalFocusManager.current
    
    // Función de consulta reutilizada de StockScreen
    fun lanzarConsulta() {
        val empresaId = empresa?.codigo ?: return
        
        val onFinallyUI: () -> Unit = {
            codigoArticulo = TextFieldValue("")
            descripcionBusqueda = TextFieldValue("")
            if (DeviceUtils.hasHardwareScanner(context)) {
                wedgeFocusRequester.requestFocus()
            } else {
                focusManager.clearFocus(force = true)
            }
        }
        
        when {
            codigoArticulo.text.isNotBlank() -> {
                Log.d("STOCK_SEARCH", "🟢 Consultando por artículo")
                stockLogic.consultarStock(
                    codigoEmpresa = empresaId.toShort(),
                    codigoArticulo = codigoArticulo.text,
                    codigoUbicacion = null,
                    onFinally = onFinallyUI
                )
            }
            else -> {
                Log.d("STOCK_SEARCH", "🔴 No se cumple ninguna condición")
                stockViewModel.setError("Introduce un código de artículo.")
            }
        }
    }
    
    // Búsqueda por descripción con debounce
    LaunchedEffect(Unit) {
        snapshotFlow { descripcionBusqueda.text }
            .debounce(900)
            .filter { it.length >= 3 }
            .distinctUntilChanged()
            .collect { texto ->
                if (skipNextDescripcionSearch) {
                    skipNextDescripcionSearch = false
                    return@collect
                }
                
                stockLogic.buscarArticuloPorDescripcion(
                    codigoEmpresa = empresaCodigo ?: return@collect,
                    codigoAlmacen = null,
                    descripcion = texto,
                    onUnico = { codArticulo ->
                        codigoArticulo = TextFieldValue(codArticulo)
                        stockViewModel.setMostrarDialogoSeleccion(false)
                        lanzarConsulta()
                    },
                    onMultiple = { lista ->
                        stockViewModel.setArticulosFiltrados(lista)
                        stockViewModel.setMostrarDialogoSeleccion(true)
                    },
                    onError = {
                        stockViewModel.setError("❌ Error buscando por descripción")
                    }
                )
            }
    }
    
    // Campo fantasma para escáner hardware
    if (DeviceUtils.hasHardwareScanner(context)) {
        Box(
            modifier = Modifier
                .focusRequester(wedgeFocusRequester)
                .focusable()
                .onPreviewKeyEvent { event ->
                    if (event.nativeKeyEvent?.action == KeyEvent.ACTION_MULTIPLE) {
                        event.nativeKeyEvent.characters?.let { code ->
                            stockLogic.procesarCodigoEscaneado(
                                code = code.trim(),
                                almacenSel = null,
                                empresaId = empresa?.codigo?.toShort() ?: return@onPreviewKeyEvent true,
                                onCodigoArticuloDetectado = {
                                    codigoArticulo = it
                                    lanzarConsulta()
                                },
                                onUbicacionDetectada = { /* No necesario para búsqueda de artículos */ },
                                onPaletDetectado = { _ -> /* No aplica en búsqueda rápida */ },
                                onError = { 
                                    stockViewModel.setError(it)
                                    SoundUtils.getInstance().playErrorSound()
                                },
                                lanzarConsulta = { lanzarConsulta() },
                                onMultipleArticulos = { lista ->
                                    stockViewModel.setArticulosFiltrados(lista)
                                    stockViewModel.setMostrarDialogoSeleccion(true)
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
        
        LaunchedEffect(Unit) { wedgeFocusRequester.requestFocus() }
    }
    
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .heightIn(max = 600.dp)
            .verticalScroll(rememberScrollState()),
        verticalArrangement = Arrangement.spacedBy(16.dp)
    ) {
        // Título
        Text(
            "Consulta de Stock",
            style = MaterialTheme.typography.titleLarge
        )
        
        // Botón de escaneo para dispositivos sin hardware scanner
        if (!DeviceUtils.hasHardwareScanner(context)) {
            Button(
                onClick = {
                    escaneando = true
                    escaneoProcesado = false
                },
                modifier = Modifier.fillMaxWidth()
            ) {
                Text("Escanear QR")
            }
        }
        
        // Campos de búsqueda
        ArticuloSearchSection(
            codigoArticulo = codigoArticulo,
            onCodigoChange = { codigoArticulo = it },
            descripcion = descripcionBusqueda,
            onDescripcionChange = { descripcionBusqueda = it },
            onSearchDescripcion = {
                stockLogic.buscarArticuloPorDescripcion(
                    codigoEmpresa = empresaCodigo ?: return@ArticuloSearchSection,
                    codigoAlmacen = null,
                    descripcion = descripcionBusqueda.text,
                    onUnico = { codArticulo ->
                        codigoArticulo = TextFieldValue(codArticulo)
                        stockViewModel.setMostrarDialogoSeleccion(false)
                        lanzarConsulta()
                    },
                    onMultiple = { lista ->
                        stockViewModel.setArticulosFiltrados(lista)
                        stockViewModel.setMostrarDialogoSeleccion(true)
                    },
                    onError = { mensaje ->
                        stockViewModel.setError(mensaje)
                    }
                )
            }
        )
        
        // Botón consultar
        Button(
            onClick = { lanzarConsulta() },
            enabled = codigoArticulo.text.isNotBlank(),
            modifier = Modifier.fillMaxWidth()
        ) {
            Text("Consultar Stock")
        }
        
        // Mostrar error si existe
        if (error != null) {
            Text(
                "⚠️ $error",
                color = MaterialTheme.colorScheme.error,
                style = MaterialTheme.typography.bodyMedium
            )
        }
        
        // Mostrar loading
        if (cargando) {
            Box(
                modifier = Modifier.fillMaxWidth(),
                contentAlignment = Alignment.Center
            ) {
                CircularProgressIndicator()
            }
        }
        
        // Resultados de stock
        if (resultado.isNotEmpty()) {
            Text(
                "Resultados encontrados:",
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.Bold
            )
            
            LazyColumn(
                modifier = Modifier.heightIn(max = 300.dp),
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                items(resultado) { stock ->
                    StockResultCard(stock = stock)
                }
            }
        }
    }
    
    // Diálogo de selección de artículos múltiples
    if (mostrarDialogoSeleccion) {
        AlertDialog(
            onDismissRequest = { stockViewModel.setMostrarDialogoSeleccion(false) },
            title = { Text("Selecciona un artículo") },
            text = {
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .heightIn(max = 400.dp)
                        .verticalScroll(rememberScrollState())
                ) {
                    articulosFiltrados.forEach { articulo ->
                        Card(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(vertical = 4.dp)
                                .clickable {
                                    // Seleccionar el artículo y hacer la consulta
                                    skipNextDescripcionSearch = true
                                    codigoArticulo = TextFieldValue(articulo.codigoArticulo)
                                    descripcionBusqueda = TextFieldValue(articulo.descripcion ?: "")
                                    stockViewModel.setMostrarDialogoSeleccion(false)
                                    lanzarConsulta()
                                },
                            elevation = CardDefaults.cardElevation(4.dp)
                        ) {
                            Column(Modifier.padding(12.dp)) {
                                Text(
                                    text = "📦 ${articulo.codigoArticulo}",
                                    style = MaterialTheme.typography.bodyLarge,
                                    fontWeight = FontWeight.Bold
                                )
                                Spacer(Modifier.height(4.dp))
                                Text(
                                    text = articulo.descripcion ?: "Sin descripción",
                                    style = MaterialTheme.typography.bodyMedium
                                )
                            }
                        }
                    }
                }
            },
            confirmButton = {},
            dismissButton = {
                TextButton(onClick = { stockViewModel.setMostrarDialogoSeleccion(false) }) {
                    Text("Cancelar")
                }
            }
        )
    }
    
    // Escáner QR para dispositivos sin hardware scanner
    if (escaneando && !DeviceUtils.hasHardwareScanner(context)) {
        Box(
            modifier = Modifier
                .fillMaxSize()
                .background(Color.Black.copy(alpha = 0.6f)),
            contentAlignment = Alignment.Center
        ) {
            Column(horizontalAlignment = Alignment.CenterHorizontally) {
                Text(
                    text = "Escaneando código…",
                    style = MaterialTheme.typography.titleMedium
                )
                
                Spacer(Modifier.height(12.dp))
                
                QRScannerView(
                    modifier = Modifier
                        .fillMaxWidth(0.5f)
                        .aspectRatio(1f),
                    onCodeScanned = { code ->
                        if (escaneoProcesado) return@QRScannerView
                        escaneoProcesado = true
                        escaneando = false
                        
                        stockLogic.procesarCodigoEscaneado(
                            code = code,
                            almacenSel = null,
                            empresaId = empresa?.codigo?.toShort() ?: return@QRScannerView,
                            onCodigoArticuloDetectado = {
                                codigoArticulo = it
                                lanzarConsulta()
                            },
                            onUbicacionDetectada = { /* No necesario */ },
                            onPaletDetectado = { _ -> /* No aplica en búsqueda rápida */ },
                            onError = { 
                                stockViewModel.setError(it)
                                SoundUtils.getInstance().playErrorSound()
                            },
                            lanzarConsulta = { lanzarConsulta() },
                            onMultipleArticulos = { lista ->
                                stockViewModel.setArticulosFiltrados(lista)
                                stockViewModel.setMostrarDialogoSeleccion(true)
                            }
                        )
                    }
                )
                
                Spacer(Modifier.height(12.dp))
                
                Button(onClick = { escaneando = false }) {
                    Text("Cancelar escaneo")
                }
            }
        }
    }
}

@Composable
fun ArticuloSearchSection(
    codigoArticulo: TextFieldValue,
    onCodigoChange: (TextFieldValue) -> Unit,
    descripcion: TextFieldValue,
    onDescripcionChange: (TextFieldValue) -> Unit,
    onSearchDescripcion: () -> Unit,
    modifier: Modifier = Modifier
) {
    Row(
        modifier = modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        OutlinedTextField(
            value = codigoArticulo,
            onValueChange = onCodigoChange,
            label = { Text("Código") },
            modifier = Modifier.weight(1f),
            singleLine = true
        )
        
        OutlinedTextField(
            value = descripcion,
            onValueChange = onDescripcionChange,
            label = { Text("Buscar descripción") },
            modifier = Modifier.weight(2f),
            singleLine = true,
            trailingIcon = {
                IconButton(onClick = onSearchDescripcion) {
                    Icon(Icons.Default.Search, contentDescription = "Buscar")
                }
            },
            keyboardOptions = KeyboardOptions.Default.copy(imeAction = ImeAction.Search),
            keyboardActions = KeyboardActions(
                onSearch = { onSearchDescripcion() }
            )
        )
    }
}

@Composable
fun StockResultCard(
    stock: Stock,
    modifier: Modifier = Modifier
) {
    val fechaCorta = stock.fechaCaducidad?.take(10) ?: "Sin fecha"
    val saldoPositivo = stock.disponible > 0
    val colorSaldo = if (saldoPositivo)
        MaterialTheme.colorScheme.onSurface
    else
        MaterialTheme.colorScheme.error
    
    Card(
        modifier = modifier
            .fillMaxWidth()
            .padding(vertical = 4.dp),
        elevation = CardDefaults.cardElevation(2.dp)
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(12.dp)
        ) {
            Text(
                text = "📦 ${stock.codigoArticulo} — ${stock.descripcionArticulo}",
                style = MaterialTheme.typography.titleMedium
            )
            Spacer(Modifier.height(8.dp))
            Text("🏬 Almacén: ${stock.codigoAlmacen} - ${stock.almacen}")
            Text("📍 Ubicación: ${stock.ubicacion}")
            Text("📋 Partida: ${stock.partida}")
            Text("🗓 Caducidad: $fechaCorta")
            
            Spacer(Modifier.height(8.dp))
            
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                Text(
                    text = "📦 Disponible: ${FormatUtils.formatearCantidad(stock.disponible)}",
                    color = colorSaldo,
                    style = MaterialTheme.typography.bodyLarge,
                    fontWeight = FontWeight.Bold
                )
                
                if (stock.reservado > 0) {
                    Text("🔒 Reservado: ${FormatUtils.formatearCantidad(stock.reservado)}")
                }
            }
        }
    }
}
