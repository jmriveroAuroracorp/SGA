package com.example.sga.data.dto.almacenes

data class AlmacenDto(
    val codigoAlmacen: String,
    val nombreAlmacen: String,
    val codigoEmpresa: Short,
    val esDelCentro: Boolean
)