using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using SGA_Api.Data;
using SGA_Api.Hubs;
using SGA_Api.Logic;
using SGA_Api.Middleware;
using SGA_Api.Services;
using System.IO;
using System.Runtime.ExceptionServices;


var builder = WebApplication.CreateBuilder(args);

// Configurar logging para mostrar en consola
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information); // Information para logs normales, Debug muestra demasiado detalle

// Filtrar mensajes de debug de Entity Framework Core
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.ChangeTracking", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Connection", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Infrastructure", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Update", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Query", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Model.Validation", LogLevel.Warning);

// Agregar filtro para suprimir ObjectDisposedException del contenedor de DI durante el shutdown
builder.Logging.AddFilter((category, logLevel) =>
{
    // Si el mensaje contiene ObjectDisposedException y DependencyInjection, no lo mostramos
    // Esto se aplicará a los logs, pero Visual Studio seguirá mostrando FirstChanceException
    return true; // Permitimos todos los logs, el filtro real está en los manejadores de excepciones
});

// Agregamos el DbContext de SAGE
builder.Services.AddDbContext<SageDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Sage"))
           .LogTo(Console.WriteLine, LogLevel.Warning)); // Warning para solo ver errores y warnings de EF

// Agregamos el DbContext de AURORA_SGA
builder.Services.AddDbContext<AuroraSgaDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("AuroraSga"))
           .LogTo(Console.WriteLine, LogLevel.Warning)); // Warning para solo ver errores y warnings de EF

// Agregamos el DbContext de StorageControl
builder.Services.AddDbContext<StorageControlDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("StorageControl"))
           .LogTo(Console.WriteLine, LogLevel.Warning)); // Warning para solo ver errores y warnings de EF

// Agregamos el DbContext de MobilityWH3
builder.Services.AddDbContext<MobilityWH3DbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MobilityWH3"))
           .LogTo(Console.WriteLine, LogLevel.Warning)); // Warning para solo ver errores y warnings de EF


// Add services to the container.

builder.Services.AddControllers()
    .AddApplicationPart(typeof(Program).Assembly)
    .AddControllersAsServices()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// Configurar rutas case-insensitive
builder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = true;
    options.LowercaseQueryStrings = true;
});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "SGA API",
        Version = "v1",
        Description = "API para el Sistema de Gestión de Almacén"
    });
    
    // Configurar para manejar referencias circulares y problemas de serialización
    c.UseInlineDefinitionsForEnums();
    c.SupportNonNullableReferenceTypes();
    c.CustomSchemaIds(type => type.FullName?.Replace("+", "."));
    
    // Ignorar propiedades problemáticas
    c.IgnoreObsoleteActions();
    c.IgnoreObsoleteProperties();
    
    // Configurar para manejar referencias circulares sin filtro personalizado
    
    // Configurar para evitar problemas de serialización
    c.DocInclusionPredicate((docName, apiDesc) => true);
    c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());
});
builder.Services.AddScoped<IPesajeService, PesajeLogic>();
builder.Services.AddScoped<IConteosService>(provider => 
    new ConteosService(
        provider.GetRequiredService<AuroraSgaDbContext>(),
        provider.GetRequiredService<SageDbContext>(),
        provider.GetRequiredService<StorageControlDbContext>(),
        provider.GetRequiredService<ILogger<ConteosService>>(),
        provider.GetRequiredService<INotificacionesConteosService>()
    ));
builder.Services.AddScoped<IOrdenTraspasoService, OrdenTraspasoService>();
builder.Services.AddScoped<INotificacionesTraspasosService, NotificacionesTraspasosService>();
builder.Services.AddScoped<INotificacionesConteosService, NotificacionesConteosService>();
builder.Services.AddScoped<INotificacionesOrdenTraspasoService, NotificacionesOrdenTraspasoService>();
builder.Services.AddScoped<INotificacionesService, NotificacionesService>();
builder.Services.AddScoped<INotificacionesUnificadasService, NotificacionesUnificadasService>();
builder.Services.AddScoped<INotificacionesTeamsService, NotificacionesTeamsService>();
builder.Services.AddScoped<IRolesSgaService, RolesSgaService>();
builder.Services.AddScoped<ICalidadService, CalidadService>();
builder.Services.AddScoped<IValidacionTraspasoService, ValidacionTraspasoService>();
builder.Services.AddScoped<IValidacionAlergenosPaletService, ValidacionAlergenosPaletService>();
builder.Services.AddScoped<RendimientosService>(provider => 
    new RendimientosService(
        provider.GetRequiredService<AuroraSgaDbContext>(),
        provider.GetRequiredService<SageDbContext>(),
        provider.GetRequiredService<ILogger<RendimientosService>>()
    ));
//builder.Services.AddHostedService<SGA_Api.Services.TraspasoFinalizacionBackgroundService>();
//builder.Services.AddHostedService<NotificacionesTeamsBackgroundService>();
//builder.Services.AddHostedService<ConteosPeriodicosBackgroundService>();

