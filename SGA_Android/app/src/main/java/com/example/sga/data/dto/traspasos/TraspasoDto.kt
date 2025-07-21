package com.example.sga.data.dto.traspasos

data class TraspasoDto(
    val id: String,
    val almacenOrigen: String,
    val almacenDestino: String?,
    val codigoEstado: String,
    val fechaInicio: String,
    val usuarioInicioId: Int,
    val paletId: String,
    val fechaFinalizacion: String?,
    val usuarioFinalizacionId: Int?,
    val ubicacionDestino: String?,
    val ubicacionOrigen: String?,
    val codigoPalet: String?
) 