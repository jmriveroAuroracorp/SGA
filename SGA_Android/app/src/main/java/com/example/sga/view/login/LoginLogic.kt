package com.example.sga.view.login
import android.app.Activity
import android.content.Context
import android.os.Build
import android.os.Handler
import android.os.Looper
import android.provider.Settings
import android.util.Log
import androidx.core.app.ActivityCompat
import com.example.sga.data.ApiManager
import com.example.sga.data.dto.login.ConfiguracionUsuarioDto
import com.example.sga.data.dto.login.DispositivoActivoDto
import com.example.sga.data.dto.login.DispositivoDto
import com.example.sga.data.dto.login.LogEventoDto
import com.example.sga.data.dto.login.LoginRequestDto
import com.example.sga.data.dto.login.LoginResponseDto
import com.example.sga.data.dto.traspasos.TraspasoPendienteDto
import com.example.sga.data.dto.traspasos.PaletDto
import com.example.sga.data.mapper.LoginMapper
import com.example.sga.service.Traspasos.EstadoTraspasosService
import com.example.sga.view.app.SessionViewModel
import com.example.sga.view.traspasos.PaletFlujoStore
import org.json.JSONObject
import retrofit2.Call
import retrofit2.Callback
import retrofit2.Response
import kotlinx.coroutines.launch
import kotlinx.coroutines.CoroutineScope

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
        Log.d("LoginLogic", ">>> onLoginClick llamado - Usuario: '$username', Password: '${password.take(2)}***'")
        when {
            username.isBlank() || password.isBlank() -> {
                Log.d("LoginLogic", ">>> Campos vacíos")
                showError("Rellena todos los campos")
            }
            username.any { !it.isDigit() } -> {
                Log.d("LoginLogic", ">>> Usuario no numérico")
                showError("El usuario debe ser numérico")
            }
            else -> {
                Log.d("LoginLogic", ">>> Validación OK, verificando sesión activa...")
                loginViewModel.setLoading(true) // Activar carga
                comprobarSesionActivaAntesLogin(username.toInt(), password, showError, mostrarDialogoConfirmacion)
            }
        }
    }

    private fun hacerLogin(
        idUsuario: Int,
        password: String,
        showError: (String) -> Unit,
        mostrarDialogoConfirmacion: (String, () -> Unit) -> Unit = { _, _ -> }
    ) {
        val idDispositivo = Settings.Secure.getString(context.contentResolver, Settings.Secure.ANDROID_ID)

        val request = LoginRequestDto(
            operario = idUsuario,
            contraseña = password,
            idDispositivo = idDispositivo,
            tipoDispositivo = "Android"
        )
        Log.d("LoginLogic", ">>> Haciendo login con usuario=$idUsuario, pass=$password")

        ApiManager.userApi.login(request).enqueue(object : Callback<LoginResponseDto> {
            override fun onResponse(call: Call<LoginResponseDto>, response: Response<LoginResponseDto>) {
                Log.d("LoginLogic", ">>> Respuesta recibida - Código: ${response.code()}, Exitoso: ${response.isSuccessful}")
                if (response.isSuccessful) {
                    val dto = response.body()
                    if (dto != null) {
                        val user = LoginMapper.LoginMapper.fromDto(dto)
                        loginViewModel.setUser(user)
                        loginViewModel.setError(null)
                        loginViewModel.setLoading(false) // Desactivar carga

                        sessionViewModel.sessionToken = dto.token
                        sessionViewModel.actualizarTimestamp()
                        sessionViewModel.setContraseña(password)
                        
                        // Establecer el dispositivo después del login exitoso
                        val dispositivoDto = DispositivoDto(
                            id = request.idDispositivo,
                            tipo = request.tipoDispositivo,
                            idUsuario = idUsuario
                        )
                        sessionViewModel.setDispositivo(dispositivoDto)

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

                        // Inicializar ApiManager sin callback de sesión caducada
                        ApiManager.init(sessionViewModel) {
                            // Solo mostrar mensaje si no es inmediatamente después del login
                            val timestamp = sessionViewModel.tokenTimestamp.value ?: 0L
                            if (System.currentTimeMillis() - timestamp > 5000) {
                                sessionViewModel.mostrarMensajeCaducidad()
                                Handler(Looper.getMainLooper()).postDelayed({
                                    sessionViewModel.clearSession()
                                    Log.w("SGA", "⚠️ Sesión cerrada desde otro dispositivo.")
                                }, 3000)
                            }
                        }

                        PaletFlujoStore.init(context)
                        val saved = PaletFlujoStore.load()
                        if (saved != null && saved.second == user.id.toInt()) {
                            // ir directo a traspasos, no comprobar traspasos pendientes
                            loginViewModel.setTraspasoEsDePalet(false)
                            loginViewModel.setTraspasoDirectoDesdePaletCerrado(false)
                            loginViewModel.emitirDestino(true) // true → Traspasos
                            return
                        }

                        crearLogEvento(user.id.toInt(), request.idDispositivo)
                        EstadoTraspasosService.iniciar(user.id.toInt(), context)

                        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                            ActivityCompat.requestPermissions(
                                context as Activity,
                                arrayOf(android.Manifest.permission.POST_NOTIFICATIONS),
                                1001
                            )
                        }

                        comprobarTraspasoPendiente(
                            usuarioId = user.id.toInt(),
                            onResult = { lista ->
                                if (lista.any { it.codigoEstado.equals("PENDIENTE", true) }) {
                                    // Guarda todos los traspasos en el ViewModel si no lo hacías ya
                                    loginViewModel.setListaTraspasosPendientes(lista)

                                    // Verificar si algún traspaso pendiente tiene orden asociada
                                    verificarTraspasosConOrden(
                                        lista = lista,
                                        onTieneOrden = {
                                            // Hay traspasos con orden asociada → Navegar a OrdenTraspasoProcesoScreen
                                            loginViewModel.setTraspasoPendienteDeOrden(true)
                                            loginViewModel.emitirDestino(true)
                                        },
                                        onSinOrden = {
                                            // No hay traspasos con orden → Navegar a TraspasosScreen
                                            val hayAlgunoDePalet = lista.any { it.tipoTraspaso.equals("PALET", true) }
                                            val hayAlgunoDirecto = lista.any { it.paletCerrado }

                                            loginViewModel.setTraspasoEsDePalet(hayAlgunoDePalet)
                                            loginViewModel.setTraspasoDirectoDesdePaletCerrado(hayAlgunoDirecto)
                                            loginViewModel.emitirDestino(true)
                                        },
                                        onError = { error ->
                                            showError("Error verificando órdenes: $error")
                                        }
                                    )
                                } else {
                                    loginViewModel.emitirDestino(false)
                                }
                            },
                            onError = { msg -> showError(msg) }
                        )
                    }
                    else {
                        showError("Respuesta vacía del servidor")
                        loginViewModel.setLoading(false) // Desactivar carga
                    }
                } else {
                    val errorBody = response.errorBody()?.string()
                    Log.d("LoginLogic", ">>> Error en login - Código: ${response.code()}, Body: $errorBody")
                    val mensajeError = extraerMensajeDesdeJson(errorBody)
                    
                    // Mostrar mensaje específico según el código de error
                    val mensajeFinal = when (response.code()) {
                        401 -> "Contraseña incorrecta"
                        404 -> "Usuario no encontrado"
                        403 -> "Usuario bloqueado o sin permisos"
                        else -> mensajeError ?: "Error de autenticación"
                    }
                    
                    Log.d("LoginLogic", ">>> Mostrando error: $mensajeFinal")
                    showError(mensajeFinal)
                    loginViewModel.setLoading(false) // Desactivar carga
                    Log.d("LoginLogic", ">>> showError llamado, isLoading = false")
                }
            }

            override fun onFailure(call: Call<LoginResponseDto>, t: Throwable) {
                Log.e("LoginLogic", ">>> Error de conexión: ${t.localizedMessage}", t)
                showError("Error de conexión: ${t.localizedMessage}")
                loginViewModel.setLoading(false) // Desactivar carga
            }

            private fun extraerMensajeDesdeJson(json: String?): String? {
                if (json.isNullOrBlank()) return null
                return try {
                    val jsonObject = JSONObject(json)
                    // Buscar diferentes campos que pueden contener el mensaje
                    jsonObject.optString("message", null) 
                        ?: jsonObject.optString("error", null)
                        ?: jsonObject.optString("detail", null)
                        ?: jsonObject.optString("Message", null) // Por si viene con mayúscula
                } catch (e: Exception) {
                    // Si no es JSON válido, devolver el texto tal como viene
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

    private fun comprobarSesionActivaAntesLogin(
        idUsuario: Int,
        password: String,
        showError: (String) -> Unit,
        mostrarDialogo: (String, () -> Unit) -> Unit
    ) {
        Log.d("LoginLogic", ">>> Comprobando sesión activa ANTES del login para usuario: $idUsuario")
        ApiManager.userApi.obtenerDispositivoActivo(idUsuario).enqueue(object : Callback<DispositivoActivoDto> {
            override fun onResponse(call: Call<DispositivoActivoDto>, response: Response<DispositivoActivoDto>) {
                Log.d("LoginLogic", ">>> Respuesta sesión activa - Código: ${response.code()}, Body: ${response.body()}")
                if (response.isSuccessful && response.body() != null) {
                    val dispositivo = response.body()!!
                    Log.d("LoginLogic", ">>> Sesión activa encontrada: ${dispositivo.tipo}")
                    val dto = DispositivoDto(
                        id = dispositivo.id,
                        tipo = dispositivo.tipo,
                        idUsuario = idUsuario
                    )
                    sessionViewModel.setDispositivo(dto)

                    val mensaje = "Ya hay una sesión activa en el dispositivo: ${dispositivo.tipo}.\n¿Quieres cerrarla?"
                    mostrarDialogo(mensaje) {
                        Log.d("LoginLogic", ">>> Usuario confirmó cerrar sesión anterior, procediendo con login")
                        hacerLogin(idUsuario, password, showError, mostrarDialogo)
                    }
                } else {
                    Log.d("LoginLogic", ">>> No hay sesión activa, procediendo con login")
                    hacerLogin(idUsuario, password, showError, mostrarDialogo)
                }
            }

            override fun onFailure(call: Call<DispositivoActivoDto>, t: Throwable) {
                Log.e("LoginLogic", ">>> Error consultando sesión activa: ${t.localizedMessage}", t)
                // Si hay error consultando sesión activa, proceder con login
                hacerLogin(idUsuario, password, showError, mostrarDialogo)
            }
        })
    }
    fun comprobarTraspasoPendiente(
        usuarioId: Int,
        onResult: (List<TraspasoPendienteDto>) -> Unit,
        onError: (String) -> Unit
    ) {
        ApiManager.traspasosApi
            .comprobarTraspasoPendiente(usuarioId)
            .enqueue(object : Callback<List<TraspasoPendienteDto>> {
                override fun onResponse(
                    call: Call<List<TraspasoPendienteDto>>,
                    response: Response<List<TraspasoPendienteDto>>
                ) {
                    if (response.isSuccessful) {
                        val lista = response.body().orEmpty()
                        onResult(lista)
                    } else if (response.code() == 404) {
                        onResult(emptyList()) // Consideramos 404 como "ningún traspaso pendiente"
                    } else {
                        onError("HTTP ${response.code()}")
                    }
                }

                override fun onFailure(call: Call<List<TraspasoPendienteDto>>, t: Throwable) {
                    onError("Red: ${t.message}")
                }
            })
    }

    private fun verificarTraspasosConOrden(
        lista: List<TraspasoPendienteDto>,
        onTieneOrden: () -> Unit,
        onSinOrden: () -> Unit,
        onError: (String) -> Unit
    ) {
        // Obtener todos los palets de los traspasos pendientes
        val traspasosPendientes = lista.filter { it.codigoEstado.equals("PENDIENTE", true) }
        
        if (traspasosPendientes.isEmpty()) {
            onSinOrden()
            return
        }
        
        // Verificar cada traspaso pendiente
        var verificados = 0
        var tieneOrden = false
        
        traspasosPendientes.forEach { traspaso ->
            // Obtener el palet para verificar si tiene orden asociada
            if (traspaso.paletId != null) {
                ApiManager.traspasosApi.obtenerPalet(traspaso.paletId)
                .enqueue(object : Callback<PaletDto> {
                    override fun onResponse(call: Call<PaletDto>, response: Response<PaletDto>) {
                        verificados++
                        
                        if (response.isSuccessful) {
                            val palet = response.body()
                            if (palet?.ordenTrabajoId != null) {
                                // Este palet tiene una orden asociada
                                tieneOrden = true
                                // Guardar el ordenId para navegación
                                android.util.Log.d("LOGIN_LOGIC", "🔍 Palet con orden encontrado: ${palet.ordenTrabajoId}")
                                
                            }
                        }
                        
                        // Si hemos verificado todos los traspasos
                        if (verificados == traspasosPendientes.size) {
                            if (tieneOrden) {
                                onTieneOrden()
                            } else {
                                onSinOrden()
                            }
                        }
                    }
                    
                    override fun onFailure(call: Call<PaletDto>, t: Throwable) {
                        verificados++
                        
                        // Si hemos verificado todos los traspasos (aunque haya fallado alguno)
                        if (verificados == traspasosPendientes.size) {
                            if (tieneOrden) {
                                onTieneOrden()
                            } else {
                                onSinOrden()
                            }
                        }
                    }
                })
            } else {
                // Si no hay paletId, no puede tener orden asociada
                verificados++
                if (verificados == traspasosPendientes.size) {
                    if (tieneOrden) {
                        onTieneOrden()
                    } else {
                        onSinOrden()
                    }
                }
            }
        }
    }

}


