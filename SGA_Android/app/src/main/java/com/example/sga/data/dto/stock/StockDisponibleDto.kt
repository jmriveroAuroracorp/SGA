package com.example.sga.data.dto.stock

data class StockDisponibleDto(
    val codigoArticulo: String,
    val descripcion: String?,
    val partida: String?,
    val ubicacion: String?,
    val disponible: Double,
    val codigoAlmacen: String?,
    val almacen: String?,
    val fechaCaducidad: String?,
    val unidadSaldo: Double?,
    val reservado: Double?,
    val tipoStock: String?, // "Suelto" o "Paletizado"
    val paletId: String?, // ID del palet si es paletizado
    val codigoPalet: String?, // Código del palet si es paletizado
    val estadoPalet: String?, // "Abierto" o "Cerrado" si es paletizado
    val ordenTrabajoId: String?
)

