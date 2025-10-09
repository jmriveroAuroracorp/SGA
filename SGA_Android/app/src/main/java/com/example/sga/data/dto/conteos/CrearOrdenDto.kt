package com.example.sga.data.dto.conteos

data class CrearOrdenDto(
    val titulo: String,
    val visibilidad: String,
    val modoGeneracion: String,
    val alcance: String,
    val filtrosJson: String?,
    val creadoPorCodigo: String,
    val codigoOperario: String?
)
