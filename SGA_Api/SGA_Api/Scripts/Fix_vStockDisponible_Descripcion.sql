-- Script para corregir la descripción del artículo en la vista vStockDisponible
-- Asegura que siempre devuelva la descripción usando ISNULL

USE [AURORA_SGA]
GO

IF OBJECT_ID('dbo.vStockDisponible', 'V') IS NOT NULL
    DROP VIEW [dbo].[vStockDisponible]
GO

SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE VIEW [dbo].[vStockDisponible] AS
SELECT 
    -- 🔷 CORREGIDO: Usar ISNULL para asegurar que siempre haya descripción
    ISNULL(Articulos.DescripcionArticulo, '') AS DescripcionArticulo,  
    ISNULL(Articulos.CodigoAlternativo, '') AS CodigoAlternativo, 
    ISNULL(Articulos.CodigoAlternativo2, '') AS CodigoAlternativo2, 
    ISNULL(Articulos.ReferenciaEdi_, '') AS ReferenciaEdi_, 
    ISNULL(Articulos.MRHCodigoAlternativo3, '') AS MRHCodigoAlternativo3, 
    ISNULL(Articulos.VCodigoDUN14, '') AS VCodigoDUN14, 
    Almacenes.CodigoCentro, 
    stock.CodigoEmpresa, 
    stock.CodigoArticulo,
    stock.CodigoAlmacen,
    ISNULL(Almacenes.Almacen, '') AS Almacen,
    stock.Ubicacion,
    stock.Partida,
    stock.FechaCaducidad,
    -- 🔷 CORREGIDO: Forzar precisión de 6 decimales para UnidadSaldo
    CAST(stock.UnidadSaldo AS decimal(38,6)) AS UnidadSaldo,
    -- 🔷 CORREGIDO: Forzar precisión de 6 decimales para Reservado
    CAST(ISNULL(reservas.Reservado, 0) AS decimal(38,6)) AS Reservado,
    -- 🔷 CORREGIDO: Forzar precisión de 6 decimales (decimal(38,6)) para preservar la precisión completa
    -- Convierte ambos operandos a decimal(38,6) antes de la resta para mantener la precisión completa
    CAST(
        CAST(stock.UnidadSaldo AS decimal(38,6)) - 
        CAST(ISNULL(reservas.Reservado, 0) AS decimal(38,6))
    AS decimal(38,6)) AS Disponible

FROM StorageControl.dbo.AcumuladoStockUbicacion AS stock
LEFT OUTER JOIN Aurora.dbo.Almacenes WITH (NOLOCK) 
    ON stock.CodigoEmpresa = Almacenes.CodigoEmpresa 
   AND stock.CodigoAlmacen = Almacenes.CodigoAlmacen
LEFT OUTER JOIN Aurora.dbo.Articulos WITH (NOLOCK) 
    ON stock.CodigoArticulo = Articulos.CodigoArticulo  
   AND stock.CodigoEmpresa = Articulos.CodigoEmpresa

LEFT OUTER JOIN (
    SELECT 
        CodigoEmpresa,
        CodigoArticulo,
        CodigoAlmacen,
        Ubicacion,
        Lote,
        -- 🔷 CORREGIDO: Forzar precisión de 6 decimales para el SUM de Reservado
        CAST(SUM(Reservado) AS decimal(38,6)) AS Reservado
    FROM (
        -- Reservas por traspasos pendientes
        SELECT 
            Trasp.CodigoEmpresa,
            Trasp.CodigoArticulo,
            Trasp.AlmacenOrigen AS CodigoAlmacen,
            Trasp.UbicacionOrigen AS Ubicacion,
            Trasp.Partida AS Lote,
            -- 🔷 CORREGIDO: Convertir Cantidad a decimal(38,6) antes del SUM
            CAST(Trasp.Cantidad AS decimal(38,6)) AS Reservado
        FROM Aurora_Sga.dbo.Traspasos AS Trasp
        WHERE Trasp.CodigoEstado IN ('PENDIENTE', 'EN_TRANSITO', 'PENDIENTE_ERP')

        UNION ALL

        -- Líneas temporales SOLO si su traspaso está pendiente (EXCLUIR líneas de conteo)
        SELECT 
            t.CodigoEmpresa,
            t.CodigoArticulo,
            t.CodigoAlmacen,
            t.Ubicacion,
            t.Lote,
            -- 🔷 CORREGIDO: Convertir Cantidad a decimal(38,6) antes del SUM
            CAST(t.Cantidad AS decimal(38,6)) AS Reservado
        FROM Aurora_Sga.dbo.TempPaletLineas t
        LEFT JOIN Aurora_Sga.dbo.Traspasos tr
            ON t.TraspasoId = tr.Id
        WHERE (tr.Id IS NULL OR tr.CodigoEstado IN ('PENDIENTE', 'EN_TRANSITO', 'PENDIENTE_ERP'))
          AND t.ConteoId IS NULL  -- 🔑 EXCLUIR líneas de conteo

    ) AS union_reservas
    GROUP BY 
        CodigoEmpresa,
        CodigoArticulo,
        CodigoAlmacen,
        Ubicacion,
        Lote
) AS reservas
    ON stock.CodigoEmpresa = reservas.CodigoEmpresa
   AND stock.CodigoArticulo = reservas.CodigoArticulo
   AND stock.CodigoAlmacen = reservas.CodigoAlmacen
   AND stock.Ubicacion = reservas.Ubicacion
   AND stock.Partida = reservas.Lote

WHERE stock.Ejercicio = (
    SELECT TOP 1 Ejercicio 
    FROM Aurora.dbo.Periodos  
    WHERE CodigoEmpresa = stock.CodigoEmpresa 
      AND Fechainicio <= GETDATE() 
    ORDER BY Fechainicio DESC
)
AND stock.UnidadSaldo <> 0;
GO

PRINT '✅ Vista vStockDisponible corregida.';
PRINT '   Ahora usa ISNULL para asegurar que siempre haya descripción del artículo.';
GO

