using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using SGA_Api.Data;
using SGA_Api.Logic;
using SGA_Api.Middleware;
using SGA_Api.Services;
using System.IO;
using SGA_Api.Middleware;


var builder = WebApplication.CreateBuilder(args);

// Agregamos el DbContext de SAGE
builder.Services.AddDbContext<SageDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Sage")));

// Agregamos el DbContext de AURORA_SGA
builder.Services.AddDbContext<AuroraSgaDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("AuroraSga")));

// Agregamos el DbContext de StorageControl
builder.Services.AddDbContext<StorageControlDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("StorageControl")));

// Agregamos el DbContext de MobilityWH3
builder.Services.AddDbContext<MobilityWH3DbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MobilityWH3")));


// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IPesajeService, PesajeLogic>();
builder.Services.AddHostedService<SGA_Api.Services.TraspasoFinalizacionBackgroundService>();

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
//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger();
    app.UseSwaggerUI();
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

app.Run();
