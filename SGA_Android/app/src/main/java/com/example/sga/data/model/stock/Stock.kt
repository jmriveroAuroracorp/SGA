package com.example.sga.data.model.stock

data class Stock(
    val codigoEmpresa: String,
    val codigoArticulo: String,
    val descripcionArticulo: String?,
    val codigoAlmacen: String,
    val almacen: String,
    val ubicacion: String,
    val partida: String,
    val fechaCaducidad: String,
    val unidadesSaldo: Double,
    val reservado: Double,
    val disponible: Double,
    val tipoStock: String,
    val paletId: String?,
    val codigoPalet: String?,
    val estadoPalet: String?
)