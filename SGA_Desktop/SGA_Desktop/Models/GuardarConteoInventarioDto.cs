using System;
using System.Collections.Generic;

namespace SGA_Desktop.Models
{
    public class GuardarConteoInventarioDto
    {
        public Guid IdInventario { get; set; }
        public List<ArticuloConteoDto> Articulos { get; set; } = new List<ArticuloConteoDto>();
    }

    public class ArticuloConteoDto
    {
        public string CodigoArticulo { get; set; } = string.Empty;
        public string CodigoUbicacion { get; set; } = string.Empty;
        public string Partida { get; set; } = string.Empty;
        public DateTime? FechaCaducidad { get; set; }
        public decimal CantidadInventario { get; set; }
        public int UsuarioConteo { get; set; }
    }
} 