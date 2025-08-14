package com.example.sga.view.etiquetas

import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.focusable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.ArrowDropDown
import androidx.compose.material.icons.filled.Remove
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.TextFieldValue
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.filled.Print
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.navigation.NavHostController
import com.example.sga.data.dto.etiquetas.LogImpresionDto
import com.example.sga.service.LectorPDA.DeviceUtils
import androidx.compose.ui.text.font.FontWeight
import com.example.sga.service.scanner.QRScannerView
import com.example.sga.view.app.SessionViewModel
import com.example.sga.view.components.AppTopBar
import androidx.compose.material.icons.filled.Search
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.input.key.onPreviewKeyEvent
import androidx.compose.ui.layout.layout
import androidx.compose.ui.text.style.TextAlign
import com.example.sga.view.stock.StockCard
import java.time.LocalDate

@Composable
private fun ArticuloSearchSection(
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
            onValueChange = onDescripcionChange,
            label = { Text("Descripción") },
            modifier = Modifier.weight(2f),
            singleLine = true,
            trailingIcon = {
                IconButton(onClick = onSearchDescripcion) {
                    Icon(Icons.Default.Search, contentDescription = "Buscar")
                }
            },
            keyboardOptions = KeyboardOptions.Default.copy(imeAction = ImeAction.Search),
            keyboardActions = androidx.compose.foundation.text.KeyboardActions(onSearch = { onSearchDescripcion() })
        )
    }
}

/* -----------------------------------------------------
   Pantalla principal
   ----------------------------------------------------- */
