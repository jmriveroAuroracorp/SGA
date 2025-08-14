package com.example.sga.data.dto.traspasos

import java.util.*

data class CrearTraspasoArticuloDto(
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
    val codigoEmpresa: Short? = null,
    val unidadMedida: String? = null,
    val observaciones: String? = null,
    val reabrirSiCerradoOrigen: Boolean? = null
) 