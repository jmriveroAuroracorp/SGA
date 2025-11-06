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
import com.example.sga.data.dto.traspasos.TraspasoPendienteDto
import com.example.sga.data.model.stock.Stock
import com.example.sga.view.app.SessionViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow


object PaletFlujoStore {
    private const val FILE = "palet_flujo"
    private const val K_ID = "paletId"
    private const val K_USER = "usuarioId"
    private lateinit var ctx: android.content.Context
    fun init(c: android.content.Context) { ctx = c.applicationContext }
    private fun prefs() = ctx.getSharedPreferences(FILE, android.content.Context.MODE_PRIVATE)
    fun save(paletId: String, usuarioId: Int) {
        prefs().edit().putString(K_ID, paletId).putInt(K_USER, usuarioId).apply()
    }
    fun load(): Pair<String, Int>? {
        val p = prefs(); val id = p.getString(K_ID, null) ?: return null
        val uid = p.getInt(K_USER, -1); if (uid == -1) return null
        return id to uid
    }
    fun clear() { prefs().edit().clear().apply() }
}

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
    
    // Mapa temporal para almacenar el codigoPaletOrigen de cada línea
    private val _codigosPaletOrigen = MutableStateFlow<Map<String, String>>(emptyMap())
    val codigosPaletOrigen: StateFlow<Map<String, String>> = _codigosPaletOrigen

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
    // --- Bloqueo SOLO cuando el palet se ha creado desde esta pantalla ---
    private val _flujoCreacionPaletActivo = MutableStateFlow(false)
    val flujoCreacionPaletActivo: StateFlow<Boolean> = _flujoCreacionPaletActivo

    private val _paletEnFlujoId = MutableStateFlow<String?>(null)
    val paletEnFlujoId: StateFlow<String?> = _paletEnFlujoId




    fun activarFlujoCreacion(paletId: String, usuarioId: Int) {
        _paletEnFlujoId.value = paletId
        _flujoCreacionPaletActivo.value = true
        PaletFlujoStore.save(paletId, usuarioId) // guardamos para reanudar
    }

    fun finalizarFlujoCreacion() {
        _paletEnFlujoId.value = null
        _flujoCreacionPaletActivo.value = false
        PaletFlujoStore.clear() // limpiamos persistencia
    }

    fun cargarAlmacenesPermitidos(sessionViewModel: SessionViewModel, codigoEmpresa: Int) {
        val user = sessionViewModel.user.value ?: return

        logic.cargarAlmacenesPermitidos(
            user = user,
            codigoEmpresa = codigoEmpresa,
            onSuccess = { lista ->
                _almacenesAutorizados.value = lista
                // Incluir tanto almacenes específicos como del centro
                val almacenesEspecificos = user.codigosAlmacen
                val almacenesDelCentro = lista.filter { it.esDelCentro }.map { it.codigoAlmacen }
                almacenesPermitidos.value = (almacenesEspecificos + almacenesDelCentro).distinct()
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

    fun crearPalet(dto: PaletCrearDto, onSuccess: (PaletDto) -> Unit = {}) {
        _cargando.value = true
        _error.value = null

        logic.crearPalet(
            dto = dto,
            onSuccess = { nuevo ->
                _paletCreado.value = nuevo
                setPaletSeleccionado(nuevo)
                val usuarioId = dto.usuarioAperturaId
                activarFlujoCreacion(nuevo.id, usuarioId)
                _cargando.value = false
                onSuccess(nuevo)
            },
            onError = {
                _error.value = it
                _cargando.value = false
            }
        )
    }

    fun reanudarFlujoSiAplica(usuarioIdActual: Int, onListo: (PaletDto) -> Unit) {
        val saved = PaletFlujoStore.load() ?: return
        val (paletId, usuarioIdGuardado) = saved
        if (usuarioIdGuardado != usuarioIdActual) {
            PaletFlujoStore.clear()
            return
        }
        obtenerPalet(
            id = paletId,
            onSuccess = { palet ->
                if (palet.estado.equals("Abierto", true)) {
                    setPaletSeleccionado(palet)
                    _paletEnFlujoId.value = palet.id
                    _flujoCreacionPaletActivo.value = true
                    onListo(palet)
                } else {
                    PaletFlujoStore.clear()
                }
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
        codigoAlmacen: String?,
        codigoEmpresa: Short,
        //ubicacionOrigen: String?, // ✅ AÑADIDO AQUÍ
        onSuccess: (String) -> Unit,
        onError: (String) -> Unit
    ) {
        logic.cerrarPalet(
            idPalet = id,
            usuarioId = usuarioId,
            codigoAlmacen = codigoAlmacen,
            codigoEmpresa = codigoEmpresa,
            //ubicacionOrigen = ubicacionOrigen, // ✅ Y AQUÍ
            onSuccess = { traspasoId ->
                // ✅ Si el palet cerrado era el que estaba en flujo de creación, se termina el flujo
                if (_paletEnFlujoId.value == id) {
                    finalizarFlujoCreacion()
                }
                onSuccess(traspasoId)
            },
            onError = { onError(it) }
        )
    }

    fun validarUbicacionDePalet(
        palet: PaletDto,
        ubicacionEscaneada: Pair<String, String>,
        onValidado: () -> Unit,
        onError: (String) -> Unit
    ) {
        val TAG = "VALIDAR_PALET"

        val almacenEscaneado = ubicacionEscaneada.first.trim().uppercase()
        val ubicEscaneadaNorm = ubicacionEscaneada.second.trim().uppercase()

        // 🔎 Contexto de entrada
        android.util.Log.d(TAG, "▷ validarUbicacionDePalet() id=${palet.id}, codigo=${palet.codigoPalet}")
        android.util.Log.d(TAG, "   escaneado → almacen='$almacenEscaneado', ubicacion='$ubicEscaneadaNorm' (raw='${ubicacionEscaneada.first}'|'${ubicacionEscaneada.second}')")

        logic.obtenerUbicacionDePalet(
            idPalet = palet.id,
            onResult = { almApi, ubiApi ->
                android.util.Log.d(TAG, "   backend  → almacen='$almApi', ubicacion='$ubiApi'")

                val coincideAlm = almApi == almacenEscaneado
                val coincideUbi = ubiApi == ubicEscaneadaNorm
                android.util.Log.d(TAG, "   comparación → almacenOK=$coincideAlm, ubicOK=$coincideUbi")

                if (!coincideAlm || !coincideUbi) {
                    android.util.Log.e(
                        TAG,
                        "❌ NO COINCIDE. escaneado=[$almacenEscaneado|$ubicEscaneadaNorm] vs backend=[$almApi|$ubiApi]"
                    )
                    onError("La ubicación escaneada y el palet escaneado no coinciden.")
                } else {
                    android.util.Log.d(TAG, "✅ Ubicación de palet VALIDADA")
                    onValidado()
                }
            },
            onError = { msg ->
                android.util.Log.e(TAG, "❌ Error obtenerUbicacionDePalet(id=${palet.id}): $msg")
                onError(msg)
            }
        )
    }

    fun completarTraspaso(
        id: String,
        codigoAlmacenDestino: String,
        ubicacionDestino: String,
        usuarioId: Int,
        paletId: String? = null,
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
            paletId = paletId,
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
        codigoPaletOrigen: String? = null,
        onSuccess: () -> Unit
    ) {
        logic.anadirLineaPalet(
            idPalet = idPalet,
            dto = dto,
            onSuccess = {
                // Almacenar temporalmente el codigoPaletOrigen
                if (codigoPaletOrigen != null) {
                    val currentMap = _codigosPaletOrigen.value.toMutableMap()
                    currentMap[idPalet] = codigoPaletOrigen
                    _codigosPaletOrigen.value = currentMap
                }
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

    fun limpiarPaletVacío(paletId: String, usuarioId: Int) {
        logic.forzarVaciadoPalet(
            idPalet = paletId,
            usuarioId = usuarioId,
            onSuccess = {
                // Limpiar el palet seleccionado y finalizar el flujo de creación
                clearPaletSeleccionado()
                finalizarFlujoCreacion()
                _paletCreado.value = null
                _lineasPalet.value = emptyMap()
                _error.value = null
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
            onSuccess = { responseJson ->
                _error.value = null
                // Aquí podrías almacenar la respuesta si necesitas mostrarla
                Log.d("IMPRESION_SUCCESS", "Respuesta del backend: $responseJson")
            },
            onError = {
                _error.value = it
            }
        )
    }
    
    fun imprimirEtiquetaPaletConCallback(dto: LogImpresionDto, onComplete: (String) -> Unit) {
        logic.imprimirEtiquetaPalet(
            dto = dto,
            onSuccess = { responseJson ->
                _error.value = null
                Log.d("IMPRESION_SUCCESS", "Respuesta del backend: $responseJson")
                onComplete(responseJson)
            },
            onError = { error ->
                _error.value = error
                Log.e("IMPRESION_ERROR", "Error al imprimir: $error")
                onComplete("ERROR: $error")
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
        onConflictoPalet: (com.example.sga.data.dto.traspasos.ConflictoPaletResponse) -> Unit = {},
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
            onConflictoPalet = { conflicto ->
                _cargando.value = false
                onConflictoPalet(conflicto)
            },
            onError = {
                _cargando.value = false
                onError(it)
            }
        )
    }

    fun precheckFinalizarArticulo(
        codigoEmpresa: Short,
        almacenDestino: String,
        ubicacionDestino: String,
        onResult: (existe: Boolean, paletId: String?, cerrado: Boolean, aviso: String?, cantidadPalets: Int?, palets: List<PaletDto>?) -> Unit,
        onError: (String) -> Unit
    ) {
        logic.precheckFinalizarArticulo(
            codigoEmpresa = codigoEmpresa,
            almacenDestino = almacenDestino,
            ubicacionDestino = ubicacionDestino,
            onResult = onResult,
            onError = onError
        )
    }

    /**
     * Valida que un código GS1 escaneado corresponda a uno de los palets en la ubicación destino
     */
    fun validarGS1ContraPalets(
        codigoEscaneado: String,
        paletsDisponibles: List<PaletDto>
    ): String? {
        val TAG = "VALIDAR_GS1_PALET"
        Log.d(TAG, "🔍 Validando código: '$codigoEscaneado'")
        
        // Extraer GS1 si viene con prefijo "00"
        val gs1Regex = Regex("""^00(\d{18})""")
        val gs1Extraido = gs1Regex.find(codigoEscaneado)?.groupValues?.get(1) ?: codigoEscaneado.trim()
        
        Log.d(TAG, "🔢 GS1 normalizado: '$gs1Extraido'")
        
        val paletCoincidente = paletsDisponibles.find { palet ->
            val gs1Palet = palet.codigoGS1?.trim()
            val coincide = gs1Palet == gs1Extraido
            Log.d(TAG, "   Comparando: ${palet.codigoPalet} GS1='$gs1Palet' -> ${if (coincide) "✅" else "❌"}")
            coincide
        }
        
        return paletCoincidente?.let {
            Log.d(TAG, "✅ Palet validado: ${it.codigoPalet} (ID: ${it.id})")
            it.id
        } ?: run {
            Log.w(TAG, "❌ GS1 no encontrado: $gs1Extraido")
            null
        }
    }

    fun comprobarTraspasoPendiente(
        usuarioId: Int,
        onSuccess: (List<TraspasoPendienteDto>) -> Unit,
        onNoPendiente: () -> Unit,
        onError: (String) -> Unit
    ) {
        logic.comprobarTraspasoPendiente(
            usuarioId = usuarioId,
            onSuccess = { lista ->
                val pendientes = lista
                    .filter { it.codigoEstado.equals("PENDIENTE", ignoreCase = true) }

                // ✅ Guarda los traspasos para que estén disponibles luego
                _traspasosPendientes.value = pendientes

                if (pendientes.isNotEmpty()) {
                    onSuccess(pendientes)
                } else {
                    onNoPendiente()
                }
            },
            onError = { error -> onError(error) }
        )
    }

    private val _traspasosPendientes = MutableStateFlow<List<TraspasoPendienteDto>>(emptyList())
    val traspasosPendientes: StateFlow<List<TraspasoPendienteDto>> = _traspasosPendientes

    fun setTraspasosPendientes(lista: List<TraspasoPendienteDto>) {
        _traspasosPendientes.value = lista
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
    fun clearPendientes() {
        _traspasosPendientes.value = emptyList()
    }

    fun finalizarTraspasoPalet(
        traspasoId: String,
        dto: FinalizarTraspasoPaletDto,
        paletId: String? = null,               // <-- NUEVO
        onSuccess: () -> Unit,
        onError: (String) -> Unit
    ) {
        _cargando.value = true
        logic.finalizarTraspasoPalet(
            traspasoId = traspasoId,
            dto = dto,
            paletId = paletId,                  // <-- PROPAGAR
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

    // TraspasosViewModel.kt
    fun cargarArticuloPorCodigo(
        empresaId: Short,
        codigoArticulo: String,
        codigoAlmacen: String? = null,
        codigoCentro: String? = null,
        almacen: String? = null,
        onSuccess: (ArticuloDto) -> Unit,
        onError: (String) -> Unit
    ) {
        logic.buscarArticuloPorCodigo(
            codigoEmpresa = empresaId,
            codigoArticulo = codigoArticulo,
            codigoAlmacen = codigoAlmacen,
            codigoCentro = codigoCentro,
            almacen = almacen,
            onSuccess = onSuccess,
            onError = onError
        )
    }
}