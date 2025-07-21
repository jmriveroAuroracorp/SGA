package com.example.sga.data.dto.traspasos

data class CrearTraspasoDto(
    val almacenOrigen: String,
    val ubicacionOrigen: String,
    val paletId: String,
    val usuarioInicioId: Int,
    val codigoPalet: String
) 