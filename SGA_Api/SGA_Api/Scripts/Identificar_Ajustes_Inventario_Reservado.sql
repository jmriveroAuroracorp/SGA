-- Script para identificar TempPaletLineas con TraspasoId NULL que están generando reservado incorrectamente
-- Estas líneas son ajustes de inventario que NO deberían contar como reservado

DECLARE @CodigoEmpresa SMALLINT = 1;
DECLARE @CodigoArticulo VARCHAR(50) = 'PR10002';
DECLARE @CodigoAlmacen VARCHAR(20) = 'PR';
DECLARE @Ubicacion VARCHAR(50) = 'UB001001001001';
DECLARE @Partida VARCHAR(50) = '987456321';

-- ============================================
-- IDENTIFICAR TempPaletLineas con TraspasoId NULL
-- ============================================
PRINT '=========================================';
PRINT 'TempPaletLineas con TraspasoId NULL que generan Reservado';
PRINT '=========================================';
SELECT 
    t.Id AS TempPaletLineaId,
    t.PaletId,
    t.TraspasoId,
    t.CodigoArticulo,
    t.Lote,
    t.CodigoAlmacen,
    t.Ubicacion,
    t.Cantidad AS CantidadReservada,
    t.FechaAgregado,
    t.UsuarioId,
    t.Observaciones,
    t.EsHeredada,
    t.ConteoId,
    t.Procesada,
    CASE 
        WHEN t.TraspasoId IS NULL THEN 'SIN TRASPASO'
        ELSE 'CON TRASPASO'
    END AS Estado
FROM Aurora_Sga.dbo.TempPaletLineas t
WHERE t.CodigoAlmacen = @CodigoAlmacen
  AND t.Ubicacion = @Ubicacion
  AND t.CodigoArticulo = @CodigoArticulo
  AND (t.Lote = @Partida OR (t.Lote IS NULL AND @Partida IS NULL))
  AND t.ConteoId IS NULL
  AND t.TraspasoId IS NULL  -- Solo las que NO tienen traspaso
ORDER BY t.FechaAgregado DESC;

-- ============================================
-- RESUMEN: Total de estas líneas
-- ============================================
PRINT '';
PRINT '=========================================';
PRINT 'RESUMEN: Total Reservado de líneas SIN TraspasoId';
PRINT '=========================================';
SELECT 
    COUNT(*) AS CantidadLineas,
    SUM(t.Cantidad) AS TotalReservado,
    SUM(CASE WHEN t.Cantidad > 0 THEN t.Cantidad ELSE 0 END) AS TotalPositivo,
    SUM(CASE WHEN t.Cantidad < 0 THEN ABS(t.Cantidad) ELSE 0 END) AS TotalNegativo
FROM Aurora_Sga.dbo.TempPaletLineas t
WHERE t.CodigoAlmacen = @CodigoAlmacen
  AND t.Ubicacion = @Ubicacion
  AND t.CodigoArticulo = @CodigoArticulo
  AND (t.Lote = @Partida OR (t.Lote IS NULL AND @Partida IS NULL))
  AND t.ConteoId IS NULL
  AND t.TraspasoId IS NULL;

