-- Consulta SQL para obtener estadísticas de movimientos sin pareja
-- Esta consulta muestra cuántos movimientos de salida y entrada no tienen su contrapartida

DECLARE @FechaDesde DATETIME = CAST(GETDATE() AS DATE); -- Hoy
DECLARE @FechaHasta DATETIME = DATEADD(SECOND, -1, DATEADD(DAY, 1, CAST(CAST(GETDATE() AS DATE) AS DATETIME))); -- Fin de hoy

-- Obtener todos los movimientos en el rango de fechas
WITH Movimientos AS (
    SELECT 
        MovPosicion,
        TipoMovimiento,
        CodigoArticulo,
        CodigoAlmacen,
        AlmacenContrapartida,
        Unidades,
        Partida,
        Fecha,
        CodigoCanal
    FROM [dbo].[MovimientoStock]
    WHERE (TipoMovimiento = 1 OR TipoMovimiento = 2)
      AND Fecha >= @FechaDesde
      AND Fecha <= @FechaHasta
),
Salidas AS (
    SELECT * FROM Movimientos WHERE TipoMovimiento = 2
),
Entradas AS (
    SELECT * FROM Movimientos WHERE TipoMovimiento = 1
),
-- Identificar traspasos completos usando ROW_NUMBER para evitar duplicados
TraspasosCompletos AS (
    SELECT 
        Salida.MovPosicion AS SalidaMovPosicion,
        Entrada.MovPosicion AS EntradaMovPosicion,
        ROW_NUMBER() OVER (
            PARTITION BY Salida.MovPosicion 
            ORDER BY ABS(DATEDIFF(HOUR, Salida.Fecha, Entrada.Fecha))
        ) AS RN_Salida,
        ROW_NUMBER() OVER (
            PARTITION BY Entrada.MovPosicion 
            ORDER BY ABS(DATEDIFF(HOUR, Salida.Fecha, Entrada.Fecha))
        ) AS RN_Entrada
    FROM Salidas AS Salida
    INNER JOIN Entradas AS Entrada
        ON Salida.CodigoArticulo = Entrada.CodigoArticulo
        AND Salida.Unidades = Entrada.Unidades
        AND (Salida.Partida = Entrada.Partida 
             OR (LTRIM(RTRIM(ISNULL(Salida.Partida, ''))) = '' 
                 AND LTRIM(RTRIM(ISNULL(Entrada.Partida, ''))) = ''))
        AND ABS(DATEDIFF(HOUR, Salida.Fecha, Entrada.Fecha)) <= 24
        AND (
            -- Si AlmacenContrapartida está lleno, usarlo
            (LTRIM(RTRIM(ISNULL(Salida.AlmacenContrapartida, ''))) != '' 
             AND Entrada.CodigoAlmacen = Salida.AlmacenContrapartida)
            OR
            -- Si no, relacionar por almacenes diferentes
            (LTRIM(RTRIM(ISNULL(Salida.AlmacenContrapartida, ''))) = '' 
             AND Salida.CodigoAlmacen != Entrada.CodigoAlmacen)
        )
),
-- Filtrar para que cada salida y entrada solo aparezca una vez (la más cercana en tiempo)
TraspasosUnicos AS (
    SELECT DISTINCT
        SalidaMovPosicion,
        EntradaMovPosicion
    FROM TraspasosCompletos
    WHERE RN_Salida = 1 AND RN_Entrada = 1
)
-- Estadísticas generales
SELECT 
    'ESTADÍSTICAS GENERALES' AS Tipo,
    (SELECT COUNT(*) FROM Movimientos) AS TotalMovimientos,
    (SELECT COUNT(*) FROM Salidas) AS TotalSalidas,
    (SELECT COUNT(*) FROM Entradas) AS TotalEntradas,
    (SELECT COUNT(DISTINCT SalidaMovPosicion) FROM TraspasosUnicos) AS SalidasConPareja,
    (SELECT COUNT(DISTINCT EntradaMovPosicion) FROM TraspasosUnicos) AS EntradasConPareja,
    (SELECT COUNT(*) FROM Salidas) - (SELECT COUNT(DISTINCT SalidaMovPosicion) FROM TraspasosUnicos) AS SalidasSinPareja,
    (SELECT COUNT(*) FROM Entradas) - (SELECT COUNT(DISTINCT EntradaMovPosicion) FROM TraspasosUnicos) AS EntradasSinPareja,
    (SELECT COUNT(DISTINCT SalidaMovPosicion) FROM TraspasosUnicos) AS TraspasosCompletos,
    CAST(
        CASE 
            WHEN (SELECT COUNT(*) FROM Salidas) > 0 
            THEN ((SELECT COUNT(*) FROM Salidas) - (SELECT COUNT(DISTINCT SalidaMovPosicion) FROM TraspasosUnicos)) * 100.0 / (SELECT COUNT(*) FROM Salidas)
            ELSE 0 
        END AS DECIMAL(5,2)
    ) AS PorcentajeSalidasSinPareja,
    CAST(
        CASE 
            WHEN (SELECT COUNT(*) FROM Entradas) > 0 
            THEN ((SELECT COUNT(*) FROM Entradas) - (SELECT COUNT(DISTINCT EntradaMovPosicion) FROM TraspasosUnicos)) * 100.0 / (SELECT COUNT(*) FROM Entradas)
            ELSE 0 
        END AS DECIMAL(5,2)
    ) AS PorcentajeEntradasSinPareja;

