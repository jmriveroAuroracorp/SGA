package com.example.sga.data.network.interceptor

import com.example.sga.view.app.SessionViewModel
import okhttp3.Interceptor
import okhttp3.Response

class AuthInterceptor(
    private val sessionViewModel: SessionViewModel,
    private val onUnauthorized: () -> Unit
) : Interceptor {

    override fun intercept(chain: Interceptor.Chain): Response {
        val token = sessionViewModel.sessionToken
        val requestBuilder = chain.request().newBuilder()

        if (!token.isNullOrBlank()) {
            requestBuilder.addHeader("Authorization", "Bearer $token")
        }

        val response = chain.proceed(requestBuilder.build())

        if (response.code == 401) {
            onUnauthorized() // 👈 se lanza cuando el token ha sido rechazado
        }

        return response
    }
}