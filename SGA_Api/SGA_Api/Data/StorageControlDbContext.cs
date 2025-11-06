using Microsoft.EntityFrameworkCore;
using SGA_Api.Models.Almacen;
using SGA_Api.Models.Stock;

namespace SGA_Api.Data
{
    public class StorageControlDbContext : DbContext
    {
        public StorageControlDbContext(DbContextOptions<StorageControlDbContext> options)
            : base(options)
        {
        }
        public DbSet<AcumuladoStockUbicacion> AcumuladoStockUbicacion { get; set; }
		public DbSet<Ubicaciones> Ubicaciones { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AcumuladoStockUbicacion>()
                .HasNoKey()
                .Property(e => e.UnidadSaldo)
                .HasPrecision(18, 6); // 🔷 CORREGIDO: Forzar precisión de 6 decimales para preservar valores exactos

			modelBuilder
		  .Entity<Ubicaciones>()
		  .HasNoKey()
		  .ToView("Ubicaciones");


		}
    }
}
