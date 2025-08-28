-- Script para agregar la columna AjusteFinal a la tabla InventarioLineas
-- Ejecutar en la base de datos AuroraSga

-- Agregar la columna AjusteFinal
ALTER TABLE InventarioLineas 
ADD AjusteFinal DECIMAL(18,4) NULL;

-- Comentario para documentar la columna
EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'Ajuste final calculado: StockContado - StockActual', 
    @level0type = N'SCHEMA', @level0name = N'dbo', 
    @level1type = N'TABLE', @level1name = N'InventarioLineas', 
    @level2type = N'COLUMN', @level2name = N'AjusteFinal';

-- Actualizar registros existentes con el ajuste calculado
UPDATE InventarioLineas 
SET AjusteFinal = StockContado - StockActual 
WHERE StockContado IS NOT NULL;

PRINT 'Columna AjusteFinal agregada exitosamente a InventarioLineas'; 