-- Script para modificar las columnas de OrdenTraspasoLineas que necesitan aceptar NULL
-- Ejecutar este script en la base de datos AURORA_SGA

USE [AURORA_SGA]
GO

-- Modificar las columnas que se completarán desde mobility para que acepten NULL
-- Estas columnas se rellenarán cuando se ejecute el traspaso desde mobility

-- CodigoAlmacenOrigen - se completará desde mobility
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OrdenTraspasoLineas]') AND name = 'CodigoAlmacenOrigen')
BEGIN
    ALTER TABLE [dbo].[OrdenTraspasoLineas] ALTER COLUMN [CodigoAlmacenOrigen] [varchar](10) NULL
    PRINT 'Columna CodigoAlmacenOrigen modificada para aceptar NULL'
END
GO

-- UbicacionOrigen - se completará desde mobility
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OrdenTraspasoLineas]') AND name = 'UbicacionOrigen')
BEGIN
    ALTER TABLE [dbo].[OrdenTraspasoLineas] ALTER COLUMN [UbicacionOrigen] [varchar](30) NULL
    PRINT 'Columna UbicacionOrigen modificada para aceptar NULL'
END
GO

-- Partida - se completará desde mobility
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OrdenTraspasoLineas]') AND name = 'Partida')
BEGIN
    ALTER TABLE [dbo].[OrdenTraspasoLineas] ALTER COLUMN [Partida] [varchar](50) NULL
    PRINT 'Columna Partida modificada para aceptar NULL'
END
GO

-- PaletOrigen - se completará desde mobility
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OrdenTraspasoLineas]') AND name = 'PaletOrigen')
BEGIN
    ALTER TABLE [dbo].[OrdenTraspasoLineas] ALTER COLUMN [PaletOrigen] [nvarchar](50) NULL
    PRINT 'Columna PaletOrigen modificada para aceptar NULL'
END
GO

-- UbicacionDestino - se completará desde mobility
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OrdenTraspasoLineas]') AND name = 'UbicacionDestino')
BEGIN
    ALTER TABLE [dbo].[OrdenTraspasoLineas] ALTER COLUMN [UbicacionDestino] [varchar](30) NULL
    PRINT 'Columna UbicacionDestino modificada para aceptar NULL'
END
GO

-- PaletDestino - se completará desde mobility
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OrdenTraspasoLineas]') AND name = 'PaletDestino')
BEGIN
    ALTER TABLE [dbo].[OrdenTraspasoLineas] ALTER COLUMN [PaletDestino] [nvarchar](50) NULL
    PRINT 'Columna PaletDestino modificada para aceptar NULL'
END
GO

-- CantidadMovida - se completará desde mobility
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OrdenTraspasoLineas]') AND name = 'CantidadMovida')
BEGIN
    ALTER TABLE [dbo].[OrdenTraspasoLineas] ALTER COLUMN [CantidadMovida] [decimal](18, 4) NULL
    PRINT 'Columna CantidadMovida modificada para aceptar NULL'
END
GO

-- FechaInicio - se completará desde mobility
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OrdenTraspasoLineas]') AND name = 'FechaInicio')
BEGIN
    ALTER TABLE [dbo].[OrdenTraspasoLineas] ALTER COLUMN [FechaInicio] [datetime] NULL
    PRINT 'Columna FechaInicio modificada para aceptar NULL'
END
GO

-- FechaFinalizacion - se completará desde mobility
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OrdenTraspasoLineas]') AND name = 'FechaFinalizacion')
BEGIN
    ALTER TABLE [dbo].[OrdenTraspasoLineas] ALTER COLUMN [FechaFinalizacion] [datetime] NULL
    PRINT 'Columna FechaFinalizacion modificada para aceptar NULL'
END
GO

-- IdTraspaso - se completará desde mobility
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OrdenTraspasoLineas]') AND name = 'IdTraspaso')
BEGIN
    ALTER TABLE [dbo].[OrdenTraspasoLineas] ALTER COLUMN [IdTraspaso] [uniqueidentifier] NULL
    PRINT 'Columna IdTraspaso modificada para aceptar NULL'
END
GO

-- FechaCaducidad - se completará desde mobility
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OrdenTraspasoLineas]') AND name = 'FechaCaducidad')
BEGIN
    ALTER TABLE [dbo].[OrdenTraspasoLineas] ALTER COLUMN [FechaCaducidad] [datetime] NULL
    PRINT 'Columna FechaCaducidad modificada para aceptar NULL'
END
GO

PRINT 'Script completado. Las columnas han sido modificadas para aceptar NULL.'
GO
