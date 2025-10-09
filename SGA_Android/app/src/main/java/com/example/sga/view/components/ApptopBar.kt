package com.example.sga.view.components


import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.navigation.NavHostController
import com.example.sga.view.app.SessionViewModel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AppTopBar(
    sessionViewModel: SessionViewModel,
    navController: NavHostController,
    title: String = "",
    showBackButton: Boolean = true,
    customNavigationIcon: (@Composable (() -> Unit))? = null,
) {
    val user by sessionViewModel.user.collectAsState()
    val empresa by sessionViewModel.empresaSeleccionada.collectAsState()
    val configuration = LocalConfiguration.current
    val density = LocalDensity.current

    val saludo = title.ifBlank { "Hola, ${user?.name ?: ""}" }
    val empresaTexto = "${empresa?.nombre ?: ""}"
    val fitsInOneLine = remember { mutableStateOf(true) }
    val fontSizePx = with(LocalDensity.current) {
        MaterialTheme.typography.titleLarge.fontSize.toPx()
    }

    LaunchedEffect(saludo, empresaTexto, configuration) {
        val paint = android.graphics.Paint().apply {
            textSize = fontSizePx
        }

        val totalText = "$saludo    $empresaTexto"
        val textWidthPx = paint.measureText(totalText)
        val screenWidthPx = with(density) { configuration.screenWidthDp.dp.toPx() }

        fitsInOneLine.value = textWidthPx < (screenWidthPx - 80)
    }

    TopAppBar(
        title = {
            if (title.isNotBlank()) {
                Text(
                    text = title,
                    style = MaterialTheme.typography.bodyMedium,
                    modifier = Modifier.fillMaxWidth(),
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis
                )
            } else {
                Text(
                    text = empresaTexto,
                    style = MaterialTheme.typography.bodyMedium,
                    modifier = Modifier.fillMaxWidth(),
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis
                )
            }
        },
        navigationIcon = {
            when {
                customNavigationIcon != null -> customNavigationIcon()
                showBackButton -> {
                    IconButton(onClick = {
                        val popped = navController.popBackStack()
                        if (!popped) {
                            navController.navigate("home") {
                                popUpTo("home") { inclusive = true }
                                launchSingleTop = true
                            }
                        }
                    }) {
                        Icon(
                            imageVector = Icons.AutoMirrored.Filled.ArrowBack,
                            contentDescription = "Volver"
                        )
                    }
                }
            }
        }
    )
}

