# 🔧 Instrucciones para Corregir la Precisión de Decimales en Inventarios

## 📋 Resumen del Problema

Las cantidades de stock en inventarios se están mostrando con solo 2-4 decimales (ej: `12.36`) cuando deberían mostrar hasta 6 decimales (ej: `12.35998`).

## 🎯 Solución Completa

### Paso 1: Actualizar las Columnas de la Base de Datos

**Ejecutar:** `Fix_InventarioLineas_Precision.sql`

Este script cambia las columnas de `decimal(18,4)` a `decimal(18,6)` en:
- `InventarioLineasTemp`: `StockActual`, `CantidadContada`
- `InventarioLineas`: `StockActual`, `StockTeorico`, `StockContado`, `AjusteFinal`

### Paso 2: Recalcular los Valores Existentes

**OPCIÓN A - Script SQL (Recomendado para datos existentes):**
Ejecutar: `Recalcular_StockActual_InventarioLineasTemp.sql`

Este script recalcula `StockActual` desde las fuentes originales:
- **Líneas de PALET**: Desde `PaletLineas` (suma de cantidades del palet)
- **Líneas SUELTAS**: Desde `AcumuladoStockUbicacion` - `PaletLineas` (stock total menos paletizado)

**OPCIÓN B - Regenerar desde API (Para nuevos inventarios o regeneración completa):**
1. Usar el endpoint: `POST /api/Inventario/generar-lineas-temporales/{idInventario}`
2. Esto eliminará las líneas existentes y creará nuevas con precisión completa

### Paso 3: Verificar que los Formatos en Frontend Estén Correctos

Los formatos XAML ya están actualizados a `StringFormat='0.########'` en:
- ✅ `ContarInventarioDialog.xaml`
- ✅ `VerInventarioDialog.xaml`
- ✅ `ReconteoLineasProblematicasDialog.xaml`
- ✅ Otros diálogos de inventario

## ⚠️ IMPORTANTE

**Los datos EXISTENTES no se actualizarán automáticamente al cambiar el tipo de columna.**

Debes ejecutar el script de recálculo (`Recalcular_StockActual_InventarioLineasTemp.sql`) O regenerar las líneas temporales desde el API.

## 🔍 Verificación

Después de ejecutar los scripts:

1. **Verificar en SQL:**
   ```sql
   SELECT StockActual, CantidadContada 
   FROM InventarioLineasTemp 
   WHERE IdInventario = 'TU_ID_INVENTARIO'
   ```

2. **Verificar en la aplicación:**
   - Abrir el inventario en la aplicación
   - Verificar que los valores muestran hasta 6 decimales (ej: `12.35998` en lugar de `12.36`)

## 📝 Notas Técnicas

- La vista `vStockDisponible` ya fue actualizada a precisión de 6 decimales
- Los modelos C# ya están actualizados a `decimal(18,6)`
- Los formatos XAML usan `StringFormat='0.########'` (muestra hasta 8 decimales significativos)

