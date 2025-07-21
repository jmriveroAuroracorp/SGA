package com.example.sga.data.dto.traspasos

data class CompletarTraspasoDto(
    val codigoAlmacenDestino: String,
    val ubicacionDestino: String,
    val fechaFinalizacion: String, // formato ISO 8601: yyyy-MM-ddTHH:mm:ss
    val usuarioFinalizacionId: Int
)
