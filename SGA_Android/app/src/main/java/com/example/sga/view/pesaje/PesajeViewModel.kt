package com.example.sga.view.pesaje

import androidx.lifecycle.ViewModel
import com.example.sga.data.model.pesaje.Pesaje
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow

class PesajeViewModel : ViewModel() {

    private val _scanning = MutableStateFlow(false)
    val scanning: StateFlow<Boolean> = _scanning

    private val _error = MutableStateFlow<String?>(null)
    val error: StateFlow<String?> = _error

    private val _resultado = MutableStateFlow<Pesaje?>(null) // ← usamos el modelo limpio
    val resultado: StateFlow<Pesaje?> = _resultado

    fun empezarEscaneo(iniciar: Boolean = true) {
        _error.value = null
        _resultado.value = null
        _scanning.value = iniciar
    }

    fun mostrarError(msg: String?) {
        _error.value = msg
        _resultado.value = null
        _scanning.value = false
    }

    fun setResultado(resultado: Pesaje) {
        _resultado.value = resultado
        _scanning.value = false
    }
}
