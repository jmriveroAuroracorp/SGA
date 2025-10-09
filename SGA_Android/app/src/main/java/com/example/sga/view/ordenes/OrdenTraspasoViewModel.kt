package com.example.sga.view.ordenes

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.example.sga.data.dto.ordenes.*
import com.example.sga.data.dto.etiquetas.LogImpresionDto
import com.example.sga.data.dto.traspasos.LineaPaletCrearDto
import com.example.sga.data.dto.traspasos.CompletarTraspasoDto
import com.example.sga.data.model.user.User
import com.example.sga.view.app.SessionViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.delay

class OrdenTraspasoViewModel : ViewModel() {
    
    lateinit var sessionViewModel: SessionViewModel
    private lateinit var logic: OrdenTraspasoLogic
    
    fun init(session: SessionViewModel) {
        sessionViewModel = session
        logic = OrdenTraspasoLogic(this, sessionViewModel)
    }
    
    // Estados principales
    private val _ordenes = MutableStateFlow<List<OrdenTraspasoDto>>(emptyList())
    val ordenes: StateFlow<List<OrdenTraspasoDto>> = _ordenes.asStateFlow()
    
    // Estado para contar órdenes activas (se actualiza automáticamente)
    private val _ordenesActivas = MutableStateFlow(0)
    val ordenesActivas: StateFlow<Int> = _ordenesActivas.asStateFlow()
    
    // Función para actualizar automáticamente el conteo de órdenes activas
    private fun actualizarConteoOrdenesActivas() {
        val ordenes = _ordenes.value
        val activas = ordenes.count { orden ->
            orden.estado == "PENDIENTE" || orden.estado == "EN_PROCESO"
        }
        _ordenesActivas.value = activas
        android.util.Log.d("VIEWMODEL_ORDEN", "🔄 Conteo actualizado: $activas órdenes activas")
    }
    
    private val _ordenSeleccionada = MutableStateFlow<OrdenTraspasoDto?>(null)
    val ordenSeleccionada: StateFlow<OrdenTraspasoDto?> = _ordenSeleccionada.asStateFlow()
    
    private val _lineaSeleccionada = MutableStateFlow<LineaOrdenTraspasoDetalleDto?>(null)
    val lineaSeleccionada: StateFlow<LineaOrdenTraspasoDetalleDto?> = _lineaSeleccionada.asStateFlow()
    
    private val _stockDisponible = MutableStateFlow<List<StockDisponibleDto>>(emptyList())
    val stockDisponible: StateFlow<List<StockDisponibleDto>> = _stockDisponible.asStateFlow()
    
    // Estados de UI
    private val _cargando = MutableStateFlow(false)
    val cargando: StateFlow<Boolean> = _cargando.asStateFlow()
    
    private val _error = MutableStateFlow<String?>(null)
    val error: StateFlow<String?> = _error.asStateFlow()
    
    private val _mensaje = MutableStateFlow<String?>(null)
    val mensaje: StateFlow<String?> = _mensaje.asStateFlow()
    
    // Estados mejorados para manejo de errores
    enum class EstadoUI {
        NORMAL, CARGANDO, ERROR, EXITO
    }
    
    private val _estadoUI = MutableStateFlow(EstadoUI.NORMAL)
    val estadoUI: StateFlow<EstadoUI> = _estadoUI.asStateFlow()
    
    private val _mostrarDialogoErrorCrearMateria = MutableStateFlow(false)
    val mostrarDialogoErrorCrearMateria: StateFlow<Boolean> = _mostrarDialogoErrorCrearMateria.asStateFlow()
    
    private val _detallesErrorCrearMateria = MutableStateFlow<String?>(null)
    val detallesErrorCrearMateria: StateFlow<String?> = _detallesErrorCrearMateria.asStateFlow()
    
    private val _mostrarAdvertenciaCantidad = MutableStateFlow(false)
    val mostrarAdvertenciaCantidad: StateFlow<Boolean> = _mostrarAdvertenciaCantidad.asStateFlow()
    
    private val _mensajeAdvertenciaCantidad = MutableStateFlow<String?>(null)
    val mensajeAdvertenciaCantidad: StateFlow<String?> = _mensajeAdvertenciaCantidad.asStateFlow()
    
    
    // Estados para filtros
    private val _filtroEstado = MutableStateFlow<String?>(null)
    val filtroEstado: StateFlow<String?> = _filtroEstado.asStateFlow()
    
    // Estados para formularios
    private val _cantidadMovida = MutableStateFlow("")
    val cantidadMovida: StateFlow<String> = _cantidadMovida.asStateFlow()
    
    private val _ubicacionOrigenSeleccionada = MutableStateFlow<StockDisponibleDto?>(null)
    val ubicacionOrigenSeleccionada: StateFlow<StockDisponibleDto?> = _ubicacionOrigenSeleccionada.asStateFlow()
    
    private val _ubicacionDestino = MutableStateFlow("")
    val ubicacionDestino: StateFlow<String> = _ubicacionDestino.asStateFlow()
    
    private val _paletDestino = MutableStateFlow("")
    val paletDestino: StateFlow<String> = _paletDestino.asStateFlow()
    
    // Estados para gestión de palets
    private val _paletListoParaUbicar = MutableStateFlow<String?>(null)
    val paletListoParaUbicar: StateFlow<String?> = _paletListoParaUbicar.asStateFlow()
    
    private val _codigoGS1Palet = MutableStateFlow<String?>(null)
    val codigoGS1Palet: StateFlow<String?> = _codigoGS1Palet.asStateFlow()
    
    private val _paletsPendientes = MutableStateFlow<List<PaletPendienteDto>>(emptyList())
    val paletsPendientes: StateFlow<List<PaletPendienteDto>> = _paletsPendientes.asStateFlow()
    
    private val _almacenDestinoPalet = MutableStateFlow("")
    val almacenDestinoPalet: StateFlow<String> = _almacenDestinoPalet.asStateFlow()
    
    private val _ubicacionDestinoPalet = MutableStateFlow("")
    val ubicacionDestinoPalet: StateFlow<String> = _ubicacionDestinoPalet.asStateFlow()
    
    // Estados para el flujo de escaneo
    private val _procesoEscaneoActivo = MutableStateFlow(false)
    val procesoEscaneoActivo: StateFlow<Boolean> = _procesoEscaneoActivo.asStateFlow()
    
    private val _esperandoUbicacion = MutableStateFlow(false)
    val esperandoUbicacion: StateFlow<Boolean> = _esperandoUbicacion.asStateFlow()
    
    private val _esperandoArticulo = MutableStateFlow(false)
    val esperandoArticulo: StateFlow<Boolean> = _esperandoArticulo.asStateFlow()
    
    private val _ubicacionValidada = MutableStateFlow(false)
    val ubicacionValidada: StateFlow<Boolean> = _ubicacionValidada.asStateFlow()
    
    private val _articuloValidado = MutableStateFlow<com.example.sga.data.dto.stock.ArticuloDto?>(null)
    val articuloValidado: StateFlow<com.example.sga.data.dto.stock.ArticuloDto?> = _articuloValidado.asStateFlow()
    
    private val _stockSeleccionadoParaEscaneo = MutableStateFlow<StockDisponibleDto?>(null)
    val stockSeleccionadoParaEscaneo: StateFlow<StockDisponibleDto?> = _stockSeleccionadoParaEscaneo.asStateFlow()
    
    // Estados para funcionalidad de traspasos/palets
    private val _tiposPalet = MutableStateFlow<List<com.example.sga.data.dto.traspasos.TipoPaletDto>>(emptyList())
    val tiposPalet: StateFlow<List<com.example.sga.data.dto.traspasos.TipoPaletDto>> = _tiposPalet.asStateFlow()
    
