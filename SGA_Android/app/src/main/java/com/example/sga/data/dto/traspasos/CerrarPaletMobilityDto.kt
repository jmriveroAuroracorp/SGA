package com.example.sga.data.dto.traspasos

data class CerrarPaletMobilityDto(
    val usuarioId: Int,
    val codigoAlmacen: String,
    val codigoEmpresa: Short,
    val ubicacionOrigen: String? = null // ✅ OPCIONAL
)

