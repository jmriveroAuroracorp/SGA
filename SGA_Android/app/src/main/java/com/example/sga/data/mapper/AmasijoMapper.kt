package com.example.sga.data.mapper

import com.example.sga.data.dto.pesajedto.Amasijodto
import com.example.sga.data.model.pesaje.Amasijo
import com.example.sga.data.model.pesaje.Componente

object AmasijoMapper {
    fun fromDto(dto: Amasijodto, componenteMapper: (dto: com.example.sga.data.dto.pesajedto.Componentedto) -> Componente): Amasijo {
        return Amasijo(
            amasijo = dto.amasijo,
            totalPesado = dto.totalPesado,
            componentes = dto.componentes.map(componenteMapper)
        )
    }
}
