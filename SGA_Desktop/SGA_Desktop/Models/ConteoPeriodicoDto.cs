using System;

namespace SGA_Desktop.Models
{
    public class ConteoPeriodicoDto
    {
        public Guid GuidID { get; set; }
        public int CodigoEmpresa { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public int? FrecuenciaDias { get; set; }
        public DateTime? FechaUltimaRenovacion { get; set; }
        public DateTime? FechaProximaRenovacion { get; set; }
        public bool Activo { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string? CodigoOperario { get; set; }
        public string? CodigoAlmacen { get; set; }
        public string Alcance { get; set; } = string.Empty;
        public string CreadoPorCodigo { get; set; } = string.Empty;
        public byte Prioridad { get; set; }
        public DateTime FechaCreacion { get; set; }
        public int TotalRenovaciones { get; set; }
        
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
        
        // Propiedades calculadas para la UI
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
    }
}

