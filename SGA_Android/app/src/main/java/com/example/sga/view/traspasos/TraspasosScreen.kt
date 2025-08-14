package com.example.sga.view.traspasos

import android.util.Log
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.focusable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.ArrowDropDown
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Print
import androidx.compose.material.icons.filled.Remove
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.navigation.NavHostController
import com.example.sga.data.dto.traspasos.PaletCrearDto
import com.example.sga.view.app.SessionViewModel
import com.example.sga.view.components.AppTopBar
import androidx.compose.ui.Alignment
import androidx.compose.ui.focus.FocusRequester
import com.example.sga.data.dto.traspasos.PaletDto
import com.example.sga.service.LectorPDA.DeviceUtils
import com.example.sga.service.scanner.QRScannerView
import androidx.compose.ui.layout.layout
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.input.key.onPreviewKeyEvent
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import com.example.sga.data.dto.etiquetas.LogImpresionDto
import com.example.sga.data.dto.stock.ArticuloDto
import com.example.sga.data.dto.traspasos.MoverPaletDto
import com.example.sga.data.dto.traspasos.LineaPaletCrearDto
import com.example.sga.data.dto.traspasos.components.DialogSeleccionArticulo
import com.example.sga.data.dto.traspasos.CrearTraspasoArticuloDto
import com.example.sga.data.dto.traspasos.FinalizarTraspasoArticuloDto
import com.example.sga.data.model.stock.Stock
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.filled.SwapVert
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.zIndex
import com.example.sga.data.dto.traspasos.FinalizarTraspasoPaletDto
import java.time.LocalDateTime
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.ui.focus.onFocusChanged


@Composable
fun StockSelectionCards(
    stocks: List<Stock>,
    botonLabel: String,
    onConfirm: (stock: Stock, cantidad: Double) -> Unit
) {
    Text(
        "Selecciona cantidad",
        style = MaterialTheme.typography.titleMedium
    )
    stocks.forEach { stock ->
        val cantidadInput = remember { mutableStateOf(stock.unidadesSaldo.toString()) }
        Card(
            modifier = Modifier
                .fillMaxWidth()
                .padding(vertical = 4.dp),
            elevation = CardDefaults.cardElevation(2.dp)
        ) {
            Column(Modifier.padding(12.dp)) {
                Text("${stock.codigoArticulo} - ${stock.descripcionArticulo}", style = MaterialTheme.typography.bodyMedium)
                Text("Cantidad disponible: ${stock.unidadesSaldo}")
                val focusManager = LocalFocusManager.current

                OutlinedTextField(
                    value = cantidadInput.value,
                    onValueChange = { cantidadInput.value = it },
                    label = { Text("Cantidad a añadir") },
                    keyboardOptions = KeyboardOptions.Default.copy(
                        keyboardType = KeyboardType.Number,
                        imeAction = ImeAction.Done
                    ),
                    keyboardActions = KeyboardActions(
                        onDone = {
                            val cantidad = cantidadInput.value.toDoubleOrNull()
                            if (cantidad != null && cantidad > 0.0 && cantidad <= stock.unidadesSaldo) {
                                onConfirm(stock, cantidad)
                                focusManager.clearFocus()
                            }
                        }
                    ),
                    modifier = Modifier.fillMaxWidth()
                )

                Text("Lote: ${stock.partida ?: "—"}")
                Text("Ubicación: ${stock.ubicacion ?: "—"}")
                Text("Caducidad: ${stock.fechaCaducidad ?: "—"}")
                Spacer(modifier = Modifier.height(8.dp))

                Button(
                    onClick = {
                        val cantidad = cantidadInput.value.toDoubleOrNull()
                        if (cantidad == null || cantidad <= 0.0 || cantidad > stock.unidadesSaldo) {
                            return@Button
                        }
                        onConfirm(stock, cantidad)
                    },
                    modifier = Modifier.fillMaxWidth(),
                    enabled = cantidadInput.value.toDoubleOrNull()?.let { it > 0 && it <= stock.unidadesSaldo } ?: false
                ) {
                    Text(botonLabel)
                }
            }
        }
    }
}

@Composable

