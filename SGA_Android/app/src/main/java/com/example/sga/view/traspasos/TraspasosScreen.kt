package com.example.sga.view.traspasos

import android.util.Log
import androidx.activity.compose.BackHandler
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.foundation.focusable
import androidx.compose.foundation.interaction.MutableInteractionSource
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
import androidx.compose.material.icons.filled.Error
import androidx.compose.material.icons.filled.Print
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material.icons.filled.Remove
import androidx.compose.material.icons.filled.Lock
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.DialogProperties
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.navigation.NavHostController
import com.example.sga.data.dto.traspasos.PaletCrearDto
import com.example.sga.view.app.SessionViewModel
import com.example.sga.view.components.AppTopBar
import androidx.compose.ui.Alignment
import androidx.compose.ui.focus.FocusRequester
import com.example.sga.data.dto.traspasos.PaletDto
import com.example.sga.service.lector.DeviceUtils
import androidx.compose.ui.platform.LocalContext
import com.example.sga.service.scanner.QRScannerView
import androidx.compose.ui.layout.layout
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.input.key.onPreviewKeyEvent
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
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
import androidx.compose.material.icons.filled.Search
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.zIndex
import com.example.sga.data.dto.traspasos.FinalizarTraspasoPaletDto
import java.time.LocalDateTime
import androidx.compose.ui.platform.LocalContext
import com.example.sga.data.dto.traspasos.LineaPaletDto
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.async
import kotlinx.coroutines.awaitAll
import com.example.sga.utils.SoundUtils
import com.example.sga.utils.FormatUtils

enum class ImpresionTipo {
    PALET, ARTICULO
}

