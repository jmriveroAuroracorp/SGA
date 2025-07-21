package com.example.sga.data

import com.example.sga.data.dto.version.VersionAppDto
import okhttp3.ResponseBody
import retrofit2.http.GET
import retrofit2.http.Streaming

interface VersionApiService {
    @GET("version")
    suspend fun getUltimaVersion(): VersionAppDto

    @GET("version/descargar")
    @Streaming
    suspend fun descargarAPK(): ResponseBody
}