    private val _paletCreado = MutableStateFlow<com.example.sga.data.dto.traspasos.PaletDto?>(null)
    val paletCreado: StateFlow<com.example.sga.data.dto.traspasos.PaletDto?> = _paletCreado.asStateFlow()
    
    // Estado para diálogo de impresión obligatoria
    private val _mostrarDialogoImpresion = MutableStateFlow(false)
    val mostrarDialogoImpresion: StateFlow<Boolean> = _mostrarDialogoImpresion.asStateFlow()
    
    private val _paletParaImprimir = MutableStateFlow<com.example.sga.data.dto.traspasos.PaletDto?>(null)
    val paletParaImprimir: StateFlow<com.example.sga.data.dto.traspasos.PaletDto?> = _paletParaImprimir.asStateFlow()
    
    // Datos para añadir línea automáticamente después de imprimir
    private val _articuloParaLinea = MutableStateFlow<com.example.sga.data.dto.stock.ArticuloDto?>(null)
    val articuloParaLinea: StateFlow<com.example.sga.data.dto.stock.ArticuloDto?> = _articuloParaLinea.asStateFlow()
    
    private val _ubicacionParaLinea = MutableStateFlow<Pair<String, String>?>(null)
    val ubicacionParaLinea: StateFlow<Pair<String, String>?> = _ubicacionParaLinea.asStateFlow()
    
    private val _lineaParaAnadir = MutableStateFlow<com.example.sga.data.dto.ordenes.LineaOrdenTraspasoDetalleDto?>(null)
    val lineaParaAnadir: StateFlow<com.example.sga.data.dto.ordenes.LineaOrdenTraspasoDetalleDto?> = _lineaParaAnadir.asStateFlow()
    
    // Estado para diálogo de selección de cantidad
    private val _mostrarDialogoCantidad = MutableStateFlow(false)
    val mostrarDialogoCantidad: StateFlow<Boolean> = _mostrarDialogoCantidad.asStateFlow()
    
    // Estados para diálogo de ajuste de inventario
    private val _mostrarDialogoAjusteInventario = MutableStateFlow(false)
    val mostrarDialogoAjusteInventario: StateFlow<Boolean> = _mostrarDialogoAjusteInventario.asStateFlow()
    
    private val _cantidadEncontrada = MutableStateFlow("")
    val cantidadEncontrada: StateFlow<String> = _cantidadEncontrada.asStateFlow()
    
    private val _lineaParaAjuste = MutableStateFlow<LineaOrdenTraspasoDetalleDto?>(null)
    val lineaParaAjuste: StateFlow<LineaOrdenTraspasoDetalleDto?> = _lineaParaAjuste.asStateFlow()
    
    
    private val _paletSeleccionado = MutableStateFlow<com.example.sga.data.dto.traspasos.PaletDto?>(null)
    val paletSeleccionado: StateFlow<com.example.sga.data.dto.traspasos.PaletDto?> = _paletSeleccionado.asStateFlow()
    
    private val _lineasPalet = MutableStateFlow<Map<String, List<com.example.sga.data.dto.traspasos.LineaPaletDto>>>(emptyMap())
    val lineasPalet: StateFlow<Map<String, List<com.example.sga.data.dto.traspasos.LineaPaletDto>>> = _lineasPalet.asStateFlow()
    
    private val _resultadoStock = MutableStateFlow<List<com.example.sga.data.model.stock.Stock>>(emptyList())
    val resultadoStock: StateFlow<List<com.example.sga.data.model.stock.Stock>> = _resultadoStock.asStateFlow()
    
    private val _impresoras = MutableStateFlow<List<com.example.sga.data.dto.etiquetas.ImpresoraDto>>(emptyList())
    val impresoras: StateFlow<List<com.example.sga.data.dto.etiquetas.ImpresoraDto>> = _impresoras.asStateFlow()
    
    private val _flujoCreacionPaletActivo = MutableStateFlow(false)
    val flujoCreacionPaletActivo: StateFlow<Boolean> = _flujoCreacionPaletActivo.asStateFlow()
    
    // Setters para estados
    fun setOrdenes(ordenes: List<OrdenTraspasoDto>) {
        _ordenes.value = ordenes
        // Calcular órdenes activas automáticamente
        calcularOrdenesActivas(ordenes)
    }
    
    fun setOrdenesActivas(cantidad: Int) {
        _ordenesActivas.value = cantidad
    }
    
    private fun calcularOrdenesActivas(ordenes: List<OrdenTraspasoDto>) {
        val ordenesActivas = ordenes.count { orden ->
            orden.estado == "PENDIENTE" || orden.estado == "EN_PROCESO"
        }
        _ordenesActivas.value = ordenesActivas
    }
    
    fun setOrdenSeleccionada(orden: OrdenTraspasoDto?) {
        _ordenSeleccionada.value = orden
    }
    
    fun setLineaSeleccionada(linea: LineaOrdenTraspasoDetalleDto?) {
        _lineaSeleccionada.value = linea
    }
    
    fun seleccionarLineaConVerificacion(linea: LineaOrdenTraspasoDetalleDto, user: User) {
        viewModelScope.launch {
            val ordenId = _ordenSeleccionada.value?.idOrdenTraspaso ?: return@launch
            
            // Mostrar indicador de carga
            setCargando(true)
            
            // Polling inteligente: verificar cada 500ms hasta 10 segundos
            var intentos = 0
            val maxIntentos = 50 // 50 * 200ms = 10 segundos máximo
            
            while (intentos < maxIntentos) {
                // Polling adaptativo: más rápido al principio
                val delayTime = if (intentos < 10) 100L else 200L
                delay(delayTime)
                
                try {
                    val orden = logic.obtenerOrdenSilenciosa(ordenId)
                    if (orden != null) {
                        // DEBUG: Ver todas las líneas
                        orden.lineas.forEach { l -> 
                            android.util.Log.d("DEBUG_ORDEN", "Línea: ${l.idLineaOrdenTraspaso} - Estado: ${l.estado}")
                        }
                        val lineaActualizada = orden.lineas.find { it.idLineaOrdenTraspaso == linea.idLineaOrdenTraspaso }
                        
                        // Si se subdividió, volver a la lista
                        if (lineaActualizada?.estado == "SUBDIVIDIDO") {
                            android.util.Log.d("DEBUG_ORDEN", "🔄 SUBDIVIDIDO detectado! EJECUTANDO...")
                            setCargando(false) // Ocultar indicador de carga
                            setLineaSeleccionada(null)
                            _ordenSeleccionada.value = orden
                            _stockDisponible.value = emptyList()
                            setMensaje("ℹ️ La línea se ha subdividido. Seleccione la línea correcta.")
                            android.util.Log.d("DEBUG_ORDEN", "🔄 DESPUÉS DE setMensaje, SALIENDO...")
                            return@launch
                        }
                        
                        // Si cambió a EN_PROCESO, es línea normal (no se subdividió)
                        if (lineaActualizada?.estado == "EN_PROCESO") {
                            break
                        }
                    }
                } catch (e: Exception) {
                    // Si hay error en la API, continuar
                }
                
                intentos++
            }
            
            // Paso 2: Si NO se subdividió, seleccionar la línea normalmente
            setCargando(false) // Ocultar indicador de carga
            android.util.Log.d("VIEWMODEL_ORDEN", "✅ No se subdividió, continuando con la línea")
            setLineaSeleccionada(linea)
            consultarStockLinea(linea.idLineaOrdenTraspaso, user)
        }
    }
    
    fun setStockDisponible(stock: List<StockDisponibleDto>) {
        _stockDisponible.value = stock
    }
    
    fun setCargando(cargando: Boolean) {
        _cargando.value = cargando
        _estadoUI.value = if (cargando) EstadoUI.CARGANDO else EstadoUI.NORMAL
    }
    
