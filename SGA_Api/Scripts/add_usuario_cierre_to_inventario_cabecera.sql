-- Script para agregar la columna UsuarioCierreId a la tabla InventarioCabecera
-- Ejecutar en la base de datos AuroraSga

-- Agregar la columna UsuarioCierreId
ALTER TABLE InventarioCabecera 
ADD UsuarioCierreId INT NULL;

-- Comentario para documentar la columna
EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'ID del usuario que cierra el inventario', 
    @level0type = N'SCHEMA', @level0name = N'dbo', 
    @level1type = N'TABLE', @level1name = N'InventarioCabecera', 
    @level2type = N'COLUMN', @level2name = N'UsuarioCierreId';

PRINT 'Columna UsuarioCierreId agregada exitosamente a InventarioCabecera'; 