-- Detalle de salidas sin pareja (usando las mismas CTEs)
WITH Movimientos AS (
    SELECT 
        MovPosicion,
        TipoMovimiento,
        CodigoArticulo,
        CodigoAlmacen,
        AlmacenContrapartida,
        Unidades,
        Partida,
        Fecha,
        CodigoCanal
    FROM [dbo].[MovimientoStock]
    WHERE (TipoMovimiento = 1 OR TipoMovimiento = 2)
      AND Fecha >= @FechaDesde
      AND Fecha <= @FechaHasta
),
Salidas AS (
    SELECT * FROM Movimientos WHERE TipoMovimiento = 2
),
Entradas AS (
    SELECT * FROM Movimientos WHERE TipoMovimiento = 1
),
TraspasosCompletos AS (
    SELECT 
        Salida.MovPosicion AS SalidaMovPosicion,
        Entrada.MovPosicion AS EntradaMovPosicion,
        ROW_NUMBER() OVER (
            PARTITION BY Salida.MovPosicion 
            ORDER BY ABS(DATEDIFF(HOUR, Salida.Fecha, Entrada.Fecha))
        ) AS RN_Salida,
        ROW_NUMBER() OVER (
            PARTITION BY Entrada.MovPosicion 
            ORDER BY ABS(DATEDIFF(HOUR, Salida.Fecha, Entrada.Fecha))
        ) AS RN_Entrada
    FROM Salidas AS Salida
    INNER JOIN Entradas AS Entrada
        ON Salida.CodigoArticulo = Entrada.CodigoArticulo
        AND Salida.Unidades = Entrada.Unidades
        AND (Salida.Partida = Entrada.Partida 
             OR (LTRIM(RTRIM(ISNULL(Salida.Partida, ''))) = '' 
                 AND LTRIM(RTRIM(ISNULL(Entrada.Partida, ''))) = ''))
        AND ABS(DATEDIFF(HOUR, Salida.Fecha, Entrada.Fecha)) <= 24
        AND (
            (LTRIM(RTRIM(ISNULL(Salida.AlmacenContrapartida, ''))) != '' 
             AND Entrada.CodigoAlmacen = Salida.AlmacenContrapartida)
            OR
            (LTRIM(RTRIM(ISNULL(Salida.AlmacenContrapartida, ''))) = '' 
             AND Salida.CodigoAlmacen != Entrada.CodigoAlmacen)
        )
),
TraspasosUnicos AS (
    SELECT DISTINCT
        SalidaMovPosicion,
        EntradaMovPosicion
    FROM TraspasosCompletos
    WHERE RN_Salida = 1 AND RN_Entrada = 1
)
SELECT 
    'SALIDAS SIN PAREJA' AS Tipo,
    Salida.MovPosicion,
    Salida.CodigoArticulo,
    Salida.CodigoAlmacen AS AlmacenOrigen,
    Salida.AlmacenContrapartida,
    Salida.Unidades,
    Salida.Partida,
    Salida.Fecha,
    Salida.CodigoCanal
