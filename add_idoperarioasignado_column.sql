USE [AURORA_SGA]
GO

-- Agregar la columna IdOperarioAsignado a OrdenTraspasoCabecera si no existe
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.OrdenTraspasoCabecera') AND name = 'IdOperarioAsignado')
BEGIN
    ALTER TABLE [dbo].[OrdenTraspasoCabecera] ADD [IdOperarioAsignado] INT NULL;
    PRINT 'Columna IdOperarioAsignado agregada a OrdenTraspasoCabecera';
END
ELSE
BEGIN
    PRINT 'La columna IdOperarioAsignado ya existe en OrdenTraspasoCabecera';
END
GO

-- Verificar que la columna existe
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'OrdenTraspasoCabecera' 
    AND COLUMN_NAME = 'IdOperarioAsignado';
GO

PRINT 'Script completado. Verificar que IdOperarioAsignado existe en OrdenTraspasoCabecera.';
