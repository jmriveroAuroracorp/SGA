-- Script para investigar el origen del Reservado en una ubicación/artículo/partida específica
-- Reemplaza los valores de las variables según tu caso

DECLARE @CodigoEmpresa SMALLINT = 1; -- Cambiar según tu empresa
DECLARE @CodigoArticulo VARCHAR(50) = 'TU_ARTICULO'; -- Cambiar por el artículo
DECLARE @CodigoAlmacen VARCHAR(20) = 'PR'; -- Cambiar por el almacén
DECLARE @Ubicacion VARCHAR(50) = 'UB001001001001'; -- Cambiar por la ubicación
DECLARE @Partida VARCHAR(50) = '987456321'; -- Cambiar por la partida

-- ============================================
-- 0. PRIMERO: Verificar directamente en vStockDisponible
-- ============================================
PRINT '=========================================';
PRINT '0. VERIFICAR EN vStockDisponible (DIRECTO)';
PRINT '=========================================';
SELECT 
    CodigoEmpresa,
    CodigoArticulo,
    CodigoAlmacen,
    Ubicacion,
    Partida,
    UnidadSaldo,
    Reservado,
    Disponible
FROM Aurora_Sga.dbo.vStockDisponible
WHERE CodigoAlmacen = @CodigoAlmacen
  AND Ubicacion = @Ubicacion
  AND (@CodigoEmpresa IS NULL OR CodigoEmpresa = @CodigoEmpresa)
  AND (@CodigoArticulo = 'TU_ARTICULO' OR CodigoArticulo = @CodigoArticulo)
  AND (@Partida = '987456321' OR Partida = @Partida OR (Partida IS NULL AND @Partida IS NULL))
ORDER BY CodigoArticulo, Partida;

-- ============================================
-- 1. TRASPASOS PENDIENTES que generan reservado (BÚSQUEDA AMPLIA)
-- ============================================
PRINT '';
PRINT '=========================================';
PRINT '1. TRASPASOS PENDIENTES (Origen del Reservado) - BÚSQUEDA AMPLIA';
PRINT '=========================================';
-- Primero buscar por ubicación exacta
SELECT 
    t.Id AS TraspasoId,
    t.CodigoEstado,
    t.TipoTraspaso,
    t.CodigoArticulo,
    t.Partida,
    t.AlmacenOrigen,
    t.UbicacionOrigen,
    t.Cantidad AS CantidadReservada,
    t.FechaInicio,
    t.UsuarioInicioId,
    t.PaletId,
    t.CodigoPalet,
    t.Comentario
FROM Aurora_Sga.dbo.Traspasos t
WHERE t.AlmacenOrigen = @CodigoAlmacen
  AND t.UbicacionOrigen = @Ubicacion
  AND t.CodigoEstado IN ('PENDIENTE', 'EN_TRANSITO', 'PENDIENTE_ERP')
  AND (@CodigoEmpresa IS NULL OR t.CodigoEmpresa = @CodigoEmpresa)
  AND (@CodigoArticulo = 'TU_ARTICULO' OR t.CodigoArticulo = @CodigoArticulo)
  AND (@Partida = '987456321' OR t.Partida = @Partida OR (t.Partida IS NULL AND @Partida IS NULL))
ORDER BY t.FechaInicio DESC;

-- ============================================
-- 2. TEMP PALET LINEAS asociadas a traspasos pendientes (BÚSQUEDA AMPLIA)
-- ============================================
PRINT '';
PRINT '=========================================';
PRINT '2. TEMP PALET LINEAS (Origen del Reservado) - BÚSQUEDA AMPLIA';
PRINT '=========================================';
SELECT 
    t.Id AS TempPaletLineaId,
    t.PaletId,
    t.TraspasoId,
    tr.CodigoEstado AS EstadoTraspaso,
    tr.TipoTraspaso,
    t.CodigoArticulo,
    t.Lote,
    t.CodigoAlmacen,
    t.Ubicacion,
    t.Cantidad AS CantidadReservada,
    t.FechaAgregado,
    t.UsuarioId,
    t.Observaciones,
    t.EsHeredada,
    t.ConteoId
FROM Aurora_Sga.dbo.TempPaletLineas t
LEFT JOIN Aurora_Sga.dbo.Traspasos tr ON t.TraspasoId = tr.Id
WHERE t.CodigoAlmacen = @CodigoAlmacen
  AND t.Ubicacion = @Ubicacion
  AND t.ConteoId IS NULL  -- Excluir líneas de conteo
  AND (@CodigoEmpresa IS NULL OR t.CodigoEmpresa = @CodigoEmpresa)
  AND (@CodigoArticulo = 'TU_ARTICULO' OR t.CodigoArticulo = @CodigoArticulo)
  AND (@Partida = '987456321' OR t.Lote = @Partida OR (t.Lote IS NULL AND @Partida IS NULL))
  AND (tr.Id IS NULL OR tr.CodigoEstado IN ('PENDIENTE', 'EN_TRANSITO', 'PENDIENTE_ERP'))
ORDER BY t.FechaAgregado DESC;

-- ============================================
-- 3. RESUMEN: TOTAL RESERVADO calculado
-- ============================================
PRINT '';
PRINT '=========================================';
PRINT '3. RESUMEN: TOTAL RESERVADO';
PRINT '=========================================';
SELECT 
    'Traspasos Pendientes' AS Origen,
    SUM(t.Cantidad) AS TotalReservado
FROM Aurora_Sga.dbo.Traspasos t
WHERE t.CodigoEmpresa = @CodigoEmpresa
  AND t.CodigoArticulo = @CodigoArticulo
  AND t.AlmacenOrigen = @CodigoAlmacen
  AND t.UbicacionOrigen = @Ubicacion
  AND (t.Partida = @Partida OR (t.Partida IS NULL AND @Partida IS NULL))
  AND t.CodigoEstado IN ('PENDIENTE', 'EN_TRANSITO', 'PENDIENTE_ERP')

UNION ALL

SELECT 
    'TempPaletLineas' AS Origen,
    SUM(t.Cantidad) AS TotalReservado
FROM Aurora_Sga.dbo.TempPaletLineas t
LEFT JOIN Aurora_Sga.dbo.Traspasos tr ON t.TraspasoId = tr.Id
WHERE t.CodigoEmpresa = @CodigoEmpresa
  AND t.CodigoArticulo = @CodigoArticulo
  AND t.CodigoAlmacen = @CodigoAlmacen
  AND t.Ubicacion = @Ubicacion
  AND (t.Lote = @Partida OR (t.Lote IS NULL AND @Partida IS NULL))
  AND t.ConteoId IS NULL
  AND (tr.Id IS NULL OR tr.CodigoEstado IN ('PENDIENTE', 'EN_TRANSITO', 'PENDIENTE_ERP'));

-- ============================================
-- 5. BUSCAR TODOS LOS ARTÍCULOS EN ESA UBICACIÓN (para verificar parámetros)
-- ============================================
PRINT '';
PRINT '=========================================';
PRINT '5. TODOS LOS ARTÍCULOS EN ESA UBICACIÓN (para verificar parámetros)';
PRINT '=========================================';
SELECT TOP 20
    CodigoEmpresa,
    CodigoArticulo,
    CodigoAlmacen,
    Ubicacion,
    Partida,
    UnidadSaldo,
    Reservado,
    Disponible
FROM Aurora_Sga.dbo.vStockDisponible
WHERE CodigoAlmacen = @CodigoAlmacen
  AND Ubicacion = @Ubicacion
  AND Reservado > 0  -- Solo los que tienen reservado
ORDER BY Reservado DESC, CodigoArticulo;

