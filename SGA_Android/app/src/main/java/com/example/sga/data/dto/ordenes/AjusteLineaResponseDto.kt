package com.example.sga.data.dto.ordenes

data class AjusteLineaResponseDto(
    val success: Boolean,
    val mensaje: String,
    val estadoLinea: String?,
    val diferencia: Double?,
    val requiereSupervision: Boolean
)
