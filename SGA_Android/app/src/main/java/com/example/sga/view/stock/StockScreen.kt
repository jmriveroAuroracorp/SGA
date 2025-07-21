@file:OptIn(ExperimentalMaterial3Api::class)
package com.example.sga.view.stock

import android.os.Build
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import android.util.Log
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Print
import androidx.compose.material.icons.filled.Search
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
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
import androidx.compose.ui.zIndex
import kotlinx.coroutines.flow.*
import android.widget.Toast
import androidx.compose.foundation.focusable
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.input.key.onPreviewKeyEvent
import com.example.sga.service.LectorPDA.DeviceUtils
import android.view.KeyEvent
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.ArrowDropDown
import androidx.compose.material.icons.filled.Remove
import androidx.compose.ui.layout.layout

@Composable
fun StockCard(
    stock: Stock,
    onPrintClick: (Stock) -> Unit,          // ← nuevo callback
    modifier: Modifier = Modifier
) {
    val fechaCorta     = stock.fechaCaducidad?.take(10) ?: "Sin fecha"
    val saldoPositivo  = stock.unidadesSaldo > 0
    val colorSaldo     = if (saldoPositivo)
        MaterialTheme.colorScheme.onSurface
    else
        MaterialTheme.colorScheme.error
    val estiloSaldo    = if (saldoPositivo)
        MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.Bold)
    else
        MaterialTheme.typography.bodyLarge

    Card(
        modifier = modifier
            .fillMaxWidth()
            .padding(vertical = 4.dp),
        elevation = CardDefaults.cardElevation(defaultElevation = 4.dp)
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Column(Modifier.weight(1f)) {
                Text(
                    text  = "📦 ${stock.codigoArticulo} — ${stock.descripcionArticulo}",
                    style = MaterialTheme.typography.titleMedium
                )
                Spacer(Modifier.height(8.dp))
                Text("🏬 Almacén: ${stock.codigoAlmacen} - ${stock.almacen}")
                Text("📍 Ubicación: ${stock.ubicacion}")
                Text("📋 Partida: ${stock.partida}")
                Text("🗓 Caducidad: $fechaCorta")
                Text(
                    text  = "📦 Saldo: ${"%.2f".format(stock.unidadesSaldo)}",
                    color = colorSaldo,
                    style = estiloSaldo
                )
            }

            IconButton(onClick = { onPrintClick(stock) }) {
                Icon(Icons.Default.Print, contentDescription = "Imprimir etiqueta")
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
    val stockLogic = remember { StockLogic(stockViewModel) }
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
        if (DeviceUtils.isHoneywell) escaneando = true
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

    fun lanzarConsulta() {
        val empresaId = empresa?.codigo ?: return

        Log.d(
            "CONSULTA_STOCK",
            "🔍 Articulo=${codigoArticulo.text}  Ubicacion=${codigoUbicacion.text}  Almacen=$almacenSel"
        )

        when {
            codigoArticulo.text.isNotBlank() -> {
                Log.d("CONSULTA_STOCK", "🟢 Consultando por artículo")
                stockLogic.consultarStock(
                    codigoEmpresa = empresaId,
                    codigoArticulo = codigoArticulo.text,
                    codigoUbicacion = null
                )
            }

            codigoUbicacion.text.isNotBlank() && almacenSel != null && almacenSel != "Todos" -> {
                Log.d("CONSULTA_STOCK", "🟡 Consultando por ubicación y almacén")
                stockLogic.consultarStock(
                    codigoEmpresa = empresaId,
                    codigoArticulo = null,
                    codigoUbicacion = codigoUbicacion.text,
                    codigoAlmacen = almacenSel,
                    partida = stockViewModel.partidaSeleccionada.value
                )
            }

            else -> {
                Log.d("CONSULTA_STOCK", "🔴 No se cumple ninguna condición")
                stockViewModel.setError("Introduce un código de artículo o una ubicación con almacén válido.")
            }
        }
    }
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
                    codigoEmpresa = empresaCodigo ?: return@collect,  // 👈 nuevo
                    codigoAlmacen = almacenFiltro,
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
            if (DeviceUtils.isHoneywell) {
                val focusRequester = remember { FocusRequester() }

                Box(
                    modifier = Modifier
                        .focusRequester(focusRequester)
                        .focusable()
                        .onPreviewKeyEvent { event ->
                            if (event.nativeKeyEvent?.action == KeyEvent.ACTION_MULTIPLE) {
                                event.nativeKeyEvent.characters?.let { code ->
                                    stockLogic.procesarCodigoEscaneado(
                                        code = code.trim(),
                                        almacenSel = almacenSel,
                                        empresaId  = empresa?.codigo?.toShort()
                                            ?: return@onPreviewKeyEvent true,
                                        onCodigoArticuloDetectado = { codigoArticulo = it },
                                        onUbicacionDetectada      = { codigoUbicacion = it },
                                        onMultipleArticulos       = { lista ->
                                            stockViewModel.setArticulosFiltrados(lista)
                                            stockViewModel.setMostrarDialogoSeleccion(true)
                                        },
                                        onError         = { stockViewModel.setError(it) },
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

                LaunchedEffect(Unit) { focusRequester.requestFocus() }
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



                    Spacer(Modifier.height(8.dp))

                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.Center
                    ) {
                        Column(modifier = Modifier.fillMaxWidth()) {
                            if (!DeviceUtils.isHoneywell) {
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
                            if (almacenSel == null || almacenSel == "Todos") {
                                Row(
                                    verticalAlignment = Alignment.CenterVertically,
                                    modifier = Modifier.padding(top = 4.dp)
                                ) {
                                    Icon(
                                        imageVector = Icons.Default.Warning,
                                        contentDescription = null,
                                        tint = MaterialTheme.colorScheme.error,
                                        modifier = Modifier.size(16.dp)
                                    )
                                    Spacer(Modifier.width(4.dp))
                                    Text(
                                        text = "Selecciona un almacén para escanear ubicaciones.",
                                        color = MaterialTheme.colorScheme.error,
                                        style = MaterialTheme.typography.bodySmall
                                    )
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
                    val impresoraActiva =
                        sessionViewModel.impresoraSeleccionada.collectAsState().value

                    OutlinedTextField(
                        value = impresoraActiva ?: "Sin impresora asignada",
                        onValueChange = {},
                        readOnly = true,
                        label = { Text("Impresora asignada") },
                        modifier = Modifier.fillMaxWidth()
                    )
                    Spacer(Modifier.height(16.dp))


                    Text("Almacén", style = MaterialTheme.typography.titleMedium)
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

                    Spacer(Modifier.height(8.dp))


                    TabRow(selectedTabIndex = vistaSeleccionadaIndex) {
                        opcionesVista.forEachIndexed { index, texto ->
                            Tab(
                                selected = vistaSeleccionadaIndex == index,
                                onClick = { vistaSeleccionadaIndex = index },
                                text = { Text(texto.capitalize()) }
                            )
                        }
                    }

                    Spacer(Modifier.height(12.dp))

                    Button(
                        onClick = { lanzarConsulta() },
                        enabled = puedeConsultar,
                        modifier = Modifier.fillMaxWidth()
                    ) { Text("Consultar Stock") }


                    Spacer(Modifier.height(16.dp))

                    if (error != null) {
                        Text("⚠️ $error", color = MaterialTheme.colorScheme.error)
                        Spacer(Modifier.height(16.dp))
                    }
                }

                /** --------- FILTRO DE RESULTADOS --------- **/
                val filtrado = resultado.filter { item ->
                    val porPermiso = item.codigoCentro == usuario?.codigoCentro ||
                            usuario?.codigosAlmacen?.contains(item.codigoAlmacen) == true

                    val porAlmacen =
                        almacenSel == null || almacenSel == "Todos" || item.codigoAlmacen == almacenSel

                    porPermiso && porAlmacen
                }

                val agrupado = when (vistaSeleccionada) {
                    "Almacén" -> filtrado.groupBy { it.almacen }
                    "Partida" -> filtrado.groupBy { it.partida }
                    // "Fecha Cad"    -> filtrado.groupBy { it.fechaCaducidad }
                    "Artículo" -> filtrado.groupBy { it.codigoArticulo }
                    else -> filtrado.groupBy { "Sin clasificar" }
                }

                /** --------- RESULTADOS AGRUPADOS --------- **/
                agrupado.forEach { (grupo, items) ->
                    item {
                        Text(
                            text = when (vistaSeleccionada) {
                                "Fecha Cad" -> "🗓 Caducidad: $grupo"
                                "Almacén" -> "📦 Almacén: $grupo"
                                "Partida" -> "📋 Partida: $grupo"
                                "Artículo" -> "🔢 Artículo: $grupo"
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

                        StockCard(
                            stock = stock,
                            onPrintClick = { mostrarModal = true }
                        )

                        if (mostrarModal) {
                            var dropOpen by remember { mutableStateOf(false) }
                            val impresoraSel = impresionLogic.impresoras.value.find {
                                it.nombre == sessionViewModel.impresoraSeleccionada.value
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

        if (escaneando && !DeviceUtils.isHoneywell) {     // ← únicamente tablets/móviles
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
                            .height(250.dp),
                        onCodeScanned = { code ->
                            if (escaneoProcesado) return@QRScannerView
                            escaneando = false          // cierra overlay tras 1 lectura

                            stockLogic.procesarCodigoEscaneado(
                                code = code,
                                almacenSel = almacenSel,
                                empresaId = empresa?.codigo?.toShort()
                                    ?: return@QRScannerView,
                                onCodigoArticuloDetectado = { codigoArticulo = it },
                                onUbicacionDetectada = { codigoUbicacion = it },
                                onMultipleArticulos = { lista ->
                                    stockViewModel.setArticulosFiltrados(lista)
                                    stockViewModel.setMostrarDialogoSeleccion(true)
                                },
                                onError = { stockViewModel.setError(it) },
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