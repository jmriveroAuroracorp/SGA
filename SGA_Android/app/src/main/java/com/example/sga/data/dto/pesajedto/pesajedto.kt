package com.example.sga.data.dto.pesajedto

    data class Pesajedto(
        val ejercicioFabricacion: Int,
        val serieFabricacion: String,
        val numeroFabricacion: Int,
        val vNumeroAmasijos: Double,
        val ordenesTrabajo: List<OrdenTrabajodto>
    )
