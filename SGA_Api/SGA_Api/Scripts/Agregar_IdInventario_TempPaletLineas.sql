-- Script para agregar la columna InventarioId a la tabla TempPaletLineas
-- Esta columna permite la trazabilidad de ajustes de inventario en las líneas temporales de palets
-- Similar a como ConteoId rastrea los ajustes de conteo

USE [AURORA_SGA]
GO

-- Verificar si la columna ya existe antes de agregarla
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TempPaletLineas') AND name = 'InventarioId')
BEGIN
    -- Agregar la columna InventarioId como nullable GUID
    ALTER TABLE [dbo].[TempPaletLineas]
    ADD [InventarioId] uniqueidentifier NULL;
    
    PRINT 'Columna InventarioId agregada exitosamente a TempPaletLineas';
    
    -- Opcional: Agregar un índice para mejorar las consultas por InventarioId
    -- (similar a como probablemente existe para ConteoId)
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TempPaletLineas_InventarioId' AND object_id = OBJECT_ID('dbo.TempPaletLineas'))
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_TempPaletLineas_InventarioId]
        ON [dbo].[TempPaletLineas] ([InventarioId])
        WHERE [InventarioId] IS NOT NULL; -- Índice filtrado solo para valores no nulos
        PRINT 'Índice IX_TempPaletLineas_InventarioId creado exitosamente';
    END
    ELSE
    BEGIN
        PRINT 'El índice IX_TempPaletLineas_InventarioId ya existe';
    END
END
ELSE
BEGIN
    PRINT 'La columna InventarioId ya existe en TempPaletLineas';
END
GO

PRINT 'Script completado. La columna InventarioId está lista para usar.';
GO

