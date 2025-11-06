package com.example.sga.data.dto.traspasos

data class PrecheckResp(
    val existe: Boolean,
    val paletId: String? = null,
    val codigoPalet: String? = null,
    val cerrado: Boolean? = null,
    val cantidadPalets: Int? = null,
    val palets: List<PaletDto>? = null,
    val aviso: String? = null
)