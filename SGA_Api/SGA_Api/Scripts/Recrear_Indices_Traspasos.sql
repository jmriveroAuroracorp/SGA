-- Script para recrear los índices eliminados en la tabla Traspasos
-- Estos índices fueron eliminados para permitir cambiar la precisión de Cantidad a decimal(18,6)

USE [AURORA_SGA]
GO

-- Recrear IX_traspasos_FechaInicio
-- Este índice probablemente incluye Cantidad como columna incluida para optimizar consultas por fecha
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_traspasos_FechaInicio' AND object_id = OBJECT_ID('dbo.Traspasos'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_traspasos_FechaInicio] 
    ON [dbo].[Traspasos] ([FechaInicio])
    INCLUDE ([Cantidad]); -- Cantidad como columna incluida para evitar lookups adicionales
    PRINT 'Índice recreado: IX_traspasos_FechaInicio';
END
ELSE
BEGIN
    PRINT 'El índice IX_traspasos_FechaInicio ya existe';
END
GO

-- Recrear IX_traspasos_FechaInicio_UsuarioInicioId
-- Este índice probablemente incluye Cantidad como columna incluida para optimizar consultas por fecha y usuario
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_traspasos_FechaInicio_UsuarioInicioId' AND object_id = OBJECT_ID('dbo.Traspasos'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_traspasos_FechaInicio_UsuarioInicioId] 
    ON [dbo].[Traspasos] ([FechaInicio], [UsuarioInicioId])
    INCLUDE ([Cantidad]); -- Cantidad como columna incluida para evitar lookups adicionales
    PRINT 'Índice recreado: IX_traspasos_FechaInicio_UsuarioInicioId';
END
ELSE
BEGIN
    PRINT 'El índice IX_traspasos_FechaInicio_UsuarioInicioId ya existe';
END
GO

PRINT 'Script completado. Los índices han sido recreados.';
GO

