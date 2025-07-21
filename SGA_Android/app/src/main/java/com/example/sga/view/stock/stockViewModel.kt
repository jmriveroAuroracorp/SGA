package com.example.sga.view.stock

import androidx.lifecycle.ViewModel
import com.example.sga.data.dto.stock.ArticuloDto
import com.example.sga.data.model.stock.Stock
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow

class StockViewModel : ViewModel() {

    private val _resultado = MutableStateFlow<List<Stock>>(emptyList())
    val resultado: StateFlow<List<Stock>> = _resultado

    private val _error = MutableStateFlow<String?>(null)
    val error: StateFlow<String?> = _error

    private val _cargando = MutableStateFlow(false)
    val cargando: StateFlow<Boolean> = _cargando

    private val _articulosFiltrados = MutableStateFlow<List<ArticuloDto>>(emptyList())
    val articulosFiltrados: StateFlow<List<ArticuloDto>> = _articulosFiltrados

    private val _mostrarDialogoSeleccion = MutableStateFlow(false)
    val mostrarDialogoSeleccion: StateFlow<Boolean> = _mostrarDialogoSeleccion

    private val _partidaSeleccionada = MutableStateFlow<String?>(null)
    val partidaSeleccionada: StateFlow<String?> = _partidaSeleccionada

    fun setResultado(lista: List<Stock>) {
        _resultado.value = lista
    }

    fun setError(mensaje: String?) {
        _error.value = mensaje
    }

    fun setCargando(valor: Boolean) {
        _cargando.value = valor
    }
    fun setArticulosFiltrados(lista: List<ArticuloDto>) {
        _articulosFiltrados.value = lista
    }

    fun setMostrarDialogoSeleccion(valor: Boolean) {
        _mostrarDialogoSeleccion.value = valor
    }
    fun setPartidaSeleccionada(partida: String?) {
        _partidaSeleccionada.value = partida
    }
}