FROM Salidas AS Salida
WHERE NOT EXISTS (
    SELECT 1 FROM TraspasosUnicos AS TC
    WHERE TC.SalidaMovPosicion = Salida.MovPosicion
)
ORDER BY Salida.Fecha DESC;

-- Detalle de entradas sin pareja (usando las mismas CTEs)
WITH Movimientos AS (
    SELECT 
        MovPosicion,
        TipoMovimiento,
        CodigoArticulo,
        CodigoAlmacen,
        AlmacenContrapartida,
        Unidades,
        Partida,
        Fecha,
        CodigoCanal
    FROM [dbo].[MovimientoStock]
    WHERE (TipoMovimiento = 1 OR TipoMovimiento = 2)
      AND Fecha >= @FechaDesde
      AND Fecha <= @FechaHasta
),
Salidas AS (
    SELECT * FROM Movimientos WHERE TipoMovimiento = 2
),
Entradas AS (
    SELECT * FROM Movimientos WHERE TipoMovimiento = 1
),
TraspasosCompletos AS (
    SELECT 
        Salida.MovPosicion AS SalidaMovPosicion,
        Entrada.MovPosicion AS EntradaMovPosicion,
        ROW_NUMBER() OVER (
            PARTITION BY Salida.MovPosicion 
            ORDER BY ABS(DATEDIFF(HOUR, Salida.Fecha, Entrada.Fecha))
        ) AS RN_Salida,
        ROW_NUMBER() OVER (
            PARTITION BY Entrada.MovPosicion 
            ORDER BY ABS(DATEDIFF(HOUR, Salida.Fecha, Entrada.Fecha))
        ) AS RN_Entrada
    FROM Salidas AS Salida
    INNER JOIN Entradas AS Entrada
        ON Salida.CodigoArticulo = Entrada.CodigoArticulo
        AND Salida.Unidades = Entrada.Unidades
        AND (Salida.Partida = Entrada.Partida 
             OR (LTRIM(RTRIM(ISNULL(Salida.Partida, ''))) = '' 
                 AND LTRIM(RTRIM(ISNULL(Entrada.Partida, ''))) = ''))
        AND ABS(DATEDIFF(HOUR, Salida.Fecha, Entrada.Fecha)) <= 24
        AND (
            (LTRIM(RTRIM(ISNULL(Salida.AlmacenContrapartida, ''))) != '' 
             AND Entrada.CodigoAlmacen = Salida.AlmacenContrapartida)
            OR
            (LTRIM(RTRIM(ISNULL(Salida.AlmacenContrapartida, ''))) = '' 
             AND Salida.CodigoAlmacen != Entrada.CodigoAlmacen)
        )
),
TraspasosUnicos AS (
    SELECT DISTINCT
        SalidaMovPosicion,
        EntradaMovPosicion
    FROM TraspasosCompletos
    WHERE RN_Salida = 1 AND RN_Entrada = 1
)
SELECT 
    'ENTRADAS SIN PAREJA' AS Tipo,
    Entrada.MovPosicion,
    Entrada.CodigoArticulo,
    Entrada.CodigoAlmacen AS AlmacenDestino,
    Entrada.Unidades,
    Entrada.Partida,
    Entrada.Fecha,
    Entrada.CodigoCanal
FROM Entradas AS Entrada
WHERE NOT EXISTS (
    SELECT 1 FROM TraspasosUnicos AS TC
    WHERE TC.EntradaMovPosicion = Entrada.MovPosicion
)
ORDER BY Entrada.Fecha DESC;

