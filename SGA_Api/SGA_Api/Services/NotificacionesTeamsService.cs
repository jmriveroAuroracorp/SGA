using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGA_Api.Data;
using SGA_Api.Models.Notificaciones;
using SGA_Api.Models.Traspasos;
using System.Text;
using System.Data.Common;

namespace SGA_Api.Services
{
    public interface INotificacionesTeamsService
    {
        Task<int> DeterminarTipoNotificacionAsync(SageDbContext sageDbContext, string? almacen);
        Task<MrhTipoNotificacion?> ObtenerTipoNotificacionAsync(SageDbContext sageDbContext, int tipoNotificacion);
        string ConstruirMensajeTraspaso(Traspaso traspaso, string mensajeError);
        Task InsertarMrhNotificacionAsync(SageDbContext sageDbContext, Traspaso traspaso, MrhTipoNotificacion tipoNotificacion, string mensaje);
    }

    public class NotificacionesTeamsService : INotificacionesTeamsService
    {
        private readonly ILogger<NotificacionesTeamsService> _logger;

        public NotificacionesTeamsService(ILogger<NotificacionesTeamsService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Determina el tipo de notificación según el almacén destino
        /// Reglas: 002 → Canela (95), 100 → Andalucía (94), 300 → Oceania (97), resto → General (96)
        /// </summary>
        public async Task<int> DeterminarTipoNotificacionAsync(SageDbContext sageDbContext, string? almacen)
        {
            if (string.IsNullOrEmpty(almacen))
            {
                _logger.LogWarning("Almacén destino vacío, usando tipo 96 (General) por defecto");
                return 96; // General por defecto
            }

            var almacenNormalizado = almacen.Trim();

            // Mapeo según almacén destino:
            // 002 → Canela (95)
            // 100 → Andalucía (94)
            // 300 → Oceania (97)
            // Resto → General (96)
            var tipoNotificacion = almacenNormalizado switch
            {
                "002" => 95,  // Canela
                "100" => 94,  // Andalucía
                "300" => 97,  // Oceania
                _ => 96       // General por defecto
            };

            _logger.LogInformation("Determinando tipo de notificación para almacén destino {Almacen}. Tipo asignado: {TipoNotificacion}", 
                almacen, tipoNotificacion);
            
            return tipoNotificacion;
        }

        /// <summary>
        /// Obtiene los datos del tipo de notificación desde AURORA usando SQL directo
        /// </summary>
        public async Task<MrhTipoNotificacion?> ObtenerTipoNotificacionAsync(SageDbContext sageDbContext, int tipoNotificacion)
        {
            try
            {
                _logger.LogInformation("Buscando tipo de notificación {TipoNotificacion} con CodigoEmpresa = 1 en MRH_TiposNotificacione", tipoNotificacion);
                
                // Usar consulta SQL directa con JOIN a MRH_NotificacionesEmail para obtener Email y CanalTeams
                // MRH_TiposNotificacione solo tiene: MRH_TipoNotificacion, Descripcion, Departamento
                // MRH_NotificacionesEmail tiene: CodigoEmpresa, MRH_TipoNotificacion, Email, CanalTeams, etc.
                var sql = @"
                    SELECT TOP 1 
                        ne.CodigoEmpresa, 
                        ne.MRH_TipoNotificacion, 
                        ne.Email, 
                        ne.Telefono, 
                        ne.TelegramID, 
                        tn.Descripcion, 
                        ne.CanalTeams, 
                        tn.Departamento 
                    FROM AURORA.dbo.MRH_NotificacionesEmail ne
                    INNER JOIN AURORA.dbo.MRH_TiposNotificacione tn ON ne.MRH_TipoNotificacion = tn.MRH_TipoNotificacion
                    WHERE ne.CodigoEmpresa = @codigoEmpresa AND ne.MRH_TipoNotificacion = @tipoNotificacion";
                
                var parametros = new[]
                {
                    new Microsoft.Data.SqlClient.SqlParameter("@codigoEmpresa", 1),
                    new Microsoft.Data.SqlClient.SqlParameter("@tipoNotificacion", tipoNotificacion)
                };
                
                // Ejecutar consulta y mapear manualmente
                var connection = sageDbContext.Database.GetDbConnection();
                await connection.OpenAsync();
                
                try
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = sql;
                    command.Parameters.AddRange(parametros);
                    
                    using var reader = await command.ExecuteReaderAsync();
                    
                    if (await reader.ReadAsync())
                    {
                        // Leer por índice según el orden del SELECT
                        var tipo = new MrhTipoNotificacion
                        {
                            CodigoEmpresa = reader.GetInt16(0),  // CodigoEmpresa
                            TipoNotificacion = reader.GetInt16(1),  // MRH_TipoNotificacion (smallint)
                            Email = reader.IsDBNull(2) ? null : reader.GetString(2),  // Email
                            Telefono = reader.IsDBNull(3) ? null : reader.GetString(3),  // Telefono
                            TelegramID = reader.IsDBNull(4) ? null : reader.GetString(4),  // TelegramID
                            Descripcion = reader.IsDBNull(5) ? null : reader.GetString(5),  // Descripcion
                            CanalTeams = reader.IsDBNull(6) ? null : reader.GetString(6),  // CanalTeams
                            Departamento = reader.IsDBNull(7) ? null : reader.GetString(7)  // Departamento
                        };
                        
                        _logger.LogInformation("Tipo de notificación {TipoNotificacion} encontrado correctamente", tipoNotificacion);
                        return tipo;
                    }
                    else
                    {
                        _logger.LogWarning("No se encontró tipo de notificación {TipoNotificacion} en MRH_TiposNotificacione", tipoNotificacion);
                        return null;
                    }
                }
                finally
                {
                    await connection.CloseAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener tipo de notificación {TipoNotificacion}: {Mensaje}", tipoNotificacion, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Construye el mensaje completo con todos los datos del traspaso
        /// </summary>
        public string ConstruirMensajeTraspaso(Traspaso traspaso, string mensajeError)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"<strong>Error en Traspaso ERP</strong><br><br>");
            
            // Información básica
            sb.AppendLine($"<strong>Tipo de Traspaso:</strong> {traspaso.TipoTraspaso ?? "N/A"}<br>");
            
            if (!string.IsNullOrEmpty(traspaso.CodigoPalet))
                sb.AppendLine($"<strong>Código Palet:</strong> {traspaso.CodigoPalet}<br>");
            
            if (!string.IsNullOrEmpty(traspaso.CodigoArticulo))
                sb.AppendLine($"<strong>Código Artículo:</strong> {traspaso.CodigoArticulo}<br>");
            
            if (traspaso.Cantidad.HasValue)
                sb.AppendLine($"<strong>Cantidad:</strong> {traspaso.Cantidad.Value}<br>");
            
            // Almacenes y ubicaciones
            if (!string.IsNullOrEmpty(traspaso.AlmacenOrigen))
                sb.AppendLine($"<strong>Almacén Origen:</strong> {traspaso.AlmacenOrigen}<br>");
            
            if (!string.IsNullOrEmpty(traspaso.UbicacionOrigen))
                sb.AppendLine($"<strong>Ubicación Origen:</strong> {traspaso.UbicacionOrigen}<br>");
            
            if (!string.IsNullOrEmpty(traspaso.AlmacenDestino))
                sb.AppendLine($"<strong>Almacén Destino:</strong> {traspaso.AlmacenDestino}<br>");
            
            if (!string.IsNullOrEmpty(traspaso.UbicacionDestino))
                sb.AppendLine($"<strong>Ubicación Destino:</strong> {traspaso.UbicacionDestino}<br>");
            
            // Información adicional
            if (!string.IsNullOrEmpty(traspaso.Partida))
                sb.AppendLine($"<strong>Partida:</strong> {traspaso.Partida}<br>");
            
            if (traspaso.FechaCaducidad.HasValue)
                sb.AppendLine($"<strong>Fecha Caducidad:</strong> {traspaso.FechaCaducidad.Value:dd/MM/yyyy}<br>");
            
            if (traspaso.FechaInicio != default)
                sb.AppendLine($"<strong>Fecha Inicio:</strong> {traspaso.FechaInicio:dd/MM/yyyy HH:mm}<br>");
            
            if (traspaso.FechaFinalizacion.HasValue)
                sb.AppendLine($"<strong>Fecha Finalización:</strong> {traspaso.FechaFinalizacion.Value:dd/MM/yyyy HH:mm}<br>");
            
            // Error
            sb.AppendLine($"<br><strong>Error:</strong><br>");
            sb.AppendLine($"{mensajeError ?? "Error no especificado"}<br>");
            
            // IDs para referencia
            sb.AppendLine($"<br><strong>ID Traspaso:</strong> {traspaso.Id}<br>");
            if (traspaso.MovPosicionOrigen != Guid.Empty)
                sb.AppendLine($"<strong>MovPosicion Origen:</strong> {traspaso.MovPosicionOrigen}<br>");
            if (traspaso.MovPosicionDestino != Guid.Empty)
                sb.AppendLine($"<strong>MovPosicion Destino:</strong> {traspaso.MovPosicionDestino}<br>");

            return sb.ToString();
        }

        /// <summary>
        /// Inserta la notificación en MRH_Notificaciones en AURORA
        /// </summary>
        public async Task InsertarMrhNotificacionAsync(
            SageDbContext sageDbContext,
            Traspaso traspaso,
            MrhTipoNotificacion tipoNotificacion,
            string mensajeCompleto)
        {
            try
            {
                var asunto = $"Error en Traspaso - {traspaso.TipoTraspaso ?? "Traspaso"}";
                if (!string.IsNullOrEmpty(traspaso.CodigoPalet))
                    asunto += $" - Palet: {traspaso.CodigoPalet}";
                else if (!string.IsNullOrEmpty(traspaso.CodigoArticulo))
                    asunto += $" - Artículo: {traspaso.CodigoArticulo}";

                // MovPosicion se genera automáticamente con NEWID() en SQL
                var sql = @"
                    INSERT INTO MRH_Notificaciones 
                    (CodigoEmpresa, MovPosicion, MRH_OrigenNotificacion, MRH_Interno, FechaRegistro, 
                     EnviaEmail, Email, Asunto, Mensaje, CanalTeams)
                    VALUES 
                    (@codigoEmpresa, NEWID(), @origenNotificacion, @interno, @fechaRegistro,
                     @enviaEmail, @email, @asunto, @mensaje, @canalTeams)";

                var parametros = new[]
                {
                    new Microsoft.Data.SqlClient.SqlParameter("@codigoEmpresa", 1),
                    new Microsoft.Data.SqlClient.SqlParameter("@origenNotificacion", "AURORA SGA"),
                    new Microsoft.Data.SqlClient.SqlParameter("@interno", -1),
                    new Microsoft.Data.SqlClient.SqlParameter("@fechaRegistro", DateTime.Now),
                    new Microsoft.Data.SqlClient.SqlParameter("@enviaEmail", -1),
                    new Microsoft.Data.SqlClient.SqlParameter("@email", tipoNotificacion.Email ?? "aurorabot@auroracorp.es"),
                    new Microsoft.Data.SqlClient.SqlParameter("@asunto", asunto),
                    new Microsoft.Data.SqlClient.SqlParameter("@mensaje", mensajeCompleto),
                    new Microsoft.Data.SqlClient.SqlParameter("@canalTeams", tipoNotificacion.CanalTeams ?? (object)DBNull.Value)
                };

                await sageDbContext.Database.ExecuteSqlRawAsync(sql, parametros);
                
                _logger.LogInformation("Notificación MRH insertada para traspaso {TraspasoId}, tipo {TipoNotificacion}",
                    traspaso.Id, tipoNotificacion.TipoNotificacion);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al insertar notificación MRH para traspaso {TraspasoId}", traspaso.Id);
                throw;
            }
        }
    }
}

