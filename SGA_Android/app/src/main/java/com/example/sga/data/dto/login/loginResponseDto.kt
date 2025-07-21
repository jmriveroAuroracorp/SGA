package com.example.sga.data.dto.login

data class LoginResponseDto(
    val operario: Int,
    val nombreOperario: String,
    val codigosAplicacion: List<Short>,
    val codigosAlmacen: List<String>,
    val empresas: List<EmpresaDto>,
    val token: String,
    val codigoCentro: String
)
