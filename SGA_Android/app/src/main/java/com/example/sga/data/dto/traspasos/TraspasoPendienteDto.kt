package com.example.sga.data.dto.traspasos

data class TraspasoPendienteDto(
    val id: String,
    val codigoEstado: String,
    val tipoTraspaso: String ,
    val paletCerrado: Boolean,
    val paletId: String?,
    val idLineaOrden: String? = null  // ID de la línea de orden que originó este traspaso
)
