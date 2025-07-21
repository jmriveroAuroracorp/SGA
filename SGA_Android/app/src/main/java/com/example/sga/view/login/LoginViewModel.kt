package com.example.sga.view.login

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
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

    fun setUser(user: User) {
        _user.value = user
    }

    fun setError(msg: String?) {
        _error.value = msg
    }

    sealed interface Destino {
        object Home : Destino
        data class Traspasos(val esPalet: Boolean) : Destino
    }
    private val _navigate = MutableSharedFlow<Destino>(replay = 1) // <--- replay de 1
    val navigate = _navigate.asSharedFlow()

    fun emitirDestino(pendiente: Boolean) {
        viewModelScope.launch {
            if (pendiente) {
                val esPalet = traspasoEsDePalet.value
                _navigate.emit(Destino.Traspasos(esPalet))
            } else {
                _navigate.emit(Destino.Home)
            }

            _navigate.resetReplayCache() // 🔐 Limpia el valor después de usarlo
        }
    }


    private val _traspasoEsDePalet = MutableStateFlow(false)
    val traspasoEsDePalet: StateFlow<Boolean> = _traspasoEsDePalet

    fun setTraspasoEsDePalet(valor: Boolean) {
        _traspasoEsDePalet.value = valor
    }

}
