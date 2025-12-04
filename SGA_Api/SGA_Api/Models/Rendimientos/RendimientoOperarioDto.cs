namespace SGA_Api.Models.Rendimientos
{
    public class RendimientoOperarioDto
    {
        public int OperarioId { get; set; }
        public string? NombreOperario { get; set; }
        
        // Métricas de productividad
        public int TotalOperaciones { get; set; }
        public int TraspasosCompletados { get; set; }
        public int TraspasosPalet { get; set; } // Traspasos de tipo PALET
        public int TraspasosArticulo { get; set; } // Traspasos de tipo ARTÍCULO
        public int LineasInventarioGeneradas { get; set; } // Total de líneas generadas para el inventario
        public int LineasInventarioContadas { get; set; } // Solo las líneas que realmente contó el usuario
        public int LineasInventarioCreadas { get; set; } // Líneas creadas manualmente (StockActual = 0 y tiene conteo)
        public int LecturasConteo { get; set; }
        public int ConteosCompletados { get; set; } // Órdenes de conteo cerradas
        
        // Métricas de tiempo
        public double? TiempoPromedioTraspasosMinutos { get; set; }
        public double? TiempoPromedioInventarioMinutos { get; set; }
        public double? TiempoPromedioConteoMinutos { get; set; }
        
        // Métricas adicionales
        public int TraspasosConErrores { get; set; }
        
        // Métricas de productividad por hora
        public double? LineasPorHora { get; set; } // Para inventarios
        public double? LecturasPorHora { get; set; } // Para conteos
        public double? TraspasosPorDia { get; set; }
        
        // Métricas adicionales para análisis
        public double? PorcentajeDelTotal { get; set; } // % del total del equipo
        public int Ranking { get; set; } // Posición en el ranking
        public double? TiempoMinimoTraspasosMinutos { get; set; } // Tiempo más rápido
        public double? TiempoMaximoTraspasosMinutos { get; set; } // Tiempo más lento
        public double? TiempoMedianoTraspasosMinutos { get; set; } // Tiempo mediano
        public double? TasaFinalizacion { get; set; } // % de traspasos completados vs iniciados
        public int DiasActivos { get; set; } // Días con actividad
        public double? TiempoTotalTrabajadoMinutos { get; set; } // Tiempo total trabajado
        public int AlmacenesDiferentes { get; set; } // Cantidad de almacenes diferentes trabajados
        public int ArticulosDiferentes { get; set; } // Cantidad de artículos diferentes trabajados
        public DateTime? UltimaActividad { get; set; } // Fecha/hora de última actividad
        public double? VariacionPorcentual { get; set; } // Variación % vs promedio del equipo
    }
}

