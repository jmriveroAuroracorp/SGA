package com.example.sga.data.model.conteos

data class ResultadoConteo(
    val guidID: String,
    val ordenGuid: String,
    val diferencia: Double,
    val accionFinal: String,
    val aprobadoPorCodigo: String?,
    val fechaEvaluacion: String,
    val ajusteAplicado: Boolean,
    val codigoAlmacen: String,
    val codigoUbicacion: String,
    val codigoArticulo: String,
    val descripcionArticulo: String?,
    val lotePartida: String,
    val cantidadContada: Double,
    val cantidadStock: Double,
    val usuarioCodigo: String?
)
