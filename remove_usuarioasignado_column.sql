USE [AURORA_SGA]
GO

-- Eliminar la columna UsuarioAsignado de OrdenTraspasoCabecera si existe
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.OrdenTraspasoCabecera') AND name = 'UsuarioAsignado')
BEGIN
    ALTER TABLE [dbo].[OrdenTraspasoCabecera] DROP COLUMN [UsuarioAsignado];
    PRINT 'Columna UsuarioAsignado eliminada de OrdenTraspasoCabecera';
END
ELSE
BEGIN
    PRINT 'La columna UsuarioAsignado no existe en OrdenTraspasoCabecera';
END
GO

-- Verificar que la columna fue eliminada
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'OrdenTraspasoCabecera' 
    AND COLUMN_NAME = 'UsuarioAsignado';
GO

PRINT 'Script completado. La columna UsuarioAsignado ha sido eliminada de OrdenTraspasoCabecera.';
