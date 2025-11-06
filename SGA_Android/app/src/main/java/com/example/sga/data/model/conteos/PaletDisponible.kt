package com.example.sga.data.model.conteos

import java.util.UUID

data class PaletDisponible(
    val paletId: UUID,
    val codigoPalet: String,
    val codigoGS1: String,
    val cantidad: Double,
    val estado: String
)

