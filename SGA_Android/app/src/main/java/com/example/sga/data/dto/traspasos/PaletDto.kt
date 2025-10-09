package com.example.sga.data.dto.traspasos

import com.google.gson.annotations.SerializedName

data class PaletDto(
    val id: String,

    @SerializedName("codigo")
    val codigoPalet: String,

    @SerializedName("tipoPaletCodigo")
    val tipoPaletCodigo: String,

    @SerializedName("estado")
    val estado: String,

    @SerializedName("fechaApertura")
    val fechaApertura: String?,

    @SerializedName("fechaCierre")
    val fechaCierre: String?,

    @SerializedName("usuarioAperturaId")
    val usuarioAperturaId: Int?,

    @SerializedName("usuarioCierreId")
    val usuarioCierreId: Int?,

    @SerializedName("ordenTrabajoId")
    val ordenTrabajoId: String?,

    @SerializedName("altura")
    val altura: Double?,

    @SerializedName("peso")
    val peso: Double?,

    @SerializedName("etiquetaGenerada")
    val etiquetaGenerada: Boolean,

    @SerializedName("isVaciado")
    val isVaciado: Boolean,

    @SerializedName("fechaVaciado")
    val fechaVaciado: String?,

    @SerializedName("codigoEmpresa")
    val codigoEmpresa: Short,

    @SerializedName("codigoGS1")
    val codigoGS1: String?

)
