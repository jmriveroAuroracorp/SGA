using System;

namespace SGA_Desktop.Helpers
{
    /// <summary>
    /// Store para comunicar filtros predefinidos a la vista de órdenes de traspaso
    /// </summary>
    public static class OrdenTraspasoFiltroStore
    {
        public static event EventHandler<FiltroOrdenTraspasoEventArgs>? FiltroSolicitado;

        public static void SolicitarFiltro(TipoFiltroOrden tipoFiltro)
        {
            FiltroSolicitado?.Invoke(null, new FiltroOrdenTraspasoEventArgs { TipoFiltro = tipoFiltro });
        }
    }

    public class FiltroOrdenTraspasoEventArgs : EventArgs
    {
        public TipoFiltroOrden TipoFiltro { get; set; }
    }

    public enum TipoFiltroOrden
    {
        Ninguno,
        TodasPendientes,
        EnProceso,
        PrioridadAlta,
        AsignadasAMi,
        SinAsignar
    }
}

