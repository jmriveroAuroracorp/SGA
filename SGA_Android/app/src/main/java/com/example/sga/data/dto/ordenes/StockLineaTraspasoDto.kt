package com.example.sga.data.dto.ordenes

data class StockLineaTraspasoDto(
    val codigoAlmacen: String,
    val ubicacion: String?,
    val partida: String?,
    val fechaCaducidad: String?,
    val cantidadDisponible: Double,
    val codigoArticulo: String,
    val descripcionArticulo: String?
)