    fun setError(error: String?) {
        _error.value = error
        _estadoUI.value = if (error != null) EstadoUI.ERROR else EstadoUI.NORMAL
    }
    
    fun setMensaje(mensaje: String?) {
        _mensaje.value = mensaje
        _estadoUI.value = if (mensaje != null) EstadoUI.EXITO else EstadoUI.NORMAL
    }
    
    fun setEstadoUI(estado: EstadoUI) {
        _estadoUI.value = estado
    }
    
    fun setFiltroEstado(estado: String?) {
        _filtroEstado.value = estado
    }
    
    fun setCantidadMovida(cantidad: String) {
        _cantidadMovida.value = cantidad
    }
    
    fun setUbicacionOrigenSeleccionada(stock: StockDisponibleDto?) {
        _ubicacionOrigenSeleccionada.value = stock
    }
    
    fun setUbicacionDestino(ubicacion: String) {
        _ubicacionDestino.value = ubicacion
    }
    
    fun setPaletDestino(palet: String) {
        _paletDestino.value = palet
    }
    
    fun setPaletListoParaUbicar(palet: String?) {
        _paletListoParaUbicar.value = palet
    }
    
    fun setCodigoGS1Palet(codigoGS1: String?) {
        _codigoGS1Palet.value = codigoGS1
    }
    
    fun setPaletsPendientes(palets: List<PaletPendienteDto>) {
        _paletsPendientes.value = palets
    }
    
    fun setAlmacenDestinoPalet(almacen: String) {
        _almacenDestinoPalet.value = almacen
    }
    
    fun setUbicacionDestinoPalet(ubicacion: String) {
        _ubicacionDestinoPalet.value = ubicacion
    }
    
    // Setters para flujo de escaneo
    fun setProcesoEscaneoActivo(activo: Boolean) {
        _procesoEscaneoActivo.value = activo
    }
    
    fun setEsperandoUbicacion(esperando: Boolean) {
        _esperandoUbicacion.value = esperando
    }
    
    fun setEsperandoArticulo(esperando: Boolean) {
        _esperandoArticulo.value = esperando
    }
    
    fun setUbicacionValidada(validada: Boolean) {
        _ubicacionValidada.value = validada
    }
    
    fun setArticuloValidado(articulo: com.example.sga.data.dto.stock.ArticuloDto?) {
        _articuloValidado.value = articulo
    }
    
    fun setStockSeleccionadoParaEscaneo(stock: StockDisponibleDto?) {
        _stockSeleccionadoParaEscaneo.value = stock
    }
    
    // Función para obtener órdenes filtradas (mostrando todas, incluyendo bloqueadas)
    fun getOrdenesFiltradas(): List<OrdenTraspasoDto> {
        val estadoFiltro = _filtroEstado.value
        return if (estadoFiltro == null) {
            _ordenes.value
        } else {
            _ordenes.value.filter { orden ->
                orden.lineas.any { linea -> linea.estado == estadoFiltro }
            }
        }
    }
    
    // Función para obtener líneas no bloqueadas de una orden
    fun getLineasNoBloqueadas(orden: OrdenTraspasoDto): List<LineaOrdenTraspasoDetalleDto> {
        return orden.lineas.filter { linea ->
            linea.estado != "BLOQUEADA"
        }
    }
    
    // Función para verificar si una orden está bloqueada
    fun estaOrdenBloqueada(orden: OrdenTraspasoDto): Boolean {
        return orden.estado == "BLOQUEADA" || orden.lineas.any { linea -> linea.estado == "BLOQUEADA" }
    }
    
    // Función para obtener líneas bloqueadas de una orden (para información)
    fun getLineasBloqueadas(orden: OrdenTraspasoDto): List<LineaOrdenTraspasoDetalleDto> {
        return orden.lineas.filter { linea -> linea.estado == "BLOQUEADA" }
    }
    
    // Función para obtener líneas del operario de una orden (excluyendo bloqueadas)
    fun getLineasOperario(orden: OrdenTraspasoDto, idOperario: String): List<LineaOrdenTraspasoDetalleDto> {
        return orden.lineas.filter { linea ->
            linea.idOperarioAsignado == idOperario.toInt() && linea.estado != "BLOQUEADA"
        }
    }
    
    // Función para limpiar formulario
    fun limpiarFormulario() {
        _cantidadMovida.value = ""
        _ubicacionOrigenSeleccionada.value = null
        _ubicacionDestino.value = ""
        _paletDestino.value = ""
        _stockDisponible.value = emptyList()
        _almacenDestinoPalet.value = ""
        _ubicacionDestinoPalet.value = ""
    }
    
    // Función para limpiar errores y mensajes
    fun limpiarMensajes() {
        _error.value = null
        _mensaje.value = null
    }
    
    // Métodos que llaman al Logic
    fun cargarOrdenes(user: User) {
        logic.listarOrdenes(user)
    }
    
    fun cargarOrdenDetallada(idOrden: String) {
        logic.cargarOrdenDetallada(idOrden)
    }
    
    fun iniciarOrden(idOrden: String, user: User) {
        logic.iniciarOrden(idOrden, user)
    }
    
    fun iniciarLinea(idLinea: String, user: User) {
        logic.iniciarLinea(idLinea, user)
    }
    
    fun consultarStockLinea(idLinea: String, user: User) {
        logic.consultarStockLinea(idLinea, user)
    }
    
    fun actualizarLineaConCantidad(
        idLinea: String, 
        cantidadMovida: Double, 
        paletDestino: String? = null,
        codigoAlmacenOrigen: String? = null,
        ubicacionOrigen: String? = null,
        onSuccess: () -> Unit
    ) {
        logic.actualizarLineaConCantidad(
            idLinea = idLinea,
            cantidadMovida = cantidadMovida,
            paletDestino = paletDestino,
            codigoAlmacenOrigen = codigoAlmacenOrigen ?: "",
            ubicacionOrigen = ubicacionOrigen ?: "",
            onSuccess = onSuccess,
            onError = { 
                android.util.Log.e("VIEWMODEL_ORDEN", "❌ Error en actualizarLineaConCantidad: $it")
                _error.value = it
                // IMPORTANTE: Cerrar el diálogo incluso si hay error
                onSuccess()
            }
        )
    }
    
    fun cargarStockDisponible(codigoEmpresa: Int, codigoArticulo: String, user: User) {
        logic.cargarStockDisponible(codigoEmpresa, codigoArticulo, user)
    }
    
    fun completarTraspaso(idLinea: String, user: User, dto: ActualizarLineaOrdenTraspasoDto) {
        logic.completarTraspaso(idLinea, user, dto)
    }
    
    fun verificarPaletsPendientes(ordenId: String, user: User) {
        logic.verificarPaletsPendientes(ordenId, user)
    }
    
    fun ubicarPalet(ordenId: String, paletDestino: String, dto: UbicarPaletDto, user: User) {
        logic.ubicarPalet(ordenId, paletDestino, dto, user)
    }
    
    fun getCodigoEmpresa(sessionViewModel: SessionViewModel): Int {
        return logic.getCodigoEmpresa(sessionViewModel)
    }
    
    fun crearActualizarLineaDto(
        linea: LineaOrdenTraspasoDetalleDto,
        stockSeleccionado: StockDisponibleDto,
        cantidadMovida: Double,
        ubicacionDestino: String,
        paletDestino: String,
        user: User
    ): ActualizarLineaOrdenTraspasoDto {
        return logic.crearActualizarLineaDto(
            linea, stockSeleccionado, cantidadMovida, ubicacionDestino, paletDestino, user
        )
    }
    
