package com.example.sga.data.dto.almacenes

data class AlmacenesAutorizadosDto(
    val codigoCentro: String,
    val codigoEmpresa: Int,
    val codigosAlmacen: List<String>? = null
)