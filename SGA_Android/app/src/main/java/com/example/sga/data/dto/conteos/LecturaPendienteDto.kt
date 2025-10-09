package com.example.sga.data.dto.conteos

data class LecturaPendienteDto(
    val codigoAlmacen: String,
    val codigoUbicacion: String,
    val codigoArticulo: String,
    val descripcionArticulo: String?,
    val lotePartida: String?,
    val cantidadStock: Double?,
    val cantidadTeorica: Double?,
    val fechaCaducidad: String?
)
