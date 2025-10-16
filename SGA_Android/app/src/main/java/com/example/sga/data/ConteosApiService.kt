package com.example.sga.data

import com.example.sga.data.dto.conteos.*
import retrofit2.http.*

interface ConteosApiService {
    
    // Crear orden de conteo
    @POST("conteos/ordenes")
    suspend fun crearOrden(@Body crearOrdenDto: CrearOrdenDto): OrdenConteoDto
    
    // Listar órdenes
    @GET("conteos/ordenes")
    suspend fun listarOrdenes(
        @Query("codigoOperario") codigoOperario: String? = null,
        @Query("estado") estado: String? = null
    ): List<OrdenConteoDto>
    
    // Obtener orden por GUID
    @GET("conteos/ordenes/{guidID}")
    suspend fun obtenerOrden(@Path("guidID") guidID: String): OrdenConteoDto
    
    // Iniciar orden
    @POST("conteos/ordenes/{guidID}/start")
    suspend fun iniciarOrden(
        @Path("guidID") guidID: String,
        @Query("codigoOperario") codigoOperario: String
    ): OrdenConteoDto
    
    // Asignar operario
    @POST("conteos/ordenes/{guidID}/asignar")
    suspend fun asignarOperario(
        @Path("guidID") guidID: String,
        @Body asignarOperarioDto: AsignarOperarioDto
    ): OrdenConteoDto
    
    // Obtener lecturas pendientes
    @GET("conteos/ordenes/{guidID}/lecturas-pendientes")
    suspend fun obtenerLecturasPendientes(
        @Path("guidID") guidID: String,
        @Query("codigoOperario") codigoOperario: String? = null
    ): List<LecturaConteoDto>
    
    // Registrar lectura
    @POST("conteos/ordenes/{guidID}/lecturas")
    suspend fun registrarLectura(
        @Path("guidID") guidID: String,
        @Body lecturaDto: LecturaDto
    ): LecturaConteoDto
    
    // Cerrar orden
    @POST("conteos/ordenes/{guidID}/cerrar")
    suspend fun cerrarOrden(@Path("guidID") guidID: String): CerrarOrdenResponseDto
    
    // Obtener resultados de conteo
    @GET("conteos/ordenes/{guidID}/resultados")
    suspend fun obtenerResultados(@Path("guidID") guidID: String): List<ResultadoConteoDto>
    
    // Obtener palets disponibles para una ubicación y artículo
    @GET("conteos/palets-disponibles")
    suspend fun obtenerPaletsDisponibles(
        @Query("codigoAlmacen") codigoAlmacen: String,
        @Query("ubicacion") ubicacion: String? = null,
        @Query("codigoArticulo") codigoArticulo: String? = null,
        @Query("lote") lote: String? = null,
        @Query("fechaCaducidad") fechaCaducidad: String? = null
    ): List<PaletDisponibleDto>
}
