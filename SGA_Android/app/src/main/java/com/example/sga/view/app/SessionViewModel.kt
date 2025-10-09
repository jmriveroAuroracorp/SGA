package com.example.sga.view.app

import android.util.Log
import androidx.lifecycle.ViewModel
import com.example.sga.data.model.user.User
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import androidx.compose.runtime.mutableStateOf
import com.example.sga.data.dto.login.DispositivoDto
import com.example.sga.data.model.user.Empresa

class SessionViewModel : ViewModel() {

    var sessionToken: String? = null
        set(value) {
            field = value
            isLoggedIn.value = value != null
        }

    val isLoggedIn = mutableStateOf(false)

    private val _user = MutableStateFlow<User?>(null)
    private val _dispositivo = MutableStateFlow<DispositivoDto?>(null)
    val dispositivo: StateFlow<DispositivoDto?> = _dispositivo

    val user: StateFlow<User?> = _user

    private val _tokenTimestamp = MutableStateFlow<Long?>(null)
    val tokenTimestamp: StateFlow<Long?> = _tokenTimestamp

    fun actualizarTimestamp() {
        _tokenTimestamp.value = System.currentTimeMillis()
    }

    private val _tokenExpirado = MutableStateFlow(false)
    val tokenExpirado: StateFlow<Boolean> = _tokenExpirado

    fun marcarTokenExpirado(valor: Boolean) {
        _tokenExpirado.value = valor
    }

    fun setUser(user: User?) {
        _user.value = user
    }

    fun tienePermiso(codigo: Short): Boolean {
        return _user.value?.permisos?.contains(codigo) ?: false
    }

    fun clearSession() {
        Log.w("SGA_SESSION", "⚠️ Sesión cerrada o token inválido. Cerrando sesión.")
        sessionToken = null
        setUser(null)
        _tokenTimestamp.value = null  // ✅ Limpiar también el timestamp
    }
    private val _mensajeCaducidad = MutableStateFlow(false)
    val mensajeCaducidad: StateFlow<Boolean> = _mensajeCaducidad

    fun mostrarMensajeCaducidad() {
        _mensajeCaducidad.value = true
    }
    fun ocultarMensajeCaducidad() {
        _mensajeCaducidad.value = false
    }
    private val _modoVigilanciaActiva = MutableStateFlow(false)

    val modoVigilanciaActiva: StateFlow<Boolean> = _modoVigilanciaActiva

    fun activarVigilanciaActiva() {
        _modoVigilanciaActiva.value = true
    }

    fun resetVigilancia() {
        _modoVigilanciaActiva.value = false
    }

    private val _contraseña = MutableStateFlow<String?>(null)
    val contraseña: StateFlow<String?> = _contraseña

    fun setContraseña(valor: String) {
        _contraseña.value = valor
    }

    fun clearContraseña() {
        _contraseña.value = null
    }

    private val _empresaSeleccionada = MutableStateFlow<Empresa?>(null)
    val empresaSeleccionada: StateFlow<Empresa?> = _empresaSeleccionada

    fun setEmpresaSeleccionada(emp: Empresa) {
        _empresaSeleccionada.value = emp
    }
    fun setDispositivo(dispositivo: DispositivoDto) {
        _dispositivo.value = dispositivo
    }
    private val _impresoraSeleccionada = MutableStateFlow<String?>(null)
    val impresoraSeleccionada: StateFlow<String?> = _impresoraSeleccionada

    fun actualizarImpresora(nombre: String) {
        _impresoraSeleccionada.value = nombre
    }
}


