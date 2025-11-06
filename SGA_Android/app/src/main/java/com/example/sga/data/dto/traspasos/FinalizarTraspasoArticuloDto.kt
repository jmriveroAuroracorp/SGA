package com.example.sga.data.dto.traspasos

import com.google.gson.annotations.SerializedName

data class FinalizarTraspasoArticuloDto(
    val almacenDestino: String,
    val ubicacionDestino: String,
    val usuarioId: Int,
    @SerializedName("ConfirmarAgregarAPalet")
    val confirmarAgregarAPalet: Boolean? = null,
    @SerializedName("DejarSuelto")
    val dejarSuelto: Boolean? = null,
    @SerializedName("PaletIdConfirmado")
    val paletIdConfirmado: String? = null  // ID del palet seleccionado mediante escaneo GS1
) 