    // Funciones para el flujo de escaneo
    fun iniciarProcesoEscaneo(stockSeleccionado: StockDisponibleDto) {
        _stockSeleccionadoParaEscaneo.value = stockSeleccionado
        _procesoEscaneoActivo.value = true
        _esperandoUbicacion.value = true
        _esperandoArticulo.value = false
        _ubicacionValidada.value = false
        _articuloValidado.value = null
        _error.value = null
    }
    
    fun cancelarProcesoEscaneo() {
        _procesoEscaneoActivo.value = false
        _esperandoUbicacion.value = false
        _esperandoArticulo.value = false
        _ubicacionValidada.value = false
        _articuloValidado.value = null
        _stockSeleccionadoParaEscaneo.value = null
        _error.value = null
    }
    
    fun procesarCodigoEscaneado(
        codigo: String, 
        stockEsperado: StockDisponibleDto, 
        lineaSeleccionada: LineaOrdenTraspasoDetalleDto
    ) {
        if (!::logic.isInitialized) return
        
        logic.procesarEscaneoParaOrden(
            stockEsperado = stockEsperado,
            lineaSeleccionada = lineaSeleccionada,
            code = codigo,
            empresaId = sessionViewModel.empresaSeleccionada.value?.codigo?.toShort() ?: return,
            onUbicacionCorrecta = {
                _ubicacionValidada.value = true
                _esperandoUbicacion.value = false
                _esperandoArticulo.value = true
                _error.value = null
            },
            onUbicacionIncorrecta = { esperada, escaneada ->
                _error.value = "❌ Ubicación incorrecta.\nDebe ir a: $esperada\nHa escaneado: $escaneada"
            },
            onArticuloCorrecto = { articulo ->
                _articuloValidado.value = articulo
                _esperandoArticulo.value = false
                _error.value = null
                // Establecer cantidad por defecto
                _cantidadMovida.value = lineaSeleccionada.cantidadPlan.toString()
            },
            onArticuloIncorrecto = { esperado, escaneado ->
                _error.value = "❌ Artículo incorrecto.\nDebe escanear: $esperado\nHa escaneado: $escaneado"
            },
            onError = { error ->
                _error.value = error
            }
        )
    }
    
    fun completarLineaOrden(
        linea: LineaOrdenTraspasoDetalleDto,
        stockSeleccionado: StockDisponibleDto,
        user: User
    ) {
        val cantidad = _cantidadMovida.value.toDoubleOrNull()
        if (cantidad == null || cantidad <= 0) {
            _error.value = "Cantidad inválida"
            return
        }
        
        val dto = crearActualizarLineaDto(
            linea = linea,
            stockSeleccionado = stockSeleccionado,
            cantidadMovida = cantidad,
            ubicacionDestino = linea.codigoAlmacenDestino ?: "",
            paletDestino = "PAL-${System.currentTimeMillis()}", // Generar palet automático
            user = user
        )
        
        completarTraspaso(linea.idLineaOrdenTraspaso, user, dto)
        cancelarProcesoEscaneo()
    }
    
    fun limpiarSelecciones() {
        _ubicacionOrigenSeleccionada.value = null
        _stockDisponible.value = emptyList()
        cancelarProcesoEscaneo()
    }
    
    // Setters para funcionalidad de traspasos/palets
    fun setTiposPalet(tipos: List<com.example.sga.data.dto.traspasos.TipoPaletDto>) {
        _tiposPalet.value = tipos
    }
    
    fun setPaletCreado(palet: com.example.sga.data.dto.traspasos.PaletDto?) {
        _paletCreado.value = palet
    }
    
    fun setPaletSeleccionado(palet: com.example.sga.data.dto.traspasos.PaletDto?) {
        _paletSeleccionado.value = palet
    }
    
    fun setLineasPalet(paletId: String, lineas: List<com.example.sga.data.dto.traspasos.LineaPaletDto>) {
        _lineasPalet.value = _lineasPalet.value + (paletId to lineas)
    }
    
    fun setResultadoStock(stocks: List<com.example.sga.data.model.stock.Stock>) {
        _resultadoStock.value = stocks
    }
    
    fun setImpresoras(impresoras: List<com.example.sga.data.dto.etiquetas.ImpresoraDto>) {
        _impresoras.value = impresoras
    }
    
    fun setFlujoCreacionPaletActivo(activo: Boolean) {
        _flujoCreacionPaletActivo.value = activo
    }
    
    // Setters para diálogo de ajuste de inventario
    fun setMostrarDialogoAjusteInventario(mostrar: Boolean) {
        _mostrarDialogoAjusteInventario.value = mostrar
    }
    
    fun setCantidadEncontrada(cantidad: String) {
        _cantidadEncontrada.value = cantidad
    }
    
    fun setLineaParaAjuste(linea: LineaOrdenTraspasoDetalleDto?) {
        _lineaParaAjuste.value = linea
    }
    
    // Funciones para interactuar con TraspasosLogic
    fun cargarTiposPalet() {
        if (!::logic.isInitialized) return
        val traspasosLogic = com.example.sga.view.traspasos.TraspasosLogic()
        traspasosLogic.obtenerTiposPalet(
            onSuccess = { tipos -> setTiposPalet(tipos) },
            onError = { setError(it) }
        )
    }
    
    fun cargarImpresoras() {
        if (!::logic.isInitialized) return
        val traspasosLogic = com.example.sga.view.traspasos.TraspasosLogic()
        traspasosLogic.cargarImpresoras(
            onResult = { imp -> setImpresoras(imp) },
            onError = { setError(it) }
        )
    }
    
    fun crearPalet(dto: com.example.sga.data.dto.traspasos.PaletCrearDto, onSuccess: (com.example.sga.data.dto.traspasos.PaletDto) -> Unit) {
        if (!::logic.isInitialized) return
        val traspasosLogic = com.example.sga.view.traspasos.TraspasosLogic()
        setCargando(true)
        traspasosLogic.crearPalet(
            dto = dto,
            onSuccess = { palet ->
                setPaletCreado(palet)
                setCargando(false)
                onSuccess(palet)
            },
            onError = { 
                setError(it)
                setCargando(false)
            }
        )
    }
    
    fun obtenerLineasPalet(paletId: String) {
        android.util.Log.d("VIEWMODEL_ORDEN", "🔍 Obteniendo líneas del palet: $paletId")
        
        if (!::logic.isInitialized) {
            android.util.Log.e("VIEWMODEL_ORDEN", "❌ Logic no inicializado para obtener líneas")
            return
        }
        
        val traspasosLogic = com.example.sga.view.traspasos.TraspasosLogic()
        traspasosLogic.obtenerLineasPalet(
            idPalet = paletId,
            onSuccess = { lineas -> 
                android.util.Log.d("VIEWMODEL_ORDEN", "✅ Líneas obtenidas exitosamente: ${lineas.size} líneas")
                setLineasPalet(paletId, lineas) 
            },
            onError = { 
                android.util.Log.e("VIEWMODEL_ORDEN", "❌ Error al obtener líneas del palet: $it")
                setError(it) 
            }
        )
    }
    
    fun actualizarImpresoraSeleccionadaEnBD(
        nombre: String,
        sessionViewModel: com.example.sga.view.app.SessionViewModel
    ) {
        if (!::logic.isInitialized) return
        val traspasosLogic = com.example.sga.view.traspasos.TraspasosLogic()
        traspasosLogic.actualizarImpresoraSeleccionadaEnBD(nombre, sessionViewModel)
    }
    
    fun imprimirEtiquetaPalet(dto: LogImpresionDto) {
        if (!::logic.isInitialized) return
        val traspasosLogic = com.example.sga.view.traspasos.TraspasosLogic()
        traspasosLogic.imprimirEtiquetaPalet(
            dto = dto,
            onSuccess = {
                _error.value = null
            },
            onError = {
                _error.value = it
            }
        )
    }
    
