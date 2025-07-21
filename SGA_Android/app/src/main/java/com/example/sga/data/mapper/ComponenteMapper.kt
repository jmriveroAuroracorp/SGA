package com.example.sga.data.mapper

import com.example.sga.data.dto.pesajedto.Componentedto
import com.example.sga.data.model.pesaje.Componente

object ComponenteMapper {
    fun fromDto(dto: Componentedto): Componente {
        return Componente(
            articuloComponente = dto.articuloComponente,
            descripcionArticulo = dto.descripcionArticulo,
            partida = dto.partida,
            fechaCaduca = dto.fechaCaduca,
            unidadesComponente = dto.unidadesComponente
        )
    }
}