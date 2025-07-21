package com.example.sga.data.dto.traspasos

data class TraspasoArticuloDto(
    val id: String,
    val almacenOrigen: String,
    val ubicacionOrigen: String,
    val almacenDestino: String?,
    val ubicacionDestino: String?,
    val usuarioId: Int,
    val fecha: String,
    val codigoArticulo: String,
    val cantidad: Double,
    val estado: String
) 