    fun anadirLinea(
        idPalet: String,
        dto: LineaPaletCrearDto,
        onSuccess: () -> Unit
    ) {
        android.util.Log.d("VIEWMODEL_ORDEN", "🚀 anadirLinea llamado con palet: $idPalet")
        
        if (!::logic.isInitialized) {
            android.util.Log.e("VIEWMODEL_ORDEN", "❌ Logic no inicializado")
            _error.value = "Error: Lógica no inicializada"
            return
        }
        
        val traspasosLogic = com.example.sga.view.traspasos.TraspasosLogic()
        android.util.Log.d("VIEWMODEL_ORDEN", "📞 Llamando traspasosLogic.anadirLineaPalet")
        
        traspasosLogic.anadirLineaPalet(
            idPalet = idPalet,
            dto = dto,
            onSuccess = {
                android.util.Log.d("VIEWMODEL_ORDEN", "✅ Línea añadida al palet con éxito")
                
                // Paso final: actualizar la línea de la orden con la cantidad seleccionada
                _lineaParaAnadir.value?.let { linea ->
                    android.util.Log.d("VIEWMODEL_ORDEN", "📝 Actualizando línea de orden: ${linea.idLineaOrdenTraspaso} con cantidad: ${dto.cantidad}")
                    
                    // IMPORTANTE: Usar el codigoAlmacen y ubicacion del DTO (que viene del escaneo)
                    val paletDestino = _paletParaImprimir.value?.codigoPalet
                    val almacenEscaneado = dto.codigoAlmacen
                    val ubicacionEscaneada = dto.ubicacion ?: ""
                    android.util.Log.d("VIEWMODEL_ORDEN", "📦 Palet destino: $paletDestino")
                    android.util.Log.d("VIEWMODEL_ORDEN", "📍 Ubicación DESDE DTO: $almacenEscaneado/$ubicacionEscaneada")
                    
                    actualizarLineaConCantidad(
                        idLinea = linea.idLineaOrdenTraspaso,
                        cantidadMovida = dto.cantidad,
                        paletDestino = paletDestino,
                        codigoAlmacenOrigen = almacenEscaneado,
                        ubicacionOrigen = ubicacionEscaneada
                    ) {
                        android.util.Log.d("VIEWMODEL_ORDEN", "✅ Línea de orden actualizada, recargando orden")
                        
                        // Limpiar valores DESPUÉS de actualizar la línea
                        _paletParaImprimir.value = null
                        _articuloParaLinea.value = null
                        _ubicacionParaLinea.value = null
                        _lineaParaAnadir.value = null
                        
                        // Recargar la orden para mostrar las cantidades actualizadas
                        _ordenSeleccionada.value?.let { orden ->
                            android.util.Log.d("VIEWMODEL_ORDEN", "🔄 Recargando orden: ${orden.idOrdenTraspaso}")
                            logic.cargarOrdenDetallada(orden.idOrdenTraspaso)
                            
                            // Después de recargar, procesar la siguiente línea
                            // Usar un delay para asegurar que la orden se haya recargado
                            viewModelScope.launch {
                                delay(500) // 500ms de delay
                                procesarSiguienteLinea()
                            }
                        }
                        
                        // Actualizar líneas del palet
                        obtenerLineasPalet(idPalet)
                        
                        onSuccess()
                    }
                } ?: run {
                    android.util.Log.e("VIEWMODEL_ORDEN", "❌ No hay línea para actualizar")
                    onSuccess()
                }
            },
            onError = { 
                android.util.Log.e("VIEWMODEL_ORDEN", "❌ Error al añadir línea: $it")
                _error.value = it
                // Cerrar el diálogo de cantidad cuando hay error
                android.util.Log.d("VIEWMODEL_ORDEN", "🔒 Cerrando diálogo por error")
                cerrarDialogoCantidad()
            }
        )
    }
    
    fun cerrarPalet(
        id: String,
        usuarioId: Int,
        codigoAlmacen: String?,
        codigoEmpresa: Short,
        onSuccess: (String) -> Unit,
        onError: (String) -> Unit
    ) {
        if (!::logic.isInitialized) return
        val traspasosLogic = com.example.sga.view.traspasos.TraspasosLogic()
        traspasosLogic.cerrarPalet(
            idPalet = id,
            usuarioId = usuarioId,
            codigoAlmacen = codigoAlmacen,
            codigoEmpresa = codigoEmpresa,
            onSuccess = { mensaje ->
                // Limpiar estados cuando se cierra el palet
                setPaletSeleccionado(null)
                _lineasPalet.value = emptyMap()
                onSuccess(mensaje)
            },
            onError = onError
        )
    }
    
    fun abrirPalet(
        id: String,
        usuarioId: Int,
        onSuccess: () -> Unit,
        onError: (String) -> Unit
    ) {
        if (!::logic.isInitialized) return
        val traspasosLogic = com.example.sga.view.traspasos.TraspasosLogic()
        traspasosLogic.reabrirPalet(
            idPalet = id,
            usuarioId = usuarioId,
            onSuccess = onSuccess,
            onError = onError
        )
    }
    
    /**
     * Actualiza las líneas completadas de la orden con el IdTraspaso
     */
    private fun actualizarLineasCompletadasConTraspaso(idTraspaso: String) {
        if (!::logic.isInitialized) return
        
        val orden = _ordenSeleccionada.value
        if (orden == null) {
            android.util.Log.w("OrdenTraspasoViewModel", "⚠️ No hay orden seleccionada para actualizar líneas")
            return
        }
        
        // Obtener las líneas completadas de la orden
        val lineasCompletadas = orden.lineas.filter { it.completada && it.idTraspaso == null }
        
        if (lineasCompletadas.isEmpty()) {
            android.util.Log.d("OrdenTraspasoViewModel", "ℹ️ No hay líneas completadas sin IdTraspaso para actualizar")
            return
        }
        
        android.util.Log.d("OrdenTraspasoViewModel", "📝 Actualizando ${lineasCompletadas.size} líneas completadas con IdTraspaso: $idTraspaso")
        
        // Actualizar cada línea completada usando la función del Logic
        lineasCompletadas.forEach { linea ->
            val dto = ActualizarLineaOrdenTraspasoDto(
                estado = linea.estado,
                cantidadMovida = linea.cantidadMovida,
                completada = linea.completada,
                idOperarioAsignado = linea.idOperarioAsignado,
                fechaInicio = linea.fechaInicio,
                fechaFinalizacion = linea.fechaFinalizacion,
                idTraspaso = idTraspaso, // ✅ AQUÍ ESTÁ LA CLAVE
                fechaCaducidad = linea.fechaCaducidad,
                codigoAlmacenOrigen = linea.codigoAlmacenOrigen,
                ubicacionOrigen = linea.ubicacionOrigen,
                partida = linea.partida,
                paletOrigen = linea.paletOrigen,
                codigoAlmacenDestino = linea.codigoAlmacenDestino,
                ubicacionDestino = linea.ubicacionDestino,
                paletDestino = linea.paletDestino
            )
            
            logic.actualizarLineaConIdTraspaso(
                dto = dto,
                idLinea = linea.idLineaOrdenTraspaso,
                onSuccess = {
                    android.util.Log.d("OrdenTraspasoViewModel", "✅ Línea ${linea.idLineaOrdenTraspaso} actualizada con IdTraspaso")
                },
                onError = { error ->
                    android.util.Log.e("OrdenTraspasoViewModel", "❌ Error al actualizar línea ${linea.idLineaOrdenTraspaso}: $error")
                }
            )
        }
    }
    
