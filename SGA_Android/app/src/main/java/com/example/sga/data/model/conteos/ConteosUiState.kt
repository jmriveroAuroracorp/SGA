package com.example.sga.data.model.conteos

sealed class ConteosUiState<out T> {
    object Loading : ConteosUiState<Nothing>()
    data class Success<T>(val data: T) : ConteosUiState<T>()
    data class Error(val message: String) : ConteosUiState<Nothing>()
}

data class OrdenesListState(
    val ordenes: List<OrdenConteo> = emptyList(),
    val isLoading: Boolean = false,
    val error: String? = null
)

data class OrdenDetailState(
    val orden: OrdenConteo? = null,
    val lecturas: List<LecturaConteo> = emptyList(),
    val resultados: List<ResultadoConteo> = emptyList(),
    val isLoading: Boolean = false,
    val error: String? = null
)

data class LecturaState(
    val lectura: LecturaConteo? = null,
    val cantidadContada: String = "",
    val comentario: String = "",
    val isLoading: Boolean = false,
    val error: String? = null,
    val isSuccess: Boolean = false
)
