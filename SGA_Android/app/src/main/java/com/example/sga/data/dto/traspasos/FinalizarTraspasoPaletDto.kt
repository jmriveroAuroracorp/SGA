package com.example.sga.data.dto.traspasos

data class FinalizarTraspasoPaletDto(
    val almacenDestino: String,
    val ubicacionDestino: String,
    val usuarioFinalizacionId: Int,
    val codigoEstado: String = "PENDIENTE_ERP"
)
