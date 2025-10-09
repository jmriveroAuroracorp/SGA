package com.example.sga.data.dto.ordenes

data class OrdenTraspasoDto(
    val idOrdenTraspaso: String,
    val codigoEmpresa: Int,
    val estado: String,
    val prioridad: Int,
    val fechaPlan: String?,
    val fechaInicio: String?,
    val fechaFinalizacion: String?,
    val tipoOrigen: String,
    val idOrigen: String?,
    val usuarioCreacion: Int,
    val usuarioAsignado: Int?,
    val comentarios: String?,
    val fechaCreacion: String,
    val codigoOrden: String?,
    val codigoAlmacenDestino: String?,
    val lineas: List<LineaOrdenTraspasoDetalleDto>
)
