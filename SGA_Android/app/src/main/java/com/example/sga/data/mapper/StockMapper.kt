package com.example.sga.data.mapper

import com.example.sga.data.dto.stock.StockDto
import com.example.sga.data.model.stock.Stock

object StockMapper {
    fun fromDto(dto: StockDto): Stock {
        return Stock(
            codigoEmpresa = dto.codigoEmpresa,
            codigoArticulo = dto.codigoArticulo,
            descripcionArticulo = dto.descripcionArticulo,
            codigoAlmacen = dto.codigoAlmacen,
            almacen = dto.almacen,
            ubicacion = dto.ubicacion,
            partida = dto.partida,
            fechaCaducidad = dto.fechaCaducidad,
            unidadesSaldo = dto.unidadSaldo,
            reservado = dto.reservado,
            disponible = dto.disponible,
            tipoStock = dto.tipoStock,
            paletId = dto.paletId,
            codigoPalet = dto.codigoPalet,
            estadoPalet = dto.estadoPalet
        )
    }
}