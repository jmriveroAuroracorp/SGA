package com.example.sga.view.Almacen


import android.util.Log
import com.example.sga.data.ApiManager
import com.example.sga.data.dto.almacenes.AlmacenDto
import com.example.sga.data.dto.almacenes.AlmacenesAutorizadosDto
import com.example.sga.view.app.SessionViewModel
import kotlin.text.toShortOrNull
import retrofit2.Call
import retrofit2.Callback
import retrofit2.Response

class AlmacenLogic(
    private val viewModel: AlmacenViewModel,
    private val session: SessionViewModel
) {

    /** Carga y filtra la lista de almacenes permitidos */
    /*fun cargarAlmacenes() {
        val centro = session.user.value?.codigoCentro ?: return
        viewModel.setCargando(true)

        ApiManager.almacenApi.obtenerAlmacenes(centro)
            .enqueue(object : Callback<String> {                 // ← String
                override fun onResponse(
                    call: Call<String>,
                    response: Response<String>
                ) {
                    if (response.isSuccessful) {
                        val raw = response.body() ?: "[]"

                        // Parseamos la cadena "[\"101\",\"102\"]" a List<String>
                        val todos: List<String> = Gson().fromJson(
                            raw,
                            object : com.google.gson.reflect.TypeToken<List<String>>() {}.type
                        )

                        val permitidos = filtrarAlmacenes(todos)
                        viewModel.setLista(permitidos.sorted())
                        viewModel.setError(null)
                    } else {
                        viewModel.setError("Error ${response.code()}")
                    }
                    viewModel.setCargando(false)
                }

                override fun onFailure(call: Call<String>, t: Throwable) {
                    viewModel.setError("Error conexión: ${t.localizedMessage}")
                    viewModel.setCargando(false)
                    Log.e("AlmacenLogic", "❌", t)
                }
            })
    }*/
    fun cargarAlmacenes() {
        val user   = session.user.value ?: return
        val centro = user.codigoCentro   ?: return

        // ‼️ El primer elemento de la lista empresas es un objeto (EmpresaDto), no un String
        val codigoEmpresa = user.empresas?.firstOrNull()?.codigo ?: return    // Int

        val codigosAlmacen = user.codigosAlmacen ?: emptyList()

        viewModel.setCargando(true)

        val body = AlmacenesAutorizadosDto(
            codigoCentro   = centro,
            codigoEmpresa  = codigoEmpresa,   // ya es Int → coincide con el DTO
            codigosAlmacen = codigosAlmacen
        )
        Log.d("ALMACEN", "→ body=$body")

        ApiManager.almacenApi.obtenerAlmacenesAutorizados(body)
            .enqueue(object : Callback<List<AlmacenDto>> {
                override fun onResponse(
                    call: Call<List<AlmacenDto>>,
                    response: Response<List<AlmacenDto>>
                ) {
                    Log.d("ALMACEN", "← code=${response.code()} body=${response.body()}")
                    if (response.isSuccessful) {
                        val lista = response.body().orEmpty()
                        
                        // 🔍 LOGS DE DEBUG DETALLADOS
                        Log.d("ALMACEN_DEBUG", """
                            🏢 Centro usuario: ${user.codigoCentro}
                            👤 Códigos almacén específicos: ${user.codigosAlmacen}
                            📦 Total almacenes recibidos: ${lista.size}
                            
                            📋 DETALLE DE ALMACENES:
                            ${lista.joinToString("\n") { almacen ->
                                "  • ${almacen.codigoAlmacen} - ${almacen.nombreAlmacen} | esDelCentro: ${almacen.esDelCentro} | empresa: ${almacen.codigoEmpresa}"
                            }}
                            ═══════════════════════════════
                        """.trimIndent())
                        
                        // Ordenamos por "código + nombre" para que aparezcan agrupados
                        viewModel.setLista(lista.sortedBy { it.codigoAlmacen + it.nombreAlmacen })
                        viewModel.setError(null)
                    } else {
                        viewModel.setError("Error ${response.code()}")
                    }
                    viewModel.setCargando(false)
                }

                override fun onFailure(call: Call<List<AlmacenDto>>, t: Throwable) {
                    viewModel.setError("Error conexión: ${t.localizedMessage}")
                    viewModel.setCargando(false)
                    Log.e("AlmacenLogic", "❌", t)
                }
            })
    }

    /** Regla: en user.codigosAlmacen  OR  contiene codigoCentro */
    private fun filtrarAlmacenes(todos: List<String>): List<String> {
        val permisos = session.user.value?.codigosAlmacen ?: emptyList()
        val centro   = session.user.value?.codigoCentro ?: ""
        return (permisos + todos.filter { it.contains(centro) })
            .distinct()
            .sorted()

    }

    fun onAlmacenSeleccionado(codigo: String) {
        viewModel.setSeleccionado(codigo)
    }
}