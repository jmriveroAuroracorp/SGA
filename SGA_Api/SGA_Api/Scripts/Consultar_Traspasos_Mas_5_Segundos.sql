-- Consulta para contar traspasos del usuario 3960 con tiempo superior a 5 segundos
SELECT 
    COUNT(*) AS TotalTraspasosMas5Segundos,
    COUNT(*) * 100.0 / (SELECT COUNT(*) FROM Traspasos WHERE UsuarioInicioId = 3960 AND FechaFinalizacion IS NOT NULL) AS Porcentaje
FROM 
    Traspasos
WHERE 
    UsuarioInicioId = 3960
    AND FechaFinalizacion IS NOT NULL
    AND DATEDIFF(SECOND, FechaInicio, FechaFinalizacion) > 5;

-- Consulta detallada: mostrar todos los traspasos con su tiempo en segundos
SELECT 
    Id,
    AlmacenOrigen,
    AlmacenDestino,
    CodigoEstado,
    FechaInicio,
    FechaFinalizacion,
    DATEDIFF(SECOND, FechaInicio, FechaFinalizacion) AS TiempoSegundos,
    TipoTraspaso,
    CodigoPalet,
    CodigoArticulo,
    Cantidad
FROM 
    Traspasos
WHERE 
    UsuarioInicioId = 3960
    AND FechaFinalizacion IS NOT NULL
ORDER BY 
    FechaInicio DESC;

-- Resumen por rangos de tiempo
SELECT 
    CASE 
        WHEN DATEDIFF(SECOND, FechaInicio, FechaFinalizacion) <= 5 THEN '0-5 segundos'
        WHEN DATEDIFF(SECOND, FechaInicio, FechaFinalizacion) <= 10 THEN '6-10 segundos'
        WHEN DATEDIFF(SECOND, FechaInicio, FechaFinalizacion) <= 30 THEN '11-30 segundos'
        WHEN DATEDIFF(SECOND, FechaInicio, FechaFinalizacion) <= 60 THEN '31-60 segundos'
        ELSE 'Más de 60 segundos'
    END AS RangoTiempo,
    COUNT(*) AS Cantidad,
    COUNT(*) * 100.0 / (SELECT COUNT(*) FROM Traspasos WHERE UsuarioInicioId = 3960 AND FechaFinalizacion IS NOT NULL) AS Porcentaje
FROM 
    Traspasos
WHERE 
    UsuarioInicioId = 3960
    AND FechaFinalizacion IS NOT NULL
GROUP BY 
    CASE 
        WHEN DATEDIFF(SECOND, FechaInicio, FechaFinalizacion) <= 5 THEN '0-5 segundos'
        WHEN DATEDIFF(SECOND, FechaInicio, FechaFinalizacion) <= 10 THEN '6-10 segundos'
        WHEN DATEDIFF(SECOND, FechaInicio, FechaFinalizacion) <= 30 THEN '11-30 segundos'
        WHEN DATEDIFF(SECOND, FechaInicio, FechaFinalizacion) <= 60 THEN '31-60 segundos'
        ELSE 'Más de 60 segundos'
    END
ORDER BY 
    MIN(DATEDIFF(SECOND, FechaInicio, FechaFinalizacion));

