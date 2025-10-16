package com.example.sga.data.model.conteos

data class PaletDisponible(
    val paletId: String,
    val codigoPalet: String,
    val codigoGS1: String?,
    val cantidad: Double,
    val estado: String
)
