using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Ubicacion;
using SGA_Api.Models.Alergenos;
using System;

namespace SGA_Api.Controllers.Ubicacion
{
	[Route("api/ubicaciones")]
	[ApiController]
	public class UbicacionesController : ControllerBase
	{
		private readonly AuroraSgaDbContext _auroraSgaContext;

		public UbicacionesController(AuroraSgaDbContext context)
		{
			_auroraSgaContext = context;
		}

		[HttpGet]
		public async Task<IActionResult> GetUbicacionesDetalladas(
			[FromQuery] short codigoEmpresa,
			[FromQuery] string codigoAlmacen)
		{
			if (string.IsNullOrWhiteSpace(codigoAlmacen))
				return BadRequest("Debes especificar un código de almacén.");

			var data = await _auroraSgaContext.vUbicacionesDetalladas
				.Where(u => u.CodigoEmpresa == codigoEmpresa
						 && u.CodigoAlmacen == codigoAlmacen)
				.ToListAsync();

			return Ok(data);
		}



		//	[HttpGet("basica")]
		//	public async Task<IActionResult> GetUbicacionesBasico(
		//[FromQuery] short codigoEmpresa,
		//[FromQuery] string codigoAlmacen)
		//	{
		//		if (string.IsNullOrWhiteSpace(codigoAlmacen))
		//			return BadRequest("Debes especificar un código de almacén.");

		//		var lista = await (
		//			from u in _auroraSgaContext.Ubicaciones
		//			join cfg in _auroraSgaContext.Ubicaciones_Configuracion
		//				on new
		//				{
		//					u.CodigoEmpresa,
		//					u.CodigoAlmacen,
		//					CodigoUbicacion = u.CodigoUbicacion
		//				}
		//				equals new
		//				{
		//					cfg.CodigoEmpresa,
		//					cfg.CodigoAlmacen,
		//					CodigoUbicacion = cfg.Ubicacion
		//				} into cfgj
		//			from cfg in cfgj.DefaultIfEmpty()

		//				// Join a la tabla maestra de tipos
		//			join tu in _auroraSgaContext.TipoUbicaciones
		//				on cfg.TipoUbicacionId equals tu.TipoUbicacionId into tuj
		//			from tu in tuj.DefaultIfEmpty()

		//			where u.CodigoEmpresa == codigoEmpresa
		//			   && u.CodigoAlmacen == codigoAlmacen
		//			   && u.Obsoleta == 0

		//			select new UbicacionDetallada
		//			{
		//				CodigoEmpresa = u.CodigoEmpresa,
		//				CodigoAlmacen = u.CodigoAlmacen,
		//				Ubicacion = u.CodigoUbicacion,
		//				DescripcionUbicacion = u.DescripcionUbicacion ?? "",
		//				Pasillo = u.Pasillo,
		//				Estanteria = u.Estanteria,
		//				Altura = u.Altura,
		//				Posicion = u.Posicion,

		//				TemperaturaMin = cfg.TemperaturaMin,
		//				TemperaturaMax = cfg.TemperaturaMax,
		//				TipoPaletPermitido = cfg.TipoPaletPermitido ?? "",
		//				Habilitada = (cfg.Habilitada ?? true),

		//				// Estos dos en lugar de la vieja string
		//				TipoUbicacionId = cfg.TipoUbicacionId,
		//				TipoUbicacionDescripcion = tu.Descripcion ?? "",

		//				AlergenosPermitidos = "",
		//				AlergenosPresentes = "",
		//				RiesgoContaminacion = false
		//			}
		//		).ToListAsync();

