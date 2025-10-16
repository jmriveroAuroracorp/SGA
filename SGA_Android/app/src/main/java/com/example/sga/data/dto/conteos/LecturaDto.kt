package com.example.sga.data.dto.conteos

data class LecturaDto(
    val codigoArticulo: String,
    val codigoUbicacion: String,
    val codigoAlmacen: String,
    val lotePartida: String,
    val cantidadContada: Double,
    val usuarioCodigo: String,
    val comentario: String?,
    val ordenGuid: String? = null,
    val fechaCaducidad: String? = null,
    val paletId: String? = null,
    val codigoPalet: String? = null,
    val codigoGS1: String? = null
)
