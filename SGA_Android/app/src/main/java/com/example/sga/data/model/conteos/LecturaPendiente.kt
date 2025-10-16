package com.example.sga.data.model.conteos

data class LecturaPendiente(
    val codigoAlmacen: String,
    val codigoUbicacion: String,
    val codigoArticulo: String,
    val descripcionArticulo: String?,
    val lotePartida: String?,
    val cantidadStock: Double?,
    val cantidadTeorica: Double?,
    val cantidadContada: Double?,
    val fechaCaducidad: String?,
    val paletId: String? = null,
    val codigoPalet: String? = null,
    val codigoGS1: String? = null
)
