using System;

namespace SGA_Desktop.Helpers
{
    /// <summary>
    /// Store para comunicar filtros predefinidos a la vista de conteos rotativos
    /// </summary>
    public static class ConteoFiltroStore
    {
        public static event EventHandler<FiltroConteoEventArgs>? FiltroSolicitado;

        public static void SolicitarFiltro(TipoFiltroConteo tipoFiltro)
        {
            FiltroSolicitado?.Invoke(null, new FiltroConteoEventArgs { TipoFiltro = tipoFiltro });
        }
    }

    public class FiltroConteoEventArgs : EventArgs
    {
        public TipoFiltroConteo TipoFiltro { get; set; }
    }

    public enum TipoFiltroConteo
    {
        Ninguno,
        Pendientes,
        EnProceso,
        PendientesRevision,
        PrioridadAlta,
        Cerrados
    }
}

