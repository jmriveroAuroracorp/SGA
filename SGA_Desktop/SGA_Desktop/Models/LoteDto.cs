using System;
using Newtonsoft.Json;

namespace SGA_Desktop.Models
{
    public class LoteDto
    {
        [JsonProperty("partida")]
        public string Partida { get; set; } = string.Empty;
        
        [JsonProperty("fechaCaducidad")]
        public DateTime? FechaCaducidad { get; set; }
    }
}

