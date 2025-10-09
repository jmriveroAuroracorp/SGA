package com.example.sga.view.home

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Menu
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.example.sga.view.app.SessionViewModel
import androidx.compose.material3.*
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.ui.platform.LocalContext
import androidx.navigation.NavHostController
import kotlinx.coroutines.launch
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import androidx.compose.material.icons.automirrored.filled.Logout
import androidx.compose.material.icons.filled.CompareArrows
import androidx.compose.material.icons.filled.Print
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.filled.Speed
import androidx.compose.material.icons.filled.Storage
import androidx.compose.material.icons.filled.Inventory
import androidx.compose.material.icons.filled.Assignment
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.remember
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.Alignment
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.text.font.FontWeight
import com.example.sga.view.components.AppTopBar
import androidx.compose.ui.focus.focusProperties
import com.example.sga.view.components.EmpresaActivaDisplay
import com.example.sga.view.conteos.ConteoLogic
import com.example.sga.view.conteos.ConteoViewModel
import com.example.sga.service.Traspasos.EstadoTraspasosService
import android.util.Log
import androidx.lifecycle.viewmodel.compose.viewModel
import android.Manifest
import com.google.accompanist.permissions.*
import com.example.sga.view.ordenes.OrdenTraspasoViewModel
import com.example.sga.view.ordenes.OrdenTraspasoLogic
import com.example.sga.service.Ordenes.OrdenesTraspasoService
import com.example.sga.service.Conteos.ConteosService

@OptIn(ExperimentalMaterial3Api::class, ExperimentalPermissionsApi::class)


