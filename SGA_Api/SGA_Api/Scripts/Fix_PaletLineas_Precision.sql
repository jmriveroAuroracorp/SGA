-- =============================================
-- Script para actualizar precisión de Cantidad en TempPaletLineas y PaletLineas
-- Cambia de decimal(18,4) a decimal(18,6) para preservar valores exactos
-- =============================================

USE [AuroraSGA];
GO

PRINT 'Iniciando actualización de precisión en tablas de líneas de palets...';
GO

-- =============================================
-- 1. Actualizar TempPaletLineas.Cantidad
-- =============================================
BEGIN TRY
    PRINT 'Actualizando TempPaletLineas.Cantidad...';
    
    ALTER TABLE [dbo].[TempPaletLineas]
    ALTER COLUMN [Cantidad] DECIMAL(18,6) NOT NULL;
    
    PRINT '✓ TempPaletLineas.Cantidad actualizada a DECIMAL(18,6)';
END TRY
BEGIN CATCH
    PRINT '✗ Error al actualizar TempPaletLineas.Cantidad:';
    PRINT ERROR_MESSAGE();
    THROW;
END CATCH
GO

-- =============================================
-- 2. Actualizar PaletLineas.Cantidad
-- =============================================
BEGIN TRY
    PRINT 'Actualizando PaletLineas.Cantidad...';
    
    ALTER TABLE [dbo].[PaletLineas]
    ALTER COLUMN [Cantidad] DECIMAL(18,6) NOT NULL;
    
    PRINT '✓ PaletLineas.Cantidad actualizada a DECIMAL(18,6)';
END TRY
BEGIN CATCH
    PRINT '✗ Error al actualizar PaletLineas.Cantidad:';
    PRINT ERROR_MESSAGE();
    THROW;
END CATCH
GO

PRINT '';
PRINT '==========================================';
PRINT 'Script completado exitosamente.';
PRINT 'Las columnas Cantidad en TempPaletLineas y PaletLineas ahora tienen precisión de 6 decimales.';
PRINT '==========================================';
GO

