package com.example.sga.data

import com.example.sga.data.dto.login.ConfiguracionUsuarioDto
import com.example.sga.data.dto.login.ConfiguracionUsuarioPatchDto
import com.example.sga.data.dto.login.DispositivoActivoDto
import com.example.sga.data.dto.login.DispositivoDto
import com.example.sga.data.dto.login.LogEventoDto
import com.example.sga.data.dto.login.LoginRequestDto
import com.example.sga.data.dto.login.LoginResponseDto

import retrofit2.Call
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.PATCH
import retrofit2.http.POST
import retrofit2.http.Path
import retrofit2.http.Query

interface UserApiService {
    @POST("Login")
    fun login(@Body request: LoginRequestDto): Call<LoginResponseDto>

    @POST("dispositivo/registrar")
    fun registrarDispositivo(@Body dto: DispositivoDto): Call<Void>

    @POST("LogEvento/crear")
    fun crearLogEvento(@Body dto: LogEventoDto): Call<Void>

    @POST("dispositivo/desactivar")
    fun desactivarDispositivo(@Body dto: DispositivoDto): Call<Void>

    @GET("dispositivo/activo")
    fun obtenerDispositivoActivo(@Query("idUsuario") idUsuario: Int): Call<DispositivoActivoDto>

    @GET("Usuarios/{id}")
    fun obtenerConfiguracionUsuario(@Path("id") id: Int): Call<ConfiguracionUsuarioDto>

    @PATCH("Usuarios/{id}")
    fun actualizarConfiguracionUsuario(
        @Path("id") idUsuario: Int,
        @Body configuracion: ConfiguracionUsuarioPatchDto
    ): Call<Void>

}