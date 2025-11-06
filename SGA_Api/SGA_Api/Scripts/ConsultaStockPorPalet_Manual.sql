-- =============================================
-- CONSULTA MANUAL PARA PROBAR BÚSQUEDA POR PALET
-- Simula lo que hace el endpoint: GET api/Stock/articulo/disponible
-- =============================================

-- ⚙️ CONFIGURACIÓN: Cambia estos valores según tu caso de prueba
DECLARE @CodigoEmpresa SMALLINT = 1;  -- ⚠️ CAMBIAR según tu empresa
DECLARE @CodigoArticulo VARCHAR(50) = 'TU_ARTICULO_AQUI';  -- ⚠️ CAMBIAR por tu código de artículo
DECLARE @CodigoAlmacen VARCHAR(10) = NULL;  -- ⚠️ OPCIONAL: NULL para todos, o 'ALM001' para uno específico
DECLARE @CodigoPalet VARCHAR(50) = NULL;  -- ⚠️ OPCIONAL: NULL para todos, o 'PAL001' para uno específico

-- =============================================
-- PASO 1: Consultar stock disponible desde la vista
-- =============================================
SELECT 
    sd.CodigoEmpresa,
    sd.CodigoArticulo,
    sd.DescripcionArticulo,
    sd.CodigoAlmacen,
    sd.Almacen,
    sd.Ubicacion,
    sd.Partida,
    sd.FechaCaducidad,
    sd.UnidadSaldo,
    sd.Reservado,
    sd.Disponible
FROM AURORA_SGA.dbo.vStockDisponible sd
WHERE sd.CodigoEmpresa = @CodigoEmpresa
  AND sd.CodigoArticulo = @CodigoArticulo
  AND (@CodigoAlmacen IS NULL OR sd.CodigoAlmacen = @CodigoAlmacen)
  AND sd.Disponible > 0
ORDER BY sd.CodigoAlmacen, sd.Ubicacion, sd.Partida;

-- =============================================
-- PASO 2: Consultar palets asociados a cada ubicación/partida
-- =============================================
SELECT 
    pl.PaletId,
    p.Codigo AS CodigoPalet,
    p.Estado AS EstadoPalet,
    pl.CodigoEmpresa,
    pl.CodigoArticulo,
    pl.CodigoAlmacen,
    pl.Ubicacion,
    pl.Lote AS Partida,
    pl.Cantidad AS CantidadEnPalet,
    p.FechaApertura,
    p.FechaCierre
FROM AURORA_SGA.dbo.PaletLineas pl
INNER JOIN AURORA_SGA.dbo.Palets p ON pl.PaletId = p.Id
WHERE pl.CodigoEmpresa = @CodigoEmpresa
  AND pl.CodigoArticulo = @CodigoArticulo
  AND (@CodigoAlmacen IS NULL OR pl.CodigoAlmacen = @CodigoAlmacen)
  AND (@CodigoPalet IS NULL OR p.Codigo LIKE '%' + @CodigoPalet + '%')
  AND (UPPER(p.Estado) = 'ABIERTO' OR UPPER(p.Estado) = 'CERRADO')
ORDER BY pl.CodigoAlmacen, pl.Ubicacion, pl.Lote, p.Codigo;

