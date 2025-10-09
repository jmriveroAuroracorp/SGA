package com.example.sga.data.dto.traspasos

data class CompletarTraspasoResponse(
    val message: String?,
    val id: String,  // ← ID del traspaso creado
    val codigoEstado: String?
)

