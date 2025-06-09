using Microsoft.EntityFrameworkCore;
using SGA_Api.Models.Login;
using SGA_Api.Models.Pesaje;
using SGA_Api.Models.Stock;

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
        public DbSet<OperarioAlmacen> OperariosAlmacenes { get; set; }
        public DbSet<OperarioEmpresa> OperariosEmpresas { get; set; }
        public DbSet<Almacenes> Almacenes { get; set; }
        public DbSet<Periodo> Periodos { get; set; }
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

                modelBuilder.Entity<OperarioAlmacen>()
                .ToTable("MRH_OperariosAlmacenes")
                .HasKey(o => new { o.CodigoEmpresa, o.Operario, o.CodigoAlmacen });

            modelBuilder.Entity<OperarioAlmacen>()
                .Property(o => o.CodigoEmpresa)
                .HasColumnName("CodigoEmpresa");

            modelBuilder.Entity<OperarioAlmacen>()
                .Property(o => o.Operario)
                .HasColumnName("Operario");

            modelBuilder.Entity<OperarioAlmacen>()
                .Property(o => o.CodigoAlmacen)
                .HasColumnName("CodigoAlmacen")
                .HasMaxLength(10); // o el tamaño real si lo sabes (ej. 5, 50...) 

            //Empresas Asignadas
            modelBuilder.Entity<OperarioEmpresa>()
            .ToTable("MRH_SGAOperariosEmpresas")
            .HasNoKey(); 

            modelBuilder.Entity<OperarioEmpresa>()
                .Property(e => e.Operario)
                .HasColumnName("Operario");

            modelBuilder.Entity<OperarioEmpresa>()
                .Property(e => e.Empresa)
                .HasColumnName("Empresa");

            modelBuilder.Entity<Almacenes>().HasNoKey();
            modelBuilder.Entity<Periodo>().HasNoKey();

        }
    }
}