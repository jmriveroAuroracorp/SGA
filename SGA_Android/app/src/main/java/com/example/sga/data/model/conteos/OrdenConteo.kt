package com.example.sga.data.model.conteos

data class OrdenConteo(
    val guidID: String,
    val codigoEmpresa: String,
    val titulo: String,
    val visibilidad: String,
    val modoGeneracion: String,
    val alcance: String,
    val filtrosJson: String?,
    val codigoAlmacen: String?,
    val codigoArticulo: String?,
    val codigoUbicacion: String?,
    val codigoOperario: String?,
    val estado: String,
    val fechaCreacion: String,
    val fechaAsignacion: String?,
    val fechaInicio: String?,
    val fechaCierre: String?,
    val creadoPorCodigo: String,
    val prioridad: Int
)
