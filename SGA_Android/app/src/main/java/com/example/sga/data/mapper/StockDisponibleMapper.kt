package com.example.sga.data.mapper

import com.example.sga.data.dto.stock.StockDisponibleDto
import com.example.sga.data.model.stock.Stock


object StockDisponibleMapper {
    fun fromDisponibleDto(dto: StockDisponibleDto, codigoEmpresa: String): Stock {
        return Stock(
            codigoEmpresa       = codigoEmpresa,
            codigoArticulo      = dto.codigoArticulo,
            descripcionArticulo = dto.descripcion ?: "Sin descripción",
            codigoAlmacen       = dto.codigoAlmacen ?: "",
            almacen             = dto.almacen ?: "",
            ubicacion           = dto.ubicacion ?: "",
            partida             = dto.partida ?: "",
            fechaCaducidad      = dto.fechaCaducidad ?: "",
            unidadesSaldo       = dto.disponible,
            reservado           = dto.reservado ?: 0.0,
            disponible          = dto.disponible,
            tipoStock           = dto.tipoStock ?: "Suelto", // Usar el tipoStock real del endpoint
            paletId             = dto.paletId,
            codigoPalet         = dto.codigoPalet,
            estadoPalet         = dto.estadoPalet,
            ordenTrabajoId      = dto.ordenTrabajoId
        )
    }
}