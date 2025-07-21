package com.example.sga.view.app

import android.os.Build
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.ui.platform.LocalContext
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.navigation.compose.rememberNavController
import com.example.sga.data.ApiManager
import kotlinx.coroutines.launch
import android.provider.Settings
import android.content.Intent
import android.util.Log
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Text
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp

@Composable
fun SGAApp() {
    val context = LocalContext.current
    val sessionViewModel: SessionViewModel = viewModel()
    val navController = rememberNavController()
    val scope = rememberCoroutineScope()
    val readyToRender = rememberSaveable { mutableStateOf(false) }

    val updateLogic = remember { UpdateLogic(sessionViewModel) }
    val isLoggedIn by sessionViewModel.isLoggedIn

    // Navegar a login solo cuando esté montado y no haya sesión
    LaunchedEffect(readyToRender.value, isLoggedIn) {
        if (readyToRender.value && !isLoggedIn && navController.currentDestination?.route != "login") {
            navController.navigate("login") {
                popUpTo(0) { inclusive = true }
            }
        }
    }

    val permisoInstalacionLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.StartActivityForResult()
    ) {
        scope.launch {
            updateLogic.comprobarYActualizar(context, ApiManager.versionApi)
            readyToRender.value = true
        }
    }

    LaunchedEffect(Unit) {
        if (!readyToRender.value) {
            ApiManager.init(sessionViewModel) {
                sessionViewModel.clearSession()
            }

            updateLogic.setReintentoLanzador {
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                    val intent = Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES).apply {
                        data = android.net.Uri.parse("package:${context.packageName}")
                    }
                    permisoInstalacionLauncher.launch(intent)
                }
            }

            updateLogic.comprobarYActualizar(context, ApiManager.versionApi)
            readyToRender.value = true
        }
    }

    if (readyToRender.value) {
        AppScreen(navController, sessionViewModel)
    } else {
        Box(
            modifier = Modifier.fillMaxSize(),
            contentAlignment = Alignment.Center
        ) {
            Column(horizontalAlignment = Alignment.CenterHorizontally) {
                CircularProgressIndicator()
                Spacer(modifier = Modifier.height(24.dp))
                Text("Buscando y descargando actualizaciones…")
            }
        }
    }
}




















