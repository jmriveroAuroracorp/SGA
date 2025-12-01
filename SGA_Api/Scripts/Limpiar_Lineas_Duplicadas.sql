-- ⚠️ IMPORTANTE: Cambiar el PaletId por el que necesites
DECLARE @PaletId UNIQUEIDENTIFIER = 'E5BCEDD0-2889-41BE-811E-7343A6694469';

-- Eliminar todas las líneas de CO60044 del palet específico
DELETE FROM [dbo].[TempPaletLineas]
WHERE [PaletId] = @PaletId
  AND [CodigoArticulo] = 'CO60044';

-- Eliminar duplicados del palet específico, dejar solo UNA línea por artículo (sin importar lote)
WITH LineasConPrioridad AS (
    SELECT 
        [Id],
        ROW_NUMBER() OVER (
            PARTITION BY [PaletId], [CodigoArticulo]
            ORDER BY 
                CASE WHEN [TraspasoId] IS NOT NULL THEN 0 ELSE 1 END,
                [FechaAgregado] DESC
        ) AS Prioridad
    FROM [dbo].[TempPaletLineas]
    WHERE [PaletId] = @PaletId
      AND [Procesada] = 0
)
DELETE tpl
FROM [dbo].[TempPaletLineas] tpl
INNER JOIN LineasConPrioridad lcp ON tpl.[Id] = lcp.[Id]
WHERE lcp.Prioridad > 1;