-- =============================================
-- PASO 3: CONSULTA COMBINADA (simula lo que devuelve el API)
-- Muestra stock disponible con información de palets
-- =============================================
WITH StockBase AS (
    SELECT 
        sd.CodigoEmpresa,
        sd.CodigoArticulo,
        sd.DescripcionArticulo,
        sd.CodigoAlmacen,
        sd.Almacen,
        sd.Ubicacion,
        sd.Partida,
        sd.FechaCaducidad,
        sd.UnidadSaldo,
        sd.Reservado,
        sd.Disponible
    FROM AURORA_SGA.dbo.vStockDisponible sd
    WHERE sd.CodigoEmpresa = @CodigoEmpresa
      AND sd.CodigoArticulo = @CodigoArticulo
      AND (@CodigoAlmacen IS NULL OR sd.CodigoAlmacen = @CodigoAlmacen)
      AND sd.Disponible > 0
),
PaletsInfo AS (
    SELECT 
        pl.CodigoEmpresa,
        pl.CodigoArticulo,
        pl.CodigoAlmacen,
        pl.Ubicacion,
        pl.Lote AS Partida,
        pl.PaletId,
        p.Codigo AS CodigoPalet,
        p.Estado AS EstadoPalet,
        pl.Cantidad AS CantidadEnPalet
    FROM AURORA_SGA.dbo.PaletLineas pl
    INNER JOIN AURORA_SGA.dbo.Palets p ON pl.PaletId = p.Id
    WHERE pl.CodigoEmpresa = @CodigoEmpresa
      AND pl.CodigoArticulo = @CodigoArticulo
      AND (@CodigoAlmacen IS NULL OR pl.CodigoAlmacen = @CodigoAlmacen)
      AND (@CodigoPalet IS NULL OR p.Codigo LIKE '%' + @CodigoPalet + '%')
      AND (UPPER(p.Estado) = 'ABIERTO' OR UPPER(p.Estado) = 'CERRADO')
),
TotalPaletizadoPorUbicacion AS (
    SELECT 
        CodigoEmpresa,
        CodigoArticulo,
        CodigoAlmacen,
        Ubicacion,
        Partida,
        SUM(CantidadEnPalet) AS TotalPaletizado
    FROM PaletsInfo
    GROUP BY CodigoEmpresa, CodigoArticulo, CodigoAlmacen, Ubicacion, Partida
)
SELECT 
    sb.CodigoEmpresa,
    sb.CodigoArticulo,
    sb.DescripcionArticulo,
    sb.CodigoAlmacen,
    sb.Almacen,
    sb.Ubicacion,
    sb.Partida,
    sb.FechaCaducidad,
    sb.UnidadSaldo,
    sb.Reservado,
    sb.Disponible AS DisponibleTotal,
    ISNULL(tp.TotalPaletizado, 0) AS TotalPaletizado,
    CAST(sb.Disponible AS decimal(38,6)) - CAST(ISNULL(tp.TotalPaletizado, 0) AS decimal(38,6)) AS StockSuelto,
    -- Información del palet (si hay)
    pi.PaletId,
    pi.CodigoPalet,
    pi.EstadoPalet,
    pi.CantidadEnPalet,
    -- Tipo de stock
    CASE 
        WHEN pi.PaletId IS NOT NULL THEN 'Paletizado'
        ELSE 'Suelto'
    END AS TipoStock
FROM StockBase sb
LEFT JOIN TotalPaletizadoPorUbicacion tp 
    ON sb.CodigoEmpresa = tp.CodigoEmpresa
    AND sb.CodigoArticulo = tp.CodigoArticulo
    AND sb.CodigoAlmacen = tp.CodigoAlmacen
    AND sb.Ubicacion = tp.Ubicacion
    AND sb.Partida = tp.Partida
LEFT JOIN PaletsInfo pi
    ON sb.CodigoEmpresa = pi.CodigoEmpresa
    AND sb.CodigoArticulo = pi.CodigoArticulo
    AND sb.CodigoAlmacen = pi.CodigoAlmacen
    AND sb.Ubicacion = pi.Ubicacion
    AND sb.Partida = pi.Partida
ORDER BY sb.CodigoAlmacen, sb.Ubicacion, sb.Partida, pi.CodigoPalet;

-- =============================================
-- PASO 4: RESUMEN - Solo contar cuántos palets hay
-- =============================================
SELECT 
    COUNT(DISTINCT p.Id) AS TotalPalets,
    COUNT(*) AS TotalLineasPalet,
    SUM(pl.Cantidad) AS TotalCantidadPaletizada
FROM AURORA_SGA.dbo.PaletLineas pl
INNER JOIN AURORA_SGA.dbo.Palets p ON pl.PaletId = p.Id
WHERE pl.CodigoEmpresa = @CodigoEmpresa
  AND pl.CodigoArticulo = @CodigoArticulo
  AND (@CodigoAlmacen IS NULL OR pl.CodigoAlmacen = @CodigoAlmacen)
  AND (@CodigoPalet IS NULL OR p.Codigo LIKE '%' + @CodigoPalet + '%')
  AND (UPPER(p.Estado) = 'ABIERTO' OR UPPER(p.Estado) = 'CERRADO');

