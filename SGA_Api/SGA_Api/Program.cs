using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using SGA_Api.Data;
using SGA_Api.Hubs;
using SGA_Api.Logic;
using SGA_Api.Middleware;
using SGA_Api.Services;
using System.IO;


var builder = WebApplication.CreateBuilder(args);

// Configurar logging para mostrar en consola
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Debug); // Cambiar a Debug para ver TODO

// Agregamos el DbContext de SAGE
builder.Services.AddDbContext<SageDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Sage"))
           .LogTo(Console.WriteLine, LogLevel.Information)); // Cambiar a Information para ver más

// Agregamos el DbContext de AURORA_SGA
builder.Services.AddDbContext<AuroraSgaDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("AuroraSga"))
           .LogTo(Console.WriteLine, LogLevel.Information)); // Cambiar a Information para ver más

// Agregamos el DbContext de StorageControl
builder.Services.AddDbContext<StorageControlDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("StorageControl"))
           .LogTo(Console.WriteLine, LogLevel.Information)); // Cambiar a Information para ver más

// Agregamos el DbContext de MobilityWH3
builder.Services.AddDbContext<MobilityWH3DbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MobilityWH3"))
           .LogTo(Console.WriteLine, LogLevel.Information)); // Cambiar a Information para ver más


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
        provider.GetRequiredService<ILogger<ConteosService>>()
    ));
builder.Services.AddScoped<IOrdenTraspasoService, OrdenTraspasoService>();
builder.Services.AddScoped<INotificacionesTraspasosService, NotificacionesTraspasosService>();
builder.Services.AddScoped<INotificacionesConteosService, NotificacionesConteosService>();
builder.Services.AddScoped<INotificacionesService, NotificacionesService>();
builder.Services.AddScoped<IRolesSgaService, RolesSgaService>();
builder.Services.AddScoped<ICalidadService, CalidadService>();
//builder.Services.AddHostedService<SGA_Api.Services.TraspasoFinalizacionBackgroundService>();
//builder.Services.AddHostedService<SGA_Api.Services.ConteosAjustesBackgroundService>();

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

app.Run();
