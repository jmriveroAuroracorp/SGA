@file:OptIn(ExperimentalMaterial3Api::class)
package com.example.sga.view.stock

import android.os.Build
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import android.util.Log
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Print
import androidx.compose.material.icons.filled.Search
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.input.TextFieldValue
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.navigation.NavHostController
import com.example.sga.data.model.stock.Stock
import com.example.sga.service.scanner.QRScannerView
import com.example.sga.view.app.SessionViewModel
import com.example.sga.view.components.AppTopBar
import com.example.sga.view.components.LoadingDialog
import androidx.compose.ui.platform.LocalSoftwareKeyboardController
import com.example.sga.view.Almacen.AlmacenLogic
import com.example.sga.view.Almacen.AlmacenViewModel
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.zIndex
import kotlinx.coroutines.flow.*
import android.widget.Toast
import androidx.compose.foundation.focusable
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.input.key.onPreviewKeyEvent
import com.example.sga.service.lector.DeviceUtils
import android.view.KeyEvent
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.ArrowDropDown
import androidx.compose.material.icons.filled.Remove
import androidx.compose.ui.layout.layout
import androidx.compose.ui.platform.LocalFocusManager
import com.example.sga.utils.SoundUtils
import com.example.sga.utils.FormatUtils

@Composable
fun StockCard(
    stock: Stock,
    onPrintClick: (Stock) -> Unit,          // ← callback para imprimir artículo
    onPrintPaletClick: (Stock) -> Unit,     // ← callback para imprimir palet
    sessionViewModel: SessionViewModel,      // ← agregado para verificar permisos
    modifier: Modifier = Modifier
) {
    val fechaCorta     = stock.fechaCaducidad?.take(10) ?: "Sin fecha"
    val saldoPositivo  = stock.disponible > 0
    val colorSaldo     = if (saldoPositivo)
        MaterialTheme.colorScheme.onSurface
    else
        MaterialTheme.colorScheme.error
    val estiloSaldo    = if (saldoPositivo)
        MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.Bold)
    else
        MaterialTheme.typography.bodyLarge

    // Colores para el tipo de stock
    val colorTipoStock = when (stock.tipoStock) {
        "Suelto" -> Color(0xFF4CAF50) // Verde
        "Paletizado" -> Color(0xFF2196F3) // Azul
        else -> MaterialTheme.colorScheme.onSurface
    }

    Card(
        modifier = modifier
            .fillMaxWidth()
            .padding(vertical = 4.dp),
        elevation = CardDefaults.cardElevation(defaultElevation = 4.dp)
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Column(Modifier.weight(1f)) {
                    // 🔷 NUEVO: Badge de bloqueo de calidad
                    if (stock.isBloqueadoCalidad) {
                        Card(
                            colors = CardDefaults.cardColors(
                                containerColor = Color(0xFFFF6B6B)
                            ),
                            modifier = Modifier
                                .padding(bottom = 8.dp)
                                .fillMaxWidth()
                        ) {
                            Row(
                                modifier = Modifier.padding(horizontal = 8.dp, vertical = 4.dp),
                                verticalAlignment = Alignment.CenterVertically,
                                horizontalArrangement = Arrangement.Start
                            ) {
                                Text(
                                    text = "🔒",
                                    style = MaterialTheme.typography.bodySmall
                                )
                                Spacer(Modifier.width(4.dp))
                                Text(
                                    text = "BLOQUEADO CALIDAD - ${stock.tipoBloqueoCalidad}",
                                    color = Color.White,
                                    style = MaterialTheme.typography.bodySmall.copy(
                                        fontWeight = FontWeight.Bold
                                    )
                                )
                            }
                        }
                    }
                    
                    Text(
                        text  = "📦 ${stock.codigoArticulo} — ${stock.descripcionArticulo}",
                        style = MaterialTheme.typography.titleMedium
                    )
                    Spacer(Modifier.height(8.dp))
                    Text("🏬 Almacén: ${stock.codigoAlmacen} - ${stock.almacen}")
                    Text("📍 Ubicación: ${stock.ubicacion}")
                    Text("📋 Partida: ${stock.partida}")
                    Text("🗓 Caducidad: $fechaCorta")
                    
                    // 🔷 NUEVO: Mostrar motivo de bloqueo si existe
                    if (stock.isBloqueadoCalidad && !stock.motivoBloqueoCalidad.isNullOrBlank()) {
                        Spacer(Modifier.height(4.dp))
                        Text(
                            text = "⚠️ Motivo: ${stock.motivoBloqueoCalidad}",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.error
                        )
                    }
                }

                // Iconos de impresión si el usuario tiene permiso 11
                if (sessionViewModel.tienePermiso(11)) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        // Botón para imprimir etiqueta de artículo
                        IconButton(onClick = { onPrintClick(stock) }) {
                            Icon(Icons.Default.Print, contentDescription = "Imprimir etiqueta de artículo")
                        }
                        
                        // Botón adicional para imprimir etiqueta de palet (solo si está paletizado)
                        if (stock.tipoStock == "Paletizado" && stock.paletId != null) {
                            IconButton(onClick = { onPrintPaletClick(stock) }) {
                                Icon(
                                    Icons.Default.Print,
                                    contentDescription = "Imprimir etiqueta de palet",
                                    tint = Color(0xFF2196F3) // Azul para diferenciar del botón de artículo
                                )
                            }
                        }
                    }
                }
            }
            
            Spacer(Modifier.height(8.dp))
            
            // Información de stock
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                Column {
                    Text(
                        text = "📦 Disponible: ${FormatUtils.formatearCantidad(stock.disponible)}",
                        color = colorSaldo,
                        style = estiloSaldo
                    )
                    if (stock.reservado > 0) {
                        Text("🔒 Reservado: ${FormatUtils.formatearCantidad(stock.reservado)}")
                    }
                }
                
                // Badge del tipo de stock
                Card(
                    colors = CardDefaults.cardColors(
                        containerColor = colorTipoStock.copy(alpha = 0.1f)
                    ),
                    modifier = Modifier.padding(4.dp)
                ) {
                    Text(
                        text = when (stock.tipoStock) {
                            "Suelto" -> "Suelto"
                            "Paletizado" -> "Paletizado"
                            else -> stock.tipoStock
                        },
                        color = colorTipoStock,
                        style = MaterialTheme.typography.bodySmall.copy(fontWeight = FontWeight.Bold),
                        modifier = Modifier.padding(horizontal = 8.dp, vertical = 4.dp)
                    )
                }
            }
            
            // Información del palet (solo si es paletizado)
            if (stock.tipoStock == "Paletizado" && stock.codigoPalet != null) {
                Spacer(Modifier.height(8.dp))
                Card(
                    colors = CardDefaults.cardColors(
                        containerColor = Color(0xFF2196F3).copy(alpha = 0.1f)
                    ),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Column(modifier = Modifier.padding(8.dp)) {
                        Text(
                            text = "🏗️ Información del Palet",
                            style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.Bold),
                            color = Color(0xFF2196F3)
                        )
                        Spacer(Modifier.height(4.dp))
                        Text("📋 Código: ${stock.codigoPalet}")
                        Text("📊 Estado: ${stock.estadoPalet}")
                        if (stock.ordenTrabajoId != null) {
                            Text("📋 Orden de Trabajo: ${stock.ordenTrabajoId}")
                        }
                    }
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
    Row(modifier = modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
        OutlinedTextField(
            value = codigoArticulo,
            onValueChange = onCodigoChange,
            label = { Text("Código") },
            modifier = Modifier.weight(1f),
            singleLine = true
        )

        OutlinedTextField(
            value = descripcion,
            onValueChange = {
                onDescripcionChange(it)
            },
            label = { Text("Buscar descripción") },
            modifier = Modifier
                .weight(2f),
            singleLine = true,
            trailingIcon = {
                IconButton(onClick = onSearchDescripcion) {
                    Icon(Icons.Default.Search, contentDescription = "Buscar")
                }
            },
            keyboardOptions = KeyboardOptions.Default.copy(imeAction = ImeAction.Search),
            keyboardActions = KeyboardActions(
                onSearch = {
                    onSearchDescripcion()
                }
            )
        )
    }
}
@Composable
fun StockScreen(
    navController: NavHostController,
    sessionViewModel: SessionViewModel,
    stockViewModel: StockViewModel = viewModel()
) {
    val context = LocalContext.current
    val stockLogic = remember { StockLogic(stockViewModel, context) }
    val almacenVM: AlmacenViewModel = viewModel()
    val almacenLogic = remember { AlmacenLogic(almacenVM, sessionViewModel) }
    val impresionLogic = remember {
        ImpresionLogic { msg ->
            Toast.makeText(context, msg, Toast.LENGTH_SHORT).show()
        }
    }
    LaunchedEffect(Unit) {
        almacenLogic.cargarAlmacenes()
        impresionLogic.cargarImpresoras()
        // 🔊 Inicializar sonidos del sistema
        SoundUtils.getInstance().initialize(context)
    }

    val listaAlmacenes by almacenVM.lista.collectAsState()
    val almacenSel by almacenVM.seleccionado.collectAsState()

    val resultado by stockViewModel.resultado.collectAsState()

    val cargando by stockViewModel.cargando.collectAsState()
    val error by stockViewModel.error.collectAsState()
    val empresa = sessionViewModel.empresaSeleccionada.collectAsState().value

    var codigoArticulo by remember { mutableStateOf(TextFieldValue("")) }
    var codigoUbicacion by remember { mutableStateOf(TextFieldValue("")) }

    val keyboardController = LocalSoftwareKeyboardController.current

    val opcionesVista = listOf("Almacén", "Partida", "Artículo")
    var vistaSeleccionadaIndex by remember { mutableStateOf(0) }
    val vistaSeleccionada = opcionesVista[vistaSeleccionadaIndex]


    var escaneando by remember { mutableStateOf(false) }
    var escaneoProcesado by remember { mutableStateOf(false) }
    LaunchedEffect(Unit) {
        if (DeviceUtils.hasHardwareScanner(context)) escaneando = true
    }
    val usuario by sessionViewModel.user.collectAsState()
    var descripcionBusqueda by remember { mutableStateOf(TextFieldValue("")) }
    LaunchedEffect(resultado) {
        if (resultado.isNotEmpty()) {
            codigoArticulo = TextFieldValue("")
            descripcionBusqueda = TextFieldValue("")
        }
    }
    val articulosFiltrados by stockViewModel.articulosFiltrados.collectAsState()
    val mostrarDialogoSeleccion by stockViewModel.mostrarDialogoSeleccion.collectAsState()
    var skipNextDescripcionSearch by remember { mutableStateOf(false) }
    val empresaCodigo: Short? = empresa?.codigo?.toShort()          // ← nuevo
    val almacenFiltro: String? = almacenSel?.takeIf { it != "Todos" } // ← nuevo
    val wedgeFocusRequester = remember { FocusRequester() }

    val focusManager = LocalFocusManager.current

/*fun lanzarConsulta() {
    val empresaId = empresa?.codigo ?: return

    // Normalizar ubicación/almacén si viene en formato "213$UB..."
    val rawUbi = codigoUbicacion.text.trim()
    var ubi = rawUbi
    var alm: String? = almacenSel?.takeIf { it.isNotBlank() && it != "Todos" }

    if (rawUbi.contains('$')) {
        val parts = rawUbi.split('$', limit = 2)
        if (parts.size == 2) {
            val prefijo = parts[0]
            val cuerpo  = parts[1]
            if (prefijo.isNotBlank()) {
                alm = prefijo        // almacén extraído del escaneo
                ubi = cuerpo         // ubicación limpia
            } else {
                // caso "$UB..." -> solo nos quedamos con lo de después
                ubi = cuerpo
            }
        }
    }

    Log.d("CONSULTA_STOCK", "🔍 Articulo=${codigoArticulo.text}  Ubicacion=$ubi  Almacen=$alm")
    val onFinallyUI: () -> Unit = {
        // limpia el campo artículo SIEMPRE
        codigoArticulo = TextFieldValue("")
        codigoUbicacion = TextFieldValue("")
        almacenLogic.onAlmacenSeleccionado("Todos")
        if (DeviceUtils.hasHardwareScanner(context)) {
            // vuelve a dar foco al "campo fantasma" para captar el siguiente escaneo
            wedgeFocusRequester.requestFocus()
        } else {
            // en tablets/móviles sí liberamos foco
            focusManager.clearFocus(force = true)
        }
    }

    when {
        codigoArticulo.text.isNotBlank() -> {
            Log.d("CONSULTA_STOCK", "🟢 Consultando por artículo")
            stockLogic.consultarStock(
                codigoEmpresa   = empresaId.toShort(),
                codigoArticulo  = codigoArticulo.text,
                codigoUbicacion = null,
                onFinally       = onFinallyUI
            )
        }
        ubi.isNotBlank() && alm != null -> {
            Log.d("CONSULTA_STOCK", "🟡 Consultando por ubicación y almacén")
            stockLogic.consultarStock(
                codigoEmpresa   = empresaId.toShort(),
                codigoArticulo  = null,
                codigoUbicacion = ubi,
                codigoAlmacen   = alm,
                onFinally       = onFinallyUI
            )
        }
        else -> {
            Log.d("CONSULTA_STOCK", "🔴 No se cumple ninguna condición")
            stockViewModel.setError("Introduce un código de artículo o una ubicación con almacén válido.")
        }
    }
}*/
fun lanzarConsulta() {
    val empresaId = empresa?.codigo ?: return

    // Normalizar ubicación/almacén: "ALM$UB...", "ALM$", "$UB..."
    val rawUbi = codigoUbicacion.text.trim()
    var ubi = rawUbi
    var alm: String? = almacenSel?.takeIf { it.isNotBlank() && it != "Todos" }
    var esSinUbicar = false // ← ALM$ (ubi = "")

    if (rawUbi.contains('$')) {
        val parts = rawUbi.split('$', limit = 2)
        if (parts.size == 2) {
            val prefijo = parts[0].trim()
            val cuerpo  = parts[1].trim()

            if (prefijo.isNotEmpty()) {
                // "ALM$..." (incluye "ALM$" => sin ubicar)
                alm = prefijo
                ubi = cuerpo            // "" si es ALM$
                esSinUbicar = cuerpo.isEmpty()
            } else {
                // "$UB..." -> solo nos quedamos con lo de después
                ubi = cuerpo
            }
        }
    }

    Log.d("CONSULTA_STOCK", "🔍 Articulo=${codigoArticulo.text}  Ubicacion=$ubi  Almacen=$alm  SinUbicar=$esSinUbicar")

    val onFinallyUI: () -> Unit = {
        // limpia el campo artículo SIEMPRE
        codigoArticulo = TextFieldValue("")
        codigoUbicacion = TextFieldValue("")
        almacenLogic.onAlmacenSeleccionado("Todos")
        if (DeviceUtils.hasHardwareScanner(context)) {
            // vuelve a dar foco al "campo fantasma" para captar el siguiente escaneo
            wedgeFocusRequester.requestFocus()
        } else {
            // en tablets/móviles sí liberamos foco
            focusManager.clearFocus(force = true)
        }
    }

    when {
        // 1) Consulta por artículo (código directo o EAN ya resuelto a código)
        codigoArticulo.text.isNotBlank() -> {
            Log.d("CONSULTA_STOCK", "🟢 Consultando por artículo")
            stockLogic.consultarStock(
                codigoEmpresa   = empresaId.toShort(),
                codigoArticulo  = codigoArticulo.text,
                codigoUbicacion = null,
                onFinally       = onFinallyUI
            )
        }

        // 2) Consulta por almacén + ubicación
        //    - ubi normal (no vacía), o
        //    - ALM$ => esSinUbicar = true (ubi vacía permitida)
        alm != null && (ubi.isNotBlank() || esSinUbicar) -> {
            Log.d("CONSULTA_STOCK", "🟡 Consultando por ubicación y almacén (sinUbicar=$esSinUbicar)")
            stockLogic.consultarStock(
                codigoEmpresa   = empresaId.toShort(),
                codigoArticulo  = null,
                codigoUbicacion = ubi,   // "" si es ALM$ (sin ubicar)
                codigoAlmacen   = alm,
                onFinally       = onFinallyUI
            )
        }

        // 3) Nada válido
        else -> {
            Log.d("CONSULTA_STOCK", "🔴 No se cumple ninguna condición")
            stockViewModel.setError("Introduce un código de artículo o una ubicación con almacén válido.")
        }
    }
}

    // ✅ OPTIMIZADO: Debounce mejorado para reducir llamadas de red
    LaunchedEffect(Unit) {
        snapshotFlow { descripcionBusqueda.text }
            .debounce(1200) // ✅ Aumentado de 900ms a 1200ms para menos llamadas
            .filter { it.length >= 4 } // ✅ Aumentado de 3 a 4 caracteres para menos llamadas
            .distinctUntilChanged()
            .collect { texto ->
                if (skipNextDescripcionSearch) {
                    skipNextDescripcionSearch = false
                    return@collect
                }

                stockLogic.buscarArticuloPorDescripcion(
                    codigoEmpresa = empresaCodigo ?: return@collect,
                    codigoAlmacen = almacenFiltro,
                    descripcion = texto,
                    onUnico = { codArticulo ->
                        codigoUbicacion = TextFieldValue("")
                        almacenLogic.onAlmacenSeleccionado("Todos")
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

    val puedeConsultar = remember(codigoArticulo.text, codigoUbicacion.text, almacenSel) {
        codigoArticulo.text.isNotBlank() ||
                (codigoUbicacion.text.isNotBlank() && almacenSel != null && almacenSel != "Todos")
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
            Modifier
                .fillMaxSize()
        ) {


            /* ── 1. Campo fantasma SOLO en PDA Honeywell ─────────────── */
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
                                        almacenSel = almacenSel,
                                        empresaId  = empresa?.codigo?.toShort()
                                            ?: return@onPreviewKeyEvent true,
                                        //onCodigoArticuloDetectado = { codigoArticulo = it },
                                        onCodigoArticuloDetectado = {
                                            codigoUbicacion = TextFieldValue("")
                                            almacenLogic.onAlmacenSeleccionado("Todos")
                                            codigoArticulo = it
                                            lanzarConsulta()
                                        },
                                        onPaletDetectado = { palet ->
                                            stockLogic.consultarStockPorPalet(
                                                palet = palet,
                                                codigoEmpresa = empresa?.codigo?.toShort() ?: return@procesarCodigoEscaneado,
                                                onSuccess = { stockDelPalet ->
                                                    Log.d("PALET_STOCK", "✅ Stock del palet ${palet.codigoPalet} cargado: ${stockDelPalet.size} artículos")
                                                },
                                                onError = { error ->
                                                    stockViewModel.setError("Error al consultar stock del palet: $error")
                                                }
                                            )
                                        },

                                        onUbicacionDetectada = { tfv ->
                                            val s = tfv.text.trim()
                                            if (s.contains('$')) {
                                                val parts = s.split('$', limit = 2)
                                                if (parts.size == 2 && parts[0].isNotBlank()) {
                                                    // "ALM$UB..."  o  "ALM$"
                                                    val almCode = parts[0]
                                                    val cuerpo  = parts[1]
                                                    // selecciona visualmente el almacén si quieres
                                                    // almacenLogic.onAlmacenSeleccionado(almCode)

                                                    // ⬅️ clave: si es "ALM$" dejamos "ALM$" en el TextField
                                                    codigoUbicacion = if (cuerpo.isBlank())
                                                        TextFieldValue("$almCode$")
                                                    else
                                                        TextFieldValue(s) // deja "ALM$UB..."
                                                } else {
                                                    // "$UB..." -> deja "UB..." (sin "$")
                                                    codigoUbicacion = TextFieldValue(parts.getOrNull(1) ?: s.removePrefix("$"))
                                                }
                                            } else {
                                                codigoUbicacion = tfv
                                            }
                                        },
                                        onMultipleArticulos       = { lista ->
                                            stockViewModel.setArticulosFiltrados(lista)
                                            stockViewModel.setMostrarDialogoSeleccion(true)
                                        },
                                        onError         = { 
                                            stockViewModel.setError(it)
                                            SoundUtils.getInstance().playErrorSound()
                                        },
                                        lanzarConsulta  = { lanzarConsulta() }
                                    )
                                }
                                true
                            } else false
                        }
                        // 👇 Esto elimina completamente su presencia en layout
                        .layout { measurable, constraints ->
                            val placeable = measurable.measure(constraints)
                            layout(0, 0) { placeable.place(0, 0) }
                        }
                )

                LaunchedEffect(Unit) { wedgeFocusRequester.requestFocus() }
            }

            /* ───────────────────────────────────────────────────────── */

            LazyColumn(
                modifier = Modifier
                    .padding(padding)
                    .fillMaxSize(),
                contentPadding = PaddingValues(16.dp)
            ) {

                /** --------- ENCABEZADO Y FORMULARIO --------- **/
                item {
                    Text("Consulta de Stock", style = MaterialTheme.typography.titleLarge)
                    
                    Spacer(Modifier.height(16.dp))

                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.Center
                    ) {
                        Column(modifier = Modifier.fillMaxWidth()) {
                            if (!DeviceUtils.hasHardwareScanner(context)) {
                                Button(
                                    onClick = {
                                        Log.d(
                                            "ESCANEO",
                                            "▶️ escaneando = true  —  MANUFACTURER=${Build.MANUFACTURER}"
                                        )
                                        escaneando = true
                                        escaneoProcesado = false
                                    },
                                    enabled = true,
                                    modifier = Modifier.fillMaxWidth()
                                ) {
                                    Text("Escanear QR")
                                }
                            }
                           
                        }
                    }

                    ArticuloSearchSection(
                        codigoArticulo = codigoArticulo,
                        onCodigoChange = { codigoArticulo = it },
                        descripcion = descripcionBusqueda,
                        onDescripcionChange = { descripcionBusqueda = it },
                        onSearchDescripcion = {
                            stockLogic.buscarArticuloPorDescripcion(
                                codigoEmpresa = empresaCodigo ?: return@ArticuloSearchSection,
                                codigoAlmacen = almacenFiltro,
                                descripcion = descripcionBusqueda.text,
                                onUnico = { codArticulo ->
                                    codigoUbicacion = TextFieldValue("")
                                    almacenLogic.onAlmacenSeleccionado("Todos")
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
                    Spacer(Modifier.height(16.dp))


                    /* Text("Almacén", style = MaterialTheme.typography.titleMedium)
                    Spacer(modifier = Modifier.height(8.dp))

                    var dropOpen by remember { mutableStateOf(false) }

                    ExposedDropdownMenuBox(
                        expanded = dropOpen,
                        onExpandedChange = { dropOpen = !dropOpen }
                    ) {
                        val textoAlmacenSel = when (almacenSel) {
                            null, "Todos" -> "Selecciona un almacén"
                            else -> {
                                val encontrado =
                                    listaAlmacenes.find { it.codigoAlmacen == almacenSel }
                                encontrado?.let { "${it.codigoAlmacen} - ${it.nombreAlmacen}" }
                                    ?: almacenSel
                            }
                        }

                        OutlinedTextField(
                            readOnly = true,
                            value = textoAlmacenSel ?: "",
                            onValueChange = {},
                            label = { Text("Almacén") },
                            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = dropOpen) },
                            modifier = Modifier
                                .fillMaxWidth()
                                .menuAnchor()
                        )

                        ExposedDropdownMenu(
                            expanded = dropOpen,
                            onDismissRequest = { dropOpen = false }
                        ) {
                            val listaConTodos = listOf(null) + listaAlmacenes

                            listaConTodos.forEach { almacen ->
                                val texto =
                                    almacen?.let { "${it.codigoAlmacen} - ${it.nombreAlmacen}" }
                                        ?: "Todos"

                                DropdownMenuItem(
                                    text = { Text(texto) },
                                    onClick = {
                                        almacenLogic.onAlmacenSeleccionado(
                                            almacen?.codigoAlmacen ?: "Todos"
                                        )
                                        dropOpen = false
                                    }
                                )
                            }
                        }
                    }

                    Spacer(Modifier.height(8.dp)) */


                    Button(
                        onClick = { lanzarConsulta() },
                        enabled = puedeConsultar,
                        modifier = Modifier.fillMaxWidth()
                    ) { Text("Consultar Stock") }

                    Spacer(Modifier.height(12.dp))

                    // Solo mostrar el selector de vista cuando hay resultados
                    if (resultado.isNotEmpty()) {
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(vertical = 8.dp),
                            horizontalArrangement = Arrangement.spacedBy(8.dp)
                        ) {
                            opcionesVista.forEachIndexed { index, texto ->
                                Box(
                                    modifier = Modifier
                                        .clip(MaterialTheme.shapes.large)
                                        .background(
                                            color = if (vistaSeleccionadaIndex == index) 
                                                MaterialTheme.colorScheme.primary.copy(alpha = 0.1f)
                                            else 
                                                Color.Transparent
                                        )
                                        .border(
                                            width = 1.dp,
                                            color = if (vistaSeleccionadaIndex == index) 
                                                MaterialTheme.colorScheme.primary
                                            else 
                                                MaterialTheme.colorScheme.outline.copy(alpha = 0.3f),
                                            shape = MaterialTheme.shapes.large
                                        )
                                        .clickable { vistaSeleccionadaIndex = index }
                                        .padding(horizontal = 12.dp, vertical = 8.dp),
                                    contentAlignment = Alignment.Center
                                ) {
                                    Text(
                                        text = texto,
                                        color = if (vistaSeleccionadaIndex == index) 
                                            MaterialTheme.colorScheme.primary
                                        else 
                                            MaterialTheme.colorScheme.onSurface,
                                        style = MaterialTheme.typography.bodyMedium.copy(
                                            fontWeight = FontWeight.Normal
                                        ),
                                        maxLines = 1,
                                        overflow = TextOverflow.Ellipsis
                                    )
                                }
                            }
                        }

                        Spacer(Modifier.height(12.dp))
                    }

                    if (error != null) {
                        Text("⚠️ $error", color = MaterialTheme.colorScheme.error)
                        Spacer(Modifier.height(16.dp))
                    }
                }

                /** --------- INFORMACIÓN DEL PALET --------- **/
                // Mostrar información del palet solo si TODOS los resultados son del mismo palet
                // (esto indica que se escaneó un palet específico, no una ubicación)
                resultado.firstOrNull()?.let { primerStock ->
                    if (primerStock.tipoStock == "Paletizado" && 
                        primerStock.paletId != null &&
                        resultado.all { it.paletId == primerStock.paletId && it.codigoPalet == primerStock.codigoPalet }) {
                        item {
                            Card(
                                colors = CardDefaults.cardColors(
                                    containerColor = Color(0xFF2196F3).copy(alpha = 0.1f)
                                ),
                                modifier = Modifier.fillMaxWidth()
                            ) {
                                Column(modifier = Modifier.padding(12.dp)) {
                                    Text(
                                        text = "🏗️ Stock del Palet: ${primerStock.codigoPalet}",
                                        style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                                        color = Color(0xFF2196F3)
                                    )
                                    Text("📊 Estado: ${primerStock.estadoPalet}")
                                    if (primerStock.ordenTrabajoId != null) {
                                        Text("📋 Orden de Trabajo: ${primerStock.ordenTrabajoId}")
                                    }
                                    if (primerStock.codigoAlmacen.isNotEmpty()) {
                                        Text("🏬 Almacén: ${primerStock.codigoAlmacen}")
                                    }
                                    if (primerStock.ubicacion.isNotEmpty()) {
                                        Text("📍 Ubicación: ${primerStock.ubicacion}")
                                    }
                                    Text("📦 Artículos en el palet: ${resultado.size}")
                                }
                            }
                            Spacer(Modifier.height(16.dp))
                        }
                    }
                }

                /** --------- FILTRO DE RESULTADOS --------- **/
                val filtrado = resultado.filter { item ->
                    // 1️⃣ Filtro por permisos específicos del usuario
                    val porPermisoEspecifico = usuario?.codigosAlmacen?.contains(item.codigoAlmacen) == true
                    
                    // 2️⃣ Filtro por almacenes del centro del usuario
                    val porCentro = listaAlmacenes.any { almacen -> 
                        almacen.codigoAlmacen == item.codigoAlmacen && almacen.esDelCentro 
                    }
                    
                    // 3️⃣ Filtro por almacén seleccionado en UI
                    val porAlmacen =
                        almacenSel == null || almacenSel == "Todos" || item.codigoAlmacen == almacenSel

                    // 🔍 LOGS DE DEBUG
                    Log.d("STOCK_FILTER", """
                        📦 Almacén: ${item.codigoAlmacen}
                        👤 Usuario codigosAlmacen: ${usuario?.codigosAlmacen}
                        🏢 Usuario codigoCentro: ${usuario?.codigoCentro}
                        ✅ porPermisoEspecifico: $porPermisoEspecifico
                        🏭 porCentro: $porCentro
                        🎯 porAlmacen: $porAlmacen
                        📋 Lista almacenes disponibles: ${listaAlmacenes.map { "${it.codigoAlmacen}(esDelCentro=${it.esDelCentro})" }}
                        ⚖️ Resultado final: ${(porPermisoEspecifico || porCentro) && porAlmacen}
                        ═══════════════════════════════
                    """.trimIndent())

                    // Combinar permisos: específicos OR del centro
                    (porPermisoEspecifico || porCentro) && porAlmacen
                }

                val agrupado = when (vistaSeleccionada) {
                    "Almacén" -> filtrado.groupBy { it.almacen }
                    "Partida" -> filtrado.groupBy { it.partida }
                    "Artículo" -> filtrado.groupBy { it.codigoArticulo }
                    "Tipo Stock" -> filtrado.groupBy { it.tipoStock }
                    else -> filtrado.groupBy { "Sin clasificar" }
                }

                /** --------- RESULTADOS AGRUPADOS Y ORDENADOS --------- **/
                agrupado.forEach { (grupo, itemsSinOrdenar) ->
                    // Ordenar por fecha de caducidad (más próxima primero)
                    val items = itemsSinOrdenar.sortedBy { stock ->
                        stock.fechaCaducidad?.takeIf { it.isNotEmpty() } ?: "9999-12-31"
                    }
                    item {
                        Text(
                            text = when (vistaSeleccionada) {
                                "Almacén" -> "📦 Almacén: $grupo"
                                "Partida" -> "📋 Partida: $grupo"
                                "Artículo" -> "🔢 Artículo: $grupo"
                                "Tipo Stock" -> when (grupo) {
                                    "Suelto" -> "📦 Stock Suelto"
                                    "Paletizado" -> "🏗️ Stock Paletizado"
                                    else -> "📊 Tipo: $grupo"
                                }
                                else -> grupo
                            },
                            style = MaterialTheme.typography.titleMedium,
                            modifier = Modifier.padding(vertical = 8.dp)
                        )
                    }

                    /*items(items) { stock ->
                        StockCard(
                            stock = stock,
                            onPrintClick = { sel ->
                                val empresaId = empresaCodigo ?: return@StockCard
                                val impresoraNombre = sessionViewModel.impresoraSeleccionada.value
                                val impresora =
                                    impresionLogic.impresoras.value.find { it.nombre == impresoraNombre }
                                        ?: return@StockCard
                                impresionLogic.imprimirStock(
                                    empresaId = empresaId,
                                    stock = sel,
                                    usuario = usuario?.name ?: "Desconocido",
                                    dispositivoId = sessionViewModel.dispositivo.value?.id
                                        ?: "Desconocido",
                                    idImpresora = impresora.id
                                )
                            }
                        )
                        Spacer(Modifier.height(8.dp))
                    }*/
                    items(items) { stock ->
                        var mostrarModal by remember { mutableStateOf(false) }
                        var mostrarModalPalet by remember { mutableStateOf(false) }

                        StockCard(
                            stock = stock,
                            onPrintClick = { mostrarModal = true },
                            onPrintPaletClick = { mostrarModalPalet = true },
                            sessionViewModel = sessionViewModel
                        )

                        if (mostrarModal) {

                            var dropOpen by remember { mutableStateOf(false) }
                            val impresoraSeleccionadaNombre by sessionViewModel.impresoraSeleccionada.collectAsState()
                            val impresoraSel = remember(impresoraSeleccionadaNombre, impresionLogic.impresoras.value) {
                                impresionLogic.impresoras.value.find { imp -> imp.nombre == impresoraSeleccionadaNombre }
                            }
                            var copias by remember { mutableStateOf(1) }

                            AlertDialog(
                                onDismissRequest = { mostrarModal = false },
                                confirmButton = {
                                    TextButton(onClick = {
                                        val empresaId = empresaCodigo ?: return@TextButton

                                        if (impresoraSel == null || copias <= 0) {
                                            stockViewModel.setError("Selecciona impresora y número de copias válido")
                                            return@TextButton
                                        }

                                        impresionLogic.imprimirStock(
                                            empresaId     = empresaId,
                                            stock         = stock,
                                            usuario       = usuario?.name ?: "Desconocido",
                                            dispositivoId = sessionViewModel.dispositivo.value?.id ?: "Desconocido",
                                            idImpresora   = impresoraSel.id,
                                            copias        = copias
                                        )

                                        mostrarModal = false
                                    }) {
                                        Text("Imprimir")
                                    }
                                },
                                dismissButton = {
                                    TextButton(onClick = { mostrarModal = false }) {
                                        Text("Cancelar")
                                    }
                                },
                                title = { Text("Impresión de etiqueta") },
                                text = {
                                    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                                        Text("Impresora")

                                        Box {
                                            OutlinedTextField(
                                                readOnly = true,
                                                value = impresoraSel?.nombre ?: "",
                                                onValueChange = {},
                                                label = { Text("Impresora") },
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
                                                impresionLogic.impresoras.value.forEach { imp ->
                                                    DropdownMenuItem(
                                                        text = { Text(imp.nombre) },
                                                        onClick = {
                                                            sessionViewModel.actualizarImpresora(imp.nombre)
                                                            dropOpen = false
                                                        }
                                                    )
                                                }
                                            }
                                        }

                                        Text("Número de copias")
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
                                }
                            )
                        }

                        // Diálogo de impresión de palet
                        if (mostrarModalPalet && stock.paletId != null) {
                            var dropOpenPalet by remember { mutableStateOf(false) }
                            val impresoraSeleccionadaNombre by sessionViewModel.impresoraSeleccionada.collectAsState()
                            val impresoraSel = remember(impresoraSeleccionadaNombre, impresionLogic.impresoras.value) {
                                impresionLogic.impresoras.value.find { imp -> imp.nombre == impresoraSeleccionadaNombre }
                            }
                            var copiasPalet by remember { mutableStateOf(1) }

                            AlertDialog(
                                onDismissRequest = { mostrarModalPalet = false },
                                confirmButton = {
                                    TextButton(onClick = {
                                        if (impresoraSel == null || copiasPalet <= 0) {
                                            stockViewModel.setError("Selecciona impresora y número de copias válido")
                                            return@TextButton
                                        }

                                        impresionLogic.imprimirPalet(
                                            paletId       = stock.paletId!!,
                                            usuario       = usuario?.name ?: "Desconocido",
                                            dispositivoId = sessionViewModel.dispositivo.value?.id ?: "Desconocido",
                                            idImpresora   = impresoraSel.id,
                                            copias        = copiasPalet
                                        )

                                        mostrarModalPalet = false
                                    }) {
                                        Text("Imprimir")
                                    }
                                },
                                dismissButton = {
                                    TextButton(onClick = { mostrarModalPalet = false }) {
                                        Text("Cancelar")
                                    }
                                },
                                title = { Text("Impresión de etiqueta de palet") },
                                text = {
                                    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                                        // Información del palet
                                        Card(
                                            colors = CardDefaults.cardColors(
                                                containerColor = Color(0xFF2196F3).copy(alpha = 0.1f)
                                            ),
                                            modifier = Modifier.fillMaxWidth()
                                        ) {
                                            Column(modifier = Modifier.padding(12.dp)) {
                                                Text(
                                                    text = "Palet: ${stock.codigoPalet ?: "—"}",
                                                    style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.Bold),
                                                    color = Color(0xFF2196F3)
                                                )
                                                Text("📊 Estado: ${stock.estadoPalet ?: "—"}")
                                                if (stock.ordenTrabajoId != null) {
                                                    Text("📋 Orden de Trabajo: ${stock.ordenTrabajoId}")
                                                }
                                            }
                                        }

                                        Text("Impresora")

                                        Box {
                                            OutlinedTextField(
                                                readOnly = true,
                                                value = impresoraSel?.nombre ?: "",
                                                onValueChange = {},
                                                label = { Text("Impresora") },
                                                modifier = Modifier.fillMaxWidth(),
                                                trailingIcon = {
                                                    IconButton(onClick = { dropOpenPalet = !dropOpenPalet }) {
                                                        Icon(Icons.Default.ArrowDropDown, contentDescription = null)
                                                    }
                                                }
                                            )
                                            DropdownMenu(
                                                expanded = dropOpenPalet,
                                                onDismissRequest = { dropOpenPalet = false }
                                            ) {
                                                impresionLogic.impresoras.value.forEach { imp ->
                                                    DropdownMenuItem(
                                                        text = { Text(imp.nombre) },
                                                        onClick = {
                                                            sessionViewModel.actualizarImpresora(imp.nombre)
                                                            dropOpenPalet = false
                                                        }
                                                    )
                                                }
                                            }
                                        }

                                        Text("Número de copias")
                                        Row(verticalAlignment = Alignment.CenterVertically) {
                                            IconButton(onClick = { if (copiasPalet > 1) copiasPalet-- }) {
                                                Icon(Icons.Default.Remove, contentDescription = "Menos")
                                            }
                                            Text(copiasPalet.toString(), modifier = Modifier.padding(8.dp))
                                            IconButton(onClick = { copiasPalet++ }) {
                                                Icon(Icons.Default.Add, contentDescription = "Más")
                                            }
                                        }
                                    }
                                }
                            )
                        }

                        Spacer(Modifier.height(8.dp))
                    }

                }
            }

            /** --------- LOADING --------- **/
            if (cargando) {
                LoadingDialog(texto = "Consultando stock...")
            }
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
                                            skipNextDescripcionSearch = true
                                            codigoUbicacion = TextFieldValue("")
                                            almacenLogic.onAlmacenSeleccionado("Todos")
                                            codigoArticulo = TextFieldValue(articulo.codigoArticulo)
                                            descripcionBusqueda =
                                                TextFieldValue(articulo.descripcion ?: "")

                                            stockViewModel.setPartidaSeleccionada(articulo.partida)

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

        }

        if (escaneando && !DeviceUtils.hasHardwareScanner(context)) {     // ← únicamente tablets/móviles
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .background(Color.Black.copy(alpha = 0.6f))
                    .zIndex(10f),
                contentAlignment = Alignment.Center
            ) {
                Column(horizontalAlignment = Alignment.CenterHorizontally) {

                    Text(
                        text = "Escaneando código…",
                        style = MaterialTheme.typography.titleMedium,
                        textAlign = TextAlign.Center,
                        modifier = Modifier.fillMaxWidth()
                    )

                    Spacer(Modifier.height(12.dp))

                    QRScannerView(
                        modifier = Modifier
                            .fillMaxWidth(0.5f)
                            .aspectRatio(1f), // Hace que sea cuadrado (mismo ancho que altura)
                        onCodeScanned = { code ->
                            if (escaneoProcesado) return@QRScannerView
                            escaneoProcesado = true     // marca como procesado para evitar múltiples ejecuciones
                            escaneando = false          // cierra overlay tras 1 lectura

                            stockLogic.procesarCodigoEscaneado(
                                code = code,
                                almacenSel = almacenSel,
                                empresaId = empresa?.codigo?.toShort()
                                    ?: return@QRScannerView,
                                //onCodigoArticuloDetectado = { codigoArticulo = it },
                                onCodigoArticuloDetectado = {
                                    codigoArticulo = it
                                    lanzarConsulta()
                                },
                                onPaletDetectado = { palet ->
                                    stockLogic.consultarStockPorPalet(
                                        palet = palet,
                                        codigoEmpresa = empresa?.codigo?.toShort() ?: return@procesarCodigoEscaneado,
                                        onSuccess = { stockDelPalet ->
                                            Log.d("PALET_STOCK", "✅ Stock del palet ${palet.codigoPalet} cargado: ${stockDelPalet.size} artículos")
                                        },
                                        onError = { error ->
                                            stockViewModel.setError("Error al consultar stock del palet: $error")
                                        }
                                    )
                                },

                                /*onUbicacionDetectada = { tfv ->
                                    val s = tfv.text.trim()
                                    if (s.contains('$')) {
                                        val parts = s.split('$', limit = 2)
                                        if (parts.size == 2 && parts[0].isNotBlank()) {
                                            almacenLogic.onAlmacenSeleccionado(parts[0])
                                            codigoUbicacion = TextFieldValue(parts[1])
                                        } else {
                                            codigoUbicacion = TextFieldValue(parts.getOrNull(1) ?: s.removePrefix("$"))
                                        }
                                    } else {
                                        codigoUbicacion = tfv
                                    }
                                }*/
                                onUbicacionDetectada = { tfv ->
                                    val s = tfv.text.trim()
                                    if (s.contains('$')) {
                                        val parts = s.split('$', limit = 2)
                                        if (parts.size == 2 && parts[0].isNotBlank()) {
                                            // "ALM$UB..."  o  "ALM$"
                                            val almCode = parts[0]
                                            val cuerpo  = parts[1]
                                            // selecciona visualmente el almacén si quieres
                                            // almacenLogic.onAlmacenSeleccionado(almCode)

                                            // ⬅️ clave: si es "ALM$" dejamos "ALM$" en el TextField
                                            codigoUbicacion = if (cuerpo.isBlank())
                                                TextFieldValue("$almCode$")
                                            else
                                                TextFieldValue(s) // deja "ALM$UB..."
                                        } else {
                                            // "$UB..." -> deja "UB..." (sin "$")
                                            codigoUbicacion = TextFieldValue(parts.getOrNull(1) ?: s.removePrefix("$"))
                                        }
                                    } else {
                                        codigoUbicacion = tfv
                                    }
                                }
                                ,
                                onMultipleArticulos = { lista ->
                                    stockViewModel.setArticulosFiltrados(lista)
                                    stockViewModel.setMostrarDialogoSeleccion(true)
                                },
                                onError = { 
                                    stockViewModel.setError(it)
                                    SoundUtils.getInstance().playErrorSound()
                                },
                                lanzarConsulta = { lanzarConsulta() }
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
}

