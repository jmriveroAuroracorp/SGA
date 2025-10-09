package com.example.sga.data.dto.ordenes

data class ActualizarLineaResponseDto(
    val success: Boolean,
    val paletListoParaUbicar: String?,
    val mensaje: String?,
    val estadoLinea: String?  // NUEVO CAMPO
)
