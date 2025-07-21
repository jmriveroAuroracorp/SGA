package com.example.sga.data.dto.traspasos

import com.google.gson.annotations.SerializedName

data class LineaPaletDto(
    val id: String,
    @SerializedName("paletId")
    val idPalet: String,
    val codigoArticulo: String,
    @SerializedName("descripcionArticulo")
    val descripcion: String?,
    val lote: String?,
    val fechaCaducidad: String?,
    val cantidad: Double,
    val ubicacion: String?
)