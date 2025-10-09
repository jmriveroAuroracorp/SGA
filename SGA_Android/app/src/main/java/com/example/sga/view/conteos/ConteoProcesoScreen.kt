package com.example.sga.view.conteos

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.collectAsState
import kotlinx.coroutines.delay
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import android.util.Log
import com.example.sga.data.model.conteos.OrdenConteo
import com.example.sga.data.model.conteos.LecturaConteo
import com.example.sga.data.model.conteos.LecturaPendiente
import com.example.sga.data.model.conteos.EstadoEscaneoConteo
import com.example.sga.view.components.AppTopBar
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.ImeAction
import com.example.sga.service.lector.DeviceUtils
import androidx.compose.ui.platform.LocalContext
import com.example.sga.service.scanner.QRScannerView
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.input.key.onPreviewKeyEvent
import androidx.compose.ui.layout.layout
import androidx.compose.ui.platform.LocalFocusManager
import androidx.activity.compose.BackHandler
import androidx.compose.foundation.focusable
import com.example.sga.view.conteos.EstadoChip
import com.example.sga.view.conteos.InfoRow
import com.example.sga.data.dto.traspasos.components.DialogSeleccionArticulo

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ConteoProcesoScreen(
    ordenGuid: String,
    conteoViewModel: ConteoViewModel,
    conteoLogic: ConteoLogic,
    sessionViewModel: com.example.sga.view.app.SessionViewModel,
    navController: androidx.navigation.NavHostController
) {
    val ordenSeleccionada by conteoViewModel.ordenSeleccionada.collectAsState()
    val lecturasPendientes by conteoViewModel.lecturasPendientes.collectAsState()
    val cargando by conteoViewModel.cargando.collectAsState()
    val error by conteoViewModel.error.collectAsState()
    val mensaje by conteoViewModel.mensaje.collectAsState()
    val user by sessionViewModel.user.collectAsState()
    val context = LocalContext.current
    
    // Estados de escaneo
    val estadoEscaneo by conteoViewModel.estadoEscaneo.collectAsState()
    val ubicacionEscaneada by conteoViewModel.ubicacionEscaneada.collectAsState()
    val articuloEscaneado by conteoViewModel.articuloEscaneado.collectAsState()
    val lecturasCompletadas by conteoViewModel.lecturasCompletadas.collectAsState()
    val mostrarDialogoConfirmacionArticulo by conteoViewModel.mostrarDialogoConfirmacionArticulo.collectAsState()
    val articuloParaConfirmar by conteoViewModel.articuloParaConfirmar.collectAsState()
    val mostrarDialogoSeleccionArticulo by conteoViewModel.mostrarDialogoSeleccionArticulo.collectAsState()
    val articulosFiltrados by conteoViewModel.articulosFiltrados.collectAsState()
    val conteoCompletado by conteoViewModel.conteoCompletado.collectAsState()
    val modoLecturaManual by conteoViewModel.modoLecturaManual.collectAsState()
    
         // Estados locales
     var escaneando by remember { mutableStateOf(false) }
     var escaneoProcesado by remember { mutableStateOf(false) }
     var cantidadInput by remember { mutableStateOf("") }
     var comentarioInput by remember { mutableStateOf("") }
     var mostrarInputCantidad by remember { mutableStateOf(false) }
     
     // Estados para modo manual
     var ubicacionManual by remember { mutableStateOf<String?>(null) }
     var articuloManual by remember { mutableStateOf<String?>(null) }
     var partidaManual by remember { mutableStateOf<String?>(null) }
     var fechaCaducidadManual by remember { mutableStateOf<String?>(null) }
    val focusRequester = remember { FocusRequester() }
    val focusManager = LocalFocusManager.current

    // Cargar orden y lecturas al entrar en la pantalla
    LaunchedEffect(ordenGuid) {
        conteoLogic.obtenerOrden(ordenGuid)
        conteoLogic.obtenerLecturasPendientes(ordenGuid, user?.id)
        // Iniciar escaneo
        conteoViewModel.setEstadoEscaneo(EstadoEscaneoConteo.EsperandoUbicacion)
    }

    // Navegar de vuelta cuando el conteo esté completado
    LaunchedEffect(conteoCompletado) {
        if (conteoCompletado) {
            delay(2000) // Mostrar mensaje por 2 segundos
            navController.navigate("conteos") {
                popUpTo("conteos") { inclusive = true }
            }
        }
    }

    // Manejar escaneo en PDA
    LaunchedEffect(Unit) {
        val hasScanner = DeviceUtils.hasHardwareScanner(context)
        Log.d("ConteoProcesoScreen", "🔍 Verificando dispositivo: hasHardwareScanner = $hasScanner")
        Log.d("ConteoProcesoScreen", "📊 Scanner info: ${DeviceUtils.hardwareScannerHint(context)}")
        if (hasScanner) {
            delay(1000) // Delay más largo para asegurar que la UI esté completamente lista
            try {
                focusRequester.requestFocus()
                Log.d("ConteoProcesoScreen", "✅ Focus solicitado para PDA")
            } catch (e: Exception) {
                Log.e("ConteoProcesoScreen", "❌ Error al solicitar focus: ${e.message}")
            }
        } else {
            Log.d("ConteoProcesoScreen", "📱 Dispositivo móvil detectado")
        }
    }

    Scaffold(
        topBar = {
            AppTopBar(
                sessionViewModel = sessionViewModel,
                navController = navController,
                title = ordenSeleccionada?.titulo ?: "Proceso de Conteo",
                showBackButton = false,
                customNavigationIcon = {
                    IconButton(onClick = {
                        navController.navigate("conteos") {
                            popUpTo("conteos") { inclusive = true }
                        }
                    }) {
                        Icon(
                            imageVector = Icons.AutoMirrored.Filled.ArrowBack,
                            contentDescription = "Volver a Conteos"
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

            if (cargando) {
                Box(
                    modifier = Modifier.fillMaxSize(),
                    contentAlignment = Alignment.Center
                ) {
                    CircularProgressIndicator()
                }
            } else {
                LazyColumn(
                    modifier = Modifier.fillMaxSize(),
                    contentPadding = PaddingValues(16.dp),
                    verticalArrangement = Arrangement.spacedBy(16.dp)
                ) {
                    // Información de la orden
                    ordenSeleccionada?.let { orden ->
                        item {
                            OrdenInfoCard(orden = orden, conteoViewModel = conteoViewModel)
                        }
                    }

                
                                         // Estado actual del escaneo
                     item {
                         EstadoEscaneoCard(
                             estadoEscaneo = estadoEscaneo,
                             ubicacionEscaneada = ubicacionEscaneada,
                             articuloEscaneado = articuloEscaneado,
                             modoLecturaManual = modoLecturaManual
                         )
                     }

                                           // Aplicar filtrado de almacenes permitidos y del centro
                        val lecturasFiltradas = lecturasPendientes.filter { lectura ->
                            // 1️⃣ Filtro por permisos específicos del usuario
                            val porPermisoEspecifico = user?.codigosAlmacen?.contains(lectura.codigoAlmacen) == true
                            
                            // 2️⃣ Filtro por almacenes del centro del usuario (código de almacén contiene el código del centro)
                            val porCentro = user?.codigoCentro?.let { centro ->
                                lectura.codigoAlmacen.contains(centro)
                            } ?: false
                            
                            // Combinar permisos: específicos OR del centro
                            porPermisoEspecifico || porCentro
                        }
                        
                                                                  // Lista de lecturas pendientes
                                               // Solo mostrar el título si hay lecturas pendientes
                        val lecturasPendientesFiltradas = lecturasFiltradas.filter { it.cantidadContada == null || it.cantidadContada <= 0 }
                        if (lecturasPendientesFiltradas.isNotEmpty()) {
                            item {
                                Text(
                                    text = "Artículos Pendientes de Conteo",
                                    style = MaterialTheme.typography.titleMedium,
                                    fontWeight = FontWeight.Bold,
                                    color = MaterialTheme.colorScheme.onSurface,
                                    modifier = Modifier.padding(vertical = 8.dp)
                                )
                            }
                        }
                        
                        // Mostrar solo las lecturas que aún están pendientes (no completadas) y permitidas
                      items(lecturasFiltradas.filter { it.cantidadContada == null || it.cantidadContada <= 0 }) { lectura ->
                          LecturaPendienteCard(
                              lectura = lectura,
                              ordenVisibilidad = ordenSeleccionada?.visibilidad ?: ""
                          )
                      }

                     

                    // Botones de acción
                    item {
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.spacedBy(8.dp)
                        ) {
                            // Botón para escanear (solo en móviles)
                            if (!DeviceUtils.hasHardwareScanner(context)) {
                                Button(
                                    onClick = { escaneando = true },
                                    modifier = Modifier.weight(1f)
                                ) {
                                    Icon(Icons.Default.QrCodeScanner, contentDescription = null)
                                    Spacer(modifier = Modifier.width(8.dp))
                                    Text("Escanear")
                                }
                            }
                            
                            // Botón para lectura manual o salir del modo manual
                            if (modoLecturaManual) {
                                Button(
                                    onClick = {
                                        conteoViewModel.setModoLecturaManual(false)
                                        conteoViewModel.limpiarEscaneo()
                                        conteoViewModel.setMensaje("Modo lectura manual desactivado.")
                                    },
                                    modifier = Modifier.weight(1f),
                                    colors = ButtonDefaults.buttonColors(
                                        containerColor = MaterialTheme.colorScheme.errorContainer
                                    )
                                ) {
                                    Icon(Icons.Default.Close, contentDescription = null)
                                    Spacer(modifier = Modifier.width(8.dp))
                                    Text("Salir Manual")
                                }
                            } else {
                                Button(
                                    onClick = {
                                        conteoViewModel.setModoLecturaManual(true)
                                        conteoViewModel.setEstadoEscaneo(EstadoEscaneoConteo.EsperandoUbicacion)
                                        conteoViewModel.setMensaje("Modo lectura manual activado. Escanee la ubicación.")
                                    },
                                    modifier = Modifier.weight(1f),
                                    colors = ButtonDefaults.buttonColors(
                                        containerColor = MaterialTheme.colorScheme.primary
                                    )
                                ) {
                                    Icon(Icons.Default.Add, contentDescription = null)
                                    Spacer(modifier = Modifier.width(8.dp))
                                    Text("Lectura Manual")
                                }
                            }
                            
                        }
                    }
                 }
             }
         }

         // Scanner para móviles
         if (escaneando && !DeviceUtils.hasHardwareScanner(context)) {
             QRScannerView(
                 modifier = Modifier.fillMaxSize(),
                 onCodeScanned = { code ->
                     if (escaneoProcesado) return@QRScannerView
                     escaneoProcesado = true
                     escaneando = false
                     
                     procesarCodigoEscaneado(
                         code, ordenGuid, user?.id, conteoViewModel, conteoLogic, modoLecturaManual,
                         onArticuloManual = { codAlm, codUbi, codArt, partida, fechaCaducidad ->
                             Log.d("ConteoProcesoScreen", "📝 Modo manual - Artículo: $codArt en $codAlm-$codUbi, Fecha: $fechaCaducidad")
                             ubicacionManual = "$codAlm$$codUbi"
                             articuloManual = codArt
                             partidaManual = partida
                             fechaCaducidadManual = fechaCaducidad
                             cantidadInput = ""
                             comentarioInput = ""
                             mostrarInputCantidad = true
                             conteoViewModel.setError(null)
                         }
                     )
                     escaneoProcesado = false
                 }
             )
         }

         // Focus invisible para PDA (dentro del Scaffold pero al final)
         if (DeviceUtils.hasHardwareScanner(context)) {
             Log.d("ConteoProcesoScreen", "🔧 Creando Box invisible para PDA - hasHardwareScanner = true")
             Box(
                 modifier = Modifier
                     .fillMaxSize()
                     .focusRequester(focusRequester)
                     .focusable()
                     .onPreviewKeyEvent { event ->
                         Log.d("ConteoProcesoScreen", "🎯 Evento de teclado recibido: action=${event.nativeKeyEvent?.action}, keyCode=${event.nativeKeyEvent?.keyCode}")
                         if (event.nativeKeyEvent?.action == android.view.KeyEvent.ACTION_MULTIPLE) {
                             if (escaneoProcesado) return@onPreviewKeyEvent true
                             escaneoProcesado = true

                             event.nativeKeyEvent.characters?.let { code ->
                                 Log.d("ConteoProcesoScreen", "📥 Código escaneado: $code")
                                 procesarCodigoEscaneado(
                                     code.trim(), ordenGuid, user?.id, conteoViewModel, conteoLogic, modoLecturaManual,
                                     onArticuloManual = { codAlm, codUbi, codArt, partida, fechaCaducidad ->
                                         Log.d("ConteoProcesoScreen", "📝 Modo manual - Artículo: $codArt en $codAlm-$codUbi, Fecha: $fechaCaducidad")
                                         ubicacionManual = "$codAlm$$codUbi"
                                         articuloManual = codArt
                                         partidaManual = partida
                                         fechaCaducidadManual = fechaCaducidad
                                         cantidadInput = ""
                                         comentarioInput = ""
                                         mostrarInputCantidad = true
                                         conteoViewModel.setError(null)
                                     }
                                 )
                             }
                             escaneoProcesado = false
                             true
                         } else {
                             Log.d("ConteoProcesoScreen", "🎯 Evento de teclado ignorado: action=${event.nativeKeyEvent?.action}")
                             false
                         }
                     }
                     .layout { measurable, constraints ->
                         val placeable = measurable.measure(constraints)
                         layout(0, 0) { placeable.place(0, 0) }
                     }
             )
         }


     }


    // Diálogo de confirmación de artículo
    if (mostrarDialogoConfirmacionArticulo && articuloParaConfirmar != null) {
        AlertDialog(
            onDismissRequest = {
                conteoViewModel.setMostrarDialogoConfirmacionArticulo(false)
                conteoViewModel.setArticuloParaConfirmar(null)
            },
            title = { Text("Confirmar Artículo") },
                         text = { 
                 Text("¿El artículo ${articuloParaConfirmar!!.codigoArticulo} está en la ubicación ${articuloParaConfirmar!!.codigoAlmacen}-${articuloParaConfirmar!!.codigoUbicacion}? Si es así, confirme.")
             },
            confirmButton = {
                Button(onClick = {
                    conteoViewModel.setArticuloEscaneado(articuloParaConfirmar)
                    conteoViewModel.setEstadoEscaneo(EstadoEscaneoConteo.EsperandoCantidad)
                    conteoViewModel.setMostrarDialogoConfirmacionArticulo(false)
                    conteoViewModel.setArticuloParaConfirmar(null)
                }) {
                    Text("Sí, confirmar")
                }
            },
            dismissButton = {
                TextButton(onClick = {
                    conteoViewModel.setMostrarDialogoConfirmacionArticulo(false)
                    conteoViewModel.setArticuloParaConfirmar(null)
                    conteoViewModel.setEstadoEscaneo(EstadoEscaneoConteo.EsperandoArticulo)
                                 }) {
                     Text("No, escanear otro artículo")
                 }
            }
        )
    }

    // Diálogo de selección de artículos múltiples
    if (mostrarDialogoSeleccionArticulo && articulosFiltrados.isNotEmpty()) {
        DialogSeleccionArticulo(
            lista = articulosFiltrados,
            onDismiss = {
                conteoViewModel.setMostrarDialogoSeleccionArticulo(false)
                conteoViewModel.setArticulosFiltrados(emptyList())
            },
            onSeleccion = { articuloSeleccionado ->
                // Procesar la selección del artículo
                conteoLogic.procesarSeleccionArticulo(
                    articuloSeleccionado = articuloSeleccionado,
                    onArticuloDetectado = { articulo ->
                        Log.d("ConteoProcesoScreen", "🛒 Artículo seleccionado: ${articulo.codigoArticulo}")
                        
                        // Verificar si el artículo está en la ubicación escaneada
                        val ubicacionActual = conteoViewModel.ubicacionEscaneada.value
                        if (ubicacionActual != null) {
                            val (codAlm, codUbi) = ubicacionActual.split("$")
                            if (articulo.codigoAlmacen == codAlm && articulo.codigoUbicacion == codUbi) {
                                // Artículo correcto, proceder a cantidad
                                conteoViewModel.setArticuloEscaneado(articulo)
                                conteoViewModel.setEstadoEscaneo(EstadoEscaneoConteo.EsperandoCantidad)
                                conteoViewModel.setMensaje("Artículo ${articulo.codigoArticulo} confirmado. Introduzca la cantidad.")
                            } else {
                                // Artículo en ubicación diferente, mostrar diálogo de confirmación
                                conteoViewModel.setArticuloParaConfirmar(articulo)
                                conteoViewModel.setMostrarDialogoConfirmacionArticulo(true)
                            }
                        }
                        
                        // Cerrar diálogo de selección
                        conteoViewModel.setMostrarDialogoSeleccionArticulo(false)
                        conteoViewModel.setArticulosFiltrados(emptyList())
                    },
                    onError = { errorMsg ->
                        Log.e("ConteoProcesoScreen", "❌ Error al procesar selección: $errorMsg")
                        conteoViewModel.setError(errorMsg)
                        conteoViewModel.setMostrarDialogoSeleccionArticulo(false)
                        conteoViewModel.setArticulosFiltrados(emptyList())
                    }
                )
                         }
         )
     }

     // Diálogo de registro de cantidad
     if ((estadoEscaneo == EstadoEscaneoConteo.EsperandoCantidad && articuloEscaneado != null) || mostrarInputCantidad) {
         AlertDialog(
             onDismissRequest = {
                 // No permitir cerrar el diálogo sin registrar
             },
             title = { 
                 Text(
                     text = "Registrar Cantidad",
                     style = MaterialTheme.typography.titleLarge,
                     fontWeight = FontWeight.Bold
                 )
             },
             text = { 
                 Column(
                     modifier = Modifier.fillMaxWidth(),
                     verticalArrangement = Arrangement.spacedBy(12.dp)
                 ) {
                     // Información del artículo
                     val codigoArticulo = if (mostrarInputCantidad) articuloManual else articuloEscaneado!!.codigoArticulo
                     val ubicacionCompleta = if (mostrarInputCantidad) ubicacionManual else "${articuloEscaneado!!.codigoAlmacen}-${articuloEscaneado!!.codigoUbicacion}"
                     
                     InfoRow(
                         icon = Icons.Default.Inventory,
                         label = "Artículo",
                         value = codigoArticulo ?: ""
                     )
                     
                     InfoRow(
                         icon = Icons.Default.LocationOn,
                         label = "Ubicación",
                         value = ubicacionCompleta ?: ""
                     )
                     
                     // Solo mostrar stock actual si la visibilidad es VISIBLE y no es modo manual
                     if (!mostrarInputCantidad && ordenSeleccionada?.visibilidad == "VISIBLE" && articuloEscaneado!!.cantidadStock != null) {
                         InfoRow(
                             icon = Icons.Default.Assessment,
                             label = "Stock Actual",
                             value = articuloEscaneado!!.cantidadStock.toString()
                         )
                     }
                     
                                           // Input de cantidad
                      OutlinedTextField(
                          value = cantidadInput,
                          onValueChange = { cantidadInput = it },
                          label = { Text("Cantidad Contada") },
                          modifier = Modifier.fillMaxWidth(),
                          keyboardOptions = KeyboardOptions(
                              keyboardType = KeyboardType.Decimal,
                              imeAction = ImeAction.Next
                          ),
                          singleLine = true
                      )
                      
                      // Input de comentario opcional
                      OutlinedTextField(
                          value = comentarioInput,
                          onValueChange = { comentarioInput = it },
                          label = { Text("Comentario (opcional)") },
                          modifier = Modifier.fillMaxWidth(),
                          keyboardOptions = KeyboardOptions(
                              keyboardType = KeyboardType.Text,
                              imeAction = ImeAction.Done
                          ),
                          singleLine = true,
                          maxLines = 3
                      )
                 }
             },
             confirmButton = {
                 Button(
                     onClick = {
                         val cantidad = cantidadInput.toDoubleOrNull()
                         if (cantidad != null && cantidad >= 0) {
                             if (mostrarInputCantidad) {
                                 // Modo manual: usar datos manuales
                                 val ubicacionParts = ubicacionManual!!.split("$")
                                 val codAlm = ubicacionParts[0]
                                 val codUbi = ubicacionParts[1]
                                 
                                 conteoLogic.registrarLectura(
                                     ordenGuid = ordenGuid,
                                     codigoUbicacion = codUbi,
                                     codigoArticulo = articuloManual!!,
                                     descripcionArticulo = null,
                                     lotePartida = partidaManual,
                                     cantidadContada = cantidad,
                                     usuarioCodigo = user?.id ?: "",
                                     comentario = comentarioInput.takeIf { it.isNotEmpty() },
                                     fechaCaducidad = fechaCaducidadManual // Usar la fecha extraída del código escaneado
                                 )
                                 
                                 // Limpiar modo manual
                                 mostrarInputCantidad = false
                                 ubicacionManual = null
                                 articuloManual = null
                                 partidaManual = null
                                 fechaCaducidadManual = null
                                 cantidadInput = ""
                                 comentarioInput = ""
                                 conteoViewModel.setModoLecturaManual(false)
                                 conteoViewModel.limpiarEscaneo()
                             } else {
                                 // Modo normal: usar datos de articuloEscaneado
                                 val ubicacionEscaneada = conteoViewModel.ubicacionEscaneada.value
                                 val codigoUbicacion = if (ubicacionEscaneada != null) {
                                     ubicacionEscaneada
                                 } else {
                                     articuloEscaneado!!.codigoUbicacion
                                 }
                                 
                                 conteoLogic.registrarLectura(
                                     ordenGuid = ordenGuid,
                                     codigoUbicacion = codigoUbicacion,
                                     codigoArticulo = articuloEscaneado!!.codigoArticulo,
                                     descripcionArticulo = articuloEscaneado!!.descripcionArticulo,
                                     lotePartida = articuloEscaneado!!.lotePartida,
                                     cantidadContada = cantidad,
                                     usuarioCodigo = user?.id ?: "",
                                     comentario = comentarioInput.takeIf { it.isNotEmpty() },
                                     fechaCaducidad = articuloEscaneado!!.fechaCaducidad
                                 )
                                 
                                 // Limpiar y continuar
                                 cantidadInput = ""
                                 comentarioInput = ""
                                 conteoViewModel.limpiarEscaneo()
                                 conteoViewModel.setEstadoEscaneo(EstadoEscaneoConteo.EsperandoUbicacion)
                             }
                             
                             // NOTA: Las lecturas se recargan automáticamente desde ConteoLogic.kt
                             // después de registrar exitosamente la lectura
                         }
                     },
                     enabled = cantidadInput.toDoubleOrNull() != null && cantidadInput.toDoubleOrNull()!! >= 0,
                     modifier = Modifier.fillMaxWidth()
                 ) {
                     Icon(Icons.Default.Check, contentDescription = null)
                     Spacer(modifier = Modifier.width(8.dp))
                     Text("Registrar Lectura")
                 }
             },
             dismissButton = {
                 TextButton(
                     onClick = {
                         // Cancelar y volver a esperar artículo
                         cantidadInput = ""
                         comentarioInput = ""
                         conteoViewModel.setEstadoEscaneo(EstadoEscaneoConteo.EsperandoArticulo)
                         conteoViewModel.setArticuloEscaneado(null)
                     }
                 ) {
                     Text("Cancelar")
                 }
             }
         )
     }
 }

// Función para procesar código escaneado
private fun procesarCodigoEscaneado(
    code: String,
    ordenGuid: String,
    codigoOperario: String?,
    conteoViewModel: ConteoViewModel,
    conteoLogic: ConteoLogic,
    modoManual: Boolean,
    onArticuloManual: (String, String, String, String?, String?) -> Unit
) {
    Log.d("ConteoProcesoScreen", "📥 Procesando código: $code")
    
    if (codigoOperario == null) {
        conteoViewModel.setError("Usuario no identificado")
        return
    }

    conteoLogic.procesarCodigoEscaneado(
        code = code,
        ordenGuid = ordenGuid,
        codigoOperario = codigoOperario,
        modoManual = modoManual,
        onUbicacionDetectada = { ubicacion ->
            Log.d("ConteoProcesoScreen", "📍 Ubicación detectada: $ubicacion")
            conteoViewModel.setUbicacionEscaneada(ubicacion)
            conteoViewModel.setEstadoEscaneo(EstadoEscaneoConteo.EsperandoArticulo)
            conteoViewModel.setMensaje("Ubicación escaneada: $ubicacion. Ahora escanee el artículo.")
            conteoViewModel.setError(null)
        },
        onArticuloDetectado = { articulo ->
            Log.d("ConteoProcesoScreen", "🛒 Artículo detectado: ${articulo.codigoArticulo}")
            conteoViewModel.setError(null)
            
            // Verificar si el artículo está en la ubicación escaneada
            val ubicacionActual = conteoViewModel.ubicacionEscaneada.value
            if (ubicacionActual != null) {
                val (codAlm, codUbi) = ubicacionActual.split("$")
                if (articulo.codigoAlmacen == codAlm && articulo.codigoUbicacion == codUbi) {
                    // Artículo correcto, proceder a cantidad
                    conteoViewModel.setArticuloEscaneado(articulo)
                    conteoViewModel.setEstadoEscaneo(EstadoEscaneoConteo.EsperandoCantidad)
                    conteoViewModel.setMensaje("Artículo ${articulo.codigoArticulo} confirmado. Introduzca la cantidad.")
                } else {
                    // Artículo en ubicación diferente, mostrar diálogo de confirmación
                    conteoViewModel.setArticuloParaConfirmar(articulo)
                    conteoViewModel.setMostrarDialogoConfirmacionArticulo(true)
                }
            }
        },
        onMultipleArticulos = { articulos ->
            Log.d("ConteoProcesoScreen", "🔍 Múltiples artículos encontrados: ${articulos.size}")
            conteoViewModel.setArticulosFiltrados(articulos)
            conteoViewModel.setMostrarDialogoSeleccionArticulo(true)
        },
        onArticuloManual = onArticuloManual,
        onError = { errorMsg ->
            Log.e("ConteoProcesoScreen", "❌ Error: $errorMsg")
            conteoViewModel.setError(errorMsg)
        }
    )
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun EstadoEscaneoCard(
    estadoEscaneo: EstadoEscaneoConteo,
    ubicacionEscaneada: String?,
    articuloEscaneado: LecturaPendiente?,
    modoLecturaManual: Boolean = false
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
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
                    text = "Estado del Escaneo",
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.onSurface
                )
                
                if (modoLecturaManual) {
                    Surface(
                        color = MaterialTheme.colorScheme.secondary,
                        shape = RoundedCornerShape(12.dp)
                    ) {
                        Text(
                            text = "MANUAL",
                            style = MaterialTheme.typography.labelSmall,
                            color = MaterialTheme.colorScheme.onSecondary,
                            modifier = Modifier.padding(horizontal = 8.dp, vertical = 4.dp)
                        )
                    }
                }
            }
            
            Spacer(modifier = Modifier.height(8.dp))
            
            when (estadoEscaneo) {
                EstadoEscaneoConteo.Inactivo -> {
                    Text(
                        text = "Iniciando escaneo...",
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
                EstadoEscaneoConteo.EsperandoUbicacion -> {
                    Text(
                        text = "📍 Escanee la ubicación",
                        color = MaterialTheme.colorScheme.primary,
                        fontWeight = FontWeight.Bold
                    )
                }
                EstadoEscaneoConteo.EsperandoArticulo -> {
                    Column {
                        Text(
                            text = "🛒 Escanee el artículo",
                            color = MaterialTheme.colorScheme.primary,
                            fontWeight = FontWeight.Bold
                        )
                        ubicacionEscaneada?.let { ubicacion ->
                            Text(
                                text = "Ubicación: $ubicacion",
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                                style = MaterialTheme.typography.bodySmall
                            )
                        }
                    }
                }
                EstadoEscaneoConteo.EsperandoCantidad -> {
                    Column {
                        Text(
                            text = "📊 Introduzca la cantidad",
                            color = MaterialTheme.colorScheme.primary,
                            fontWeight = FontWeight.Bold
                        )
                        ubicacionEscaneada?.let { ubicacion ->
                            Text(
                                text = "Ubicación: $ubicacion",
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                                style = MaterialTheme.typography.bodySmall
                            )
                        }
                        articuloEscaneado?.let { articulo ->
                            Text(
                                text = "Artículo: ${articulo.codigoArticulo}",
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                                style = MaterialTheme.typography.bodySmall
                            )
                        }
                    }
                }
                EstadoEscaneoConteo.Procesando -> {
                    Text(
                        text = "⏳ Procesando...",
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            }
        }
    }
}



@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun OrdenInfoCard(
    orden: OrdenConteo,
    conteoViewModel: ConteoViewModel
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        elevation = CardDefaults.cardElevation(defaultElevation = 4.dp)
    ) {
        Column(
            modifier = Modifier.padding(16.dp)
        ) {
            // Header con título y estado
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Column(modifier = Modifier.weight(1f)) {
                    Text(
                        text = orden.titulo,
                        style = MaterialTheme.typography.titleLarge,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                }
                EstadoChip(estado = orden.estado)
            }

            Spacer(modifier = Modifier.height(16.dp))

            // Información de la orden
            Column(
                modifier = Modifier.fillMaxWidth(),
                verticalArrangement = Arrangement.spacedBy(8.dp)
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
                    value = orden.fechaAsignacion?.let { conteoViewModel.formatearFecha(it) } ?: "No asignado"
                )

                orden.fechaInicio?.let { fechaInicio ->
                    InfoRow(
                        icon = Icons.Default.PlayArrow,
                        label = "Iniciado",
                        value = conteoViewModel.formatearFecha(fechaInicio)
                    )
                }
            }
        }
    }
}


@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun LecturaPendienteCard(
    lectura: LecturaPendiente,
    ordenVisibilidad: String
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surface
        )
    ) {
        Column(
            modifier = Modifier.padding(12.dp)
        ) {
                         // Header con ubicación (lo más visible)
             Row(
                 modifier = Modifier.fillMaxWidth(),
                 horizontalArrangement = Arrangement.SpaceBetween,
                 verticalAlignment = Alignment.CenterVertically
             ) {
                 Column(modifier = Modifier.weight(1f)) {
                     // Ubicación en negrita y grande - lo primero que debe ver el operario
                     Text(
                         text = "${lectura.codigoAlmacen}-${lectura.codigoUbicacion}",
                         style = MaterialTheme.typography.titleMedium,
                         fontWeight = FontWeight.Bold,
                         color = MaterialTheme.colorScheme.primary
                     )
                     
                     Spacer(modifier = Modifier.height(8.dp))
                     
                     // Artículo y descripción
                     Text(
                         text = lectura.codigoArticulo,
                         style = MaterialTheme.typography.titleSmall,
                         fontWeight = FontWeight.Medium,
                         color = MaterialTheme.colorScheme.onSurface
                     )
                     
                     // Mostrar descripción del artículo si está disponible
                     lectura.descripcionArticulo?.let { descripcion ->
                         if (descripcion.isNotEmpty()) {
                             Text(
                                 text = descripcion,
                                 style = MaterialTheme.typography.bodyMedium,
                                 color = MaterialTheme.colorScheme.onSurfaceVariant,
                                 maxLines = 2
                             )
                         }
                     }
                                   }
            }
            
                         Spacer(modifier = Modifier.height(8.dp))
             
             // Información adicional: Lote, Fecha Caducidad y Stock
             Row(
                 modifier = Modifier.fillMaxWidth(),
                 horizontalArrangement = Arrangement.SpaceBetween
             ) {
                 // Lote/Partida si existe
                 lectura.lotePartida?.let { lote ->
                     if (lote.isNotEmpty()) {
                         Column {
                             Text(
                                 text = "Lote",
                                 style = MaterialTheme.typography.bodySmall,
                                 color = MaterialTheme.colorScheme.onSurfaceVariant
                             )
                             Text(
                                 text = lote,
                                 style = MaterialTheme.typography.bodyMedium,
                                 fontWeight = FontWeight.Medium
                             )
                         }
                     }
                 }
                 
                 // Fecha de caducidad si existe
                 lectura.fechaCaducidad?.let { fecha ->
                     if (fecha.isNotEmpty()) {
                         Column {
                             Text(
                                 text = "Caducidad",
                                 style = MaterialTheme.typography.bodySmall,
                                 color = MaterialTheme.colorScheme.onSurfaceVariant
                             )
                             Text(
                                 text = fecha,
                                 style = MaterialTheme.typography.bodyMedium,
                                 fontWeight = FontWeight.Medium
                             )
                         }
                     }
                 }
                 
                 // Stock actual (solo si es VISIBLE)
                 if (ordenVisibilidad == "VISIBLE" && lectura.cantidadStock != null) {
                     Column {
                         Text(
                             text = "Stock Actual",
                             style = MaterialTheme.typography.bodySmall,
                             color = MaterialTheme.colorScheme.onSurfaceVariant
                         )
                         Text(
                             text = lectura.cantidadStock.toString(),
                             style = MaterialTheme.typography.bodyMedium,
                             fontWeight = FontWeight.Medium
                         )
                     }
                 }
             }
        }
    }
}


