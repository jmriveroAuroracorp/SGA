-- Script para actualizar la vista vStockConPalets y excluir palets vaciados
-- Los palets vaciados no deben aparecer en inventarios porque no tienen stock

-- Eliminar la vista existente
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vStockConPalets')
BEGIN
    DROP VIEW vStockConPalets;
    PRINT 'Vista vStockConPalets eliminada.';
END

-- Crear la nueva vista excluyendo palets vaciados
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
FROM AcumuladoStockUbicacion s
LEFT JOIN PaletLineas pl ON 
    s.CodigoEmpresa = pl.CodigoEmpresa AND
    s.CodigoArticulo = pl.CodigoArticulo AND
    s.CodigoAlmacen = pl.CodigoAlmacen AND
    s.Ubicacion = pl.Ubicacion AND
    (s.Partida = pl.Lote OR (s.Partida IS NULL AND pl.Lote IS NULL))
LEFT JOIN Palets p ON pl.PaletId = p.Id
WHERE (p.Estado IS NULL OR p.Estado != 'Vaciado');

PRINT 'Vista vStockConPalets actualizada correctamente.';
PRINT 'Los palets vaciados han sido excluidos de la vista.';

-- Verificar que la vista se creó correctamente
SELECT TOP 5 * FROM vStockConPalets WHERE CodigoPalet IS NOT NULL; 