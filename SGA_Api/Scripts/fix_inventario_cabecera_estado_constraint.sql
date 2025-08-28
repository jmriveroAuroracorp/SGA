-- Script para actualizar la constraint de estado de InventarioCabecera
-- Permite los nuevos estados: EN_CONTEO, CONSOLIDADO

USE [AURORA_SGA]
GO

-- Primero, vamos a ver qué constraints existen actualmente
SELECT CONSTRAINT_NAME, CHECK_CLAUSE 
FROM INFORMATION_SCHEMA.CHECK_CONSTRAINTS 
WHERE CONSTRAINT_NAME IN (
    SELECT CONSTRAINT_NAME 
    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
    WHERE TABLE_NAME = 'InventarioCabecera' 
    AND CONSTRAINT_TYPE = 'CHECK'
)
GO

-- Eliminar la constraint de estado existente
-- La constraint actual es: ([Estado]='CERRADO' OR [Estado]='PENDIENTE_CIERRE' OR [Estado]='ABIERTO')
-- Necesitamos encontrar su nombre exacto

DECLARE @constraintName NVARCHAR(128)

SELECT @constraintName = tc.CONSTRAINT_NAME
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
INNER JOIN INFORMATION_SCHEMA.CHECK_CONSTRAINTS cc ON tc.CONSTRAINT_NAME = cc.CONSTRAINT_NAME
WHERE tc.TABLE_NAME = 'InventarioCabecera' 
AND tc.CONSTRAINT_TYPE = 'CHECK'
AND cc.CHECK_CLAUSE LIKE '%Estado%'

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

-- Verificar que podemos insertar/actualizar con los nuevos estados
PRINT 'Verificando que los nuevos estados son válidos...'
GO

-- Probar actualizar un inventario existente (solo para verificar, no se ejecutará realmente)
-- UPDATE [dbo].[InventarioCabecera] SET [Estado] = 'EN_CONTEO' WHERE [Estado] = 'ABIERTO'
-- UPDATE [dbo].[InventarioCabecera] SET [Estado] = 'CONSOLIDADO' WHERE [Estado] = 'EN_CONTEO'

PRINT 'Script completado exitosamente'
GO 