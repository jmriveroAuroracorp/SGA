-- Script para investigar los palets problemáticos (CORREGIDO)
-- PAL25-0000111, PAL25-0000112, PAL25-0000116

-- 1. Verificar estado de los palets
SELECT 
    p.Id,
    p.Codigo,
    p.Estado,
    p.FechaApertura,
    p.FechaCierre,
    p.FechaVaciado,
    p.UsuarioAperturaId,
    p.UsuarioCierreId,
    p.UsuarioVaciadoId
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
    pl.Lote,
    pl.FechaAgregado,
    pl.UsuarioId
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
    tpl.Lote,
    tpl.FechaAgregado,
    tpl.UsuarioId,
    tpl.Procesada,
    tpl.EsHeredada
FROM TempPaletLineas tpl
INNER JOIN Palets p ON tpl.PaletId = p.Id
WHERE p.Codigo IN ('PAL25-0000111', 'PAL25-0000112', 'PAL25-0000116')
ORDER BY p.Codigo, tpl.CodigoArticulo;

-- 4. Verificar estructura de la tabla Traspasos
SELECT TOP 1 * FROM Traspasos;

-- 5. Verificar traspasos relacionados (usando la estructura correcta)
SELECT 
    t.TraspasoId,
    t.PaletId,
    p.Codigo as CodigoPalet,
    t.TipoTraspaso,
    t.CodigoEstado,
    t.FechaCreacion,
    t.FechaFinalizacion,
    t.AlmacenOrigen,
    t.AlmacenDestino,
    t.UbicacionOrigen,
    t.UbicacionDestino
FROM Traspasos t
INNER JOIN Palets p ON t.PaletId = p.Id
WHERE p.Codigo IN ('PAL25-0000111', 'PAL25-0000112', 'PAL25-0000116')
ORDER BY p.Codigo, t.FechaCreacion;

-- 6. Verificar log de palets
SELECT 
    lp.Id,
    lp.PaletId,
    p.Codigo as CodigoPalet,
    lp.Accion,
    lp.Detalle,
    lp.Fecha,
    lp.IdUsuario
FROM LogPalet lp
INNER JOIN Palets p ON lp.PaletId = p.Id
WHERE p.Codigo IN ('PAL25-0000111', 'PAL25-0000112', 'PAL25-0000116')
ORDER BY p.Codigo, lp.Fecha DESC;

-- 7. Verificar si hay inventarios relacionados
SELECT 
    ilt.IdTemp,
    ilt.IdInventario,
    ilt.CodigoArticulo,
    ilt.CodigoUbicacion,
    ilt.CantidadContada,
    ilt.PaletId,
    ilt.CodigoPalet,
    ilt.EstadoPalet,
    ic.CodigoAlmacen,
    ic.FechaCreacion
FROM InventarioLineasTemp ilt
INNER JOIN InventarioCabecera ic ON ilt.IdInventario = ic.Id
WHERE ilt.PaletId IN (
    SELECT p.Id FROM Palets p 
    WHERE p.Codigo IN ('PAL25-0000111', 'PAL25-0000112', 'PAL25-0000116')
)
ORDER BY ic.FechaCreacion DESC;

-- 8. Verificar si hay líneas con cantidades negativas
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
AND pl.Cantidad < 0
ORDER BY p.Codigo, pl.Cantidad;

-- 9. Verificar líneas temporales con cantidades negativas
SELECT 
    tpl.Id,
    tpl.PaletId,
    p.Codigo as CodigoPalet,
    tpl.CodigoArticulo,
    tpl.Cantidad,
    tpl.Ubicacion,
    tpl.Lote,
    tpl.Procesada
FROM TempPaletLineas tpl
INNER JOIN Palets p ON tpl.PaletId = p.Id
WHERE p.Codigo IN ('PAL25-0000111', 'PAL25-0000112', 'PAL25-0000116')
AND tpl.Cantidad < 0
ORDER BY p.Codigo, tpl.Cantidad; 