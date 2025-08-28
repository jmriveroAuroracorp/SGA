-- Script para forzar la actualización de la vista vStockConPalets
-- y excluir definitivamente los palets vaciados

PRINT '=== ACTUALIZANDO VISTA vStockConPalets ===';

-- 1. Eliminar la vista existente
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vStockConPalets')
BEGIN
    DROP VIEW vStockConPalets;
    PRINT '✓ Vista vStockConPalets eliminada.';
END
ELSE
BEGIN
    PRINT '⚠ Vista vStockConPalets no existía.';
END

GO

-- 2. Crear la nueva vista con filtro de palets vaciados
-- Usando referencias completas a las bases de datos
CREATE VIEW vStockConPalets AS
SELECT 
    s.CodigoEmpresa,
    s.CodigoArticulo,
    s.CodigoAlmacen,
    s.Ubicacion,
    s.Partida,
    s.FechaCaducidad,
    s.UnidadSaldo,
    s.Ejercicio,
    p.Codigo as CodigoPalet,
    p.Id as PaletId,
    p.Estado as EstadoPalet
FROM [StorageControl].[dbo].[AcumuladoStockUbicacion] s
LEFT JOIN [AURORA_SGA].[dbo].[PaletLineas] pl ON 
    s.CodigoEmpresa = pl.CodigoEmpresa AND
    s.CodigoArticulo = pl.CodigoArticulo AND
    s.CodigoAlmacen = pl.CodigoAlmacen AND
    s.Ubicacion = pl.Ubicacion AND
    (s.Partida = pl.Lote OR (s.Partida IS NULL AND pl.Lote IS NULL))
LEFT JOIN [AURORA_SGA].[dbo].[Palets] p ON pl.PaletId = p.Id
WHERE (p.Estado IS NULL OR p.Estado != 'Vaciado');

GO

PRINT '✓ Vista vStockConPalets recreada con filtro de palets vaciados.';

-- 3. Verificar que los palets vaciados ya no aparecen
PRINT '';
PRINT '=== VERIFICACIÓN: PALETS VACIADOS NO DEBEN APARECER ===';
DECLARE @PaletsVaciadosCount INT = (
    SELECT COUNT(*) 
    FROM vStockConPalets v 
    WHERE v.EstadoPalet = 'Vaciado'
);

IF @PaletsVaciadosCount = 0
BEGIN
    PRINT '✅ CORRECTO: No hay palets vaciados en la vista.';
END
ELSE
BEGIN
    PRINT '❌ ERROR: Aún hay ' + CAST(@PaletsVaciadosCount AS VARCHAR) + ' palets vaciados en la vista.';
END

-- 4. Mostrar resumen de estados en la vista
PRINT '';
PRINT '=== RESUMEN DE ESTADOS EN vStockConPalets ===';
SELECT 
    v.EstadoPalet,
    COUNT(*) as Cantidad
FROM vStockConPalets v
WHERE v.CodigoPalet IS NOT NULL
GROUP BY v.EstadoPalet
ORDER BY v.EstadoPalet;

-- 5. Mostrar algunos ejemplos de palets que SÍ aparecen
PRINT '';
PRINT '=== EJEMPLOS DE PALETS EN LA VISTA ===';
SELECT TOP 5
    v.CodigoPalet,
    v.EstadoPalet,
    v.CodigoArticulo,
    v.Ubicacion,
    v.UnidadSaldo
FROM vStockConPalets v
WHERE v.CodigoPalet IS NOT NULL
ORDER BY v.EstadoPalet, v.CodigoPalet; 