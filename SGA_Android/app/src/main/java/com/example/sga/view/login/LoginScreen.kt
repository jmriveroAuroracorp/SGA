package com.example.sga.view.login

import android.util.Log
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.navigation.NavController
import com.example.sga.view.app.SessionViewModel
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.platform.LocalSoftwareKeyboardController
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.input.ImeAction
import com.example.sga.R.drawable.logo_aurorasga
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Warning

@Composable
fun LoginScreen(
    navController: NavController,
    loginViewModel: LoginViewModel = viewModel(),
    sessionViewModel: SessionViewModel = viewModel()
) {
    Log.d(" SGA_UPDATE ", ">>> LoginScreen mostrada")
    val context = LocalContext.current
    val logic = remember { LoginLogic(loginViewModel, sessionViewModel, context) }

    var operario by remember { mutableStateOf("") }
    var contraseña by remember { mutableStateOf("") }
    var localError by remember { mutableStateOf("") }

    // Auto-ocultar error después de 2 segundos
    LaunchedEffect(localError) {
        if (localError.isNotBlank()) {
            kotlinx.coroutines.delay(2000)
            localError = ""
        }
    }

    val user by loginViewModel.user.collectAsState()
    val backendError by loginViewModel.error.collectAsState()
    val isLoading by loginViewModel.isLoading.collectAsState() // Agregar estado de carga
    val keyboardController = LocalSoftwareKeyboardController.current
    val focusContraseña = remember { androidx.compose.ui.focus.FocusRequester() }
    // Navegación reactiva según el destino que emita el ViewModel
    LaunchedEffect(Unit) {
        loginViewModel.navigate.collect { destino ->
            when (destino) {
                is LoginViewModel.Destino.Traspasos -> navController.navigate("traspasos/${destino.esPalet}/${destino.directoDesdePaletCerrado}") {
                    popUpTo("login") { inclusive = true } // ✅ Elimina Login del backstack
                }
                LoginViewModel.Destino.OrdenTraspaso -> {
                    android.util.Log.d("LOGIN_NAVIGATION", "🔍 Navegando a ordenes (con traspaso pendiente)")
                    navController.navigate("ordenes") {
                        popUpTo("login") { inclusive = true }
                    }
                }
                LoginViewModel.Destino.Home -> navController.navigate("home") {
                    popUpTo("login") { inclusive = true } // ✅ Igual aquí
                }
            }
        }
    }

    // 🔐 Diálogo de sesión activa
    var mostrarDialogo by remember { mutableStateOf(false) }
    var mensajeDialogo by remember { mutableStateOf("") }
    var onConfirmarLogin: (() -> Unit)? by remember { mutableStateOf(null) }

    // Si hay usuario válido, lo guardamos en la sesión y navegamos
    LaunchedEffect(user) {
        if (user != null) {
            sessionViewModel.setUser(user!!)
        }

    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(Color.White)
            .padding(32.dp),
        verticalArrangement = Arrangement.Center,
        horizontalAlignment = Alignment.CenterHorizontally
    ) {

        Image(
            painter = painterResource(id = logo_aurorasga),
            contentDescription = "Logotipo Aurora SGA",
            modifier = Modifier
                .fillMaxWidth()
                .padding(32.dp)
                .height(100.dp),
            contentScale = ContentScale.Fit
        )

        Spacer(modifier = Modifier.height(32.dp))

        // Subtítulo
        Text(
            text = "Acceso:",
            style = MaterialTheme.typography.titleMedium
        )

        // Mostrar error si existe
        if (localError.isNotBlank()) {
            Spacer(modifier = Modifier.height(8.dp))
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors(
                    containerColor = MaterialTheme.colorScheme.errorContainer
                )
            ) {
                Row(
                    modifier = Modifier.padding(12.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Icon(
                        imageVector = Icons.Default.Warning,
                        contentDescription = "Error",
                        tint = MaterialTheme.colorScheme.onErrorContainer
                    )
                    Spacer(modifier = Modifier.width(8.dp))
                    Text(
                        text = localError,
                        color = MaterialTheme.colorScheme.onErrorContainer,
                        style = MaterialTheme.typography.bodyMedium
                    )
                }
            }
        }

        Spacer(modifier = Modifier.height(16.dp))

        OutlinedTextField(
            value = operario,
            onValueChange = { input ->
                if (input.all { it.isDigit() }) {
                    operario = input
                }
            },
            label = { Text("Operario") },
            modifier = Modifier.fillMaxWidth(),
            keyboardOptions = KeyboardOptions(
                keyboardType = KeyboardType.Number,
                imeAction = ImeAction.Next
            ),
            keyboardActions = KeyboardActions(
                //onDone = { keyboardController?.hide() }
                onNext = { focusContraseña.requestFocus() }
            ),
            singleLine = true
        )

        Spacer(modifier = Modifier.height(16.dp))

        OutlinedTextField(
            value = contraseña,
            onValueChange = { input ->
                if (input.all { it.isDigit() }) {
                    contraseña = input
                }
            },
            label = { Text("Contraseña") },
            modifier = Modifier
                .fillMaxWidth()
                .focusRequester(focusContraseña),
            keyboardOptions = KeyboardOptions(
                keyboardType = KeyboardType.NumberPassword,
                imeAction = ImeAction.Done
            ),
            keyboardActions = KeyboardActions(
                onDone = {
                    keyboardController?.hide()
                    logic.onLoginClick(
                        operario,
                        contraseña,
                        showError = { msg -> localError = msg },
                        mostrarDialogoConfirmacion = { mensaje, onAceptar ->
                            mensajeDialogo = mensaje
                            onConfirmarLogin = onAceptar
                            mostrarDialogo = true
                        }
                    )
                }
            ),
            visualTransformation = PasswordVisualTransformation(),
            singleLine = true
        )

        Spacer(modifier = Modifier.height(16.dp))

        Button(
            onClick = {
                keyboardController?.hide()
                logic.onLoginClick(
                    operario,
                    contraseña,
                    showError = { msg -> localError = msg },
                    mostrarDialogoConfirmacion = { mensaje, onAceptar ->
                        mensajeDialogo = mensaje
                        onConfirmarLogin = onAceptar
                        mostrarDialogo = true
                    }
                )
            },
            modifier = Modifier.fillMaxWidth(),
            enabled = !isLoading // Deshabilitar durante carga
        ) {
            if (isLoading) {
                CircularProgressIndicator(
                    modifier = Modifier.size(20.dp),
                    color = MaterialTheme.colorScheme.onPrimary,
                    strokeWidth = 2.dp
                )
                Spacer(modifier = Modifier.width(8.dp))
                Text("Conectando...")
            } else {
                Text("Entrar")
            }
        }
    }
    // Diálogo de confirmación
    if (mostrarDialogo) {
        AlertDialog(
            onDismissRequest = { mostrarDialogo = false },
            confirmButton = {
                TextButton(onClick = {
                    mostrarDialogo = false
                    onConfirmarLogin?.invoke()
                }) {
                    Text("Sí, cerrar sesión anterior")
                }
            },
            dismissButton = {
                TextButton(onClick = {
                    mostrarDialogo = false
                }) {
                    Text("No")
                }
            },
            title = { Text("Sesión activa detectada") },
            text = { Text(mensajeDialogo) }
        )
    }
}

