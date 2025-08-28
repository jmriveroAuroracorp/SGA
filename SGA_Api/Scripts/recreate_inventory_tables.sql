-- =========================================
-- SCRIPT COMPLETO PARA RECREAR TABLAS DE INVENTARIO
-- =========================================

-- =========================================
-- ELIMINAR TABLAS EXISTENTES (en orden correcto por FK)
-- =========================================

-- Eliminar en orden inverso por las Foreign Keys
DROP TABLE IF EXISTS InventarioAjustes;
DROP TABLE IF EXISTS InventarioLineas;
DROP TABLE IF EXISTS InventarioLineasTemp;
DROP TABLE IF EXISTS InventarioCabecera;
GO

-- =========================================
-- RECREAR TABLAS
-- =========================================

-- 1. Cabecera de Inventario
CREATE TABLE InventarioCabecera (
    IdInventario UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    IdSecuencial INT IDENTITY(1,1) NOT NULL,
    CodigoEmpresa SMALLINT NOT NULL,
    CodigoAlmacen VARCHAR(10) NOT NULL,
    Zona VARCHAR(20) NULL,
    RangoUbicaciones VARCHAR(50) NULL,
    TipoInventario VARCHAR(10) NOT NULL CHECK (TipoInventario IN ('TOTAL','PARCIAL')),
    Comentarios NVARCHAR(500) NULL,
    Estado VARCHAR(20) NOT NULL DEFAULT 'ABIERTO' CHECK (Estado IN ('ABIERTO','PENDIENTE_CIERRE','CERRADO')),
    UsuarioCreacionId INT NOT NULL,
    FechaCreacion DATETIME NOT NULL DEFAULT GETDATE(),
    FechaCierre DATETIME NULL
);
GO

-- 2. Líneas de Inventario TEMPORALES
CREATE TABLE InventarioLineasTemp (
    IdTemp UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    IdInventario UNIQUEIDENTIFIER NOT NULL,
    CodigoArticulo VARCHAR(30) NOT NULL,
    CodigoUbicacion VARCHAR(30) NOT NULL,
    CantidadContada DECIMAL(18,4) NULL,
    UsuarioConteoId INT NOT NULL,
    FechaConteo DATETIME NOT NULL DEFAULT GETDATE(),
    Observaciones NVARCHAR(500) NULL,
    Consolidado BIT NOT NULL DEFAULT 0,
    FechaConsolidacion DATETIME NULL,
    UsuarioConsolidacionId INT NULL,
    CONSTRAINT FK_LineasTemp_InventarioCabecera FOREIGN KEY (IdInventario) REFERENCES InventarioCabecera(IdInventario) ON DELETE CASCADE
);
GO

-- CORRECCIÓN: Agregar StockActual con DEFAULT más preciso
ALTER TABLE InventarioLineasTemp 
ADD StockActual DECIMAL(18,4) NOT NULL DEFAULT 0.0000;

-- Añadir campos Partida y FechaCaducidad a InventarioLineasTemp
ALTER TABLE InventarioLineasTemp 
ADD Partida VARCHAR(50) NULL,
    FechaCaducidad DATETIME NULL;
GO

-- 3. Líneas de Inventario CONSOLIDADAS
CREATE TABLE InventarioLineas (
    IdLinea UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    IdInventario UNIQUEIDENTIFIER NOT NULL,
    CodigoArticulo VARCHAR(30) NOT NULL,
    CodigoUbicacion VARCHAR(30) NOT NULL,
    StockTeorico DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    StockContado DECIMAL(18,4) NULL,
    Estado VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE' CHECK (Estado IN ('PENDIENTE','CONTADA','REVISAR')),
    UsuarioValidacionId INT NULL,
    FechaValidacion DATETIME NULL,
    Observaciones NVARCHAR(500) NULL,
    CONSTRAINT FK_Lineas_InventarioCabecera FOREIGN KEY (IdInventario) REFERENCES InventarioCabecera(IdInventario) ON DELETE CASCADE
);
GO

-- Añadir campos Partida y FechaCaducidad a InventarioLineas
ALTER TABLE InventarioLineas 
ADD Partida VARCHAR(50) NULL,
    FechaCaducidad DATETIME NULL;
GO

