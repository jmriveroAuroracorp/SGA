package com.example.sga.data.dto.traspasos

data class PaletCrearDto(
    val codigoEmpresa: Short,
    val usuarioAperturaId: Int,
    val tipoPaletCodigo: String,
    val ordenTrabajoId: String? = null
)
