using Microsoft.AspNetCore.Mvc;
using SGA_Api.Models.Version;
using System.IO;
using System.Linq;
using System;

namespace SGA_Api.Controllers.Actualizacion

{
        [ApiController]
        [Route("api/[controller]")]
        public class VersionController : ControllerBase
        {
            [HttpGet]
            public ActionResult<VersionAppDto> Get()
            {
                // TEMPORALMENTE COMENTADO - Para evitar actualizaciones forzadas durante desarrollo
                // TODO: Descomentar cuando esté listo para producción
                /*
                var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "actualizaciones");

                if (!Directory.Exists(folderPath))
                    return StatusCode(500, "Carpeta 'actualizaciones' no encontrada.");

                var apkFiles = Directory.GetFiles(folderPath, "SGA-v*.apk");

                if (!apkFiles.Any())
                    return StatusCode(500, "No se encontró ningún archivo .apk con versión.");

                var lastApk = apkFiles
                    .Select(file => new
                    {
                        Path = file,
                        Version = Path.GetFileNameWithoutExtension(file)
                                    .Replace("SGA-v", "")
                    })
                    .OrderByDescending(f => Version.Parse(f.Version))
                    .First();

                var dto = new VersionAppDto
                {
                    Version = lastApk.Version,
                    Url = $"http://10.0.0.175:5234/actualizaciones/SGA-v{lastApk.Version}.apk"
                };

                return Ok(dto);
                */

                // TEMPORAL: Devolver versión igual a la actual para evitar actualizaciones
                var dto = new VersionAppDto
                {
                    Version = "1.1.11", // Misma versión que la instalada para que no se detecte como nueva
                    Url = ""
                };

                return Ok(dto);
            }
        [HttpGet("descargar")]
        public IActionResult DescargarUltimaApk()
        {
            // TEMPORALMENTE COMENTADO - Para evitar descargas durante desarrollo
            // TODO: Descomentar cuando esté listo para producción
            /*
            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "actualizaciones");

            if (!Directory.Exists(folderPath))
                return StatusCode(500, "Carpeta 'actualizaciones' no encontrada.");

            var apkFiles = Directory.GetFiles(folderPath, "SGA-v*.apk");

            if (!apkFiles.Any())
                return StatusCode(500, "No se encontró ningún archivo .apk con versión.");

            var lastApk = apkFiles
                .Select(file => new
                {
                    Path = file,
                    Version = Path.GetFileNameWithoutExtension(file).Replace("SGA-v", "")
                })
                .OrderByDescending(f => Version.Parse(f.Version))
                .First();

            return PhysicalFile(lastApk.Path, "application/vnd.android.package-archive", Path.GetFileName(lastApk.Path));
            */

            // TEMPORAL: Devolver error para evitar descargas
            return StatusCode(404, "Actualizaciones temporalmente deshabilitadas para desarrollo");
        }

    }
}