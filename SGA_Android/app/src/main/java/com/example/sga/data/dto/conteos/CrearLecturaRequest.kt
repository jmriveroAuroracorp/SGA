package com.example.sga.data.dto.conteos

data class CrearLecturaRequest(
    val ordenGuid: String,
    val codigoAlmacen: String,
    val codigoUbicacion: String,
    val codigoArticulo: String,
    val descripcionArticulo: String?,
    val lotePartida: String?,
    val cantidadContada: Double,
    val usuarioCodigo: String,
    val comentario: String?,
    val fechaCaducidad: String?
)