// Configuración de SignalR
builder.Services.AddSignalR();

// CORS aqu
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod(); 
    });
});

// Configurar el host para que espere a que los servicios se detengan correctamente
builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(10); // Dar tiempo para que los servicios se detengan
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseRouting();

//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "SGA API v1");
        c.RoutePrefix = "swagger";
    });
//}
app.UseStaticFiles(); // Para wwwroot (si lo usas, opcional)

// A�adir esta configuraci�n personalizada para /actualizaciones
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(@"C:\wamp64\www\SGA_Api\actualizaciones"),
    RequestPath = "/actualizaciones"
});
app.UseMiddleware<TokenValidationMiddleware>();
app.UseAuthorization();

app.MapControllers();

// Mapeo del Hub de SignalR para notificaciones de traspasos
app.MapHub<NotificacionesTraspasosHub>("/notificacionesTraspasosHub");

// Log de inicio para confirmar que los logs funcionan
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("🚀 SGA API iniciada correctamente - Logs funcionando!");

// Variable para rastrear si la aplicación se está cerrando
var isShuttingDown = false;

// Configurar shutdown graceful
var hostApplicationLifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
hostApplicationLifetime.ApplicationStopping.Register(() =>
{
    isShuttingDown = true;
    logger.LogInformation("🛑 Iniciando cierre ordenado de la aplicación...");
});

// Manejar FirstChanceException - Visual Studio las muestra automáticamente en modo debug
// No podemos suprimirlas completamente, pero podemos registrarlas como información
AppDomain.CurrentDomain.FirstChanceException += (sender, e) =>
{
    if (isShuttingDown && e.Exception is ObjectDisposedException disposedEx)
    {
        // Verificar si es del contenedor de DI (las que queremos ignorar)
        var isFromDI = disposedEx.Source?.Contains("Microsoft.Extensions.DependencyInjection") == true ||
                       disposedEx.StackTrace?.Contains("Microsoft.Extensions.DependencyInjection") == true ||
                       disposedEx.StackTrace?.Contains("ServiceProvider") == true;
        
        if (isFromDI)
        {
            // Estas excepciones son normales durante el shutdown
            // Visual Studio las mostrará, pero no afectan la funcionalidad
            // No hacemos nada aquí, solo las identificamos
        }
    }
};

// Suprimir excepciones no observadas de ObjectDisposedException durante el shutdown
TaskScheduler.UnobservedTaskException += (sender, e) =>
{
    if (isShuttingDown)
    {
        var aggregateException = e.Exception;
        if (aggregateException != null)
        {
            // Verificar si alguna de las excepciones internas es ObjectDisposedException
            var hasObjectDisposed = aggregateException.InnerExceptions
                .Any(ex => ex is ObjectDisposedException disposedEx &&
                          (disposedEx.Source?.Contains("Microsoft.Extensions.DependencyInjection") == true ||
                           disposedEx.StackTrace?.Contains("Microsoft.Extensions.DependencyInjection") == true ||
                           disposedEx.StackTrace?.Contains("ServiceProvider") == true));
            
            if (hasObjectDisposed)
            {
                e.SetObserved(); // Marcar como observada para evitar que se propague
                return;
            }
        }
    }
    
    // También verificar InnerException
    if (e.Exception?.InnerException is ObjectDisposedException disposedInner)
    {
        var isFromDI = disposedInner.Source?.Contains("Microsoft.Extensions.DependencyInjection") == true ||
                       disposedInner.StackTrace?.Contains("Microsoft.Extensions.DependencyInjection") == true ||
                       disposedInner.StackTrace?.Contains("ServiceProvider") == true;
        
        if (isFromDI)
        {
            e.SetObserved();
        }
    }
};

// Manejar excepciones no manejadas - aunque ObjectDisposedException normalmente no llega aquí
AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
{
    if (isShuttingDown && e.ExceptionObject is ObjectDisposedException)
    {
        // Durante el shutdown, estas excepciones son esperadas
        // No hacer nada, solo evitar que se propague más
    }
};

// Ejecutar la aplicación de forma asíncrona para permitir shutdown graceful
try
{
    await app.RunAsync();
}
catch (ObjectDisposedException)
{
    // Ignorar ObjectDisposedException durante el cierre - es normal
    logger.LogInformation("✅ Aplicación cerrada correctamente");
}
catch (Exception ex)
{
    // Solo registrar errores que no sean ObjectDisposedException
    if (ex is not ObjectDisposedException && 
        (ex.InnerException == null || ex.InnerException is not ObjectDisposedException))
    {
        logger.LogError(ex, "❌ Error al cerrar la aplicación");
        throw;
    }
    logger.LogInformation("✅ Aplicación cerrada correctamente");
}
