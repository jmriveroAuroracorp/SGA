package com.example.sga.data.mapper

import com.example.sga.data.dto.login.LoginResponseDto
import com.example.sga.data.model.user.Empresa
import com.example.sga.data.model.user.User

class LoginMapper {
    object LoginMapper {
        fun fromDto(dto: LoginResponseDto): User {
            val empresas = dto.empresas.map {
                Empresa(codigo = it.codigo, nombre = it.nombre)
            }

            return User(
                id = dto.operario.toString(),
                name = dto.nombreOperario,
                permisos = dto.codigosAplicacion,
                codigosAlmacen = dto.codigosAlmacen,
                empresas = empresas,
                codigoCentro = dto.codigoCentro,
                mrhLimiteInventarioEuros = dto.mrhLimiteInventarioEuros,
                mrhLimiteInventarioUnidades = dto.mrhLimiteInventarioUnidades
            )
        }
    }
}