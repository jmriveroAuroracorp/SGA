-- Script para crear la tabla NotificacionesTeamsCola en AURORA_SGA
-- Esta tabla actúa como cola para procesar notificaciones a Teams cuando hay ERROR_ERP en traspasos

USE [AURORA_SGA]
GO

-- Crear tabla
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[NotificacionesTeamsCola]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[NotificacionesTeamsCola](
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [TraspasoId] UNIQUEIDENTIFIER NOT NULL,
        [Estado] NVARCHAR(20) NOT NULL DEFAULT 'Pendiente',
        [Intentos] INT NOT NULL DEFAULT 0,
        [FechaCreacion] DATETIME NOT NULL DEFAULT GETDATE(),
        [FechaProcesado] DATETIME NULL,
        [ErrorMensaje] NVARCHAR(500) NULL,
        [MensajeError] NVARCHAR(MAX) NULL,
        CONSTRAINT [FK_NotificacionesTeamsCola_Traspasos] 
            FOREIGN KEY ([TraspasoId]) 
            REFERENCES [dbo].[traspasos]([id])
            ON DELETE NO ACTION
            ON UPDATE NO ACTION
    )
    
    PRINT 'Tabla NotificacionesTeamsCola creada exitosamente'
END
ELSE
BEGIN
    PRINT 'La tabla NotificacionesTeamsCola ya existe'
END
GO

-- Crear índice para optimizar consultas de registros pendientes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_NotificacionesTeamsCola_Estado' AND object_id = OBJECT_ID('dbo.NotificacionesTeamsCola'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_NotificacionesTeamsCola_Estado] 
    ON [dbo].[NotificacionesTeamsCola] ([Estado])
    WHERE [Estado] = 'Pendiente'
    
    PRINT 'Índice IX_NotificacionesTeamsCola_Estado creado exitosamente'
END
ELSE
BEGIN
    PRINT 'El índice IX_NotificacionesTeamsCola_Estado ya existe'
END
GO

-- Crear índice adicional en FechaCreacion para ordenar por antigüedad
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_NotificacionesTeamsCola_FechaCreacion' AND object_id = OBJECT_ID('dbo.NotificacionesTeamsCola'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_NotificacionesTeamsCola_FechaCreacion] 
    ON [dbo].[NotificacionesTeamsCola] ([FechaCreacion])
    
    PRINT 'Índice IX_NotificacionesTeamsCola_FechaCreacion creado exitosamente'
END
ELSE
BEGIN
    PRINT 'El índice IX_NotificacionesTeamsCola_FechaCreacion ya existe'
END
GO

PRINT 'Script completado'
GO