    fun completarTraspaso(
        id: String,
        codigoAlmacenDestino: String,
        ubicacionDestino: String,
        usuarioId: Int,
        paletId: String?,
        onSuccess: () -> Unit,
        onError: (String) -> Unit
    ) {
        if (!::logic.isInitialized) return
        val traspasosLogic = com.example.sga.view.traspasos.TraspasosLogic()
        val fechaFinalizacion = java.time.LocalDateTime.now().toString()

        val dto = CompletarTraspasoDto(
            codigoAlmacenDestino = codigoAlmacenDestino,
            ubicacionDestino = ubicacionDestino,
            fechaFinalizacion = fechaFinalizacion,
            usuarioFinalizacionId = usuarioId
        )

        traspasosLogic.completarTraspaso(
            idTraspaso = id,
            dto = dto,
            paletId = paletId,
            onSuccess = {
                // Después de completar el traspaso, actualizar las líneas completadas con el IdTraspaso
                actualizarLineasCompletadasConTraspaso(id)
                // NO limpiar paletSeleccionado - el operario debe cerrarlo manualmente
                // Solo limpiar estados de creación de palet
                _paletCreado.value = null
                onSuccess()
            },
            onError = onError
        )
    }
    
    fun ubicarPaletEnOrden(
        ordenId: String,
        paletDestino: String,
        codigoAlmacenDestino: String,
        ubicacionDestino: String,
        onSuccess: () -> Unit,
        onError: (String) -> Unit
    ) {
        if (!::logic.isInitialized) return
        logic.ubicarPaletEnOrden(
            ordenId = ordenId,
            paletDestino = paletDestino,
            codigoAlmacenDestino = codigoAlmacenDestino,
            ubicacionDestino = ubicacionDestino,
            onSuccess = onSuccess,
            onError = onError
        )
    }
    
    fun actualizarLineaConIdTraspaso(
        idLinea: String,
        idTraspaso: String,
        onSuccess: () -> Unit,
        onError: (String) -> Unit
    ) {
        if (!::logic.isInitialized) return
        logic.actualizarLineaConTraspaso(
            idLinea = idLinea,
            idTraspaso = idTraspaso,
            onSuccess = onSuccess,
            onError = onError
        )
    }
    
    fun activarDialogoImpresion(
        palet: com.example.sga.data.dto.traspasos.PaletDto,
        articulo: com.example.sga.data.dto.stock.ArticuloDto,
        ubicacion: Pair<String, String>,
        linea: com.example.sga.data.dto.ordenes.LineaOrdenTraspasoDetalleDto
    ) {
        android.util.Log.d("VIEWMODEL_IMPRESION", "🖨️ Activando diálogo para palet: ${palet.codigoPalet}")
        _paletParaImprimir.value = palet
        _articuloParaLinea.value = articulo
        _ubicacionParaLinea.value = ubicacion
        _lineaParaAnadir.value = linea
        _mostrarDialogoImpresion.value = true
        android.util.Log.d("VIEWMODEL_IMPRESION", "📦 Estado ViewModel: palet=${_paletParaImprimir.value?.codigoPalet}, dialogo=${_mostrarDialogoImpresion.value}")
    }
    
    fun cerrarDialogoImpresion() {
        android.util.Log.d("VIEWMODEL_IMPRESION", "🔒 Cerrando diálogo de impresión")
        _mostrarDialogoImpresion.value = false
        // Activar diálogo de cantidad ANTES de limpiar datos
        _mostrarDialogoCantidad.value = true
        android.util.Log.d("VIEWMODEL_IMPRESION", "📊 Activando diálogo de cantidad - valor: ${_mostrarDialogoCantidad.value}")
        // NO limpiar datos aquí - se necesitan para el diálogo de cantidad
    }
    
    fun cerrarDialogoCantidad() {
        android.util.Log.d("VIEWMODEL_IMPRESION", "🔒 Cerrando diálogo de cantidad")
        try {
            _mostrarDialogoCantidad.value = false
            android.util.Log.d("VIEWMODEL_IMPRESION", "✅ Diálogo marcado como cerrado - valor: ${_mostrarDialogoCantidad.value}")
            
            // NO limpiar estos valores aquí - se necesitan para actualizar la línea de orden
            // Se limpiarán después de que se complete la actualización
            // _paletParaImprimir.value = null
            // _articuloParaLinea.value = null
            // _ubicacionParaLinea.value = null
            // _lineaParaAnadir.value = null
            
            android.util.Log.d("VIEWMODEL_IMPRESION", "🎉 Diálogo de cantidad cerrado exitosamente")
        } catch (e: Exception) {
            android.util.Log.e("VIEWMODEL_IMPRESION", "❌ Error al cerrar diálogo: ${e.message}", e)
            _error.value = "Error interno al cerrar diálogo: ${e.message}"
        }
    }
    
    fun activarDialogoCantidadDirecto(
        palet: com.example.sga.data.dto.traspasos.PaletDto,
        articulo: com.example.sga.data.dto.stock.ArticuloDto,
        ubicacion: Pair<String, String>,
        linea: com.example.sga.data.dto.ordenes.LineaOrdenTraspasoDetalleDto
    ) {
        android.util.Log.d("VIEWMODEL_CANTIDAD", "🎯 Activando diálogo de cantidad directamente (sin impresión)")
        _paletParaImprimir.value = palet
        _articuloParaLinea.value = articulo
        _ubicacionParaLinea.value = ubicacion
        _lineaParaAnadir.value = linea
        _mostrarDialogoCantidad.value = true
        android.util.Log.d("VIEWMODEL_CANTIDAD", "📦 Estado: palet=${palet.codigoPalet}, dialogo=${_mostrarDialogoCantidad.value} - ACTIVANDO DIÁLOGO")
    }
    
    fun eliminarLineaPalet(
        idLinea: String,
        usuarioId: Int,
        paletId: String
    ) {
        val traspasosLogic = com.example.sga.view.traspasos.TraspasosLogic()
        traspasosLogic.eliminarLineaPalet(
            idLinea = idLinea,
            usuarioId = usuarioId,
            onSuccess = {
                // Refrescar las líneas del palet después de eliminar
                obtenerLineasPalet(paletId)
                setMensaje("Línea eliminada del palet")
            },
            onError = { error ->
                setMensaje("Error al eliminar línea: $error")
            }
        )
    }
    
    fun procesarSiguienteLinea() {
        val ordenActual = _ordenSeleccionada.value
        val usuarioActual = sessionViewModel.user.value
        
        if (ordenActual != null && usuarioActual != null) {
            // Obtener líneas pendientes del usuario
            val lineasDelOperario = ordenActual.lineas.filter { linea ->
                linea.idOperarioAsignado == usuarioActual.id.toInt() && !linea.completada
            }.sortedWith(compareBy { linea ->
                when (linea.estado) {
                    "EN_PROCESO" -> 1  // Primero las que ya están empezadas
                    "PENDIENTE" -> 2   // Luego las pendientes
                    "SUBDIVIDIDO" -> 3 // Las subdivididas al final (por ahora)
                    else -> 4
                }
            })
            
            if (lineasDelOperario.isNotEmpty()) {
                // Seleccionar automáticamente la siguiente línea
                val siguienteLinea = lineasDelOperario.first()
                setLineaSeleccionada(siguienteLinea)
                
                // Cargar stock de la nueva línea PRIMERO para mostrar ubicación
                consultarStockLinea(siguienteLinea.idLineaOrdenTraspaso, usuarioActual)
                
                // Limpiar solo el artículo validado (mantener ubicación y palet si están)
                _articuloValidado.value = null
                
                setMensaje("Siguiente artículo seleccionado: ${siguienteLinea.codigoArticulo}")
            } else {
                // No hay más líneas pendientes
                setMensaje("¡Trabajo completado! No hay más artículos pendientes.")
                // Limpiar todo el estado
                _lineaSeleccionada.value = null
                _stockDisponible.value = emptyList()
                _articuloValidado.value = null
                _ubicacionValidada.value = false
            }
        }
    }
    
