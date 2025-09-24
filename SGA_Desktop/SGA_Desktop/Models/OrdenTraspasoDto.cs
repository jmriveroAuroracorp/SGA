namespace SGA_Desktop.Models
{
    public class OrdenTraspasoDto
    {
        public Guid IdOrdenTraspaso { get; set; }
        public short CodigoEmpresa { get; set; }
        public string Estado { get; set; }
        public short Prioridad { get; set; }
        public DateTime? FechaPlan { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFinalizacion { get; set; }
        public string TipoOrigen { get; set; }
        public int UsuarioCreacion { get; set; }
        public string? NombreUsuarioCreacion { get; set; }
        public string? Comentarios { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string CodigoOrden { get; set; }
        public string? CodigoAlmacenDestino { get; set; }
        public List<LineaOrdenTraspasoDetalleDto> Lineas { get; set; } = new();

        // Propiedades calculadas para la UI
        public string EstadoFormateado => Estado switch
        {
            "PENDIENTE" => "Pendiente",
            "EN_PROCESO" => "En Proceso",
            "COMPLETADA" => "Completada",
            "CANCELADA" => "Cancelada",
            _ => Estado
        };

        public string AlmacenOrigenDescripcion => Lineas.FirstOrDefault()?.CodigoAlmacenOrigen ?? "N/A";
        public string AlmacenDestinoDescripcion => Lineas.FirstOrDefault()?.CodigoAlmacenDestino ?? "N/A";
        public string UsuarioAsignadoNombre => "Sin asignar"; // Se asigna por línea individual
        
        // Conteo excluyendo líneas SUBDIVIDIDO (que son líneas padre que se descomponen)
        public int TotalLineas => Lineas.Count(l => l.Estado != "SUBDIVIDIDO");
        public int LineasCompletadas => Lineas.Count(l => l.Completada && l.Estado != "SUBDIVIDIDO");
        public string ProgresoTexto => $"{LineasCompletadas} de {TotalLineas} líneas";
        public double PorcentajeProgreso => TotalLineas > 0 ? (LineasCompletadas * 100.0 / TotalLineas) : 0;
        
        // Información sobre subdivisiones
        public bool TieneSubdivisiones => Lineas.Any(l => l.Estado == "SUBDIVIDIDO");
        public int NumeroSubdivisiones => Lineas.Count(l => l.Estado == "SUBDIVIDIDO");
        public string TextoSubdivisiones => TieneSubdivisiones ? $"{NumeroSubdivisiones} subdivisión{(NumeroSubdivisiones > 1 ? "es" : "")}" : "";
        public bool PuedeEditar => Estado == "PENDIENTE";
        public bool PuedeCancelar => Estado == "PENDIENTE" || Estado == "EN_PROCESO";
        
        public string PrioridadTexto => Prioridad switch
        {
            1 => "Muy Baja",
            2 => "Baja", 
            3 => "Normal",
            4 => "Alta",
            5 => "Muy Alta",
            _ => "Desconocida"
        };
    }

    public class LineaOrdenTraspasoDetalleDto
    {
        public Guid IdLineaOrden { get; set; }
        public Guid IdOrdenTraspaso { get; set; }
        public int Orden { get; set; }
        public string CodigoArticulo { get; set; }
        public string? DescripcionArticulo { get; set; }
        public DateTime? FechaCaducidad { get; set; }
        public decimal CantidadPlan { get; set; }
        public string? CodigoAlmacenOrigen { get; set; }
        public string? UbicacionOrigen { get; set; }
        public string? Partida { get; set; }
        public string? PaletOrigen { get; set; }
        public string CodigoAlmacenDestino { get; set; }
        public string? UbicacionDestino { get; set; }
        public string? PaletDestino { get; set; }
        public string Estado { get; set; }
        public decimal? CantidadMovida { get; set; }
        public bool Completada { get; set; }
        public int IdOperarioAsignado { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFinalizacion { get; set; }
        public Guid? IdTraspaso { get; set; }
        
        // Propiedades adicionales para la UI
        public string NombreOperario { get; set; } = "Sin asignar";
        public bool EsPadre { get; set; } = false;
        
        // Propiedad calculada para la diferencia (Plan - Movida)
        public decimal? Diferencia => Estado == "SUBDIVIDIDO" ? null : CantidadPlan - CantidadMovida;
        public string DiferenciaTexto => Estado == "SUBDIVIDIDO" ? "" : $"{Diferencia:F2}";
    }

    public class CrearOrdenTraspasoDto
    {
        public short CodigoEmpresa { get; set; }
        public short Prioridad { get; set; } = 10;
        public DateTime? FechaPlan { get; set; }
        public string TipoOrigen { get; set; } = "SGA";
        public int UsuarioCreacion { get; set; }
        public string? Comentarios { get; set; }
        public string? CodigoAlmacenDestino { get; set; }
        public List<CrearLineaOrdenTraspasoDto> Lineas { get; set; } = new();
    }

    public class CrearLineaOrdenTraspasoDto
    {
        public int Orden { get; set; }
        public string CodigoArticulo { get; set; }
        public string? DescripcionArticulo { get; set; }
        public DateTime? FechaCaducidad { get; set; }
        public decimal CantidadPlan { get; set; }
        public string? CodigoAlmacenOrigen { get; set; }
        public string? UbicacionOrigen { get; set; }
        public string? Partida { get; set; }
        public string? PaletOrigen { get; set; }
        public string CodigoAlmacenDestino { get; set; }
        public string? UbicacionDestino { get; set; }
        public string? PaletDestino { get; set; }
        public int IdOperarioAsignado { get; set; }
        
        // Propiedades adicionales para la UI
        [System.Text.Json.Serialization.JsonIgnore]
        public OperariosAccesoDto? OperarioSeleccionado 
        { 
            get => _operarioSeleccionado;
            set 
            { 
                _operarioSeleccionado = value;
                IdOperarioAsignado = value?.Operario ?? 0;
            }
        }
        
        private OperariosAccesoDto? _operarioSeleccionado;
        
        // Propiedades para filtrado individual por línea
        [System.Text.Json.Serialization.JsonIgnore]
        public string FiltroOperarioLinea { get; set; } = "";
        
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsDropDownOpenLinea { get; set; } = false;
        
        // Lista de operarios disponibles para esta línea (se establecerá desde el ViewModel)
        [System.Text.Json.Serialization.JsonIgnore]
        public System.Collections.ObjectModel.ObservableCollection<OperariosAccesoDto>? OperariosDisponiblesLinea { get; set; }
        
        // Vista filtrada para esta línea específica
        [System.Text.Json.Serialization.JsonIgnore]
        public System.ComponentModel.ICollectionView? OperariosViewLinea { get; set; }
    }
} 