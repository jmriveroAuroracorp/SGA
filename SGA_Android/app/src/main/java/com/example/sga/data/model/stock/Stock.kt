package com.example.sga.data.model.stock

data class Stock(
    val codigoEmpresa: String,
    val codigoArticulo: String,
    val descripcionArticulo: String?,
    val codigoCentro: String,
    val codigoAlmacen: String,
    val almacen: String,
    val ubicacion: String,
    val partida: String,
    val fechaCaducidad: String,
    val unidadesSaldo: Double
)