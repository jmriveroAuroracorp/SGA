-- Script para RECALCULAR StockActual en InventarioLineasTemp
-- Este script recalcula el StockActual desde vStockDisponible para líneas SUELTAS
-- Para líneas de PALET, se calcula desde PaletLineas
--
-- ⚠️ IMPORTANTE: Ejecutar SOLO después de cambiar las columnas a decimal(18,6)
-- ⚠️ ADVERTENCIA: Este script recalcula TODAS las líneas temporales abiertas
--    Revisa los resultados antes de aplicarlos en producción

USE [AURORA_SGA]
GO

-- Script para recalcular StockActual desde las fuentes de datos actuales
-- Esto es útil después de cambiar la precisión de decimal(18,4) a decimal(18,6)

BEGIN TRANSACTION;

BEGIN TRY
    PRINT 'Iniciando recálculo de StockActual en líneas temporales...';
    
    -- Variables para tracking
    DECLARE @LineasActualizadas INT = 0;
    DECLARE @LineasPalet INT = 0;
    DECLARE @LineasSuelto INT = 0;
    
    -- RECALCULAR LÍNEAS DE PALET desde PaletLineas
    -- Suma la cantidad de todas las líneas del palet para ese artículo/ubicación/partida
    UPDATE lt
    SET lt.StockActual = ISNULL(
        (SELECT SUM(pl.Cantidad) 
         FROM Aurora_Sga.dbo.PaletLineas pl
         WHERE pl.PaletId = lt.PaletId
           AND pl.CodigoEmpresa = (SELECT CodigoEmpresa FROM Aurora_Sga.dbo.InventarioCabecera WHERE IdInventario = lt.IdInventario)
           AND pl.CodigoAlmacen = lt.CodigoAlmacen
           AND pl.Ubicacion = lt.CodigoUbicacion
           AND pl.CodigoArticulo = lt.CodigoArticulo
           AND (pl.Lote = lt.Partida OR (pl.Lote IS NULL AND lt.Partida IS NULL))
           AND (pl.FechaCaducidad = lt.FechaCaducidad OR (pl.FechaCaducidad IS NULL AND lt.FechaCaducidad IS NULL))
        ), 0)
    FROM Aurora_Sga.dbo.InventarioLineasTemp lt
    WHERE lt.PaletId IS NOT NULL
      AND lt.Consolidado = 0;
    
    SET @LineasPalet = @@ROWCOUNT;
    PRINT CONCAT('Actualizadas ', @LineasPalet, ' líneas de PALET');
    
    -- RECALCULAR LÍNEAS SUELTAS desde AcumuladoStockUbicacion y PaletLineas
    -- Stock suelto = Total en ubicación - Stock paletizado
    UPDATE lt
    SET lt.StockActual = CAST(
        ISNULL(
            (SELECT SUM(CAST(s.UnidadSaldo AS decimal(38,6)))
             FROM StorageControl.dbo.AcumuladoStockUbicacion s
             INNER JOIN Aurora.dbo.Periodos p ON p.CodigoEmpresa = s.CodigoEmpresa
             WHERE s.CodigoEmpresa = (SELECT CodigoEmpresa FROM Aurora_Sga.dbo.InventarioCabecera WHERE IdInventario = lt.IdInventario)
               AND s.Ejercicio = (SELECT TOP 1 Ejercicio FROM Aurora.dbo.Periodos WHERE CodigoEmpresa = s.CodigoEmpresa AND Fechainicio <= GETDATE() ORDER BY Fechainicio DESC)
               AND s.CodigoAlmacen = lt.CodigoAlmacen
               AND s.Ubicacion = lt.CodigoUbicacion
               AND s.CodigoArticulo = lt.CodigoArticulo
               AND (s.Partida = lt.Partida OR (s.Partida IS NULL AND lt.Partida IS NULL))
               AND (s.FechaCaducidad = lt.FechaCaducidad OR (s.FechaCaducidad IS NULL AND lt.FechaCaducidad IS NULL))
            ), 0) -
        ISNULL(
            (SELECT SUM(CAST(pl.Cantidad AS decimal(38,6)))
             FROM Aurora_Sga.dbo.PaletLineas pl
             WHERE pl.CodigoEmpresa = (SELECT CodigoEmpresa FROM Aurora_Sga.dbo.InventarioCabecera WHERE IdInventario = lt.IdInventario)
               AND pl.CodigoAlmacen = lt.CodigoAlmacen
               AND pl.Ubicacion = lt.CodigoUbicacion
               AND pl.CodigoArticulo = lt.CodigoArticulo
               AND (pl.Lote = lt.Partida OR (pl.Lote IS NULL AND lt.Partida IS NULL))
               AND (pl.FechaCaducidad = lt.FechaCaducidad OR (pl.FechaCaducidad IS NULL AND lt.FechaCaducidad IS NULL))
            ), 0)
        AS decimal(18,6))
    FROM Aurora_Sga.dbo.InventarioLineasTemp lt
    WHERE lt.PaletId IS NULL
      AND lt.Consolidado = 0;
    
    SET @LineasSuelto = @@ROWCOUNT;
    PRINT CONCAT('Actualizadas ', @LineasSuelto, ' líneas SUELTAS');
    
    SET @LineasActualizadas = @LineasPalet + @LineasSuelto;
    
    PRINT '';
    PRINT CONCAT('✅ Recálculo completado. Total líneas actualizadas: ', @LineasActualizadas);
    PRINT '   - Líneas de PALET: ', @LineasPalet;
    PRINT '   - Líneas SUELTAS: ', @LineasSuelto;
    
    COMMIT TRANSACTION;
    PRINT '';
    PRINT '✅ Transacción confirmada. Los valores ahora tienen precisión de 6 decimales.';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT '';
    PRINT '❌ ERROR al recalcular:';
    PRINT ERROR_MESSAGE();
    PRINT '';
    PRINT '⚠️ Transacción revertida. No se aplicaron cambios.';
END CATCH
GO