    /**
     * Función mejorada para actualizar línea con manejo robusto de errores
     */
    fun actualizarLineaOrdenTraspasoMejorado(
        idLinea: String,
        cantidadMovida: Double,
        paletDestino: String? = null,
        onSuccess: (String?) -> Unit = {},
        onError: (String) -> Unit = { setError(it) }
    ) {
        if (!::logic.isInitialized) {
            setError("Error: Lógica no inicializada")
            return
        }
        
        // Asignar automáticamente el paletDestino desde el palet activo
        val paletDestinoFinal = paletDestino ?: _paletSeleccionado.value?.codigoPalet
        
        if (paletDestinoFinal == null) {
            setError("❌ No hay palet activo seleccionado. Debe crear o seleccionar un palet antes de añadir artículos.")
            return
        }
        
        android.util.Log.d("VIEWMODEL_PALET", "📦 Asignando paletDestino automáticamente: $paletDestinoFinal")
        
        setEstadoUI(EstadoUI.CARGANDO)
        setError(null)
        
        viewModelScope.launch {
            try {
                val response = logic.actualizarLineaOrdenTraspaso(
                    idLinea = idLinea,
                    cantidadMovida = cantidadMovida,
                    paletDestino = paletDestinoFinal
                )
                
                if (response.isSuccess && response.getOrNull()?.success == true) {
                    // Continuar flujo normal
                    setEstadoUI(EstadoUI.EXITO)
                    val paletListoParaUbicar = response.getOrNull()?.paletListoParaUbicar
                    setPaletListoParaUbicar(paletListoParaUbicar)
                    onSuccess(paletListoParaUbicar)
                    setMensaje("Línea actualizada correctamente")
                } else {
                    // Manejar error
                    val errorMessage = response.getOrNull()?.mensaje ?: "Error desconocido"
                    mostrarErrorCrearMateria(errorMessage)
                    onError(errorMessage)
                }
            } catch (e: Exception) {
                val errorMsg = "Error de conexión: ${e.message}"
                setError(errorMsg)
                onError(errorMsg)
            } finally {
                setEstadoUI(EstadoUI.NORMAL)
            }
        }
    }
    
    fun mostrarErrorCrearMateria(mensaje: String) {
        android.util.Log.d("VIEWMODEL_ERROR", "🚨 Mostrando error crear materia/bloqueo: $mensaje")
        
        // Limpiar estado anterior antes de mostrar nuevo error
        _detallesErrorCrearMateria.value = null
        
        // Detectar si es un error de diferencia de stock
        val regex = "Sistema: ([\\d,]+(?:\\.[\\d]+)?), Encontrado: ([\\d,]+(?:\\.[\\d]+)?), Diferencia: \\+([\\d,]+(?:\\.[\\d]+)?)"
        val match = regex.toRegex().find(mensaje)
        
        if (match != null) {
            // Función para convertir número con comas a double correctamente
            fun parseNumber(numberStr: String): Double {
                return numberStr.replace(",", ".").toDouble()
            }
            
            // Extraer cantidades y redondear a 2 decimales
            val stockSistema = String.format("%.2f", parseNumber(match.groupValues[1]))
            val stockEncontrado = String.format("%.2f", parseNumber(match.groupValues[2]))
            val diferencia = String.format("%.2f", parseNumber(match.groupValues[3]))
            
            _detallesErrorCrearMateria.value = """
                ❌ Bloqueo automático
                
                Sistema: $stockSistema
                Encontrado: $stockEncontrado
                Diferencia: $diferencia
                
                Esta artículo requiere supervisión para continuar.
                ⚠️La línea ha sido bloqueada automáticamente.
            """.trimIndent()
        } else {
            // Mostrar mensaje genérico si no se puede parsear
            _detallesErrorCrearMateria.value = """
                ❌ Bloqueo automático
                
                $mensaje
                
                ⚠️ Este ajuste requiere supervisión para continuar.
            """.trimIndent()
        }
        
        // Activar el diálogo
        _mostrarDialogoErrorCrearMateria.value = true
        android.util.Log.d("VIEWMODEL_ERROR", "✅ Error asignado y diálogo activado: ${_detallesErrorCrearMateria.value}")
        setEstadoUI(EstadoUI.ERROR)
    }
    
    /**
     * Cerrar diálogo de error crear materia
     */
    fun cerrarDialogoErrorCrearMateria() {
        android.util.Log.d("VIEWMODEL_ERROR", "🔒 Cerrando diálogo error crear materia")
        _mostrarDialogoErrorCrearMateria.value = false
        _detallesErrorCrearMateria.value = null
        
        // También cerrar el diálogo de cantidad y volver a selección de líneas
        android.util.Log.d("VIEWMODEL_ERROR", "🔒 Cerrando también diálogo de cantidad")
        cerrarDialogoCantidad()
        
        // Limpiar línea seleccionada para volver a la selección
        android.util.Log.d("VIEWMODEL_ERROR", "🔒 Limpiando línea seleccionada")
        _lineaSeleccionada.value = null
        
        // Recargar la orden para reflejar los cambios
        _ordenSeleccionada.value?.let { orden ->
            android.util.Log.d("VIEWMODEL_ERROR", "🔄 Recargando orden: ${orden.idOrdenTraspaso}")
            if (::logic.isInitialized) {
                logic.cargarOrdenDetallada(orden.idOrdenTraspaso)
            }
        }
        
        setEstadoUI(EstadoUI.NORMAL)
    }
    
    /**
     * Validación previa opcional para verificar cantidades antes de enviar
     */
    fun validarCantidadAntesDeEnviar(
        cantidadIngresada: Double,
        stockSistema: Double
    ): Boolean {
        if (!::logic.isInitialized) {
            setError("Error: Lógica no inicializada")
            return false
        }
        
        return logic.validarCantidadAntesDeEnviar(
            cantidadIngresada = cantidadIngresada,
            stockSistema = stockSistema
        ) { mensajeAdvertencia ->
            _mensajeAdvertenciaCantidad.value = mensajeAdvertencia
            _mostrarAdvertenciaCantidad.value = true
        }
    }
    
    /**
     * Cerrar diálogo de advertencia de cantidad
     */
    fun cerrarDialogoAdvertenciaCantidad() {
        _mostrarAdvertenciaCantidad.value = false
        _mensajeAdvertenciaCantidad.value = null
    }
    
    /**
     * Confirmar advertencia de cantidad y continuar
     */
    fun confirmarAdvertenciaCantidad(
        idLinea: String,
        cantidadMovida: Double,
        paletDestino: String? = null
    ) {
        cerrarDialogoAdvertenciaCantidad()
        actualizarLineaOrdenTraspasoMejorado(idLinea, cantidadMovida, paletDestino)
    }
    
    /**
     * Desbloquear línea (solo supervisores)
     */
    fun desbloquearLinea(
        idLinea: String,
        onSuccess: () -> Unit = {},
        onError: (String) -> Unit = { setError(it) }
    ) {
        if (!::logic.isInitialized) {
            setError("Error: Lógica no inicializada")
            return
        }
        
        setEstadoUI(EstadoUI.CARGANDO)
        setError(null)
        
        viewModelScope.launch {
            try {
                val response = logic.desbloquearLinea(idLinea)
                
                if (response.isSuccess) {
                    setEstadoUI(EstadoUI.EXITO)
                    setMensaje("Línea desbloqueada correctamente")
                    onSuccess()
                    
                    // Recargar la orden para reflejar el desbloqueo
                    _ordenSeleccionada.value?.let { orden ->
                        logic.cargarOrdenDetallada(orden.idOrdenTraspaso)
                    }
                } else {
                    val error = response.exceptionOrNull()?.message ?: "Error desconocido"
                    setError("Error al desbloquear línea: $error")
                    onError(error)
                }
            } catch (e: Exception) {
                val errorMsg = "Error de conexión: ${e.message}"
                setError(errorMsg)
                onError(errorMsg)
            } finally {
                setEstadoUI(EstadoUI.NORMAL)
            }
        }
    }
    
