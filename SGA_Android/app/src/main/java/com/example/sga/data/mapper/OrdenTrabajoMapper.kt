package com.example.sga.data.mapper

import com.example.sga.data.dto.pesajedto.OrdenTrabajodto
import com.example.sga.data.model.pesaje.Amasijo
import com.example.sga.data.model.pesaje.OrdenTrabajo

object OrdenTrabajoMapper {
    fun fromDto(dto: OrdenTrabajodto, amasijoMapper: (dto: com.example.sga.data.dto.pesajedto.Amasijodto) -> Amasijo): OrdenTrabajo {
        return OrdenTrabajo(
            codigoArticuloOT = dto.codigoArticuloOT,
            descripcionArticuloOT = dto.descripcionArticuloOT,
            amasijos = dto.amasijos.map(amasijoMapper)
        )
    }
}