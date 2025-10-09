-- =====================================================
-- MIGRACIÓN MULTIALMACÉN - INVENTARIOS
-- Fecha: 29/08/2025
-- Descripción: Permite inventarios que abarquen múltiples almacenes
-- =====================================================

USE [AURORA_SGA]
GO

-- =====================================================
-- FASE 1: CREAR NUEVA TABLA InventarioAlmacenes
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[InventarioAlmacenes]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[InventarioAlmacenes](
        [Id] [uniqueidentifier] NOT NULL,
        [IdInventario] [uniqueidentifier] NOT NULL,
        [CodigoAlmacen] [varchar](10) NOT NULL,
        [CodigoEmpresa] [smallint] NOT NULL,
        [FechaCreacion] [datetime] NOT NULL,
        PRIMARY KEY CLUSTERED ([Id] ASC)
        WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY],
        CONSTRAINT [UK_InventarioAlmacenes_Inventario_Almacen] UNIQUE([IdInventario], [CodigoAlmacen])
    ) ON [PRIMARY]
    
    -- Defaults
    ALTER TABLE [dbo].[InventarioAlmacenes] ADD DEFAULT (newid()) FOR [Id]
    ALTER TABLE [dbo].[InventarioAlmacenes] ADD DEFAULT (getdate()) FOR [FechaCreacion]
    
    -- Foreign Key
    ALTER TABLE [dbo].[InventarioAlmacenes] WITH CHECK ADD 
        CONSTRAINT [FK_InventarioAlmacenes_Cabecera] FOREIGN KEY([IdInventario])
        REFERENCES [dbo].[InventarioCabecera] ([IdInventario])
        ON DELETE CASCADE
    
    ALTER TABLE [dbo].[InventarioAlmacenes] CHECK CONSTRAINT [FK_InventarioAlmacenes_Cabecera]
    
    PRINT '✓ Tabla InventarioAlmacenes creada correctamente'
END
ELSE
BEGIN
    PRINT '⚠️ Tabla InventarioAlmacenes ya existe'
END
GO

-- =====================================================
-- FASE 2: CREAR ÍNDICES PARA RENDIMIENTO
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_InventarioAlmacenes_IdInventario')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_InventarioAlmacenes_IdInventario] 
    ON [dbo].[InventarioAlmacenes]([IdInventario])
    INCLUDE ([CodigoAlmacen], [CodigoEmpresa])
    
    PRINT '✓ Índice IX_InventarioAlmacenes_IdInventario creado'
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_InventarioAlmacenes_CodigoAlmacen')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_InventarioAlmacenes_CodigoAlmacen] 
    ON [dbo].[InventarioAlmacenes]([CodigoAlmacen], [CodigoEmpresa])
    INCLUDE ([IdInventario])
    
    PRINT '✓ Índice IX_InventarioAlmacenes_CodigoAlmacen creado'
END
GO

-- =====================================================
-- FASE 3: MIGRAR DATOS EXISTENTES
-- =====================================================
DECLARE @RegistrosMigrados INT = 0

-- Verificar si ya hay datos migrados
IF NOT EXISTS (SELECT 1 FROM [dbo].[InventarioAlmacenes])
BEGIN
    -- Migrar todos los inventarios existentes
    INSERT INTO [dbo].[InventarioAlmacenes] (IdInventario, CodigoAlmacen, CodigoEmpresa, FechaCreacion)
    SELECT 
        IdInventario, 
        CodigoAlmacen, 
        CodigoEmpresa, 
        FechaCreacion
    FROM [dbo].[InventarioCabecera]
    WHERE CodigoAlmacen IS NOT NULL
    
    SET @RegistrosMigrados = @@ROWCOUNT
    PRINT '✓ Migrados ' + CAST(@RegistrosMigrados AS VARCHAR(10)) + ' registros de inventarios existentes'
END
ELSE
BEGIN
    PRINT '⚠️ Ya existen datos en InventarioAlmacenes, saltando migración'
END
GO

-- =====================================================
-- FASE 4: CREAR VISTA DE COMPATIBILIDAD
-- =====================================================
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vInventarioConAlmacenes')
    DROP VIEW [dbo].[vInventarioConAlmacenes]
GO

CREATE VIEW [dbo].[vInventarioConAlmacenes]
AS
SELECT 
    ic.*,
    -- Para compatibilidad: primer almacén como almacén principal
    ia_principal.CodigoAlmacen AS AlmacenPrincipal,
    -- Lista de todos los almacenes
    STUFF((
        SELECT ', ' + ia.CodigoAlmacen
        FROM [dbo].[InventarioAlmacenes] ia
        WHERE ia.IdInventario = ic.IdInventario
        ORDER BY ia.CodigoAlmacen
        FOR XML PATH('')
    ), 1, 2, '') AS AlmacenesIncluidos,
    -- Contador de almacenes
    (SELECT COUNT(*) FROM [dbo].[InventarioAlmacenes] ia2 WHERE ia2.IdInventario = ic.IdInventario) AS NumeroAlmacenes,
    -- Indicador multialmacén
    CASE 
        WHEN (SELECT COUNT(*) FROM [dbo].[InventarioAlmacenes] ia3 WHERE ia3.IdInventario = ic.IdInventario) > 1 
        THEN 1 
        ELSE 0 
    END AS EsMultialmacen
