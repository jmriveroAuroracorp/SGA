package com.example.sga.data.dto.login

data class LogEventoDto(
    val IdUsuario: Int,
    val IdDispositivo: String,
    val Tipo: String = "LOGIN",
    val Origen: String = "PantallaLogin",
    val Descripcion: String = "Inicio de sesión correcto",
    val Detalle: String = "El usuario accedió desde dispositivo móvil"
)

