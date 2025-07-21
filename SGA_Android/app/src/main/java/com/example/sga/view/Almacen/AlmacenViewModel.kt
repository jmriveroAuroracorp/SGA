package com.example.sga.view.Almacen


import androidx.lifecycle.ViewModel
import com.example.sga.data.dto.almacenes.AlmacenDto
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow

class AlmacenViewModel : ViewModel() {

    private val _lista = MutableStateFlow<List<AlmacenDto>>(emptyList())
    val lista: StateFlow<List<AlmacenDto>> = _lista


    private val _cargando = MutableStateFlow(false)
    val cargando: StateFlow<Boolean> = _cargando

    private val _error = MutableStateFlow<String?>(null)
    val error: StateFlow<String?> = _error

    private val _seleccionado = MutableStateFlow<String?>(null)
    val seleccionado: StateFlow<String?> = _seleccionado

    /* ---------- setters ---------- */
    fun setLista(l: List<AlmacenDto>) { _lista.value = l }
    fun setCargando(b: Boolean)        { _cargando.value = b }
    fun setError(msg: String?)         { _error.value = msg }
    fun setSeleccionado(cod: String?)  { _seleccionado.value = cod }
}