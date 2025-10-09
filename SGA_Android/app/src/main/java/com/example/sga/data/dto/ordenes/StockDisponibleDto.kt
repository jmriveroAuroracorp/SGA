package com.example.sga.data.dto.ordenes

data class StockDisponibleDto(
    val codigoAlmacen: String,
    val ubicacion: String?,
    val codigoArticulo: String,
    val descripcionArticulo: String?,
    val partida: String?,
    val cantidadDisponible: Double,
    val fechaCaducidad: String?
)
