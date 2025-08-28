-- Script para corregir el DEFAULT de StockActual en InventarioLineasTemp
-- El problema es que DEFAULT 0 está causando que se guarde 0.0000 en lugar del valor real

-- 1. Encontrar el nombre de la constraint actual
DECLARE @ConstraintName NVARCHAR(128)
SELECT @ConstraintName = dc.name
FROM sys.default_constraints dc
JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
WHERE c.name = 'StockActual' AND OBJECT_NAME(dc.parent_object_id) = 'InventarioLineasTemp'

-- 2. Eliminar el DEFAULT actual si existe
IF @ConstraintName IS NOT NULL
BEGIN
    DECLARE @DropSQL NVARCHAR(MAX) = 'ALTER TABLE InventarioLineasTemp DROP CONSTRAINT ' + @ConstraintName
    EXEC sp_executesql @DropSQL
    PRINT 'Constraint eliminada: ' + @ConstraintName
END
ELSE
BEGIN
    PRINT 'No se encontró constraint para StockActual'
END

-- 3. Agregar un nuevo DEFAULT que permita valores más precisos
ALTER TABLE InventarioLineasTemp 
ADD CONSTRAINT DF_InventarioLineasTemp_StockActual DEFAULT 0.0000 FOR StockActual;

PRINT 'Nueva constraint agregada: DF_InventarioLineasTemp_StockActual'

-- 4. Verificar la estructura actualizada
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    NUMERIC_PRECISION,
    NUMERIC_SCALE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'InventarioLineasTemp' 
AND COLUMN_NAME = 'StockActual';

-- 5. Verificar que los registros existentes no se vean afectados
SELECT TOP 5 
    CodigoArticulo,
    CodigoUbicacion,
    StockActual,
    CantidadContada
FROM InventarioLineasTemp 
WHERE CodigoArticulo = '11080' 
AND CodigoUbicacion = 'UB001004001003'
ORDER BY FechaConteo DESC; 