@Composable
fun EtiquetasScreen(
    navController: NavHostController,
    sessionViewModel: SessionViewModel,
    viewModel: EtiquetasViewModel = viewModel()

) {
    /* ----- Session & ViewModel state ----- */
    viewModel.init(sessionViewModel)
    val empresa by sessionViewModel.empresaSeleccionada.collectAsState()
    val codigoEmpresa: Short? = empresa?.codigo?.toShort()
    var pestañaSeleccionada by remember { mutableStateOf(0) }
    val pestañas = listOf("Artículos", "Ubicaciones")

    val articulo              by viewModel.articuloSeleccionado.collectAsState()
    val alergenos             by viewModel.alergenos.collectAsState()
    val impresoras            by viewModel.impresoras.collectAsState()
    val cargando              by viewModel.cargando.collectAsState()
    val error                 by viewModel.error.collectAsState()
    val articulosFiltrados    by viewModel.articulosFiltrados.collectAsState()
    val mostrarDialogo        by viewModel.mostrarDialogoSeleccion.collectAsState()

    /* ----- Local UI state ----- */
    var codigoArticuloTF  by remember { mutableStateOf(TextFieldValue("")) }
    var descripcionTF     by remember { mutableStateOf(TextFieldValue("")) }
    var copias            by remember { mutableStateOf(1) }
    var escaneando        by remember { mutableStateOf(false) }
    var escaneoProcesado by remember { mutableStateOf(false) }
    var dropOpen          by remember { mutableStateOf(false) }
    val impresoraNombre = sessionViewModel.impresoraSeleccionada.collectAsState().value
    val impresoraSel = impresoras.find { it.nombre == impresoraNombre }
    val resultado by viewModel.resultado.collectAsState()

    /* ----- Cargar impresoras una sola vez ----- */
    LaunchedEffect(Unit) { viewModel.cargarImpresoras() }

    /* ----- Si se selecciona artículo, traer alérgenos automáticamente ----- */
    LaunchedEffect(articulo) {
        articulo?.let { art ->
            codigoEmpresa?.let { ce -> viewModel.obtenerAlergenos(ce, art.codigoArticulo) }
        }
    }

    Scaffold(
        topBar = {
            AppTopBar(
                sessionViewModel = sessionViewModel,
                navController = navController,
                title = ""
            )
        }
    ) { padding ->
        if (DeviceUtils.isHoneywell) {
            val focusRequester = remember { FocusRequester() }

            Box(
                modifier = Modifier
                    .focusRequester(focusRequester)
                    .focusable()
                    .onPreviewKeyEvent { event ->
                        if (event.nativeKeyEvent?.action == android.view.KeyEvent.ACTION_MULTIPLE) {
                            event.nativeKeyEvent.characters?.let { code ->
                                codigoEmpresa?.let { ce ->
                                    viewModel.procesarCodigoEscaneado(code.trim(), ce)
                                } ?: viewModel.setError("Empresa no seleccionada")
                            }
                            true
                        } else false
                    }
                    .layout { measurable, constraints ->
                        val placeable = measurable.measure(constraints)
                        layout(0, 0) { placeable.place(0, 0) }
                    }
            )

            LaunchedEffect(Unit) { focusRequester.requestFocus() }
        }

        LazyColumn(
            modifier = Modifier
                .padding(padding)
                .fillMaxSize(),
            contentPadding = PaddingValues(16.dp)
        ) {
            item {
                Column {
                    Text("Gestión de Etiquetas", style = MaterialTheme.typography.titleLarge)
                    Spacer(Modifier.height(8.dp))

                    TabRow(selectedTabIndex = pestañaSeleccionada) {
                        pestañas.forEachIndexed { index, title ->
                            Tab(
                                selected = pestañaSeleccionada == index,
                                onClick = { pestañaSeleccionada = index },
                                text = { Text(title) }
                            )
                        }
                    }

                    Spacer(Modifier.height(16.dp))
                }
            }

            if (pestañaSeleccionada == 0) {
                /* Escáner QR o campos de búsqueda */
                if (escaneando && !DeviceUtils.isHoneywell) {
                    item {
                        Box(
                            modifier = Modifier
                                .fillMaxSize()
                                .background(MaterialTheme.colorScheme.background.copy(alpha = 0.85f)),
                            contentAlignment = Alignment.Center
                        ) {
                            Column(horizontalAlignment = Alignment.CenterHorizontally) {
                                Text(
                                    text = "Escaneando código de artículo...",
                                    style = MaterialTheme.typography.titleMedium,
                                    textAlign = TextAlign.Center
                                )
                                Spacer(modifier = Modifier.height(12.dp))

                                QRScannerView(
                                    modifier = Modifier
                                        .fillMaxWidth(0.5f)
                                        .height(250.dp),
                                    onCodeScanned = { code ->
                                        if (escaneoProcesado) return@QRScannerView
                                        escaneoProcesado = true
                                        escaneando = false
                                        codigoEmpresa?.let { ce ->
                                            viewModel.procesarCodigoEscaneado(code, ce)
                                        } ?: viewModel.setError("Empresa no seleccionada")
                                    }
                                )

                                Spacer(modifier = Modifier.height(12.dp))
                                Button(onClick = { escaneando = false }) {
                                    Text("Cancelar escaneo")
                                }
                            }
                        }
                    }
                }else {
                    /* Botón escanear */
                    item {
                        if (!DeviceUtils.isHoneywell) {
                            Button(
                                onClick = {
                                    viewModel.setError(null)
                                    escaneoProcesado = false
                                    escaneando = true
                                },
                                modifier = Modifier.fillMaxWidth()
                            ) {
                                Text("Escanear QR")
                            }
                            Spacer(Modifier.height(12.dp))
                        }
                    }

                    /* Campos código + descripción */
                    item {
                        ArticuloSearchSection(
                            codigoArticulo = codigoArticuloTF,
                            onCodigoChange = { codigoArticuloTF = it },
                            descripcion = descripcionTF,
                            onDescripcionChange = { descripcionTF = it },
                            onSearchDescripcion = {
                                if (descripcionTF.text.isNotBlank()) {
                                    codigoEmpresa?.let { ce ->
                                        viewModel.buscarPorDescripcion(descripcionTF.text, ce)
                                    } ?: viewModel.setError("Empresa no seleccionada")
                                }
                            }
                        )
                        Spacer(Modifier.height(8.dp))
                    }

                    /* Botón buscar */
                    item {
                        val puedeBuscar =
                            codigoArticuloTF.text.isNotBlank() || descripcionTF.text.isNotBlank()
                        Button(
                            onClick = {
                                when {
                                    codigoArticuloTF.text.isNotBlank() -> {
                                        codigoEmpresa?.let { ce ->
                                            viewModel.buscarPorCodigo(codigoArticuloTF.text, ce)
                                        } ?: viewModel.setError("Empresa no seleccionada")
                                    }

                                    descripcionTF.text.isNotBlank() -> {
                                        codigoEmpresa?.let { ce ->
                                            viewModel.buscarPorDescripcion(descripcionTF.text, ce)
                                        } ?: viewModel.setError("Empresa no seleccionada")
                                    }
                                }
                            },
                            enabled = puedeBuscar,
                            modifier = Modifier.fillMaxWidth()
                        ) { Text("Buscar artículo") }
                        Spacer(Modifier.height(12.dp))
                    }
                }

                /* Mostrar artículo seleccionado (solo si hay uno y ya se ha cerrado el diálogo) */
                val articuloSel = articulo
                item {
                    LaunchedEffect(key1 = articuloSel, key2 = mostrarDialogo) {
                        if (!mostrarDialogo && articuloSel != null) {
                            codigoArticuloTF = TextFieldValue("")
                            descripcionTF = TextFieldValue("")
                        }
                    }

                    if (!mostrarDialogo && articuloSel != null) {
                        Card(
                            modifier = Modifier.fillMaxWidth(),
                            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant),
                            elevation = CardDefaults.cardElevation(defaultElevation = 4.dp)
                        ) {
                            Column(modifier = Modifier.padding(16.dp)) {
                                Text(
                                    "Artículo seleccionado",
                                    style = MaterialTheme.typography.titleMedium
                                )
                                Spacer(Modifier.height(8.dp))
                                Text("Código: ${articuloSel.codigoArticulo}")
                                Text("Descripción: ${articuloSel.descripcion}")
                                if (!alergenos.isNullOrBlank()) {
                                    Text("Alérgenos: $alergenos")
                                }
                            }
                        }
                        Spacer(Modifier.height(16.dp))
                    }
                }
                items(resultado) { stock ->
                    var mostrarModal by remember { mutableStateOf(false) }

                    Card(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(vertical = 4.dp),
                        elevation = CardDefaults.cardElevation(4.dp)
                    ) {
                        Column(modifier = Modifier.padding(16.dp)) {

                            Text("Artículo: ${stock.codigoArticulo}")
                            Text("Descripción: ${stock.descripcionArticulo ?: "—"}")
                            Text("Partida: ${stock.partida ?: "—"}")
                            Text("Caducidad: ${stock.fechaCaducidad ?: "—"}")

                            Spacer(Modifier.height(8.dp))

                            // 🖨 Icono de impresora que abre el modal
                            Row(horizontalArrangement = Arrangement.spacedBy(16.dp)) {
                                IconButton(onClick = { mostrarModal = true }) {
                                    Icon(Icons.Default.Print, contentDescription = "Configurar e imprimir")
                                }
                            }
                        }
                    }

                    if (mostrarModal) {
                        AlertDialog(
                            onDismissRequest = { mostrarModal = false },
                            confirmButton = {
                                TextButton(onClick = {
                                    val empresaId = codigoEmpresa ?: return@TextButton

                                    val impNombre = sessionViewModel.impresoraSeleccionada.value
                                    val impresora = impresoras.find { it.nombre == impNombre }

                                    if (impresora == null || copias <= 0) {
                                        viewModel.setError("Faltan datos para imprimir")
                                        return@TextButton
                                    }

                                    val dto = LogImpresionDto(
                                        usuario = sessionViewModel.user.value?.name ?: "Desconocido",
                                        dispositivo = sessionViewModel.dispositivo.value?.id
                                            ?: "Desconocido",
                                        idImpresora = impresora.id,
                                        etiquetaImpresa = 0,
                                        codigoArticulo = stock.codigoArticulo,
                                        descripcionArticulo = stock.descripcionArticulo ?: "",
                                        copias = copias,
                                        codigoAlternativo = articulo?.codigoAlternativo?.takeIf { it.isNotBlank() }
                                            ?: "",
                                        fechaCaducidad = stock.fechaCaducidad?.take(10),
                                        partida = stock.partida,
                                        alergenos = alergenos,
                                        pathEtiqueta = "\\\\Sage200\\mrh\\Servicios\\PrintCenter\\ETIQUETAS\\MMPP_MES.nlbl",
                                        tipoEtiqueta = 1,
                                        codigoGS1 = null,
                                        codigoPalet = null
                                    )

                                    viewModel.enviarLogImpresion(dto)
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
                                            impresoras.forEach { imp ->
                                                DropdownMenuItem(
                                                    text = { Text(imp.nombre) },
                                                    onClick = {
                                                        dropOpen = false
                                                        sessionViewModel.actualizarImpresora(imp.nombre)
                                                        viewModel.actualizarImpresoraSeleccionadaEnBD(imp.nombre)
                                                    }
                                                )
                                            }
                                        }
                                    }

                                    Text("Número de copias", style = MaterialTheme.typography.bodyMedium)
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
                }

                item {
                    if (cargando) {
                        CircularProgressIndicator()
                        Spacer(Modifier.height(16.dp))
                    }
                    error?.let { Text("Error: $it", color = MaterialTheme.colorScheme.error) }
                }
            }else if (pestañaSeleccionada == 1) {
                // Pestaña "Ubicaciones" (por ahora solo informativo)
                item {
                    Text(
                        "Pantalla de impresión de ubicaciones próximamente...",
                        style = MaterialTheme.typography.bodyMedium
                    )
                    Spacer(Modifier.height(16.dp))
                }
            }
        }

        /* ------------- Dialogo selección múltiple ------------- */
        if (mostrarDialogo && articulosFiltrados.isNotEmpty()) {
            AlertDialog(
                onDismissRequest = { viewModel.setMostrarDialogoSeleccion(false) },
                confirmButton = {},
                title = { Text("Selecciona un artículo") },
                text = {
                    Column(
                        modifier = Modifier
                            .fillMaxWidth()
                            .heightIn(max = 400.dp)
                            .verticalScroll(rememberScrollState())
                    ) {
                        articulosFiltrados.forEach { art ->
                            Card(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .padding(vertical = 4.dp)
                                    .clickable {
                                        viewModel.setArticuloSeleccionado(art)
                                        codigoEmpresa?.let { ce ->
                                            viewModel.obtenerAlergenos(ce, art.codigoArticulo)
                                            viewModel.consultarStock(ce, art.codigoArticulo)
                                        }
                                        viewModel.setMostrarDialogoSeleccion(false)
                                    },
                                elevation = CardDefaults.cardElevation(4.dp)
                            ) {
                                Column(Modifier.padding(12.dp)) {
                                    Text(
                                        text = "📦 ${art.codigoArticulo}",
                                        style = MaterialTheme.typography.bodyLarge,
                                        fontWeight = FontWeight.Bold
                                    )
                                    Spacer(Modifier.height(4.dp))
                                    Text(
                                        text = art.descripcion ?: "Sin descripción",
                                        style = MaterialTheme.typography.bodyMedium
                                    )
                                }
                            }
                        }
                    }
                },
                dismissButton = {
                    TextButton(onClick = { viewModel.setMostrarDialogoSeleccion(false) }) {
                        Text("Cancelar")
                    }
                }
            )
        }

        val articuloUnico = articulo
        if (mostrarDialogo && articuloUnico != null && articulosFiltrados.isEmpty()) {
            AlertDialog(
                onDismissRequest = { viewModel.setMostrarDialogoSeleccion(false) },
                title = { Text("Artículo encontrado") },
                text = {
                    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        Text("Código: ${articuloUnico.codigoArticulo}", style = MaterialTheme.typography.bodyLarge)
                        Text("Descripción: ${articuloUnico.descripcion}", style = MaterialTheme.typography.bodyLarge)
                        if (!alergenos.isNullOrBlank()) {
                            Text("Alérgenos: $alergenos", style = MaterialTheme.typography.bodyMedium)
                        }
                    }
                },
                confirmButton = {
                    TextButton(onClick = { viewModel.setMostrarDialogoSeleccion(false) }) {
                        Text("Aceptar")
                    }
                }
            )
        }

    }
}


