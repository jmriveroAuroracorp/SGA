package com.example.sga.view.home

import androidx.compose.foundation.layout.*
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
import androidx.compose.material.icons.automirrored.filled.Logout
import com.example.sga.view.components.AppTopBar
import com.example.sga.view.components.EmpresaActivaDisplay

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun HomeScreen(
    sessionViewModel: SessionViewModel,
    navController: NavHostController
) {
    val context = LocalContext.current
    val user by sessionViewModel.user.collectAsState()
    val logic = remember { HomeLogic(sessionViewModel) }

    val drawerState = rememberDrawerState(initialValue = DrawerValue.Closed)
    val scope = rememberCoroutineScope()

    ModalNavigationDrawer(
        drawerState = drawerState,
        drawerContent = {
            ModalDrawerSheet {
                Spacer(Modifier.height(16.dp))
                Text(
                    "Menú",
                    style = MaterialTheme.typography.titleLarge,
                    modifier = Modifier.padding(16.dp)
                )

                HorizontalDivider()

                // Opción: Pesaje
                if (sessionViewModel.tienePermiso(7)) {
                    NavigationDrawerItem(
                        label = { Text("Pesaje") },
                        selected = false,
                        onClick = {
                            navController.navigate("pesaje")
                            scope.launch { drawerState.close() }
                        }
                    )
                }
                NavigationDrawerItem(
                    label = { Text("Traspasos") },
                    selected = false,
                    onClick = {
                        navController.navigate("traspasos/false")
                        scope.launch { drawerState.close() }
                    }
                )

                // Opción: Stock
                if (sessionViewModel.tienePermiso(7/*8*/)) {
                    NavigationDrawerItem(
                        label = { Text("Stock") },
                        selected = false,
                        onClick = {
                            navController.navigate("stock")
                            scope.launch { drawerState.close() }
                        }
                    )
                }
                NavigationDrawerItem(
                    label = { Text("Etiquetas") },
                    selected = false,
                    onClick = {
                        navController.navigate("etiquetas")
                        scope.launch { drawerState.close() }
                    }
                )

                // Opción: Empresa (visible si tiene más de una empresa)
                if ((user?.empresas?.size ?: 0) > 1) {
                    NavigationDrawerItem(
                        label = { Text("Configuración") },
                        selected = false,
                        onClick = {
                            navController.navigate("configuracion")
                            scope.launch { drawerState.close() }
                        }
                    )
                }

                Spacer(modifier = Modifier.weight(1f))

                // Logout
                NavigationDrawerItem(
                    label = { Text("Cerrar sesión") },
                    selected = false,
                    onClick = {
                        user?.let { logic.hacerLogout(it, context, navController) }
                    },
                    icon = { Icon(Icons.AutoMirrored.Filled.Logout, contentDescription = null) }
                )
            }
        }
    ) {
        Scaffold(
            topBar = {
                AppTopBar(
                    title = "Bienvenido, ${user?.name ?: ""}",
                    sessionViewModel = sessionViewModel,
                    navController = navController,
                    showBackButton = false,
                    customNavigationIcon = {
                        IconButton(onClick = {
                            scope.launch { drawerState.open() }
                        }) {
                            Icon(Icons.Default.Menu, contentDescription = "Abrir menú")
                        }
                    }
                )
            }

        ) { paddingValues ->
            // Contenido principal
            Box(modifier = Modifier.padding(paddingValues)) {
                // Aquí puedes poner una imagen, texto, etc.
            }
        }
    }
}

