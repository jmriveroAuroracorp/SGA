-- Script para crear la vista vStockConPalets
-- Esta vista combina el stock real con información de palets

-- Eliminar la vista si existe
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vStockConPalets')
BEGIN
    DROP VIEW vStockConPalets;
    PRINT 'Vista vStockConPalets eliminada.';
END

-- Crear la nueva vista
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

PRINT 'Vista vStockConPalets creada correctamente.'; 