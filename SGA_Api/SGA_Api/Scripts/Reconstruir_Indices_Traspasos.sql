-- Script SEGURO para reorganizar/reconstruir índices de la tabla Traspasos
-- Versión que se puede ejecutar con la base de datos en uso
-- Ejecutar en SQL Server Management Studio conectado a AURORA_SGA

USE [AURORA_SGA];
GO

PRINT '========================================';
PRINT 'Mantenimiento de índices de la tabla Traspasos';
PRINT 'Versión SEGURA - se puede ejecutar con la BD en uso';
PRINT '========================================';
PRINT '';

-- OPCIÓN 1: REORGANIZE (más seguro, menos bloqueo, pero solo reduce fragmentación moderada)
-- Primero detectamos los nombres reales de los índices
PRINT 'OPCIÓN 1: Detectando índices y reorganizándolos (más seguro, menos bloqueo)...';
PRINT '';

-- Crear tabla temporal con los nombres de índices
DECLARE @Indices TABLE (
    NombreIndice NVARCHAR(128),
    EsImportante BIT
);

-- Insertar todos los índices no clustered de la tabla Traspasos
INSERT INTO @Indices (NombreIndice, EsImportante)
SELECT 
    i.name,
    CASE 
        WHEN i.name LIKE '%Usuario%' OR i.name LIKE '%Fecha%' THEN 1
        ELSE 0
    END
FROM sys.indexes i
INNER JOIN sys.tables t ON i.object_id = t.object_id
WHERE t.name = 'Traspasos'
    AND i.type > 0  -- Excluir heap
    AND i.is_primary_key = 0;  -- Excluir clave primaria

-- Reorganizar índices importantes primero (los que tienen Usuario o Fecha en el nombre)
PRINT 'Reorganizando índices importantes (Usuario y Fecha)...';
DECLARE @NombreIndice NVARCHAR(128);
DECLARE @SQL NVARCHAR(MAX);

DECLARE idx_cursor CURSOR FOR
SELECT NombreIndice 
FROM @Indices 
WHERE EsImportante = 1
ORDER BY NombreIndice;

OPEN idx_cursor;
FETCH NEXT FROM idx_cursor INTO @NombreIndice;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @SQL = 'ALTER INDEX [' + @NombreIndice + '] ON [Traspasos] REORGANIZE;';
    PRINT 'Reorganizando: ' + @NombreIndice + '...';
    
    BEGIN TRY
        EXEC sp_executesql @SQL;
        PRINT '   ✓ ' + @NombreIndice + ' reorganizado';
    END TRY
    BEGIN CATCH
        PRINT '   ✗ Error reorganizando ' + @NombreIndice + ': ' + ERROR_MESSAGE();
    END CATCH
    
    PRINT '';
    FETCH NEXT FROM idx_cursor INTO @NombreIndice;
END;

CLOSE idx_cursor;
DEALLOCATE idx_cursor;

-- Reorganizar el resto de índices
PRINT 'Reorganizando resto de índices...';
DECLARE idx_cursor2 CURSOR FOR
SELECT NombreIndice 
FROM @Indices 
WHERE EsImportante = 0
ORDER BY NombreIndice;

OPEN idx_cursor2;
FETCH NEXT FROM idx_cursor2 INTO @NombreIndice;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @SQL = 'ALTER INDEX [' + @NombreIndice + '] ON [Traspasos] REORGANIZE;';
    PRINT 'Reorganizando: ' + @NombreIndice + '...';
    
    BEGIN TRY
        EXEC sp_executesql @SQL;
        PRINT '   ✓ ' + @NombreIndice + ' reorganizado';
    END TRY
    BEGIN CATCH
        PRINT '   ✗ Error reorganizando ' + @NombreIndice + ': ' + ERROR_MESSAGE();
    END CATCH
    
    
    PRINT '';
    FETCH NEXT FROM idx_cursor2 INTO @NombreIndice;
END;

CLOSE idx_cursor2;
DEALLOCATE idx_cursor2;

PRINT '========================================';
PRINT '✓ Reorganización completada';
PRINT '========================================';
PRINT '';
PRINT 'NOTA: REORGANIZE es más seguro pero puede no eliminar toda la fragmentación.';
PRINT 'Si después de esto las consultas siguen lentas, ejecuta la OPCIÓN 2 (más abajo).';
PRINT '';

