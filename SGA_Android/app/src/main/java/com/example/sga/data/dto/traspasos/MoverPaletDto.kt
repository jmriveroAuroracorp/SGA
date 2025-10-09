package com.example.sga.data.dto.traspasos

data class MoverPaletDto(
    val paletId: String,
    val usuarioId: Int,
    val codigoPalet: String,
    val codigoEmpresa: Short,
    val fechaInicio: String,              // Formato ISO 8601
    val tipoTraspaso: String = "PALET",
    val codigoEstado: String = "PENDIENTE",
    val comentario: String? = null,
    )

