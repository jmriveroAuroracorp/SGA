using Microsoft.EntityFrameworkCore;
using SGA_Api.Models.Registro;

namespace SGA_Api.Data
{
    public class AuroraSgaDbContext : DbContext
    {
        public AuroraSgaDbContext(DbContextOptions<AuroraSgaDbContext> options) : base(options) { }

        public DbSet<Dispositivo> Dispositivos { get; set; }
        public DbSet<LogEvento> LogEventos { get; set; }

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

        }
    }
}