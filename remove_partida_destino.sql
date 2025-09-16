-- Script para modificar las columnas de partida en la tabla OrdenTraspasoLineas
-- 1. Renombrar PartidaOrigen a Partida
-- 2. Eliminar PartidaDestino
-- 3. Agregar columna FechaCaducidad
-- Ejecutar en la base de datos AURORA_SGA

USE [AURORA_SGA]
GO

-- Renombrar PartidaOrigen a Partida
EXEC sp_rename 'dbo.OrdenTraspasoLineas.PartidaOrigen', 'Partida', 'COLUMN'
GO

-- Eliminar la columna PartidaDestino
ALTER TABLE [dbo].[OrdenTraspasoLineas] 
DROP COLUMN [PartidaDestino]
GO

-- Agregar columna FechaCaducidad
ALTER TABLE [dbo].[OrdenTraspasoLineas] 
ADD [FechaCaducidad] [datetime] NULL
GO

-- Verificar que la columna se eliminó correctamente
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'OrdenTraspasoLineas' 
AND TABLE_SCHEMA = 'dbo'
ORDER BY ORDINAL_POSITION
GO
