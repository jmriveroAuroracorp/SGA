-- Consulta para encontrar palets sin líneas que NO están marcados como "Vaciado"
-- Útil para detectar palets que deberían haberse marcado como vaciados automáticamente

SELECT 
    p.Id,
    p.Codigo AS CodigoPalet,
    p.Estado,
    p.CodigoEmpresa,
    p.FechaApertura,
    p.FechaCierre,
    p.FechaVaciado,
    p.UsuarioAperturaId,
    p.UsuarioCierreId,
    p.UsuarioVaciadoId,
    -- Conteo de líneas temporales pendientes
    (SELECT COUNT(*) FROM TempPaletLineas tpl 
     WHERE tpl.PaletId = p.Id AND tpl.Procesada = 0) AS CantidadTemporalesPendientes,
    -- Conteo de líneas definitivas
    (SELECT COUNT(*) FROM PaletLineas pl 
     WHERE pl.PaletId = p.Id) AS CantidadLineasDefinitivas,
    -- Total de líneas temporales (todas, procesadas o no)
    (SELECT COUNT(*) FROM TempPaletLineas tpl2 
     WHERE tpl2.PaletId = p.Id) AS TotalTemporales,
    -- Suma de cantidades de líneas definitivas
    (SELECT ISNULL(SUM(pl2.Cantidad), 0) FROM PaletLineas pl2 
     WHERE pl2.PaletId = p.Id) AS TotalCantidadDefinitiva
FROM 
    Palets p
WHERE 
    -- NO tiene líneas temporales pendientes (Procesada = false)
    NOT EXISTS (
        SELECT 1 FROM TempPaletLineas tpl 
        WHERE tpl.PaletId = p.Id AND tpl.Procesada = 0
    )
    -- Y NO tiene líneas definitivas
    AND NOT EXISTS (
        SELECT 1 FROM PaletLineas pl 
        WHERE pl.PaletId = p.Id
    )
    -- Y NO está marcado como "Vaciado"
    AND p.Estado != 'Vaciado'
ORDER BY 
    p.FechaApertura DESC;

-- RESUMEN: Contar cuántos palets hay en esta situación
SELECT 
    COUNT(*) AS TotalPaletsSinLineasNoVaciados
FROM 
    Palets p
WHERE 
    NOT EXISTS (
        SELECT 1 FROM TempPaletLineas tpl 
        WHERE tpl.PaletId = p.Id AND tpl.Procesada = 0
    )
    AND NOT EXISTS (
        SELECT 1 FROM PaletLineas pl 
        WHERE pl.PaletId = p.Id
    )
    AND p.Estado != 'Vaciado';

-- VERSIÓN ALTERNATIVA: Incluye también palets que tienen líneas pero con cantidad total <= 0
SELECT 
    p.Id,
    p.Codigo AS CodigoPalet,
    p.Estado,
    p.CodigoEmpresa,
    -- Suma de cantidades definitivas
    ISNULL(SUM(pl.Cantidad), 0) AS TotalCantidad,
    -- Verifica si hay líneas positivas
    CASE 
        WHEN EXISTS (
            SELECT 1 FROM PaletLineas pl2 
            WHERE pl2.PaletId = p.Id AND pl2.Cantidad > 0
        ) THEN 1 
        ELSE 0 
    END AS TieneLineasPositivas
FROM 
    Palets p
    LEFT JOIN PaletLineas pl ON pl.PaletId = p.Id
WHERE 
    p.Estado != 'Vaciado'
    AND NOT EXISTS (
        SELECT 1 FROM TempPaletLineas tpl 
        WHERE tpl.PaletId = p.Id AND tpl.Procesada = 0
    )
GROUP BY 
    p.Id, p.Codigo, p.Estado, p.CodigoEmpresa
HAVING 
    -- No tiene líneas definitivas O tiene líneas pero cantidad total <= 0
    (COUNT(pl.Id) = 0 OR (SUM(pl.Cantidad) <= 0 AND NOT EXISTS (
        SELECT 1 FROM PaletLineas pl3 
        WHERE pl3.PaletId = p.Id AND pl3.Cantidad > 0
    )))
ORDER BY 
    p.FechaApertura DESC;


