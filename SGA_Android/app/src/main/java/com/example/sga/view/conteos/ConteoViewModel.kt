package com.example.sga.view.conteos

import androidx.lifecycle.ViewModel
import com.example.sga.data.model.conteos.*
import com.example.sga.data.model.user.User
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

class ConteoViewModel : ViewModel() {

    // Estados para listado de órdenes
    private val _ordenes = MutableStateFlow<List<OrdenConteo>>(emptyList())
    val ordenes: StateFlow<List<OrdenConteo>> = _ordenes

    private val _ordenSeleccionada = MutableStateFlow<OrdenConteo?>(null)
    val ordenSeleccionada: StateFlow<OrdenConteo?> = _ordenSeleccionada

    // Estados para lecturas
    private val _lecturasPendientes = MutableStateFlow<List<LecturaPendiente>>(emptyList())
    val lecturasPendientes: StateFlow<List<LecturaPendiente>> = _lecturasPendientes

    private val _lecturaActual = MutableStateFlow<LecturaPendiente?>(null)
    val lecturaActual: StateFlow<LecturaPendiente?> = _lecturaActual

    // Estados para resultados
    private val _resultados = MutableStateFlow<List<ResultadoConteo>>(emptyList())
    val resultados: StateFlow<List<ResultadoConteo>> = _resultados

    // Estados de UI
    private val _cargando = MutableStateFlow(false)
    val cargando: StateFlow<Boolean> = _cargando

    private val _error = MutableStateFlow<String?>(null)
    
    // Estado para modo lectura manual
    private val _modoLecturaManual = MutableStateFlow(false)
    val modoLecturaManual: StateFlow<Boolean> = _modoLecturaManual
    val error: StateFlow<String?> = _error

    private val _mensaje = MutableStateFlow<String?>(null)
    val mensaje: StateFlow<String?> = _mensaje

    // Estados para formularios
    private val _cantidadContada = MutableStateFlow("")
    val cantidadContada: StateFlow<String> = _cantidadContada

    private val _comentario = MutableStateFlow("")
    val comentario: StateFlow<String> = _comentario

    private val _codigoOperario = MutableStateFlow("")
    val codigoOperario: StateFlow<String> = _codigoOperario

    // Estados de navegación
    private val _mostrarDialogoCrearOrden = MutableStateFlow(false)
    val mostrarDialogoCrearOrden: StateFlow<Boolean> = _mostrarDialogoCrearOrden

    private val _mostrarDialogoAsignarOperario = MutableStateFlow(false)
    val mostrarDialogoAsignarOperario: StateFlow<Boolean> = _mostrarDialogoAsignarOperario

    private val _ordenParaAsignar = MutableStateFlow<OrdenConteo?>(null)
    val ordenParaAsignar: StateFlow<OrdenConteo?> = _ordenParaAsignar.asStateFlow()

    // Estado para conteos activos
    private val _conteosActivos = MutableStateFlow(0)
    val conteosActivos: StateFlow<Int> = _conteosActivos

    // Estado del usuario
    private val _user = MutableStateFlow<User?>(null)
    val user: StateFlow<User?> = _user

    // Estados de escaneo
    private val _estadoEscaneo = MutableStateFlow<EstadoEscaneoConteo>(EstadoEscaneoConteo.Inactivo)
    val estadoEscaneo: StateFlow<EstadoEscaneoConteo> = _estadoEscaneo.asStateFlow()

    // Datos del escaneo actual
    private val _ubicacionEscaneada = MutableStateFlow<String?>(null)
    val ubicacionEscaneada: StateFlow<String?> = _ubicacionEscaneada.asStateFlow()

    private val _articuloEscaneado = MutableStateFlow<LecturaPendiente?>(null)
    val articuloEscaneado: StateFlow<LecturaPendiente?> = _articuloEscaneado.asStateFlow()

    // Contador de lecturas completadas
    private val _lecturasCompletadas = MutableStateFlow(0)
    val lecturasCompletadas: StateFlow<Int> = _lecturasCompletadas.asStateFlow()

    // Estados para diálogos
    private val _mostrarDialogoConfirmacionArticulo = MutableStateFlow(false)
    val mostrarDialogoConfirmacionArticulo: StateFlow<Boolean> = _mostrarDialogoConfirmacionArticulo.asStateFlow()

    private val _articuloParaConfirmar = MutableStateFlow<LecturaPendiente?>(null)
    val articuloParaConfirmar: StateFlow<LecturaPendiente?> = _articuloParaConfirmar.asStateFlow()

    // Estados para selección de artículos múltiples
    private val _articulosFiltrados = MutableStateFlow<List<com.example.sga.data.dto.stock.ArticuloDto>>(emptyList())
    val articulosFiltrados: StateFlow<List<com.example.sga.data.dto.stock.ArticuloDto>> = _articulosFiltrados.asStateFlow()

    private val _mostrarDialogoSeleccionArticulo = MutableStateFlow(false)
    val mostrarDialogoSeleccionArticulo: StateFlow<Boolean> = _mostrarDialogoSeleccionArticulo.asStateFlow()

    // Estado para indicar que el conteo está completado
    private val _conteoCompletado = MutableStateFlow(false)
    val conteoCompletado: StateFlow<Boolean> = _conteoCompletado.asStateFlow()

    // Estados para selección de palets
    private val _paletsDisponibles = MutableStateFlow<List<PaletDisponible>>(emptyList())
    val paletsDisponibles: StateFlow<List<PaletDisponible>> = _paletsDisponibles.asStateFlow()

    private val _mostrarDialogoSeleccionPalet = MutableStateFlow(false)
    val mostrarDialogoSeleccionPalet: StateFlow<Boolean> = _mostrarDialogoSeleccionPalet.asStateFlow()

