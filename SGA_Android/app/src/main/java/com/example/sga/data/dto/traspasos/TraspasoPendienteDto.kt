package com.example.sga.data.dto.traspasos

data class TraspasoPendienteDto(
    val id: String,
    val codigoEstado: String,
    val tipoTraspaso: String // ← nuevo campo añadido
)
