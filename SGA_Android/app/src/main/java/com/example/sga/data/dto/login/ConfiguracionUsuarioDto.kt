package com.example.sga.data.dto.login

data class ConfiguracionUsuarioDto(
    val idUsuario: Int,
    val idEmpresa: String,
    val impresora: String?,
    val etiqueta: String?
)
