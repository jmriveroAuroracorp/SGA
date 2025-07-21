package com.example.sga.view.theme

import android.os.Build
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.dynamicDarkColorScheme
import androidx.compose.material3.dynamicLightColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext

private val LightColors = lightColorScheme(
    primary = AuroraBlue,
    onPrimary = Color.White,
    secondary = AuroraBlueLight,
    background = AuroraGray,
    surface = Color.White,
    onSurface = Color.Black,
    onBackground = Color.Black
)

private val DarkColors = darkColorScheme(
    primary = AuroraBlue,
    onPrimary = Color.White,
    secondary = AuroraBlueLight,
    background = Color.Black,
    surface = Color.DarkGray,
    onSurface = Color.White,
    onBackground = Color.White
)

@Composable
fun SGATheme(
    darkTheme: Boolean = isSystemInDarkTheme(),
    dynamicColor: Boolean = true,
    content: @Composable () -> Unit
) {
    val colorScheme = when {
        dynamicColor && Build.VERSION.SDK_INT >= Build.VERSION_CODES.S -> {
            val context = LocalContext.current
            if (darkTheme) dynamicDarkColorScheme(context) else dynamicLightColorScheme(context)
        }

        darkTheme -> DarkColors
        else -> LightColors
    }

    MaterialTheme(
        colorScheme = colorScheme,
        typography = Typography, // Usa el Typography.kt por defecto, o edítalo si quieres
        content = content
    )
}