		//		return Ok(lista);
		//	}
		[HttpGet("basica")]
		public async Task<IActionResult> GetUbicacionesBasico(
		[FromQuery] short codigoEmpresa,
		[FromQuery] string codigoAlmacen)
		{
			if (string.IsNullOrWhiteSpace(codigoAlmacen))
				return BadRequest("Debes especificar un código de almacén.");

			var lista = await (
				from u in _auroraSgaContext.Ubicaciones
				where u.CodigoEmpresa == codigoEmpresa
				   && u.CodigoAlmacen == codigoAlmacen
				   && u.Obsoleta == 0

				// LEFT JOIN a configuración
				join cfg in _auroraSgaContext.Ubicaciones_Configuracion
					on new { u.CodigoEmpresa, u.CodigoAlmacen, CodigoUbicacion = u.CodigoUbicacion }
					equals new { cfg.CodigoEmpresa, cfg.CodigoAlmacen, CodigoUbicacion = cfg.Ubicacion }
					into cfgGroup
				from cfg in cfgGroup.DefaultIfEmpty()

					// LEFT JOIN a tipologías
				join tu in _auroraSgaContext.TipoUbicaciones
					on cfg.TipoUbicacionId equals tu.TipoUbicacionId into tuGroup
				from tu in tuGroup.DefaultIfEmpty()

				select new
				{
					u.CodigoEmpresa,
					u.CodigoAlmacen,
					Ubicacion = u.CodigoUbicacion,
					DescripcionUbicacion = u.DescripcionUbicacion ?? "",
					Pasillo = u.Pasillo,
					Estanteria = u.Estanteria,
					Altura = u.Altura,
					Posicion = u.Posicion,
					Orden = u.Orden,
					// PROTEGEMOS LOS CAMPOS OPCIONALES
					TemperaturaMin = cfg != null ? cfg.TemperaturaMin : (short?)null,
					TemperaturaMax = cfg != null ? cfg.TemperaturaMax : (short?)null,
					TipoPaletPermitido = cfg != null ? cfg.TipoPaletPermitido : "",
					Habilitada = cfg != null ? cfg.Habilitada.GetValueOrDefault(true) : true,

					TipoUbicacionId = cfg != null ? cfg.TipoUbicacionId : (int?)null,
					TipoUbicacionDescripcion = tu != null ? tu.Descripcion : "",

					AlergenosPermitidos = "",  // o incluso podrías tirarlas desde aquí
					AlergenosPresentes = "",
					RiesgoContaminacion = false,
					 // Nuevos campos físicos
					Peso = u.Peso,
					DimensionX = u.DimensionX,
					DimensionY = u.DimensionY,
					DimensionZ = u.DimensionZ,
					Angulo = u.Angulo
				}
			).ToListAsync();

			return Ok(lista);
		}

		/// <summary>
		/// GET api/ubicaciones/alergenos/presentes
		/// Devuelve la lista de Códigos de Alergeno presentes en una ubicación.
		/// </summary>
		[HttpGet("alergenos/presentes")]
		public async Task<IActionResult> GetAlergenosPresentes(
		[FromQuery] short codigoEmpresa,
		[FromQuery] string codigoAlmacen,
		[FromQuery] string? ubicacion)
		{
			// Si viene null, lo tratamos como cadena vacía.
			var loc = ubicacion ?? "";

			var lista = await _auroraSgaContext.VUbicacionesAlergenos
				.Where(x =>
					x.CodigoEmpresa == codigoEmpresa &&
					x.CodigoAlmacen == codigoAlmacen &&
					x.Ubicacion == loc           // ahora "" coincide con "" en la vista
				)
				.Select(x => new AlergenoDto
				{
					Codigo = x.VCodigoAlergeno,
					Descripcion = x.VDescripcionAlergeno
				})
				.Distinct()
				.ToListAsync();

			return Ok(lista);
		}


		/// <summary>
		/// GET api/ubicaciones/alergenos/permitidos
		/// Devuelve la lista de Códigos de Alergeno permitidos en una ubicación.
		/// </summary>
		[HttpGet("alergenos/permitidos")]
		public async Task<IActionResult> GetAlergenosPermitidos(
		[FromQuery] short codigoEmpresa,
		[FromQuery] string codigoAlmacen,
		[FromQuery] string? ubicacion)
		{
			var loc = ubicacion ?? "";

			var lista = await _auroraSgaContext.Ubicaciones_AlergenosPermitidos
				.Where(up => up.CodigoEmpresa == codigoEmpresa
						  && up.CodigoAlmacen  == codigoAlmacen
						  && up.Ubicacion      == loc)
				.Join(
					_auroraSgaContext.AlergenoMaestros,
					up => up.VCodigoAlergeno,
					am => am.VCodigoAlergeno,
					(up, am) => new AlergenoDto {
						Codigo      = am.VCodigoAlergeno,
						Descripcion = am.VDescripcionAlergeno
					}
				)
				.Distinct()
				.ToListAsync();


			return Ok(lista);
		}

