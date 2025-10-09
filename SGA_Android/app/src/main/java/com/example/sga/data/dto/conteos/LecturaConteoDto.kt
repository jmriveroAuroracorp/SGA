package com.example.sga.data.dto.conteos

data class LecturaConteoDto(
    val guidID: String,
    val ordenGuid: String,
    val codigoAlmacen: String,
    val codigoUbicacion: String,
    val codigoArticulo: String,
    val descripcionArticulo: String?,
    val lotePartida: String,
    val cantidadContada: Double?,
    val cantidadStock: Double?,
    val usuarioCodigo: String,
    val fecha: String,
    val comentario: String?,
    val fechaCaducidad: String?
)
