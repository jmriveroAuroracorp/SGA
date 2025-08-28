-- Verificar la estructura correcta de la tabla Traspasos
SELECT TOP 1 * FROM Traspasos;

-- Verificar columnas de Traspasos
SELECT COLUMN_NAME, DATA_TYPE 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Traspasos'
ORDER BY ORDINAL_POSITION;

-- Consulta simple para ver traspasos de los palets problemáticos
SELECT * FROM Traspasos 
WHERE PaletId IN (
    SELECT Id FROM Palets 
    WHERE Codigo IN ('PAL25-0000111', 'PAL25-0000112', 'PAL25-0000116')
); 