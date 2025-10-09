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
            tipoStock           = "Disponible", // StockDisponibleDto no distingue entre suelto/paletizado
            paletId             = null,
            codigoPalet         = null,
            estadoPalet         = null
        )
    }
}