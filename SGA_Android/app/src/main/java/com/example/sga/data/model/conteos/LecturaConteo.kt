package com.example.sga.data.model.conteos

data class LecturaConteo(
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
    val fechaCaducidad: String?,
    val paletId: String? = null,
    val codigoPalet: String? = null,
    val codigoGS1: String? = null
)