@Composable
fun HomeScreen(
    sessionViewModel: SessionViewModel,
    navController: NavHostController
) {
    val context = LocalContext.current
    val user by sessionViewModel.user.collectAsState()
    val logic = remember { HomeLogic(sessionViewModel) }
    
    // Lógica para conteos - ViewModel compartido para optimizar carga
    val conteoViewModel: ConteoViewModel = viewModel(key = "ConteoViewModel")
    val conteoLogic = remember { ConteoLogic(conteoViewModel) }
    val conteosActivos by conteoViewModel.conteosActivos.collectAsState()
    val ordenes by conteoViewModel.ordenes.collectAsState()
    
    // Lógica para órdenes de traspaso - ViewModel compartido para optimizar carga
    val ordenTraspasoViewModel: OrdenTraspasoViewModel = viewModel(key = "OrdenTraspasoViewModel")
    val ordenTraspasoLogic = remember { OrdenTraspasoLogic(ordenTraspasoViewModel, sessionViewModel) }
    val ordenesActivas by ordenTraspasoViewModel.ordenesActivas.collectAsState()

    // Verificar conteos activos solo una vez al cargar HomeScreen
    LaunchedEffect(user?.id) {
        user?.id?.let { codigo ->
            Log.d("HomeScreen", "🔄 Cargando conteos activos para usuario: $codigo")
            conteoLogic.verificarConteosActivos(codigo) { cantidad ->
                Log.d("HomeScreen", "📊 Conteos activos recibidos: $cantidad")
            }
        }
    }
    
    // Verificar órdenes de traspaso activas solo una vez al cargar HomeScreen
    LaunchedEffect(user?.id) {
        user?.let { usuario ->
            Log.d("HomeScreen", "🔄 Cargando órdenes activas para usuario: ${usuario.id}")
            ordenTraspasoLogic.verificarOrdenesActivas(usuario) { cantidad ->
                Log.d("HomeScreen", "📊 Órdenes activas recibidas: $cantidad")
            }
        }
    }
    
    // Actualización automática cada 30 segundos
    LaunchedEffect(user?.id) {
        user?.let { usuario ->
            while (isActive) {
                delay(30_000L) // cada 30 segundos
                Log.d("HomeScreen", "🔄 Actualización automática de datos")
                
                // Recargar conteos
                usuario.id?.let { codigo ->
                    conteoLogic.verificarConteosActivos(codigo) { cantidad ->
                        Log.d("HomeScreen", "📊 Conteos actualizados: $cantidad")
                    }
                }
                
                // Recargar órdenes de traspaso
                ordenTraspasoLogic.verificarOrdenesActivas(usuario) { cantidad ->
                    Log.d("HomeScreen", "📊 Órdenes actualizadas: $cantidad")
                }
            }
        }
    }
    
    // Actualización más frecuente cuando hay cambios en el estado
    LaunchedEffect(conteosActivos, ordenesActivas) {
        Log.d("HomeScreen", "🔄 Estado cambiado - conteos: $conteosActivos, órdenes: $ordenesActivas")
    }

    // Reiniciar servicio de notificaciones al volver al home
    LaunchedEffect(user?.id) {
        user?.id?.let { codigo ->
            Log.d("HomeScreen", "🔄 Reiniciando notificaciones de traspasos")
            EstadoTraspasosService.iniciar(codigo.toInt(), context)
        }
    }
    
    // Iniciar servicios de verificación periódica
    LaunchedEffect(user?.id) {
        user?.let { usuario ->
            val codigoEmpresa = sessionViewModel.empresaSeleccionada.value?.codigo?.toInt() ?: 1
            
            Log.d("HomeScreen", "🔄 Iniciando servicios de verificación periódica")
            Log.d("HomeScreen", "👤 Usuario: ${usuario.id}")
            Log.d("HomeScreen", "🏢 Empresa: $codigoEmpresa")
            
            // Iniciar servicio de órdenes de traspaso
            OrdenesTraspasoService.iniciar(
                usuarioId = usuario.id.toInt(),
                codigoEmpresa = codigoEmpresa,
                context = context
            )
            
            // Iniciar servicio de conteos
            ConteosService.iniciar(
                codigoOperario = usuario.id,
                context = context
            )
        }
    }

    // Solicitar permisos de cámara al cargar el home
    val cameraPermissionState = rememberPermissionState(Manifest.permission.CAMERA)
    
    LaunchedEffect(Unit) {
        if (!cameraPermissionState.status.isGranted) {
            Log.d("HomeScreen", "📷 Solicitando permiso de cámara")
            cameraPermissionState.launchPermissionRequest()
        } else {
            Log.d("HomeScreen", "📷 Permiso de cámara ya otorgado")
        }
    }

    // —— Definición de accesos con sus permisos
    data class Acceso(
        val titulo: String,
        val permiso: Int?,
        val icono: @Composable () -> Unit,
        val onClick: () -> Unit
    )

    val accesos = buildList {
        // Pesaje (permiso 7)
        if (sessionViewModel.tienePermiso(7)) add(
            Acceso(
                titulo = "Pesaje",
                permiso = 7,
                icono = { Icon(Icons.Filled.Speed, contentDescription = null) },
                onClick = { navController.navigate("pesaje") }
            )
        )
        // Traspasos (permiso 12)
        if (sessionViewModel.tienePermiso(/*12*/12)) add(
            Acceso(
                titulo = "Traspasos",
                permiso = 12,
                icono = { Icon(Icons.Filled.CompareArrows, contentDescription = null) },
                onClick = { navController.navigate("traspasos/false/false") }
            )
        )
        // Stock (permiso 10)
        if (sessionViewModel.tienePermiso(/*10*/10)) add(
            Acceso(
                titulo = "Stock",
                permiso = 10,
                icono = { Icon(Icons.Filled.Storage, contentDescription = null) },
                onClick = { navController.navigate("stock") }
            )
        )
        // Imprimir (permiso 11)
        if (sessionViewModel.tienePermiso(/*11*/11)) add(
            Acceso(
                titulo = "Imprimir",
                permiso = 11,
                icono = { Icon(Icons.Filled.Print, contentDescription = null) },
                onClick = { navController.navigate("etiquetas") }
            )
        )
        // Conteos (permiso 13)
        if (sessionViewModel.tienePermiso(13)) add(
            Acceso(
                titulo = if (conteosActivos > 0) "Conteos ($conteosActivos)" else "Conteos",
                permiso = 13,
                icono = { 
                    Icon(
                        Icons.Filled.Inventory, 
                        contentDescription = null,
                        tint = if (conteosActivos > 0) Color.Unspecified else Color.Gray
                    ) 
                },
                onClick = { 
                    if (conteosActivos > 0) {
                        navController.navigate("conteos")
                    }
                }
            )
        )
        // Órdenes de Traspaso (permiso 13)
        if (sessionViewModel.tienePermiso(13)) add(
            Acceso(
                titulo = if (ordenesActivas > 0) "Órdenes ($ordenesActivas)" else "Órdenes",
                permiso = 13,
                icono = { 
                    Icon(
                        Icons.Filled.Assignment, 
                        contentDescription = null,
                        tint = if (ordenesActivas > 0) Color.Unspecified else Color.Gray
                    ) 
                },
                onClick = { 
                    if (ordenesActivas > 0) {
                        navController.navigate("ordenes")
                    }
                }
            )
        )
        // Configuración (lo dejamos visible siempre; si quieres condicionar a nº de empresas, modificar)
        add(
            Acceso(
                titulo = "Configuración",
                permiso = null,
                icono = { Icon(Icons.Filled.Settings, contentDescription = null) },
                onClick = { navController.navigate("configuracion") }
            )
        )
        // Cerrar sesión
        add(
            Acceso(
                titulo = "Cerrar sesión",
                permiso = null,
                icono = { Icon(Icons.AutoMirrored.Filled.Logout, contentDescription = null) },
                onClick = {
                    user?.let { logic.hacerLogout(it, context, navController) }
                }
            )
        )
    }

    Scaffold(
        topBar = {
            AppTopBar(
                title = "Bienvenido, ${user?.name ?: ""}",
                sessionViewModel = sessionViewModel,
                navController = navController,
                showBackButton = false,
                customNavigationIcon = null // ← sin botón de menú
            )
        }
    ) { padding ->
        // —— Rejilla 2 columnas con scroll vertical
        Column(
            modifier = Modifier
                .padding(padding)
                .fillMaxSize()
        ) {
            Column(
                modifier = Modifier
                    .padding(16.dp)
                    .fillMaxWidth()
                    .verticalScroll(rememberScrollState())
            ) {
                // Saludo/empresa opcional aquí si quieres
                Spacer(Modifier.height(8.dp))

                accesos.chunked(2).forEach { fila ->
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.spacedBy(16.dp)
                    ) {
                        fila.forEach { acceso ->
                            FeatureCard(
                                title = acceso.titulo,
                                icon = acceso.icono,
                                onClick = acceso.onClick,
                                modifier = Modifier
                                    .weight(1f)
                                    .height(110.dp),
                                isEnabled = (acceso.titulo != "Conteos" && !acceso.titulo.startsWith("Conteos (")) && 
                                           (acceso.titulo != "Órdenes" && !acceso.titulo.startsWith("Órdenes (")) ||
                                           (acceso.titulo == "Conteos" || acceso.titulo.startsWith("Conteos (")) && conteosActivos > 0 ||
                                           (acceso.titulo == "Órdenes" || acceso.titulo.startsWith("Órdenes (")) && ordenesActivas > 0
                            )
                        }
                        if (fila.size == 1) {
                            Spacer(modifier = Modifier.weight(1f))
                        }
                    }
                    Spacer(Modifier.height(16.dp))
                }
                
                // Espacio adicional al final para evitar que la última fila quede cortada
                Spacer(Modifier.height(32.dp))
            }
        }
    }
}

@Composable
private fun FeatureCard(
    title: String,
    icon: @Composable () -> Unit,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    isEnabled: Boolean = true
) {
    
    Card(
        onClick = onClick,
        modifier = modifier
            .focusProperties { canFocus = false },  // ← añade esto
        shape = RoundedCornerShape(20.dp),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
    ) {
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(16.dp),
            contentAlignment = Alignment.Center
        ) {
            Column(horizontalAlignment = Alignment.CenterHorizontally) {
                Box(Modifier.size(36.dp)) { icon() }
                Spacer(Modifier.height(8.dp))
                Text(
                    text = title,
                    style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
                    maxLines = 1,
                    color = if (isEnabled) MaterialTheme.colorScheme.onSurface else Color.Gray
                )
            }
        }
    }


    val focusManager = LocalFocusManager.current
    LaunchedEffect(Unit) {
        focusManager.clearFocus(force = true)
    }
}
