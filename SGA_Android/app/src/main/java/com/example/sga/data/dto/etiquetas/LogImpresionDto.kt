package com.example.sga.data.dto.etiquetas

import java.time.LocalDate

data class LogImpresionDto(
    val usuario: String,
    val dispositivo: String,
    val idImpresora: Int,
    val etiquetaImpresa: Int,
    val codigoArticulo: String,
    val descripcionArticulo: String,
    val copias: Int = 1,
    val codigoAlternativo: String? = null,
    val fechaCaducidad: LocalDate? = null,
    val partida: String? = null,
    val alergenos: String? = null,
    val pathEtiqueta: String? = null
)