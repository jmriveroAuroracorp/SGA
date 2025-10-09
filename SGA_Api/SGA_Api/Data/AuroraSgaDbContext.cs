using Microsoft.EntityFrameworkCore;
using SGA_Api.Models.Alergenos;
using SGA_Api.Models.Calidad;
using SGA_Api.Models.Impresion;
using SGA_Api.Models.Palet;
using SGA_Api.Models.Registro;
using SGA_Api.Models.Stock;
using SGA_Api.Models.Traspasos;
using SGA_Api.Models.Ubicacion;
using SGA_Api.Models.UsuarioConf;
using SGA_Api.Models.Inventario;
using SGA_Api.Models.Conteos;
using SGA_Api.Models.OrdenTraspaso;
using SGA_Api.Models.Notificaciones;
using SGA_Api.Models;
using SGA_Api.Models.Login;

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
		public DbSet<Palet> Palets { get; set; }
		public DbSet<TipoEstadoPalet> TipoEstadoPalet { get; set; }
		public DbSet<UsuarioConNombre> vUsuariosConNombre { get; set; }
		public DbSet<PaletLinea> PaletLineas { get; set; }
		public DbSet<TempPaletLinea> TempPaletLineas { get; set; }
		public DbSet<LogPalet> LogPalet { get; set; } = null!;
		public DbSet<StockDisponible> StockDisponible => Set<StockDisponible>();
		public DbSet<Traspaso> Traspasos { get; set; }
		public DbSet<EstadoTraspaso> TipoEstadosTraspaso { get; set; }
		public DbSet<InventarioCabecera> InventarioCabecera { get; set; }
		public DbSet<InventarioLineasTemp> InventarioLineasTemp { get; set; }
		public DbSet<InventarioLineas> InventarioLineas { get; set; }
		public DbSet<InventarioAjustes> InventarioAjustes { get; set; }
		public DbSet<InventarioAlmacenes> InventarioAlmacenes { get; set; }
		
		// Entidades de Conteos
		public DbSet<OrdenConteo> OrdenesConteo { get; set; }
		public DbSet<LecturaConteo> LecturasConteo { get; set; }
		public DbSet<ResultadoConteo> ResultadosConteo { get; set; }


		// Entidades de Órdenes de Traspaso
		public DbSet<OrdenTraspasoCabecera> OrdenTraspasoCabecera { get; set; }
		public DbSet<OrdenTraspasoLinea> OrdenTraspasoLinea { get; set; }

		// Entidades de Notificaciones
		public DbSet<Notificacion> Notificaciones { get; set; }
		public DbSet<NotificacionDestinatario> NotificacionesDestinatarios { get; set; }
		public DbSet<NotificacionLectura> NotificacionesLecturas { get; set; }

		// Configuraciones predefinidas
		public DbSet<ConfiguracionPredefinida> ConfiguracionesPredefinidas { get; set; }
		public DbSet<ConfiguracionPredefinidaPermiso> ConfiguracionesPredefinidasPermisos { get; set; }
		public DbSet<ConfiguracionPredefinidaEmpresa> ConfiguracionesPredefinidasEmpresas { get; set; }
		public DbSet<ConfiguracionPredefinidaAlmacen> ConfiguracionesPredefinidasAlmacenes { get; set; }
		public DbSet<OperarioConfiguracionAplicada> OperariosConfiguracionesAplicadas { get; set; }

		// Entidades de Calidad
		public DbSet<BloqueoCalidad> BloqueosCalidad { get; set; }


		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			// Configurar convenciones por defecto para evitar conflictos
			modelBuilder.HasDefaultSchema("dbo");

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
			modelBuilder.Entity<LogImpresion>(ent =>
			{
				ent.ToTable("log_impresiones");
				ent.Property(e => e.CodAlmacen).HasColumnName("codAlmacen");
				ent.Property(e => e.CodUbicacion).HasColumnName("codUbicacion");
				ent.Property(e => e.Altura).HasColumnName("altura");
				ent.Property(e => e.Estanteria).HasColumnName("estanteria");
				ent.Property(e => e.Pasillo).HasColumnName("pasillo");
				ent.Property(e => e.Posicion).HasColumnName("posicion");
			});

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
				.Property(u => u.IdRol).HasColumnName("IdRol");
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

			modelBuilder.Entity<TipoEstadoPalet>(eb =>
			{
				eb.ToTable("TipoEstadoPalet");
				eb.HasKey(e => e.CodigoEstado);
				eb.Property(e => e.Descripcion).IsRequired();
				eb.Property(e => e.Orden).IsRequired();
			});

			modelBuilder.Entity<Palet>(eb =>
			{
				eb.HasOne<TipoEstadoPalet>()
				  .WithMany(e => e.Palets)
				  .HasForeignKey(p => p.Estado)
				  .HasConstraintName("FK_Palets_TipoEstadoPalet");
			});

			modelBuilder.Entity<UsuarioConNombre>(ent =>
			{
				ent.HasNoKey();
				ent.ToView("vUsuariosConNombre");
			});

			modelBuilder.Entity<StockDisponible>()
				.ToView("vStockDisponible")
				.HasNoKey();

			modelBuilder.Entity<TempPaletLinea>(eb =>
			{
				eb.Property(e => e.Cantidad)
				  .HasColumnType("decimal(18,4)");
			});

            // Mapeo explícito de la entidad Traspaso
            modelBuilder.Entity<Traspaso>(ent =>
            {
                ent.ToTable("traspasos");
                ent.HasKey(t => t.Id);

                ent.Property(t => t.Id).HasColumnName("id");
                ent.Property(t => t.AlmacenOrigen).HasColumnName("AlmacenOrigen");
                ent.Property(t => t.AlmacenDestino).HasColumnName("AlmacenDestino");
                ent.Property(t => t.CodigoEstado).HasColumnName("CodigoEstado");
                ent.Property(t => t.FechaInicio).HasColumnName("FechaInicio");
                ent.Property(t => t.UsuarioInicioId).HasColumnName("UsuarioInicioId");
                ent.Property(t => t.PaletId).HasColumnName("PaletId");
                ent.Property(t => t.FechaFinalizacion).HasColumnName("FechaFinalizacion");
                ent.Property(t => t.UsuarioFinalizacionId).HasColumnName("UsuarioFinalizacionId");
                ent.Property(t => t.UbicacionDestino).HasColumnName("UbicacionDestino");
                ent.Property(t => t.UbicacionOrigen).HasColumnName("UbicacionOrigen");
                ent.Property(t => t.CodigoPalet).HasColumnName("CodigoPalet");
                ent.Property(t => t.CodigoArticulo).HasColumnName("CodigoArticulo");
                ent.Property(t => t.Cantidad).HasColumnName("Cantidad");
                ent.Property(t => t.TipoTraspaso).HasColumnName("TipoTraspaso");
                ent.Property(t => t.FechaCaducidad).HasColumnName("FechaCaducidad");
                ent.Property(t => t.Partida).HasColumnName("Partida");
                ent.Property(t => t.CodigoEmpresa).HasColumnName("CodigoEmpresa");
                ent.Property(t => t.MovPosicionOrigen).HasColumnName("MovPosicionOrigen");
                ent.Property(t => t.MovPosicionDestino).HasColumnName("MovPosicionDestino");
				ent.Property(t => t.Comentario).HasColumnName("Comentario");
				ent.Property(t => t.EstadoErp).HasColumnName("EstadoErp");
			});

            // Configuración de entidades de Inventario
            modelBuilder.Entity<InventarioCabecera>(ent =>
            {
                ent.ToTable("InventarioCabecera");
                ent.HasKey(i => i.IdInventario);
                
                ent.Property(i => i.IdInventario).HasColumnName("IdInventario");
                ent.Property(i => i.CodigoInventario).HasColumnName("CodigoInventario").HasMaxLength(30);
                ent.Property(i => i.CodigoEmpresa).HasColumnName("CodigoEmpresa").HasColumnType("SMALLINT");
                ent.Property(i => i.CodigoAlmacen).HasColumnName("CodigoAlmacen").HasMaxLength(10);
                ent.Property(i => i.RangoUbicaciones).HasColumnName("RangoUbicaciones").HasMaxLength(50);
                ent.Property(i => i.TipoInventario).HasColumnName("TipoInventario").HasMaxLength(10);
                ent.Property(i => i.Comentarios).HasColumnName("Comentarios").HasColumnType("NVARCHAR(500)");
                ent.Property(i => i.Estado).HasColumnName("Estado").HasMaxLength(20);
                ent.Property(i => i.UsuarioCreacionId).HasColumnName("UsuarioCreacionId");
                ent.Property(i => i.UsuarioProcesamientoId).HasColumnName("UsuarioProcesamientoId");
                ent.Property(i => i.FechaCreacion).HasColumnName("FechaCreacion").HasColumnType("DATETIME");
                ent.Property(i => i.FechaCierre).HasColumnName("FechaCierre").HasColumnType("DATETIME");

                // Índice único por empresa + código inventario
                ent.HasIndex(i => new { i.CodigoEmpresa, i.CodigoInventario })
                    .IsUnique()
                    .HasDatabaseName("UX_InventarioCabecera_Empresa_CodigoInventario");
            });

            modelBuilder.Entity<InventarioLineasTemp>(ent =>
            {
                ent.ToTable("InventarioLineasTemp");
                ent.HasKey(i => i.IdTemp);
                
                ent.Property(i => i.IdTemp).HasColumnName("IdTemp");
                ent.Property(i => i.IdInventario).HasColumnName("IdInventario");
                ent.Property(i => i.CodigoArticulo).HasColumnName("CodigoArticulo").HasMaxLength(30);
                ent.Property(i => i.CodigoUbicacion).HasColumnName("CodigoUbicacion").HasMaxLength(30);
                ent.Property(i => i.Partida).HasColumnName("Partida").HasMaxLength(50);
                ent.Property(i => i.FechaCaducidad).HasColumnName("FechaCaducidad").HasColumnType("DATETIME");
                ent.Property(i => i.CantidadContada).HasColumnName("CantidadContada").HasColumnType("DECIMAL(18,4)");
                ent.Property(i => i.StockActual).HasColumnName("StockActual").HasColumnType("DECIMAL(18,4)");
                ent.Property(i => i.UsuarioConteoId).HasColumnName("UsuarioConteoId");
                ent.Property(i => i.FechaConteo).HasColumnName("FechaConteo").HasColumnType("DATETIME");
                ent.Property(i => i.Observaciones).HasColumnName("Observaciones").HasColumnType("NVARCHAR(500)");
                ent.Property(i => i.Consolidado).HasColumnName("Consolidado");
                ent.Property(i => i.FechaConsolidacion).HasColumnName("FechaConsolidacion").HasColumnType("DATETIME");
                ent.Property(i => i.UsuarioConsolidacionId).HasColumnName("UsuarioConsolidacionId");

                ent.HasOne(i => i.Inventario)
                    .WithMany(ic => ic.LineasTemp)
                    .HasForeignKey(i => i.IdInventario)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<InventarioLineas>(ent =>
            {
                ent.ToTable("InventarioLineas");
                ent.HasKey(i => i.IdLinea);
                
                ent.Property(i => i.IdLinea).HasColumnName("IdLinea");
                ent.Property(i => i.IdInventario).HasColumnName("IdInventario");
                ent.Property(i => i.CodigoArticulo).HasColumnName("CodigoArticulo").HasMaxLength(30);
                ent.Property(i => i.CodigoUbicacion).HasColumnName("CodigoUbicacion").HasMaxLength(30);
                ent.Property(i => i.StockTeorico).HasColumnName("StockTeorico").HasColumnType("DECIMAL(18,4)");
                ent.Property(i => i.StockContado).HasColumnName("StockContado").HasColumnType("DECIMAL(18,4)");
                ent.Property(i => i.Estado).HasColumnName("Estado").HasMaxLength(20);
                ent.Property(i => i.UsuarioValidacionId).HasColumnName("UsuarioValidacionId");
                ent.Property(i => i.FechaValidacion).HasColumnName("FechaValidacion").HasColumnType("DATETIME");
                ent.Property(i => i.Observaciones).HasColumnName("Observaciones").HasColumnType("NVARCHAR(500)");

                ent.HasOne(i => i.Inventario)
                    .WithMany(ic => ic.Lineas)
                    .HasForeignKey(i => i.IdInventario)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<InventarioAjustes>(ent =>
            {
                ent.ToTable("InventarioAjustes");
                ent.HasKey(i => i.IdAjuste);
                
                ent.Property(i => i.IdAjuste).HasColumnName("IdAjuste");
                ent.Property(i => i.IdInventario).HasColumnName("IdInventario").IsRequired(false);
                ent.Property(i => i.CodigoArticulo).HasColumnName("CodigoArticulo").HasMaxLength(30);
                ent.Property(i => i.CodigoUbicacion).HasColumnName("CodigoUbicacion").HasMaxLength(30);
                ent.Property(i => i.Diferencia).HasColumnName("Diferencia").HasColumnType("DECIMAL(18,4)");
                // ent.Property(i => i.TipoAjuste).HasColumnName("TipoAjuste").HasMaxLength(10).IsRequired(); // Comentado temporalmente
                ent.Property(i => i.UsuarioId).HasColumnName("UsuarioId");
                ent.Property(i => i.Fecha).HasColumnName("Fecha").HasColumnType("DATETIME");
                ent.Property(i => i.IdConteo).HasColumnName("IdConteo");
                ent.Property(i => i.CodigoEmpresa).HasColumnName("CodigoEmpresa").HasColumnType("SMALLINT");
                ent.Property(i => i.CodigoAlmacen).HasColumnName("CodigoAlmacen").HasMaxLength(10);
                //ent.Property(i => i.Estado).HasColumnName("Estado").HasMaxLength(20).HasDefaultValue("PENDIENTE_ERP");
                ent.Property(i => i.Estado).HasColumnName("Estado").HasMaxLength(20);
                ent.Property(i => i.EstadoErp).HasColumnName("EstadoErp").HasMaxLength(500).IsRequired(false);
                ent.Property(i => i.FechaCaducidad).HasColumnName("FechaCaducidad").HasColumnType("DATETIME").IsRequired(false);
                ent.Property(i => i.Partida).HasColumnName("Partida").HasMaxLength(50).IsRequired(false);
                ent.HasOne(i => i.Inventario)
                    .WithMany(ic => ic.Ajustes)
                    .HasForeignKey(i => i.IdInventario)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<InventarioAlmacenes>(ent =>
            {
                ent.ToTable("InventarioAlmacenes");
                ent.HasKey(i => i.Id);
                
                ent.Property(i => i.Id).HasColumnName("Id");
                ent.Property(i => i.IdInventario).HasColumnName("IdInventario");
                ent.Property(i => i.CodigoAlmacen).HasColumnName("CodigoAlmacen").HasMaxLength(10);
                ent.Property(i => i.CodigoEmpresa).HasColumnName("CodigoEmpresa").HasColumnType("SMALLINT");
                ent.Property(i => i.FechaCreacion).HasColumnName("FechaCreacion").HasColumnType("DATETIME");

                // Relación con InventarioCabecera
                ent.HasOne(i => i.Inventario)
                    .WithMany(ic => ic.Almacenes)
                    .HasForeignKey(i => i.IdInventario)
                    .OnDelete(DeleteBehavior.Cascade);

                // Índice único para evitar duplicados
                ent.HasIndex(i => new { i.IdInventario, i.CodigoAlmacen })
                    .IsUnique()
                    .HasDatabaseName("UX_InventarioAlmacenes_Inventario_Almacen");
            });

            // Configuración de entidades de Conteos
            // OrdenConteo
            modelBuilder.Entity<OrdenConteo>(entity =>
            {
                entity.ToTable("OrdenConteo");
                entity.HasKey(e => e.GuidID);
                
                // Configuración explícita para evitar convenciones por defecto
                entity.Property(e => e.GuidID).HasColumnName("GuidID").HasDefaultValueSql("NEWID()");
                entity.Property(e => e.CodigoEmpresa).HasColumnName("CodigoEmpresa").HasColumnType("int").HasDefaultValue(1);
                entity.Property(e => e.Titulo).HasColumnName("Titulo").HasMaxLength(120).IsRequired();
                entity.Property(e => e.Visibilidad).HasColumnName("Visibilidad").HasMaxLength(10).IsRequired();
                entity.Property(e => e.ModoGeneracion).HasColumnName("ModoGeneracion").HasMaxLength(10).IsRequired();
                entity.Property(e => e.Alcance).HasColumnName("Alcance").HasMaxLength(20).IsRequired();
                entity.Property(e => e.FiltrosJson).HasColumnName("FiltrosJson");
                entity.Property(e => e.FechaPlan).HasColumnName("FechaPlan");
                entity.Property(e => e.FechaEjecucion).HasColumnName("FechaEjecucion");
                entity.Property(e => e.SupervisorCodigo).HasColumnName("SupervisorCodigo").HasMaxLength(50);
                entity.Property(e => e.CreadoPorCodigo).HasColumnName("CreadoPorCodigo").HasMaxLength(50).IsRequired();
                entity.Property(e => e.Estado).HasColumnName("Estado").HasMaxLength(20).HasDefaultValue("PLANIFICADO");
                entity.Property(e => e.Prioridad).HasColumnName("Prioridad").HasColumnType("tinyint").HasDefaultValue((byte)3);
                entity.Property(e => e.FechaCreacion).HasColumnName("FechaCreacion").HasDefaultValueSql("sysdatetime()");
                entity.Property(e => e.CodigoOperario).HasColumnName("CodigoOperario").HasMaxLength(50);
                entity.Property(e => e.CodigoAlmacen).HasColumnName("CodigoAlmacen").HasMaxLength(10);
                entity.Property(e => e.CodigoUbicacion).HasColumnName("CodigoUbicacion").HasMaxLength(30);
                entity.Property(e => e.CodigoArticulo).HasColumnName("CodigoArticulo").HasMaxLength(30);
                entity.Property(e => e.DescripcionArticulo).HasColumnName("DescripcionArticulo").HasMaxLength(300);
                entity.Property(e => e.LotePartida).HasColumnName("LotePartida").HasMaxLength(40);
                entity.Property(e => e.CantidadTeorica).HasColumnName("CantidadTeorica").HasColumnType("decimal(18,4)");
                entity.Property(e => e.Comentario).HasColumnName("Comentario").HasMaxLength(500);
                entity.Property(e => e.FechaAsignacion).HasColumnName("FechaAsignacion");
                entity.Property(e => e.FechaInicio).HasColumnName("FechaInicio");
                entity.Property(e => e.FechaCierre).HasColumnName("FechaCierre");
            });

            // LecturaConteo
            modelBuilder.Entity<LecturaConteo>(entity =>
            {
                entity.ToTable("LecturaConteo");
                entity.HasKey(e => e.GuidID);
                
                // Configuración explícita para evitar convenciones por defecto
                entity.Property(e => e.GuidID).HasColumnName("GuidID").HasDefaultValueSql("NEWID()");
                entity.Property(e => e.OrdenGuid).HasColumnName("OrdenGuid");
                entity.Property(e => e.CodigoAlmacen).HasColumnName("CodigoAlmacen").HasMaxLength(10).IsRequired();
                entity.Property(e => e.CodigoUbicacion).HasColumnName("CodigoUbicacion").HasMaxLength(30);
                entity.Property(e => e.CodigoArticulo).HasColumnName("CodigoArticulo").HasMaxLength(30);
                entity.Property(e => e.DescripcionArticulo).HasColumnName("DescripcionArticulo").HasMaxLength(100);
                entity.Property(e => e.LotePartida).HasColumnName("LotePartida").HasMaxLength(40);
                entity.Property(e => e.CantidadContada).HasColumnName("CantidadContada").HasColumnType("decimal(18,4)");
                entity.Property(e => e.CantidadStock).HasColumnName("CantidadStock").HasColumnType("decimal(18,4)");
                entity.Property(e => e.UsuarioCodigo).HasColumnName("UsuarioCodigo").HasMaxLength(50).IsRequired();
                entity.Property(e => e.Fecha).HasColumnName("Fecha").HasDefaultValueSql("sysutcdatetime()");
                entity.Property(e => e.Comentario).HasColumnName("Comentario").HasMaxLength(500);
                entity.Property(e => e.FechaCaducidad).HasColumnName("FechaCaducidad").HasColumnType("date");

                // Foreign key relationship
                entity.HasOne(e => e.Orden)
                    .WithMany(e => e.Lecturas)
                    .HasForeignKey(e => e.OrdenGuid)
                    .HasPrincipalKey(e => e.GuidID)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Cascade);
            });


            // ResultadoConteo
            modelBuilder.Entity<ResultadoConteo>(entity =>
            {
                entity.ToTable("ResultadoConteo");
                entity.HasKey(e => e.GuidID);

                // Configuración explícita para evitar convenciones por defecto
                entity.Property(e => e.GuidID).HasColumnName("GuidID").HasDefaultValueSql("NEWID()");
                entity.Property(e => e.OrdenGuid).HasColumnName("OrdenGuid").IsRequired();
                entity.Property(e => e.CodigoAlmacen).HasColumnName("CodigoAlmacen").HasMaxLength(10).IsRequired();
                entity.Property(e => e.CodigoUbicacion).HasColumnName("CodigoUbicacion").HasMaxLength(30);
                entity.Property(e => e.CodigoArticulo).HasColumnName("CodigoArticulo").HasMaxLength(30);
                entity.Property(e => e.DescripcionArticulo).HasColumnName("DescripcionArticulo").HasMaxLength(300);
                entity.Property(e => e.LotePartida).HasColumnName("LotePartida").HasMaxLength(40);
                entity.Property(e => e.CantidadContada).HasColumnName("CantidadContada").HasColumnType("decimal(18,4)");
                entity.Property(e => e.CantidadStock).HasColumnName("CantidadStock").HasColumnType("decimal(18,4)");
                entity.Property(e => e.UsuarioCodigo).HasColumnName("UsuarioCodigo").HasMaxLength(50).IsRequired();
                entity.Property(e => e.Diferencia).HasColumnName("Diferencia").HasColumnType("decimal(18,4)").IsRequired();
                entity.Property(e => e.AccionFinal).HasColumnName("AccionFinal").HasMaxLength(20).IsRequired();
                entity.Property(e => e.AprobadoPorCodigo).HasColumnName("AprobadoPorCodigo").HasMaxLength(50);
                entity.Property(e => e.FechaEvaluacion).HasColumnName("FechaEvaluacion").HasDefaultValueSql("sysutcdatetime()");
                entity.Property(e => e.AjusteAplicado).HasColumnName("AjusteAplicado").HasDefaultValue(false);
                entity.Property(e => e.FechaCaducidad).HasColumnName("FechaCaducidad").HasColumnType("date");

                // ✅ Relación única y explícita usando SOLO OrdenGuid
                entity.HasOne(r => r.Orden)
                      .WithMany(o => o.Resultados)
                      .HasForeignKey(r => r.OrdenGuid)
                      .HasPrincipalKey(o => o.GuidID)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // --- Configuraciones de Notificaciones ---
            
            // Configuración para Notificacion
            modelBuilder.Entity<Notificacion>(entity =>
            {
                entity.ToTable("Notificaciones");
                entity.HasKey(e => e.IdNotificacion);
                
                entity.Property(e => e.IdNotificacion).HasColumnName("IdNotificacion").HasDefaultValueSql("NEWID()");
                entity.Property(e => e.CodigoEmpresa).HasColumnName("CodigoEmpresa").HasDefaultValue(1);
                entity.Property(e => e.TipoNotificacion).HasColumnName("TipoNotificacion").HasMaxLength(20).IsRequired();
                entity.Property(e => e.ProcesoId).HasColumnName("ProcesoId");
                entity.Property(e => e.Titulo).HasColumnName("Titulo").HasMaxLength(200).IsRequired();
                entity.Property(e => e.Mensaje).HasColumnName("Mensaje").HasMaxLength(500).IsRequired();
                entity.Property(e => e.EstadoAnterior).HasColumnName("EstadoAnterior").HasMaxLength(20);
                entity.Property(e => e.EstadoActual).HasColumnName("EstadoActual").HasMaxLength(20);
                entity.Property(e => e.FechaCreacion).HasColumnName("FechaCreacion").HasDefaultValueSql("sysdatetime()");
                entity.Property(e => e.EsActiva).HasColumnName("EsActiva").HasDefaultValue(true);
                entity.Property(e => e.EsGrupal).HasColumnName("EsGrupal").HasDefaultValue(false);
                entity.Property(e => e.GrupoDestino).HasColumnName("GrupoDestino").HasMaxLength(50);
                entity.Property(e => e.Comentario).HasColumnName("Comentario").HasMaxLength(500);

                // Relaciones
                entity.HasMany(e => e.Destinatarios)
                      .WithOne(d => d.Notificacion)
                      .HasForeignKey(d => d.IdNotificacion)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.Lecturas)
                      .WithOne(l => l.Notificacion)
                      .HasForeignKey(l => l.IdNotificacion)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configuración para NotificacionDestinatario
            modelBuilder.Entity<NotificacionDestinatario>(entity =>
            {
                entity.ToTable("NotificacionesDestinatarios");
                entity.HasKey(e => e.IdDestinatario);
                
                entity.Property(e => e.IdDestinatario).HasColumnName("IdDestinatario").HasDefaultValueSql("NEWID()");
                entity.Property(e => e.IdNotificacion).HasColumnName("IdNotificacion").IsRequired();
                entity.Property(e => e.UsuarioId).HasColumnName("UsuarioId").IsRequired();
                entity.Property(e => e.FechaCreacion).HasColumnName("FechaCreacion").HasDefaultValueSql("sysdatetime()");
                entity.Property(e => e.EsActiva).HasColumnName("EsActiva").HasDefaultValue(true);

                // Relación con Usuario (asumiendo que existe la tabla usuarios)
                entity.HasOne(d => d.Usuario)
                      .WithMany()
                      .HasForeignKey(d => d.UsuarioId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configuración para NotificacionLectura
            modelBuilder.Entity<NotificacionLectura>(entity =>
            {
                entity.ToTable("NotificacionesLecturas");
                entity.HasKey(e => e.IdLectura);
                
                entity.Property(e => e.IdLectura).HasColumnName("IdLectura").HasDefaultValueSql("NEWID()");
                entity.Property(e => e.IdNotificacion).HasColumnName("IdNotificacion").IsRequired();
                entity.Property(e => e.UsuarioId).HasColumnName("UsuarioId").IsRequired();
                entity.Property(e => e.FechaLeida).HasColumnName("FechaLeida").HasDefaultValueSql("sysdatetime()");

                // Relación con Usuario
                entity.HasOne(l => l.Usuario)
                      .WithMany()
                      .HasForeignKey(l => l.UsuarioId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Índice único para evitar lecturas duplicadas
                entity.HasIndex(e => new { e.IdNotificacion, e.UsuarioId })
                      .IsUnique()
                      .HasDatabaseName("IX_NotificacionesLecturas_Notificacion_Usuario");
            });

            // Configuración para BloqueoCalidad
            modelBuilder.Entity<BloqueoCalidad>(entity =>
            {
                entity.ToTable("BloqueosCalidad");
                entity.HasKey(e => e.Id);
                
                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.CodigoEmpresa).HasColumnName("CodigoEmpresa");
                entity.Property(e => e.CodigoArticulo).HasColumnName("CodigoArticulo").HasMaxLength(30);
                entity.Property(e => e.LotePartida).HasColumnName("LotePartida").HasMaxLength(50);
                entity.Property(e => e.CodigoAlmacen).HasColumnName("CodigoAlmacen").HasMaxLength(10);
                entity.Property(e => e.Ubicacion).HasColumnName("Ubicacion").HasMaxLength(30);
                entity.Property(e => e.Bloqueado).HasColumnName("Bloqueado");
                entity.Property(e => e.UsuarioBloqueoId).HasColumnName("UsuarioBloqueoId");
                entity.Property(e => e.FechaBloqueo).HasColumnName("FechaBloqueo");
                entity.Property(e => e.ComentarioBloqueo).HasColumnName("ComentarioBloqueo").HasMaxLength(500);
                entity.Property(e => e.UsuarioDesbloqueoId).HasColumnName("UsuarioDesbloqueoId");
                entity.Property(e => e.FechaDesbloqueo).HasColumnName("FechaDesbloqueo");
                entity.Property(e => e.ComentarioDesbloqueo).HasColumnName("ComentarioDesbloqueo").HasMaxLength(500);
                entity.Property(e => e.FechaCreacion).HasColumnName("FechaCreacion");
                entity.Property(e => e.FechaModificacion).HasColumnName("FechaModificacion");
            });

        }
    }
}