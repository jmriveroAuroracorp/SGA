-- Script para agregar información de palets a la tabla InventarioLineasTemp
-- Este script permite rastrear qué palet pertenece cada línea de inventario

-- Verificar si la tabla existe
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'InventarioLineasTemp')
BEGIN
    -- Agregar columna PaletId si no existe
    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                   WHERE TABLE_NAME = 'InventarioLineasTemp' AND COLUMN_NAME = 'PaletId')
    BEGIN
        ALTER TABLE InventarioLineasTemp 
        ADD PaletId UNIQUEIDENTIFIER NULL;
        
        PRINT 'Columna PaletId agregada a InventarioLineasTemp';
    END
    
    -- Agregar columna CodigoPalet si no existe
    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                   WHERE TABLE_NAME = 'InventarioLineasTemp' AND COLUMN_NAME = 'CodigoPalet')
    BEGIN
        ALTER TABLE InventarioLineasTemp 
        ADD CodigoPalet NVARCHAR(50) NULL;
        
        PRINT 'Columna CodigoPalet agregada a InventarioLineasTemp';
    END
    
    -- Agregar columna EstadoPalet si no existe
    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                   WHERE TABLE_NAME = 'InventarioLineasTemp' AND COLUMN_NAME = 'EstadoPalet')
    BEGIN
        ALTER TABLE InventarioLineasTemp 
        ADD EstadoPalet NVARCHAR(20) NULL;
        
        PRINT 'Columna EstadoPalet agregada a InventarioLineasTemp';
    END
    
    PRINT 'Todas las columnas de información de palets han sido agregadas correctamente.';
END
ELSE
BEGIN
    PRINT 'La tabla InventarioLineasTemp no existe. Verificar el nombre de la tabla.';
END 