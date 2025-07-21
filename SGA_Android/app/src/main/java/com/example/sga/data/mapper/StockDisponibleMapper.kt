package com.example.sga.data.mapper

import com.example.sga.data.dto.stock.StockDisponibleDto
import com.example.sga.data.model.stock.Stock


object StockDisponibleMapper {
    fun fromDisponibleDto(dto: StockDisponibleDto): Stock {
        return Stock(
            codigoArticulo      = dto.codigoArticulo,
            descripcionArticulo = dto.descripcion ?: "Sin descripción",
            partida             = dto.partida ?: "",
            ubicacion           = dto.ubicacion ?: "",
            unidadesSaldo       = dto.disponible,
            codigoEmpresa       = "1",  // Si quieres dejarlo vacío, pon ""
            codigoCentro        = "",   // No lo devuelve el backend
            codigoAlmacen       = dto.codigoAlmacen ?: "",
            almacen             = dto.almacen ?: "",
            fechaCaducidad      = dto.fechaCaducidad ?: ""
        )
    }
}