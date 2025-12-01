-- Script para agregar campo TipoBloqueo a la tabla BloqueosCalidad
-- Fecha: 2024
-- Descripción: Permite definir si el bloqueo es TOTAL (bloquea todos los traspasos) 
--              o SOLO_PULMON (solo bloquea traspasos a ubicaciones PULMÓN)

USE [AURORA_SGA]
GO

-- Verificar si la columna ya existe
IF NOT EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = 'dbo'
    AND TABLE_NAME = 'BloqueosCalidad' 
    AND COLUMN_NAME = 'TipoBloqueo'
)
BEGIN
    -- Agregar columna TipoBloqueo con valor por defecto
    ALTER TABLE [dbo].[BloqueosCalidad]
    ADD [TipoBloqueo] [nvarchar](20) NOT NULL DEFAULT ('TOTAL');
    
    -- Agregar comentario/descripción
    EXEC sp_addextendedproperty 
        @name = N'MS_Description', 
        @value = N'Tipo de bloqueo: TOTAL = bloquea todos los traspasos, SOLO_PULMON = solo bloquea traspasos a PULMÓN', 
        @level0type = N'SCHEMA', @level0name = N'dbo', 
        @level1type = N'TABLE', @level1name = N'BloqueosCalidad', 
        @level2type = N'COLUMN', @level2name = N'TipoBloqueo';
    
    PRINT 'Campo TipoBloqueo agregado exitosamente a BloqueosCalidad';
    PRINT 'Todos los bloqueos existentes se han establecido con TipoBloqueo = TOTAL por defecto';
END
ELSE
BEGIN
    PRINT 'El campo TipoBloqueo ya existe en BloqueosCalidad';
END
GO

