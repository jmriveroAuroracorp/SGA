-- Script para verificar que los palets vaciados están excluidos de la vista vStockConPalets
-- Los palets vaciados no deben aparecer en inventarios porque no tienen stock

-- 1. Verificar palets vaciados en la tabla Palets
PRINT '=== PALETS VACIADOS EN LA TABLA Palets ===';
SELECT 
    p.Id,
    p.Codigo,
    p.Estado,
    p.FechaVaciado,
    p.UsuarioVaciadoId
FROM Palets p 
WHERE p.Estado = 'Vaciado'
ORDER BY p.Codigo;

-- 2. Verificar que los palets vaciados NO aparecen en vStockConPalets
PRINT '';
PRINT '=== VERIFICACIÓN: PALETS VACIADOS NO DEBEN APARECER EN vStockConPalets ===';
SELECT 
    v.CodigoPalet,
    v.EstadoPalet,
    v.CodigoArticulo,
    v.Ubicacion,
    v.UnidadSaldo
FROM vStockConPalets v
WHERE v.EstadoPalet = 'Vaciado';

-- 3. Verificar palets que SÍ aparecen en vStockConPalets (solo Abiertos y Cerrados)
PRINT '';
PRINT '=== PALETS QUE SÍ APARECEN EN vStockConPalets ===';
SELECT 
    v.CodigoPalet,
    v.EstadoPalet,
    v.CodigoArticulo,
    v.Ubicacion,
    v.UnidadSaldo
FROM vStockConPalets v
WHERE v.CodigoPalet IS NOT NULL
ORDER BY v.EstadoPalet, v.CodigoPalet;

-- 4. Resumen de estados de palets en la vista
PRINT '';
PRINT '=== RESUMEN DE ESTADOS DE PALETS EN vStockConPalets ===';
SELECT 
    v.EstadoPalet,
    COUNT(*) as Cantidad
FROM vStockConPalets v
WHERE v.CodigoPalet IS NOT NULL
GROUP BY v.EstadoPalet
ORDER BY v.EstadoPalet;

-- 5. Verificar que no hay palets vaciados con stock > 0 (esto sería un error)
PRINT '';
PRINT '=== VERIFICACIÓN: NO DEBE HABER PALETS VACIADOS CON STOCK > 0 ===';
SELECT 
    p.Codigo as CodigoPalet,
    p.Estado,
    pl.CodigoArticulo,
    pl.Cantidad,
    pl.Ubicacion
FROM Palets p
INNER JOIN PaletLineas pl ON p.Id = pl.PaletId
WHERE p.Estado = 'Vaciado' AND pl.Cantidad > 0; 