package com.example.sga.data.dto.stock

data class StockDisponibleDto(
    val codigoArticulo: String,
    val descripcion: String?,
    val partida: String?,
    val ubicacion: String?,
    val disponible: Double,
    val codigoAlmacen: String?,
    val almacen: String?,
    val fechaCaducidad: String?,
    val unidadSaldo: Double?,
    val reservado: Double?
)

