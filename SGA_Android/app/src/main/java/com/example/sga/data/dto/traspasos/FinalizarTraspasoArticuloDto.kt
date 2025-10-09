package com.example.sga.data.dto.traspasos

import com.google.gson.annotations.SerializedName

data class FinalizarTraspasoArticuloDto(
    val almacenDestino: String,
    val ubicacionDestino: String,
    val usuarioId: Int,
    @SerializedName("confirmarAgregarAPalet")
    val confirmarAgregarAPalet: Boolean? = null
) 