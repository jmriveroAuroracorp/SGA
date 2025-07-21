package com.example.sga.data.model.user

data class LoginResponse(
    val operario: Int,
    val nombreOperario: String,
    val codigosAplicacion: List<Short>
)


