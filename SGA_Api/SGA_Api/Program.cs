using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using SGA_Api.Data;
using SGA_Api.Logic;
using SGA_Api.Middleware;
using SGA_Api.Services;
using System.IO;
using SGA_Api.Middleware;
using SGA_Api.JsonConverter;


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

//JSON CONVERTER
builder.Services
	   .AddControllers()
	   .AddJsonOptions(opts =>
		   opts.JsonSerializerOptions.Converters.Add(new ShortJsonConverter())
	   );

// CORS aquí
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

if (app.Environment.IsDevelopment())
{
	app.UseDeveloperExceptionPage();
	app.UseSwagger();
	app.UseSwaggerUI();
}

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
//app.UseSwagger();
//    app.UseSwaggerUI();
////}
app.UseStaticFiles(); // Para wwwroot (si lo usas, opcional)

// Añadir esta configuración personalizada para /actualizaciones
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(@"C:\wamp64\www\SGA_Api\actualizaciones"),
    RequestPath = "/actualizaciones"
});
app.UseMiddleware<TokenValidationMiddleware>();

app.UseExceptionHandler(errApp =>
{
	errApp.Run(async context =>
	{
		// Siempre JSON
		context.Response.ContentType = "application/json";

		// Captura la excepción
		var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
		var ex = feature?.Error;

		// Código HTTP 500
		context.Response.StatusCode = StatusCodes.Status500InternalServerError;

		// Construye un payload JSON con el detalle
		var response = new
		{
			Message = ex?.Message,
			Exception = app.Environment.IsDevelopment() ? ex?.ToString() : null
		};

		await context.Response.WriteAsJsonAsync(response);
	});
});

app.UseAuthorization();

app.MapControllers();

app.Run();
