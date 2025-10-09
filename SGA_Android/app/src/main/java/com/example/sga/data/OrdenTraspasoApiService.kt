package com.example.sga.data

import com.example.sga.data.dto.ordenes.*
import retrofit2.Response
import retrofit2.http.*

interface OrdenTraspasoApiService {
    
    /** 1. Listar órdenes por operario */
    @GET("OrdenTraspaso/operario/{idOperario}")
    suspend fun getOrdenesPorOperario(
        @Path("idOperario") idOperario: Int,
        @Query("codigoEmpresa") codigoEmpresa: Int
    ): Response<List<OrdenTraspasoDto>>
    
    /** 2. Iniciar orden */
    @POST("OrdenTraspaso/{id}/iniciar/{idOperario}")
    suspend fun iniciarOrden(
        @Path("id") idOrden: String,
        @Path("idOperario") idOperario: Int
    ): Response<OrdenTraspasoDto>
    
    /** 3. Consultar stock de línea */
    @GET("OrdenTraspaso/linea/{idLinea}/stock")
    suspend fun consultarStockLinea(@Path("idLinea") idLinea: String): Response<List<StockLineaTraspasoDto>>
    
    /** 4. Actualizar línea */
    @PUT("OrdenTraspaso/linea/{id}")
    suspend fun actualizarLinea(
        @Path("id") idLinea: String,
        @Body dto: ActualizarLineaOrdenTraspasoDto
    ): Response<ActualizarLineaResponseDto>
    
    /** 5. Verificar palets pendientes */
    @GET("OrdenTraspaso/{ordenId}/palets-pendientes")
    suspend fun verificarPaletsPendientes(@Path("ordenId") ordenId: String): Response<List<PaletPendienteDto>>
    
    /** 6. Ubicar palet */
    @PUT("OrdenTraspaso/{ordenId}/palet/{paletDestino}/ubicar")
    suspend fun ubicarPalet(
        @Path("ordenId") ordenId: String,
        @Path("paletDestino") paletDestino: String,
        @Body dto: UbicarPaletDto
    ): Response<Unit>
    
    // Endpoints adicionales para compatibilidad con la implementación actual
    @GET("OrdenTraspaso/{id}")
    suspend fun getOrdenTraspaso(@Path("id") id: String): Response<OrdenTraspasoDto>
    
    @PUT("OrdenTraspaso/linea/{idLinea}/estado")
    suspend fun actualizarEstadoLinea(
        @Path("idLinea") idLinea: String,
        @Body dto: ActualizarEstadoLineaDto
    ): Response<Unit>
    
    @GET("OrdenTraspaso/stock/{codigoEmpresa}/{codigoArticulo}/{idOperario}")
    suspend fun getStockDisponible(
        @Path("codigoEmpresa") codigoEmpresa: Int,
        @Path("codigoArticulo") codigoArticulo: String,
        @Path("idOperario") idOperario: Int
    ): Response<List<StockDisponibleDto>>
    
    @PUT("OrdenTraspaso/linea/{idLinea}/completa")
    suspend fun actualizarLineaCompleta(
        @Path("idLinea") idLinea: String,
        @Body dto: ActualizarLineaOrdenTraspasoDto
    ): Response<Unit>
    
    @PUT("OrdenTraspaso/{id}")
    suspend fun actualizarOrdenTraspaso(
        @Path("id") id: String,
        @Body dto: ActualizarOrdenTraspasoDto
    ): Response<Unit>
    
    /** 7. Desbloquear línea (solo supervisores) */
    @POST("OrdenTraspaso/linea/{idLinea}/desbloquear")
    suspend fun desbloquearLinea(@Path("idLinea") idLinea: String): Response<Unit>
    
    /** 8. Ajustar inventario de línea */
    @POST("OrdenTraspaso/linea/{idLinea}/ajuste")
    suspend fun ajustarLineaOrdenTraspaso(
        @Path("idLinea") idLinea: String,
        @Body dto: AjusteLineaOrdenTraspasoDto
    ): Response<AjusteLineaResponseDto>
    
    /** 9. Actualizar ID de traspaso en línea */
    @PUT("OrdenTraspaso/linea/{idLinea}")
    suspend fun actualizarIdTraspaso(
        @Path("idLinea") idLinea: String,
        @Body dto: ActualizarIdTraspasoDto
    ): Response<Unit>
}
