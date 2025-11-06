-- Script para actualizar la precisión de decimales en la tabla Traspasos
-- Cambia de decimal(18,4) a decimal(18,6) para preservar la precisión completa

USE [AURORA_SGA]
GO

-- Eliminar índices que dependen de la columna Cantidad
-- Nota: Estos índices probablemente incluyen Cantidad como columna incluida (INCLUDE)
-- Los eliminamos temporalmente y los recrearemos después sin cambiar su estructura

IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_traspasos_FechaInicio' AND object_id = OBJECT_ID('dbo.Traspasos'))
BEGIN
    DROP INDEX [IX_traspasos_FechaInicio] ON [dbo].[Traspasos];
    PRINT 'Índice eliminado: IX_traspasos_FechaInicio';
END
GO

IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_traspasos_FechaInicio_UsuarioInicioId' AND object_id = OBJECT_ID('dbo.Traspasos'))
BEGIN
    DROP INDEX [IX_traspasos_FechaInicio_UsuarioInicioId] ON [dbo].[Traspasos];
    PRINT 'Índice eliminado: IX_traspasos_FechaInicio_UsuarioInicioId';
END
GO

-- Actualizar la columna Cantidad
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Traspasos') AND name = 'Cantidad')
BEGIN
    ALTER TABLE [dbo].[Traspasos]
    ALTER COLUMN [Cantidad] decimal(18,6) NULL;
    PRINT 'Columna Cantidad actualizada en Traspasos';
END
GO

-- Recrear los índices eliminados
-- Nota: Si estos índices incluían Cantidad como columna incluida (INCLUDE), 
-- puedes recrearlos con la misma estructura y la columna Cantidad ahora tendrá 6 decimales

-- Recrear IX_traspasos_FechaInicio (ajusta la definición según tu necesidad)
-- Ejemplo genérico (ajusta según la estructura real):
-- CREATE NONCLUSTERED INDEX [IX_traspasos_FechaInicio] 
-- ON [dbo].[Traspasos] ([FechaInicio])
-- INCLUDE ([Cantidad]); -- Si Cantidad estaba incluida
-- GO

-- Recrear IX_traspasos_FechaInicio_UsuarioInicioId (ajusta la definición según tu necesidad)
-- Ejemplo genérico (ajusta según la estructura real):
-- CREATE NONCLUSTERED INDEX [IX_traspasos_FechaInicio_UsuarioInicioId] 
-- ON [dbo].[Traspasos] ([FechaInicio], [UsuarioInicioId])
-- INCLUDE ([Cantidad]); -- Si Cantidad estaba incluida
-- GO

PRINT 'Script completado. La columna Cantidad en Traspasos ahora tiene precisión de 6 decimales.';
PRINT '';
PRINT '⚠️ IMPORTANTE: Se eliminaron los índices:';
PRINT '   - IX_traspasos_FechaInicio';
PRINT '   - IX_traspasos_FechaInicio_UsuarioInicioId';
PRINT '';
PRINT '   Para recrearlos, ejecuta:';
PRINT '   sp_helpindex ''Traspasos''; -- Para ver la estructura original';
PRINT '   O recrea los índices manualmente con la misma estructura.';
GO

