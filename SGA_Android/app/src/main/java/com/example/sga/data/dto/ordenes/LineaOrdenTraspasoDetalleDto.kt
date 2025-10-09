package com.example.sga.data.dto.ordenes

import com.google.gson.annotations.SerializedName

data class LineaOrdenTraspasoDetalleDto(
    @SerializedName("idLineaOrden")
    val idLineaOrdenTraspaso: String,
    val idOrdenTraspaso: String,
    val numeroLinea: Int,
    val codigoArticulo: String,
    val descripcionArticulo: String?,
    val cantidadPlan: Double,
    val codigoAlmacenOrigen: String,
    val ubicacionOrigen: String?,
    val partida: String?,
    val paletOrigen: String?,
    val codigoAlmacenDestino: String,
    val ubicacionDestino: String?,
    val paletDestino: String?,
    val estado: String,
    val cantidadMovida: Double,
    val completada: Boolean,
    val idOperarioAsignado: Int?,
    val fechaInicio: String?,
    val fechaFinalizacion: String?,
    val idTraspaso: String?,
    val fechaCaducidad: String?
)
