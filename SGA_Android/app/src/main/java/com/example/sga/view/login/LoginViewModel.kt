package com.example.sga.view.login

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.example.sga.data.dto.traspasos.TraspasoPendienteDto
import com.example.sga.data.model.user.User
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.launch

class LoginViewModel : ViewModel() {

    private val _user = MutableStateFlow<User?>(null)
    val user: StateFlow<User?> = _user

    private val _error = MutableStateFlow<String?>(null)
    val error: StateFlow<String?> = _error

    // Agregar estado de carga
    private val _isLoading = MutableStateFlow(false)
    val isLoading: StateFlow<Boolean> = _isLoading

    fun setUser(user: User) {
        _user.value = user
    }

    fun setError(msg: String?) {
        _error.value = msg
    }

    // Agregar función para manejar el estado de carga
    fun setLoading(loading: Boolean) {
        _isLoading.value = loading
    }

    sealed interface Destino {
        object Home : Destino
        object OrdenTraspaso : Destino
        data class Traspasos(
            val esPalet: Boolean,
            val directoDesdePaletCerrado: Boolean = false
        ) : Destino
    }

    private val _navigate = MutableSharedFlow<Destino>(replay = 0) // <--- replay de 1
    val navigate = _navigate.asSharedFlow()

    fun emitirDestino(pendiente: Boolean) {
        viewModelScope.launch {
            if (pendiente) {
                val esDeOrden = traspasoPendienteDeOrden.value
                if (esDeOrden) {
                    // Traspaso pendiente de orden → Navegar a OrdenTraspasoProcesoScreen
                    _navigate.emit(Destino.OrdenTraspaso)
                } else {
                    // Traspaso pendiente normal → Navegar a TraspasosScreen
                    val esPalet = traspasoEsDePalet.value
                    val directo = traspasoDirectoDesdePaletCerrado.value
                    _navigate.emit(Destino.Traspasos(esPalet, directo))
                }
            } else {
                _navigate.emit(Destino.Home)
            }
            _navigate.resetReplayCache()
        }
    }

    private val _traspasoEsDePalet = MutableStateFlow(false)
    val traspasoEsDePalet: StateFlow<Boolean> = _traspasoEsDePalet

    fun setTraspasoEsDePalet(valor: Boolean) {
        _traspasoEsDePalet.value = valor
    }
    private val _traspasoDirectoDesdePaletCerrado = MutableStateFlow(false)
    val traspasoDirectoDesdePaletCerrado: StateFlow<Boolean> = _traspasoDirectoDesdePaletCerrado
    fun setTraspasoDirectoDesdePaletCerrado(valor: Boolean) {
        _traspasoDirectoDesdePaletCerrado.value = valor
    }
    private val _listaTraspasosPendientes = MutableStateFlow<List<TraspasoPendienteDto>>(emptyList())
    val listaTraspasosPendientes: StateFlow<List<TraspasoPendienteDto>> = _listaTraspasosPendientes

    fun setListaTraspasosPendientes(lista: List<TraspasoPendienteDto>) {
        _listaTraspasosPendientes.value = lista
    }

    private val _traspasoPendienteDeOrden = MutableStateFlow(false)
    val traspasoPendienteDeOrden: StateFlow<Boolean> = _traspasoPendienteDeOrden

    fun setTraspasoPendienteDeOrden(valor: Boolean) {
        _traspasoPendienteDeOrden.value = valor
    }

    private val _ordenIdTraspasoPendiente = MutableStateFlow<String?>(null)
    val ordenIdTraspasoPendiente: StateFlow<String?> = _ordenIdTraspasoPendiente

    fun setOrdenIdTraspasoPendiente(ordenId: String?) {
        _ordenIdTraspasoPendiente.value = ordenId
    }

}
