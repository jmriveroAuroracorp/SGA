package com.example.sga.data.dto.stock

data class StockDto(
    val codigoEmpresa: String,
    val codigoArticulo: String,
    val descripcionArticulo: String?,
    val codigoAlmacen: String,
    val almacen: String,
    val ubicacion: String,
    val partida: String,
    val fechaCaducidad: String?,
    val unidadSaldo: Double,
    val reservado: Double,
    val disponible: Double,
    val tipoStock: String,
    val paletId: String?,
    val codigoPalet: String?,
    val estadoPalet: String?,
    val ordenTrabajoId: String?,
    // 🔷 NUEVO: Campos de bloqueo de calidad
    // Gson hace matching automático case-insensitive entre PascalCase (API) y camelCase (Kotlin)
    val isBloqueadoCalidad: Boolean? = false,
    val motivoBloqueoCalidad: String? = null,
    val fechaBloqueoCalidad: String? = null,
    val tipoBloqueoCalidad: String? = "TOTAL"
)