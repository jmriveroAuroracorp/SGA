package com.example.sga.view.etiquetas

import android.util.Log
import androidx.lifecycle.ViewModel
import com.example.sga.data.dto.stock.ArticuloDto
import com.example.sga.data.dto.etiquetas.ImpresoraDto
import com.example.sga.data.dto.etiquetas.AlergenosDto
import com.example.sga.data.dto.etiquetas.LogImpresionDto
import com.example.sga.data.model.stock.Stock
import com.example.sga.view.app.SessionViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow

class EtiquetasViewModel : ViewModel() {
    lateinit var sessionViewModel: SessionViewModel
    private lateinit var logic: EtiquetasLogic

    fun init(session: SessionViewModel) {
        sessionViewModel = session
        logic = EtiquetasLogic(this, sessionViewModel)
    }

    private val _articuloSeleccionado = MutableStateFlow<ArticuloDto?>(null)
    val articuloSeleccionado: StateFlow<ArticuloDto?> = _articuloSeleccionado

    private val _alergenos = MutableStateFlow<String?>(null)
    val alergenos: StateFlow<String?> = _alergenos

    private val _impresoras = MutableStateFlow<List<ImpresoraDto>>(emptyList())
    val impresoras: StateFlow<List<ImpresoraDto>> = _impresoras

    private val _mostrarDialogoSeleccion = MutableStateFlow(false)
    val mostrarDialogoSeleccion: StateFlow<Boolean> = _mostrarDialogoSeleccion

    private val _articulosFiltrados = MutableStateFlow<List<ArticuloDto>>(emptyList())
    val articulosFiltrados: StateFlow<List<ArticuloDto>> = _articulosFiltrados

    private val _error = MutableStateFlow<String?>(null)
    val error: StateFlow<String?> = _error

    private val _cargando = MutableStateFlow(false)
    val cargando: StateFlow<Boolean> = _cargando

    private val _resultado = MutableStateFlow<List<Stock>>(emptyList())
    val resultado: StateFlow<List<Stock>> = _resultado

    fun setResultado(lista: List<Stock>) {
        _resultado.value = lista
    }

    fun setArticuloSeleccionado(articulo: ArticuloDto?) {
        _articuloSeleccionado.value = articulo
    }

    fun setAlergenos(alerg: String?) {
        _alergenos.value = alerg
    }

    fun setMostrarDialogoSeleccion(valor: Boolean) {
        _mostrarDialogoSeleccion.value = valor
    }

    fun setCargando(valor: Boolean) {
        _cargando.value = valor
    }

    fun setError(mensaje: String?) {
        _error.value = mensaje
    }
    fun buscarPorCodigo(codigo: String, codigoEmpresa: Short) {
        _articulosFiltrados.value = emptyList()
        setCargando(true)
        logic.buscarArticuloPorCodigo(
            codigo,
            codigoEmpresa,
            onUnico = {
                mostrarArticuloUnico(it, codigoEmpresa)
                setCargando(false)
            },
            onError = {
                setError(it)
                setCargando(false)
            }
        )
    }

    fun buscarPorDescripcion(descripcion: String, codigoEmpresa: Short) {
        Log.d("ETIQ", "Buscando por descripción: $descripcion")
        setCargando(true)
        logic.buscarArticuloPorDescripcion(
            descripcion,
            codigoEmpresa,
            onUnico = {
                mostrarArticuloUnico(it, codigoEmpresa)
                setCargando(false)
            },
            onMultiple = {
                _articulosFiltrados.value = it
                setMostrarDialogoSeleccion(true)
                setCargando(false)
            },
            onError = {
                setError(it)
                setCargando(false)
            }
        )
    }

    fun procesarCodigoEscaneado(code: String, codigoEmpresa: Short) {
        _articuloSeleccionado.value = null
        _articulosFiltrados.value = emptyList()
        setCargando(true)

        logic.procesarCodigoEscaneado(
            code = code,
            empresaId = codigoEmpresa,
            onCodigoDetectado = { codigoTF ->
                buscarPorCodigo(codigoTF.text, codigoEmpresa)
            },
            onMultipleArticulos = {
                _articulosFiltrados.value = it
                setMostrarDialogoSeleccion(true)
                setCargando(false)
            },
            onError = {
                setError(it)
                setCargando(false)
            }
        )
    }

    private fun mostrarArticuloUnico(art: ArticuloDto, codigoEmpresa: Short) {
        setArticuloSeleccionado(art)
        obtenerAlergenos(codigoEmpresa, art.codigoArticulo)
        consultarStock(codigoEmpresa, art.codigoArticulo)
        setMostrarDialogoSeleccion(false)
    }

    fun obtenerAlergenos(codigoEmpresa: Short, codigoArticulo: String) {
        logic.obtenerAlergenos(codigoEmpresa, codigoArticulo) {
            setAlergenos(it?.alergenos)
        }
    }

    fun cargarImpresoras() {
        logic.obtenerImpresoras(
            onResult = { _impresoras.value = it },
            onError = { setError(it) }
        )
    }

    fun enviarLogImpresion(dto: LogImpresionDto) {
        logic.enviarImpresion(dto) { respuesta ->
            if (respuesta == null) {
                setError("Error al enviar log de impresión")
            }
            // si necesitas actualizar estado, puedes hacerlo aquí
        }
    }

    fun actualizarImpresoraSeleccionadaEnBD(nombre: String) {
        logic.actualizarImpresoraSeleccionadaEnBD(nombre)
    }
    fun consultarStock(codigoEmpresa: Short, codigoArticulo: String) {
        setCargando(true)
        logic.consultarStock(
            codigoEmpresa = codigoEmpresa,
            codigoArticulo = codigoArticulo,
            onSuccess = { lista ->
                val desc = articuloSeleccionado.value?.descripcion ?: ""
                setResultado(lista.map { it.copy(descripcionArticulo = desc) })
                setCargando(false)
            },
            onError = { msg ->
                setError(msg)
                setCargando(false)
            }
        )
    }

}