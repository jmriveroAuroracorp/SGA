-- Script para actualizar el campo AjusteFinal en registros existentes de InventarioLineas
-- Ejecutar en la base de datos AuroraSga

-- Actualizar registros existentes donde AjusteFinal es NULL
-- Calculamos: AjusteFinal = StockContado - StockActual
UPDATE InventarioLineas 
SET AjusteFinal = StockContado - StockActual 
WHERE AjusteFinal IS NULL 
  AND StockContado IS NOT NULL 
  AND StockActual IS NOT NULL;

-- Mostrar estadísticas de la actualización
SELECT 
    'Registros actualizados' as Descripcion,
    COUNT(*) as Cantidad
FROM InventarioLineas 
WHERE AjusteFinal IS NOT NULL
UNION ALL
SELECT 
    'Registros con AjusteFinal NULL' as Descripcion,
    COUNT(*) as Cantidad
FROM InventarioLineas 
WHERE AjusteFinal IS NULL;

-- Mostrar algunos ejemplos de registros actualizados
SELECT TOP 10
    CodigoArticulo,
    CodigoUbicacion,
    StockActual,
    StockContado,
    AjusteFinal,
    CASE 
        WHEN AjusteFinal > 0 THEN 'POSITIVO'
        WHEN AjusteFinal < 0 THEN 'NEGATIVO'
        ELSE 'SIN AJUSTE'
    END as TipoAjuste
FROM InventarioLineas 
WHERE AjusteFinal IS NOT NULL
ORDER BY FechaValidacion DESC;

PRINT 'Actualización de AjusteFinal completada'; 