fun TraspasosScreen(
    navController: NavHostController,
    sessionViewModel: SessionViewModel,
    viewModel: TraspasosViewModel = viewModel(),
    esPalet: Boolean,
    directoDesdePaletCerrado: Boolean = false
) {
    /* ------------------  State que ya tenías  ------------------ */
    val empresa   = sessionViewModel.empresaSeleccionada.collectAsState().value?.codigo?.toShort() ?: return
    val usuarioId = sessionViewModel.user.collectAsState().value?.id?.toIntOrNull() ?: return

    val paletCreado by viewModel.paletCreado.collectAsState()

    /* ------------------ 1️⃣  Lista de opciones  ------------------ */
    val tiposPalet  by viewModel.tiposPalet.collectAsState()

    /* ------------------ 2️⃣  Opción elegida  ------------------ */
    var tipoSeleccionado by remember { mutableStateOf<String?>(null) }
    var ordenTrabajo      by remember { mutableStateOf("") }
    var dropOpen          by remember { mutableStateOf(false) }

    val paletEscaneado = viewModel.paletSeleccionado.collectAsState().value

    var articuloPendiente by remember { mutableStateOf<ArticuloDto?>(null) }
    var mostrarDialogoCrearPalet by remember { mutableStateOf(false) }
    var crearPaletActivo by remember { mutableStateOf(false) }
    var mostrarDialogoPaletCerrado by remember { mutableStateOf(false) }
    val resultadoStock = viewModel.resultadoStock.collectAsState().value

    var esperandoUbicacionDestino by remember { mutableStateOf(false) }
    var esperandoUbicacionParaCerrar by remember { mutableStateOf(false) }

    var traspasoPendienteId by remember { mutableStateOf<String?>(null) }
    var articuloPendienteMoverLocal by remember { mutableStateOf<ArticuloDto?>(null) }

    LaunchedEffect(Unit) {
        viewModel.setTraspasoEsDePalet(esPalet)
    }
    LaunchedEffect(Unit) {
        viewModel.setTraspasoDirectoDesdePaletCerrado(directoDesdePaletCerrado)
    }

    LaunchedEffect(Unit) {
        viewModel.comprobarTraspasoPendiente(
            usuarioId = usuarioId,
            onSuccess = { idTraspaso ->
                traspasoPendienteId = idTraspaso
                esperandoUbicacionDestino = true
            },
            onNoPendiente = {},
            onError = { errorMsg ->
                Log.e("TRASPASOS_UI", "Error comprobando traspaso pendiente: $errorMsg")
            }
        )
    }

    /* Carga inicial de la lista */
    LaunchedEffect(Unit) { viewModel.cargarTiposPalet() }
    LaunchedEffect(Unit) {
        viewModel.cargarImpresoras()
    }
    val scroll = rememberScrollState()
    val lineasPalet by viewModel.lineasPalet.collectAsState()

    var escaneando        by remember { mutableStateOf(false) }
    var escaneoProcesado  by remember { mutableStateOf(false) }
    val empresaSel        = sessionViewModel.empresaSeleccionada.collectAsState().value
    val empresaId         = empresaSel?.codigo?.toShort()
    val focusRequester    = remember { FocusRequester() }
    var triggerLineaPendiente by remember { mutableStateOf(false) }
    val articulosFiltrados = viewModel.articulosFiltrados.collectAsState().value
    val mostrarDialogoSeleccion = viewModel.mostrarDialogoSeleccion.collectAsState().value
    var mostrarDialogoImpresion by remember { mutableStateOf(false) }
    var mostrarDialogoCerrarPalet by remember { mutableStateOf(false) }
    var idPaletParaCerrar by remember { mutableStateOf<String?>(null) }
    var copias by remember { mutableIntStateOf(1) }
    var paletParaImprimir by remember { mutableStateOf<PaletDto?>(null) }
    var dropOpenImpresora by remember { mutableStateOf(false) }
    val impresoras by viewModel.impresoras.collectAsState()
    val impresoraNombre = sessionViewModel.impresoraSeleccionada.collectAsState().value
    val impresoraSel = impresoras.find { it.nombre == impresoraNombre }
    var articuloPendienteMover by remember { mutableStateOf<ArticuloDto?>(null) }
    var mostrarDialogoUbicacionPrimero by remember { mutableStateOf(false) }
    var mostrarDialogoMoverArticulo by remember { mutableStateOf(false) }
    var mostrarDialogoCancelarArticulo by remember { mutableStateOf(false) }
    var ubicacionEscaneada by remember { mutableStateOf<Pair<String,String>?>(null) }
    var mostrarDialogoCantidad by remember { mutableStateOf(false) }
    var cantidadArticulo by remember { mutableStateOf("1.0") }
    var articuloParaTraspaso by remember { mutableStateOf<ArticuloDto?>(null) }
    var ubicacionParaTraspaso by remember { mutableStateOf<Pair<String, String>?>(null) }

    // Nuevo: observar el traspaso pendiente y el artículo pendiente de mover
    val articuloPendienteMoverVM by viewModel.articuloPendienteMover.collectAsState()
    var mostrarDialogoMoverArticuloVM by remember { mutableStateOf(false) }
    var errorTraspasoArticulo by remember { mutableStateOf<String?>(null) }


    var mostrarDialogoExito by remember { mutableStateOf(false) }
    var mostrarDialogoErrorFinalizar by remember { mutableStateOf<String?>(null) }
    var mostrarDialogoTraspasoDirecto by remember { mutableStateOf(false) }
    //var traspasoMoverPaletPendiente by remember { mutableStateOf(false) }

    LaunchedEffect(Unit) {
        viewModel.cargarAlmacenesPermitidos(
            sessionViewModel = sessionViewModel,
            codigoEmpresa = empresa.toInt()
        )
    }

    Scaffold(
        topBar = {
            Box(Modifier.fillMaxWidth()) {
                AppTopBar(
                    sessionViewModel = sessionViewModel,
                    navController = navController,
                    title = ""
                )
            }
        }
    ) { padding ->
        if (DeviceUtils.isHoneywell) {
            Box(
                modifier = Modifier
                    .focusRequester(focusRequester)
                    .focusable()
                    .onPreviewKeyEvent { event ->
                        if (event.nativeKeyEvent?.action == android.view.KeyEvent.ACTION_MULTIPLE) {
                            if (escaneoProcesado) return@onPreviewKeyEvent true
                            escaneoProcesado = true

                            event.nativeKeyEvent.characters?.let { code ->
                                empresaId?.let { empId ->
                                    viewModel.procesarCodigoEscaneado(
                                        code = code.trim(),
                                        empresaId = empId,
                                        codigoAlmacen = null,
                                        codigoCentro = null,
                                        almacen = null,

                                        onUbicacionDetectada = { codAlm, codUbi ->
                                            ubicacionEscaneada = codAlm to codUbi
                                            escaneoProcesado = false
                                        },
                                        onPaletDetectado = { palet ->
                                            if (ubicacionEscaneada == null) {
                                                mostrarDialogoUbicacionPrimero = true
                                                escaneoProcesado = false
                                            } else {
                                                viewModel.validarUbicacionDePalet(
                                                    palet = palet,
                                                    ubicacionEscaneada = ubicacionEscaneada!!,
                                                    onValidado = {
                                                        viewModel.setPaletSeleccionado(palet)
                                                        viewModel.obtenerLineasDePalet(palet.id)
                                                        idPaletParaCerrar = palet.id
                                                        // NO se abre el diálogo aquí
                                                        escaneoProcesado = false
                                                    },
                                                    onError = { msg ->
                                                        mostrarDialogoErrorFinalizar = msg
                                                        escaneoProcesado = false
                                                    }
                                                )
                                            }
                                        },
                                                onArticuloDetectado = { articuloDto ->
                                            val loc = ubicacionEscaneada         // ← guarda la referencia
                                            if (loc == null) {
                                                mostrarDialogoUbicacionPrimero = true
                                                Log.e("TRASPASOS_UI", "Se ha escaneado un artículo sin ubicación. Mostrando diálogo de ubicación requerida.")
                                            } else if (paletEscaneado != null &&
                                                paletEscaneado!!.estado.equals("Abierto", ignoreCase = true) &&
                                                empresaId != null) {

                                                val (codAlm, codUbi) = loc      // ← ahora es seguro desestructurar
                                                viewModel.buscarStockYMostrar(
                                                    codigoArticulo      = articuloDto.codigoArticulo,
                                                    empresaId           = empresaId,
                                                    codigoAlmacen       = codAlm,
                                                    codigoUbicacion     = codUbi,
                                                    almacenesPermitidos = viewModel.almacenesPermitidos.value
                                                )
                                            } else {
                                                articuloPendiente        = articuloDto
                                                mostrarDialogoCrearPalet = true
                                            }
                                            escaneoProcesado = false
                                        },
                                        onMultipleArticulos = { articulos ->
                                            if (ubicacionEscaneada == null) {
                                                mostrarDialogoUbicacionPrimero = true
                                                Log.e("TRASPASOS_UI", "Se ha escaneado un artículo sin ubicación. Mostrando diálogo de ubicación requerida.")
                                            } else {
                                                viewModel.setArticulosFiltrados(articulos)
                                                viewModel.setMostrarDialogoSeleccion(true)
                                            }
                                            escaneoProcesado = false
                                        },
                                        onError = {
                                            escaneoProcesado = false
                                        }
                                    )
                                }
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

        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(
                    top = padding.calculateTopPadding(),
                    start = 16.dp,
                    end = 16.dp,
                    bottom = 16.dp
                )
                .verticalScroll(scroll),
            verticalArrangement = Arrangement.spacedBy(16.dp)
        ) {

            LaunchedEffect(paletCreado, triggerLineaPendiente) {
                val nuevoPalet = paletCreado
                if (triggerLineaPendiente && nuevoPalet != null && articuloPendiente != null) {
                    viewModel.setPaletSeleccionado(nuevoPalet)
                    crearPaletActivo = false
                    val (codAlm, codUbi) = ubicacionEscaneada ?: return@LaunchedEffect
                    viewModel.buscarStockYMostrar(
                        codigoArticulo = articuloPendiente!!.codigoArticulo,
                        empresaId = empresaId ?: return@LaunchedEffect,
                        codigoAlmacen    = codAlm,
                        codigoUbicacion  = codUbi,
                        almacenesPermitidos = viewModel.almacenesPermitidos.value
                    )

                    articuloPendiente = null
                    triggerLineaPendiente = false
                }
            }

            Text("Traspasos", style = MaterialTheme.typography.titleLarge)
            if (escaneando && !DeviceUtils.isHoneywell) {
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .background(MaterialTheme.colorScheme.background.copy(alpha = 0.85f)),
                    horizontalAlignment = Alignment.CenterHorizontally
                ) {
                    Text(
                        "Escaneando código de palet...",
                        style = MaterialTheme.typography.titleMedium
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

                            empresaId?.let { empId ->
                                viewModel.procesarCodigoEscaneado(
                                    code = code.trim(),
                                    empresaId = empId,
                                    codigoAlmacen = null,
                                    codigoCentro = null,
                                    almacen = null,

                                    onUbicacionDetectada = { codAlm, codUbi ->
                                        ubicacionEscaneada = codAlm to codUbi
                                        escaneoProcesado = false
                                    },
                                    onPaletDetectado = { palet ->
                                        if (ubicacionEscaneada == null) {
                                            mostrarDialogoUbicacionPrimero = true
                                            escaneoProcesado = false
                                        } else {
                                            viewModel.validarUbicacionDePalet(
                                                palet = palet,
                                                ubicacionEscaneada = ubicacionEscaneada!!,
                                                onValidado = {
                                                    viewModel.setPaletSeleccionado(palet)
                                                    viewModel.obtenerLineasDePalet(palet.id)
                                                    idPaletParaCerrar = palet.id
                                                    // NO se abre el diálogo aquí
                                                    escaneoProcesado = false
                                                },
                                                onError = { msg ->
                                                    mostrarDialogoErrorFinalizar = msg
                                                    escaneoProcesado = false
                                                }
                                            )
                                        }
                                    },
                                            onArticuloDetectado = { articuloDto ->
                                        val loc = ubicacionEscaneada
                                        if (loc == null) {
                                            mostrarDialogoUbicacionPrimero = true
                                            Log.e("TRASPASOS_UI", "Se ha escaneado un artículo sin ubicación. Mostrando diálogo de ubicación requerida.")
                                        } else if (
                                            paletEscaneado != null &&
                                            paletEscaneado!!.estado.equals("Abierto", ignoreCase = true) &&
                                            empresaId != null
                                        ) {
                                            val (codAlm, codUbi) = loc
                                            viewModel.buscarStockYMostrar(
                                                codigoArticulo      = articuloDto.codigoArticulo,
                                                empresaId           = empresaId,
                                                codigoAlmacen       = codAlm,
                                                codigoUbicacion     = codUbi,
                                                almacenesPermitidos = viewModel.almacenesPermitidos.value
                                            )
                                        } else {
                                            articuloPendiente = articuloDto
                                            mostrarDialogoCrearPalet = true
                                        }
                                        escaneoProcesado = false
                                    },
                                    onMultipleArticulos = { articulos ->
                                        if (ubicacionEscaneada == null) {
                                            mostrarDialogoUbicacionPrimero = true
                                            Log.e("TRASPASOS_UI", "Se ha escaneado un artículo sin ubicación. Mostrando diálogo de ubicación requerida.")
                                        } else {
                                            viewModel.setArticulosFiltrados(articulos)
                                            viewModel.setMostrarDialogoSeleccion(true)
                                        }
                                        escaneoProcesado = false
                                    },
                                    onError = {
                                        escaneoProcesado = false
                                    }
                                )
                            }
                        }
                    )

                    Spacer(modifier = Modifier.height(12.dp))
                    Button(onClick = { escaneando = false }) {
                        Text("Cancelar escaneo")
                    }
                    Spacer(Modifier.height(12.dp))
                }
            } else if (!DeviceUtils.isHoneywell) {
                Button(
                    onClick = {
                        escaneoProcesado = false
                        escaneando = true
                    },
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Text("Escanear Palet o Artículo")
                }
                Spacer(Modifier.height(12.dp))
            }

            when (ubicacionEscaneada) {
                null -> {
                    // Aún no hay ubicación
                    Text(
                        "Escanee una etiqueta de ubicación",
                        style = MaterialTheme.typography.titleMedium
                    )
                }
                else -> {
                    // Ya hay ubicación → muéstrala y cambia la instrucción
                    val (almacen, ubi) = ubicacionEscaneada!!
                    Text(
                        "Ubicación seleccionada: $almacen - $ubi",
                        style = MaterialTheme.typography.titleMedium
                    )
                    Spacer(Modifier.height(4.dp))
                    Text(
                        "Ahora escanee un palet o artículo",
                        style = MaterialTheme.typography.bodyMedium
                    )
                }
            }

            paletEscaneado?.let { palet ->
                Card(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(vertical = 4.dp),
                    elevation = CardDefaults.cardElevation(4.dp)
                ) {
                    Column(Modifier.padding(12.dp)) {
                        Text("📦 ${palet.codigoPalet}", style = MaterialTheme.typography.bodyLarge)
                        Text("Estado: ${palet.estado}")
                        Text("Tipo: ${palet.tipoPaletCodigo}")
                        Text("Orden: ${palet.ordenTrabajoId ?: "Sin orden"}")

                        val lineas = lineasPalet[palet.id] ?: emptyList()
                        val estaAbiertoInicial = palet.estado.equals("Abierto", ignoreCase = true)
                        //var estaAbierto by remember(palet.id) { mutableStateOf(estaAbiertoInicial) }
                        val estaAbierto = palet.estado.equals("Abierto", ignoreCase = true)
                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(bottom = 8.dp)
                        ) {
                            Text("Estado del palet:", style = MaterialTheme.typography.bodyMedium)
                            Spacer(Modifier.width(8.dp))
                            Switch(
                                checked = estaAbierto,
                                onCheckedChange = { nuevoEstado ->
                                    if (nuevoEstado) {
                                        // REABRIR
                                        viewModel.reabrirPalet(palet.id, usuarioId) {
                                            viewModel.obtenerPalet(palet.id) { viewModel.setPaletSeleccionado(it) }
                                            viewModel.obtenerLineasDePalet(palet.id)
                                        }
                                    } else {
                                        // CERRAR solo si el palet está realmente abierto
                                        if (palet.estado.equals("Abierto", ignoreCase = true)) {
                                            idPaletParaCerrar = palet.id
                                            mostrarDialogoCerrarPalet = true
                                        } else {
                                            mostrarDialogoErrorFinalizar = "El palet ya está cerrado."
                                        }
                                    }
                                }
                            )
                            Spacer(Modifier.width(8.dp))
                            Text(
                                if (estaAbierto) "Abierto" else "Cerrado",
                                style = MaterialTheme.typography.bodySmall
                            )
                        }

                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            modifier = Modifier
                                .fillMaxWidth()
                                .clickable { viewModel.clearPaletSeleccionado()}
                                .padding(bottom = 8.dp)
                        ) {
                            Icon(
                                imageVector = Icons.Default.ArrowBack,
                                contentDescription = "Cerrar detalle",
                                tint = MaterialTheme.colorScheme.primary
                            )
                            Spacer(Modifier.width(8.dp))
                            Text(
                                text = "Volver",
                                style = MaterialTheme.typography.bodyMedium,
                                color = MaterialTheme.colorScheme.primary
                            )
                        }

                        if (lineas.isEmpty()) {
                            Text(
                                "Este palet no tiene líneas.",
                                style = MaterialTheme.typography.bodySmall
                            )
                        } else {
                            LazyColumn(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .heightIn(max = 400.dp)
                            ) {
                                itemsIndexed(lineas) { index, linea ->
                                    Card(
                                        modifier = Modifier
                                            .fillMaxWidth()
                                            .padding(start = 12.dp, bottom = 8.dp),
                                        elevation = CardDefaults.cardElevation(2.dp),
                                        colors = CardDefaults.cardColors(containerColor = Color.White)
                                    ) {
                                        Box(Modifier.fillMaxWidth()) {
                                            Column(Modifier.padding(8.dp)) {
                                                Text("Artículo #${index + 1}", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.primary)
                                                Text("📦 ${linea.codigoArticulo} - ${linea.descripcion}", style = MaterialTheme.typography.bodyMedium)
                                                Text("Cantidad: ${linea.cantidad}", style = MaterialTheme.typography.bodySmall)
                                                Text("Lote: ${linea.lote ?: "Sin lote"}", style = MaterialTheme.typography.bodySmall)
                                                Text("Caducidad: ${linea.fechaCaducidad ?: "Sin fecha"}", style = MaterialTheme.typography.bodySmall)
                                                Text("Ubicación: ${linea.ubicacion ?: "Sin ubicación"}", style = MaterialTheme.typography.bodySmall)
                                            }

                                            if (palet.estado.equals("Abierto", ignoreCase = true)) {
                                                IconButton(
                                                    onClick = {
                                                        viewModel.eliminarLineaPalet(
                                                            idLinea = linea.id,
                                                            usuarioId = usuarioId,
                                                            paletId = palet.id
                                                        )
                                                    },
                                                    modifier = Modifier.align(Alignment.TopEnd)
                                                ) {
                                                    Icon(
                                                        imageVector = Icons.Default.Close,
                                                        contentDescription = "Eliminar línea",
                                                        tint = Color.Red
                                                    )
                                                }
                                            }
                                        }
                                    }

                                }
                            }
                        }
                        if (palet.estado.equals("Cerrado", ignoreCase = true)) {
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.SpaceBetween
                            ) {
                                IconButton(
                                    onClick = {
                                        paletParaImprimir = palet
                                        mostrarDialogoImpresion = true
                                    }
                                ) {
                                    Icon(Icons.Default.Print, contentDescription = "Imprimir etiqueta de palet")
                                }

                                IconButton(
                                    onClick = {
                                        mostrarDialogoTraspasoDirecto = true
                                    }
                                ) {
                                    Icon(
                                        imageVector = Icons.Default.SwapVert,
                                        contentDescription = "Traspasar palet cerrado",
                                        modifier = Modifier.graphicsLayer(rotationZ = 90f)
                                    )
                                }
                            }
                        }
                    }
                }
            }
            if (resultadoStock.isNotEmpty()) {
                StockSelectionCards(
                    stocks = resultadoStock,
                    botonLabel = "Añadir al palet",
                    onConfirm = { stock, cantidad ->
                        viewModel.anadirLinea(
                            idPalet = paletEscaneado!!.id,
                            dto = LineaPaletCrearDto(
                                codigoEmpresa = empresa,
                                usuarioId = usuarioId,
                                codigoArticulo = stock.codigoArticulo,
                                descripcion = stock.descripcionArticulo,
                                lote = stock.partida,
                                fechaCaducidad = stock.fechaCaducidad,
                                cantidad = cantidad,
                                codigoAlmacen = stock.codigoAlmacen,
                                ubicacion = stock.ubicacion
                            )
                        ) {
                            mostrarDialogoCantidad = false
                            articuloParaTraspaso = null
                            ubicacionParaTraspaso = null
                            cantidadArticulo = "1.0"
                            viewModel.limpiarStock()
                            ubicacionEscaneada = null
                            viewModel.clearUbicacionOrigen()
                        }
                    }
                )
            }

            if (crearPaletActivo) {
            /* ---- Selector de tipo de palet ---- */
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
                                tipoSeleccionado =
                                    tipo.codigoPalet       // ✅ guardas solo el código
                                dropOpen = false
                            }
                        )
                    }
                }
            }

            /* ---- Orden de trabajo (opcional) ---- */
            OutlinedTextField(
                value = ordenTrabajo,
                onValueChange = { ordenTrabajo = it },
                label = { Text("Orden de trabajo (opcional)") },
                modifier = Modifier.fillMaxWidth()
            )

            /* ---- Botón crear ---- */
            Button(
                onClick = {
                    viewModel.crearPalet(
                        PaletCrearDto(
                            codigoEmpresa = empresa,
                            usuarioAperturaId = usuarioId,
                            tipoPaletCodigo = tipoSeleccionado ?: return@Button,
                            ordenTrabajoId = ordenTrabajo.takeIf { it.isNotBlank() }
                        )
                    )
                    triggerLineaPendiente = true
                },
                enabled = tipoSeleccionado != null,
                modifier = Modifier.fillMaxWidth()
            ) {
                Text("Crear palet")
            }

            }
            if (mostrarDialogoCerrarPalet && idPaletParaCerrar != null) {
                val lineasDelPalet = lineasPalet[idPaletParaCerrar] ?: emptyList()

                // Campos para peso y altura
                var peso by remember { mutableStateOf("0") }
                var altura by remember { mutableStateOf("0") }

                AlertDialog(
                    onDismissRequest = { mostrarDialogoCerrarPalet = false },

                    title = { Text("Confirmar cierre de palet") },

                    text = {
                        Column(Modifier.heightIn(max = 300.dp)) {
                            Text(
                                "Antes de cerrar el palet, compruebe que los artículos y cantidades " +
                                        "mostradas coinciden con las reales.\n",
                                style = MaterialTheme.typography.bodyMedium
                            )
                            Spacer(Modifier.height(8.dp))

                            // Campos de peso y altura
                            OutlinedTextField(
                                value = peso,
                                onValueChange = { peso = it.filter { c -> c.isDigit() || c == '.' } },
                                label = { Text("Peso (kg) [opcional]") },
                                modifier = Modifier.fillMaxWidth(),
                                singleLine = true
                            )
                            Spacer(Modifier.height(8.dp))
                            OutlinedTextField(
                                value = altura,
                                onValueChange = { altura = it.filter { c -> c.isDigit() || c == '.' } },
                                label = { Text("Altura (cm) [opcional]") },
                                modifier = Modifier.fillMaxWidth(),
                                singleLine = true
                            )
                            Spacer(Modifier.height(8.dp))

                            LazyColumn(Modifier.fillMaxWidth()) {
                                // 👉 necesitas import androidx.compose.foundation.lazy.items
                                items(lineasDelPalet) { linea ->
                                    Column(Modifier.padding(vertical = 4.dp)) {
                                        Text(
                                            "${linea.codigoArticulo} - ${linea.descripcion ?: "Sin descripción"}",
                                            style = MaterialTheme.typography.bodySmall
                                        )
                                        Text(
                                            "Cantidad: ${linea.cantidad}, Lote: ${linea.lote ?: "—"}",
                                            style = MaterialTheme.typography.bodySmall
                                        )
                                        HorizontalDivider()
                                    }
                                }
                            }
                        }
                    },

                    confirmButton = {
                        TextButton(onClick = {
                            Log.d("CERRAR_PALET", "✅ Botón 'Sí' pulsado para cerrar palet")

                            mostrarDialogoCerrarPalet = false

                            val (codigoAlmacen, ubicacion) = ubicacionEscaneada
                                ?: run {
                                    Log.e("CERRAR_PALET", "❌ ubicacionEscaneada es null, no se puede cerrar el palet")
                                    return@TextButton
                                }

                            Log.d("CERRAR_PALET", """
        📤 Enviando datos a cerrarPalet:
        - Palet ID: $idPaletParaCerrar
        - Usuario ID: $usuarioId
        - Empresa: $empresa
        - Código Almacén: $codigoAlmacen
        - Ubicación Origen: $ubicacion
    """.trimIndent())

                            viewModel.cerrarPalet(
                                id = idPaletParaCerrar!!,
                                usuarioId = usuarioId,
                                codigoAlmacen = codigoAlmacen,
                                codigoEmpresa = empresa,
                                ubicacionOrigen = ubicacion,
                                onSuccess = { traspasoId ->
                                    Log.d("CERRAR_PALET", "✅ Palet cerrado correctamente. Traspaso ID: $traspasoId")
                                    traspasoPendienteId = traspasoId
                                    viewModel.setTraspasoEsDePalet(true)
                                    esperandoUbicacionDestino = true
                                    mostrarDialogoCerrarPalet = false
                                    idPaletParaCerrar = null
                                },
                                onError = {
                                    Log.e("CERRAR_PALET", "❌ Error al cerrar palet: $it")
                                    mostrarDialogoErrorFinalizar = it
                                }
                            )
                        }){
                            Text("Sí")
                        }
                    },
                    dismissButton = {
                        TextButton(onClick = {
                            mostrarDialogoCerrarPalet = false      // ← no se cierra
                            idPaletParaCerrar = null
                        }) { Text("No") }
                    }
                )
            }

            if (mostrarDialogoPaletCerrado) {
                AlertDialog(
                    onDismissRequest = { mostrarDialogoPaletCerrado = false },
                    confirmButton = {
                        TextButton(onClick = {
                            mostrarDialogoPaletCerrado = false
                        }) {
                            Text("OK")
                        }
                    },
                    title = { Text("Palet cerrado") },
                    text = {
                        Text("No se pueden añadir artículos a un palet cerrado. Escanee otro o reábralo para continuar.")
                    }
                )
            }
            if (mostrarDialogoSeleccion) {
                DialogSeleccionArticulo(
                    lista = articulosFiltrados,
                    onDismiss = { viewModel.setMostrarDialogoSeleccion(false) },
                    onSeleccion = { articuloSeleccionado ->
                        viewModel.setMostrarDialogoSeleccion(false)

                        val loc = ubicacionEscaneada
                        if (loc == null) {
                            mostrarDialogoUbicacionPrimero = true
                            Log.e(
                                "TRASPASOS_UI",
                                "Se ha escaneado un artículo sin ubicación. Mostrando diálogo de ubicación requerida."
                            )
                        } else {                                  // ← a partir de aquí loc es no-nulo
                            if (
                                paletEscaneado != null &&
                                paletEscaneado!!.estado.equals("Abierto", ignoreCase = true) &&
                                empresaId != null
                            ) {
                                val (codAlm, codUbi) = loc
                                viewModel.buscarStockYMostrar(
                                    codigoArticulo      = articuloSeleccionado.codigoArticulo,
                                    empresaId           = empresaId,
                                    codigoAlmacen       = codAlm,
                                    codigoUbicacion     = codUbi,
                                    almacenesPermitidos = viewModel.almacenesPermitidos.value
                                )
                            } else {
                                articuloPendiente        = articuloSeleccionado
                                mostrarDialogoCrearPalet = true
                            }
                        }
                    }
                )
            }
        }
    }
    ////***\\\
    if (mostrarDialogoTraspasoDirecto && paletEscaneado != null) {
        AlertDialog(
            onDismissRequest = { mostrarDialogoTraspasoDirecto = false },
            title = { Text("¿Desea realizar un traspaso de este palet?") },
            confirmButton = {
                TextButton(onClick = {
                    mostrarDialogoTraspasoDirecto = false

                    val palet = paletEscaneado!!
                    val fechaAhora = LocalDateTime.now()

                    val dto = MoverPaletDto(
                        paletId = palet.id,
                        usuarioId = usuarioId,
                        codigoPalet = palet.codigoPalet,
                        codigoEstado = "PENDIENTE",
                        codigoEmpresa = empresa,
                        fechaInicio = fechaAhora.toString(),
                        tipoTraspaso = "PALET",
                        comentario = null
                    )
                    viewModel.moverPalet(
                        dto = dto,
                        onSuccess = {
                            idPaletParaCerrar = null
                            esperandoUbicacionDestino = true
                            viewModel.setTraspasoEsDePalet(true)
                            viewModel.setTraspasoDirectoDesdePaletCerrado(true)
                        },
                        onError = { mostrarDialogoErrorFinalizar = it }
                    )
                }) {
                    Text("Sí")
                }
            },
            dismissButton = {
                TextButton(onClick = {
                    mostrarDialogoTraspasoDirecto = false
                }) {
                    Text("No")
                }
            }
        )
    }

    if (mostrarDialogoCrearPalet && articuloPendiente != null) {
        AlertDialog(
            // No dejamos cerrar tocando fuera; obligamos a escoger
            onDismissRequest = { /* vacío a propósito */ },

            title = { Text("Crear nuevo palet") },

            text = {
                Text(
                    "Has escaneado un artículo pero no hay ningún palet activo.\n\n" +
                            "• Pulsa Sí para crear un palet nuevo y añadirlo.\n" +
                            "• Pulsa No para mantener el artículo seleccionado y traspasarlo.\n" +
                            "• Pulsa Cancelar para descartar el escaneo."
                )
            },

            // --- 🔑 AQUÍ VAN LOS TRES BOTONES ---
            confirmButton = {
                Row(
                    horizontalArrangement = Arrangement.spacedBy(12.dp),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    // 1️⃣ SÍ -> Crea el palet
                    TextButton(
                        modifier = Modifier.weight(1f),
                        onClick = {
                            crearPaletActivo = true            // activará el flujo de creación
                            mostrarDialogoCrearPalet = false
                        }
                    ) { Text("Sí") }

                    // 2️⃣ NO -> Guarda artículo para traspaso
                    TextButton(
                        modifier = Modifier.weight(1f),
                        onClick = {
                            Log.d("TRASPASOS_UI", "Botón NO pulsado. Artículo: $articuloPendiente, Ubicación: $ubicacionEscaneada")
                            val art = articuloPendiente
                            val ubicOrigen = ubicacionEscaneada
                            if (ubicOrigen == null) {
                                mostrarDialogoUbicacionPrimero = true
                                Log.e("TRASPASOS_UI", "Intento de crear traspaso sin ubicación. Mostrando diálogo de ubicación requerida.")
                                return@TextButton
                            }
                            if (art != null) {
                                articuloParaTraspaso = art
                                ubicacionParaTraspaso = ubicOrigen
                                // Consultar stock solo por artículo y partida/lote
                                viewModel.buscarStockYMostrar(
                                    codigoArticulo = art.codigoArticulo,
                                    empresaId = empresaId ?: return@TextButton,
                                    codigoAlmacen = ubicOrigen.first,
                                    codigoUbicacion = ubicOrigen.second,
                                    partida = art.partida,
                                    almacenesPermitidos = null
                                )
                                mostrarDialogoCantidad = true
                                mostrarDialogoCrearPalet = false
                            } else {
                                Log.e("TRASPASOS_UI", "No se puede crear traspaso: art=$art, ubic=$ubicOrigen")
                            }
                            articuloPendiente = null
                        },
                        enabled = true
                    ) { Text("No") }

                    // 3️⃣ CANCELAR -> Descarta completamente
                    TextButton(
                        modifier = Modifier.weight(1f),
                        onClick = {
                            articuloPendiente = null           // olvidamos el artículo
                            mostrarDialogoCrearPalet = false   // cerramos sin más acciones
                        }
                    ) { Text("Cancelar") }
                }
            },

            // No usamos dismissButton porque ya tenemos los tres dentro del Row
            dismissButton = {}
        )
    }

    if (mostrarDialogoUbicacionPrimero) {
        AlertDialog(
            onDismissRequest = { mostrarDialogoUbicacionPrimero = false },
            confirmButton = { TextButton(onClick = { mostrarDialogoUbicacionPrimero = false }) { Text("OK") } },
            title = { Text("Ubicación requerida") },
            text  = { Text("Escanee primero la ubicación del palet o del artículo.") }
        )
    }
    if (mostrarDialogoMoverArticulo && articuloPendienteMover != null) {
        AlertDialog(
            onDismissRequest = { /* bloqueamos salida para forzar decisión */ },
            title = { Text("Artículo pendiente") },
            text = {
                val codigo = articuloPendienteMover?.codigoArticulo ?: "—"
                Text("Artículo $codigo en memoria. Escanee ubicación destino o cancele.")
            },
            confirmButton = { TextButton(onClick = { mostrarDialogoCancelarArticulo = true }) { Text("Cancelar") } },
            dismissButton = { } // No botón “Cerrar” para que no se quede colgado
        )
    }
    if (mostrarDialogoCancelarArticulo) {
        AlertDialog(
            onDismissRequest = { mostrarDialogoCancelarArticulo = false },
            title = { Text("Cancelar artículo") },
            text  = { Text("¿Desea descartar el artículo escaneado?") },
            confirmButton = {
                TextButton(onClick = {
                    articuloPendienteMover = null
                    mostrarDialogoMoverArticulo = false
                    mostrarDialogoCancelarArticulo = false
                }) { Text("Sí") }
            },
            dismissButton = {
                TextButton(onClick = { mostrarDialogoCancelarArticulo = false }) { Text("No") }
            }
        )
    }

    if (mostrarDialogoImpresion && paletParaImprimir != null) {
        AlertDialog(
            onDismissRequest = { mostrarDialogoImpresion = false },
            title = { Text("Imprimir etiqueta de palet") },
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
                                IconButton(onClick = { dropOpenImpresora  = !dropOpenImpresora  }) {
                                    Icon(Icons.Default.ArrowDropDown, contentDescription = null)
                                }
                            }
                        )
                        DropdownMenu(
                            expanded = dropOpenImpresora ,
                            onDismissRequest = { dropOpenImpresora = false }
                        ) {
                            impresoras.forEach { imp ->
                                DropdownMenuItem(
                                    text = { Text(imp.nombre) },
                                    onClick = {
                                        dropOpen = false
                                        sessionViewModel.actualizarImpresora(imp.nombre)
                                        viewModel.actualizarImpresoraSeleccionadaEnBD(imp.nombre, sessionViewModel)
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
            },
            confirmButton = {
                TextButton(onClick = {
                    val usuario = sessionViewModel.user.value?.name ?: return@TextButton
                    val dispositivo = sessionViewModel.dispositivo.value?.id ?: return@TextButton
                    val impresora = impresoras.find { it.nombre == impresoraNombre } ?: return@TextButton

                    val dto = LogImpresionDto(
                        usuario = usuario,
                        dispositivo = dispositivo,
                        idImpresora = impresora.id,
                        etiquetaImpresa = 2,
                        codigoArticulo = paletParaImprimir!!.codigoPalet,
                        descripcionArticulo = "Etiqueta palet",
                        copias = copias,
                        codigoAlternativo = "",
                        fechaCaducidad = null,
                        partida = null,
                        alergenos = null,
                        pathEtiqueta = "\\\\Null"
                    )
                    viewModel.imprimirEtiquetaPalet(dto)
                    mostrarDialogoImpresion = false
                }) {
                    Text("Imprimir")
                }
            },
            dismissButton = {
                TextButton(onClick = { mostrarDialogoImpresion = false }) {
                    Text("Cancelar")
                }
            }
        )
    }

    // Mostrar el diálogo de espera solo si hay artículo pendiente en el ViewModel y el flag local está activo
    if (mostrarDialogoMoverArticuloVM && articuloPendienteMoverVM != null) {
        AlertDialog(
            onDismissRequest = { /* bloqueamos salida para forzar decisión */ },
            title = { Text("Artículo pendiente") },
            text = {
                val codigo = articuloPendienteMoverVM?.codigoArticulo ?: "—"
                Text("Artículo $codigo en memoria. Escanee ubicación destino o cancele.")
            },
            confirmButton = { TextButton(onClick = { mostrarDialogoCancelarArticulo = true }) { Text("Cancelar") } },
            dismissButton = { } // No botón “Cerrar” para que no se quede colgado
        )
    }
    // Mostrar error si lo hay
    if (errorTraspasoArticulo != null) {
        AlertDialog(
            onDismissRequest = { errorTraspasoArticulo = null },
            title = { Text("Error") },
            text = { Text(errorTraspasoArticulo!!) },
            confirmButton = { TextButton(onClick = { errorTraspasoArticulo = null }) { Text("OK") } },
            dismissButton = {}
        )
    }

    if (mostrarDialogoCantidad && articuloParaTraspaso != null) {
        val stocks = viewModel.resultadoStock.collectAsState().value.filter {
            it.codigoArticulo == articuloParaTraspaso!!.codigoArticulo &&
            (articuloParaTraspaso!!.partida == null || it.partida == articuloParaTraspaso!!.partida)
        }
        val partidaEscaneada = articuloParaTraspaso!!.partida
        val fechaCaducidadEscaneada = articuloParaTraspaso!!.fechaCaducidad
        if (stocks.isNotEmpty()) {
            AlertDialog(
                onDismissRequest = { /* vacío a propósito */ },
                title = { Text("Cantidad a traspasar") },
                text = {
                    StockSelectionCards(
                        stocks = stocks,
                        botonLabel = "Traspasar artículo",
                        onConfirm = { stock, cantidad ->
                            if (partidaEscaneada == null) {
                                Log.e("TRASPASOS_UI", "ERROR: partida es null antes del POST. No se enviará el traspaso.")
                                return@StockSelectionCards
                            }
                            viewModel.crearTraspasoArticulo(
                                dto = CrearTraspasoArticuloDto(
                                    almacenOrigen = stock.codigoAlmacen,
                                    ubicacionOrigen = stock.ubicacion ?: "",
                                    codigoArticulo = stock.codigoArticulo,
                                    cantidad = cantidad,
                                    usuarioId = usuarioId,
                                    partida = partidaEscaneada,
                                    fechaCaducidad = fechaCaducidadEscaneada,
                                    finalizar = false,
                                ),
                                onSuccess = {
                                    Log.d("TRASPASOS_UI", "POST traspaso artículo OK. ID guardado.")
                                    viewModel.setArticuloPendienteMover(articuloParaTraspaso)
                                    traspasoPendienteId = it
                                    articuloPendienteMoverLocal = articuloParaTraspaso
                                    esperandoUbicacionDestino = true
                                    mostrarDialogoMoverArticuloVM = true  // ← mantener visible el diálogo bloqueante
                                },
                                onError = { msg ->
                                    Log.e("TRASPASOS_UI", "Error en POST traspaso artículo: $msg")
                                    errorTraspasoArticulo = msg
                                    mostrarDialogoMoverArticuloVM = false
                                }
                            )
                            mostrarDialogoCantidad = false
                            articuloParaTraspaso = null
                            ubicacionParaTraspaso = null
                            cantidadArticulo = "1.0"
                            viewModel.limpiarStock()
                        }
                    )
                },
                confirmButton = {
                    TextButton(
                        onClick = {
                            mostrarDialogoCantidad = false
                            articuloParaTraspaso = null
                            ubicacionParaTraspaso = null
                            cantidadArticulo = "1.0"
                            viewModel.limpiarStock()
                        }
                    ) {
                        Text("Cancelar")
                    }
                },
                dismissButton = null
            )
        }
    }

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
    }

    // Escaneo de ubicación destino (Honeywell físico)
    if (esperandoUbicacionDestino && DeviceUtils.isHoneywell) {
        Box(
            modifier = Modifier
                .fillMaxSize()
                .focusRequester(focusRequester)
                .focusable()
                .onPreviewKeyEvent { event ->
                    if (event.nativeKeyEvent?.action == android.view.KeyEvent.ACTION_MULTIPLE) {
                        event.nativeKeyEvent.characters?.let { code ->
                            Log.d("ESCANEO_DESTINO", "📥 Código escaneado: '$code'")
                            val trimmed = code.trim()

                            Log.d("ESCANEO_DESTINO", "📦 Almacenes permitidos: ${viewModel.almacenesPermitidos.value}")
                            val (almacenDestino, ubicacionDestino) = if (trimmed.contains('$')) {
                                val parts = trimmed.split('$')
                                if (parts.size == 2) parts[0] to parts[1] else return@onPreviewKeyEvent false
                            } else {
                                "PR" to trimmed
                            }

                            Log.d("ESCANEO_DESTINO", "✅ Interpretado -> Almacén: $almacenDestino | Ubicación: $ubicacionDestino")

                            if (viewModel.almacenesPermitidos.value.contains(almacenDestino)) {
                                Log.d("ESCANEO_DESTINO", "🟢 Ubicación permitida")
                                Log.d("PUT", "DTO -> almacen: $almacenDestino | ubicacion: $ubicacionDestino | user: $usuarioId | id: $traspasoPendienteId ")

                                if (viewModel.traspasoEsDePalet.value && viewModel.traspasoDirectoDesdePaletCerrado.value) {
                                    Log.d("ESCANEO_DESTINO", "🚚 Finalizando traspaso directo desde palet cerrado")

                                    val dto = FinalizarTraspasoPaletDto(
                                        almacenDestino = almacenDestino,
                                        ubicacionDestino = ubicacionDestino,
                                        usuarioFinalizacionId = usuarioId,
                                        codigoEstado = "PENDIENTE_ERP"
                                    )
                                    Log.d("ESCANEO_DESTINO", "🚚 $dto")
                                    viewModel.finalizarTraspasoPalet(
                                        traspasoId = viewModel.traspasoPendienteId.value ?: return@onPreviewKeyEvent false,
                                        dto = dto,
                                        onSuccess = {
                                            Log.d("ESCANEO_DESTINO", "✅ finalizado correctamente")
                                            esperandoUbicacionDestino = false
                                            mostrarDialogoExito = true
                                            idPaletParaCerrar = null
                                            traspasoPendienteId = null
                                            ubicacionEscaneada = null
                                            viewModel.setTraspasoEsDePalet(false)
                                            viewModel.setTraspasoDirectoDesdePaletCerrado(false)
                                        },
                                        onError = { msg ->
                                            Log.d("ESCANEO_DESTINO", "❌ Error: $msg")
                                            mostrarDialogoErrorFinalizar = msg
                                        }
                                    )
                                } else if (viewModel.traspasoEsDePalet.value) {
                                    Log.d("ESCANEO_DESTINO", "🟡 Finalizando traspaso desde palet (no directo)")

                                    viewModel.completarTraspaso(
                                        id = traspasoPendienteId!!,
                                        codigoAlmacenDestino = almacenDestino,
                                        ubicacionDestino = ubicacionDestino,
                                        usuarioId = usuarioId,
                                        onSuccess = {
                                            Log.d("ESCANEO_DESTINO", "✅ completarTraspaso ok")
                                            esperandoUbicacionDestino = false
                                            mostrarDialogoExito = true
                                            mostrarDialogoMoverArticuloVM = false
                                            mostrarDialogoMoverArticulo = false
                                            articuloPendienteMover = null
                                            paletEscaneado?.id?.let { id ->
                                                viewModel.obtenerPalet(id) { viewModel.setPaletSeleccionado(it) }
                                                viewModel.obtenerLineasDePalet(id)
                                            }
                                            traspasoPendienteId = null
                                            articuloPendienteMoverLocal = null
                                            viewModel.setTraspasoEsDePalet(false)
                                            viewModel.clearPaletSeleccionado()
                                            ubicacionEscaneada = null
                                        },
                                        onError = { msg ->
                                            Log.d("ESCANEO_DESTINO", "❌ completarTraspaso ERROR: $msg")
                                            mostrarDialogoErrorFinalizar = msg
                                        }
                                    )
                                } else {
                                    Log.d("ESCANEO_DESTINO", "🧪 Finalizando traspaso de artículo")
                                    viewModel.finalizarTraspasoArticulo(
                                        id = traspasoPendienteId!!,
                                        dto = FinalizarTraspasoArticuloDto(
                                            almacenDestino = almacenDestino,
                                            ubicacionDestino = ubicacionDestino,
                                            usuarioId = usuarioId
                                        ),
                                        onSuccess = {
                                            Log.d("ESCANEO_DESTINO", "✅ finalizarTraspasoArticulo ok")
                                            esperandoUbicacionDestino = false
                                            mostrarDialogoExito = true
                                            mostrarDialogoMoverArticuloVM = false
                                            mostrarDialogoMoverArticulo = false
                                            articuloPendienteMover = null
                                            traspasoPendienteId = null
                                            articuloPendienteMoverLocal = null
                                            viewModel.clearPaletSeleccionado()
                                            ubicacionEscaneada = null
                                        },
                                        onError = { msg ->
                                            Log.d("ESCANEO_DESTINO", "❌ finalizarTraspasoArticulo ERROR: $msg")
                                            mostrarDialogoErrorFinalizar = msg
                                        }
                                    )
                                }
                            } else {
                                Log.d("ESCANEO_DESTINO", "❌ Ubicación '$almacenDestino' no permitida")
                                mostrarDialogoErrorFinalizar = "Ubicación no permitida."
                            }
                        }
                        true
                    } else false
                }
        ) {}

        LaunchedEffect(esperandoUbicacionDestino) {
            if (esperandoUbicacionDestino) {
                Log.d("ESCANEO_DESTINO", "📌 Lanzando focusRequester")
                focusRequester.requestFocus()
            }
        }
    }

    // Escaneo de ubicación destino (QRScannerView)
    if (esperandoUbicacionDestino && !DeviceUtils.isHoneywell) {
        QRScannerView(
            modifier = Modifier.fillMaxWidth().height(250.dp),
            onCodeScanned = { code ->
                if (!esperandoUbicacionDestino) return@QRScannerView
                val trimmed = code.trim()
                val (almacenDestino, ubicacionDestino) = if (trimmed.contains('$')) {
                    val parts = trimmed.split('$')
                    val esUbicacion = trimmed.startsWith("UBI", ignoreCase = true) || trimmed.contains('$')
                    if (!esUbicacion) {
                        mostrarDialogoErrorFinalizar = "Escanee primero una **ubicación** válida."
                        return@QRScannerView
                    }
                    if (parts.size == 2) parts[0] to parts[1] else return@QRScannerView
                } else {
                    "PR" to trimmed
                }
                if (viewModel.almacenesPermitidos.value.contains(almacenDestino)) {
                    esperandoUbicacionDestino = false
                    if (viewModel.traspasoEsDePalet.value) {
                        viewModel.completarTraspaso(
                            id = traspasoPendienteId!!,
                            codigoAlmacenDestino = almacenDestino,
                            ubicacionDestino = ubicacionDestino,
                            usuarioId = usuarioId,
                            onSuccess = {
                                esperandoUbicacionDestino = false
                                mostrarDialogoExito = true
                                mostrarDialogoMoverArticuloVM = false
                                mostrarDialogoMoverArticulo = false
                                articuloPendienteMover = null
                                // limpiar estados
                                traspasoPendienteId = null
                                articuloPendienteMoverLocal = null
                                viewModel.setTraspasoEsDePalet(false)
                            },
                            onError = { msg ->
                                mostrarDialogoErrorFinalizar = msg
                            }
                        )
                    } else {
                        viewModel.finalizarTraspasoArticulo(
                            id = traspasoPendienteId!!,
                            dto = FinalizarTraspasoArticuloDto(
                                almacenDestino = almacenDestino,
                                ubicacionDestino = ubicacionDestino,
                                usuarioId = usuarioId
                            ),
                            onSuccess = {
                                esperandoUbicacionDestino = false
                                mostrarDialogoExito = true
                                mostrarDialogoMoverArticuloVM = false
                                mostrarDialogoMoverArticulo = false
                                articuloPendienteMover = null
                                // limpiar estados
                                traspasoPendienteId = null
                                articuloPendienteMoverLocal = null
                            },
                            onError = { msg ->
                                mostrarDialogoErrorFinalizar = msg
                            }
                        )
                    }
                }   else {
                    mostrarDialogoErrorFinalizar = "Ubicación no permitida."
                }
            }
        )
    }

    // Mensaje de éxito
    if (mostrarDialogoExito) {
        AlertDialog(
            onDismissRequest = {
                mostrarDialogoExito = false
                // limpiar todos los estados y navegar a pantalla principal
                navController.popBackStack(navController.graph.startDestinationId, false)
            },
            title = { Text("Traspaso realizado") },
            text = { Text("Traspaso realizado con éxito.") },
            confirmButton = {
                TextButton(onClick = {
                    mostrarDialogoExito = false
                    navController.popBackStack(navController.graph.startDestinationId, false)
                }) { Text("Aceptar") }
            },
            dismissButton = null
        )
    }

    // Mensaje de error
    if (mostrarDialogoErrorFinalizar != null) {
        AlertDialog(
            onDismissRequest = { mostrarDialogoErrorFinalizar = null },
            title = { Text("Error") },
            text = { Text(mostrarDialogoErrorFinalizar!!) },
            confirmButton = {
                TextButton(onClick = { mostrarDialogoErrorFinalizar = null }) { Text("OK") }
            },
            dismissButton = null
        )
    }
    if (esperandoUbicacionParaCerrar && idPaletParaCerrar != null) {
        // Overlay visual
        Box(
            modifier = Modifier
                .fillMaxSize()
                .background(Color.Black.copy(alpha = 0.5f))
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
                Text("Escanee la ubicación destino", style = MaterialTheme.typography.titleLarge)
                Spacer(modifier = Modifier.height(8.dp))
                Text("Para cerrar el palet, escanee una ubicación válida de destino.")
            }
        }

        if (esperandoUbicacionDestino && DeviceUtils.isHoneywell) {
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .focusRequester(focusRequester)
                    .focusable()
                    .onPreviewKeyEvent { event ->
                        if (event.nativeKeyEvent?.action == android.view.KeyEvent.ACTION_MULTIPLE) {
                            val code = event.nativeKeyEvent.characters ?: return@onPreviewKeyEvent false
                            val trimmed = code.trim()

                            val (almacenDestino, ubicacionDestino) = if (trimmed.contains('$')) {
                                val parts = trimmed.split('$')
                                if (parts.size == 2) parts[0] to parts[1] else return@onPreviewKeyEvent false
                            } else {
                                "PR" to trimmed
                            }

                            if (viewModel.almacenesPermitidos.value.contains(almacenDestino)) {
                                viewModel.completarTraspaso(
                                    id = traspasoPendienteId!!,
                                    codigoAlmacenDestino = almacenDestino,
                                    ubicacionDestino = ubicacionDestino,
                                    usuarioId = usuarioId,
                                    onSuccess = {
                                        esperandoUbicacionDestino = false
                                        mostrarDialogoExito = true
                                        mostrarDialogoMoverArticuloVM = false
                                        mostrarDialogoMoverArticulo = false
                                        articuloPendienteMover = null
                                        traspasoPendienteId = null
                                        articuloPendienteMoverLocal = null
                                    },
                                    onError = { msg ->
                                        mostrarDialogoErrorFinalizar = msg
                                    }
                                )
                            } else {
                                mostrarDialogoErrorFinalizar = "Ubicación no permitida."
                            }
                            true
                        } else false
                    }
            ) {}
            LaunchedEffect(esperandoUbicacionDestino) {
                focusRequester.requestFocus()
            }
        }
        if (esperandoUbicacionDestino && !DeviceUtils.isHoneywell) {
            QRScannerView(
                modifier = Modifier.fillMaxWidth().height(250.dp),
                onCodeScanned = { code ->
                    val trimmed = code.trim()
                    val (almacenDestino, ubicacionDestino) = if (trimmed.contains('$')) {
                        val parts = trimmed.split('$')
                        if (parts.size == 2) parts[0] to parts[1] else return@QRScannerView
                    } else {
                        "PR" to trimmed
                    }

                    if (viewModel.almacenesPermitidos.value.contains(almacenDestino)) {
                        viewModel.completarTraspaso(
                            id = traspasoPendienteId!!,
                            codigoAlmacenDestino = almacenDestino,
                            ubicacionDestino = ubicacionDestino,
                            usuarioId = usuarioId,
                            onSuccess = {
                                esperandoUbicacionDestino = false
                                mostrarDialogoExito = true
                                mostrarDialogoMoverArticuloVM = false
                                mostrarDialogoMoverArticulo = false
                                articuloPendienteMover = null
                                traspasoPendienteId = null
                                articuloPendienteMoverLocal = null
                            },
                            onError = { msg ->
                                mostrarDialogoErrorFinalizar = msg
                            }
                        )
                    } else {
                        mostrarDialogoErrorFinalizar = "Ubicación no permitida."
                    }
                }
            )
        }

    }

}