    private val _paletSeleccionado = MutableStateFlow<PaletDisponible?>(null)
    val paletSeleccionado: StateFlow<PaletDisponible?> = _paletSeleccionado.asStateFlow()


    // Setters
    fun setOrdenes(lista: List<OrdenConteo>) {
        _ordenes.value = lista
    }

    fun setOrdenSeleccionada(orden: OrdenConteo?) {
        _ordenSeleccionada.value = orden
    }

    fun setLecturasPendientes(lista: List<LecturaPendiente>) {
        _lecturasPendientes.value = lista
    }

    fun setLecturaActual(lectura: LecturaPendiente?) {
        _lecturaActual.value = lectura
    }

    fun setResultados(lista: List<ResultadoConteo>) {
        _resultados.value = lista
    }

    fun setCargando(valor: Boolean) {
        _cargando.value = valor
    }

    fun setError(mensaje: String?) {
        _error.value = mensaje
    }

    fun setMensaje(mensaje: String?) {
        _mensaje.value = mensaje
    }

    fun setCantidadContada(cantidad: String) {
        _cantidadContada.value = cantidad
    }

    fun setComentario(comentario: String) {
        _comentario.value = comentario
    }

    fun setCodigoOperario(codigo: String) {
        _codigoOperario.value = codigo
    }

    fun setMostrarDialogoCrearOrden(valor: Boolean) {
        _mostrarDialogoCrearOrden.value = valor
    }

    fun setMostrarDialogoAsignarOperario(valor: Boolean) {
        _mostrarDialogoAsignarOperario.value = valor
    }

    fun setOrdenParaAsignar(orden: OrdenConteo?) {
        _ordenParaAsignar.value = orden
    }

    fun setConteosActivos(valor: Int) {
        _conteosActivos.value = valor
    }

    fun setUser(user: User?) {
        _user.value = user
    }

    // Setters para estados de escaneo
    fun setEstadoEscaneo(estado: EstadoEscaneoConteo) {
        _estadoEscaneo.value = estado
    }

    fun setUbicacionEscaneada(ubicacion: String?) {
        _ubicacionEscaneada.value = ubicacion
    }

    fun setArticuloEscaneado(articulo: LecturaPendiente?) {
        _articuloEscaneado.value = articulo
    }

    fun setLecturasCompletadas(cantidad: Int) {
        _lecturasCompletadas.value = cantidad
    }

    fun setMostrarDialogoConfirmacionArticulo(mostrar: Boolean) {
        _mostrarDialogoConfirmacionArticulo.value = mostrar
    }

    fun setArticuloParaConfirmar(articulo: LecturaPendiente?) {
        _articuloParaConfirmar.value = articulo
    }

    fun setArticulosFiltrados(articulos: List<com.example.sga.data.dto.stock.ArticuloDto>) {
        _articulosFiltrados.value = articulos
    }

    fun setMostrarDialogoSeleccionArticulo(mostrar: Boolean) {
        _mostrarDialogoSeleccionArticulo.value = mostrar
    }

    fun setConteoCompletado(completado: Boolean) {
        _conteoCompletado.value = completado
    }

    fun setPaletsDisponibles(palets: List<PaletDisponible>) {
        _paletsDisponibles.value = palets
    }

    fun setMostrarDialogoSeleccionPalet(mostrar: Boolean) {
        _mostrarDialogoSeleccionPalet.value = mostrar
    }

    fun setPaletSeleccionado(palet: PaletDisponible?) {
        _paletSeleccionado.value = palet
    }


    // Limpiar estados
    fun limpiarFormulario() {
        _cantidadContada.value = ""
        _comentario.value = ""
    }

    fun limpiarMensajes() {
        _error.value = null
        _mensaje.value = null
    }

    fun limpiarEscaneo() {
        _estadoEscaneo.value = EstadoEscaneoConteo.Inactivo
        _ubicacionEscaneada.value = null
        _articuloEscaneado.value = null
        _mostrarDialogoConfirmacionArticulo.value = false
        _articuloParaConfirmar.value = null
        _mostrarDialogoSeleccionArticulo.value = false
        _articulosFiltrados.value = emptyList()
        _mostrarDialogoSeleccionPalet.value = false
        _paletsDisponibles.value = emptyList()
        _paletSeleccionado.value = null
    }

    // Función para formatear fecha a dd/mm/yyyy HH:mm
    fun formatearFecha(fecha: String): String {
        return try {
            // Asumiendo que la fecha viene en formato ISO (yyyy-mm-ddTHH:mm:ss o similar)
            val partes = fecha.split("T")
            if (partes.size >= 2) {
                val fechaParte = partes[0].split("-")
                val horaParte = partes[1].split(":")
                
                if (fechaParte.size >= 3 && horaParte.size >= 2) {
                    val fechaFormateada = "${fechaParte[2]}/${fechaParte[1]}/${fechaParte[0]}"
                    val horaFormateada = "${horaParte[0]}:${horaParte[1]}"
                    "$fechaFormateada $horaFormateada"
                } else if (fechaParte.size >= 3) {
                    "${fechaParte[2]}/${fechaParte[1]}/${fechaParte[0]}"
                } else {
                    fecha
                }
            } else {
                // Si no tiene formato T, intentar solo fecha
                val fechaParte = fecha.split("-")
                if (fechaParte.size >= 3) {
                    "${fechaParte[2]}/${fechaParte[1]}/${fechaParte[0]}"
                } else {
                    fecha
                }
            }
        } catch (e: Exception) {
            fecha
        }
    }
    
    // Funciones para modo lectura manual
    fun setModoLecturaManual(modoManual: Boolean) {
        _modoLecturaManual.value = modoManual
    }
    
}
