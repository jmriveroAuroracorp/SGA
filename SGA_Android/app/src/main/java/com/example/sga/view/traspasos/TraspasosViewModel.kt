package com.example.sga.view.traspasos


import android.util.Log
import androidx.lifecycle.ViewModel
import com.example.sga.data.dto.almacenes.AlmacenDto
import com.example.sga.data.dto.etiquetas.ImpresoraDto
import com.example.sga.data.dto.etiquetas.LogImpresionDto
import com.example.sga.data.dto.stock.ArticuloDto
import com.example.sga.data.dto.traspasos.CompletarTraspasoDto
import com.example.sga.data.dto.traspasos.LineaPaletCrearDto
import com.example.sga.data.dto.traspasos.LineaPaletDto
import com.example.sga.data.dto.traspasos.PaletCrearDto
import com.example.sga.data.dto.traspasos.PaletDto
import com.example.sga.data.dto.traspasos.TipoPaletDto
import com.example.sga.data.dto.traspasos.CrearTraspasoArticuloDto
import com.example.sga.data.dto.traspasos.FinalizarTraspasoArticuloDto
import com.example.sga.data.dto.traspasos.FinalizarTraspasoPaletDto
import com.example.sga.data.dto.traspasos.MoverPaletDto
import com.example.sga.data.model.stock.Stock
import com.example.sga.view.app.SessionLogic
import com.example.sga.view.app.SessionViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow

class TraspasosViewModel : ViewModel() {

    private val logic = TraspasosLogic()
    private val _tiposPalet = MutableStateFlow<List<TipoPaletDto>>(emptyList())
    val tiposPalet: StateFlow<List<TipoPaletDto>> = _tiposPalet

    private val _paletCreado = MutableStateFlow<PaletDto?>(null)
    val paletCreado: StateFlow<PaletDto?> = _paletCreado

    private val _error = MutableStateFlow<String?>(null)
    val error: StateFlow<String?> = _error

    private val _cargando = MutableStateFlow(false)
    val cargando: StateFlow<Boolean> = _cargando

    private val _lineasPalet = MutableStateFlow<Map<String, List<LineaPaletDto>>>(emptyMap())
    val lineasPalet: StateFlow<Map<String, List<LineaPaletDto>>> = _lineasPalet

    private val _articulosFiltrados = MutableStateFlow<List<ArticuloDto>>(emptyList())
    val articulosFiltrados: StateFlow<List<ArticuloDto>> = _articulosFiltrados

    private val _mostrarDialogoSeleccion = MutableStateFlow(false)
    val mostrarDialogoSeleccion: StateFlow<Boolean> = _mostrarDialogoSeleccion

    private val _resultadoStock = MutableStateFlow<List<Stock>>(emptyList())
    val resultadoStock: StateFlow<List<Stock>> = _resultadoStock

    private val _impresoras = MutableStateFlow<List<ImpresoraDto>>(emptyList())
    val impresoras: StateFlow<List<ImpresoraDto>> = _impresoras
    private val _almacenesAutorizados = MutableStateFlow<List<AlmacenDto>>(emptyList())


    val almacenesPermitidos = MutableStateFlow<List<String>>(emptyList())

    // ViewModel
    private val _ubicacionOrigen = MutableStateFlow<Pair<String,String>?>(null)
    val ubicacionOrigen: StateFlow<Pair<String,String>?> = _ubicacionOrigen

    fun setUbicacionOrigen(codAlmacen: String, codUbicacion: String) {
        _ubicacionOrigen.value = codAlmacen to codUbicacion
    }
    fun clearUbicacionOrigen() {
        _ubicacionOrigen.value = null
    }

    fun cargarAlmacenesPermitidos(sessionViewModel: SessionViewModel, codigoEmpresa: Int) {
        val user = sessionViewModel.user.value ?: return

        logic.cargarAlmacenesPermitidos(
            user = user,
            codigoEmpresa = codigoEmpresa,
            onSuccess = { lista ->
                _almacenesAutorizados.value = lista
                almacenesPermitidos.value = lista.mapNotNull { it.codigoAlmacen }
            },
            onError = { _error.value = it }
        )
    }

