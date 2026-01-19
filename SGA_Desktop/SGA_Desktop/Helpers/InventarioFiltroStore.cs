using System;

namespace SGA_Desktop.Helpers
{
    /// <summary>
    /// Store para comunicar filtros predefinidos a la vista de inventarios
    /// </summary>
    public static class InventarioFiltroStore
    {
        public static event EventHandler<FiltroInventarioEventArgs>? FiltroSolicitado;

        public static void SolicitarFiltro(TipoFiltroInventario tipoFiltro)
        {
            FiltroSolicitado?.Invoke(null, new FiltroInventarioEventArgs { TipoFiltro = tipoFiltro });
        }
    }

    public class FiltroInventarioEventArgs : EventArgs
    {
        public TipoFiltroInventario TipoFiltro { get; set; }
    }

    public enum TipoFiltroInventario
    {
        Ninguno,
        Abiertos,
        EnConteo,
        Consolidados,
        PendientesCierre,
        Cerrados
    }
}