-- ===================================================================
-- OPCIÓN 2: REBUILD ONLINE (solo si tienes SQL Server Enterprise/Standard)
-- Descomentar solo si REORGANIZE no fue suficiente
-- ===================================================================

/*
PRINT '========================================';
PRINT 'OPCIÓN 2: Reconstruyendo índices con REBUILD ONLINE';
PRINT 'Solo ejecutar si REORGANIZE no fue suficiente';
PRINT '========================================';
PRINT '';

-- Verificar versión de SQL Server
DECLARE @Version INT = CAST(LEFT(CAST(SERVERPROPERTY('ProductVersion') AS VARCHAR(50)), 2) AS INT);
IF @Version >= 12  -- SQL Server 2014 o superior
BEGIN
    PRINT 'SQL Server soporta REBUILD ONLINE. Procediendo...';
    PRINT '';
    
    -- Reconstruir índices más importantes con ONLINE (no bloquea la tabla)
    PRINT '1. Reconstruyendo IX_traspasos_Fechalnicio_Usuariolniciold (ONLINE)...';
    ALTER INDEX IX_traspasos_Fechalnicio_Usuariolniciold ON Traspasos REBUILD WITH (ONLINE = ON, FILLFACTOR = 90);
    PRINT '   ✓ Reconstruido';
    PRINT '';
    
    PRINT '2. Reconstruyendo IX_traspasos_Usuariolniciold (ONLINE)...';
    ALTER INDEX IX_traspasos_Usuariolniciold ON Traspasos REBUILD WITH (ONLINE = ON, FILLFACTOR = 90);
    PRINT '   ✓ Reconstruido';
    PRINT '';
    
    PRINT '3. Reconstruyendo IX_traspasos_UsuarioFinalizacionld (ONLINE)...';
    ALTER INDEX IX_traspasos_UsuarioFinalizacionld ON Traspasos REBUILD WITH (ONLINE = ON, FILLFACTOR = 90);
    PRINT '   ✓ Reconstruido';
    PRINT '';
    
    PRINT '4. Reconstruyendo IX_traspasos_Fechalnicio (ONLINE)...';
    ALTER INDEX IX_traspasos_Fechalnicio ON Traspasos REBUILD WITH (ONLINE = ON, FILLFACTOR = 90);
    PRINT '   ✓ Reconstruido';
    PRINT '';
END
ELSE
BEGIN
    PRINT 'Tu versión de SQL Server no soporta REBUILD ONLINE.';
    PRINT 'Usa REORGANIZE (OPCIÓN 1) o programa el REBUILD para horas de menor uso.';
END
*/

-- ===================================================================
-- OPCIÓN 3: Reconstruir solo el índice MÁS IMPORTANTE (más rápido)
-- ===================================================================

/*
PRINT '========================================';
PRINT 'OPCIÓN 3: Reconstruyendo solo el índice más importante';
PRINT 'Este es el que más afecta a tus consultas de operario';
PRINT '========================================';
PRINT '';

PRINT 'Reconstruyendo IX_traspasos_Fechalnicio_Usuariolniciold...';
-- Intentar ONLINE primero, si falla usar OFFLINE
BEGIN TRY
    ALTER INDEX IX_traspasos_Fechalnicio_Usuariolniciold ON Traspasos REBUILD WITH (ONLINE = ON, FILLFACTOR = 90);
    PRINT '✓ Reconstruido con ONLINE (sin bloqueo)';
END TRY
BEGIN CATCH
    PRINT 'ONLINE no disponible, usando REORGANIZE (más seguro)...';
    ALTER INDEX IX_traspasos_Fechalnicio_Usuariolniciold ON Traspasos REORGANIZE;
    PRINT '✓ Reorganizado';
END CATCH
*/

PRINT '';
PRINT '========================================';
PRINT 'RECOMENDACIÓN:';
PRINT '1. Ejecuta primero la OPCIÓN 1 (REORGANIZE) - ya está ejecutada arriba';
PRINT '2. Prueba las consultas - deberían mejorar';
PRINT '3. Si siguen lentas, descomenta y ejecuta la OPCIÓN 3 (solo el índice más importante)';
PRINT '4. Verifica resultados con: Consultar_Indices_Traspasos.sql (consulta 2)';
PRINT '========================================';