    fun cargarTiposPalet() {
        logic.obtenerTiposPalet(
            onSuccess = { _tiposPalet.value = it },
            onError = { _error.value = it }
        )
    }

    fun crearPalet(dto: PaletCrearDto) {
        _cargando.value = true
        _error.value = null

        logic.crearPalet(
            dto = dto,
            onSuccess = {
                _paletCreado.value = it
                _cargando.value = false
            },
            onError = {
                _error.value = it
                _cargando.value = false
            }
        )
    }
    fun obtenerPalet(id: String, onSuccess: (PaletDto) -> Unit) {
        logic.obtenerPalet(
            idPalet = id,
            onSuccess = { onSuccess(it) },
            onError = { /* podrías emitir un error si lo necesitas */ }
        )
    }

    fun obtenerLineasDePalet(idPalet: String) {
        logic.obtenerLineasPalet(idPalet,
            onSuccess = { lineas ->
                _lineasPalet.value = _lineasPalet.value.toMutableMap().apply {
                    this[idPalet] = lineas
                }
            },
            onError = {
                _lineasPalet.value = _lineasPalet.value.toMutableMap().apply {
                    this[idPalet] = emptyList()
                }
            }
        )
    }
    fun cerrarPalet(
        id: String,
        usuarioId: Int,
        codigoAlmacen: String,
        codigoEmpresa: Short,
        ubicacionOrigen: String?, // ✅ AÑADIDO AQUÍ
        onSuccess: (String) -> Unit,
        onError: (String) -> Unit
    ) {
        logic.cerrarPalet(
            idPalet = id,
            usuarioId = usuarioId,
            codigoAlmacen = codigoAlmacen,
            codigoEmpresa = codigoEmpresa,
            ubicacionOrigen = ubicacionOrigen, // ✅ Y AQUÍ
            onSuccess = { traspasoId -> onSuccess(traspasoId) },
            onError = { onError(it) }
        )
    }

    fun validarUbicacionDePalet(
        palet: PaletDto,
        ubicacionEscaneada: Pair<String, String>,
        onValidado: () -> Unit,
        onError: (String) -> Unit
    ) {
        val almacenEscaneado = ubicacionEscaneada.first.trim().uppercase()
        val ubicacionEscaneadaNormalizada = ubicacionEscaneada.second.trim().uppercase()

        logic.obtenerUbicacionDePalet(
            idPalet = palet.id,
            onResult = { almacen, ubicacion ->
                if (almacen != almacenEscaneado || ubicacion != ubicacionEscaneadaNormalizada) {
                    onError("La ubicación escaneada y el palet escaneado no coinciden.")
                } else {
                    onValidado()
                }
            },
            onError = onError
        )
    }

    fun completarTraspaso(
        id: String,
        codigoAlmacenDestino: String,
        ubicacionDestino: String,
        usuarioId: Int,
        onSuccess: () -> Unit,
        onError: (String) -> Unit
    ) {
        val fechaFinalizacion = java.time.LocalDateTime.now().toString()

        val dto = CompletarTraspasoDto(
            codigoAlmacenDestino = codigoAlmacenDestino,
            ubicacionDestino = ubicacionDestino,
            fechaFinalizacion = fechaFinalizacion,
            usuarioFinalizacionId = usuarioId
        )

        logic.completarTraspaso(
            idTraspaso = id,
            dto = dto,
            onSuccess = {
                clearTraspasoArticuloPendiente()
                clearUbicacionOrigen()
                clearPaletSeleccionado()
                _paletCreado.value = null
                _lineasPalet.value = emptyMap()
                onSuccess()
            },
            onError = {
                onError(it)
            }
        )
    }

    fun reabrirPalet(id: String, usuarioId: Int, onRefresh: () -> Unit) {
        logic.reabrirPalet(
            idPalet = id,
            usuarioId = usuarioId,
            onSuccess = { onRefresh() },
            onError = { Log.e("PALET", "❌ Error al reabrir palet: $it") }
        )
    }

