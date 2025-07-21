package com.example.sga.data

import com.example.sga.data.dto.stock.ArticuloDto
import retrofit2.Call
import com.example.sga.data.dto.stock.StockDto
import retrofit2.http.GET
import retrofit2.http.Query

interface StockApiService {

    @GET("Stock/consulta-stock")
    fun consultarStock(
        @Query("codigoEmpresa") codigoEmpresa: Int,
        @Query("codigoUbicacion") codigoUbicacion: String? = null,
        @Query("codigoAlmacen") codigoAlmacen: String? = null,
        @Query("codigoArticulo") codigoArticulo: String? = null,
        @Query("codigoCentro") codigoCentro: String? = null,
        @Query("almacen") almacen: String? = null,
        @Query("partida") partida: String? = null
    ): Call<List<StockDto>>

    @GET("Stock/buscar-articulo")
    fun buscarArticulo(
        /* obligatorios según el backend */
        @Query("codigoEmpresa")   codigoEmpresa   : Short,
        /* criterio de búsqueda (uno de los tres) */
        @Query("descripcion")     descripcion     : String?  = null,
        @Query("codigoAlternativo") codigoAlternativo : String?  = null,
        @Query("codigoArticulo")  codigoArticulo  : String?  = null,
        /* mismos filtros que consulta-stock */
        @Query("codigoUbicacion") codigoUbicacion : String?  = null,
        @Query("codigoAlmacen")   codigoAlmacen   : String?  = null,
        @Query("codigoCentro")    codigoCentro    : String?  = null,
        @Query("almacen")         almacen         : String?  = null,
        @Query("partida")         partida         : String?  = null
    ): Call<List<ArticuloDto>>
}