-- 4. Ajustes de Inventario
CREATE TABLE InventarioAjustes (
    IdAjuste UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    IdInventario UNIQUEIDENTIFIER NOT NULL,
    CodigoArticulo VARCHAR(30) NOT NULL,
    CodigoUbicacion VARCHAR(30) NOT NULL,
    Diferencia DECIMAL(18,4) NOT NULL,
    TipoAjuste VARCHAR(10) NOT NULL CHECK (TipoAjuste IN ('POSITIVO','NEGATIVO')),
    UsuarioId INT NOT NULL,
    Fecha DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Ajustes_InventarioCabecera FOREIGN KEY (IdInventario) REFERENCES InventarioCabecera(IdInventario) ON DELETE CASCADE
);
GO

-- =========================================
-- ÍNDICES recomendados
-- =========================================

-- Índices por Inventario (para joins y búsquedas rápidas)
CREATE INDEX IX_LineasTemp_IdInventario ON InventarioLineasTemp(IdInventario);
CREATE INDEX IX_Lineas_IdInventario ON InventarioLineas(IdInventario);
CREATE INDEX IX_Ajustes_IdInventario ON InventarioAjustes(IdInventario);

-- Índices por Artículo + Ubicación (consultas típicas)
CREATE INDEX IX_LineasTemp_Articulo_Ubicacion ON InventarioLineasTemp(CodigoArticulo, CodigoUbicacion);
CREATE INDEX IX_Lineas_Articulo_Ubicacion ON InventarioLineas(CodigoArticulo, CodigoUbicacion);
CREATE INDEX IX_Ajustes_Articulo_Ubicacion ON InventarioAjustes(CodigoArticulo, CodigoUbicacion);

-- Índices adicionales para consultas frecuentes
CREATE INDEX IX_InventarioCabecera_Empresa_Almacen ON InventarioCabecera(CodigoEmpresa, CodigoAlmacen);
CREATE INDEX IX_InventarioCabecera_Estado ON InventarioCabecera(Estado);
CREATE INDEX IX_InventarioCabecera_FechaCreacion ON InventarioCabecera(FechaCreacion);
GO

-- =========================================
-- PROCEDIMIENTO ALMACENADO
-- =========================================

CREATE OR ALTER PROCEDURE ConsolidarInventarioLineas 
    @IdInventario UNIQUEIDENTIFIER,
    @UsuarioValidacionId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    -- 1. Insertar o actualizar líneas definitivas desde las temporales
    MERGE InventarioLineas AS target
    USING (
        SELECT 
            IdInventario, 
            CodigoArticulo, 
            CodigoUbicacion, 
            SUM(CantidadContada) AS StockContado
        FROM InventarioLineasTemp 
        WHERE IdInventario = @IdInventario AND Consolidado = 0
        GROUP BY IdInventario, CodigoArticulo, CodigoUbicacion
    ) AS source
    ON (target.IdInventario = source.IdInventario 
        AND target.CodigoArticulo = source.CodigoArticulo 
        AND target.CodigoUbicacion = source.CodigoUbicacion)
    WHEN MATCHED THEN 
        UPDATE SET 
            target.StockContado = source.StockContado,
            target.Estado = 'CONTADA',
            target.UsuarioValidacionId = @UsuarioValidacionId,
            target.FechaValidacion = GETDATE()
    WHEN NOT MATCHED BY TARGET THEN 
        INSERT (IdInventario, CodigoArticulo, CodigoUbicacion, StockTeorico, StockContado, Estado, UsuarioValidacionId, FechaValidacion)
        VALUES (source.IdInventario, source.CodigoArticulo, source.CodigoUbicacion, 0.0000, source.StockContado, 'CONTADA', @UsuarioValidacionId, GETDATE());

    -- 2. Marcar temporales como consolidadas
    UPDATE InventarioLineasTemp 
    SET Consolidado = 1,
        FechaConsolidacion = GETDATE(),
        UsuarioConsolidacionId = @UsuarioValidacionId
    WHERE IdInventario = @IdInventario AND Consolidado = 0;
END
GO

-- =========================================
-- VERIFICACIÓN FINAL
-- =========================================

-- Verificar que las tablas se crearon correctamente
SELECT 
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE,
    NUMERIC_PRECISION,
    NUMERIC_SCALE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME IN ('InventarioCabecera', 'InventarioLineasTemp', 'InventarioLineas', 'InventarioAjustes')
ORDER BY TABLE_NAME, ORDINAL_POSITION;

PRINT '=== TABLAS DE INVENTARIO RECREADAS CORRECTAMENTE ===';
PRINT 'StockActual ahora tiene DEFAULT 0.0000 en lugar de 0';
PRINT 'Esto debería resolver el problema de precisión decimal'; 