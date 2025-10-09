package com.example.sga.data.dto.ordenes

data class PaletPendienteDto(
    val paletDestino: String,
    val codigoGS1: String?,
    val lineasCompletas: Int,
    val cantidadTotal: Double,
    val listoParaUbicar: Boolean
)
