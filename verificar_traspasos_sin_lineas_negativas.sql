-- Consulta para verificar por qué no se crearon líneas negativas en traspasos de artículo
-- Usa esta consulta con los datos de tus traspasos (Id, CodigoArticulo, AlmacenOrigen, UbicacionOrigen, Partida)

-- Reemplaza estos valores con los datos de tus traspasos:
-- @TraspasoId = el Id del traspaso que quieres verificar
-- O ejecuta sin filtro para ver todos los traspasos tipo ARTICULO

DECLARE @TraspasoId UNIQUEIDENTIFIER = NULL; -- Cambia esto por el ID del traspaso que quieres revisar
DECLARE @CodigoArticulo NVARCHAR(50) = NULL; -- Opcional: filtrar por artículo
DECLARE @FechaDesde DATETIME = DATEADD(DAY, -7, GETDATE()); -- Últimos 7 días

-- PASO 1: Listar traspasos tipo ARTICULO y verificar si tienen líneas negativas asociadas
SELECT 
    t.Id AS TraspasoId,
    t.CodigoArticulo,
    t.Cantidad AS CantidadTraspaso,
    t.AlmacenOrigen,
    t.UbicacionOrigen,
    t.AlmacenDestino,
    t.UbicacionDestino,
    t.Partida AS Lote,
    t.TipoTraspaso,
    t.CodigoEstado,
    t.FechaInicio,
    t.FechaFinalizacion,
    t.PaletId AS PaletIdEnTraspaso, -- Si tiene PaletId, debería haber creado línea negativa
    -- Verificar si existe línea negativa asociada
    (SELECT COUNT(*) FROM TempPaletLineas tpl 
     WHERE tpl.TraspasoId = t.Id AND tpl.Cantidad < 0) AS TieneLineaNegativa,
    -- Verificar líneas temporales asociadas (positivas o negativas)
    (SELECT COUNT(*) FROM TempPaletLineas tpl2 
     WHERE tpl2.TraspasoId = t.Id) AS TotalLineasTemporalesAsociadas
FROM 
    Traspasos t
WHERE 
    t.TipoTraspaso = 'ARTICULO'
    AND (@TraspasoId IS NULL OR t.Id = @TraspasoId)
    AND (@CodigoArticulo IS NULL OR t.CodigoArticulo = @CodigoArticulo)
    AND t.FechaInicio >= @FechaDesde
ORDER BY 
    t.FechaInicio DESC;

-- PASO 2: Para cada traspaso, verificar si hay stock paletizado en origen
-- (Esto replica la lógica del código)
SELECT 
    t.Id AS TraspasoId,
    t.CodigoArticulo,
    t.AlmacenOrigen,
    t.UbicacionOrigen,
    t.Partida AS LoteTraspaso,
    t.Cantidad AS CantidadTraspaso,
    -- Stock paletizado en origen (suma de PaletLineas)
    ISNULL(SUM(pl.Cantidad), 0) AS StockPaletizadoEnOrigen,
    -- Stock disponible total (debería venir de vStockDisponible)
    -- Nota: Si tienes acceso a la vista, agrégalo aquí
    -- Stock total disponible aproximado (suma de PaletLineas + suelto estimado)
    COUNT(DISTINCT pl.PaletId) AS NumeroPaletsConStock,
    -- ¿Hay líneas en PaletLineas que coincidan?
    CASE 
        WHEN EXISTS (
            SELECT 1 FROM PaletLineas pl2
            WHERE pl2.CodigoArticulo = t.CodigoArticulo
            AND pl2.CodigoAlmacen = t.AlmacenOrigen
            AND pl2.Ubicacion = t.UbicacionOrigen
            AND (pl2.Lote = t.Partida OR (pl2.Lote IS NULL AND t.Partida IS NULL))
        ) THEN 'SÍ'
        ELSE 'NO'
    END AS TieneLineaPaletQueCoincide
FROM 
    Traspasos t
    LEFT JOIN PaletLineas pl ON 
        pl.CodigoArticulo = t.CodigoArticulo
        AND pl.CodigoAlmacen = t.AlmacenOrigen
        AND pl.Ubicacion = t.UbicacionOrigen
        AND (pl.Lote = t.Partida OR (pl.Lote IS NULL AND t.Partida IS NULL))
WHERE 
    t.TipoTraspaso = 'ARTICULO'
    AND (@TraspasoId IS NULL OR t.Id = @TraspasoId)
    AND (@CodigoArticulo IS NULL OR t.CodigoArticulo = @CodigoArticulo)
    AND t.FechaInicio >= @FechaDesde
GROUP BY 
    t.Id, t.CodigoArticulo, t.AlmacenOrigen, t.UbicacionOrigen, t.Partida, t.Cantidad
ORDER BY 
    t.FechaInicio DESC;

-- PASO 3: Ver líneas de PaletLineas que deberían haber sido detectadas como origen
SELECT 
    t.Id AS TraspasoId,
    t.CodigoArticulo,
    t.AlmacenOrigen AS TraspasoAlmacenOrigen,
    t.UbicacionOrigen AS TraspasoUbicacionOrigen,
    t.Partida AS TraspasoLote,
    pl.PaletId,
    p.Codigo AS CodigoPalet,
    p.Estado AS EstadoPalet,
    pl.Cantidad AS CantidadEnPalet,
    pl.Lote AS PaletLote,
    pl.CodigoAlmacen AS PaletAlmacen,
    pl.Ubicacion AS PaletUbicacion,
    -- Verificar coincidencia exacta
    CASE 
        WHEN pl.CodigoArticulo = t.CodigoArticulo
            AND pl.CodigoAlmacen = t.AlmacenOrigen
            AND pl.Ubicacion = t.UbicacionOrigen
            AND (pl.Lote = t.Partida OR (pl.Lote IS NULL AND t.Partida IS NULL))
        THEN 'COINCIDE'
        ELSE 'NO COINCIDE'
    END AS Coincidencia,
    -- Motivo por el que no coincidió
    CASE 
        WHEN pl.CodigoArticulo != t.CodigoArticulo THEN 'Articulo diferente'
        WHEN pl.CodigoAlmacen != t.AlmacenOrigen THEN 'Almacen diferente: ' + pl.CodigoAlmacen + ' vs ' + t.AlmacenOrigen
        WHEN pl.Ubicacion != t.UbicacionOrigen THEN 'Ubicacion diferente: ' + ISNULL(pl.Ubicacion, 'NULL') + ' vs ' + ISNULL(t.UbicacionOrigen, 'NULL')
        WHEN (pl.Lote != t.Partida OR (pl.Lote IS NULL AND t.Partida IS NOT NULL) OR (pl.Lote IS NOT NULL AND t.Partida IS NULL)) 
        THEN 'Lote diferente: ' + ISNULL(pl.Lote, 'NULL') + ' vs ' + ISNULL(t.Partida, 'NULL')
        ELSE 'OK'
    END AS MotivoDiferencia
FROM 
    Traspasos t
    LEFT JOIN PaletLineas pl ON pl.CodigoArticulo = t.CodigoArticulo
    LEFT JOIN Palets p ON p.Id = pl.PaletId
WHERE 
    t.TipoTraspaso = 'ARTICULO'
    AND (@TraspasoId IS NULL OR t.Id = @TraspasoId)
    AND (@CodigoArticulo IS NULL OR t.CodigoArticulo = @CodigoArticulo)
    AND t.FechaInicio >= @FechaDesde
    AND pl.CodigoAlmacen = t.AlmacenOrigen
ORDER BY 
    t.FechaInicio DESC, pl.PaletId;