FROM [dbo].[InventarioCabecera] ic
LEFT JOIN [dbo].[InventarioAlmacenes] ia_principal ON ic.IdInventario = ia_principal.IdInventario
    AND ia_principal.CodigoAlmacen = (
        SELECT TOP 1 CodigoAlmacen 
        FROM [dbo].[InventarioAlmacenes] 
        WHERE IdInventario = ic.IdInventario 
        ORDER BY CodigoAlmacen
    )
GO

PRINT '✓ Vista vInventarioConAlmacenes creada'
GO

-- =====================================================
-- FASE 5: STORED PROCEDURES AUXILIARES
-- =====================================================

-- Procedimiento para agregar almacén a inventario
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_AgregarAlmacenAInventario')
    DROP PROCEDURE [dbo].[sp_AgregarAlmacenAInventario]
GO

CREATE PROCEDURE [dbo].[sp_AgregarAlmacenAInventario]
    @IdInventario UNIQUEIDENTIFIER,
    @CodigoAlmacen VARCHAR(10),
    @CodigoEmpresa SMALLINT
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRY
        -- Verificar que el inventario existe
        IF NOT EXISTS (SELECT 1 FROM [dbo].[InventarioCabecera] WHERE IdInventario = @IdInventario)
        BEGIN
            RAISERROR('El inventario especificado no existe', 16, 1)
            RETURN
        END
        
        -- Verificar que no esté ya agregado
        IF EXISTS (SELECT 1 FROM [dbo].[InventarioAlmacenes] WHERE IdInventario = @IdInventario AND CodigoAlmacen = @CodigoAlmacen)
        BEGIN
            RAISERROR('El almacén ya está incluido en este inventario', 16, 1)
            RETURN
        END
        
        -- Verificar que el inventario esté abierto
        IF EXISTS (SELECT 1 FROM [dbo].[InventarioCabecera] WHERE IdInventario = @IdInventario AND Estado != 'ABIERTO')
        BEGIN
            RAISERROR('Solo se pueden modificar inventarios en estado ABIERTO', 16, 1)
            RETURN
        END
        
        -- Agregar almacén
        INSERT INTO [dbo].[InventarioAlmacenes] (IdInventario, CodigoAlmacen, CodigoEmpresa)
        VALUES (@IdInventario, @CodigoAlmacen, @CodigoEmpresa)
        
        SELECT 'Almacén agregado correctamente' AS Resultado
        
    END TRY
    BEGIN CATCH
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE()
        RAISERROR(@ErrorMessage, 16, 1)
    END CATCH
END
GO

-- Procedimiento para obtener almacenes de un inventario
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_ObtenerAlmacenesInventario')
    DROP PROCEDURE [dbo].[sp_ObtenerAlmacenesInventario]
GO

CREATE PROCEDURE [dbo].[sp_ObtenerAlmacenesInventario]
    @IdInventario UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        ia.CodigoAlmacen,
        ia.CodigoEmpresa,
        ia.FechaCreacion
    FROM [dbo].[InventarioAlmacenes] ia
    WHERE ia.IdInventario = @IdInventario
    ORDER BY ia.CodigoAlmacen
END
GO

PRINT '✓ Stored procedures creados'
GO

-- =====================================================
-- FASE 6: VERIFICACIÓN DE LA MIGRACIÓN
-- =====================================================
DECLARE @TotalInventarios INT
DECLARE @TotalRelaciones INT
DECLARE @InventariosSinRelacion INT

SELECT @TotalInventarios = COUNT(*) FROM [dbo].[InventarioCabecera]
SELECT @TotalRelaciones = COUNT(*) FROM [dbo].[InventarioAlmacenes]
SELECT @InventariosSinRelacion = COUNT(*) 
FROM [dbo].[InventarioCabecera] ic
LEFT JOIN [dbo].[InventarioAlmacenes] ia ON ic.IdInventario = ia.IdInventario
WHERE ia.IdInventario IS NULL

PRINT '=================== RESUMEN MIGRACIÓN ==================='
PRINT 'Total inventarios en cabecera: ' + CAST(@TotalInventarios AS VARCHAR(10))
PRINT 'Total relaciones almacén creadas: ' + CAST(@TotalRelaciones AS VARCHAR(10))
PRINT 'Inventarios sin relación almacén: ' + CAST(@InventariosSinRelacion AS VARCHAR(10))

IF @InventariosSinRelacion = 0
    PRINT '✅ MIGRACIÓN COMPLETADA EXITOSAMENTE'
ELSE
    PRINT '⚠️ Hay inventarios sin relación de almacén - revisar'

PRINT '======================================================'

-- Mostrar algunos ejemplos
PRINT 'Ejemplos de inventarios multialmacén:'
SELECT TOP 5 
    CodigoInventario,
    AlmacenesIncluidos,
    NumeroAlmacenes,
    CASE WHEN EsMultialmacen = 1 THEN 'SÍ' ELSE 'NO' END AS MultiAlmacen
FROM [dbo].[vInventarioConAlmacenes]
ORDER BY NumeroAlmacenes DESC, CodigoInventario

GO

PRINT '🚀 Migración multialmacén completada. ¡Ya puedes crear inventarios que abarquen múltiples almacenes!' 