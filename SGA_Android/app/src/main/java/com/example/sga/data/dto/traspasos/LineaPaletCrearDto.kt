package com.example.sga.data.dto.traspasos

import com.google.gson.annotations.SerializedName

data class LineaPaletCrearDto(
    val codigoEmpresa: Short,
    val codigoArticulo: String,
    @SerializedName("DescripcionArticulo")
    val descripcion: String? = null,
    val lote: String? = null,
    val fechaCaducidad: String? = null,
    val cantidad: Double,
    val codigoAlmacen: String,
    val ubicacion: String? = null,
    val usuarioId: Int
)