    /**
     * Activar diálogo de ajuste de inventario
     */
    fun activarDialogoAjusteInventario(linea: LineaOrdenTraspasoDetalleDto) {
        android.util.Log.d("VIEWMODEL_AJUSTE", "🔧 Activando diálogo de ajuste para línea: ${linea.idLineaOrdenTraspaso}")
        _lineaParaAjuste.value = linea
        _cantidadEncontrada.value = ""
        _mostrarDialogoAjusteInventario.value = true
    }
    
    /**
     * Cerrar diálogo de ajuste de inventario
     */
    fun cerrarDialogoAjusteInventario() {
        android.util.Log.d("VIEWMODEL_AJUSTE", "🔒 Cerrando diálogo de ajuste")
        _mostrarDialogoAjusteInventario.value = false
        _lineaParaAjuste.value = null
        _cantidadEncontrada.value = ""
    }
    
    /**
     * Actualizar stock disponible con la cantidad del ajuste
     */
    private fun actualizarStockDisponibleConAjuste(cantidadEncontrada: Double) {
        val stockActual = _stockDisponible.value
        if (stockActual.isNotEmpty()) {
            // Actualizar la cantidad disponible con la cantidad encontrada en el ajuste
            val stockActualizado = stockActual.map { stock ->
                stock.copy(cantidadDisponible = cantidadEncontrada)
            }
            _stockDisponible.value = stockActualizado
            
            android.util.Log.d("VIEWMODEL_AJUSTE", "📊 Stock actualizado: ${cantidadEncontrada} unidades")
        }
    }
    
    /**
     * Confirmar ajuste de inventario
     */
    fun confirmarAjusteInventario(
        onSuccess: () -> Unit = {},
        onError: (String) -> Unit = { setError(it) }
    ) {
        val linea = _lineaParaAjuste.value
        val cantidadStr = _cantidadEncontrada.value
        
        if (linea == null) {
            setError("No hay línea seleccionada para ajustar")
            return
        }
        
        val cantidadEncontrada = cantidadStr.toDoubleOrNull()
        if (cantidadEncontrada == null || cantidadEncontrada < 0) {
            setError("Cantidad encontrada inválida")
            return
        }
        
        if (!::logic.isInitialized) {
            setError("Error: Lógica no inicializada")
            return
        }
        
        setEstadoUI(EstadoUI.CARGANDO)
        setError(null)
        
        viewModelScope.launch {
            try {
                android.util.Log.d("VIEWMODEL_AJUSTE", "📡 Enviando ajuste: línea=${linea.idLineaOrdenTraspaso}, cantidad=$cantidadEncontrada")
                
                val response = logic.ajustarLineaOrdenTraspaso(
                    idLinea = linea.idLineaOrdenTraspaso,
                    cantidadEncontrada = cantidadEncontrada
                )
                
                if (response.isSuccess) {
                    val ajusteResponse = response.getOrNull()
                    if (ajusteResponse?.success == true) {
                        setEstadoUI(EstadoUI.EXITO)
                        setMensaje(ajusteResponse.mensaje)
                        
                        // Cerrar diálogo
                        cerrarDialogoAjusteInventario()
                        
                        if (ajusteResponse.requiereSupervision == false) {
                            // CASO 1: Ajuste dentro de límites del operario
                            // - Mensaje: "Ajuste aplicado. Diferencia: -5.00"
                            // - Success: true, RequiereSupervision: false
                            // - La línea NO se bloquea, el operario puede continuar
                            // - Actualizar stock con la cantidad encontrada
                            actualizarStockDisponibleConAjuste(cantidadEncontrada)
                            android.util.Log.d("VIEWMODEL_AJUSTE", "✅ Ajuste dentro de límites - operario puede continuar")
                            
                        } else {
                            // CASO 2: Ajuste que supera límites del operario
                            // - Mensaje: "Ajuste enviado a supervisión. Diferencia: -15.00. Puedes continuar trabajando."
                            // - Success: true, RequiereSupervision: true
                            // - La línea NO se bloquea, el operario puede continuar trabajando
                            // - Recargar orden para obtener datos actualizados
                            _ordenSeleccionada.value?.let { orden ->
                                logic.cargarOrdenDetallada(orden.idOrdenTraspaso)
                            }
                            android.util.Log.d("VIEWMODEL_AJUSTE", "⚠️ Ajuste enviado a supervisión - operario puede continuar")
                            
                            // Mostrar mensaje de supervisión por más tiempo (no se limpia automáticamente)
                            viewModelScope.launch {
                                delay(3000) // Mostrar por 3 segundos
                                limpiarMensajes()
                            }
                        }
                        
                        onSuccess()
                    } else {
                        // CASO 3: Crear materia (diferencia positiva) - LÍNEA BLOQUEADA
                        // - Mensaje: "No se puede crear materia. Sistema: 10, Encontrado: 15, Diferencia: +5.00. Línea y orden bloqueadas."
                        // - Success: false, RequiereSupervision: true
                        // - La línea SÍ se bloquea, el operario NO puede continuar
                        val mensajeError = ajusteResponse?.mensaje ?: "Error desconocido del servidor"
                        val estadoLinea = ajusteResponse?.estadoLinea
                        
                        android.util.Log.d("VIEWMODEL_AJUSTE", "🚫 CASO 3: Línea bloqueada - operario NO puede continuar")
                        android.util.Log.d("VIEWMODEL_AJUSTE", "⚠️ [AJUSTE] Respuesta con success=false: mensaje=$mensajeError, estadoLinea=$estadoLinea, requiereSupervision=${ajusteResponse?.requiereSupervision}")
                        
                        if (ajusteResponse?.requiereSupervision == true) {
                            // Mostrar mensaje especial para supervisión requerida
                            mostrarErrorCrearMateria(mensajeError)
                        } else if (estadoLinea == "BLOQUEADA") {
                            // Línea bloqueada por diferencia de stock
                            mostrarErrorCrearMateria(mensajeError)
                        } else {
                            // Otros errores
                            setError(mensajeError)
                        }
                        
                        // Cerrar diálogo también en caso de error
                        cerrarDialogoAjusteInventario()
                        onError(mensajeError)
                    }
                } else {
                    val error = response.exceptionOrNull()?.message ?: "Error desconocido"
                    setError("Error al ajustar inventario: $error")
                    
                    // Cerrar diálogo también en caso de error de conexión
                    cerrarDialogoAjusteInventario()
                    onError(error)
                }
            } catch (e: Exception) {
                val errorMsg = "Error de conexión: ${e.message}"
                setError(errorMsg)
                
                // Cerrar diálogo también en caso de excepción
                cerrarDialogoAjusteInventario()
                onError(errorMsg)
            } finally {
                setEstadoUI(EstadoUI.NORMAL)
            }
        }
    }
    
    /**
     * Verificar si una línea puede ser ajustada (estado EN_PROCESO o PENDIENTE)
     */
    fun puedeAjustarInventario(linea: LineaOrdenTraspasoDetalleDto): Boolean {
        return linea.estado == "EN_PROCESO" || linea.estado == "PENDIENTE"
    }
    
    /**
     * Deseleccionar el palet actual para permitir crear uno nuevo
     */
    fun deseleccionarPalet() {
        android.util.Log.d("VIEWMODEL_PALET", "🗑️ Deseleccionando palet actual")
        _paletSeleccionado.value = null
        _lineasPalet.value = emptyMap()
    }
    
}

