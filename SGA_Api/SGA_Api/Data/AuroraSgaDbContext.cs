using Microsoft.EntityFrameworkCore;
using SGA_Api.Models.Impresion;
using SGA_Api.Models.Registro;
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
		}
	}
}