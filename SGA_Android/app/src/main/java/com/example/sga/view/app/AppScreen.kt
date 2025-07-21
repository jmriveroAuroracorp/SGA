package com.example.sga.view.app

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.platform.LocalContext
import androidx.navigation.NavHostController
import com.example.sga.service.Inactivity.InactivityTracker
import com.example.sga.view.home.HomeLogic
import com.example.sga.view.navigation.NavGraph
import kotlinx.coroutines.delay
import androidx.compose.runtime.*
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.zIndex
import androidx.compose.material3.*
import androidx.compose.ui.text.input.KeyboardType


@Composable
fun AppScreen(navController: NavHostController, sessionViewModel: SessionViewModel) {
    val context = LocalContext.current
    val userState = sessionViewModel.user.collectAsState()
    val user = userState.value

    val showReauthDialog = remember { mutableStateOf(false) }
    val tokenExpirado by sessionViewModel.tokenExpirado.collectAsState()

    LaunchedEffect(user) {
        if (user != null) {
            InactivityTracker.initialize {
                user?.let {
                    HomeLogic(sessionViewModel).hacerLogout(it, context, navController)
                }
            }
        } else {
            InactivityTracker.stop()
        }
    }

    NavGraph(
        navController = navController,
        sessionViewModel = sessionViewModel
    )

    val vigilanciaActiva by sessionViewModel.modoVigilanciaActiva.collectAsState()
    LaunchedEffect(Unit) {
        while (true) {
            delay(1000L) // cada segundo

            val tokenTime = sessionViewModel.tokenTimestamp.value
            val now = System.currentTimeMillis()

            val limite = if (vigilanciaActiva) {
                1 * 60 * 60 * 1000L // 1 hora
            } else {
                8 * 60 * 60 * 1000L // 8 horas
            }
            /*
            val limite = if (vigilanciaActiva) {
                20 * 1000L // 20 segundos
            } else {
                1 * 60 * 1000L // 1 minutos
            }*/

            if (tokenTime != null && now - tokenTime >= limite) {
                sessionViewModel.marcarTokenExpirado(true)
            }
        }
    }

    LaunchedEffect(tokenExpirado) {
        if (tokenExpirado) {
            showReauthDialog.value = true
        }
    }
    val mostrarDialogoPassword = remember { mutableStateOf(false) }
    val passwordInput = remember { mutableStateOf("") }
    val errorPassword = remember { mutableStateOf(false) }

    if (showReauthDialog.value && user != null) {
        AlertDialog(
            onDismissRequest = {},
            title = { Text("¿Sigues siendo tú?") },
            text = { Text("Confirmación de sesión para ${user.name}.") },
            confirmButton = {
                TextButton(onClick = {
                    showReauthDialog.value = false
                    mostrarDialogoPassword.value = true // 👈 vamos al diálogo de contraseña
                }) {
                    Text("Sí")
                }
            },
            dismissButton = {
                TextButton(onClick = {
                    HomeLogic(sessionViewModel).hacerLogout(user, context, navController)
                    showReauthDialog.value = false
                }) {
                    Text("No")
                }
            }
        )
    }
    if (mostrarDialogoPassword.value && user != null) {
        AlertDialog(
            onDismissRequest = {},
            title = { Text("Verificación de identidad") },
            text = {
                Column {
                    Text("Introduce tu contraseña para continuar:")
                    Spacer(modifier = Modifier.height(8.dp))
                    OutlinedTextField(
                        value = passwordInput.value,
                        onValueChange = {
                            passwordInput.value = it
                            errorPassword.value = false
                        },
                        placeholder = { Text("Contraseña") },
                        singleLine = true,
                        visualTransformation = PasswordVisualTransformation(),
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.NumberPassword),
                        isError = errorPassword.value
                    )
                    if (errorPassword.value) {
                        Text(
                            text = "Contraseña incorrecta",
                            color = Color.Red,
                            fontSize = 12.sp,
                            modifier = Modifier.padding(top = 4.dp)
                        )
                    }
                }
            },
            confirmButton = {
                TextButton(onClick = {
                    val contraseñaCorrecta = sessionViewModel.contraseña.value

                    if (passwordInput.value == contraseñaCorrecta) {
                        sessionViewModel.actualizarTimestamp()
                        sessionViewModel.marcarTokenExpirado(false)
                        sessionViewModel.activarVigilanciaActiva()
                        passwordInput.value = ""
                        mostrarDialogoPassword.value = false
                    } else {
                        errorPassword.value = true
                    }
                }) {
                    Text("Confirmar")
                }
            },
            dismissButton = {
                TextButton(onClick = {
                    mostrarDialogoPassword.value = false
                    showReauthDialog.value = true // 👈 vuelve al anterior
                    passwordInput.value = ""
                }) {
                    Text("Atrás")
                }
            }
        )
    }

    val mostrarMensajeCaducidad by sessionViewModel.mensajeCaducidad.collectAsState()

    if (mostrarMensajeCaducidad) {
        LaunchedEffect(Unit) {
            delay(3000L)
            sessionViewModel.ocultarMensajeCaducidad()
        }

        Box(
            modifier = Modifier
                .fillMaxSize()
                .background(Color.Black.copy(alpha = 0.5f))
                .zIndex(1f),
            contentAlignment = Alignment.Center
        ) {
            Text(
                text = "Su sesión ha caducado. Será redirigido al login.",
                color = Color.White,
                fontSize = 18.sp,
                modifier = Modifier
                    .padding(32.dp)
                    .background(Color.DarkGray, shape = RoundedCornerShape(8.dp))
                    .padding(16.dp)
            )
        }
    }
}

