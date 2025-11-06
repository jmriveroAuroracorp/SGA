package com.example.sga.utils

import java.util.Locale

/**
 * Utilidades para formateo de datos (números, fechas, etc.).
 */
object FormatUtils {
    
    /**
     * Formatea un número Double mostrando hasta 4 decimales,
     * eliminando ceros innecesarios por la derecha sin redondear.
     * Usa punto (.) como separador decimal.
     * 
     * Ejemplos:
     * - 200.0000 → "200"
     * - 200.5000 → "200.5"
     * - 200.1200 → "200.12"
     * - 200.1234 → "200.1234"
     * - 10.1230 → "10.123"
     * 
     * @param valor El número a formatear
     * @return String formateado con hasta 4 decimales significativos
     */
    fun formatearCantidad(valor: Double): String {
        // Formato con 4 decimales usando Locale US para mantener el punto como separador
        val formatted = String.format(Locale.US, "%.4f", valor)
        // Eliminar ceros innecesarios por la derecha y el punto decimal si no hay decimales
        return formatted.trimEnd('0').trimEnd('.')
    }
    
    /**
     * Formatea una fecha ISO a formato DD-MM-YYYY.
     * Maneja formatos: "YYYY-MM-DD" y "YYYY-MM-DDTHH:mm:ss"
     * 
     * Ejemplos:
     * - "2024-03-15" → "15-03-2024"
     * - "2024-03-15T10:30:00" → "15-03-2024"
     * - null → null
     * 
     * @param fecha Fecha en formato ISO (puede ser null)
     * @return Fecha formateada en DD-MM-YYYY o null si la entrada es null/inválida
     */
    fun formatearFecha(fecha: String?): String? {
        if (fecha == null || fecha.isEmpty()) return null
        return try {
            // Extraer solo la parte de la fecha (antes de 'T' si existe)
            val fechaSolo = fecha.split("T")[0]
            val partes = fechaSolo.split("-")
            if (partes.size == 3) {
                // Convertir YYYY-MM-DD a DD-MM-YYYY
                "${partes[2]}-${partes[1]}-${partes[0]}"
            } else {
                fecha // Devolver original si no se puede parsear
            }
        } catch (e: Exception) {
            fecha // Devolver original en caso de error
        }
    }
}

