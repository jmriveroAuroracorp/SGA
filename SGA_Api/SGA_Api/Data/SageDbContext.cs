using Microsoft.EntityFrameworkCore;
using SGA_Api.Models.Almacen;
using SGA_Api.Models.Login;
using SGA_Api.Models.Pesaje;
using SGA_Api.Models.Stock;
using SGA_Api.Models.Notificaciones;

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
		public DbSet<MovimientoStock> MovimientoStock { get; set; }
		
		// Entidades de Notificaciones MRH
		public DbSet<MrhTipoNotificacion> MrhTipoNotificaciones { get; set; }
		public DbSet<MrhNotificacion> MrhNotificaciones { get; set; }


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

			// Configuración para MovimientoStock
			modelBuilder.Entity<MovimientoStock>(entity =>
			{
				entity.ToTable("MovimientoStock");
				entity.HasKey(e => new { e.CodigoEmpresa, e.Ejercicio, e.Periodo, e.Fecha, e.FechaRegistro, e.Serie, e.Documento, e.MovPosicion });
				
				// Configurar precisión decimal
				entity.Property(e => e.Unidades).HasColumnType("decimal(28,10)");
				entity.Property(e => e.Precio).HasColumnType("decimal(28,10)");
				entity.Property(e => e.Importe).HasColumnType("decimal(28,10)");
				entity.Property(e => e.Unidades2_).HasColumnType("decimal(28,10)");
				entity.Property(e => e.FactorConversion_).HasColumnType("decimal(28,10)");
				entity.Property(e => e.ImporteCoste).HasColumnType("decimal(28,10)");
				entity.Property(e => e.UnidadEntrada).HasColumnType("decimal(28,10)");
				entity.Property(e => e.UnidadStock).HasColumnType("decimal(28,10)");
				entity.Property(e => e.PrecioMedio).HasColumnType("decimal(28,10)");
			});

			// Configuración para MRH_TiposNotificacione
			// Nota: La clave primaria real es solo MRH_TipoNotificacion, pero CodigoEmpresa se obtiene del JOIN
			modelBuilder.Entity<MrhTipoNotificacion>(entity =>
			{
				entity.ToTable("MRH_TiposNotificacione", "dbo");
				entity.HasKey(e => e.TipoNotificacion);
				
				entity.Property(e => e.CodigoEmpresa).HasColumnName("CodigoEmpresa");
				entity.Property(e => e.TipoNotificacion).HasColumnName("MRH_TipoNotificacion");
				entity.Property(e => e.Email).HasColumnName("Email").HasMaxLength(200);
				entity.Property(e => e.Telefono).HasColumnName("Telefono").HasMaxLength(50);
				entity.Property(e => e.TelegramID).HasColumnName("TelegramID").HasMaxLength(100);
				entity.Property(e => e.Descripcion).HasColumnName("Descripcion").HasMaxLength(200);
				entity.Property(e => e.CanalTeams).HasColumnName("CanalTeams").HasMaxLength(500);
				entity.Property(e => e.Departamento).HasColumnName("Departamento").HasMaxLength(100);
			});

			// Configuración para MRH_Notificaciones
			modelBuilder.Entity<MrhNotificacion>(entity =>
			{
				entity.ToTable("MRH_Notificaciones");
				entity.HasKey(e => new { e.CodigoEmpresa, e.MovPosicion });
				
				entity.Property(e => e.CodigoEmpresa).HasColumnName("CodigoEmpresa");
				entity.Property(e => e.MovPosicion).HasColumnName("MovPosicion");
				entity.Property(e => e.OrigenNotificacion).HasColumnName("MRH_OrigenNotificacion").HasMaxLength(100);
				entity.Property(e => e.Interno).HasColumnName("MRH_Interno");
				entity.Property(e => e.FechaRegistro).HasColumnName("FechaRegistro");
				entity.Property(e => e.FechaConfirmadaEnvio).HasColumnName("FechaConfirmadaEnvio");
				entity.Property(e => e.EnviaEmail).HasColumnName("EnviaEmail");
				entity.Property(e => e.EnviaApp).HasColumnName("EnviaApp");
				entity.Property(e => e.Leido).HasColumnName("Leido");
				entity.Property(e => e.Email).HasColumnName("Email").HasMaxLength(200);
				entity.Property(e => e.Nombre).HasColumnName("Nombre").HasMaxLength(200);
				entity.Property(e => e.Asunto).HasColumnName("Asunto").HasMaxLength(500);
				entity.Property(e => e.Mensaje).HasColumnName("Mensaje");
				entity.Property(e => e.ErrorEnvio).HasColumnName("ErrorEnvio").HasMaxLength(500);
				entity.Property(e => e.EmailEmisor).HasColumnName("EmailEmisor").HasMaxLength(200);
				entity.Property(e => e.SmtpEmisor).HasColumnName("SmtpEmisor").HasMaxLength(200);
				entity.Property(e => e.PassEmisor).HasColumnName("PassEmisor").HasMaxLength(200);
				entity.Property(e => e.FirmaEmisor).HasColumnName("FirmaEmisor");
				entity.Property(e => e.TelegramID).HasColumnName("TelegramID").HasMaxLength(100);
				entity.Property(e => e.FechaConfirmadaEnvioT).HasColumnName("FechaConfirmadaEnvioT");
				entity.Property(e => e.CanalTeams).HasColumnName("CanalTeams").HasMaxLength(500);
			});

		}
	}
}