		[HttpPost]
		public async Task<IActionResult> CrearUbicacionDetallada(
		[FromBody] CrearUbicacionDetalladaDto dto)
		{
			// 1. Validar mínimos
			if (string.IsNullOrWhiteSpace(dto.CodigoAlmacen)
			 || string.IsNullOrWhiteSpace(dto.CodigoUbicacion))
			{
				return BadRequest("Almacén y código de ubicación obligatorios.");
			}

			// 2. Chequear no duplicar
			var existe = await _auroraSgaContext.Ubicaciones
			   .AnyAsync(u =>
					u.CodigoEmpresa == dto.CodigoEmpresa &&
					u.CodigoAlmacen == dto.CodigoAlmacen &&
					u.CodigoUbicacion == dto.CodigoUbicacion);
			if (existe) return Conflict("La ubicación ya existe.");

			// 3. Crear entidad Ubicaciones
			var u = new SGA_Api.Models.Ubicacion.Ubicacion
			{
				CodigoEmpresa = dto.CodigoEmpresa,
				CodigoAlmacen = dto.CodigoAlmacen,
				CodigoUbicacion = dto.CodigoUbicacion,
				DescripcionUbicacion = dto.DescripcionUbicacion,
				Pasillo = dto.Pasillo,
				Estanteria = dto.Estanteria,
				Altura = dto.Altura,
				Posicion = dto.Posicion,
				Obsoleta = 0,
				Orden = dto.Orden,
				Peso = dto.Peso,
				DimensionX = dto.DimensionX,
				DimensionY = dto.DimensionY,
				DimensionZ = dto.DimensionZ,
				Angulo = dto.Angulo
			};
			_auroraSgaContext.Ubicaciones.Add(u);

			// 4. Crear configuración
			var cfg = new UbicacionesConfiguracion
			{
				CodigoEmpresa = dto.CodigoEmpresa,
				CodigoAlmacen = dto.CodigoAlmacen,
				Ubicacion = dto.CodigoUbicacion,
				TemperaturaMin = dto.TemperaturaMin,
				TemperaturaMax = dto.TemperaturaMax,
				TipoPaletPermitido = dto.TipoPaletPermitido,
				Habilitada = dto.Habilitada,
				TipoUbicacionId = dto.TipoUbicacionId
			};
			_auroraSgaContext.Ubicaciones_Configuracion.Add(cfg);

			await _auroraSgaContext.SaveChangesAsync();

			// 5) Insertar los alérgenos permitidos
			foreach (var codAler in dto.AlergenosPermitidos)
			{
				_auroraSgaContext.Ubicaciones_AlergenosPermitidos.Add(new UbicacionesAlergenosPermitidos
				{
					CodigoEmpresa = dto.CodigoEmpresa,
					CodigoAlmacen = dto.CodigoAlmacen,
					Ubicacion = dto.CodigoUbicacion,
					VCodigoAlergeno = codAler
				});
			}
			await _auroraSgaContext.SaveChangesAsync();


			return CreatedAtAction(
				nameof(GetUbicacionesBasico),
				new { dto.CodigoEmpresa, dto.CodigoAlmacen },
				dto);
		}

		[HttpGet("{codigoEmpresa}/{codigoAlmacen}/{codigoUbicacion}")]
		public async Task<ActionResult<UbicacionDetalladaDto>> GetUbicacionDetallada(
		short codigoEmpresa,
		string codigoAlmacen,
		string codigoUbicacion)
		{
			var dto = await _auroraSgaContext
				.Set<UbicacionDetallada>()   // entidad que mapea la vista vUbicacionesDetalladas
				.Where(x =>
					x.CodigoEmpresa == codigoEmpresa &&
					x.CodigoAlmacen == codigoAlmacen &&
					x.Ubicacion == codigoUbicacion)
				.Select(x => new UbicacionDetalladaDto
				{
					CodigoEmpresa = x.CodigoEmpresa,
					CodigoAlmacen = x.CodigoAlmacen,
					CodigoUbicacion = x.Ubicacion,
					DescripcionUbicacion = x.DescripcionUbicacion,
					Pasillo = x.Pasillo,
					Estanteria = x.Estanteria,
					Altura = x.Altura,
					Posicion = x.Posicion,
					Orden = x.Orden,
					TemperaturaMin = x.TemperaturaMin,
					TemperaturaMax = x.TemperaturaMax,
					TipoPaletPermitido = x.TipoPaletPermitido,
					Habilitada = x.Habilitada,
					TipoUbicacionId = x.TipoUbicacionId,
					TipoUbicacionDescripcion = x.TipoUbicacionDescripcion
				})
				.FirstOrDefaultAsync();

			if (dto == null)
				return NotFound();

			return Ok(dto);
		}

