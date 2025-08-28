-- Script simplificado para investigar los palets problemáticos
-- Ejecutar cada consulta por separado

-- 1. Verificar estado de los palets
SELECT 
    p.Id,
    p.Codigo,
    p.Estado,
    p.FechaApertura,
    p.FechaCierre,
    p.FechaVaciado
FROM Palets p
WHERE p.Codigo IN ('PAL25-0000111', 'PAL25-0000112', 'PAL25-0000116')
ORDER BY p.Codigo;

-- 2. Verificar líneas de palet (definitivas)
SELECT 
    pl.Id,
    pl.PaletId,
    p.Codigo as CodigoPalet,
    pl.CodigoArticulo,
    pl.Cantidad,
    pl.Ubicacion,
    pl.Lote
FROM PaletLineas pl
INNER JOIN Palets p ON pl.PaletId = p.Id
WHERE p.Codigo IN ('PAL25-0000111', 'PAL25-0000112', 'PAL25-0000116')
ORDER BY p.Codigo, pl.CodigoArticulo;

-- 3. Verificar líneas temporales
SELECT 
    tpl.Id,
    tpl.PaletId,
    p.Codigo as CodigoPalet,
    tpl.CodigoArticulo,
    tpl.Cantidad,
    tpl.Ubicacion,
    tpl.Procesada
FROM TempPaletLineas tpl
INNER JOIN Palets p ON tpl.PaletId = p.Id
WHERE p.Codigo IN ('PAL25-0000111', 'PAL25-0000112', 'PAL25-0000116')
ORDER BY p.Codigo, tpl.CodigoArticulo;

-- 4. Verificar estructura de Traspasos
SELECT TOP 1 * FROM Traspasos;

-- 5. Verificar log de palets
SELECT 
    lp.Id,
    lp.PaletId,
    p.Codigo as CodigoPalet,
    lp.Accion,
    lp.Detalle,
    lp.Fecha
FROM LogPalet lp
INNER JOIN Palets p ON lp.PaletId = p.Id
WHERE p.Codigo IN ('PAL25-0000111', 'PAL25-0000112', 'PAL25-0000116')
ORDER BY p.Codigo, lp.Fecha DESC; 