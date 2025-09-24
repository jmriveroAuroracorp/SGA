-- Script para dar todos los permisos al usuario 1226
-- IMPORTANTE: Ejecutar en la base de datos del SGA

-- Primero, eliminar permisos existentes del usuario 1226 (si los tiene)
DELETE FROM MRH_accesosOperariosSGA 
WHERE Operario = 1226 AND CodigoEmpresa = 1;

-- Insertar todos los permisos disponibles (códigos 7-15) para el usuario 1226
INSERT INTO MRH_accesosOperariosSGA (CodigoEmpresa, Operario, MRH_CodigoAplicacion)
SELECT 1, 1226, MRH_CodigoAplicacion
FROM MRH_AplicacionesSGA 
WHERE MRH_CodigoAplicacion >= 7
AND NOT EXISTS (
    SELECT 1 FROM MRH_accesosOperariosSGA 
    WHERE Operario = 1226 
    AND CodigoEmpresa = 1 
    AND MRH_CodigoAplicacion = MRH_AplicacionesSGA.MRH_CodigoAplicacion
);

-- Verificar que se insertaron correctamente
SELECT 
    COUNT(*) as TotalPermisos,
    STRING_AGG(CAST(MRH_CodigoAplicacion AS VARCHAR), ', ') as PermisosAsignados
FROM MRH_accesosOperariosSGA 
WHERE Operario = 1226 AND CodigoEmpresa = 1;

PRINT 'Permisos asignados al usuario 1226 exitosamente';
