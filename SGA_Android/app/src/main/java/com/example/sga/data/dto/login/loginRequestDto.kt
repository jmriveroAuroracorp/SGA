package com.example.sga.data.dto.login

data class LoginRequestDto(
    val operario: Int,
    val contraseña: String,
    val idDispositivo: String,
    val tipoDispositivo: String
)