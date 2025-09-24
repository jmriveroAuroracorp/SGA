-- Script para eliminar las tablas de configuraciones predefinidas
-- IMPORTANTE: Ejecutar en el orden correcto debido a las foreign keys

-- 1. Eliminar tablas dependientes primero (por las foreign keys)
DROP TABLE IF EXISTS ConfiguracionesPredefinidasAlmacenes;
DROP TABLE IF EXISTS ConfiguracionesPredefinidasEmpresas;
DROP TABLE IF EXISTS ConfiguracionesPredefinidasPermisos;

-- 2. Eliminar tabla principal al final
DROP TABLE IF EXISTS ConfiguracionesPredefinidas;

PRINT 'Tablas de configuraciones predefinidas eliminadas correctamente';
