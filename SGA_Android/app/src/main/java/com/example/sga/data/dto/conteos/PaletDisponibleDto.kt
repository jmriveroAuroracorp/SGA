package com.example.sga.data.dto.conteos

data class PaletDisponibleDto(
    val paletId: String,
    val codigoPalet: String,
    val codigoGS1: String?,
    val cantidad: Double,
    val estado: String
)
