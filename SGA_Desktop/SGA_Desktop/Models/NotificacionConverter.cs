using System;
using System.Collections.Generic;
using System.Linq;

namespace SGA_Desktop.Models
{
    /// <summary>
    /// Clase de utilidad para convertir entre diferentes tipos de notificaciones
    /// </summary>
    public static class NotificacionConverter
    {
        /// <summary>
        /// Convierte una NotificacionApiDto a NotificacionDto (modelo existente del Desktop)
        /// </summary>
        public static NotificacionDto ConvertirADesktopDto(this NotificacionApiDto apiDto)
        {
            // Determinar el tipo basado en el estado actual
            var tipo = apiDto.EstadoActual switch
            {
                "COMPLETADO" => "success",
                "ERROR_ERP" => "error", 
                "PENDIENTE_ERP" => "warning",
                "PENDIENTE" => "info",
                _ => "info"
            };

            return new NotificacionDto
            {
                Id = apiDto.IdNotificacion,
                Titulo = apiDto.Titulo,
                Mensaje = apiDto.Mensaje,
                Tipo = tipo,
                FechaCreacion = apiDto.FechaCreacion,
                Leida = apiDto.Leida,
                UsuarioId = apiDto.Destinatarios?.FirstOrDefault()?.UsuarioId ?? 0, // Se asignará desde el usuario actual si es 0
                TraspasoId = apiDto.ProcesoId?.ToString(),
                EstadoAnterior = apiDto.EstadoAnterior,
                EstadoActual = apiDto.EstadoActual,
                TipoTraspaso = apiDto.TipoNotificacion == "TRASPASO" ? "ARTICULO" : null, // Asumimos artículo por defecto
                // Los datos adicionales se pueden extraer del objeto DatosAdicionales si es necesario
            };
        }

        /// <summary>
        /// Convierte una NotificacionResumenApiDto a NotificacionDto (modelo existente del Desktop)
        /// </summary>
        public static NotificacionDto ConvertirADesktopDto(this NotificacionResumenApiDto resumenDto)
        {
            return new NotificacionDto
            {
                Id = resumenDto.IdNotificacion,
                Titulo = resumenDto.Titulo,
                Mensaje = resumenDto.MensajeResumido,
                Tipo = resumenDto.TipoIcono,
                FechaCreacion = resumenDto.FechaCreacion,
                Leida = resumenDto.Leida,
                UsuarioId = 0, // No disponible en el resumen
                TraspasoId = resumenDto.ProcesoId?.ToString(),
                EstadoAnterior = null, // No disponible en el resumen
                EstadoActual = resumenDto.EstadoActual,
                TipoTraspaso = resumenDto.TipoNotificacion == "TRASPASO" ? "TRASPASO" : null,
            };
        }

        /// <summary>
        /// Convierte una NotificacionDto (modelo existente) a CrearNotificacionApiDto
        /// </summary>
        public static CrearNotificacionApiDto ConvertirACrearApiDto(this NotificacionDto desktopDto, int usuarioDestinatario)
        {
            return new CrearNotificacionApiDto
            {
                CodigoEmpresa = 1,
                TipoNotificacion = desktopDto.TipoTraspaso ?? "AVISO_GENERAL",
                ProcesoId = !string.IsNullOrEmpty(desktopDto.TraspasoId) && Guid.TryParse(desktopDto.TraspasoId, out var traspasoId) ? traspasoId : null,
                Titulo = desktopDto.Titulo,
                Mensaje = desktopDto.Mensaje,
                EstadoAnterior = desktopDto.EstadoAnterior,
                EstadoActual = desktopDto.EstadoActual,
                EsGrupal = false,
                UsuarioIds = new List<int> { usuarioDestinatario }
            };
        }

        /// <summary>
        /// Crea un MarcarLeidaApiDto para marcar una notificación como leída
        /// </summary>
        public static MarcarLeidaApiDto CrearMarcarLeidaDto(Guid idNotificacion, int usuarioId)
        {
            return new MarcarLeidaApiDto
            {
                IdNotificacion = idNotificacion,
                UsuarioId = usuarioId,
                FechaLeida = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Extrae información adicional de traspaso desde DatosAdicionales si está disponible
        /// </summary>
        public static void ExtraerDatosTraspaso(NotificacionApiDto apiDto, NotificacionDto desktopDto)
        {
            if (apiDto.DatosAdicionales == null) return;

            try
            {
                // Aquí se podría implementar la extracción de datos específicos
                // dependiendo de la estructura del objeto DatosAdicionales
                // Por ejemplo, si viene como un diccionario o un objeto específico
                
                // Ejemplo de implementación básica:
                // if (apiDto.DatosAdicionales is Dictionary<string, object> datos)
                // {
                //     if (datos.ContainsKey("CodigoPalet"))
                //         desktopDto.CodigoPalet = datos["CodigoPalet"]?.ToString();
                //     if (datos.ContainsKey("CodigoArticulo"))
                //         desktopDto.CodigoArticulo = datos["CodigoArticulo"]?.ToString();
                //     // etc.
                // }
            }
            catch (Exception)
            {
                // Silenciar errores de conversión para no romper la funcionalidad principal
            }
        }

        /// <summary>
        /// Convierte una lista de NotificacionApiDto a lista de NotificacionDto
        /// </summary>
        public static List<NotificacionDto> ConvertirListaADesktopDto(this IEnumerable<NotificacionApiDto> apiDtos)
        {
            return apiDtos?.Select(dto => dto.ConvertirADesktopDto()).ToList() ?? new List<NotificacionDto>();
        }

        /// <summary>
        /// Convierte una lista de NotificacionResumenApiDto a lista de NotificacionDto
        /// </summary>
        public static List<NotificacionDto> ConvertirListaADesktopDto(this IEnumerable<NotificacionResumenApiDto> resumenDtos)
        {
            return resumenDtos?.Select(dto => dto.ConvertirADesktopDto()).ToList() ?? new List<NotificacionDto>();
        }
    }
}

