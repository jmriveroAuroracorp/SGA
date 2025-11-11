-- Consulta para contar traspasos de hoy usando la misma lógica que el endpoint
-- Relaciona movimientos de salida (TipoMovimiento = 2) con entradas (TipoMovimiento = 1)
-- por: mismo artículo, misma cantidad, misma partida, fechas cercanas (24 horas)

DECLARE @FechaHoy DATETIME = CAST(GETDATE() AS DATE); -- Fecha de hoy a las 00:00:00
DECLARE @FechaHoyFin DATETIME = DATEADD(DAY, 1, @FechaHoy); -- Fin del día de hoy

-- Primero, ver qué valores tiene CodigoCanal en los movimientos de hoy
SELECT 
    CodigoCanal,
    COUNT(*) AS TotalMovimientos,
    COUNT(CASE WHEN TipoMovimiento = 1 THEN 1 END) AS Entradas,
    COUNT(CASE WHEN TipoMovimiento = 2 THEN 1 END) AS Salidas
FROM [dbo].[MovimientoStock]
WHERE Fecha >= @FechaHoy
    AND Fecha < @FechaHoyFin
    AND TipoMovimiento IN (1, 2)
GROUP BY CodigoCanal
ORDER BY TotalMovimientos DESC;

-- Contar traspasos completos (salida + entrada relacionadas)
-- IMPORTANTE: Solo traspasos Mobility (CodigoCanal = 0)
SELECT 
    COUNT(DISTINCT Salida.MovPosicion) AS TotalTraspasosCompletos,
    COUNT(*) AS TotalMovimientosRelacionados
FROM [dbo].[MovimientoStock] AS Salida
INNER JOIN [dbo].[MovimientoStock] AS Entrada
    ON Salida.CodigoArticulo = Entrada.CodigoArticulo
    AND Salida.Unidades = Entrada.Unidades
    AND (Salida.Partida = Entrada.Partida OR (Salida.Partida IS NULL AND Entrada.Partida IS NULL) OR (LTRIM(RTRIM(Salida.Partida)) = '' AND LTRIM(RTRIM(Entrada.Partida)) = ''))
    AND Salida.TipoMovimiento = 2  -- Salida
    AND Entrada.TipoMovimiento = 1  -- Entrada
    AND Salida.MovPosicion != Entrada.MovPosicion  -- Diferentes movimientos
    AND Salida.Fecha >= @FechaHoy
    AND Salida.Fecha < @FechaHoyFin
    AND Entrada.Fecha >= @FechaHoy
    AND Entrada.Fecha < @FechaHoyFin
    AND ABS(DATEDIFF(HOUR, Salida.Fecha, Entrada.Fecha)) <= 24  -- Fechas dentro de 24 horas
    AND Salida.CodigoCanal = '0'  -- SOLO traspasos Mobility (CodigoCanal = 0 exactamente)
    AND Entrada.CodigoCanal = '0'  -- SOLO traspasos Mobility (CodigoCanal = 0 exactamente)
    AND (
        -- Si AlmacenContrapartida está lleno, usarlo
        (LTRIM(RTRIM(Salida.AlmacenContrapartida)) != '' AND Entrada.CodigoAlmacen = Salida.AlmacenContrapartida)
        OR
        -- Si no, relacionar por almacenes diferentes
        (LTRIM(RTRIM(Salida.AlmacenContrapartida)) = '' AND Salida.CodigoAlmacen != Entrada.CodigoAlmacen)
    );

-- Consulta adicional: Ver detalles de los traspasos encontrados (solo Mobility)
SELECT 
    Salida.MovPosicion AS MovPosicionSalida,
    Entrada.MovPosicion AS MovPosicionEntrada,
    Salida.CodigoArticulo,
    Salida.Unidades AS Cantidad,
    Salida.Partida,
    Salida.CodigoAlmacen AS AlmacenOrigen,
    Salida.AlmacenContrapartida AS AlmacenDestinoContrapartida,
    Entrada.CodigoAlmacen AS AlmacenDestinoEntrada,
    Salida.CodigoCanal AS CodigoCanalSalida,
    Entrada.CodigoCanal AS CodigoCanalEntrada,
    Salida.Fecha AS FechaSalida,
    Entrada.Fecha AS FechaEntrada,
    DATEDIFF(HOUR, Salida.Fecha, Entrada.Fecha) AS DiferenciaHoras
