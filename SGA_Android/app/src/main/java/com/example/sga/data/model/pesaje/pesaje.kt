package com.example.sga.data.model.pesaje

    data class Pesaje(
        val ejercicio: Int,
        val serie: String,
        val numero: Int,
        val numeroAmasijos: Double,
        val ordenesTrabajo: List<OrdenTrabajo>
    )
