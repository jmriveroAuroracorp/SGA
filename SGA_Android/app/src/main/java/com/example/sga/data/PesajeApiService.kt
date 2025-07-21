package com.example.sga.data

import com.example.sga.data.dto.pesajedto.*
import retrofit2.Call
import retrofit2.http.GET
import retrofit2.http.Path

interface PesajeApiService {

    @GET("pesaje/{ejercicio}/{serie}/{numero}")
    fun getPesaje(
        @Path("ejercicio") ejercicio: String,
        @Path("serie") serie: String,
        @Path("numero") numero: String
    ): Call<Pesajedto>

    @GET("pesaje/amasijo/{idAmasijo}")
    fun getPesajePorAmasijo(@Path("idAmasijo") id: String): Call<Pesajedto>

}
