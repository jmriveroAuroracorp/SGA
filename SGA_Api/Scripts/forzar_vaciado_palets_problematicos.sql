-- Script para forzar el vaciado de los palets problemáticos
-- PAL25-0000111, PAL25-0000112, PAL25-0000116

BEGIN TRANSACTION;

-- 1. Marcar los palets como vaciados
UPDATE Palets 
SET Estado = 'Vaciado', 
    FechaVaciado = GETDATE(),
    UsuarioVaciadoId = 4066
WHERE Codigo IN ('PAL25-0000111', 'PAL25-0000112', 'PAL25-0000116')
AND Estado != 'Vaciado';

-- 2. Eliminar las líneas definitivas de palet
DELETE FROM PaletLineas 
WHERE PaletId IN (
    SELECT Id FROM Palets 
    WHERE Codigo IN ('PAL25-0000111', 'PAL25-0000112', 'PAL25-0000116')
);

-- 3. Eliminar las líneas temporales de palet
DELETE FROM TempPaletLineas 
WHERE PaletId IN (
    SELECT Id FROM Palets 
    WHERE Codigo IN ('PAL25-0000111', 'PAL25-0000112', 'PAL25-0000116')
);

-- 4. Agregar logs de vaciado forzado
INSERT INTO LogPalet (PaletId, Fecha, IdUsuario, Accion, Detalle)
SELECT 
    p.Id,
    GETDATE(),
    4066,
    'Vaciado',
    'Vaciado forzado por problemas con sistema de inventarios'
FROM Palets p
WHERE p.Codigo IN ('PAL25-0000111', 'PAL25-0000112', 'PAL25-0000116')
AND p.Estado = 'Vaciado';

-- 5. Verificar el resultado
SELECT 
    p.Codigo,
    p.Estado,
    p.FechaVaciado,
    COUNT(pl.Id) as LineasDefinitivas,
    COUNT(tpl.Id) as LineasTemporales
FROM Palets p
LEFT JOIN PaletLineas pl ON p.Id = pl.PaletId
LEFT JOIN TempPaletLineas tpl ON p.Id = tpl.PaletId
WHERE p.Codigo IN ('PAL25-0000111', 'PAL25-0000112', 'PAL25-0000116')
GROUP BY p.Codigo, p.Estado, p.FechaVaciado;

COMMIT TRANSACTION;

PRINT 'Vaciado forzado completado para los 3 palets problemáticos.'; 