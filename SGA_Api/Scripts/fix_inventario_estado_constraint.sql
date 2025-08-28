-- Script para actualizar la constraint de estado del inventario
-- Permite los nuevos estados: EN_CONTEO, CONSOLIDADO

USE [AURORA_SGA]
GO

-- Primero, vamos a ver qué constraint existe actualmente
SELECT CONSTRAINT_NAME, CHECK_CLAUSE 
FROM INFORMATION_SCHEMA.CHECK_CONSTRAINTS 
WHERE CONSTRAINT_NAME LIKE '%Inventario%Estado%' OR CONSTRAINT_NAME LIKE '%Inventari%Estado%'
GO

-- Intentar eliminar la constraint existente (si existe)
DECLARE @constraintName NVARCHAR(128)

SELECT @constraintName = CONSTRAINT_NAME 
FROM INFORMATION_SCHEMA.CHECK_CONSTRAINTS 
WHERE CONSTRAINT_NAME LIKE '%Inventario%Estado%' OR CONSTRAINT_NAME LIKE '%Inventari%Estado%'

IF @constraintName IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = 'ALTER TABLE [dbo].[InventarioCabecera] DROP CONSTRAINT [' + @constraintName + ']'
    EXEC sp_executesql @sql
    PRINT 'Constraint eliminada: ' + @constraintName
END
ELSE
BEGIN
    PRINT 'No se encontró constraint de estado para InventarioCabecera'
END
GO

-- Crear la nueva constraint con todos los estados válidos
ALTER TABLE [dbo].[InventarioCabecera] 
ADD CONSTRAINT [CK_InventarioCabecera_Estado] 
CHECK ([Estado] IN ('ABIERTO', 'EN_CONTEO', 'CONSOLIDADO', 'PENDIENTE_CIERRE', 'CERRADO'))
GO

PRINT 'Nueva constraint creada con estados: ABIERTO, EN_CONTEO, CONSOLIDADO, PENDIENTE_CIERRE, CERRADO'
GO

-- Verificar que la constraint se creó correctamente
SELECT CONSTRAINT_NAME, CHECK_CLAUSE 
FROM INFORMATION_SCHEMA.CHECK_CONSTRAINTS 
WHERE CONSTRAINT_NAME = 'CK_InventarioCabecera_Estado'
GO 