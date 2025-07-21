package com.example.sga.data

import com.example.sga.data.dto.almacenes.AlmacenDto
import com.example.sga.data.dto.almacenes.AlmacenesAutorizadosDto
import retrofit2.Call
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.Query

interface AlmacenApiService {
    @GET("Almacen")
    fun obtenerAlmacenes(
        @Query("codigoCentro") codigoCentro: String
    ): Call<String>

    @POST("Almacen/Combos/Autorizados")
    fun obtenerAlmacenesAutorizados(
        @Body body: AlmacenesAutorizadosDto
    ): Call<List<AlmacenDto>>
}