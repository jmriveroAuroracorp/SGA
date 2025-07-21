package com.example.sga.data.dto.traspasos

import com.google.gson.annotations.SerializedName

data class TipoPaletDto(
    @SerializedName("codigoPalet") val codigoPalet: String,
    @SerializedName("descripcion") val descripcion: String
)