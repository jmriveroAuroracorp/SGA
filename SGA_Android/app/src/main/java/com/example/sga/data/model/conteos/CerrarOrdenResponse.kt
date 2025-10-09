package com.example.sga.data.model.conteos

data class CerrarOrdenResponse(
    val ordenGuid: String,
    val totalLecturas: Int,
    val resultadosCreados: Int,
    val fechaCierre: String
)
