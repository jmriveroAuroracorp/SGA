-- Script para eliminar la columna IdOrigen de la tabla OrdenTraspasoCabecera
-- Ejecutar este script en la base de datos AURORA_SGA

USE [AURORA_SGA]
GO

-- Eliminar la columna IdOrigen si existe
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OrdenTraspasoCabecera]') AND name = 'IdOrigen')
BEGIN
    ALTER TABLE [dbo].[OrdenTraspasoCabecera] DROP COLUMN [IdOrigen]
    PRINT 'Columna IdOrigen eliminada exitosamente de OrdenTraspasoCabecera'
END
ELSE
BEGIN
    PRINT 'La columna IdOrigen no existe en OrdenTraspasoCabecera'
END
GO
