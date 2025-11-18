-- Script para consultar los índices de la tabla Traspasos
-- Ejecutar en SQL Server Management Studio conectado a la base de datos AURORA_SGA

-- 1. Ver todos los índices de la tabla Traspasos (incluyendo claves primarias y foráneas)
SELECT 
    i.name AS NombreIndice,
    i.type_desc AS TipoIndice,
    i.is_unique AS EsUnico,
    i.is_primary_key AS EsClavePrimaria,
    i.is_unique_constraint AS EsConstraintUnico,
    STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS Columnas,
    STRING_AGG(CASE WHEN ic.is_descending_key = 1 THEN c.name + ' DESC' ELSE c.name END, ', ') 
        WITHIN GROUP (ORDER BY ic.key_ordinal) AS ColumnasConOrden
FROM 
    sys.indexes i
    INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
    INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
    INNER JOIN sys.tables t ON i.object_id = t.object_id
WHERE 
    t.name = 'Traspasos'
    AND i.type > 0  -- Excluir heap (tabla sin índice clustered)
GROUP BY 
    i.name, i.type_desc, i.is_unique, i.is_primary_key, i.is_unique_constraint, i.index_id
ORDER BY 
    i.is_primary_key DESC, i.is_unique DESC, i.name;

-- 2. Ver índices con más detalle (incluyendo estadísticas y fragmentación)
SELECT 
    OBJECT_NAME(i.object_id) AS Tabla,
    i.name AS NombreIndice,
    i.type_desc AS TipoIndice,
    i.is_unique AS EsUnico,
    i.is_primary_key AS EsClavePrimaria,
    ps.avg_fragmentation_in_percent AS FragmentacionPorcentaje,
    ps.page_count AS Paginas,
    ps.record_count AS Registros,
    CASE 
        WHEN ps.avg_fragmentation_in_percent > 30 THEN 'REBUILD recomendado'
        WHEN ps.avg_fragmentation_in_percent > 10 THEN 'REORGANIZE recomendado'
        ELSE 'OK'
    END AS Recomendacion
FROM 
    sys.indexes i
    INNER JOIN sys.dm_db_index_physical_stats(DB_ID(), OBJECT_ID('Traspasos'), NULL, NULL, 'LIMITED') ps
        ON i.object_id = ps.object_id AND i.index_id = ps.index_id
WHERE 
    OBJECT_NAME(i.object_id) = 'Traspasos'
    AND i.type > 0
ORDER BY 
    ps.avg_fragmentation_in_percent DESC;

-- 3. Ver qué columnas están indexadas (más simple)
SELECT 
    i.name AS NombreIndice,
    i.type_desc AS Tipo,
    COL_NAME(ic.object_id, ic.column_id) AS Columna,
    ic.key_ordinal AS Orden,
    ic.is_descending_key AS Descendente
FROM 
    sys.indexes i
    INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
WHERE 
    OBJECT_NAME(i.object_id) = 'Traspasos'
ORDER BY 
    i.name, ic.key_ordinal;

-- 4. Verificar si hay índices en las columnas que usamos frecuentemente
SELECT 
    CASE 
        WHEN EXISTS (
            SELECT 1 
            FROM sys.indexes i
            INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
            WHERE OBJECT_NAME(i.object_id) = 'Traspasos' 
            AND c.name = 'UsuarioInicioId'
        ) THEN 'SÍ existe índice en UsuarioInicioId'
        ELSE 'NO existe índice en UsuarioInicioId'
    END AS IndiceUsuarioInicioId,
    CASE 
        WHEN EXISTS (
            SELECT 1 
            FROM sys.indexes i
            INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
            WHERE OBJECT_NAME(i.object_id) = 'Traspasos' 
            AND c.name = 'UsuarioFinalizacionId'
        ) THEN 'SÍ existe índice en UsuarioFinalizacionId'
        ELSE 'NO existe índice en UsuarioFinalizacionId'
    END AS IndiceUsuarioFinalizacionId,
    CASE 
        WHEN EXISTS (
            SELECT 1 
            FROM sys.indexes i
            INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
            WHERE OBJECT_NAME(i.object_id) = 'Traspasos' 
            AND c.name = 'FechaInicio'
        ) THEN 'SÍ existe índice en FechaInicio'
        ELSE 'NO existe índice en FechaInicio'
    END AS IndiceFechaInicio;

-- 5. Script para crear índices recomendados si no existen
-- (Solo ejecutar si los índices no existen y quieres crearlos)

-- Índice compuesto para búsquedas por usuario y fecha (muy útil para nuestras consultas)
IF NOT EXISTS (
    SELECT 1 
    FROM sys.indexes 
    WHERE name = 'IX_Traspasos_UsuarioInicioId_FechaInicio' 
    AND object_id = OBJECT_ID('Traspasos')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Traspasos_UsuarioInicioId_FechaInicio
    ON Traspasos (UsuarioInicioId, FechaInicio DESC)
    INCLUDE (UsuarioFinalizacionId, CodigoEstado, AlmacenOrigen, AlmacenDestino);
    PRINT 'Índice IX_Traspasos_UsuarioInicioId_FechaInicio creado';
END
ELSE
BEGIN
    PRINT 'El índice IX_Traspasos_UsuarioInicioId_FechaInicio ya existe';
END

-- Índice para UsuarioFinalizacionId (por si también se busca por este campo)
IF NOT EXISTS (
    SELECT 1 
    FROM sys.indexes 
    WHERE name = 'IX_Traspasos_UsuarioFinalizacionId_FechaInicio' 
    AND object_id = OBJECT_ID('Traspasos')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Traspasos_UsuarioFinalizacionId_FechaInicio
    ON Traspasos (UsuarioFinalizacionId, FechaInicio DESC)
    WHERE UsuarioFinalizacionId IS NOT NULL;
    PRINT 'Índice IX_Traspasos_UsuarioFinalizacionId_FechaInicio creado';
END
ELSE
BEGIN
    PRINT 'El índice IX_Traspasos_UsuarioFinalizacionId_FechaInicio ya existe';
END

-- Índice para búsquedas solo por fecha (cuando no hay filtro de usuario)
IF NOT EXISTS (
    SELECT 1 
    FROM sys.indexes 
    WHERE name = 'IX_Traspasos_FechaInicio' 
    AND object_id = OBJECT_ID('Traspasos')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Traspasos_FechaInicio
    ON Traspasos (FechaInicio DESC)
    INCLUDE (UsuarioInicioId, UsuarioFinalizacionId, CodigoEstado);
    PRINT 'Índice IX_Traspasos_FechaInicio creado';
END
ELSE
BEGIN
    PRINT 'El índice IX_Traspasos_FechaInicio ya existe';
END

