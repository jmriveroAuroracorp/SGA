package com.example.sga.data

import com.example.sga.data.dto.stock.StockDisponibleDto
import com.example.sga.data.dto.traspasos.*
import com.example.sga.service.Traspasos.EstadoTraspasosService
import retrofit2.Call
import retrofit2.http.Body
import retrofit2.http.POST
import retrofit2.http.*
interface TraspasosApiService {

    /** Crear nuevo palet **/
    @POST("Palet")
    fun crearPalet(@Body dto: PaletCrearDto): Call<PaletDto>

    @GET("Palet/by-gs1/{codigoGS1}")
    fun obtenerPaletPorGS1(@Path("codigoGS1") codigoGS1: String): Call<PaletDto>

    @GET("Stock/articulo/disponible")
    fun obtenerStockDisponible(
        @Query("codigoEmpresa") empresaId: Short,
        @Query("codigoArticulo") codigoArticulo: String? = null,
        @Query("descripcion") descripcion: String? = null,
        @Query("partida") partida: String? = null,
        @Query("codigoAlmacen") codigoAlmacen: String? = null,
        @Query("codigoUbicacion") codigoUbicacion: String? = null
    ): Call<List<StockDisponibleDto>>

    /** Añadir línea a un palet **/
    @POST("Palet/{id}/lineas")
    fun añadirLineaPalet(
        @Path("id") idPalet: String,
        @Body dto: LineaPaletCrearDto
    ): Call<LineaPaletDto>

    /** Obtener todas las líneas de un palet **/
    @GET("Palet/{id}/lineas")
    fun obtenerLineasPalet(@Path("id") idPalet: String): Call<List<LineaPaletDto>>

    /** Eliminar una línea de palet **/
    @DELETE("Palet/lineas/{lineaId}")
    fun eliminarLineaPalet(
        @Path("lineaId") idLinea: String,
        @Query("usuarioId") usuarioId: Int
    ): Call<Void>

    @POST("Palet/{id}/cerrar-mobility")
    fun cerrarPalet(
        @Path("id") idPalet: String,
        @Body dto: CerrarPaletMobilityDto
    ): Call<TraspasoCreadoResponse>

    @POST("Palet/{id}/completar-traspaso")
    fun completarTraspaso(
        @Path("id") id: String,
        @Body dto: CompletarTraspasoDto
    ): Call<Void>

    @GET("Traspasos/palets-movibles")
    fun obtenerPaletsMovibles(): Call<List<PaletMovibleDto>>

    @POST("Palet/{id}/reabrir")
    fun reabrirPalet(
        @Path("id") idPalet: String,
        @Query("usuarioId") usuarioId: Int
    ): Call<Void>

    /** Buscar palets con filtros básicos (abiertos) **/
    @GET("Palet/filtros")
    fun buscarPalets(
        @Query("codigoEmpresa") empresa: String,
        @Query("usuarioApertura") usuario: String
    ): Call<List<PaletDto>>
    @GET("Palet/{id}")
    fun obtenerPalet(@Path("id") idPalet: String): Call<PaletDto>

    /** Obtener todos los tipos de palet **/
    @GET("Palet/maestros")
    fun obtenerTiposPalet(): Call<List<TipoPaletDto>>

    /** Obtener los estados posibles de palets **/
    @GET("Palet/estados")
    fun obtenerEstadosPalet(): Call<List<EstadoPaletDto>>

    /** Crear traspaso de artículo individual **/
    @POST("Traspasos/articulo")
    fun crearTraspasoArticulo(@Body dto: CrearTraspasoArticuloDto): Call<TraspasoArticuloDto>

    /** Finalizar traspaso de artículo individual **/
    @PUT("Traspasos/articulo/{id}/finalizar")
    fun finalizarTraspasoArticulo(
        @Path("id") id: String,
        @Body dto: FinalizarTraspasoArticuloDto
    ): Call<Void>

    @GET("Traspasos/pendiente-usuario")
    fun comprobarTraspasoPendiente(
        @Query("usuarioId") usuarioId: Int
    ): Call<TraspasoPendienteDto>

    @POST("Traspasos/mover-palet")
    fun moverPalet(@Body dto: MoverPaletDto): Call<MoverPaletResponse>

    @PUT("Traspasos/{id}/finalizar-palet")
    fun finalizarTraspasoPalet(
        @Path("id") traspasoId: String,
        @Body dto: FinalizarTraspasoPaletDto
    ): Call<Void>

    @PUT("traspasos/palet/{paletId}/finalizar")
    fun finalizarTraspasoPaletPorPaletId(
        @Path("paletId") paletId: String,
        @Body dto: FinalizarTraspasoPaletDto
    ): Call<Void>

    @GET("Traspaso/estado-usuario")
    fun obtenerEstadosTraspasosPorUsuario(@Query("usuarioId") usuarioId: Int): Call<List<EstadoTraspasosService.TraspasoEstadoDto>>

}