		[HttpPut("{codigoEmpresa}/{codigoAlmacen}/{codigoUbicacion}")]
		public async Task<IActionResult> ActualizarUbicacionDetallada(
		short codigoEmpresa,
		string codigoAlmacen,
		string codigoUbicacion,
		[FromBody] CrearUbicacionDetalladaDto dto)
		{
			// 1. Validaciones básicas
			if (dto == null)
				return BadRequest("El cuerpo de la solicitud no puede ser vacío.");

			if (dto.CodigoEmpresa != codigoEmpresa
			 || !string.Equals(dto.CodigoAlmacen, codigoAlmacen, StringComparison.Ordinal)
			 || !string.Equals(dto.CodigoUbicacion, codigoUbicacion, StringComparison.Ordinal))
			{
				return BadRequest("Los parámetros de ruta y el DTO no coinciden.");
			}

			// 2. Buscar la entidad Ubicacion
			var entidad = await _auroraSgaContext.Ubicaciones
				.FirstOrDefaultAsync(u =>
					u.CodigoEmpresa == codigoEmpresa &&
					u.CodigoAlmacen == codigoAlmacen &&
					u.CodigoUbicacion == codigoUbicacion);
			if (entidad == null)
				return NotFound();

			// 3. Actualizar campos físicos
			entidad.DescripcionUbicacion = dto.DescripcionUbicacion;
			entidad.Pasillo = dto.Pasillo;
			entidad.Estanteria = dto.Estanteria;
			entidad.Altura = dto.Altura;
			entidad.Posicion = dto.Posicion;
			entidad.Orden = dto.Orden;
			entidad.Peso = dto.Peso;
			entidad.DimensionX = dto.DimensionX;
			entidad.DimensionY = dto.DimensionY;
			entidad.DimensionZ = dto.DimensionZ;
			entidad.Angulo = dto.Angulo;
			entidad.Orden = dto.Orden;

			// 4. Actualizar configuración si existe
			var cfg = await _auroraSgaContext.Ubicaciones_Configuracion
				.FirstOrDefaultAsync(c =>
					c.CodigoEmpresa == codigoEmpresa &&
					c.CodigoAlmacen == codigoAlmacen &&
					c.Ubicacion == codigoUbicacion);
			if (cfg != null)
			{
				cfg.TemperaturaMin = dto.TemperaturaMin;
				cfg.TemperaturaMax = dto.TemperaturaMax;
				cfg.TipoPaletPermitido = dto.TipoPaletPermitido;
				cfg.Habilitada = dto.Habilitada;
				cfg.TipoUbicacionId = dto.TipoUbicacionId;
				
			}

			// 5. Reemplazar alérgenos permitidos
			var existentes = _auroraSgaContext.Ubicaciones_AlergenosPermitidos
				.Where(a =>
					a.CodigoEmpresa == codigoEmpresa &&
					a.CodigoAlmacen == codigoAlmacen &&
					a.Ubicacion == codigoUbicacion);
			_auroraSgaContext.Ubicaciones_AlergenosPermitidos.RemoveRange(existentes);

			if (dto.AlergenosPermitidos != null)
			{
				foreach (var codAler in dto.AlergenosPermitidos)
				{
					_auroraSgaContext.Ubicaciones_AlergenosPermitidos.Add(new UbicacionesAlergenosPermitidos
					{
						CodigoEmpresa = codigoEmpresa,
						CodigoAlmacen = codigoAlmacen,
						Ubicacion = codigoUbicacion,
						VCodigoAlergeno = codAler
					});
				}
			}

			// 6. Guardar cambios
			await _auroraSgaContext.SaveChangesAsync();
			return NoContent();
		}


		/// <summary>
		/// GET api/ubicaciones/tipos
		/// Devuelve la lista completa de tipos de ubicación.
		/// </summary>
		[HttpGet("tipos")]
		public async Task<ActionResult<List<TipoUbicacionDto>>> GetTiposUbicacion()
		{
			try
			{
				var lista = await _auroraSgaContext.TipoUbicaciones
					.Select(t => new TipoUbicacionDto
					{
						TipoUbicacionId = t.TipoUbicacionId,
						Descripcion = t.Descripcion
					})
					.ToListAsync();

				return Ok(lista);
			}
			catch (Exception ex)
			{
				// Para depurar en desarrollo
				return Problem(detail: ex.Message, statusCode: 500);
			}
		}



	}

}
