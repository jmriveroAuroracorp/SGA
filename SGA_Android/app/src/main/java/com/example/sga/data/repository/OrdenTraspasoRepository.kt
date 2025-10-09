package com.example.sga.data.repository

import com.example.sga.data.OrdenTraspasoApiService
import com.example.sga.data.dto.ordenes.*

class OrdenTraspasoRepository(private val apiService: OrdenTraspasoApiService) {
    
    suspend fun getOrdenesPorOperario(idOperario: Int, codigoEmpresa: Int): Result<List<OrdenTraspasoDto>> {
        return try {
            val response = apiService.getOrdenesPorOperario(idOperario, codigoEmpresa)
            if (response.isSuccessful) {
                Result.success(response.body() ?: emptyList())
            } else {
                Result.failure(Exception("Error: ${response.code()} - ${response.message()}"))
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }
    
    suspend fun getOrdenTraspaso(id: String): Result<OrdenTraspasoDto> {
        return try {
            val response = apiService.getOrdenTraspaso(id)
            if (response.isSuccessful) {
                Result.success(response.body()!!)
            } else {
                Result.failure(Exception("Error: ${response.code()} - ${response.message()}"))
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }
    
    suspend fun actualizarEstadoLinea(idLinea: String, estado: String): Result<Unit> {
        return try {
            val response = apiService.actualizarEstadoLinea(idLinea, ActualizarEstadoLineaDto(estado))
            if (response.isSuccessful) {
                Result.success(Unit)
            } else {
                Result.failure(Exception("Error: ${response.code()} - ${response.message()}"))
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }
    
    suspend fun getStockDisponible(codigoEmpresa: Int, codigoArticulo: String, idOperario: Int): Result<List<StockDisponibleDto>> {
        return try {
            val response = apiService.getStockDisponible(codigoEmpresa, codigoArticulo, idOperario)
            if (response.isSuccessful) {
                Result.success(response.body() ?: emptyList())
            } else {
                Result.failure(Exception("Error: ${response.code()} - ${response.message()}"))
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }
    
    suspend fun actualizarLineaCompleta(idLinea: String, dto: ActualizarLineaOrdenTraspasoDto): Result<Unit> {
        return try {
            val response = apiService.actualizarLineaCompleta(idLinea, dto)
            if (response.isSuccessful) {
                Result.success(Unit)
            } else {
                Result.failure(Exception("Error: ${response.code()} - ${response.message()}"))
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }
    
    suspend fun actualizarOrdenTraspaso(id: String, dto: ActualizarOrdenTraspasoDto): Result<Unit> {
        return try {
            val response = apiService.actualizarOrdenTraspaso(id, dto)
            if (response.isSuccessful) {
                Result.success(Unit)
            } else {
                Result.failure(Exception("Error: ${response.code()} - ${response.message()}"))
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }
}
