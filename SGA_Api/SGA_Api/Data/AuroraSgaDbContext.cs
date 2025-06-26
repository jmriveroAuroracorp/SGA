using Microsoft.EntityFrameworkCore;
using SGA_Api.Models.Alergenos;
using SGA_Api.Models.Impresion;
using SGA_Api.Models.Palet;
using SGA_Api.Models.Registro;
using SGA_Api.Models.Ubicacion;
using SGA_Api.Models.UsuarioConf;

namespace SGA_Api.Data
{
	public class AuroraSgaDbContext : DbContext
	{
		public AuroraSgaDbContext(DbContextOptions<AuroraSgaDbContext> options) : base(options) { }

		public DbSet<Dispositivo> Dispositivos { get; set; }
		public DbSet<LogEvento> LogEventos { get; set; }
		public DbSet<LogImpresion> LogImpresiones { get; set; }
		public DbSet<Usuario> Usuarios { get; set; }
		public DbSet<Impresora> Impresoras { get; set; }
		public DbSet<UbicacionDetallada> vUbicacionesDetalladas { get; set; }
		public DbSet<Ubicacion> Ubicaciones { get; set; }
		public DbSet<UbicacionesConfiguracion> Ubicaciones_Configuracion { get; set; }
		public DbSet<UbicacionesAlergenosPermitidos> Ubicaciones_AlergenosPermitidos { get; set; }
		public DbSet<VUbicacionesAlergenos> VUbicacionesAlergenos { get; set; }
		public DbSet<AlergenoMaestroDto> vAlergenos { get; set; }
		public DbSet<AlergenoMaestro> AlergenoMaestros { get; set; }
		public DbSet<TipoUbicacion> TipoUbicaciones { get; set; }
		public DbSet<TipoPalet> TipoPalets { get; set; }
		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			// --- Dispositivo ---
			modelBuilder.Entity<Dispositivo>()
				.ToTable("dispositivos")
				.HasKey(d => d.Id);

			modelBuilder.Entity<Dispositivo>()
				.Property(d => d.Id)
				.HasColumnName("id")
				.IsRequired()
				.HasMaxLength(50);

			modelBuilder.Entity<Dispositivo>()
				.Property(d => d.Tipo)
				.HasColumnName("tipo")
				.HasMaxLength(100);

			modelBuilder.Entity<Dispositivo>()
				.Property(d => d.Activo)
				.HasColumnName("activo");

			// --- LogEvento ---
			modelBuilder.Entity<LogEvento>()
				.ToTable("log_eventos")
				.HasKey(e => e.Id);

			modelBuilder.Entity<LogEvento>()
				.Property(e => e.Detalle)
				.HasColumnType("text");

			// RELACIÓN: LogEvento → Dispositivo (obligatoria)
			modelBuilder.Entity<LogEvento>()
				.HasOne(e => e.Dispositivo)
				.WithMany(d => d.LogEventos)
				.HasForeignKey(e => e.IdDispositivo)
				.IsRequired()
				.OnDelete(DeleteBehavior.Restrict);

			// PrintCenter
			modelBuilder.Entity<LogImpresion>().ToTable("log_impresiones");

			//Tabla Usuarios. Configuración por defecto
			modelBuilder.Entity<Usuario>()
			  .ToTable("usuarios")
			  .HasKey(u => u.IdUsuario);

			modelBuilder.Entity<Usuario>()
				.Property(u => u.IdUsuario).HasColumnName("IdUsuario");
			modelBuilder.Entity<Usuario>()
				.Property(u => u.IdEmpresa).HasColumnName("IdEmpresa");
			modelBuilder.Entity<Usuario>()
				.Property(u => u.Impresora).HasColumnName("Impresora");
			modelBuilder.Entity<Usuario>()
				.Property(u => u.Etiqueta).HasColumnName("Etiqueta");

			modelBuilder.Entity<Impresora>(ent =>
			{
				ent.ToTable("impresoras");
				ent.HasKey(p => p.Id);
				ent.Property(p => p.Id)
				   .HasColumnName("id")
				   .IsRequired();
				ent.Property(p => p.Nombre)
				   .HasColumnName("nombre")
				   .IsRequired()
				   .HasMaxLength(200);
			});

