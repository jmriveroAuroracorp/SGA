using Microsoft.EntityFrameworkCore;
using SGA_Api.Models.Almacen;
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
        public DbSet<AplicacionSGA> AplicacionesSGA { get; set; }
        public DbSet<Periodo> Periodos { get; set; }
		public DbSet<Empresa> Empresas { get; set; } = default!;
		public DbSet<Articulo> Articulos { get; set; }
        public DbSet<VAuxiliarEmpleado> VAuxiliarEmpleados { get; set; }
        
        // Vista para los alérgenos de las etiquetas
		public DbSet<VisArticulo> VisArticulos { get; set; } = null!;
		public DbSet<AcumuladoStock> AcumuladoStock { get; set; } = null!;


		protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Tabla operarios
            modelBuilder.Entity<Operario>()
                .ToTable("operarios")
                .HasKey(o => o.Id);

            // Configurar para evitar OUTPUT clause en operarios (tabla con triggers)
            modelBuilder.Entity<Operario>()
                .Property(o => o.MRH_LimiteInventarioEuros)
                .HasAnnotation("SqlServer:UseSqlOutputClause", false);
            
            modelBuilder.Entity<Operario>()
                .Property(o => o.MRH_LimiteInventarioUnidades)
                .HasAnnotation("SqlServer:UseSqlOutputClause", false);

            // Tabla MRH_accesosOperariosSGA
            modelBuilder.Entity<AccesoOperario>()
                .ToTable("MRH_accesosOperariosSGA")
                .HasKey(a => new { a.CodigoEmpresa, a.Operario, a.MRH_CodigoAplicacion }); // Clave primaria compuesta

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
                        modelBuilder.Entity<OperarioEmpresa>(entity =>
            {
                entity.ToTable("MRH_SGAOperariosEmpresas");
                entity.HasKey(e => new { e.CodigoEmpresa, e.Operario, e.EmpresaOrigen });
         
                entity.Property(e => e.CodigoEmpresa).HasColumnName("CodigoEmpresa");
                entity.Property(e => e.Operario).HasColumnName("Operario");
                entity.Property(e => e.EmpresaOrigen).HasColumnName("EmpresaOrigen");
                entity.Property(e => e.Empresa).HasColumnName("Empresa").HasMaxLength(45);
            });

            //Almacenes
			modelBuilder.Entity<Almacenes>()
		   .HasNoKey()
		   .ToTable("Almacenes");   


			modelBuilder.Entity<Periodo>().HasNoKey();

			modelBuilder.Entity<Empresa>(e =>
			{
				e.ToTable("EMPRESAS");                
				e.HasKey(x => x.CodigoEmpresa);

				e.Property(x => x.CodigoEmpresa)
				 .HasColumnName("CodigoEmpresa");      

				e.Property(x => x.EmpresaNombre)
				 .HasColumnName("Empresa");
			});

			modelBuilder.Entity<AplicacionSGA>(e =>
			{
				e.ToTable("MRH_AplicacionesSGA");
				e.HasKey(x => new { x.CodigoEmpresa, x.MRH_CodigoAplicacion });

				e.Property(x => x.CodigoEmpresa)
				 .HasColumnName("CodigoEmpresa");

				e.Property(x => x.MRH_CodigoAplicacion)
				 .HasColumnName("MRH_CodigoAplicacion");

				e.Property(x => x.Descripcion)
				 .HasColumnName("Descripcion");
			});

			modelBuilder
	          .Entity<Ubicaciones>()
	          .HasNoKey()
	          .ToView("Ubicaciones");

			modelBuilder.Entity<VisArticulo>(eb =>
			{
				eb.HasNoKey();
				eb.ToView("Vis_Articulos");

				eb.Property(v => v.CodigoEmpresa)
				  .HasColumnName("CodigoEmpresa");

				eb.Property(v => v.CodigoArticulo)
				  .HasColumnName("CodigoArticulo");

				eb.Property(v => v.DescripcionArticulo)
				  .HasColumnName("DescripcionArticulo");

				eb.Property(v => v.CodigoAlternativo)
				  .HasColumnName("CodigoAlternativo");

				eb.Property(v => v.VNEWAlergenos)         // coincide con tu POCO
				  .HasColumnName("VNEWALERGENOS");        // exacto nombre de la columna
			});

			// Configuración para AcumuladoStock
			modelBuilder.Entity<AcumuladoStock>()
				.ToTable("AcumuladoStock")
				.HasNoKey(); // Vista sin clave primaria definida

			// Configuración para VAuxiliarEmpleado
			modelBuilder.Entity<VAuxiliarEmpleado>()
				.ToTable("VAuxiliarEmpleado")
				.HasKey(ve => new { ve.CodigoEmpresa, ve.CodigoEmpleado });

		}
	}
}