@Composable
fun StockSelectionCards(
    stocks: List<Stock>,
    botonLabel: String,
    onConfirm: (stock: Stock, cantidad: Double) -> Unit,
    viewModel: TraspasosViewModel,
    usuarioId: Int,
    empresa: Short,
    almacenesPermitidos: List<String>,
    onScanPalet: (() -> Unit)? = null,
    mostrarVolver: Boolean = false,
    onVolver: (() -> Unit)? = null,
    paletEscaneadoEspecifico: Stock? = null
) {
   Text(
        "Selecciona cantidad",
        style = MaterialTheme.typography.titleMedium
    )

    // Separar stock por tipo para mostrar todo
    val stockSuelto = stocks.filter { stock -> stock.tipoStock == "Suelto" }
    val paletsAbiertos = stocks.filter { stock ->
        stock.tipoStock == "Paletizado" && stock.estadoPalet?.equals("Abierto", ignoreCase = true) == true
    }
    val paletsCerrados = stocks.filter { stock ->
        stock.tipoStock == "Paletizado" && stock.estadoPalet?.equals("Cerrado", ignoreCase = true) == true
    }

    // Agrupar por artículo para mostrar mejor
    val stockAgrupado = stocks.groupBy { stock ->
        "${stock.codigoArticulo}-${stock.partida}-${stock.fechaCaducidad}"
    }

    if (stockSuelto.isEmpty() && paletsAbiertos.isEmpty() && paletsCerrados.isEmpty()) {

        Card(
            modifier = Modifier
                .fillMaxWidth()
                .padding(vertical = 4.dp),
            elevation = CardDefaults.cardElevation(2.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.errorContainer)
        ) {
            Column(Modifier.padding(12.dp)) {
                Text(
                    "⚠️ No hay stock disponible",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onErrorContainer
                )

                if (paletsCerrados.isNotEmpty()) {
                    if (paletsCerrados.size == 1) {
                        val paletCerrado = paletsCerrados.first()
                        Text(
                            "El artículo está en el palet ${paletCerrado.codigoPalet ?: "desconocido"} que está Cerrado.",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onErrorContainer
                        )
                        Spacer(modifier = Modifier.height(8.dp))

                        // Switch para reabrir el palet
                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            modifier = Modifier.fillMaxWidth()
                        ) {
                            Text(
                                "Reabrir ${paletCerrado.codigoPalet ?: "desconocido"}",
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onErrorContainer
                            )
                            Spacer(modifier = Modifier.width(8.dp))
                            Switch(
                                checked = false, // Siempre false porque está cerrado
                                onCheckedChange = { isChecked ->
                                    if (isChecked) {
                                        // Reabrir el palet
                                        paletCerrado.paletId?.let { paletId ->
                                            viewModel.reabrirPalet(paletId, usuarioId) {
                                                // Actualizar el stock después de reabrir
                                                viewModel.buscarStockYMostrar(
                                                    codigoArticulo = paletCerrado.codigoArticulo,
                                                    empresaId = empresa,
                                                    codigoAlmacen = paletCerrado.codigoAlmacen,
                                                    codigoUbicacion = paletCerrado.ubicacion,
                                                    almacenesPermitidos = viewModel.almacenesPermitidos.value,
                                                    partida = paletCerrado.partida
                                                )
                                            }
                                        }
                                    }
                                }
                            )
                        }
                    } else {
                        Text(
                            "El artículo está en los siguientes palets cerrados:",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onErrorContainer
                        )
                        Spacer(modifier = Modifier.height(8.dp))

                        // Mostrar cada palet con su propio switch
                        paletsCerrados.forEach { palet ->
                            Row(
                                verticalAlignment = Alignment.CenterVertically,
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .padding(vertical = 4.dp)
                            ) {
                                Text(
                                    "Palet ${palet.codigoPalet ?: "desconocido"}:",
                                    style = MaterialTheme.typography.bodyMedium
                                )
                                Spacer(modifier = Modifier.width(8.dp))
                                Switch(
                                    checked = false, // Siempre false porque está cerrado
                                    onCheckedChange = { isChecked ->
                                        if (isChecked) {
                                            // Reabrir este palet específico
                                            palet.paletId?.let { paletId ->
                                                viewModel.reabrirPalet(paletId, usuarioId) {
                                                    // Actualizar el stock después de reabrir
                                                    viewModel.buscarStockYMostrar(
                                                        codigoArticulo = palet.codigoArticulo,
                                                        empresaId = empresa,
                                                        codigoAlmacen = palet.codigoAlmacen,
                                                        codigoUbicacion = palet.ubicacion,
                                                        almacenesPermitidos = viewModel.almacenesPermitidos.value,
                                                        partida = palet.partida
                                                    )
                                                }
                                            }
                                        }
                                    }
                                )
                                Spacer(modifier = Modifier.width(8.dp))
                                Text(
                                    if (false) "Abierto" else "Cerrado",
                                    style = MaterialTheme.typography.bodySmall
                                )
                            }
                        }

                        Spacer(modifier = Modifier.height(8.dp))
                        Text(
                            "Activa el switch del palet que quieres reabrir para usar su stock.",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onErrorContainer
                        )
                    }
                } else {
                    Text(
                        "El artículo está solo disponible en palets cerrados y no se puede usar.",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onErrorContainer
                    )
                }
            }
        }
        return
    }

    // Mostrar cada grupo de stock (artículo + partida + fecha)
    stockAgrupado.forEach { (clave, stocksDelArticulo) ->
        val stockPrincipal = stocksDelArticulo.first()
        val cantidadInput = remember { mutableStateOf(FormatUtils.formatearCantidad(stockPrincipal.unidadesSaldo)) }

        // Separar por tipo dentro del grupo
        val sueltosDelArticulo = stocksDelArticulo.filter { it.tipoStock == "Suelto" }
        val abiertosDelArticulo = stocksDelArticulo.filter {
            it.tipoStock == "Paletizado" && it.estadoPalet?.equals("Abierto", ignoreCase = true) == true
        }
        val cerradosDelArticulo = stocksDelArticulo.filter {
            it.tipoStock == "Paletizado" && it.estadoPalet?.equals("Cerrado", ignoreCase = true) == true
        }

        val tieneStockDisponible = sueltosDelArticulo.isNotEmpty() || abiertosDelArticulo.isNotEmpty()

        Card(
            modifier = Modifier
                .fillMaxWidth()
                .padding(vertical = 4.dp),
            elevation = CardDefaults.cardElevation(2.dp),
            colors = CardDefaults.cardColors(
                containerColor = if (tieneStockDisponible) MaterialTheme.colorScheme.surface else MaterialTheme.colorScheme.surfaceVariant
            )
        ) {
            Column(Modifier.padding(12.dp)) {
                Text("${stockPrincipal.codigoArticulo} - ${stockPrincipal.descripcionArticulo}", style = MaterialTheme.typography.bodyMedium)

                // Mostrar stock suelto si hay
                if (sueltosDelArticulo.isNotEmpty()) {
                    Spacer(modifier = Modifier.height(4.dp))
                    Text("📦 Stock suelto disponible:", style = MaterialTheme.typography.bodySmall, fontWeight = FontWeight.Bold)
                    sueltosDelArticulo.forEach { stock ->
                        var cantidadSuelto by remember { mutableStateOf(FormatUtils.formatearCantidad(stock.unidadesSaldo)) }

                        Card(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(vertical = 2.dp),
                            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.primaryContainer)
                        ) {
                            Column(Modifier.padding(8.dp)) {
                                Text("📦 Stock suelto - ${FormatUtils.formatearCantidad(stock.unidadesSaldo)} unidades en ${stock.ubicacion}",
                                     style = MaterialTheme.typography.bodySmall)

                                Spacer(modifier = Modifier.height(4.dp))

                                Row(
                                    modifier = Modifier.fillMaxWidth(),
                                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                                ) {
                                    OutlinedTextField(
                                        value = cantidadSuelto,
                                        onValueChange = { cantidadSuelto = it },
                                        label = { Text("Cantidad") },
                                        modifier = Modifier.weight(1f),
                                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number)
                                    )

                                    Button(
                                        onClick = {
                                            val cantidad = cantidadSuelto.toDoubleOrNull()
                                            if (cantidad != null && cantidad > 0 && cantidad <= stock.unidadesSaldo) {
                                                onConfirm(stock, cantidad)
                                            }
                                        },
                                        enabled = cantidadSuelto.toDoubleOrNull()?.let { it > 0 && it <= stock.unidadesSaldo } ?: false
                                    ) {
                                        Text("Añadir")
                                    }
                                }
                            }
                        }
                    }
                }

                // Mostrar palets si hay (abiertos o cerrados)
                val totalPalets = abiertosDelArticulo.size + cerradosDelArticulo.size
                if (totalPalets > 0) {
                    Spacer(modifier = Modifier.height(4.dp))
                    Text("📦 Palets disponibles:", style = MaterialTheme.typography.bodySmall, fontWeight = FontWeight.Bold)

                    // Si hay múltiples palets Y no hay palet escaneado específico, mostrar botón para escanear
                    if (totalPalets > 1 && paletEscaneadoEspecifico == null) {
                        Card(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(vertical = 2.dp),
                            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.secondaryContainer)
                        ) {
                            Column(Modifier.padding(8.dp)) {
                                Text("Se encontraron $totalPalets palets disponibles:", style = MaterialTheme.typography.bodySmall)
                                abiertosDelArticulo.forEach { palet ->
                                    Text("• ${palet.codigoPalet ?: "N/A"} - ${FormatUtils.formatearCantidad(palet.unidadesSaldo)} unidades",
                                         style = MaterialTheme.typography.bodySmall,
                                         modifier = Modifier.padding(start = 8.dp))
                                }
                                cerradosDelArticulo.forEach { palet ->
                                    Text("• ${palet.codigoPalet ?: "N/A"} - ${FormatUtils.formatearCantidad(palet.unidadesSaldo)} unidades",
                                         style = MaterialTheme.typography.bodySmall,
                                         modifier = Modifier.padding(start = 8.dp))
                                }
                                Spacer(modifier = Modifier.height(8.dp))
                                Button(
                                    onClick = { 
                                        onScanPalet?.invoke() 
                                    },
                                    modifier = Modifier.fillMaxWidth()
                                ) {
                                    Icon(Icons.Default.QrCodeScanner, contentDescription = null)
                                    Spacer(modifier = Modifier.width(8.dp))
                                    Text("Escanear palet específico")
                                }
                            }
                        }
                        
                        // Si hay palet escaneado específico, mostrarlo
                        if (paletEscaneadoEspecifico != null) {
                            // Buscar el palet escaneado en abiertos
                            abiertosDelArticulo.find { it.codigoPalet == paletEscaneadoEspecifico.codigoPalet }?.let { palet ->
                                var cantidadPalet by remember { mutableStateOf(FormatUtils.formatearCantidad(palet.unidadesSaldo)) }
                                
                                Card(
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .padding(vertical = 2.dp),
                                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.secondaryContainer)
                                ) {
                                    Column(Modifier.padding(8.dp)) {
                                        Text("🔓 Palet ${palet.codigoPalet ?: "N/A"} - ${FormatUtils.formatearCantidad(palet.unidadesSaldo)} unidades en ${palet.ubicacion}",
                                             style = MaterialTheme.typography.bodySmall)

                                        Spacer(modifier = Modifier.height(4.dp))

                                        Row(
                                            modifier = Modifier.fillMaxWidth(),
                                            horizontalArrangement = Arrangement.spacedBy(8.dp)
                                        ) {
                                            OutlinedTextField(
                                                value = cantidadPalet,
                                                onValueChange = { cantidadPalet = it },
                                                label = { Text("Cantidad") },
                                                modifier = Modifier.weight(1f),
                                                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number)
                                            )

                                            Button(
                                                onClick = {
                                                    val cantidad = cantidadPalet.toDoubleOrNull()
                                                    if (cantidad != null && cantidad > 0 && cantidad <= palet.unidadesSaldo) {
                                                        onConfirm(palet, cantidad)
                                                    }
                                                },
                                                enabled = cantidadPalet.toDoubleOrNull()?.let { it > 0 && it <= palet.unidadesSaldo } ?: false
                                            ) {
                                                Text("Añadir")
                                            }
                                        }
                                    }
                                }
                            }
                            
                            // Buscar el palet escaneado en cerrados
                            cerradosDelArticulo.find { it.codigoPalet == paletEscaneadoEspecifico.codigoPalet }?.let { palet ->
                                var cantidadPalet by remember { mutableStateOf("") }
                                var reabrirPalet by remember { mutableStateOf(false) }
                                
                                Row(
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .padding(vertical = 4.dp),
                                    verticalAlignment = Alignment.CenterVertically
                                ) {
                                    Icon(
                                        Icons.Default.Lock,
                                        contentDescription = "Palet cerrado",
                                        tint = MaterialTheme.colorScheme.error
                                    )
                                    Spacer(modifier = Modifier.width(8.dp))
                                    Column(modifier = Modifier.weight(1f)) {
                                        Text(
                                            "Palet: ${palet.codigoPalet ?: "N/A"}",
                                            style = MaterialTheme.typography.bodyMedium,
                                            fontWeight = FontWeight.Bold
                                        )
                                        Text(
                                            "Disponible: ${FormatUtils.formatearCantidad(palet.unidadesSaldo)} unidades",
                                            style = MaterialTheme.typography.bodySmall
                                        )
                                    }
                                    Switch(
                                        checked = reabrirPalet,
                                        onCheckedChange = { reabrirPalet = it },
                                        enabled = true
                                    )
                                    Spacer(modifier = Modifier.width(8.dp))
                                    OutlinedTextField(
                                        value = cantidadPalet,
                                        onValueChange = { cantidadPalet = it },
                                        label = { Text("Cantidad") },
                                        modifier = Modifier.width(100.dp),
                                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                                        enabled = reabrirPalet
                                    )
                                    Spacer(modifier = Modifier.width(8.dp))
                                    Button(
                                        onClick = {
                                            val cantidad = cantidadPalet.toDoubleOrNull()
                                            if (cantidad != null && cantidad > 0 && cantidad <= palet.unidadesSaldo) {
                                                onConfirm(palet, cantidad)
                                            }
                                        },
                                        enabled = reabrirPalet && cantidadPalet.toDoubleOrNull()?.let { it > 0 && it <= palet.unidadesSaldo } ?: false
                                    ) {
                                        Text("Añadir")
                                    }
                                }
                            }
                        }
                    } else {
                        // Si solo hay un palet, mostrarlo directamente (abierto o cerrado)
                    (abiertosDelArticulo + cerradosDelArticulo).forEach { palet ->
                        var cantidadPalet by remember { mutableStateOf(FormatUtils.formatearCantidad(palet.unidadesSaldo)) }
                        
                        // Verificar si este palet fue escaneado específicamente
                        val esPaletEscaneado = paletEscaneadoEspecifico?.codigoPalet == palet.codigoPalet
                        // Si solo hay un palet, mostrarlo siempre. Si hay múltiples, solo si está escaneado
                        val estaHabilitado = if (totalPalets == 1) true else (paletEscaneadoEspecifico == null || esPaletEscaneado)
                        
                        // Solo mostrar el palet si está habilitado
                        if (estaHabilitado) {
                            val estaCerrado = palet.estadoPalet?.equals("Cerrado", ignoreCase = true) == true
                            var reabrirPalet by remember { mutableStateOf(false) }
                            
                            Card(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .padding(vertical = 2.dp),
                                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.secondaryContainer)
                            ) {
                                Column(Modifier.padding(8.dp)) {
                                    Row(
                                        modifier = Modifier.fillMaxWidth(),
                                        verticalAlignment = Alignment.CenterVertically
                                    ) {
                                        Text(
                                            if (estaCerrado) "🔒 Palet ${palet.codigoPalet ?: "N/A"}" else "🔓 Palet ${palet.codigoPalet ?: "N/A"}",
                                            style = MaterialTheme.typography.bodySmall
                                        )
                                        if (estaCerrado) {
                                            Spacer(modifier = Modifier.weight(1f))
                                            Switch(
                                                checked = reabrirPalet,
                                                onCheckedChange = { reabrirPalet = it },
                                                enabled = estaHabilitado
                                            )
                                        }
                                    }
                                    
                                    Text("${FormatUtils.formatearCantidad(palet.unidadesSaldo)} unidades en ${palet.ubicacion}",
                                         style = MaterialTheme.typography.bodySmall)

                                    Spacer(modifier = Modifier.height(4.dp))

                                    Row(
                                        modifier = Modifier.fillMaxWidth(),
                                        horizontalArrangement = Arrangement.spacedBy(8.dp)
                                    ) {
                                        OutlinedTextField(
                                            value = cantidadPalet,
                                            onValueChange = { cantidadPalet = it },
                                            label = { Text("Cantidad") },
                                            modifier = Modifier.weight(1f),
                                            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                                            enabled = if (estaCerrado) reabrirPalet else true
                                        )

                                        Button(
                                            onClick = {
                                                val cantidad = cantidadPalet.toDoubleOrNull()
                                                if (cantidad != null && cantidad > 0 && cantidad <= palet.unidadesSaldo) {
                                                    onConfirm(palet, cantidad)
                                                }
                                            },
                                            enabled = if (estaCerrado) reabrirPalet && cantidadPalet.toDoubleOrNull()?.let { it > 0 && it <= palet.unidadesSaldo } ?: false
                                                   else cantidadPalet.toDoubleOrNull()?.let { it > 0 && it <= palet.unidadesSaldo } ?: false
                                        ) {
                                            Text("Añadir")
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                }


                // Información adicional del artículo
                Spacer(modifier = Modifier.height(8.dp))
                Text("Lote: ${stockPrincipal.partida ?: "—"}")
                Text("Ubicación: ${stockPrincipal.ubicacion ?: "—"}")
                Text("Caducidad: ${FormatUtils.formatearFecha(stockPrincipal.fechaCaducidad) ?: "—"}")
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
    val dispositivo = sessionViewModel.dispositivo.collectAsState().value
    val context = LocalContext.current

    val paletCreado by viewModel.paletCreado.collectAsState()
    // bloqueo SOLO cuando el palet se ha creado en esta pantalla
    val flujoCreacionActivo = viewModel.flujoCreacionPaletActivo.collectAsState().value

    /* ------------------ 1️⃣  Lista de opciones  ------------------ */
    val tiposPalet  by viewModel.tiposPalet.collectAsState()
    val impresoras by viewModel.impresoras.collectAsState()
    val almacenesPermitidos by viewModel.almacenesPermitidos.collectAsState()

    // Estado para esperar a que estén cargados los datos iniciales
    var datosListos by remember { mutableStateOf(false) }

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

    var precheckConfirmar by remember { mutableStateOf(false) }
    var esPaletRecienCreado by remember { mutableStateOf(false) }
    var paletParaImprimir by remember { mutableStateOf<PaletDto?>(null) }

    LaunchedEffect(Unit) {
        viewModel.setTraspasoEsDePalet(esPalet)
        viewModel.setTraspasoDirectoDesdePaletCerrado(directoDesdePaletCerrado)
        PaletFlujoStore.init(navController.context)
        SoundUtils.getInstance().initialize(context)

        // Iniciar las cargas
        viewModel.cargarTiposPalet()
        viewModel.cargarImpresoras()
        viewModel.cargarAlmacenesPermitidos(
            sessionViewModel = sessionViewModel,
            codigoEmpresa = empresa.toInt()
        )
    }

    // Esperar a que realmente estén cargados los datos observando los StateFlows
    LaunchedEffect(tiposPalet, impresoras, almacenesPermitidos) {
        if (tiposPalet.isNotEmpty() && impresoras.isNotEmpty() && almacenesPermitidos.isNotEmpty()) {
            datosListos = true
        }
    }

    LaunchedEffect(usuarioId) {
        viewModel.reanudarFlujoSiAplica(
            usuarioIdActual = usuarioId,
            onListo = { palet ->
                viewModel.obtenerLineasDePalet(palet.id)
                // Marcar como palet recién creado para usar el flujo correcto de cierre
                esPaletRecienCreado = true
                // Asignar el palet para que pueda imprimirse al cerrar
                paletParaImprimir = palet
            }
        )
    }

    val traspasos = viewModel.traspasosPendientes.collectAsState().value

    LaunchedEffect(Unit) {
        viewModel.comprobarTraspasoPendiente(
            usuarioId = usuarioId,
            onSuccess = {
                esperandoUbicacionDestino = true
            },
            onNoPendiente = {},
            onError = { errorMsg ->
                Log.e("TRASPASOS_UI", "Error comprobando traspaso pendiente: $errorMsg")
            }
        )
    }

    /* Carga inicial de la lista - Ya consolidado arriba */
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
    var dropOpenImpresora by remember { mutableStateOf(false) }
    var logsImpresion by remember { mutableStateOf(mutableListOf<String>()) }
    
    // Variables para impresión genérica
    var mostrarDialogoImpresionGenerica by remember { mutableStateOf(false) }
    var tipoImpresion by remember { mutableStateOf<ImpresionTipo?>(null) }
    var articuloParaImprimir by remember { mutableStateOf<ArticuloDto?>(null) }
    var stockParaImprimir by remember { mutableStateOf<Stock?>(null) }
    var paletParaImprimirGenerico by remember { mutableStateOf<PaletDto?>(null) }

    // Variables para mostrar JSON
    var mostrarJsonDialogo by remember { mutableStateOf(false) }
    var jsonEnviado by remember { mutableStateOf("") }
    var jsonRespuesta by remember { mutableStateOf("") }
    val impresoraNombre = sessionViewModel.impresoraSeleccionada.collectAsState().value
    val impresoraSel = impresoras.find { it.nombre == impresoraNombre }
    var articuloPendienteMover by remember { mutableStateOf<ArticuloDto?>(null) }
    var mostrarDialogoUbicacionPrimero by remember { mutableStateOf(false) }
    //var mostrarDialogoMoverArticulo by remember { mutableStateOf(false) }
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

    // Observar errores del ViewModel
    val errorViewModel by viewModel.error.collectAsState()


    var mostrarDialogoExito by remember { mutableStateOf(false) }
    var mostrarDialogoErrorFinalizar by remember { mutableStateOf<String?>(null) }
    var mostrarDialogoTraspasoDirecto by remember { mutableStateOf(false) }
    var cerrarPaletDespuesDeImprimir by remember { mutableStateOf(false) }
    var reactivarEscaner by remember { mutableStateOf(false) }

    var mostrarDialogoCantidadDesdePalet by remember { mutableStateOf(false) }
    var lineaSeleccionada by remember { mutableStateOf<LineaPaletDto?>(null) }
    var cantidadExtraer by remember { mutableStateOf("1.0") }

    // --- PRECHECK palet en destino (ARTÍCULO) ---
    var mostrarDialogoPrecheck by remember { mutableStateOf(false) }
    var precheckAviso by remember { mutableStateOf<String?>(null) }
// Acción diferida a ejecutar si el usuario confirma
    var accionTrasConfirmacion by remember { mutableStateOf<(() -> Unit)?>(null) }
    var comentarioTraspaso by remember { mutableStateOf("") }

    // --- MÚLTIPLES PALETS en destino ---
    var mostrarDialogoEscanearPalet by remember { mutableStateOf(false) }
    var paletsDisponiblesEnDestino by remember { mutableStateOf<List<PaletDto>>(emptyList()) }
    var paletIdDestinoSeleccionado by remember { mutableStateOf<String?>(null) }
    var esperandoEscaneoGS1 by remember { mutableStateOf(false) }

    // --- CONFLICTO PALET (409) ---
    var mostrarDialogoOpcionesPalet by remember { mutableStateOf(false) }
    var conflictoPaletActual by remember { mutableStateOf<com.example.sga.data.dto.traspasos.ConflictoPaletResponse?>(null) }
    var traspasoIdPendienteConflicto by remember { mutableStateOf<String?>(null) }
    var almacenDestinoConflicto by remember { mutableStateOf<String?>(null) }
    var ubicacionDestinoConflicto by remember { mutableStateOf<String?>(null) }
    var onFinalizarConflictoExito by remember { mutableStateOf<(() -> Unit)?>(null) }
    var onFinalizarConflictoError by remember { mutableStateOf<((String) -> Unit)?>(null) }

    // Diálogo de búsqueda de stock
    var mostrarDialogoBusquedaStock by remember { mutableStateOf(false) }

    // Variables para el flujo de escaneo de palet específico
    var mostrarDialogoEscaneoPalet by remember { mutableStateOf(false) }
    
    var paletEscaneadoLocal by remember { mutableStateOf<Stock?>(null) }
    var stocksDisponibles by remember { mutableStateOf<List<Stock>>(emptyList()) }
    var onConfirmPalet by remember { mutableStateOf<((Stock, Double) -> Unit)?>(null) }


    LaunchedEffect(reactivarEscaner) {
        if (reactivarEscaner&& DeviceUtils.hasHardwareScanner(context)) {
            delay(200)
            focusRequester.requestFocus()
            reactivarEscaner = false
        }
    }

    /* Carga de almacenes permitidos - Ya consolidado arriba */
    LaunchedEffect(esperandoUbicacionDestino) {
        if (esperandoUbicacionDestino) {
            Log.d("ESCANEO_DESTINO", "📌 Lanzando focusRequester")

            // ✅ Si no tienes pendientes cargados, vuelve a consultarlos
            if (viewModel.traspasosPendientes.value.isEmpty()) {
                Log.d("ESCANEO_DESTINO", "📡 Cargando traspasos pendientes tras reinicio")

                viewModel.comprobarTraspasoPendiente(
                    usuarioId = usuarioId,
                    onSuccess = { lista ->
                        if (lista.isNotEmpty()) {
                            Log.d("ESCANEO_DESTINO", "✅ Cargados ${lista.size} traspasos pendientes")
                            viewModel.setTraspasosPendientes(lista)
                            esperandoUbicacionDestino = true // ya estaba en true, pero por claridad
                        } else {
                            Log.d("ESCANEO_DESTINO", "⚠️ No había pendientes tras relanzar flujo")
                            esperandoUbicacionDestino = false
                        }
                    },
                    onNoPendiente = {
                        Log.d("ESCANEO_DESTINO", "⚠️ No se encontraron pendientes")
                        esperandoUbicacionDestino = false
                    },
                    onError = {
                        Log.d("ESCANEO_DESTINO", "❌ Error al cargar pendientes: $it")
                        mostrarDialogoErrorFinalizar = it
                        esperandoUbicacionDestino = false
                    }
                )
            }
            if (DeviceUtils.hasHardwareScanner(context)) {
                delay(200)
                focusRequester.requestFocus()
            }
        }
    }

    Scaffold(
        topBar = {
            Box(Modifier.fillMaxWidth()) {
                AppTopBar(
                    sessionViewModel = sessionViewModel,
                    navController = navController,
                    title = ""
                )
                if (esperandoUbicacionDestino || mostrarDialogoCantidad || mostrarDialogoCantidadDesdePalet) {
                    // Tapa SOLO la AppBar (incluida la flecha) sin tocar el contenido
                    Box(
                        modifier = Modifier
                            .matchParentSize()
                            .clickable(
                                indication = null,
                                interactionSource = remember { MutableInteractionSource() }
                            ) { /* bloqueado */ }
                    )
                }
            }
        }
    ) { padding ->
        // 1) Bloquear botón "atrás" físico/gestual mientras dure el flujo de creación o diálogos críticos
        androidx.activity.compose.BackHandler(
            enabled = esperandoUbicacionDestino || mostrarDialogoCantidad || mostrarDialogoCantidadDesdePalet
        ) { /* no-op: evita salir de Traspasos */ }

        if (DeviceUtils.hasHardwareScanner(context) && !esperandoUbicacionDestino) {
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
                                            // Validar que el usuario tenga permisos para el almacén de origen
                                            if (viewModel.almacenesPermitidos.value.contains(codAlm)) {
                                                ubicacionEscaneada = codAlm to codUbi
                                                SoundUtils.getInstance().playSuccessSound()
                                            } else {
                                                mostrarDialogoErrorFinalizar = "No tienes permisos para operar en el almacén '$codAlm'. Ubicación no permitida."
                                                SoundUtils.getInstance().playErrorSound()
                                            }
                                            escaneoProcesado = false
                                        },
                                        onPaletDetectado = { palet ->
                                            if (ubicacionEscaneada == null) {
                                                mostrarDialogoUbicacionPrimero = true
                                                escaneoProcesado = false
                                                reactivarEscaner = true
                                                SoundUtils.getInstance().playErrorSound()
                                            } else {
                                                viewModel.validarUbicacionDePalet(
                                                    palet = palet,
                                                    ubicacionEscaneada = ubicacionEscaneada!!,
                                                    onValidado = {
                                                        viewModel.setPaletSeleccionado(palet)
                                                        viewModel.obtenerLineasDePalet(palet.id)
                                                        idPaletParaCerrar = palet.id
                                                        escaneoProcesado = false
                                                        reactivarEscaner = true
                                                    },
                                                    onError = { msg ->
                                                        mostrarDialogoErrorFinalizar = msg
                                                        escaneoProcesado = false
                                                        reactivarEscaner = true
                                                    }
                                                )
                                            }
                                        },
                                        onArticuloDetectado = { articuloDto ->
                                            val loc = ubicacionEscaneada
                                            if (loc == null) {
                                                mostrarDialogoUbicacionPrimero = true
                                                reactivarEscaner = true
                                                Log.e("TRASPASOS_UI", "Se ha escaneado un artículo sin ubicación. Mostrando diálogo de ubicación requerida.")
                                                SoundUtils.getInstance().playErrorSound()
                                            } else if (paletEscaneado != null &&
                                                paletEscaneado!!.estado.equals("Abierto", ignoreCase = true) &&
                                                empresaId != null
                                            ) {
                                                val (codAlm, codUbi) = loc
                                                viewModel.buscarStockYMostrar(
                                                    codigoArticulo = articuloDto.codigoArticulo,
                                                    empresaId = empresaId,
                                                    codigoAlmacen = codAlm,
                                                    codigoUbicacion = codUbi,
                                                    almacenesPermitidos = viewModel.almacenesPermitidos.value,
                                                    partida = articuloDto.partida
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
                                                reactivarEscaner = true
                                                Log.e("TRASPASOS_UI", "Se ha escaneado un artículo sin ubicación. Mostrando diálogo de ubicación requerida.")
                                                SoundUtils.getInstance().playErrorSound()
                                            } else {
                                                viewModel.setArticulosFiltrados(articulos)
                                                viewModel.setMostrarDialogoSeleccion(true)
                                            }
                                            escaneoProcesado = false
                                        },
                                        onError = {
                                            escaneoProcesado = false
                                            reactivarEscaner = true
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
            LaunchedEffect(reactivarEscaner) {
                if (reactivarEscaner && DeviceUtils.hasHardwareScanner(context)) {
                    focusRequester.requestFocus()
                    reactivarEscaner = false
                }
            }
        }

        if (datosListos) {
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
                        almacenesPermitidos = viewModel.almacenesPermitidos.value,
                        partida = articuloPendiente!!.partida
                    )

                    articuloPendiente = null
                    triggerLineaPendiente = false
                }
            }

            // Establecer mostrarDialogoCantidad solo cuando hay stock disponible
            LaunchedEffect(resultadoStock, articuloParaTraspaso) {
                if (resultadoStock.isNotEmpty() && articuloParaTraspaso != null) {
                    mostrarDialogoCantidad = true
                }
            }

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text("Traspasos", style = MaterialTheme.typography.titleLarge)
                
                // Icono de búsqueda de stock (siempre visible)
                IconButton(
                    onClick = {
                        mostrarDialogoBusquedaStock = true
                    }
                ) {
                    androidx.compose.material3.Surface(
                        modifier = Modifier.size(48.dp),
                        shape = RoundedCornerShape(12.dp),
                        color = MaterialTheme.colorScheme.primary,
                        shadowElevation = 4.dp
                    ) {
                        Box(contentAlignment = Alignment.Center, modifier = Modifier.fillMaxSize()) {
                            Icon(
                                imageVector = Icons.Default.Search,
                                contentDescription = "Buscar stock",
                                tint = Color.White,
                                modifier = Modifier.size(24.dp)
                            )
                        }
                    }
                }
            }
            if (escaneando && !DeviceUtils.hasHardwareScanner(context)) {
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .background(MaterialTheme.colorScheme.background.copy(alpha = 0.85f)),
                    horizontalAlignment = Alignment.CenterHorizontally
                ) {
                    Text(
                        "Escaneando...",
                        style = MaterialTheme.typography.titleMedium
                    )
                    Spacer(modifier = Modifier.height(24.dp))

                    QRScannerView(
                        modifier = Modifier
                            .fillMaxWidth(0.5f)
                            .aspectRatio(1f), // Hace que sea cuadrado (mismo ancho que altura)
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
                                        // Validar que el usuario tenga permisos para el almacén de origen
                                        if (viewModel.almacenesPermitidos.value.contains(codAlm)) {
                                            ubicacionEscaneada = codAlm to codUbi
                                            SoundUtils.getInstance().playSuccessSound()
                                        } else {
                                            mostrarDialogoErrorFinalizar = "No tienes permisos para operar en el almacén '$codAlm'. Ubicación no permitida."
                                            SoundUtils.getInstance().playErrorSound()
                                        }
                                        escaneoProcesado = false
                                    },
                                    onPaletDetectado = { palet ->
                                        if (ubicacionEscaneada == null) {
                                            mostrarDialogoUbicacionPrimero = true
                                            escaneoProcesado = false
                                            SoundUtils.getInstance().playErrorSound()
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
                                            SoundUtils.getInstance().playErrorSound()
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
                                                almacenesPermitidos = viewModel.almacenesPermitidos.value,
                                                partida = articuloDto.partida
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
                                            SoundUtils.getInstance().playErrorSound()
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

                    Spacer(modifier = Modifier.height(24.dp))
                    Button(onClick = { escaneando = false }) {
                        Text("Cancelar escaneo")
                    }
                    Spacer(Modifier.height(12.dp))
                }
            } else if (!DeviceUtils.hasHardwareScanner(context)) {
                Button(
                    onClick = {
                        escaneoProcesado = false
                        escaneando = true
                    },
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Text("Escanear")
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
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Text("📦 ${palet.codigoPalet}", style = MaterialTheme.typography.bodyLarge)

                            Row(
                                horizontalArrangement = Arrangement.spacedBy(8.dp)
                            ) {
                                // Botón X para limpiar palet vacío (solo si está abierto, en flujo de creación y sin líneas)
                                if (palet.estado.equals("Abierto", ignoreCase = true) && 
                                    flujoCreacionActivo && 
                                    (lineasPalet[palet.id] ?: emptyList()).filter { it.cantidad > 0.0 }.isEmpty()) {
                                    IconButton(
                                        onClick = {
                                            viewModel.limpiarPaletVacío(palet.id, usuarioId)
                                        }
                                    ) {
                                        androidx.compose.material3.Surface(
                                            modifier = Modifier.size(56.dp),
                                            shape = RoundedCornerShape(12.dp),
                                            color = Color.Red,
                                            shadowElevation = 4.dp
                                        ) {
                                            Box(contentAlignment = Alignment.Center, modifier = Modifier.fillMaxSize()) {
                                                Icon(
                                                    imageVector = Icons.Default.Close,
                                                    contentDescription = "Limpiar palet vacío",
                                                    tint = Color.White,
                                                    modifier = Modifier.size(28.dp)
                                                )
                                            }
                                        }
                                    }
                                }


                                // Botón de traspaso directo (solo si está cerrado)
                                if (palet.estado.equals("Cerrado", ignoreCase = true)) {
                                    IconButton(
                                        onClick = {
                                            mostrarDialogoTraspasoDirecto = true
                                        }
                                    ) {
                                        androidx.compose.material3.Surface(
                                            modifier = Modifier.size(56.dp),
                                            shape = RoundedCornerShape(12.dp),
                                            color = MaterialTheme.colorScheme.primary,
                                            shadowElevation = 4.dp
                                        ) {
                                            Box(contentAlignment = Alignment.Center, modifier = Modifier.fillMaxSize()) {
                                                Icon(
                                                    imageVector = Icons.Default.SwapVert,
                                                    contentDescription = "Traspasar palet cerrado",
                                                    tint = Color.White,
                                                    modifier = Modifier.size(28.dp)
                                                )
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        Text("Estado: ${palet.estado}")
                        Text("Tipo: ${palet.tipoPaletCodigo}")
                        Text("Orden: ${palet.ordenTrabajoId ?: "Sin orden"}")

                        //val lineas = lineasPalet[palet.id] ?: emptyList()
                        val lineas = (lineasPalet[palet.id] ?: emptyList())
                            .filter { it.cantidad > 0.0 }
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
                                .clickable(enabled = !flujoCreacionActivo) {
                                    viewModel.clearPaletSeleccionado()
                                }
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
                                                Text("Cantidad: ${FormatUtils.formatearCantidad(linea.cantidad)}", style = MaterialTheme.typography.bodySmall)
                                                Text("Lote: ${linea.lote ?: "Sin lote"}", style = MaterialTheme.typography.bodySmall)
                                                Text("Caducidad: ${FormatUtils.formatearFecha(linea.fechaCaducidad) ?: "Sin fecha"}", style = MaterialTheme.typography.bodySmall)
                                                Text("Ubicación: ${linea.ubicacion ?: "Sin ubicación"}", style = MaterialTheme.typography.bodySmall)

                                                // Mostrar palet origen temporalmente solo si hay uno
                                                val codigoOrigen = viewModel.codigosPaletOrigen.collectAsState().value[palet.id]
                                                if (codigoOrigen != null) {
                                                    Text("Origen: Palet $codigoOrigen",
                                                         style = MaterialTheme.typography.bodySmall,
                                                         color = MaterialTheme.colorScheme.primary)
                                                }
                                            }

                                            if (palet.estado.equals("Abierto", ignoreCase = true)) {
                                                if (flujoCreacionActivo) {
                                                    // Comportamiento actual (eliminar línea)
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
                                                } else {
                                                    // NUEVO: sacar artículo del palet (traspaso artículo)
                                                    IconButton(
                                                        onClick = {
                                                            val ubi = viewModel.ubicacionOrigen.value
                                                            if (ubi == null) {
                                                                // Por seguridad, si alguien abre sin escanear ubicación
                                                                mostrarDialogoUbicacionPrimero = true
                                                                SoundUtils.getInstance().playErrorSound()
                                                            } else {
                                                                lineaSeleccionada = linea
                                                                cantidadExtraer = FormatUtils.formatearCantidad(linea.cantidad)
                                                                mostrarDialogoCantidadDesdePalet = true
                                                            }
                                                        },
                                                        modifier = Modifier.align(Alignment.TopEnd)
                                                    ) {
                                                    androidx.compose.material3.Surface(
                                                        modifier = Modifier.size(56.dp),
                                                        shape = RoundedCornerShape(12.dp),
                                                        color = MaterialTheme.colorScheme.primary,
                                                        shadowElevation = 4.dp
                                                    ) {
                                                        Box(contentAlignment = Alignment.Center, modifier = Modifier.fillMaxSize()) {
                                                            Icon(
                                                                imageVector = Icons.Default.SwapVert,
                                                                contentDescription = "Sacar artículo del palet",
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

                                }
                            }
                        }
                        if (palet.estado.equals("Cerrado", ignoreCase = true)) {
                            Spacer(modifier = Modifier.height(12.dp))

                            // Campo de comentario para traspaso
                            val focusManagerPalet = LocalFocusManager.current
                            OutlinedTextField(
                                value = comentarioTraspaso,
                                onValueChange = { if (it.length <= 200) comentarioTraspaso = it },
                                label = { Text("Comentario para traspaso (opcional)") },
                                modifier = Modifier.fillMaxWidth(),
                                maxLines = 2,
                                singleLine = false,
                                keyboardOptions = KeyboardOptions(
                                    imeAction = ImeAction.Done
                                ),
                                keyboardActions = KeyboardActions(
                                    onDone = {
                                        focusManagerPalet.clearFocus()
                                    }
                                )
                            )

                            Spacer(modifier = Modifier.height(8.dp))

                            IconButton(
                                onClick = {
                                    viewModel.obtenerPalet(palet.id) {
                                        tipoImpresion = ImpresionTipo.PALET
                                        paletParaImprimirGenerico = it
                                        mostrarDialogoImpresionGenerica = true
                                    }
                                }
                            ) {
                                Icon(Icons.Default.Print, contentDescription = "Imprimir etiqueta de palet")
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
                                ubicacion = stock.ubicacion,
                                observaciones = null,
                                paletIdOrigen = if (stock.tipoStock == "Paletizado") stock.paletId else null
                            ),
                            codigoPaletOrigen = if (stock.tipoStock == "Paletizado") stock.codigoPalet else null
                        ) {
                            mostrarDialogoCantidad = false
                            articuloParaTraspaso = null
                            ubicacionParaTraspaso = null
                            cantidadArticulo = "1.0"
                            viewModel.limpiarStock()
                            ubicacionEscaneada = null
                            viewModel.clearUbicacionOrigen()
                            reactivarEscaner = true
                        }
                    },
                    viewModel = viewModel,
                    usuarioId = usuarioId,
                    empresa = empresa,
                    almacenesPermitidos = viewModel.almacenesPermitidos.value,
                    paletEscaneadoEspecifico = paletEscaneadoLocal,
                    onScanPalet = {
                        stocksDisponibles = resultadoStock
                        onConfirmPalet = { stock, cantidad ->
                            // NO ejecutar onConfirm aquí, solo cerrar el diálogo
                            // El palet escaneado se manejará en StockSelectionCards
                        }
                        mostrarDialogoEscaneoPalet = true
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
                        ),
                        onSuccess = { nuevoPalet ->
                            paletParaImprimir = nuevoPalet      // ✅ ASIGNAR AQUÍ
                        }
                    )
                    triggerLineaPendiente = true
                    esPaletRecienCreado = true
                },
                enabled = tipoSeleccionado != null,
                modifier = Modifier.fillMaxWidth()
            ) {
                Text("Crear palet")
            }

            }
            if (mostrarDialogoCerrarPalet && idPaletParaCerrar != null) {
                //val lineasDelPalet = lineasPalet[idPaletParaCerrar] ?: emptyList()
                val lineasDelPalet = (lineasPalet[idPaletParaCerrar] ?: emptyList())
                    .filter { it.cantidad > 0.0 }
                // Campos para peso y altura (comentados - no se utilizan)
                // var peso by remember { mutableStateOf("0") }
                // var altura by remember { mutableStateOf("0") }

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

                            // Campos de peso y altura (comentados - no se utilizan)
                            /*
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
                            */

                            LazyColumn(Modifier.fillMaxWidth()) {
                                // 👉 necesitas import androidx.compose.foundation.lazy.items
                                items(lineasDelPalet) { linea ->
                                    Column(Modifier.padding(vertical = 4.dp)) {
                                        Text(
                                            "${linea.codigoArticulo} - ${linea.descripcion ?: "Sin descripción"}",
                                            style = MaterialTheme.typography.bodySmall
                                        )
                                        Text(
                                            "Cantidad: ${FormatUtils.formatearCantidad(linea.cantidad)}, Lote: ${linea.lote ?: "—"}",
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

                            if (esPaletRecienCreado) {
                                Log.d("CERRAR_PALET", "🆕 Palet recién creado, se lanza impresión antes de cierre")
                                // Usar el palet que ya está cargado
                                tipoImpresion = ImpresionTipo.PALET
                                paletParaImprimirGenerico = paletEscaneado
                                cerrarPaletDespuesDeImprimir = true
                                mostrarDialogoImpresionGenerica = true
                                return@TextButton
                            }

                            val (codigoAlmacen, ubicacion) = ubicacionEscaneada ?: run {
                                Log.e("CERRAR_PALET", "❌ ubicacionEscaneada es null, no se puede cerrar el palet")
                                return@TextButton
                            }

                            viewModel.cerrarPalet(
                                id = idPaletParaCerrar!!,
                                usuarioId = usuarioId,
                                codigoAlmacen = codigoAlmacen,
                                codigoEmpresa = empresa,
                                //ubicacionOrigen = ubicacion,
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
                            SoundUtils.getInstance().playErrorSound()
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
                                    almacenesPermitidos = viewModel.almacenesPermitidos.value,
                                    partida = articuloSeleccionado.partida
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
        } // Cerrar if (datosListos)
    }
    ////***\\\
    if (mostrarDialogoTraspasoDirecto && paletEscaneado != null) {
        AlertDialog(
            onDismissRequest = { mostrarDialogoTraspasoDirecto = false },
            title = { Text("Confirmar traspaso") },
            text = {
                Text("¿Desea realizar el traspaso de este palet?")
            },
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
                        comentario = comentarioTraspaso.trim().takeUnless { it.isBlank() }
                    )
                    viewModel.moverPalet(
                        dto = dto,
                        onSuccess = {
                            idPaletParaCerrar = null
                            esperandoUbicacionDestino = true
                            viewModel.setTraspasoEsDePalet(true)
                            viewModel.setTraspasoDirectoDesdePaletCerrado(true)
                            comentarioTraspaso = "" // Limpiar el comentario después de usarlo
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
                                SoundUtils.getInstance().playErrorSound()
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
                                // NO establecer mostrarDialogoCantidad aquí - se establecerá solo si hay stock disponible
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

    if (mostrarDialogoCancelarArticulo) {
        AlertDialog(
            onDismissRequest = { mostrarDialogoCancelarArticulo = false },
            title = { Text("Cancelar artículo") },
            text  = { Text("¿Desea descartar el artículo escaneado?") },
            confirmButton = {
                TextButton(onClick = {
                    // Limpieza VM existente
                    viewModel.setArticuloPendienteMover(null)
                    mostrarDialogoMoverArticuloVM = false
                    mostrarDialogoCancelarArticulo = false

                    // 🔽 Desbloqueo que faltaba:
                    esperandoUbicacionDestino = false          // quita el overlay y el lector de destino
                    traspasoPendienteId = null                 // olvida el id local
                    viewModel.clearPendientes()                // vacía la lista local de pendientes
                    viewModel.setTraspasoEsDePalet(false)      // asegura que no estamos en flujo palet
                    //articuloPendienteMoverLocal = null
                    ubicacionEscaneada = null                  // opcional: volvemos a estado neutro
                }) { Text("Sí") }
            },
            dismissButton = {
                TextButton(onClick = { mostrarDialogoCancelarArticulo = false }) { Text("No") }
            }
        )
    }

    // Diálogo de impresión genérico (palet o artículo)
    if (mostrarDialogoImpresionGenerica && tipoImpresion != null) {
        AlertDialog(
            onDismissRequest = {
                mostrarDialogoImpresionGenerica = false
                logsImpresion.clear()
            },
            title = { 
                Text(
                    when (tipoImpresion) {
                        ImpresionTipo.PALET -> "Imprimir etiqueta de palet"
                        ImpresionTipo.ARTICULO -> "Imprimir etiqueta de artículo"
                        null -> "Imprimir etiqueta"
                    }
                ) 
            },
            text = {
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .heightIn(max = 500.dp)
                        .verticalScroll(rememberScrollState()),
                    verticalArrangement = Arrangement.spacedBy(12.dp)
                ) {
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
                                        dropOpenImpresora = false
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

                    // Área de logs de debug
                    if (logsImpresion.isNotEmpty()) {
                        Spacer(modifier = Modifier.height(8.dp))
                        Card(
                            modifier = Modifier.fillMaxWidth(),
                            colors = CardDefaults.cardColors(
                                containerColor = MaterialTheme.colorScheme.surfaceVariant
                            )
                        ) {
                            Column(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .padding(8.dp)
                            ) {
                                Text(
                                    "Estado de la impresión:",
                                    style = MaterialTheme.typography.labelMedium,
                                    fontWeight = FontWeight.Bold
                                )
                                Spacer(modifier = Modifier.height(4.dp))
                                logsImpresion.forEach { log ->
                                    Text(
                                        log,
                                        style = MaterialTheme.typography.bodySmall,
                                        fontFamily = FontFamily.Monospace,
                                        modifier = Modifier.padding(vertical = 2.dp)
                                    )
                                }
                            }
                        }
                    }
                }
            },
            confirmButton = {
                Button(onClick = {
                    logsImpresion.clear()
                    logsImpresion.add("🖨️ Iniciando impresión...")
                    Log.d("IMPRIMIR_PALET", "🖨️ Botón Imprimir pulsado")

                    // Validar usuario
                    val usuario = sessionViewModel.user.value?.name
                    if (usuario == null) {
                        logsImpresion.add("❌ ERROR: Usuario no disponible")
                        Log.e("IMPRIMIR_PALET", "❌ Error: usuario no disponible")
                        mostrarDialogoErrorFinalizar = "Error: No se pudo obtener el usuario. Por favor, reinicia sesión."
                        return@Button
                    }
                    logsImpresion.add("✅ Usuario: $usuario")
                    Log.d("IMPRIMIR_PALET", "✅ Usuario: $usuario")

                    // Validar dispositivo
                    val dispositivoId = dispositivo?.id
                    if (dispositivoId == null) {
                        logsImpresion.add("❌ ERROR: Dispositivo no disponible")
                        Log.e("IMPRIMIR_PALET", "❌ Error: dispositivo no disponible")
                        mostrarDialogoErrorFinalizar = "Error: No se pudo obtener el dispositivo."
                        return@Button
                    }
                    logsImpresion.add("✅ Dispositivo: $dispositivoId")
                    Log.d("IMPRIMIR_PALET", "✅ Dispositivo: $dispositivoId")

                    // Validar impresora
                    val impresora = impresoras.find { it.nombre == impresoraNombre }
                    if (impresora == null) {
                        logsImpresion.add("❌ ERROR: Impresora no seleccionada")
                        logsImpresion.add("   Nombre buscado: '$impresoraNombre'")
                        logsImpresion.add("   Impresoras disponibles: ${impresoras.size}")
                        impresoras.forEach { imp ->
                            logsImpresion.add("   - ${imp.nombre}")
                        }
                        Log.e("IMPRIMIR_PALET", "❌ Error: impresora no seleccionada o no encontrada. Nombre: $impresoraNombre")
                        mostrarDialogoErrorFinalizar = "Error: No se ha seleccionado ninguna impresora. Por favor, selecciona una impresora."
                        return@Button
                    }
                    logsImpresion.add("✅ Impresora: ${impresora.nombre}")
                    logsImpresion.add("   ID: ${impresora.id}")
                    Log.d("IMPRIMIR_PALET", "✅ Impresora: ${impresora.nombre} (ID: ${impresora.id})")

                    // Crear DTO según el tipo de impresión
                    val dto = when (tipoImpresion) {
                        ImpresionTipo.PALET -> {
                            if (paletParaImprimirGenerico == null) {
                                logsImpresion.add("❌ ERROR: Palet no disponible")
                                Log.e("IMPRIMIR_GENERICO", "❌ Error: paletParaImprimirGenerico es null")
                                mostrarDialogoErrorFinalizar = "Error: No se pudo obtener la información del palet."
                                return@Button
                            }
                            logsImpresion.add("✅ Palet: ${paletParaImprimirGenerico!!.codigoPalet}")
                            logsImpresion.add("   GS1: ${paletParaImprimirGenerico!!.codigoGS1}")
                            Log.d("IMPRIMIR_GENERICO", "✅ Palet: ${paletParaImprimirGenerico!!.codigoPalet}, GS1: ${paletParaImprimirGenerico!!.codigoGS1}")

                            LogImpresionDto(
                                usuario = usuario,
                                dispositivo = dispositivoId,
                                idImpresora = impresora.id,
                                etiquetaImpresa = 0,
                                tipoEtiqueta = 2,
                                copias = copias,
                                pathEtiqueta = "\\\\Sage200\\mrh\\Servicios\\PrintCenter\\ETIQUETAS\\PALET.nlbl",
                                codigoGS1 = paletParaImprimirGenerico!!.codigoGS1,
                                codigoPalet = paletParaImprimirGenerico!!.codigoPalet
                            )
                        }
                        ImpresionTipo.ARTICULO -> {
                            if (stockParaImprimir == null || articuloParaImprimir == null) {
                                logsImpresion.add("❌ ERROR: Datos de artículo no disponibles")
                                Log.e("IMPRIMIR_GENERICO", "❌ Error: stockParaImprimir o articuloParaImprimir es null")
                                mostrarDialogoErrorFinalizar = "Error: No se pudo obtener la información del artículo."
                                return@Button
                            }
                            logsImpresion.add("✅ Artículo: ${stockParaImprimir!!.codigoArticulo}")
                            Log.d("IMPRIMIR_GENERICO", "✅ Artículo: ${stockParaImprimir!!.codigoArticulo}")

                            // Obtener alérgenos del backend antes de imprimir
                            logsImpresion.add("🔍 Consultando alérgenos...")
                            com.example.sga.data.ApiManager.etiquetasApiService.getAlergenos(
                                codigoEmpresa = empresa,
                                codigoArticulo = stockParaImprimir!!.codigoArticulo
                            ).enqueue(object : retrofit2.Callback<com.example.sga.data.dto.etiquetas.AlergenosDto> {
                                override fun onResponse(
                                    call: retrofit2.Call<com.example.sga.data.dto.etiquetas.AlergenosDto>,
                                    response: retrofit2.Response<com.example.sga.data.dto.etiquetas.AlergenosDto>
                                ) {
                                    val alergenosTexto = response.body()?.alergenos
                                    logsImpresion.add("✅ Alérgenos: ${alergenosTexto ?: "(ninguno)"}")
                                    
                                    val dto = LogImpresionDto(
                                        usuario = usuario,
                                        dispositivo = dispositivoId,
                                        idImpresora = impresora.id,
                                        etiquetaImpresa = 0,
                                        tipoEtiqueta = 1,
                                        copias = copias,
                                        pathEtiqueta = "\\\\Sage200\\mrh\\Servicios\\PrintCenter\\ETIQUETAS\\MMPP_MES.nlbl",
                                        codigoArticulo = stockParaImprimir!!.codigoArticulo,
                                        descripcionArticulo = stockParaImprimir!!.descripcionArticulo ?: "",
                                        codigoAlternativo = articuloParaImprimir!!.codigoAlternativo ?: "",
                                        fechaCaducidad = stockParaImprimir!!.fechaCaducidad?.take(10),
                                        partida = stockParaImprimir!!.partida,
                                        alergenos = alergenosTexto?.takeIf { it.isNotBlank() } ?: "",
                                        codigoGS1 = null,
                                        codigoPalet = null
                                    )

                                    Log.d("IMPRIMIR_GENERICO", "📄 DTO creado con alérgenos - Copias: $copias")
                                    logsImpresion.add("📄 DTO creado - Copias: $copias")
                                    logsImpresion.add("🚀 Enviando petición al servidor...")
                                    
                                    // Enviar a impresión
                                    viewModel.imprimirEtiquetaPalet(dto)
                                    
                                    logsImpresion.add("✅ Petición enviada correctamente")
                                    Log.d("IMPRIMIR_ARTICULO", "✅ Llamada a impresión completada")
                                    
                                    // Esperar un poco para que el usuario vea el último mensaje
                                    kotlinx.coroutines.GlobalScope.launch {
                                        kotlinx.coroutines.delay(1500)
                                        mostrarDialogoImpresionGenerica = false
                                        logsImpresion.clear()
                                    }
                                }

                                override fun onFailure(
                                    call: retrofit2.Call<com.example.sga.data.dto.etiquetas.AlergenosDto>,
                                    t: Throwable
                                ) {
                                    Log.w("IMPRIMIR_GENERICO", "⚠️ Alérgenos no disponibles: ${t.message}")
                                    logsImpresion.add("⚠️ Alérgenos no disponibles, continuando sin ellos")
                                    
                                    // Continuar sin alérgenos
                                    val dto = LogImpresionDto(
                                        usuario = usuario,
                                        dispositivo = dispositivoId,
                                        idImpresora = impresora.id,
                                        etiquetaImpresa = 0,
                                        tipoEtiqueta = 1,
                                        copias = copias,
                                        pathEtiqueta = "\\\\Sage200\\mrh\\Servicios\\PrintCenter\\ETIQUETAS\\MMPP_MES.nlbl",
                                        codigoArticulo = stockParaImprimir!!.codigoArticulo,
                                        descripcionArticulo = stockParaImprimir!!.descripcionArticulo ?: "",
                                        codigoAlternativo = articuloParaImprimir!!.codigoAlternativo ?: "",
                                        fechaCaducidad = stockParaImprimir!!.fechaCaducidad?.take(10),
                                        partida = stockParaImprimir!!.partida,
                                        alergenos = "",
                                        codigoGS1 = null,
                                        codigoPalet = null
                                    )

                                    Log.d("IMPRIMIR_GENERICO", "📄 DTO creado sin alérgenos - Copias: $copias")
                                    logsImpresion.add("📄 DTO creado - Copias: $copias")
                                    logsImpresion.add("🚀 Enviando petición al servidor...")
                                    
                                    // Enviar a impresión
                                    viewModel.imprimirEtiquetaPalet(dto)
                                    
                                    logsImpresion.add("✅ Petición enviada correctamente")
                                    Log.d("IMPRIMIR_ARTICULO", "✅ Llamada a impresión completada")
                                    
                                    // Esperar un poco para que el usuario vea el último mensaje
                                    kotlinx.coroutines.GlobalScope.launch {
                                        kotlinx.coroutines.delay(1500)
                                        mostrarDialogoImpresionGenerica = false
                                        logsImpresion.clear()
                                    }
                                }
                            })
                            
                            // No crear ni enviar el DTO aquí - se hace en los callbacks de arriba
                            return@Button
                        }
                        null -> {
                            logsImpresion.add("❌ ERROR: Tipo de impresión no definido")
                            mostrarDialogoErrorFinalizar = "Error: Tipo de impresión no definido."
                            return@Button
                        }
                    }

                    // Solo para tipo PALET (ARTICULO se maneja en sus callbacks)
                    if (tipoImpresion == ImpresionTipo.PALET) {
                        logsImpresion.add("📄 DTO creado - Copias: $copias")
                        logsImpresion.add("🚀 Enviando petición al servidor...")
                        Log.d("IMPRIMIR_PALET", "📄 DTO creado - Copias: $copias")
                        Log.d("IMPRIMIR_PALET", "🚀 Llamando a viewModel.imprimirEtiquetaPalet()")

                        // Función normal de impresión (sin debug)
                        viewModel.imprimirEtiquetaPalet(dto)

                        logsImpresion.add("✅ Petición enviada correctamente")
                        Log.d("IMPRIMIR_PALET", "✅ Llamada a impresión completada")

                        // Esperar un poco para que el usuario vea el último mensaje
                        kotlinx.coroutines.GlobalScope.launch {
                            kotlinx.coroutines.delay(1500)
                            mostrarDialogoImpresionGenerica = false
                            logsImpresion.clear()
                        }
                    }
                    if (cerrarPaletDespuesDeImprimir) {
                        cerrarPaletDespuesDeImprimir = false

                        viewModel.cerrarPalet(
                            id = idPaletParaCerrar!!,
                            usuarioId = usuarioId,
                            codigoAlmacen = null,
                            codigoEmpresa = empresa,
                            //ubicacionOrigen = null,
                            onSuccess = { traspasoId ->
                                Log.d("CERRAR_PALET", "✅ Palet cerrado correctamente tras impresión. Traspaso ID: $traspasoId")
                                traspasoPendienteId = traspasoId
                                viewModel.setTraspasoEsDePalet(true)
                                esperandoUbicacionDestino = true
                                idPaletParaCerrar = null
                                esPaletRecienCreado = false
                            },
                            onError = {
                                Log.e("CERRAR_PALET", "❌ Error al cerrar palet tras impresión: $it")
                                //mostrarDialogoErrorFinalizar = it
                            }
                        )
                    }
                }) {
                    Text("Imprimir")
                }
            },
            dismissButton = {
                TextButton(onClick = {
                    mostrarDialogoImpresionGenerica = false
                    logsImpresion.clear()
                }) {
                    Text("Cancelar")
                }
            }
        )
    }
    LaunchedEffect(mostrarDialogoImpresionGenerica) {
        if (!mostrarDialogoImpresionGenerica && DeviceUtils.hasHardwareScanner(context)) {
            // damos tiempo a que se recomponga el Box con focusRequester
            delay(200)
            reactivarEscaner = true   // ya tienes un LaunchedEffect(reactivarEscaner) que llama a requestFocus()
        }
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

    // Mostrar error del ViewModel (ej: cuando no hay stock disponible)
    if (errorViewModel != null) {
        AlertDialog(
            onDismissRequest = { viewModel.setError(null) },
            title = { Text("Error") },
            text = { Text(errorViewModel!!) },
            confirmButton = {
                TextButton(onClick = {
                    viewModel.setError(null)
                    // Reactivar el escáner después de cerrar el error
                    reactivarEscaner = true
                }) {
                    Text("OK")
                }
            },
            dismissButton = {}
        )
    }


// Diálogo de cantidad + comentario
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
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .heightIn(max = 500.dp)
                        .verticalScroll(rememberScrollState()),
                    verticalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    val focusManager = LocalFocusManager.current
                    OutlinedTextField(
                        value = comentarioTraspaso,
                        onValueChange = { if (it.length <= 200) comentarioTraspaso = it },
                        label = { Text("Comentario (opcional)") },
                        modifier = Modifier.fillMaxWidth(),
                        maxLines = 2,
                        singleLine = false,
                        keyboardOptions = KeyboardOptions(
                            imeAction = ImeAction.Done
                        ),
                        keyboardActions = KeyboardActions(
                            onDone = {
                                focusManager.clearFocus()
                            }
                        ),
                        colors = OutlinedTextFieldDefaults.colors(
                            focusedContainerColor = MaterialTheme.colorScheme.surface,
                            unfocusedContainerColor = MaterialTheme.colorScheme.surface
                        )
                    )

                    StockSelectionCards(
                        stocks = stocks,
                        botonLabel = "Traspasar artículo",
                        onConfirm = { stock, cantidad ->
                            if (partidaEscaneada == null) {
                                Log.e("TRASPASOS_UI", "ERROR: partida es null antes del POST.")
                                return@StockSelectionCards
                            }
                            viewModel.crearTraspasoArticulo(
                                dto = CrearTraspasoArticuloDto(
                                    codigoEmpresa   = empresaId ?: return@StockSelectionCards,
                                    almacenOrigen   = stock.codigoAlmacen,
                                    ubicacionOrigen = stock.ubicacion ?: "",
                                    codigoArticulo  = stock.codigoArticulo,
                                    cantidad        = cantidad,
                                    usuarioId       = usuarioId,
                                    partida         = partidaEscaneada,
                                    fechaCaducidad  = fechaCaducidadEscaneada,
                                    finalizar       = false,
                                    comentario      = comentarioTraspaso.trim().takeUnless { it.isBlank() },
                                ),
                                onSuccess = {
                                    viewModel.setArticuloPendienteMover(articuloParaTraspaso)
                                    traspasoPendienteId = it
                                    esperandoUbicacionDestino = true
                                    mostrarDialogoMoverArticuloVM = true
                                    SoundUtils.getInstance().playSuccessSound()
                                },
                                onError = {
                                    errorTraspasoArticulo = it
                                    mostrarDialogoMoverArticuloVM = false
                                    SoundUtils.getInstance().playErrorSound()
                                }
                            )
                            mostrarDialogoCantidad = false
                            articuloParaTraspaso = null
                            ubicacionParaTraspaso = null
                            cantidadArticulo = "1.0"
                            comentarioTraspaso = ""
                            viewModel.limpiarStock()
                        },
                        viewModel = viewModel,
                        usuarioId = usuarioId,
                        empresa = empresa,
                        almacenesPermitidos = viewModel.almacenesPermitidos.value,
                        paletEscaneadoEspecifico = paletEscaneadoLocal,
                        onScanPalet = {
                            stocksDisponibles = stocks
                            onConfirmPalet = { stock, cantidad ->
                                // NO ejecutar onConfirm aquí, solo cerrar el diálogo
                                // El palet escaneado se manejará en StockSelectionCards
                            }
                            mostrarDialogoEscaneoPalet = true
                            
                        }
                    )

                    // Información del artículo con icono de impresión
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(top = 8.dp),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Column {
                            Text("Lote: ${articuloParaTraspaso!!.partida ?: "—"}")
                            Text("Ubicación: ${ubicacionParaTraspaso?.second ?: "—"}")
                            Text("Caducidad: ${FormatUtils.formatearFecha(articuloParaTraspaso!!.fechaCaducidad) ?: "—"}")
                        }
                        
                        // Icono de impresión
                        IconButton(
                            onClick = {
                                // Buscar el stock correspondiente al artículo
                                val stockEncontrado = stocks.firstOrNull { 
                                    it.codigoArticulo == articuloParaTraspaso!!.codigoArticulo 
                                }
                                if (stockEncontrado != null) {
                                    tipoImpresion = ImpresionTipo.ARTICULO
                                    articuloParaImprimir = articuloParaTraspaso
                                    stockParaImprimir = stockEncontrado
                                    mostrarDialogoImpresionGenerica = true
                                }
                            }
                        ) {
                            Icon(
                                Icons.Default.Print, 
                                contentDescription = "Imprimir",
                                tint = Color.Black
                            )
                        }
                    }

                    // Pie del diálogo controlado por ti
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(top = 8.dp.coerceAtLeast(0.dp)),
                        horizontalArrangement = Arrangement.End
                    ) {
                        TextButton(
                            onClick = {
                                mostrarDialogoCantidad = false
                                articuloParaTraspaso = null
                                ubicacionParaTraspaso = null
                                cantidadArticulo = "1.0"
                                comentarioTraspaso = ""
                                viewModel.limpiarStock()
                            }
                        ) {
                            Text("Cancelar")
                        }
                    }
                }
            },
            confirmButton = {}, // <- lo dejamos vacío
            dismissButton = null,
            modifier = Modifier
                .fillMaxWidth()
                .wrapContentHeight()
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


    // 1) Lógica de "lote" que ya tienes en Honeywell (la reutilizamos tal cual)
    fun resolverDestino(almacenDestino: String, ubicacionDestino: String) {
        Log.d("RESOLVER_DESTINO", "🎯 INICIO - almacenDestino='$almacenDestino', ubicacionDestino='$ubicacionDestino'")

        val pendientes = viewModel.traspasosPendientes.value
            .filter { it.codigoEstado.equals("PENDIENTE", true) }

        Log.d("RESOLVER_DESTINO", "📋 Traspasos pendientes encontrados: ${pendientes.size}")
        pendientes.forEachIndexed { index, item ->
            Log.d("RESOLVER_DESTINO", "  [$index] id=${item.id}, tipo=${item.tipoTraspaso}, estado=${item.codigoEstado}, paletCerrado=${item.paletCerrado}")
        }

        if (pendientes.isEmpty()) {
            Log.e("RESOLVER_DESTINO", "❌ No hay traspasos pendientes")
            mostrarDialogoErrorFinalizar = "No hay traspasos pendientes"
            return
        }

        val tipo = pendientes.first().tipoTraspaso.uppercase()
        val paletCerrado = pendientes.first().paletCerrado

        Log.d("RESOLVER_DESTINO", "📦 Primer traspaso - tipo='$tipo', paletCerrado=$paletCerrado")

        val total = pendientes.size
        var completados = 0
        var exitos = 0
        var fallo = false

        fun onFinDeLote() {
            if (fallo) {
                esperandoUbicacionDestino = true
                mostrarDialogoExito = false
            } else {
                esperandoUbicacionDestino = false
                mostrarDialogoExito = true
                mostrarDialogoMoverArticuloVM = false
                articuloPendienteMover = null
                ubicacionEscaneada = null
                idPaletParaCerrar = null
                traspasoPendienteId = null
                viewModel.clearPaletSeleccionado()
                viewModel.setTraspasoEsDePalet(false)
                viewModel.setTraspasoDirectoDesdePaletCerrado(false)
                viewModel.clearPendientes()
                viewModel.setArticuloPendienteMover(null)
            }
            // 🔁 SIEMPRE limpiamos la bandera al cerrar lote
            precheckConfirmar = false   // <-- añadido
            escaneoProcesado = false
        }

        when {
            // ——— PALET CERRADO ———
            tipo == "PALET" && paletCerrado -> {
                val body = FinalizarTraspasoPaletDto(
                    almacenDestino        = almacenDestino,
                    ubicacionDestino      = ubicacionDestino,
                    usuarioFinalizacionId = usuarioId,
                    codigoEstado          = "PENDIENTE_ERP"
                )
                pendientes.forEach { dtoItem ->
                    viewModel.finalizarTraspasoPalet(
                        traspasoId = dtoItem.id,
                        dto        = body,
                        paletId    = paletEscaneado?.id,
                        onSuccess  = {
                            exitos++; completados++;
                            if (completados == total) onFinDeLote()
                            SoundUtils.getInstance().playSuccessSound()
                        },
                        onError    = { msg ->
                            mostrarDialogoErrorFinalizar = msg
                            fallo = true; completados++;
                            if (completados == total) onFinDeLote()
                            SoundUtils.getInstance().playErrorSound()
                        }
                    )
                }
            }

            // ——— PALET ABIERTO ———
            tipo == "PALET" -> {
                pendientes.forEach { dtoItem ->
                    viewModel.completarTraspaso(
                        id = dtoItem.id,
                        codigoAlmacenDestino = almacenDestino,
                        ubicacionDestino = ubicacionDestino,
                        usuarioId = usuarioId,
                        paletId = paletEscaneado?.id,
                        onSuccess = {
                            exitos++; completados++;
                            if (completados == total) onFinDeLote()
                            SoundUtils.getInstance().playSuccessSound()
                        },
                        onError = { msg ->
                            mostrarDialogoErrorFinalizar = msg
                            fallo = true; completados++;
                            if (completados == total) onFinDeLote()
                            SoundUtils.getInstance().playErrorSound()
                        }
                    )
                }
                paletEscaneado?.id?.let { id ->
                    viewModel.obtenerPalet(id) { viewModel.setPaletSeleccionado(it) }
                    viewModel.obtenerLineasDePalet(id)
                }
            }

            // ——— ARTÍCULO ———
            else -> {
                Log.d("RESOLVER_DESTINO", "🔄 Entrando en bloque ARTÍCULO - ${pendientes.size} traspasos")
                
                // Función auxiliar para finalizar con los contadores
                fun finalizarConOpciones(
                    traspasoId: String,
                    dto: com.example.sga.data.dto.traspasos.FinalizarTraspasoArticuloDto,
                    esReintento: Boolean = false
                ) {
                    viewModel.finalizarTraspasoArticulo(
                        id = traspasoId,
                        dto = dto,
                        onSuccess = {
                            Log.d("FINALIZAR_ARTICULO", "✅ Finalizado ${if (esReintento) "tras resolver conflicto" else "sin conflictos"}")
                            exitos++; completados++
                            if (completados == total) onFinDeLote()
                            SoundUtils.getInstance().playSuccessSound()
                        },
                        onConflictoPalet = { conflicto ->
                            Log.d("FINALIZAR_ARTICULO", "⚠️ 409 Conflicto detectado")
                            Log.d("FINALIZAR_ARTICULO", "📋 Conflicto - opciones: ${conflicto.opciones}")
                            Log.d("FINALIZAR_ARTICULO", "📋 Conflicto - paletId: ${conflicto.paletId}")
                            Log.d("FINALIZAR_ARTICULO", "📋 Conflicto - cantidadPalets: ${conflicto.cantidadPalets}")
                            Log.d("FINALIZAR_ARTICULO", "📋 Conflicto - palets: ${conflicto.palets}")
                            Log.d("FINALIZAR_ARTICULO", "📋 Conflicto - message: ${conflicto.message}")
                            
                            // Guardar datos del conflicto
                            conflictoPaletActual = conflicto
                            traspasoIdPendienteConflicto = traspasoId
                            almacenDestinoConflicto = almacenDestino
                            ubicacionDestinoConflicto = ubicacionDestino
                            
                            // Guardar callbacks para cuando se resuelva el conflicto
                            onFinalizarConflictoExito = {
                                Log.d("FINALIZAR_ARTICULO", "✅ Finalizado tras resolver conflicto")
                                exitos++; completados++
                                if (completados == total) onFinDeLote()
                            }
                            onFinalizarConflictoError = { msg ->
                                Log.e("FINALIZAR_ARTICULO", "❌ Error tras resolver conflicto: $msg")
                                mostrarDialogoErrorFinalizar = msg
                                fallo = true; completados++
                                if (completados == total) onFinDeLote()
                            }
                            
                            // Si hay opciones definidas, mostrar el diálogo de opciones (PRIORIDAD)
                            if (!conflicto.opciones.isNullOrEmpty()) {
                                Log.d("FINALIZAR_ARTICULO", "📋 Mostrando diálogo con ${conflicto.opciones.size} opciones")
                                mostrarDialogoOpcionesPalet = true
                                esperandoUbicacionDestino = false
                            }
                            // Si NO hay opciones pero hay múltiples palets, cargar info y mostrar GS1
                            else if (!conflicto.palets.isNullOrEmpty()) {
                                Log.d("FINALIZAR_ARTICULO", "📦 Múltiples palets detectados (${conflicto.palets.size}) - Abriendo diálogo de selección")
                                // Cargar información completa de los palets
                                if (almacenDestino != null && ubicacionDestino != null) {
                                    viewModel.precheckFinalizarArticulo(
                                        codigoEmpresa = empresa,
                                        almacenDestino = almacenDestino,
                                        ubicacionDestino = ubicacionDestino,
                                        onResult = { existe, paletId, cerrado, aviso, cantidadPalets, palets ->
                                            if (palets != null && palets.isNotEmpty()) {
                                                paletsDisponiblesEnDestino = palets
                                                mostrarDialogoEscanearPalet = true
                                                esperandoEscaneoGS1 = true
                                                esperandoUbicacionDestino = false
                                            } else {
                                                Log.e("FINALIZAR_ARTICULO", "❌ No se pudieron cargar los palets")
                                                mostrarDialogoErrorFinalizar = "Error al cargar información de palets"
                                                fallo = true; completados++
                                                if (completados == total) onFinDeLote()
                                            }
                                        },
                                        onError = { msg ->
                                            Log.e("FINALIZAR_ARTICULO", "❌ Error al cargar palets: $msg")
                                            mostrarDialogoErrorFinalizar = msg
                                            fallo = true; completados++
                                            if (completados == total) onFinDeLote()
                                        }
                                    )
                                }
                            }
                            // Si no hay opciones ni palets, dejar suelto automáticamente
                            else {
                                Log.d("FINALIZAR_ARTICULO", "⚠️ Sin opciones ni palets - Dejando material suelto automáticamente")
                                
                                val dtoSuelto = com.example.sga.data.dto.traspasos.FinalizarTraspasoArticuloDto(
                                    almacenDestino = almacenDestino,
                                    ubicacionDestino = ubicacionDestino,
                                    usuarioId = usuarioId,
                                    confirmarAgregarAPalet = null,
                                    dejarSuelto = true,
                                    paletIdConfirmado = null
                                )
                                
                                viewModel.finalizarTraspasoArticulo(
                                    id = traspasoId,
                                    dto = dtoSuelto,
                                    onSuccess = {
                                        Log.d("FINALIZAR_ARTICULO", "✅ Material dejado suelto automáticamente")
                                        exitos++; completados++
                                        if (completados == total) onFinDeLote()
                                        SoundUtils.getInstance().playSuccessSound()
                                    },
                                    onError = { msg ->
                                        Log.e("FINALIZAR_ARTICULO", "❌ Error al dejar suelto: $msg")
                                        mostrarDialogoErrorFinalizar = msg
                                        fallo = true; completados++
                                        if (completados == total) onFinDeLote()
                                        SoundUtils.getInstance().playErrorSound()
                                    }
                                )
                            }
                        },
                        onError = { msg ->
                            Log.e("FINALIZAR_ARTICULO", "❌ Error: $msg")
                            mostrarDialogoErrorFinalizar = msg
                            fallo = true; completados++
                            if (completados == total) onFinDeLote()
                            SoundUtils.getInstance().playErrorSound()
                        }
                    )
                }
                
                pendientes.forEach { dtoItem ->
                    Log.d("FINALIZAR_ARTICULO", "📦 Llamando finalizarTraspasoArticulo para id=${dtoItem.id}")
                    
                    // Intentar finalizar directamente, el backend responderá si hay conflicto
                    val dto = com.example.sga.data.dto.traspasos.FinalizarTraspasoArticuloDto(
                        almacenDestino = almacenDestino,
                        ubicacionDestino = ubicacionDestino,
                        usuarioId = usuarioId,
                        confirmarAgregarAPalet = null,
                        dejarSuelto = null,
                        paletIdConfirmado = null
                    )
                    
                    finalizarConOpciones(dtoItem.id, dto, esReintento = false)
                }
            }
        }
    }
    // 2) Captura común que usa procesarCodigoEscaneado y desemboca en la misma lógica de destino
    fun manejarCodigoDestino(code: String) {
        Log.d("MANEJAR_DESTINO", "📷 Código escaneado: '$code'")

        viewModel.procesarCodigoEscaneado(
            code = code,
            empresaId = empresa,
            onUbicacionDetectada = { almacenDestino, ubicacionDestino ->
                Log.d("MANEJAR_DESTINO", "🏢 Ubicación detectada: almacen='$almacenDestino', ubicacion='$ubicacionDestino'")

                if (!viewModel.almacenesPermitidos.value.contains(almacenDestino)) {
                    Log.e("MANEJAR_DESTINO", "❌ Ubicación no permitida: '$almacenDestino'")
                    mostrarDialogoErrorFinalizar = "Ubicación no permitida."
                    return@procesarCodigoEscaneado
                }

                Log.d("MANEJAR_DESTINO", "✅ Ubicación válida - Llamando resolverDestino")
                resolverDestino(almacenDestino, ubicacionDestino)
            },
            // En la fase de destino, el resto de detecciones NO aplican
            onArticuloDetectado = {
                Log.d("MANEJAR_DESTINO", "⚠️ Se detectó artículo pero NO aplica en fase de destino")
            },
            onMultipleArticulos = {
                Log.d("MANEJAR_DESTINO", "⚠️ Se detectaron múltiples artículos pero NO aplica en fase de destino")
            },
            onPaletDetectado = {
                Log.d("MANEJAR_DESTINO", "⚠️ Se detectó palet pero NO aplica en fase de destino")
            },
            onError = { msg ->
                Log.e("MANEJAR_DESTINO", "❌ Error al procesar código: $msg")
                mostrarDialogoErrorFinalizar = msg
            }
        )
    }

    if (esperandoUbicacionDestino && DeviceUtils.hasHardwareScanner(context)) {
        Box(
            modifier = Modifier
                .fillMaxSize()
                .focusRequester(focusRequester)
                .focusable()
                .onPreviewKeyEvent { event ->
                    if (event.nativeKeyEvent?.action == android.view.KeyEvent.ACTION_MULTIPLE) {
                        event.nativeKeyEvent.characters?.let { code ->
                            manejarCodigoDestino(code)
                            return@onPreviewKeyEvent true
                        }
                    }
                    false
                }
        ) {}

        LaunchedEffect(esperandoUbicacionDestino && DeviceUtils.hasHardwareScanner(context)) {
            if (esperandoUbicacionDestino){
                delay(200)
                focusRequester.requestFocus()
            }
        }
    }
    // —— Estado para evitar reescaneos continuos en cámara ——
    val scope = rememberCoroutineScope()
    var procesandoDestino by remember { mutableStateOf(false) }
    var ultimoCodigo by remember { mutableStateOf<String?>(null) }

// Escaneo de ubicación destino (QRScannerView) — MÓVIL / TABLET
    if (esperandoUbicacionDestino && !DeviceUtils.hasHardwareScanner(context)) {
        QRScannerView(
            modifier = Modifier
                .fillMaxWidth(0.5f)
                .aspectRatio(1f), // Hace que sea cuadrado (mismo ancho que altura)
            onCodeScanned = { raw ->
                if (!esperandoUbicacionDestino) return@QRScannerView

                val code = raw.trim()
                // Debounce: ignora si ya estamos procesando o si es el mismo código repetido
                if (procesandoDestino || ultimoCodigo == code) return@QRScannerView

                procesandoDestino = true
                ultimoCodigo = code

                // Usa SIEMPRE la misma entrada común que en PDA
                manejarCodigoDestino(code)

                // Pequeña ventana para evitar múltiples lecturas consecutivas del mismo QR
                scope.launch {
                    kotlinx.coroutines.delay(900) // ajusta si hace falta
                    procesandoDestino = false
                }
            }
        )
    }

// (Opcional) Cuando se cierre el flujo de destino, resetea el lock
    LaunchedEffect(esperandoUbicacionDestino) {
        if (!esperandoUbicacionDestino) {
            procesandoDestino = false
            ultimoCodigo = null
        }
    }

    // Mensaje de éxito
    if (mostrarDialogoExito) {
        AlertDialog(
            onDismissRequest = {
                mostrarDialogoExito = false
                ubicacionEscaneada = null
                escaneoProcesado = false
                idPaletParaCerrar = null
                traspasoPendienteId = null
                articuloPendienteMover = null

                viewModel.clearPaletSeleccionado()
                viewModel.setTraspasoEsDePalet(false)
                viewModel.setTraspasoDirectoDesdePaletCerrado(false)
                viewModel.clearPendientes()
                viewModel.setArticuloPendienteMover(null)
            },
            title = { Text("Traspaso realizado") },
            text = { Text("Traspaso realizado con éxito.") },
            confirmButton = {
                TextButton(onClick = {
                    mostrarDialogoExito = false
                    ubicacionEscaneada = null
                    escaneoProcesado = false
                    idPaletParaCerrar = null
                    traspasoPendienteId = null
                    articuloPendienteMover = null
                    viewModel.clearPaletSeleccionado()
                    viewModel.setTraspasoEsDePalet(false)
                    viewModel.setTraspasoDirectoDesdePaletCerrado(false)
                    viewModel.clearPendientes()
                    viewModel.setArticuloPendienteMover(null)
                }) { Text("Aceptar") }
            },
            dismissButton = null
        )
    }

    LaunchedEffect(mostrarDialogoExito) {
        if (!mostrarDialogoExito && ubicacionEscaneada == null && DeviceUtils.hasHardwareScanner(context)) {
            escaneoProcesado = false
            delay(200)
            focusRequester.requestFocus()
        }
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
    }
    if (!esperandoUbicacionDestino && mostrarDialogoCantidadDesdePalet && lineaSeleccionada != null) {
        AlertDialog(
            onDismissRequest = { /* bloqueamos para forzar acción */ },
            title = { Text("Cantidad a extraer") },
            text = {
                Column {
                    OutlinedTextField(
                        value = cantidadExtraer,
                        onValueChange = {
                            val limpio = it.filter { c -> c.isDigit() || c=='.' }
                            cantidadExtraer = limpio
                        },
                        label = { Text("Cantidad (máx. ${FormatUtils.formatearCantidad(lineaSeleccionada!!.cantidad)})") },
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth()
                    )
                }
            },
            confirmButton = {
                TextButton(onClick = {
                    val qty = cantidadExtraer.toDoubleOrNull() ?: 0.0
                    val max = lineaSeleccionada!!.cantidad
                    if (qty <= 0.0 || qty > max) return@TextButton

                    val ubi = viewModel.ubicacionOrigen.value ?: return@TextButton
                    val (almOrigen, ubicOrigen) = ubi
                    val linea = lineaSeleccionada!!

                    // CERRAR YA EL DIÁLOGO Y LIMPIAR SELECCIÓN
                    mostrarDialogoCantidadDesdePalet = false
                    lineaSeleccionada = null
                    cantidadExtraer = "1.0"
                    comentarioTraspaso = ""
                    escaneoProcesado = false   // permite el siguiente escaneo

                    viewModel.cargarArticuloPorCodigo(
                        empresaId = empresaId ?: return@TextButton,
                        codigoArticulo = linea.codigoArticulo,
                        onSuccess = { artApi ->
                            val articuloDesdeLinea = artApi.copy(
                                partida = linea.lote,
                                fechaCaducidad = linea.fechaCaducidad
                            )

                            viewModel.crearTraspasoArticulo(
                                dto = CrearTraspasoArticuloDto(
                                    codigoEmpresa   = empresa,
                                    almacenOrigen   = almOrigen,
                                    ubicacionOrigen = ubicOrigen,
                                    codigoArticulo  = linea.codigoArticulo,
                                    cantidad        = qty,
                                    usuarioId       = usuarioId,
                                    partida         = linea.lote,
                                    fechaCaducidad  = linea.fechaCaducidad,
                                    finalizar       = false,
                                    descripcionArticulo = linea.descripcion ?: artApi.descripcion,
                                    comentario = comentarioTraspaso.takeIf { it.isNotBlank() },
                                ),
                                onSuccess = { id ->
                                    viewModel.setArticuloPendienteMover(articuloDesdeLinea)
                                    traspasoPendienteId = id
                                    ubicacionEscaneada = null  // Limpiar ubicación para forzar nuevo escaneo de destino
                                    esperandoUbicacionDestino = true
                                    mostrarDialogoMoverArticuloVM = true  // tu bloqueo
                                    SoundUtils.getInstance().playSuccessSound()
                                },
                                onError = { msg ->
                                    errorTraspasoArticulo = msg
                                    SoundUtils.getInstance().playErrorSound()
                                }
                            )
                        },
                        onError = { msg ->
                            errorTraspasoArticulo = msg
                            SoundUtils.getInstance().playErrorSound()
                        }
                    )
                }) {
                    Text("Traspasar artículo")
                }
            },
            dismissButton = {
                TextButton(onClick = {
                    mostrarDialogoCantidadDesdePalet = false
                    lineaSeleccionada = null
                    cantidadExtraer = "1.0"
                    comentarioTraspaso = ""
                }) { Text("Cancelar") }
            }
        )
    }

    // ——— Diálogo de confirmación PRECHECK (ARTÍCULO) ———
    if (mostrarDialogoPrecheck) {
        AlertDialog(
            onDismissRequest = {
                mostrarDialogoPrecheck = false
                precheckAviso = null
                accionTrasConfirmacion = null
                precheckConfirmar = false
                esperandoUbicacionDestino = true
            },
            title = { Text("Confirmar paletización") },
            text  = { Text(precheckAviso ?: "Hay un palet en destino. ¿Desea continuar?") },
            confirmButton = {
                TextButton(onClick = {
                    precheckConfirmar = true
                    val accion = accionTrasConfirmacion
                    mostrarDialogoPrecheck = false
                    precheckAviso = null
                    accionTrasConfirmacion = null
                    accion?.invoke()
                }) { Text("Continuar") }
            },
            dismissButton = {
                TextButton(onClick = {
                    mostrarDialogoPrecheck = false
                    precheckAviso = null
                    accionTrasConfirmacion = null
                    precheckConfirmar = false
                    esperandoUbicacionDestino = true
                }) { Text("Cancelar") }
            }
        )
    }

    // ——— DIÁLOGO DE OPCIONES DE PALET (409 Conflict) ———
    if (mostrarDialogoOpcionesPalet && conflictoPaletActual != null) {
        val conflicto = conflictoPaletActual!!
        val traspasoId = traspasoIdPendienteConflicto
        val almDest = almacenDestinoConflicto
        val ubiDest = ubicacionDestinoConflicto
        
        AlertDialog(
            onDismissRequest = { /* No hacer nada */ },
            properties = DialogProperties(
                dismissOnBackPress = false,
                dismissOnClickOutside = false
            ),
            title = { 
                Text(
                    text = "Palet detectado en destino",
                    style = MaterialTheme.typography.titleLarge,
                    fontWeight = androidx.compose.ui.text.font.FontWeight.Bold
                )
            },
            text = {
                Column(modifier = Modifier.fillMaxWidth()) {
                    Text(
                        text = "Elija una opción:",
                        style = MaterialTheme.typography.bodyMedium,
                        modifier = Modifier.padding(bottom = 16.dp)
                    )
                    
                    // Botones de opciones
                    conflicto.opciones?.forEach { opcion ->
                        when (opcion.tipo) {
                            "paletizar" -> {
                                Button(
                                    onClick = {
                                        Log.d("OPCIONES_PALET", "📦 Usuario eligió escanear palet")
                                        // Cerrar este diálogo y abrir el de escaneo GS1
                                        mostrarDialogoOpcionesPalet = false
                                        mostrarDialogoEscanearPalet = true
                                        esperandoEscaneoGS1 = true
                                    },
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .padding(vertical = 4.dp),
                                    colors = ButtonDefaults.buttonColors(
                                        containerColor = MaterialTheme.colorScheme.primary
                                    )
                                ) {
                                    Icon(
                                        imageVector = Icons.Default.QrCodeScanner,
                                        contentDescription = null,
                                        modifier = Modifier.size(20.dp)
                                    )
                                    Spacer(modifier = Modifier.width(8.dp))
                                    Text("Escanear palet para paletizar")
                                }
                            }
                            "suelto" -> {
                                Button(
                                    onClick = {
                                        Log.d("OPCIONES_PALET", "✅ Usuario eligió: Dejar suelto")
                                        if (traspasoId != null && almDest != null && ubiDest != null) {
                                            val dtoSuelto = com.example.sga.data.dto.traspasos.FinalizarTraspasoArticuloDto(
                                                almacenDestino = almDest,
                                                ubicacionDestino = ubiDest,
                                                usuarioId = usuarioId,
                                                confirmarAgregarAPalet = null,
                                                dejarSuelto = true,
                                                paletIdConfirmado = null
                                            )
                                            
                                            viewModel.finalizarTraspasoArticulo(
                                                id = traspasoId,
                                                dto = dtoSuelto,
                                                onSuccess = {
                                                    Log.d("OPCIONES_PALET", "✅ Material dejado suelto")
                                                    mostrarDialogoOpcionesPalet = false
                                                    conflictoPaletActual = null
                                                    esperandoUbicacionDestino = false
                                                    onFinalizarConflictoExito?.invoke()
                                                    onFinalizarConflictoExito = null
                                                    onFinalizarConflictoError = null
                                                },
                                                onError = { error ->
                                                    Log.e("OPCIONES_PALET", "❌ Error: $error")
                                                    mostrarDialogoOpcionesPalet = false
                                                    onFinalizarConflictoError?.invoke(error)
                                                    onFinalizarConflictoExito = null
                                                    onFinalizarConflictoError = null
                                                }
                                            )
                                        }
                                    },
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .padding(vertical = 4.dp),
                                    colors = ButtonDefaults.buttonColors(
                                        containerColor = MaterialTheme.colorScheme.secondary
                                    )
                                ) {
                                    Icon(
                                        imageVector = Icons.Default.Add,
                                        contentDescription = null,
                                        modifier = Modifier.size(20.dp)
                                    )
                                    Spacer(modifier = Modifier.width(8.dp))
                                    Text("Dejar material suelto")
                                }
                            }
                            "cancelar" -> {
                                // Este caso ya no se usa - el botón de cancelar está en la parte inferior
                            }
                        }
                    }
                    
                    // Botón de cancelar en la parte inferior
                    Spacer(modifier = Modifier.height(8.dp))
                    OutlinedButton(
                        onClick = {
                            Log.d("OPCIONES_PALET", "🚫 Usuario canceló desde botón inferior")
                            mostrarDialogoOpcionesPalet = false
                            conflictoPaletActual = null
                            traspasoIdPendienteConflicto = null
                            almacenDestinoConflicto = null
                            ubicacionDestinoConflicto = null
                            esperandoUbicacionDestino = true
                            // Limpiar callbacks sin invocarlos - cancelar no es un error
                            onFinalizarConflictoExito = null
                            onFinalizarConflictoError = null
                        },
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Icon(
                            imageVector = Icons.Default.Close,
                            contentDescription = null,
                            modifier = Modifier.size(20.dp)
                        )
                        Spacer(modifier = Modifier.width(8.dp))
                        Text("Cancelar y buscar otra ubicación")
                    }
                }
            },
            confirmButton = {}, // Vacío porque los botones están en el text
            dismissButton = null
        )
    }

    // ——— Variables para diálogo de GS1 (Múltiples palets en destino) ———
    var errorValidacionGS1 by remember { mutableStateOf<String?>(null) }
    val focusRequesterGS1 = remember { FocusRequester() }

    // Función para procesar el GS1 escaneado
    fun procesarGS1Escaneado(codigoCompleto: String) {
        Log.d("GS1_DIALOG", "📦 Código escaneado: '$codigoCompleto'")

        // Validar GS1 contra los palets disponibles
        val paletIdValidado = viewModel.validarGS1ContraPalets(
            codigoEscaneado = codigoCompleto,
            paletsDisponibles = paletsDisponiblesEnDestino
        )

        if (paletIdValidado != null) {
            // GS1 válido, finalizar traspaso con este palet
            Log.d("GS1_DIALOG", "✅ GS1 válido, palet ID: $paletIdValidado")
            mostrarDialogoEscanearPalet = false
            esperandoEscaneoGS1 = false
            
            // Finalizar el traspaso con el palet escaneado
            val traspasoId = traspasoIdPendienteConflicto
            val almDest = almacenDestinoConflicto
            val ubiDest = ubicacionDestinoConflicto
            
            if (traspasoId != null && almDest != null && ubiDest != null) {
                val dtoConfirm = com.example.sga.data.dto.traspasos.FinalizarTraspasoArticuloDto(
                    almacenDestino = almDest,
                    ubicacionDestino = ubiDest,
                    usuarioId = usuarioId,
                    confirmarAgregarAPalet = true,
                    dejarSuelto = null,
                    paletIdConfirmado = paletIdValidado
                )
                
                viewModel.finalizarTraspasoArticulo(
                    id = traspasoId,
                    dto = dtoConfirm,
                    onSuccess = {
                        Log.d("GS1_DIALOG", "✅ Artículo agregado al palet escaneado")
                        traspasoIdPendienteConflicto = null
                        almacenDestinoConflicto = null
                        ubicacionDestinoConflicto = null
                        esperandoUbicacionDestino = false
                        onFinalizarConflictoExito?.invoke()
                        onFinalizarConflictoExito = null
                        onFinalizarConflictoError = null
                        paletsDisponiblesEnDestino = emptyList()
                        SoundUtils.getInstance().playSuccessSound()
                    },
                    onError = { error ->
                        Log.e("GS1_DIALOG", "❌ Error: $error")
                        mostrarDialogoErrorFinalizar = error
                        onFinalizarConflictoError?.invoke(error)
                        onFinalizarConflictoExito = null
                        onFinalizarConflictoError = null
                        paletsDisponiblesEnDestino = emptyList()
                        SoundUtils.getInstance().playErrorSound()
                    }
                )
            }
        } else {
            Log.w("GS1_DIALOG", "❌ GS1 no válido: '$codigoCompleto'")
            errorValidacionGS1 = "El código GS1 escaneado no corresponde a ningún palet en esta ubicación."
            SoundUtils.getInstance().playErrorSound()
        }
    }


    // ——— Diálogo visual de GS1 (Múltiples palets en destino) ———
    if (mostrarDialogoEscanearPalet) {
        var codigoCapturado by remember { mutableStateOf("") }
        
        // Cargar palets cuando se abre el diálogo
        LaunchedEffect(mostrarDialogoEscanearPalet) {
            val almDest = almacenDestinoConflicto
            val ubiDest = ubicacionDestinoConflicto
            
            if (almDest != null && ubiDest != null && paletsDisponiblesEnDestino.isEmpty()) {
                viewModel.precheckFinalizarArticulo(
                    codigoEmpresa = empresa,
                    almacenDestino = almDest,
                    ubicacionDestino = ubiDest,
                    onResult = { existe, paletId, cerrado, aviso, cantidadPalets, palets ->
                        if (palets != null && palets.isNotEmpty()) {
                            paletsDisponiblesEnDestino = palets
                        }
                    },
                    onError = { msg ->
                        Log.e("GS1_DIALOG", "Error al obtener palets: $msg")
                    }
                )
            }
        }

        AlertDialog(
            onDismissRequest = {
                mostrarDialogoEscanearPalet = false
                esperandoEscaneoGS1 = false
                paletsDisponiblesEnDestino = emptyList()
                paletIdDestinoSeleccionado = null
                errorValidacionGS1 = null
                esperandoUbicacionDestino = true
            },
            title = { Text("Múltiples palets en destino") },
            text = {
                val focusRequesterDialogBox = remember { FocusRequester() }

                LaunchedEffect(Unit) {
                    delay(400)
                    try {
                        focusRequesterDialogBox.requestFocus()
                        Log.d("GS1_DIALOG", "🎯 Focus solicitado al Box del diálogo")
                    } catch (e: Exception) {
                        Log.e("GS1_DIALOG", "❌ Error al solicitar focus: ${e.message}")
                    }
                }

                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .focusRequester(focusRequesterDialogBox)
                        .focusable()
                        .onPreviewKeyEvent { event ->
                            Log.d("GS1_DIALOG", "🔍 Box recibió KeyEvent - action: ${event.nativeKeyEvent?.action}")
                            // Capturar el escaneo directamente desde el diálogo
                            if (event.nativeKeyEvent?.action == android.view.KeyEvent.ACTION_MULTIPLE) {
                                event.nativeKeyEvent.characters?.let { code ->
                                    Log.d("GS1_DIALOG", "✅ Diálogo capturó escaneo: '$code'")
                                    procesarGS1Escaneado(code.trim())
                                    return@onPreviewKeyEvent true
                                }
                            }
                            false
                        }
                ) {
                    Column {

                    Text("Se encontraron ${paletsDisponiblesEnDestino.size} palets en la ubicación destino.")
                    Spacer(modifier = Modifier.height(8.dp))
                    Text("Escanea el código GS1 del palet donde deseas agregar el artículo:",
                        style = MaterialTheme.typography.bodyMedium,
                        fontWeight = FontWeight.Bold)

                    Spacer(modifier = Modifier.height(12.dp))

                    // Listado de palets disponibles con formato GS1 completo
                    Text("Palets disponibles:", fontWeight = FontWeight.Bold)
                    paletsDisponiblesEnDestino.forEach { palet ->
                        val gs1Display = palet.codigoGS1?.let { "00$it" } ?: "Sin GS1"
                        Text(
                            "• ${palet.codigoPalet}",
                            style = MaterialTheme.typography.bodyMedium,
                            fontWeight = FontWeight.Bold,
                            modifier = Modifier.padding(start = 8.dp, top = 4.dp)
                        )
                        Text(
                            "  GS1: $gs1Display",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            modifier = Modifier.padding(start = 8.dp, bottom = 4.dp)
                        )
                    }

                    Spacer(modifier = Modifier.height(16.dp))

                    // Indicador visual de espera de escaneo
                    Card(
                        modifier = Modifier.fillMaxWidth(),
                        colors = CardDefaults.cardColors(
                            containerColor = MaterialTheme.colorScheme.primaryContainer
                        )
                    ) {
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(12.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Icon(
                                imageVector = Icons.Default.QrCodeScanner,
                                contentDescription = "Escaneando",
                                tint = MaterialTheme.colorScheme.primary,
                                modifier = Modifier.size(32.dp)
                            )
                            Spacer(modifier = Modifier.width(12.dp))
                            Text(
                                "Esperando escaneo de GS1...",
                                style = MaterialTheme.typography.bodyMedium,
                                color = MaterialTheme.colorScheme.onPrimaryContainer
                            )
                        }
                    }

                    // Mostrar error de validación si existe
                    errorValidacionGS1?.let { error ->
                        Spacer(modifier = Modifier.height(12.dp))
                        Card(
                            modifier = Modifier.fillMaxWidth(),
                            colors = CardDefaults.cardColors(
                                containerColor = MaterialTheme.colorScheme.errorContainer
                            )
                        ) {
                            Row(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .padding(12.dp),
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                Icon(
                                    imageVector = Icons.Default.Error,
                                    contentDescription = "Error",
                                    tint = MaterialTheme.colorScheme.error,
                                    modifier = Modifier.size(24.dp)
                                )
                                Spacer(modifier = Modifier.width(12.dp))
                                Text(
                                    text = error,
                                    color = MaterialTheme.colorScheme.onErrorContainer,
                                    style = MaterialTheme.typography.bodySmall
                                )
                            }
                        }
                    }
                    } // Fin Column
                } // Fin Box
            },
            confirmButton = {},
            dismissButton = {
                TextButton(onClick = {
                    mostrarDialogoEscanearPalet = false
                    esperandoEscaneoGS1 = false
                    paletsDisponiblesEnDestino = emptyList()
                    paletIdDestinoSeleccionado = null
                    errorValidacionGS1 = null
                    esperandoUbicacionDestino = true
                }) {
                    Text("Cancelar")
                }
            }
        )
    }

    // Diálogo de búsqueda de stock
    if (mostrarDialogoBusquedaStock) {
        AlertDialog(
            onDismissRequest = { mostrarDialogoBusquedaStock = false },
            title = { Text("Consulta de Stock") },
            text = {
                StockSearchContent(
                    sessionViewModel = sessionViewModel,
                    onDismiss = { mostrarDialogoBusquedaStock = false }
                )
            },
            confirmButton = {},
            dismissButton = {
                TextButton(onClick = { mostrarDialogoBusquedaStock = false }) {
                    Text("Cerrar")
                }
            }
        )
    }

    // Diálogo de escaneo de palet específico
    if (mostrarDialogoEscaneoPalet) {
        var errorValidacion by remember { mutableStateOf<String?>(null) }
        val focusRequesterDialogo = remember { FocusRequester() }

        // Función para validar el código escaneado usando procesarCodigoEscaneado
        fun validarCodigoEscaneado(codigo: String) {
            viewModel.procesarCodigoEscaneado(
                code = codigo,
                empresaId = empresa,
                onUbicacionDetectada = { _, _ -> 
                    // No aplica en este contexto
                },
                onPaletDetectado = { palet ->
                    // Buscar el palet en los stocks disponibles por código de palet
                    val paletEncontrado = stocksDisponibles.find { stock ->
                        stock.tipoStock == "Paletizado" &&
                        stock.codigoPalet == palet.codigoPalet
                    }
                    
                    if (paletEncontrado != null) {
                        paletEscaneadoLocal = paletEncontrado
                        errorValidacion = null
                        SoundUtils.getInstance().playSuccessSound()
                    } else {
                        errorValidacion = "El palet escaneado no está disponible para este artículo."
                        paletEscaneadoLocal = null
                        SoundUtils.getInstance().playErrorSound()
                    }
                },
                onArticuloDetectado = { _ ->
                    errorValidacion = "Se detectó un artículo, pero se esperaba un palet."
                    paletEscaneadoLocal = null
                    SoundUtils.getInstance().playErrorSound()
                },
                onMultipleArticulos = { _ ->
                    errorValidacion = "Se detectaron múltiples artículos, pero se esperaba un palet."
                    paletEscaneadoLocal = null
                    SoundUtils.getInstance().playErrorSound()
                },
                onError = { msg ->
                    errorValidacion = msg
                    paletEscaneadoLocal = null
                    SoundUtils.getInstance().playErrorSound()
                }
            )
        }

        AlertDialog(
            onDismissRequest = {
                mostrarDialogoEscaneoPalet = false
                // NO limpiar paletEscaneadoEspecifico - se mantiene para usar
                stocksDisponibles = emptyList()
                errorValidacion = null
            },
            title = { Text("Escanear palet específico") },
            text = {
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .heightIn(max = 400.dp)
                        .verticalScroll(rememberScrollState()),
                    verticalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    Text("Escanea el código del palet que quieres usar:")

                    // Mostrar lista de palets disponibles
                    val paletsDisponibles = stocksDisponibles.filter { it.tipoStock == "Paletizado" }
                    if (paletsDisponibles.isNotEmpty()) {
                        Text("Palets disponibles:", fontWeight = FontWeight.Bold)
                        paletsDisponibles.forEach { palet ->
                            Text(
                                "• ${palet.codigoPalet ?: "N/A"} (${palet.estadoPalet}) - ${FormatUtils.formatearCantidad(palet.unidadesSaldo)} unidades",
                                style = MaterialTheme.typography.bodySmall,
                                modifier = Modifier.padding(start = 8.dp)
                            )
                        }
                        Spacer(modifier = Modifier.height(8.dp))
                    }

                    // Indicador visual de espera de escaneo
                    Card(
                        modifier = Modifier.fillMaxWidth(),
                        colors = CardDefaults.cardColors(
                            containerColor = MaterialTheme.colorScheme.primaryContainer
                        )
                    ) {
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(12.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Icon(
                                imageVector = Icons.Default.QrCodeScanner,
                                contentDescription = "Escaneando",
                                tint = MaterialTheme.colorScheme.primary,
                                modifier = Modifier.size(32.dp)
                            )
                            Spacer(modifier = Modifier.width(12.dp))
                            Text(
                                "Esperando escaneo de palet...",
                                style = MaterialTheme.typography.bodyMedium,
                                color = MaterialTheme.colorScheme.onPrimaryContainer
                            )
                        }
                    }

                    // Mostrar error si existe
                    errorValidacion?.let { error ->
                        Card(
                            modifier = Modifier.fillMaxWidth(),
                            colors = CardDefaults.cardColors(
                                containerColor = MaterialTheme.colorScheme.errorContainer
                            )
                        ) {
                            Row(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .padding(12.dp),
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                Icon(
                                    imageVector = Icons.Default.Error,
                                    contentDescription = "Error",
                                    tint = MaterialTheme.colorScheme.error,
                                    modifier = Modifier.size(24.dp)
                                )
                                Spacer(modifier = Modifier.width(12.dp))
                                Text(
                                    text = error,
                                    color = MaterialTheme.colorScheme.onErrorContainer,
                                    style = MaterialTheme.typography.bodySmall
                                )
                            }
                        }
                    }

                    // Mostrar palet seleccionado si existe
                    paletEscaneadoLocal?.let { palet ->
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
                                Text("Estado: ${palet.estadoPalet}")
                                if (palet.ordenTrabajoId != null) {
                                    Text("Orden de Trabajo: ${palet.ordenTrabajoId}")
                                }
                                Text("Cantidad: ${FormatUtils.formatearCantidad(palet.unidadesSaldo)} unidades")
                                Text("Ubicación: ${palet.ubicacion}")
                            }
                        }
                    }

                    // Box invisible para capturar escaneos
                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .focusRequester(focusRequesterDialogo)
                            .focusable()
                            .onPreviewKeyEvent { event ->
                                if (event.nativeKeyEvent?.action == android.view.KeyEvent.ACTION_MULTIPLE) {
                                    event.nativeKeyEvent.characters?.let { code ->
                                        validarCodigoEscaneado(code.trim())
                                        return@onPreviewKeyEvent true
                                    }
                                }
                                false
                            }
                    ) {
                        // Contenido invisible para capturar focus
                    }
                }
            },
            confirmButton = {
                Button(
                    onClick = {
                        
                        // Cerrar el diálogo y mantener el palet escaneado
                        mostrarDialogoEscaneoPalet = false
                        stocksDisponibles = emptyList()
                        errorValidacion = null
                        
                        // paletEscaneadoLocal ya está establecido en validarCodigoEscaneado
                    },
                    enabled = paletEscaneadoLocal != null
                ) {
                    Text("Usar este palet")
                }
            },
            dismissButton = {
                TextButton(onClick = {
                    mostrarDialogoEscaneoPalet = false
                    paletEscaneadoLocal = null
                    stocksDisponibles = emptyList()
                    errorValidacion = null
                }) {
                    Text("Cancelar")
                }
            }
        )

        // Solicitar focus cuando se abre el diálogo
        LaunchedEffect(mostrarDialogoEscaneoPalet) {
            if (mostrarDialogoEscaneoPalet && DeviceUtils.hasHardwareScanner(context)) {
                delay(200)
                focusRequesterDialogo.requestFocus()
            }
        }
    }

}






