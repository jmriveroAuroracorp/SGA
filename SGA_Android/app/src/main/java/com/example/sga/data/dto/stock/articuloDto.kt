package com.example.sga.data.dto.stock

import java.time.LocalDate

data class ArticuloDto(
    val codigoArticulo: String,
    val descripcion: String,
    val codigoAlternativo: String?,
    val partida: String?,
    val fechaCaducidad: String? = null
)