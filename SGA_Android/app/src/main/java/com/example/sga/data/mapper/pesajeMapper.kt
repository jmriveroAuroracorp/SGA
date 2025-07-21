package com.example.sga.data.mapper

import com.example.sga.data.model.pesaje.*
import com.example.sga.data.dto.pesajedto.*

object PesajeMapper {
    fun fromDto(dto: Pesajedto, OrdenTrabajoMapper: (OrdenTrabajodto) -> OrdenTrabajo): Pesaje {
        return Pesaje(
            ejercicio = dto.ejercicioFabricacion,
            serie = dto.serieFabricacion,
            numero = dto.numeroFabricacion,
            numeroAmasijos = dto.vNumeroAmasijos,
            ordenesTrabajo = dto.ordenesTrabajo.map(OrdenTrabajoMapper)
        )
    }
}
