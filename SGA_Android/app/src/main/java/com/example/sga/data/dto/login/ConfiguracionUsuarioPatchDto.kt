package com.example.sga.data.dto.login

data class ConfiguracionUsuarioPatchDto(
    val idEmpresa: String,
    val impresora: String? = null,
    val etiqueta: String? = null
)