			// Mapea Ubicacion ↔ dbo.Ubicaciones
			modelBuilder.Entity<Ubicacion>(ent =>
			{
				ent.ToTable("Ubicaciones");
				ent.HasKey(u => new { u.CodigoEmpresa, u.CodigoAlmacen, u.CodigoUbicacion });

				// Aquí indicamos que la prop CodigoUbicacion usa la columna "Ubicacion"
				ent.Property(u => u.CodigoUbicacion)
				   .HasColumnName("Ubicacion")
				   .HasMaxLength(50); // pon el tamaño real si lo sabes

				// Mapea el resto por convención o explícitamente:
				ent.Property(u => u.DescripcionUbicacion)
				   .HasColumnName("DescripcionUbicacion");
				ent.Property(u => u.Obsoleta)
				   .HasColumnName("Obsoleta");
				// … y así con cualquier otra que no coincida por nombre

			});

			// Mapea Ubicaciones_Configuracion ↔ dbo.Ubicaciones_Configuracion
			modelBuilder.Entity<UbicacionesConfiguracion>(ent =>
			{
				ent.ToTable("Ubicaciones_Configuracion");
				ent.HasKey(c => new { c.CodigoEmpresa, c.CodigoAlmacen, c.Ubicacion });

				// Igual aquí: tu prop CodigoUbicacion va en la columna "Ubicacion"
				ent.Property(c => c.Ubicacion)
				   .HasColumnName("Ubicacion")
				   .HasMaxLength(50);

				// Las demás props normalmente bastan por convención
			});

			modelBuilder.Entity<TipoUbicacion>(ent =>
			{
				ent.ToTable("TipoUbicaciones");                // nombre real de la tabla
				ent.HasKey(t => t.TipoUbicacionId);            // PK
				ent.Property(t => t.TipoUbicacionId)
				   .HasColumnName("TipoUbicacionId");
				ent.Property(t => t.Descripcion)
				   .HasColumnName("Descripcion")
				   .HasMaxLength(100)
				   .IsRequired();
			});
			// mapeo tabla de permitidos
			modelBuilder.Entity<UbicacionesAlergenosPermitidos>(ent =>
			{
				ent.ToTable("Ubicaciones_AlergenosPermitidos");
				ent.HasKey(x => new { x.CodigoEmpresa, x.CodigoAlmacen, x.Ubicacion, x.VCodigoAlergeno });
				// por convención todas las props coinciduen con columnas
			});

			// mapeo vista de alérgenos presentes
			modelBuilder.Entity<VUbicacionesAlergenos>(ent =>
			{
				ent.HasNoKey();
				ent.ToView("vubicaciones_alergenos");
				// si la vista devuelve exactamente esas 4 columnas, no necesita más
			});

			modelBuilder.Entity<AlergenoMaestroDto>(ent =>
			{
				ent.HasNoKey();
				ent.ToView("vAlergenos");
				// Las propiedades de AlergenoMaestroDto coinciden por
				// convención con las columnas CodigoEmpresa, Codigo, Descripcion
			});

			modelBuilder.Entity<AlergenoMaestro>(ent =>
			{
				ent.HasKey(a => new { a.CodigoEmpresa, a.VCodigoAlergeno });
				ent.ToTable("vAlergenos", schema: "dbo");  // Ajusta el esquema si hace falta
				ent.Property(a => a.CodigoEmpresa)
				   .HasColumnName("CodigoEmpresa");
				ent.Property(a => a.VCodigoAlergeno)
				   .HasColumnName("Codigo");
				ent.Property(a => a.VDescripcionAlergeno)
				   .HasColumnName("Descripcion");
			});

			// Mapeo de la vista ya existente:
			modelBuilder
  .Entity<UbicacionDetallada>(eb =>
  {
	  eb.HasNoKey();
	  eb.ToView("vUbicacionesDetalladas");
	  eb.Property(v => v.Orden).HasColumnName("Orden");
  });

		}
	}
}