package com.example.sga.view.conversion

import androidx.lifecycle.ViewModel
import com.example.sga.data.dto.stock.ArticuloDto
import com.example.sga.data.dto.traspasos.LineaPaletDto
import com.example.sga.data.dto.traspasos.PaletDto
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow

class ConversionViewModel : ViewModel() {

    private val logic = ConversionLogic()

    // Estados de UI
    private val _cargando = MutableStateFlow(false)
    val cargando: StateFlow<Boolean> = _cargando

    private val _error = MutableStateFlow<String?>(null)
    val error: StateFlow<String?> = _error

    // Ubicación escaneada
    private val _ubicacionOrigen = MutableStateFlow<Pair<String, String>?>(null)
    val ubicacionOrigen: StateFlow<Pair<String, String>?> = _ubicacionOrigen

    // Artículo escaneado (no necesario para conversión)
    private val _articuloSeleccionado = MutableStateFlow<ArticuloDto?>(null)
    val articuloSeleccionado: StateFlow<ArticuloDto?> = _articuloSeleccionado

    // Palet escaneado
    private val _paletSeleccionado = MutableStateFlow<PaletDto?>(null)
    val paletSeleccionado: StateFlow<PaletDto?> = _paletSeleccionado

    // Líneas del palet
    private val _lineasPalet = MutableStateFlow<List<LineaPaletDto>>(emptyList())
    val lineasPalet: StateFlow<List<LineaPaletDto>> = _lineasPalet

    // Diálogo de conversión
    private val _mostrarDialogoConversion = MutableStateFlow(false)
    val mostrarDialogoConversion: StateFlow<Boolean> = _mostrarDialogoConversion

    private val _lineaAConvertir = MutableStateFlow<LineaPaletDto?>(null)
    val lineaAConvertir: StateFlow<LineaPaletDto?> = _lineaAConvertir

    private val _almacenesPermitidos = MutableStateFlow<List<String>>(emptyList())
    val almacenesPermitidos: StateFlow<List<String>> = _almacenesPermitidos

    fun setUbicacionOrigen(codAlmacen: String, codUbicacion: String) {
        _ubicacionOrigen.value = codAlmacen to codUbicacion
        _error.value = null
    }

    fun clearUbicacionOrigen() {
        _ubicacionOrigen.value = null
    }

    fun setArticuloSeleccionado(articulo: ArticuloDto) {
        _articuloSeleccionado.value = articulo
        _error.value = null
    }

    fun clearArticuloSeleccionado() {
        _articuloSeleccionado.value = null
    }

    fun setPaletSeleccionado(palet: PaletDto) {
        // NO establecer el palet aquí - solo se establece después de validación exitosa
        _error.value = null
    }

    fun setPaletSeleccionadoYValidado(palet: PaletDto) {
        _paletSeleccionado.value = palet
        obtenerLineasDePalet(palet.id)
        _error.value = null
    }

    fun clearPaletSeleccionado() {
        _paletSeleccionado.value = null
        _lineasPalet.value = emptyList()
    }

    fun obtenerLineasDePalet(idPalet: String) {
        _cargando.value = true
        logic.obtenerLineasPalet(
            idPalet = idPalet,
            onSuccess = { lineas ->
                _lineasPalet.value = lineas
                _cargando.value = false
            },
            onError = { error ->
                _error.value = error
                _lineasPalet.value = emptyList()
                _cargando.value = false
            }
        )
    }

    fun procesarCodigoEscaneado(
        code: String,
        empresaId: Short,
        codigoAlmacen: String? = null,
        codigoCentro: String? = null,
        almacen: String? = null,
        onUbicacionDetectada: (String, String) -> Unit,
        onPaletDetectado: (PaletDto) -> Unit,
        onError: (String) -> Unit
    ) {
        _cargando.value = true
        _error.value = null

        logic.procesarCodigoEscaneado(
            code = code,
            empresaId = empresaId,
            codigoAlmacen = codigoAlmacen,
            codigoCentro = codigoCentro,
            almacen = almacen,
            onUbicacionDetectada = { codAlm, codUbi ->
                setUbicacionOrigen(codAlm, codUbi)
                onUbicacionDetectada(codAlm, codUbi)
                _cargando.value = false
            },
            onPaletDetectado = { palet ->
                setPaletSeleccionado(palet)
                onPaletDetectado(palet)
                _cargando.value = false
            },
            onError = { error ->
                _error.value = error
                onError(error)
                _cargando.value = false
            }
        )
    }

    fun abrirDialogoConversion(linea: LineaPaletDto) {
        _lineaAConvertir.value = linea
        _mostrarDialogoConversion.value = true
    }

    fun cerrarDialogoConversion() {
        _mostrarDialogoConversion.value = false
        _lineaAConvertir.value = null
    }

    fun convertirLinea(
        lineaOriginal: LineaPaletDto,
        nuevoCodigoArticulo: String,
        cantidadAConvertir: Double,
        empresaId: Short,
        usuarioId: Int,
        onSuccess: () -> Unit,
        onError: (String) -> Unit
    ) {
        _cargando.value = true
        logic.convertirLinea(
            lineaOriginal = lineaOriginal,
            nuevoCodigoArticulo = nuevoCodigoArticulo,
            cantidadAConvertir = cantidadAConvertir,
            empresaId = empresaId,
            usuarioId = usuarioId,
            onSuccess = {
                // Recargar líneas del palet
                _paletSeleccionado.value?.let { palet ->
                    obtenerLineasDePalet(palet.id)
                }
                cerrarDialogoConversion()
                _cargando.value = false
                onSuccess()
            },
            onError = { error ->
                _error.value = error
                _cargando.value = false
                onError(error)
            }
        )
    }

    fun resetearFlujo() {
        clearUbicacionOrigen()
        clearArticuloSeleccionado()
        clearPaletSeleccionado()
        _error.value = null
    }

    fun setError(mensaje: String?) {
        _error.value = mensaje
    }

    fun validarUbicacionDePalet(
        palet: PaletDto,
        ubicacionEscaneada: Pair<String, String>,
        onValidado: () -> Unit,
        onError: (String) -> Unit
    ) {
        logic.validarUbicacionDePalet(
            palet = palet,
            ubicacionEscaneada = ubicacionEscaneada,
            onValidado = onValidado,
            onError = onError
        )
    }

    fun cargarAlmacenesPermitidos(user: com.example.sga.data.model.user.User, empresaId: Int) {
        logic.cargarAlmacenesPermitidos(
            user = user,
            empresaId = empresaId,
            onSuccess = { almacenes ->
                _almacenesPermitidos.value = almacenes
            },
            onError = { error ->
                _error.value = error
            }
        )
    }

    fun reabrirPalet(id: String, usuarioId: Int, onRefresh: () -> Unit) {
        logic.reabrirPalet(
            idPalet = id,
            usuarioId = usuarioId,
            onSuccess = { onRefresh() },
            onError = { error ->
                _error.value = error
            }
        )
    }

    fun obtenerPalet(id: String, onSuccess: (com.example.sga.data.dto.traspasos.PaletDto) -> Unit) {
        logic.obtenerPalet(
            idPalet = id,
            onSuccess = onSuccess,
            onError = { error ->
                _error.value = error
            }
        )
    }
}

