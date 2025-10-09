package com.example.sga.data


import android.util.Log
import com.example.sga.data.network.interceptor.AuthInterceptor
import com.example.sga.view.app.SessionViewModel
import okhttp3.OkHttpClient
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory
import retrofit2.converter.scalars.ScalarsConverterFactory
import okhttp3.logging.HttpLoggingInterceptor

object ApiManager {
    private const val BASE_URL = "http://10.0.0.175:5234/api/"
    //private const val BASE_URL = "http://localhost:5234/api/"
    // Retrofit con interceptor, creado de forma dinámica

    private fun createRetrofit(sessionViewModel: SessionViewModel, onUnauthorized: () -> Unit): Retrofit {
        val logging = HttpLoggingInterceptor { message ->
            Log.d("HTTP_LOG", message)
        }.apply {
            level = HttpLoggingInterceptor.Level.BODY
        }

        val client = OkHttpClient.Builder()
            .addInterceptor(logging) // ← Añade este interceptor antes que el tuyo
            .addInterceptor(AuthInterceptor(sessionViewModel, onUnauthorized))
            .build()

        return Retrofit.Builder()
            .baseUrl(BASE_URL)
            .client(client)
            .addConverterFactory(ScalarsConverterFactory.create()) // ← si usas String
            .addConverterFactory(GsonConverterFactory.create())
            .build()
    }

    // Apis accesibles tras login
    lateinit var pesajeApi: PesajeApiService
    lateinit var userApi: UserApiService
    lateinit var versionApi: VersionApiService
    lateinit var stockApi: StockApiService
    lateinit var almacenApi: AlmacenApiService
    lateinit var etiquetasApiService: EtiquetasApiService
    lateinit var traspasosApi: TraspasosApiService
    lateinit var conteosApi: ConteosApiService
    lateinit var ordenTraspasoApi: OrdenTraspasoApiService

    fun init(sessionViewModel: SessionViewModel, onUnauthorized: () -> Unit) {
        val retrofit = createRetrofit(sessionViewModel, onUnauthorized)
        pesajeApi = retrofit.create(PesajeApiService::class.java)
        userApi = retrofit.create(UserApiService::class.java)
        versionApi = retrofit.create(VersionApiService::class.java)
        stockApi = retrofit.create(StockApiService::class.java)
        almacenApi = retrofit.create(AlmacenApiService::class.java)
        etiquetasApiService = retrofit.create(EtiquetasApiService::class.java)
        traspasosApi = retrofit.create(TraspasosApiService::class.java)
        conteosApi = retrofit.create(ConteosApiService::class.java)
        ordenTraspasoApi = retrofit.create(OrdenTraspasoApiService::class.java)
    }
}


