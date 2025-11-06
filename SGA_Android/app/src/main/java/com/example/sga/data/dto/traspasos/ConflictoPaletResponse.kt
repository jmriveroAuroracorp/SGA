package com.example.sga.data.dto.traspasos

import com.google.gson.annotations.SerializedName

data class ConflictoPaletResponse(
    val message: String,
    val requiereConfirmacion: Boolean,
    val paletDetectado: Boolean,
    val paletCerrado: Boolean? = null,
    val paletId: String? = null,  // Nullable cuando hay múltiples palets
    val codigoPalet: String? = null,  // Nullable cuando hay múltiples palets
    val almacen: String? = null,
    val ubicacion: String? = null,
    val opciones: List<OpcionPalet>? = null,  // Puede ser null si no hay opciones
    val cantidadPalets: Int? = null,  // Cantidad de palets en la ubicación
    val palets: List<PaletInfo>? = null  // Lista de palets disponibles
)

data class PaletInfo(
    val paletId: String
)

data class OpcionPalet(
    val tipo: String,  // "paletizar", "suelto", "cancelar"
    val descripcion: String,
    val accion: String  // "ConfirmarAgregarAPalet", "DejarSuelto", "Cancelar"
)

