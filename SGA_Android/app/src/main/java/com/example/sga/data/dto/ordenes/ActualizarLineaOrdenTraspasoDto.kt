package com.example.sga.data.dto.ordenes

data class ActualizarLineaOrdenTraspasoDto(
    val estado: String?,
    val cantidadMovida: Double?,
    val completada: Boolean?,
    val idOperarioAsignado: Int?,
    val fechaInicio: String?,
    val fechaFinalizacion: String?,
    val idTraspaso: String?,
    val fechaCaducidad: String?,
    val codigoAlmacenOrigen: String?,
    val ubicacionOrigen: String?,
    val partida: String?,
    val paletOrigen: String?,
    val codigoAlmacenDestino: String?,
    val ubicacionDestino: String?,
    val paletDestino: String?
)