FROM [dbo].[MovimientoStock] AS Salida
INNER JOIN [dbo].[MovimientoStock] AS Entrada
    ON Salida.CodigoArticulo = Entrada.CodigoArticulo
    AND Salida.Unidades = Entrada.Unidades
    AND (Salida.Partida = Entrada.Partida OR (Salida.Partida IS NULL AND Entrada.Partida IS NULL) OR (LTRIM(RTRIM(Salida.Partida)) = '' AND LTRIM(RTRIM(Entrada.Partida)) = ''))
    AND Salida.TipoMovimiento = 2  -- Salida
    AND Entrada.TipoMovimiento = 1  -- Entrada
    AND Salida.MovPosicion != Entrada.MovPosicion  -- Diferentes movimientos
    AND Salida.Fecha >= @FechaHoy
    AND Salida.Fecha < @FechaHoyFin
    AND Entrada.Fecha >= @FechaHoy
    AND Entrada.Fecha < @FechaHoyFin
    AND ABS(DATEDIFF(HOUR, Salida.Fecha, Entrada.Fecha)) <= 24  -- Fechas dentro de 24 horas
    AND Salida.CodigoCanal = '0'  -- SOLO traspasos Mobility (CodigoCanal = 0 exactamente)
    AND Entrada.CodigoCanal = '0'  -- SOLO traspasos Mobility (CodigoCanal = 0 exactamente)
    AND (
        -- Si AlmacenContrapartida está lleno, usarlo
        (LTRIM(RTRIM(Salida.AlmacenContrapartida)) != '' AND Entrada.CodigoAlmacen = Salida.AlmacenContrapartida)
        OR
        -- Si no, relacionar por almacenes diferentes
        (LTRIM(RTRIM(Salida.AlmacenContrapartida)) = '' AND Salida.CodigoAlmacen != Entrada.CodigoAlmacen)
    )
ORDER BY Salida.Fecha DESC;

-- Consulta para ver estadísticas: total de movimientos de hoy (solo Mobility)
SELECT 
    TipoMovimiento,
    CASE 
        WHEN TipoMovimiento = 1 THEN 'Entrada'
        WHEN TipoMovimiento = 2 THEN 'Salida'
        ELSE 'Otro'
    END AS TipoMovimientoDesc,
    COUNT(*) AS TotalMovimientos
FROM [dbo].[MovimientoStock]
WHERE Fecha >= @FechaHoy
    AND Fecha < @FechaHoyFin
    AND TipoMovimiento IN (1, 2)
    AND CodigoCanal = '0'  -- SOLO traspasos Mobility (CodigoCanal = 0 exactamente)
GROUP BY TipoMovimiento
ORDER BY TipoMovimiento;

-- Consulta adicional: Movimientos de salida SIN entrada correspondiente (solo Mobility)
SELECT 
    COUNT(*) AS SalidasSinEntrada
FROM [dbo].[MovimientoStock] AS Salida
WHERE Salida.TipoMovimiento = 2
    AND Salida.CodigoCanal = '0'  -- SOLO Mobility
    AND Salida.Fecha >= @FechaHoy
    AND Salida.Fecha < @FechaHoyFin
    AND NOT EXISTS (
        SELECT 1
        FROM [dbo].[MovimientoStock] AS Entrada
        WHERE Entrada.TipoMovimiento = 1
            AND Entrada.CodigoCanal = '0'  -- SOLO Mobility
            AND Entrada.CodigoArticulo = Salida.CodigoArticulo
            AND Entrada.Unidades = Salida.Unidades
            AND (Entrada.Partida = Salida.Partida OR (Entrada.Partida IS NULL AND Salida.Partida IS NULL) OR (LTRIM(RTRIM(Entrada.Partida)) = '' AND LTRIM(RTRIM(Salida.Partida)) = ''))
            AND Entrada.Fecha >= @FechaHoy
            AND Entrada.Fecha < @FechaHoyFin
            AND ABS(DATEDIFF(HOUR, Salida.Fecha, Entrada.Fecha)) <= 24
            AND (
                (LTRIM(RTRIM(Salida.AlmacenContrapartida)) != '' AND Entrada.CodigoAlmacen = Salida.AlmacenContrapartida)
                OR
                (LTRIM(RTRIM(Salida.AlmacenContrapartida)) = '' AND Salida.CodigoAlmacen != Entrada.CodigoAlmacen)
            )
    );

-- Consulta adicional: Movimientos de entrada SIN salida correspondiente (solo Mobility)
SELECT 
    COUNT(*) AS EntradasSinSalida
FROM [dbo].[MovimientoStock] AS Entrada
WHERE Entrada.TipoMovimiento = 1
    AND Entrada.CodigoCanal = '0'  -- SOLO Mobility
    AND Entrada.Fecha >= @FechaHoy
    AND Entrada.Fecha < @FechaHoyFin
    AND NOT EXISTS (
        SELECT 1
        FROM [dbo].[MovimientoStock] AS Salida
        WHERE Salida.TipoMovimiento = 2
            AND Salida.CodigoCanal = '0'  -- SOLO Mobility
            AND Salida.CodigoArticulo = Entrada.CodigoArticulo
            AND Salida.Unidades = Entrada.Unidades
            AND (Salida.Partida = Entrada.Partida OR (Salida.Partida IS NULL AND Entrada.Partida IS NULL) OR (LTRIM(RTRIM(Salida.Partida)) = '' AND LTRIM(RTRIM(Entrada.Partida)) = ''))
            AND Salida.Fecha >= @FechaHoy
            AND Salida.Fecha < @FechaHoyFin
            AND ABS(DATEDIFF(HOUR, Salida.Fecha, Entrada.Fecha)) <= 24
            AND (
                (LTRIM(RTRIM(Salida.AlmacenContrapartida)) != '' AND Entrada.CodigoAlmacen = Salida.AlmacenContrapartida)
                OR
                (LTRIM(RTRIM(Salida.AlmacenContrapartida)) = '' AND Salida.CodigoAlmacen != Entrada.CodigoAlmacen)
            )
    );
