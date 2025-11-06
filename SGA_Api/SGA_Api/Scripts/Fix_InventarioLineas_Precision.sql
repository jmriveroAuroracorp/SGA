-- Script para actualizar la precisión de decimales en las tablas de inventario
-- Cambia de decimal(18,4) a decimal(18,6) para preservar la precisión completa

USE [AURORA_SGA]
GO

-- 1. Actualizar InventarioLineasTemp
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.InventarioLineasTemp') AND name = 'StockActual')
BEGIN
    ALTER TABLE [dbo].[InventarioLineasTemp]
    ALTER COLUMN [StockActual] decimal(18,6) NOT NULL;
    PRINT 'Columna StockActual actualizada en InventarioLineasTemp';
END
GO

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.InventarioLineasTemp') AND name = 'CantidadContada')
BEGIN
    ALTER TABLE [dbo].[InventarioLineasTemp]
    ALTER COLUMN [CantidadContada] decimal(18,6) NULL;
    PRINT 'Columna CantidadContada actualizada en InventarioLineasTemp';
END
GO

-- 2. Actualizar InventarioLineas
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.InventarioLineas') AND name = 'StockActual')
BEGIN
    ALTER TABLE [dbo].[InventarioLineas]
    ALTER COLUMN [StockActual] decimal(18,6) NOT NULL;
    PRINT 'Columna StockActual actualizada en InventarioLineas';
END
GO

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.InventarioLineas') AND name = 'StockTeorico')
BEGIN
    ALTER TABLE [dbo].[InventarioLineas]
    ALTER COLUMN [StockTeorico] decimal(18,6) NOT NULL;
    PRINT 'Columna StockTeorico actualizada en InventarioLineas';
END
GO

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.InventarioLineas') AND name = 'StockContado')
BEGIN
    ALTER TABLE [dbo].[InventarioLineas]
    ALTER COLUMN [StockContado] decimal(18,6) NULL;
    PRINT 'Columna StockContado actualizada en InventarioLineas';
END
GO

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.InventarioLineas') AND name = 'AjusteFinal')
BEGIN
    ALTER TABLE [dbo].[InventarioLineas]
    ALTER COLUMN [AjusteFinal] decimal(18,6) NULL;
    PRINT 'Columna AjusteFinal actualizada en InventarioLineas';
END
GO

-- 3. IMPORTANTE: Después de cambiar el tipo de columna, los datos existentes 
--    seguirán teniendo 4 decimales. Si necesitas recalcular los valores,
--    deberás regenerar las líneas temporales del inventario desde el API
--    o usar el siguiente script para recalcular StockActual desde vStockDisponible
--
--    NOTA: Este script NO recalcula automáticamente porque StockActual puede
--    venir de cálculos complejos (palets, stock suelto, etc.). Lo mejor es
--    regenerar las líneas temporales desde el endpoint de generar líneas.

PRINT 'Script completado. Todas las columnas de inventario ahora tienen precisión de 6 decimales.';
PRINT '';
PRINT '⚠️ IMPORTANTE: Los datos existentes seguirán mostrando 4 decimales.';
PRINT '   Para ver los valores con 6 decimales:';
PRINT '   1. Regenera las líneas temporales del inventario desde el API';
PRINT '   2. O crea un nuevo inventario y las nuevas líneas tendrán 6 decimales';
GO

