-- Script para actualizar líneas de inventario existentes con información de palets
-- Este script actualiza las líneas que ya existen con la información de palets desde vStockConPalets

BEGIN TRANSACTION;

-- Actualizar líneas de inventario con información de palets
UPDATE InventarioLineasTemp 
SET 
    PaletId = v.PaletId,
    CodigoPalet = v.CodigoPalet,
    EstadoPalet = v.EstadoPalet
FROM InventarioLineasTemp ilt
INNER JOIN vStockConPalets v ON 
    ilt.CodigoArticulo = v.CodigoArticulo AND
    ilt.CodigoUbicacion = v.Ubicacion AND
    (ilt.Partida = v.Partida OR (ilt.Partida IS NULL AND v.Partida IS NULL)) AND
    ilt.StockActual = v.UnidadSaldo
WHERE 
    ilt.PaletId IS NULL AND  -- Solo actualizar líneas que no tienen información de palet
    v.PaletId IS NOT NULL AND  -- Solo si hay información de palet disponible
    v.UnidadSaldo > 0;  -- Solo palets con stock positivo

-- Verificar el resultado
SELECT 
    'Líneas actualizadas' as Accion,
    COUNT(*) as Cantidad
FROM InventarioLineasTemp 
WHERE PaletId IS NOT NULL;

-- Mostrar algunas líneas actualizadas como ejemplo
SELECT TOP 5
    ilt.CodigoArticulo,
    ilt.CodigoUbicacion,
    ilt.StockActual,
    ilt.PaletId,
    ilt.CodigoPalet,
    ilt.EstadoPalet
FROM InventarioLineasTemp ilt
WHERE ilt.PaletId IS NOT NULL
ORDER BY ilt.CodigoArticulo, ilt.CodigoUbicacion;

COMMIT TRANSACTION;

PRINT 'Actualización de líneas de inventario con información de palets completada.'; 