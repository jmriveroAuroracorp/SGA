package com.example.sga.data.dto.conteos

data class CerrarOrdenResponseDto(
    val ordenGuid: String,
    val totalLecturas: Int,
    val resultadosCreados: Int,
    val fechaCierre: String
)
