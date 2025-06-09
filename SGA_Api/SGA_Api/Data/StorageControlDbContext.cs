using Microsoft.EntityFrameworkCore;
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AcumuladoStockUbicacion>().HasNoKey();
        }
    }
}
