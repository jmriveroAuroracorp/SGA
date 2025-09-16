USE [AURORA_SGA]
GO

-- Modificar IdOperarioAsignado para que NO acepte NULL
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.OrdenTraspasoLineas') AND name = 'IdOperarioAsignado' AND is_nullable = 1)
BEGIN
    -- Primero, actualizar todos los registros NULL a un valor por defecto (por ejemplo, 0)
    UPDATE [dbo].[OrdenTraspasoLineas] 
    SET [IdOperarioAsignado] = 0 
    WHERE [IdOperarioAsignado] IS NULL;
    
    -- Luego, modificar la columna para que no acepte NULL
    ALTER TABLE [dbo].[OrdenTraspasoLineas] ALTER COLUMN [IdOperarioAsignado] INT NOT NULL;
    PRINT 'Columna IdOperarioAsignado modificada para NO aceptar NULL';
END
ELSE
BEGIN
    PRINT 'La columna IdOperarioAsignado ya no acepta NULL o no existe';
END
GO

-- Verificar que la columna existe y no acepta NULL
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'OrdenTraspasoLineas' 
    AND COLUMN_NAME = 'IdOperarioAsignado';
GO

PRINT 'Script completado. IdOperarioAsignado ahora es NOT NULL.';
