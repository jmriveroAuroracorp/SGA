package com.example.sga.data.dto.stock

data class StockDto(
    val codigoEmpresa: String,
    val codigoArticulo: String,
    val descripcionArticulo: String?,
    val codigoAlmacen: String,
    val almacen: String,
    val ubicacion: String,
    val partida: String,
    val fechaCaducidad: String,
    val unidadSaldo: Double,
    val reservado: Double,
    val disponible: Double,
    val tipoStock: String,
    val paletId: String?,
    val codigoPalet: String?,
    val estadoPalet: String?
)