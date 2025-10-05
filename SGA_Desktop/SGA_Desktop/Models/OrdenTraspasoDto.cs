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
        public int LineasCompletadas => Lineas.Count(l => (l.Completada || l.Estado == "CANCELADA") && l.Estado != "SUBDIVIDIDO");
        public int LineasCanceladas => Lineas.Count(l => l.Estado == "CANCELADA" && l.Estado != "SUBDIVIDIDO");
        public string ProgresoTexto => $"{LineasCompletadas} de {TotalLineas} líneas";
        public double PorcentajeProgreso => TotalLineas > 0 ? (LineasCompletadas * 100.0 / TotalLineas) : 0;
        
        // Propiedades para el color de la barra de progreso
        public bool TieneLineasCanceladas => LineasCanceladas > 0;
        public System.Windows.Media.Brush ColorBarraProgreso 
        { 
            get 
            {
                if (TotalLineas == 0)
                {
                    return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(108, 117, 125)); // Gris
                }

                var lineasCompletadas = Lineas.Count(l => l.Completada || l.Estado == "COMPLETADA");
                var lineasCanceladas = Lineas.Count(l => l.Estado == "CANCELADA");
                
                // Solo contar líneas que se muestran en la barra (completadas + canceladas)
                var lineasVisibles = lineasCompletadas + lineasCanceladas;
                
                if (lineasVisibles == 0)
                {
                    return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(108, 117, 125)); // Gris
                }
                
                // Calcular porcentajes basados solo en líneas visibles
                var porcentajeCompletadas = (lineasCompletadas * 100.0) / lineasVisibles;
                var porcentajeCanceladas = (lineasCanceladas * 100.0) / lineasVisibles;
                
                // Si solo hay líneas completadas, usar verde sólido
                if (lineasCanceladas == 0)
                {
                    return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 124, 16)); // Verde
                }
                
                // Si solo hay líneas canceladas, usar rojo sólido
                if (lineasCompletadas == 0)
                {
                    return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(209, 52, 56)); // Rojo
                }
                
                // Crear gradiente para mostrar líneas completadas y canceladas
                var gradient = new System.Windows.Media.LinearGradientBrush();
                gradient.StartPoint = new System.Windows.Point(0, 0);
                gradient.EndPoint = new System.Windows.Point(1, 0);
                
                double offset = 0;
                
                // Agregar segmento verde para líneas completadas
                gradient.GradientStops.Add(new System.Windows.Media.GradientStop(
                    System.Windows.Media.Color.FromRgb(16, 124, 16), offset)); // Verde
                offset += porcentajeCompletadas / 100.0;
                gradient.GradientStops.Add(new System.Windows.Media.GradientStop(
                    System.Windows.Media.Color.FromRgb(16, 124, 16), offset));
                
                // Agregar segmento rojo para líneas canceladas
                gradient.GradientStops.Add(new System.Windows.Media.GradientStop(
                    System.Windows.Media.Color.FromRgb(209, 52, 56), offset)); // Rojo
                offset += porcentajeCanceladas / 100.0;
                gradient.GradientStops.Add(new System.Windows.Media.GradientStop(
                    System.Windows.Media.Color.FromRgb(209, 52, 56), offset));
                
                return gradient;
            }
        }
        
        // Información sobre subdivisiones
        public bool TieneSubdivisiones => Lineas.Any(l => l.Estado == "SUBDIVIDIDO");
        public int NumeroSubdivisiones => Lineas.Count(l => l.Estado == "SUBDIVIDIDO");
        public string TextoSubdivisiones => TieneSubdivisiones ? $"{NumeroSubdivisiones} subdivisión{(NumeroSubdivisiones > 1 ? "es" : "")}" : "";
        public bool PuedeEditar => Estado == "PENDIENTE" || Estado == "SIN_ASIGNAR" || Estado == "EN_PROCESO";
        public bool PuedeCancelar => Estado == "PENDIENTE" || Estado == "EN_PROCESO" || Estado == "SIN_ASIGNAR";
        
        public string EstadoTexto => Estado switch
        {
            "PENDIENTE" => "Pendiente",
            "EN_PROCESO" => "En Proceso",
            "COMPLETADA" => "Completada",
            "CANCELADA" => "Cancelada",
            "SIN_ASIGNAR" => "Sin Asignar",
            _ => Estado
        };
        
        public string EstadoColor => Estado switch
        {
            "PENDIENTE" => "#FFA500", // Naranja
            "EN_PROCESO" => "#0078D4", // Azul
            "COMPLETADA" => "#107C10", // Verde
            "CANCELADA" => "#D13438", // Rojo
            "SIN_ASIGNAR" => "#6C757D", // Gris oscuro para diferenciarlo
            _ => "#808080" // Gris por defecto
        };
        
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
        public string Estado { get; set; } = "PENDIENTE";
        
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
        
        // Propiedad para verificar si se puede editar el operario de esta línea
        [System.Text.Json.Serialization.JsonIgnore]
        public bool PuedeEditarOperario => Estado == "PENDIENTE" || Estado == "SIN_ASIGNAR";
    }

    public class ActualizarLineaOrdenTraspasoDto
    {
        public string? Estado { get; set; }
        public decimal? CantidadMovida { get; set; }
        public bool? Completada { get; set; }
        public int IdOperarioAsignado { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFinalizacion { get; set; }
        public Guid? IdTraspaso { get; set; }
    }
} 