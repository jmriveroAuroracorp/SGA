package com.example.sga.data.dto.etiquetas

import com.google.gson.annotations.SerializedName
import java.time.LocalDate

data class LogImpresionDto(
    val usuario: String,
    val dispositivo: String,
    val idImpresora: Int,
    val etiquetaImpresa: Int,
    val codigoArticulo: String? = null,
    val descripcionArticulo: String? = null,
    val copias: Int = 1,
    val codigoAlternativo: String? = null,
    val fechaCaducidad: String? = null,
    val partida: String? = null,
    val alergenos: String? = null,
    val pathEtiqueta: String? = null,
    @SerializedName("TipoEtiqueta")
    val tipoEtiqueta: Int? = null,
    @SerializedName("CodigoGS1")
    val codigoGS1: String? = null,
    @SerializedName("CodigoPalet")
    val codigoPalet: String? = null
)