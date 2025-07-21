package com.example.sga.view.login
import android.content.Context
import android.os.Handler
import android.os.Looper
import android.provider.Settings
import android.util.Log
import com.example.sga.data.ApiManager
import com.example.sga.data.dto.login.ConfiguracionUsuarioDto
import com.example.sga.data.dto.login.DispositivoActivoDto
import com.example.sga.data.dto.login.DispositivoDto
import com.example.sga.data.dto.login.LogEventoDto
import com.example.sga.data.dto.login.LoginRequestDto
import com.example.sga.data.dto.login.LoginResponseDto
import com.example.sga.data.dto.traspasos.TraspasoPendienteDto
import com.example.sga.data.mapper.LoginMapper
import com.example.sga.view.app.SessionViewModel
import org.json.JSONObject
import retrofit2.Call
import retrofit2.Callback
import retrofit2.Response

class LoginLogic(
    private val loginViewModel: LoginViewModel,
    private val sessionViewModel: SessionViewModel,
    private val context: Context
) {

    fun onLoginClick(
        username: String,
        password: String,
        showError: (String) -> Unit,
        mostrarDialogoConfirmacion: (String, () -> Unit) -> Unit
    ) {
        when {
            username.isBlank() || password.isBlank() -> showError("Rellena todos los campos")
            username.any { !it.isDigit() } -> showError("El usuario debe ser numérico")
            else -> {
                showError("")
                comprobarSesionActiva(username.toInt(), password, showError, mostrarDialogoConfirmacion)
            }
        }
    }

    private fun hacerLogin(
        idUsuario: Int,
        password: String,
        showError: (String) -> Unit
    ) {
        val idDispositivo = Settings.Secure.getString(context.contentResolver, Settings.Secure.ANDROID_ID)

        val request = LoginRequestDto(
            operario = idUsuario,
            contraseña = password,
            idDispositivo = idDispositivo,
            tipoDispositivo = "Android"
        )

        ApiManager.userApi.login(request).enqueue(object : Callback<LoginResponseDto> {
            override fun onResponse(call: Call<LoginResponseDto>, response: Response<LoginResponseDto>) {
                if (response.isSuccessful) {
                    val dto = response.body()
                    if (dto != null) {
                        val user = LoginMapper.LoginMapper.fromDto(dto)
                        loginViewModel.setUser(user)
                        loginViewModel.setError(null)

                        sessionViewModel.sessionToken = dto.token
                        sessionViewModel.actualizarTimestamp()
                        sessionViewModel.setContraseña(password)

                        // Llamada para cargar la empresa por defecto
                        ApiManager.userApi.obtenerConfiguracionUsuario(dto.operario)
                            .enqueue(object : Callback<ConfiguracionUsuarioDto> {
                                override fun onResponse(
                                    call: Call<ConfiguracionUsuarioDto>,
                                    response: Response<ConfiguracionUsuarioDto>
                                ) {
                                    if (response.isSuccessful) {
                                        val config = response.body()
                                        val empresaPorDefecto = user.empresas.find {
                                            it.codigo.toString() == config?.idEmpresa
                                        }
                                        empresaPorDefecto?.let {
                                            sessionViewModel.setEmpresaSeleccionada(it)
                                        }
                                        // ✅ Añadir impresora seleccionada
                                        config?.impresora?.let {
                                            sessionViewModel.actualizarImpresora(it)
                                        }
                                    }
                                }

                                override fun onFailure(call: Call<ConfiguracionUsuarioDto>, t: Throwable) {
                                    Log.e("LoginLogic", "⚠️ No se pudo cargar configuración del usuario: ${t.localizedMessage}")
                                }
                            })

                        // Inicializar y manejar caducidad
                        ApiManager.init(sessionViewModel) {
                            sessionViewModel.mostrarMensajeCaducidad()
                            Handler(Looper.getMainLooper()).postDelayed({
                                sessionViewModel.clearSession()
                                Log.w("SGA", "⚠️ Sesión cerrada desde otro dispositivo.")
                            }, 3000)
                        }

                        crearLogEvento(user.id.toInt(), request.idDispositivo)
                        comprobarTraspasoPendiente(
                            usuarioId = user.id.toInt(),
                            onResult = { dto ->
                                if (dto != null && dto.codigoEstado.equals("PENDIENTE", true)) {
                                    loginViewModel.setTraspasoEsDePalet(dto.tipoTraspaso.equals("PALET", true))
                                    loginViewModel.emitirDestino(true)
                                } else {
                                    loginViewModel.emitirDestino(false)
                                }
                            },
                            onError = { msg -> showError(msg) }
                        )
                    }
                    else {
                        showError("Respuesta vacía del servidor")
                    }
                } else {
                    val errorBody = response.errorBody()?.string()
                    val mensajeError = extraerMensajeDesdeJson(errorBody)
                    showError(mensajeError ?: "Credenciales inválidas")
                }
            }

            override fun onFailure(call: Call<LoginResponseDto>, t: Throwable) {
                showError("Error de conexión: ${t.localizedMessage}")
            }

            private fun extraerMensajeDesdeJson(json: String?): String? {
                if (json.isNullOrBlank()) return null
                return try {
                    val jsonObject = JSONObject(json)
                    jsonObject.optString("message", null)
                } catch (e: Exception) {
                    json
                }
            }
        })
    }

    private fun crearLogEvento(idUsuario: Int, idDispositivo: String) {
        val log = LogEventoDto(IdUsuario = idUsuario, IdDispositivo = idDispositivo)
        ApiManager.userApi.crearLogEvento(log).enqueue(object : Callback<Void> {
            override fun onResponse(call: Call<Void>, response: Response<Void>) {
                Log.d("LoginLogic", ">>> LogEvento creado. Código: ${response.code()}")
            }

            override fun onFailure(call: Call<Void>, t: Throwable) {
                Log.e("LoginLogic", ">>> Error creando LogEvento: ${t.localizedMessage}")
            }
        })
    }

    private fun comprobarSesionActiva(
        idUsuario: Int,
        password: String,
        showError: (String) -> Unit,
        mostrarDialogo: (String, () -> Unit) -> Unit
    ) {
        ApiManager.userApi.obtenerDispositivoActivo(idUsuario).enqueue(object : Callback<DispositivoActivoDto> {
            override fun onResponse(call: Call<DispositivoActivoDto>, response: Response<DispositivoActivoDto>) {
                if (response.isSuccessful && response.body() != null) {
                    val dispositivo = response.body()!!
                    val dto = DispositivoDto(
                        id = dispositivo.id,
                        tipo = dispositivo.tipo,
                        idUsuario = idUsuario
                    )
                    sessionViewModel.setDispositivo(dto)

                    val mensaje = "Ya hay una sesión activa en el dispositivo: ${dispositivo.tipo}.\n¿Quieres cerrarla?"
                    mostrarDialogo(mensaje) {
                        hacerLogin(idUsuario, password, showError)
                    }
                } else {
                    hacerLogin(idUsuario, password, showError)
                }
            }

            override fun onFailure(call: Call<DispositivoActivoDto>, t: Throwable) {
                showError("Error consultando sesión activa: ${t.localizedMessage}")
            }
        })
    }
    fun comprobarTraspasoPendiente(
        usuarioId: Int,
        onResult: (TraspasoPendienteDto?) -> Unit,
        onError: (String) -> Unit
    ) {
        ApiManager.traspasosApi
            .comprobarTraspasoPendiente(usuarioId)
            .enqueue(object : Callback<TraspasoPendienteDto> {
                override fun onResponse(
                    call: Call<TraspasoPendienteDto>,
                    response: Response<TraspasoPendienteDto>
                ) {
                    if (response.isSuccessful) {
                        onResult(response.body())  // ✅ Devuelves el DTO completo
                    } else if (response.code() == 404) {
                        onResult(null)            // ✅ Sin traspaso → null
                    } else {
                        onError("HTTP ${response.code()}")
                    }
                }

                override fun onFailure(call: Call<TraspasoPendienteDto>, t: Throwable) {
                    onError("Red: ${t.message}")
                }
            })
    }


}


