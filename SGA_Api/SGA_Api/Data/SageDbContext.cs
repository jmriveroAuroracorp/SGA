using Microsoft.EntityFrameworkCore;
using SGA_Api.Models.Login;
using SGA_Api.Models.Pesaje;

namespace SGA_Api.Data
{
    public class SageDbContext : DbContext
    {
        public SageDbContext(DbContextOptions<SageDbContext> options)
            : base(options)
        {
        }

        public DbSet<Operario> Operarios { get; set; }
        public DbSet<AccesoOperario> AccesosOperarios { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Tabla operarios
            modelBuilder.Entity<Operario>()
                .ToTable("operarios")
                .HasKey(o => o.Id);

            // Tabla MRH_accesosOperariosSGA
            modelBuilder.Entity<AccesoOperario>()
                .ToTable("MRH_accesosOperariosSGA")
                .HasKey(a => a.Operario); // Clave primaria

            // Relación (opcional, por si en el futuro quieres navegación)
            modelBuilder.Entity<AccesoOperario>()
                .HasOne<Operario>()
                .WithMany()
                .HasForeignKey(a => a.Operario);
        }
    }
}