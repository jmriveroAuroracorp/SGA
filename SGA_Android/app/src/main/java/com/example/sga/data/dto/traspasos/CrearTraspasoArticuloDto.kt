package com.example.sga.data.dto.traspasos

import java.util.*

data class CrearTraspasoArticuloDto(
    val codigoEmpresa: Short,
    val almacenOrigen: String,
    val ubicacionOrigen: String,
    val codigoArticulo: String,
    val cantidad: Double,
    val usuarioId: Int,
    val almacenDestino: String? = null,
    val ubicacionDestino: String? = null,
    val finalizar: Boolean? = null,
    val fechaCaducidad: String? = null,
    val partida: String? = null,
    val movPosicionOrigen: String? = null,
    val movPosicionDestino: String? = null,
    val descripcionArticulo: String? = null,
    val unidadMedida: String? = null,
    val observaciones: String? = null,
    val comentario: String? = null,
    val paletIdDestino: String? = null  // ID del palet destino seleccionado manualmente
) 