    fun anadirLinea(
        idPalet: String,
        dto: LineaPaletCrearDto,
        onSuccess: () -> Unit
    ) {
        logic.anadirLineaPalet(
            idPalet = idPalet,
            dto = dto,
            onSuccess = {
                obtenerLineasDePalet(idPalet)
                onSuccess()
            },
            onError = { _error.value = it }
        )
    }
    fun eliminarLineaPalet(idLinea: String, usuarioId: Int, paletId: String) {
        logic.eliminarLineaPalet(idLinea, usuarioId,
            onSuccess = {
                obtenerLineasDePalet(paletId)
            },
            onError = {
                _error.value = it
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
        onArticuloDetectado: (ArticuloDto) -> Unit,
        onMultipleArticulos: (List<ArticuloDto>) -> Unit,
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
                onPaletDetectado(palet)
                obtenerLineasDePalet(palet.id)
                _cargando.value = false
            },
            onArticuloDetectado = {
                onArticuloDetectado(it)
                _cargando.value = false
            },
            onMultipleArticulos = {
                _articulosFiltrados.value = it
                _mostrarDialogoSeleccion.value = true
                onMultipleArticulos(it)
                _cargando.value = false
            },
            onError = {
                _error.value = it
                onError(it)
                _cargando.value = false
            }
        )
    }

    fun setMostrarDialogoSeleccion(valor: Boolean) {
        _mostrarDialogoSeleccion.value = valor
    }

    fun setArticulosFiltrados(lista: List<ArticuloDto>) {
        _articulosFiltrados.value = lista
    }

    fun buscarStockYMostrar(
        codigoArticulo: String,
        empresaId: Short,
        codigoAlmacen: String? = null,
        codigoUbicacion: String? = null,
        almacenesPermitidos: List<String>? = null,
        partida: String? = null
    ) {
        _cargando.value = true
        _resultadoStock.value = emptyList()

        logic.consultarStockConDescripcion(
            codigoEmpresa = empresaId,
            codigoArticulo = codigoArticulo,
            codigoAlmacen = codigoAlmacen,
            codigoUbicacion = codigoUbicacion,
            almacenesPermitidos = almacenesPermitidos,
            partida = partida,
            onSuccess = { lista ->
                _resultadoStock.value = lista
                _cargando.value = false
            },
            onError = {
                _error.value = it
                _cargando.value = false
            }
        )
    }

    fun cargarImpresoras() {
        logic.cargarImpresoras(
            onResult = { _impresoras.value = it },
            onError = { setError(it) } // si tienes función setError
        )
    }


    fun imprimirEtiquetaPalet(dto: LogImpresionDto) {
        logic.imprimirEtiquetaPalet(
            dto = dto,
            onSuccess = {
                _error.value = null
            },
            onError = {
                _error.value = it
            }
        )
    }
    fun actualizarImpresoraSeleccionadaEnBD(
        nombre: String,
        sessionViewModel: SessionViewModel
    ) {
        logic.actualizarImpresoraSeleccionadaEnBD(nombre, sessionViewModel)
    }

    fun limpiarStock() {
        _resultadoStock.value = emptyList()
    }
    fun setError(mensaje: String?) {
        _error.value = mensaje
    }
    private val _paletSeleccionado = MutableStateFlow<PaletDto?>(null)
    val paletSeleccionado: StateFlow<PaletDto?> = _paletSeleccionado

    fun setPaletSeleccionado(palet: PaletDto?) {
        _paletSeleccionado.value = palet
    }

    fun clearPaletSeleccionado() {
        _paletSeleccionado.value = null
    }

    // Estado para traspaso de artículo pendiente
    private val _traspasoArticuloPendienteId = MutableStateFlow<String?>(null)
    val traspasoArticuloPendienteId: StateFlow<String?> = _traspasoArticuloPendienteId

    private val _articuloPendienteMover = MutableStateFlow<ArticuloDto?>(null)
    val articuloPendienteMover: StateFlow<ArticuloDto?> = _articuloPendienteMover

    fun setArticuloPendienteMover(articulo: ArticuloDto?) {
        _articuloPendienteMover.value = articulo
    }
    fun clearTraspasoArticuloPendiente() {
        _traspasoArticuloPendienteId.value = null
        _articuloPendienteMover.value = null
        _resultadoStock.value = emptyList()
    }

