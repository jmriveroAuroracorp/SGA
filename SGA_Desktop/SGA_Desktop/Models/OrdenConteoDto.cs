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
                    "PALET" => "Palet",
                    _ => Alcance
                };
            }
        }

        // Propiedad para el nombre del operario (se asigna desde el ViewModel)
        public string? NombreOperario { get; set; }
        
        public string OperarioDisplay => string.IsNullOrEmpty(NombreOperario) 
            ? "Sin asignar"
            : NombreOperario;

        // Propiedades para mostrar información resumida
        public bool PuedeAsignar => Estado == "PLANIFICADO";
        public bool PuedeCerrar => Estado == "EN_PROCESO";
        public bool PuedeVer => true;
        public bool PuedeEditar => Estado == "PLANIFICADO" || Estado == "ASIGNADO";
    }
} 