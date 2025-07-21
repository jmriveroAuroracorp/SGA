package com.example.sga.data

import com.example.sga.data.dto.etiquetas.AlergenosDto
import com.example.sga.data.dto.etiquetas.ImpresoraDto
import com.example.sga.data.dto.etiquetas.LogImpresionDto
import com.example.sga.data.dto.stock.ArticuloDto
import retrofit2.Call
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.Query

interface EtiquetasApiService {

    @GET("Stock/buscar-articulo")
    fun buscarArticulo(
        @Query("codigoEmpresa") codigoEmpresa: Short,
        @Query("descripcion") descripcion: String? = null,
        @Query("codigoAlternativo") codigoAlternativo: String? = null,
        @Query("codigoArticulo") codigoArticulo: String? = null,
        @Query("codigoUbicacion") codigoUbicacion: String? = null,
        @Query("codigoAlmacen") codigoAlmacen: String? = null,
        @Query("codigoCentro") codigoCentro: String? = null,
        @Query("almacen") almacen: String? = null,
        @Query("partida") partida: String? = null
    ): Call<List<ArticuloDto>>

    @GET("Stock/articulo/alergenos")
    fun getAlergenos(
        @Query("codigoEmpresa") codigoEmpresa: Short,
        @Query("codigoArticulo") codigoArticulo: String
    ): Call<AlergenosDto>

    @GET("Impresion/impresoras")
    fun getImpresoras(): Call<List<ImpresoraDto>>

    @POST("log")
    fun insertarLogImpresion(
        @Body dto: LogImpresionDto
    ): Call<LogImpresionDto>

}