    fun crearTraspasoArticulo(
        dto: CrearTraspasoArticuloDto,
        onSuccess: (String) -> Unit,
        onError: (String) -> Unit
    ) {
        _cargando.value = true
        logic.crearTraspasoArticulo(
            dto = dto,
            onSuccess = { traspasoDto ->
                _traspasoArticuloPendienteId.value = traspasoDto.id
                _cargando.value = false
                onSuccess(traspasoDto.id)
            },
            onError = {
                _cargando.value = false
                onError(it)
            }
        )
    }

    fun finalizarTraspasoArticulo(
        id: String,
        dto: FinalizarTraspasoArticuloDto,
        onSuccess: () -> Unit,
        onError: (String) -> Unit
    ) {
        _cargando.value = true
        logic.finalizarTraspasoArticulo(
            id = id,
            dto = dto,
            onSuccess = {
                clearTraspasoArticuloPendiente()
                clearUbicacionOrigen()
                _paletCreado.value = null
                _lineasPalet.value = emptyMap()
                _cargando.value = false
                onSuccess()
            },
            onError = {
                _cargando.value = false
                onError(it)
            }
        )
    }


    fun comprobarTraspasoPendiente(
        usuarioId: Int,
        onSuccess: (String) -> Unit,
        onNoPendiente: () -> Unit,
        onError: (String) -> Unit
    ) {
        logic.comprobarTraspasoPendiente(
            usuarioId = usuarioId,
            onSuccess = { dto ->
                if (dto != null && dto.codigoEstado.equals("PENDIENTE", ignoreCase = true)) {
                    onSuccess(dto.id)
                } else {
                    onNoPendiente()
                }
            },
            onError = { error ->
                onError(error)
            }
        )
    }
    private val _traspasoEsDePalet = MutableStateFlow(false)
    val traspasoEsDePalet: StateFlow<Boolean> = _traspasoEsDePalet

    fun setTraspasoEsDePalet(valor: Boolean) {
        _traspasoEsDePalet.value = valor
    }

    fun moverPalet(
        dto: MoverPaletDto,
        onSuccess: (String) -> Unit, // devuelve el ID
        onError: (String) -> Unit
    ) {
        logic.moverPalet(
            dto = dto,
            onSuccess = { id ->
                _traspasoPendienteId.value = id // o _traspasoPendienteId.value = id
                onSuccess(id)
            },
            onError = {
                _error.value = it
                onError(it)
            }
        )
    }
    private val _traspasoPendienteId = MutableStateFlow<String?>(null)
    val traspasoPendienteId: StateFlow<String?> = _traspasoPendienteId

    fun finalizarTraspasoPalet(
        traspasoId: String,
        dto: FinalizarTraspasoPaletDto,
        onSuccess: () -> Unit,
        onError: (String) -> Unit
    ) {
        _cargando.value = true
        logic.finalizarTraspasoPalet(
            traspasoId = traspasoId,
            dto = dto,
            onSuccess = {
                _cargando.value = false
                onSuccess()
            },
            onError = {
                _cargando.value = false
                onError(it)
            }
        )
    }
    private val _traspasoDirectoDesdePaletCerrado = MutableStateFlow(false)
    val traspasoDirectoDesdePaletCerrado: StateFlow<Boolean> = _traspasoDirectoDesdePaletCerrado

    fun setTraspasoDirectoDesdePaletCerrado(valor: Boolean) {
        _traspasoDirectoDesdePaletCerrado.value = valor
    }

    fun finalizarPaletPorPaletId(
        paletId: String,
        dto: FinalizarTraspasoPaletDto,
        onSuccess: () -> Unit,
        onError: (String) -> Unit
    ) {
        _cargando.value = true
        logic.finalizarTraspasoPaletPorPaletId(
            paletId = paletId,
            dto = dto,
            onSuccess = {
                _cargando.value = false
                onSuccess()
            },
            onError = {
                _cargando.value = false
                onError(it)
            }
        )
    }

}