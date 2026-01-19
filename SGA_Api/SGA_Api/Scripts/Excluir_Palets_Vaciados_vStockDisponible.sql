-- Script para excluir líneas temporales de palets vaciados en vStockDisponible
-- Las líneas temporales de palets vaciados no deben contar como reservadas

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
    Articulos.DescripcionArticulo,  
    Articulos.CodigoAlternativo, 
    Articulos.CodigoAlternativo2, 
    Articulos.ReferenciaEdi_, 
    Articulos.MRHCodigoAlternativo3, 
    Articulos.VCodigoDUN14, 
    Almacenes.CodigoCentro, 
    stock.CodigoEmpresa, 
    stock.CodigoArticulo,
    stock.CodigoAlmacen,
    Almacenes.Almacen,
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

        -- 🔷 CORREGIDO: Líneas temporales con TraspasoId Y el traspaso está pendiente
        -- 🔷 NUEVO: EXCLUIR líneas de palets vaciados
        SELECT 
            t.CodigoEmpresa,
            t.CodigoArticulo,
            t.CodigoAlmacen,
            t.Ubicacion,
            t.Lote,
            -- 🔷 CORREGIDO: Convertir Cantidad a decimal(38,6) antes del SUM
            CAST(t.Cantidad AS decimal(38,6)) AS Reservado
        FROM Aurora_Sga.dbo.TempPaletLineas t
        INNER JOIN Aurora_Sga.dbo.Traspasos tr
            ON t.TraspasoId = tr.Id
        LEFT JOIN Aurora_Sga.dbo.Palets p
            ON t.PaletId = p.Id
        WHERE tr.CodigoEstado IN ('PENDIENTE', 'EN_TRANSITO', 'PENDIENTE_ERP')
          AND t.ConteoId IS NULL  -- 🔑 EXCLUIR líneas de conteo
          AND t.Procesada = 0  -- 🔑 Solo líneas no procesadas
          AND (p.Estado IS NULL OR UPPER(p.Estado) != 'VACIADO')  -- 🔑 EXCLUIR líneas de palets vaciados

        UNION ALL

        -- 🔷 NUEVO: Líneas temporales SIN TraspasoId (material siendo agregado al palet)
        -- 🔷 NUEVO: EXCLUIR líneas de palets vaciados
        -- Estas líneas deben contarse como reservadas para evitar que otro usuario "robe" el material
        -- mientras un operario está agregando artículos al palet
        SELECT 
            t.CodigoEmpresa,
            t.CodigoArticulo,
            t.CodigoAlmacen,
            t.Ubicacion,
            t.Lote,
            -- 🔷 CORREGIDO: Convertir Cantidad a decimal(38,6) antes del SUM
            CAST(t.Cantidad AS decimal(38,6)) AS Reservado
        FROM Aurora_Sga.dbo.TempPaletLineas t
        LEFT JOIN Aurora_Sga.dbo.Palets p
            ON t.PaletId = p.Id
        WHERE t.TraspasoId IS NULL  -- 🔑 Líneas sin traspaso asignado (aún no se cerró el palet)
          AND t.Procesada = 0  -- 🔑 Solo líneas no procesadas
          AND t.ConteoId IS NULL  -- 🔑 EXCLUIR líneas de conteo
          AND t.InventarioId IS NULL  -- 🔑 EXCLUIR líneas de inventario (ajustes)
          AND (p.Estado IS NULL OR UPPER(p.Estado) != 'VACIADO')  -- 🔑 EXCLUIR líneas de palets vaciados

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
