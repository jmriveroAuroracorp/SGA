-- =============================================
-- Script para actualizar precisión de Diferencia en InventarioAjustes
-- Cambia de decimal(18,4) a decimal(18,6) para preservar valores exactos
-- =============================================

USE [AuroraSGA];
GO

PRINT 'Iniciando actualización de precisión en InventarioAjustes...';
GO

-- =============================================
-- Actualizar InventarioAjustes.Diferencia
-- =============================================
BEGIN TRY
    PRINT 'Actualizando InventarioAjustes.Diferencia...';
    
    ALTER TABLE [dbo].[InventarioAjustes]
    ALTER COLUMN [Diferencia] DECIMAL(18,6) NOT NULL;
    
    PRINT '✓ InventarioAjustes.Diferencia actualizada a DECIMAL(18,6)';
END TRY
BEGIN CATCH
    PRINT '✗ Error al actualizar InventarioAjustes.Diferencia:';
    PRINT ERROR_MESSAGE();
    THROW;
END CATCH
GO

PRINT '';
PRINT '==========================================';
PRINT 'Script completado exitosamente.';
PRINT 'La columna Diferencia en InventarioAjustes ahora tiene precisión de 6 decimales.';
PRINT '==========================================';
GO

