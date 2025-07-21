package com.example.sga.data.dto.pesajedto

data class Amasijodto(
    val amasijo: String,
    val totalPesado: Double,
    val componentes: List<Componentedto>
)