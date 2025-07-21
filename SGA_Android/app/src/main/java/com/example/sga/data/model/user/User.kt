package com.example.sga.data.model.user

data class User(
    val id: String,
    val name: String,
    val permisos: List<Short>, // ← aquí van directamente los códigos de MRH_CodigoAplicacion
    val codigosAlmacen: List<String>,
    val empresas: List<Empresa>,         // ← lista de empresas permitidas
    val codigoCentro: String,             // ← valor por defecto que viene en login
    val impresora: String? = null
)
