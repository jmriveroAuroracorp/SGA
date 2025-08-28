-- Verificar datos en la vista vStockConPalets
-- Específicamente para PR10002 en UB001001001001

-- 1. Verificar si la vista tiene datos para PR10002
SELECT * FROM vStockConPalets 
WHERE CodigoArticulo = 'PR10002' 
AND Ubicacion = 'UB001001001001'
ORDER BY CodigoPalet;

-- 2. Verificar si hay palets para PR10002 en cualquier ubicación
SELECT * FROM vStockConPalets 
WHERE CodigoArticulo = 'PR10002' 
AND PaletId IS NOT NULL
ORDER BY Ubicacion, CodigoPalet;

-- 3. Verificar la línea de inventario específica
SELECT 
    ilt.IdTemp,
    ilt.CodigoArticulo,
    ilt.CodigoUbicacion,
    ilt.StockActual,
    ilt.PaletId,
    ilt.CodigoPalet,
    ilt.EstadoPalet
FROM InventarioLineasTemp ilt
WHERE ilt.CodigoArticulo = 'PR10002' 
AND ilt.CodigoUbicacion = 'UB001001001001';

-- 4. Verificar si hay palets en PaletLineas para PR10002
SELECT 
    pl.Id,
    pl.PaletId,
    p.Codigo as CodigoPalet,
    p.Estado as EstadoPalet,
    pl.CodigoArticulo,
    pl.Ubicacion,
    pl.Cantidad
FROM PaletLineas pl
INNER JOIN Palets p ON pl.PaletId = p.Id
WHERE pl.CodigoArticulo = 'PR10002'
ORDER BY pl.Ubicacion, p.Codigo; 