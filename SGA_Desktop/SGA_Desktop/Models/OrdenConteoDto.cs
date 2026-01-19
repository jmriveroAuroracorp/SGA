using System;
using System.Collections.Generic;

namespace SGA_Desktop.Models
{
    public class OrdenConteoDto
    {
        public Guid GuidID { get; set; }
        public int CodigoEmpresa { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Visibilidad { get; set; } = string.Empty;
        public string ModoGeneracion { get; set; } = string.Empty;
        public string Alcance { get; set; } = string.Empty;
        public string? FiltrosJson { get; set; }
        public DateTime? FechaPlan { get; set; }
        public DateTime? FechaEjecucion { get; set; }
        public string? SupervisorCodigo { get; set; }
        public string CreadoPorCodigo { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public byte Prioridad { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string? CodigoOperario { get; set; }
        public string? CodigoAlmacen { get; set; }
        public string? CodigoUbicacion { get; set; }
        public string? CodigoArticulo { get; set; }
        public string? DescripcionArticulo { get; set; }
        public string? LotePartida { get; set; }
        public decimal? CantidadTeorica { get; set; }
        public string? Comentario { get; set; }
        public DateTime? FechaAsignacion { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaCierre { get; set; }
        
        // Total de lecturas registradas
        public int? TotalLecturas { get; set; }
        
        // Propiedades para conteos periódicos
        public bool EsPeriodico { get; set; } = false;
        public bool Activo { get; set; } = true;
        public DateTime? FechaProximaRenovacion { get; set; }
        public int? FrecuenciaDias { get; set; }
        
        // Si esta orden es una renovación de un conteo periódico, aquí está el Guid del conteo periódico original
        public Guid? OrdenPadreGuid { get; set; }

        // Propiedades adicionales para la UI
        public string EstadoFormateado
        {
            get
            {
                return Estado switch
                {
                    "PLANIFICADO" => "Planificado",
                    "ASIGNADO" => "Asignado",
                    "EN_PROCESO" => "En Proceso",
                    "CERRADO" => "Cerrado",
                    "CANCELADO" => "Cancelado",
                    _ => Estado
                };
            }
        }

        public string PrioridadTexto
        {
            get
            {
                return Prioridad switch
                {
                    1 => "Muy Baja",
                    2 => "Baja",
                    3 => "Normal",
                    4 => "Alta",
                    5 => "Muy Alta",
                    _ => "Normal"
                };
            }
        }

        public string AlcanceFormateado
        {
            get
            {
                return Alcance switch
                {
                    "ALMACEN" => "Almacén",
                    "PASILLO" => "Pasillo",
                    "ESTANTERIA" => "Estantería",
                    "UBICACION" => "Ubicación",
                    "ARTICULO" => "Artículo",
                    "MULTIARTICULO" => "MultiArtículo",
                    "PALET" => "Palet",
                    _ => Alcance
                };
            }
        }

        // Propiedad para el nombre del operario (se asigna desde el ViewModel)
        public string? NombreOperario { get; set; }
        
        // Propiedad para el nombre del creador (se asigna desde el ViewModel)
        public string? NombreCreador { get; set; }
        
        public string OperarioDisplay => string.IsNullOrEmpty(NombreOperario) 
            ? "Sin asignar"
            : NombreOperario;
            
        public string CreadorDisplay => string.IsNullOrEmpty(NombreCreador) 
            ? CreadoPorCodigo ?? "N/A"
            : NombreCreador;

        // Propiedades para mostrar información resumida
        public bool PuedeAsignar => Estado == "PLANIFICADO";
        public bool PuedeCerrar => Estado == "EN_PROCESO";
        public bool PuedeVer => true;
        public bool PuedeEditar => Estado == "PLANIFICADO" || Estado == "ASIGNADO";
        
        // Propiedad calculada para mostrar líneas contadas
        public string LineasContadasTexto
        {
            get
            {
                if (TotalLecturas.HasValue && TotalLecturas.Value > 0)
                {
                    return $"{TotalLecturas.Value} línea{(TotalLecturas.Value == 1 ? "" : "s")} contada{(TotalLecturas.Value == 1 ? "" : "s")}";
                }
                return "Sin líneas contadas";
            }
        }
        
        // Propiedad calculada para saber si la fecha plan está vencida (pasada y no cerrada)
        public bool FechaPlanVencida
        {
            get
            {
                if (!FechaPlan.HasValue)
                    return false;
                    
                // Si la fecha plan ya pasó y la orden no está cerrada
                return FechaPlan.Value < DateTime.Now && 
                       Estado != "CERRADO" && 
                       !FechaCierre.HasValue;
            }
        }
        
        // Propiedad calculada para saber si esta orden es una renovación de un conteo periódico
        public bool EsRenovacion => OrdenPadreGuid.HasValue;
        
        // Propiedad calculada para saber si es el conteo periódico original (no una renovación)
        public bool EsPeriodicoOriginal => EsPeriodico && !OrdenPadreGuid.HasValue;
    }
} 